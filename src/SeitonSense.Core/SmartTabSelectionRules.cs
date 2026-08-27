namespace SeitonSense.Core;

/// <summary>
/// One value-only observation of an exact native CC enemy slot for one owned
/// Smart Tab target request. The caller proves both the reviewed geometric
/// reach tier and FFXIV's native range/line-of-sight result before selection.
/// </summary>
public readonly record struct SmartTabSelectionCandidate(
    int EnemySlot,
    TargetPressureActorIdentity Actor,
    bool ExactCanonicalIdentity,
    bool IsHostile,
    bool Alive,
    bool Targetable,
    bool HasActiveGuard,
    bool HasNativeRangeAndLineOfSight,
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
/// Pure deterministic Smart Tab policy. Reviewed melee jobs rank melee before
/// gap-closer reach; reviewed ranged jobs supply one ranged tier. Then exact HP
/// ratio, positive fresh team pressure, verified Guard cooldown unavailability,
/// trusted MP ratio, and stable native S-slot decide the target.
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

    /// <summary>
    /// Selects the first candidate in the deterministic ranking when the
    /// current actor is absent, invalid, ineligible, or not part of this exact
    /// candidate set. When the current actor exactly matches one eligible
    /// candidate, selects the following ranked candidate and wraps at the end.
    /// No cursor state is retained between requests.
    /// </summary>
    public static int SelectNextCandidateIndex(
        IReadOnlyList<SmartTabSelectionCandidate>? candidates,
        TargetPressureActorIdentity localPlayer,
        TargetPressureActorIdentity? currentActor)
    {
        if (!HasUnambiguousCandidateSet(candidates, localPlayer)) return -1;

        var rankedEligibleIndices = new List<int>(candidates!.Count);
        for (var index = 0; index < candidates.Count; index++)
        {
            if (IsEligibleCandidate(candidates[index], localPlayer))
                rankedEligibleIndices.Add(index);
        }

        if (rankedEligibleIndices.Count == 0) return -1;

        rankedEligibleIndices.Sort((leftIndex, rightIndex) =>
            Compare(candidates[leftIndex], candidates[rightIndex]));

        if (currentActor is not { IsValid: true } exactCurrentActor)
            return rankedEligibleIndices[0];

        foreach (var candidate in candidates)
        {
            if (candidate.Actor != exactCurrentActor &&
                SharesEitherId(candidate.Actor, exactCurrentActor))
            {
                return -1;
            }
        }

        for (var rank = 0; rank < rankedEligibleIndices.Count; rank++)
        {
            var index = rankedEligibleIndices[rank];
            var candidateActor = candidates[index].Actor;
            if (candidateActor != exactCurrentActor)
            {
                if (SharesEitherId(candidateActor, exactCurrentActor)) return -1;
                continue;
            }

            var nextRank = (rank + 1) % rankedEligibleIndices.Count;
            return rankedEligibleIndices[nextRank];
        }

        return rankedEligibleIndices[0];
    }

    public static bool TryCreateIntent(
        IReadOnlyList<SmartTabSelectionCandidate>? candidates,
        TargetPressureActorIdentity localPlayer,
        out SmartTabSelectionIntent intent)
        => TryCreateIntent(candidates, localPlayer, currentActor: null, out intent);

    public static bool TryCreateIntent(
        IReadOnlyList<SmartTabSelectionCandidate>? candidates,
        TargetPressureActorIdentity localPlayer,
        TargetPressureActorIdentity? currentActor,
        out SmartTabSelectionIntent intent)
    {
        intent = default;
        var selectedIndex = SelectNextCandidateIndex(candidates, localPlayer, currentActor);
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
        candidate.HasNativeRangeAndLineOfSight &&
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
        candidate.ReachTier is SmartTargetReachTier.Melee or
            SmartTargetReachTier.GapCloser or
            SmartTargetReachTier.RangedOrOther &&
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
