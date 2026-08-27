using SeitonSense.Core;

internal static class SmartTabSelectionSelfTests
{
    private static readonly TargetPressureActorIdentity LocalPlayer = new(0x100, 0x100);

    public static void ReachTierPrecedesEveryCombatSignal()
    {
        var candidates = new[]
        {
            Candidate(3, hp: 1, maxHp: 100, reach: SmartTargetReachTier.GapCloser,
                pressure: 5, guard: GuardAvailability.Unavailable, mp: 1),
            Candidate(5, hp: 99, maxHp: 100, reach: SmartTargetReachTier.Melee,
                pressure: null, guard: GuardAvailability.Unknown, mpTrusted: false),
        };

        Equal(1, Select(candidates), "melee reach wins before all combat signals");
    }

    public static void RankingOrderIsExactAndDeterministic()
    {
        Equal(0, Select([
            Candidate(4, hp: 49, maxHp: 100, pressure: null,
                guard: GuardAvailability.Unknown, mpTrusted: false),
            Candidate(1, hp: 50, maxHp: 100, pressure: 5,
                guard: GuardAvailability.Unavailable, mp: 0),
        ]), "lowest exact HP ratio wins inside one reach tier");

        Equal(1, Select([
            Candidate(1, pressure: 1),
            Candidate(2, pressure: 4),
            Candidate(3, pressure: null),
        ]), "highest fresh positive pressure wins an HP tie");

        Equal(1, Select([
            Candidate(1, pressure: 2, guard: GuardAvailability.Ready),
            Candidate(3, pressure: 2, guard: GuardAvailability.Unavailable),
            Candidate(2, pressure: 2, guard: GuardAvailability.Unknown),
        ]), "verified Guard unavailability wins after pressure");

        Equal(1, Select([
            Candidate(1, pressure: 2, guard: GuardAvailability.Ready, mp: 1, maxMp: 3),
            Candidate(4, pressure: 2, guard: GuardAvailability.Ready, mp: 33, maxMp: 100),
            Candidate(2, pressure: 2, guard: GuardAvailability.Ready, mpTrusted: false),
        ]), "trusted MP ratio is exact and precedes unknown telemetry");

        Equal(1, Select([
            Candidate(5, pressure: 0, guard: GuardAvailability.Unknown, mpTrusted: false),
            Candidate(2, pressure: null, guard: GuardAvailability.Ready, mpTrusted: false),
        ]), "stable native slot is the final tie break");

        var cycleCandidates = new[]
        {
            Candidate(5, hp: 30),
            Candidate(2, hp: 10),
            Candidate(4, hp: 20),
        };
        Equal(1, SelectNext(cycleCandidates, currentActor: null),
            "no current actor starts at rank one regardless of input order");
        Equal(2, SelectNext(cycleCandidates, cycleCandidates[1].Actor),
            "an exact current rank one advances to rank two");
        Equal(0, SelectNext(cycleCandidates, cycleCandidates[2].Actor),
            "an exact current rank two advances to rank three");
        Equal(1, SelectNext(cycleCandidates, cycleCandidates[0].Actor),
            "the last exact ranked actor wraps to rank one");
        Equal(1, SelectNext(cycleCandidates, new TargetPressureActorIdentity(0x999, 0x999)),
            "an actor outside the exact eligible set restarts at rank one");
        Equal(0, SelectNext([cycleCandidates[0]], cycleCandidates[0].Actor),
            "one eligible actor wraps to itself");
    }

    public static void EligibilityAndAmbiguityFailClosed()
    {
        var valid = Candidate(1);
        var ineligible = new[]
        {
            valid with { ExactCanonicalIdentity = false },
            valid with { IsHostile = false },
            valid with { Alive = false },
            valid with { Targetable = false },
            valid with { HasActiveGuard = true },
            valid with { HasNativeRangeAndLineOfSight = false },
            valid with { CurrentHp = 0 },
            valid with { MaximumHp = 0 },
            valid with { CurrentHp = 101, MaximumHp = 100 },
            valid with { ReachTier = (SmartTargetReachTier)255 },
        };
        foreach (var candidate in ineligible)
        {
            False(SmartTabSelectionRules.IsEligibleCandidate(candidate, LocalPlayer),
                "every exact live hostile non-Guard melee gate is required");
            Equal(-1, Select([candidate]), "an ineligible candidate cannot be selected");
        }

        True(
            SmartTabSelectionRules.IsEligibleCandidate(
                valid with { ReachTier = SmartTargetReachTier.RangedOrOther },
                LocalPlayer),
            "a caller-proven reviewed ranged tier is eligible");

        Equal(1, Select([
            Candidate(1, hp: 1) with { HasNativeRangeAndLineOfSight = false },
            Candidate(2, hp: 50),
        ]), "a blocked rank-one enemy is skipped for the reachable candidate");

        Equal(-1, Select([
            Candidate(1),
            Candidate(1, entityId: 0x302),
        ]), "duplicate slot fails closed");
        Equal(-1, Select([
            Candidate(1, gameObjectId: 0x401, entityId: 0x301),
            Candidate(2, gameObjectId: 0x401, entityId: 0x302),
        ]), "duplicate game-object identity fails closed");
        Equal(-1, Select([
            Candidate(1, gameObjectId: 0x401, entityId: 0x301),
            Candidate(2, gameObjectId: 0x402, entityId: 0x301),
        ]), "duplicate entity identity fails closed");

        Equal(-1, SelectNext([
            Candidate(1),
            Candidate(1, entityId: 0x302),
        ], currentActor: null), "ranked cycling also fails closed on an ambiguous set");

        var eligible = Candidate(2, hp: 40);
        var currentButIneligible = Candidate(3, hp: 1) with { HasActiveGuard = true };
        Equal(0, SelectNext([eligible, currentButIneligible], currentButIneligible.Actor),
            "an exact but ineligible current actor restarts at eligible rank one");
        Equal(-1, SelectNext(
                [eligible],
                new TargetPressureActorIdentity(eligible.Actor.GameObjectId, 0x999)),
            "a partial current identity match fails closed");
    }

    public static void FrozenIntentNeverReranksOrChangesActor()
    {
        var selected = Candidate(2, hp: 20, maxHp: 100);
        var other = Candidate(3, hp: 30, maxHp: 100);
        True(SmartTabSelectionRules.TryCreateIntent(
                [selected, other],
                LocalPlayer,
                out var intent),
            "one exact target can be frozen");
        Equal(2, intent.EnemySlot, "selected slot is frozen");
        Equal(selected.Actor, intent.Target, "selected actor is frozen");

        True(SmartTabSelectionRules.CanSetExactIntent(
                intent,
                selected with { CurrentHp = 90 },
                LocalPlayer),
            "ranking drift does not invalidate the same eligible actor");
        False(SmartTabSelectionRules.CanSetExactIntent(
                intent,
                other with { CurrentHp = 1 },
                LocalPlayer),
            "a newly better actor cannot replace the frozen target");
        False(SmartTabSelectionRules.CanSetExactIntent(
                intent,
                selected with { HasActiveGuard = true },
                LocalPlayer),
            "the frozen actor becoming ineligible cancels without substitution");
        False(SmartTabSelectionRules.CanSetExactIntent(
                intent,
                selected with { HasNativeRangeAndLineOfSight = false },
                LocalPlayer),
            "the frozen actor losing native line of sight cancels without substitution");

        True(SmartTabSelectionRules.TryCreateIntent(
                [selected, other],
                LocalPlayer,
                selected.Actor,
                out var cycledIntent),
            "an exact eligible current actor can freeze the next ranked actor");
        Equal(other.Actor, cycledIntent.Target,
            "cycle intent freezes only the next exact ranked actor");
        False(SmartTabSelectionRules.CanSetExactIntent(
                cycledIntent,
                selected,
                LocalPlayer),
            "cycle intent cannot fall back to the formerly current actor");
    }

    private static int Select(IReadOnlyList<SmartTabSelectionCandidate> candidates) =>
        SmartTabSelectionRules.SelectBestCandidateIndex(candidates, LocalPlayer);

    private static int SelectNext(
        IReadOnlyList<SmartTabSelectionCandidate> candidates,
        TargetPressureActorIdentity? currentActor) =>
        SmartTabSelectionRules.SelectNextCandidateIndex(candidates, LocalPlayer, currentActor);

    private static SmartTabSelectionCandidate Candidate(
        int slot,
        uint hp = 50,
        uint maxHp = 100,
        SmartTargetReachTier reach = SmartTargetReachTier.Melee,
        int? pressure = 0,
        GuardAvailability guard = GuardAvailability.Ready,
        bool mpTrusted = true,
        uint mp = 5_000,
        uint maxMp = 10_000,
        ulong gameObjectId = 0,
        uint entityId = 0)
    {
        gameObjectId = gameObjectId == 0 ? (ulong)(0x400 + slot) : gameObjectId;
        entityId = entityId == 0 ? (uint)(0x300 + slot) : entityId;
        return new SmartTabSelectionCandidate(
            slot,
            new TargetPressureActorIdentity(gameObjectId, entityId),
            ExactCanonicalIdentity: true,
            IsHostile: true,
            Alive: true,
            Targetable: true,
            HasActiveGuard: false,
            HasNativeRangeAndLineOfSight: true,
            hp,
            maxHp,
            reach,
            pressure,
            guard,
            mpTrusted,
            mp,
            maxMp);
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
