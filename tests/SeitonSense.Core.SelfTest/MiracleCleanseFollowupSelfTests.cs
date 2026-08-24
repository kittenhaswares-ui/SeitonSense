using SeitonSense.Core;

internal static class MiracleCleanseFollowupSelfTests
{
    internal static void ExactPurifySignalAcceptsActionLevelOrKnownRecovery()
    {
        foreach (var statusId in new ushort[] { 1_343, 1_344, 1_345, 1_347, 3_085, 3_219 })
            True(IsExact(effectValue: statusId), $"exact self-Purify recovery {statusId}");

        True(IsExact(effectType: 0, effectValue: 0), "exact self-Purify action packet without exposed recovery");

        False(IsExact(action: 29_057), "wrong action");
        False(IsExact(target: 11), "not self-targeted");
        False(IsExact(effectType: 0x0E), "status add is not recovery");
        False(IsExact(effectType: 0, effectValue: 1_343), "action-level sentinel must be exactly zero/zero");
        False(IsExact(effectValue: 9_999), "unknown recovery status excluded");
        False(IsExact(caster: 0, target: 0), "invalid actor IDs");
        False(IsExact(globalSequence: 0, sourceSequence: 0), "missing packet identity");
    }

    internal static void ValidatedSignalRetriesOnlyCanonicalResolutionInsideOriginalDeadline()
    {
        var target = Target(9);
        var signal = Signal(target, sequence: 99, now: 1_000);
        var key = signal.Key;
        var first = MiracleCleanseFollowupRules.RetireValidatedSignal(
            MiracleCleanseFollowupSignalLedger.Initial,
            key);
        True(first.IsNewValidatedSignal, "first validated packet is terminally remembered");

        var pending = new MiracleCleanseFollowupPendingResolution(
            key,
            signal.ObservedAtMilliseconds,
            LocalEntityId: 100,
            LocalCounterJobId: 24,
            FeatureGeneration: 7);
        var unresolved = MiracleCleanseFollowupRules.ResolvePendingSignal(
            pending,
            ResolutionObservation(target: null, now: 1_000));
        True(unresolved.ShouldRetry, "one transient canonical miss retains only the same exact signal");
        Equal(pending, unresolved.NextPending!.Value, "canonical retry cannot replace or mutate signal identity");
        False(unresolved.DidResolve, "missing canonical identity cannot arm the lifecycle");

        var duplicate = MiracleCleanseFollowupRules.RetireValidatedSignal(
            first.NextState,
            key);
        False(duplicate.IsNewValidatedSignal, "duplicate packet cannot enqueue or extend resolution");
        Equal(1, duplicate.NextState.RetiredSignals.Length, "duplicate adds no second retirement");

        var beforeDeadline = MiracleCleanseFollowupRules.ResolvePendingSignal(
            pending,
            ResolutionObservation(target, now: 1_749));
        True(beforeDeadline.DidResolve, "the same signal may resolve at 749ms");
        True(beforeDeadline.NextPending is null, "resolution is removed before lifecycle exposure");
        Equal(signal, beforeDeadline.ResolvedSignal!.Value, "resolution preserves the original packet and timestamp");

        var atDeadline = MiracleCleanseFollowupRules.ResolvePendingSignal(
            pending,
            ResolutionObservation(target, now: 1_750));
        Equal(
            MiracleCleanseFollowupResolutionRetireReason.AcquisitionExpired,
            atDeadline.RetireReason,
            "the original exact 750ms acquisition deadline is terminal");
        True(atDeadline.NextPending is null, "deadline retirement cannot retry");

        var wrongTarget = Target(10);
        var changed = MiracleCleanseFollowupRules.ResolvePendingSignal(
            pending,
            ResolutionObservation(wrongTarget, now: 1_001));
        Equal(
            MiracleCleanseFollowupResolutionRetireReason.CanonicalIdentityChanged,
            changed.RetireReason,
            "a different canonical actor is terminal rather than a fallback");

        foreach (var gated in new[]
                 {
                     ResolutionObservation(null, 1_001) with { ConfigurationEnabled = false },
                     ResolutionObservation(null, 1_001) with { IsCrystallineConflict = false },
                     ResolutionObservation(null, 1_001) with { IsLocalCounterJobValid = false },
                     ResolutionObservation(null, 1_001) with { LocalEntityId = 101 },
                     ResolutionObservation(null, 1_001) with { LocalCounterJobId = 23 },
                     ResolutionObservation(null, 1_001) with { FeatureGeneration = 8 },
                     ResolutionObservation(null, 1_001) with { HardReset = true },
                 })
        {
            var retired = MiracleCleanseFollowupRules.ResolvePendingSignal(pending, gated);
            Equal(
                MiracleCleanseFollowupResolutionDecisionKind.Retired,
                retired.Kind,
                "disable/context/death-job-generation/reset gates clear pending state");
            True(retired.NextPending is null, "closed gate retains no pending resolution");
        }

        Equal(5, MiracleCleanseFollowupRules.MaximumPendingResolutions,
            "pending storage is bounded to the five canonical enemy slots");
    }

    internal static void ExactLifecyclePromotesOnceAfterObservedRelease()
    {
        var target = Target(10);
        var signal = Signal(target, sequence: 100, now: 1_000);

        var signalObserved = MiracleCleanseFollowupRules.Observe(
            MiracleCleanseFollowupState.Initial,
            Observation(signal, Candidate(target), 1_000));
        Equal(
            MiracleCleanseFollowupDecisionKind.SignalObserved,
            signalObserved.Kind,
            "server cleanse signal arms only a presence check");

        var resilienceObserved = MiracleCleanseFollowupRules.Observe(
            signalObserved.NextState,
            Observation(null, Candidate(target, resilienceCount: 1), 1_050));
        Equal(
            MiracleCleanseFollowupDecisionKind.ResilienceObserved,
            resilienceObserved.Kind,
            "positive live Resilience presence is latched");

        var firstMissing = MiracleCleanseFollowupRules.Observe(
            resilienceObserved.NextState,
            Observation(null, Candidate(target), 3_800));
        False(firstMissing.ShouldPromote, "first absence sample cannot promote");

        var beforeGrace = MiracleCleanseFollowupRules.Observe(
            firstMissing.NextState,
            Observation(null, Candidate(target), 3_949));
        False(beforeGrace.ShouldPromote, "149ms missing is still grace");

        var ready = MiracleCleanseFollowupRules.Observe(
            beforeGrace.NextState,
            Observation(null, Candidate(target), 3_950));
        True(ready.ShouldPromote, "actual 150ms absence promotes once");
        True(ready.RetiresSignalBeforePromotion, "signal retires before runtime promotion");
        Equal(target, ready.PromotionIntent!.Value.Target, "exact actor retained");
        Equal(signal, ready.PromotionIntent.Value.Signal, "exact server intent retained");
        Equal(
            3_950L,
            ready.PromotionIntent.Value.ReleasedAtMilliseconds,
            "promotion retains the original stable-release edge");
        Equal(
            MiracleCleanseFollowupPhase.WaitingForSignal,
            ready.NextState.Phase,
            "state is terminal before existing dispatcher receives it");

        var duplicate = MiracleCleanseFollowupRules.Observe(
            ready.NextState,
            Observation(signal, Candidate(target), 3_951));
        False(duplicate.ShouldPromote, "same server signal never retries");
        Equal(1, duplicate.NextState.ObservedSignals.Length, "dedupe key retained");
    }

    internal static void MissingGraceRejectsFlickerAndAmbiguity()
    {
        var target = Target(20);
        var state = ArmWithResilience(target, now: 1_000);

        var missing = MiracleCleanseFollowupRules.Observe(
            state,
            Observation(null, Candidate(target), 1_100));
        var returned = MiracleCleanseFollowupRules.Observe(
            missing.NextState,
            Observation(null, Candidate(target, resilienceCount: 1), 1_200));
        False(returned.ShouldPromote, "presence returning inside grace is flicker");
        Equal(-1L, returned.NextState.ResilienceMissingSinceMilliseconds, "flicker resets absence");

        var missingAgain = MiracleCleanseFollowupRules.Observe(
            returned.NextState,
            Observation(null, Candidate(target), 1_300));
        var released = MiracleCleanseFollowupRules.Observe(
            missingAgain.NextState,
            Observation(null, Candidate(target), 1_450));
        True(released.ShouldPromote, "continuous absence promotes once");

        var ambiguousState = ArmWithResilience(target, now: 2_000);
        var ambiguous = MiracleCleanseFollowupRules.Observe(
            ambiguousState,
            Observation(null, Candidate(target, resilienceCount: 2), 2_100));
        Equal(
            MiracleCleanseFollowupCancelReason.ResilienceObservationAmbiguous,
            ambiguous.CancelReason,
            "duplicate 3248 rows are not presence or absence proof");
    }

    internal static void AcquisitionReleaseAndOpportunityWindowsAreBounded()
    {
        var target = Target(30);
        var signal = Signal(target, sequence: 300, now: 1_000);
        var armed = MiracleCleanseFollowupRules.Observe(
            MiracleCleanseFollowupState.Initial,
            Observation(signal, Candidate(target), 1_000));
        var insideAcquisition = MiracleCleanseFollowupRules.Observe(
            armed.NextState,
            Observation(null, Candidate(target), 1_749));
        False(insideAcquisition.ShouldPromote, "749ms remains inside acquisition");
        var acquisitionExpired = MiracleCleanseFollowupRules.Observe(
            insideAcquisition.NextState,
            Observation(null, Candidate(target), 1_750));
        Equal(
            MiracleCleanseFollowupCancelReason.ResilienceNotObserved,
            acquisitionExpired.CancelReason,
            "exact 750ms boundary expires");

        var waitingForEnd = ArmWithResilience(target, now: 2_000);
        var beforeReleaseCap = MiracleCleanseFollowupRules.Observe(
            waitingForEnd,
            Observation(null, Candidate(target, resilienceCount: 1), 4_999));
        False(beforeReleaseCap.ShouldPromote, "2999ms live presence remains bounded wait");
        var releaseTimedOut = MiracleCleanseFollowupRules.Observe(
            beforeReleaseCap.NextState,
            Observation(null, Candidate(target, resilienceCount: 1), 5_000));
        Equal(
            MiracleCleanseFollowupCancelReason.ResilienceReleaseTimedOut,
            releaseTimedOut.CancelReason,
            "exact 3000ms live-presence boundary expires");

        var lateAbsenceState = ArmWithResilience(target, now: 10_000);
        var lateFirstAbsence = MiracleCleanseFollowupRules.Observe(
            lateAbsenceState,
            Observation(null, Candidate(target), 12_999));
        False(lateFirstAbsence.ShouldPromote, "absence at 2999ms still needs stable grace");
        var graceCrossedHardDeadline = MiracleCleanseFollowupRules.Observe(
            lateFirstAbsence.NextState,
            Observation(null, Candidate(target), 13_149));
        Equal(
            MiracleCleanseFollowupCancelReason.ResilienceReleaseTimedOut,
            graceCrossedHardDeadline.CancelReason,
            "absence grace cannot cross the hard 3000ms release deadline");

        var releaseState = ReachReleaseOpportunity(target, now: 6_000);
        var beforeOpportunityEnd = MiracleCleanseFollowupRules.Observe(
            releaseState,
            Observation(null, Candidate(target), 9_249, higherPriority: true));
        False(beforeOpportunityEnd.ShouldPromote,
            "bound priority wait remains inside the original three-second held lease");
        var opportunityExpired = MiracleCleanseFollowupRules.Observe(
            beforeOpportunityEnd.NextState,
            Observation(null, Candidate(target), 9_250));
        Equal(
            MiracleCleanseFollowupCancelReason.ReleaseOpportunityExpired,
            opportunityExpired.CancelReason,
            "exact three-second bound release boundary cannot promote late");

        var unboundState = ArmWithResilience(target, now: 14_000);
        var unboundMissing = MiracleCleanseFollowupRules.Observe(
            unboundState,
            Observation(
                null,
                Candidate(target, reservationKey: 0, reservedKeyDown: false),
                14_100));
        var unboundRelease = MiracleCleanseFollowupRules.Observe(
            unboundMissing.NextState,
            Observation(
                null,
                Candidate(target, reservationKey: 0, reservedKeyDown: false),
                14_250,
                higherPriority: true));
        var unboundExpired = MiracleCleanseFollowupRules.Observe(
            unboundRelease.NextState,
            Observation(null, Candidate(target), 14_750));
        Equal(
            MiracleCleanseFollowupCancelReason.ReleaseOpportunityExpired,
            unboundExpired.CancelReason,
            "unbound key acquisition remains strict at the original 500 ms boundary");
    }

    internal static void HigherPriorityWaitsWithoutDestroyingOpportunity()
    {
        var target = Target(40);
        var releaseState = ReachReleaseOpportunity(target, now: 1_000);
        var priority = MiracleCleanseFollowupRules.Observe(
            releaseState,
            Observation(null, Candidate(target), 1_251, higherPriority: true));
        False(priority.ShouldPromote, "existing immediate MCH/SAM/VPR threat wins");
        Equal(
            MiracleCleanseFollowupPhase.ReleaseOpportunity,
            priority.NextState.Phase,
            "bounded opportunity survives transient priority");

        var free = MiracleCleanseFollowupRules.Observe(
            priority.NextState,
            Observation(
                null,
                Candidate(target, reservationKey: 66, reservedKeyDown: true),
                1_850));
        True(free.ShouldPromote,
            "dispatcher clearing after 500 ms receives the original bound promotion");
        Equal(
            1_250L,
            free.PromotionIntent!.Value.ReleasedAtMilliseconds,
            "priority wait cannot restart the three-second held lease");
        Equal(65, free.PromotionIntent.Value.GameplayKeyToken,
            "a later reported key cannot replace the exact frozen key");
    }

    internal static void TeamPressureHasNoMinimumAndUnknownRemainsEligible()
    {
        var target = Target(45);
        var releaseState = ReachReleaseOpportunity(target, now: 1_000);
        var unknown = MiracleCleanseFollowupRules.Observe(
            releaseState,
            Observation(
                null,
                Candidate(target),
                1_251,
                teamPressureKnown: false));
        True(unknown.ShouldPromote, "unknown pressure is an eligible lower-rank fallback");

        releaseState = ReachReleaseOpportunity(target, now: 2_000);
        var knownZero = MiracleCleanseFollowupRules.Observe(
            releaseState,
            Observation(
                null,
                Candidate(target),
                2_251,
                teamPressureKnown: true,
                teamTargetCount: 0));
        True(knownZero.ShouldPromote, "fresh known pressure zero has no minimum gate");
        Equal(
            2_250L,
            knownZero.PromotionIntent!.Value.ReleasedAtMilliseconds,
            "pressure does not restart the release lifetime");

        releaseState = ReachReleaseOpportunity(target, now: 2_500);
        var unreachable = MiracleCleanseFollowupRules.Observe(
            releaseState,
            Observation(
                null,
                Candidate(target, counterActionReachable: false),
                2_751));
        True(unreachable.ShouldPromote,
            "an exact out-of-range release freezes now and waits in the bounded dispatcher lease");
        Equal(MiracleCleanseFollowupPhase.WaitingForSignal, unreachable.NextState.Phase,
            "promotion retires the short release window before the dispatcher waits for native reachability");

        releaseState = ReachReleaseOpportunity(target, now: 3_000);
        var expired = MiracleCleanseFollowupRules.Observe(
            releaseState,
            Observation(null, Candidate(target), 6_250));
        Equal(
            MiracleCleanseFollowupCancelReason.ReleaseOpportunityExpired,
            expired.CancelReason,
            "the exact held-lease deadline is terminal without a pressure gate");
    }

    internal static void IdentityAmbiguityAndConcurrencyFailClosed()
    {
        var target = Target(50);
        var signal = Signal(target, sequence: 500, now: 1_000);
        var wrongTarget = Target(51);
        var changed = MiracleCleanseFollowupRules.Observe(
            MiracleCleanseFollowupState.Initial,
            Observation(signal, Candidate(wrongTarget), 1_000));
        Equal(
            MiracleCleanseFollowupCancelReason.CandidateChanged,
            changed.CancelReason,
            "signal target cannot drift");

        var invalidIdentity = MiracleCleanseFollowupRules.Observe(
            MiracleCleanseFollowupState.Initial,
            Observation(signal, Candidate(target) with
            {
                IsExactCanonicalEnemy = false,
            }, 1_000));
        Equal(
            MiracleCleanseFollowupCancelReason.CandidateIdentityInvalid,
            invalidIdentity.CancelReason,
            "noncanonical actor fails closed");

        var first = MiracleCleanseFollowupRules.Observe(
            MiracleCleanseFollowupState.Initial,
            Observation(signal, Candidate(target), 1_000));
        var secondTarget = Target(52);
        var secondSignal = Signal(secondTarget, sequence: 501, now: 1_010);
        var concurrent = MiracleCleanseFollowupRules.Observe(
            first.NextState,
            Observation(secondSignal, Candidate(secondTarget), 1_010));
        Equal(
            MiracleCleanseFollowupCancelReason.ConcurrentSignal,
            concurrent.CancelReason,
            "concurrent cleanse cannot replace first target");
        Equal(2, concurrent.NextState.ObservedSignals.Length, "both signals remain deduped");
        Equal(
            signal,
            concurrent.NextState.ActiveSignal!.Value,
            "first exact lifecycle remains authoritative");
    }

    internal static void IndependentEnemySlotsKeepDistinctPurifyEpisodes()
    {
        var firstTarget = Target(60);
        var secondTarget = Target(61);
        var first = ArmWithResilience(firstTarget, 1_000);
        var second = ArmWithResilience(secondTarget, 1_000);

        first = MiracleCleanseFollowupRules.Observe(
            first,
            Observation(null, Candidate(firstTarget), 1_100)).NextState;
        second = MiracleCleanseFollowupRules.Observe(
            second,
            Observation(null, Candidate(secondTarget), 1_100)).NextState;
        var firstReady = MiracleCleanseFollowupRules.Observe(
            first,
            Observation(null, Candidate(firstTarget), 1_250, higherPriority: true));
        var secondReady = MiracleCleanseFollowupRules.Observe(
            second,
            Observation(null, Candidate(secondTarget), 1_250, higherPriority: true));

        Equal(
            MiracleCleanseFollowupPhase.ReleaseOpportunity,
            firstReady.NextState.Phase,
            "first exact slot keeps its own release episode");
        Equal(
            MiracleCleanseFollowupPhase.ReleaseOpportunity,
            secondReady.NextState.Phase,
            "second exact slot keeps its own release episode");
        Equal(
            firstTarget,
            firstReady.NextState.ActiveSignal!.Value.Target,
            "first identity remains frozen");
        Equal(
            secondTarget,
            secondReady.NextState.ActiveSignal!.Value.Target,
            "second identity remains frozen");

        var firstPromotion = MiracleCleanseFollowupRules.Observe(
            firstReady.NextState,
            Observation(null, Candidate(firstTarget), 1_251));
        var secondPromotion = MiracleCleanseFollowupRules.Observe(
            secondReady.NextState,
            Observation(null, Candidate(secondTarget), 1_251));
        True(firstPromotion.ShouldPromote, "first distinct slot can yield one candidate");
        True(secondPromotion.ShouldPromote, "second distinct slot can yield one candidate");
    }

    internal static void ExpectedEndUsesFirstAuthoritativeAbsentFrame()
    {
        var target = Target(70);
        var signal = Signal(target, sequence: 700, now: 1_000);
        var observed = MiracleCleanseFollowupRules.Observe(
            MiracleCleanseFollowupState.Initial,
            Observation(
                signal,
                Candidate(
                    target,
                    resilienceCount: 1,
                    remainingMilliseconds: 2_000,
                    reservationKey: 65,
                    reservedKeyDown: true),
                1_000));
        Equal(3_000L, observed.NextState.ExpectedProtectionEndAtMilliseconds, "validated expected end is absolute");
        Equal(0, observed.NextState.GameplayKeyToken, "enemy episode stores no early key during immunity");

        var stillPresent = MiracleCleanseFollowupRules.Observe(
            observed.NextState,
            Observation(
                null,
                Candidate(
                    target,
                    resilienceCount: 1,
                    remainingMilliseconds: 1,
                    reservationKey: 66,
                    reservedKeyDown: true),
                3_000));
        False(stillPresent.ShouldPromote, "expected time alone never authorizes through live Resilience");
        Equal(0, stillPresent.NextState.GameplayKeyToken, "movement-key changes remain irrelevant during immunity");

        var firstAbsent = MiracleCleanseFollowupRules.Observe(
            stillPresent.NextState,
            Observation(
                null,
                Candidate(target, reservationKey: 66, reservedKeyDown: true),
                3_001));
        True(firstAbsent.ShouldPromote, "first live absence after expected end promotes immediately");
        Equal(66, firstAbsent.PromotionIntent!.Value.GameplayKeyToken, "first executable frame freezes the current exact key");
        Equal(3_000L, firstAbsent.PromotionIntent.Value.ExpectedProtectionEndAtMilliseconds, "promotion retains non-extending hint");
        Equal(3_001L, firstAbsent.PromotionIntent.Value.ReleasedAtMilliseconds, "actual absence remains release authority");
    }

    internal static void InvalidExpectedEndKeepsAbsenceGrace()
    {
        var target = Target(71);
        var signal = Signal(target, sequence: 701, now: 1_000);
        var observed = MiracleCleanseFollowupRules.Observe(
            MiracleCleanseFollowupState.Initial,
            Observation(
                signal,
                Candidate(
                    target,
                    resilienceCount: 1,
                    remainingMilliseconds:
                        MiracleCleanseFollowupRules.MaximumResilienceRemainingMilliseconds + 1),
                1_000));
        True(observed.NextState.ExpectedProtectionEndAtMilliseconds <= 0, "implausible duration is not trusted");

        var missing = MiracleCleanseFollowupRules.Observe(
            observed.NextState,
            Observation(null, Candidate(target), 2_000));
        False(missing.ShouldPromote, "untimed first absence retains anti-flicker grace");
        var ready = MiracleCleanseFollowupRules.Observe(
            missing.NextState,
            Observation(null, Candidate(target), 2_150));
        True(ready.ShouldPromote, "untimed continuous absence still promotes after grace");
    }

    internal static void ReservationBindsAtReleaseAndThenRequiresExactKey()
    {
        var target = Target(72);
        var signal = Signal(target, sequence: 702, now: 1_000);
        var observed = MiracleCleanseFollowupRules.Observe(
            MiracleCleanseFollowupState.Initial,
            Observation(
                signal,
                Candidate(
                    target,
                    resilienceCount: 1,
                    remainingMilliseconds: 100,
                    reservationKey: 65,
                    reservedKeyDown: true),
                1_000));
        var changedDuringResilience = MiracleCleanseFollowupRules.Observe(
            observed.NextState,
            Observation(
                null,
                Candidate(
                    target,
                    resilienceCount: 1,
                    reservationKey: 66,
                    reservedKeyDown: false),
                1_001));
        Equal(MiracleCleanseFollowupCancelReason.None, changedDuringResilience.CancelReason,
            "releasing or changing a movement key during Resilience does not destroy the enemy episode");
        Equal(0, changedDuringResilience.NextState.GameplayKeyToken,
            "no input is owned before authoritative Resilience absence");

        var absentWithoutKey = MiracleCleanseFollowupRules.Observe(
            changedDuringResilience.NextState,
            Observation(
                null,
                Candidate(target, reservationKey: 0, reservedKeyDown: false),
                1_100));
        False(absentWithoutKey.ShouldPromote, "release is remembered even when no key exists on its first frame");
        Equal(MiracleCleanseFollowupPhase.ReleaseOpportunity, absentWithoutKey.NextState.Phase,
            "the bounded 500 ms release opportunity stays available");
        Equal(0, absentWithoutKey.NextState.GameplayKeyToken, "no key is invented");

        var acquiredInsideWindow = MiracleCleanseFollowupRules.Observe(
            absentWithoutKey.NextState,
            Observation(
                null,
                Candidate(target, reservationKey: 67, reservedKeyDown: true),
                1_101));
        True(acquiredInsideWindow.ShouldPromote, "a current held key can be acquired inside the original release window");
        Equal(67, acquiredInsideWindow.PromotionIntent!.Value.GameplayKeyToken,
            "promotion owns exactly the key observed at release");

        var strictTarget = Target(73);
        var strictSignal = Signal(strictTarget, sequence: 703, now: 2_000);
        var strictPresent = MiracleCleanseFollowupRules.Observe(
            MiracleCleanseFollowupState.Initial,
            Observation(
                strictSignal,
                Candidate(
                    strictTarget,
                    resilienceCount: 1,
                    remainingMilliseconds: 100,
                    reservationKey: 66,
                    reservedKeyDown: true),
                2_000));
        var frozenBehindPriority = MiracleCleanseFollowupRules.Observe(
            strictPresent.NextState,
            Observation(
                null,
                Candidate(strictTarget, reservationKey: 66, reservedKeyDown: true),
                2_100,
                higherPriority: true));
        Equal(
            66,
            frozenBehindPriority.NextState.GameplayKeyToken,
            "once release begins, the exact key is frozen while priority work runs");

        var releasedFrozenKey = MiracleCleanseFollowupRules.Observe(
            frozenBehindPriority.NextState,
            Observation(
                null,
                Candidate(strictTarget, reservationKey: 67, reservedKeyDown: false),
                2_101));
        Equal(MiracleCleanseFollowupCancelReason.ReservationKeyReleased,
            releasedFrozenKey.CancelReason,
            "after release binding, letting go terminally cancels the exact intent");
    }

    internal static void PromotionKindLabelsConfirmationWithoutBroadeningStartRules()
    {
        Equal(
            0L,
            MiracleInterceptRules.GetThreatLifetimeMilliseconds(
                MiracleInterceptThreatKind.PostPurifyCrowdControl),
            "follow-up is not a native start-signal lifetime");
        False(
            MiracleInterceptRules.IsExpectedJob(
                MiracleInterceptThreatKind.PostPurifyCrowdControl,
                24),
            "follow-up is not classified from a job-specific hostile start");

        var pending = new MiracleInterceptPendingAttempt(
            LocalCasterEntityId: 100,
            ActionId: MiracleInterceptConfirmationRules.MiracleOfNatureActionId,
            TargetGameObjectId: 10_200,
            TargetEntityId: 200,
            Threat: MiracleInterceptThreatKind.PostPurifyCrowdControl,
            UseActionAccepted: true,
            AttemptedAtMilliseconds: 1_000,
            ExpectedSourceSequence: 9)
        {
            RemovedStatusId = MiracleCleanseFollowupRules.StunStatusId,
        };
        True(pending.IsValid, "shared landing confirmation accepts follow-up label");
    }

    private static MiracleCleanseFollowupState ArmWithResilience(
        MiracleCleanseFollowupTargetIdentity target,
        long now)
    {
        var signal = Signal(target, (uint)now, now);
        var decision = MiracleCleanseFollowupRules.Observe(
            MiracleCleanseFollowupState.Initial,
            Observation(signal, Candidate(target, resilienceCount: 1), now));
        Equal(
            MiracleCleanseFollowupDecisionKind.ResilienceObserved,
            decision.Kind,
            "test setup latched Resilience presence");
        return decision.NextState;
    }

    private static MiracleCleanseFollowupState ReachReleaseOpportunity(
        MiracleCleanseFollowupTargetIdentity target,
        long now)
    {
        var state = ArmWithResilience(target, now);
        var missing = MiracleCleanseFollowupRules.Observe(
            state,
            Observation(null, Candidate(target), now + 100));
        var releasedButBusy = MiracleCleanseFollowupRules.Observe(
            missing.NextState,
            Observation(null, Candidate(target), now + 250, higherPriority: true));
        False(releasedButBusy.ShouldPromote, "test setup leaves promotion pending");
        Equal(
            MiracleCleanseFollowupPhase.ReleaseOpportunity,
            releasedButBusy.NextState.Phase,
            "test setup reached release opportunity");
        return releasedButBusy.NextState;
    }

    private static MiracleCleanseFollowupTargetIdentity Target(uint entityId) =>
        new(entityId + 10_000UL, entityId, 24);

    private static MiracleCleanseFollowupSignal Signal(
        MiracleCleanseFollowupTargetIdentity target,
        uint sequence,
        long now) =>
        new(
            new MiracleCleanseFollowupSignalKey(
                target.EntityId,
                MiracleCleanseFollowupRules.PurifyActionId,
                target.EntityId,
                MiracleCleanseFollowupRules.RecoveredFromStatusEffectType,
                (ushort)MiracleCleanseFollowupRules.StunStatusId,
                sequence,
                1),
            target,
            now);

    private static MiracleCleanseFollowupCandidate Candidate(
        MiracleCleanseFollowupTargetIdentity target,
        int resilienceCount = 0,
        long remainingMilliseconds = 0,
        int reservationKey = 65,
        bool reservedKeyDown = true,
        bool counterActionReachable = true) =>
        new(
            target,
            IsExactCanonicalEnemy: true,
            IsAliveAndTargetable: true,
            ActiveResilienceStatusCount: resilienceCount)
        {
            ResilienceRemainingMilliseconds = remainingMilliseconds,
            ReservationGameplayKeyToken = reservationKey,
            ReservedGameplayKeyPhysicallyDown = reservedKeyDown,
            CounterActionReachable = counterActionReachable,
        };

    private static MiracleCleanseFollowupObservation Observation(
        MiracleCleanseFollowupSignal? signal,
        MiracleCleanseFollowupCandidate candidate,
        long now,
        bool higherPriority = false,
        bool teamPressureKnown = true,
        int teamTargetCount = 0) =>
        new(
            ConfigurationEnabled: true,
            IsCrystallineConflict: true,
            IsLocalCounterJobValid: true,
            HigherPriorityClaimed: higherPriority,
            NewSignal: signal,
            Candidate: candidate,
            TeamTargetCountKnown: teamPressureKnown,
            TeamTargetCount: teamTargetCount,
            NowMilliseconds: now);

    private static MiracleCleanseFollowupResolutionObservation ResolutionObservation(
        MiracleCleanseFollowupTargetIdentity? target,
        long now) =>
        new(
            ConfigurationEnabled: true,
            IsCrystallineConflict: true,
            IsLocalCounterJobValid: true,
            LocalEntityId: 100,
            LocalCounterJobId: 24,
            FeatureGeneration: 7,
            UniqueCanonicalTarget: target,
            NowMilliseconds: now);

    private static bool IsExact(
        uint caster = 10,
        uint action = MiracleCleanseFollowupRules.PurifyActionId,
        uint target = 10,
        byte effectType = MiracleCleanseFollowupRules.RecoveredFromStatusEffectType,
        ushort effectValue = (ushort)MiracleCleanseFollowupRules.StunStatusId,
        uint globalSequence = 100,
        ushort sourceSequence = 1) =>
        MiracleCleanseFollowupRules.IsExactPurifySignal(
            caster,
            action,
            target,
            effectType,
            effectValue,
            globalSequence,
            sourceSequence);

    private static void True(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static void False(bool value, string message) => True(!value, message);

    private static void Equal<T>(T expected, T actual, string message)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
    }
}
