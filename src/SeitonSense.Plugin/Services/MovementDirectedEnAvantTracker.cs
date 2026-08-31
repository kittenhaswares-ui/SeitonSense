using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using SeitonSense.Core;
using SeitonSense.Plugin.Models;

namespace SeitonSense.Plugin.Services;

internal readonly record struct MovementDirectedEnAvantDiagnostics(
    bool Enabled,
    bool Tracking,
    bool DirectionReady,
    int ConsistentSegments,
    float ConsistentDistanceYalms,
    float HeadingRadians,
    long DirectionAgeMilliseconds,
    string LastEvent)
{
    internal string ToChatLine() =>
        $"enabled={Enabled},tracking={Tracking},ready={DirectionReady}," +
        $"segments={ConsistentSegments},distance={ConsistentDistanceYalms:0.000}," +
        $"heading={HeadingRadians:0.000},age={DirectionAgeMilliseconds},last={LastEvent}";
}

/// <summary>
/// Samples only the local DNC's recent world-space movement. This preserves
/// diagonals, remapped controls, controller input, Standard/Legacy modes, and
/// autorun without reading or guessing physical keys. Eligibility remains
/// stable while a command key is pressed; actual displacement and freshness,
/// rather than a one-frame native MOVE flag, prove that the player is moving.
/// It never invokes an
/// action; it can only expose one fresh identity-bound heading snapshot to the
/// explicit /seitonenavant command.
/// </summary>
internal sealed class MovementDirectedEnAvantTracker : IDisposable
{
    private readonly object gate = new();
    private readonly PluginConfiguration configuration;
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly IFramework framework;
    private readonly IDutyState dutyState;
    private readonly ICondition condition;

    private MovementDirectedEnAvantState state =
        MovementDirectedEnAvantState.Initial;
    private string lastEvent = "Idle; no DNC movement sampled.";
    private bool started;
    private bool disposed;

    internal MovementDirectedEnAvantTracker(
        PluginConfiguration configuration,
        IClientState clientState,
        IObjectTable objectTable,
        IFramework framework,
        IDutyState dutyState,
        ICondition condition)
    {
        this.configuration = configuration;
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.framework = framework;
        this.dutyState = dutyState;
        this.condition = condition;
    }

    internal MovementDirectedEnAvantDiagnostics Diagnostics
    {
        get
        {
            lock (gate)
            {
                var age = state.LastMovementAtMilliseconds >= 0
                    ? Math.Max(0, Environment.TickCount64 - state.LastMovementAtMilliseconds)
                    : -1;
                return new MovementDirectedEnAvantDiagnostics(
                    configuration.Enabled &&
                    configuration.EnableBackwardPanicShukuchiCommand,
                    state.LastSample.IsValid,
                    state.HasDirection &&
                    age <= MovementDirectedEnAvantRules.MaximumDirectionAgeMilliseconds,
                    state.ConsistentSegmentCount,
                    state.ConsistentDistanceYalms,
                    state.HeadingRadians,
                    age,
                    lastEvent);
            }
        }
    }

    internal void Start()
    {
        if (started || disposed) return;
        started = true;
        framework.Update += OnFrameworkUpdate;
    }

    internal MovementDirectedEnAvantSnapshot Capture()
    {
        if (!started || disposed || !TryCreateCurrentSample(out var sample))
        {
            lock (gate) lastEvent = "Command refused: current DNC movement identity unavailable.";
            return default;
        }

        lock (gate)
        {
            // The macro callback can land between framework samples. Fold in
            // the current position once so the command never depends on the
            // previous frame having observed the final movement segment.
            if (!state.LastSample.IsValid ||
                state.LastSample.Fingerprint != sample.Fingerprint ||
                sample.ObservedAtMilliseconds > state.LastSample.ObservedAtMilliseconds)
            {
                state = MovementDirectedEnAvantRules.Observe(state, sample);
            }

            if (!MovementDirectedEnAvantRules.TryCapture(
                    state,
                    sample.Fingerprint,
                    Environment.TickCount64,
                    out var snapshot))
            {
                lastEvent = "Command refused: move consistently before using /seitonenavant.";
                return default;
            }

            lastEvent = "Fresh DNC movement direction frozen for one explicit command.";
            return snapshot;
        }
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        if (disposed) return;

        try
        {
            ObserveCurrentMovement();
        }
        catch
        {
            Reset("Tracking reset: movement sampling faulted closed.");
        }
    }

    private void ObserveCurrentMovement()
    {
        if (!TryCreateCurrentSample(out var sample))
        {
            Reset("Tracking reset: DNC movement context unavailable.");
            return;
        }

        lock (gate)
        {
            state = MovementDirectedEnAvantRules.Observe(state, sample);
            lastEvent = state.HasDirection
                ? "Fresh consistent DNC movement direction is ready."
                : "Sampling DNC movement; two consistent segments are required.";
        }
    }

    private bool TryCreateCurrentSample(out MovementDirectedEnAvantSample sample)
    {
        sample = default;
        if (!TryCaptureCurrentFingerprint(out var fingerprint)) return false;

        var local = objectTable.LocalPlayer;
        if (local is null) return false;

        var position = local.Position;
        sample = new MovementDirectedEnAvantSample(
            fingerprint,
            position.X,
            position.Z,
            Environment.TickCount64);
        return sample.IsValid;
    }

    private bool TryCaptureCurrentFingerprint(
        out MovementDirectedEnAvantFingerprint fingerprint)
    {
        fingerprint = default;
        if (!configuration.Enabled ||
            !configuration.EnableBackwardPanicShukuchiCommand ||
            condition[ConditionFlag.BetweenAreas] ||
            condition[ConditionFlag.BetweenAreas51] ||
            condition[ConditionFlag.BeingMoved] ||
            condition[ConditionFlag.Mounted] ||
            !PanicShukuchiRules.IsSupportedContext(
                ResolveContext(),
                configuration.EnableWolvesDenTesting))
        {
            return false;
        }

        var local = objectTable.LocalPlayer;
        if (!HasValidDancerIdentity(local)) return false;

        fingerprint = new MovementDirectedEnAvantFingerprint(
            clientState.TerritoryType,
            (ulong)(nuint)local!.Address,
            local.GameObjectId,
            local.EntityId,
            local.ClassJob.RowId);
        return fingerprint.IsValid;
    }

    private SupportedPvPContext ResolveContext()
    {
        var content = dutyState.ContentFinderCondition;
        return PvPMatchRules.ResolveSupportedContext(
            clientState.IsPvP,
            clientState.IsPvPExcludingDen,
            configuration.EnableWolvesDenTesting,
            clientState.TerritoryType,
            content.IsValid,
            content.IsValid && content.Value.PvP,
            content.IsValid ? content.Value.ContentUICategory.RowId : 0,
            content.IsValid && content.Value.CrystallineConflictCasualRoulette,
            content.IsValid && content.Value.CrystallineConflictRankedRoulette);
    }

    private static bool HasValidDancerIdentity(IPlayerCharacter? local) =>
        local is not null &&
        local.Address != nint.Zero &&
        local.IsValid() &&
        !local.IsDead &&
        local.IsTargetable &&
        local.CurrentHp > 0 &&
        local.MaxHp >= local.CurrentHp &&
        local.ClassJob.IsValid &&
        local.ClassJob.RowId == BackwardDashRules.DancerJobId &&
        local.GameObjectId is not 0 and not 0xE0000000UL and not ulong.MaxValue &&
        local.EntityId is not 0 and not 0xE0000000U and not uint.MaxValue;

    private void Reset(string reason)
    {
        lock (gate)
        {
            state = MovementDirectedEnAvantState.Initial;
            lastEvent = reason;
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (started) framework.Update -= OnFrameworkUpdate;
        started = false;
        Reset("Disposed.");
    }
}
