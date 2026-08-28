using SeitonSense.Core;

internal static class PvpRangeHelperSelfTests
{
    public static void EveryPvpEnabledJobHasAnExactReviewedEnvelope()
    {
        var expected = new Dictionary<uint, float>
        {
            [19] = 25f,
            [21] = 20f,
            [32] = 20f,
            [37] = 20f,
            [24] = 25f,
            [28] = 25f,
            [33] = 25f,
            [40] = 25f,
            [20] = 20f,
            [22] = 20f,
            [30] = 20f,
            [34] = 20f,
            [39] = 25f,
            [41] = 20f,
            [23] = 25f,
            [31] = 25f,
            [38] = 25f,
            [25] = 25f,
            [27] = 25f,
            [35] = 25f,
            [42] = 25f,
        };

        foreach (var pair in expected)
        {
            True(PvpRangeHelperRules.TryGetProfile(pair.Key, out var profile),
                $"job {pair.Key} has a reviewed profile");
            Equal(pair.Key, profile.JobId, $"job {pair.Key} identity");
            Equal(5f, profile.MeleeRangeYalms, $"job {pair.Key} melee range");
            Equal(pair.Value, profile.MaximumActionRangeYalms,
                $"job {pair.Key} maximum range");
        }
    }

    public static void UnknownJobsAndInvalidHitboxesFailClosed()
    {
        foreach (var jobId in new uint[] { 0, 1, 2, 8, 26, 36, 43, 99 })
        {
            False(PvpRangeHelperRules.TryGetProfile(jobId, out _),
                $"unknown or non-combat job {jobId}");
            Equal(0f, PvpRangeHelperRules.GetMaximumActionRangeYalms(jobId),
                $"unknown job {jobId} has no invented range");
        }

        False(PvpRangeHelperRules.TryGetWorldRadii(30, -0.01f, out _, out _),
            "negative hitbox");
        False(PvpRangeHelperRules.TryGetWorldRadii(30, float.NaN, out _, out _),
            "non-finite hitbox");
        False(PvpRangeHelperRules.TryGetWorldRadii(99, 0.5f, out _, out _),
            "unknown job world radii");
    }

    public static void WorldRadiiStartAtTheLocalHitboxEdge()
    {
        True(PvpRangeHelperRules.TryGetWorldRadii(30, 0.5f, out var melee, out var maximum),
            "NIN radii resolve");
        Equal(5.5f, melee, "NIN melee radius from actor center");
        Equal(20.5f, maximum, "NIN maximum radius from actor center");

        True(PvpRangeHelperRules.TryGetWorldRadii(38, 1.25f, out melee, out maximum),
            "DNC radii resolve");
        Equal(6.25f, melee, "DNC melee radius from actor center");
        Equal(26.25f, maximum, "DNC maximum radius from actor center");
    }

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
