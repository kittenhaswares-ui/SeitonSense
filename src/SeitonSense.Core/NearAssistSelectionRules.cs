namespace SeitonSense.Core;

public enum NearAssistAllyRole
{
    SupportOrUnknown = 0,
    MeleeDamage = 1,
    RangedDamage = 2,
}

public readonly record struct NearAssistAllySelectionCandidate(
    uint EntityId,
    float DistanceSquared,
    NearAssistAllyRole Role);

public static class NearAssistSelectionRules
{
    // A role preference may refine a nearby cluster, but it must never pull the
    // assist across the arena away from the player who is actually beside us.
    public const float RolePreferenceWindowYalms = 8f;

    public static NearAssistAllyRole ClassifyPlayableJob(uint jobId) => jobId switch
    {
        // Physical ranged and casters.
        23 or 25 or 27 or 31 or 35 or 38 or 42 => NearAssistAllyRole.RangedDamage,
        // Melee DPS.
        20 or 22 or 30 or 34 or 39 or 41 => NearAssistAllyRole.MeleeDamage,
        // Tanks, healers, limited jobs, classes, and unknown future rows stay
        // in the non-preferred tier until they are explicitly reviewed.
        _ => NearAssistAllyRole.SupportOrUnknown,
    };

    public static int SelectBestIndex(
        IReadOnlyList<NearAssistAllySelectionCandidate> candidates,
        bool preferDamageRoles)
    {
        if (candidates.Count == 0) return -1;

        var nearestDistance = float.PositiveInfinity;
        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            if (!IsValid(candidate)) continue;
            nearestDistance = MathF.Min(nearestDistance, MathF.Sqrt(candidate.DistanceSquared));
        }

        if (!float.IsFinite(nearestDistance)) return -1;

        var maximumPreferredDistance = nearestDistance + RolePreferenceWindowYalms;
        var maximumPreferredDistanceSquared = maximumPreferredDistance * maximumPreferredDistance;
        var bestIndex = -1;
        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            if (!IsValid(candidate)) continue;
            if (preferDamageRoles && candidate.DistanceSquared > maximumPreferredDistanceSquared) continue;

            if (bestIndex < 0 || IsBetter(candidate, candidates[bestIndex], preferDamageRoles))
                bestIndex = index;
        }

        return bestIndex;
    }

    private static bool IsBetter(
        NearAssistAllySelectionCandidate candidate,
        NearAssistAllySelectionCandidate current,
        bool preferDamageRoles)
    {
        if (preferDamageRoles && candidate.Role != current.Role)
            return candidate.Role > current.Role;

        var distance = candidate.DistanceSquared.CompareTo(current.DistanceSquared);
        return distance < 0 || distance == 0 && candidate.EntityId < current.EntityId;
    }

    private static bool IsValid(NearAssistAllySelectionCandidate candidate) =>
        candidate.EntityId is not 0 and not 0xE0000000 &&
        float.IsFinite(candidate.DistanceSquared) &&
        candidate.DistanceSquared >= 0f &&
        Enum.IsDefined(candidate.Role);
}
