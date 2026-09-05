using SeitonSense.Core;

internal static class PaladinGuardianMacroSelfTests
{
    public static void DangerThresholdsUseExistingRiskAndFreshPressure()
    {
        var distant = Candidate(10, hp: 20, pressure: null, distance: 15f);
        Require(Select(distant) == 0, "At 20% rescue needs no known pressure.");
        Require(Select(distant with { CurrentHp = 21 }) == -1, "Above 20% needs fresh pressure outside fallback range.");
        Require(Select(distant with { CurrentHp = 40, IncomingEnemyCount = 2 }) == 0, "40% with two attackers qualifies.");
        Require(Select(distant with { CurrentHp = 41, IncomingEnemyCount = 2 }) == -1, "Two attackers cannot rescue above 40%.");
        Require(Select(distant with { CurrentHp = 50, IncomingEnemyCount = 3 }) == 0, "50% with three attackers qualifies.");
        Require(Select(distant with { CurrentHp = 51, IncomingEnemyCount = 5 }) == -1, "Above 50% is outside danger thresholds.");
        Require(Select(distant with { CurrentHp = 40, IncomingEnemyCount = 1 }) == -1, "One attacker cannot open the higher threshold.");
        Require(Select(distant with { CurrentHp = 40, IncomingEnemyCount = 6 }) == -1, "Impossible pressure cannot open rescue.");

        var pressured = distant with { CurrentHp = 40, IncomingEnemyCount = 2 };
        Require(PaladinGuardianMacroRules.SelectCandidateIndex([pressured], 1_000, 750) == 0,
            "The existing 250-ms publication boundary is inclusive.");
        foreach (var publishedAt in new long[] { 749, -1, 1_001 })
        {
            Require(PaladinGuardianMacroRules.SelectCandidateIndex([pressured], 1_000, publishedAt) == -1,
                "Old, unknown, and future pressure cannot manufacture danger.");
            Require(!PaladinGuardianMacroRules.IsEligibleCandidate(pressured, 1_000, publishedAt),
                "Frozen-target validation uses the same freshness rule.");
            Require(PaladinGuardianMacroRules.SelectCandidateIndex([distant], 1_000, publishedAt) == 0,
                "Unknown pressure never suppresses the unconditional critical route.");
        }
    }

    public static void DangerSelectionPreservesExistingRanking()
    {
        PaladinGuardianCandidate[][] cases =
        [
            [Candidate(10, 19, 0, 18f), Candidate(20, 21, 5, 2f)],
            [Candidate(10, 20, 5, 3f), Candidate(20, 10, 1, 18f)],
            [Candidate(10, 10, 1, 2f), Candidate(20, 10, 4, 18f)],
            [Candidate(10, 30, 3, 2f), Candidate(20, 40, 5, 18f)],
            [Candidate(10, 10, 4, 8f), Candidate(20, 10, 4, 4f)],
            [Candidate(10, 10, 4, 4f, slot: 3), Candidate(20, 10, 4, 4f, slot: 2)],
        ];
        foreach (var candidates in cases)
        {
            var expected = DefensiveUtilityRules.SelectGuardianCandidateIndex(candidates);
            Require(PaladinGuardianMacroRules.SelectCandidateIndex(candidates, 1_000, 1_000) == expected,
                "Manual danger selection keeps existing critical/proactive, HP, pressure, distance, and slot ranking.");
            Array.Reverse(candidates);
            Require(PaladinGuardianMacroRules.SelectCandidateIndex(candidates, 1_000, 1_000) ==
                    DefensiveUtilityRules.SelectGuardianCandidateIndex(candidates),
                "Input order cannot replace existing deterministic danger ties.");
        }
    }

    public static void DangerUsesFullNativeRangeBeforeNearbyFallback()
    {
        var close = Candidate(10, hp: 100, distance: 1f);
        var far = Candidate(20, hp: 20, distance: 21f);
        Require(PaladinGuardianMacroRules.SelectCandidateIndex([close, far], 1_000, -1) == 1,
            "Danger precedes the nearest ally, with native hitbox-aware reachability authoritative even above raw sheet distance.");
        Require(PaladinGuardianMacroRules.IsEligibleCandidate(far, 1_000, -1),
            "Frozen distant critical actor stays valid while native range succeeds.");
        far = far with { HasNativeRangeAndLineOfSight = false };
        Require(PaladinGuardianMacroRules.SelectCandidateIndex([close, far], 1_000, -1) == 0,
            "An unreachable endangered actor does not prevent the nearby fallback.");
        Require(!PaladinGuardianMacroRules.IsEligibleCandidate(far, 1_000, -1),
            "A frozen actor losing native range/LoS is rejected, not redirected.");
    }

    public static void FallbackSelectsClosestWithinSixYalms()
    {
        Require(PaladinGuardianMacroRules.NearbyFallbackRangeYalms == 6f, "Nearby range is exactly six yalms.");
        var edge = Candidate(10, hp: 100, distance: 6f);
        Require(Select(edge) == 0, "Healthy nearby allies are allowed at the exact inclusive boundary.");
        Require(PaladinGuardianMacroRules.IsEligibleCandidate(edge, 1_000, -1), "Final validation accepts the same boundary.");
        var outside = edge with { DistanceSquared = 36.001f };
        Require(Select(outside) == -1, "Healthy allies beyond six yalms cannot be the fallback.");
        Require(!PaladinGuardianMacroRules.IsEligibleCandidate(outside, 1_000, -1), "Final validation rejects moving beyond fallback range.");
        var nearerHealthy = Candidate(20, hp: 100, distance: 2f);
        var fartherLow = Candidate(30, hp: 21, pressure: null, distance: 3f);
        Require(PaladinGuardianMacroRules.SelectCandidateIndex([fartherLow, edge, nearerHealthy], 1_000, -1) == 2,
            "Fallback is nearest, not lowest HP or list order; no healer-role assumption exists.");
    }

    public static void InvalidTargetsNeverBecomeDangerOrFallback()
    {
        Require(PaladinGuardianMacroRules.SelectCandidateIndex(null, 1_000, 1_000) == -1, "Missing candidates yield no selection.");
        Require(PaladinGuardianMacroRules.SelectCandidateIndex([], 1_000, 1_000) == -1, "Empty candidates yield no selection.");
        foreach (var hp in new uint[] { 20, 100 })
        {
            var candidate = Candidate(10, hp, distance: 1f);
            var invalid = new[]
            {
                candidate with { GameObjectId = 0 },
                candidate with { EntityId = 0 },
                candidate with { PartySlot = 0 },
                candidate with { PartySlot = 9 },
                candidate with { IsExactPartyMember = false },
                candidate with { IsSelf = true },
                candidate with { IsAlive = false },
                candidate with { IsTargetable = false },
                candidate with { HasValidNativeTarget = false },
                candidate with { HasNativeRangeAndLineOfSight = false },
                candidate with { CurrentHp = 0 },
                candidate with { CurrentHp = 101 },
                candidate with { MaximumHp = 0 },
                candidate with { DistanceSquared = -1f },
                candidate with { DistanceSquared = float.NaN },
                candidate with { DistanceSquared = float.PositiveInfinity },
            };
            foreach (var rejected in invalid)
            {
                Require(Select(rejected) == -1, "Invalid exact targets cannot enter either selection route.");
                Require(!PaladinGuardianMacroRules.IsEligibleCandidate(rejected, 1_000, 1_000),
                    "The final frozen-target check rejects every invalid target too.");
                Require(PaladinGuardianMacroRules.SelectCandidateIndex(
                        [rejected, Candidate(20, 100, distance: 5f)], 1_000, 1_000) == 1,
                    "Discarding an invalid row preserves the original selected index.");
            }
        }
    }

    public static void FallbackTiesRemainExactAndDeterministic()
    {
        var first = Candidate(10, hp: 100, distance: 5f, slot: 3);
        var second = Candidate(20, hp: 100, distance: 5f, slot: 2);
        Require(PaladinGuardianMacroRules.SelectCandidateIndex([first, second], 1_000, -1) == 1,
            "Equal-distance fallback prefers stable party slot.");
        Require(PaladinGuardianMacroRules.SelectCandidateIndex([second, first], 1_000, -1) == 0,
            "Reordering the input does not change the exact selected actor.");
        second = second with { PartySlot = first.PartySlot };
        Require(PaladinGuardianMacroRules.SelectCandidateIndex([first, second], 1_000, -1) == 0,
            "Entity identity breaks an otherwise complete tie.");
        second = second with { EntityId = first.EntityId, GameObjectId = first.GameObjectId + 1 };
        Require(PaladinGuardianMacroRules.SelectCandidateIndex([second, first], 1_000, -1) == 1,
            "Full game-object identity is the final deterministic tie-break.");
    }

    private static int Select(PaladinGuardianCandidate candidate) =>
        PaladinGuardianMacroRules.SelectCandidateIndex([candidate], 1_000, 1_000);

    private static PaladinGuardianCandidate Candidate(
        uint entityId,
        uint hp,
        int? pressure = 0,
        float distance = 5f,
        int slot = 2) =>
        new(0x1000UL + entityId, entityId, slot, hp, 100, pressure, distance * distance,
            IsExactPartyMember: true, IsSelf: false, IsAlive: true, IsTargetable: true,
            HasValidNativeTarget: true, HasNativeRangeAndLineOfSight: true);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
