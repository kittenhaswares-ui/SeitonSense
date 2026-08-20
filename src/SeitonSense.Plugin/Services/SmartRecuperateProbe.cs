using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using SeitonSense.Core;

namespace SeitonSense.Plugin.Services;

internal sealed record SmartRecuperateProbeSnapshot(
    SmartRecuperateDecisionKind Decision,
    SmartRecuperateDecisionReason Reason,
    uint ResolvedActionId,
    uint CurrentHp,
    uint MaximumHp,
    uint MissingHp,
    uint CurrentMp,
    uint MaximumMp,
    bool LocallyReady,
    bool GuardSuppressed,
    VirtualKey HeldGameplayKey,
    bool InputClaimed,
    bool UseActionAttempted,
    bool UseActionAccepted,
    long AttemptCount,
    long AcceptedCount,
    string LastEvent)
{
    internal static SmartRecuperateProbeSnapshot Initial { get; } = new(
        Decision: SmartRecuperateDecisionKind.None,
        Reason: SmartRecuperateDecisionReason.None,
        ResolvedActionId: 0,
        CurrentHp: 0,
        MaximumHp: 0,
        MissingHp: 0,
        CurrentMp: 0,
        MaximumMp: 0,
        LocallyReady: false,
        GuardSuppressed: false,
        HeldGameplayKey: VirtualKey.NO_KEY,
        InputClaimed: false,
        UseActionAttempted: false,
        UseActionAccepted: false,
        AttemptCount: 0,
        AcceptedCount: 0,
        LastEvent: "Waiting");
}

/// <summary>
/// Converts one unclaimed held physical gameplay-key generation into at most
/// one exact self-targeted PvP Recuperate request. Insufficient HP loss, MP, or
/// readiness does not consume the generation, allowing the same physical hold
/// to become eligible on a later real MP tick. Once an intent is dispatchable,
/// input is consumed before terminal revalidation and no failure is retried.
/// </summary>
internal sealed unsafe class SmartRecuperateProbe
{
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly IDutyState dutyState;
    private readonly NearAssistRedirector nearAssist;
    private readonly IPluginLog log;
    private SmartRecuperateProbeSnapshot snapshot =
        SmartRecuperateProbeSnapshot.Initial;
    private long attemptCount;
    private long acceptedCount;
    private long nextErrorLogAt;
    private string lastEvent = "Waiting";

    internal SmartRecuperateProbe(
        IClientState clientState,
        IObjectTable objectTable,
        IDutyState dutyState,
        NearAssistRedirector nearAssist,
        IPluginLog log)
    {
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.dutyState = dutyState;
        this.nearAssist = nearAssist;
        this.log = log;
    }

    internal SmartRecuperateProbeSnapshot Snapshot => Volatile.Read(ref snapshot);

    internal SmartRecuperateProbeSnapshot Observe(
        IPlayerCharacter? localPlayer,
        bool isCrystallineConflict,
        bool configurationEnabled,
        bool metadataVerified,
        bool actionHelpersSuppressedByGuard,
        bool higherPriorityClaimed,
        EmergencyActionInputFrame inputFrame,
        long nowMilliseconds,
        bool hardReset = false)
    {
        var effectiveHardReset = hardReset || nowMilliseconds < 0;
        var localIdentityValid = TryGetExactIdentity(
            localPlayer,
            out var localIdentity);
        var localAlive = localIdentityValid && IsAlive(localPlayer);
        var localTargetable = localIdentityValid && localPlayer!.IsTargetable;
        var currentHp = localIdentityValid ? localPlayer!.CurrentHp : 0;
        var maximumHp = localIdentityValid ? localPlayer!.MaxHp : 0;
        var currentMp = localIdentityValid ? localPlayer!.CurrentMp : 0;
        var maximumMp = localIdentityValid ? localPlayer!.MaxMp : 0;
        var resolvedActionId = 0u;
        var actionReady = localIdentityValid &&
                          TryGetActionState(
                              localPlayer!,
                              out resolvedActionId);

        var input = inputFrame.Snapshot;
        var decision = SmartRecuperateRules.Observe(
            new SmartRecuperateObservation(
                configurationEnabled,
                isCrystallineConflict,
                localIdentity,
                localAlive,
                localTargetable,
                metadataVerified,
                actionHelpersSuppressedByGuard,
                higherPriorityClaimed,
                input.ProbeSucceeded,
                input.IsTextInputActive,
                inputFrame.HeldGameplayKeyEligible,
                resolvedActionId,
                actionReady,
                currentHp,
                maximumHp,
                currentMp,
                maximumMp,
                effectiveHardReset));

        // This physical generation is terminal before any repeated native
        // reads. Drift, rejection, or an exception cannot retry Recuperate or
        // allow a lower-priority helper to reuse this generation.
        var inputClaimed = decision.ShouldConsumeInputGeneration;
        if (inputClaimed) inputFrame.Consume();

        var attempted = false;
        var accepted = false;
        if (decision.ShouldDispatch && decision.Intent is { } intent)
        {
            try
            {
                accepted = TryUseRecuperateOnce(
                    intent,
                    localPlayer!.Address,
                    configurationEnabled,
                    metadataVerified,
                    higherPriorityClaimed,
                    out attempted);
                if (attempted) Interlocked.Increment(ref attemptCount);
                if (accepted) Interlocked.Increment(ref acceptedCount);
                lastEvent = attempted
                    ? $"Self action {intent.ActionId} attempted (accepted={accepted})"
                    : "Terminal frozen-intent revalidation failed";
            }
            catch (Exception exception)
            {
                if (attempted) Interlocked.Increment(ref attemptCount);
                lastEvent = "Terminal action exception";
                LogAttemptFailure(exception, nowMilliseconds);
            }
        }
        else
        {
            lastEvent = decision.Reason.ToString();
        }

        var result = new SmartRecuperateProbeSnapshot(
            decision.Kind,
            decision.Reason,
            resolvedActionId,
            currentHp,
            maximumHp,
            SmartRecuperateRules.GetMissingHp(currentHp, maximumHp),
            currentMp,
            maximumMp,
            actionReady,
            actionHelpersSuppressedByGuard,
            input.HeldGameplayKey,
            inputClaimed,
            attempted,
            accepted,
            Interlocked.Read(ref attemptCount),
            Interlocked.Read(ref acceptedCount),
            lastEvent);
        Volatile.Write(ref snapshot, result);
        return result;
    }

    internal void Reset()
    {
        lastEvent = "Reset";
        Volatile.Write(ref snapshot, SmartRecuperateProbeSnapshot.Initial with
        {
            AttemptCount = Interlocked.Read(ref attemptCount),
            AcceptedCount = Interlocked.Read(ref acceptedCount),
            LastEvent = lastEvent,
        });
    }

    internal SmartRecuperateProbeSnapshot FailClosed()
    {
        lastEvent = "Failed closed";
        var result = SmartRecuperateProbeSnapshot.Initial with
        {
            Decision = SmartRecuperateDecisionKind.Cancelled,
            Reason = SmartRecuperateDecisionReason.HardReset,
            AttemptCount = Interlocked.Read(ref attemptCount),
            AcceptedCount = Interlocked.Read(ref acceptedCount),
            LastEvent = lastEvent,
        };
        Volatile.Write(ref snapshot, result);
        return result;
    }

    private bool TryUseRecuperateOnce(
        SmartRecuperateIntent intent,
        nint expectedLocalAddress,
        bool configurationEnabled,
        bool metadataVerified,
        bool higherPriorityClaimed,
        out bool attempted)
    {
        attempted = false;
        var currentLocal = ResolveExactLocalPlayer(
            intent.LocalPlayer,
            expectedLocalAddress);
        if (currentLocal is null) return false;

        var isCrystallineConflict = IsCurrentCrystallineConflict();
        var localAlive = IsAlive(currentLocal);
        var localTargetable = currentLocal.IsTargetable;
        var guardSuppressed = IsCurrentlySuppressedByGuard(
            currentLocal,
            Environment.TickCount64);
        var actionReady = TryGetActionState(
            currentLocal,
            out var resolvedActionId);
        var currentIdentity = new TargetPressureActorIdentity(
            currentLocal.GameObjectId,
            currentLocal.EntityId);
        if (!SmartRecuperateRules.CanUseFrozenIntent(
                intent,
                configurationEnabled,
                isCrystallineConflict,
                currentIdentity,
                localAlive,
                localTargetable,
                metadataVerified,
                guardSuppressed,
                higherPriorityClaimed,
                resolvedActionId,
                actionReady,
                currentLocal.CurrentHp,
                currentLocal.MaxHp,
                currentLocal.CurrentMp,
                currentLocal.MaxMp))
        {
            return false;
        }

        var actionManager = ActionManager.Instance();
        if (actionManager == null) return false;

        attempted = true;
        // Recuperate is self-only. Passing the frozen exact local GOID directly
        // leaves hard, soft, focus, and mouseover targets untouched.
        return nearAssist.RunWithoutRedirect(() =>
            actionManager->UseAction(
                ActionType.Action,
                intent.ActionId,
                intent.LocalPlayer.GameObjectId,
                0,
                ActionManager.UseActionMode.None,
                0));
    }

    private bool TryGetExactIdentity(
        IPlayerCharacter? player,
        out TargetPressureActorIdentity identity)
    {
        identity = default;
        if (player is null ||
            player.Address == nint.Zero ||
            !IsNetworkObjectId(player.GameObjectId) ||
            !IsNetworkEntityId(player.EntityId))
        {
            return false;
        }

        var native = (GameObject*)player.Address;
        if (native == null || native->EntityId != player.EntityId) return false;
        var tablePlayer = objectTable.SearchByEntityId(player.EntityId) as
            IPlayerCharacter;
        if (tablePlayer is null ||
            tablePlayer.Address != player.Address ||
            tablePlayer.GameObjectId != player.GameObjectId ||
            tablePlayer.EntityId != player.EntityId)
        {
            return false;
        }

        identity = new TargetPressureActorIdentity(
            player.GameObjectId,
            player.EntityId);
        return identity.IsValid;
    }

    private IPlayerCharacter? ResolveExactLocalPlayer(
        TargetPressureActorIdentity expectedIdentity,
        nint expectedAddress)
    {
        var current = objectTable.LocalPlayer;
        return TryGetExactIdentity(current, out var currentIdentity) &&
               currentIdentity == expectedIdentity &&
               current!.Address == expectedAddress
            ? current
            : null;
    }

    private bool IsCurrentCrystallineConflict()
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
                   conditionValid &&
                       condition.Value.CrystallineConflictCasualRoulette,
                   conditionValid &&
                       condition.Value.CrystallineConflictRankedRoulette) ==
               SupportedPvPContext.CrystallineConflict;
    }

    private bool IsCurrentlySuppressedByGuard(
        IPlayerCharacter localPlayer,
        long nowMilliseconds)
    {
        if (DefensiveUtilityProbe.HasActiveGuard(localPlayer)) return true;
        return nearAssist.TryGetRecentExactLocalGuardAttempt(
            clientState.TerritoryType,
            localPlayer.GameObjectId,
            localPlayer.EntityId,
            nowMilliseconds,
            DefensiveUtilityRules.GuardPropagationLatchMilliseconds,
            out _);
    }

    private static bool TryGetActionState(
        IPlayerCharacter localPlayer,
        out uint resolvedActionId)
    {
        resolvedActionId = 0;
        if (!localPlayer.ClassJob.IsValid ||
            localPlayer.ClassJob.RowId == 0 ||
            GetNativeObject(localPlayer) == null)
        {
            return false;
        }

        var actionManager = ActionManager.Instance();
        if (actionManager == null) return false;
        resolvedActionId = actionManager->GetAdjustedActionId(
            SmartRecuperateRules.ActionId);
        return resolvedActionId == SmartRecuperateRules.ActionId &&
               actionManager->IsActionOffCooldown(
                   ActionType.Action,
                   resolvedActionId);
    }

    private static bool IsAlive(IPlayerCharacter? player) =>
        player is not null &&
        !player.IsDead &&
        SmartRecuperateRules.HasValidHealth(player.CurrentHp, player.MaxHp);

    private static GameObject* GetNativeObject(IPlayerCharacter player)
    {
        var native = (GameObject*)player.Address;
        return native != null && native->EntityId == player.EntityId
            ? native
            : null;
    }

    private static bool IsNetworkEntityId(uint entityId) =>
        entityId is not 0 and not 0xE0000000 and not uint.MaxValue;

    private static bool IsNetworkObjectId(ulong gameObjectId) =>
        gameObjectId is not 0 and not 0xE0000000 and not ulong.MaxValue;

    private void LogAttemptFailure(Exception exception, long nowMilliseconds)
    {
        if (nowMilliseconds >= 0 && nowMilliseconds < nextErrorLogAt) return;
        nextErrorLogAt = nowMilliseconds < 0
            ? 0
            : nowMilliseconds > long.MaxValue - 10_000
                ? long.MaxValue
                : nowMilliseconds + 10_000;
        log.Error(
            exception,
            "Seiton Sense Smart Recuperate attempt failed and will not be retried.");
    }
}
