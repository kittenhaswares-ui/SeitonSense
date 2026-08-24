using System.Numerics;
using SeitonSense.Core;

internal static class SmartTargetReachSelfTests
{
    public static void ReviewedMeleeJobsAndGapCapsAreExact()
    {
        var expected = new Dictionary<uint, float>
        {
            [20] = 20f,
            [22] = 20f,
            [30] = 20f,
            [34] = 20f,
            [39] = 15f,
            [41] = 20f,
        };

        foreach (var pair in expected)
        {
            True(SmartTargetReachRules.IsReviewedMeleeJob(pair.Key), $"job {pair.Key} is reviewed");
            Equal(pair.Value, SmartTargetReachRules.GetReviewedGapCloserRangeYalms(pair.Key),
                $"job {pair.Key} gap cap");
        }

        var expectedRanged = new Dictionary<uint, float>
        {
            [23] = 25f,
            [25] = 25f,
            [27] = 25f,
            [31] = 25f,
            [35] = 25f,
            [38] = 15f,
            [42] = 25f,
        };
        foreach (var pair in expectedRanged)
        {
            True(SmartTargetReachRules.IsReviewedRangedJob(pair.Key),
                $"job {pair.Key} is reviewed ranged");
            True(SmartTargetReachRules.IsReviewedSmartTabJob(pair.Key),
                $"job {pair.Key} is reviewed for Smart Tab");
            Equal(pair.Value, SmartTargetReachRules.GetReviewedRangedRangeYalms(pair.Key),
                $"job {pair.Key} ranged cap");
        }

        foreach (var job in new uint[] { 0, 19, 21, 24, 26, 28, 32, 33, 36, 37, 40, 99 })
        {
            False(SmartTargetReachRules.IsReviewedMeleeJob(job), $"job {job} is not reviewed melee");
            False(SmartTargetReachRules.IsReviewedRangedJob(job), $"job {job} is not reviewed ranged");
            False(SmartTargetReachRules.IsReviewedSmartTabJob(job), $"job {job} is unsupported");
            Equal(0f, SmartTargetReachRules.GetReviewedGapCloserRangeYalms(job),
                $"job {job} has no invented gap cap");
            Equal(0f, SmartTargetReachRules.GetReviewedRangedRangeYalms(job),
                $"job {job} has no invented ranged cap");
        }
    }

    public static void HitboxEdgeBoundariesProduceOnlyMeleeOrGapTiers()
    {
        True(Try(job: 30, enemyCenterX: 6f, out var melee), "exact 5-yalm edge is melee");
        Equal(SmartTargetReachTier.Melee, melee, "melee boundary tier");

        True(Try(job: 30, enemyCenterX: 6.001f, out var gap), "just outside melee is gap reach");
        Equal(SmartTargetReachTier.GapCloser, gap, "gap tier immediately follows melee");

        True(Try(job: 30, enemyCenterX: 21f, out var ninjaCap), "exact NIN 20-yalm edge is reachable");
        Equal(SmartTargetReachTier.GapCloser, ninjaCap, "NIN cap tier");
        False(Try(job: 30, enemyCenterX: 21.001f, out _), "past NIN cap is rejected");

        True(Try(job: 39, enemyCenterX: 16f, out var reaperCap), "exact RPR 15-yalm edge is reachable");
        Equal(SmartTargetReachTier.GapCloser, reaperCap, "RPR cap tier");
        False(Try(job: 39, enemyCenterX: 16.001f, out _), "past RPR cap is rejected");

        True(Try(job: 23, enemyCenterX: 26f, out var bardCap),
            "exact BRD 25-yalm edge is reachable");
        Equal(SmartTargetReachTier.RangedOrOther, bardCap, "BRD has one ranged tier");
        False(Try(job: 23, enemyCenterX: 26.001f, out _), "past BRD cap is rejected");

        True(Try(job: 38, enemyCenterX: 16f, out var dancerCap),
            "exact DNC 15-yalm edge is reachable");
        Equal(SmartTargetReachTier.RangedOrOther, dancerCap, "DNC has one ranged tier");
        False(Try(job: 38, enemyCenterX: 16.001f, out _), "past DNC cap is rejected");
    }

    public static void UnknownJobsAndInvalidGeometryFailClosed()
    {
        False(Try(job: 24, enemyCenterX: 2f, out _), "unsupported healer is rejected even nearby");
        False(SmartTargetReachRules.TryResolveReachTier(
                30,
                new Vector3(float.NaN, 0f, 0f),
                0.5f,
                Vector3.Zero,
                0.5f,
                out _),
            "non-finite position fails closed");
        False(SmartTargetReachRules.TryResolveReachTier(
                30,
                Vector3.Zero,
                -0.01f,
                Vector3.Zero,
                0.5f,
                out _),
            "negative hitbox fails closed");
    }

    private static bool Try(uint job, float enemyCenterX, out SmartTargetReachTier tier) =>
        SmartTargetReachRules.TryResolveReachTier(
            job,
            Vector3.Zero,
            0.5f,
            new Vector3(enemyCenterX, 0f, 0f),
            0.5f,
            out tier);

    private static void True(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"Expected true: {message}");
    }

    private static void False(bool condition, string message) => True(!condition, message);

    private static void Equal<T>(T expected, T actual, string message)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
    }
}
