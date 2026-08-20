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

        True(
            MiracleProtectionEndRules.DispatchConsumesHeldConsent(
                MiracleInterceptThreatKind.MarksmanSpite),
            "startup reactive dispatch keeps its one-generation contract");
        True(
            MiracleProtectionEndRules.DispatchConsumesHeldConsent(
                MiracleInterceptThreatKind.Contradance),
            "all startup markers clear protection-end consent");
        False(
            MiracleProtectionEndRules.DispatchConsumesHeldConsent(
                MiracleInterceptThreatKind.PostPurifyCrowdControl),
            "post-Purify dispatch retains consent for a later distinct episode");
        False(
            MiracleProtectionEndRules.DispatchConsumesHeldConsent(
                MiracleInterceptThreatKind.PostGuardCrowdControl),
            "post-Guard dispatch retains consent for a later distinct episode");
    }

    internal static void RankingIsPressureThenHealthThenTrustedMpThenIdentity()
    {
        var unknownLowHp = Candidate(slot: 1, pressureKnown: false, hp: 1_000);
        var knownZero = Candidate(slot: 2, pressureKnown: true, pressure: 0, hp: 9_000);
        Less(knownZero, unknownLowHp, "known zero pressure ranks ahead of unknown");

        var higherPressure = Candidate(slot: 3, pressure: 3, hp: 9_000);
        var lowerPressure = Candidate(slot: 4, pressure: 2, hp: 1_000);
        Less(higherPressure, lowerPressure, "higher pressure ranks before HP");

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
