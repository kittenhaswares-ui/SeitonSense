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

        foreach (var job in new uint[] { 0, 19, 21, 23, 25, 27, 31, 35, 38, 42, 99 })
        {
            False(SmartTargetReachRules.IsReviewedMeleeJob(job), $"job {job} is not reviewed melee");
            Equal(0f, SmartTargetReachRules.GetReviewedGapCloserRangeYalms(job),
                $"job {job} has no invented gap cap");
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
    }

    public static void UnknownJobsAndInvalidGeometryFailClosed()
    {
        False(Try(job: 23, enemyCenterX: 2f, out _), "ranged job is rejected even inside melee distance");
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
