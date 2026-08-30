using System.Numerics;

namespace SeitonSense.Core;

/// <summary>
/// One exact CC enemy observation used only before an automatic Zantetsuken
/// intent is frozen. The caller proves native actor identity, current status
/// rows, geometry, and endpoint reachability; this value-only policy owns
/// deterministic target-centered AoE ranking.
/// </summary>
public readonly record struct SamuraiZantetsukenTargetCandidate(
    int EnemySlot,
    SamuraiReactiveCounterCcTarget Target,
    bool ExactCanonicalIdentity,
    bool AliveAndTargetable,
    uint CurrentHp,
    uint MaximumHp,
    int OwnSourceKuzushiCount,
    byte ShieldPercentage,
    int ExecuteBlockingProtectionCount,
    bool HasNativeRangeAndLineOfSight,
    Vector3 Position,
    float HitboxRadius);

/// <summary>
/// Ranks reachable Zantetsuken endpoints by the number of vulnerable enemy
/// hitboxes reached by its reviewed 5-yalm target-centered circle. Guard and
/// Chiten are intentionally not blocking protections. Exact Covered, Hallowed
/// Ground, and Undead Redemption rows exclude an actor from both endpoint
/// selection and useful-cluster scoring.
/// </summary>
public static class SamuraiZantetsukenTargetSelectionRules
{
    public const float EffectRangeYalms = 5f;

    public static int SelectBestEligibleTargetIndex(
        IReadOnlyList<SamuraiZantetsukenTargetCandidate>? candidates)
    {
        if (!HasUnambiguousCandidateSet(candidates)) return -1;

        var bestIndex = -1;
        var bestClusterSize = -1;
        for (var index = 0; index < candidates!.Count; index++)
        {
            var candidate = candidates[index];
            if (!IsSelectableTarget(candidate)) continue;

            var clusterSize = CountUsefulClusterMembers(candidates, index);
            if (bestIndex < 0 ||
                Compare(
                    candidate,
                    clusterSize,
                    candidates[bestIndex],
                    bestClusterSize) < 0)
            {
                bestIndex = index;
                bestClusterSize = clusterSize;
            }
        }

        return bestIndex;
    }

    public static int CountUsefulClusterMembers(
        IReadOnlyList<SamuraiZantetsukenTargetCandidate>? candidates,
        int targetIndex)
    {
        if (!HasUnambiguousCandidateSet(candidates) ||
            targetIndex < 0 ||
            targetIndex >= candidates!.Count ||
            !IsSelectableTarget(candidates[targetIndex]))
        {
            return 0;
        }

        var target = candidates[targetIndex];
        var count = 0;
        foreach (var candidate in candidates)
        {
            if (!IsUsefulClusterMember(candidate) ||
                !CircleReachesActor(target.Position, candidate))
            {
                continue;
            }

            count++;
        }

        return count;
    }

    public static bool IsSelectableTarget(
        SamuraiZantetsukenTargetCandidate candidate) =>
        IsUsefulClusterMember(candidate) &&
        candidate.HasNativeRangeAndLineOfSight;

    public static bool IsUsefulClusterMember(
        SamuraiZantetsukenTargetCandidate candidate) =>
        IsStructurallyValid(candidate) &&
        candidate.AliveAndTargetable &&
        candidate.CurrentHp > 0 &&
        candidate.ExecuteBlockingProtectionCount == 0;

    private static int Compare(
        SamuraiZantetsukenTargetCandidate left,
        int leftClusterSize,
        SamuraiZantetsukenTargetCandidate right,
        int rightClusterSize)
    {
        var cluster = rightClusterSize.CompareTo(leftClusterSize);
        if (cluster != 0) return cluster;

        var leftExecute = HasOwnUnshieldedKuzushi(left);
        var rightExecute = HasOwnUnshieldedKuzushi(right);
        if (leftExecute != rightExecute) return leftExecute ? -1 : 1;

        var health = CompareHealthRatio(left, right);
        if (health != 0) return health;

        return left.EnemySlot.CompareTo(right.EnemySlot);
    }

    private static bool HasOwnUnshieldedKuzushi(
        SamuraiZantetsukenTargetCandidate candidate) =>
        candidate.OwnSourceKuzushiCount == 1 &&
        candidate.ShieldPercentage == 0;

    private static int CompareHealthRatio(
        SamuraiZantetsukenTargetCandidate left,
        SamuraiZantetsukenTargetCandidate right) =>
        ((ulong)left.CurrentHp * right.MaximumHp).CompareTo(
            (ulong)right.CurrentHp * left.MaximumHp);

    private static bool CircleReachesActor(
        Vector3 center,
        SamuraiZantetsukenTargetCandidate candidate)
    {
        var deltaX = (double)center.X - candidate.Position.X;
        var deltaZ = (double)center.Z - candidate.Position.Z;
        var distanceSquared = (deltaX * deltaX) + (deltaZ * deltaZ);
        var hitRadius = (double)EffectRangeYalms + candidate.HitboxRadius;
        return distanceSquared <= hitRadius * hitRadius;
    }

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
        candidate.MaximumHp > 0 &&
        candidate.CurrentHp <= candidate.MaximumHp &&
        candidate.OwnSourceKuzushiCount >= 0 &&
        candidate.ExecuteBlockingProtectionCount >= 0 &&
        IsFinite(candidate.Position) &&
        float.IsFinite(candidate.HitboxRadius) &&
        candidate.HitboxRadius >= 0f;

    private static bool IsFinite(Vector3 point) =>
        float.IsFinite(point.X) &&
        float.IsFinite(point.Y) &&
        float.IsFinite(point.Z);
}
