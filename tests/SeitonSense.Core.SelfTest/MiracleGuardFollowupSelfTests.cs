using SeitonSense.Core;

internal static class MiracleGuardFollowupSelfTests
{
    internal static void ExactGuardRowsAndAbsenceCannotSyntheticArm()
    {
        True(MiracleGuardFollowupRules.IsExactGuardStatus(3_054), "CC Guard row");
        True(MiracleGuardFollowupRules.IsExactGuardStatus(3_673), "alternate Guard row");
        False(MiracleGuardFollowupRules.IsExactGuardStatus(0), "unknown row");
        Equal(
            0L,
            MiracleInterceptRules.GetThreatLifetimeMilliseconds(
                MiracleInterceptThreatKind.PostGuardCrowdControl),
            "polling follow-up cannot become a broad native start signature");
        False(
            MiracleInterceptRules.IsExpectedJob(
                MiracleInterceptThreatKind.PostGuardCrowdControl,
                24),
            "post-Guard label is not inferred from an enemy job");
        True(
            new MiracleInterceptPendingAttempt(
                LocalCasterEntityId: 100,
                ActionId: MiracleInterceptConfirmationRules.MiracleOfNatureActionId,
                TargetGameObjectId: 0x1010,
                TargetEntityId: 10,
                Threat: MiracleInterceptThreatKind.PostGuardCrowdControl,
                UseActionAccepted: true,
                AttemptedAtMilliseconds: 1_000).IsValid,
            "shared landing confirmation accepts the post-Guard label without a removed status");

        var target = Target(slot: 1, entityId: 10);
        var absent = MiracleGuardFollowupRules.Observe(
            MiracleGuardFollowupState.Initial,
            Observation(Candidate(target, guardCount: 0, teamTargetCount: 4), 1_000));
        False(absent.ShouldPromote, "absence without prior exact presence cannot arm");
        Equal(
            MiracleGuardFollowupPhase.WaitingForGuard,
            Find(absent.NextState, 1).Phase,
            "actor waits for a positive Guard observation");

        var stillAbsent = MiracleGuardFollowupRules.Observe(
            absent.NextState,
            Observation(Candidate(target, guardCount: 0, teamTargetCount: 4), 1_100));
        False(stillAbsent.ShouldPromote, "repeated absence remains inert");
        Equal(0, stillAbsent.NewGuardEpisodeCount, "absence invents no episode");
    }

    internal static void FirstVerifiedAbsentFramePromotesOnceAndRequiresPositiveRearm()
    {
        var target = Target(slot: 2, entityId: 20);
        var present = MiracleGuardFollowupRules.Observe(
            MiracleGuardFollowupState.Initial,
            Observation(Candidate(target, guardCount: 1), 1_000));
        Equal(1, present.NewGuardEpisodeCount, "positive Guard opens one episode");
        Equal(
            MiracleGuardFollowupPhase.GuardPresent,
            Find(present.NextState, 2).Phase,
            "Guard presence is latched");

        var firstAbsent = MiracleGuardFollowupRules.Observe(
            present.NextState,
            Observation(Candidate(target, guardCount: 0, teamTargetCount: 2), 1_001));
        True(firstAbsent.ShouldPromote, "first verified absent framework frame promotes immediately");
        Equal(1, firstAbsent.NewReleaseOpportunityCount, "one release edge");
        Equal(1_001L, firstAbsent.PromotionIntent!.Value.ReleasedAtMilliseconds, "release timestamp is exact");
        Equal(target, firstAbsent.PromotionIntent.Value.Target, "frozen actor is exact");
        Equal(
            MiracleGuardFollowupPhase.WaitingForGuard,
            Find(firstAbsent.NextState, 2).Phase,
            "episode is spent before runtime dispatch");

        var duplicateAbsent = MiracleGuardFollowupRules.Observe(
            firstAbsent.NextState,
            Observation(Candidate(target, guardCount: 0, teamTargetCount: 4), 1_002));
        False(duplicateAbsent.ShouldPromote, "absence cannot retry a spent episode");

        var rearmed = MiracleGuardFollowupRules.Observe(
            duplicateAbsent.NextState,
            Observation(Candidate(target, guardCount: 1), 1_100));
        Equal(1, rearmed.NewGuardEpisodeCount, "a later positive Guard rearms");
        var secondAbsent = MiracleGuardFollowupRules.Observe(
            rearmed.NextState,
            Observation(Candidate(target, guardCount: 0, teamTargetCount: 2), 1_101));
        True(secondAbsent.ShouldPromote, "a genuinely rearmed episode may promote once");
    }

    internal static void FreshTeamFocusAndPriorityWaitOnlyInsideOriginalWindow()
    {
        var target = Target(slot: 3, entityId: 30);
        var present = MiracleGuardFollowupRules.Observe(
            MiracleGuardFollowupState.Initial,
            Observation(Candidate(target, guardCount: 1), 1_000));
        var release = MiracleGuardFollowupRules.Observe(
            present.NextState,
            Observation(
                Candidate(target, guardCount: 0, teamTargetCountKnown: false),
                1_100,
                higherPriority: true));
        False(release.ShouldPromote, "higher priority cannot dispatch the release");
        Equal(
            MiracleGuardFollowupPhase.ReleaseOpportunity,
            Find(release.NextState, 3).Phase,
            "release remains bounded to its original edge");

        var belowFocus = MiracleGuardFollowupRules.Observe(
            release.NextState,
            Observation(Candidate(target, guardCount: 0, teamTargetCount: 1), 1_200));
        False(belowFocus.ShouldPromote, "one team target is insufficient");

        var inside = MiracleGuardFollowupRules.Observe(
            belowFocus.NextState,
            Observation(Candidate(target, guardCount: 0, teamTargetCount: 2), 1_599));
        True(inside.ShouldPromote, "fresh total team focus may promote at 499 ms");

        present = MiracleGuardFollowupRules.Observe(
            inside.NextState,
            Observation(Candidate(target, guardCount: 1), 2_000));
        release = MiracleGuardFollowupRules.Observe(
            present.NextState,
            Observation(Candidate(target, guardCount: 0, teamTargetCount: 1), 2_100));
        var boundary = MiracleGuardFollowupRules.Observe(
            release.NextState,
            Observation(Candidate(target, guardCount: 0, teamTargetCount: 2), 2_600));
        False(boundary.ShouldPromote, "500 ms boundary is already expired");
        Equal(1, boundary.ExpiredOpportunityCount, "expiry is diagnosed exactly once");
    }

    internal static void SimultaneousReleaseRanksHpThenSlotAndRetiresEveryOther()
    {
        var slot1 = Target(slot: 1, entityId: 101);
        var slot2 = Target(slot: 2, entityId: 102);
        var present = MiracleGuardFollowupRules.Observe(
            MiracleGuardFollowupState.Initial,
            Observation(
                [
                    Candidate(slot1, guardCount: 1, hp: 5_000, maxHp: 10_000),
                    Candidate(slot2, guardCount: 1, hp: 2_000, maxHp: 10_000),
                ],
                1_000));
        var released = MiracleGuardFollowupRules.Observe(
            present.NextState,
            Observation(
                [
                    Candidate(slot1, guardCount: 0, teamTargetCount: 2, hp: 5_000, maxHp: 10_000),
                    Candidate(slot2, guardCount: 0, teamTargetCount: 2, hp: 2_000, maxHp: 10_000),
                ],
                1_001));
        True(released.ShouldPromote, "one simultaneous opportunity promotes");
        Equal(slot2, released.PromotionIntent!.Value.Target, "lowest exact HP ratio wins");
        Equal(1, released.RetiredOtherOpportunityCount, "other ready opportunity is spent");
        True(
            released.NextState.Actors.All(static actor =>
                actor.Phase == MiracleGuardFollowupPhase.WaitingForGuard),
            "all same-frame release opportunities retire before dispatch");

        var delayed = MiracleGuardFollowupRules.Observe(
            released.NextState,
            Observation(
                [
                    Candidate(slot1, guardCount: 0, teamTargetCount: 2),
                    Candidate(slot2, guardCount: 0, teamTargetCount: 2),
                ],
                1_002));
        False(delayed.ShouldPromote, "retired opportunity cannot dispatch on a later frame");

        present = MiracleGuardFollowupRules.Observe(
            delayed.NextState,
            Observation(
                [Candidate(slot1, guardCount: 1), Candidate(slot2, guardCount: 1)],
                2_000));
        var tied = MiracleGuardFollowupRules.Observe(
            present.NextState,
            Observation(
                [
                    Candidate(slot2, guardCount: 0, teamTargetCount: 2, hp: 4_000, maxHp: 8_000),
                    Candidate(slot1, guardCount: 0, teamTargetCount: 2, hp: 5_000, maxHp: 10_000),
                ],
                2_001));
        Equal(slot1, tied.PromotionIntent!.Value.Target, "equal HP ratio uses lower S-slot");
    }

    internal static void IdentityLifeAndStatusAmbiguityBreakTheEpisode()
    {
        var original = Target(slot: 4, entityId: 40);
        var replacement = Target(slot: 4, entityId: 41);
        var present = MiracleGuardFollowupRules.Observe(
            MiracleGuardFollowupState.Initial,
            Observation(Candidate(original, guardCount: 1), 1_000));
        var replaced = MiracleGuardFollowupRules.Observe(
            present.NextState,
            Observation(Candidate(replacement, guardCount: 0, teamTargetCount: 4), 1_001));
        False(replaced.ShouldPromote, "same slot with changed life identity cannot release");
        var oldReturnsAbsent = MiracleGuardFollowupRules.Observe(
            replaced.NextState,
            Observation(Candidate(original, guardCount: 0, teamTargetCount: 4), 1_002));
        False(oldReturnsAbsent.ShouldPromote, "returning absence cannot synthesize the old episode");

        present = MiracleGuardFollowupRules.Observe(
            oldReturnsAbsent.NextState,
            Observation(Candidate(original, guardCount: 1), 1_100));
        var dead = MiracleGuardFollowupRules.Observe(
            present.NextState,
            Observation(Candidate(original, guardCount: 0, alive: false), 1_101));
        Equal(0, dead.NextState.Actors.Length, "life/targetability loss drops tracked identity");
        var revivedAbsent = MiracleGuardFollowupRules.Observe(
            dead.NextState,
            Observation(Candidate(original, guardCount: 0, teamTargetCount: 4), 1_102));
        False(revivedAbsent.ShouldPromote, "a new life must show Guard again");

        present = MiracleGuardFollowupRules.Observe(
            revivedAbsent.NextState,
            Observation(Candidate(original, guardCount: 1), 1_200));
        var ambiguousRows = MiracleGuardFollowupRules.Observe(
            present.NextState,
            Observation(Candidate(original, guardCount: 2, teamTargetCount: 4), 1_201));
        Equal(0, ambiguousRows.NextState.Actors.Length, "duplicate Guard rows are not absence proof");
    }

    internal static void ConfigurationContextClockAndHardResetClearAllEpisodes()
    {
        var target = Target(slot: 5, entityId: 50);
        var present = MiracleGuardFollowupRules.Observe(
            MiracleGuardFollowupState.Initial,
            Observation(Candidate(target, guardCount: 1), 1_000));
        var disabled = MiracleGuardFollowupRules.Observe(
            present.NextState,
            Observation(Candidate(target, guardCount: 1), 1_001) with
            {
                ConfigurationEnabled = false,
            });
        Equal(MiracleGuardFollowupCancelReason.ConfigurationDisabled, disabled.CancelReason, "config gate");
        Equal(0, disabled.NextState.Actors.Length, "config closes all episodes");

        present = MiracleGuardFollowupRules.Observe(
            MiracleGuardFollowupState.Initial,
            Observation(Candidate(target, guardCount: 1), 2_000));
        var context = MiracleGuardFollowupRules.Observe(
            present.NextState,
            Observation(Candidate(target, guardCount: 1), 2_001) with
            {
                IsCrystallineConflict = false,
            });
        Equal(MiracleGuardFollowupCancelReason.OutsideCrystallineConflict, context.CancelReason, "context gate");

        present = MiracleGuardFollowupRules.Observe(
            MiracleGuardFollowupState.Initial,
            Observation(Candidate(target, guardCount: 1), 3_000));
        var clock = MiracleGuardFollowupRules.Observe(
            present.NextState,
            Observation(Candidate(target, guardCount: 0, teamTargetCount: 4), 2_999));
        Equal(MiracleGuardFollowupCancelReason.ClockMovedBackwards, clock.CancelReason, "clock rollback");
        Equal(0, clock.NextState.Actors.Length, "clock rollback clears all episodes");

        var hardReset = MiracleGuardFollowupRules.Observe(
            present.NextState,
            Observation(Candidate(target, guardCount: 1), 3_001) with { HardReset = true });
        Equal(MiracleGuardFollowupCancelReason.HardReset, hardReset.CancelReason, "hard reset reason");
        Equal(0, hardReset.NextState.Actors.Length, "hard reset clears all episodes");
    }

    private static MiracleGuardFollowupTargetIdentity Target(int slot, uint entityId) =>
        new(slot, 0x1000UL + entityId, entityId, 30);

    private static MiracleGuardFollowupCandidate Candidate(
        MiracleGuardFollowupTargetIdentity target,
        int guardCount,
        bool alive = true,
        bool teamTargetCountKnown = true,
        int teamTargetCount = 0,
        uint hp = 5_000,
        uint maxHp = 10_000) =>
        new(
            target,
            IsExactCanonicalEnemy: true,
            IsAliveAndTargetable: alive,
            guardCount,
            hp,
            maxHp,
            teamTargetCountKnown,
            teamTargetCount);

    private static MiracleGuardFollowupObservation Observation(
        MiracleGuardFollowupCandidate candidate,
        long now,
        bool higherPriority = false) =>
        Observation([candidate], now, higherPriority);

    private static MiracleGuardFollowupObservation Observation(
        IReadOnlyList<MiracleGuardFollowupCandidate> candidates,
        long now,
        bool higherPriority = false) =>
        new(
            ConfigurationEnabled: true,
            IsCrystallineConflict: true,
            IsLocalCounterJobValid: true,
            HigherPriorityClaimed: higherPriority,
            candidates,
            NowMilliseconds: now);

    private static MiracleGuardFollowupActorState Find(
        MiracleGuardFollowupState state,
        int slot) =>
        state.Actors.Single(actor => actor.Target.EnemySlot == slot);

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
