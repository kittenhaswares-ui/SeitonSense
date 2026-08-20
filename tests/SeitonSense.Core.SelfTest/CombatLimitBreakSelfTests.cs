using SeitonSense.Core;

internal static class CombatLimitBreakSelfTests
{
    internal static void CatalogIsCompleteCurrentAndUnique()
    {
        Equal(21, CombatLimitBreakCatalog.Definitions.Length, "all PvP jobs are represented");
        Equal(
            21,
            CombatLimitBreakCatalog.Definitions.Select(static definition => definition.JobId).Distinct().Count(),
            "job IDs are unique");

        var actions = CombatLimitBreakCatalog.Definitions
            .SelectMany(static definition => definition.Actions)
            .ToArray();
        Equal(actions.Length, actions.Select(static action => action.ActionId).Distinct().Count(), "action IDs are unique");
        uint[] expectedDirectCasterDamageActions =
        [
            29_071, 29_072, 29_073, 41_433, 29_097, 41_437, 29_557, 29_131, 29_469,
            29_230, 41_500, 41_508, 29_485, 29_498, 29_499, 29_515, 29_516, 29_537,
            39_190, 39_173, 41_467, 29_415, 41_480, 41_481, 41_484, 41_485, 41_498,
            39_216, 39_217,
        ];
        var directCasterDamageActions = actions
            .Where(CombatLimitBreakCatalog.IsDirectlyAttributableDamage)
            .Select(static action => action.ActionId)
            .Order()
            .ToArray();
        True(
            expectedDirectCasterDamageActions.Order().SequenceEqual(directCasterDamageActions),
            "all 29 directly attributable damage rows are pinned");
        True(CombatLimitBreakCatalog.Definitions.All(static definition =>
            definition.Actions.Any(CombatLimitBreakCatalog.IsActivation)), "every job has an activation");

        True(
            CombatLimitBreakCatalog.TryFindByAction(41_502, out var scholar, out var seraphism) &&
            scholar.JobId == 28 &&
            scholar.IconId == 9_068 &&
            CombatLimitBreakCatalog.IsActivation(seraphism),
            "current Scholar Seraphism identity is pinned");
        True(
            CombatLimitBreakCatalog.TryFindByAction(41_498, out var redMage, out var southernCross) &&
            redMage.JobId == 35 &&
            redMage.IconId == 9_692 &&
            CombatLimitBreakCatalog.IsDirectlyAttributableDamage(southernCross),
            "current Red Mage Southern Cross identity is pinned");
        True(
            CombatLimitBreakCatalog.TryFindByAction(29_678, out var summoner, out var phoenix) &&
            CombatLimitBreakCatalog.ResolveIconId(summoner, phoenix) == 9_683,
            "Phoenix uses its own activation icon");
        True(
            CombatLimitBreakCatalog.TryFindByAction(29_675, out _, out var megaflare) &&
            megaflare.DamageAttribution == CombatLimitBreakDamageAttribution.PetOwnerRequired &&
            !CombatLimitBreakCatalog.IsDirectlyAttributableDamage(megaflare),
            "pet damage remains blocked until owner resolution is proven");
    }

    internal static void DamageDecoderIsExactAndFailClosed()
    {
        True(
            CombatLimitBreakEventRules.TryDecodeDirectDamage(3, 0, 0, 40_000, out var normal) &&
            normal == 40_000,
            "normal damage uses the low word");
        True(
            CombatLimitBreakEventRules.TryDecodeDirectDamage(5, 1, 0x40, 100, out var extended) &&
            extended == 65_636,
            "large damage includes Param3 as the high byte");
        True(
            CombatLimitBreakEventRules.TryDecodeDirectDamage(6, 0, 0, 1, out var parried) &&
            parried == 1,
            "parried damage remains real damage");

        False(CombatLimitBreakEventRules.TryDecodeDirectDamage(4, 0, 0, 10, out _), "healing is not damage");
        False(CombatLimitBreakEventRules.TryDecodeDirectDamage(3, 1, 0, 10, out _), "orphan high byte is malformed");
        True(
            CombatLimitBreakEventRules.TryDecodeDirectDamage(3, 0, 0x10, 10, out var flagged) &&
            flagged == 10,
            "undocumented Param4 bits do not suppress nonzero damage");
        False(CombatLimitBreakEventRules.TryDecodeDirectDamage(7, 0, 0, 10, out _), "invulnerable effect is not damage");
        False(CombatLimitBreakEventRules.TryDecodeDirectDamage(3, 0, 0x80, 10, out _), "source-applied damage is rejected");
        False(CombatLimitBreakEventRules.TryDecodeDirectDamage(3, 0, 0, 0, out _), "zero damage is rejected");
    }

    internal static void DurationEvidenceRequiresExactCarrierAndSource()
    {
        var dancer = CombatLimitBreakCatalog.Definitions.Single(static definition => definition.JobId == 38);
        var wrongSource = new CombatLimitBreakStatusObservation(200, 3_024, 999, 3.5f);
        False(
            CombatLimitBreakEventRules.TryResolveDuration(dancer, 100, [wrongSource], out _),
            "target status from another source is rejected");

        var exact = new CombatLimitBreakStatusObservation(200, 3_024, 100, 3.25f);
        True(
            CombatLimitBreakEventRules.TryResolveDuration(dancer, 100, [exact], out var seduced) &&
            seduced.StatusId == 3_024 &&
            seduced.RemainingMilliseconds == 3_250,
            "source-owned target duration is live and conservatively rounded");

        var paladin = CombatLimitBreakCatalog.Definitions.Single(static definition => definition.JobId == 19);
        var casterStatus = new CombatLimitBreakStatusObservation(100, 1_302, 0, 9.2f);
        True(
            CombatLimitBreakEventRules.TryResolveDuration(paladin, 100, [casterStatus], out var hallowed) &&
            hallowed.StatusId == 1_302 &&
            hallowed.RemainingMilliseconds == 9_200,
            "exact caster carrier is sufficient for a self phase");
        False(
            CombatLimitBreakEventRules.TryResolveDuration(
                paladin,
                100,
                [casterStatus with { CarrierEntityId = 101 }],
                out _),
            "caster status on another actor is rejected");
    }

    private static void True(bool value, string label)
    {
        if (!value) throw new InvalidOperationException($"Expected true: {label}");
    }

    private static void False(bool value, string label) => True(!value, label);

    private static void Equal<T>(T expected, T actual, string label)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
    }
}
