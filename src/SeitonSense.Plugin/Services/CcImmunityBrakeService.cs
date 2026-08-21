using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Party;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using SeitonSense.Core;
using SeitonSense.Plugin.Models;

namespace SeitonSense.Plugin.Services;

internal readonly record struct CcImmunityBrakeDiagnostics(
    bool Configured,
    bool ActiveInCurrentContext,
    int VerifiedActions,
    int VerifiedStatuses,
    long EvaluatedAttempts,
    long BlockedAttempts,
    long FailedOpenAttempts,
    long DefaultTargetResolutions,
    long ExactTargetResolutions,
    long TargetResolutionFailures,
    uint LastActionId,
    uint LastBlockerStatusId,
    int LastEnemySlot,
    ulong LastOriginalTargetId,
    ulong LastForwardedTargetId,
    ulong LastEffectiveTargetId,
    uint LastMode,
    bool LastTargetSuppressedByRedirect,
    string LastTargetResolution,
    string LastSampledStatuses,
    string LastEvent);

/// <summary>
/// Evaluates one already incoming action attempt. It owns no queue, timer,
/// delayed work, target mutation, replay, retry, or action dispatch.
/// </summary>
internal sealed unsafe class CcImmunityBrakeService
{
    private readonly PluginConfiguration configuration;
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly IPartyList partyList;
    private readonly IDutyState dutyState;
    private readonly IPluginLog log;
    private readonly IReadOnlySet<uint> verifiedActionIds;
    private readonly IReadOnlySet<uint> verifiedStatusIds;
    private readonly object diagnosticsGate = new();

    private long evaluatedAttempts;
    private long blockedAttempts;
    private long failedOpenAttempts;
    private long defaultTargetResolutions;
    private long exactTargetResolutions;
    private long targetResolutionFailures;
    private uint lastActionId;
    private uint lastBlockerStatusId;
    private int lastEnemySlot;
    private ulong lastOriginalTargetId;
    private ulong lastForwardedTargetId;
    private ulong lastEffectiveTargetId;
    private uint lastMode;
    private bool lastTargetSuppressedByRedirect;
    private string lastTargetResolution = "Not evaluated";
    private string lastSampledStatuses = "none";
    private string lastEvent = "Ready";
    private long nextErrorLogAt;

    internal CcImmunityBrakeService(
        PluginConfiguration configuration,
        IClientState clientState,
        IObjectTable objectTable,
        IPartyList partyList,
        IDutyState dutyState,
        IDataManager dataManager,
        IPluginLog log)
    {
        this.configuration = configuration;
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.partyList = partyList;
        this.dutyState = dutyState;
        this.log = log;
        var metadata = CcImmunityBrakeMetadataGuard.Validate(dataManager, log);
        verifiedActionIds = metadata.VerifiedActionIds;
        verifiedStatusIds = metadata.VerifiedStatusIds;
    }

    internal CcImmunityBrakeDiagnostics Diagnostics
    {
        get
        {
            lock (diagnosticsGate)
            {
                return new CcImmunityBrakeDiagnostics(
                    configuration.Enabled && configuration.EnableCcImmunityBrake,
                    configuration.Enabled &&
                    configuration.EnableCcImmunityBrake &&
                    ResolveContext() == SupportedPvPContext.CrystallineConflict,
                    verifiedActionIds.Count,
                    verifiedStatusIds.Count,
                    evaluatedAttempts,
                    blockedAttempts,
                    failedOpenAttempts,
                    defaultTargetResolutions,
                    exactTargetResolutions,
                    targetResolutionFailures,
                    lastActionId,
                    lastBlockerStatusId,
                    lastEnemySlot,
                    lastOriginalTargetId,
                    lastForwardedTargetId,
                    lastEffectiveTargetId,
                    lastMode,
                    lastTargetSuppressedByRedirect,
                    lastTargetResolution,
                    lastSampledStatuses,
                    lastEvent);
            }
        }
    }

    internal IReadOnlySet<uint> VerifiedStatusIds => verifiedStatusIds;
    internal IReadOnlySet<uint> VerifiedActionIds => verifiedActionIds;

    internal bool ShouldBlock(
        ActionType actionType,
        uint resolvedActionId,
        ulong originalTargetId,
        ulong forwardedTargetId,
        bool targetSuppressedByRedirect,
        ActionManager.UseActionMode mode)
    {
        if (!IsRecognizedInvocation(actionType, mode) ||
            !configuration.Enabled ||
            !configuration.EnableCcImmunityBrake ||
            !verifiedActionIds.Contains(resolvedActionId) ||
            ResolveContext() != SupportedPvPContext.CrystallineConflict)
        {
            return false;
        }

        Interlocked.Increment(ref evaluatedAttempts);
        var localPlayer = objectTable.LocalPlayer;
        var localJobId = IsLivePlayer(localPlayer) && HasValidNativeIdentity(localPlayer!) &&
                         localPlayer!.ClassJob.IsValid
            ? localPlayer.ClassJob.RowId
            : 0;
        var defaultTargetCarrier = CcImmunityBrakeTargetRules.IsDefaultTargetCarrier(forwardedTargetId) &&
                                   forwardedTargetId == originalTargetId &&
                                   !targetSuppressedByRedirect;
        var nativeHardTargetId = defaultTargetCarrier
            ? GetNativeHardTargetId(localPlayer)
            : 0;
        var effectiveTargetId = CcImmunityBrakeTargetRules.ResolveEffectiveTargetId(
            originalTargetId,
            forwardedTargetId,
            nativeHardTargetId,
            targetSuppressedByRedirect);
        var exactTarget = TryResolveExactCanonicalEnemy(
            localPlayer,
            effectiveTargetId,
            out var target,
            out var enemySlot,
            out var targetResolution);
        var targetIdentity = target is null
            ? default
            : new TargetPressureActorIdentity(target.GameObjectId, target.EntityId);
        var targetJobId = target?.ClassJob.IsValid == true ? target.ClassJob.RowId : 0;
        var liveStatuses = target?.StatusList
            .Select(static status => status.StatusId)
            .Where(verifiedStatusIds.Contains)
            .ToArray();
        var hardTargetStable = !defaultTargetCarrier ||
                               GetNativeHardTargetId(localPlayer) == nativeHardTargetId;
        if (!hardTargetStable)
        {
            exactTarget = false;
            targetResolution = "Native hard target changed during evaluation";
        }
        var decision = CcImmunityBrakeRules.Evaluate(
            masterEnabled: true,
            configuration.IsCcBrakeJobEnabled(localJobId),
            configuration.IsCcBrakeActionEnabled(resolvedActionId),
            localJobId,
            resolvedActionId,
            effectiveTargetId,
            targetIdentity,
            targetJobId,
            exactTarget,
            liveStatuses);
        RecordDecision(
            decision,
            resolvedActionId,
            enemySlot,
            originalTargetId,
            forwardedTargetId,
            effectiveTargetId,
            mode,
            defaultTargetCarrier,
            targetSuppressedByRedirect,
            targetResolution,
            liveStatuses);
        return decision.ShouldBlock;
    }

    internal void RecordFailedOpen(Exception exception)
    {
        Interlocked.Increment(ref failedOpenAttempts);
        lock (diagnosticsGate) lastEvent = "Runtime exception; passed unchanged";

        var now = Environment.TickCount64;
        lock (diagnosticsGate)
        {
            if (now < nextErrorLogAt) return;
            nextErrorLogAt = now + 10_000;
        }

        try
        {
            log.Error(exception, "Seiton Sense CC-immunity brake failed open; action passed unchanged.");
        }
        catch
        {
            // Diagnostics must never alter the incoming action path.
        }
    }

    private void RecordDecision(
        CcImmunityBrakeDecision decision,
        uint actionId,
        int enemySlot,
        ulong originalTargetId,
        ulong forwardedTargetId,
        ulong effectiveTargetId,
        ActionManager.UseActionMode mode,
        bool defaultTargetCarrier,
        bool targetSuppressedByRedirect,
        string targetResolution,
        IReadOnlyCollection<uint>? sampledStatuses)
    {
        if (decision.ShouldBlock) Interlocked.Increment(ref blockedAttempts);
        if (defaultTargetCarrier && effectiveTargetId != 0)
            Interlocked.Increment(ref defaultTargetResolutions);
        if (targetResolution.StartsWith("Exact canonical enemy", StringComparison.Ordinal))
            Interlocked.Increment(ref exactTargetResolutions);
        else
            Interlocked.Increment(ref targetResolutionFailures);
        lock (diagnosticsGate)
        {
            lastActionId = actionId;
            lastBlockerStatusId = decision.BlockerStatusId;
            lastEnemySlot = enemySlot;
            lastOriginalTargetId = originalTargetId;
            lastForwardedTargetId = forwardedTargetId;
            lastEffectiveTargetId = effectiveTargetId;
            lastMode = (uint)mode;
            lastTargetSuppressedByRedirect = targetSuppressedByRedirect;
            lastTargetResolution = targetResolution;
            lastSampledStatuses = sampledStatuses is { Count: > 0 }
                ? string.Join(',', sampledStatuses.Order())
                : "none";
            lastEvent = decision.ShouldBlock
                ? $"Blocked {decision.Action?.DisplayName ?? actionId.ToString()} on e{enemySlot}"
                : decision.Reason.ToString();
        }
    }

    private bool TryResolveExactCanonicalEnemy(
        IPlayerCharacter? localPlayer,
        ulong targetId,
        out IPlayerCharacter? target,
        out int enemySlot,
        out string resolution)
    {
        target = null;
        enemySlot = 0;
        resolution = "Local player invalid";
        if (!IsLivePlayer(localPlayer) || !HasValidNativeIdentity(localPlayer!))
        {
            return false;
        }
        resolution = "Default/explicit target unresolved";
        if (!IsNetworkObjectId(targetId)) return false;

        var partyEntityIds = partyList
            .Select(static member => member.EntityId)
            .Where(IsNetworkEntityId)
            .ToHashSet();
        var visibleEntityIds = objectTable.PlayerObjects
            .OfType<IPlayerCharacter>()
            .Select(static player => player.EntityId)
            .Where(IsNetworkEntityId)
            .ToHashSet();
        var completePublicCcPartyFallback =
            PvPMatchRules.IsPublicCrystallineConflictTerritory(clientState.TerritoryType) &&
            partyEntityIds.Count == 5 &&
            partyEntityIds.Contains(localPlayer!.EntityId) &&
            partyEntityIds.IsSubsetOf(visibleEntityIds);
        var matches = new List<(int Slot, IPlayerCharacter Player)>(1);
        var seenIdentities = new HashSet<(ulong GameObjectId, uint EntityId)>();
        for (var slot = EnemySlotRules.FirstSlot; slot <= EnemySlotRules.LastSlot; slot++)
        {
            var candidate = EnemySlotResolver.Resolve(objectTable, slot);
            var isSelf = candidate is not null &&
                         (candidate.GameObjectId == localPlayer!.GameObjectId ||
                          candidate.EntityId == localPlayer.EntityId);
            var isPartyOrAllianceMember = candidate is not null &&
                (partyEntityIds.Contains(candidate.EntityId) ||
                 (candidate.StatusFlags & (StatusFlags.PartyMember | StatusFlags.AllianceMember)) != 0);
            var hasHostileFlag = candidate is not null &&
                                 (candidate.StatusFlags & StatusFlags.Hostile) != 0;
            if (!IsLivePlayer(candidate) ||
                !HasValidNativeIdentity(candidate!) ||
                !EnemySlotRules.CanUseResolvedEnemy(
                    isSelf,
                    isPartyOrAllianceMember,
                    hasHostileFlag,
                    completePublicCcPartyFallback,
                    !candidate!.IsDead && candidate.CurrentHp > 0,
                    candidate.IsTargetable,
                    candidate.CurrentHp,
                    candidate.MaxHp) ||
                !candidate.ClassJob.IsValid)
            {
                continue;
            }

            var identity = (candidate.GameObjectId, candidate.EntityId);
            if (!seenIdentities.Add(identity))
            {
                resolution = "Duplicate canonical enemy identity";
                return false;
            }
            if (targetId == candidate.GameObjectId || targetId == candidate.EntityId)
                matches.Add((slot, candidate));
        }

        if (matches.Count != 1)
        {
            resolution = matches.Count == 0
                ? "Target is not an exact live canonical e1-e5 enemy"
                : "Target matched multiple canonical enemies";
            return false;
        }
        var match = matches[0];
        var tableCandidate = objectTable.SearchByEntityId(match.Player.EntityId) as IPlayerCharacter;
        if (tableCandidate is null ||
            tableCandidate.Address != match.Player.Address ||
            tableCandidate.GameObjectId != match.Player.GameObjectId ||
            tableCandidate.EntityId != match.Player.EntityId)
        {
            resolution = "Object-table identity changed";
            return false;
        }

        target = match.Player;
        enemySlot = match.Slot;
        resolution = (match.Player.StatusFlags & StatusFlags.Hostile) != 0
            ? "Exact canonical enemy via hostile flag"
            : "Exact canonical enemy via complete public-CC party fallback";
        return true;
    }

    private static ulong GetNativeHardTargetId(IPlayerCharacter? localPlayer)
    {
        if (localPlayer is null || !HasValidNativeIdentity(localPlayer)) return 0;
        var character = (Character*)localPlayer.Address;
        return character == null ? 0 : character->GetTargetId().Id;
    }

    private SupportedPvPContext ResolveContext()
    {
        var condition = dutyState.ContentFinderCondition;
        var conditionValid = condition.IsValid;
        return PvPMatchRules.ResolveSupportedContext(
            clientState.IsPvP,
            clientState.IsPvPExcludingDen,
            includeWolvesDenTesting: false,
            clientState.TerritoryType,
            conditionValid,
            conditionValid && condition.Value.PvP,
            conditionValid ? condition.Value.ContentUICategory.RowId : 0,
            conditionValid && condition.Value.CrystallineConflictCasualRoulette,
            conditionValid && condition.Value.CrystallineConflictRankedRoulette);
    }

    private static bool IsRecognizedInvocation(
        ActionType actionType,
        ActionManager.UseActionMode mode) =>
        actionType is ActionType.Action or ActionType.PvPAction &&
        (mode is ActionManager.UseActionMode.None or
                 ActionManager.UseActionMode.Macro or
                 ActionManager.UseActionMode.Queue ||
         (uint)mode == 100);

    private static bool IsLivePlayer(IPlayerCharacter? player) =>
        player is not null &&
        player.Address != 0 &&
        IsNetworkEntityId(player.EntityId) &&
        IsNetworkObjectId(player.GameObjectId) &&
        player.IsTargetable &&
        !player.IsDead &&
        player.CurrentHp > 0 &&
        player.MaxHp >= player.CurrentHp;

    private static bool HasValidNativeIdentity(IPlayerCharacter player)
    {
        var native = (GameObject*)player.Address;
        return native != null && native->EntityId == player.EntityId;
    }

    private static bool IsNetworkEntityId(uint entityId) =>
        entityId is not 0 and not 0xE0000000;

    private static bool IsNetworkObjectId(ulong objectId) =>
        objectId is not 0 and not 0xE0000000UL and not ulong.MaxValue;
}
