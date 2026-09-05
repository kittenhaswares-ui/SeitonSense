using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace SeitonSense.Core;

public sealed record OfficialCrystallineConflictRankEntry(
    string Name, string HomeWorld, ulong CharacterId, string Tier, int Position);

public sealed record OfficialCrystallineConflictRankPage(
    OfficialCrystallineConflictRankEntry[] Entries, int Season, string SourceUpdatedText)
{
    public static OfficialCrystallineConflictRankPage Empty { get; } = new([], 0, string.Empty);
}

/// <summary>
/// Reads the small, public English Lodestone CC ranking markup. This is a
/// partial leaderboard, never a census: absent or malformed players have no
/// inferred tier. HTML fragments from subsequent pages have no season/date;
/// the caller must keep their verified first-page snapshot context.
/// </summary>
public static class OfficialCrystallineConflictRankRules
{
    public const int MaximumPageCharacters = 1_000_000;
    public const int MaximumEntriesPerPage = 300;
    public const int MaximumSnapshotEntries = 380;
    public static readonly TimeSpan RefreshInterval = TimeSpan.FromHours(24);

    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(200);
    private const RegexOptions Options = RegexOptions.CultureInvariant | RegexOptions.Singleline;
    private static readonly Regex DivOpen = Pattern("<div\\b(?<attributes>[^<>]*)>");
    private static readonly Regex DivTag = Pattern("<(?<close>/)?div\\b[^<>]*>");
    private static readonly Regex Image = Pattern("<img\\b(?<attributes>[^<>]*)>");
    private static readonly Regex Heading = Pattern("<h3\\b[^<>]*>(?<body>[^<>]*)</h3\\s*>");
    private static readonly Regex Span = Pattern("<span\\b(?<attributes>[^<>]*)>(?<body>.*?)</span\\s*>");
    private static readonly Regex Paragraph = Pattern("<p\\b(?<attributes>[^<>]*)>(?<body>.*?)</p\\s*>");
    private static readonly Regex Section = Pattern("<section\\b(?<attributes>[^<>]*)>(?<body>.*?)</section\\s*>");
    private static readonly Regex Attribute = Pattern(
        "(?<name>[A-Za-z_:][A-Za-z0-9_:.-]*)\\s*=\\s*(?:\"(?<double>[^\"]*)\"|'(?<single>[^']*)')");
    private static readonly Regex Tags = Pattern("<[^<>]*>");
    private static readonly Regex Spaces = Pattern("\\s+");
    private static readonly Regex CharacterPath = Pattern("^/lodestone/character/(?<id>[0-9]{1,20})/$");
    private static readonly Regex WorldAndDc = Pattern("^(?<world>[^\\[\\]<>]+)\\s+\\[(?<dc>[^\\[\\]<>]+)\\]$");
    private static readonly Regex SeasonLabel = Pattern("^Season (?<season>[1-9][0-9]{0,3})$");

    public static bool IsSupportedRegion(string? region) =>
        region is "Elemental" or "Primal" or "Light" or "Materia";

    // Region selectors verified on the official standings page. Logical DC
    // membership is listed at /lodestone/worldstatus/; unknown names do not
    // silently fall back to another region.
    public static bool TryGetRegionForDataCenter(string? dataCenter, out string region)
    {
        region = dataCenter?.Trim().ToUpperInvariant() switch
        {
            "ELEMENTAL" or "GAIA" or "MANA" or "METEOR" => "Elemental",
            "AETHER" or "PRIMAL" or "CRYSTAL" or "DYNAMIS" => "Primal",
            "CHAOS" or "LIGHT" => "Light",
            "MATERIA" => "Materia",
            _ => string.Empty,
        };
        return region.Length != 0;
    }

    public static bool CanRefresh(DateTimeOffset now, DateTimeOffset lastAttempt) =>
        now > DateTimeOffset.UnixEpoch &&
        (lastAttempt == default || lastAttempt >= DateTimeOffset.UnixEpoch &&
            now >= lastAttempt && now - lastAttempt >= RefreshInterval);

    /// <summary>
    /// Validates a fully combined/deduplicated regional snapshot or disk cache.
    /// Empty is structurally valid for a recorded failed refresh, but is not
    /// proof of a successful official snapshot. The service checks that too.
    /// </summary>
    public static bool IsValidSnapshotEntries(IReadOnlyCollection<OfficialCrystallineConflictRankEntry>? entries)
    {
        if (entries is null || entries.Count > MaximumSnapshotEntries) return false;
        var identities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var characterIds = new HashSet<ulong>();
        foreach (var entry in entries)
        {
            if (entry is null || !IsIdentityText(entry.Name, 3, 42) ||
                !IsIdentityText(entry.HomeWorld, 1, 64) || entry.CharacterId == 0 ||
                entry.Position is < 1 or > 300 || CanonicalTier(entry.Tier).Length == 0 ||
                !identities.Add(Identity(entry)) || !characterIds.Add(entry.CharacterId))
                return false;
        }
        return true;
    }

    public static OfficialCrystallineConflictRankPage ParsePage(string? html)
    {
        if (string.IsNullOrWhiteSpace(html) || html.Length > MaximumPageCharacters)
            return OfficialCrystallineConflictRankPage.Empty;
        try
        {
            var starts = DivOpen.Matches(html).Cast<Match>()
                .Where(match => HasClass(match.Groups["attributes"].Value, "ranking_set"))
                .ToArray();
            if (starts.Length > MaximumEntriesPerPage)
                return OfficialCrystallineConflictRankPage.Empty;

            var entries = new List<OfficialCrystallineConflictRankEntry>(starts.Length);
            for (var i = 0; i < starts.Length; i++)
            {
                var limit = i + 1 < starts.Length ? starts[i + 1].Index : html.Length;
                if (TryReadDivBody(html, starts[i], limit, out var body) &&
                    TryParseEntry(starts[i].Groups["attributes"].Value, body, out var entry))
                    entries.Add(entry);
            }

            // Duplicate copies are harmless; contradictory identity/tier rows
            // are not evidence for either value and must remain unknown.
            var unambiguous = entries.Distinct().ToArray();
            var duplicateIds = unambiguous.GroupBy(entry => entry.CharacterId)
                .Where(group => group.Count() > 1).Select(group => group.Key).ToHashSet();
            var duplicateIdentities = unambiguous
                .GroupBy(Identity, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1).Select(group => group.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var exact = unambiguous.Where(entry => !duplicateIds.Contains(entry.CharacterId) &&
                !duplicateIdentities.Contains(Identity(entry))).ToArray();
            return new(exact, ReadSeason(html), ReadSourceUpdatedText(html));
        }
        catch (RegexMatchTimeoutException)
        {
            return OfficialCrystallineConflictRankPage.Empty;
        }
    }

    private static bool TryParseEntry(string attributes, string body,
        out OfficialCrystallineConflictRankEntry entry)
    {
        entry = null!;
        var character = CharacterPath.Match(ReadAttribute(attributes, "data-href"));
        if (!character.Success || !ulong.TryParse(character.Groups["id"].Value,
                NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id == 0)
            return false;
        var names = Heading.Matches(body);
        if (names.Count != 1) return false;
        var name = WebUtility.HtmlDecode(names[0].Groups["body"].Value).Trim();
        if (!IsIdentityText(name, 3, 42)) return false;

        var worldSpans = Span.Matches(body).Cast<Match>()
            .Where(match => HasClass(match.Groups["attributes"].Value, "world")).ToArray();
        if (worldSpans.Length != 1) return false;
        var worldText = PlainText(worldSpans[0].Groups["body"].Value);
        var worldMatch = WorldAndDc.Match(worldText);
        if (!worldMatch.Success ||
            !TryGetRegionForDataCenter(worldMatch.Groups["dc"].Value, out _)) return false;
        var world = worldMatch.Groups["world"].Value.Trim();
        if (!IsIdentityText(world, 1, 64)) return false;

        var tier = string.Empty;
        var position = 0;
        var tierCount = 0;
        var positionCount = 0;
        foreach (Match div in DivOpen.Matches(body))
        {
            var attrs = div.Groups["attributes"].Value;
            if (HasClass(attrs, "tier"))
            {
                tierCount++;
                if (!TryReadDivBody(body, div, body.Length, out var tierBody)) return false;
                var images = Image.Matches(tierBody);
                if (images.Count != 1) return false;
                tier = CanonicalTier(ReadAttribute(images[0].Groups["attributes"].Value, "alt"));
            }
            if (HasClass(attrs, "order"))
            {
                positionCount++;
                if (!TryReadDivBody(body, div, body.Length, out var orderBody) ||
                    !int.TryParse(PlainText(orderBody), NumberStyles.None,
                        CultureInfo.InvariantCulture, out position)) return false;
            }
        }
        if (tierCount != 1 || positionCount != 1 || tier.Length == 0 || position is < 1 or > 300)
            return false;
        entry = new(name, world, id, tier, position);
        return true;
    }

    private static int ReadSeason(string html)
    {
        var headers = Section.Matches(html).Cast<Match>()
            .Where(match => HasClass(match.Groups["attributes"].Value, "cc-ranking__header")).ToArray();
        if (headers.Length != 1) return 0;
        var seasons = Image.Matches(headers[0].Groups["body"].Value).Cast<Match>()
            .Select(match => SeasonLabel.Match(ReadAttribute(match.Groups["attributes"].Value, "alt")))
            .Where(match => match.Success).ToArray();
        return seasons.Length == 1 && int.TryParse(seasons[0].Groups["season"].Value,
            NumberStyles.None, CultureInfo.InvariantCulture, out var season) ? season : 0;
    }

    private static string ReadSourceUpdatedText(string html)
    {
        var headings = Section.Matches(html).Cast<Match>()
            .Where(match => HasClass(match.Groups["attributes"].Value, "cc-ranking__heading")).ToArray();
        if (headings.Length != 1) return string.Empty;
        var dates = Paragraph.Matches(headings[0].Groups["body"].Value).Cast<Match>()
            .Where(match => HasClass(match.Groups["attributes"].Value, "summarytime")).ToArray();
        if (dates.Length != 1) return string.Empty;
        var text = PlainText(dates[0].Groups["body"].Value);
        return IsPlainText(text, 1, 160) ? text : string.Empty;
    }

    private static bool TryReadDivBody(string html, Match opening, int limit, out string body)
    {
        body = string.Empty;
        var start = opening.Index + opening.Length;
        var depth = 1;
        for (var tag = DivTag.Match(html, start); tag.Success && tag.Index < limit; tag = tag.NextMatch())
        {
            depth += tag.Groups["close"].Success ? -1 : 1;
            if (depth != 0) continue;
            body = html[start..tag.Index];
            return true;
        }
        return false;
    }

    private static string ReadAttribute(string attributes, string name)
    {
        string? found = null;
        foreach (Match attribute in Attribute.Matches(attributes))
        {
            if (!attribute.Groups["name"].Value.Equals(name, StringComparison.OrdinalIgnoreCase)) continue;
            if (found is not null) return string.Empty; // Ambiguous duplicate attributes.
            found = WebUtility.HtmlDecode(attribute.Groups["double"].Success
                ? attribute.Groups["double"].Value : attribute.Groups["single"].Value);
        }
        return found ?? string.Empty;
    }

    private static bool HasClass(string attributes, string className) =>
        ReadAttribute(attributes, "class").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Contains(className, StringComparer.Ordinal);
    private static string PlainText(string html) =>
        Spaces.Replace(WebUtility.HtmlDecode(Tags.Replace(html, " ")), " ").Trim();
    private static bool IsPlainText(string value, int minimum, int maximum) =>
        value.Length >= minimum && value.Length <= maximum &&
        !value.Any(character => char.IsControl(character) || character is '<' or '>');
    private static bool IsIdentityText(string? value, int minimum, int maximum) =>
        value is not null && value == value.Trim() && IsPlainText(value, minimum, maximum) &&
        !value.Contains('|') && !string.IsNullOrWhiteSpace(value);
    private static string Identity(OfficialCrystallineConflictRankEntry entry) => $"{entry.HomeWorld}|{entry.Name}";
    private static string CanonicalTier(string value) => value switch
    {
        "Bronze" or "Silver" or "Gold" or "Platinum" or "Diamond" or "Crystal" or "Omega" or "Ultima" => value,
        _ => string.Empty,
    };
    private static Regex Pattern(string pattern) => new(pattern, Options, MatchTimeout);
}
