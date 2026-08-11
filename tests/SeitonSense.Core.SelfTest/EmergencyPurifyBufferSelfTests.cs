using SeitonSense.Core;

internal static class EmergencyPurifyBufferSelfTests
{
    private static readonly PurifyCcStatusInstance StatusA = new(1343, 1);
    private static readonly PurifyCcStatusInstance StatusB = new(4325, 2);

    public static void SameFrameFreshKeyCanDispatch()
    {
        var decision = Observe(
            EmergencyPurifyBufferState.Initial,
            status: StatusA,
            freshKey: true,
            locallyReady: true,
            now: 1_000);

        True(decision.ShouldDispatch, "a real fresh edge is not lost when the status first appears");
        Equal(EmergencyPurifyBufferPhase.SpentUntilStatusGone, decision.NextState.Phase, "dispatch consumes first");

        decision = Observe(decision.NextState, StatusA, freshKey: true, locallyReady: true, now: 1_001);
        False(decision.ShouldDispatch, "same continuous status still gets at most one attempt");
    }

    public static void DispatchConsumesBeforeAttempt()
    {
        var state = Observe(EmergencyPurifyBufferState.Initial, StatusA, now: 1_000).NextState;
        var decision = Observe(state, StatusA, freshKey: true, locallyReady: false, now: 1_100);
        Equal(EmergencyPurifyBufferDecisionKind.Armed, decision.Kind, "buffer armed while Purify is locked");

        decision = Observe(decision.NextState, StatusA, locallyReady: false, now: 1_200);
        False(decision.ShouldDispatch, "locked frame does not dispatch");

        decision = Observe(decision.NextState, StatusA, locallyReady: true, now: 1_201);
        True(decision.ShouldDispatch, "first locally-ready frame dispatches");
        Equal(
            EmergencyPurifyBufferPhase.SpentUntilStatusGone,
            decision.NextState.Phase,
            "returned state is consumed before caller attempts Purify");

        var afterRejectedAttempt = Observe(
            decision.NextState,
            StatusA,
            freshKey: true,
            locallyReady: true,
            now: 1_202);
        Equal(EmergencyPurifyBufferDecisionKind.None, afterRejectedAttempt.Kind, "failure or rejection is never retried");
        False(afterRejectedAttempt.ShouldDispatch, "same continuous status gets exactly one attempt");
    }

    public static void ReadyAtArmDispatchesExactlyOnce()
    {
        var state = Observe(EmergencyPurifyBufferState.Initial, StatusA, now: 1_000).NextState;
        var decision = Observe(state, StatusA, freshKey: true, locallyReady: true, now: 1_100);
        True(decision.ShouldDispatch, "ready key edge dispatches immediately");
        Equal(EmergencyPurifyBufferPhase.SpentUntilStatusGone, decision.NextState.Phase, "immediate dispatch consumes");

        decision = Observe(decision.NextState, StatusA, locallyReady: true, now: 1_101);
        False(decision.ShouldDispatch, "next ready frame does not repeat");
    }

    public static void TimeoutWithoutAttemptCanRearm()
    {
        Equal(750L, EmergencyPurifyBufferRules.DefaultBufferMilliseconds, "default buffer");
        Equal(100L, EmergencyPurifyBufferRules.NormalizeBufferMilliseconds(-1), "minimum clamp");
        Equal(1_000L, EmergencyPurifyBufferRules.NormalizeBufferMilliseconds(50_000), "maximum clamp");

        var state = Observe(EmergencyPurifyBufferState.Initial, StatusA, now: 1_000).NextState;
        var armed = Observe(
            state,
            StatusA,
            freshKey: true,
            locallyReady: false,
            now: 1_100,
            bufferMilliseconds: 750);
        Equal(1_850L, armed.NextState.ExpiresAtMilliseconds, "exact default deadline");

        var inside = Observe(armed.NextState, StatusA, locallyReady: false, now: 1_849);
        Equal(EmergencyPurifyBufferDecisionKind.None, inside.Kind, "inside deadline remains buffered");

        var boundary = Observe(inside.NextState, StatusA, locallyReady: true, now: 1_850);
        Equal(EmergencyPurifyBufferDecisionKind.Cancelled, boundary.Kind, "deadline wins over readiness");
        Equal(EmergencyPurifyBufferCancelReason.TimedOut, boundary.CancelReason, "timeout reason exposed");
        Equal(EmergencyPurifyBufferPhase.WaitingForFreshKey, boundary.NextState.Phase, "timeout does not fake an action attempt");

        var sameStatus = Observe(boundary.NextState, StatusA, freshKey: true, locallyReady: true, now: 1_851);
        True(sameStatus.ShouldDispatch, "a later distinct key can try after a no-attempt timeout");
    }

    public static void StatusAbsenceIsTheOnlyRearmForSameInstance()
    {
        var state = Observe(EmergencyPurifyBufferState.Initial, StatusA, now: 1_000).NextState;
        var spent = Observe(state, StatusA, freshKey: true, locallyReady: true, now: 1_100).NextState;

        var stillPresent = Observe(spent, StatusA, freshKey: true, locallyReady: true, now: 2_000);
        Equal(EmergencyPurifyBufferDecisionKind.None, stillPresent.Kind, "continuous status stays spent");

        var gone = Observe(stillPresent.NextState, status: null, now: 2_001);
        Equal(EmergencyPurifyBufferCancelReason.StatusGone, gone.CancelReason, "status disappearance exposed");
        Equal(EmergencyPurifyBufferState.Initial, gone.NextState, "absence rearms the lifecycle");

        var seenAgain = Observe(gone.NextState, StatusA, freshKey: true, locallyReady: true, now: 2_002);
        True(seenAgain.ShouldDispatch, "a fresh edge on the new status frame can dispatch");
    }

    public static void ExactStatusReplacementNeedsANewKey()
    {
        var state = Observe(EmergencyPurifyBufferState.Initial, StatusA, now: 1_000).NextState;
        state = Observe(state, StatusA, freshKey: true, locallyReady: false, now: 1_100).NextState;

        var replaced = Observe(state, StatusB, freshKey: true, locallyReady: true, now: 1_200);
        True(replaced.ShouldDispatch, "a fresh key can dispatch for the replacement status immediately");
        Equal(EmergencyPurifyBufferPhase.SpentUntilStatusGone, replaced.NextState.Phase, "replacement dispatch consumes");
    }

    public static void TemporarySafetyGatesDoNotSpendAnAttempt()
    {
        AssertGateCancellation(
            observation => observation with { ConfigurationEnabled = false },
            EmergencyPurifyBufferCancelReason.ConfigurationDisabled,
            "configuration off");
        AssertGateCancellation(
            observation => observation with { IsSupportedPvPContext = false },
            EmergencyPurifyBufferCancelReason.OutsideSupportedPvPContext,
            "outside supported PvP context");
        AssertGateCancellation(
            observation => observation with { IsAlive = false },
            EmergencyPurifyBufferCancelReason.PlayerDead,
            "death");
        AssertGateCancellation(
            observation => observation with { IsLocalPlayerIdentityValid = false },
            EmergencyPurifyBufferCancelReason.LocalPlayerIdentityInvalid,
            "invalid local-player identity");
        AssertGateCancellation(
            observation => observation with { IsResilienceActive = true },
            EmergencyPurifyBufferCancelReason.ResilienceActive,
            "Resilience");
        AssertGateCancellation(
            observation => observation with { IsTextInputActive = true },
            EmergencyPurifyBufferCancelReason.TextInputActive,
            "text input");
    }

    public static void HardResetAndInvalidInputsFailClosed()
    {
        var state = Observe(EmergencyPurifyBufferState.Initial, StatusA, now: 1_000).NextState;
        state = Observe(state, StatusA, freshKey: true, locallyReady: false, now: 1_100).NextState;

        var reset = Observe(state, StatusA, locallyReady: true, now: 1_101, hardReset: true);
        Equal(EmergencyPurifyBufferCancelReason.HardReset, reset.CancelReason, "hard-reset reason");
        Equal(EmergencyPurifyBufferState.Initial, reset.NextState, "hard reset clears all state");
        False(reset.ShouldDispatch, "hard reset wins over readiness");

        var invalid = Observe(
            EmergencyPurifyBufferState.Initial,
            new PurifyCcStatusInstance(1343, 0),
            freshKey: true,
            locallyReady: true,
            now: 2_000);
        Equal(EmergencyPurifyBufferCancelReason.InvalidStatusInstance, invalid.CancelReason, "invalid instance reason");
        Equal(EmergencyPurifyBufferState.Initial, invalid.NextState, "invalid identity fails closed");

        state = Observe(EmergencyPurifyBufferState.Initial, StatusA, now: 3_000).NextState;
        var backwards = Observe(state, StatusA, freshKey: true, locallyReady: true, now: 2_999);
        Equal(EmergencyPurifyBufferCancelReason.ClockMovedBackwards, backwards.CancelReason, "clock regression reason");
        Equal(EmergencyPurifyBufferPhase.WaitingForFreshKey, backwards.NextState.Phase, "clock regression waits without faking an attempt");
        False(backwards.ShouldDispatch, "clock regression never dispatches");
    }

    private static void AssertGateCancellation(
        Func<EmergencyPurifyBufferObservation, EmergencyPurifyBufferObservation> mutate,
        EmergencyPurifyBufferCancelReason expectedReason,
        string label)
    {
        var state = Observe(EmergencyPurifyBufferState.Initial, StatusA, now: 1_000).NextState;
        state = Observe(state, StatusA, freshKey: true, locallyReady: false, now: 1_100).NextState;
        var baseObservation = ValidObservation(StatusA, now: 1_200) with
        {
            FreshKeyPressed = true,
            PurifyLocallyReady = true,
        };

        var cancelled = EmergencyPurifyBufferRules.Observe(state, mutate(baseObservation));
        Equal(EmergencyPurifyBufferDecisionKind.Cancelled, cancelled.Kind, $"{label} decision");
        Equal(expectedReason, cancelled.CancelReason, $"{label} reason");
        Equal(EmergencyPurifyBufferPhase.WaitingForFreshKey, cancelled.NextState.Phase, $"{label} waits");
        False(cancelled.ShouldDispatch, $"{label} blocks dispatch");

        var repeatedCancellation = EmergencyPurifyBufferRules.Observe(
            cancelled.NextState,
            mutate(baseObservation with { NowMilliseconds = 1_201 }));
        Equal(EmergencyPurifyBufferDecisionKind.Cancelled, repeatedCancellation.Kind, $"{label} remains blocked");

        var restored = Observe(
            repeatedCancellation.NextState,
            StatusA,
            freshKey: true,
            locallyReady: true,
            now: 1_202);
        True(restored.ShouldDispatch, $"{label} never consumed the later real key");
    }

    private static EmergencyPurifyBufferDecision Observe(
        EmergencyPurifyBufferState state,
        PurifyCcStatusInstance? status,
        bool freshKey = false,
        bool locallyReady = false,
        long now = 0,
        bool hardReset = false,
        long bufferMilliseconds = EmergencyPurifyBufferRules.DefaultBufferMilliseconds) =>
        EmergencyPurifyBufferRules.Observe(
            state,
            ValidObservation(status, now) with
            {
                FreshKeyPressed = freshKey,
                PurifyLocallyReady = locallyReady,
                HardReset = hardReset,
                BufferMilliseconds = bufferMilliseconds,
            });

    private static EmergencyPurifyBufferObservation ValidObservation(
        PurifyCcStatusInstance? status,
        long now) =>
        new(
            ConfigurationEnabled: true,
            IsSupportedPvPContext: true,
            IsAlive: true,
            IsLocalPlayerIdentityValid: true,
            IsResilienceActive: false,
            IsTextInputActive: false,
            StatusInstance: status,
            FreshKeyPressed: false,
            PurifyLocallyReady: false,
            NowMilliseconds: now);

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
