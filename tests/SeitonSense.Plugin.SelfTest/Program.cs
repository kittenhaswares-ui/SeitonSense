using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SeitonSense.Core;
using SeitonSense.Plugin.Services;

var tests = new (string Name, Action Run)[]
{
    ("PvpStats import cutoff keeps a five-minute no-overlap margin", ImportCutoffUsesFirstNativeEpoch),
    ("PvpStats import rejects every out-of-bound timestamp atomically", ImportBoundariesAreAtomic),
    ("PvpStats merge rejects a stale store generation", StaleGenerationCannotMerge),
    ("PvpStats import is one-shot and persists no raw identity", ImportIsOneShotAndPseudonymous),
    ("schema-1 map history migrates without blocking a fresh import", SchemaOneMigrationIsImportable),
    ("schema-2 player rows are discarded while map and overall W/L survive", SchemaTwoPlayersFailClosed),
    ("unknown old player-history epoch blocks import", UnknownPlayerEpochFailsClosed),
    ("LiteDB local datetime conversion preserves the UTC instant", LiteDbLocalDateTimeUsesUtcInstant),
    ("full imported player history evicts deterministically for one native match", FullImportStillAcceptsNativeMatch),
    ("repeated rejected Guard spam preserves the original attempt", RejectedGuardSpamRestoresOriginalAttempt),
};

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

static void ImportIsOneShotAndPseudonymous()
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

        var json = File.ReadAllText(Path.Combine(directory, "cc-map-stats.json"));
        False(json.Contains("Remote Alpha", StringComparison.OrdinalIgnoreCase), "raw remote name absent");
        False(json.Contains("Local Tester", StringComparison.OrdinalIgnoreCase), "raw local name absent");
        using var document = JsonDocument.Parse(json);
        Equal(4, document.RootElement.GetProperty("Schema").GetInt32(), "store schema");
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
        Generation: 1);

    var afterSecondPress = NearAssistRedirector.RestorePreviousLocalGuardAttempt(
        original,
        territoryId,
        localGameObjectId,
        localEntityId);
    True(afterSecondPress is { Generation: 1 }, "second rejected press restores original attempt");

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

static CapturedMapResult Sample(long capturedAt) => new(
    true,
    1293,
    1003,
    1,
    355,
    1000,
    0,
    capturedAt,
    Participants());

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
    [new PvpStatsObservedPlayerAggregate("Remote Alpha", 21, 2, 1, 3, 40_000)]);

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
    File.WriteAllText(path, JsonSerializer.Serialize(document));
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
