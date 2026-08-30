using System.Numerics;

namespace SeitonSense.Core;

/// <summary>
/// One Smart Action candidate plus caller-observed hitbox-edge distance. Eligibility,
/// identity, native action reach/line-of-sight, and protection remain owned by
/// <see cref="SmartTargetSelectionRules"/>; distance changes only the ranking.
/// </summary>
public readonly record struct SmartTargetFarthestCandidate(
    SmartTargetSelectionCandidate Selection,
    float EdgeDistanceYalms);

/// <summary>
/// Deterministic farthest-reachable variant used by /seitonfar. The complete
/// candidate set must remain unambiguous and every distance must be finite.
/// Eligible candidates are ranked by descending hitbox-edge distance, then stable
/// native enemy slot. One exact action/actor tuple is frozen for final reuse.
/// </summary>
public static class SmartTargetFarthestSelectionRules
{
    public static int SelectBestCandidateIndex(
        IReadOnlyList<SmartTargetFarthestCandidate>? candidates,
        TargetPressureActorIdentity localPlayer)
    {
        if (candidates is null || candidates.Count == 0) return -1;

        var selections = new SmartTargetSelectionCandidate[candidates.Count];
        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            if (!float.IsFinite(candidate.EdgeDistanceYalms) ||
                candidate.EdgeDistanceYalms < 0f)
            {
                return -1;
            }

            selections[index] = candidate.Selection;
        }

        if (!SmartTargetSelectionRules.HasUnambiguousCandidateSet(selections, localPlayer))
            return -1;

        var bestIndex = -1;
        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            if (!SmartTargetSelectionRules.IsEligibleCandidate(candidate.Selection, localPlayer))
                continue;

            if (bestIndex < 0 || Compare(candidate, candidates[bestIndex]) < 0)
                bestIndex = index;
        }

        return bestIndex;
    }

    public static bool TryCreateIntent(
        uint resolvedActionId,
        IReadOnlyList<SmartTargetFarthestCandidate>? candidates,
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

    public static bool TryMeasureEdgeDistance(
        Vector3 sourcePosition,
        float sourceHitboxRadius,
        Vector3 targetPosition,
        float targetHitboxRadius,
        out float edgeDistanceYalms)
    {
        edgeDistanceYalms = float.NaN;
        if (!IsFinite(sourcePosition) ||
            !IsFinite(targetPosition) ||
            !float.IsFinite(sourceHitboxRadius) ||
            !float.IsFinite(targetHitboxRadius) ||
            sourceHitboxRadius < 0f ||
            targetHitboxRadius < 0f)
        {
            return false;
        }

        var centerDistance = Vector3.Distance(sourcePosition, targetPosition);
        if (!float.IsFinite(centerDistance)) return false;

        edgeDistanceYalms = MathF.Max(
            0f,
            centerDistance - sourceHitboxRadius - targetHitboxRadius);
        return float.IsFinite(edgeDistanceYalms);
    }

    private static int Compare(
        SmartTargetFarthestCandidate left,
        SmartTargetFarthestCandidate right)
    {
        var distance = right.EdgeDistanceYalms.CompareTo(left.EdgeDistanceYalms);
        return distance != 0
            ? distance
            : left.Selection.EnemySlot.CompareTo(right.Selection.EnemySlot);
    }

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) &&
        float.IsFinite(value.Y) &&
        float.IsFinite(value.Z);
}
