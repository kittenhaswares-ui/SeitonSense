using System.Text;
using LiteDB;

namespace SeitonSense.Plugin.Services;

internal readonly record struct PvpStatsObservedPlayerAggregate(
    string PlayerName,
    ushort WorldId,
    long Wins,
    long Losses,
    long WinsAgainst,
    long LossesAgainst,
    int Matches,
    long LastSeenUnixSeconds);

internal readonly record struct PvpStatsHistoryReadProgress(
    int DocumentsScanned,
    int TotalDocuments,
    double Fraction);

internal sealed record PvpStatsHistoryReadResult(
    bool Success,
    string Status,
    int DocumentsScanned,
    int MatchesImported,
    int MatchesSkipped,
    long LocalWins,
    long LocalLosses,
    long LatestMatchUnixSeconds,
    IReadOnlyList<PvpStatsObservedPlayerAggregate> Players)
{
    internal static PvpStatsHistoryReadResult Failed(string status) =>
        new(false, status, 0, 0, 0, 0, 0, 0, []);
}

/// <summary>
/// Reads only the documented raw BSON fields needed for a one-time local
/// history import. The original PvpStats database is held through an exclusive,
/// read-only stream for the complete scan. If PvpStats is loaded, opening that
/// stream fails before any data is read.
/// </summary>
internal static class CrystallineConflictPvpStatsHistoryReader
{
    private const string CollectionName = "ccmatch";
    private const int PlayersPerTeam = 5;
    private const int MaximumDocuments = 100_000;
    private const int MaximumDistinctPlayers = 100_000;
    private const long MaximumDatabaseBytes = 16L * 1024 * 1024 * 1024;

    internal static Task<PvpStatsHistoryReadResult> ReadAsync(
        string databasePath,
        string localPlayerName,
        string localWorldName,
        IReadOnlyDictionary<string, ushort> worldIdsByName,
        long importBeforeUnixSecondsExclusive,
        IProgress<PvpStatsHistoryReadProgress>? progress,
        CancellationToken cancellationToken) =>
        Task.Run(
            () => Read(
                databasePath,
                localPlayerName,
                localWorldName,
                worldIdsByName,
                importBeforeUnixSecondsExclusive,
                progress,
                cancellationToken),
            cancellationToken);

    private static PvpStatsHistoryReadResult Read(
        string databasePath,
        string localPlayerName,
        string localWorldName,
        IReadOnlyDictionary<string, ushort> worldIdsByName,
        long importBeforeUnixSecondsExclusive,
        IProgress<PvpStatsHistoryReadProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (!TryNormalizeAliasName(localPlayerName, out var normalizedLocalName) ||
            !TryNormalizeWorldName(localWorldName, out var normalizedLocalWorld) ||
            worldIdsByName is null ||
            worldIdsByName.Count == 0 ||
            importBeforeUnixSecondsExclusive <= 0)
        {
            return PvpStatsHistoryReadResult.Failed("The current character or world could not be verified.");
        }

        string fullPath;
        FileInfo file;
        try
        {
            fullPath = Path.GetFullPath(databasePath);
            file = new FileInfo(fullPath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return PvpStatsHistoryReadResult.Failed("The PvpStats database path is invalid.");
        }

        if (!file.Exists)
            return PvpStatsHistoryReadResult.Failed("PvpStats data.db was not found on this PC.");
        if (file.Length is <= 0 or > MaximumDatabaseBytes)
            return PvpStatsHistoryReadResult.Failed("The PvpStats database size is outside the safe import limit.");

        var players = new Dictionary<string, MutablePlayerAggregate>(StringComparer.Ordinal);
        var documentsScanned = 0;
        var matchesImported = 0;
        var matchesSkipped = 0;
        long localWins = 0;
        long localLosses = 0;
        long latestMatch = 0;

        try
        {
            using var stream = new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.None,
                1024 * 1024,
                FileOptions.SequentialScan);
            using var database = new LiteDatabase(stream, BsonMapper.Global, logStream: null);
            var collection = database.GetCollection<BsonDocument>(CollectionName);
            var totalDocuments = collection.Count();
            if (totalDocuments is < 0 or > MaximumDocuments)
                return PvpStatsHistoryReadResult.Failed("The PvpStats CC collection is outside the safe import limit.");

            foreach (var document in collection.FindAll())
            {
                cancellationToken.ThrowIfCancellationRequested();
                documentsScanned++;
                if (documentsScanned > MaximumDocuments)
                    return PvpStatsHistoryReadResult.Failed("The PvpStats CC scan exceeded its safety limit.");

                if (!TryReadMatch(
                        document,
                        normalizedLocalName,
                        normalizedLocalWorld,
                        worldIdsByName,
                        importBeforeUnixSecondsExclusive,
                        out var match))
                {
                    matchesSkipped++;
                    Report(progress, documentsScanned, totalDocuments);
                    continue;
                }

                checked
                {
                    if (match.LocalWon) localWins++;
                    else localLosses++;
                    matchesImported++;
                }

                latestMatch = Math.Max(latestMatch, match.EndedAtUnixSeconds);
                foreach (var participant in match.RemotePlayers)
                {
                    if (!players.TryGetValue(participant.Identity, out var aggregate))
                    {
                        if (players.Count >= MaximumDistinctPlayers)
                            return PvpStatsHistoryReadResult.Failed("The PvpStats player set exceeded its safety limit.");
                        aggregate = new MutablePlayerAggregate(
                            participant.PlayerName,
                            participant.WorldId);
                        players.Add(participant.Identity, aggregate);
                    }

                    aggregate.Add(
                        participant.Won,
                        participant.IsEnemy,
                        match.LocalWon,
                        match.EndedAtUnixSeconds);
                }

                Report(progress, documentsScanned, totalDocuments);
            }

            Report(progress, documentsScanned, totalDocuments, forceComplete: true);
        }
        catch (OperationCanceledException)
        {
            return PvpStatsHistoryReadResult.Failed("PvpStats history import was cancelled.");
        }
        catch (IOException)
        {
            return PvpStatsHistoryReadResult.Failed(
                "PvpStats is still using data.db. Disable or unload PvpStats, then try the import again.");
        }
        catch (UnauthorizedAccessException)
        {
            return PvpStatsHistoryReadResult.Failed("PvpStats data.db could not be opened read-only.");
        }
        catch (LiteException)
        {
            return PvpStatsHistoryReadResult.Failed("PvpStats data.db is not a supported readable LiteDB file.");
        }
        catch (OverflowException)
        {
            return PvpStatsHistoryReadResult.Failed("PvpStats history counters exceeded their safe bounds.");
        }

        if (matchesImported == 0)
        {
            return new PvpStatsHistoryReadResult(
                false,
                "No completed Casual or Ranked CC matches matched the current character.",
                documentsScanned,
                0,
                matchesSkipped,
                0,
                0,
                0,
                []);
        }

        var resultPlayers = players
            .OrderByDescending(static pair => pair.Value.Matches)
            .ThenByDescending(static pair => pair.Value.LastSeenUnixSeconds)
            .ThenBy(static pair => pair.Key, StringComparer.Ordinal)
            .Select(static pair => pair.Value.ToResult())
            .ToArray();
        return new PvpStatsHistoryReadResult(
            true,
            $"Read {matchesImported:N0} matching CC matches and {resultPlayers.Length:N0} players.",
            documentsScanned,
            matchesImported,
            matchesSkipped,
            localWins,
            localLosses,
            latestMatch,
            resultPlayers);
    }

    private static bool TryReadMatch(
        BsonDocument document,
        string localPlayerName,
        string localWorldName,
        IReadOnlyDictionary<string, ushort> worldIdsByName,
        long importBeforeUnixSecondsExclusive,
        out ImportedMatch match)
    {
        match = default;
        if (!TryGetBoolean(document, "IsCompleted", out var completed) || !completed ||
            !TryGetBoolean(document, "IsDeleted", out var deleted) || deleted ||
            !TryGetBoolean(document, "IsQuarantined", out var quarantined) || quarantined ||
            !TryGetString(document, "MatchType", out var matchType) ||
            matchType is not ("Casual" or "Ranked") ||
            !TryGetString(document, "MatchWinner", out var winner) ||
            winner is not ("Astra" or "Umbra") ||
            !TryGetDateTime(document, "MatchEndTime", out var endedAt))
        {
            return false;
        }

        // LiteDB materializes BSON datetimes in local time. Convert that
        // instant back to UTC; merely relabelling the wall clock as UTC would
        // shift every imported match by the machine's timezone offset.
        if (!TryConvertBsonDateTimeToUnixSeconds(endedAt, out var endedAtUnixSeconds) ||
            endedAtUnixSeconds >= importBeforeUnixSecondsExclusive)
            return false;

        if (!TryGetDocument(document, "LocalPlayer", out var localAlias) ||
            !TryReadAlias(localAlias, worldIdsByName, out var declaredLocal) ||
            !string.Equals(declaredLocal.PlayerName, localPlayerName, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(declaredLocal.WorldName, localWorldName, StringComparison.OrdinalIgnoreCase) ||
            !TryGetDocument(document, "Teams", out var teams) ||
            teams.Count != 2 ||
            !TryReadTeam(teams, "Astra", worldIdsByName, out var astra) ||
            !TryReadTeam(teams, "Umbra", worldIdsByName, out var umbra))
        {
            return false;
        }

        var all = astra.Players.Concat(umbra.Players).ToArray();
        if (all.Length != PlayersPerTeam * 2 ||
            all.Select(static player => player.Identity).Distinct(StringComparer.Ordinal).Count() != all.Length)
        {
            return false;
        }

        var localMatches = all
            .Where(player =>
                string.Equals(player.PlayerName, localPlayerName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(player.WorldName, localWorldName, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (localMatches.Length != 1) return false;

        var localTeam = astra.Players.Any(player =>
            string.Equals(player.Identity, localMatches[0].Identity, StringComparison.Ordinal))
            ? "Astra"
            : "Umbra";
        var remote = new List<ImportedRemotePlayer>(all.Length - 1);
        foreach (var player in astra.Players)
        {
            if (player.Identity == localMatches[0].Identity) continue;
            remote.Add(new ImportedRemotePlayer(
                player.Identity,
                player.PlayerName,
                player.WorldId,
                winner == "Astra",
                IsEnemy: localTeam != "Astra"));
        }
        foreach (var player in umbra.Players)
        {
            if (player.Identity == localMatches[0].Identity) continue;
            remote.Add(new ImportedRemotePlayer(
                player.Identity,
                player.PlayerName,
                player.WorldId,
                winner == "Umbra",
                IsEnemy: localTeam != "Umbra"));
        }

        if (remote.Count != all.Length - 1) return false;
        match = new ImportedMatch(
            winner == localTeam,
            endedAtUnixSeconds,
            remote.ToArray());
        return true;
    }

    private static bool TryReadTeam(
        BsonDocument teams,
        string expectedName,
        IReadOnlyDictionary<string, ushort> worldIdsByName,
        out ImportedTeam team)
    {
        team = default;
        if (!TryGetDocument(teams, expectedName, out var teamDocument) ||
            !TryGetString(teamDocument, "TeamName", out var teamName) ||
            !string.Equals(teamName, expectedName, StringComparison.Ordinal) ||
            !TryGetArray(teamDocument, "Players", out var playerArray) ||
            playerArray.Count != PlayersPerTeam)
        {
            return false;
        }

        var players = new ImportedAlias[PlayersPerTeam];
        for (var index = 0; index < players.Length; index++)
        {
            if (!playerArray[index].IsDocument ||
                !TryGetDocument(playerArray[index].AsDocument, "Alias", out var alias) ||
                !TryReadAlias(alias, worldIdsByName, out players[index]))
            {
                return false;
            }
        }

        team = new ImportedTeam(players);
        return true;
    }

    private static bool TryReadAlias(
        BsonDocument alias,
        IReadOnlyDictionary<string, ushort> worldIdsByName,
        out ImportedAlias imported)
    {
        imported = default;
        if (!TryGetString(alias, "Name", out var name) ||
            !TryGetString(alias, "HomeWorld", out var world) ||
            !TryNormalizeAliasName(name, out var normalizedName) ||
            !TryNormalizeWorldName(world, out var normalizedWorld) ||
            !worldIdsByName.TryGetValue(normalizedWorld, out var worldId) ||
            worldId == 0)
        {
            return false;
        }

        imported = new ImportedAlias(
            $"{worldId}|{normalizedName.ToUpperInvariant()}",
            normalizedName,
            normalizedWorld,
            worldId);
        return true;
    }

    private static bool TryNormalizeAliasName(string? value, out string normalized)
    {
        normalized = string.Empty;
        try
        {
            normalized = value?.Trim().Normalize(NormalizationForm.FormC) ?? string.Empty;
        }
        catch (ArgumentException)
        {
            return false;
        }

        return normalized.Length is >= 3 and <= 42 &&
               Encoding.UTF8.GetByteCount(normalized) <=
               CrystallineConflictMapResultPlayer.PlayerNameBufferLength - 1 &&
               !normalized.Any(character => char.IsControl(character) || char.IsSurrogate(character));
    }

    private static bool TryNormalizeWorldName(string? value, out string normalized)
    {
        normalized = value?.Trim() ?? string.Empty;
        return normalized.Length is >= 2 and <= 32 &&
               normalized.All(character => char.IsLetter(character) || character is '-' or '\'');
    }

    private static bool TryGetString(BsonDocument document, string key, out string value)
    {
        value = string.Empty;
        return document.TryGetValue(key, out var bson) &&
               bson.IsString &&
               !string.IsNullOrWhiteSpace(value = bson.AsString);
    }

    private static bool TryGetBoolean(BsonDocument document, string key, out bool value)
    {
        value = false;
        if (!document.TryGetValue(key, out var bson) || !bson.IsBoolean) return false;
        value = bson.AsBoolean;
        return true;
    }

    private static bool TryGetDateTime(BsonDocument document, string key, out DateTime value)
    {
        value = default;
        if (!document.TryGetValue(key, out var bson) || !bson.IsDateTime) return false;
        value = bson.AsDateTime;
        return value > DateTime.UnixEpoch;
    }

    internal static bool TryConvertBsonDateTimeToUnixSeconds(
        DateTime value,
        out long unixSeconds)
    {
        unixSeconds = 0;
        try
        {
            // LiteDB exposes BSON datetimes as local wall time. ToUniversalTime
            // preserves the stored instant for Local/UTC and treats a rare
            // Unspecified value consistently with the machine-local reader.
            unixSeconds = new DateTimeOffset(value.ToUniversalTime())
                .ToUnixTimeSeconds();
            return unixSeconds > 0;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool TryGetDocument(BsonDocument document, string key, out BsonDocument value)
    {
        value = null!;
        if (!document.TryGetValue(key, out var bson) || !bson.IsDocument) return false;
        value = bson.AsDocument;
        return true;
    }

    private static bool TryGetArray(BsonDocument document, string key, out BsonArray value)
    {
        value = null!;
        if (!document.TryGetValue(key, out var bson) || !bson.IsArray) return false;
        value = bson.AsArray;
        return true;
    }

    private static void Report(
        IProgress<PvpStatsHistoryReadProgress>? progress,
        int scanned,
        int total,
        bool forceComplete = false)
    {
        if (progress is null || (!forceComplete && scanned % 32 != 0)) return;
        var fraction = forceComplete
            ? 1d
            : total <= 0
                ? 0d
                : Math.Clamp((double)scanned / total, 0d, 1d);
        progress.Report(new PvpStatsHistoryReadProgress(scanned, total, fraction));
    }

    private readonly record struct ImportedAlias(
        string Identity,
        string PlayerName,
        string WorldName,
        ushort WorldId);

    private readonly record struct ImportedTeam(ImportedAlias[] Players);

    private readonly record struct ImportedRemotePlayer(
        string Identity,
        string PlayerName,
        ushort WorldId,
        bool Won,
        bool IsEnemy);

    private readonly record struct ImportedMatch(
        bool LocalWon,
        long EndedAtUnixSeconds,
        ImportedRemotePlayer[] RemotePlayers);

    private sealed class MutablePlayerAggregate(string playerName, ushort worldId)
    {
        internal string PlayerName { get; } = playerName;
        internal ushort WorldId { get; } = worldId;
        internal long Wins { get; private set; }
        internal long Losses { get; private set; }
        internal long WinsAgainst { get; private set; }
        internal long LossesAgainst { get; private set; }
        internal int Matches { get; private set; }
        internal long LastSeenUnixSeconds { get; private set; }

        internal void Add(
            bool won,
            bool isEnemy,
            bool localWon,
            long endedAtUnixSeconds)
        {
            checked
            {
                if (won) Wins++;
                else Losses++;
                if (isEnemy)
                {
                    if (localWon) WinsAgainst++;
                    else LossesAgainst++;
                }
                Matches++;
            }
            LastSeenUnixSeconds = Math.Max(LastSeenUnixSeconds, endedAtUnixSeconds);
        }

        internal PvpStatsObservedPlayerAggregate ToResult() =>
            new(
                PlayerName,
                WorldId,
                Wins,
                Losses,
                WinsAgainst,
                LossesAgainst,
                Matches,
                LastSeenUnixSeconds);
    }
}
