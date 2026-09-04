using SeitonSense.Core;

internal static class SamuraiSeitonTargetSelectionSelfTests
{
    private static readonly TargetPressureActorIdentity LocalSamurai =
        new(0x100, 0x100);

    public static void PreferredStatusesAndSafeStackCountWinFirst()
    {
        var candidates = new[]
        {
            Candidate(1, distance: 1f),
            Candidate(2, distance: 3f) with
            {
                OwnSourceDebanaCount = 1,
            },
            Candidate(3, distance: 5f) with
            {
                OwnSourceKuzushiCount = 1,
                ExactStunCount = 1,
            },
        };

        Equal(2, Select(candidates),
            "two exact preferred rows beat one row and a nearer unmarked target");

        var equalPreference = new[]
        {
            Candidate(4, distance: 4f) with { OwnSourceKuzushiCount = 1 },
            Candidate(2, distance: 2f) with { OwnSourceDebanaCount = 1 },
            Candidate(1, distance: 3f) with { ExactStunCount = 1 },
        };
        Equal(1, Select(equalPreference),
            "Kuzushi Debana and Stun are equal evidence, then nearest edge wins");
    }

    public static void FiveYalmBoundaryAndFallbackRankingAreExact()
    {
        var boundary = new[]
        {
            Candidate(1, distance: 5.001f) with { ExactStunCount = 1 },
            Candidate(2, distance: 5f),
        };
        Equal(1, Select(boundary), "5.000y is eligible and 5.001y is not");

        var distanceBeforeHealth = new[]
        {
            Candidate(1, distance: 2f, hp: 99),
            Candidate(2, distance: 3f, hp: 1),
        };
        Equal(0, Select(distanceBeforeHealth),
            "inside the same status tier distance wins before health");

        var healthThenCombatSignals = new[]
        {
            Candidate(5, distance: 2f, hp: 40, pressure: 1,
                guard: GuardAvailability.Ready, mp: 5_000),
            Candidate(4, distance: 2f, hp: 20, pressure: 0,
                guard: GuardAvailability.Unknown, mpTrusted: false),
        };
        Equal(1, Select(healthThenCombatSignals),
            "lower exact HP ratio wins before later combat signals");

        var pressureGuardMpSlot = new[]
        {
            Candidate(5, distance: 2f, pressure: 2,
                guard: GuardAvailability.Ready, mp: 1_000),
            Candidate(4, distance: 2f, pressure: 3,
                guard: GuardAvailability.Ready, mp: 9_000),
        };
        Equal(1, Select(pressureGuardMpSlot), "higher positive pressure wins first");

        pressureGuardMpSlot[0] = pressureGuardMpSlot[0] with
        {
            Selection = pressureGuardMpSlot[0].Selection with
            {
                FreshTeamPressureCount = 3,
                GuardAvailability = GuardAvailability.Unavailable,
            },
        };
        Equal(0, Select(pressureGuardMpSlot), "Guard unavailable wins a pressure tie");

        pressureGuardMpSlot[1] = pressureGuardMpSlot[1] with
        {
            Selection = pressureGuardMpSlot[1].Selection with
            {
                GuardAvailability = GuardAvailability.Unavailable,
            },
        };
        Equal(0, Select(pressureGuardMpSlot), "lower trusted MP ratio wins next");

        pressureGuardMpSlot[1] = pressureGuardMpSlot[1] with
        {
            Selection = pressureGuardMpSlot[1].Selection with { CurrentMp = 1_000 },
        };
        Equal(1, Select(pressureGuardMpSlot), "stable lower S-slot breaks the final tie");
    }

    public static void ProtectionIdentityAndTelemetryFailClosed()
    {
        var protectedPreferred = new[]
        {
            Candidate(1, distance: 1f) with
            {
                ExactStunCount = 1,
                Selection = Candidate(1, distance: 1f).Selection with
                {
                    CallerProvenProtectionSafe = false,
                },
            },
            Candidate(2, distance: 2f),
        };
        Equal(1, Select(protectedPreferred),
            "ordinary Smart Action protection still excludes a preferred target");

        var unknownDistance = new[]
        {
            Candidate(1, distance: 1f),
            Candidate(2, distance: float.NaN),
        };
        Equal(-1, Select(unknownDistance),
            "one unknown distance makes the complete ordering unsafe");

        var duplicateStatus = new[]
        {
            Candidate(1, distance: 1f) with { OwnSourceDebanaCount = 2 },
        };
        Equal(-1, Select(duplicateStatus),
            "duplicate exact status rows fail closed instead of adding preference");

        var duplicateActor = new[]
        {
            Candidate(1, distance: 1f, gameObjectId: 0x900, entityId: 0x901),
            Candidate(2, distance: 2f, gameObjectId: 0x900, entityId: 0x902),
        };
        Equal(-1, Select(duplicateActor),
            "partial canonical actor collisions retain Smart Action fail-closed behavior");
    }

    public static void FrozenIntentRechecksActorProtectionAndFiveYalms()
    {
        const uint actionId = 29_530;
        var selected = Candidate(3, distance: 2f) with
        {
            OwnSourceDebanaCount = 1,
        };
        True(SamuraiSeitonTargetSelectionRules.TryCreateIntent(
                actionId,
                [Candidate(1, distance: 1f), selected],
                LocalSamurai,
                out var intent),
            "one exact Samurai target is frozen");
        Equal(3, intent.EnemySlot, "preferred exact target is frozen");

        True(SamuraiSeitonTargetSelectionRules.CanUseExactIntent(
                intent,
                selected,
                LocalSamurai,
                actionId),
            "same actor remains valid inside 5y");
        False(SamuraiSeitonTargetSelectionRules.CanUseExactIntent(
                intent,
                selected with { EdgeDistanceYalms = 5.001f },
                LocalSamurai,
                actionId),
            "distance drift beyond 5y cancels without reranking");
        False(SamuraiSeitonTargetSelectionRules.CanUseExactIntent(
                intent,
                Candidate(1, distance: 1f),
                LocalSamurai,
                actionId),
            "a now-better alternate cannot replace the frozen actor");
        False(SamuraiSeitonTargetSelectionRules.CanUseExactIntent(
                intent,
                selected with
                {
                    Selection = selected.Selection with
                    {
                        CallerProvenProtectionSafe = false,
                    },
                },
                LocalSamurai,
                actionId),
            "final protection drift cancels without reranking");
        False(SamuraiSeitonTargetSelectionRules.CanUseExactIntent(
                intent,
                selected,
                LocalSamurai,
                actionId + 1),
            "resolved action drift cancels the frozen intent");
    }

    private static int Select(
        IReadOnlyList<SamuraiSeitonTargetSelectionCandidate> candidates) =>
        SamuraiSeitonTargetSelectionRules.SelectBestCandidateIndex(
            candidates,
            LocalSamurai);

    private static SamuraiSeitonTargetSelectionCandidate Candidate(
        int slot,
        float distance,
        uint hp = 50,
        uint maxHp = 100,
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
        return new SamuraiSeitonTargetSelectionCandidate(
            new SmartTargetSelectionCandidate(
                slot,
                new TargetPressureActorIdentity(gameObjectId, entityId),
                ExactCanonicalIdentity: true,
                IsHostile: true,
                Alive: true,
                Targetable: true,
                hp,
                maxHp,
                SmartTargetReachTier.Melee,
                HasValidActionTarget: true,
                HasNativeRangeAndLineOfSight: true,
                pressure,
                guard,
                mpTrusted,
                mp,
                maxMp,
                CallerProvenProtectionSafe: true),
            distance,
            OwnSourceKuzushiCount: 0,
            OwnSourceDebanaCount: 0,
            ExactStunCount: 0);
    }

    private static void True(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException($"Expected true: {label}");
    }

    private static void False(bool condition, string label) => True(!condition, label);

    private static void Equal<T>(T expected, T actual, string label)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException(
                $"{label}: expected {expected}, got {actual}");
    }
}
