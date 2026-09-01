using SeitonSense.Core;

internal static class SamuraiReactiveSelfTests
{
    public static void ZantetsukenKuzushiEvidenceRequiresFreshFiniteOwnStatus()
    {
        const uint localSamurai = 0x1000_1234;
        True(
            SamuraiZantetsukenRules.IsExactCurrentOwnSourceKuzushi(
                SamuraiZantetsukenRules.KuzushiStatusId,
                localSamurai,
                4f,
                localSamurai),
            "fresh four-second own Kuzushi");
        True(
            SamuraiZantetsukenRules.IsExactCurrentOwnSourceKuzushi(
                SamuraiZantetsukenRules.KuzushiStatusId,
                localSamurai,
                6f,
                localSamurai),
            "fresh extended six-second own Kuzushi");

        foreach (var remaining in new[]
                 {
                     0f,
                     -0.001f,
                     float.NaN,
                     float.PositiveInfinity,
                 })
        {
            False(
                SamuraiZantetsukenRules.IsExactCurrentOwnSourceKuzushi(
                    SamuraiZantetsukenRules.KuzushiStatusId,
                    localSamurai,
                    remaining,
                    localSamurai),
                "expired or non-finite Kuzushi cannot authorize Zantetsuken");
        }

        False(
            SamuraiZantetsukenRules.IsExactCurrentOwnSourceKuzushi(
                SamuraiZantetsukenRules.KuzushiStatusId,
                localSamurai + 1,
                4f,
                localSamurai),
            "another Samurai's Kuzushi is not ours");
        False(
            SamuraiZantetsukenRules.IsExactCurrentOwnSourceKuzushi(
                SamuraiZantetsukenRules.KuzushiStatusId + 1,
                localSamurai,
                4f,
                localSamurai),
            "another status is not Kuzushi");
        False(
            SamuraiZantetsukenRules.IsExactCurrentOwnSourceKuzushi(
                SamuraiZantetsukenRules.KuzushiStatusId,
                localSamurai,
                4f,
                0),
            "invalid local identity fails closed");
    }

    private const int HeldKey = 65;
    private static readonly TargetPressureActorIdentity LocalSamurai = new(
        GameObjectId: 0x1000_1234,
        EntityId: 0x1234);
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

    public static void ZantetsukenAutomaticGateBlocksOnlyExactHardProtection()
    {
        var armed = SamuraiZantetsukenRules.Arm(Enemy, 2_000);
        True(armed.IsActive, "automatic exact Zantetsuken intent");
        Equal(
            SamuraiZantetsukenRules.ActionId,
            SamuraiZantetsukenRules.Observe(
                armed,
                ZantetsukenObservation()).ActionId,
            "automatic intent needs no key but does require exact own Kuzushi");
        Equal(
            SamuraiZantetsukenDecisionKind.Cancelled,
            SamuraiZantetsukenRules.Observe(
                armed,
                ZantetsukenObservation() with
                {
                    ExactOwnSourceKuzushiPresent = false,
                }).Kind,
            "missing exact own-source Kuzushi cancels before any native attempt");
        Equal(
            SamuraiZantetsukenDecisionKind.Cancelled,
            SamuraiZantetsukenRules.Observe(
                armed,
                ZantetsukenObservation(executeBlockingProtectionCount: 1)).Kind,
            "exact Covered or invulnerability cancels");
        Equal(
            SamuraiZantetsukenDecisionKind.Cancelled,
            SamuraiZantetsukenRules.Observe(
                armed,
                ZantetsukenObservation() with
                {
                    HasNativeRangeAndLineOfSight = false,
                }).Kind,
            "frozen endpoint reachability drift cancels before reranking");
        Equal(
            SamuraiZantetsukenDecisionKind.Waiting,
            SamuraiZantetsukenRules.Observe(
                armed,
                ZantetsukenObservation() with
                {
                    BoundPresent = true,
                }).Kind,
            "Bind waits without spending the automatic intent");
        True(
            SamuraiZantetsukenTargetSelectionRules.IsSelectableTarget(
                ZantetsukenCandidate(1, x: 0f) with
                {
                    ShieldPercentage = 100,
                }),
            "exact own Kuzushi is required but shield remains a ranking input, not a gate");

        False(
            NinjaSeitonProtectionStatusCatalog.IsExecuteBlockingStatus(
                SmartActionProtectionRules.GuardStatusId),
            "Guard remains eligible");
        False(
            NinjaSeitonProtectionStatusCatalog.IsExecuteBlockingStatus(
                SmartActionProtectionRules.ChitenStatusId),
            "Chiten remains eligible");
        True(
            NinjaSeitonProtectionStatusCatalog.IsExecuteBlockingStatus(
                NinjaSeitonProtectionStatusCatalog.CoveredPvpAlternateStatusId),
            "Covered blocks");
        True(
            NinjaSeitonProtectionStatusCatalog.IsExecuteBlockingStatus(
                NinjaSeitonProtectionStatusCatalog.HallowedGroundStatusId),
            "Hallowed Ground blocks");
        True(
            NinjaSeitonProtectionStatusCatalog.IsExecuteBlockingStatus(
                NinjaSeitonProtectionStatusCatalog.UndeadRedemptionStatusId),
            "Undead Redemption blocks");

        var joblessDummy = Enemy with { JobId = 0 };
        False(
            SamuraiZantetsukenRules.Arm(joblessDummy, 2_000).IsActive,
            "jobless target rejected in normal CC");
        True(
            SamuraiZantetsukenRules.Arm(
                joblessDummy,
                2_000,
                allowJoblessWolvesDenTarget: true).IsActive,
            "reviewed Wolves Den dummy opt-in");
    }

    public static void ZantetsukenCollectsForFifteenHundredMillisecondsBeforeSelection()
    {
        Equal(
            1_500L,
            SamuraiZantetsukenRules.CollectionDelayMilliseconds,
            "fixed Kuzushi collection delay");

        var noEvidence = SamuraiZantetsukenRules.ObserveCollection(
            SamuraiZantetsukenCollectionState.Initial,
            ZantetsukenCollectionObservation(
                nowMilliseconds: 1_000,
                exactOwnSourceKuzushiPresent: false));
        False(noEvidence.NextState.IsCollecting, "LB readiness alone does not start collection");
        False(noEvidence.CanSelectAndFreezeTarget, "no Kuzushi cannot select a target");

        var firstEvidence = SamuraiZantetsukenRules.ObserveCollection(
            noEvidence.NextState,
            ZantetsukenCollectionObservation(nowMilliseconds: 1_100));
        True(firstEvidence.NextState.IsCollecting, "first exact own Kuzushi starts collection");
        Equal(
            1_100L,
            firstEvidence.NextState.FirstExactOwnSourceKuzushiAtMilliseconds,
            "collection begins at the first evidence timestamp");
        False(firstEvidence.CanSelectAndFreezeTarget, "first evidence never fires immediately");

        var candidatesAtFirstEvidence = new[]
        {
            ZantetsukenCandidate(1, x: 0f),
            ZantetsukenCandidate(2, x: 10f) with
            {
                OwnSourceKuzushiCount = 0,
            },
            ZantetsukenCandidate(3, x: 14f) with
            {
                OwnSourceKuzushiCount = 0,
            },
        };
        Equal(
            0,
            SamuraiZantetsukenTargetSelectionRules.SelectBestEligibleTargetIndex(
                candidatesAtFirstEvidence),
            "only the first Kuzushi carrier is selectable at collection start");

        var additionalEvidence = SamuraiZantetsukenRules.ObserveCollection(
            firstEvidence.NextState,
            ZantetsukenCollectionObservation(nowMilliseconds: 2_000));
        Equal(
            1_100L,
            additionalEvidence.NextState.FirstExactOwnSourceKuzushiAtMilliseconds,
            "later Kuzushi evidence does not restart the window");
        False(additionalEvidence.CanSelectAndFreezeTarget, "collection remains closed before 1.5 seconds");

        var beforeBoundary = SamuraiZantetsukenRules.ObserveCollection(
            additionalEvidence.NextState,
            ZantetsukenCollectionObservation(nowMilliseconds: 2_599));
        False(beforeBoundary.CanSelectAndFreezeTarget, "1499 milliseconds remains too early");
        False(
            SamuraiZantetsukenRules.HasCollectionDelayElapsed(
                beforeBoundary.NextState,
                2_599),
            "pure elapsed gate is closed at 1499 milliseconds");

        var atBoundary = SamuraiZantetsukenRules.ObserveCollection(
            beforeBoundary.NextState,
            ZantetsukenCollectionObservation(nowMilliseconds: 2_600));
        True(atBoundary.CanSelectAndFreezeTarget, "1500 millisecond boundary opens target ranking");
        True(
            SamuraiZantetsukenRules.HasCollectionDelayElapsed(
                atBoundary.NextState,
                2_600),
            "pure elapsed gate opens at exactly 1500 milliseconds");
        True(
            SamuraiZantetsukenRules.CanSelectAndFreezeTarget(
                atBoundary.NextState,
                ZantetsukenCollectionObservation(nowMilliseconds: 2_600)),
            "pure final boundary recheck opens only at the exact deadline");

        var candidatesAtMaturity = candidatesAtFirstEvidence.ToArray();
        candidatesAtMaturity[0] = candidatesAtMaturity[0] with
        {
            OwnSourceKuzushiCount = 0,
        };
        candidatesAtMaturity[1] = candidatesAtMaturity[1] with
        {
            OwnSourceKuzushiCount = 1,
        };
        Equal(
            1,
            SamuraiZantetsukenTargetSelectionRules.SelectBestEligibleTargetIndex(
                candidatesAtMaturity),
            "a later Kuzushi carrier can replace the expired first carrier and win the larger current 5y cluster");
        Equal(
            2,
            SamuraiZantetsukenTargetSelectionRules.CountUsefulClusterMembers(
                candidatesAtMaturity,
                1),
            "the later carrier reaches itself and its nearby cluster member");

        var armed = SamuraiZantetsukenRules.Arm(Enemy, 2_600);
        Equal(
            SamuraiZantetsukenRules.ActionId,
            SamuraiZantetsukenRules.Observe(
                armed,
                ZantetsukenObservation()).ActionId,
            "a freshly ranked valid target may attempt at the exact collection deadline");
        Equal(
            SamuraiZantetsukenDecisionKind.Cancelled,
            SamuraiZantetsukenRules.Observe(
                armed,
                ZantetsukenObservation() with
                {
                    ExactOwnSourceKuzushiPresent = false,
                }).Kind,
            "an open collection gate never replaces the final Kuzushi check");
        Equal(
            SamuraiZantetsukenDecisionKind.Cancelled,
            SamuraiZantetsukenRules.Observe(
                armed,
                ZantetsukenObservation(executeBlockingProtectionCount: 1)).Kind,
            "an open collection gate never replaces the final protection check");
        Equal(
            SamuraiZantetsukenDecisionKind.Cancelled,
            SamuraiZantetsukenRules.Observe(
                armed,
                ZantetsukenObservation() with
                {
                    ExactTargetStillCurrent = false,
                }).Kind,
            "an open collection gate never replaces the final frozen-target identity check");
    }

    public static void ZantetsukenCollectionResetsAndFailsClosedOnInvalidTime()
    {
        var collecting = SamuraiZantetsukenRules.ObserveCollection(
            SamuraiZantetsukenCollectionState.Initial,
            ZantetsukenCollectionObservation(nowMilliseconds: 1_000));

        var transientGap = SamuraiZantetsukenRules.ObserveCollection(
            collecting.NextState,
            ZantetsukenCollectionObservation(
                nowMilliseconds: 1_200,
                exactOwnSourceKuzushiPresent: false));
        Equal(
            collecting.NextState,
            transientGap.NextState,
            "pre-deadline status flicker preserves the first evidence timestamp");
        False(transientGap.CanSelectAndFreezeTarget, "status flicker cannot open collection");

        var noEvidenceAtMaturity = SamuraiZantetsukenRules.ObserveCollection(
            transientGap.NextState,
            ZantetsukenCollectionObservation(
                nowMilliseconds: 2_500,
                exactOwnSourceKuzushiPresent: false));
        Equal(
            SamuraiZantetsukenCollectionState.Initial,
            noEvidenceAtMaturity.NextState,
            "mature window without current Kuzushi resets");
        False(
            noEvidenceAtMaturity.CanSelectAndFreezeTarget,
            "mature missing evidence fails closed");

        foreach (var invalid in new[]
                 {
                     ZantetsukenCollectionObservation(
                         nowMilliseconds: 1_200,
                         enabled: false),
                     ZantetsukenCollectionObservation(
                         nowMilliseconds: 1_200,
                         hardReset: true),
                     ZantetsukenCollectionObservation(nowMilliseconds: -1),
                 })
        {
            var reset = SamuraiZantetsukenRules.ObserveCollection(
                collecting.NextState,
                invalid);
            Equal(
                SamuraiZantetsukenCollectionState.Initial,
                reset.NextState,
                "invalid collection observation resets state");
            False(reset.CanSelectAndFreezeTarget, "invalid observation fails closed");
        }

        var contextDrift = SamuraiZantetsukenRules.ObserveCollection(
            collecting.NextState,
            ZantetsukenCollectionObservation(
                nowMilliseconds: 1_200,
                context: SupportedPvPContext.WolvesDen));
        Equal(
            SamuraiZantetsukenCollectionState.Initial,
            contextDrift.NextState,
            "PvP context drift resets collection");
        False(contextDrift.CanSelectAndFreezeTarget, "context drift fails closed");

        var localDrift = SamuraiZantetsukenRules.ObserveCollection(
            collecting.NextState,
            ZantetsukenCollectionObservation(
                nowMilliseconds: 1_200,
                localPlayer: LocalSamurai with
                {
                    EntityId = LocalSamurai.EntityId + 1,
                }));
        Equal(
            SamuraiZantetsukenCollectionState.Initial,
            localDrift.NextState,
            "local actor drift resets collection");
        False(localDrift.CanSelectAndFreezeTarget, "local actor drift fails closed");

        var backwards = SamuraiZantetsukenRules.ObserveCollection(
            collecting.NextState,
            ZantetsukenCollectionObservation(nowMilliseconds: 999));
        Equal(
            SamuraiZantetsukenCollectionState.Initial,
            backwards.NextState,
            "backwards clock resets collection");
        False(backwards.CanSelectAndFreezeTarget, "backwards clock cannot open the gate");

        var malformed = SamuraiZantetsukenRules.ObserveCollection(
            new SamuraiZantetsukenCollectionState(
                SupportedPvPContext.CrystallineConflict,
                LocalSamurai,
                -2),
            ZantetsukenCollectionObservation(nowMilliseconds: 10_000));
        Equal(
            SamuraiZantetsukenCollectionState.Initial,
            malformed.NextState,
            "malformed collection state resets");
        False(malformed.CanSelectAndFreezeTarget, "malformed state cannot select a target");

        var restarted = SamuraiZantetsukenRules.ObserveCollection(
            SamuraiZantetsukenCollectionState.Initial,
            ZantetsukenCollectionObservation(nowMilliseconds: 2_000));
        Equal(
            2_000L,
            restarted.NextState.FirstExactOwnSourceKuzushiAtMilliseconds,
            "new evidence after reset starts a fresh full delay");
        False(restarted.CanSelectAndFreezeTarget, "fresh generation does not inherit elapsed time");
        False(
            SamuraiZantetsukenRules.CanSelectAndFreezeTarget(
                restarted.NextState,
                ZantetsukenCollectionObservation(nowMilliseconds: 3_499)),
            "pure final gate rejects one millisecond before maturity");
        False(
            SamuraiZantetsukenRules.CanSelectAndFreezeTarget(
                restarted.NextState,
                ZantetsukenCollectionObservation(
                    nowMilliseconds: 3_500,
                    exactOwnSourceKuzushiPresent: false)),
            "pure final gate requires current exact own Kuzushi");
        False(
            SamuraiZantetsukenRules.CanSelectAndFreezeTarget(
                restarted.NextState,
                ZantetsukenCollectionObservation(
                    nowMilliseconds: 3_500,
                    context: SupportedPvPContext.WolvesDen)),
            "pure final gate rechecks the exact PvP context");
        False(
            SamuraiZantetsukenRules.CanSelectAndFreezeTarget(
                restarted.NextState,
                ZantetsukenCollectionObservation(
                    nowMilliseconds: 3_500,
                    localPlayer: LocalSamurai with
                    {
                        GameObjectId = LocalSamurai.GameObjectId + 1,
                    })),
            "pure final gate rechecks the exact local actor");
    }

    public static void ZantetsukenRanksLargestVulnerableFiveYalmCluster()
    {
        var candidates = new[]
        {
            ZantetsukenCandidate(1, x: 0f) with
            {
                OwnSourceKuzushiCount = 0,
            },
            ZantetsukenCandidate(2, x: 4f),
            ZantetsukenCandidate(3, x: 8f) with
            {
                OwnSourceKuzushiCount = 0,
            },
        };
        Equal(
            1,
            SamuraiZantetsukenTargetSelectionRules
                .SelectBestEligibleTargetIndex(candidates),
            "middle endpoint reaches the largest 5y cluster");
        Equal(
            3,
            SamuraiZantetsukenTargetSelectionRules
                .CountUsefulClusterMembers(candidates, 1),
            "selected Kuzushi endpoint counts nearby non-Kuzushi hitboxes");

        var protectedMiddle = new[]
        {
            ZantetsukenCandidate(1, x: 0f),
            ZantetsukenCandidate(2, x: 4f) with
            {
                ExecuteBlockingProtectionCount = 1,
            },
            ZantetsukenCandidate(3, x: 8f),
        };
        Equal(
            0,
            SamuraiZantetsukenTargetSelectionRules
                .SelectBestEligibleTargetIndex(protectedMiddle),
            "protected endpoint and cluster member are excluded");
        Equal(
            1,
            SamuraiZantetsukenTargetSelectionRules
                .CountUsefulClusterMembers(protectedMiddle, 0),
            "protected nearby actor adds no useful cluster score");

        var executeTie = new[]
        {
            ZantetsukenCandidate(1, x: 0f, currentHp: 80_000) with
            {
                OwnSourceKuzushiCount = 0,
            },
            ZantetsukenCandidate(2, x: 20f, currentHp: 90_000),
        };
        Equal(
            1,
            SamuraiZantetsukenTargetSelectionRules
                .SelectBestEligibleTargetIndex(executeTie),
            "equal clusters prefer own unshielded Kuzushi before HP");

        var healthThenSlotTie = new[]
        {
            ZantetsukenCandidate(4, x: 0f, currentHp: 40_000),
            ZantetsukenCandidate(2, x: 20f, currentHp: 30_000),
        };
        Equal(
            1,
            SamuraiZantetsukenTargetSelectionRules
                .SelectBestEligibleTargetIndex(healthThenSlotTie),
            "equal cluster and execute value prefer lower HP ratio");
        healthThenSlotTie[0] = healthThenSlotTie[0] with { CurrentHp = 30_000 };
        Equal(
            1,
            SamuraiZantetsukenTargetSelectionRules
                .SelectBestEligibleTargetIndex(healthThenSlotTie),
            "remaining tie uses lower stable S-slot");
    }

    public static void ZantetsukenClusterRankingFailsClosedAndRequiresReachability()
    {
        var candidates = new[]
        {
            ZantetsukenCandidate(1, x: 0f),
            ZantetsukenCandidate(2, x: 4f) with
            {
                HasNativeRangeAndLineOfSight = false,
            },
            ZantetsukenCandidate(3, x: 8f),
        };
        Equal(
            0,
            SamuraiZantetsukenTargetSelectionRules
                .SelectBestEligibleTargetIndex(candidates),
            "unreachable best endpoint cannot be selected; stable tie remains");

        var duplicate = new[]
        {
            ZantetsukenCandidate(1, x: 0f),
            ZantetsukenCandidate(1, x: 4f) with
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
                .SelectBestEligibleTargetIndex(duplicate),
            "ambiguous native slot set fails closed");

        var invalidGeometry = new[]
        {
            ZantetsukenCandidate(1, x: 0f),
            ZantetsukenCandidate(2, x: float.NaN),
        };
        Equal(
            -1,
            SamuraiZantetsukenTargetSelectionRules
                .SelectBestEligibleTargetIndex(invalidGeometry),
            "unknown geometry fails the complete snapshot closed");

        var noKuzushi = new[]
        {
            ZantetsukenCandidate(1, x: 0f) with
            {
                OwnSourceKuzushiCount = 0,
            },
            ZantetsukenCandidate(2, x: 4f) with
            {
                OwnSourceKuzushiCount = 0,
            },
        };
        Equal(
            -1,
            SamuraiZantetsukenTargetSelectionRules
                .SelectBestEligibleTargetIndex(noKuzushi),
            "LB readiness without exact own Kuzushi has no automatic endpoint");

        var duplicateOwnKuzushi = new[]
        {
            ZantetsukenCandidate(1, x: 0f) with
            {
                OwnSourceKuzushiCount = 2,
            },
        };
        Equal(
            -1,
            SamuraiZantetsukenTargetSelectionRules
                .SelectBestEligibleTargetIndex(duplicateOwnKuzushi),
            "duplicate own-source Kuzushi rows fail closed");
    }

    private static SamuraiZantetsukenTargetCandidate ZantetsukenCandidate(
        int slot,
        float x,
        uint currentHp = 50_000) => new(
        slot,
        new SamuraiReactiveCounterCcTarget(
            GameObjectId: (ulong)(0x3000 + slot),
            EntityId: (uint)(0x300 + slot),
            JobId: 23),
        ExactCanonicalIdentity: true,
        AliveAndTargetable: true,
        CurrentHp: currentHp,
        MaximumHp: 100_000,
        OwnSourceKuzushiCount: 1,
        ShieldPercentage: 0,
        ExecuteBlockingProtectionCount: 0,
        HasNativeRangeAndLineOfSight: true,
        Position: new System.Numerics.Vector3(x, 0f, 0f),
        HitboxRadius: 0f);

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
        int executeBlockingProtectionCount = 0) => new(
        Enabled: true,
        HardReset: false,
        ExactTargetStillCurrent: true,
        TargetAliveAndTargetable: true,
        ExactOwnSourceKuzushiPresent: true,
        ExecuteBlockingProtectionCount: executeBlockingProtectionCount,
        BoundPresent: false,
        ZantetsukenReady: true,
        HasNativeRangeAndLineOfSight: true);

    private static SamuraiZantetsukenCollectionObservation
        ZantetsukenCollectionObservation(
            long nowMilliseconds,
            bool exactOwnSourceKuzushiPresent = true,
            bool enabled = true,
            bool hardReset = false,
            SupportedPvPContext context =
                SupportedPvPContext.CrystallineConflict,
            TargetPressureActorIdentity? localPlayer = null) => new(
            Enabled: enabled,
            HardReset: hardReset,
            Context: context,
            LocalPlayer: localPlayer ?? LocalSamurai,
            ExactOwnSourceKuzushiPresent: exactOwnSourceKuzushiPresent,
            NowMilliseconds: nowMilliseconds);

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
