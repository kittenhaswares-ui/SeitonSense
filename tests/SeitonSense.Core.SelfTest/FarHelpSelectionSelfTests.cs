using SeitonSense.Core;

internal static class FarHelpSelectionSelfTests
{
    public static void PreferredTierWinsBeforeDistance()
    {
        var candidates = new[]
        {
            Candidate(10, distance: 20f, jobId: 20, partySlot: 2),
            Candidate(20, distance: 8f, jobId: 24, partySlot: 3),
            Candidate(30, distance: 15f, jobId: 22, partySlot: 4),
        };

        Equal(1, FarHelpSelectionRules.SelectBestIndex(candidates), "WHM preferred over farther melee");
    }

    public static void FarthestWinsInsideEachTier()
    {
        var preferred = new[]
        {
            Candidate(10, distance: 8f, jobId: 24, partySlot: 2),
            Candidate(20, distance: 19f, jobId: 23, partySlot: 3),
            Candidate(30, distance: 14f, jobId: 25, partySlot: 4),
            Candidate(40, distance: 20f, jobId: 20, partySlot: 5),
        };
        Equal(1, FarHelpSelectionRules.SelectBestIndex(preferred), "farthest preferred role");

        var fallback = new[]
        {
            Candidate(10, distance: 8f, jobId: 19, partySlot: 2),
            Candidate(20, distance: 19f, jobId: 20, partySlot: 3),
            Candidate(30, distance: 14f, jobId: 0, partySlot: 4),
        };
        Equal(1, FarHelpSelectionRules.SelectBestIndex(fallback), "farthest fallback role");
    }

    public static void EqualDistanceUsesStablePartyAndActorIdentity()
    {
        var candidates = new[]
        {
            Candidate(40, distance: 10f, jobId: 24, partySlot: 4, gameObjectId: 400),
            Candidate(30, distance: 10f, jobId: 24, partySlot: 3, gameObjectId: 300),
            Candidate(20, distance: 10f, jobId: 24, partySlot: 2, gameObjectId: 200),
            Candidate(10, distance: 10f, jobId: 24, partySlot: 2, gameObjectId: 100),
        };

        Equal(3, FarHelpSelectionRules.SelectBestIndex(candidates), "slot, entity, then game object identity");

        Array.Reverse(candidates);
        var reversedIndex = FarHelpSelectionRules.SelectBestIndex(candidates);
        Equal(100UL, candidates[reversedIndex].GameObjectId, "enumeration order cannot change target");
    }

    public static void ExactPartyReachabilityAndLivenessFailClosed()
    {
        var valid = Candidate(90, distance: 8f, jobId: 24, partySlot: 2);
        var candidates = new[]
        {
            valid with { GameObjectId = 0 },
            valid with { EntityId = 0xE0000000 },
            valid with { PartySlot = 0 },
            valid with { PartySlot = 9 },
            valid with { CurrentHp = 0 },
            valid with { CurrentHp = 101 },
            valid with { DistanceSquared = float.NaN },
            valid with { DistanceSquared = -1f },
            valid with { Role = (FarHelpAllyRole)99 },
            valid with { IsExactPartyMember = false },
            valid with { IsSelf = true },
            valid with { IsTargetable = false },
            valid with { HasValidActionTarget = false },
            valid with { HasRangeAndLineOfSight = false },
        };

        Equal(-1, FarHelpSelectionRules.SelectBestIndex(candidates), "every unsafe candidate is rejected");
        Equal(-1, FarHelpSelectionRules.SelectBestIndex(null), "missing snapshot fails closed");
        Equal(-1, FarHelpSelectionRules.SelectBestIndex([]), "empty snapshot fails closed");
    }

    public static void CurrentPvpJobsUseExactRoleTiers()
    {
        uint[] preferred = [23, 24, 25, 27, 28, 31, 33, 35, 38, 40, 42];
        uint[] other = [0, 1, 5, 6, 7, 19, 20, 21, 22, 26, 29, 30, 32, 34, 36, 37, 39, 41, 43];

        True(
            preferred.All(job =>
                FarHelpSelectionRules.ClassifyPlayableJob(job) ==
                FarHelpAllyRole.PreferredHealerOrRanged),
            "all current healers, physical ranged, and casters");
        True(
            other.All(job =>
                FarHelpSelectionRules.ClassifyPlayableJob(job) == FarHelpAllyRole.Other),
            "tanks, melee, classes, limited, and unknown jobs");
    }

    private static FarHelpSelectionCandidate Candidate(
        uint entityId,
        float distance,
        uint jobId,
        int partySlot,
        ulong? gameObjectId = null) =>
        new(
            gameObjectId ?? entityId,
            entityId,
            partySlot,
            CurrentHp: 100,
            MaximumHp: 100,
            distance * distance,
            FarHelpSelectionRules.ClassifyPlayableJob(jobId),
            IsExactPartyMember: true,
            IsSelf: false,
            IsTargetable: true,
            HasValidActionTarget: true,
            HasRangeAndLineOfSight: true);

    private static void True(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException($"Expected true: {label}");
    }

    private static void Equal<T>(T expected, T actual, string label)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
    }
}
