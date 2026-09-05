namespace SeitonSense.Core;

public readonly record struct CrystallineConflictPlayerStatsEntry(
    string PlayerName,
    string WorldName,
    long WinsAgainst,
    long LossesAgainst,
    long LastSeenUnixSeconds,
    long WinsTogether = 0,
    long LossesTogether = 0);

public enum CrystallineConflictPlayerStatsRankingMode
{
    LossesAgainst,
    WinsAgainst,
    WinsTogether,
    LossesTogether,
}

public enum CrystallineConflictPlayerStatsRole { Opponents, Teammates }

[Flags]
public enum CrystallineConflictPlayerStatsBadge
{
    None = 0,
    ArchNemesis = 1 << 0,
    CannonFodder = 1 << 1,
    MostLosses = ArchNemesis,
    MostWins = CannonFodder,
}

public readonly record struct CrystallineConflictPlayerStatsRankRow(
    int Rank,
    string PlayerName,
    string WorldName,
    long WinsAgainst,
    long LossesAgainst,
    long MatchesAgainst,
    double WinRate,
    long LastSeenUnixSeconds,
    CrystallineConflictPlayerStatsBadge Badges,
    CrystallineConflictPlayerStatsRole Role = CrystallineConflictPlayerStatsRole.Opponents)
{
    // Legacy field names remain source-compatible; these values always belong
    // to Role, never to a mixture of allied and opposing encounters.
    public long Wins => WinsAgainst;
    public long Losses => LossesAgainst;
    public long Games => MatchesAgainst;
}

/// <summary>
/// Pure presentation rules for separate, local-player-relative CC role histories.
/// Badges and ranks are calculated from the complete valid catalog before an
/// optional search is applied, so searching cannot promote an arbitrary row.
/// </summary>
public static class CrystallineConflictPlayerStatsRules
{
    public const long BadgeMinimumEnemyMeetings = 3;

    public static bool IsTeammateMode(CrystallineConflictPlayerStatsRankingMode mode) =>
        mode is CrystallineConflictPlayerStatsRankingMode.WinsTogether or
            CrystallineConflictPlayerStatsRankingMode.LossesTogether;

    private static bool IsLossMode(CrystallineConflictPlayerStatsRankingMode mode) =>
        mode is CrystallineConflictPlayerStatsRankingMode.LossesAgainst or
            CrystallineConflictPlayerStatsRankingMode.LossesTogether;

    public static CrystallineConflictPlayerStatsRankRow[] BuildRanking(
        IReadOnlyList<CrystallineConflictPlayerStatsEntry>? entries,
        CrystallineConflictPlayerStatsRankingMode mode,
        string? search = null)
    {
        if (entries is null || !Enum.IsDefined(mode)) return [];

        var candidates = new List<Candidate>(entries.Count);
        for (var index = 0; index < entries.Count; index++)
        {
            if (TryCreateCandidate(entries[index], index, mode, out var candidate))
                candidates.Add(candidate);
        }

        if (candidates.Count == 0) return [];

        var archNemesis = FindBadgeWinner(
            candidates,
            IsTeammateMode(mode) ? CrystallineConflictPlayerStatsRankingMode.LossesTogether :
                CrystallineConflictPlayerStatsRankingMode.LossesAgainst);
        var cannonFodder = FindBadgeWinner(
            candidates,
            IsTeammateMode(mode) ? CrystallineConflictPlayerStatsRankingMode.WinsTogether :
                CrystallineConflictPlayerStatsRankingMode.WinsAgainst);

        candidates.Sort((left, right) => Compare(left, right, mode));
        var normalizedSearch = search?.Trim();
        var hasSearch = !string.IsNullOrEmpty(normalizedSearch);
        var rows = new List<CrystallineConflictPlayerStatsRankRow>(candidates.Count);
        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            if (hasSearch &&
                !candidate.SearchText.Contains(
                    normalizedSearch!,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var badges = CrystallineConflictPlayerStatsBadge.None;
            if (archNemesis is { } nemesis && candidate.SourceIndex == nemesis.SourceIndex)
                badges |= CrystallineConflictPlayerStatsBadge.ArchNemesis;
            if (cannonFodder is { } fodder && candidate.SourceIndex == fodder.SourceIndex)
                badges |= CrystallineConflictPlayerStatsBadge.CannonFodder;

            rows.Add(new CrystallineConflictPlayerStatsRankRow(
                index + 1,
                candidate.PlayerName,
                candidate.WorldName,
                candidate.WinsAgainst,
                candidate.LossesAgainst,
                candidate.MatchesAgainst,
                candidate.WinRate,
                candidate.LastSeenUnixSeconds,
                badges,
                IsTeammateMode(mode) ? CrystallineConflictPlayerStatsRole.Teammates :
                    CrystallineConflictPlayerStatsRole.Opponents));
        }

        return rows.ToArray();
    }

    private static Candidate? FindBadgeWinner(
        IReadOnlyList<Candidate> candidates,
        CrystallineConflictPlayerStatsRankingMode mode)
    {
        Candidate? winner = null;
        foreach (var candidate in candidates)
        {
            if (candidate.MatchesAgainst < BadgeMinimumEnemyMeetings)
                continue;
            if (IsLossMode(mode) &&
                candidate.LossesAgainst == 0)
                continue;
            if (!IsLossMode(mode) &&
                candidate.WinsAgainst == 0)
                continue;

            if (winner is null || Compare(candidate, winner.Value, mode) < 0)
                winner = candidate;
        }

        return winner;
    }

    private static bool TryCreateCandidate(
        CrystallineConflictPlayerStatsEntry entry,
        int sourceIndex,
        CrystallineConflictPlayerStatsRankingMode mode,
        out Candidate candidate)
    {
        candidate = default;
        var playerName = entry.PlayerName?.Trim();
        var worldName = entry.WorldName?.Trim();
        var wins = IsTeammateMode(mode) ? entry.WinsTogether : entry.WinsAgainst;
        var losses = IsTeammateMode(mode) ? entry.LossesTogether : entry.LossesAgainst;
        if (string.IsNullOrWhiteSpace(playerName) ||
            string.IsNullOrWhiteSpace(worldName) ||
            wins < 0 ||
            losses < 0 ||
            wins > long.MaxValue - losses ||
            entry.LastSeenUnixSeconds < 0)
        {
            return false;
        }

        var matches = wins + losses;
        if (matches == 0) return false;

        candidate = new Candidate(
            sourceIndex,
            playerName,
            worldName,
            wins,
            losses,
            matches,
            wins / (double)matches,
            entry.LastSeenUnixSeconds,
            $"{playerName} @ {worldName}");
        return true;
    }

    private static int Compare(
        Candidate left,
        Candidate right,
        CrystallineConflictPlayerStatsRankingMode mode)
    {
        var primary = IsLossMode(mode)
            ? right.LossesAgainst.CompareTo(left.LossesAgainst)
            : right.WinsAgainst.CompareTo(left.WinsAgainst);
        if (primary != 0) return primary;

        var rate = IsLossMode(mode)
            ? left.WinRate.CompareTo(right.WinRate)
            : right.WinRate.CompareTo(left.WinRate);
        if (rate != 0) return rate;

        var matches = right.MatchesAgainst.CompareTo(left.MatchesAgainst);
        if (matches != 0) return matches;

        var lastSeen = right.LastSeenUnixSeconds.CompareTo(left.LastSeenUnixSeconds);
        if (lastSeen != 0) return lastSeen;

        var playerName = StringComparer.OrdinalIgnoreCase.Compare(left.PlayerName, right.PlayerName);
        if (playerName != 0) return playerName;
        playerName = StringComparer.Ordinal.Compare(left.PlayerName, right.PlayerName);
        if (playerName != 0) return playerName;

        var worldName = StringComparer.OrdinalIgnoreCase.Compare(left.WorldName, right.WorldName);
        if (worldName != 0) return worldName;
        worldName = StringComparer.Ordinal.Compare(left.WorldName, right.WorldName);
        if (worldName != 0) return worldName;

        return left.SourceIndex.CompareTo(right.SourceIndex);
    }

    private readonly record struct Candidate(
        int SourceIndex,
        string PlayerName,
        string WorldName,
        long WinsAgainst,
        long LossesAgainst,
        long MatchesAgainst,
        double WinRate,
        long LastSeenUnixSeconds,
        string SearchText);
}
