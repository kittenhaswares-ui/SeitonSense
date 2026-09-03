using SeitonSense.Core;

internal static class CrystallineConflictPlayerStatsSelfTests
{
    public static void InvalidNamesCountersAndOverflowAreExcluded()
    {
        var valid = Entry("Valid Player", "Balmung", 2, 1, 100);
        var entries = new[]
        {
            valid,
            Entry(" ", "Balmung", 5, 0, 100),
            Entry("No World", " ", 5, 0, 100),
            Entry("Negative Wins", "Balmung", -1, 1, 100),
            Entry("Negative Losses", "Balmung", 1, -1, 100),
            Entry("Overflow", "Balmung", long.MaxValue, 1, 100),
            Entry("Negative Time", "Balmung", 1, 0, -1),
            Entry("No Meetings", "Balmung", 0, 0, 100),
        };

        var rows = CrystallineConflictPlayerStatsRules.BuildRanking(
            entries,
            CrystallineConflictPlayerStatsRankingMode.WinsAgainst);
        Equal(1, rows.Length, "only the valid opponent row remains");
        Equal("Valid Player", rows[0].PlayerName, "valid player identity");
        Equal(3L, rows[0].MatchesAgainst, "bounded meeting total");
        Near(2d / 3d, rows[0].WinRate, "bounded win rate");

        Equal(
            0,
            CrystallineConflictPlayerStatsRules.BuildRanking(
                null,
                CrystallineConflictPlayerStatsRankingMode.WinsAgainst).Length,
            "null catalog is empty");
        Equal(
            0,
            CrystallineConflictPlayerStatsRules.BuildRanking(
                [valid],
                (CrystallineConflictPlayerStatsRankingMode)99).Length,
            "unknown ranking mode fails closed");
    }

    public static void BothModesUseTheirExactRateTieBreaks()
    {
        var entries = new[]
        {
            Entry("Alpha", "Balmung", 1, 4, 100),
            Entry("Bravo", "Balmung", 2, 4, 200),
            Entry("Charlie", "Balmung", 5, 1, 300),
            Entry("Delta", "Balmung", 5, 5, 400),
        };

        var losses = CrystallineConflictPlayerStatsRules.BuildRanking(
            entries,
            CrystallineConflictPlayerStatsRankingMode.LossesAgainst);
        SequenceEqual(
            ["Delta", "Alpha", "Bravo", "Charlie"],
            losses.Select(static row => row.PlayerName),
            "loss mode is losses desc then local win rate asc");

        var wins = CrystallineConflictPlayerStatsRules.BuildRanking(
            entries,
            CrystallineConflictPlayerStatsRankingMode.WinsAgainst);
        SequenceEqual(
            ["Charlie", "Delta", "Bravo", "Alpha"],
            wins.Select(static row => row.PlayerName),
            "win mode is wins desc then local win rate desc");
        SequenceEqual([1, 2, 3, 4], wins.Select(static row => row.Rank), "global ranks are ordinal");
    }

    public static void BadgesAreGlobalAndRequireThreeEnemyMeetings()
    {
        var entries = new[]
        {
            Entry("Nemesis", "Cerberus", 1, 5, 500),
            Entry("Fodder", "Cerberus", 6, 1, 400),
            Entry("Too New", "Cerberus", 2, 0, 900),
            Entry("Ordinary", "Cerberus", 1, 2, 300),
        };

        var all = CrystallineConflictPlayerStatsRules.BuildRanking(
            entries,
            CrystallineConflictPlayerStatsRankingMode.LossesAgainst);
        var nemesis = Single(all, "Nemesis");
        var fodder = Single(all, "Fodder");
        var tooNew = Single(all, "Too New");
        True(
            nemesis.Badges.HasFlag(CrystallineConflictPlayerStatsBadge.ArchNemesis),
            "global loss leader is the arch nemesis");
        True(
            fodder.Badges.HasFlag(CrystallineConflictPlayerStatsBadge.CannonFodder),
            "global win leader is cannon fodder");
        Equal(CrystallineConflictPlayerStatsBadge.None, tooNew.Badges, "two meetings cannot earn a badge");

        var searchedOrdinary = CrystallineConflictPlayerStatsRules.BuildRanking(
            entries,
            CrystallineConflictPlayerStatsRankingMode.LossesAgainst,
            "ordinary");
        Equal(1, searchedOrdinary.Length, "search leaves one ordinary row");
        Equal(
            CrystallineConflictPlayerStatsBadge.None,
            searchedOrdinary[0].Badges,
            "search cannot promote the remaining row to a global badge");

        var searchedNemesis = CrystallineConflictPlayerStatsRules.BuildRanking(
            entries,
            CrystallineConflictPlayerStatsRankingMode.LossesAgainst,
            "nemesis @ cerberus");
        Equal(1, searchedNemesis.Length, "full display identity is searchable");
        True(
            searchedNemesis[0].Badges.HasFlag(CrystallineConflictPlayerStatsBadge.ArchNemesis),
            "global badge survives search");
    }

    public static void SearchAndFinalIdentityTiesAreDeterministic()
    {
        var entries = new[]
        {
            Entry("Bravo", "Balmung", 2, 2, 100),
            Entry("Alpha", "Cactuar", 2, 2, 100),
            Entry("Alpha", "Balmung", 2, 2, 100),
            Entry("Recent", "Ragnarok", 2, 2, 200),
        };

        var rows = CrystallineConflictPlayerStatsRules.BuildRanking(
            entries,
            CrystallineConflictPlayerStatsRankingMode.WinsAgainst);
        SequenceEqual(
            ["Recent @ Ragnarok", "Alpha @ Balmung", "Alpha @ Cactuar", "Bravo @ Balmung"],
            rows.Select(static row => $"{row.PlayerName} @ {row.WorldName}"),
            "last seen then name and world resolve exact ties");

        var search = CrystallineConflictPlayerStatsRules.BuildRanking(
            entries,
            CrystallineConflictPlayerStatsRankingMode.WinsAgainst,
            "ALPHA @ caCT");
        Equal(1, search.Length, "display identity search is case insensitive");
        Equal("Cactuar", search[0].WorldName, "search includes world name");
        Equal(3, search[0].Rank, "search preserves the global rank");
    }

    private static CrystallineConflictPlayerStatsEntry Entry(
        string playerName,
        string worldName,
        long wins,
        long losses,
        long lastSeen) =>
        new(playerName, worldName, wins, losses, lastSeen);

    private static CrystallineConflictPlayerStatsRankRow Single(
        IEnumerable<CrystallineConflictPlayerStatsRankRow> rows,
        string playerName) =>
        rows.Single(row => string.Equals(row.PlayerName, playerName, StringComparison.Ordinal));

    private static void True(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static void Equal<T>(T expected, T actual, string message)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
    }

    private static void Near(double expected, double actual, string message)
    {
        if (Math.Abs(expected - actual) > 0.000_001d)
            throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
    }

    private static void SequenceEqual<T>(
        IReadOnlyList<T> expected,
        IEnumerable<T> actual,
        string message)
    {
        var values = actual.ToArray();
        if (!expected.SequenceEqual(values))
        {
            throw new InvalidOperationException(
                $"{message}: expected [{string.Join(", ", expected)}], got [{string.Join(", ", values)}]");
        }
    }
}
