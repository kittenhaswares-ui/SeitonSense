using Dalamud.Game;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Party;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Lumina.Excel.Sheets;
using SeitonSense.Core;
using SeitonSense.Plugin.Models;
using ActionSheet = Lumina.Excel.Sheets.Action;

namespace SeitonSense.Plugin.Services;

internal sealed record IsolationWarningRuntimeSnapshot(
    bool Active,
    bool Visible,
    IsolationWarningSignal Signal)
{
    internal static IsolationWarningRuntimeSnapshot Inactive { get; } =
        new(false, false, IsolationWarningSignal.Unknown);
}

internal sealed record IsolationAwarenessDiagnostics(
    bool Configured,
    bool ProbeMetadataVerified,
    bool IsCrystallineConflict,
    int PartyCount,
    int ResolvedAllyCount,
    int AliveAllyCount,
    int ConnectedAllyCount,
    int ReadyResults,
    int NotFacingResults,
    int LineOfSightFailures,
    int RangeFailures,
    int UnknownResults,
    uint LastNativeResult,
    IsolationWarningSignal Signal,
    bool Visible,
    string LastEvent)
{
    internal static IsolationAwarenessDiagnostics Inactive(bool metadataVerified) =>
        new(
            false,
            metadataVerified,
            false,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            uint.MaxValue,
            IsolationWarningSignal.Unknown,
            false,
            "Inactive");

    internal string ToChatLine() =>
        $"configured={Configured},meta={ProbeMetadataVerified},cc={IsCrystallineConflict}," +
        $"party={PartyCount},resolved={ResolvedAllyCount},alive={AliveAllyCount}," +
        $"connected={ConnectedAllyCount},native={ReadyResults}/{NotFacingResults}/" +
        $"{LineOfSightFailures}/{RangeFailures}/{UnknownResults}," +
        $"last-result={(LastNativeResult == uint.MaxValue ? "none" : LastNativeResult)}," +
        $"signal={Signal},visible={Visible},last={LastEvent}";
}

/// <summary>
/// Produces a local, read-only warning when no exact living CC party member is
/// reachable through one metadata-verified native 20-yalm range/LoS probe.
/// </summary>
internal sealed class IsolationAwarenessService : IDisposable
{
    internal const uint ProbeActionId = 29_484; // PvP MNK Thunderclap: exact 20y party target.
    private const uint ReadyResult = 0;
    private const uint LineOfSightFailureResult = 562;
    private const uint NotFacingResult = 565;
    private const uint RangeFailureResult = 566;
    private const long UpdateIntervalMilliseconds = 100;

    private readonly PluginConfiguration configuration;
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly IPartyList partyList;
    private readonly IDutyState dutyState;
    private readonly IFramework framework;
    private readonly IPluginLog log;
    private readonly bool probeMetadataVerified;
    private IsolationWarningState state = IsolationWarningState.Initial;
    private IsolationWarningRuntimeSnapshot snapshot = IsolationWarningRuntimeSnapshot.Inactive;
    private IsolationAwarenessDiagnostics diagnostics;
    private long nextUpdateAt;
    private long nextErrorLogAt;
    private uint activeTerritory;
    private ulong activeLocalGameObjectId;
    private uint activeLocalEntityId;
    private bool started;
    private bool disposed;

    internal IsolationAwarenessService(
        PluginConfiguration configuration,
        IClientState clientState,
        IObjectTable objectTable,
        IPartyList partyList,
        IDutyState dutyState,
        IFramework framework,
        IDataManager dataManager,
        IPluginLog log)
    {
        this.configuration = configuration;
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.partyList = partyList;
        this.dutyState = dutyState;
        this.framework = framework;
        this.log = log;
        probeMetadataVerified = ValidateProbeMetadata(dataManager, log);
        diagnostics = IsolationAwarenessDiagnostics.Inactive(probeMetadataVerified);
    }

    internal IsolationWarningRuntimeSnapshot Snapshot => Volatile.Read(ref snapshot);
    internal IsolationAwarenessDiagnostics Diagnostics => Volatile.Read(ref diagnostics);

    internal void Start()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (started) return;
        started = true;
        framework.Update += OnFrameworkUpdate;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (started) framework.Update -= OnFrameworkUpdate;
        ResetRuntime();
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        if (disposed || Environment.TickCount64 < nextUpdateAt) return;
        nextUpdateAt = Environment.TickCount64 + UpdateIntervalMilliseconds;

        try
        {
            UpdateSnapshot();
        }
        catch (Exception exception)
        {
            ResetRuntime();
            var now = Environment.TickCount64;
            if (now < nextErrorLogAt) return;
            nextErrorLogAt = now + 10_000;
            log.Error(exception, "Seiton Sense isolation scan failed closed.");
        }
    }

    private unsafe void UpdateSnapshot()
    {
        var now = Environment.TickCount64;
        var local = objectTable.LocalPlayer;
        var localGameObjectId = local?.GameObjectId ?? 0;
        var localEntityId = local?.EntityId ?? 0;
        if (clientState.TerritoryType != activeTerritory ||
            localGameObjectId != activeLocalGameObjectId ||
            localEntityId != activeLocalEntityId)
        {
            state = IsolationWarningState.Initial;
            activeTerritory = clientState.TerritoryType;
            activeLocalGameObjectId = localGameObjectId;
            activeLocalEntityId = localEntityId;
        }

        var configured = configuration.Enabled && configuration.WarnWhenIsolated;
        var context = ResolveContext();
        var isCc = context == SupportedPvPContext.CrystallineConflict;
        var localValid = IsExactLiveLocal(local);
        var partyIds = partyList.Select(static member => member.EntityId).ToArray();
        var partyCount = partyIds.Length;
        IPlayerCharacter[] allies = [];
        var resolvedAllyCount = 0;
        var completeParty = localValid && TryResolveExactParty(
            local!,
            partyIds,
            out allies,
            out resolvedAllyCount);

        var observations = new List<IsolationAllyObservation>(
            IsolationWarningRules.ExpectedNonSelfPartyMembers);
        var aliveCount = 0;
        var connectedCount = 0;
        var readyResults = 0;
        var notFacingResults = 0;
        var lineOfSightFailures = 0;
        var rangeFailures = 0;
        var unknownResults = 0;
        var lastNativeResult = uint.MaxValue;

        var sourceObject = localValid ? (GameObject*)local!.Address : null;
        if (completeParty && sourceObject != null && sourceObject->EntityId == local!.EntityId)
        {
            foreach (var ally in allies)
            {
                var confirmedDead = ally.IsDead || ally.CurrentHp == 0;
                if (confirmedDead)
                {
                    observations.Add(new IsolationAllyObservation(
                        true,
                        false,
                        ally.IsTargetable,
                        IsolationAllyReachability.Unavailable));
                    continue;
                }

                var validAliveTelemetry = ally.MaxHp > 0 && ally.MaxHp >= ally.CurrentHp;
                aliveCount++;
                if (!validAliveTelemetry || !ally.IsTargetable)
                {
                    unknownResults++;
                    observations.Add(new IsolationAllyObservation(
                        true,
                        true,
                        false,
                        IsolationAllyReachability.Unknown));
                    continue;
                }

                var targetObject = (GameObject*)ally.Address;
                uint result;
                if (targetObject == null || targetObject->EntityId != ally.EntityId)
                {
                    result = uint.MaxValue;
                }
                else
                {
                    result = ActionManager.GetActionInRangeOrLoS(
                        ProbeActionId,
                        sourceObject,
                        targetObject);
                }

                lastNativeResult = result;
                var reachability = result switch
                {
                    ReadyResult => IsolationAllyReachability.Connected,
                    NotFacingResult => IsolationAllyReachability.Connected,
                    LineOfSightFailureResult => IsolationAllyReachability.Disconnected,
                    RangeFailureResult => IsolationAllyReachability.Disconnected,
                    _ => IsolationAllyReachability.Unknown,
                };

                switch (result)
                {
                    case ReadyResult:
                        readyResults++;
                        break;
                    case NotFacingResult:
                        notFacingResults++;
                        break;
                    case LineOfSightFailureResult:
                        lineOfSightFailures++;
                        break;
                    case RangeFailureResult:
                        rangeFailures++;
                        break;
                    default:
                        unknownResults++;
                        break;
                }

                if (reachability == IsolationAllyReachability.Connected)
                    connectedCount++;
                observations.Add(new IsolationAllyObservation(
                    true,
                    true,
                    true,
                    reachability));
            }
        }

        var decision = IsolationWarningRules.Observe(
            state,
            new IsolationWarningObservation(
                configured && probeMetadataVerified,
                isCc,
                localValid,
                completeParty,
                now,
                observations));
        state = decision.NextState;

        var active = configured && probeMetadataVerified && isCc && localValid && completeParty;
        Interlocked.Exchange(
            ref snapshot,
            new IsolationWarningRuntimeSnapshot(active, state.IsVisible, decision.Signal));
        Volatile.Write(
            ref diagnostics,
            new IsolationAwarenessDiagnostics(
                configured,
                probeMetadataVerified,
                isCc,
                partyCount,
                resolvedAllyCount,
                aliveCount,
                connectedCount,
                readyResults,
                notFacingResults,
                lineOfSightFailures,
                rangeFailures,
                unknownResults,
                lastNativeResult,
                decision.Signal,
                state.IsVisible,
                ResolveLastEvent(
                    configured,
                    probeMetadataVerified,
                    isCc,
                    localValid,
                    completeParty,
                    decision)));
    }

    private bool TryResolveExactParty(
        IPlayerCharacter local,
        IReadOnlyList<uint> partyIds,
        out IPlayerCharacter[] allies,
        out int resolvedAllyCount)
    {
        allies = [];
        resolvedAllyCount = 0;
        if (partyIds.Count != 5 ||
            partyIds.Any(static id => !IsValidEntityId(id)) ||
            partyIds.Distinct().Count() != 5 ||
            !partyIds.Contains(local.EntityId))
        {
            return false;
        }

        var visiblePlayers = objectTable.PlayerObjects
            .OfType<IPlayerCharacter>()
            .Where(static player =>
                player.Address != 0 &&
                IsValidEntityId(player.EntityId) &&
                IsValidGameObjectId(player.GameObjectId))
            .ToArray();
        var exactByEntityId = visiblePlayers
            .GroupBy(static player => player.EntityId)
            .Where(static group => group.Count() == 1)
            .ToDictionary(static group => group.Key, static group => group.Single());
        if (!exactByEntityId.TryGetValue(local.EntityId, out var exactLocal) ||
            exactLocal.Address != local.Address ||
            exactLocal.GameObjectId != local.GameObjectId)
        {
            return false;
        }

        var result = new List<IPlayerCharacter>(4);
        var objectIds = new HashSet<ulong> { local.GameObjectId };
        foreach (var entityId in partyIds)
        {
            if (entityId == local.EntityId) continue;
            if (!exactByEntityId.TryGetValue(entityId, out var ally) ||
                !objectIds.Add(ally.GameObjectId))
            {
                resolvedAllyCount = result.Count;
                return false;
            }

            result.Add(ally);
            resolvedAllyCount++;
        }

        if (result.Count != IsolationWarningRules.ExpectedNonSelfPartyMembers)
            return false;

        allies = result.ToArray();
        return true;
    }

    private SupportedPvPContext ResolveContext()
    {
        var condition = dutyState.ContentFinderCondition;
        var valid = condition.IsValid;
        return PvPMatchRules.ResolveSupportedContext(
            clientState.IsPvP,
            clientState.IsPvPExcludingDen,
            includeWolvesDenTesting: false,
            clientState.TerritoryType,
            valid,
            valid && condition.Value.PvP,
            valid ? condition.Value.ContentUICategory.RowId : 0,
            valid && condition.Value.CrystallineConflictCasualRoulette,
            valid && condition.Value.CrystallineConflictRankedRoulette);
    }

    private void ResetRuntime()
    {
        state = IsolationWarningState.Initial;
        Interlocked.Exchange(ref snapshot, IsolationWarningRuntimeSnapshot.Inactive);
        Volatile.Write(ref diagnostics, IsolationAwarenessDiagnostics.Inactive(probeMetadataVerified));
    }

    private static bool IsExactLiveLocal(IPlayerCharacter? player) =>
        player is not null &&
        player.Address != 0 &&
        IsValidEntityId(player.EntityId) &&
        IsValidGameObjectId(player.GameObjectId) &&
        !player.IsDead &&
        player.IsTargetable &&
        player.CurrentHp > 0 &&
        player.MaxHp >= player.CurrentHp;

    private static bool IsValidEntityId(uint entityId) =>
        entityId is not 0 and not 0xE0000000 and not uint.MaxValue;

    private static bool IsValidGameObjectId(ulong gameObjectId) =>
        gameObjectId is not 0 and not 0xE0000000UL and not ulong.MaxValue;

    private static bool ValidateProbeMetadata(IDataManager dataManager, IPluginLog log)
    {
        try
        {
            var actions = dataManager.GetExcelSheet<ActionSheet>(ClientLanguage.English);
            var valid = actions.TryGetRow(ProbeActionId, out var action) &&
                        action.Name.ToString() == "Thunderclap" &&
                        action.IsPvP &&
                        action.IsPlayerAction &&
                        action.ClassJob.RowId == 20 &&
                        action.Range == 20 &&
                        action.EffectRange == 0 &&
                        action.CanTargetParty &&
                        action.CanTargetHostile &&
                        !action.CanTargetSelf &&
                        !action.TargetArea &&
                        action.RequiresLineOfSight &&
                        action.AffectsPosition;
            if (!valid)
                log.Warning("Seiton Sense isolation 20y/LoS probe metadata failed closed.");
            return valid;
        }
        catch (Exception exception)
        {
            log.Warning(exception, "Seiton Sense isolation 20y/LoS probe metadata failed closed.");
            return false;
        }
    }

    private static string ResolveLastEvent(
        bool configured,
        bool metadataVerified,
        bool isCc,
        bool localValid,
        bool completeParty,
        IsolationWarningDecision decision)
    {
        if (!configured) return "Disabled";
        if (!metadataVerified) return "ProbeMetadataInvalid";
        if (!isCc) return "OutsideCrystallineConflict";
        if (!localValid) return "LocalPlayerInvalid";
        if (!completeParty) return "PartyNotResolvedExactly";
        return decision.Signal switch
        {
            IsolationWarningSignal.Unknown => "NativeReachabilityUnknown",
            IsolationWarningSignal.Connected when decision.NextState.IsVisible => "ClearDebounce",
            IsolationWarningSignal.Connected => "Connected",
            IsolationWarningSignal.Isolated when decision.NextState.IsVisible => "Visible",
            IsolationWarningSignal.Isolated => "EntryDebounce",
            _ => "Unknown",
        };
    }
}
