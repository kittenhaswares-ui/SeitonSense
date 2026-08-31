using SeitonSense.Core;

internal static class HeldActionRetrySelfTests
{
    internal static void ProvenFalseRetriesAreThrottledAndBounded()
    {
        var state = HeldActionRetryState.Initial;
        for (var attempt = 1; attempt <= HeldActionRetryRules.MaximumNativeAttempts; attempt++)
        {
            var now = 1_000L +
                      ((attempt - 1) * HeldActionRetryRules.NativeRetryThrottleMilliseconds);
            var decision = HeldActionRetryRules.Complete(
                state,
                now,
                ClientActionAttemptOutcome.ClientRejected);
            if (attempt < HeldActionRetryRules.MaximumNativeAttempts)
            {
                Equal(HeldActionRetryDisposition.RetryScheduled, decision.Disposition, $"false {attempt}");
                False(HeldActionRetryRules.CanAttempt(decision.NextState, now + 49), $"false {attempt} throttled");
                True(HeldActionRetryRules.CanAttempt(decision.NextState, now + 50), $"false {attempt} released");
                state = decision.NextState;
            }
            else
            {
                Equal(HeldActionRetryDisposition.RejectedTerminal, decision.Disposition, "eighth false terminal");
                Equal(HeldActionRetryState.Initial, decision.NextState, "terminal clears retry");
            }
        }
    }

    internal static void OnlyProvenFalseCanRetainTheFrozenIntent()
    {
        var pending = HeldActionRetryRules.Complete(
            HeldActionRetryState.Initial,
            1_000,
            ClientActionAttemptOutcome.ClientRejected).NextState;

        Equal(
            HeldActionRetryDisposition.AcceptedTerminal,
            HeldActionRetryRules.Complete(
                pending,
                1_075,
                ClientActionAttemptOutcome.ClientAccepted).Disposition,
            "accepted");
        Equal(
            HeldActionRetryDisposition.AmbiguousTerminal,
            HeldActionRetryRules.Complete(
                pending,
                1_075,
                ClientActionAttemptOutcome.AcceptanceUnknown).Disposition,
            "exception ambiguity");
        Equal(
            HeldActionRetryDisposition.CancelledTerminal,
            HeldActionRetryRules.Complete(
                pending,
                1_075,
                ClientActionAttemptOutcome.NotInvoked).Disposition,
            "pre-boundary cancellation");
        False(HeldActionRetryRules.CanAttempt(HeldActionRetryState.Initial, 1_075), "no frozen intent");
        True(
            HeldActionRetryRules.CanAttemptFrozenIntent(HeldActionRetryState.Initial, 1_075),
            "a caller-owned frozen intent may soft-wait before its first native call");

        var softWait = HeldActionRetryRules.Complete(
            pending,
            1_075,
            ClientActionAttemptOutcome.SoftUnavailable);
        Equal(HeldActionRetryDisposition.SoftWait, softWait.Disposition, "soft unavailable");
        Equal(pending, softWait.NextState, "soft wait consumes zero attempts");

        var beforeSoftWait = pending;
        False(
            HeldActionRetryRules.IsNativeBoundaryNearQueueable(
                animationLockSeconds: 0.600f,
                localPlayerIsCasting: false,
                castActionId: 0,
                actionQueued: false),
            "600 ms animation lock is a soft wait");
        Equal(beforeSoftWait, pending, "soft wait consumes zero attempts");
        True(
            HeldActionRetryRules.IsNativeBoundaryNearQueueable(
                animationLockSeconds: 0.010f,
                localPlayerIsCasting: false,
                castActionId: 0,
                actionQueued: false),
            "small nonzero lock is near queueable");
        False(
            HeldActionRetryRules.IsNativeBoundaryNearQueueable(0f, false, 0, true),
            "existing native queue is a soft wait");
    }

    internal static void NativeFalseRequiresAStableReadyBoundaryFingerprint()
    {
        const uint actionId = 29_400;
        var ready = Fingerprint(actionId);

        Equal(
            ClientActionAttemptOutcome.ClientRejected,
            ClientActionAttemptBoundaryRules.Classify(
                clientReturnedAccepted: false,
                actionId,
                ready,
                ready),
            "unchanged clean-ready false is proven rejection");
        Equal(
            ClientActionAttemptOutcome.ClientAccepted,
            ClientActionAttemptBoundaryRules.Classify(
                clientReturnedAccepted: true,
                actionId,
                ready,
                ready with { ActionQueued = true }),
            "true is terminal accepted regardless of post-call transition");

        var ambiguousTransitions = new[]
        {
            ready with { ActionQueued = true },
            ready with { QueuedActionId = actionId },
            ready with { LastUsedActionSequence = 8 },
            ready with { AnimationLockSeconds = 0.6f },
            ready with { CastActionId = actionId },
            ready with { AdjustedActionId = actionId + 1 },
            ready with { IsActionOffCooldown = false },
            ready with { ResourceStatus = 1 },
        };
        foreach (var after in ambiguousTransitions)
        {
            Equal(
                ClientActionAttemptOutcome.AcceptanceUnknown,
                ClientActionAttemptBoundaryRules.Classify(
                    clientReturnedAccepted: false,
                    actionId,
                    ready,
                    after),
                $"transition {after}");
        }

        Equal(
            ClientActionAttemptOutcome.AcceptanceUnknown,
            ClientActionAttemptBoundaryRules.Classify(
                clientReturnedAccepted: false,
                actionId,
                default,
                ready),
            "missing baseline cannot prove rejection");
    }

    internal static void CriticalRecoveryCanProveFalseAcrossAnUnchangedOccupiedQueue()
    {
        const uint recoveryActionId = 29_408;
        var occupied = Fingerprint(recoveryActionId) with
        {
            ActionQueued = true,
            QueuedActionType = 1,
            QueuedActionId = 29_400,
            QueuedTargetId = 0x1001,
            QueuedExtraParam = 7,
            QueueMode = 0,
            QueuedComboRouteId = 3,
        };

        False(
            occupied.IsExactActionReady(recoveryActionId),
            "ordinary readiness remains strict with an occupied queue");
        True(
            occupied.IsCriticalRecoveryActionReady(
                recoveryActionId,
                allowOccupiedQueue: true),
            "reviewed critical recovery may inspect an occupied queue");
        False(
            occupied.IsCriticalRecoveryActionReady(
                recoveryActionId,
                allowOccupiedQueue: false),
            "critical classifier requires an explicit queue opt-in");
        False(
            HeldActionRetryRules.IsNativeBoundaryNearQueueable(
                0f,
                localPlayerIsCasting: false,
                castActionId: 0,
                actionQueued: true),
            "ordinary native boundary stays queue-exclusive");
        True(
            HeldActionRetryRules.IsNativeBoundaryNearQueueable(
                0f,
                localPlayerIsCasting: false,
                castActionId: 0,
                actionQueued: true,
                allowOccupiedQueue: true),
            "critical recovery boundary explicitly tolerates the occupied queue");

        Equal(
            ClientActionAttemptOutcome.ClientRejected,
            ClientActionAttemptBoundaryRules.ClassifyCriticalRecovery(
                clientReturnedAccepted: false,
                recoveryActionId,
                occupied,
                occupied,
                allowOccupiedQueue: true),
            "unchanged occupied queue proves one clean critical-recovery false");
        Equal(
            ClientActionAttemptOutcome.AcceptanceUnknown,
            ClientActionAttemptBoundaryRules.Classify(
                clientReturnedAccepted: false,
                recoveryActionId,
                occupied,
                occupied),
            "ordinary classifier still rejects the occupied boundary");
        Equal(
            ClientActionAttemptOutcome.AcceptanceUnknown,
            ClientActionAttemptBoundaryRules.ClassifyCriticalRecovery(
                clientReturnedAccepted: false,
                recoveryActionId,
                occupied,
                occupied with { QueuedTargetId = 0x2002 },
                allowOccupiedQueue: true),
            "a mutated queue target is acceptance-ambiguous");
        Equal(
            ClientActionAttemptOutcome.AcceptanceUnknown,
            ClientActionAttemptBoundaryRules.ClassifyCriticalRecovery(
                clientReturnedAccepted: false,
                recoveryActionId,
                occupied,
                occupied with { ActionQueued = false },
                allowOccupiedQueue: true),
            "a cleared queue is acceptance-ambiguous");
        Equal(
            ClientActionAttemptOutcome.ClientAccepted,
            ClientActionAttemptBoundaryRules.ClassifyCriticalRecovery(
                clientReturnedAccepted: true,
                recoveryActionId,
                occupied,
                occupied with { ActionQueued = false },
                allowOccupiedQueue: true),
            "client true remains terminal acceptance even when the queue changes");

        var blocked = occupied with { AnimationLockSeconds = 0.6f };
        True(
            ClientActionAttemptBoundaryRules.BecameCriticalRecoveryReady(
                recoveryActionId,
                blocked,
                occupied,
                allowOccupiedQueue: true),
            "not-ready to ready is one real recovery edge");
        True(
            ClientActionAttemptBoundaryRules.BecameCriticalRecoveryReady(
                recoveryActionId,
                default,
                occupied,
                allowOccupiedQueue: true),
            "unreadable to ready is one real recovery edge");
        False(
            ClientActionAttemptBoundaryRules.BecameCriticalRecoveryReady(
                recoveryActionId,
                occupied with { AnimationLockSeconds = 0.05f },
                occupied with { AnimationLockSeconds = 0.04f },
                allowOccupiedQueue: true),
            "timer movement while already ready is not another edge");
    }

    internal static void CriticalRecoveryRetryWakesOnAnEdgeOrFallbackFrameOnlyOnce()
    {
        var pending = HeldActionRetryRules.Complete(
            HeldActionRetryState.Initial,
            1_000,
            ClientActionAttemptOutcome.ClientRejected).NextState;

        False(
            HeldActionRetryRules.CanAttemptOnBoundaryEdgeOrThrottle(
                pending,
                nowMilliseconds: 1_001,
                currentFrameId: 11,
                lastAttemptFrameId: 10,
                relevantBoundaryEdge: false),
            "a quiet frame before the fallback throttle does not retry");
        True(
            HeldActionRetryRules.CanAttemptOnBoundaryEdgeOrThrottle(
                pending,
                nowMilliseconds: 1_001,
                currentFrameId: 11,
                lastAttemptFrameId: 10,
                relevantBoundaryEdge: true),
            "a relevant boundary edge releases the retry before 50 ms");
        False(
            HeldActionRetryRules.CanAttemptOnBoundaryEdgeOrThrottle(
                pending,
                nowMilliseconds: 1_001,
                currentFrameId: 10,
                lastAttemptFrameId: 10,
                relevantBoundaryEdge: true),
            "the same framework frame cannot dispatch twice");
        True(
            HeldActionRetryRules.CanAttemptOnBoundaryEdgeOrThrottle(
                pending,
                nowMilliseconds: 1_050,
                currentFrameId: 12,
                lastAttemptFrameId: 10,
                relevantBoundaryEdge: false),
            "the legacy 50 ms fallback still guarantees progress without an edge");
        True(
            HeldActionRetryRules.CanAttemptFrozenIntentOnBoundaryEdgeOrThrottle(
                HeldActionRetryState.Initial,
                nowMilliseconds: 1_000,
                currentFrameId: 10,
                lastAttemptFrameId: -1,
                relevantBoundaryEdge: false),
            "an initial frozen intent can use its first eligible frame");
        False(
            HeldActionRetryRules.CanAttemptFrozenIntentOnBoundaryEdgeOrThrottle(
                HeldActionRetryState.Initial,
                nowMilliseconds: 1_000,
                currentFrameId: 10,
                lastAttemptFrameId: 10,
                relevantBoundaryEdge: true),
            "initial intent also obeys the one-attempt-per-frame boundary");
        False(
            HeldActionRetryRules.CanAttemptOnBoundaryEdgeOrThrottle(
                new HeldActionRetryState(
                    HeldActionRetryRules.MaximumNativeAttempts,
                    1_000),
                nowMilliseconds: 1_001,
                currentFrameId: 11,
                lastAttemptFrameId: 10,
                relevantBoundaryEdge: true),
            "an edge cannot revive an exhausted retry budget");
    }

    internal static void AcceptedEpisodeDoesNotLatchAContinuousHeldKey()
    {
        False(
            HeldActionRetryRules.ShouldLatchHeldKeyUntilRelease(
                HeldActionRetryDisposition.AcceptedTerminal),
            "accepted exact episode leaves continuous hold available");
        False(
            HeldActionRetryRules.ShouldLatchHeldKeyUntilRelease(
                HeldActionRetryDisposition.RetryScheduled),
            "scheduled retry remains owned by frozen intent");
        True(
            HeldActionRetryRules.ShouldLatchHeldKeyUntilRelease(
                HeldActionRetryDisposition.RejectedTerminal),
            "exhausted false episode latches exact key");
        True(
            HeldActionRetryRules.ShouldLatchHeldKeyUntilRelease(
                HeldActionRetryDisposition.AmbiguousTerminal),
            "ambiguous episode latches exact key");
        False(
            HeldActionRetryRules.ShouldLatchHeldKeyUntilRelease(
                HeldActionRetryDisposition.CancelledTerminal),
            "ordinary cancellation spends only its exact episode");
    }

    internal static void FrozenThrottleAndGlobalWaitRetainOnlyEligiblePriority()
    {
        var pending = new HeldActionRetryState(1, 1_050);
        True(
            HeldActionRetryRules.RetainsSchedulerFrame(
                pending,
                1_001,
                exactIntentValid: true,
                actionSpecificReady: true),
            "proven-false throttle retains the scheduler frame");
        True(
            HeldActionRetryRules.RetainsSchedulerFrame(
                HeldActionRetryState.Initial,
                1_001,
                exactIntentValid: true,
                actionSpecificReady: true),
            "an initial frozen intent may retain a globally blocked native boundary");
        False(
            HeldActionRetryRules.RetainsSchedulerFrame(
                pending,
                1_001,
                exactIntentValid: true,
                actionSpecificReady: false),
            "action-specific cooldown or resources leave the frame to lower helpers");
        False(
            HeldActionRetryRules.RetainsSchedulerFrame(
                pending,
                1_001,
                exactIntentValid: true,
                actionSpecificReady: true,
                targetSpecificReady: false),
            "target-specific range or identity waits leave the frame to lower helpers");
        False(
            HeldActionRetryRules.RetainsSchedulerFrame(
                HeldActionRetryState.Initial,
                -1,
                exactIntentValid: true,
                actionSpecificReady: true),
            "invalid clocks fail closed");
    }

    internal static void InitialExactIntentClaimsCastSoftWaitWithoutSpendingBudget()
    {
        var initial = HeldActionRetryState.Initial;
        False(initial.IsPending, "an initial freeze has not spent a native attempt");
        False(
            HeldActionRetryRules.IsNativeBoundaryNearQueueable(
                animationLockSeconds: 0f,
                localPlayerIsCasting: true,
                castActionId: 12_345,
                actionQueued: false),
            "a complete active cast is a global soft wait");
        False(
            HeldActionRetryRules.IsNativeBoundaryNearQueueable(0f, true, 0, false),
            "IsCasting must clear before the action boundary");
        False(
            HeldActionRetryRules.IsNativeBoundaryNearQueueable(0f, false, 12_345, false),
            "CastActionId must clear before the action boundary");
        True(
            HeldActionRetryRules.RetainsSchedulerFrame(
                initial,
                1_000,
                exactIntentValid: true,
                actionSpecificReady: true,
                targetSpecificReady: true),
            "an exact action/target/key/episode freeze claims the cast-soft-wait frame");

        var softWait = HeldActionRetryRules.Complete(
            initial,
            1_000,
            ClientActionAttemptOutcome.SoftUnavailable);
        Equal(HeldActionRetryDisposition.SoftWait, softWait.Disposition, "cast soft wait");
        Equal(initial, softWait.NextState, "cast soft wait preserves the initial freeze");
        Equal(0, softWait.NextState.NativeAttemptCount, "cast wait spends zero attempts");
        True(
            HeldActionRetryRules.RetainsSchedulerFrame(
                softWait.NextState,
                1_001,
                exactIntentValid: true,
                actionSpecificReady: true,
                targetSpecificReady: true),
            "the same exact freeze retains scheduler ownership on the next cast frame");
        False(
            HeldActionRetryRules.RetainsSchedulerFrame(
                softWait.NextState,
                1_001,
                exactIntentValid: true,
                actionSpecificReady: true,
                targetSpecificReady: false),
            "target drift cannot retain or redirect the frozen intent");
        True(
            HeldActionRetryRules.IsNativeBoundaryNearQueueable(0f, false, 0, false),
            "actual execution becomes eligible only after both cast signals clear");
    }

    internal static void OptInLatencyWindowExtendsOnlyCleanFalseBudget()
    {
        HeldActionRetryRules.ConfigureLatencyResponsePolicy(
            enabled: true,
            HeldActionRetryRules.DefaultLatencyResponseWindowMilliseconds);
        try
        {
            Equal(1_000, HeldActionRetryRules.CurrentLatencyResponseWindowMilliseconds, "configured window");
            Equal(21, HeldActionRetryRules.CurrentMaximumNativeAttempts, "1000 ms at 50 ms cadence");

            var state = HeldActionRetryState.Initial;
            for (var attempt = 1; attempt <= 21; attempt++)
            {
                var now = 5_000L +
                          ((attempt - 1) * HeldActionRetryRules.NativeRetryThrottleMilliseconds);
                var decision = HeldActionRetryRules.Complete(
                    state,
                    now,
                    ClientActionAttemptOutcome.ClientRejected);
                if (attempt < 21)
                {
                    Equal(HeldActionRetryDisposition.RetryScheduled, decision.Disposition, $"extended false {attempt}");
                    state = decision.NextState;
                }
                else
                {
                    Equal(HeldActionRetryDisposition.RejectedTerminal, decision.Disposition, "extended terminal");
                }
            }

            var accepted = HeldActionRetryRules.Complete(
                new HeldActionRetryState(12, 6_000),
                6_000,
                ClientActionAttemptOutcome.ClientAccepted);
            Equal(HeldActionRetryDisposition.AcceptedTerminal, accepted.Disposition, "acceptance stays terminal");

            var ambiguous = HeldActionRetryRules.Complete(
                new HeldActionRetryState(12, 6_000),
                6_000,
                ClientActionAttemptOutcome.AcceptanceUnknown);
            Equal(HeldActionRetryDisposition.AmbiguousTerminal, ambiguous.Disposition, "ambiguity stays terminal");

            var frozenBudget = HeldActionRetryRules.Complete(
                HeldActionRetryState.Initial,
                7_000,
                ClientActionAttemptOutcome.ClientRejected).NextState;
            Equal(21, frozenBudget.NativeAttemptLimit, "the first clean false freezes the enabled budget");
            HeldActionRetryRules.ConfigureLatencyResponsePolicy(false, 0);
            Equal(8, HeldActionRetryRules.CurrentMaximumNativeAttempts, "new intents return to legacy budget");
            True(frozenBudget.IsPending, "an existing exact intent cannot be stranded by a live policy shrink");
            var afterShrink = HeldActionRetryRules.Complete(
                frozenBudget,
                7_050,
                ClientActionAttemptOutcome.ClientRejected);
            Equal(HeldActionRetryDisposition.RetryScheduled, afterShrink.Disposition, "frozen extended intent continues after shrink");
            Equal(21, afterShrink.NextState.NativeAttemptLimit, "frozen limit survives the next retry");
        }
        finally
        {
            HeldActionRetryRules.ConfigureLatencyResponsePolicy(false, 0);
        }

        Equal(0, HeldActionRetryRules.CurrentLatencyResponseWindowMilliseconds, "legacy reset");
        Equal(HeldActionRetryRules.MaximumNativeAttempts, HeldActionRetryRules.CurrentMaximumNativeAttempts, "legacy budget restored");
    }

    private static ClientActionAttemptFingerprint Fingerprint(uint actionId) =>
        new(
            Captured: true,
            ActionQueued: false,
            QueuedActionType: 0,
            QueuedActionId: 0,
            QueuedTargetId: 0,
            QueuedExtraParam: 0,
            QueueMode: 0,
            QueuedComboRouteId: 0,
            LastUsedActionSequence: 7,
            AnimationLockSeconds: 0f,
            CastActionId: 0,
            AdjustedActionId: actionId,
            IsActionOffCooldown: true,
            ResourceStatus: 0);

    private static void Equal<T>(T expected, T actual, string label)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
    }

    private static void True(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException($"Expected true: {label}");
    }

    private static void False(bool condition, string label)
    {
        if (condition) throw new InvalidOperationException($"Expected false: {label}");
    }
}
