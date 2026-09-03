namespace SeitonSense.Core;

public readonly record struct CrystallineConflictPlayerStatsEntry(
    string PlayerName,
    string WorldName,
    long WinsAgainst,
    long LossesAgainst,
    long LastSeenUnixSeconds);

public enum CrystallineConflictPlayerStatsRankingMode
{
    LossesAgainst,
    WinsAgainst,
}

[Flags]
public enum CrystallineConflictPlayerStatsBadge
{
    None = 0,
    ArchNemesis = 1 << 0,
    CannonFodder = 1 << 1,
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
    CrystallineConflictPlayerStatsBadge Badges);

/// <summary>
/// Pure presentation rules for a local, player-relative CC opponent history.
/// Badges and ranks are calculated from the complete valid catalog before an
/// optional search is applied, so searching cannot promote an arbitrary row.
/// </summary>
public static class CrystallineConflictPlayerStatsRules
{
    public const long BadgeMinimumEnemyMeetings = 3;

    public static CrystallineConflictPlayerStatsRankRow[] BuildRanking(
        IReadOnlyList<CrystallineConflictPlayerStatsEntry>? entries,
        CrystallineConflictPlayerStatsRankingMode mode,
        string? search = null)
    {
        if (entries is null || !Enum.IsDefined(mode)) return [];

        var candidates = new List<Candidate>(entries.Count);
        for (var index = 0; index < entries.Count; index++)
        {
            if (TryCreateCandidate(entries[index], index, out var candidate))
                candidates.Add(candidate);
        }

        if (candidates.Count == 0) return [];

        var archNemesis = FindBadgeWinner(
            candidates,
            CrystallineConflictPlayerStatsRankingMode.LossesAgainst);
        var cannonFodder = FindBadgeWinner(
            candidates,
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
                badges));
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
            if (mode == CrystallineConflictPlayerStatsRankingMode.LossesAgainst &&
                candidate.LossesAgainst == 0)
                continue;
            if (mode == CrystallineConflictPlayerStatsRankingMode.WinsAgainst &&
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
        out Candidate candidate)
    {
        candidate = default;
        var playerName = entry.PlayerName?.Trim();
        var worldName = entry.WorldName?.Trim();
        if (string.IsNullOrWhiteSpace(playerName) ||
            string.IsNullOrWhiteSpace(worldName) ||
            entry.WinsAgainst < 0 ||
            entry.LossesAgainst < 0 ||
            entry.WinsAgainst > long.MaxValue - entry.LossesAgainst ||
            entry.LastSeenUnixSeconds < 0)
        {
            return false;
        }

        var matches = entry.WinsAgainst + entry.LossesAgainst;
        if (matches == 0) return false;

        candidate = new Candidate(
            sourceIndex,
            playerName,
            worldName,
            entry.WinsAgainst,
            entry.LossesAgainst,
            matches,
            entry.WinsAgainst / (double)matches,
            entry.LastSeenUnixSeconds,
            $"{playerName} @ {worldName}");
        return true;
    }

    private static int Compare(
        Candidate left,
        Candidate right,
        CrystallineConflictPlayerStatsRankingMode mode)
    {
        var primary = mode == CrystallineConflictPlayerStatsRankingMode.LossesAgainst
            ? right.LossesAgainst.CompareTo(left.LossesAgainst)
            : right.WinsAgainst.CompareTo(left.WinsAgainst);
        if (primary != 0) return primary;

        var rate = mode == CrystallineConflictPlayerStatsRankingMode.LossesAgainst
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
