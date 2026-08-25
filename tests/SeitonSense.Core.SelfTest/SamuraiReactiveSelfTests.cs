using SeitonSense.Core;

internal static class SamuraiReactiveSelfTests
{
    private const int HeldKey = 65;
    private static readonly SamuraiReactiveCounterCcTarget Enemy = new(
        GameObjectId: 0x2001,
        EntityId: 0x201,
        JobId: 23);

    public static void ProtectionSignalsAndLeasesAreExact()
    {
        Equal(
            SamuraiReactiveProtectionKind.PurifyResilience,
            SamuraiReactiveRuntimeRules.ClassifyExactProtectionSignal(
                SamuraiReactiveRuntimeRules.PurifyActionId,
                0x201,
                0x201,
                1,
                globalSequence: 9,
                sourceSequence: 0),
            "exact self-Purify");
        Equal(
            SamuraiReactiveProtectionKind.Guard,
            SamuraiReactiveRuntimeRules.ClassifyExactProtectionSignal(
                SamuraiReactiveRuntimeRules.GuardActionId,
                0x201,
                0x201,
                1,
                globalSequence: 0,
                sourceSequence: 7),
            "exact self-Guard");
        Equal(
            SamuraiReactiveProtectionKind.None,
            SamuraiReactiveRuntimeRules.ClassifyExactProtectionSignal(
                SamuraiReactiveRuntimeRules.PurifyActionId,
                0x201,
                0x202,
                1,
                globalSequence: 9,
                sourceSequence: 7),
            "different target");
        Equal(
            SamuraiReactiveProtectionKind.None,
            SamuraiReactiveRuntimeRules.ClassifyExactProtectionSignal(
                SamuraiReactiveRuntimeRules.GuardActionId,
                0x201,
                0x201,
                1,
                globalSequence: 0,
                sourceSequence: 0),
            "missing packet sequence");
        True(
            SamuraiReactiveRuntimeRules.IsExpectedProtectionStatus(
                SamuraiReactiveProtectionKind.Guard,
                SamuraiReactiveRuntimeRules.GuardAlternateStatusId),
            "alternate Guard row");
        True(
            SamuraiReactiveRuntimeRules.IsInsideLease(
                1_000,
                2_000,
                SamuraiReactiveRuntimeRules.SignalStatusObservationLeaseMilliseconds),
            "inclusive acquisition boundary");
        False(
            SamuraiReactiveRuntimeRules.IsInsideLease(
                1_000,
                2_001,
                SamuraiReactiveRuntimeRules.SignalStatusObservationLeaseMilliseconds),
            "expired acquisition boundary");
    }

    public static void SotenMineuchiSequenceIsOneExactStagedIntent()
    {
        Equal(
            SamuraiReactiveCounterCcRules.MineuchiMaximumRangeYalms,
            SamuraiReactiveCounterCcRules.NormalizeSotenMaximumRangeYalms(-1f),
            "minimum range clamp");
        Equal(
            SamuraiReactiveCounterCcRules.SotenMaximumRangeYalms,
            SamuraiReactiveCounterCcRules.NormalizeSotenMaximumRangeYalms(float.NaN),
            "invalid range fallback");

        var armed = SamuraiReactiveCounterCcRules.Arm(Enemy, HeldKey, 1_000);
        True(armed.IsActive, "exact enemy/key arm");
        var protectedMelee = SamuraiReactiveCounterCcRules.Observe(
            armed,
            CounterObservation(distance: 4f, protectionPresent: true));
        Equal(
            SamuraiReactiveCounterCcDecisionKind.Waiting,
            protectedMelee.Kind,
            "Mineuchi waits for authoritative absence");
        var direct = SamuraiReactiveCounterCcRules.Observe(
            armed,
            CounterObservation(distance: 4f, protectionPresent: false));
        Equal(
            SamuraiReactiveCounterCcRules.MineuchiActionId,
            direct.ActionId,
            "direct Mineuchi skips Soten");

        var soten = SamuraiReactiveCounterCcRules.Observe(
            armed,
            CounterObservation(distance: 15f, protectionPresent: false));
        Equal(
            SamuraiReactiveCounterCcRules.SotenActionId,
            soten.ActionId,
            "one Soten approach");
        var afterSoten = SamuraiReactiveCounterCcRules.CompleteAttempt(
            armed,
            soten.ActionId,
            ClientActionAttemptOutcome.ClientAccepted);
        Equal(
            SamuraiReactiveCounterCcPhase.ApproachAccepted,
            afterSoten.Phase,
            "accepted approach reserves Mineuchi stage");
        Equal(
            SamuraiReactiveCounterCcDecisionKind.Waiting,
            SamuraiReactiveCounterCcRules.Observe(
                afterSoten,
                CounterObservation(distance: 10f, protectionPresent: false)).Kind,
            "no second Soten");
        Equal(
            SamuraiReactiveCounterCcRules.MineuchiActionId,
            SamuraiReactiveCounterCcRules.Observe(
                afterSoten,
                CounterObservation(distance: 4f, protectionPresent: false)).ActionId,
            "Mineuchi only after arrival");

        var rejected = SamuraiReactiveCounterCcRules.CompleteAttempt(
            armed,
            SamuraiReactiveCounterCcRules.SotenActionId,
            ClientActionAttemptOutcome.ClientRejected);
        Equal(
            SamuraiReactiveCounterCcPhase.Spent,
            rejected.Phase,
            "Soten rejection is terminal");
    }

    public static void ZantetsukenRequiresOwnKuzushiAndZeroShield()
    {
        var armed = SamuraiZantetsukenRules.Arm(Enemy, HeldKey, 2_000);
        True(armed.IsActive, "exact Zantetsuken intent");
        Equal(
            SamuraiZantetsukenDecisionKind.Waiting,
            SamuraiZantetsukenRules.Observe(
                armed,
                ZantetsukenObservation(shieldPercentage: 1)).Kind,
            "any shield waits");
        Equal(
            SamuraiZantetsukenRules.ActionId,
            SamuraiZantetsukenRules.Observe(
                armed,
                ZantetsukenObservation(shieldPercentage: 0)).ActionId,
            "own Kuzushi and zero shield");
        Equal(
            SamuraiZantetsukenDecisionKind.Cancelled,
            SamuraiZantetsukenRules.Observe(
                armed,
                ZantetsukenObservation(shieldPercentage: 0) with
                {
                    OwnSourceKuzushiCount = 0,
                }).Kind,
            "missing or foreign Kuzushi cancels");

        var joblessDummy = Enemy with { JobId = 0 };
        False(
            SamuraiZantetsukenRules.Arm(joblessDummy, HeldKey, 2_000).IsActive,
            "jobless target rejected in normal CC");
        True(
            SamuraiZantetsukenRules.Arm(
                joblessDummy,
                HeldKey,
                2_000,
                allowJoblessWolvesDenTarget: true).IsActive,
            "reviewed Wolves Den dummy opt-in");
    }

    private static SamuraiReactiveCounterCcObservation CounterObservation(
        float distance,
        bool protectionPresent) => new(
        Enabled: true,
        HardReset: false,
        ExactTargetStillCurrent: true,
        TargetAliveAndTargetable: true,
        ExactGameplayKeyStillDown: true,
        ProtectionPresent: protectionPresent,
        DistanceKnown: true,
        TargetEdgeDistanceYalms: distance,
        SotenReady: true,
        MineuchiReady: true,
        BoundPresent: false,
        SotenApproachWindowOpen: !protectionPresent,
        ConfiguredSotenMaximumRangeYalms:
            SamuraiReactiveCounterCcRules.SotenMaximumRangeYalms);

    private static SamuraiZantetsukenObservation ZantetsukenObservation(
        byte shieldPercentage) => new(
        Enabled: true,
        HardReset: false,
        ExactTargetStillCurrent: true,
        TargetAliveAndTargetable: true,
        ExactGameplayKeyStillDown: true,
        OwnSourceKuzushiCount: 1,
        ShieldPercentage: shieldPercentage,
        BoundPresent: false,
        ZantetsukenReady: true,
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
            throw new InvalidOperationException(
                $"{label}: expected {expected}, got {actual}");
    }
}
