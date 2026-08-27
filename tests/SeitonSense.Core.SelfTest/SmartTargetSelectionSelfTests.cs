using SeitonSense.Core;

internal static class SmartTargetSelectionSelfTests
{
    private static readonly TargetPressureActorIdentity LocalPlayer = new(0x100, 0x100);

    public static void ReachTierWinsBeforeEveryCombatSignal()
    {
        var candidates = new[]
        {
            Candidate(3, hp: 1, maxHp: 100, reach: SmartTargetReachTier.RangedOrOther,
                pressure: 5, guard: GuardAvailability.Unavailable, mp: 1, maxMp: 10_000),
            Candidate(2, hp: 2, maxHp: 100, reach: SmartTargetReachTier.GapCloser,
                pressure: 5, guard: GuardAvailability.Unavailable, mp: 1, maxMp: 10_000),
            Candidate(5, hp: 99, maxHp: 100, reach: SmartTargetReachTier.Melee,
                pressure: null, guard: GuardAvailability.Unknown, mpTrusted: false),
        };

        Equal(2, SmartTargetSelectionRules.SelectBestCandidateIndex(candidates, LocalPlayer),
            "melee tier wins before health and every later signal");
        Equal(1, SmartTargetSelectionRules.SelectBestCandidateIndex(candidates[..2], LocalPlayer),
            "gap-closer tier wins before ranged when melee is absent");
    }

    public static void RankingOrderIsExactAndDeterministic()
    {
        var worseSignalsLowerHp = new[]
        {
            Candidate(4, hp: 49, maxHp: 100, pressure: null,
                guard: GuardAvailability.Unknown, mpTrusted: false),
            Candidate(1, hp: 50, maxHp: 100, pressure: 5,
                guard: GuardAvailability.Unavailable, mp: 0, maxMp: 10_000),
        };
        Equal(0, Select(worseSignalsLowerHp), "exact lower HP ratio remains primary inside reach tier");

        var pressure = new[]
        {
            Candidate(1, pressure: 1),
            Candidate(2, pressure: 4),
            Candidate(3, pressure: null),
        };
        Equal(1, Select(pressure), "higher fresh positive pressure wins exact HP tie");

        var positiveVsZeroUnknown = new[]
        {
            Candidate(1, pressure: 0),
            Candidate(2, pressure: null),
            Candidate(5, pressure: 1),
        };
        Equal(2, Select(positiveVsZeroUnknown), "known positive pressure beats zero and unknown");
        Equal(0, Select(positiveVsZeroUnknown[..2]), "known zero and unknown are neutral, then slot wins");

        var guard = new[]
        {
            Candidate(1, pressure: 2, guard: GuardAvailability.Ready),
            Candidate(3, pressure: 2, guard: GuardAvailability.Unavailable),
            Candidate(2, pressure: 2, guard: GuardAvailability.Unknown),
        };
        Equal(1, Select(guard), "verified Guard cooldown unavailability wins after pressure");
        Equal(0, Select(new[] { guard[0], guard[2] }),
            "ready and unknown Guard are neutral and never synthesize down");

        var mp = new[]
        {
            Candidate(1, pressure: 2, guard: GuardAvailability.Ready,
                mp: 1, maxMp: 3),
            Candidate(4, pressure: 2, guard: GuardAvailability.Ready,
                mp: 33, maxMp: 100),
            Candidate(2, pressure: 2, guard: GuardAvailability.Ready,
                mpTrusted: false),
        };
        Equal(1, Select(mp), "trusted MP ratio comparison is exact rather than rounded");
        Equal(0, Select(new[] { mp[0], mp[2] }), "trusted MP ranks before unknown telemetry");

        var slot = new[]
        {
            Candidate(5, pressure: 0, guard: GuardAvailability.Unknown, mpTrusted: false),
            Candidate(2, pressure: null, guard: GuardAvailability.Ready, mpTrusted: false),
        };
        Equal(1, Select(slot), "stable native S-slot is the final tie break");
    }

    public static void EligibilityAndAmbiguityFailClosed()
    {
        var valid = Candidate(1);
        var ineligible = new[]
        {
            valid with { IsHostile = false },
            valid with { Alive = false },
            valid with { Targetable = false },
            valid with { CurrentHp = 0 },
            valid with { MaximumHp = 0 },
            valid with { CurrentHp = 101, MaximumHp = 100 },
            valid with { HasValidActionTarget = false },
            valid with { HasNativeRangeAndLineOfSight = false },
            valid with { CallerProvenProtectionSafe = false },
        };
        foreach (var candidate in ineligible)
        {
            False(SmartTargetSelectionRules.IsEligibleCandidate(candidate, LocalPlayer),
                "every live hostile and native action gate is required");
            Equal(-1, SmartTargetSelectionRules.SelectBestCandidateIndex([candidate], LocalPlayer),
                "an ineligible candidate cannot create a target");
        }

        var reachable = Candidate(2);
        Equal(1, SmartTargetSelectionRules.SelectBestCandidateIndex(
                [valid with { HasNativeRangeAndLineOfSight = false }, reachable],
                LocalPlayer),
            "an out-of-range exact enemy is skipped without hiding a reachable enemy");

        var duplicateSlot = new[] { Candidate(1), Candidate(1, entityId: 0x302) };
        Equal(-1, Select(duplicateSlot), "duplicate canonical slot fails closed");

        var duplicateGameObject = new[]
        {
            Candidate(1, gameObjectId: 0x401, entityId: 0x301),
            Candidate(2, gameObjectId: 0x401, entityId: 0x302),
        };
        Equal(-1, Select(duplicateGameObject), "partial game-object identity collision fails closed");

        var duplicateEntity = new[]
        {
            Candidate(1, gameObjectId: 0x401, entityId: 0x301),
            Candidate(2, gameObjectId: 0x402, entityId: 0x301),
        };
        Equal(-1, Select(duplicateEntity), "partial entity identity collision fails closed");

        var structurallyInvalid = new[]
        {
            valid with { EnemySlot = 0 },
            valid with { Actor = default },
            valid with { Actor = new TargetPressureActorIdentity(LocalPlayer.GameObjectId, 0x999) },
            valid with { ExactCanonicalIdentity = false },
            valid with { ReachTier = (SmartTargetReachTier)99 },
            valid with { FreshTeamPressureCount = -1 },
            valid with { GuardAvailability = (GuardAvailability)99 },
            valid with { HasTrustedMp = true, CurrentMp = 101, MaximumMp = 100 },
        };
        foreach (var candidate in structurallyInvalid)
        {
            Equal(-1, SmartTargetSelectionRules.SelectBestCandidateIndex([candidate], LocalPlayer),
                "invalid or unprovable candidate structure fails closed");
        }
    }

    public static void FrozenIntentNeverReranksOrChangesAction()
    {
        const uint actionId = 29_507;
        var selected = Candidate(2, hp: 20, maxHp: 100);
        var other = Candidate(3, hp: 30, maxHp: 100);
        True(SmartTargetSelectionRules.TryCreateIntent(
                actionId,
                [selected, other],
                LocalPlayer,
                out var intent),
            "one exact action and target can be frozen");
        Equal(2, intent.EnemySlot, "selected slot is frozen");
        Equal(selected.Actor, intent.Target, "selected actor is frozen");

        var selectedNowWorse = selected with { CurrentHp = 90 };
        var otherNowBetter = other with { CurrentHp = 1 };
        True(SmartTargetSelectionRules.CanUseExactIntent(
                intent,
                selectedNowWorse,
                LocalPlayer,
                actionId),
            "ranking drift does not rerun selection for the consumed action");
        False(SmartTargetSelectionRules.CanUseExactIntent(
                intent,
                otherNowBetter,
                LocalPlayer,
                actionId),
            "a newly better candidate cannot replace the frozen target");
        False(SmartTargetSelectionRules.CanUseExactIntent(
                intent,
                selectedNowWorse,
                LocalPlayer,
                actionId + 1),
            "a different action cannot reuse the frozen target");
        False(SmartTargetSelectionRules.CanUseExactIntent(
                intent,
                selectedNowWorse with { HasNativeRangeAndLineOfSight = false },
                LocalPlayer,
                actionId),
            "final native range drift cancels without an alternate");
        False(SmartTargetSelectionRules.TryCreateIntent(0, [selected], LocalPlayer, out _),
            "invalid action identity cannot freeze an intent");
    }

    private static int Select(IReadOnlyList<SmartTargetSelectionCandidate> candidates) =>
        SmartTargetSelectionRules.SelectBestCandidateIndex(candidates, LocalPlayer);

    private static SmartTargetSelectionCandidate Candidate(
        int slot,
        uint hp = 50,
        uint maxHp = 100,
        SmartTargetReachTier reach = SmartTargetReachTier.RangedOrOther,
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
        return new SmartTargetSelectionCandidate(
            slot,
            new TargetPressureActorIdentity(gameObjectId, entityId),
            ExactCanonicalIdentity: true,
            IsHostile: true,
            Alive: true,
            Targetable: true,
            hp,
            maxHp,
            reach,
            HasValidActionTarget: true,
            HasNativeRangeAndLineOfSight: true,
            pressure,
            guard,
            mpTrusted,
            mp,
            maxMp,
            CallerProvenProtectionSafe: true);
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
