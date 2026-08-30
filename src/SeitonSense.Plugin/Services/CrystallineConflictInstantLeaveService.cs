using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.DutyState;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using SeitonSense.Core;
using SeitonSense.Plugin.Models;

namespace SeitonSense.Plugin.Services;

internal sealed class CrystallineConflictInstantLeaveService : IDisposable
{
    private readonly PluginConfiguration configuration;
    private readonly IClientState clientState;
    private readonly IPlayerState playerState;
    private readonly IFramework framework;
    private readonly ICondition condition;
    private readonly IDutyState dutyState;
    private readonly CrystallineConflictMapStatisticsService resultCapture;
    private readonly IPluginLog log;
    private CrystallineConflictInstantLeaveState state =
        CrystallineConflictInstantLeaveState.Idle;
    private volatile CrystallineConflictInstantLeaveDiagnostics diagnostics;
    private long confirmedResultCount;
    private long nativeRequestCount;
    private long confirmedExitCount;
    private long lifecycleResetCount;
    private long duplicateResultCount;
    private long cancellationCount;
    private long nativeFaultCount;
    private string lastEvent = "Idle; no confirmed CC result observed.";
    private bool disposed;

    internal CrystallineConflictInstantLeaveService(
        PluginConfiguration configuration,
        IClientState clientState,
        IPlayerState playerState,
        IFramework framework,
        ICondition condition,
        IDutyState dutyState,
        CrystallineConflictMapStatisticsService resultCapture,
        IPluginLog log)
    {
        this.configuration = configuration;
        this.clientState = clientState;
        this.playerState = playerState;
        this.framework = framework;
        this.condition = condition;
        this.dutyState = dutyState;
        this.resultCapture = resultCapture;
        this.log = log;
        diagnostics = CreateDiagnostics(Environment.TickCount64);
        resultCapture.ConfirmedResult += OnConfirmedResult;
        clientState.TerritoryChanged += OnTerritoryChanged;
        dutyState.DutyStarted += OnDutyStarted;
        framework.Update += OnFrameworkUpdate;
    }

    internal CrystallineConflictInstantLeaveDiagnostics Diagnostics => diagnostics;

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        resultCapture.ConfirmedResult -= OnConfirmedResult;
        clientState.TerritoryChanged -= OnTerritoryChanged;
        dutyState.DutyStarted -= OnDutyStarted;
        framework.Update -= OnFrameworkUpdate;
        state = CrystallineConflictInstantLeaveState.Idle;
        lastEvent = "Disposed.";
        diagnostics = CreateDiagnostics(Environment.TickCount64);
    }

    private void OnConfirmedResult(ConfirmedCrystallineConflictResultBoundary result)
    {
        if (disposed) return;

        var transition = CrystallineConflictInstantLeaveRules.ObserveConfirmedResult(
            state,
            IsEnabled,
            exactResultConfirmed: true,
            result.IsPvpExcludingWolvesDen,
            result.TerritoryId,
            result.LocalContentId,
            result.CapturedAtMilliseconds,
            Environment.TickCount64);
        state = transition.State;
        if (transition.Decision == CrystallineConflictInstantLeaveDecision.Armed)
        {
            confirmedResultCount++;
            lastEvent = "Confirmed public CC result armed one normal leave request.";
            log.Information("Instant CC leave armed from one confirmed public match result.");
        }
        else if (transition.Decision == CrystallineConflictInstantLeaveDecision.DuplicateIgnored)
        {
            duplicateResultCount++;
            lastEvent = "Duplicate result ignored; this match context is already spent.";
            log.Information(
                "Instant CC leave ignored a confirmed result because the previous match context was still spent.");
        }

        diagnostics = CreateDiagnostics(Environment.TickCount64);
    }

    private void OnTerritoryChanged(uint territoryId)
    {
        if (disposed) return;

        ApplyLifecycleTransition(
            CrystallineConflictInstantLeaveRules.ObserveTerritoryChanged(state, territoryId),
            "territory change");
    }

    private void OnDutyStarted(IDutyStateEventArgs _)
    {
        if (disposed) return;

        ApplyLifecycleTransition(
            CrystallineConflictInstantLeaveRules.ObserveDutyStarted(
                state,
                clientState.IsPvPExcludingDen,
                clientState.TerritoryType,
                playerState.ContentId),
            "public CC duty start");
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        if (disposed) return;

        var now = Environment.TickCount64;
        var enabled = IsEnabled;
        var betweenAreas =
            condition[ConditionFlag.BetweenAreas] ||
            condition[ConditionFlag.BetweenAreas51];
        var liveIsPvp = clientState.IsPvPExcludingDen;
        var liveTerritory = clientState.TerritoryType;
        var liveContentId = playerState.ContentId;
        var exactLiveContext =
            liveIsPvp &&
            PvPMatchRules.IsPublicCrystallineConflictTerritory(liveTerritory) &&
            liveTerritory == state.TerritoryId &&
            liveContentId != 0 &&
            liveContentId == state.LocalContentId;

        var nativeBoundaryAvailable = true;
        var canLeaveCurrentContent = false;
        if (state.Phase == CrystallineConflictInstantLeavePhase.WaitingForNativeBoundary &&
            enabled &&
            exactLiveContext &&
            !betweenAreas &&
            now <= state.ExpiresAtMilliseconds)
        {
            try
            {
                canLeaveCurrentContent = EventFramework.CanLeaveCurrentContent();
            }
            catch (Exception exception)
            {
                nativeBoundaryAvailable = false;
                log.Warning(
                    exception,
                    "Instant CC leave could not query the native leave boundary and was cancelled.");
            }
        }

        var transition = CrystallineConflictInstantLeaveRules.Evaluate(
            state,
            enabled,
            liveIsPvp,
            liveTerritory,
            liveContentId,
            betweenAreas,
            nativeBoundaryAvailable,
            canLeaveCurrentContent,
            now);
        state = transition.State;

        switch (transition.Decision)
        {
            case CrystallineConflictInstantLeaveDecision.RequestLeave:
                // The pure policy reserves the one-shot before this void call.
                diagnostics = CreateDiagnostics(now);
                try
                {
                    EventFramework.LeaveCurrentContent(false);
                    nativeRequestCount++;
                    lastEvent = "Normal leave requested once; waiting for the zone transition.";
                    log.Information("Instant CC leave sent one normal non-forced native leave request.");
                }
                catch (Exception exception)
                {
                    nativeFaultCount++;
                    state = CrystallineConflictInstantLeaveRules.MarkNativeCallFailed(state);
                    lastEvent = "Native leave request faulted; no retry will be issued.";
                    log.Warning(exception, "Instant CC leave request faulted and will not retry.");
                }

                break;
            case CrystallineConflictInstantLeaveDecision.Cancelled:
                cancellationCount++;
                lastEvent = $"Cancelled safely: {transition.Reason}.";
                log.Information(
                    "Instant CC leave cancelled safely at {Reason}.",
                    transition.Reason);
                break;
            case CrystallineConflictInstantLeaveDecision.ExitConfirmed:
                confirmedExitCount++;
                lastEvent = "CC context exited after the leave request.";
                log.Information("Instant CC leave observed the requested match context exit.");
                break;
            case CrystallineConflictInstantLeaveDecision.ContextReset:
                lifecycleResetCount++;
                lastEvent = "Previous CC context cleared; ready for a later match.";
                log.Information("Instant CC leave cleared the previous match context.");
                break;
        }

        diagnostics = CreateDiagnostics(now);
    }

    private void ApplyLifecycleTransition(
        CrystallineConflictInstantLeaveTransition transition,
        string source)
    {
        state = transition.State;
        switch (transition.Decision)
        {
            case CrystallineConflictInstantLeaveDecision.ExitConfirmed:
                confirmedExitCount++;
                lastEvent = $"CC context exited and rearmed by {source}.";
                log.Information(
                    "Instant CC leave rearmed after the requested match exit ({Source}).",
                    source);
                break;
            case CrystallineConflictInstantLeaveDecision.ContextReset:
                lifecycleResetCount++;
                lastEvent = $"Previous CC context cleared by {source}.";
                log.Information(
                    "Instant CC leave cleared a spent match context ({Source}).",
                    source);
                break;
            default:
                return;
        }

        diagnostics = CreateDiagnostics(Environment.TickCount64);
    }

    private bool IsEnabled =>
        configuration.Enabled &&
        configuration.EnableInstantLeaveAfterCrystallineConflict;

    private CrystallineConflictInstantLeaveDiagnostics CreateDiagnostics(long nowMilliseconds) =>
        new(
            IsEnabled,
            resultCapture.CaptureAvailable,
            state.Phase,
            state.Reason,
            state.TerritoryId,
            state.Phase == CrystallineConflictInstantLeavePhase.WaitingForNativeBoundary
                ? Math.Max(0, state.ExpiresAtMilliseconds - nowMilliseconds)
                : 0,
            confirmedResultCount,
            nativeRequestCount,
            confirmedExitCount,
            lifecycleResetCount,
            duplicateResultCount,
            cancellationCount,
            nativeFaultCount,
            lastEvent);
}

internal sealed record CrystallineConflictInstantLeaveDiagnostics(
    bool Enabled,
    bool ResultHookAvailable,
    CrystallineConflictInstantLeavePhase Phase,
    CrystallineConflictInstantLeaveReason Reason,
    uint TerritoryId,
    long RemainingMilliseconds,
    long ConfirmedResultCount,
    long NativeRequestCount,
    long ConfirmedExitCount,
    long LifecycleResetCount,
    long DuplicateResultCount,
    long CancellationCount,
    long NativeFaultCount,
    string LastEvent)
{
    internal string ToChatLine() =>
        $"instant-cc-leave[enabled/hook={Enabled}/{ResultHookAvailable}, " +
        $"state={Phase}/{Reason}, territory={TerritoryId}, ttl={RemainingMilliseconds}ms, " +
        $"result/request/exit/reset/duplicate/cancel/fault={ConfirmedResultCount}/{NativeRequestCount}/" +
        $"{ConfirmedExitCount}/{LifecycleResetCount}/{DuplicateResultCount}/" +
        $"{CancellationCount}/{NativeFaultCount}, last={LastEvent}]";
}
