namespace SeitonSense.Core;

/// <summary>
/// One value-only observation of an exact native CC enemy slot for one owned
/// Smart Tab target request. Reach is caller-proven by
/// <see cref="SmartTargetReachRules"/>; no action-specific range result is
/// invented for this action-free selection path.
/// </summary>
public readonly record struct SmartTabSelectionCandidate(
    int EnemySlot,
    TargetPressureActorIdentity Actor,
    bool ExactCanonicalIdentity,
    bool IsHostile,
    bool Alive,
    bool Targetable,
    bool HasActiveGuard,
    uint CurrentHp,
    uint MaximumHp,
    SmartTargetReachTier ReachTier,
    int? FreshTeamPressureCount,
    GuardAvailability GuardAvailability,
    bool HasTrustedMp,
    uint CurrentMp,
    uint MaximumMp);

/// <summary>
/// The sole actor selected for one Smart Tab request. Integration may
/// revalidate only this tuple immediately before setting the native hard target;
/// it must never rerank or substitute another enemy after intent creation.
/// </summary>
public readonly record struct SmartTabSelectionIntent(
    int EnemySlot,
    TargetPressureActorIdentity Target)
{
    public bool IsValid =>
        EnemySlotRules.IsValidSlot(EnemySlot) &&
        Target.IsValid;
}

/// <summary>
/// Pure deterministic Smart Tab policy. Melee reach precedes reviewed gap-closer
/// reach, then exact HP ratio, positive fresh team pressure, verified Guard
/// cooldown unavailability, trusted MP ratio, and stable native S-slot.
/// </summary>
public static class SmartTabSelectionRules
{
    public static int SelectBestCandidateIndex(
        IReadOnlyList<SmartTabSelectionCandidate>? candidates,
        TargetPressureActorIdentity localPlayer)
    {
        if (!HasUnambiguousCandidateSet(candidates, localPlayer)) return -1;

        var bestIndex = -1;
        for (var index = 0; index < candidates!.Count; index++)
        {
            var candidate = candidates[index];
            if (!IsEligibleCandidate(candidate, localPlayer)) continue;
            if (bestIndex < 0 || Compare(candidate, candidates[bestIndex]) < 0)
                bestIndex = index;
        }

        return bestIndex;
    }

    public static bool TryCreateIntent(
        IReadOnlyList<SmartTabSelectionCandidate>? candidates,
        TargetPressureActorIdentity localPlayer,
        out SmartTabSelectionIntent intent)
    {
        intent = default;
        var selectedIndex = SelectBestCandidateIndex(candidates, localPlayer);
        if (selectedIndex < 0) return false;

        var selected = candidates![selectedIndex];
        intent = new SmartTabSelectionIntent(selected.EnemySlot, selected.Actor);
        return intent.IsValid;
    }

    /// <summary>
    /// Final validation of only the frozen slot and actor. Ranking drift may not
    /// replace the selected enemy; an ineligible frozen actor simply cancels.
    /// </summary>
    public static bool CanSetExactIntent(
        SmartTabSelectionIntent intent,
        SmartTabSelectionCandidate candidate,
        TargetPressureActorIdentity localPlayer) =>
        intent.IsValid &&
        candidate.EnemySlot == intent.EnemySlot &&
        candidate.Actor == intent.Target &&
        IsEligibleCandidate(candidate, localPlayer);

    public static bool HasUnambiguousCandidateSet(
        IReadOnlyList<SmartTabSelectionCandidate>? candidates,
        TargetPressureActorIdentity localPlayer)
    {
        if (candidates is null || candidates.Count == 0 || !localPlayer.IsValid)
            return false;

        var occupiedSlots = new HashSet<int>();
        var occupiedGameObjectIds = new HashSet<ulong>();
        var occupiedEntityIds = new HashSet<uint>();
        foreach (var candidate in candidates)
        {
            if (!IsStructurallyValid(candidate, localPlayer) ||
                !occupiedSlots.Add(candidate.EnemySlot) ||
                !occupiedGameObjectIds.Add(candidate.Actor.GameObjectId) ||
                !occupiedEntityIds.Add(candidate.Actor.EntityId))
            {
                return false;
            }
        }

        return true;
    }

    public static bool IsEligibleCandidate(
        SmartTabSelectionCandidate candidate,
        TargetPressureActorIdentity localPlayer) =>
        IsStructurallyValid(candidate, localPlayer) &&
        candidate.IsHostile &&
        candidate.Alive &&
        candidate.Targetable &&
        !candidate.HasActiveGuard &&
        candidate.CurrentHp > 0 &&
        candidate.MaximumHp >= candidate.CurrentHp;

    private static bool IsStructurallyValid(
        SmartTabSelectionCandidate candidate,
        TargetPressureActorIdentity localPlayer) =>
        localPlayer.IsValid &&
        candidate.Actor.IsValid &&
        !SharesEitherId(candidate.Actor, localPlayer) &&
        EnemySlotRules.IsValidSlot(candidate.EnemySlot) &&
        candidate.ExactCanonicalIdentity &&
        candidate.ReachTier is SmartTargetReachTier.Melee or SmartTargetReachTier.GapCloser &&
        candidate.FreshTeamPressureCount is null or >= 0 &&
        Enum.IsDefined(candidate.GuardAvailability) &&
        (!candidate.HasTrustedMp ||
         candidate.MaximumMp > 0 && candidate.CurrentMp <= candidate.MaximumMp);

    private static int Compare(
        SmartTabSelectionCandidate left,
        SmartTabSelectionCandidate right)
    {
        var reach = left.ReachTier.CompareTo(right.ReachTier);
        if (reach != 0) return reach;

        var health = CompareRatio(
            left.CurrentHp,
            left.MaximumHp,
            right.CurrentHp,
            right.MaximumHp);
        if (health != 0) return health;

        var pressure = ComparePressure(left.FreshTeamPressureCount, right.FreshTeamPressureCount);
        if (pressure != 0) return pressure;

        var leftGuardUnavailable = left.GuardAvailability == GuardAvailability.Unavailable;
        var rightGuardUnavailable = right.GuardAvailability == GuardAvailability.Unavailable;
        if (leftGuardUnavailable != rightGuardUnavailable)
            return leftGuardUnavailable ? -1 : 1;

        if (left.HasTrustedMp != right.HasTrustedMp)
            return left.HasTrustedMp ? -1 : 1;
        if (left.HasTrustedMp)
        {
            var mp = CompareRatio(
                left.CurrentMp,
                left.MaximumMp,
                right.CurrentMp,
                right.MaximumMp);
            if (mp != 0) return mp;
        }

        return left.EnemySlot.CompareTo(right.EnemySlot);
    }

    private static int ComparePressure(int? left, int? right)
    {
        var leftPositive = left is > 0;
        var rightPositive = right is > 0;
        if (leftPositive != rightPositive) return leftPositive ? -1 : 1;
        if (!leftPositive) return 0;

        return right!.Value.CompareTo(left!.Value);
    }

    private static int CompareRatio(
        uint leftCurrent,
        uint leftMaximum,
        uint rightCurrent,
        uint rightMaximum) =>
        ((ulong)leftCurrent * rightMaximum).CompareTo(
            (ulong)rightCurrent * leftMaximum);

    private static bool SharesEitherId(
        TargetPressureActorIdentity left,
        TargetPressureActorIdentity right) =>
        left.GameObjectId == right.GameObjectId || left.EntityId == right.EntityId;
}
