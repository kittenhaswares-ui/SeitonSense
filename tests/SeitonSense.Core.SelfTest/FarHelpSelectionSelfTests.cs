using SeitonSense.Core;

internal static class FarHelpSelectionSelfTests
{
    public static void DistanceAlwaysWinsBeforeRole()
    {
        var candidates = new[]
        {
            Candidate(10, distance: 20f, jobId: 20, partySlot: 2),
            Candidate(20, distance: 8f, jobId: 24, partySlot: 3),
            Candidate(30, distance: 15f, jobId: 22, partySlot: 4),
        };

        Equal(0, FarHelpSelectionRules.SelectBestIndex(candidates), "farther melee beats nearer healer");
    }

    public static void FarthestWinsAcrossAllRoles()
    {
        var candidates = new[]
        {
            Candidate(10, distance: 8f, jobId: 24, partySlot: 2),
            Candidate(20, distance: 19f, jobId: 23, partySlot: 3),
            Candidate(30, distance: 14f, jobId: 25, partySlot: 4),
            Candidate(40, distance: 20f, jobId: 20, partySlot: 5),
        };
        Equal(3, FarHelpSelectionRules.SelectBestIndex(candidates), "globally farthest valid ally");
    }

    public static void EqualDistanceIgnoresRoleAndUsesStablePartyOrder()
    {
        var roleNeutralTie = new[]
        {
            Candidate(10, distance: 10f, jobId: 20, partySlot: 2),
            Candidate(20, distance: 10f, jobId: 23, partySlot: 3),
            Candidate(30, distance: 10f, jobId: 24, partySlot: 4),
        };
        Equal(0, FarHelpSelectionRules.SelectBestIndex(roleNeutralTie), "role never changes an exact-distance tie");

        var barelyFartherOther = new[]
        {
            Candidate(10, distance: 10.0001f, jobId: 20, partySlot: 2),
            Candidate(20, distance: 10f, jobId: 24, partySlot: 3),
        };
        Equal(0, FarHelpSelectionRules.SelectBestIndex(barelyFartherOther), "any measurable distance advantage wins regardless of role");
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
        True(
            FarHelpSelectionRules.IsEligible(valid with { Role = (FarHelpAllyRole)99 }),
            "role observations never change action eligibility");
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
            valid with { IsExactPartyMember = false },
            valid with { IsSelf = true },
            valid with { IsTargetable = false },
            valid with { HasValidActionTarget = false },
            valid with { HasRangeAndLineOfSight = false },
        };

        Equal(-1, FarHelpSelectionRules.SelectBestIndex(candidates), "every action-unsafe candidate is rejected");
        Equal(-1, FarHelpSelectionRules.SelectBestIndex(null), "missing snapshot fails closed");
        Equal(-1, FarHelpSelectionRules.SelectBestIndex([]), "empty snapshot fails closed");
    }

    public static void BacklineSafetyNeverOverridesDistance()
    {
        var exactBoundary = Candidate(
            10,
            distance: 28f,
            jobId: 20,
            partySlot: 2,
            minimumEnemyEdgeDistance: FarHelpSelectionRules.MinimumBacklineEnemyEdgeClearance);
        True(!FarHelpSelectionRules.IsBacklineSafe(exactBoundary), "exactly 10y edge clearance is rejected");

        var justSafe = Candidate(
            20,
            distance: 24f,
            jobId: 24,
            partySlot: 3,
            minimumEnemyEdgeDistance: 10.001f);
        True(FarHelpSelectionRules.IsBacklineSafe(justSafe), "10.001y edge clearance is safe");

        var shorterSafe = Candidate(
            30,
            distance: 18f,
            jobId: 23,
            partySlot: 4,
            minimumEnemyEdgeDistance: 12f);
        var candidates = new[] { exactBoundary, justSafe, shorterSafe };
        Equal(0, FarHelpSelectionRules.SelectBestIndex(candidates), "the farthest ally wins regardless of backline safety");

        Equal(
            0,
            FarHelpSelectionRules.SelectBestIndex(
                [
                    exactBoundary,
                    shorterSafe with { HasCompleteCanonicalEnemySnapshot = false },
                ]),
            "backline diagnostics never change the farthest reachable ally");

        var unknownSnapshot = new[]
        {
            Candidate(40, distance: 12f, jobId: 24, partySlot: 2) with
            {
                HasCompleteCanonicalEnemySnapshot = false,
                MinimumCanonicalEnemyEdgeDistance = float.NaN,
            },
            Candidate(50, distance: 21f, jobId: 20, partySlot: 3) with
            {
                HasCompleteCanonicalEnemySnapshot = false,
                CanonicalLiveEnemyCount = 0,
            },
        };
        Equal(1, FarHelpSelectionRules.SelectBestIndex(unknownSnapshot), "unknown snapshot falls back to farthest ally");

        True(
            !FarHelpSelectionRules.IsBacklineSafe(
                exactBoundary with { CanonicalLiveEnemyCount = 0 }),
            "missing live-enemy set fails closed");
        True(
            !FarHelpSelectionRules.IsBacklineSafe(
                exactBoundary with { CanonicalLiveEnemyCount = 6 }),
            "ambiguous oversized enemy set fails closed");
    }

    public static void CurrentPvpJobsHaveDiagnosticRoleLabels()
    {
        uint[] healers = [24, 28, 33, 40];
        uint[] rangedOrCasters = [23, 25, 27, 31, 35, 38, 42];
        uint[] other = [0, 1, 5, 6, 7, 19, 20, 21, 22, 26, 29, 30, 32, 34, 36, 37, 39, 41, 43];

        True(
            healers.All(job =>
                FarHelpSelectionRules.ClassifyPlayableJob(job) == FarHelpAllyRole.Healer),
            "all current healers");
        True(
            rangedOrCasters.All(job =>
                FarHelpSelectionRules.ClassifyPlayableJob(job) == FarHelpAllyRole.RangedOrCaster),
            "all current physical ranged and casters");
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
        ulong? gameObjectId = null,
        float minimumEnemyEdgeDistance = FarHelpSelectionRules.MinimumBacklineEnemyEdgeClearance + 1f,
        int canonicalLiveEnemyCount = FarHelpSelectionRules.MaximumCanonicalEnemyCount,
        bool hasCompleteCanonicalEnemySnapshot = true) =>
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
            HasRangeAndLineOfSight: true,
            HasCompleteCanonicalEnemySnapshot: hasCompleteCanonicalEnemySnapshot,
            CanonicalLiveEnemyCount: canonicalLiveEnemyCount,
            MinimumCanonicalEnemyEdgeDistance: minimumEnemyEdgeDistance);

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
