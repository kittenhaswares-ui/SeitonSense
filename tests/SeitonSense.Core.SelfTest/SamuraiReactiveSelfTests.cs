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

    public static void PredictiveTimingRequiresExactWarmEvidence()
    {
        var exactEffect = new SamuraiReactiveActionEffectSignal(
            ObservedAtMilliseconds: 1_400,
            CasterEntityId: 0x100,
            TargetEntityId: Enemy.EntityId,
            ActionId: SamuraiReactiveCounterCcRules.SotenActionId,
            GlobalSequence: 7,
            SourceSequence: 11);
        True(exactEffect.IsValid, "exact local Soten effect sequence");
        False(
            (exactEffect with { SourceSequence = 0 }).IsValid,
            "unbound ActionEffect cannot teach timing");
        False(
            (exactEffect with { TargetEntityId = 0x100 }).IsValid,
            "self-target effect cannot teach hostile timing");
        True(
            SamuraiReactiveRuntimeRules.CanRegisterExactTimingAttempt(
                attemptedAtMilliseconds: 1_001,
                registrationNowMilliseconds: 1_002),
            "native attempt after frame-start still registers against fresh time");
        False(
            SamuraiReactiveRuntimeRules.CanRegisterExactTimingAttempt(
                attemptedAtMilliseconds: 1_003,
                registrationNowMilliseconds: 1_002),
            "genuinely future native attempt is rejected");

        var oneSoten = Samples((420, 15f));
        var fiveMineuchi = Samples(
            (200, 1f),
            (210, 2f),
            (220, 3f),
            (230, 4f),
            (240, 5f));
        False(
            SamuraiReactivePredictiveTimingRules.TryGetCombinedTiming(
                oneSoten,
                15f,
                fiveMineuchi,
                out _),
            "one exact Soten sample stays in conservative fallback");

        var twoSoten = Samples(
            (420, 15f),
            (450, 15.3f));
        True(
            SamuraiReactivePredictiveTimingRules.TryGetCombinedTiming(
                twoSoten,
                15f,
                fiveMineuchi,
                out var timing),
            "two current Soten and five Mineuchi samples arm prediction");
        var expectedMineuchiLead = 200 -
            ReactiveCounterCcImpactTimingRules.LandingSafetyMarginMilliseconds;
        var expectedCombinedLead = 450 + expectedMineuchiLead;
        Equal(450, timing.SotenTransitMilliseconds, "slowest safe Soten transit");
        Equal(expectedMineuchiLead, timing.MineuchiSafeLeadMilliseconds, "Mineuchi lands after expiry");
        Equal(expectedCombinedLead, timing.CombinedSotenLeadMilliseconds, "combined approach lead");
        True(
            SamuraiReactivePredictiveTimingRules.ShouldStartPredictiveSoten(
                1,
                expectedCombinedLead,
                timing),
            "Soten starts at its learned combined window");
        False(
            SamuraiReactivePredictiveTimingRules.ShouldStartPredictiveSoten(
                1,
                expectedCombinedLead + 1,
                timing),
            "Soten never starts before its learned window");
        True(
            SamuraiReactivePredictiveTimingRules.ShouldStartPredictiveMineuchi(
                1,
                expectedMineuchiLead,
                timing.MineuchiSafeLeadMilliseconds),
            "Mineuchi starts only in its final learned window");
        False(
            SamuraiReactivePredictiveTimingRules.ShouldStartPredictiveMineuchi(
                1,
                expectedMineuchiLead + 1,
                timing.MineuchiSafeLeadMilliseconds),
            "Mineuchi does not fire early into protection");

        var nearerOnlySoten = Samples(
            (300, 8f),
            (310, 9f),
            (320, 10f),
            (330, 11f),
            (340, 12f));
        False(
            SamuraiReactivePredictiveTimingRules.TryGetSotenTransitMilliseconds(
                nearerOnlySoten,
                15f,
                out _),
            "nearer Soten samples cannot teach a farther dash");

        var unstableMineuchi = Samples(
            (600, 1f),
            (610, 2f),
            (620, 3f),
            (630, 4f),
            (200, 5f));
        True(
            SamuraiReactivePredictiveTimingRules
                .TryGetMineuchiSafeLeadMilliseconds(
                    unstableMineuchi,
                    5f,
                    out var collapsedLead),
            "a newly observed faster effect keeps only a late safe window");
        Equal(
            ReactiveCounterCcImpactTimingRules.MinimumUsefulLeadMilliseconds,
            collapsedLead,
            "faster effect collapses prediction to the latest useful edge");

        var armed = SamuraiReactiveCounterCcRules.Arm(Enemy, HeldKey, 1_000);
        var acceptedSoten = SamuraiReactiveCounterCcRules.CompleteAttempt(
            armed,
            SamuraiReactiveCounterCcRules.SotenActionId,
            ClientActionAttemptOutcome.ClientAccepted);
        Equal(
            SamuraiReactiveCounterCcDecisionKind.Waiting,
            SamuraiReactiveCounterCcRules.Observe(
                acceptedSoten,
                CounterObservation(distance: 4f, protectionPresent: true)).Kind,
            "arrived SAM still waits outside Mineuchi final window");
        Equal(
            SamuraiReactiveCounterCcRules.MineuchiActionId,
            SamuraiReactiveCounterCcRules.Observe(
                acceptedSoten,
                CounterObservation(distance: 4f, protectionPresent: true) with
                {
                    MineuchiImpactWindowOpen = true,
                }).ActionId,
            "measured final window authorizes exact Mineuchi");
    }

    public static void ProtectionEndConsentUsesTheCurrentHeldKey()
    {
        False(
            SamuraiReactiveCounterCcRules.CanAcquireProtectionEndConsent(
                protectionObserved: true,
                protectionPresent: true,
                HeldKey),
            "a key held during active protection does not prematurely arm");
        True(
            SamuraiReactiveCounterCcRules.CanAcquireProtectionEndConsent(
                protectionObserved: true,
                protectionPresent: false,
                HeldKey + 1),
            "the current key at the authoritative release edge arms");
        False(
            SamuraiReactiveCounterCcRules.CanAcquireProtectionEndConsent(
                protectionObserved: false,
                protectionPresent: false,
                HeldKey + 1),
            "status absence without observed protection cannot arm");

        var armedAtRelease = SamuraiReactiveCounterCcRules.Arm(
            Enemy,
            HeldKey,
            1_000);
        var rebound = SamuraiReactiveCounterCcRules.RebindUncommittedHeldConsent(
            armedAtRelease,
            HeldKey + 1);
        Equal(HeldKey + 1, rebound.GameplayKeyToken, "W to A switch uses current consent");

        Equal(
            SamuraiReactiveCounterCcDecisionKind.Cancelled,
            SamuraiReactiveCounterCcRules.Observe(
                rebound,
                CounterObservation(distance: 15f, protectionPresent: false) with
                {
                    ExactGameplayKeyStillDown = false,
                }).Kind,
            "uncommitted Soten still requires a current held key");

        var committed = SamuraiReactiveCounterCcRules.CompleteAttempt(
            rebound,
            SamuraiReactiveCounterCcRules.SotenActionId,
            ClientActionAttemptOutcome.ClientAccepted);
        Equal(
            SamuraiReactiveCounterCcRules.MineuchiActionId,
            SamuraiReactiveCounterCcRules.Observe(
                committed,
                CounterObservation(distance: 4f, protectionPresent: false) with
                {
                    ExactGameplayKeyStillDown = false,
                }).ActionId,
            "accepted Soten completes Mineuchi after key release or switch");
        Equal(
            HeldKey + 1,
            SamuraiReactiveCounterCcRules.RebindUncommittedHeldConsent(
                committed,
                HeldKey + 2).GameplayKeyToken,
            "committed approach cannot be rebound to a later input generation");
    }

    public static void WolvesDenUsesExactCurrentTargetAndTargetedActions()
    {
        Equal(
            SamuraiReactiveCounterCcNativeInvocationKind.TargetedUseAction,
            SamuraiReactiveCounterCcRules.GetNativeInvocationKind(
                SamuraiReactiveCounterCcRules.SotenActionId),
            "Soten is a hostile targeted action, not a ground-target action");
        Equal(
            SamuraiReactiveCounterCcNativeInvocationKind.TargetedUseAction,
            SamuraiReactiveCounterCcRules.GetNativeInvocationKind(
                SamuraiReactiveCounterCcRules.MineuchiActionId),
            "Mineuchi uses the same exact target boundary");
        Equal(
            SamuraiReactiveCounterCcNativeInvocationKind.None,
            SamuraiReactiveCounterCcRules.GetNativeInvocationKind(123),
            "unknown action cannot cross the SAM native boundary");

        True(
            SamuraiReactiveRuntimeRules.IsExactWolvesDenCurrentTarget(
                localEntityId: 0x100,
                signalCasterEntityId: Enemy.EntityId,
                currentTargetEntityId: Enemy.EntityId),
            "enemy protection signal matches the current Wolves Den target");
        False(
            SamuraiReactiveRuntimeRules.IsExactWolvesDenCurrentTarget(
                localEntityId: 0x100,
                signalCasterEntityId: Enemy.EntityId,
                currentTargetEntityId: Enemy.EntityId + 1),
            "a different current target never receives the frozen signal");
        False(
            SamuraiReactiveRuntimeRules.IsExactWolvesDenCurrentTarget(
                localEntityId: Enemy.EntityId,
                signalCasterEntityId: Enemy.EntityId,
                currentTargetEntityId: Enemy.EntityId),
            "the local actor can never become its own Wolves Den counter target");
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

    public static void ZantetsukenRanksFarthestReachableEligibleTargetThenSlot()
    {
        var candidates = new[]
        {
            ZantetsukenCandidate(1, edgeDistance: 8f),
            ZantetsukenCandidate(2, edgeDistance: 18f),
            ZantetsukenCandidate(3, edgeDistance: 12f),
        };
        Equal(
            1,
            SamuraiZantetsukenTargetSelectionRules
                .SelectFarthestEligibleTargetIndex(candidates),
            "farthest exact eligible target wins");

        var ineligibleFarthest = new[]
        {
            ZantetsukenCandidate(1, edgeDistance: 19f) with
            {
                ShieldPercentage = 1,
            },
            ZantetsukenCandidate(2, edgeDistance: 18f) with
            {
                OwnSourceKuzushiCount = 0,
            },
            ZantetsukenCandidate(3, edgeDistance: 11f),
        };
        Equal(
            2,
            SamuraiZantetsukenTargetSelectionRules
                .SelectFarthestEligibleTargetIndex(ineligibleFarthest),
            "shielded or non-owned Kuzushi targets are not selected");

        var slotTie = new[]
        {
            ZantetsukenCandidate(4, edgeDistance: 14f),
            ZantetsukenCandidate(2, edgeDistance: 14f),
        };
        Equal(
            1,
            SamuraiZantetsukenTargetSelectionRules
                .SelectFarthestEligibleTargetIndex(slotTie),
            "equal distance uses lower S-slot");
    }

    public static void ZantetsukenFarthestRankingFailsClosedAndRequiresReachability()
    {
        var candidates = new[]
        {
            ZantetsukenCandidate(1, edgeDistance: 18f) with
            {
                HasNativeRangeAndLineOfSight = false,
            },
            ZantetsukenCandidate(2, edgeDistance: 12f),
        };
        Equal(
            1,
            SamuraiZantetsukenTargetSelectionRules
                .SelectFarthestEligibleTargetIndex(candidates),
            "unreachable endpoint cannot be selected");

        var duplicate = new[]
        {
            ZantetsukenCandidate(1, edgeDistance: 10f),
            ZantetsukenCandidate(1, edgeDistance: 15f) with
            {
                Target = new SamuraiReactiveCounterCcTarget(
                    GameObjectId: 0x3002,
                    EntityId: 0x302,
                    JobId: 23),
            },
        };
        Equal(
            -1,
            SamuraiZantetsukenTargetSelectionRules
                .SelectFarthestEligibleTargetIndex(duplicate),
            "ambiguous native slot set fails closed");

        var invalidDistance = new[]
        {
            ZantetsukenCandidate(1, edgeDistance: 10f),
            ZantetsukenCandidate(2, edgeDistance: float.NaN),
        };
        Equal(
            -1,
            SamuraiZantetsukenTargetSelectionRules
                .SelectFarthestEligibleTargetIndex(invalidDistance),
            "unknown edge distance fails the complete snapshot closed");
    }

    private static SamuraiZantetsukenTargetCandidate ZantetsukenCandidate(
        int slot,
        float edgeDistance) => new(
        slot,
        new SamuraiReactiveCounterCcTarget(
            GameObjectId: (ulong)(0x3000 + slot),
            EntityId: (uint)(0x300 + slot),
            JobId: 23),
        ExactCanonicalIdentity: true,
        AliveAndTargetable: true,
        OwnSourceKuzushiCount: 1,
        ShieldPercentage: 0,
        HasNativeRangeAndLineOfSight: true,
        TargetEdgeDistanceYalms: edgeDistance);

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

    private static ReactiveCounterCcImpactSample[] Samples(
        params (int DelayMilliseconds, float EdgeDistanceYalms)[] values)
    {
        var samples = new List<ReactiveCounterCcImpactSample>();
        foreach (var value in values)
        {
            True(
                ReactiveCounterCcImpactTimingRules.TryCreateCalibrationSample(
                    value.DelayMilliseconds,
                    value.EdgeDistanceYalms,
                    out var sample),
                "valid timing sample fixture");
            samples.Add(sample);
        }

        return samples.ToArray();
    }

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
