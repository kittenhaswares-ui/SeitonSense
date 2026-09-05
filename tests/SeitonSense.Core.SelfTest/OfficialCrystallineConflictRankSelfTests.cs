using SeitonSense.Core;

internal static class OfficialCrystallineConflictRankSelfTests
{
    // Compact fixture from the public English Lodestone page, 2026-09-05.
    // Avatar/asset URLs are irrelevant to identity and intentionally omitted.
    private const string Header = """
        <section class="cc-ranking__header">
          <h2><img src="season.png" alt="Season 21"></h2>
          <a href="/lodestone/ranking/crystallineconflict/reward/21">Rewards</a>
          <a href="/lodestone/ranking/crystallineconflict/result/20/?dcgroup=Light">Previous season</a>
        </section>
        <section class="cc-ranking__heading">
          <p class="summarytime na not_crystal_2"><span>09/04/2026</span>10:00 p.m. to 11:00 p.m. (PST)</p>
          <p class="tier na">Tier</p>
        </section>
        """;
    private const string Row = """
        <div class="ranking_set" data-href="/lodestone/character/51094995/">
          <div class="order">1</div><div class="prev_order"></div>
          <div class="face"><div class="face-wrapper"><img src="avatar.jpg" alt=""></div></div>
          <div class="name not_crystal_2"><div class="cc-ranking__result__name"><div>
            <h3>Crystalia Aguilau&#39;ra</h3>
            <span class="world"><i class="xiv-lds xiv-lds-home-world js__tooltip" data-tooltip="Home World"></i>Phantom [Chaos]</span>
          </div></div></div>
          <div class="tier"><img src="tier.png" alt="Diamond" data-tooltip="Diamond" class="js--wolvesden-tooltip"></div>
        </div>
        """;

    public static void VerifiedFullPageParsesIdentityTierAndSnapshot()
    {
        var page = OfficialCrystallineConflictRankRules.ParsePage(Header + Row);
        Require(page.Season == 21, "current header season is not confused with previous-season links");
        Require(page.SourceUpdatedText == "09/04/2026 10:00 p.m. to 11:00 p.m. (PST)", "source update text is kept without inventing UTC precision");
        Require(page.Entries.Length == 1, "one exact published entry");
        var entry = page.Entries[0];
        Require(entry.Name == "Crystalia Aguilau'ra" && entry.HomeWorld == "Phantom", "entity-decoded exact name and home world");
        Require(entry.CharacterId == 51094995 && entry.Tier == "Diamond" && entry.Position == 1,
            "character ID, published tier and ladder position come from separate exact fields");
    }

    public static void PartialPageNeverInventsSnapshotOrUnlistedRanks()
    {
        var row = Row.Replace("<div class=\"order\">1</div>", "<div class=\"order\">51</div>")
            .Replace("Diamond", "Omega");
        var page = OfficialCrystallineConflictRankRules.ParsePage(row);
        Require(page.Entries.Length == 1 && page.Entries[0].Position == 51 && page.Entries[0].Tier == "Omega", "later native HTML fragments parse");
        Require(page.Season == 0 && page.SourceUpdatedText.Length == 0, "fragment metadata must be inherited by the caller from verified first page");
        Require(OfficialCrystallineConflictRankRules.ParsePage(Header).Entries.Length == 0,
            "an empty list cannot invent Bronze or a current rank for absent players");
        Require(OfficialCrystallineConflictRankRules.ParsePage(Row.Replace("Diamond", "Ultima")).Entries[0].Tier == "Ultima",
            "newer official top tiers are supported explicitly");
    }

    public static void MalformedRowsCannotBorrowAnAdjacentIdentityOrTier()
    {
        var variants = new[]
        {
            Row.Replace("51094995", "0"), Row.Replace("51094995", "99999999999999999999"),
            Row.Replace("/lodestone/character/", "https://example.invalid/lodestone/character/"),
            Row.Replace("class=\"world\"", "class=\"worldish\""),
            Row.Replace("Phantom [Chaos]", "Phantom"), Row.Replace("Phantom [Chaos]", "Phantom [Unknown]"),
            Row.Replace("Diamond", "Unranked"), Row.Replace("<div class=\"order\">1</div>", ""),
            Row.Replace("<div class=\"order\">1</div>", "<div class=\"order\">301</div>"),
            Row.Replace("Crystalia Aguilau&#39;ra", "Crystalia&#10;Aguilau"),
            Row.Replace("alt=\"Diamond\"", "alt=\"Diamond\" alt=\"Crystal\""),
        };
        foreach (var malformed in variants)
        {
            var page = OfficialCrystallineConflictRankRules.ParsePage(malformed + Row);
            Require(page.Entries.Length == 1 && page.Entries[0].Name == "Crystalia Aguilau'ra",
                "bad row is excluded while its valid neighbor remains exact");
        }
        var incomplete = "<div class=\"ranking_set\" data-href=\"/lodestone/character/123/\"><h3>Missing Fields</h3>";
        Require(OfficialCrystallineConflictRankRules.ParsePage(incomplete + Row).Entries.Length == 1,
            "unclosed preceding row never consumes the next row's tier/world");
    }

    public static void ContradictoryRowsRemainUnknownRatherThanPickingAWinner()
    {
        Require(OfficialCrystallineConflictRankRules.ParsePage(Row + Row).Entries.Length == 1,
            "an exact duplicate is de-duplicated");
        Require(OfficialCrystallineConflictRankRules.ParsePage(Row + Row.Replace("Diamond", "Crystal")).Entries.Length == 0,
            "same character with contradictory tiers is unknown");
        Require(OfficialCrystallineConflictRankRules.ParsePage(Row + Row.Replace("51094995", "51094996")).Entries.Length == 0,
            "same name/home-world assigned different character IDs is unknown");
        Require(OfficialCrystallineConflictRankRules.ParsePage(Row + Row.Replace("Phantom [Chaos]", "Odin [Light]")).Entries.Length == 0,
            "same character ID assigned different home worlds is unknown");
    }

    public static void InputBoundsAndAmbiguousMetadataFailClosed()
    {
        Require(OfficialCrystallineConflictRankRules.ParsePage(null).Entries.Length == 0, "null input");
        Require(OfficialCrystallineConflictRankRules.ParsePage("<html>maintenance</html>").Season == 0, "maintenance page cannot become current season");
        Require(OfficialCrystallineConflictRankRules.ParsePage(new string('x', OfficialCrystallineConflictRankRules.MaximumPageCharacters + 1)).Entries.Length == 0,
            "oversized response is not parsed");
        Require(OfficialCrystallineConflictRankRules.ParsePage(string.Concat(Enumerable.Repeat(Row, 301))).Entries.Length == 0,
            "a response beyond the public leaderboard cap is not accepted");
        var duplicate = OfficialCrystallineConflictRankRules.ParsePage(Header + Header + Row);
        Require(duplicate.Season == 0 && duplicate.SourceUpdatedText.Length == 0, "contradictory full-page boundaries cannot supply trusted snapshot context");
        Require(OfficialCrystallineConflictRankRules.ParsePage(Header.Replace("Season 21", "Previous Seasons") + Row).Season == 0,
            "unknown season labels are never inferred from reward links");
    }

    public static void RegionWhitelistMapsOnlyKnownOfficialDataCenters()
    {
        foreach (var (dataCenter, expected) in new[]
        {
            ("Chaos", "Light"), ("Light", "Light"), ("Materia", "Materia"),
            ("Aether", "Primal"), ("Primal", "Primal"), ("Crystal", "Primal"), ("Dynamis", "Primal"),
            ("Elemental", "Elemental"), ("Gaia", "Elemental"), ("Mana", "Elemental"), ("Meteor", "Elemental"),
        })
            Require(OfficialCrystallineConflictRankRules.TryGetRegionForDataCenter(dataCenter, out var actual) && actual == expected &&
                OfficialCrystallineConflictRankRules.IsSupportedRegion(actual), "logical DC maps to exact published regional selector");
        Require(OfficialCrystallineConflictRankRules.TryGetRegionForDataCenter(" chaos ", out var europe) && europe == "Light", "DC lookup is whitespace/case tolerant");
        foreach (var invalid in new[] { "", "Europe", "Light&page=999", "Unknown", null })
        {
            Require(!OfficialCrystallineConflictRankRules.TryGetRegionForDataCenter(invalid, out _), "unknown DC cannot select a default region");
            Require(!OfficialCrystallineConflictRankRules.IsSupportedRegion(invalid), "URL region tokens are exact whitelisted values");
        }
    }

    public static void CombinedSnapshotRequiresOneToOneIdentityAndCharacterIds()
    {
        var entry = OfficialCrystallineConflictRankRules.ParsePage(Row).Entries[0];
        Require(OfficialCrystallineConflictRankRules.IsValidSnapshotEntries([entry]), "one valid published entry");
        Require(OfficialCrystallineConflictRankRules.IsValidSnapshotEntries([]), "empty failed-attempt cache is structurally valid, not rank evidence");
        Require(!OfficialCrystallineConflictRankRules.IsValidSnapshotEntries(null), "null cache array");
        Require(!OfficialCrystallineConflictRankRules.IsValidSnapshotEntries([entry, entry]), "service must de-duplicate cross-page copies before saving");
        Require(!OfficialCrystallineConflictRankRules.IsValidSnapshotEntries([entry, entry with { CharacterId = 123 }]), "name/world cannot map to two character IDs across pages");
        Require(!OfficialCrystallineConflictRankRules.IsValidSnapshotEntries([entry, entry with { HomeWorld = "Odin" }]), "one character cannot map to two identities across pages");
        Require(!OfficialCrystallineConflictRankRules.IsValidSnapshotEntries([entry, entry with { Name = entry.Name.ToUpperInvariant(), CharacterId = 123 }]), "identity matching is case-insensitive like prediction runtime");
        foreach (var invalid in new[]
        {
            entry with { Name = "   " }, entry with { Name = " Leading Space" },
            entry with { Name = "Bad|Identity" }, entry with { HomeWorld = "Bad\nWorld" },
            entry with { CharacterId = 0 }, entry with { Tier = "Unknown" },
            entry with { Position = 0 }, entry with { Position = 301 },
        })
            Require(!OfficialCrystallineConflictRankRules.IsValidSnapshotEntries([invalid]), "malformed cached entry is rejected");
        Require(!OfficialCrystallineConflictRankRules.IsValidSnapshotEntries(Enumerable.Repeat(entry, 381).ToArray()), "snapshot cap includes all tiers but is bounded");
    }

    public static void RefreshCadenceUsesElapsedDayAndRejectsFutureTimestamps()
    {
        var now = new DateTimeOffset(2026, 9, 5, 12, 0, 0, TimeSpan.Zero);
        Require(OfficialCrystallineConflictRankRules.CanRefresh(now, default), "first refresh has no previous attempt");
        Require(!OfficialCrystallineConflictRankRules.CanRefresh(now, now), "an attempt now cannot repeat");
        Require(!OfficialCrystallineConflictRankRules.CanRefresh(now, now.AddHours(-23.99)), "a fresh cache remains offline for the full day");
        Require(OfficialCrystallineConflictRankRules.CanRefresh(now, now.AddHours(-24)), "exactly 24 hours permits refresh");
        Require(OfficialCrystallineConflictRankRules.CanRefresh(now, now.AddDays(-7)), "stale snapshot can refresh");
        Require(!OfficialCrystallineConflictRankRules.CanRefresh(now, now.AddHours(1)), "clock reversal or future attempt does not cause repeated requests");
        Require(!OfficialCrystallineConflictRankRules.CanRefresh(default, default), "uninitialized clock cannot initiate a request");
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
