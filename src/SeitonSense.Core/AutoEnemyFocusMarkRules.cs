namespace SeitonSense.Core;

public readonly record struct AutoEnemyFocusMarkCandidate(
    int EnemySlot,
    ulong GameObjectId,
    uint EntityId,
    bool ExactCanonicalIdentity,
    bool Alive,
    bool Targetable,
    bool GuardUnavailable,
    uint CurrentHp,
    uint MaximumHp,
    bool LowMpActive,
    uint CurrentMp,
    uint MaximumMp,
    int TeamTargetCount)
{
    public bool LowHp =>
        MaximumHp > 0 &&
        CurrentHp > 0 &&
        CurrentHp <= MaximumHp &&
        (ulong)CurrentHp * 100UL <= (ulong)MaximumHp * 50UL;

    public bool LowMp =>
        LowMpActive &&
        MaximumMp > 0 &&
        CurrentMp <= MaximumMp;

    public bool IsValidIdentity =>
        EnemySlot is >= 1 and <= 5 &&
        GameObjectId is not 0 and not 0xE0000000UL &&
        EntityId is not 0 and not 0xE0000000u;
}

public static class AutoEnemyFocusMarkRules
{
    public static bool ShouldClearConfirmedOwnership(
        bool pluginEnabled,
        bool featureEnabled,
        bool ownershipConfirmed) =>
        ownershipConfirmed && (!pluginEnabled || !featureEnabled);

    public static bool IsEligible(AutoEnemyFocusMarkCandidate candidate) =>
        candidate.IsValidIdentity &&
        candidate.ExactCanonicalIdentity &&
        candidate.Alive &&
        candidate.Targetable &&
        candidate.GuardUnavailable &&
        candidate.CurrentHp > 0 &&
        candidate.MaximumHp > 0 &&
        candidate.CurrentHp <= candidate.MaximumHp &&
        (candidate.LowHp || candidate.LowMp);

    public static AutoEnemyFocusMarkCandidate? Select(
        IEnumerable<AutoEnemyFocusMarkCandidate> candidates)
    {
        AutoEnemyFocusMarkCandidate? best = null;
        foreach (var candidate in candidates)
        {
            if (!IsEligible(candidate)) continue;
            if (best is null || Compare(candidate, best.Value) < 0)
                best = candidate;
        }

        return best;
    }

    public static bool CanConfirmOwnership(
        ulong expectedGameObjectId,
        long markerTimeBeforeCommand,
        ulong observedGameObjectId,
        long observedMarkerTime) =>
        expectedGameObjectId is not 0 and not 0xE0000000UL &&
        observedGameObjectId == expectedGameObjectId &&
        observedMarkerTime != markerTimeBeforeCommand;

    public static bool CanClearOwnedMarker(
        int ownedEnemySlot,
        ulong ownedGameObjectId,
        uint ownedEntityId,
        long ownedMarkerTime,
        int currentEnemySlot,
        ulong currentGameObjectId,
        uint currentEntityId,
        ulong observedMarkerGameObjectId,
        long observedMarkerTime) =>
        ownedEnemySlot is >= 1 and <= 5 &&
        ownedEnemySlot == currentEnemySlot &&
        ownedGameObjectId is not 0 and not 0xE0000000UL &&
        ownedEntityId is not 0 and not 0xE0000000u &&
        ownedGameObjectId == currentGameObjectId &&
        ownedEntityId == currentEntityId &&
        observedMarkerGameObjectId == ownedGameObjectId &&
        observedMarkerTime == ownedMarkerTime;

    private static int Compare(
        AutoEnemyFocusMarkCandidate left,
        AutoEnemyFocusMarkCandidate right)
    {
        var priority = Priority(right).CompareTo(Priority(left));
        if (priority != 0) return priority;

        var hp = CompareRatio(
            left.CurrentHp,
            left.MaximumHp,
            right.CurrentHp,
            right.MaximumHp);
        if (hp != 0) return hp;

        var mp = CompareTrustedMp(left, right);
        if (mp != 0) return mp;

        var teamTargets = right.TeamTargetCount.CompareTo(left.TeamTargetCount);
        return teamTargets != 0
            ? teamTargets
            : left.EnemySlot.CompareTo(right.EnemySlot);
    }

    private static int Priority(AutoEnemyFocusMarkCandidate candidate) =>
        (candidate.LowHp, candidate.LowMp) switch
        {
            (true, true) => 3,
            (true, false) => 2,
            (false, true) => 1,
            _ => 0,
        };

    private static int CompareTrustedMp(
        AutoEnemyFocusMarkCandidate left,
        AutoEnemyFocusMarkCandidate right)
    {
        if (left.LowMp != right.LowMp) return left.LowMp ? -1 : 1;
        if (!left.LowMp) return 0;
        return CompareRatio(
            left.CurrentMp,
            left.MaximumMp,
            right.CurrentMp,
            right.MaximumMp);
    }

    private static int CompareRatio(uint leftCurrent, uint leftMaximum, uint rightCurrent, uint rightMaximum)
    {
        if (leftMaximum == 0 || rightMaximum == 0)
            return leftMaximum == rightMaximum ? 0 : leftMaximum == 0 ? 1 : -1;

        var leftScaled = (ulong)leftCurrent * rightMaximum;
        var rightScaled = (ulong)rightCurrent * leftMaximum;
        return leftScaled.CompareTo(rightScaled);
    }
}
