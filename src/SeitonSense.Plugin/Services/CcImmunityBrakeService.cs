using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Party;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using SeitonSense.Core;
using SeitonSense.Plugin.Models;

namespace SeitonSense.Plugin.Services;

internal readonly record struct CcImmunityBrakeDiagnostics(
    bool Enabled,
    int VerifiedActions,
    int VerifiedStatuses,
    long EvaluatedAttempts,
    long BlockedAttempts,
    long FailedOpenAttempts,
    uint LastActionId,
    uint LastBlockerStatusId,
    int LastEnemySlot,
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
    private uint lastActionId;
    private uint lastBlockerStatusId;
    private int lastEnemySlot;
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
                    verifiedActionIds.Count,
                    verifiedStatusIds.Count,
                    evaluatedAttempts,
                    blockedAttempts,
                    failedOpenAttempts,
                    lastActionId,
                    lastBlockerStatusId,
                    lastEnemySlot,
                    lastEvent);
            }
        }
    }

    internal IReadOnlySet<uint> VerifiedStatusIds => verifiedStatusIds;

    internal bool ShouldBlock(
        ActionType actionType,
        uint resolvedActionId,
        ulong forwardedTargetId,
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
        var exactTarget = TryResolveExactCanonicalEnemy(
            localPlayer,
            forwardedTargetId,
            out var target,
            out var enemySlot);
        var targetIdentity = target is null
            ? default
            : new TargetPressureActorIdentity(target.GameObjectId, target.EntityId);
        var targetJobId = target?.ClassJob.IsValid == true ? target.ClassJob.RowId : 0;
        var liveStatuses = target?.StatusList
            .Select(static status => status.StatusId)
            .Where(verifiedStatusIds.Contains)
            .ToArray();
        var decision = CcImmunityBrakeRules.Evaluate(
            masterEnabled: true,
            configuration.IsCcBrakeJobEnabled(localJobId),
            configuration.IsCcBrakeActionEnabled(resolvedActionId),
            localJobId,
            resolvedActionId,
            forwardedTargetId,
            targetIdentity,
            targetJobId,
            exactTarget,
            liveStatuses);
        RecordDecision(decision, resolvedActionId, enemySlot);
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
        int enemySlot)
    {
        if (decision.ShouldBlock) Interlocked.Increment(ref blockedAttempts);
        lock (diagnosticsGate)
        {
            lastActionId = actionId;
            lastBlockerStatusId = decision.BlockerStatusId;
            lastEnemySlot = enemySlot;
            lastEvent = decision.ShouldBlock
                ? $"Blocked {decision.Action?.DisplayName ?? actionId.ToString()} on e{enemySlot}"
                : decision.Reason.ToString();
        }
    }

    private bool TryResolveExactCanonicalEnemy(
        IPlayerCharacter? localPlayer,
        ulong targetId,
        out IPlayerCharacter? target,
        out int enemySlot)
    {
        target = null;
        enemySlot = 0;
        if (!IsLivePlayer(localPlayer) ||
            !HasValidNativeIdentity(localPlayer!) ||
            !IsNetworkObjectId(targetId))
        {
            return false;
        }

        var partyEntityIds = partyList
            .Select(static member => member.EntityId)
            .Where(IsNetworkEntityId)
            .ToHashSet();
        var matches = new List<(int Slot, IPlayerCharacter Player)>(1);
        var seenIdentities = new HashSet<(ulong GameObjectId, uint EntityId)>();
        for (var slot = EnemySlotRules.FirstSlot; slot <= EnemySlotRules.LastSlot; slot++)
        {
            var candidate = EnemySlotResolver.Resolve(objectTable, slot);
            if (!IsLivePlayer(candidate) ||
                !HasValidNativeIdentity(candidate!) ||
                candidate!.GameObjectId == localPlayer!.GameObjectId ||
                candidate.EntityId == localPlayer.EntityId ||
                partyEntityIds.Contains(candidate.EntityId) ||
                (candidate.StatusFlags & (StatusFlags.PartyMember | StatusFlags.AllianceMember)) != 0 ||
                (candidate.StatusFlags & StatusFlags.Hostile) == 0 ||
                !candidate.ClassJob.IsValid)
            {
                continue;
            }

            var identity = (candidate.GameObjectId, candidate.EntityId);
            if (!seenIdentities.Add(identity)) return false;
            if (targetId == candidate.GameObjectId || targetId == candidate.EntityId)
                matches.Add((slot, candidate));
        }

        if (matches.Count != 1) return false;
        var match = matches[0];
        var tableCandidate = objectTable.SearchByEntityId(match.Player.EntityId) as IPlayerCharacter;
        if (tableCandidate is null ||
            tableCandidate.Address != match.Player.Address ||
            tableCandidate.GameObjectId != match.Player.GameObjectId ||
            tableCandidate.EntityId != match.Player.EntityId)
        {
            return false;
        }

        target = match.Player;
        enemySlot = match.Slot;
        return true;
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
