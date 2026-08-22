using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using SeitonSense.Core;

namespace SeitonSense.Plugin.Services;

internal sealed record NinjaSeitonDispatchProbeSnapshot(
    NinjaSeitonDispatchDecisionKind Decision,
    NinjaSeitonDispatchDecisionReason Reason,
    uint ResolvedActionId,
    int CandidateCount,
    int EnemySlot,
    ulong TargetGameObjectId,
    uint TargetEntityId,
    uint RevalidatedCurrentHp,
    uint RevalidatedMaximumHp,
    uint ExecuteBlockingStatusId,
    bool BoundaryThresholdRevalidated,
    bool ThresholdDriftCancelled,
    bool ProtectionDriftCancelled,
    bool LocallyReady,
    VirtualKey FreshGameplayKey,
    bool InputClaimed,
    HeldCastCancellationRequest? CastCancellationRequest,
    bool UseActionAttempted,
    bool UseActionAccepted,
    long AttemptCount,
    long AcceptedCount,
    long ThresholdDriftCancellationCount,
    long ProtectionDriftCancellationCount,
    string CandidateResolution,
    string LastEvent)
{
    internal static NinjaSeitonDispatchProbeSnapshot Initial { get; } = new(
        NinjaSeitonDispatchDecisionKind.None,
        NinjaSeitonDispatchDecisionReason.None,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        false,
        false,
        false,
        false,
        VirtualKey.NO_KEY,
        false,
        null,
        false,
        false,
        0,
        0,
        0,
        0,
        "Not evaluated",
        "Waiting");
}

/// <summary>
/// Converts a held physical gameplay-key episode into bounded exact Seiton
/// requests. A client-accepted base action may expose one separately adjusted
/// follow-up epoch; every frozen action/actor retry remains exact.
/// </summary>
internal sealed class NinjaSeitonDispatchProbe
{
    private readonly IObjectTable objectTable;
    private readonly ExecuteTracker executeTracker;
    private readonly NearAssistRedirector nearAssist;
    private readonly IPluginLog log;
    private NinjaSeitonDispatchProbeSnapshot snapshot = NinjaSeitonDispatchProbeSnapshot.Initial;
    private NinjaSeitonAcceptedHoldState acceptedHold = NinjaSeitonAcceptedHoldState.Initial;
    private FrozenSeitonRetry? frozenRetry;
    private VirtualKey terminalHeldKey = VirtualKey.NO_KEY;
    private long attemptCount;
    private long acceptedCount;
    private long thresholdDriftCancellationCount;
    private long protectionDriftCancellationCount;
    private long frozenIntentEpochToken;
    private long nextErrorLogAt;
    private string lastEvent = "Waiting";

    internal NinjaSeitonDispatchProbe(
        IObjectTable objectTable,
        ExecuteTracker executeTracker,
        NearAssistRedirector nearAssist,
        IPluginLog log)
    {
        this.objectTable = objectTable;
        this.executeTracker = executeTracker;
        this.nearAssist = nearAssist;
        this.log = log;
    }

    internal NinjaSeitonDispatchProbeSnapshot Snapshot => Volatile.Read(ref snapshot);

    internal NinjaSeitonDispatchProbeSnapshot Observe(
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
        if (hardReset)
        {
            acceptedHold = NinjaSeitonAcceptedHoldState.Initial;
            frozenRetry = null;
            terminalHeldKey = VirtualKey.NO_KEY;
        }

        if (terminalHeldKey != VirtualKey.NO_KEY &&
            !inputFrame.IsGameplayKeyPhysicallyDown(terminalHeldKey))
        {
            terminalHeldKey = VirtualKey.NO_KEY;
        }

        var localAlive = IsLivePlayer(localPlayer);
        var localIdentity = HasValidNativeIdentity(localPlayer)
            ? new TargetPressureActorIdentity(localPlayer!.GameObjectId, localPlayer.EntityId)
            : default;
        var localJobId = localPlayer?.ClassJob.IsValid == true
            ? localPlayer.ClassJob.RowId
            : 0;
        var featureContextReady = configurationEnabled &&
                                  isCrystallineConflict &&
                                  localAlive &&
                                  ExecuteThreshold.IsNinja(localJobId) &&
                                  metadataVerified &&
                                  !actionHelpersSuppressedByGuard &&
                                  !hardReset;
        var resolvedActionId = 0u;
        var actionLocallyReady = featureContextReady &&
                          localIdentity.IsValid &&
                          SeitonReadinessProbe.TryGetReadyAction(localPlayer!, out resolvedActionId) &&
                          IsActionResourceReady(resolvedActionId);
        var nearQueueable = actionLocallyReady && IsNativeBoundaryNearQueueable(localPlayer!);

        var input = inputFrame.Snapshot;
        var acceptedKey = acceptedHold.OwnsHold
            ? (VirtualKey)acceptedHold.HeldKeyCode
            : VirtualKey.NO_KEY;
        var exactAcceptedKeyDown = acceptedHold.OwnsHold &&
                                   inputFrame.IsGameplayKeyPhysicallyDown(acceptedKey);
        acceptedHold = NinjaSeitonDispatchRules.ObserveAcceptedHold(
            acceptedHold,
            hardReset,
            featureContextReady && input.ProbeSucceeded && !input.IsTextInputActive,
            exactAcceptedKeyDown);
        acceptedKey = acceptedHold.OwnsHold
            ? (VirtualKey)acceptedHold.HeldKeyCode
            : VirtualKey.NO_KEY;
        var hasHeldEpoch = acceptedHold.OwnsHold
            ? NinjaSeitonDispatchRules.CanOpenAdjustedActionEpoch(
                acceptedHold,
                resolvedActionId)
            : inputFrame.HeldGameplayKeyEligible;
        var shouldResolveCandidates = frozenRetry is null &&
                                      terminalHeldKey == VirtualKey.NO_KEY &&
                                      actionLocallyReady &&
                                      !higherPriorityClaimed &&
                                      input.ProbeSucceeded &&
                                      !input.IsTextInputActive &&
                                      hasHeldEpoch;
        var candidateResolution = "Not evaluated: no eligible held action epoch";
        var candidates = shouldResolveCandidates
            ? ResolveExactCandidates(localPlayer!, resolvedActionId, out candidateResolution)
            : [];
        var decision = NinjaSeitonDispatchRules.Observe(
            new NinjaSeitonDispatchObservation(
                configurationEnabled,
                isCrystallineConflict,
                localJobId,
                localIdentity,
                localAlive,
                metadataVerified,
                actionHelpersSuppressedByGuard,
                higherPriorityClaimed,
                input.ProbeSucceeded,
                input.IsTextInputActive,
                hasHeldEpoch,
                resolvedActionId,
                actionLocallyReady,
                candidates,
                hardReset));

        var inputClaimed = false;
        HeldCastCancellationRequest? castCancellationRequest = null;
        var attempted = false;
        var accepted = false;
        var revalidatedCurrentHp = 0u;
        var revalidatedMaximumHp = 0u;
        var boundaryThresholdRevalidated = false;
        var thresholdDriftCancelled = false;
        var protectionDriftCancelled = false;
        var observedBlockingStatusId = 0u;
        NinjaSeitonDispatchCandidate? observedCandidate = null;
        if (frozenRetry is { } retry)
        {
            var exactBaseContext = featureContextReady &&
                                   localIdentity == retry.LocalPlayer &&
                                   input.ProbeSucceeded &&
                                   !input.IsTextInputActive &&
                                   inputFrame.IsGameplayKeyPhysicallyDown(retry.HeldKey);
            if (!exactBaseContext)
            {
                SpendFrozenEpisode(retry, latchCircuitBreaker: false);
                lastEvent = $"S{retry.Intent.EnemySlot} frozen Seiton retry cancelled by exact context/key drift";
            }
            else
            {
                var finalActionReady = SeitonReadinessProbe.TryGetReadyAction(
                    localPlayer!,
                    out var finalResolvedActionId) &&
                    IsActionResourceReady(finalResolvedActionId);
                var finalNearQueueable = finalActionReady &&
                                         IsNativeBoundaryNearQueueable(localPlayer!);
                if (finalActionReady && finalResolvedActionId != retry.Intent.ActionId)
                {
                    SpendFrozenEpisode(retry, latchCircuitBreaker: false);
                    lastEvent = $"S{retry.Intent.EnemySlot} frozen action changed from {retry.Intent.ActionId} to {finalResolvedActionId}";
                }
                else if (finalActionReady)
                {
                    var finalCandidate = ResolveFrozenIntent(
                        localPlayer!,
                        retry.Intent,
                        finalResolvedActionId);
                    observedCandidate = finalCandidate;
                    if (finalCandidate is { } finalObserved)
                    {
                        revalidatedCurrentHp = finalObserved.CurrentHp;
                        revalidatedMaximumHp = finalObserved.MaximumHp;
                    }

                    if (finalCandidate is not { } exactCandidate ||
                        !NinjaSeitonDispatchRules.CanUseExactIntent(
                            retry.Intent,
                            exactCandidate,
                            localIdentity,
                            finalResolvedActionId,
                            finalActionReady))
                    {
                        protectionDriftCancelled =
                            finalCandidate is { HasExecuteBlockingProtection: true };
                        thresholdDriftCancelled =
                            !protectionDriftCancelled &&
                            finalCandidate is { } thresholdCandidate &&
                            IsValidAtOrAboveHalf(thresholdCandidate);
                        if (protectionDriftCancelled)
                            observedBlockingStatusId =
                                finalCandidate!.Value.ExecuteBlockingStatusId;
                        SpendFrozenEpisode(retry, latchCircuitBreaker: false);
                        lastEvent = protectionDriftCancelled
                            ? $"S{retry.Intent.EnemySlot} frozen Seiton cancelled by protection status {finalCandidate!.Value.ExecuteBlockingStatusId}"
                            : $"S{retry.Intent.EnemySlot} frozen Seiton retry cancelled by exact target/range/threshold drift";
                    }
                    else
                    {
                        var retainsSchedulerFrame =
                            HeldActionRetryRules.RetainsSchedulerFrame(
                                retry.Retry,
                                nowMilliseconds,
                                exactIntentValid: true,
                                actionSpecificReady: true,
                                targetSpecificReady: true);
                        if (!higherPriorityClaimed &&
                            !inputFrame.IsConsumed &&
                            retainsSchedulerFrame)
                        {
                            inputClaimed = true;
                            inputFrame.Consume();
                            if (!finalNearQueueable)
                            {
                                castCancellationRequest = CreateCastCancellationRequest(
                                    localPlayer!,
                                    retry,
                                    out var castBlockingStatusId);
                                if (castBlockingStatusId != 0)
                                {
                                    protectionDriftCancelled = true;
                                    observedBlockingStatusId = castBlockingStatusId;
                                    SpendFrozenEpisode(retry, latchCircuitBreaker: false);
                                    lastEvent = $"S{retry.Intent.EnemySlot} frozen Seiton protection {castBlockingStatusId} blocked cast cancellation";
                                }
                                else
                                {
                                    lastEvent = $"S{retry.Intent.EnemySlot} frozen Seiton waiting for global native boundary";
                                }
                            }
                            else if (!HeldActionRetryRules.CanAttemptFrozenIntent(
                                         retry.Retry,
                                         nowMilliseconds))
                            {
                                lastEvent = $"S{retry.Intent.EnemySlot} frozen Seiton retaining retry throttle priority";
                            }
                            else
                            {
                                var outcome = TryUseSeitonOnce(
                                    localPlayer!,
                                    retry.Intent,
                                    out attempted,
                                    out var boundaryCandidate,
                                    out boundaryThresholdRevalidated,
                                    out thresholdDriftCancelled,
                                    out protectionDriftCancelled);
                                observedCandidate = boundaryCandidate ?? observedCandidate;
                                if (boundaryCandidate is { } observedBoundaryCandidate)
                                {
                                    revalidatedCurrentHp = observedBoundaryCandidate.CurrentHp;
                                    revalidatedMaximumHp = observedBoundaryCandidate.MaximumHp;
                                    if (observedBoundaryCandidate.HasExecuteBlockingProtection)
                                    {
                                        observedBlockingStatusId =
                                            observedBoundaryCandidate.ExecuteBlockingStatusId;
                                    }
                                }

                                accepted = outcome == ClientActionAttemptOutcome.ClientAccepted;
                                CompleteAttempt(retry, outcome, nowMilliseconds);
                                lastEvent = protectionDriftCancelled
                                    ? $"S{retry.Intent.EnemySlot} protection status {observedBlockingStatusId} blocked native Seiton"
                                    : DescribeAttempt(
                                        retry.Intent,
                                        retry.Retry.NativeAttemptCount + 1,
                                        outcome);
                            }
                        }
                    }
                }
            }
        }
        else if (terminalHeldKey == VirtualKey.NO_KEY &&
                 decision.ShouldDispatch &&
                 decision.Intent is { } intent)
        {
            var heldKey = acceptedHold.OwnsHold
                ? (VirtualKey)acceptedHold.HeldKeyCode
                : input.HeldGameplayKey;
            var retryIntent = new FrozenSeitonRetry(
                intent,
                localIdentity,
                heldKey,
                NextIntentEpochToken(),
                HeldActionRetryState.Initial);
            inputClaimed = true;
            inputFrame.Consume();
            var outcome = TryUseSeitonOnce(
                localPlayer!,
                intent,
                out attempted,
                out var boundaryCandidate,
                out boundaryThresholdRevalidated,
                out thresholdDriftCancelled,
                out protectionDriftCancelled);
            observedCandidate = boundaryCandidate;
            if (boundaryCandidate is { } observedBoundaryCandidate)
            {
                revalidatedCurrentHp = observedBoundaryCandidate.CurrentHp;
                revalidatedMaximumHp = observedBoundaryCandidate.MaximumHp;
            }

            accepted = outcome == ClientActionAttemptOutcome.ClientAccepted;
            CompleteAttempt(retryIntent, outcome, nowMilliseconds);
            if (outcome == ClientActionAttemptOutcome.SoftUnavailable)
            {
                castCancellationRequest = CreateCastCancellationRequest(
                    localPlayer!,
                    retryIntent,
                    out var castBlockingStatusId);
                if (castBlockingStatusId != 0)
                {
                    protectionDriftCancelled = true;
                    observedBlockingStatusId = castBlockingStatusId;
                    SpendFrozenEpisode(retryIntent, latchCircuitBreaker: false);
                }
            }

            lastEvent = protectionDriftCancelled
                ? $"S{intent.EnemySlot} protection status {observedBlockingStatusId} blocked Seiton"
                : DescribeAttempt(intent, 1, outcome);
        }

        if (attempted) Interlocked.Increment(ref attemptCount);
        if (accepted) Interlocked.Increment(ref acceptedCount);

        if (thresholdDriftCancelled)
            Interlocked.Increment(ref thresholdDriftCancellationCount);
        if (protectionDriftCancelled)
            Interlocked.Increment(ref protectionDriftCancellationCount);

        var selectedCandidate = observedCandidate ?? (decision.SelectedCandidateIndex >= 0 &&
                                decision.SelectedCandidateIndex < candidates.Count
            ? candidates[decision.SelectedCandidateIndex]
            : (NinjaSeitonDispatchCandidate?)null);
        var result = new NinjaSeitonDispatchProbeSnapshot(
            decision.Kind,
            decision.Reason,
            resolvedActionId,
            candidates.Count,
            selectedCandidate?.EnemySlot ?? 0,
            selectedCandidate?.Actor.GameObjectId ?? 0,
            selectedCandidate?.Actor.EntityId ?? 0,
            revalidatedCurrentHp,
            revalidatedMaximumHp,
            observedBlockingStatusId != 0
                ? observedBlockingStatusId
                : selectedCandidate?.ExecuteBlockingStatusId ?? 0,
            boundaryThresholdRevalidated,
            thresholdDriftCancelled,
            protectionDriftCancelled,
            actionLocallyReady,
            frozenRetry?.HeldKey ??
            (acceptedHold.OwnsHold ? (VirtualKey)acceptedHold.HeldKeyCode : input.HeldGameplayKey),
            inputClaimed,
            castCancellationRequest,
            attempted,
            accepted,
            Interlocked.Read(ref attemptCount),
            Interlocked.Read(ref acceptedCount),
            Interlocked.Read(ref thresholdDriftCancellationCount),
            Interlocked.Read(ref protectionDriftCancellationCount),
            candidateResolution,
            lastEvent);
        Volatile.Write(ref snapshot, result);
        return result;
    }

    internal void Reset()
    {
        acceptedHold = NinjaSeitonAcceptedHoldState.Initial;
        frozenRetry = null;
        terminalHeldKey = VirtualKey.NO_KEY;
        lastEvent = "Reset";
        Volatile.Write(ref snapshot, NinjaSeitonDispatchProbeSnapshot.Initial with
        {
            AttemptCount = Interlocked.Read(ref attemptCount),
            AcceptedCount = Interlocked.Read(ref acceptedCount),
            ThresholdDriftCancellationCount =
                Interlocked.Read(ref thresholdDriftCancellationCount),
            ProtectionDriftCancellationCount =
                Interlocked.Read(ref protectionDriftCancellationCount),
            LastEvent = lastEvent,
        });
    }

    internal NinjaSeitonDispatchProbeSnapshot FailClosed()
    {
        var failedKey = frozenRetry?.HeldKey ??
                        (acceptedHold.OwnsHold
                            ? (VirtualKey)acceptedHold.HeldKeyCode
                            : terminalHeldKey);
        acceptedHold = NinjaSeitonAcceptedHoldState.Initial;
        frozenRetry = null;
        terminalHeldKey = failedKey;
        lastEvent = "Failed closed";
        var result = NinjaSeitonDispatchProbeSnapshot.Initial with
        {
            Decision = NinjaSeitonDispatchDecisionKind.Cancelled,
            Reason = NinjaSeitonDispatchDecisionReason.HardReset,
            AttemptCount = Interlocked.Read(ref attemptCount),
            AcceptedCount = Interlocked.Read(ref acceptedCount),
            ThresholdDriftCancellationCount =
                Interlocked.Read(ref thresholdDriftCancellationCount),
            ProtectionDriftCancellationCount =
                Interlocked.Read(ref protectionDriftCancellationCount),
            LastEvent = lastEvent,
        };
        Volatile.Write(ref snapshot, result);
        return result;
    }

    private IReadOnlyList<NinjaSeitonDispatchCandidate> ResolveExactCandidates(
        IPlayerCharacter localPlayer,
        uint actionId,
        out string resolution)
    {
        var diagnosticsBefore = executeTracker.Diagnostics;
        if (!diagnosticsBefore.Active ||
            !diagnosticsBefore.IsCrystallineConflict ||
            !diagnosticsBefore.SeitonMetadataVerified)
        {
            resolution = "Tracker context unavailable";
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
                snapshotEnemy.GameObjectId is 0 or 0xE0000000 ||
                snapshotEnemy.EntityId is 0 or 0xE0000000 ||
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
            if (tablePlayer is null ||
                tablePlayer.Address != player.Address ||
                tablePlayer.GameObjectId != player.GameObjectId ||
                tablePlayer.EntityId != player.EntityId)
            {
                resolution = $"Native S{slot} object-table identity mismatch";
                return [];
            }

            if (!seenGameObjectIds.Add(player.GameObjectId) ||
                !seenEntityIds.Add(player.EntityId) ||
                !seenAddresses.Add(player.Address))
            {
                resolution = "Native e1-e5 identities duplicate";
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

        var candidates = new List<NinjaSeitonDispatchCandidate>(eligibleCurrentSlots.Length);
        foreach (var (slot, player) in eligibleCurrentSlots)
        {
            if (!snapshotsBySlot.TryGetValue(slot, out var snapshotEnemy) ||
                snapshotEnemy.GameObjectId != player.GameObjectId ||
                snapshotEnemy.EntityId != player.EntityId)
            {
                resolution = $"Tracker/native S{slot} identity mismatch";
                return [];
            }

            var expectedTarget = new TargetPressureActorIdentity(
                player.GameObjectId,
                player.EntityId);
            var candidate = BuildExactSlotCandidate(
                localPlayer,
                actionId,
                slot,
                expectedTarget);
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
            if (!HasValidNativeIdentity(stablePlayer) ||
                stablePlayer!.Address != player.Address ||
                stablePlayer.GameObjectId != player.GameObjectId ||
                stablePlayer.EntityId != player.EntityId)
            {
                resolution = $"Native S{slot} changed during capture";
                return [];
            }
        }

        var protectedCandidates = candidates.Count(
            static candidate => candidate.HasExecuteBlockingProtection);
        resolution =
            $"Exact coherent set: {candidates.Count} candidates, protected={protectedCandidates}";
        return candidates;
    }

    private NinjaSeitonDispatchCandidate? ResolveFrozenIntent(
        IPlayerCharacter localPlayer,
        NinjaSeitonDispatchIntent intent,
        uint actionId) =>
        BuildExactSlotCandidate(
            localPlayer,
            actionId,
            intent.EnemySlot,
            intent.Target);

    private unsafe NinjaSeitonDispatchCandidate? BuildExactSlotCandidate(
        IPlayerCharacter localPlayer,
        uint actionId,
        int enemySlot,
        TargetPressureActorIdentity expectedTarget)
    {
        if (!NinjaSeitonDispatchRules.IsExactSeitonAction(actionId) ||
            !EnemySlotRules.IsValidSlot(enemySlot) ||
            !expectedTarget.IsValid)
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
        var exactCanonicalIdentity = tableTarget is not null &&
                                     tableTarget.Address == target.Address &&
                                     tableTarget.GameObjectId == target.GameObjectId &&
                                     tableTarget.EntityId == target.EntityId;
        var sourceObject = GetNativeObject(localPlayer);
        var targetObject = GetNativeObject(target);
        var validActionTarget = sourceObject != null && targetObject != null;
        var rangeAndLineOfSight = validActionTarget &&
                                  SeitonReadinessProbe.HasRangeAndLineOfSight(
                                      localPlayer,
                                      target,
                                      actionId,
                                      out _);
        NinjaSeitonProtectionProbe.TryFindExecuteBlockingStatus(
            target,
            out var executeBlockingStatusId,
            out _);
        return new NinjaSeitonDispatchCandidate(
            enemySlot,
            expectedTarget,
            exactCanonicalIdentity,
            IsLivePlayer(target),
            target.IsTargetable,
            target.CurrentHp,
            target.MaxHp,
            executeBlockingStatusId,
            validActionTarget,
            rangeAndLineOfSight);
    }

    private unsafe ClientActionAttemptOutcome TryUseSeitonOnce(
        IPlayerCharacter localPlayer,
        NinjaSeitonDispatchIntent intent,
        out bool attempted,
        out NinjaSeitonDispatchCandidate? boundaryCandidate,
        out bool boundaryThresholdRevalidated,
        out bool thresholdDriftCancelled,
        out bool protectionDriftCancelled)
    {
        attempted = false;
        boundaryCandidate = null;
        boundaryThresholdRevalidated = false;
        thresholdDriftCancelled = false;
        protectionDriftCancelled = false;
        if (!HasValidNativeIdentity(localPlayer) ||
            !intent.IsValid)
        {
            return ClientActionAttemptOutcome.NotInvoked;
        }

        var actionManager = ActionManager.Instance();
        if (actionManager == null) return ClientActionAttemptOutcome.NotInvoked;

        var attemptedAtBoundary = false;
        var softUnavailableAtBoundary = false;
        NinjaSeitonDispatchCandidate? candidateAtBoundary = null;
        var thresholdRevalidatedAtBoundary = false;
        var thresholdDriftAtBoundary = false;
        var protectionDriftAtBoundary = false;
        var boundaryBefore = default(ClientActionAttemptFingerprint);
        var boundaryAfter = default(ClientActionAttemptFingerprint);
        try
        {
            var accepted = nearAssist.RunWithoutRedirect(() =>
            {
                if (!HasValidNativeIdentity(localPlayer))
                {
                    return false;
                }

                var ready = SeitonReadinessProbe.TryGetReadyAction(
                    localPlayer,
                    out var resolvedActionId) &&
                    IsActionResourceReady(resolvedActionId);
                if (resolvedActionId != intent.ActionId) return false;
                if (!ready || !IsNativeBoundaryNearQueueable(localPlayer))
                {
                    softUnavailableAtBoundary = true;
                    return false;
                }

                var currentLocalIdentity = new TargetPressureActorIdentity(
                    localPlayer.GameObjectId,
                    localPlayer.EntityId);
                var exactCandidate = ResolveFrozenIntent(
                    localPlayer,
                    intent,
                    resolvedActionId);
                if (exactCandidate is not { } frozenCandidate)
                    return false;

                candidateAtBoundary = frozenCandidate;
                if (!NinjaSeitonDispatchRules.CanUseExactIntent(
                        intent,
                        frozenCandidate,
                        currentLocalIdentity,
                        resolvedActionId,
                        actionLocallyReady: true))
                {
                    protectionDriftAtBoundary =
                        frozenCandidate.HasExecuteBlockingProtection;
                    thresholdDriftAtBoundary =
                        !protectionDriftAtBoundary &&
                        IsValidAtOrAboveHalf(frozenCandidate);
                    return false;
                }

                // HP is deliberately read once more after every other exact
                // preflight, leaving only the unavoidable client-call/server
                // execution race after this strict sub-50 check.
                var thresholdResult = ReadFrozenThresholdAtUseActionBoundary(
                    intent,
                    out var currentHp,
                    out var maximumHp,
                    out var executeBlockingStatusId);
                candidateAtBoundary = frozenCandidate with
                {
                    CurrentHp = currentHp,
                    MaximumHp = maximumHp,
                    ExecuteBlockingStatusId = executeBlockingStatusId,
                };
                if (thresholdResult == BoundaryThresholdResult.Protected)
                {
                    protectionDriftAtBoundary = true;
                    return false;
                }

                if (thresholdResult != BoundaryThresholdResult.BelowHalf)
                {
                    thresholdDriftAtBoundary =
                        thresholdResult == BoundaryThresholdResult.AtOrAboveHalf;
                    return false;
                }

                thresholdRevalidatedAtBoundary = true;
                boundaryBefore = ClientActionAttemptBoundary.Capture(
                    actionManager,
                    intent.ActionId);
                attemptedAtBoundary = true;
                var clientAccepted = actionManager->UseAction(
                    ActionType.Action,
                    intent.ActionId,
                    intent.Target.GameObjectId,
                    0,
                    ActionManager.UseActionMode.None,
                    0);
                boundaryAfter = ClientActionAttemptBoundary.Capture(
                    actionManager,
                    intent.ActionId);
                return clientAccepted;
            });
            return attemptedAtBoundary
                ? ClientActionAttemptBoundaryRules.Classify(
                    accepted,
                    intent.ActionId,
                    boundaryBefore,
                    boundaryAfter)
                : softUnavailableAtBoundary
                    ? ClientActionAttemptOutcome.SoftUnavailable
                    : ClientActionAttemptOutcome.NotInvoked;
        }
        catch (Exception exception)
        {
            LogAttemptFailure(exception, Environment.TickCount64);
            return attemptedAtBoundary
                ? ClientActionAttemptOutcome.AcceptanceUnknown
                : softUnavailableAtBoundary
                    ? ClientActionAttemptOutcome.SoftUnavailable
                    : ClientActionAttemptOutcome.NotInvoked;
        }
        finally
        {
            attempted = attemptedAtBoundary;
            boundaryCandidate = candidateAtBoundary;
            boundaryThresholdRevalidated = thresholdRevalidatedAtBoundary;
            thresholdDriftCancelled = thresholdDriftAtBoundary;
            protectionDriftCancelled = protectionDriftAtBoundary;
        }
    }

    private BoundaryThresholdResult ReadFrozenThresholdAtUseActionBoundary(
        NinjaSeitonDispatchIntent intent,
        out uint currentHp,
        out uint maximumHp,
        out uint executeBlockingStatusId)
    {
        currentHp = 0;
        maximumHp = 0;
        executeBlockingStatusId = 0;
        var target = EnemySlotResolver.Resolve(objectTable, intent.EnemySlot);
        if (!HasValidNativeIdentity(target) ||
            target!.GameObjectId != intent.Target.GameObjectId ||
            target.EntityId != intent.Target.EntityId)
        {
            return BoundaryThresholdResult.Unresolved;
        }

        var tableTarget = objectTable.SearchByEntityId(target.EntityId) as IPlayerCharacter;
        if (tableTarget is null ||
            tableTarget.Address != target.Address ||
            tableTarget.GameObjectId != target.GameObjectId ||
            tableTarget.EntityId != target.EntityId)
        {
            return BoundaryThresholdResult.Unresolved;
        }

        currentHp = target.CurrentHp;
        maximumHp = target.MaxHp;
        if (target.IsDead ||
            !target.IsTargetable ||
            !ExecuteThreshold.HasValidHp(currentHp, maximumHp))
        {
            return BoundaryThresholdResult.InvalidTarget;
        }

        if (NinjaSeitonProtectionProbe.TryFindExecuteBlockingStatus(
                target,
                out executeBlockingStatusId,
                out _))
        {
            return BoundaryThresholdResult.Protected;
        }

        return ExecuteThreshold.IsBelowHalf(currentHp, maximumHp)
            ? BoundaryThresholdResult.BelowHalf
            : BoundaryThresholdResult.AtOrAboveHalf;
    }

    private static bool IsValidAtOrAboveHalf(
        NinjaSeitonDispatchCandidate candidate) =>
        ExecuteThreshold.HasValidHp(candidate.CurrentHp, candidate.MaximumHp) &&
        !ExecuteThreshold.IsBelowHalf(candidate.CurrentHp, candidate.MaximumHp);

    private static unsafe bool IsNativeBoundaryNearQueueable(IPlayerCharacter localPlayer)
    {
        var actionManager = ActionManager.Instance();
        return actionManager != null &&
               HeldActionRetryRules.IsNativeBoundaryNearQueueable(
                   actionManager->AnimationLock,
                   localPlayer.IsCasting,
                   actionManager->CastActionId,
                   actionManager->ActionQueued);
    }

    private static unsafe bool IsCastCancellationBoundaryReady(
        IPlayerCharacter localPlayer)
    {
        var actionManager = ActionManager.Instance();
        return actionManager != null &&
               localPlayer.IsCasting &&
               actionManager->CastActionId != 0 &&
               !actionManager->ActionQueued &&
               float.IsFinite(actionManager->AnimationLock) &&
               actionManager->AnimationLock >= 0f &&
               actionManager->AnimationLock <=
               HeldCastCancellationRules.MaximumCancellationAnimationLockSeconds;
    }

    private HeldCastCancellationRequest? CreateCastCancellationRequest(
        IPlayerCharacter localPlayer,
        FrozenSeitonRetry frozen,
        out uint executeBlockingStatusId)
    {
        executeBlockingStatusId = 0;
        if (!IsCastCancellationBoundaryReady(localPlayer) ||
            !HasValidNativeIdentity(localPlayer))
        {
            return null;
        }

        var currentLocalIdentity = new TargetPressureActorIdentity(
            localPlayer.GameObjectId,
            localPlayer.EntityId);
        if (currentLocalIdentity != frozen.LocalPlayer ||
            !SeitonReadinessProbe.TryGetReadyAction(
                localPlayer,
                out var resolvedActionId) ||
            resolvedActionId != frozen.Intent.ActionId ||
            !IsActionResourceReady(resolvedActionId))
        {
            return null;
        }

        var exactCandidate = ResolveFrozenIntent(
            localPlayer,
            frozen.Intent,
            resolvedActionId);
        if (exactCandidate is not { } candidate) return null;
        if (candidate.HasExecuteBlockingProtection)
        {
            executeBlockingStatusId = candidate.ExecuteBlockingStatusId;
            return null;
        }

        if (!NinjaSeitonDispatchRules.CanUseExactIntent(
                frozen.Intent,
                candidate,
                currentLocalIdentity,
                resolvedActionId,
                actionLocallyReady: true))
        {
            return null;
        }

        var request = new HeldCastCancellationRequest(
            HeldCastCancellationHelperKind.NinjaSeiton,
            frozen.Intent.ActionId,
            frozen.LocalPlayer,
            frozen.Intent.Target,
            (int)frozen.HeldKey,
            frozen.IntentEpochToken);
        return request.IsValid ? request : null;
    }

    private ulong NextIntentEpochToken()
    {
        while (true)
        {
            var current = Volatile.Read(ref frozenIntentEpochToken);
            var next = current >= long.MaxValue ? 1 : current + 1;
            if (Interlocked.CompareExchange(
                    ref frozenIntentEpochToken,
                    next,
                    current) == current)
            {
                return (ulong)next;
            }
        }
    }

    private static unsafe bool IsActionResourceReady(uint actionId)
    {
        if (actionId is not (SeitonReadinessProbe.BaseActionId or
            SeitonReadinessProbe.FollowUpActionId))
        {
            return false;
        }

        var actionManager = ActionManager.Instance();
        return actionManager != null &&
               actionManager->CheckActionResources(ActionType.Action, actionId) == 0;
    }

    private enum BoundaryThresholdResult
    {
        Unresolved = 0,
        InvalidTarget = 1,
        AtOrAboveHalf = 2,
        BelowHalf = 3,
        Protected = 4,
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
        player.EntityId is not 0 and not 0xE0000000 &&
        player.GameObjectId is not 0 and not 0xE0000000;

    private void CompleteAttempt(
        FrozenSeitonRetry frozen,
        ClientActionAttemptOutcome outcome,
        long nowMilliseconds)
    {
        var completion = HeldActionRetryRules.Complete(
            frozen.Retry,
            Math.Max(0, nowMilliseconds),
            outcome);
        if (completion.RetryScheduled ||
            completion.Disposition == HeldActionRetryDisposition.SoftWait)
        {
            frozenRetry = frozen with { Retry = completion.NextState };
            return;
        }

        frozenRetry = null;
        if (outcome == ClientActionAttemptOutcome.ClientAccepted)
        {
            acceptedHold = NinjaSeitonDispatchRules.BeginAcceptedHold(
                (int)frozen.HeldKey,
                frozen.Intent.ActionId);
            return;
        }

        SpendFrozenEpisode(
            frozen,
            HeldActionRetryRules.ShouldLatchHeldKeyUntilRelease(
                completion.Disposition));
    }

    private void SpendFrozenEpisode(
        FrozenSeitonRetry frozen,
        bool latchCircuitBreaker)
    {
        frozenRetry = null;
        acceptedHold = NinjaSeitonDispatchRules.RetireAdjustedActionEpoch(
            acceptedHold,
            frozen.Intent.ActionId);
        if (latchCircuitBreaker)
            terminalHeldKey = frozen.HeldKey;
    }

    private static string DescribeAttempt(
        NinjaSeitonDispatchIntent intent,
        int attempt,
        ClientActionAttemptOutcome outcome) =>
        $"S{intent.EnemySlot} action {intent.ActionId} attempt " +
        $"{attempt}/{HeldActionRetryRules.MaximumNativeAttempts}: {outcome}";

    private void LogAttemptFailure(Exception exception, long nowMilliseconds)
    {
        if (nowMilliseconds < nextErrorLogAt) return;
        nextErrorLogAt = nowMilliseconds + 10_000;
        log.Error(exception, "Seiton Sense Ninja Seiton attempt ended with ambiguous acceptance.");
    }

    private readonly record struct FrozenSeitonRetry(
        NinjaSeitonDispatchIntent Intent,
        TargetPressureActorIdentity LocalPlayer,
        VirtualKey HeldKey,
        ulong IntentEpochToken,
        HeldActionRetryState Retry);
}
