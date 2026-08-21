using SeitonSense.Core;

internal static class MiracleInterceptSelfTests
{
    internal static void ExactStartSignaturesAreNarrow()
    {
        Equal(
            MiracleInterceptThreatKind.MarksmanSpite,
            Classify(29_415, 10, 20, 0x1B, false, true),
            "MCH early marker");
        Equal(
            MiracleInterceptThreatKind.Zantetsuken,
            Classify(29_537, 10, 20, 0, true, true),
            "SAM empty start packet");
        Equal(
            MiracleInterceptThreatKind.FuriousBacklash,
            Classify(39_188, 10, 10, 0, true, true),
            "VPR self start packet");
        Equal(
            MiracleInterceptThreatKind.Contradance,
            Classify(29_432, 10, 10, 0, true, true, variation: 0),
            "DNC variation-0 self startup packet");

        Equal(MiracleInterceptThreatKind.None, Classify(29_415, 10, 20, 3, false, true), "MCH hit");
        Equal(MiracleInterceptThreatKind.None, Classify(29_537, 10, 20, 0, true, false), "SAM extra effect");
        Equal(MiracleInterceptThreatKind.None, Classify(39_188, 10, 20, 0, true, true), "VPR wrong target");
        Equal(MiracleInterceptThreatKind.None, Classify(39_187, 10, 10, 0, true, true), "base Backlash excluded");
        Equal(
            MiracleInterceptThreatKind.None,
            Classify(29_432, 10, 10, 0, true, true, variation: 2),
            "DNC impact variation excluded");
        Equal(
            MiracleInterceptThreatKind.None,
            Classify(29_432, 10, 20, 0, true, true, variation: 0),
            "DNC startup must be self-targeted");

        Equal(
            3,
            MiracleInterceptRules.GetDispatchPriority(MiracleInterceptThreatKind.MarksmanSpite),
            "urgent one-shot threat priority");
        Equal(
            2,
            MiracleInterceptRules.GetDispatchPriority(MiracleInterceptThreatKind.Contradance),
            "DNC startup follows urgent one-shots");
        Equal(
            1,
            MiracleInterceptRules.GetDispatchPriority(
                MiracleInterceptThreatKind.PostPurifyCrowdControl),
            "post-Purify follow-up is lowest reactive priority");
        Equal(
            1,
            MiracleInterceptRules.GetDispatchPriority(
                MiracleInterceptThreatKind.PostGuardCrowdControl),
            "post-Guard follow-up shares the lowest reactive priority");
    }

    internal static void HeldInputDispatchesAndSignalCannotRearm()
    {
        var threat = Threat(MiracleInterceptThreatKind.Zantetsuken, 10, 100);
        var first = MiracleInterceptRules.Observe(
            MiracleInterceptState.Initial,
            Observation(threat, now: 100, held: true));
        True(first.ShouldDispatch, "first exact held opportunity dispatches");
        True(first.ShouldConsumeInputGeneration, "input consumed before native call");

        var duplicate = MiracleInterceptRules.Observe(
            first.NextState,
            Observation(threat, now: 101, held: true));
        False(duplicate.ShouldDispatch, "same event cannot retry after false/throw");
    }

    internal static void ViperMayAlreadyBeUnprotectedOnFirstFrame()
    {
        var threat = Threat(MiracleInterceptThreatKind.FuriousBacklash, 20, 500);
        var decision = MiracleInterceptRules.Observe(
            MiracleInterceptState.Initial,
            Observation(
                threat,
                now: 508,
                held: true,
                hardened: false,
                otherProtection: false));
        True(decision.ShouldDispatch, "39188 plus first live absence is enough");
    }

    internal static void ViperWaitsForActualProtectionAbsence()
    {
        var threat = Threat(MiracleInterceptThreatKind.FuriousBacklash, 20, 500);
        var protectedDecision = MiracleInterceptRules.Observe(
            MiracleInterceptState.Initial,
            Observation(threat, now: 501, held: true, hardened: true));
        False(protectedDecision.ShouldDispatch, "never cast into Hardened Scales");
        False(protectedDecision.ShouldConsumeInputGeneration, "waiting does not steal hold");

        var released = MiracleInterceptRules.Observe(
            protectedDecision.NextState,
            Observation(null, now: 512, held: true));
        True(released.ShouldDispatch, "first verified absence dispatches");
    }

    internal static void OtherProtectionAndRangeWaitOnlyInsideDeadline()
    {
        var threat = Threat(MiracleInterceptThreatKind.MarksmanSpite, 30, 1_000);
        var protectedDecision = MiracleInterceptRules.Observe(
            MiracleInterceptState.Initial,
            Observation(threat, 1_000, held: true, otherProtection: true));
        False(protectedDecision.ShouldDispatch, "other full immunity blocks");

        var outOfRange = MiracleInterceptRules.Observe(
            protectedDecision.NextState,
            Observation(null, 1_499, held: true, range: false));
        False(outOfRange.ShouldDispatch, "range failure waits boundedly");

        var expired = MiracleInterceptRules.Observe(
            outOfRange.NextState,
            Observation(null, 1_500, held: true));
        Equal(MiracleInterceptCancelReason.ThreatExpired, expired.CancelReason, "exact 500ms boundary expires");
        False(expired.ShouldConsumeInputGeneration, "expiry does not claim key");

        var viper = Threat(MiracleInterceptThreatKind.FuriousBacklash, 31, 2_000);
        var viperWaiting = MiracleInterceptRules.Observe(
            MiracleInterceptState.Initial,
            Observation(viper, 2_000, held: true, hardened: true));
        var viperExpired = MiracleInterceptRules.Observe(
            viperWaiting.NextState,
            Observation(null, 2_250, held: true));
        Equal(MiracleInterceptCancelReason.ThreatExpired, viperExpired.CancelReason, "exact 250ms Viper boundary expires");

        var dancer = Threat(MiracleInterceptThreatKind.Contradance, 32, 3_000);
        var dancerWaiting = MiracleInterceptRules.Observe(
            MiracleInterceptState.Initial,
            Observation(dancer, 3_000));
        var dancerExpired = MiracleInterceptRules.Observe(
            dancerWaiting.NextState,
            Observation(null, 3_750, held: true));
        Equal(
            MiracleInterceptCancelReason.ThreatExpired,
            dancerExpired.CancelReason,
            "exact 750ms DNC startup boundary expires");
    }

    internal static void HigherPriorityAndIdentityFailClosed()
    {
        var threat = Threat(MiracleInterceptThreatKind.Zantetsuken, 40, 2_000);
        var priority = MiracleInterceptRules.Observe(
            MiracleInterceptState.Initial,
            Observation(threat, 2_000, held: true) with { HigherPriorityClaimed = true });
        Equal(MiracleInterceptCancelReason.HigherPriorityClaimed, priority.CancelReason, "Purify/Rescue wins");

        var invalidIdentity = MiracleInterceptRules.Observe(
            MiracleInterceptState.Initial,
            Observation(threat, 2_000, held: true) with { CandidateIdentityValid = false });
        Equal(MiracleInterceptCancelReason.CandidateIdentityInvalid, invalidIdentity.CancelReason, "identity drift cancels");
        False(invalidIdentity.ShouldConsumeInputGeneration, "cancel does not consume");
    }

    internal static void StableHoldWinsAndTypingNeverTriggers()
    {
        var threat = Threat(MiracleInterceptThreatKind.MarksmanSpite, 50, 3_000);
        var fresh = MiracleInterceptRules.Observe(
            MiracleInterceptState.Initial,
            Observation(threat, 3_000, fresh: true, held: true));
        Equal(MiracleInterceptInputTrigger.HeldPhysicalKey, fresh.InputTrigger, "stable hold wins");

        var typing = MiracleInterceptRules.Observe(
            MiracleInterceptState.Initial,
            Observation(threat with
            {
                Signal = threat.Signal with { GlobalSequence = 51 },
            }, 3_000, fresh: true, held: true) with
            {
                IsTextInputActive = true,
            });
        False(typing.ShouldDispatch, "chat input is never intent");
    }

    internal static void ThreatJobsAndActionsMustMatch()
    {
        var wrongJob = Threat(MiracleInterceptThreatKind.Zantetsuken, 60, 4_000) with { TargetJobId = 31 };
        var result = MiracleInterceptRules.Observe(
            MiracleInterceptState.Initial,
            Observation(wrongJob, 4_000, held: true));
        Equal(MiracleInterceptCancelReason.InvalidSignal, result.CancelReason, "job mismatch");

        var wrongAction = Threat(MiracleInterceptThreatKind.Zantetsuken, 61, 4_000) with
        {
            Signal = new MiracleInterceptSignalKey(61, 29_415, 61, 1),
        };
        result = MiracleInterceptRules.Observe(
            MiracleInterceptState.Initial,
            Observation(wrongAction, 4_000, held: true));
        Equal(MiracleInterceptCancelReason.InvalidSignal, result.CancelReason, "action mismatch");
    }

    private static MiracleInterceptThreatKind Classify(
        uint action,
        uint caster,
        uint target,
        byte firstType,
        bool firstEmpty,
        bool additionalEmpty,
        byte variation = 0) =>
        MiracleInterceptRules.ClassifyExactStartSignal(
            action,
            caster,
            target,
            1,
            firstType,
            firstEmpty,
            additionalEmpty,
            variation);

    private static MiracleInterceptThreat Threat(
        MiracleInterceptThreatKind kind,
        uint entityId,
        long observedAt)
    {
        uint action = kind switch
        {
            MiracleInterceptThreatKind.MarksmanSpite => MiracleInterceptRules.MarksmanSpiteActionId,
            MiracleInterceptThreatKind.Zantetsuken => MiracleInterceptRules.ZantetsukenActionId,
            MiracleInterceptThreatKind.FuriousBacklash => MiracleInterceptRules.FuriousBacklashActionId,
            MiracleInterceptThreatKind.Contradance => MiracleInterceptRules.ContradanceActionId,
            _ => 0,
        };
        uint job = kind switch
        {
            MiracleInterceptThreatKind.MarksmanSpite => MiracleInterceptRules.MachinistJobId,
            MiracleInterceptThreatKind.Zantetsuken => MiracleInterceptRules.SamuraiJobId,
            MiracleInterceptThreatKind.FuriousBacklash => MiracleInterceptRules.ViperJobId,
            MiracleInterceptThreatKind.Contradance => MiracleInterceptRules.DancerJobId,
            _ => 0,
        };
        return new MiracleInterceptThreat(
            kind,
            new MiracleInterceptSignalKey(entityId, action, entityId + 100, 1),
            entityId + 10_000UL,
            entityId,
            job,
            observedAt);
    }

    private static MiracleInterceptObservation Observation(
        MiracleInterceptThreat? threat,
        long now,
        bool fresh = false,
        bool held = false,
        bool hardened = false,
        bool otherProtection = false,
        bool range = true) =>
        new(
            ConfigurationEnabled: true,
            IsCrystallineConflict: true,
            IsLocalCounterJobValid: true,
            HigherPriorityClaimed: false,
            NewThreat: threat,
            CandidateIdentityValid: true,
            CandidateAliveAndTargetable: true,
            HasHardenedScales: hardened,
            HasOtherVerifiedCcProtection: otherProtection,
            HasNativeRangeAndLineOfSight: range,
            IsTextInputActive: false,
            FreshKeyPressed: fresh,
            HeldKeyEligible: held,
            NowMilliseconds: now);

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
