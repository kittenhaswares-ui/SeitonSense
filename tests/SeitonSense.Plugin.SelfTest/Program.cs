using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LiteDB;
using SeitonSense.Core;
using SeitonSense.Plugin.Services;

var tests = new (string Name, Action Run)[]
{
    ("CC prediction preparation snapshot stays visible without live totals", PredictionPreparationSnapshotStaysVisible),
    ("PvpStats import cutoff keeps a five-minute no-overlap margin", ImportCutoffUsesFirstNativeEpoch),
    ("PvpStats import rejects every out-of-bound timestamp atomically", ImportBoundariesAreAtomic),
    ("PvpStats merge rejects a stale store generation", StaleGenerationCannotMerge),
    ("native matchup counters use enemy encounters and local perspective", NativeMatchupCountersUseEnemyEncounters),
    ("current player snapshots are searchable while schema-4 identity stays closed", PlayerSnapshotsRespectIdentityEpoch),
    ("searchable schema-5 history reloads with its persisted HMAC salt", SearchableHistoryReloadsWithPersistedSalt),
    ("PvpStats import is one-shot and persists searchable matchup identity", ImportIsOneShotAndSearchable),
    ("schema-4 PvpStats details backfill is idempotent without double-counting W/L", PvpStatsBackfillIsIdempotent),
    ("schema-1 map history migrates without blocking a fresh import", SchemaOneMigrationIsImportable),
    ("schema-2 player rows are discarded while map and overall W/L survive", SchemaTwoPlayersFailClosed),
    ("unknown old player-history epoch blocks import", UnknownPlayerEpochFailsClosed),
    ("LiteDB local datetime conversion preserves the UTC instant", LiteDbLocalDateTimeUsesUtcInstant),
    ("PvpStats reader accepts the current LiteDB CC shape", PvpStatsReaderAcceptsCurrentLiteDbShape),
    ("full imported player history evicts deterministically for one native match", FullImportStillAcceptsNativeMatch),
    ("repeated rejected Guard spam preserves the original attempt", RejectedGuardSpamRestoresOriginalAttempt),
};

static void PredictionPreparationSnapshotStaysVisible()
{
    var snapshot = CrystallineConflictPredictionSnapshot.Preparing();

    True(snapshot.IsActive, "preparation snapshot remains drawable");
    False(snapshot.IsComplete, "roster is not claimed complete before exact 5 + 5 capture");
    False(snapshot.HasCombatStarted, "preparation does not claim combat started");
    False(snapshot.LiveTotalsIncomplete, "live combat totals remain closed during preparation");
    Equal(0, snapshot.Allies.Length, "unresolved allies are not fabricated");
    Equal(0, snapshot.Enemies.Length, "unresolved enemies are not fabricated");
}

try
{
    foreach (var test in tests)
    {
        test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }

    Console.WriteLine($"PASS all {tests.Length} plugin self-tests");
}
catch (Exception exception)
{
    Console.Error.WriteLine($"FAIL {exception.GetType().Name}: {exception.Message}");
    Environment.ExitCode = 1;
}
return;

static void ImportCutoffUsesFirstNativeEpoch()
{
    WithStore((store, _) =>
    {
        const long capturedAt = 50_000;
        True(store.TryRecord(Sample(capturedAt), false, true), "native player history record");
        True(store.TryGetPvpStatsImportPlan(1003, out var plan), "import plan");
        Equal(capturedAt - 300, plan.ImportBeforeUnixSecondsExclusive, "five-minute overlap margin");
    });
}

static void ImportBoundariesAreAtomic()
{
    WithStore((store, directory) =>
    {
        const long capturedAt = 50_000;
        True(store.TryRecord(Sample(capturedAt), false, true), "native player history record");
        True(store.TryGetPvpStatsImportPlan(1003, out var plan), "bounded import plan");
        Equal(49_700L, plan.ImportBeforeUnixSecondsExclusive, "exclusive cutoff");
        var unchangedJson = File.ReadAllText(Path.Combine(directory, "cc-map-stats.json"));

        var invalidImports = new[]
        {
            ImportResultWithTimes(0, 0),
            ImportResultWithTimes(plan.ImportBeforeUnixSecondsExclusive, 49_699),
            ImportResultWithTimes(40_000, 40_001),
            ImportResultWithTimes(40_000, 0),
        };
        foreach (var invalid in invalidImports)
        {
            False(
                store.TryMergePvpStatsHistory(
                    1003,
                    plan.StoreGeneration,
                    plan.ImportBeforeUnixSecondsExclusive,
                    invalid,
                    out _),
                $"invalid import latest={invalid.LatestMatchUnixSeconds}");
            Equal(
                unchangedJson,
                File.ReadAllText(Path.Combine(directory, "cc-map-stats.json")),
                "failed import leaves the store byte-for-byte unchanged");
            True(store.TryGetPvpStatsImportPlan(1003, out var afterFailure), "plan after rejection");
            Equal(plan.StoreGeneration, afterFailure.StoreGeneration, "failed import generation");
            False(afterFailure.AlreadyImported, "failed import marker");
        }

        True(
            store.TryMergePvpStatsHistory(
                1003,
                plan.StoreGeneration,
                plan.ImportBeforeUnixSecondsExclusive,
                ImportResultWithTimes(49_699, 49_699),
                out var accepted),
            "last second before the exclusive cutoff");
        True(accepted.Success, "boundary-valid import result");
    });
}

static void StaleGenerationCannotMerge()
{
    WithStore((store, unusedDirectory) =>
    {
        _ = unusedDirectory;
        True(store.TryGetPvpStatsImportPlan(1003, out var before), "initial plan");
        True(store.TryReset(), "reset");
        False(
            store.TryMergePvpStatsHistory(
                1003,
                before.StoreGeneration,
                before.ImportBeforeUnixSecondsExclusive,
                ImportResult(),
                out var ignored),
            "pre-reset import generation");
        _ = ignored;
    });
}

static void NativeMatchupCountersUseEnemyEncounters()
{
    WithStore((store, directory) =>
    {
        True(store.TryRecord(Sample(50_000), false, true), "first native player history record");
        var firstJson = File.ReadAllText(Path.Combine(directory, "cc-map-stats.json"));
        True(firstJson.Contains("Enemy One", StringComparison.Ordinal), "enemy identity persisted for search");
        False(firstJson.Contains("Ally One", StringComparison.Ordinal), "ally-only identity remains HMAC-only");

        var changedSides = Participants();
        changedSides[0] = changedSides[0] with { Team = 1 };
        changedSides[5] = changedSides[5] with { Team = 0 };
        True(
            store.TryRecord(SampleWithParticipants(60_000, 2, changedSides), false, true),
            "second native player history record");
        var secondJson = File.ReadAllText(Path.Combine(directory, "cc-map-stats.json"));
        True(secondJson.Contains("Ally One", StringComparison.Ordinal), "identity becomes searchable after enemy encounter");

        var snapshot = store.GetPlayerStatisticsSnapshot(1003);
        Equal(6, snapshot.Players.Length, "only identities encountered as enemies are listed");

        var enemyOne = snapshot.Players.Single(
            static player => player.PlayerName == "Enemy One");
        Equal(1L, enemyOne.WinsAgainst, "local win against Enemy One");
        Equal(0L, enemyOne.LossesAgainst, "later allied loss does not count against Enemy One");
        Equal(60_000L, enemyOne.LastSeenUnixSeconds, "last seen refreshes on a later allied encounter");

        var allyOne = snapshot.Players.Single(
            static player => player.PlayerName == "Ally One");
        Equal(0L, allyOne.WinsAgainst, "earlier allied win does not count against Ally One");
        Equal(1L, allyOne.LossesAgainst, "local loss after Ally One becomes an enemy");

        True(
            store.TryGetObservedPlayerRecord(
                1003,
                "Enemy One",
                21,
                false,
                true,
                out var enemyRecord),
            "Enemy One participant record");
        Equal(0L, enemyRecord.Wins, "Enemy One participant wins");
        Equal(2L, enemyRecord.Losses, "Enemy One participant losses across both sides");

        True(
            store.TryGetObservedPlayerRecord(
                1003,
                "Ally One",
                21,
                false,
                false,
                out var allyRecord),
            "Ally One participant record");
        Equal(2L, allyRecord.Wins, "Ally One participant wins across both sides");
        Equal(0L, allyRecord.Losses, "Ally One participant losses");
    });
}

static void PlayerSnapshotsRespectIdentityEpoch()
{
    WithStore((store, _) =>
    {
        True(store.TryRecord(Sample(50_000), false, true), "native searchable history");
        var snapshot = store.GetPlayerStatisticsSnapshot(1003);
        var ranking = CrystallineConflictPlayerStatsRules.BuildRanking(
            snapshot.Players
                .Select(static player => new CrystallineConflictPlayerStatsEntry(
                    player.PlayerName,
                    player.WorldId.ToString(),
                    player.WinsAgainst,
                    player.LossesAgainst,
                    player.LastSeenUnixSeconds))
                .ToArray(),
            CrystallineConflictPlayerStatsRankingMode.WinsAgainst,
            "enemy one");

        Equal(1, ranking.Length, "current snapshot can be searched by player name");
        Equal("Enemy One", ranking[0].PlayerName, "search result identity");
        Equal(1L, ranking[0].WinsAgainst, "search result local win");
        Equal(0L, ranking[0].LossesAgainst, "search result local loss");
    });

    WithPreparedStore(
        CreateUnknownEpochSchemaFourDocument,
        (store, _) =>
        {
            True(
                store.TryGetObservedPlayerRecord(
                    1003,
                    "Remote Alpha",
                    21,
                    false,
                    false,
                    out var legacyRecord),
                "schema-4 one-way history remains usable for prediction");
            Equal(1L, legacyRecord.Matches, "schema-4 participant history survives migration");
            Equal(
                0,
                store.GetPlayerStatisticsSnapshot(1003).Players.Length,
                "schema-4 one-way keys do not invent searchable player identity");
        });
}

static void SearchableHistoryReloadsWithPersistedSalt()
{
    WithStore((store, directory) =>
    {
        True(store.TryRecord(Sample(50_000), false, true), "persist searchable history");
        Equal(5, store.GetPlayerStatisticsSnapshot(1003).Players.Length, "initial enemy snapshot");

        var reloaded = new CrystallineConflictMapStatisticsStore(directory, null!);
        True(reloaded.StorageAvailable, "schema-5 store reloads with its saved salt");
        var snapshot = reloaded.GetPlayerStatisticsSnapshot(1003);
        Equal(5, snapshot.Players.Length, "searchable rows survive restart");
        var enemyOne = snapshot.Players.Single(
            static player => player.PlayerName == "Enemy One");
        Equal(1L, enemyOne.WinsAgainst, "reloaded local wins against");
        Equal(0L, enemyOne.LossesAgainst, "reloaded local losses against");

        True(
            reloaded.TryRecord(SampleWithParticipants(60_000, 2, Participants()), false, true),
            "reloaded store accepts another native result");
        var updated = reloaded.GetPlayerStatisticsSnapshot(1003).Players.Single(
            static player => player.PlayerName == "Enemy One");
        Equal(1L, updated.WinsAgainst, "post-restart win preserved");
        Equal(1L, updated.LossesAgainst, "post-restart loss recorded");
    });
}

static void ImportIsOneShotAndSearchable()
{
    WithStore((store, directory) =>
    {
        True(store.TryGetPvpStatsImportPlan(1003, out var firstPlan), "initial plan");
        True(
            store.TryMergePvpStatsHistory(
                1003,
                firstPlan.StoreGeneration,
                firstPlan.ImportBeforeUnixSecondsExclusive,
                ImportResult(),
                out var first),
            "first merge");
        True(first.Success && !first.AlreadyImported, "first merge result");
        True(first.ImportedLocalRecord, "empty own record filled from import");

        True(store.TryGetPvpStatsImportPlan(1003, out var secondPlan), "second plan");
        True(secondPlan.AlreadyImported, "one-time marker");
        True(
            store.TryMergePvpStatsHistory(
                1003,
                secondPlan.StoreGeneration,
                secondPlan.ImportBeforeUnixSecondsExclusive,
                ImportResult(),
                out var second),
            "second merge is a no-op");
        True(second.AlreadyImported, "second merge reports already imported");

        True(
            store.TryGetObservedPlayerRecord(
                1003,
                "Remote Alpha",
                21,
                false,
                false,
                out var remote),
            "imported remote record");
        Equal(3L, remote.Matches, "remote match count");

        var snapshot = store.GetPlayerStatisticsSnapshot(1003);
        Equal(1, snapshot.Players.Length, "imported searchable player count");
        Equal("Remote Alpha", snapshot.Players[0].PlayerName, "imported searchable player name");
        Equal(21, snapshot.Players[0].WorldId, "imported searchable player world");
        Equal(1L, snapshot.Players[0].WinsAgainst, "imported local wins against player");
        Equal(1L, snapshot.Players[0].LossesAgainst, "imported local losses against player");

        var json = File.ReadAllText(Path.Combine(directory, "cc-map-stats.json"));
        True(json.Contains("Remote Alpha", StringComparison.OrdinalIgnoreCase), "searchable remote name persisted locally");
        False(json.Contains("Local Tester", StringComparison.OrdinalIgnoreCase), "raw local name absent");
        using var document = JsonDocument.Parse(json);
        Equal(5, document.RootElement.GetProperty("Schema").GetInt32(), "store schema");
    });
}

static void PvpStatsBackfillIsIdempotent()
{
    WithPreparedStore(
        CreateSchemaFourImportedHistoryDocument,
        (store, _) =>
        {
            True(store.TryGetPvpStatsImportPlan(1003, out var firstPlan), "schema-4 backfill plan");
            False(firstPlan.AlreadyImported, "searchable details still need backfill");
            Equal(50_001L, firstPlan.ImportBeforeUnixSecondsExclusive, "legacy unbounded cutoff is capped after the original import time");
            Equal(3, firstPlan.PreviouslyImportedMatches, "saved imported matches reused");
            Equal(1, firstPlan.PreviouslyImportedPlayers, "saved imported players reused");

            True(
                store.TryMergePvpStatsHistory(
                    1003,
                    firstPlan.StoreGeneration,
                    firstPlan.ImportBeforeUnixSecondsExclusive,
                    ImportResultWithSkippedHistoricalPlayer(),
                    out var first),
                "schema-4 player details backfill");
            True(first.Success && !first.AlreadyImported, "first backfill result");
            Equal(1, first.ImportedPlayers, "backfilled searchable players");
            False(first.ImportedLocalRecord, "backfill does not replace local W/L");

            True(
                store.TryGetObservedPlayerRecord(
                    1003,
                    "Remote Alpha",
                    21,
                    false,
                    false,
                    out var afterFirst),
                "backfilled participant record");
            Equal(2L, afterFirst.Wins, "participant wins are not added twice");
            Equal(1L, afterFirst.Losses, "participant losses are not added twice");
            var firstSnapshot = store.GetPlayerStatisticsSnapshot(1003);
            Equal(1, firstSnapshot.Players.Length, "backfilled searchable snapshot");
            Equal(1L, firstSnapshot.Players[0].WinsAgainst, "backfilled local wins against");
            Equal(1L, firstSnapshot.Players[0].LossesAgainst, "backfilled local losses against");
            False(
                firstSnapshot.Players.Any(static player => player.PlayerName == "Skipped History"),
                "a later native-only row does not gain unproven old import details");
            True(
                store.TryGetObservedPlayerRecord(
                    1003,
                    "Skipped History",
                    21,
                    false,
                    false,
                    out var skipped),
                "later native-only participant record remains available by HMAC");
            Equal(0L, skipped.Wins, "skipped historical wins are not added");
            Equal(1L, skipped.Losses, "native-only participant loss remains exact");

            True(store.TryGetPvpStatsImportPlan(1003, out var secondPlan), "post-backfill plan");
            True(secondPlan.AlreadyImported, "details backfill becomes one-shot");
            True(
                store.TryMergePvpStatsHistory(
                    1003,
                    secondPlan.StoreGeneration,
                    secondPlan.ImportBeforeUnixSecondsExclusive,
                    ImportResult(),
                    out var second),
                "repeated backfill is a no-op");
            True(second.Success && second.AlreadyImported, "repeated backfill result");

            True(
                store.TryGetObservedPlayerRecord(
                    1003,
                    "Remote Alpha",
                    21,
                    false,
                    false,
                    out var afterSecond),
                "participant record after repeated backfill");
            Equal(2L, afterSecond.Wins, "repeated backfill keeps participant wins");
            Equal(1L, afterSecond.Losses, "repeated backfill keeps participant losses");
            var secondSnapshot = store.GetPlayerStatisticsSnapshot(1003);
            Equal(1L, secondSnapshot.Players[0].WinsAgainst, "repeated backfill keeps local wins against");
            Equal(1L, secondSnapshot.Players[0].LossesAgainst, "repeated backfill keeps local losses against");
        });
}

static void SchemaOneMigrationIsImportable()
{
    WithPreparedStore(
        CreateSchemaOneDocument,
        (store, _) =>
        {
            True(store.TryGetPvpStatsImportPlan(1003, out var plan), "schema-1 import plan");
            Equal(long.MaxValue, plan.ImportBeforeUnixSecondsExclusive, "no old player-history overlap");
            False(plan.AlreadyImported, "schema-1 has no import marker");
        });
}

static void SchemaTwoPlayersFailClosed()
{
    var salt = Enumerable.Range(97, 32).Select(static value => (byte)value).ToArray();
    var legacyPlayerKey = HashCharacter(salt, 2001);
    WithPreparedStore(
        directory => CreateSchemaTwoDocument(directory, salt, legacyPlayerKey),
        (store, directory) =>
        {
            True(
                store.TryGetStatistics(
                    1003,
                    CrystallineConflictArena.TheBaysideBattleground,
                    out var map),
                "schema-2 map record");
            Equal(3L, map.Wins, "preserved schema-2 map wins");
            Equal(2L, map.Losses, "preserved schema-2 map losses");

            True(
                store.TryGetObservedPlayerRecord(
                    1003,
                    "Local Tester",
                    21,
                    true,
                    true,
                    out var overall),
                "schema-2 own overall record");
            Equal(7L, overall.Wins, "preserved schema-2 overall wins");
            Equal(5L, overall.Losses, "preserved schema-2 overall losses");
            False(
                store.TryGetObservedPlayerRecord(
                    1003,
                    "Enemy One",
                    21,
                    false,
                    false,
                    out _),
                "unresolvable schema-2 player is not current history");

            True(store.TryGetPvpStatsImportPlan(1003, out var plan), "schema-2 import plan");
            Equal(long.MaxValue, plan.ImportBeforeUnixSecondsExclusive, "discarded rows do not block import");

            True(store.TryRecord(Sample(60_000), false, true), "native history after schema-2 load");
            using var saved = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(directory, "cc-map-stats.json")));
            var character = saved.RootElement
                .GetProperty("Characters")
                .GetProperty(HashCharacter(salt, 1003));
            var observed = character.GetProperty("ObservedPlayers");
            Equal(9, observed.EnumerateObject().Count(), "only current native opponent/ally rows saved");
            False(observed.TryGetProperty(legacyPlayerKey, out _), "legacy Content-ID key discarded");
            True(
                observed.TryGetProperty(HashIdentity(salt, "21|ENEMY ONE"), out _),
                "current name/world key saved");
            Equal(8L, character.GetProperty("Overall").GetProperty("Wins").GetInt64(), "overall survives next save");
            Equal(5L, character.GetProperty("Overall").GetProperty("Losses").GetInt64(), "overall losses survive next save");
        });
}

static void UnknownPlayerEpochFailsClosed()
{
    WithPreparedStore(
        CreateUnknownEpochSchemaFourDocument,
        (store, _) =>
        {
            True(store.TryGetPvpStatsImportPlan(1003, out var plan), "unknown-epoch plan");
            Equal(0L, plan.ImportBeforeUnixSecondsExclusive, "unsafe import boundary");
        });
}

static void LiteDbLocalDateTimeUsesUtcInstant()
{
    var utc = new DateTime(2026, 7, 1, 13, 34, 57, DateTimeKind.Utc);
    var local = utc.ToLocalTime();
    True(
        CrystallineConflictPvpStatsHistoryReader.TryConvertBsonDateTimeToUnixSeconds(
            local,
            out var actual),
        "local BSON datetime");
    Equal(new DateTimeOffset(utc).ToUnixTimeSeconds(), actual, "same UTC instant");
}

static void PvpStatsReaderAcceptsCurrentLiteDbShape()
{
    var directory = Path.Combine(
        Path.GetTempPath(),
        $"SeitonSense.Plugin.PvpStatsReader.{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var databasePath = Path.Combine(directory, "data.db");
        using (var database = new LiteDatabase(databasePath))
        {
            var teams = new BsonDocument
            {
                ["Astra"] = PvpStatsTeam(
                    "Astra",
                    ["Local Tester", "Ally One", "Ally Two", "Ally Three", "Ally Four"]),
                ["Umbra"] = PvpStatsTeam(
                    "Umbra",
                    ["Enemy One", "Enemy Two", "Enemy Three", "Enemy Four", "Enemy Five"]),
            };
            database.GetCollection<BsonDocument>("ccmatch").Insert(
                new BsonDocument
                {
                    ["IsCompleted"] = true,
                    ["IsDeleted"] = false,
                    ["IsQuarantined"] = false,
                    ["MatchType"] = "Ranked",
                    ["MatchWinner"] = "Astra",
                    ["MatchEndTime"] = new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc),
                    ["LocalPlayer"] = PvpStatsAlias("Local Tester"),
                    ["Teams"] = teams,
                });
        }

        var result = CrystallineConflictPvpStatsHistoryReader.ReadAsync(
                databasePath,
                "Local Tester",
                "Alpha",
                new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Alpha"] = 21,
                },
                long.MaxValue,
                progress: null,
                CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        True(result.Success, result.Status);
        Equal(1, result.MatchesImported, "imported match count");
        Equal(9, result.Players.Count, "remote player count");
        Equal(1L, result.LocalWins, "local win count");
        Equal(0L, result.LocalLosses, "local loss count");
        var enemy = result.Players.Single(static player => player.PlayerName == "Enemy One");
        Equal(0L, enemy.Wins, "enemy participant loss orientation");
        Equal(1L, enemy.Losses, "enemy participant loss count");
        Equal(1L, enemy.WinsAgainst, "local win against imported enemy");
        Equal(0L, enemy.LossesAgainst, "no local loss against imported enemy");
        var ally = result.Players.Single(static player => player.PlayerName == "Ally One");
        Equal(1L, ally.Wins, "ally participant win orientation");
        Equal(0L, ally.Losses, "ally participant loss count");
        Equal(0L, ally.WinsAgainst, "ally result is not head-to-head");
        Equal(0L, ally.LossesAgainst, "ally loss is not head-to-head");
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static BsonDocument PvpStatsTeam(string teamName, IEnumerable<string> playerNames) =>
    new()
    {
        ["TeamName"] = teamName,
        ["Players"] = new BsonArray(
            playerNames.Select(
                static name => (BsonValue)new BsonDocument
                {
                    ["Alias"] = PvpStatsAlias(name),
                })),
    };

static BsonDocument PvpStatsAlias(string playerName) =>
    new()
    {
        ["Name"] = playerName,
        ["HomeWorld"] = "Alpha",
    };

static void FullImportStillAcceptsNativeMatch()
{
    var salt = Enumerable.Range(65, 32).Select(static value => (byte)value).ToArray();
    var oldKeys = Enumerable.Range(0, 4_096)
        .Select(index => HashIdentity(salt, $"21|ARCHIVED PLAYER {index:D4}"))
        .ToArray();
    var expectedEvictions = oldKeys
        .OrderByDescending(static key => key, StringComparer.Ordinal)
        .Take(9)
        .ToArray();

    WithPreparedStore(
        directory => CreateFullImportedHistoryDocument(directory, salt, oldKeys),
        (store, directory) =>
        {
            True(store.TryRecord(Sample(60_000), false, true), "native match after full import");
            True(
                store.TryGetObservedPlayerRecord(
                    1003,
                    "Local Tester",
                    21,
                    true,
                    true,
                    out var local),
                "local record");
            Equal(1L, local.Wins, "local native win");
            Equal(0L, local.Losses, "local native loss");

            foreach (var participant in Participants().Where(static player => player.ContentId != 1003))
            {
                True(
                    store.TryGetObservedPlayerRecord(
                        1003,
                        participant.PlayerName,
                        participant.WorldId,
                        false,
                        participant.Team == 0,
                        out var observed),
                    $"current player {participant.ContentId}");
                Equal(1L, observed.Matches, $"current player matches {participant.ContentId}");
            }

            using var saved = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(directory, "cc-map-stats.json")));
            var observedPlayers = saved.RootElement
                .GetProperty("Characters")
                .GetProperty(HashCharacter(salt, 1003))
                .GetProperty("ObservedPlayers");
            Equal(4_096, observedPlayers.EnumerateObject().Count(), "bounded player count");
            foreach (var evicted in expectedEvictions)
                False(observedPlayers.TryGetProperty(evicted, out _), "deterministic least-useful eviction");
        });
}

static void RejectedGuardSpamRestoresOriginalAttempt()
{
    const uint territoryId = 250;
    const ulong localGameObjectId = 0x1001;
    const uint localEntityId = 0x2001;
    var original = new LocalGuardActionAttempt(
        territoryId,
        localGameObjectId,
        localEntityId,
        ObservedAtMilliseconds: 1_000,
        GuardActivatedAtMilliseconds: 1_150,
        Generation: 1);

    var afterSecondPress = NearAssistRedirector.RestorePreviousLocalGuardAttempt(
        original,
        territoryId,
        localGameObjectId,
        localEntityId);
    True(afterSecondPress is { Generation: 1 }, "second rejected press restores original attempt");
    True(
        afterSecondPress is { GuardActivatedAtMilliseconds: 1_150 },
        "second rejected press preserves the original activation timestamp");

    // A rejected replacement advances the global generation even though the
    // accepted original is restored. A third press must therefore preserve the
    // same older generation instead of erasing it.
    var afterThirdPress = NearAssistRedirector.RestorePreviousLocalGuardAttempt(
        afterSecondPress,
        territoryId,
        localGameObjectId,
        localEntityId);
    True(afterThirdPress is { Generation: 1 }, "third rejected press still restores original attempt");

    True(
        NearAssistRedirector.RestorePreviousLocalGuardAttempt(
            original,
            territoryId + 1,
            localGameObjectId,
            localEntityId) is null,
        "territory mismatch fails closed");
    True(
        NearAssistRedirector.RestorePreviousLocalGuardAttempt(
            original,
            territoryId,
            localGameObjectId + 1,
            localEntityId) is null,
        "object identity mismatch fails closed");
    True(
        NearAssistRedirector.RestorePreviousLocalGuardAttempt(
            original with { Generation = 0 },
            territoryId,
            localGameObjectId,
            localEntityId) is null,
        "invalid generation fails closed");
}

static CapturedMapResult Sample(long capturedAt) => SampleWithParticipants(
    capturedAt,
    1,
    Participants());

static CapturedMapResult SampleWithParticipants(
    long capturedAt,
    byte result,
    CapturedMapResultParticipant[] participants) => new(
    true,
    1293,
    1003,
    result,
    355,
    1000,
    0,
    capturedAt,
    participants);

static CapturedMapResultParticipant[] Participants() =>
[
    Player(1001, "Ally One", 19, 0),
    Player(1002, "Ally Two", 21, 0),
    Player(1003, "Local Tester", 35, 0),
    Player(1004, "Ally Four", 24, 0),
    Player(1005, "Ally Five", 30, 0),
    Player(2001, "Enemy One", 32, 1),
    Player(2002, "Enemy Two", 23, 1),
    Player(2003, "Enemy Three", 25, 1),
    Player(2004, "Enemy Four", 34, 1),
    Player(2005, "Enemy Five", 39, 1),
];

static CapturedMapResultParticipant Player(
    ulong contentId,
    string name,
    byte job,
    byte team) =>
    new(contentId, name, 21, job, team, 1, 0, 2, 100_000, 50_000, 20_000, 10);

static PvpStatsHistoryReadResult ImportResult() => new(
    true,
    "test",
    3,
    3,
    0,
    2,
    1,
    40_000,
    [new PvpStatsObservedPlayerAggregate("Remote Alpha", 21, 2, 1, 1, 1, 3, 40_000)]);

static PvpStatsHistoryReadResult ImportResultWithSkippedHistoricalPlayer() =>
    ImportResult() with
    {
        Players =
        [
            new PvpStatsObservedPlayerAggregate("Remote Alpha", 21, 2, 1, 1, 1, 3, 40_000),
            new PvpStatsObservedPlayerAggregate("Skipped History", 21, 2, 1, 1, 1, 3, 40_000),
        ],
    };

static PvpStatsHistoryReadResult ImportResultWithTimes(long latestMatch, long playerLastSeen) =>
    ImportResult() with
    {
        LatestMatchUnixSeconds = latestMatch,
        Players =
        [
            new PvpStatsObservedPlayerAggregate(
                "Remote Alpha",
                21,
                2,
                1,
                1,
                1,
                3,
                playerLastSeen),
        ],
    };

static string CreateSchemaOneDocument(string directory)
{
    var salt = Enumerable.Range(1, 32).Select(static value => (byte)value).ToArray();
    var characterKey = HashCharacter(salt, 1003);
    var json = $$"""
    {
      "Schema": 1,
      "Salt": "{{Convert.ToBase64String(salt)}}",
      "Characters": {
        "{{characterKey}}": {
          "Maps": { "TheBaysideBattleground": { "Wins": 2, "Losses": 1 } },
          "RecentResults": []
        }
      }
    }
    """;
    var path = Path.Combine(directory, "cc-map-stats.json");
    File.WriteAllText(path, json);
    return path;
}

static string CreateSchemaTwoDocument(
    string directory,
    byte[] salt,
    string legacyPlayerKey)
{
    var characterKey = HashCharacter(salt, 1003);
    var json = $$"""
    {
      "Schema": 2,
      "Salt": "{{Convert.ToBase64String(salt)}}",
      "Characters": {
        "{{characterKey}}": {
          "Overall": { "Wins": 7, "Losses": 5 },
          "Maps": { "TheBaysideBattleground": { "Wins": 3, "Losses": 2 } },
          "ObservedPlayers": {
            "{{legacyPlayerKey}}": {
              "AllyWins": 2,
              "AllyLosses": 1,
              "EnemyWins": 3,
              "EnemyLosses": 4
            }
          },
          "RecentResults": []
        }
      }
    }
    """;
    var path = Path.Combine(directory, "cc-map-stats.json");
    File.WriteAllText(path, json);
    return path;
}

static string CreateUnknownEpochSchemaFourDocument(string directory)
{
    var salt = Enumerable.Range(33, 32).Select(static value => (byte)value).ToArray();
    var characterKey = HashCharacter(salt, 1003);
    var playerKey = HashIdentity(salt, "21|REMOTE ALPHA");
    var json = $$"""
    {
      "Schema": 4,
      "Salt": "{{Convert.ToBase64String(salt)}}",
      "Characters": {
        "{{characterKey}}": {
          "Overall": { "Wins": 1, "Losses": 0 },
          "Maps": {},
          "ObservedPlayers": {
            "{{playerKey}}": { "Wins": 1, "Losses": 0 }
          },
          "RecentResults": [],
          "PlayerHistoryStartedAtUnixSeconds": 0,
          "PvpStatsHistoryImported": false,
          "PvpStatsImportedMatches": 0,
          "PvpStatsImportedPlayers": 0,
          "PvpStatsImportedAtUnixSeconds": 0,
          "PvpStatsImportBeforeUnixSecondsExclusive": 0
        }
      }
    }
    """;
    var path = Path.Combine(directory, "cc-map-stats.json");
    File.WriteAllText(path, json);
    return path;
}

static string CreateSchemaFourImportedHistoryDocument(string directory)
{
    var salt = Enumerable.Range(129, 32).Select(static value => (byte)value).ToArray();
    var characterKey = HashCharacter(salt, 1003);
    var playerKey = HashIdentity(salt, "21|REMOTE ALPHA");
    var skippedPlayerKey = HashIdentity(salt, "21|SKIPPED HISTORY");
    var json = $$"""
    {
      "Schema": 4,
      "Salt": "{{Convert.ToBase64String(salt)}}",
      "Characters": {
        "{{characterKey}}": {
          "Overall": { "Wins": 2, "Losses": 1 },
          "Maps": {},
          "ObservedPlayers": {
            "{{playerKey}}": { "Wins": 2, "Losses": 1 },
            "{{skippedPlayerKey}}": { "Wins": 0, "Losses": 1 }
          },
          "RecentResults": [],
          "PlayerHistoryStartedAtUnixSeconds": 0,
          "PvpStatsHistoryImported": true,
          "PvpStatsImportedMatches": 3,
          "PvpStatsImportedPlayers": 1,
          "PvpStatsImportedAtUnixSeconds": 50000,
          "PvpStatsImportBeforeUnixSecondsExclusive": 9223372036854775807
        }
      }
    }
    """;
    var path = Path.Combine(directory, "cc-map-stats.json");
    File.WriteAllText(path, json);
    return path;
}

static string CreateFullImportedHistoryDocument(
    string directory,
    byte[] salt,
    IEnumerable<string> oldKeys)
{
    var observedPlayers = oldKeys.ToDictionary(
        static key => key,
        static _ => new { Wins = 1L, Losses = 0L },
        StringComparer.Ordinal);
    var document = new
    {
        Schema = 4,
        Salt = Convert.ToBase64String(salt),
        Characters = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [HashCharacter(salt, 1003)] = new
            {
                Overall = new { Wins = 0L, Losses = 0L },
                Maps = new Dictionary<string, object>(StringComparer.Ordinal),
                ObservedPlayers = observedPlayers,
                RecentResults = Array.Empty<object>(),
                PlayerHistoryStartedAtUnixSeconds = 40_000L,
                PvpStatsHistoryImported = true,
                PvpStatsImportedMatches = 10_000,
                PvpStatsImportedPlayers = 4_096,
                PvpStatsImportedAtUnixSeconds = 50_000L,
                PvpStatsImportBeforeUnixSecondsExclusive = 40_000L,
            },
        },
    };
    var path = Path.Combine(directory, "cc-map-stats.json");
    File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(document));
    return path;
}

static string HashCharacter(byte[] salt, ulong contentId)
{
    Span<byte> value = stackalloc byte[sizeof(ulong)];
    BinaryPrimitives.WriteUInt64LittleEndian(value, contentId);
    return Hash(salt, value);
}

static string HashIdentity(byte[] salt, string identity) =>
    Hash(salt, Encoding.UTF8.GetBytes(identity));

static string Hash(byte[] salt, ReadOnlySpan<byte> value)
{
    using var hmac = new HMACSHA256(salt);
    return Convert.ToBase64String(hmac.ComputeHash(value.ToArray()));
}

static void WithStore(Action<CrystallineConflictMapStatisticsStore, string> test) =>
    WithPreparedStore(_ => string.Empty, test);

static void WithPreparedStore(
    Func<string, string> prepare,
    Action<CrystallineConflictMapStatisticsStore, string> test)
{
    var directory = Path.Combine(Path.GetTempPath(), $"SeitonSense.Plugin.SelfTest.{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        _ = prepare(directory);
        var store = new CrystallineConflictMapStatisticsStore(directory, null!);
        True(store.StorageAvailable, "store available");
        test(store, directory);
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static void True(bool value, string message)
{
    if (!value) throw new InvalidOperationException(message);
}

static void False(bool value, string message) => True(!value, message);

static void Equal<T>(T expected, T actual, string message)
    where T : notnull
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
}
