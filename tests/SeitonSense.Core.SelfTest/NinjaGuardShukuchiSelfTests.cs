using SeitonSense.Core;

internal static class NinjaGuardShukuchiSelfTests
{
    private static readonly TargetPressureActorIdentity LocalPlayer = new(10_001, 1_001);

    public static void ConstantsAndStrictThresholdAreExact()
    {
        Equal(30u, NinjaGuardShukuchiRules.NinjaJobId, "NIN job ID");
        Equal(29_513u, NinjaGuardShukuchiRules.ActionId, "PvP Shukuchi action ID");
        Equal(3_054u, NinjaGuardShukuchiRules.GuardStatusId, "Guard status");
        Equal(3_673u, NinjaGuardShukuchiRules.GuardStatusAlternateId, "large-scale Guard status");
        Equal(20f, NinjaGuardShukuchiRules.NativeMaximumRangeYalms, "native range");

        True(NinjaGuardShukuchiRules.IsStrictlyBelowTwentyPercent(19, 100), "19 percent is eligible");
        True(NinjaGuardShukuchiRules.IsStrictlyBelowTwentyPercent(199, 1_000), "19.9 percent is eligible");
        False(NinjaGuardShukuchiRules.IsStrictlyBelowTwentyPercent(20, 100), "exactly 20 percent is excluded");
        False(NinjaGuardShukuchiRules.IsStrictlyBelowTwentyPercent(201, 1_000), "above 20 percent is excluded");
        False(NinjaGuardShukuchiRules.IsStrictlyBelowTwentyPercent(0, 100), "dead HP is excluded");
        False(NinjaGuardShukuchiRules.IsStrictlyBelowTwentyPercent(1, 0), "missing max HP is excluded");
        False(NinjaGuardShukuchiRules.IsStrictlyBelowTwentyPercent(101, 100), "invalid HP is excluded");
        True(
            NinjaGuardShukuchiRules.IsStrictlyBelowTwentyPercent(uint.MaxValue / 10, uint.MaxValue),
            "large HP arithmetic cannot overflow");

        True(NinjaGuardShukuchiRules.IsExactGuardStatus(3_054), "normal Guard row");
        True(NinjaGuardShukuchiRules.IsExactGuardStatus(3_673), "alternate Guard row");
        False(NinjaGuardShukuchiRules.IsExactGuardStatus(3_249), "Resilience is not Guard");
    }

    public static void NativeRangeAndPositionAreExact()
    {
        var origin = new NinjaGuardShukuchiPoint(0f, 0f, 0f);
        True(
            NinjaGuardShukuchiRules.IsWithinNativeRange(
                origin,
                new NinjaGuardShukuchiPoint(20f, 0f, 0f)),
            "exact 20-yalm boundary is included");
        False(
            NinjaGuardShukuchiRules.IsWithinNativeRange(
                origin,
                new NinjaGuardShukuchiPoint(20.001f, 0f, 0f)),
            "outside native range is excluded");
        True(
            NinjaGuardShukuchiRules.IsWithinNativeRange(
                origin,
                new NinjaGuardShukuchiPoint(12f, 16f, 0f)),
            "three-dimensional 20-yalm boundary is included");
        False(
            NinjaGuardShukuchiRules.IsWithinNativeRange(
                origin,
                new NinjaGuardShukuchiPoint(12f, 16.01f, 0f)),
            "vertical displacement counts toward native range");
        False(
            NinjaGuardShukuchiRules.IsWithinNativeRange(
                origin,
                new NinjaGuardShukuchiPoint(float.NaN, 0f, 0f)),
            "non-finite destination fails closed");
        False(
            NinjaGuardShukuchiRules.IsWithinNativeRange(
                origin with { X = float.PositiveInfinity },
                origin),
            "non-finite origin fails closed");
    }

    public static void CandidateRequiresEveryGuardLowHpGate()
    {
        var valid = Candidate(1, 20_001, 2_001, 19, 100);
        True(NinjaGuardShukuchiRules.IsEligibleCandidate(valid, LocalPlayer), "valid guarded target");

        False(NinjaGuardShukuchiRules.IsEligibleCandidate(valid with { GuardActive = false }, LocalPlayer), "live Guard required");
        False(NinjaGuardShukuchiRules.IsEligibleCandidate(valid with { CurrentHp = 20 }, LocalPlayer), "exact 20 percent rejected");
        False(NinjaGuardShukuchiRules.IsEligibleCandidate(valid with { Alive = false }, LocalPlayer), "dead rejected");
        False(NinjaGuardShukuchiRules.IsEligibleCandidate(valid with { Targetable = false }, LocalPlayer), "untargetable rejected");
        False(NinjaGuardShukuchiRules.IsEligibleCandidate(valid with { ExactCanonicalIdentity = false }, LocalPlayer), "inexact identity rejected");
        False(NinjaGuardShukuchiRules.IsEligibleCandidate(valid with { WithinNativeRange = false }, LocalPlayer), "range rejected");
        False(NinjaGuardShukuchiRules.IsEligibleCandidate(valid with { Position = new(float.NaN, 0f, 0f) }, LocalPlayer), "invalid point rejected");
        False(NinjaGuardShukuchiRules.IsEligibleCandidate(valid with { Actor = LocalPlayer }, LocalPlayer), "self rejected");
        False(NinjaGuardShukuchiRules.IsEligibleCandidate(valid with { PressureKnown = true, TeamTargetCount = -1 }, LocalPlayer), "invalid pressure rejected");
    }

    public static void PositivePressureIsOnlyARankingBonus()
    {
        var lowerHpUnknown = Candidate(1, 20_001, 2_001, 5, 100) with
        {
            PressureKnown = false,
        };
        var higherHpPositive = Candidate(2, 20_002, 2_002, 19, 100) with
        {
            PressureKnown = true,
            TeamTargetCount = 2,
        };
        Equal(
            1,
            NinjaGuardShukuchiRules.SelectBestCandidateIndex(
                [lowerHpUnknown, higherHpPositive],
                LocalPlayer),
            "fresh positive pressure is advantageous");

        var zeroPressure = higherHpPositive with { TeamTargetCount = 0 };
        Equal(
            0,
            NinjaGuardShukuchiRules.SelectBestCandidateIndex(
                [lowerHpUnknown, zeroPressure],
                LocalPlayer),
            "zero pressure is neutral and never required");

        var lowerHp = Candidate(3, 20_003, 2_003, 10, 100);
        var sameRatioEarlierSlot = Candidate(1, 20_004, 2_004, 100, 1_000);
        Equal(
            1,
            NinjaGuardShukuchiRules.SelectBestCandidateIndex(
                [lowerHp, sameRatioEarlierSlot],
                LocalPlayer),
            "equal ratios use stable S-slot");
    }

    public static void PartialSlotsWorkButAmbiguityFailsClosed()
    {
        Equal(
            0,
            NinjaGuardShukuchiRules.SelectBestCandidateIndex(
                [Candidate(4, 20_004, 2_004, 10, 100)],
                LocalPlayer),
            "one independently exact slot is enough");

        var duplicateSlot = new[]
        {
            Candidate(1, 20_001, 2_001, 10, 100),
            Candidate(1, 20_002, 2_002, 9, 100),
        };
        Equal(-1, NinjaGuardShukuchiRules.SelectBestCandidateIndex(duplicateSlot, LocalPlayer), "duplicate slot");

        var actor = new TargetPressureActorIdentity(20_001, 2_001);
        var duplicateActor = new[]
        {
            Candidate(1, actor.GameObjectId, actor.EntityId, 10, 100),
            Candidate(2, actor.GameObjectId, actor.EntityId, 9, 100),
        };
        Equal(-1, NinjaGuardShukuchiRules.SelectBestCandidateIndex(duplicateActor, LocalPlayer), "duplicate actor");
        Equal(-1, NinjaGuardShukuchiRules.SelectBestCandidateIndex(null, LocalPlayer), "null candidate set");
        Equal(-1, NinjaGuardShukuchiRules.SelectBestCandidateIndex([], default), "invalid local identity");
    }

    public static void DispatchRequiresEveryStaticAndInputGate()
    {
        var valid = Observation();
        Dispatch(valid, "valid held request");
        Cancel(valid with { ConfigurationEnabled = false }, NinjaGuardShukuchiDecisionReason.ConfigurationDisabled);
        Cancel(valid with { IsCrystallineConflict = false }, NinjaGuardShukuchiDecisionReason.OutsideCrystallineConflict);
        Cancel(valid with { LocalPlayer = default }, NinjaGuardShukuchiDecisionReason.LocalPlayerIdentityInvalid);
        Cancel(valid with { IsLocalPlayerAliveAndTargetable = false }, NinjaGuardShukuchiDecisionReason.LocalPlayerDeadOrUntargetable);
        Cancel(valid with { LocalJobId = 29 }, NinjaGuardShukuchiDecisionReason.LocalJobInvalid);
        Cancel(valid with { MetadataVerified = false }, NinjaGuardShukuchiDecisionReason.MetadataUnverified);
        Cancel(valid with { ActionHelpersSuppressedByGuard = true }, NinjaGuardShukuchiDecisionReason.GuardSuppressed);
        Cancel(valid with { HigherPriorityClaimed = true }, NinjaGuardShukuchiDecisionReason.HigherPriorityClaimed);
        Cancel(valid with { InputProbeSucceeded = false }, NinjaGuardShukuchiDecisionReason.InputProbeUnavailable);
        Cancel(valid with { IsTextInputActive = true }, NinjaGuardShukuchiDecisionReason.TextInputActive);
        Cancel(valid with { HeldGameplayKeyEligible = false }, NinjaGuardShukuchiDecisionReason.NoHeldGameplayKey);
        Cancel(valid with { ResolvedActionId = 29_514 }, NinjaGuardShukuchiDecisionReason.ResolvedActionInvalid);
        Cancel(valid with { ActionLocallyReady = false }, NinjaGuardShukuchiDecisionReason.ActionNotReady);
        Cancel(valid with { HardReset = true }, NinjaGuardShukuchiDecisionReason.HardReset);

        var none = NinjaGuardShukuchiRules.Observe(valid with
        {
            Candidates = [Candidate(1, 20_001, 2_001, 20, 100)],
        });
        False(none.ShouldDispatch, "no strict low-HP target does not dispatch");
        False(none.ShouldConsumeInputGeneration, "no target does not consume input");
        Equal(NinjaGuardShukuchiDecisionReason.NoExactGuardedLowHpTarget, none.Reason, "no target reason");
    }

    public static void FrozenIntentCannotRerankOrDrift()
    {
        var chosen = Candidate(2, 20_002, 2_002, 10, 100);
        var decision = NinjaGuardShukuchiRules.Observe(Observation() with
        {
            Candidates = [chosen],
        });
        var intent = decision.Intent ?? throw new InvalidOperationException("missing frozen intent");
        Equal(2, intent.EnemySlot, "frozen slot");
        Equal(chosen.Actor, intent.Target, "frozen actor");

        True(
            NinjaGuardShukuchiRules.CanUseExactIntent(
                intent,
                chosen,
                LocalPlayer,
                NinjaGuardShukuchiRules.ActionId,
                actionLocallyReady: true),
            "unchanged frozen actor remains valid");
        False(
            NinjaGuardShukuchiRules.CanUseExactIntent(
                intent,
                chosen with { CurrentHp = 20 },
                LocalPlayer,
                NinjaGuardShukuchiRules.ActionId,
                actionLocallyReady: true),
            "healing to exactly 20 percent cancels");
        False(
            NinjaGuardShukuchiRules.CanUseExactIntent(
                intent,
                chosen with { GuardActive = false },
                LocalPlayer,
                NinjaGuardShukuchiRules.ActionId,
                actionLocallyReady: true),
            "Guard ending cancels");
        False(
            NinjaGuardShukuchiRules.CanUseExactIntent(
                intent,
                Candidate(3, 20_003, 2_003, 1, 100),
                LocalPlayer,
                NinjaGuardShukuchiRules.ActionId,
                actionLocallyReady: true),
            "better alternate cannot replace frozen actor");
        False(
            NinjaGuardShukuchiRules.CanUseExactIntent(
                intent,
                chosen,
                LocalPlayer,
                29_514,
                actionLocallyReady: true),
            "Doton adjustment cancels");
        False(
            NinjaGuardShukuchiRules.CanUseExactIntent(
                intent,
                chosen,
                LocalPlayer,
                NinjaGuardShukuchiRules.ActionId,
                actionLocallyReady: false),
            "readiness drift cancels");
    }

    public static void CastCancellationAndRetryKeepExactIntent()
    {
        var intent = NinjaGuardShukuchiRules.Observe(Observation()).Intent ??
                     throw new InvalidOperationException("missing intent");
        var request = new HeldCastCancellationRequest(
            HeldCastCancellationHelperKind.NinjaGuardShukuchi,
            intent.ActionId,
            LocalPlayer,
            intent.Target,
            FrozenKeyCode: 0x57,
            IntentEpochToken: 1);
        True(request.IsValid, "cast cancellation request owns exact action/actor/key");
        Equal(5, (int)HeldCastCancellationHelperKind.NinjaGuardShukuchi, "priority before NIN Seiton");
        Equal(6, (int)HeldCastCancellationHelperKind.NinjaSeiton, "NIN Seiton follows Guard-Shukuchi");

        var completion = HeldActionRetryRules.Complete(
            HeldActionRetryState.Initial,
            nowMilliseconds: 1_000,
            ClientActionAttemptOutcome.ClientRejected);
        True(completion.RetryScheduled, "only proven client false may retain frozen Shukuchi");
        True(
            HeldActionRetryRules.RetainsSchedulerFrame(
                completion.NextState,
                nowMilliseconds: 1_001,
                exactIntentValid: true,
                actionSpecificReady: true,
                targetSpecificReady: true),
            "same exact retry retains its priority while throttled");
        False(
            HeldActionRetryRules.Complete(
                HeldActionRetryState.Initial,
                nowMilliseconds: 1_000,
                ClientActionAttemptOutcome.AcceptanceUnknown).RetryScheduled,
            "ambiguous acceptance never retries a possible jump");
    }

    public static void ContinuousHoldRequiresProvenCooldownRearm()
    {
        var acceptedHold = NinjaGuardShukuchiRules.BeginAcceptedHold(0x57);
        False(acceptedHold.HasAvailableReadyEpoch, "accepted action spends the initial ready epoch");
        acceptedHold = NinjaGuardShukuchiRules.ObserveAcceptedHold(
            acceptedHold,
            hardReset: false,
            ownershipContextValid: true,
            exactHeldKeyStillDown: true,
            cooldownStateKnown: true,
            cooldownReady: false);
        True(acceptedHold.ObservedCooldownUnavailable, "real cooldown unavailability is observed");
        acceptedHold = NinjaGuardShukuchiRules.ObserveAcceptedHold(
            acceptedHold,
            hardReset: false,
            ownershipContextValid: true,
            exactHeldKeyStillDown: true,
            cooldownStateKnown: true,
            cooldownReady: true);
        True(acceptedHold.HasAvailableReadyEpoch, "later cooldown rearm opens one distinct held epoch");
        True(
            NinjaGuardShukuchiRules.TrySpendReadyEpoch(
                acceptedHold,
                acceptedHold.CurrentReadyEpochToken,
                out var spent),
            "repeat epoch is spent before native work");
        False(spent.HasAvailableReadyEpoch, "same rearmed epoch cannot replay");
        Equal(
            NinjaGuardShukuchiHoldState.Initial,
            NinjaGuardShukuchiRules.ObserveAcceptedHold(
                spent,
                hardReset: false,
                ownershipContextValid: true,
                exactHeldKeyStillDown: false,
                cooldownStateKnown: true,
                cooldownReady: true),
            "physical release ends continuous ownership");
    }

    private static NinjaGuardShukuchiObservation Observation() => new(
        ConfigurationEnabled: true,
        IsCrystallineConflict: true,
        LocalJobId: NinjaGuardShukuchiRules.NinjaJobId,
        LocalPlayer,
        IsLocalPlayerAliveAndTargetable: true,
        MetadataVerified: true,
        ActionHelpersSuppressedByGuard: false,
        HigherPriorityClaimed: false,
        InputProbeSucceeded: true,
        IsTextInputActive: false,
        HeldGameplayKeyEligible: true,
        ResolvedActionId: NinjaGuardShukuchiRules.ActionId,
        ActionLocallyReady: true,
        Candidates: [Candidate(1, 20_001, 2_001, 19, 100)]);

    private static NinjaGuardShukuchiCandidate Candidate(
        int slot,
        ulong gameObjectId,
        uint entityId,
        uint currentHp,
        uint maximumHp) => new(
        slot,
        new TargetPressureActorIdentity(gameObjectId, entityId),
        ExactCanonicalIdentity: true,
        Alive: true,
        Targetable: true,
        currentHp,
        maximumHp,
        GuardActive: true,
        new NinjaGuardShukuchiPoint(slot, 0f, 0f),
        WithinNativeRange: true,
        PressureKnown: false,
        TeamTargetCount: 0);

    private static void Dispatch(NinjaGuardShukuchiObservation observation, string message)
    {
        var decision = NinjaGuardShukuchiRules.Observe(observation);
        True(decision.ShouldDispatch, message);
        True(decision.ShouldConsumeInputGeneration, $"{message}: consumes input");
    }

    private static void Cancel(
        NinjaGuardShukuchiObservation observation,
        NinjaGuardShukuchiDecisionReason expected)
    {
        var decision = NinjaGuardShukuchiRules.Observe(observation);
        False(decision.ShouldDispatch, expected.ToString());
        Equal(expected, decision.Reason, $"{expected} reason");
    }

    private static void True(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void False(bool condition, string message) => True(!condition, message);

    private static void Equal<T>(T expected, T actual, string message)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
    }
}
