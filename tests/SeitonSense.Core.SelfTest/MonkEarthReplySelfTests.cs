using SeitonSense.Core;

internal static class MonkEarthReplySelfTests
{
    public static void ExactActionAndStatusIdentityIsFixed()
    {
        Equal(20u, MonkEarthReplyRules.MonkJobId, "MNK job");
        Equal(29_482u, MonkEarthReplyRules.RiddleOfEarthActionId, "base action");
        Equal(29_483u, MonkEarthReplyRules.EarthsReplyActionId, "follow-up action");
        Equal(3_171u, MonkEarthReplyRules.EarthResonanceStatusId, "resonance status");
        Equal(150L, MonkEarthReplyRules.ResonanceMissingGraceMilliseconds, "absence grace");
    }

    public static void LowHealthThresholdIsInclusiveAndOverflowSafe()
    {
        var atThreshold = Observe(
            MonkEarthReplyState.Initial,
            Observation(1_000) with { CurrentHp = 30, MaximumHp = 100 });
        True(atThreshold.ShouldDispatch, "exactly 30 percent dispatches");
        Equal(MonkEarthReplyTrigger.LowHp, atThreshold.Trigger, "low HP reason");

        var aboveThreshold = Observe(
            MonkEarthReplyState.Initial,
            Observation(1_000) with { CurrentHp = 3_001, MaximumHp = 10_000 });
        False(aboveThreshold.ShouldDispatch, "30.01 percent does not dispatch");

        False(
            MonkEarthReplyRules.IsAtOrBelowHealthThreshold(uint.MaxValue, uint.MaxValue, 30),
            "large values compare without overflow");
        True(
            MonkEarthReplyRules.IsAtOrBelowHealthThreshold(uint.MaxValue / 4, uint.MaxValue, 30),
            "large low-health values compare without overflow");
        False(
            MonkEarthReplyRules.IsAtOrBelowHealthThreshold(1, 100, 0),
            "invalid configured percent fails closed");
    }

    public static void ExpiryThresholdIsInclusiveAndLowHpWins()
    {
        var exact = Observe(
            MonkEarthReplyState.Initial,
            Observation(2_000) with { ResonanceRemainingSeconds = 1.5f });
        True(exact.ShouldDispatch, "exact expiry boundary dispatches");
        Equal(MonkEarthReplyTrigger.Expiry, exact.Trigger, "expiry reason");

        var outside = Observe(
            MonkEarthReplyState.Initial,
            Observation(2_000) with { ResonanceRemainingSeconds = 1.501f });
        False(outside.ShouldDispatch, "outside expiry boundary waits");

        var both = Observe(
            MonkEarthReplyState.Initial,
            Observation(2_000) with
            {
                CurrentHp = 3_000,
                MaximumHp = 10_000,
                ResonanceRemainingSeconds = 1.5f,
            });
        Equal(MonkEarthReplyTrigger.LowHp, both.Trigger, "low HP wins diagnostics when both match");
    }

    public static void PurifyPriorityDefersWithoutSpending()
    {
        var deferred = Observe(
            MonkEarthReplyState.Initial,
            Observation(3_000) with
            {
                CurrentHp = 3_000,
                MaximumHp = 10_000,
                HigherPriorityClaimed = true,
            });
        False(deferred.ShouldDispatch, "Purify owns the frame");
        Equal(MonkEarthReplyPhase.TrackingResonance, deferred.NextState.Phase, "resonance remains live");
        Equal(MonkEarthReplyTrigger.None, deferred.NextState.SpentTrigger, "defer does not spend");
        Equal(MonkEarthReplyDecisionReason.HigherPriorityClaimed, deferred.Reason, "defer reason");

        var nextFrame = Observe(
            deferred.NextState,
            Observation(3_001) with { CurrentHp = 3_000, MaximumHp = 10_000 });
        True(nextFrame.ShouldDispatch, "next frame can still dispatch");
    }

    public static void ExactAdjustedFollowUpIsMandatory()
    {
        foreach (var actionId in new[] { 0u, MonkEarthReplyRules.RiddleOfEarthActionId, 1u, uint.MaxValue })
        {
            var decision = Observe(
                MonkEarthReplyState.Initial,
                Observation(4_000) with
                {
                    CurrentHp = 30,
                    MaximumHp = 100,
                    AdjustedActionId = actionId,
                });
            False(decision.ShouldDispatch, $"adjusted action {actionId} rejected");
            Equal(MonkEarthReplyDecisionReason.FollowUpNotAdjusted, decision.Reason, "exact adjustment reason");
        }
    }

    public static void DispatchSpendsBeforeAnyAttemptResult()
    {
        var dispatched = Observe(
            MonkEarthReplyState.Initial,
            Observation(5_000) with { CurrentHp = 1, MaximumHp = 100 });
        True(dispatched.ShouldDispatch, "first threshold observation dispatches");
        Equal(MonkEarthReplyPhase.SpentUntilResonanceGone, dispatched.NextState.Phase, "already spent");

        foreach (var now in new[] { 5_001L, 5_100L, 5_500L })
        {
            var repeated = Observe(
                dispatched.NextState,
                Observation(now) with { CurrentHp = 1, MaximumHp = 100 });
            False(repeated.ShouldDispatch, "same resonance never retries");
            Equal(MonkEarthReplyPhase.SpentUntilResonanceGone, repeated.NextState.Phase, "spent retained");
        }

        var disabled = Observe(
            dispatched.NextState,
            Observation(5_600) with { ConfigurationEnabled = false });
        Equal(MonkEarthReplyPhase.SpentUntilResonanceGone, disabled.NextState.Phase, "toggle off retains spent");
        var reenabled = Observe(disabled.NextState, Observation(5_601));
        False(reenabled.ShouldDispatch, "toggle off/on cannot retry the same resonance");
    }

    public static void AbsenceGracePreventsFlickerRearm()
    {
        var dispatched = Observe(
            MonkEarthReplyState.Initial,
            Observation(6_000) with { CurrentHp = 1, MaximumHp = 100 });
        var missing = Observe(
            dispatched.NextState,
            Observation(6_010) with { ResonancePresent = false });
        Equal(MonkEarthReplyPhase.SpentUntilResonanceGone, missing.NextState.Phase, "first miss retains spent");

        var flickeredBack = Observe(
            missing.NextState,
            Observation(6_100) with { CurrentHp = 1, MaximumHp = 100 });
        False(flickeredBack.ShouldDispatch, "short miss cannot rearm");

        var missingAgain = Observe(
            flickeredBack.NextState,
            Observation(6_200) with { ResonancePresent = false });
        var gone = Observe(
            missingAgain.NextState,
            Observation(6_350) with { ResonancePresent = false });
        Equal(MonkEarthReplyPhase.WaitingForResonance, gone.NextState.Phase, "confirmed absence rearms");

        var newResonance = Observe(
            gone.NextState,
            Observation(6_351) with { CurrentHp = 1, MaximumHp = 100 });
        True(newResonance.ShouldDispatch, "new continuous resonance may dispatch once");
    }

    public static void SafetyGatesAndInvalidInputsFailClosed()
    {
        var baseline = Observation(7_000) with { CurrentHp = 1, MaximumHp = 100 };
        var unsafeInputs = new[]
        {
            baseline with { ConfigurationEnabled = false },
            baseline with { IsSupportedPvPContext = false },
            baseline with { IsLocalMonkValid = false },
            baseline with { IsLocalPlayerIdentityValid = false },
            baseline with { MetadataVerified = false },
            baseline with { CurrentHp = 0 },
            baseline with { CurrentHp = 101, MaximumHp = 100 },
            baseline with { ResonanceRemainingSeconds = float.NaN },
            baseline with { ResonanceRemainingSeconds = 0f },
            baseline with { LowHpThresholdPercent = 0, TriggerBeforeExpiry = false },
            baseline with { ExpiryThresholdSeconds = float.PositiveInfinity, TriggerOnLowHp = false },
        };

        foreach (var observation in unsafeInputs)
            False(Observe(MonkEarthReplyState.Initial, observation).ShouldDispatch, "unsafe input rejected");

        var tracked = Observe(MonkEarthReplyState.Initial, Observation(7_100)).NextState;
        var rollback = Observe(tracked, Observation(7_099));
        Equal(MonkEarthReplyDecisionReason.ClockMovedBackwards, rollback.Reason, "clock rollback");
        Equal(MonkEarthReplyPhase.WaitingForResonance, rollback.NextState.Phase, "rollback clears state");

        var hardReset = Observe(tracked, Observation(7_101) with { HardReset = true });
        Equal(MonkEarthReplyDecisionReason.HardReset, hardReset.Reason, "hard reset reason");
        Equal(MonkEarthReplyPhase.WaitingForResonance, hardReset.NextState.Phase, "hard reset clears state");
    }

    private static MonkEarthReplyDecision Observe(
        MonkEarthReplyState state,
        MonkEarthReplyObservation observation) =>
        MonkEarthReplyRules.Observe(state, observation);

    private static MonkEarthReplyObservation Observation(long nowMilliseconds) => new(
        ConfigurationEnabled: true,
        IsSupportedPvPContext: true,
        IsLocalMonkValid: true,
        IsLocalPlayerIdentityValid: true,
        MetadataVerified: true,
        HigherPriorityClaimed: false,
        ResonancePresent: true,
        CurrentHp: 10_000,
        MaximumHp: 10_000,
        ResonanceRemainingSeconds: 4f,
        AdjustedActionId: MonkEarthReplyRules.EarthsReplyActionId,
        TriggerOnLowHp: true,
        TriggerBeforeExpiry: true,
        LowHpThresholdPercent: 30,
        ExpiryThresholdSeconds: 1.5f,
        NowMilliseconds: nowMilliseconds);

    private static void True(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void False(bool condition, string message) => True(!condition, message);

    private static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
    }
}
