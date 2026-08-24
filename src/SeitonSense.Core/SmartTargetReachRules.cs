using System.Numerics;

namespace SeitonSense.Core;

/// <summary>
/// Action-free geometric reach policy shared by manual Smart Tab targeting.
/// It certifies only the six reviewed melee-DPS jobs and never guesses a range
/// for tanks, ranged jobs, classes, limited jobs, or unknown future rows.
/// </summary>
public static class SmartTargetReachRules
{
    public const float MeleeRangeYalms = 5f;

    public static bool IsReviewedMeleeJob(uint jobId) =>
        GetReviewedGapCloserRangeYalms(jobId) > 0f;

    public static float GetReviewedGapCloserRangeYalms(uint jobId) => jobId switch
    {
        20 => 20f, // MNK Thunderclap
        22 => 20f, // DRG High Jump
        30 => 20f, // NIN Forked Raiju
        34 => 20f, // SAM Hissatsu: Soten
        39 => 15f, // RPR Hell's Ingress
        41 => 20f, // VPR Slither
        _ => 0f,
    };

    public static bool TryResolveReachTier(
        uint localJobId,
        Vector3 localPosition,
        float localHitboxRadius,
        Vector3 enemyPosition,
        float enemyHitboxRadius,
        out SmartTargetReachTier tier)
    {
        tier = SmartTargetReachTier.RangedOrOther;
        var gapCloserRange = GetReviewedGapCloserRangeYalms(localJobId);
        if (gapCloserRange <= 0f ||
            !IsFinite(localPosition) ||
            !IsFinite(enemyPosition) ||
            !float.IsFinite(localHitboxRadius) ||
            !float.IsFinite(enemyHitboxRadius) ||
            localHitboxRadius < 0f ||
            enemyHitboxRadius < 0f)
        {
            return false;
        }

        var centerDistance = Vector3.Distance(localPosition, enemyPosition);
        if (!float.IsFinite(centerDistance)) return false;

        var edgeDistance = MathF.Max(
            0f,
            centerDistance - localHitboxRadius - enemyHitboxRadius);
        if (edgeDistance <= MeleeRangeYalms)
        {
            tier = SmartTargetReachTier.Melee;
            return true;
        }

        if (edgeDistance > gapCloserRange) return false;
        tier = SmartTargetReachTier.GapCloser;
        return true;
    }

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) &&
        float.IsFinite(value.Y) &&
        float.IsFinite(value.Z);
}
