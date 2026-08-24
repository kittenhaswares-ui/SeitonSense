using System.Numerics;

namespace SeitonSense.Core;

/// <summary>
/// Action-free geometric reach policy shared by manual Smart Tab targeting.
/// Melee jobs retain their melee-first and reviewed-gap-closer tiers. Reviewed
/// physical/magical ranged jobs use one exact job range without a melee tier.
/// Tanks, healers, classes, limited jobs, and unknown future rows fail closed.
/// </summary>
public static class SmartTargetReachRules
{
    public const float MeleeRangeYalms = 5f;
    public const float StandardRangedRangeYalms = 25f;
    public const float DancerRangeYalms = 15f;

    public static bool IsReviewedMeleeJob(uint jobId) =>
        GetReviewedGapCloserRangeYalms(jobId) > 0f;

    public static bool IsReviewedRangedJob(uint jobId) =>
        GetReviewedRangedRangeYalms(jobId) > 0f;

    public static bool IsReviewedSmartTabJob(uint jobId) =>
        IsReviewedMeleeJob(jobId) || IsReviewedRangedJob(jobId);

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

    public static float GetReviewedRangedRangeYalms(uint jobId) => jobId switch
    {
        23 => StandardRangedRangeYalms, // BRD Powerful Shot
        25 => StandardRangedRangeYalms, // BLM Fire / Blizzard
        27 => StandardRangedRangeYalms, // SMN Ruin III
        31 => StandardRangedRangeYalms, // MCH Blast Charge
        35 => StandardRangedRangeYalms, // RDM Jolt III
        38 => DancerRangeYalms,         // DNC Cascade / Fountain
        42 => StandardRangedRangeYalms, // PCT Fire in Red
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
        var rangedRange = GetReviewedRangedRangeYalms(localJobId);
        if ((gapCloserRange <= 0f) == (rangedRange <= 0f) ||
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
        if (rangedRange > 0f)
        {
            if (edgeDistance > rangedRange) return false;
            tier = SmartTargetReachTier.RangedOrOther;
            return true;
        }

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
