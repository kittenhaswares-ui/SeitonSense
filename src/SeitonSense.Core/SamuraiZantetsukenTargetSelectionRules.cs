namespace SeitonSense.Core;

/// <summary>
/// One exact CC enemy observation used only before a Zantetsuken intent is
/// frozen. The caller proves native actor identity, own-source Kuzushi, shield,
/// and endpoint reachability; this value-only policy owns deterministic ranking.
/// </summary>
public readonly record struct SamuraiZantetsukenTargetCandidate(
    int EnemySlot,
    SamuraiReactiveCounterCcTarget Target,
    bool ExactCanonicalIdentity,
    bool AliveAndTargetable,
    int OwnSourceKuzushiCount,
    byte ShieldPercentage,
    bool HasNativeRangeAndLineOfSight,
    float TargetEdgeDistanceYalms);

/// <summary>
/// Ranks exact eligible Zantetsuken targets by descending finite hitbox-edge
/// distance, then stable native enemy slot. Zantetsuken's 100%-maximum-HP rule
/// applies only to the selected Kuzushi target; its surrounding damage is not
/// treated as another confirmed kill.
/// </summary>
public static class SamuraiZantetsukenTargetSelectionRules
{
    public static int SelectFarthestEligibleTargetIndex(
        IReadOnlyList<SamuraiZantetsukenTargetCandidate>? candidates)
    {
        if (!HasUnambiguousCandidateSet(candidates)) return -1;

        var bestIndex = -1;
        for (var index = 0; index < candidates!.Count; index++)
        {
            var candidate = candidates[index];
            if (!IsEligibleTarget(candidate)) continue;

            if (bestIndex < 0 ||
                candidate.TargetEdgeDistanceYalms >
                candidates[bestIndex].TargetEdgeDistanceYalms ||
                candidate.TargetEdgeDistanceYalms ==
                candidates[bestIndex].TargetEdgeDistanceYalms &&
                candidate.EnemySlot < candidates[bestIndex].EnemySlot)
            {
                bestIndex = index;
            }
        }

        return bestIndex;
    }

    public static bool IsEligibleTarget(
        SamuraiZantetsukenTargetCandidate candidate) =>
        IsStructurallyValid(candidate) &&
        candidate.AliveAndTargetable &&
        candidate.OwnSourceKuzushiCount == 1 &&
        candidate.ShieldPercentage == 0 &&
        candidate.HasNativeRangeAndLineOfSight;

    private static bool HasUnambiguousCandidateSet(
        IReadOnlyList<SamuraiZantetsukenTargetCandidate>? candidates)
    {
        if (candidates is null || candidates.Count == 0) return false;

        var occupiedSlots = new HashSet<int>();
        var occupiedGameObjectIds = new HashSet<ulong>();
        var occupiedEntityIds = new HashSet<uint>();
        foreach (var candidate in candidates)
        {
            if (!IsStructurallyValid(candidate) ||
                !occupiedSlots.Add(candidate.EnemySlot) ||
                !occupiedGameObjectIds.Add(candidate.Target.GameObjectId) ||
                !occupiedEntityIds.Add(candidate.Target.EntityId))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsStructurallyValid(
        SamuraiZantetsukenTargetCandidate candidate) =>
        EnemySlotRules.IsValidSlot(candidate.EnemySlot) &&
        candidate.Target.IsValid &&
        candidate.ExactCanonicalIdentity &&
        candidate.OwnSourceKuzushiCount >= 0 &&
        float.IsFinite(candidate.TargetEdgeDistanceYalms) &&
        candidate.TargetEdgeDistanceYalms >= 0f;
}
