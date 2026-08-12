using SeitonSense.Core;

internal static class NearHelpSelectionSelfTests
{
    public static void LowestExactHealthRatioWinsBeforeDistance()
    {
        var candidates = new[]
        {
            Candidate(10, currentHp: 7_000, maximumHp: 10_000, distance: 1f, partySlot: 1),
            Candidate(20, currentHp: 2_000, maximumHp: 10_000, distance: 20f, partySlot: 2),
            Candidate(30, currentHp: 5_000, maximumHp: 10_000, distance: 5f, partySlot: 3),
        };

        Equal(1, NearHelpSelectionRules.SelectBestIndex(candidates), "lowest HP ratio before proximity");
    }

    public static void HealthRatioComparisonIsExactAndOverflowSafe()
    {
        var candidates = new[]
        {
            Candidate(10, currentHp: 1, maximumHp: 3, distance: 1f, partySlot: 1),
            Candidate(20, currentHp: 33, maximumHp: 100, distance: 20f, partySlot: 2),
            Candidate(30, currentHp: uint.MaxValue - 1, maximumHp: uint.MaxValue, distance: 0f, partySlot: 3),
        };

        Equal(1, NearHelpSelectionRules.SelectBestIndex(candidates), "33/100 is lower than exact 1/3");

        candidates =
        [
            Candidate(10, uint.MaxValue - 1, uint.MaxValue, 5f, 1),
            Candidate(20, uint.MaxValue - 2, uint.MaxValue, 6f, 2),
        ];
        Equal(1, NearHelpSelectionRules.SelectBestIndex(candidates), "wide cross multiplication does not overflow");
    }

    public static void EqualHealthUsesDistanceThenStableIdentity()
    {
        var candidates = new[]
        {
            Candidate(40, 5_000, 10_000, 4f, partySlot: 4, gameObjectId: 400),
            Candidate(30, 1_000, 2_000, 3f, partySlot: 3, gameObjectId: 300),
            Candidate(20, 50, 100, 3f, partySlot: 2, gameObjectId: 200),
            Candidate(10, 1, 2, 3f, partySlot: 2, gameObjectId: 100),
        };

        Equal(3, NearHelpSelectionRules.SelectBestIndex(candidates), "distance, party slot, then entity identity");

        candidates =
        [
            Candidate(10, 1, 2, 3f, partySlot: 0, gameObjectId: 100),
            Candidate(20, 1, 2, 3f, partySlot: 5, gameObjectId: 200),
        ];
        Equal(1, NearHelpSelectionRules.SelectBestIndex(candidates), "known party slot sorts before unknown");
    }

    public static void ReachabilityAndFriendlyIdentityFailClosed()
    {
        var valid = Candidate(90, 50, 100, 8f, partySlot: 1);
        var candidates = new[]
        {
            valid with { GameObjectId = 0 },
            valid with { EntityId = 0xE0000000 },
            valid with { PartySlot = 9 },
            valid with { CurrentHp = 0 },
            valid with { CurrentHp = 101 },
            valid with { DistanceSquared = float.NaN },
            valid with { IsExactFriendly = false },
            valid with { IsSelf = true },
            valid with { HasValidActionTarget = false },
            valid with { HasRangeAndLineOfSight = false },
        };

        Equal(-1, NearHelpSelectionRules.SelectBestIndex(candidates), "every unsafe candidate is rejected");
        Equal(-1, NearHelpSelectionRules.SelectBestIndex(null), "missing candidate snapshot fails closed");
        Equal(-1, NearHelpSelectionRules.SelectBestIndex([]), "empty candidate snapshot fails closed");
    }

    private static NearHelpSelectionCandidate Candidate(
        uint entityId,
        uint currentHp,
        uint maximumHp,
        float distance,
        int partySlot,
        ulong? gameObjectId = null) =>
        new(
            gameObjectId ?? entityId,
            entityId,
            partySlot,
            currentHp,
            maximumHp,
            distance * distance,
            IsExactFriendly: true,
            IsSelf: false,
            HasValidActionTarget: true,
            HasRangeAndLineOfSight: true);

    private static void Equal<T>(T expected, T actual, string label)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
    }
}
