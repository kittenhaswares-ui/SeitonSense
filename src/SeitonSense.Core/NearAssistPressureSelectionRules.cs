namespace SeitonSense.Core;

public readonly record struct NearAssistPressureSelectionCandidate(
    NearAssistAllySelectionCandidate Ally,
    TargetPressureActorIdentity ExactEnemyTarget,
    int AllyTargetCount);

/// <summary>
/// Optional Near Assist refinement. Disabled mode delegates to the existing
/// selector exactly. Enabled mode considers team pressure only inside the same
/// nearest-ally window, then falls back to the existing role/distance ordering.
/// </summary>
public static class NearAssistPressureSelectionRules
{
    public static int SelectBestIndex(
        IReadOnlyList<NearAssistPressureSelectionCandidate> candidates,
        bool preferDamageRoles,
        bool followTeamPressure)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        if (candidates.Count == 0) return -1;

        if (!followTeamPressure)
        {
            var existingCandidates = new NearAssistAllySelectionCandidate[candidates.Count];
            for (var index = 0; index < candidates.Count; index++)
                existingCandidates[index] = candidates[index].Ally;

            return NearAssistSelectionRules.SelectBestIndex(
                existingCandidates,
                preferDamageRoles);
        }

        var nearestDistance = float.PositiveInfinity;
        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            if (!IsValid(candidate)) continue;
            nearestDistance = MathF.Min(
                nearestDistance,
                MathF.Sqrt(candidate.Ally.DistanceSquared));
        }

        if (!float.IsFinite(nearestDistance)) return -1;

        var maximumDistance = nearestDistance + NearAssistSelectionRules.RolePreferenceWindowYalms;
        var maximumDistanceSquared = maximumDistance * maximumDistance;
        if (!float.IsFinite(maximumDistanceSquared)) maximumDistanceSquared = float.MaxValue;

        var bestIndex = -1;
        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            if (!IsValid(candidate) || candidate.Ally.DistanceSquared > maximumDistanceSquared) continue;
            if (bestIndex < 0 || IsBetter(candidate, candidates[bestIndex], preferDamageRoles))
                bestIndex = index;
        }

        return bestIndex;
    }

    private static bool IsBetter(
        NearAssistPressureSelectionCandidate candidate,
        NearAssistPressureSelectionCandidate current,
        bool preferDamageRoles)
    {
        if (candidate.AllyTargetCount != current.AllyTargetCount)
            return candidate.AllyTargetCount > current.AllyTargetCount;

        if (preferDamageRoles && candidate.Ally.Role != current.Ally.Role)
            return candidate.Ally.Role > current.Ally.Role;

        var distance = candidate.Ally.DistanceSquared.CompareTo(current.Ally.DistanceSquared);
        if (distance != 0) return distance < 0;
        if (candidate.Ally.EntityId != current.Ally.EntityId)
            return candidate.Ally.EntityId < current.Ally.EntityId;
        if (candidate.ExactEnemyTarget.EntityId != current.ExactEnemyTarget.EntityId)
            return candidate.ExactEnemyTarget.EntityId < current.ExactEnemyTarget.EntityId;

        return candidate.ExactEnemyTarget.GameObjectId < current.ExactEnemyTarget.GameObjectId;
    }

    private static bool IsValid(NearAssistPressureSelectionCandidate candidate) =>
        candidate.Ally.EntityId is not 0 and not 0xE0000000 and not uint.MaxValue &&
        float.IsFinite(candidate.Ally.DistanceSquared) &&
        candidate.Ally.DistanceSquared >= 0f &&
        Enum.IsDefined(candidate.Ally.Role) &&
        candidate.ExactEnemyTarget.IsValid &&
        candidate.AllyTargetCount >= 0;
}
