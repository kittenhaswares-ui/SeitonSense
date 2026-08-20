using System.Numerics;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using SeitonSense.Core;

namespace SeitonSense.Plugin.Services;

internal sealed record DarkKnightPlungeProbeSnapshot(
    DarkKnightPlungeDecisionKind Decision,
    DarkKnightPlungeDecisionReason Reason,
    DarkKnightPlungeHoldOutcome HoldOutcome,
    bool OwnsContinuousHold,
    bool CooldownUnavailableObserved,
    VirtualKey HeldGameplayKey,
    ulong CurrentReadyEpochToken,
    ulong SpentReadyEpochToken,
    uint ResolvedActionId,
    bool CooldownStateKnown,
    bool CooldownReady,
    bool StructurallyReady,
    int CandidateCount,
    int EnemySlot,
    ulong TargetGameObjectId,
    uint TargetEntityId,
    uint RevalidatedCurrentHp,
    uint RevalidatedMaximumHp,
    float RevalidatedCenterDistanceYalms,
    bool RevalidatedTargetGuardActive,
    bool InputClaimed,
    bool UseActionAttempted,
    bool UseActionAccepted,
    long AttemptCount,
    long AcceptedCount,
    string CandidateResolution,
    string LastEvent)
{
    internal static DarkKnightPlungeProbeSnapshot Initial { get; } = new(
        DarkKnightPlungeDecisionKind.None,
        DarkKnightPlungeDecisionReason.None,
        DarkKnightPlungeHoldOutcome.None,
        false,
        false,
        VirtualKey.NO_KEY,
        0,
        0,
        0,
        false,
        false,
        false,
        0,
        0,
        0,
        0,
        0,
        0,
        0f,
        false,
        false,
        false,
        false,
        0,
        0,
        "Not evaluated",
        "Waiting");
}

/// <summary>
/// Default-off DRK PvP Plunge helper. It selects one exact canonical S1-S5
/// unguarded enemy at or below 30% HP and no farther than ten center yalms. The
/// initial attempt consumes one shared held-key generation. A client-accepted
/// attempt may retain that exact physical key, but every repeat requires a
/// separately observed cooldown active-to-ready epoch.
/// </summary>
internal sealed class DarkKnightPlungeProbe
{
    private const float AnimationLockEpsilonSeconds = 0.0005f;

    private readonly IClientState clientState;
    private readonly IDutyState dutyState;
    private readonly IObjectTable objectTable;
    private readonly ExecuteTracker executeTracker;
    private readonly NearAssistRedirector nearAssist;
    private readonly IPluginLog log;
    private DarkKnightPlungeHoldState holdState = DarkKnightPlungeHoldState.Initial;
    private DarkKnightPlungeProbeSnapshot snapshot = DarkKnightPlungeProbeSnapshot.Initial;
    private long attemptCount;
    private long acceptedCount;
    private long nextErrorLogAt;
    private string lastEvent = "Waiting";

    internal DarkKnightPlungeProbe(
        IClientState clientState,
        IDutyState dutyState,
        IObjectTable objectTable,
        ExecuteTracker executeTracker,
        NearAssistRedirector nearAssist,
        IPluginLog log)
    {
        this.clientState = clientState;
        this.dutyState = dutyState;
        this.objectTable = objectTable;
        this.executeTracker = executeTracker;
        this.nearAssist = nearAssist;
        this.log = log;
    }

    internal DarkKnightPlungeProbeSnapshot Snapshot => Volatile.Read(ref snapshot);

    internal DarkKnightPlungeProbeSnapshot Observe(
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
        var localIdentityValid = TryGetExactLiveIdentity(localPlayer, out var localIdentity);
        var localAlive = localIdentityValid && !localPlayer!.IsDead && localPlayer.CurrentHp > 0;
        var localTargetable = localIdentityValid && localPlayer!.IsTargetable;
        var localJobId = localPlayer?.ClassJob.IsValid == true
            ? localPlayer.ClassJob.RowId
            : 0;
        var exactJob = localJobId == DarkKnightPlungeRules.DarkKnightJobId;
        var featureContextValid = configurationEnabled &&
                                  isCrystallineConflict &&
                                  localAlive &&
                                  localTargetable &&
                                  exactJob &&
                                  metadataVerified &&
                                  !actionHelpersSuppressedByGuard &&
                                  !hardReset;

        // Keep every action-manager observation behind the exact DRK/CC/metadata
        // feature gate. Other jobs never probe Plunge or activate its target path.
        var nativeState = featureContextValid &&
                          TryObserveNativeState(localPlayer, out var observedNativeState)
            ? observedNativeState
            : PlungeNativeState.Unknown;
        var input = inputFrame.Snapshot;
        var ownedKey = holdState.OwnsHold
            ? (VirtualKey)holdState.HeldKeyCode
            : VirtualKey.NO_KEY;
        var exactOwnedKeyStillDown = holdState.OwnsHold &&
                                     inputFrame.IsGameplayKeyPhysicallyDown(ownedKey);
        var holdDecision = DarkKnightPlungeRules.ObserveOwnedHold(
            holdState,
            new DarkKnightPlungeHoldObservation(
                hardReset,
                featureContextValid,
                input.ProbeSucceeded,
                input.IsTextInputActive,
                (int)ownedKey,
                exactOwnedKeyStillDown,
                nativeState.CooldownStateKnown,
                nativeState.CooldownReady));
        holdState = holdDecision.State;
        ownedKey = holdState.OwnsHold
            ? (VirtualKey)holdState.HeldKeyCode
            : VirtualKey.NO_KEY;
        exactOwnedKeyStillDown = holdState.OwnsHold &&
                                 inputFrame.IsGameplayKeyPhysicallyDown(ownedKey);

        var structurallyReady = featureContextValid &&
                                HasGlobalStructuralReadiness(localPlayer!, nativeState);
        var hasInputOpportunity = holdState.OwnsHold
            ? holdState.HasAvailableReadyEpoch && exactOwnedKeyStillDown
            : inputFrame.HeldGameplayKeyEligible;
        var shouldResolveCandidates = featureContextValid &&
                                      !higherPriorityClaimed &&
                                      input.ProbeSucceeded &&
                                      !input.IsTextInputActive &&
                                      hasInputOpportunity &&
                                      nativeState.ResolvedActionId ==
                                      DarkKnightPlungeRules.ActionId &&
                                      nativeState.CooldownStateKnown &&
                                      nativeState.CooldownReady &&
                                      structurallyReady;
        var candidateResolution = "Not evaluated: no eligible held-input epoch";
        var candidates = shouldResolveCandidates
            ? ResolveExactCandidates(
                localPlayer!,
                nativeState.ResolvedActionId,
                out candidateResolution)
            : [];

        var decision = DarkKnightPlungeRules.Evaluate(
            new DarkKnightPlungeObservation(
                configurationEnabled,
                isCrystallineConflict,
                localJobId,
                localIdentity,
                localAlive,
                localTargetable,
                metadataVerified,
                actionHelpersSuppressedByGuard,
                higherPriorityClaimed,
                input.ProbeSucceeded,
                input.IsTextInputActive,
                inputFrame.HeldGameplayKeyEligible,
                (int)input.HeldGameplayKey,
                exactOwnedKeyStillDown,
                holdState,
                nativeState.ResolvedActionId,
                nativeState.CooldownStateKnown,
                nativeState.CooldownReady,
                structurallyReady,
                candidates,
                hardReset));

        var inputClaimed = false;
        var repeatEpochSpent = false;
        if (decision.ShouldConsumeSharedInputGeneration)
        {
            inputFrame.Consume();
            inputClaimed = true;
        }
        else if (decision.ShouldSpendReadyEpoch && decision.Intent is { } repeatIntent)
        {
            repeatEpochSpent = DarkKnightPlungeRules.TrySpendReadyEpoch(
                holdState,
                repeatIntent.ReadyEpochToken,
                out var spentState);
            if (repeatEpochSpent)
            {
                holdState = spentState;
                inputClaimed = true;
            }
        }

        var attempted = false;
        var accepted = false;
        DarkKnightPlungeCandidate? boundaryCandidate = null;
        if (decision.ShouldDispatch &&
            decision.Intent is { } intent &&
            (!intent.IsRepeat || repeatEpochSpent))
        {
            try
            {
                accepted = TryUsePlungeOnce(
                    localPlayer!,
                    localIdentity,
                    configurationEnabled,
                    metadataVerified,
                    higherPriorityClaimed,
                    inputFrame,
                    intent,
                    out attempted,
                    out boundaryCandidate);
                if (attempted) Interlocked.Increment(ref attemptCount);
                if (accepted) Interlocked.Increment(ref acceptedCount);

                if (!intent.IsRepeat)
                {
                    holdState = accepted
                        ? DarkKnightPlungeRules.BeginOwnedHold(intent.HeldKeyCode)
                        : DarkKnightPlungeHoldState.Initial;
                }

                lastEvent = attempted
                    ? $"S{intent.EnemySlot} Plunge attempted (accepted={accepted}, " +
                      $"epoch={intent.ReadyEpochToken})"
                    : $"S{intent.EnemySlot} terminal frozen-intent validation failed";
            }
            catch (Exception exception)
            {
                if (attempted) Interlocked.Increment(ref attemptCount);
                if (!intent.IsRepeat) holdState = DarkKnightPlungeHoldState.Initial;
                lastEvent = $"S{intent.EnemySlot} terminal Plunge exception";
                LogAttemptFailure(exception, nowMilliseconds);
            }
        }

        var selectedCandidate = decision.SelectedCandidateIndex >= 0 &&
                                decision.SelectedCandidateIndex < candidates.Count
            ? candidates[decision.SelectedCandidateIndex]
            : (DarkKnightPlungeCandidate?)null;
        var observedCandidate = boundaryCandidate ?? selectedCandidate;
        var result = new DarkKnightPlungeProbeSnapshot(
            decision.Kind,
            decision.Reason,
            holdDecision.Outcome,
            holdState.OwnsHold,
            holdState.ObservedCooldownUnavailable,
            holdState.OwnsHold ? (VirtualKey)holdState.HeldKeyCode : input.HeldGameplayKey,
            holdState.CurrentReadyEpochToken,
            holdState.SpentReadyEpochToken,
            nativeState.ResolvedActionId,
            nativeState.CooldownStateKnown,
            nativeState.CooldownReady,
            structurallyReady,
            candidates.Count,
            observedCandidate?.EnemySlot ?? 0,
            observedCandidate?.Actor.GameObjectId ?? 0,
            observedCandidate?.Actor.EntityId ?? 0,
            observedCandidate?.CurrentHp ?? 0,
            observedCandidate?.MaximumHp ?? 0,
            observedCandidate is { CenterDistanceSquared: >= 0f } exactDistance &&
            float.IsFinite(exactDistance.CenterDistanceSquared)
                ? MathF.Sqrt(exactDistance.CenterDistanceSquared)
                : 0f,
            observedCandidate?.TargetGuardActive ?? false,
            inputClaimed,
            attempted,
            accepted,
            Interlocked.Read(ref attemptCount),
            Interlocked.Read(ref acceptedCount),
            candidateResolution,
            lastEvent);
        Volatile.Write(ref snapshot, result);
        return result;
    }

    internal void Reset()
    {
        holdState = DarkKnightPlungeHoldState.Initial;
        lastEvent = "Reset";
        Volatile.Write(ref snapshot, DarkKnightPlungeProbeSnapshot.Initial with
        {
            AttemptCount = Interlocked.Read(ref attemptCount),
            AcceptedCount = Interlocked.Read(ref acceptedCount),
            LastEvent = lastEvent,
        });
    }

    internal DarkKnightPlungeProbeSnapshot FailClosed()
    {
        holdState = DarkKnightPlungeHoldState.Initial;
        lastEvent = "Failed closed";
        var result = DarkKnightPlungeProbeSnapshot.Initial with
        {
            Decision = DarkKnightPlungeDecisionKind.Cancelled,
            Reason = DarkKnightPlungeDecisionReason.HardReset,
            AttemptCount = Interlocked.Read(ref attemptCount),
            AcceptedCount = Interlocked.Read(ref acceptedCount),
            LastEvent = lastEvent,
        };
        Volatile.Write(ref snapshot, result);
        return result;
    }

    private IReadOnlyList<DarkKnightPlungeCandidate> ResolveExactCandidates(
        IPlayerCharacter localPlayer,
        uint actionId,
        out string resolution)
    {
        var diagnosticsBefore = executeTracker.Diagnostics;
        if (!diagnosticsBefore.Active || !diagnosticsBefore.IsCrystallineConflict)
        {
            resolution = "Tracker CC context unavailable";
            return [];
        }

        if (diagnosticsBefore.SlotCapacity != EnemySlotRules.LastSlot ||
            diagnosticsBefore.ResolvedSlots != EnemySlotRules.LastSlot)
        {
            resolution =
                $"Tracker slots incomplete: {diagnosticsBefore.ResolvedSlots}/{diagnosticsBefore.SlotCapacity}";
            return [];
        }

        var snapshots = executeTracker.Enemies.ToArray();
        var diagnosticsAfter = executeTracker.Diagnostics;
        if (!ReferenceEquals(diagnosticsBefore, diagnosticsAfter))
        {
            resolution = "Tracker snapshot changed during capture";
            return [];
        }

        if (snapshots.Length > EnemySlotRules.LastSlot ||
            snapshots.Length != diagnosticsBefore.ValidEnemySlots)
        {
            resolution =
                $"Tracker snapshot count drift: {snapshots.Length}/{diagnosticsBefore.ValidEnemySlots}";
            return [];
        }

        var seenSlots = new HashSet<int>();
        var seenGameObjectIds = new HashSet<ulong>();
        var seenEntityIds = new HashSet<uint>();
        var snapshotsBySlot = new Dictionary<int, EnemyHudSnapshot>(snapshots.Length);
        foreach (var snapshotEnemy in snapshots)
        {
            if (!EnemySlotRules.IsValidSlot(snapshotEnemy.Slot) ||
                !IsNetworkObjectId(snapshotEnemy.GameObjectId) ||
                !IsNetworkEntityId(snapshotEnemy.EntityId) ||
                !seenSlots.Add(snapshotEnemy.Slot) ||
                !seenGameObjectIds.Add(snapshotEnemy.GameObjectId) ||
                !seenEntityIds.Add(snapshotEnemy.EntityId))
            {
                resolution = "Tracker snapshot identity ambiguous";
                return [];
            }

            snapshotsBySlot.Add(snapshotEnemy.Slot, snapshotEnemy);
        }

        var currentSlots = new List<(int Slot, IPlayerCharacter Player)>(
            EnemySlotRules.LastSlot);
        seenGameObjectIds.Clear();
        seenEntityIds.Clear();
        var seenAddresses = new HashSet<nint>();
        for (var slot = EnemySlotRules.FirstSlot; slot <= EnemySlotRules.LastSlot; slot++)
        {
            var player = EnemySlotResolver.Resolve(objectTable, slot);
            if (!HasValidNativeIdentity(player))
            {
                resolution = $"Native S{slot} unresolved";
                return [];
            }

            var tablePlayer = objectTable.SearchByEntityId(player!.EntityId) as IPlayerCharacter;
            if (!HasSameNativeIdentity(player, tablePlayer))
            {
                resolution = $"Native S{slot} object-table identity mismatch";
                return [];
            }

            if (!seenGameObjectIds.Add(player.GameObjectId) ||
                !seenEntityIds.Add(player.EntityId) ||
                !seenAddresses.Add(player.Address))
            {
                resolution = "Native S1-S5 identities duplicate";
                return [];
            }

            currentSlots.Add((slot, player));
        }

        var eligibleCurrentSlots = currentSlots
            .Where(static entry =>
                IsLivePlayer(entry.Player) &&
                entry.Player.IsTargetable &&
                ExecuteThreshold.HasValidHp(entry.Player.CurrentHp, entry.Player.MaxHp))
            .ToArray();
        if (eligibleCurrentSlots.Length != diagnosticsBefore.ValidEnemySlots ||
            eligibleCurrentSlots.Length != snapshots.Length)
        {
            resolution =
                $"Tracker/native eligible count drift: {snapshots.Length}/{eligibleCurrentSlots.Length}";
            return [];
        }

        var candidates = new List<DarkKnightPlungeCandidate>(eligibleCurrentSlots.Length);
        foreach (var (slot, player) in eligibleCurrentSlots)
        {
            if (!snapshotsBySlot.TryGetValue(slot, out var snapshotEnemy) ||
                snapshotEnemy.GameObjectId != player.GameObjectId ||
                snapshotEnemy.EntityId != player.EntityId)
            {
                resolution = $"Tracker/native S{slot} identity mismatch";
                return [];
            }

            var candidate = BuildExactSlotCandidate(
                localPlayer,
                actionId,
                slot,
                new TargetPressureActorIdentity(player.GameObjectId, player.EntityId));
            if (candidate is not { } exact)
            {
                resolution = $"Native S{slot} action validation failed";
                return [];
            }

            candidates.Add(exact);
        }

        foreach (var (slot, player) in currentSlots)
        {
            var stablePlayer = EnemySlotResolver.Resolve(objectTable, slot);
            if (!HasSameNativeIdentity(player, stablePlayer))
            {
                resolution = $"Native S{slot} changed during capture";
                return [];
            }
        }

        resolution = $"Exact coherent set: {candidates.Count} candidates";
        return candidates;
    }

    private DarkKnightPlungeCandidate? ResolveFrozenIntent(
        IPlayerCharacter localPlayer,
        DarkKnightPlungeIntent intent,
        uint actionId) =>
        BuildExactSlotCandidate(
            localPlayer,
            actionId,
            intent.EnemySlot,
            intent.Target);

    private unsafe DarkKnightPlungeCandidate? BuildExactSlotCandidate(
        IPlayerCharacter localPlayer,
        uint actionId,
        int enemySlot,
        TargetPressureActorIdentity expectedTarget)
    {
        if (actionId != DarkKnightPlungeRules.ActionId ||
            !EnemySlotRules.IsValidSlot(enemySlot) ||
            !expectedTarget.IsValid ||
            !HasValidNativeIdentity(localPlayer))
        {
            return null;
        }

        var target = EnemySlotResolver.Resolve(objectTable, enemySlot);
        if (!HasValidNativeIdentity(target) ||
            target!.GameObjectId != expectedTarget.GameObjectId ||
            target.EntityId != expectedTarget.EntityId)
        {
            return null;
        }

        var tableTarget = objectTable.SearchByEntityId(target.EntityId) as IPlayerCharacter;
        var exactCanonicalIdentity = HasSameNativeIdentity(target, tableTarget);
        var sourceObject = GetNativeObject(localPlayer);
        var targetObject = GetNativeObject(target);
        var actionManager = ActionManager.Instance();
        var nativePointersValid = sourceObject != null && targetObject != null;
        var targetActionReady = nativePointersValid &&
                                actionManager != null &&
                                actionManager->GetAdjustedActionId(
                                    DarkKnightPlungeRules.ActionId) == actionId &&
                                actionManager->GetActionStatus(
                                    ActionType.Action,
                                    actionId,
                                    expectedTarget.GameObjectId,
                                    checkRecastActive: true,
                                    checkCastingActive: true) == 0;
        var nativeRangeAndLineOfSight = nativePointersValid &&
                                        SeitonRangeRules.HasNativeRangeAndLineOfSight(
                                            ActionManager.GetActionInRangeOrLoS(
                                                actionId,
                                                sourceObject,
                                                targetObject));
        var centerDistanceSquared = Vector3.DistanceSquared(
            localPlayer.Position,
            target.Position);
        return new DarkKnightPlungeCandidate(
            enemySlot,
            expectedTarget,
            exactCanonicalIdentity,
            IsLivePlayer(target),
            target.IsTargetable,
            target.CurrentHp,
            target.MaxHp,
            centerDistanceSquared,
            DefensiveUtilityProbe.HasActiveGuard(target),
            targetActionReady,
            nativeRangeAndLineOfSight);
    }

    private unsafe bool TryUsePlungeOnce(
        IPlayerCharacter expectedLocalPlayer,
        TargetPressureActorIdentity expectedLocalIdentity,
        bool configurationEnabled,
        bool metadataVerified,
        bool higherPriorityClaimed,
        EmergencyActionInputFrame inputFrame,
        DarkKnightPlungeIntent intent,
        out bool attempted,
        out DarkKnightPlungeCandidate? boundaryCandidate)
    {
        attempted = false;
        boundaryCandidate = null;
        if (!intent.IsValid || !expectedLocalIdentity.IsValid) return false;

        var attemptedAtBoundary = false;
        DarkKnightPlungeCandidate? observedAtBoundary = null;
        try
        {
            return nearAssist.RunWithoutRedirect(() =>
            {
                var currentLocal = objectTable.LocalPlayer;
                if (!TryGetExactLiveIdentity(currentLocal, out var currentLocalIdentity) ||
                    currentLocalIdentity != expectedLocalIdentity ||
                    !HasSameNativeIdentity(expectedLocalPlayer, currentLocal) ||
                    !currentLocal!.IsTargetable ||
                    currentLocal.ClassJob.IsValid != true ||
                    currentLocal.ClassJob.RowId != DarkKnightPlungeRules.DarkKnightJobId ||
                    ResolveCurrentContext() != SupportedPvPContext.CrystallineConflict)
                {
                    return false;
                }

                var finalNow = Environment.TickCount64;
                var guardSuppressed = IsCurrentlySuppressedByGuard(currentLocal, finalNow);
                var nativeState = TryObserveNativeState(currentLocal, out var currentNativeState)
                    ? currentNativeState
                    : PlungeNativeState.Unknown;
                var structurallyReady = HasGlobalStructuralReadiness(currentLocal, nativeState);
                var exactHeldKeyStillDown = inputFrame.IsGameplayKeyPhysicallyDown(
                    (VirtualKey)intent.HeldKeyCode);
                var exactCandidate = ResolveFrozenIntent(
                    currentLocal,
                    intent,
                    nativeState.ResolvedActionId);
                if (exactCandidate is not { } frozenCandidate) return false;
                observedAtBoundary = frozenCandidate;

                if (!DarkKnightPlungeRules.CanUseExactIntent(
                        intent,
                        frozenCandidate,
                        currentLocalIdentity,
                        configurationEnabled,
                        isCrystallineConflict: true,
                        DarkKnightPlungeRules.DarkKnightJobId,
                        localAliveAndTargetable: true,
                        metadataVerified,
                        guardSuppressed,
                        higherPriorityClaimed,
                        inputFrame.Snapshot.ProbeSucceeded,
                        inputFrame.Snapshot.IsTextInputActive,
                        exactHeldKeyStillDown,
                        nativeState.ResolvedActionId,
                        nativeState.CooldownStateKnown,
                        nativeState.CooldownReady,
                        structurallyReady))
                {
                    return false;
                }

                // Threshold, center distance, canonical S-slot, native range/LoS,
                // target action status, and local readiness were all sampled again
                // immediately before this sole direct GOID request.
                var actionManager = ActionManager.Instance();
                if (actionManager == null) return false;
                attemptedAtBoundary = true;
                return actionManager->UseAction(
                    ActionType.Action,
                    intent.ActionId,
                    intent.Target.GameObjectId,
                    0,
                    ActionManager.UseActionMode.None,
                    0);
            });
        }
        finally
        {
            attempted = attemptedAtBoundary;
            boundaryCandidate = observedAtBoundary;
        }
    }

    private static unsafe bool TryObserveNativeState(
        IPlayerCharacter? localPlayer,
        out PlungeNativeState state)
    {
        state = PlungeNativeState.Unknown;
        if (!HasValidNativeIdentity(localPlayer)) return false;

        var actionManager = ActionManager.Instance();
        var sourceObject = GetNativeObject(localPlayer!);
        if (actionManager == null || sourceObject == null) return false;

        var adjustedActionId = actionManager->GetAdjustedActionId(
            DarkKnightPlungeRules.ActionId);
        if (adjustedActionId != DarkKnightPlungeRules.ActionId) return false;

        var recastGroup = actionManager->GetRecastGroup(
            (int)ActionType.Action,
            adjustedActionId);
        if (recastGroup != DarkKnightPlungeRules.ExpectedRuntimeRecastGroupIndex)
            return false;
        var recast = actionManager->GetRecastGroupDetail(recastGroup);
        if (recast == null ||
            actionManager->GetAdditionalRecastGroup(ActionType.Action, adjustedActionId) >= 0 ||
            ActionManager.GetAdjustedRecastTime(
                ActionType.Action,
                adjustedActionId,
                true) != DarkKnightPlungeRules.ExpectedAdjustedRecastMilliseconds)
        {
            return false;
        }

        state = new PlungeNativeState(
            adjustedActionId,
            CooldownStateKnown: true,
            CooldownReady: !recast->IsActive);
        return true;
    }

    private static unsafe bool HasGlobalStructuralReadiness(
        IPlayerCharacter localPlayer,
        PlungeNativeState nativeState)
    {
        if (!nativeState.CooldownStateKnown ||
            !nativeState.CooldownReady ||
            nativeState.ResolvedActionId != DarkKnightPlungeRules.ActionId ||
            HasActiveStatus(localPlayer, EnemyCombatConstants.PvPBindStatusId))
        {
            return false;
        }

        var actionManager = ActionManager.Instance();
        if (actionManager == null ||
            actionManager->GetAdjustedActionId(DarkKnightPlungeRules.ActionId) !=
            DarkKnightPlungeRules.ActionId ||
            !actionManager->IsActionOffCooldown(
                ActionType.Action,
                DarkKnightPlungeRules.ActionId) ||
            actionManager->CheckActionResources(
                ActionType.Action,
                DarkKnightPlungeRules.ActionId) != 0)
        {
            return false;
        }

        var animationLock = actionManager->AnimationLock;
        return float.IsFinite(animationLock) &&
               animationLock >= 0f &&
               animationLock <= AnimationLockEpsilonSeconds;
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

    private SupportedPvPContext ResolveCurrentContext()
    {
        var condition = dutyState.ContentFinderCondition;
        return PvPMatchRules.ResolveSupportedContext(
            clientState.IsPvP,
            clientState.IsPvPExcludingDen,
            includeWolvesDenTesting: false,
            clientState.TerritoryType,
            condition.IsValid,
            condition.IsValid && condition.Value.PvP,
            condition.IsValid ? condition.Value.ContentUICategory.RowId : 0,
            condition.IsValid && condition.Value.CrystallineConflictCasualRoulette,
            condition.IsValid && condition.Value.CrystallineConflictRankedRoulette);
    }

    private static unsafe bool TryGetExactLiveIdentity(
        IPlayerCharacter? player,
        out TargetPressureActorIdentity identity)
    {
        identity = default;
        if (!HasValidNativeIdentity(player) ||
            player!.IsDead ||
            player.CurrentHp == 0 ||
            player.MaxHp < player.CurrentHp)
        {
            return false;
        }

        var native = GetNativeObject(player);
        if (native == null) return false;
        identity = new TargetPressureActorIdentity(player.GameObjectId, player.EntityId);
        return identity.IsValid;
    }

    private static unsafe GameObject* GetNativeObject(IPlayerCharacter? player)
    {
        if (!HasValidNativeIdentity(player)) return null;
        var native = (GameObject*)player!.Address;
        return native != null && native->EntityId == player.EntityId
            ? native
            : null;
    }

    private static bool IsLivePlayer(IPlayerCharacter? player) =>
        player is not null &&
        !player.IsDead &&
        player.CurrentHp > 0 &&
        player.MaxHp >= player.CurrentHp;

    private static bool HasValidNativeIdentity(IPlayerCharacter? player) =>
        player is not null &&
        player.Address != 0 &&
        IsNetworkEntityId(player.EntityId) &&
        IsNetworkObjectId(player.GameObjectId);

    private static bool HasSameNativeIdentity(
        IPlayerCharacter? left,
        IPlayerCharacter? right) =>
        HasValidNativeIdentity(left) &&
        HasValidNativeIdentity(right) &&
        left!.Address == right!.Address &&
        left.GameObjectId == right.GameObjectId &&
        left.EntityId == right.EntityId;

    private static bool IsNetworkEntityId(uint entityId) =>
        entityId is not 0 and not 0xE0000000 and not uint.MaxValue;

    private static bool IsNetworkObjectId(ulong gameObjectId) =>
        gameObjectId is not 0 and not 0xE0000000UL and not ulong.MaxValue;

    private static bool HasActiveStatus(IPlayerCharacter player, uint statusId)
    {
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

    private void LogAttemptFailure(Exception exception, long nowMilliseconds)
    {
        if (nowMilliseconds < nextErrorLogAt) return;
        nextErrorLogAt = nowMilliseconds + 10_000;
        log.Error(
            exception,
            "Seiton Sense DRK Plunge attempt failed; the held readiness epoch will not be retried.");
    }

    private readonly record struct PlungeNativeState(
        uint ResolvedActionId,
        bool CooldownStateKnown,
        bool CooldownReady)
    {
        internal static PlungeNativeState Unknown => new(0, false, false);
    }
}
