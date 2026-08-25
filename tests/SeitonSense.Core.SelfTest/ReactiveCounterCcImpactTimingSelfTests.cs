using SeitonSense.Core;

internal static class ReactiveCounterCcImpactTimingSelfTests
{
    private const uint ActionId = MiracleInterceptConfirmationRules.InterveneActionId;
    private const uint TargetEntityId = 0x1234;
    private const ulong TargetGameObjectId = 0x1_0000_1234;

    internal static void ActionEffectSamplesRequireExactIdentityAndBounds()
    {
        True(
            ReactiveCounterCcImpactTimingRules.TryMeasureSample(
                ActionId,
                TargetEntityId,
                77,
                1_000,
                ActionId,
                TargetEntityId,
                77,
                1_420,
                out var sample),
            "exact ActionEffect sample");
        Equal(420, sample, "request-to-effect duration");

        False(TrySample(observedActionId: 123), "wrong action");
        False(TrySample(observedTargetEntityId: TargetEntityId + 1), "wrong target");
        False(TrySample(observedSourceSequence: 78), "wrong source sequence");
        False(TrySample(observedAtMilliseconds: 1_049), "sample below lower bound");
        False(TrySample(observedAtMilliseconds: 2_501), "sample above upper bound");

        False(
            ReactiveCounterCcImpactTimingRules.TryMeasureSample(
                ActionId,
                TargetEntityId,
                expectedSourceSequence: 0,
                attemptedAtMilliseconds: 1_000,
                ActionId,
                TargetEntityId,
                observedSourceSequence: 78,
                observedAtMilliseconds: 1_300,
                out _),
            "missing client sequence can never teach timing");
    }

    internal static void CalibrationIsBucketedBoundedAndConservative()
    {
        True(
            ReactiveCounterCcImpactTimingRules.TryCreateCalibrationSample(
                420,
                4.321f,
                out var exactDistanceSample),
            "valid delay and distance create a sample");
        Equal(433, exactDistanceSample.EdgeDistanceCentiyalms, "sample distance rounds up");
        False(
            ReactiveCounterCcImpactTimingRules.TryCreateCalibrationSample(
                420,
                float.NaN,
                out _),
            "unknown distance fails closed");
        False(
            ReactiveCounterCcImpactTimingRules.TryCreateCalibrationSample(
                49,
                4f,
                out _),
            "invalid delay fails closed");

        IReadOnlyList<ReactiveCounterCcImpactSample> samples = [];
        for (var index = 0; index < 30; index++)
        {
            samples = ReactiveCounterCcImpactTimingRules.AppendBoundedSample(
                samples,
                Sample(200 + index * 10, 2f));
        }
        Equal(
            ReactiveCounterCcImpactTimingRules.MaximumSamplesPerAction,
            samples.Count,
            "sample history is bounded");
        Equal(260, samples[0].DelayMilliseconds, "oldest overflow sample removed");
        Equal(490, samples[^1].DelayMilliseconds, "latest valid sample retained");

        True(
            ReactiveCounterCcImpactTimingRules.TryGetSafeLeadMilliseconds(
                [
                    Sample(400, 3f),
                    Sample(420, 3.2f),
                    Sample(440, 3.4f),
                    Sample(460, 3.6f),
                    Sample(1_400, 3.8f),
                ],
                4f,
                out var safeLead),
            "calibration produces a safe lead");
        Equal(225, safeLead, "fastest observed effect minus landing margin");
        False(
            ReactiveCounterCcImpactTimingRules.TryGetSafeLeadMilliseconds(
                [Sample(600, 3f)],
                4f,
                out _),
            "one slow first sample cannot predict");
        True(
            ReactiveCounterCcImpactTimingRules.TryGetSafeLeadMilliseconds(
                [
                    Sample(600, 3f),
                    Sample(650, 3f),
                    Sample(250, 3f),
                    Sample(270, 3f),
                    Sample(290, 3f),
                ],
                4f,
                out var correctedSparseLead),
            "five exact samples expose the fastest observed delay");
        Equal(75, correctedSparseLead, "slow-first sample cannot pull dispatch early");
        False(
            ReactiveCounterCcImpactTimingRules.TryGetSafeLeadMilliseconds(
                [
                    Sample(600, 3f),
                    Sample(250, 3f),
                    Sample(270, 3f),
                    Sample(290, 3f),
                ],
                4f,
                out _),
            "four samples remain too sparse for prediction");
        False(
            ReactiveCounterCcImpactTimingRules.TryGetSafeLeadMilliseconds(
                [
                    Sample(600, 14f),
                    Sample(620, 14f),
                    Sample(640, 14f),
                    Sample(660, 14f),
                    Sample(680, 14f),
                ],
                4f,
                out _),
            "far samples can never teach a nearer target");
        True(
            ReactiveCounterCcImpactTimingRules.TryGetSafeLeadMilliseconds(
                [
                    Sample(400, 3f),
                    Sample(420, 3.1f),
                    Sample(440, 3.2f),
                    Sample(460, 3.3f),
                    Sample(480, 3.4f),
                ],
                14f,
                out _),
            "equal-or-nearer samples may conservatively inform a farther target");
        False(
            ReactiveCounterCcImpactTimingRules.TryGetSafeLeadMilliseconds(
                [],
                4f,
                out _),
            "missing calibration fails closed");

        var fourPersisted = new[]
        {
            Sample(400, 3f),
            Sample(420, 3.1f),
            Sample(440, 3.2f),
            Sample(460, 3.3f),
        };
        False(
            ReactiveCounterCcImpactTimingRules.TryGetSafeLeadMilliseconds(
                fourPersisted.Append(Sample(480, 3.4f)).ToArray(),
                currentSessionSamples: [],
                currentEdgeDistanceYalms: 4f,
                out _),
            "persisted-only history cannot arm a new runtime session");
        var freshEligible = Sample(480, 3.4f);
        True(
            ReactiveCounterCcImpactTimingRules.TryGetSafeLeadMilliseconds(
                fourPersisted.Append(freshEligible).ToArray(),
                [freshEligible],
                4f,
                out _),
            "one eligible fresh sample plus four persisted samples can arm");
        var freshTooFar = Sample(600, 14f);
        False(
            ReactiveCounterCcImpactTimingRules.TryGetSafeLeadMilliseconds(
                fourPersisted.Append(freshTooFar).ToArray(),
                [freshTooFar],
                4f,
                out _),
            "far current-session sample cannot unlock a nearer prediction");
    }

    internal static void PredictionAndOneCallBrakeBypassAreExact()
    {
        True(
            ReactiveCounterCcImpactTimingRules.ShouldPreDispatch(1, 300, 315),
            "one exact protection may pre-dispatch inside learned lead");
        False(
            ReactiveCounterCcImpactTimingRules.ShouldPreDispatch(2, 300, 315),
            "ambiguous protection rows fail closed");
        False(
            ReactiveCounterCcImpactTimingRules.ShouldPreDispatch(1, 316, 315),
            "outside learned lead waits");
        True(
            ReactiveCounterCcImpactTimingRules.IsScheduledProtectionStillValid(
                10_300,
                10_000,
                300,
                315),
            "stable scheduled status end");
        False(
            ReactiveCounterCcImpactTimingRules.IsScheduledProtectionStillValid(
                10_600,
                10_000,
                300,
                315),
            "status-end drift fails closed");

        var intent = new PredictiveCcBrakeBypassIntent(
            ActionId,
            TargetGameObjectId,
            TargetEntityId,
            21,
            MiracleCleanseFollowupRules.ResilienceStatusId,
            ScheduledProtectionEndAtMilliseconds: 10_300,
            SafeImpactLeadMilliseconds: 315);
        True(PredictiveCcBrakeBypassRules.IsValidIntent(intent), "cataloged exact intent");
        False(
            PredictiveCcBrakeBypassRules.IsValidIntent(intent with { ActionId = 123 }),
            "unknown action cannot arm bypass");
        False(
            PredictiveCcBrakeBypassRules.IsValidIntent(intent with { TargetGameObjectId = 0 }),
            "invalid target cannot arm bypass");
        False(
            PredictiveCcBrakeBypassRules.IsValidIntent(intent with { ProtectionStatusId = 123 }),
            "unknown protection cannot arm bypass");

        var naturalIntent = NaturalIntent(ActionId);
        True(
            PredictiveCcBrakeBypassRules.IsValidIntent(naturalIntent),
            "exact natural helper intent");
        False(
            PredictiveCcBrakeBypassRules.IsValidIntent(
                naturalIntent with { ActionId = SamuraiReactiveCounterCcRules.SotenActionId }),
            "non-profile Soten cannot arm a natural helper token");
        False(
            PredictiveCcBrakeBypassRules.IsValidIntent(
                naturalIntent with { ScheduledProtectionEndAtMilliseconds = 0 }),
            "natural helper sentinel must remain exact");

        True(CanConsume(intent), "one exact native call consumes bypass");
        False(CanConsume(intent, alreadyConsumed: true), "already consumed token");
        False(CanConsume(intent, requestedActionId: 123), "raw action mismatch");
        False(CanConsume(intent, resolvedActionId: 123), "resolved action mismatch");
        False(CanConsume(intent, originalTargetId: 99), "original target mismatch");
        False(CanConsume(intent, forwardedTargetId: 99), "forwarded target mismatch");
        False(CanConsume(intent, targetSuppressedByRedirect: true), "suppressed target");
        False(CanConsume(intent, isActionInvocation: false), "wrong invocation type");
        False(CanConsume(intent, isNormalMode: false), "wrong invocation mode");

        var exactTarget = new TargetPressureActorIdentity(
            TargetGameObjectId,
            TargetEntityId);
        var permittedOnly = CcImmunityBrakeRules.Evaluate(
            masterEnabled: true,
            jobEnabled: true,
            actionEnabled: true,
            localJobId: ReactiveCounterCcProfileRules.PaladinJobId,
            actionId: ActionId,
            incomingTargetId: TargetGameObjectId,
            resolvedTarget: exactTarget,
            targetJobId: 21,
            targetIdentityResolvedExactly: true,
            activeStatusIds: [MiracleCleanseFollowupRules.ResilienceStatusId],
            permittedPredictiveBlockerStatusId:
                MiracleCleanseFollowupRules.ResilienceStatusId);
        False(permittedOnly.ShouldBlock, "one exact predicted blocker is exempted");

        var secondProtection = CcImmunityBrakeRules.Evaluate(
            masterEnabled: true,
            jobEnabled: true,
            actionEnabled: true,
            localJobId: ReactiveCounterCcProfileRules.PaladinJobId,
            actionId: ActionId,
            incomingTargetId: TargetGameObjectId,
            resolvedTarget: exactTarget,
            targetJobId: 21,
            targetIdentityResolvedExactly: true,
            activeStatusIds:
            [
                MiracleCleanseFollowupRules.ResilienceStatusId,
                SmartWardensPaeanTargetRules.WardensPaeanWardStatusId,
            ],
            permittedPredictiveBlockerStatusId:
                MiracleCleanseFollowupRules.ResilienceStatusId);
        True(secondProtection.ShouldBlock, "a second live protection still blocks");
        Equal(
            SmartWardensPaeanTargetRules.WardensPaeanWardStatusId,
            secondProtection.BlockerStatusId,
            "the non-exempt blocker is reported");

        var duplicateScheduledProtection = CcImmunityBrakeRules.Evaluate(
            masterEnabled: true,
            jobEnabled: true,
            actionEnabled: true,
            localJobId: ReactiveCounterCcProfileRules.PaladinJobId,
            actionId: ActionId,
            incomingTargetId: TargetGameObjectId,
            resolvedTarget: exactTarget,
            targetJobId: 21,
            targetIdentityResolvedExactly: true,
            activeStatusIds:
            [
                MiracleCleanseFollowupRules.ResilienceStatusId,
                MiracleCleanseFollowupRules.ResilienceStatusId,
            ],
            permittedPredictiveBlockerStatusId:
                MiracleCleanseFollowupRules.ResilienceStatusId);
        True(
            duplicateScheduledProtection.ShouldBlock,
            "duplicate scheduled protection rows fail closed");
    }

    internal static void WhiteMageGuardPredictionCountsScheduledGuardOutsideMiracleFamily()
    {
        var guardOnly = PredictiveCcBrakeBypassRules.ClassifyProtectionSet(
            CcImmunityBrakeBlockerFamily.Miracle,
            targetJobId: 21,
            MiracleGuardFollowupRules.GuardStatusId,
            [new PredictiveCcProtectionStatusObservation(
                MiracleGuardFollowupRules.GuardStatusId,
                275)]);
        Equal(1, guardOnly.ScheduledStatusCount, "exact scheduled Guard counted");
        Equal(275L, guardOnly.ScheduledRemainingMilliseconds, "Guard remaining time retained");
        False(guardOnly.OtherBlockerPresent, "Guard is not misclassified as another Miracle blocker");

        var guardedAndPaean = PredictiveCcBrakeBypassRules.ClassifyProtectionSet(
            CcImmunityBrakeBlockerFamily.Miracle,
            targetJobId: 21,
            MiracleGuardFollowupRules.GuardStatusId,
            [
                new PredictiveCcProtectionStatusObservation(
                    MiracleGuardFollowupRules.GuardStatusId,
                    275),
                new PredictiveCcProtectionStatusObservation(
                    SmartWardensPaeanTargetRules.WardensPaeanWardStatusId,
                    2_000),
            ]);
        Equal(1, guardedAndPaean.ScheduledStatusCount, "scheduled Guard remains exact");
        True(guardedAndPaean.OtherBlockerPresent, "other Miracle blocker remains live");

        var bothGuardRows = PredictiveCcBrakeBypassRules.ClassifyProtectionSet(
            CcImmunityBrakeBlockerFamily.Miracle,
            targetJobId: 21,
            MiracleGuardFollowupRules.GuardStatusId,
            [
                new PredictiveCcProtectionStatusObservation(
                    MiracleGuardFollowupRules.GuardStatusId,
                    275),
                new PredictiveCcProtectionStatusObservation(
                    MiracleGuardFollowupRules.GuardStatusAlternateId,
                    250),
            ]);
        Equal(1, bothGuardRows.ScheduledStatusCount, "scheduled Guard row stays exact");
        True(bothGuardRows.OtherBlockerPresent, "alternate Guard row blocks independently of Miracle family");

        var duplicateGuard = PredictiveCcBrakeBypassRules.ClassifyProtectionSet(
            CcImmunityBrakeBlockerFamily.Miracle,
            targetJobId: 21,
            MiracleGuardFollowupRules.GuardStatusId,
            [
                new PredictiveCcProtectionStatusObservation(
                    MiracleGuardFollowupRules.GuardStatusId,
                    275),
                new PredictiveCcProtectionStatusObservation(
                    MiracleGuardFollowupRules.GuardStatusId,
                    250),
            ]);
        Equal(2, duplicateGuard.ScheduledStatusCount, "duplicate Guard rows stay ambiguous");
    }

    internal static void AreaCounterHookRecheckIsHelperOnlyAndExact()
    {
        var exactTarget = new TargetPressureActorIdentity(
            TargetGameObjectId,
            TargetEntityId);
        foreach (var actionId in new[]
                 {
                     MiracleInterceptConfirmationRules.ResolutionActionId,
                     MiracleInterceptConfirmationRules.ViceOfThornsActionId,
                     MiracleInterceptConfirmationRules.FrostStarActionId,
                 })
        {
            var profile = ReactiveCounterCcProfileRules.Get(actionId)!.Value;
            var intent = PredictiveIntent(actionId);
            var scheduledOnly = CcImmunityBrakeRules.EvaluatePredictiveHelperExactRecheck(
                profile.JobId,
                actionId,
                TargetGameObjectId,
                exactTarget,
                targetJobId: 21,
                targetIdentityResolvedExactly: true,
                [Protection(MiracleCleanseFollowupRules.ResilienceStatusId, 300)],
                intent,
                nowMilliseconds: 10_000);
            False(scheduledOnly.ShouldBlock, $"{actionId} permits one scheduled row");

            var naturalIntent = NaturalIntent(actionId);
            var naturalClear = CcImmunityBrakeRules.EvaluatePredictiveHelperExactRecheck(
                profile.JobId,
                actionId,
                TargetGameObjectId,
                exactTarget,
                targetJobId: 21,
                targetIdentityResolvedExactly: true,
                activeStatuses: [],
                naturalIntent,
                nowMilliseconds: 10_000);
            False(naturalClear.ShouldBlock, $"{actionId} natural exact helper passes with no blockers");

            var naturalNewBlocker = CcImmunityBrakeRules.EvaluatePredictiveHelperExactRecheck(
                profile.JobId,
                actionId,
                TargetGameObjectId,
                exactTarget,
                targetJobId: 21,
                targetIdentityResolvedExactly: true,
                [Protection(SmartWardensPaeanTargetRules.WardensPaeanWardStatusId, 2_000)],
                naturalIntent,
                nowMilliseconds: 10_000);
            True(naturalNewBlocker.ShouldBlock, $"{actionId} natural helper catches a new blocker");

            var secondProtection = CcImmunityBrakeRules.EvaluatePredictiveHelperExactRecheck(
                profile.JobId,
                actionId,
                TargetGameObjectId,
                exactTarget,
                targetJobId: 21,
                targetIdentityResolvedExactly: true,
                [
                    Protection(MiracleCleanseFollowupRules.ResilienceStatusId, 300),
                    Protection(MiracleGuardFollowupRules.GuardStatusId, 300),
                ],
                intent,
                nowMilliseconds: 10_000);
            True(secondProtection.ShouldBlock, $"{actionId} blocks a second protection");
            Equal(
                MiracleGuardFollowupRules.GuardStatusId,
                secondProtection.BlockerStatusId,
                $"{actionId} reports other protection");
        }

        var unsupported = CcImmunityBrakeRules.EvaluatePredictiveHelperExactRecheck(
            ReactiveCounterCcProfileRules.PaladinJobId,
            123,
            TargetGameObjectId,
            exactTarget,
            targetJobId: 21,
            targetIdentityResolvedExactly: true,
            [Protection(MiracleCleanseFollowupRules.ResilienceStatusId, 300)],
            PredictiveIntent(123),
            nowMilliseconds: 10_000);
        True(unsupported.ShouldBlock, "unsupported action fails closed inside predictive recheck");
        Equal(
            CcImmunityBrakeDecisionReason.ActionNotCataloged,
            unsupported.Reason,
            "unsupported helper path is explicit");

        var wrongTarget = CcImmunityBrakeRules.EvaluatePredictiveHelperExactRecheck(
            ReactiveCounterCcProfileRules.RedMageJobId,
            MiracleInterceptConfirmationRules.ResolutionActionId,
            TargetGameObjectId + 1,
            exactTarget,
            targetJobId: 21,
            targetIdentityResolvedExactly: true,
            [Protection(MiracleCleanseFollowupRules.ResilienceStatusId, 300)],
            PredictiveIntent(MiracleInterceptConfirmationRules.ResolutionActionId),
            nowMilliseconds: 10_000);
        True(wrongTarget.ShouldBlock, "predictive target mismatch fails closed");
        Equal(
            CcImmunityBrakeDecisionReason.IncomingTargetMismatch,
            wrongTarget.Reason,
            "target mismatch is reported exactly");

        var wrongJob = CcImmunityBrakeRules.EvaluatePredictiveHelperExactRecheck(
            ReactiveCounterCcProfileRules.BlackMageJobId,
            MiracleInterceptConfirmationRules.ResolutionActionId,
            TargetGameObjectId,
            exactTarget,
            targetJobId: 21,
            targetIdentityResolvedExactly: true,
            [Protection(MiracleCleanseFollowupRules.ResilienceStatusId, 300)],
            PredictiveIntent(MiracleInterceptConfirmationRules.ResolutionActionId),
            nowMilliseconds: 10_000);
        True(wrongJob.ShouldBlock, "predictive local-job mismatch fails closed");
        Equal(CcImmunityBrakeDecisionReason.JobMismatch, wrongJob.Reason, "job mismatch reason");

        var unresolvedTarget = CcImmunityBrakeRules.EvaluatePredictiveHelperExactRecheck(
            ReactiveCounterCcProfileRules.RedMageJobId,
            MiracleInterceptConfirmationRules.ResolutionActionId,
            TargetGameObjectId,
            exactTarget,
            targetJobId: 21,
            targetIdentityResolvedExactly: false,
            [Protection(MiracleCleanseFollowupRules.ResilienceStatusId, 300)],
            PredictiveIntent(MiracleInterceptConfirmationRules.ResolutionActionId),
            nowMilliseconds: 10_000);
        True(unresolvedTarget.ShouldBlock, "unresolved predictive target fails closed");

        var missingStatusTelemetry = CcImmunityBrakeRules.EvaluatePredictiveHelperExactRecheck(
            ReactiveCounterCcProfileRules.RedMageJobId,
            MiracleInterceptConfirmationRules.ResolutionActionId,
            TargetGameObjectId,
            exactTarget,
            targetJobId: 21,
            targetIdentityResolvedExactly: true,
            activeStatuses: null,
            PredictiveIntent(MiracleInterceptConfirmationRules.ResolutionActionId),
            nowMilliseconds: 10_000);
        True(missingStatusTelemetry.ShouldBlock, "missing status telemetry fails closed");

        var missingNaturalStatusTelemetry =
            CcImmunityBrakeRules.EvaluatePredictiveHelperExactRecheck(
                ReactiveCounterCcProfileRules.RedMageJobId,
                MiracleInterceptConfirmationRules.ResolutionActionId,
                TargetGameObjectId,
                exactTarget,
                targetJobId: 21,
                targetIdentityResolvedExactly: true,
                activeStatuses: null,
                NaturalIntent(MiracleInterceptConfirmationRules.ResolutionActionId),
                nowMilliseconds: 10_000);
        True(
            missingNaturalStatusTelemetry.ShouldBlock,
            "missing natural-helper status telemetry fails closed");

        var missingScheduledRow = CcImmunityBrakeRules.EvaluatePredictiveHelperExactRecheck(
            ReactiveCounterCcProfileRules.RedMageJobId,
            MiracleInterceptConfirmationRules.ResolutionActionId,
            TargetGameObjectId,
            exactTarget,
            targetJobId: 21,
            targetIdentityResolvedExactly: true,
            activeStatuses: [],
            PredictiveIntent(MiracleInterceptConfirmationRules.ResolutionActionId),
            nowMilliseconds: 10_000);
        False(missingScheduledRow.ShouldBlock, "authoritative absence at hook is an immediate safe release");

        var absentScheduledWithOtherProtection =
            CcImmunityBrakeRules.EvaluatePredictiveHelperExactRecheck(
                ReactiveCounterCcProfileRules.RedMageJobId,
                MiracleInterceptConfirmationRules.ResolutionActionId,
                TargetGameObjectId,
                exactTarget,
                targetJobId: 21,
                targetIdentityResolvedExactly: true,
                [Protection(MiracleGuardFollowupRules.GuardStatusId, 300)],
                PredictiveIntent(MiracleInterceptConfirmationRules.ResolutionActionId),
                nowMilliseconds: 10_000);
        True(
            absentScheduledWithOtherProtection.ShouldBlock,
            "scheduled absence cannot bypass a different live protection");

        var refreshedScheduledRow = CcImmunityBrakeRules.EvaluatePredictiveHelperExactRecheck(
            ReactiveCounterCcProfileRules.RedMageJobId,
            MiracleInterceptConfirmationRules.ResolutionActionId,
            TargetGameObjectId,
            exactTarget,
            targetJobId: 21,
            targetIdentityResolvedExactly: true,
            [Protection(MiracleCleanseFollowupRules.ResilienceStatusId, 900)],
            PredictiveIntent(MiracleInterceptConfirmationRules.ResolutionActionId),
            nowMilliseconds: 10_000);
        True(refreshedScheduledRow.ShouldBlock, "same-ID duration refresh cannot inherit old episode");

        var miracleGuard = CcImmunityBrakeRules.EvaluatePredictiveHelperExactRecheck(
            ReactiveCounterCcProfileRules.WhiteMageJobId,
            MiracleInterceptConfirmationRules.MiracleOfNatureActionId,
            TargetGameObjectId,
            exactTarget,
            targetJobId: 21,
            targetIdentityResolvedExactly: true,
            [Protection(MiracleGuardFollowupRules.GuardStatusId, 275)],
            PredictiveIntent(
                MiracleInterceptConfirmationRules.MiracleOfNatureActionId,
                MiracleGuardFollowupRules.GuardStatusId,
                scheduledProtectionEndAtMilliseconds: 10_275),
            nowMilliseconds: 10_000);
        False(miracleGuard.ShouldBlock, "one scheduled Guard passes WHM hook recheck");

        var duplicateMiracleGuard = CcImmunityBrakeRules.EvaluatePredictiveHelperExactRecheck(
            ReactiveCounterCcProfileRules.WhiteMageJobId,
            MiracleInterceptConfirmationRules.MiracleOfNatureActionId,
            TargetGameObjectId,
            exactTarget,
            targetJobId: 21,
            targetIdentityResolvedExactly: true,
            [
                Protection(MiracleGuardFollowupRules.GuardStatusId, 275),
                Protection(MiracleGuardFollowupRules.GuardStatusAlternateId, 250),
            ],
            PredictiveIntent(
                MiracleInterceptConfirmationRules.MiracleOfNatureActionId,
                MiracleGuardFollowupRules.GuardStatusId,
                scheduledProtectionEndAtMilliseconds: 10_275),
            nowMilliseconds: 10_000);
        True(duplicateMiracleGuard.ShouldBlock, "alternate Guard blocks WHM hook recheck");

        var naturalPostGuardMiracle = CcImmunityBrakeRules.EvaluatePredictiveHelperExactRecheck(
            ReactiveCounterCcProfileRules.WhiteMageJobId,
            MiracleInterceptConfirmationRules.MiracleOfNatureActionId,
            TargetGameObjectId,
            exactTarget,
            targetJobId: 21,
            targetIdentityResolvedExactly: true,
            [Protection(MiracleGuardFollowupRules.GuardStatusId, 275)],
            NaturalIntent(
                MiracleInterceptConfirmationRules.MiracleOfNatureActionId,
                requireGuardAbsent: true),
            nowMilliseconds: 10_000);
        True(
            naturalPostGuardMiracle.ShouldBlock,
            "natural post-Guard Miracle catches Guard reappearance");

        var ordinaryNaturalMiracle = CcImmunityBrakeRules.EvaluatePredictiveHelperExactRecheck(
            ReactiveCounterCcProfileRules.WhiteMageJobId,
            MiracleInterceptConfirmationRules.MiracleOfNatureActionId,
            TargetGameObjectId,
            exactTarget,
            targetJobId: 21,
            targetIdentityResolvedExactly: true,
            [Protection(MiracleGuardFollowupRules.GuardStatusId, 275)],
            NaturalIntent(MiracleInterceptConfirmationRules.MiracleOfNatureActionId),
            nowMilliseconds: 10_000);
        False(
            ordinaryNaturalMiracle.ShouldBlock,
            "ordinary Miracle keeps the reviewed non-Guard blocker family");

        var manualAreaAction = CcImmunityBrakeRules.Evaluate(
            masterEnabled: true,
            jobEnabled: true,
            actionEnabled: true,
            localJobId: ReactiveCounterCcProfileRules.RedMageJobId,
            actionId: MiracleInterceptConfirmationRules.ResolutionActionId,
            incomingTargetId: TargetGameObjectId,
            resolvedTarget: exactTarget,
            targetJobId: 21,
            targetIdentityResolvedExactly: true,
            activeStatusIds: [MiracleCleanseFollowupRules.ResilienceStatusId]);
        False(manualAreaAction.ShouldBlock, "manual no-token area action remains outside brake catalog");
        Equal(
            CcImmunityBrakeDecisionReason.ActionNotCataloged,
            manualAreaAction.Reason,
            "manual no-token behavior is unchanged");
    }

    private static PredictiveCcBrakeBypassIntent PredictiveIntent(
        uint actionId,
        uint protectionStatusId = MiracleCleanseFollowupRules.ResilienceStatusId,
        long scheduledProtectionEndAtMilliseconds = 10_300) =>
        new(
            actionId,
            TargetGameObjectId,
            TargetEntityId,
            21,
            protectionStatusId,
            scheduledProtectionEndAtMilliseconds,
            SafeImpactLeadMilliseconds: 315);

    private static PredictiveCcBrakeBypassIntent NaturalIntent(
        uint actionId,
        bool requireGuardAbsent = false) =>
        new(
            actionId,
            TargetGameObjectId,
            TargetEntityId,
            21,
            ProtectionStatusId: 0,
            ScheduledProtectionEndAtMilliseconds: -1,
            SafeImpactLeadMilliseconds: 0,
            RequireGuardAbsent: requireGuardAbsent);

    private static PredictiveCcProtectionStatusObservation Protection(
        uint statusId,
        long remainingMilliseconds) =>
        new(statusId, remainingMilliseconds);

    private static bool TrySample(
        uint observedActionId = ActionId,
        uint observedTargetEntityId = TargetEntityId,
        ushort observedSourceSequence = 77,
        long observedAtMilliseconds = 1_420) =>
        ReactiveCounterCcImpactTimingRules.TryMeasureSample(
            ActionId,
            TargetEntityId,
            77,
            1_000,
            observedActionId,
            observedTargetEntityId,
            observedSourceSequence,
            observedAtMilliseconds,
            out _);

    private static ReactiveCounterCcImpactSample Sample(
        int delayMilliseconds,
        float edgeDistanceYalms)
    {
        if (!ReactiveCounterCcImpactTimingRules.TryCreateCalibrationSample(
                delayMilliseconds,
                edgeDistanceYalms,
                out var sample))
        {
            throw new InvalidOperationException("Invalid test calibration sample");
        }

        return sample;
    }

    private static bool CanConsume(
        PredictiveCcBrakeBypassIntent intent,
        bool alreadyConsumed = false,
        uint requestedActionId = ActionId,
        uint resolvedActionId = ActionId,
        ulong originalTargetId = TargetGameObjectId,
        ulong forwardedTargetId = TargetGameObjectId,
        bool targetSuppressedByRedirect = false,
        bool isActionInvocation = true,
        bool isNormalMode = true) =>
        PredictiveCcBrakeBypassRules.CanConsume(
            intent,
            alreadyConsumed,
            requestedActionId,
            resolvedActionId,
            originalTargetId,
            forwardedTargetId,
            targetSuppressedByRedirect,
            isActionInvocation,
            isNormalMode);

    private static void True(bool value, string label)
    {
        if (!value) throw new InvalidOperationException($"Expected true: {label}");
    }

    private static void False(bool value, string label) => True(!value, label);

    private static void Equal<T>(T expected, T actual, string label)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
    }
}
