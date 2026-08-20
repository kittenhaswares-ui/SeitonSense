using SeitonSense.Core;

internal static class AllyRescueBufferSelfTests
{
    private static readonly AllyRescueSelectionCandidate CandidateA = Candidate(
        gameObjectId: 0x200,
        entityId: 0x20,
        instanceToken: 1,
        partySlot: 2);

    private static readonly AllyRescueSelectionCandidate CandidateB = Candidate(
        gameObjectId: 0x300,
        entityId: 0x30,
        instanceToken: 2,
        partySlot: 3);

    public static void NativeFalseRetriesOnlyTheExactLeaseUntilAccepted()
    {
        var dispatched = Observe(
            AllyRescueBufferState.Initial,
            [CandidateA],
            freshKey: true,
            locallyReady: true,
            now: 1_000);

        True(dispatched.ShouldDispatch, "same-frame status and fresh key dispatch");
        True(dispatched.ShouldConsumeInputGeneration, "the dispatch claims its framework frame");
        Equal(CandidateA.Intent, dispatched.DispatchIntent!.Value, "exact actor and status are dispatched");
        False(dispatched.NextState.HasSpent(CandidateA.Intent), "intent is not spent before native result");

        var rejected = AllyRescueBufferRules.CompleteNativeAttempt(
            dispatched.NextState,
            CandidateA.Intent,
            nowMilliseconds: 1_000,
            ClientActionAttemptOutcome.ClientRejected);
        Equal(AllyRescueNativeAttemptOutcome.RetryScheduled, rejected.Outcome, "native false schedules retry");
        False(rejected.NextState.HasSpent(CandidateA.Intent), "native false does not spend exact intent");

        var throttled = Observe(
            rejected.NextState,
            [CandidateB, CandidateA],
            locallyReady: true,
            now: 1_000 + AllyRescueBufferRules.NativeRetryThrottleMilliseconds - 1);
        False(throttled.ShouldDispatch, "retry respects the shared throttle");

        var retry = Observe(
            throttled.NextState,
            [CandidateB, CandidateA],
            locallyReady: true,
            now: 1_000 + AllyRescueBufferRules.NativeRetryThrottleMilliseconds);
        True(retry.ShouldDispatch, "same exact lease retries at throttle boundary");
        Equal(CandidateA.Intent, retry.DispatchIntent!.Value, "reranking cannot drift retry to replacement");

        var accepted = AllyRescueBufferRules.CompleteNativeAttempt(
            retry.NextState,
            CandidateA.Intent,
            nowMilliseconds: 1_000 + AllyRescueBufferRules.NativeRetryThrottleMilliseconds,
            ClientActionAttemptOutcome.ClientAccepted);
        Equal(AllyRescueNativeAttemptOutcome.AcceptedTerminal, accepted.Outcome, "first true is terminal");
        True(accepted.NextState.HasSpent(CandidateA.Intent), "accepted exact intent is spent");
        True(accepted.NextState.TrackedIntent is null, "accepted lease is cleared");
    }

    public static void NativeRetriesAreBoundedAndExceptionsAreTerminal()
    {
        var dispatched = Observe(
            AllyRescueBufferState.Initial,
            [CandidateA],
            freshKey: true,
            locallyReady: true,
            now: 1_000);
        var current = dispatched.NextState;
        for (var attempt = 1; attempt <= AllyRescueBufferRules.MaximumNativeAttempts; attempt++)
        {
            var completed = AllyRescueBufferRules.CompleteNativeAttempt(
                current,
                CandidateA.Intent,
                1_000 + ((attempt - 1) * AllyRescueBufferRules.NativeRetryThrottleMilliseconds),
                ClientActionAttemptOutcome.ClientRejected);
            if (attempt < AllyRescueBufferRules.MaximumNativeAttempts)
            {
                Equal(AllyRescueNativeAttemptOutcome.RetryScheduled, completed.Outcome, $"false {attempt} retries");
                current = completed.NextState;
            }
            else
            {
                Equal(AllyRescueNativeAttemptOutcome.RejectedTerminal, completed.Outcome, "final retry-budget false is terminal");
                True(completed.NextState.HasSpent(CandidateA.Intent), "bounded rejection spends exact instance");
                current = completed.NextState;
            }
        }

        var second = Observe(
            AllyRescueBufferState.Initial,
            [CandidateB],
            freshKey: true,
            locallyReady: true,
            now: 2_000);
        var ambiguous = AllyRescueBufferRules.CompleteNativeAttempt(
            second.NextState,
            CandidateB.Intent,
            nowMilliseconds: 2_000,
            ClientActionAttemptOutcome.AcceptanceUnknown);
        Equal(AllyRescueNativeAttemptOutcome.AmbiguousTerminal, ambiguous.Outcome, "throw after native boundary is terminal");
        True(ambiguous.NextState.HasSpent(CandidateB.Intent), "ambiguous boundary cannot retry");

        var notInvoked = AllyRescueBufferRules.CompleteNativeAttempt(
            second.NextState,
            CandidateB.Intent,
            nowMilliseconds: 2_000,
            ClientActionAttemptOutcome.NotInvoked);
        Equal(AllyRescueNativeAttemptOutcome.Cancelled, notInvoked.Outcome, "pre-boundary cancellation is terminal");
        True(notInvoked.NextState.HasSpent(CandidateB.Intent), "only a proven native false may retain the lease");

        var softWait = AllyRescueBufferRules.CompleteNativeAttempt(
            second.NextState,
            CandidateB.Intent,
            nowMilliseconds: 5_500,
            ClientActionAttemptOutcome.SoftUnavailable);
        Equal(AllyRescueNativeAttemptOutcome.SoftWait, softWait.Outcome, "known unavailability retains exact lease");
        Equal(0, softWait.NextState.NativeAttemptCount, "soft wait after 3s spends no attempt");
        Equal(CandidateB.Intent, softWait.NextState.TrackedIntent!.Value, "soft wait stays exact");
    }

    public static void StatusBoundLeaseSurvivesLongSoftWaits()
    {
        Equal(-1L, AllyRescueBufferRules.DefaultBufferMilliseconds, "status-bound sentinel");
        Equal(-1L, AllyRescueBufferRules.NormalizeBufferMilliseconds(50_000), "old durations cannot restore a timeout");

        var armed = Observe(
            AllyRescueBufferState.Initial,
            [CandidateA],
            freshKey: true,
            locallyReady: false,
            now: 1_000);
        Equal(AllyRescueBufferDecisionKind.Armed, armed.Kind, "unavailable action freezes one intent");
        Equal(-1L, armed.NextState.ExpiresAtMilliseconds, "lease has no wall-clock deadline");

        var waiting = Observe(armed.NextState, [CandidateA], locallyReady: false, now: 4_500);
        False(waiting.ShouldDispatch, "same status/key survives beyond three seconds");
        Equal(0, waiting.NextState.NativeAttemptCount, "soft wait spends no native attempts");

        var outOfRange = Observe(
            waiting.NextState,
            [CandidateA with { HasNativeRangeAndLineOfSight = false }],
            locallyReady: true,
            now: 8_000);
        False(outOfRange.ShouldDispatch, "temporary range loss remains a soft wait");
        Equal(0, outOfRange.NextState.NativeAttemptCount, "range wait spends no attempt");

        var ready = Observe(outOfRange.NextState, [CandidateA], locallyReady: true, now: 8_001);
        True(ready.ShouldDispatch, "same exact status/key dispatches whenever it becomes ready");

        var statusGone = Observe(armed.NextState, [], locallyReady: true, now: 4_501);
        Equal(AllyRescueBufferCancelReason.CandidateGone, statusGone.CancelReason, "status disappearance ends lease");
    }

    public static void HeldInputOnlyCountsAtCandidateAppearanceOrReplacement()
    {
        var observed = Observe(
            AllyRescueBufferState.Initial,
            [CandidateA],
            allowHeldKey: true,
            locallyReady: true,
            now: 1_000);
        Equal(AllyRescueBufferDecisionKind.CandidateObserved, observed.Kind, "candidate is tracked without input");

        var heldLater = Observe(
            observed.NextState,
            [CandidateA],
            heldKeyEligible: true,
            allowHeldKey: true,
            locallyReady: true,
            now: 1_001);
        False(heldLater.ShouldDispatch, "held level cannot arm after the entry observation");

        var freshLater = Observe(
            heldLater.NextState,
            [CandidateA],
            freshKey: true,
            heldKeyEligible: true,
            allowHeldKey: true,
            locallyReady: true,
            now: 1_002);
        True(freshLater.ShouldDispatch, "later real down-edge dispatches");
        Equal(AllyRescueInputTrigger.FreshKeyPress, freshLater.InputTrigger, "fresh edge wins over held level");

        var heldAtReplacement = Observe(
            AllyRescueBufferState.Initial,
            [CandidateA],
            heldKeyEligible: true,
            allowHeldKey: true,
            locallyReady: true,
            now: 2_000);
        True(heldAtReplacement.ShouldDispatch, "explicit eligible hold may own candidate appearance");
        Equal(
            AllyRescueInputTrigger.HeldKeyAtCandidateEntry,
            heldAtReplacement.InputTrigger,
            "held entry trigger is explicit");
    }

    public static void ContinuousHoldCanAuthorizeLaterDistinctIntents()
    {
        var first = Observe(
            AllyRescueBufferState.Initial,
            [CandidateA],
            heldKeyEligible: true,
            allowHeldKey: true,
            locallyReady: true,
            now: 1_000);
        var firstAccepted = AllyRescueBufferRules.CompleteNativeAttempt(
            first.NextState,
            CandidateA.Intent,
            1_000,
            ClientActionAttemptOutcome.ClientAccepted);

        var second = Observe(
            firstAccepted.NextState,
            [CandidateB],
            heldKeyEligible: true,
            allowHeldKey: true,
            locallyReady: true,
            now: 1_001);
        True(second.ShouldDispatch, "same continuous hold may authorize a distinct actor/status intent");

        var secondAccepted = AllyRescueBufferRules.CompleteNativeAttempt(
            second.NextState,
            CandidateB.Intent,
            1_001,
            ClientActionAttemptOutcome.ClientAccepted);

        var refreshedStatus = CandidateB with
        {
            Status = new AllyRescueStatusInstance(
                AllyRescueStatusRules.StunStatusId,
                InstanceToken: 3),
        };
        var third = Observe(
            secondAccepted.NextState,
            [refreshedStatus],
            heldKeyEligible: true,
            allowHeldKey: true,
            locallyReady: true,
            now: 1_002);
        True(third.ShouldDispatch, "new status instance is a distinct held-authorized intent");
    }

    public static void CandidateChangesCancelBufferedIntentWithoutTargetDrift()
    {
        var armed = Observe(
            AllyRescueBufferState.Initial,
            [CandidateA],
            freshKey: true,
            locallyReady: false,
            now: 1_000);
        var temporarilyOutOfRange = Observe(
            armed.NextState,
            [CandidateA with { HasNativeRangeAndLineOfSight = false }],
            locallyReady: true,
            now: 1_001);
        False(temporarilyOutOfRange.ShouldDispatch, "temporarily unreachable exact target waits in background");
        Equal(CandidateA.Intent, temporarilyOutOfRange.NextState.TrackedIntent!.Value, "unreachable target cannot drift");

        var changed = Observe(
            temporarilyOutOfRange.NextState,
            [CandidateB],
            locallyReady: true,
            now: 1_002);

        Equal(AllyRescueBufferCancelReason.CandidateGone, changed.CancelReason, "target change cancels old buffer");
        False(changed.ShouldDispatch, "old input never drifts onto replacement actor");
        True(changed.NextState.TrackedIntent is null, "replacement is not selected in the cancelled lease frame");
    }

    public static void RankingIsResolvedBeforeTheInputIsOwned()
    {
        var higherHp = CandidateA with
        {
            CurrentHp = 80,
            MaximumHp = 100,
            UniqueIncomingEnemyPressureCount = 5,
        };
        var lowerHp = CandidateB with
        {
            CurrentHp = 20,
            MaximumHp = 100,
            UniqueIncomingEnemyPressureCount = 0,
        };
        var decision = Observe(
            AllyRescueBufferState.Initial,
            [higherHp, lowerHp],
            freshKey: true,
            locallyReady: true,
            now: 1_000);

        True(decision.ShouldDispatch, "valid best candidate dispatches");
        Equal(1, decision.SelectedCandidateIndex, "lowest exact HP is chosen before pressure");
        Equal(lowerHp.Intent, decision.DispatchIntent!.Value, "dispatch identity matches selected candidate");
    }

    public static void SafetyGatesAndHardResetFailClosed()
    {
        var armed = Observe(
            AllyRescueBufferState.Initial,
            [CandidateA],
            freshKey: true,
            locallyReady: false,
            now: 1_000);
        var textInput = AllyRescueBufferRules.Observe(
            armed.NextState,
            ValidObservation([CandidateA], now: 1_001) with
            {
                IsTextInputActive = true,
                FreshKeyPressed = true,
                ActionLocallyReady = true,
            });
        Equal(AllyRescueBufferCancelReason.TextInputActive, textInput.CancelReason, "typing cancels buffered intent");
        False(textInput.ShouldDispatch, "typing wins over key and readiness");

        var invalidClock = Observe(
            textInput.NextState,
            [CandidateA],
            freshKey: true,
            locallyReady: true,
            now: 1_000);
        Equal(AllyRescueBufferCancelReason.InvalidClock, invalidClock.CancelReason, "clock regression fails closed");

        var reset = AllyRescueBufferRules.Observe(
            invalidClock.NextState,
            ValidObservation([CandidateA], now: 2_000) with
            {
                FreshKeyPressed = true,
                ActionLocallyReady = true,
                HardReset = true,
            });
        Equal(AllyRescueBufferCancelReason.HardReset, reset.CancelReason, "hard reset reason");
        Equal(AllyRescueBufferState.Initial, reset.NextState, "hard reset clears tracking and ledger");
        False(reset.ShouldDispatch, "hard reset never dispatches");
    }

    public static void SelfPurifyOwnsTheSharedInputBeforeAllyRescue()
    {
        var bufferedRescue = Observe(
            AllyRescueBufferState.Initial,
            [CandidateA],
            freshKey: true,
            locallyReady: false,
            now: 1_000);
        var cancelledRescue = AllyRescueBufferRules.Observe(
            bufferedRescue.NextState,
            ValidObservation([CandidateA], now: 1_001) with
            {
                DispatchAllowed = false,
                ActionLocallyReady = true,
                TrackedGameplayKeyPhysicallyDown = true,
            });
        False(cancelledRescue.ShouldDispatch, "a buffered rescue cannot dispatch beside self-Purify");
        Equal(AllyRescueBufferCancelReason.None, cancelledRescue.CancelReason, "same-frame priority pauses the lease");
        Equal(CandidateA.Intent, cancelledRescue.NextState.TrackedIntent!.Value, "priority does not destroy exact lease");

        var releasedBehindPriority = Observe(
            bufferedRescue.NextState,
            [CandidateA],
            locallyReady: false,
            now: 1_001,
            trackedKeyDown: false,
            dispatchAllowed: false);
        Equal(
            AllyRescueBufferCancelReason.HeldKeyReleased,
            releasedBehindPriority.CancelReason,
            "priority wait still revalidates the exact physical key");

        var afterPurify = Observe(
            cancelledRescue.NextState,
            [CandidateA],
            locallyReady: true,
            now: 1_001);
        True(afterPurify.ShouldDispatch, "the same hold may dispatch rescue on a later frame");

        var backgroundArmed = Observe(
            AllyRescueBufferState.Initial,
            [CandidateA],
            heldKeyEligible: true,
            allowHeldKey: true,
            locallyReady: true,
            now: 2_000,
            dispatchAllowed: false);
        Equal(AllyRescueBufferDecisionKind.Armed, backgroundArmed.Kind, "same hold arms exact rescue behind Purify");
        False(backgroundArmed.ShouldDispatch, "higher priority prevents same-frame rescue");
        var sequential = Observe(
            backgroundArmed.NextState,
            [CandidateA],
            locallyReady: true,
            now: 2_001);
        True(sequential.ShouldDispatch, "same physical hold may rescue on the next free frame");
    }

    private static AllyRescueBufferDecision Observe(
        AllyRescueBufferState state,
        IReadOnlyList<AllyRescueSelectionCandidate> candidates,
        bool freshKey = false,
        bool heldKeyEligible = false,
        bool allowHeldKey = false,
        bool locallyReady = false,
        long now = 0,
        long bufferMilliseconds = AllyRescueBufferRules.DefaultBufferMilliseconds,
        bool? trackedKeyDown = null,
        bool dispatchAllowed = true) =>
        AllyRescueBufferRules.Observe(
            state,
            ValidObservation(candidates, now) with
            {
                FreshKeyPressed = freshKey,
                HeldKeyEligible = heldKeyEligible,
                AllowHeldKeyAtCandidateEntry = allowHeldKey,
                ActionLocallyReady = locallyReady,
                BufferMilliseconds = bufferMilliseconds,
                FreshGameplayKeyToken = freshKey ? 65 : 0,
                HeldGameplayKeyToken = heldKeyEligible ? 65 : 0,
                TrackedGameplayKeyPhysicallyDown = trackedKeyDown ??
                    state.Phase == AllyRescueBufferPhase.Buffered,
                DispatchAllowed = dispatchAllowed,
            });

    private static AllyRescueBufferObservation ValidObservation(
        IReadOnlyList<AllyRescueSelectionCandidate> candidates,
        long now) =>
        new(
            ConfigurationEnabled: true,
            IsSupportedPvPContext: true,
            IsLocalPlayerAlive: true,
            IsLocalPlayerIdentityValid: true,
            IsTextInputActive: false,
            Candidates: candidates,
            FreshKeyPressed: false,
            HeldKeyEligible: false,
            AllowHeldKeyAtCandidateEntry: false,
            ActionLocallyReady: false,
            NowMilliseconds: now);

    private static AllyRescueSelectionCandidate Candidate(
        ulong gameObjectId,
        uint entityId,
        ulong instanceToken,
        int partySlot) =>
        new(
            gameObjectId,
            entityId,
            partySlot,
            new AllyRescueStatusInstance(AllyRescueStatusRules.StunStatusId, instanceToken),
            CurrentHp: 50,
            MaximumHp: 100,
            UniqueIncomingEnemyPressureCount: 0,
            CurrentMp: 5_000,
            MaximumMp: 10_000,
            HasTrustedMp: true,
            DistanceSquared: 25,
            IsExactPartyMember: true,
            IsSelf: false,
            IsAlive: true,
            IsTargetable: true,
            HasValidActionTarget: true,
            HasNativeRangeAndLineOfSight: true);

    private static void True(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException($"Expected true: {label}");
    }

    private static void False(bool condition, string label) => True(!condition, label);

    private static void Equal<T>(T expected, T actual, string label)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
    }
}
