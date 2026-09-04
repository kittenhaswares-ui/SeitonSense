namespace SeitonSense.Core;

/// <summary>
/// One ordinary Smart Action candidate plus Samurai-only melee and vulnerability
/// evidence. Status counts are caller-proven exact current rows: Kuzushi and
/// Debana must belong to the local Samurai, while Stun is the exact reviewed
/// crowd-control row. The Core deliberately does not guess localized status IDs.
/// </summary>
public readonly record struct SamuraiSeitonTargetSelectionCandidate(
    SmartTargetSelectionCandidate Selection,
    float EdgeDistanceYalms,
    int OwnSourceKuzushiCount,
    int OwnSourceDebanaCount,
    int ExactStunCount);

/// <summary>
/// Pure deterministic target policy for /seitonsam. Candidates carrying one or
/// more reviewed vulnerability rows win first; a safe exact stack-count tie is
/// then resolved by nearest hitbox edge, HP ratio, pressure, Guard cooldown, MP,
/// and stable native enemy slot. Every selected endpoint must be within 5 yalms
/// and retain the ordinary Smart Action identity, reach/LoS, and protection gates.
/// </summary>
public static class SamuraiSeitonTargetSelectionRules
{
    public const float MaximumEdgeDistanceYalms = 5f;

    public static int SelectBestCandidateIndex(
        IReadOnlyList<SamuraiSeitonTargetSelectionCandidate>? candidates,
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
        IReadOnlyList<SamuraiSeitonTargetSelectionCandidate>? candidates,
        TargetPressureActorIdentity localPlayer,
        out SmartTargetSelectionIntent intent)
    {
        intent = default;
        if (resolvedActionId == 0) return false;

        var selectedIndex = SelectBestCandidateIndex(candidates, localPlayer);
        if (selectedIndex < 0) return false;

        var selected = candidates![selectedIndex].Selection;
        intent = new SmartTargetSelectionIntent(
            resolvedActionId,
            selected.EnemySlot,
            selected.Actor);
        return intent.IsValid;
    }

    /// <summary>
    /// Revalidates only the frozen action/actor tuple. Ranking evidence may
    /// naturally change after selection, but the exact actor must remain an
    /// ordinary protection-safe Smart Action target inside the strict 5y edge.
    /// Callers cancel on false and never substitute a different target.
    /// </summary>
    public static bool CanUseExactIntent(
        SmartTargetSelectionIntent intent,
        SamuraiSeitonTargetSelectionCandidate candidate,
        TargetPressureActorIdentity localPlayer,
        uint resolvedActionId) =>
        HasValidSamuraiEvidence(candidate) &&
        candidate.EdgeDistanceYalms <= MaximumEdgeDistanceYalms &&
        SmartTargetSelectionRules.CanUseExactIntent(
            intent,
            candidate.Selection,
            localPlayer,
            resolvedActionId);

    public static bool IsEligibleCandidate(
        SamuraiSeitonTargetSelectionCandidate candidate,
        TargetPressureActorIdentity localPlayer) =>
        HasValidSamuraiEvidence(candidate) &&
        candidate.EdgeDistanceYalms <= MaximumEdgeDistanceYalms &&
        SmartTargetSelectionRules.IsEligibleCandidate(
            candidate.Selection,
            localPlayer);

    private static bool HasUnambiguousCandidateSet(
        IReadOnlyList<SamuraiSeitonTargetSelectionCandidate>? candidates,
        TargetPressureActorIdentity localPlayer)
    {
        if (candidates is null || candidates.Count == 0) return false;

        var selections = new SmartTargetSelectionCandidate[candidates.Count];
        for (var index = 0; index < candidates.Count; index++)
        {
            if (!HasValidSamuraiEvidence(candidates[index])) return false;
            selections[index] = candidates[index].Selection;
        }

        return SmartTargetSelectionRules.HasUnambiguousCandidateSet(
            selections,
            localPlayer);
    }

    private static bool HasValidSamuraiEvidence(
        SamuraiSeitonTargetSelectionCandidate candidate) =>
        float.IsFinite(candidate.EdgeDistanceYalms) &&
        candidate.EdgeDistanceYalms >= 0f &&
        candidate.OwnSourceKuzushiCount is >= 0 and <= 1 &&
        candidate.OwnSourceDebanaCount is >= 0 and <= 1 &&
        candidate.ExactStunCount is >= 0 and <= 1;

    private static int Compare(
        SamuraiSeitonTargetSelectionCandidate left,
        SamuraiSeitonTargetSelectionCandidate right)
    {
        var preferredStatusCount = PreferredStatusCount(right).CompareTo(
            PreferredStatusCount(left));
        if (preferredStatusCount != 0) return preferredStatusCount;

        var distance = left.EdgeDistanceYalms.CompareTo(right.EdgeDistanceYalms);
        if (distance != 0) return distance;

        var health = CompareRatio(
            left.Selection.CurrentHp,
            left.Selection.MaximumHp,
            right.Selection.CurrentHp,
            right.Selection.MaximumHp);
        if (health != 0) return health;

        var pressure = ComparePressure(
            left.Selection.FreshTeamPressureCount,
            right.Selection.FreshTeamPressureCount);
        if (pressure != 0) return pressure;

        var leftGuardUnavailable =
            left.Selection.GuardAvailability == GuardAvailability.Unavailable;
        var rightGuardUnavailable =
            right.Selection.GuardAvailability == GuardAvailability.Unavailable;
        if (leftGuardUnavailable != rightGuardUnavailable)
            return leftGuardUnavailable ? -1 : 1;

        if (left.Selection.HasTrustedMp != right.Selection.HasTrustedMp)
            return left.Selection.HasTrustedMp ? -1 : 1;
        if (left.Selection.HasTrustedMp)
        {
            var mp = CompareRatio(
                left.Selection.CurrentMp,
                left.Selection.MaximumMp,
                right.Selection.CurrentMp,
                right.Selection.MaximumMp);
            if (mp != 0) return mp;
        }

        return left.Selection.EnemySlot.CompareTo(right.Selection.EnemySlot);
    }

    private static int PreferredStatusCount(
        SamuraiSeitonTargetSelectionCandidate candidate) =>
        candidate.OwnSourceKuzushiCount +
        candidate.OwnSourceDebanaCount +
        candidate.ExactStunCount;

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
}
