namespace SeitonSense.Core;

/// <summary>
/// Caller-proven spatial tier for the exact harmful action being attempted.
/// Lower values are deliberately preferred before every combat signal.
/// </summary>
public enum SmartTargetReachTier : byte
{
    Melee = 0,
    GapCloser = 1,
    RangedOrOther = 2,
}

/// <summary>
/// One value-only observation of an exact native CC enemy slot. The integration
/// layer owns actor resolution and the native action range/line-of-sight probe.
/// A null pressure count means that no fresh exact pressure value is available.
/// </summary>
public readonly record struct SmartTargetSelectionCandidate(
    int EnemySlot,
    TargetPressureActorIdentity Actor,
    bool ExactCanonicalIdentity,
    bool IsHostile,
    bool Alive,
    bool Targetable,
    uint CurrentHp,
    uint MaximumHp,
    SmartTargetReachTier ReachTier,
    bool HasValidActionTarget,
    bool HasNativeRangeAndLineOfSight,
    int? FreshTeamPressureCount,
    GuardAvailability GuardAvailability,
    bool HasTrustedMp,
    uint CurrentMp,
    uint MaximumMp);

/// <summary>
/// The sole action and actor selected for one already incoming macro action.
/// Callers consume their one-shot token before creating this value and may only
/// revalidate this exact tuple; they must never rerun selection after drift.
/// </summary>
public readonly record struct SmartTargetSelectionIntent(
    uint ResolvedActionId,
    int EnemySlot,
    TargetPressureActorIdentity Target)
{
    public bool IsValid =>
        ResolvedActionId != 0 &&
        EnemySlotRules.IsValidSlot(EnemySlot) &&
        Target.IsValid;
}

/// <summary>
/// Pure deterministic policy for Smart Target. Reach tier is primary. Inside a
/// tier, exact HP ratio wins, followed by positive fresh team pressure, verified
/// Guard cooldown unavailability, trusted MP ratio, and stable native S-slot.
/// Unknown pressure and Guard state never synthesize a favorable observation.
/// </summary>
public static class SmartTargetSelectionRules
{
    public static int SelectBestCandidateIndex(
        IReadOnlyList<SmartTargetSelectionCandidate>? candidates,
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
        uint resolvedActionId,
        IReadOnlyList<SmartTargetSelectionCandidate>? candidates,
        TargetPressureActorIdentity localPlayer,
        out SmartTargetSelectionIntent intent)
    {
        intent = default;
        if (resolvedActionId == 0) return false;

        var selectedIndex = SelectBestCandidateIndex(candidates, localPlayer);
        if (selectedIndex < 0) return false;

        var selected = candidates![selectedIndex];
        intent = new SmartTargetSelectionIntent(
            resolvedActionId,
            selected.EnemySlot,
            selected.Actor);
        return intent.IsValid;
    }

    /// <summary>
    /// Final validation for only the frozen action/target tuple. A caller must
    /// cancel on false and must not select an alternate candidate.
    /// </summary>
    public static bool CanUseExactIntent(
        SmartTargetSelectionIntent intent,
        SmartTargetSelectionCandidate candidate,
        TargetPressureActorIdentity localPlayer,
        uint resolvedActionId) =>
        intent.IsValid &&
        resolvedActionId == intent.ResolvedActionId &&
        candidate.EnemySlot == intent.EnemySlot &&
        candidate.Actor == intent.Target &&
        IsEligibleCandidate(candidate, localPlayer);

    public static bool HasUnambiguousCandidateSet(
        IReadOnlyList<SmartTargetSelectionCandidate>? candidates,
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
        SmartTargetSelectionCandidate candidate,
        TargetPressureActorIdentity localPlayer) =>
        IsStructurallyValid(candidate, localPlayer) &&
        candidate.IsHostile &&
        candidate.Alive &&
        candidate.Targetable &&
        candidate.CurrentHp > 0 &&
        candidate.MaximumHp >= candidate.CurrentHp &&
        candidate.HasValidActionTarget &&
        candidate.HasNativeRangeAndLineOfSight;

    private static bool IsStructurallyValid(
        SmartTargetSelectionCandidate candidate,
        TargetPressureActorIdentity localPlayer) =>
        localPlayer.IsValid &&
        candidate.Actor.IsValid &&
        !SharesEitherId(candidate.Actor, localPlayer) &&
        EnemySlotRules.IsValidSlot(candidate.EnemySlot) &&
        candidate.ExactCanonicalIdentity &&
        Enum.IsDefined(candidate.ReachTier) &&
        candidate.FreshTeamPressureCount is null or >= 0 &&
        Enum.IsDefined(candidate.GuardAvailability) &&
        (!candidate.HasTrustedMp ||
         candidate.MaximumMp > 0 && candidate.CurrentMp <= candidate.MaximumMp);

    private static int Compare(
        SmartTargetSelectionCandidate left,
        SmartTargetSelectionCandidate right)
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
