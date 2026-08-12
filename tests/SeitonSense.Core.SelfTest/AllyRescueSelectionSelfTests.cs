using SeitonSense.Core;

internal static class AllyRescueSelectionSelfTests
{
    public static void TriggerStatusAllowlistIsExact()
    {
        True(AllyRescueStatusRules.IsTriggerStatus(1343), "Stun");
        True(AllyRescueStatusRules.IsTriggerStatus(1347), "Silence");
        True(AllyRescueStatusRules.IsTriggerStatus(3085), "Miracle of Nature");
        True(AllyRescueStatusRules.IsTriggerStatus(3219), "Deep Freeze");
        False(AllyRescueStatusRules.IsTriggerStatus(1344), "Heavy is excluded");
        False(AllyRescueStatusRules.IsTriggerStatus(1345), "Bind is excluded");
        False(AllyRescueStatusRules.IsTriggerStatus(0), "missing status is excluded");
    }

    public static void ExactHealthRatioWinsBeforeEveryOtherSignal()
    {
        var candidates = new[]
        {
            Candidate(10, currentHp: 1, maximumHp: 3, pressure: 5, currentMp: 0, maximumMp: 10_000, distance: 1f),
            Candidate(20, currentHp: 33, maximumHp: 100, pressure: 0, currentMp: 10_000, maximumMp: 10_000, distance: 20f),
        };

        Equal(1, AllyRescueSelectionRules.SelectBestIndex(candidates), "33/100 is exactly below 1/3");

        candidates =
        [
            Candidate(10, uint.MaxValue - 1, uint.MaxValue, 5, 0, uint.MaxValue, 1f),
            Candidate(20, uint.MaxValue - 2, uint.MaxValue, 0, uint.MaxValue, uint.MaxValue, 20f),
        ];
        Equal(1, AllyRescueSelectionRules.SelectBestIndex(candidates), "uint cross multiplication cannot overflow");
    }

    public static void PressureIsUniqueDescendingAndUnknownLast()
    {
        var enemyA = new TargetPressureActorIdentity(100, 10);
        var enemyB = new TargetPressureActorIdentity(200, 20);
        var ambiguousA = new TargetPressureActorIdentity(300, 30);
        var ambiguousB = new TargetPressureActorIdentity(300, 31);
        var pressure = AllyRescuePressureRules.CountUniqueIncomingEnemies(
        [
            enemyA,
            enemyA,
            enemyB,
            ambiguousA,
            ambiguousB,
            new TargetPressureActorIdentity(0, 0),
        ]);

        Equal(2, pressure!.Value, "exact duplicate attackers count once and ambiguous identities fail closed");
        Equal(0, AllyRescuePressureRules.CountUniqueIncomingEnemies([])!.Value, "empty trusted snapshot is known zero");
        True(AllyRescuePressureRules.CountUniqueIncomingEnemies(null) is null, "missing snapshot remains unknown");

        var candidates = new[]
        {
            Candidate(10, pressure: 1),
            Candidate(20, pressure: 3),
            Candidate(30, pressure: null),
        };
        Equal(1, AllyRescueSelectionRules.SelectBestIndex(candidates), "higher unique pressure wins after equal HP");

        candidates =
        [
            Candidate(10, pressure: null),
            Candidate(20, pressure: 0),
            Candidate(30, pressure: -1),
        ];
        Equal(1, AllyRescueSelectionRules.SelectBestIndex(candidates), "known zero pressure sorts before unknown or invalid pressure");
    }

    public static void TrustedMpRatioIsExactAndUnknownLast()
    {
        var candidates = new[]
        {
            Candidate(10, currentMp: 1, maximumMp: 3),
            Candidate(20, currentMp: 33, maximumMp: 100),
        };
        Equal(1, AllyRescueSelectionRules.SelectBestIndex(candidates), "33/100 is exactly lower MP than 1/3");

        candidates =
        [
            Candidate(10, currentMp: 10_000, maximumMp: 10_000),
            Candidate(20, currentMp: 0, maximumMp: 0, trustedMp: false),
            Candidate(30, currentMp: 20_000, maximumMp: 10_000, trustedMp: true),
        ];
        Equal(0, AllyRescueSelectionRules.SelectBestIndex(candidates), "a trusted MP ratio sorts before unknown or invalid telemetry");
    }

    public static void DistanceAndStableIdentityBreakFullTies()
    {
        var candidates = new[]
        {
            Candidate(40, distance: 2f, partySlot: 4, gameObjectId: 400),
            Candidate(30, distance: 1f, partySlot: 3, gameObjectId: 300),
            Candidate(20, distance: 1f, partySlot: 2, gameObjectId: 200),
            Candidate(10, distance: 1f, partySlot: 2, gameObjectId: 100),
        };
        Equal(3, AllyRescueSelectionRules.SelectBestIndex(candidates), "distance, party slot, entity, then game ID");

        candidates =
        [
            Candidate(10, gameObjectId: 101) with { Status = new AllyRescueStatusInstance(3219, 2) },
            Candidate(10, gameObjectId: 101) with { Status = new AllyRescueStatusInstance(1343, 9) },
        ];
        Equal(1, AllyRescueSelectionRules.SelectBestIndex(candidates), "status ID completes deterministic ordering");
    }

    public static void EligibilityFailsClosedAndSpentIntentIsExcluded()
    {
        var valid = Candidate(90);
        var invalid = new[]
        {
            valid with { GameObjectId = 0 },
            valid with { EntityId = 0xE0000000 },
            valid with { PartySlot = 0 },
            valid with { PartySlot = 9 },
            valid with { Status = new AllyRescueStatusInstance(1344, 1) },
            valid with { Status = new AllyRescueStatusInstance(1343, 0) },
            valid with { CurrentHp = 0 },
            valid with { CurrentHp = 101 },
            valid with { DistanceSquared = float.NaN },
            valid with { IsExactPartyMember = false },
            valid with { IsSelf = true },
            valid with { IsAlive = false },
            valid with { IsTargetable = false },
            valid with { HasValidActionTarget = false },
            valid with { HasNativeRangeAndLineOfSight = false },
        };

        Equal(-1, AllyRescueSelectionRules.SelectBestIndex(invalid), "unsafe candidates are all rejected");
        Equal(-1, AllyRescueSelectionRules.SelectBestIndex(null), "missing snapshot fails closed");
        Equal(-1, AllyRescueSelectionRules.SelectBestIndex([]), "empty snapshot fails closed");
        Equal(
            -1,
            AllyRescueSelectionRules.SelectBestIndex([valid], new HashSet<AllyRescueIntent> { valid.Intent }),
            "an exact spent actor and status application cannot be selected again");
    }

    private static AllyRescueSelectionCandidate Candidate(
        uint entityId,
        uint currentHp = 50,
        uint maximumHp = 100,
        int? pressure = 0,
        uint currentMp = 5_000,
        uint maximumMp = 10_000,
        float distance = 5f,
        int partySlot = 2,
        ulong? gameObjectId = null,
        bool trustedMp = true) =>
        new(
            gameObjectId ?? entityId,
            entityId,
            partySlot,
            new AllyRescueStatusInstance(AllyRescueStatusRules.StunStatusId, entityId),
            currentHp,
            maximumHp,
            pressure,
            currentMp,
            maximumMp,
            trustedMp,
            distance * distance,
            IsExactPartyMember: true,
            IsSelf: false,
            IsAlive: true,
            IsTargetable: true,
            HasValidActionTarget: true,
            HasNativeRangeAndLineOfSight: true);

    private static void True(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException($"Expected true: {label}");
    }

    private static void False(bool condition, string label) => True(!condition, label);

    private static void Equal<T>(T expected, T actual, string label)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
    }
}
