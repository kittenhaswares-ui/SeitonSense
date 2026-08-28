using SeitonSense.Core;

internal static class RedMageGuardEngageSelfTests
{
    private static readonly TargetPressureActorIdentity Local = new(10_000, 1_000);

    public static void ExactIdsThresholdsAndFreshWindowArePinned()
    {
        Equal(35u, RedMageGuardEngageRules.RedMageJobId, "RDM job");
        Equal(29_699u, RedMageGuardEngageRules.CorpsACorpsActionId, "Corps-a-corps");
        Equal(41_488u, RedMageGuardEngageRules.MeleeComboCarrierActionId, "fresh melee starter");
        Equal(3_054u, RedMageGuardEngageRules.GuardStatusId, "Guard status");
        Equal(3_673u, RedMageGuardEngageRules.GuardAlternateStatusId, "large-scale Guard status");
        Equal(80, RedMageGuardEngageRules.DefaultMinimumHpPercent, "default HP");
        Equal(50, RedMageGuardEngageRules.DefaultMinimumMpPercent, "default MP");
        True(RedMageGuardEngageRules.MeetsInclusivePercent(80, 100, 80), "inclusive HP edge");
        False(RedMageGuardEngageRules.MeetsInclusivePercent(79, 100, 80), "below HP edge");
        True(RedMageGuardEngageRules.MeetsInclusivePercent(uint.MaxValue, uint.MaxValue, 100), "overflow safe");

        True(RedMageGuardEngageRules.TryComputeGuardDeadline(1_000, 4_000, out var full), "fresh Guard");
        Equal(2_000L, full, "full Guard gets at most one second");
        True(RedMageGuardEngageRules.TryComputeGuardDeadline(1_000, 3_050, out var late), "late first-second sample");
        Equal(1_050L, late, "lease ends at exact first-second boundary");
        False(RedMageGuardEngageRules.TryComputeGuardDeadline(1_000, 3_000, out _), "three seconds remaining is too old");
        False(RedMageGuardEngageRules.TryComputeGuardDeadline(1_000, 4_251, out _), "impossible telemetry");
    }

    public static void PreExistingGuardCannotBecomeAFreshEpisode()
    {
        var preExisting = RedMageGuardEngageRules.ObserveGuardEpisode(
            RedMageGuardEpisodeState.Initial,
            RedMageGuardObservationKind.ExactActive);
        False(preExisting.HasUnspentEpisode, "first-seen active Guard is latched, not armed");

        var ambiguous = RedMageGuardEngageRules.ObserveGuardEpisode(
            preExisting,
            RedMageGuardObservationKind.Ambiguous);
        Equal(preExisting, ambiguous, "ambiguity never synthesizes absence");

        var absent = RedMageGuardEngageRules.ObserveGuardEpisode(
            ambiguous,
            RedMageGuardObservationKind.Absent);
        var fresh = RedMageGuardEngageRules.ObserveGuardEpisode(
            absent,
            RedMageGuardObservationKind.ExactActive);
        True(fresh.HasUnspentEpisode, "exact absent-to-present edge opens one episode");
        True(RedMageGuardEngageRules.TrySpendGuardEpisode(fresh, fresh.CurrentEpisodeToken, out var spent), "episode spends once");
        False(spent.HasUnspentEpisode, "spent episode cannot repeat");
        False(RedMageGuardEngageRules.TrySpendGuardEpisode(spent, fresh.CurrentEpisodeToken, out _), "same token cannot spend twice");
    }

    public static void InitialGatesAreInclusiveAndFailClosed()
    {
        Dispatch(RedMageGuardEngageRules.Evaluate(Observation()), "exact valid observation");
        None(Observation() with { LocalCurrentHp = 79 }, RedMageGuardEngageDecisionReason.LocalHealthBelowThreshold, "low HP");
        None(Observation() with { LocalCurrentMp = 4_999 }, RedMageGuardEngageDecisionReason.LocalMpBelowThreshold, "low MP");
        None(Observation() with { LocalMaximumMp = 9_999 }, RedMageGuardEngageDecisionReason.LocalMpBelowThreshold, "untrusted MP maximum");
        None(Observation() with { LocalJobId = 30 }, RedMageGuardEngageDecisionReason.WrongJob, "RDM only");
        None(Observation() with { ActionHelpersSuppressedByGuard = true }, RedMageGuardEngageDecisionReason.GuardSuppressed, "own Guard");
        None(Observation() with { HigherPriorityClaimed = true }, RedMageGuardEngageDecisionReason.HigherPriorityClaimed, "priority");
        None(Observation() with { ResolvedComboCarrierActionId = RedMageGuardEngageRules.EnchantedZwerchhauActionId }, RedMageGuardEngageDecisionReason.MeleeStarterUnavailable, "mid combo excluded");
        None(Observation() with { Context = SupportedPvPContext.None }, RedMageGuardEngageDecisionReason.UnsupportedContext, "PvP context");
    }

    public static void CandidateAndFrozenIntentNeverSubstitute()
    {
        var decision = RedMageGuardEngageRules.Evaluate(Observation());
        Dispatch(decision, "freeze");
        var intent = decision.Intent!.Value;
        var candidate = Candidate();
        True(CanUse(intent, candidate, 1_500), "same frozen actor inside lease");
        True(CanUse(intent, candidate with { GuardEpisodeUnspent = false }, 1_500), "spent frozen episode remains usable by its lease");
        False(CanUse(intent, candidate with { Actor = new(20_002, 2_002) }, 1_500), "actor drift");
        False(CanUse(intent, candidate with { GuardEpisodeToken = 8 }, 1_500), "episode drift");
        False(CanUse(intent, candidate with { ExactGuardStatusCount = 2 }, 1_500), "duplicate Guard rows");
        False(CanUse(intent, candidate with { HasOtherReviewedProtection = true }, 1_500), "overlapping protection");
        False(CanUse(intent, candidate with { HasNativeRangeAndLineOfSight = false }, 1_500), "range or LoS drift");
        False(CanUse(intent, candidate, 2_001), "first-second deadline passed");
        False(CanUse(intent, candidate, 1_500, exactKeyDown: false), "key release");
        False(CanUse(intent, candidate, 1_500, comboActionId: RedMageGuardEngageRules.EnchantedRedoublementActionId), "carrier changed");
    }

    public static void RankingIsDeterministicAcrossExactTargets()
    {
        var candidates = new[]
        {
            Candidate() with { EnemySlot = 3, Actor = new(20_003, 2_003), CurrentHp = 50 },
            Candidate() with { EnemySlot = 2, Actor = new(20_002, 2_002), CurrentHp = 20, GuardRemainingMilliseconds = 3_500 },
            Candidate() with { EnemySlot = 1, Actor = new(20_001, 2_001), CurrentHp = 20, GuardRemainingMilliseconds = 3_900 },
        };
        var decision = RedMageGuardEngageRules.Evaluate(Observation() with { Candidates = candidates });
        Dispatch(decision, "ranked");
        Equal(1, decision.Intent!.Value.EnemySlot, "lowest HP then youngest Guard then stable slot");
    }

    private static RedMageGuardEngageObservation Observation() => new(
        ConfigurationEnabled: true,
        Context: SupportedPvPContext.CrystallineConflict,
        LocalJobId: RedMageGuardEngageRules.RedMageJobId,
        LocalPlayer: Local,
        LocalAliveAndTargetable: true,
        LocalCurrentHp: 80,
        LocalMaximumHp: 100,
        LocalCurrentMp: 5_000,
        LocalMaximumMp: 10_000,
        MinimumHpPercent: 80,
        MinimumMpPercent: 50,
        MetadataVerified: true,
        ActionHelpersSuppressedByGuard: false,
        HigherPriorityClaimed: false,
        InputProbeSucceeded: true,
        IsTextInputActive: false,
        HeldGameplayKeyEligible: true,
        HeldGameplayKeyCode: 65,
        ResolvedActionId: RedMageGuardEngageRules.CorpsACorpsActionId,
        CorpsReady: true,
        ResolvedComboCarrierActionId: RedMageGuardEngageRules.MeleeComboCarrierActionId,
        MeleeStarterReady: true,
        NowMilliseconds: 1_000,
        Candidates: [Candidate()]);

    private static RedMageGuardEngageCandidate Candidate() => new(
        SupportedPvPContext.CrystallineConflict,
        EnemySlot: 1,
        Actor: new TargetPressureActorIdentity(20_001, 2_001),
        ExactCanonicalIdentity: true,
        Alive: true,
        Targetable: true,
        CurrentHp: 40,
        MaximumHp: 100,
        ExactGuardStatusCount: 1,
        GuardRemainingMilliseconds: 4_000,
        GuardEpisodeToken: 7,
        GuardEpisodeUnspent: true,
        HasOtherReviewedProtection: false,
        HasValidActionTarget: true,
        HasNativeRangeAndLineOfSight: true);

    private static bool CanUse(
        RedMageGuardEngageIntent intent,
        RedMageGuardEngageCandidate candidate,
        long now,
        bool exactKeyDown = true,
        uint comboActionId = RedMageGuardEngageRules.MeleeComboCarrierActionId) =>
        RedMageGuardEngageRules.CanUseFrozenIntent(
            intent,
            candidate,
            now,
            exactKeyDown,
            RedMageGuardEngageRules.CorpsACorpsActionId,
            corpsReady: true,
            comboActionId,
            meleeStarterReady: true);

    private static void Dispatch(RedMageGuardEngageDecision decision, string label)
    {
        True(decision.ShouldDispatch, label);
        Equal(RedMageGuardEngageDecisionReason.None, decision.Reason, label);
    }

    private static void None(
        RedMageGuardEngageObservation observation,
        RedMageGuardEngageDecisionReason reason,
        string label)
    {
        var decision = RedMageGuardEngageRules.Evaluate(observation);
        False(decision.ShouldDispatch, label);
        Equal(reason, decision.Reason, label);
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
            throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
    }
}
