using SeitonSense.Core;

internal static class MiracleProtectionEndSelfTests
{
    internal static void HeldConsentRequiresOneExactUnconsumedGeneration()
    {
        var inherited = MiracleProtectionEndRules.ObserveHeldConsent(
            MiracleProtectionEndHeldConsentState.Initial,
            Observation(token: 0, physicallyDown: true));
        False(inherited.IsLatched, "raw physical level cannot invent consent");

        var acquired = MiracleProtectionEndRules.ObserveHeldConsent(
            inherited,
            Observation(token: 65, physicallyDown: false));
        True(acquired.IsLatched, "one shared unconsumed eligible token acquires consent");
        Equal(65, acquired.GameplayKeyToken, "the exact key token is frozen");

        var afterSharedConsumption = MiracleProtectionEndRules.ObserveHeldConsent(
            acquired,
            Observation(token: 0, physicallyDown: true));
        Equal(acquired, afterSharedConsumption, "shared consumption does not erase a proven hold");

        var released = MiracleProtectionEndRules.ObserveHeldConsent(
            afterSharedConsumption,
            Observation(token: 0, physicallyDown: false));
        False(released.IsLatched, "physical release clears exact consent");

        foreach (var clearingObservation in new[]
                 {
                     Observation(token: 0, physicallyDown: true) with { Enabled = false },
                     Observation(token: 0, physicallyDown: true) with { IsTextInputActive = true },
                     Observation(token: 0, physicallyDown: true) with { HardReset = true },
                 })
        {
            var cleared = MiracleProtectionEndRules.ObserveHeldConsent(
                acquired,
                clearingObservation);
            False(cleared.IsLatched, $"gate clears consent: {clearingObservation}");
        }

        False(
            MiracleProtectionEndRules.DispatchConsumesHeldConsent(
                MiracleInterceptThreatKind.MarksmanSpite),
            "startup reactive dispatch keeps the continuous hold available");
        False(
            MiracleProtectionEndRules.DispatchConsumesHeldConsent(
                MiracleInterceptThreatKind.Contradance),
            "all reactive families leave the same hold available for later actions");
        False(
            MiracleProtectionEndRules.DispatchConsumesHeldConsent(
                MiracleInterceptThreatKind.PostPurifyCrowdControl),
            "post-Purify dispatch retains consent for a later distinct episode");
        False(
            MiracleProtectionEndRules.DispatchConsumesHeldConsent(
                MiracleInterceptThreatKind.PostGuardCrowdControl),
            "post-Guard dispatch retains consent for a later distinct episode");
    }

    internal static void RankingUsesPositivePressureBonusThenHealthMpAndIdentity()
    {
        var unknownLowHp = Candidate(slot: 1, pressureKnown: false, hp: 1_000);
        var knownZero = Candidate(slot: 2, pressureKnown: true, pressure: 0, hp: 9_000);
        Less(unknownLowHp, knownZero, "known zero and unknown pressure are neutral, so HP wins");

        var neutralUnknown = Candidate(slot: 1, pressureKnown: false, hp: 5_000);
        var neutralKnownZero = Candidate(slot: 2, pressureKnown: true, pressure: 0, hp: 5_000);
        Less(neutralUnknown, neutralKnownZero, "zero cannot outrank unknown before stable identity");

        var higherPressure = Candidate(slot: 3, pressure: 3, hp: 9_000);
        var lowerPressure = Candidate(slot: 4, pressure: 2, hp: 1_000);
        Less(higherPressure, lowerPressure, "higher positive pressure ranks before HP");

        var positivePressure = Candidate(slot: 5, pressure: 1, hp: 9_000);
        Less(positivePressure, unknownLowHp, "any positive fresh pressure earns the optional bonus");

        var lowerHp = Candidate(slot: 3, pressure: 2, hp: 2_000);
        var higherHp = Candidate(slot: 4, pressure: 2, hp: 4_000);
        Less(lowerHp, higherHp, "lower exact HP ratio ranks next");

        var knownMp = Candidate(
            slot: 3,
            pressure: 2,
            hp: 4_000,
            mpKnown: true,
            mp: 8_000);
        var unknownMp = Candidate(slot: 4, pressure: 2, hp: 4_000);
        Less(knownMp, unknownMp, "trusted MP ranks ahead of unknown MP");

        var lowerMp = Candidate(
            slot: 3,
            pressure: 2,
            hp: 4_000,
            mpKnown: true,
            mp: 2_000);
        var higherMp = Candidate(
            slot: 4,
            pressure: 2,
            hp: 4_000,
            mpKnown: true,
            mp: 8_000);
        Less(lowerMp, higherMp, "lower trusted MP ratio ranks next");

        var slotOne = Candidate(slot: 1, pressure: 0, hp: 4_000);
        var slotTwo = Candidate(slot: 2, pressure: 0, hp: 4_000);
        Less(slotOne, slotTwo, "lower trusted S-slot closes equal telemetry");

        var invalidSentinelMp = Candidate(
            slot: 5,
            mpKnown: true,
            mp: 500,
            maxMp: 1_000);
        False(invalidSentinelMp.IsValid, "non-PvP maximum MP cannot become trusted rank data");

        var selected = MiracleProtectionEndRules.SelectBestIndex(
            [unknownLowHp, lowerPressure, higherPressure]);
        Equal(2, selected, "one deterministic winner is selected");
    }

    internal static void WhiteMageAndBardShareProtectionEndSemantics()
    {
        foreach (var actionId in new[]
                 {
                     MiracleInterceptConfirmationRules.MiracleOfNatureActionId,
                     MiracleInterceptConfirmationRules.SilentNocturneActionId,
                 })
        {
            var purify = new MiracleInterceptPendingAttempt(
                LocalCasterEntityId: 100,
                ActionId: actionId,
                TargetGameObjectId: 0x200,
                TargetEntityId: 200,
                Threat: MiracleInterceptThreatKind.PostPurifyCrowdControl,
                UseActionAccepted: true,
                AttemptedAtMilliseconds: 1_000)
            {
                RemovedStatusId = MiracleCleanseFollowupRules.StunStatusId,
            };
            var guard = purify with
            {
                Threat = MiracleInterceptThreatKind.PostGuardCrowdControl,
                RemovedStatusId = 0,
            };
            True(purify.IsValid, $"action {actionId} accepts exact post-Purify semantics");
            True(guard.IsValid, $"action {actionId} accepts exact post-Guard semantics");
        }

        Equal(
            (ushort)MiracleCleanseFollowupRules.MiracleOfNatureStatusId,
            MiracleInterceptConfirmationRules.ExpectedStatusForAction(
                MiracleInterceptConfirmationRules.MiracleOfNatureActionId),
            "WHM retains its exact landing status");
        Equal(
            (ushort)MiracleCleanseFollowupRules.SilenceStatusId,
            MiracleInterceptConfirmationRules.ExpectedStatusForAction(
                MiracleInterceptConfirmationRules.SilentNocturneActionId),
            "BRD retains its exact landing status");
    }

    internal static void HeldLeaseSurvivesPriorityAndRetriesOnlyInsideItsBound()
    {
        const long observedAt = 1_000;
        Equal(1_500L, MiracleProtectionEndRules.HeldLeaseMilliseconds, "held episode lease");
        True(
            MiracleProtectionEndRules.CanAttempt(
                HeldActionRetryState.Initial,
                observedAt,
                nowMilliseconds: 1_600),
            "a higher-priority accepted action may finish before exact counter-CC dispatch");
        False(
            MiracleProtectionEndRules.CanAttempt(
                HeldActionRetryState.Initial,
                observedAt,
                nowMilliseconds: 2_500),
            "the 1500 ms deadline remains exclusive");
        True(
            MiracleProtectionEndRules.CanAttempt(
                HeldActionRetryState.Initial,
                observedAt,
                nowMilliseconds: 1_249,
                leaseMilliseconds: MiracleInterceptRules.FuriousBacklashThreatLifetimeMilliseconds),
            "startup Viper retry remains inside its original short window");
        False(
            MiracleProtectionEndRules.CanAttempt(
                HeldActionRetryState.Initial,
                observedAt,
                nowMilliseconds: 1_250,
                leaseMilliseconds: MiracleInterceptRules.FuriousBacklashThreatLifetimeMilliseconds),
            "startup threat window is not extended");
        foreach (var kind in new[]
                 {
                     MiracleInterceptThreatKind.MarksmanSpite,
                     MiracleInterceptThreatKind.Zantetsuken,
                     MiracleInterceptThreatKind.FuriousBacklash,
                     MiracleInterceptThreatKind.Contradance,
                 })
        {
            var lifetime = MiracleInterceptRules.GetThreatLifetimeMilliseconds(kind);
            True(
                MiracleProtectionEndRules.CanAttempt(
                    HeldActionRetryState.Initial,
                    observedAt,
                    observedAt + lifetime - 1,
                    lifetime),
                $"{kind} remains eligible immediately before its original deadline");
            False(
                MiracleProtectionEndRules.CanAttempt(
                    HeldActionRetryState.Initial,
                    observedAt,
                    observedAt + lifetime,
                    lifetime),
                $"{kind} keeps its original exclusive deadline");
        }

        var state = HeldActionRetryState.Initial;
        for (var attempt = 1; attempt <= MiracleProtectionEndRules.MaximumNativeAttempts; attempt++)
        {
            var now = observedAt +
                      ((attempt - 1) * MiracleProtectionEndRules.NativeRetryThrottleMilliseconds);
            True(
                MiracleProtectionEndRules.CanAttempt(state, observedAt, now),
                $"attempt {attempt} is eligible at its throttle boundary");
            var rejected = MiracleProtectionEndRules.CompleteNativeAttempt(
                state,
                observedAt,
                now,
                ClientActionAttemptOutcome.ClientRejected);
            if (attempt < MiracleProtectionEndRules.MaximumNativeAttempts)
            {
                Equal(MiracleProtectionEndAttemptOutcome.RetryScheduled, rejected.Outcome, $"false {attempt} retries");
                False(
                    MiracleProtectionEndRules.CanAttempt(
                        rejected.NextState,
                        observedAt,
                        now + MiracleProtectionEndRules.NativeRetryThrottleMilliseconds - 1),
                    "retry cannot run before the shared throttle");
                state = rejected.NextState;
            }
            else
            {
                Equal(MiracleProtectionEndAttemptOutcome.RejectedTerminal, rejected.Outcome, "final retry-budget false is terminal");
            }
        }

        var accepted = MiracleProtectionEndRules.CompleteNativeAttempt(
            HeldActionRetryState.Initial,
            observedAt,
            nowMilliseconds: 1_600,
            ClientActionAttemptOutcome.ClientAccepted);
        Equal(MiracleProtectionEndAttemptOutcome.AcceptedTerminal, accepted.Outcome, "first true is terminal");

        var ambiguous = MiracleProtectionEndRules.CompleteNativeAttempt(
            HeldActionRetryState.Initial,
            observedAt,
            nowMilliseconds: 1_600,
            ClientActionAttemptOutcome.AcceptanceUnknown);
        Equal(MiracleProtectionEndAttemptOutcome.AmbiguousTerminal, ambiguous.Outcome, "native exception is terminal");

        var notInvoked = MiracleProtectionEndRules.CompleteNativeAttempt(
            HeldActionRetryState.Initial,
            observedAt,
            nowMilliseconds: 1_600,
            ClientActionAttemptOutcome.NotInvoked);
        Equal(MiracleProtectionEndAttemptOutcome.CancelledTerminal, notInvoked.Outcome, "pre-boundary cancellation cannot retry");
    }

    private static MiracleProtectionEndHeldConsentObservation Observation(
        int token,
        bool physicallyDown) =>
        new(
            Enabled: true,
            IsTextInputActive: false,
            UnconsumedEligibleGameplayKeyToken: token,
            LatchedKeyPhysicallyDown: physicallyDown);

    private static MiracleProtectionEndRankCandidate Candidate(
        int slot,
        bool pressureKnown = true,
        int pressure = 0,
        uint hp = 5_000,
        uint maxHp = 10_000,
        bool mpKnown = false,
        uint mp = 0,
        uint maxMp = CombatFrameRules.ExpectedMaximumMp) =>
        new(
            MiracleInterceptThreatKind.PostGuardCrowdControl,
            slot,
            0x1000UL + (uint)slot,
            100u + (uint)slot,
            30,
            pressureKnown,
            pressure,
            hp,
            maxHp,
            mpKnown,
            mp,
            maxMp);

    private static void Less(
        MiracleProtectionEndRankCandidate left,
        MiracleProtectionEndRankCandidate right,
        string message)
    {
        if (MiracleProtectionEndRules.Compare(left, right) >= 0)
            throw new InvalidOperationException(message);
    }

    private static void True(bool value, string message)
    {
        if (!value) throw new InvalidOperationException($"Expected true: {message}");
    }

    private static void False(bool value, string message) => True(!value, message);

    private static void Equal<T>(T expected, T actual, string message)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"Expected {expected}, got {actual}: {message}");
    }
}
