using SeitonSense.Core;

internal static class MiracleCleanseFollowupSelfTests
{
    internal static void ExactPurifySignalAcceptsOnlyKnownRemovableCrowdControl()
    {
        foreach (var statusId in new ushort[] { 1_343, 1_344, 1_345, 1_347, 3_085, 3_219 })
            True(IsExact(effectValue: statusId), $"exact self-Purify recovery {statusId}");

        False(IsExact(action: 29_057), "wrong action");
        False(IsExact(target: 11), "not self-targeted");
        False(IsExact(effectType: 0x0E), "status add is not recovery");
        False(IsExact(effectValue: 9_999), "unknown recovery status excluded");
        False(IsExact(caster: 0, target: 0), "invalid actor IDs");
        False(IsExact(globalSequence: 0, sourceSequence: 0), "missing packet identity");
    }

    internal static void ValidatedSignalRetiresBeforeCanonicalResolution()
    {
        var target = Target(9);
        var key = Signal(target, sequence: 99, now: 1_000).Key;
        var first = MiracleCleanseFollowupRules.RetireValidatedSignal(
            MiracleCleanseFollowupSignalLedger.Initial,
            key);
        True(first.IsNewValidatedSignal, "first validated packet is terminally remembered");

        // Simulate the runtime's exact canonical lookup failing: no lifecycle
        // state is armed, but the packet retirement must already be durable.
        var lifecycle = MiracleCleanseFollowupState.Initial;
        var duplicate = MiracleCleanseFollowupRules.RetireValidatedSignal(
            first.NextState,
            key);
        False(duplicate.IsNewValidatedSignal, "duplicate cannot retry canonical resolution");
        True(lifecycle.ActiveSignal is null, "unresolved first observation armed no lifecycle");
        Equal(1, duplicate.NextState.RetiredSignals.Length, "duplicate adds no second retirement");
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
            Observation(null, Candidate(target), 6_749, higherPriority: true));
        False(beforeOpportunityEnd.ShouldPromote, "priority wait remains inside 500ms opportunity");
        var opportunityExpired = MiracleCleanseFollowupRules.Observe(
            beforeOpportunityEnd.NextState,
            Observation(null, Candidate(target), 6_750));
        Equal(
            MiracleCleanseFollowupCancelReason.ReleaseOpportunityExpired,
            opportunityExpired.CancelReason,
            "exact 500ms release boundary cannot promote late");
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
            Observation(null, Candidate(target), 1_252));
        True(free.ShouldPromote, "later free dispatcher slot receives promotion");
        Equal(
            1_250L,
            free.PromotionIntent!.Value.ReleasedAtMilliseconds,
            "priority wait cannot restart the 500ms release opportunity");
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

        releaseState = ReachReleaseOpportunity(target, now: 3_000);
        var expired = MiracleCleanseFollowupRules.Observe(
            releaseState,
            Observation(null, Candidate(target), 3_750));
        Equal(
            MiracleCleanseFollowupCancelReason.ReleaseOpportunityExpired,
            expired.CancelReason,
            "the exact deadline is still terminal without a pressure gate");
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
        Equal(65, observed.NextState.GameplayKeyToken, "held key is frozen during immunity");

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
        Equal(65, stillPresent.NextState.GameplayKeyToken, "later key cannot replace frozen consent");

        var firstAbsent = MiracleCleanseFollowupRules.Observe(
            stillPresent.NextState,
            Observation(
                null,
                Candidate(target, reservationKey: 66, reservedKeyDown: true),
                3_001));
        True(firstAbsent.ShouldPromote, "first live absence after expected end promotes immediately");
        Equal(65, firstAbsent.PromotionIntent!.Value.GameplayKeyToken, "promotion carries exact reserved key");
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

    internal static void ReservedKeyReleaseTerminallyCancelsEpisode()
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
                    reservationKey: 65,
                    reservedKeyDown: true),
                1_000));
        var released = MiracleCleanseFollowupRules.Observe(
            observed.NextState,
            Observation(
                null,
                Candidate(
                    target,
                    resilienceCount: 1,
                    reservationKey: 66,
                    reservedKeyDown: false),
                1_001));
        Equal(
            MiracleCleanseFollowupCancelReason.ReservationKeyReleased,
            released.CancelReason,
            "one observed release terminally cancels the reservation");
        Equal(0, released.NextState.GameplayKeyToken, "alternate key cannot inherit cancelled episode");

        var absent = MiracleCleanseFollowupRules.Observe(
            released.NextState,
            Observation(null, Candidate(target, reservationKey: 66, reservedKeyDown: true), 1_200));
        False(absent.ShouldPromote, "re-press or alternate key cannot resurrect retired signal");

        var noKeyTarget = Target(73);
        var noKeySignal = Signal(noKeyTarget, sequence: 703, now: 2_000);
        var noKeyAtSignal = MiracleCleanseFollowupRules.Observe(
            MiracleCleanseFollowupState.Initial,
            Observation(
                noKeySignal,
                Candidate(noKeyTarget, resilienceCount: 1),
                2_000));
        var keyPressedLater = MiracleCleanseFollowupRules.Observe(
            noKeyAtSignal.NextState,
            Observation(
                null,
                Candidate(
                    noKeyTarget,
                    resilienceCount: 1,
                    reservationKey: 66,
                    reservedKeyDown: true),
                2_001));
        Equal(
            0,
            keyPressedLater.NextState.GameplayKeyToken,
            "a later key cannot retroactively reserve the Purify episode");
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
            AttemptedAtMilliseconds: 1_000)
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
        int reservationKey = 0,
        bool reservedKeyDown = false) =>
        new(
            target,
            IsExactCanonicalEnemy: true,
            IsAliveAndTargetable: true,
            ActiveResilienceStatusCount: resilienceCount)
        {
            ResilienceRemainingMilliseconds = remainingMilliseconds,
            ReservationGameplayKeyToken = reservationKey,
            ReservedGameplayKeyPhysicallyDown = reservedKeyDown,
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
