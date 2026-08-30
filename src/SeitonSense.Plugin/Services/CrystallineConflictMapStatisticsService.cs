using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using Dalamud.Hooking;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using SeitonSense.Core;

namespace SeitonSense.Plugin.Services;

internal sealed class CrystallineConflictMapStatisticsService : IDisposable
{
    private const string MatchEndSignature =
        "40 55 53 56 57 41 54 41 55 41 56 41 57 48 8D AC 24 ?? ?? ?? ?? " +
        "48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 85 ?? ?? ?? ?? 0F B6 42";
    private const int MaximumQueuedResults = 16;

    private delegate void MatchEndDelegate(nint director, nint results, nint value, uint unknown);

    private readonly IClientState clientState;
    private readonly IPlayerState playerState;
    private readonly IFramework framework;
    private readonly IPluginLog log;
    private readonly Func<bool> pluginEnabled;
    private readonly Func<bool> captureEnabled;
    private readonly Func<bool> instantLeaveEnabled;
    private readonly CrystallineConflictMapStatisticsStore store;
    private readonly ConcurrentQueue<CapturedMapResultBoundary> pendingResults = new();
    private Hook<MatchEndDelegate>? matchEndHook;
    private int queuedResultCount;
    private long resetGeneration;
    private volatile bool disposed;

    internal CrystallineConflictMapStatisticsService(
        IDalamudPluginInterface pluginInterface,
        IClientState clientState,
        IPlayerState playerState,
        IFramework framework,
        IGameInteropProvider interop,
        IPluginLog log,
        Func<bool> pluginEnabled,
        Func<bool> captureEnabled,
        Func<bool> instantLeaveEnabled)
    {
        this.clientState = clientState;
        this.playerState = playerState;
        this.framework = framework;
        this.log = log;
        this.pluginEnabled = pluginEnabled;
        this.captureEnabled = captureEnabled;
        this.instantLeaveEnabled = instantLeaveEnabled;
        store = new CrystallineConflictMapStatisticsStore(
            pluginInterface.GetPluginConfigDirectory(),
            log);

        framework.Update += OnFrameworkUpdate;
        try
        {
            matchEndHook = interop.HookFromSignature<MatchEndDelegate>(
                MatchEndSignature,
                OnMatchEnd);
            matchEndHook.Enable();
            CaptureAvailable = true;
        }
        catch (Exception exception)
        {
            CaptureAvailable = false;
            log.Warning(
                exception,
                "The shared CC result capture is unavailable; local statistics remain readable and instant leave stays inactive.");
        }
    }

    internal bool CaptureAvailable { get; }
    internal bool StorageAvailable => store.StorageAvailable;
    internal event Action<ConfirmedCrystallineConflictResultBoundary>? ConfirmedResult;

    internal bool TryGetStatistics(
        ulong localContentId,
        CrystallineConflictArena arena,
        out CrystallineConflictMapWinLossSnapshot statistics) =>
        store.TryGetStatistics(localContentId, arena, out statistics);

    internal bool TryReset()
    {
        if (disposed || !store.TryReset()) return false;

        Interlocked.Increment(ref resetGeneration);
        while (pendingResults.TryDequeue(out _))
            Interlocked.Decrement(ref queuedResultCount);
        return true;
    }

    public void Dispose()
    {
        disposed = true;
        framework.Update -= OnFrameworkUpdate;
        matchEndHook?.Dispose();
        ConfirmedResult = null;
        while (pendingResults.TryDequeue(out _))
            Interlocked.Decrement(ref queuedResultCount);
    }

    private unsafe void OnMatchEnd(nint director, nint results, nint value, uint unknown)
    {
        CapturedMapResultBoundary? captured = null;
        try
        {
            if (!disposed &&
                CrystallineConflictInstantLeaveRules.ShouldObserveResult(
                    pluginEnabled(),
                    captureEnabled(),
                    instantLeaveEnabled()) &&
                results != nint.Zero)
            {
                var capturedResetGeneration = Volatile.Read(ref resetGeneration);
                var capturedIsPvpExcludingWolvesDen = clientState.IsPvPExcludingDen;
                var capturedTerritoryId = clientState.TerritoryType;
                var capturedLocalContentId = playerState.ContentId;
                var packet = *(CrystallineConflictMapResultPacket*)results;
                var participants = new CapturedMapResultParticipant[
                    CrystallineConflictMapStatisticsRules.ExpectedParticipantCount];
                var players = packet.PlayerSpan;
                for (var index = 0; index < participants.Length; index++)
                {
                    ref var player = ref players[index];
                    participants[index] = new CapturedMapResultParticipant(
                        player.ContentId,
                        player.ClassJobId,
                        player.Team,
                        player.Kills,
                        player.Deaths,
                        player.Assists,
                        player.DamageDealt,
                        player.DamageTaken,
                        player.HpRestored,
                        player.TimeOnCrystal);
                }

                captured = new CapturedMapResultBoundary(
                    capturedIsPvpExcludingWolvesDen,
                    capturedTerritoryId,
                    capturedLocalContentId,
                    packet.Result,
                    packet.MatchLength,
                    packet.AstraProgress,
                    packet.UmbraProgress,
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    Environment.TickCount64,
                    capturedResetGeneration,
                    participants);
            }
        }
        catch (Exception exception)
        {
            log.Error(exception, "Failed to copy the local CC post-match result boundary.");
        }
        finally
        {
            matchEndHook!.Original(director, results, value, unknown);
            if (captured is { } result && !disposed) Enqueue(result);
        }
    }

    private void Enqueue(CapturedMapResultBoundary result)
    {
        var depth = Interlocked.Increment(ref queuedResultCount);
        if (depth > MaximumQueuedResults)
        {
            Interlocked.Decrement(ref queuedResultCount);
            log.Warning("Dropped a local CC map result because the bounded capture queue was full.");
            return;
        }

        pendingResults.Enqueue(result);
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        while (!disposed && pendingResults.TryDequeue(out var result))
        {
            Interlocked.Decrement(ref queuedResultCount);
            try
            {
                var enabled = pluginEnabled();
                var shouldRecord = enabled && captureEnabled();
                var shouldInstantLeave = enabled && instantLeaveEnabled();
                if (!shouldRecord && !shouldInstantLeave) continue;
                if (!CrystallineConflictMapStatisticsRules.IsExactFrameworkDrainBoundary(
                        result.ResetGeneration,
                        Volatile.Read(ref resetGeneration),
                        result.IsPvpExcludingWolvesDen,
                        result.TerritoryId,
                        result.LocalContentId,
                        clientState.IsPvPExcludingDen,
                        clientState.TerritoryType,
                        playerState.ContentId))
                {
                    continue;
                }

                var sample = new CapturedMapResult(
                    result.IsPvpExcludingWolvesDen,
                    result.TerritoryId,
                    result.LocalContentId,
                    result.Result,
                    result.MatchLength,
                    result.AstraProgress,
                    result.UmbraProgress,
                    result.CapturedAtUnixSeconds,
                    result.Participants);
                var identities = sample.Participants
                    .Select(static participant => new CrystallineConflictMapParticipantIdentity(
                        participant.ContentId,
                        participant.ClassJobId,
                        participant.Team))
                    .ToArray();
                if (!CrystallineConflictMapStatisticsRules.TryConfirmResult(
                        sample.IsPvpExcludingWolvesDen,
                        sample.TerritoryId,
                        sample.Result,
                        sample.MatchLength,
                        sample.LocalContentId,
                        identities,
                        out var confirmedResult))
                {
                    continue;
                }

                // Keep the local record causally ahead of the convenience leave.
                if (shouldRecord)
                {
                    try
                    {
                        store.TryRecord(sample);
                    }
                    catch (Exception exception)
                    {
                        log.Error(
                            exception,
                            "A confirmed local CC result could not be persisted; independent consumers remain available.");
                    }
                }

                if (!shouldInstantLeave) continue;

                try
                {
                    ConfirmedResult?.Invoke(new ConfirmedCrystallineConflictResultBoundary(
                        result.IsPvpExcludingWolvesDen,
                        result.TerritoryId,
                        result.LocalContentId,
                        result.CapturedAtMilliseconds));
                }
                catch (Exception exception)
                {
                    log.Error(exception, "A confirmed CC result consumer failed closed.");
                }
            }
            catch (Exception exception)
            {
                log.Error(exception, "A local CC map result failed closed before persistence.");
            }
        }
    }
}

internal readonly record struct CapturedMapResultParticipant(
    ulong ContentId,
    byte ClassJobId,
    byte Team,
    byte Kills,
    byte Deaths,
    byte Assists,
    int DamageDealt,
    int DamageTaken,
    int HpRestored,
    ushort TimeOnCrystal);

internal readonly record struct CapturedMapResultBoundary(
    bool IsPvpExcludingWolvesDen,
    uint TerritoryId,
    ulong LocalContentId,
    byte Result,
    ushort MatchLength,
    uint AstraProgress,
    uint UmbraProgress,
    long CapturedAtUnixSeconds,
    long CapturedAtMilliseconds,
    long ResetGeneration,
    CapturedMapResultParticipant[] Participants);

internal readonly record struct ConfirmedCrystallineConflictResultBoundary(
    bool IsPvpExcludingWolvesDen,
    uint TerritoryId,
    ulong LocalContentId,
    long CapturedAtMilliseconds);

internal readonly record struct CapturedMapResult(
    bool IsPvpExcludingWolvesDen,
    uint TerritoryId,
    ulong LocalContentId,
    byte Result,
    ushort MatchLength,
    uint AstraProgress,
    uint UmbraProgress,
    long CapturedAtUnixSeconds,
    CapturedMapResultParticipant[] Participants);

internal sealed class CrystallineConflictMapStatisticsStore
{
    private const int CurrentSchema = 1;
    private const int SaltLength = 32;
    private const int MaximumCharacters = 128;
    private const int MaximumRecentResults = 32;
    private const long DuplicateWindowSeconds = 30;
    private const string FileName = "cc-map-stats.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string filePath;
    private readonly IPluginLog log;
    private MapStatisticsDocument document;
    private byte[] salt;
    private ulong cachedContentId;
    private string cachedCharacterKey = string.Empty;

    internal CrystallineConflictMapStatisticsStore(string configDirectory, IPluginLog log)
    {
        this.log = log;
        filePath = Path.Combine(configDirectory, FileName);
        document = CreateEmptyDocument();
        salt = Convert.FromBase64String(document.Salt);
        StorageAvailable = TryLoad();
    }

    internal bool StorageAvailable { get; private set; }

    internal bool TryGetStatistics(
        ulong localContentId,
        CrystallineConflictArena arena,
        out CrystallineConflictMapWinLossSnapshot statistics)
    {
        statistics = default;
        if (!StorageAvailable || localContentId == 0) return false;

        var characterKey = GetCharacterKey(localContentId);
        return document.Characters.TryGetValue(characterKey, out var character) &&
               character.Maps.TryGetValue(arena.ToString(), out var record) &&
               CrystallineConflictMapStatisticsRules.TryCreateSnapshot(
                   record.Wins,
                   record.Losses,
                   out statistics) &&
               statistics.HasData;
    }

    internal bool TryRecord(CapturedMapResult sample)
    {
        if (!StorageAvailable) return false;
        var identities = sample.Participants
            .Select(static participant => new CrystallineConflictMapParticipantIdentity(
                participant.ContentId,
                participant.ClassJobId,
                participant.Team))
            .ToArray();
        if (!CrystallineConflictMapStatisticsRules.TryConfirmResult(
                sample.IsPvpExcludingWolvesDen,
                sample.TerritoryId,
                sample.Result,
                sample.MatchLength,
                sample.LocalContentId,
                identities,
                out var confirmed))
        {
            return false;
        }

        var characterKey = GetCharacterKey(sample.LocalContentId);
        var fingerprint = ComputeResultFingerprint(sample);
        var candidate = Clone(document);
        if (!candidate.Characters.TryGetValue(characterKey, out var character))
        {
            if (candidate.Characters.Count >= MaximumCharacters) return false;
            character = new MapCharacterStatistics();
            candidate.Characters.Add(characterKey, character);
        }

        if (character.RecentResults.Any(result =>
                string.Equals(result.Fingerprint, fingerprint, StringComparison.Ordinal) &&
                Math.Abs((double)result.CapturedAtUnixSeconds - sample.CapturedAtUnixSeconds) <=
                DuplicateWindowSeconds))
        {
            log.Warning("Ignored a duplicate local CC map result payload.");
            return false;
        }

        var arenaKey = confirmed.Arena.ToString();
        if (!character.Maps.TryGetValue(arenaKey, out var record))
        {
            record = new MapWinLossRecord();
            character.Maps.Add(arenaKey, record);
        }

        if (!CrystallineConflictMapStatisticsRules.TryCreateSnapshot(
                record.Wins,
                record.Losses,
                out var existing) ||
            existing.Matches == long.MaxValue)
        {
            return false;
        }

        if (confirmed.IsWin)
        {
            record.Wins++;
        }
        else
        {
            record.Losses++;
        }

        character.RecentResults.Add(new MapRecentResult
        {
            Fingerprint = fingerprint,
            CapturedAtUnixSeconds = sample.CapturedAtUnixSeconds,
        });
        character.RecentResults = character.RecentResults
            .OrderByDescending(static result => result.CapturedAtUnixSeconds)
            .Take(MaximumRecentResults)
            .OrderBy(static result => result.CapturedAtUnixSeconds)
            .ToList();

        if (!TrySave(candidate)) return false;
        document = candidate;
        return true;
    }

    internal bool TryReset()
    {
        var candidate = CreateEmptyDocument();
        if (!TrySave(candidate)) return false;

        document = candidate;
        salt = Convert.FromBase64String(candidate.Salt);
        cachedContentId = 0;
        cachedCharacterKey = string.Empty;
        StorageAvailable = true;
        return true;
    }

    private bool TryLoad()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            if (!File.Exists(filePath)) return true;

            var loaded = JsonSerializer.Deserialize<MapStatisticsDocument>(
                File.ReadAllText(filePath),
                JsonOptions);
            if (loaded is null || !TryValidate(loaded, out var loadedSalt))
            {
                log.Warning("Local CC map statistics were malformed and will not be read or overwritten.");
                return false;
            }

            document = loaded;
            salt = loadedSalt;
            return true;
        }
        catch (Exception exception)
        {
            log.Warning(
                exception,
                "Local CC map statistics could not be loaded and will not be overwritten.");
            return false;
        }
    }

    private bool TrySave(MapStatisticsDocument candidate)
    {
        var temporaryPath = filePath + ".tmp";
        try
        {
            var json = JsonSerializer.Serialize(candidate, JsonOptions);
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, filePath, true);
            return true;
        }
        catch (Exception exception)
        {
            log.Warning(exception, "Local CC map statistics could not be saved atomically.");
            try
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
            catch
            {
                // Preserve the original document and remain fail-closed.
            }

            return false;
        }
    }

    private bool TryValidate(MapStatisticsDocument candidate, out byte[] candidateSalt)
    {
        candidateSalt = [];
        if (candidate.Schema != CurrentSchema ||
            candidate.Characters is null ||
            candidate.Characters.Count > MaximumCharacters)
        {
            return false;
        }

        try
        {
            candidateSalt = Convert.FromBase64String(candidate.Salt);
        }
        catch (FormatException)
        {
            return false;
        }

        if (candidateSalt.Length != SaltLength) return false;
        foreach (var pair in candidate.Characters)
        {
            if (!IsHash(pair.Key) ||
                pair.Value is null ||
                pair.Value.Maps is null ||
                pair.Value.Maps.Count > CrystallineConflictRotationRules.ArenaCount ||
                pair.Value.RecentResults is null ||
                pair.Value.RecentResults.Count > MaximumRecentResults)
            {
                return false;
            }

            foreach (var map in pair.Value.Maps)
            {
                if (!Enum.TryParse<CrystallineConflictArena>(map.Key, false, out var arena) ||
                    !Enum.IsDefined(arena) ||
                    map.Value is null ||
                    !CrystallineConflictMapStatisticsRules.TryCreateSnapshot(
                        map.Value.Wins,
                        map.Value.Losses,
                        out _))
                {
                    return false;
                }
            }

            if (pair.Value.RecentResults.Any(result =>
                    result is null ||
                    !IsHash(result.Fingerprint) ||
                    result.CapturedAtUnixSeconds <= 0))
            {
                return false;
            }
        }

        return true;
    }

    private string ComputeCharacterKey(ulong contentId)
    {
        Span<byte> buffer = stackalloc byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64LittleEndian(buffer, contentId);
        return ComputeHash(buffer);
    }

    private string GetCharacterKey(ulong contentId)
    {
        if (contentId == cachedContentId && !string.IsNullOrEmpty(cachedCharacterKey))
            return cachedCharacterKey;

        cachedContentId = contentId;
        cachedCharacterKey = ComputeCharacterKey(contentId);
        return cachedCharacterKey;
    }

    private string ComputeResultFingerprint(CapturedMapResult sample)
    {
        using var stream = new MemoryStream(512);
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
        {
            writer.Write(sample.TerritoryId);
            writer.Write(sample.Result);
            writer.Write(sample.MatchLength);
            writer.Write(sample.AstraProgress);
            writer.Write(sample.UmbraProgress);
            foreach (var participant in sample.Participants)
            {
                writer.Write(participant.ContentId);
                writer.Write(participant.ClassJobId);
                writer.Write(participant.Team);
                writer.Write(participant.Kills);
                writer.Write(participant.Deaths);
                writer.Write(participant.Assists);
                writer.Write(participant.DamageDealt);
                writer.Write(participant.DamageTaken);
                writer.Write(participant.HpRestored);
                writer.Write(participant.TimeOnCrystal);
            }
        }

        return ComputeHash(stream.ToArray());
    }

    private string ComputeHash(ReadOnlySpan<byte> value)
    {
        using var hmac = new HMACSHA256(salt);
        return Convert.ToBase64String(hmac.ComputeHash(value.ToArray()));
    }

    private static bool IsHash(string value)
    {
        try
        {
            var decoded = Convert.FromBase64String(value);
            return decoded.Length == 32 &&
                   string.Equals(
                       Convert.ToBase64String(decoded),
                       value,
                       StringComparison.Ordinal);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static MapStatisticsDocument CreateEmptyDocument()
    {
        var generatedSalt = RandomNumberGenerator.GetBytes(SaltLength);
        return new MapStatisticsDocument
        {
            Schema = CurrentSchema,
            Salt = Convert.ToBase64String(generatedSalt),
        };
    }

    private static MapStatisticsDocument Clone(MapStatisticsDocument source) => new()
    {
        Schema = source.Schema,
        Salt = source.Salt,
        Characters = source.Characters.ToDictionary(
            static pair => pair.Key,
            static pair => new MapCharacterStatistics
            {
                Maps = pair.Value.Maps.ToDictionary(
                    static map => map.Key,
                    static map => new MapWinLossRecord
                    {
                        Wins = map.Value.Wins,
                        Losses = map.Value.Losses,
                    },
                    StringComparer.Ordinal),
                RecentResults = pair.Value.RecentResults.Select(static result => new MapRecentResult
                {
                    Fingerprint = result.Fingerprint,
                    CapturedAtUnixSeconds = result.CapturedAtUnixSeconds,
                }).ToList(),
            },
            StringComparer.Ordinal),
    };

    private sealed class MapStatisticsDocument
    {
        public int Schema { get; set; }
        public string Salt { get; set; } = string.Empty;
        public Dictionary<string, MapCharacterStatistics> Characters { get; set; } =
            new(StringComparer.Ordinal);
    }

    private sealed class MapCharacterStatistics
    {
        public Dictionary<string, MapWinLossRecord> Maps { get; set; } =
            new(StringComparer.Ordinal);
        public List<MapRecentResult> RecentResults { get; set; } = [];
    }

    private sealed class MapWinLossRecord
    {
        public long Wins { get; set; }
        public long Losses { get; set; }
    }

    private sealed class MapRecentResult
    {
        public string Fingerprint { get; set; } = string.Empty;
        public long CapturedAtUnixSeconds { get; set; }
    }
}
