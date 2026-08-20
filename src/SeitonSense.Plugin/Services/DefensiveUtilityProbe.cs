using System.Numerics;
using Dalamud.Game;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Lumina.Excel.Sheets;
using SeitonSense.Core;
using GameAction = Lumina.Excel.Sheets.Action;
using GameStatus = Lumina.Excel.Sheets.Status;

namespace SeitonSense.Plugin.Services;

internal readonly record struct AcceptedAutoGuardianEpisode(
    long Token,
    long AcceptedAtMilliseconds,
    TargetPressureActorIdentity LocalPlayer,
    TargetPressureActorIdentity Target,
    int PartySlot)
{
    internal bool IsValid =>
        Token > 0 &&
        AcceptedAtMilliseconds >= 0 &&
        LocalPlayer.IsValid &&
        Target.IsValid &&
        LocalPlayer != Target &&
        PartySlot is >= 1 and <= 8;
}

internal sealed record DefensiveUtilityProbeSnapshot(
    bool Active,
    DefensiveUtilityActionKind Action,
    DefensiveUtilityTrigger Trigger,
    bool PressureKnown,
    int IncomingEnemyCount,
    bool GuardActive,
    bool GuardPropagationLatchActive,
    long GuardPropagationLatchRemainingMilliseconds,
    bool HighPressureStunObserved,
    bool WaitingForPostPurifyGuard,
    long PostPurifyGuardRemainingMilliseconds,
    int GuardianCandidateCount,
    ulong TargetGameObjectId,
    uint TargetEntityId,
    VirtualKey FreshGameplayKey,
    VirtualKey HeldGameplayKey,
    bool InputClaimed,
    bool UseActionAttempted,
    bool UseActionAccepted,
    long AttemptCount,
    long AcceptedCount,
    bool GuardMetadataVerified,
    bool GuardianMetadataVerified,
    AcceptedAutoGuardianEpisode? LastAcceptedGuardianEpisode,
    GuardianTriggerPopup? GuardianPopup,
    string LastEvent)
{
    internal static DefensiveUtilityProbeSnapshot Initial { get; } = new(
        false,
        DefensiveUtilityActionKind.None,
        DefensiveUtilityTrigger.None,
        false,
        0,
        false,
        false,
        0,
        false,
        false,
        0,
        0,
        0,
        0,
        VirtualKey.NO_KEY,
        VirtualKey.NO_KEY,
        false,
        false,
        false,
        0,
        0,
        false,
        false,
        null,
        null,
        "Not started");
}

/// <summary>
/// Optional CC-only defensive action helper. It never produces more than one
/// action request for one physical gameplay-key generation. A high-pressure
/// Stun is handled by the existing Purify probe first; this probe can only use
/// Guard on a later physical generation after exact Resilience observation.
/// </summary>
internal sealed class DefensiveUtilityProbe
{
    private readonly IObjectTable objectTable;
    private readonly TargetPressureTracker pressureTracker;
    private readonly NearAssistRedirector nearAssist;
    private readonly IPluginLog log;
    private readonly bool guardMetadataVerified;
    private readonly bool guardianMetadataVerified;
    private readonly HashSet<TargetPressureActorIdentity> guardianSpentActors = [];
    private DefensiveUtilityProbeSnapshot snapshot = DefensiveUtilityProbeSnapshot.Initial;
    private bool awaitingPostPurifyConfirmation;
    private long postPurifyGuardExpiresAt = -1;
    private GuardPropagationState guardPropagationState = GuardPropagationState.Initial;
    private GuardianTriggerPopup? guardianPopup;
    private AcceptedAutoGuardianEpisode? lastAcceptedGuardianEpisode;
    private long guardianEpisodeToken;
    private long attemptCount;
    private long acceptedCount;
    private long nextErrorLogAt;

    internal DefensiveUtilityProbe(
        IObjectTable objectTable,
        IDataManager dataManager,
        TargetPressureTracker pressureTracker,
        NearAssistRedirector nearAssist,
        IPluginLog log,
        PvPMetadataValidation metadata)
    {
        this.objectTable = objectTable;
        this.pressureTracker = pressureTracker;
        this.nearAssist = nearAssist;
        this.log = log;
        var localMetadata = ValidateMetadata(dataManager, log);
        guardMetadataVerified = metadata.GuardVerified && localMetadata.Guard;
        guardianMetadataVerified = metadata.GuardianVerified && localMetadata.Guardian;
    }

    internal DefensiveUtilityProbeSnapshot Snapshot => Volatile.Read(ref snapshot);

    internal GuardPropagationDecision ObserveGuardSuppression(
        bool exactGuardActive,
        long observedGuardAttemptAtMilliseconds,
        long nowMilliseconds,
        bool hardReset = false)
    {
        var decision = DefensiveUtilityRules.ObserveGuardPropagation(
            guardPropagationState,
            exactGuardActive,
            observedGuardAttemptAtMilliseconds,
            nowMilliseconds,
            hardReset);
        guardPropagationState = decision.NextState;
        return decision;
    }

    /// <summary>
    /// First defensive pass. This owns only the verified post-Purify Guard
    /// follow-up and is intentionally independent from Paladin Guardian so a
    /// self-survival helper can be scheduled between the two passes.
    /// </summary>
    internal unsafe DefensiveUtilityProbeSnapshot ObserveGuard(
        IPlayerCharacter? localPlayer,
        bool isCrystallineConflict,
        bool guardConfigurationEnabled,
        bool allowHeldGameplayKey,
        bool enableGuardOnStunPressure,
        bool pressureKnown,
        int incomingEnemyCount,
        bool highPressureStunObserved,
        bool purifyUseActionAttempted,
        bool resilienceActive,
        bool hasPurifyRemovableCrowdControl,
        bool guardActive,
        bool higherPriorityClaimed,
        EmergencyActionInputFrame inputFrame,
        long nowMilliseconds,
        bool hardReset = false)
    {
        if (hardReset)
            ResetRuntime();
        else if (!guardConfigurationEnabled || !isCrystallineConflict)
            ResetGuardOpportunityRuntime();

        var localIdentityValid = HasValidLocalPlayer(localPlayer);
        var highPressure = DefensiveUtilityRules.IsHighPressure(
            pressureKnown,
            incomingEnemyCount);

        UpdatePostPurifyGuard(
            guardConfigurationEnabled &&
            isCrystallineConflict &&
            enableGuardOnStunPressure,
            highPressureStunObserved,
            purifyUseActionAttempted,
            resilienceActive,
            hasPurifyRemovableCrowdControl,
            highPressure,
            guardActive,
            nowMilliseconds);

        var input = inputFrame.Snapshot;
        var freshKey = inputFrame.FreshGameplayKeyPressed
            ? input.FreshGameplayKey
            : VirtualKey.NO_KEY;
        var heldKey = allowHeldGameplayKey && inputFrame.HeldGameplayKeyEligible
            ? input.HeldGameplayKey
            : VirtualKey.NO_KEY;
        var inputEligible = freshKey != VirtualKey.NO_KEY || heldKey != VirtualKey.NO_KEY;
        var action = DefensiveUtilityActionKind.None;
        var trigger = DefensiveUtilityTrigger.None;
        var inputClaimed = false;
        var attempted = false;
        var accepted = false;
        var targetGameObjectId = 0UL;
        var targetEntityId = 0U;
        var lastEvent = DescribeWaitingState(
            guardConfigurationEnabled,
            isCrystallineConflict,
            localIdentityValid,
            guardActive,
            higherPriorityClaimed,
            pressureKnown,
            incomingEnemyCount);

        var canDispatch = guardConfigurationEnabled &&
                          isCrystallineConflict &&
                          localIdentityValid &&
                          input.ProbeSucceeded &&
                          !input.IsTextInputActive &&
                          inputEligible &&
                          !guardActive &&
                          !higherPriorityClaimed;
        if (canDispatch &&
            DefensiveUtilityRules.CanDispatchPostPurifyGuard(
                awaitingPostPurifyConfirmation,
                resilienceActive,
                hasPurifyRemovableCrowdControl,
                postPurifyGuardExpiresAt,
                nowMilliseconds) &&
            guardMetadataVerified &&
            IsActionOffCooldown(EnemyCombatConstants.GuardActionId))
        {
            action = DefensiveUtilityActionKind.Guard;
            trigger = DefensiveUtilityTrigger.PostPurifyHighPressureStun;
            postPurifyGuardExpiresAt = -1;
            awaitingPostPurifyConfirmation = false;
            inputClaimed = true;
            inputFrame.Consume();
            accepted = TryUseGuardOnce(localPlayer!, out attempted);
            lastEvent = accepted
                ? "Guard request accepted after verified Purify/Resilience"
                : "Post-Purify Guard intent consumed; request rejected or revalidation failed";
        }

        if (attempted) Interlocked.Increment(ref attemptCount);
        if (accepted) Interlocked.Increment(ref acceptedCount);

        var guardSuppressionNow = Math.Max(nowMilliseconds, Environment.TickCount64);
        var guardSuppression = ObserveGuardSuppression(
            HasActiveGuard(localPlayer),
            observedGuardAttemptAtMilliseconds: -1,
            guardSuppressionNow);

        var result = new DefensiveUtilityProbeSnapshot(
            guardConfigurationEnabled && isCrystallineConflict && localIdentityValid,
            action,
            trigger,
            pressureKnown,
            incomingEnemyCount,
            guardActive,
            guardSuppression.PropagationLatchActive,
            guardSuppression.RemainingMilliseconds,
            highPressureStunObserved,
            awaitingPostPurifyConfirmation || postPurifyGuardExpiresAt > nowMilliseconds,
            postPurifyGuardExpiresAt > nowMilliseconds
                ? postPurifyGuardExpiresAt - nowMilliseconds
                : 0,
            0,
            targetGameObjectId,
            targetEntityId,
            freshKey,
            heldKey,
            inputClaimed,
            attempted,
            accepted,
            Interlocked.Read(ref attemptCount),
            Interlocked.Read(ref acceptedCount),
            guardMetadataVerified,
            guardianMetadataVerified,
            lastAcceptedGuardianEpisode,
            guardianPopup,
            lastEvent);
        Volatile.Write(ref snapshot, result);
        return result;
    }

    /// <summary>
    /// Second defensive pass. Paladin Guardian has its own feature and held-key
    /// gates and can therefore be scheduled after higher-priority self healing.
    /// </summary>
    internal unsafe DefensiveUtilityProbeSnapshot ObserveGuardian(
        IPlayerCharacter? localPlayer,
        bool isCrystallineConflict,
        bool guardianConfigurationEnabled,
        bool allowHeldGameplayKey,
        bool guardActive,
        bool higherPriorityClaimed,
        EmergencyActionInputFrame inputFrame,
        long nowMilliseconds,
        bool hardReset = false)
    {
        if (hardReset)
            ResetRuntime();
        else if (!guardianConfigurationEnabled || !isCrystallineConflict)
            ResetGuardianOpportunityRuntime();

        var prior = Snapshot;
        var localIdentityValid = HasValidLocalPlayer(localPlayer);
        HashSet<TargetPressureActorIdentity> criticalGuardianActors = [];
        var guardianCandidates = guardianConfigurationEnabled &&
                                 isCrystallineConflict &&
                                 localIdentityValid &&
                                 IsPaladin(localPlayer!)
            ? BuildGuardianCandidates(localPlayer!, out criticalGuardianActors)
            : [];
        if (!guardianConfigurationEnabled || !isCrystallineConflict)
            guardianSpentActors.Clear();
        else
            guardianSpentActors.RemoveWhere(actor => !criticalGuardianActors.Contains(actor));

        var input = inputFrame.Snapshot;
        var freshKey = inputFrame.FreshGameplayKeyPressed
            ? input.FreshGameplayKey
            : VirtualKey.NO_KEY;
        var heldKey = allowHeldGameplayKey && inputFrame.HeldGameplayKeyEligible
            ? input.HeldGameplayKey
            : VirtualKey.NO_KEY;
        var inputEligible = freshKey != VirtualKey.NO_KEY || heldKey != VirtualKey.NO_KEY;
        var action = DefensiveUtilityActionKind.None;
        var trigger = DefensiveUtilityTrigger.None;
        var inputClaimed = false;
        var attempted = false;
        var accepted = false;
        var targetGameObjectId = 0UL;
        var targetEntityId = 0U;
        var selectedGuardianPartySlot = 0;
        var lastEvent = DescribeGuardianWaitingState(
            guardianConfigurationEnabled,
            isCrystallineConflict,
            localIdentityValid,
            guardActive,
            higherPriorityClaimed);

        var canDispatch = guardianConfigurationEnabled &&
                          isCrystallineConflict &&
                          localIdentityValid &&
                          input.ProbeSucceeded &&
                          !input.IsTextInputActive &&
                          inputEligible &&
                          !guardActive &&
                          !higherPriorityClaimed;
        if (canDispatch &&
            guardMetadataVerified &&
            guardianMetadataVerified &&
            IsPaladin(localPlayer!) &&
            IsActionOffCooldown(EnemyCombatConstants.GuardActionId) &&
            IsActionOffCooldown(EnemyCombatConstants.GuardianActionId))
        {
            var selectedIndex = DefensiveUtilityRules.SelectGuardianCandidateIndex(
                guardianCandidates,
                guardianSpentActors);
            if (selectedIndex >= 0)
            {
                var selected = guardianCandidates[selectedIndex];
                action = DefensiveUtilityActionKind.Guardian;
                trigger = DefensiveUtilityTrigger.PaladinGuardianLowAlly;
                targetGameObjectId = selected.GameObjectId;
                targetEntityId = selected.EntityId;
                selectedGuardianPartySlot = selected.PartySlot;
                guardianSpentActors.Add(selected.Actor);
                inputClaimed = true;
                inputFrame.Consume();
                accepted = TryUseGuardianOnce(localPlayer!, selected, out attempted);
                if (attempted && accepted)
                {
                    lastAcceptedGuardianEpisode = new AcceptedAutoGuardianEpisode(
                        NextGuardianEpisodeToken(),
                        Math.Max(nowMilliseconds, Environment.TickCount64),
                        new TargetPressureActorIdentity(
                            localPlayer!.GameObjectId,
                            localPlayer.EntityId),
                        selected.Actor,
                        selected.PartySlot);
                }

                lastEvent = accepted
                    ? $"Guardian request accepted for P{selected.PartySlot} {selected.CurrentHp}/{selected.MaximumHp}"
                    : $"Guardian intent for P{selected.PartySlot} consumed; request rejected or target changed";
            }
        }

        if (attempted) Interlocked.Increment(ref attemptCount);
        if (accepted) Interlocked.Increment(ref acceptedCount);

        guardianPopup = DefensiveUtilityRules.ObserveGuardianTriggerPopup(
            guardianPopup,
            guardianConfigurationEnabled &&
            isCrystallineConflict &&
            localIdentityValid &&
            IsPaladin(localPlayer!),
            action,
            trigger,
            attempted,
            accepted,
            selectedGuardianPartySlot,
            nowMilliseconds,
            hardReset);

        var guardianActed = action == DefensiveUtilityActionKind.Guardian;
        var result = prior with
        {
            Active = prior.Active ||
                     guardianConfigurationEnabled &&
                     isCrystallineConflict &&
                     localIdentityValid,
            Action = guardianActed ? action : prior.Action,
            Trigger = guardianActed ? trigger : prior.Trigger,
            GuardActive = guardActive,
            GuardianCandidateCount = guardianCandidates.Count,
            TargetGameObjectId = guardianActed
                ? targetGameObjectId
                : prior.TargetGameObjectId,
            TargetEntityId = guardianActed
                ? targetEntityId
                : prior.TargetEntityId,
            FreshGameplayKey = guardianActed ? freshKey : prior.FreshGameplayKey,
            HeldGameplayKey = guardianActed ? heldKey : prior.HeldGameplayKey,
            InputClaimed = prior.InputClaimed || inputClaimed,
            UseActionAttempted = prior.UseActionAttempted || attempted,
            UseActionAccepted = prior.UseActionAccepted || accepted,
            AttemptCount = Interlocked.Read(ref attemptCount),
            AcceptedCount = Interlocked.Read(ref acceptedCount),
            LastAcceptedGuardianEpisode = lastAcceptedGuardianEpisode,
            GuardianPopup = guardianPopup,
            LastEvent = guardianActed || !prior.UseActionAttempted
                ? lastEvent
                : prior.LastEvent,
        };
        Volatile.Write(ref snapshot, result);
        return result;
    }

    internal void Reset()
    {
        ResetRuntime();
        Volatile.Write(ref snapshot, DefensiveUtilityProbeSnapshot.Initial with
        {
            AttemptCount = Interlocked.Read(ref attemptCount),
            AcceptedCount = Interlocked.Read(ref acceptedCount),
            GuardMetadataVerified = guardMetadataVerified,
            GuardianMetadataVerified = guardianMetadataVerified,
            LastEvent = "Reset",
        });
    }

    internal DefensiveUtilityProbeSnapshot FailClosed(long nowMilliseconds, Exception? exception = null)
    {
        ResetOpportunityRuntime();
        if (exception is not null) LogAttemptFailure(exception, nowMilliseconds);
        var guardSuppression = ObserveGuardSuppression(
            exactGuardActive: false,
            observedGuardAttemptAtMilliseconds: -1,
            Math.Max(0, nowMilliseconds));
        var failed = DefensiveUtilityProbeSnapshot.Initial with
        {
            GuardActive = guardSuppression.SuppressDirectActionHelpers,
            GuardPropagationLatchActive = guardSuppression.PropagationLatchActive,
            GuardPropagationLatchRemainingMilliseconds = guardSuppression.RemainingMilliseconds,
            AttemptCount = Interlocked.Read(ref attemptCount),
            AcceptedCount = Interlocked.Read(ref acceptedCount),
            GuardMetadataVerified = guardMetadataVerified,
            GuardianMetadataVerified = guardianMetadataVerified,
            LastEvent = "Failed closed",
        };
        Volatile.Write(ref snapshot, failed);
        return failed;
    }

    private void UpdatePostPurifyGuard(
        bool enabled,
        bool highPressureStunObserved,
        bool purifyUseActionAttempted,
        bool resilienceActive,
        bool hasPurifyRemovableCrowdControl,
        bool highPressure,
        bool guardActive,
        long nowMilliseconds)
    {
        if (!enabled || guardActive || nowMilliseconds < 0)
        {
            awaitingPostPurifyConfirmation = false;
            postPurifyGuardExpiresAt = -1;
            return;
        }

        if (highPressureStunObserved && purifyUseActionAttempted)
        {
            awaitingPostPurifyConfirmation = true;
            postPurifyGuardExpiresAt = SaturatingAdd(
                nowMilliseconds,
                DefensiveUtilityRules.PostPurifyGuardWindowMilliseconds);
        }

        if (postPurifyGuardExpiresAt <= nowMilliseconds || !highPressure)
        {
            awaitingPostPurifyConfirmation = false;
            postPurifyGuardExpiresAt = -1;
            return;
        }

        if (awaitingPostPurifyConfirmation &&
            resilienceActive &&
            !hasPurifyRemovableCrowdControl)
        {
            awaitingPostPurifyConfirmation = false;
        }
    }

    private unsafe List<PaladinGuardianCandidate> BuildGuardianCandidates(
        IPlayerCharacter localPlayer,
        out HashSet<TargetPressureActorIdentity> criticalActors)
    {
        criticalActors = [];
        var candidates = new List<PaladinGuardianCandidate>(7);
        var sourceObject = GetNativeObject(localPlayer);
        foreach (var (slot, ally) in ResolveExactPartyMembers())
        {
            if (ally.GameObjectId == localPlayer.GameObjectId ||
                ally.EntityId == localPlayer.EntityId)
            {
                continue;
            }

            var actor = new TargetPressureActorIdentity(ally.GameObjectId, ally.EntityId);
            if (IsLivePlayer(ally) &&
                DefensiveUtilityRules.IsAtOrBelowHpPercent(
                    ally.CurrentHp,
                    ally.MaxHp,
                    DefensiveUtilityRules.GuardianAllyHpPercent))
            {
                criticalActors.Add(actor);
            }

            var targetObject = GetNativeObject(ally);
            var nativeTargetValid = sourceObject != null && targetObject != null;
            var rangeResult = nativeTargetValid
                ? ActionManager.GetActionInRangeOrLoS(
                    EnemyCombatConstants.GuardianActionId,
                    sourceObject,
                    targetObject)
                : uint.MaxValue;
            int? incomingPressure = pressureTracker.TryGetIncomingAllyPressure(
                ally.GameObjectId,
                ally.EntityId,
                out var uniqueEnemyCount)
                ? uniqueEnemyCount
                : null;
            candidates.Add(new PaladinGuardianCandidate(
                ally.GameObjectId,
                ally.EntityId,
                slot,
                ally.CurrentHp,
                ally.MaxHp,
                incomingPressure,
                Vector3.DistanceSquared(localPlayer.Position, ally.Position),
                IsExactPartyMember: true,
                IsSelf: false,
                IsAlive: IsLivePlayer(ally),
                ally.IsTargetable,
                nativeTargetValid,
                SeitonRangeRules.HasNativeRangeAndLineOfSight(rangeResult)));
        }

        return candidates;
    }

    private unsafe bool TryUseGuardOnce(IPlayerCharacter localPlayer, out bool attempted)
    {
        attempted = false;
        if (!guardMetadataVerified ||
            !HasValidLocalPlayer(localPlayer) ||
            HasActiveGuard(localPlayer))
        {
            return false;
        }

        var actionManager = ActionManager.Instance();
        if (actionManager == null ||
            actionManager->GetAdjustedActionId(EnemyCombatConstants.GuardActionId) !=
            EnemyCombatConstants.GuardActionId ||
            !actionManager->IsActionOffCooldown(
                ActionType.Action,
                EnemyCombatConstants.GuardActionId))
        {
            return false;
        }

        attempted = true;
        // Commit the global helper-suppression latch before crossing the native
        // boundary. A false return or exception is still one real Guard attempt
        // and can never be retried or followed by another plugin action inside
        // the short status-propagation window.
        ObserveGuardSuppression(
            exactGuardActive: false,
            observedGuardAttemptAtMilliseconds: Environment.TickCount64,
            nowMilliseconds: Environment.TickCount64);
        try
        {
            return nearAssist.RunWithoutRedirect(() =>
                actionManager->UseAction(
                    ActionType.Action,
                    EnemyCombatConstants.GuardActionId,
                    localPlayer.GameObjectId,
                    0,
                    ActionManager.UseActionMode.None,
                    0));
        }
        catch (Exception exception)
        {
            LogAttemptFailure(exception, Environment.TickCount64);
            return false;
        }
    }

    private unsafe bool TryUseGuardianOnce(
        IPlayerCharacter localPlayer,
        PaladinGuardianCandidate intent,
        out bool attempted)
    {
        attempted = false;
        if (!guardMetadataVerified ||
            !guardianMetadataVerified ||
            !HasValidLocalPlayer(localPlayer) ||
            !IsPaladin(localPlayer) ||
            HasActiveGuard(localPlayer))
        {
            return false;
        }

        var actionManager = ActionManager.Instance();
        if (actionManager == null ||
            actionManager->GetAdjustedActionId(EnemyCombatConstants.GuardActionId) !=
            EnemyCombatConstants.GuardActionId ||
            actionManager->GetAdjustedActionId(EnemyCombatConstants.GuardianActionId) !=
            EnemyCombatConstants.GuardianActionId ||
            !actionManager->IsActionOffCooldown(ActionType.Action, EnemyCombatConstants.GuardActionId) ||
            !actionManager->IsActionOffCooldown(ActionType.Action, EnemyCombatConstants.GuardianActionId))
        {
            return false;
        }

        foreach (var (slot, ally) in ResolveExactPartyMembers())
        {
            if (slot != intent.PartySlot ||
                ally.GameObjectId != intent.GameObjectId ||
                ally.EntityId != intent.EntityId)
            {
                continue;
            }

            var sourceObject = GetNativeObject(localPlayer);
            var targetObject = GetNativeObject(ally);
            var nativeTargetValid = sourceObject != null && targetObject != null;
            var rangeResult = nativeTargetValid
                ? ActionManager.GetActionInRangeOrLoS(
                    EnemyCombatConstants.GuardianActionId,
                    sourceObject,
                    targetObject)
                : uint.MaxValue;
            var revalidated = intent with
            {
                CurrentHp = ally.CurrentHp,
                MaximumHp = ally.MaxHp,
                DistanceSquared = Vector3.DistanceSquared(localPlayer.Position, ally.Position),
                IsAlive = IsLivePlayer(ally),
                IsTargetable = ally.IsTargetable,
                HasValidNativeTarget = nativeTargetValid,
                HasNativeRangeAndLineOfSight =
                    SeitonRangeRules.HasNativeRangeAndLineOfSight(rangeResult),
            };
            if (!DefensiveUtilityRules.IsGuardianCandidate(revalidated)) return false;

            attempted = true;
            try
            {
                return nearAssist.RunWithoutRedirect(() =>
                    actionManager->UseAction(
                        ActionType.Action,
                        EnemyCombatConstants.GuardianActionId,
                        ally.GameObjectId,
                        0,
                        ActionManager.UseActionMode.None,
                        0));
            }
            catch (Exception exception)
            {
                LogAttemptFailure(exception, Environment.TickCount64);
                return false;
            }
        }

        return false;
    }

    private IReadOnlyList<(int Slot, IPlayerCharacter Player)> ResolveExactPartyMembers()
    {
        var resolved = new List<(int Slot, IPlayerCharacter Player)>(8);
        for (var slot = 1; slot <= 8; slot++)
        {
            var player = PartySlotResolver.Resolve(objectTable, slot);
            if (HasValidNativeIdentity(player)) resolved.Add((slot, player!));
        }

        var duplicateGameIds = resolved
            .GroupBy(static item => item.Player.GameObjectId)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToHashSet();
        var duplicateEntityIds = resolved
            .GroupBy(static item => item.Player.EntityId)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToHashSet();
        return resolved
            .Where(item =>
                !duplicateGameIds.Contains(item.Player.GameObjectId) &&
                !duplicateEntityIds.Contains(item.Player.EntityId))
            .ToArray();
    }

    private static unsafe bool IsActionOffCooldown(uint actionId)
    {
        var actionManager = ActionManager.Instance();
        return actionManager != null &&
               actionManager->GetAdjustedActionId(actionId) == actionId &&
               actionManager->IsActionOffCooldown(ActionType.Action, actionId);
    }

    internal static bool HasActiveGuard(IPlayerCharacter? player) =>
        HasActiveStatus(player, EnemyCombatConstants.GuardStatusId) ||
        HasActiveStatus(player, EnemyCombatConstants.GuardStatusAlternateId);

    private static bool HasActiveStatus(IPlayerCharacter? player, uint statusId)
    {
        if (player is null || statusId == 0) return false;
        foreach (var status in player.StatusList)
        {
            if (status.StatusId == statusId &&
                float.IsFinite(status.RemainingTime) &&
                status.RemainingTime > 0f)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPaladin(IPlayerCharacter player) =>
        player.ClassJob.IsValid &&
        player.ClassJob.RowId == EnemyCombatConstants.PaladinJobId;

    private static bool IsLivePlayer(IPlayerCharacter? player) =>
        HasValidNativeIdentity(player) &&
        player!.IsTargetable &&
        !player.IsDead &&
        player.CurrentHp > 0 &&
        player.MaxHp >= player.CurrentHp;

    private static bool HasValidLocalPlayer(IPlayerCharacter? player) =>
        IsLivePlayer(player) &&
        player!.GameObjectId is not 0 and not 0xE0000000;

    private static unsafe bool HasValidNativeIdentity(IPlayerCharacter? player)
    {
        if (player is null ||
            player.Address == 0 ||
            player.EntityId is 0 or 0xE0000000 ||
            player.GameObjectId is 0 or 0xE0000000)
        {
            return false;
        }

        var native = (GameObject*)player.Address;
        return native != null && native->EntityId == player.EntityId;
    }

    private static unsafe GameObject* GetNativeObject(IPlayerCharacter player)
    {
        var native = (GameObject*)player.Address;
        return native != null && native->EntityId == player.EntityId ? native : null;
    }

    private static (bool Guard, bool Guardian) ValidateMetadata(
        IDataManager dataManager,
        IPluginLog log)
    {
        try
        {
            var actions = dataManager.GetExcelSheet<GameAction>(ClientLanguage.English);
            var descriptions = dataManager.GetExcelSheet<ActionTransient>(ClientLanguage.English);
            var statuses = dataManager.GetExcelSheet<GameStatus>(ClientLanguage.English);
            var guard = actions.TryGetRow(EnemyCombatConstants.GuardActionId, out var guardAction) &&
                        descriptions.TryGetRow(EnemyCombatConstants.GuardActionId, out var guardTransient) &&
                        statuses.TryGetRow(EnemyCombatConstants.GuardStatusId, out var guardStatus) &&
                        statuses.TryGetRow(
                            EnemyCombatConstants.GuardStatusAlternateId,
                            out var alternateGuardStatus) &&
                        IsExpectedGuard(
                            guardAction,
                            guardTransient,
                            guardStatus,
                            alternateGuardStatus);
            var guardian = actions.TryGetRow(
                               EnemyCombatConstants.GuardianActionId,
                               out var guardianAction) &&
                           descriptions.TryGetRow(
                               EnemyCombatConstants.GuardianActionId,
                               out var guardianTransient) &&
                           IsExpectedGuardian(guardianAction, guardianTransient);
            if (!guard || !guardian)
            {
                log.Warning(
                    "Seiton Sense defensive utility metadata failed closed: Guard={Guard}, Guardian={Guardian}.",
                    guard,
                    guardian);
            }

            return (guard, guardian);
        }
        catch (Exception exception)
        {
            log.Warning(exception, "Seiton Sense defensive utility metadata lookup failed closed.");
            return (false, false);
        }
    }

    private static bool IsExpectedGuard(
        GameAction action,
        ActionTransient transient,
        GameStatus guardStatus,
        GameStatus alternateGuardStatus)
    {
        var description = transient.Description.ToString();
        return action.Name.ToString() == "Guard" &&
               action.Icon == EnemyCombatConstants.GuardIconId &&
               action.IsPvP &&
               action.IsPlayerAction &&
               action.Range == 0 &&
               action.EffectRange == 0 &&
               action.Cast100ms == 0 &&
               action.Recast100ms == 300 &&
               action.CanTargetSelf &&
               !action.CanTargetParty &&
               !action.CanTargetAlly &&
               !action.CanTargetAlliance &&
               !action.CanTargetHostile &&
               !action.TargetArea &&
               !action.AffectsPosition &&
               description.Contains("Reduces damage taken by 99%", StringComparison.Ordinal) &&
               description.Contains(
                   "Effect ends upon reuse, using another action",
                   StringComparison.Ordinal) &&
               guardStatus.Name.ToString() == "Guard" &&
               alternateGuardStatus.Name.ToString() == "Guard";
    }

    private static bool IsExpectedGuardian(GameAction action, ActionTransient transient)
    {
        var description = transient.Description.ToString();
        return action.Name.ToString() == "Guardian" &&
               action.Icon == EnemyCombatConstants.GuardianIconId &&
               action.IsPvP &&
               action.IsPlayerAction &&
               action.ClassJob.IsValid &&
               action.ClassJob.RowId == EnemyCombatConstants.PaladinJobId &&
               action.Range == EnemyCombatConstants.GuardianSheetRange &&
               action.EffectRange == 0 &&
               action.Cast100ms == 0 &&
               action.Recast100ms == EnemyCombatConstants.GuardianRecast100ms &&
               !action.CanTargetSelf &&
               action.CanTargetParty &&
               !action.CanTargetAlly &&
               !action.CanTargetAlliance &&
               !action.CanTargetHostile &&
               !action.TargetArea &&
               action.RequiresLineOfSight &&
               action.AffectsPosition &&
               description.Contains("Take all damage intended for the targeted party member", StringComparison.Ordinal) &&
               description.Contains("Duration: 8s", StringComparison.Ordinal) &&
               description.Contains("closer than 10 yalms", StringComparison.Ordinal) &&
               description.Contains("Cannot be executed while bound", StringComparison.Ordinal);
    }

    private void ResetRuntime()
    {
        ResetGuardOpportunityRuntime();
        ResetGuardianOpportunityRuntime();
        guardPropagationState = GuardPropagationState.Initial;
        lastAcceptedGuardianEpisode = null;
    }

    private void ResetOpportunityRuntime()
    {
        ResetGuardOpportunityRuntime();
        ResetGuardianOpportunityRuntime();
    }

    private void ResetGuardOpportunityRuntime()
    {
        awaitingPostPurifyConfirmation = false;
        postPurifyGuardExpiresAt = -1;
    }

    private void ResetGuardianOpportunityRuntime()
    {
        guardianSpentActors.Clear();
        guardianPopup = null;
    }

    private long NextGuardianEpisodeToken()
    {
        while (true)
        {
            var current = Volatile.Read(ref guardianEpisodeToken);
            if (current == long.MaxValue) return long.MaxValue;

            var next = current + 1;
            if (Interlocked.CompareExchange(ref guardianEpisodeToken, next, current) == current)
                return next;
        }
    }

    private static string DescribeWaitingState(
        bool configurationEnabled,
        bool isCrystallineConflict,
        bool localIdentityValid,
        bool guardActive,
        bool higherPriorityClaimed,
        bool pressureKnown,
        int incomingEnemyCount)
    {
        if (!configurationEnabled) return "Disabled";
        if (!isCrystallineConflict) return "Outside Crystalline Conflict";
        if (!localIdentityValid) return "Local player invalid";
        if (guardActive) return "Active or propagating Guard blocks every plugin-owned action";
        if (higherPriorityClaimed) return "Waiting behind higher-priority survival helper";
        if (!pressureKnown) return "Pressure unknown";
        return $"Waiting; self pressure={incomingEnemyCount}";
    }

    private static string DescribeGuardianWaitingState(
        bool configurationEnabled,
        bool isCrystallineConflict,
        bool localIdentityValid,
        bool guardActive,
        bool higherPriorityClaimed)
    {
        if (!configurationEnabled) return "Paladin Guardian disabled";
        if (!isCrystallineConflict) return "Paladin Guardian outside Crystalline Conflict";
        if (!localIdentityValid) return "Paladin Guardian local player invalid";
        if (guardActive) return "Active or propagating Guard blocks Guardian";
        if (higherPriorityClaimed) return "Guardian waiting behind higher-priority survival helper";
        return "Guardian waiting for low-HP party ally and gameplay key";
    }

    private void LogAttemptFailure(Exception exception, long nowMilliseconds)
    {
        if (nowMilliseconds < nextErrorLogAt) return;
        nextErrorLogAt = nowMilliseconds + 10_000;
        log.Error(
            exception,
            "Seiton Sense defensive utility attempt failed closed and will not be retried for this intent.");
    }

    private static long SaturatingAdd(long left, long right) =>
        left > long.MaxValue - right ? long.MaxValue : left + right;
}
