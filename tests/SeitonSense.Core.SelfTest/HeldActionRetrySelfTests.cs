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
