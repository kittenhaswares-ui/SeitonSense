using SeitonSense.Core;

internal static class AutoEnemyFocusMarkSelfTests
{
    internal static void EligibilityIsStrict()
    {
        var valid = Candidate(slot: 1, hp: 50, maxHp: 100, lowMp: false, mp: 5000, maxMp: 10000);
        True(AutoEnemyFocusMarkRules.IsEligible(valid), "50% HP is eligible");
        False(AutoEnemyFocusMarkRules.IsEligible(valid with { CurrentHp = 51 }), "above 50% without low MP");
        True(AutoEnemyFocusMarkRules.IsEligible(valid with { CurrentHp = 51, LowMpActive = true }), "trusted low MP");
        False(AutoEnemyFocusMarkRules.IsEligible(valid with { GuardUnavailable = false }), "Guard ready");
        False(AutoEnemyFocusMarkRules.IsEligible(valid with { ExactCanonicalIdentity = false }), "non-canonical identity");
        False(AutoEnemyFocusMarkRules.IsEligible(valid with { Targetable = false }), "not targetable");
        False(AutoEnemyFocusMarkRules.IsEligible(valid with { CurrentHp = 0 }), "dead HP");
        False(AutoEnemyFocusMarkRules.IsEligible(valid with { MaximumHp = 0 }), "invalid HP maximum");
        False(AutoEnemyFocusMarkRules.IsEligible(valid with { EnemySlot = 0 }), "invalid enemy slot");
    }

    internal static void RankingIsDeterministic()
    {
        var hpOnly = Candidate(slot: 1, hp: 20, maxHp: 100, lowMp: false, mp: 5000, maxMp: 10000, team: 4);
        var mpOnly = Candidate(slot: 2, hp: 80, maxHp: 100, lowMp: true, mp: 100, maxMp: 10000, team: 4);
        var both = Candidate(slot: 3, hp: 49, maxHp: 100, lowMp: true, mp: 1900, maxMp: 10000);
        Equal(3, AutoEnemyFocusMarkRules.Select([hpOnly, mpOnly, both])?.EnemySlot ?? 0, "both outranks one signal");
        Equal(1, AutoEnemyFocusMarkRules.Select([mpOnly, hpOnly])?.EnemySlot ?? 0, "HP-only outranks MP-only");

        var lowerHp = both with { EnemySlot = 4, CurrentHp = 20, TeamTargetCount = 0 };
        Equal(4, AutoEnemyFocusMarkRules.Select([both, lowerHp])?.EnemySlot ?? 0, "lowest HP ratio");

        var lowerMp = both with { EnemySlot = 4, CurrentMp = 1000 };
        Equal(4, AutoEnemyFocusMarkRules.Select([both, lowerMp])?.EnemySlot ?? 0, "lowest trusted MP ratio");

        var moreTeam = both with { EnemySlot = 4, TeamTargetCount = 2 };
        Equal(4, AutoEnemyFocusMarkRules.Select([both, moreTeam])?.EnemySlot ?? 0, "highest team target count");

        var stableSlot = both with { EnemySlot = 2 };
        Equal(2, AutoEnemyFocusMarkRules.Select([both, stableSlot])?.EnemySlot ?? 0, "stable lowest slot tie break");
    }

    internal static void OwnershipChecksAreExact()
    {
        True(AutoEnemyFocusMarkRules.ShouldClearConfirmedOwnership(false, true, true), "plugin disable clears owned marker");
        True(AutoEnemyFocusMarkRules.ShouldClearConfirmedOwnership(true, false, true), "feature disable clears owned marker");
        False(AutoEnemyFocusMarkRules.ShouldClearConfirmedOwnership(false, false, false), "unowned marker never clears");
        False(AutoEnemyFocusMarkRules.ShouldClearConfirmedOwnership(true, true, true), "enabled ownership remains");

        True(AutoEnemyFocusMarkRules.CanConfirmOwnership(0x100, 10, 0x100, 11), "empty-to-exact changed time");
        False(AutoEnemyFocusMarkRules.CanConfirmOwnership(0x100, 10, 0x200, 11), "wrong target");
        False(AutoEnemyFocusMarkRules.CanConfirmOwnership(0x100, 10, 0x100, 10), "unchanged marker time");

        True(AutoEnemyFocusMarkRules.CanClearOwnedMarker(2, 0x100, 0x200, 11, 2, 0x100, 0x200, 0x100, 11), "exact ownership");
        False(AutoEnemyFocusMarkRules.CanClearOwnedMarker(2, 0x100, 0x200, 11, 3, 0x100, 0x200, 0x100, 11), "slot drift");
        False(AutoEnemyFocusMarkRules.CanClearOwnedMarker(2, 0x100, 0x200, 11, 2, 0x100, 0x201, 0x100, 11), "entity drift");
        False(AutoEnemyFocusMarkRules.CanClearOwnedMarker(2, 0x100, 0x200, 11, 2, 0x100, 0x200, 0x100, 12), "timestamp drift");
    }

    private static AutoEnemyFocusMarkCandidate Candidate(
        int slot,
        uint hp,
        uint maxHp,
        bool lowMp,
        uint mp,
        uint maxMp,
        int team = 0) =>
        new(
            slot,
            (ulong)(0x1000 + slot),
            (uint)(0x2000 + slot),
            true,
            true,
            true,
            true,
            hp,
            maxHp,
            lowMp,
            mp,
            maxMp,
            team);

    private static void True(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static void False(bool value, string message) => True(!value, message);

    private static void Equal<T>(T expected, T actual, string message)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message}: expected={expected}, actual={actual}");
    }
}
