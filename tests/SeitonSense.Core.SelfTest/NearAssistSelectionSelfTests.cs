using SeitonSense.Core;

internal static class NearAssistSelectionSelfTests
{
    public static void NearestModeIsPredictable()
    {
        var candidates = new[]
        {
            Candidate(20, 7f, NearAssistAllyRole.RangedDamage),
            Candidate(10, 2f, NearAssistAllyRole.SupportOrUnknown),
            Candidate(30, 4f, NearAssistAllyRole.MeleeDamage),
        };

        Equal(1, NearAssistSelectionRules.SelectBestIndex(candidates, false), "nearest support");
    }

    public static void SmartModePrefersDamageInsideTheNearbyCluster()
    {
        var candidates = new[]
        {
            Candidate(10, 2f, NearAssistAllyRole.SupportOrUnknown),
            Candidate(30, 6f, NearAssistAllyRole.MeleeDamage),
            Candidate(20, 8f, NearAssistAllyRole.RangedDamage),
        };

        Equal(2, NearAssistSelectionRules.SelectBestIndex(candidates, true), "nearby ranged damage");
    }

    public static void SmartModeCannotPullAcrossTheArena()
    {
        var candidates = new[]
        {
            Candidate(10, 1f, NearAssistAllyRole.SupportOrUnknown),
            Candidate(20, 9.01f, NearAssistAllyRole.RangedDamage),
        };

        Equal(0, NearAssistSelectionRules.SelectBestIndex(candidates, true), "outside eight-yalm window");
    }

    public static void SameRoleUsesDistanceThenStableEntityId()
    {
        var candidates = new[]
        {
            Candidate(30, 5f, NearAssistAllyRole.RangedDamage),
            Candidate(20, 4f, NearAssistAllyRole.RangedDamage),
            Candidate(10, 4f, NearAssistAllyRole.RangedDamage),
        };

        Equal(2, NearAssistSelectionRules.SelectBestIndex(candidates, true), "stable entity tie-break");
    }

    public static void InvalidCandidatesFailClosed()
    {
        var candidates = new[]
        {
            Candidate(0, 1f, NearAssistAllyRole.RangedDamage),
            Candidate(10, float.NaN, NearAssistAllyRole.MeleeDamage),
            new NearAssistAllySelectionCandidate(20, -1f, NearAssistAllyRole.SupportOrUnknown),
        };

        Equal(-1, NearAssistSelectionRules.SelectBestIndex(candidates, true), "all invalid");
    }

    public static void CurrentPlayableDamageJobsAreClassifiedExactly()
    {
        var ranged = new uint[] { 23, 25, 27, 31, 35, 38, 42 };
        var melee = new uint[] { 20, 22, 30, 34, 39, 41 };
        var support = new uint[] { 0, 19, 21, 24, 28, 32, 33, 36, 37, 40, uint.MaxValue };

        True(ranged.All(job => NearAssistSelectionRules.ClassifyPlayableJob(job) == NearAssistAllyRole.RangedDamage), "ranged and casters");
        True(melee.All(job => NearAssistSelectionRules.ClassifyPlayableJob(job) == NearAssistAllyRole.MeleeDamage), "melee damage");
        True(support.All(job => NearAssistSelectionRules.ClassifyPlayableJob(job) == NearAssistAllyRole.SupportOrUnknown), "support and unknown");
    }

    private static NearAssistAllySelectionCandidate Candidate(
        uint entityId,
        float distance,
        NearAssistAllyRole role) =>
        new(entityId, distance * distance, role);

    private static void Equal<T>(T expected, T actual, string label)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
    }

    private static void True(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException($"Expected true: {label}");
    }
}
