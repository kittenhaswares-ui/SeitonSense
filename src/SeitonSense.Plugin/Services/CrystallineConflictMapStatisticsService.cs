using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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
    private readonly Func<bool> playerHistoryEnabled;
    private readonly Func<bool> predictionPanelEnabled;
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
        Func<bool> instantLeaveEnabled,
        Func<bool> playerHistoryEnabled,
        Func<bool> predictionPanelEnabled)
    {
        this.clientState = clientState;
        this.playerState = playerState;
        this.framework = framework;
        this.log = log;
        this.pluginEnabled = pluginEnabled;
        this.captureEnabled = captureEnabled;
        this.instantLeaveEnabled = instantLeaveEnabled;
        this.playerHistoryEnabled = playerHistoryEnabled;
        this.predictionPanelEnabled = predictionPanelEnabled;
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
    internal event Action<ConfirmedCrystallineConflictMatchResult>? ConfirmedMatch;

    internal bool TryGetStatistics(
        ulong localContentId,
        CrystallineConflictArena arena,
        out CrystallineConflictMapWinLossSnapshot statistics) =>
        store.TryGetStatistics(localContentId, arena, out statistics);

    internal bool TryGetObservedPlayerRecord(
        ulong localContentId,
        string playerName,
        ushort worldId,
        bool isLocalPlayer,
        bool currentlyAllied,
        out CrystallineConflictMapWinLossSnapshot statistics) =>
        store.TryGetObservedPlayerRecord(
            localContentId,
            playerName,
            worldId,
            isLocalPlayer,
            currentlyAllied,
            out statistics);

    internal bool TryGetPvpStatsImportPlan(
        ulong localContentId,
        out PvpStatsHistoryImportPlan plan) =>
        store.TryGetPvpStatsImportPlan(localContentId, out plan);

    internal bool TryMergePvpStatsHistory(
        ulong localContentId,
        long expectedStoreGeneration,
        long importBeforeUnixSecondsExclusive,
        PvpStatsHistoryReadResult import,
        out PvpStatsHistoryMergeResult result) =>
        store.TryMergePvpStatsHistory(
            localContentId,
            expectedStoreGeneration,
            importBeforeUnixSecondsExclusive,
            import,
            out result);

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
        ConfirmedMatch = null;
        while (pendingResults.TryDequeue(out _))
            Interlocked.Decrement(ref queuedResultCount);
    }

    private unsafe void OnMatchEnd(nint director, nint results, nint value, uint unknown)
    {
        CapturedMapResultBoundary? captured = null;
        try
        {
            if (!disposed &&
                pluginEnabled() &&
                (captureEnabled() ||
                 instantLeaveEnabled() ||
                 playerHistoryEnabled() ||
                 predictionPanelEnabled()) &&
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
                    var playerName = player.TryReadPlayerName(out var decodedName)
                        ? decodedName
                        : string.Empty;
                    participants[index] = new CapturedMapResultParticipant(
                        player.ContentId,
                        playerName,
                        player.WorldId,
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
                var shouldRecordMap = enabled && captureEnabled();
                var shouldRecordPlayers = enabled && playerHistoryEnabled();
                var shouldPublishPrediction = enabled && predictionPanelEnabled();
                var shouldInstantLeave = enabled && instantLeaveEnabled();
                if (!shouldRecordMap &&
                    !shouldRecordPlayers &&
                    !shouldPublishPrediction &&
                    !shouldInstantLeave)
                {
                    continue;
                }
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

                var localParticipant = sample.Participants.Single(
                    participant => participant.ContentId == sample.LocalContentId);

                // Publish the validated scoreboard before mutating local history.
                // A prediction consumer that had to recover its roster from the
                // result can therefore freeze the real pre-match opening record;
                // it must never learn from the result it is meant to predict.
                if (shouldRecordPlayers || shouldPublishPrediction)
                {
                    try
                    {
                        ConfirmedMatch?.Invoke(new ConfirmedCrystallineConflictMatchResult(
                            result.IsPvpExcludingWolvesDen,
                            result.TerritoryId,
                            result.LocalContentId,
                            confirmedResult.IsWin,
                            localParticipant.Team,
                            result.MatchLength,
                            result.AstraProgress,
                            result.UmbraProgress,
                            result.CapturedAtMilliseconds,
                            result.Participants));
                    }
                    catch (Exception exception)
                    {
                        log.Error(exception, "A confirmed CC prediction result consumer failed closed.");
                    }
                }

                // Keep the local record causally ahead of the convenience leave.
                if (shouldRecordMap || shouldRecordPlayers)
                {
                    try
                    {
                        store.TryRecord(sample, shouldRecordMap, shouldRecordPlayers);
                    }
                    catch (Exception exception)
                    {
                        log.Error(
                            exception,
                            "A confirmed local CC result could not be persisted; independent consumers remain available.");
                    }
                }

                if (shouldInstantLeave)
                {
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
    string PlayerName,
    ushort WorldId,
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

internal readonly record struct ConfirmedCrystallineConflictMatchResult(
    bool IsPvpExcludingWolvesDen,
    uint TerritoryId,
    ulong LocalContentId,
    bool IsWin,
    byte LocalTeam,
    ushort MatchLength,
    uint AstraProgress,
    uint UmbraProgress,
    long CapturedAtMilliseconds,
    CapturedMapResultParticipant[] Participants);

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

internal readonly record struct PvpStatsHistoryImportPlan(
    bool AlreadyImported,
    long StoreGeneration,
    long ImportBeforeUnixSecondsExclusive,
    int PreviouslyImportedMatches,
    int PreviouslyImportedPlayers);

internal readonly record struct PvpStatsHistoryMergeResult(
    bool Success,
    bool AlreadyImported,
    int ImportedMatches,
    int ImportedPlayers,
    int SkippedPlayers,
    bool ImportedLocalRecord,
    string Status);

internal sealed class CrystallineConflictMapStatisticsStore
{
    private const int CurrentSchema = 4;
    private const int SaltLength = 32;
    private const int MaximumCharacters = 128;
    private const int MaximumRecentResults = 32;
    private const int MaximumObservedPlayersPerCharacter = 4_096;
    private const long DuplicateWindowSeconds = 30;
    private const long PvpStatsImportOverlapSafetySeconds = 300;
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
    private long mutationGeneration;

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

    internal bool TryGetObservedPlayerRecord(
        ulong localContentId,
        string playerName,
        ushort worldId,
        bool isLocalPlayer,
        bool currentlyAllied,
        out CrystallineConflictMapWinLossSnapshot statistics)
    {
        statistics = default;
        if (!StorageAvailable || localContentId == 0) return false;

        var characterKey = GetCharacterKey(localContentId);
        if (!document.Characters.TryGetValue(characterKey, out var character))
            return false;

        if (isLocalPlayer)
        {
            return CrystallineConflictMapStatisticsRules.TryCreateSnapshot(
                       character.Overall.Wins,
                       character.Overall.Losses,
                       out statistics) &&
                   statistics.HasData;
        }

        if (!TryNormalizeObservedIdentity(playerName, worldId, out var identity))
            return false;

        var playerKey = ComputeObservedPlayerKey(identity);
        if (!character.ObservedPlayers.TryGetValue(playerKey, out var record))
            return false;

        // A player's result belongs to that player regardless of which side
        // they happened to be on when we observed it. The relationship input
        // is retained in this internal API so existing callers remain source
        // compatible, but does not fragment the persistent sample.
        _ = currentlyAllied;
        return CrystallineConflictMapStatisticsRules.TryCreateSnapshot(
                   record.Wins,
                   record.Losses,
                   out statistics) &&
                   statistics.HasData;
    }

    internal bool TryGetPvpStatsImportPlan(
        ulong localContentId,
        out PvpStatsHistoryImportPlan plan)
    {
        plan = default;
        if (!StorageAvailable || localContentId == 0) return false;

        var characterKey = GetCharacterKey(localContentId);
        if (!document.Characters.TryGetValue(characterKey, out var character))
        {
            plan = new PvpStatsHistoryImportPlan(
                false,
                mutationGeneration,
                long.MaxValue,
                0,
                0);
            return true;
        }

        if (character.PvpStatsHistoryImported)
        {
            plan = new PvpStatsHistoryImportPlan(
                true,
                mutationGeneration,
                character.PvpStatsImportBeforeUnixSecondsExclusive,
                character.PvpStatsImportedMatches,
                character.PvpStatsImportedPlayers);
            return true;
        }

        plan = new PvpStatsHistoryImportPlan(
            false,
            mutationGeneration,
            character.PlayerHistoryStartedAtUnixSeconds > 0
                ? Math.Max(
                    1,
                    character.PlayerHistoryStartedAtUnixSeconds -
                    PvpStatsImportOverlapSafetySeconds)
                : character.ObservedPlayers.Count == 0
                    ? long.MaxValue
                    : 0,
            0,
            0);
        return true;
    }

    internal bool TryMergePvpStatsHistory(
        ulong localContentId,
        long expectedStoreGeneration,
        long importBeforeUnixSecondsExclusive,
        PvpStatsHistoryReadResult import,
        out PvpStatsHistoryMergeResult result)
    {
        result = default;
        if (!StorageAvailable ||
            localContentId == 0 ||
            expectedStoreGeneration != mutationGeneration ||
            importBeforeUnixSecondsExclusive <= 0 ||
            !import.Success ||
            import.MatchesImported <= 0 ||
            import.LocalWins < 0 ||
            import.LocalLosses < 0 ||
            import.LatestMatchUnixSeconds <= 0 ||
            import.LatestMatchUnixSeconds >= importBeforeUnixSecondsExclusive ||
            import.Players is null ||
            !TryAddCounters(import.LocalWins, import.LocalLosses, out var localMatches) ||
            localMatches != import.MatchesImported)
        {
            result = new PvpStatsHistoryMergeResult(
                false,
                false,
                0,
                0,
                0,
                false,
                "The PvpStats aggregate failed validation; nothing was saved.");
            return false;
        }

        var characterKey = GetCharacterKey(localContentId);
        var candidate = Clone(document);
        if (!candidate.Characters.TryGetValue(characterKey, out var character))
        {
            if (candidate.Characters.Count >= MaximumCharacters)
            {
                result = new PvpStatsHistoryMergeResult(
                    false,
                    false,
                    0,
                    0,
                    0,
                    false,
                    "The local history store already contains its maximum number of characters; nothing was saved.");
                return false;
            }
            character = new MapCharacterStatistics();
            candidate.Characters.Add(characterKey, character);
        }

        if (character.PvpStatsHistoryImported)
        {
            result = new PvpStatsHistoryMergeResult(
                true,
                true,
                character.PvpStatsImportedMatches,
                character.PvpStatsImportedPlayers,
                0,
                false,
                $"PvpStats history was already imported ({character.PvpStatsImportedMatches:N0} matches).");
            return true;
        }

        var normalized = new List<ImportPlayerUpdate>(import.Players.Count);
        var identities = new HashSet<string>(StringComparer.Ordinal);
        foreach (var player in import.Players)
        {
            if (player.Wins < 0 ||
                player.Losses < 0 ||
                player.Matches <= 0 ||
                player.LastSeenUnixSeconds <= 0 ||
                player.LastSeenUnixSeconds > import.LatestMatchUnixSeconds ||
                player.LastSeenUnixSeconds >= importBeforeUnixSecondsExclusive ||
                !TryAddCounters(player.Wins, player.Losses, out var matches) ||
                matches != player.Matches ||
                !TryNormalizeObservedIdentity(player.PlayerName, player.WorldId, out var identity))
            {
                result = new PvpStatsHistoryMergeResult(
                    false,
                    false,
                    0,
                    0,
                    0,
                    false,
                    "One imported player aggregate was invalid; nothing was saved.");
                return false;
            }

            var playerKey = ComputeObservedPlayerKey(identity);
            if (!identities.Add(playerKey))
            {
                result = new PvpStatsHistoryMergeResult(
                    false,
                    false,
                    0,
                    0,
                    0,
                    false,
                    "Imported player identities were ambiguous; nothing was saved.");
                return false;
            }

            normalized.Add(new ImportPlayerUpdate(
                playerKey,
                player.Wins,
                player.Losses,
                player.Matches,
                player.LastSeenUnixSeconds));
        }

        var combinedPlayers = new Dictionary<string, ImportMergePlayerRecord>(
            character.ObservedPlayers.Count + normalized.Count,
            StringComparer.Ordinal);
        foreach (var existing in character.ObservedPlayers)
        {
            if (!CrystallineConflictMapStatisticsRules.TryCreateSnapshot(
                    existing.Value.Wins,
                    existing.Value.Losses,
                    out var existingSnapshot))
            {
                result = new PvpStatsHistoryMergeResult(
                    false,
                    false,
                    0,
                    0,
                    0,
                    false,
                    "Existing player history failed validation; nothing was saved.");
                return false;
            }

            combinedPlayers.Add(
                existing.Key,
                new ImportMergePlayerRecord(
                    existing.Key,
                    existing.Value.Wins,
                    existing.Value.Losses,
                    existingSnapshot.Matches,
                    character.PlayerHistoryStartedAtUnixSeconds,
                    HasImportedContribution: false));
        }

        foreach (var update in normalized)
        {
            if (combinedPlayers.TryGetValue(update.PlayerKey, out var existing))
            {
                if (!TryAddCounters(existing.Wins, update.Wins, out var wins) ||
                    !TryAddCounters(existing.Losses, update.Losses, out var losses) ||
                    !TryAddCounters(existing.Matches, update.Matches, out var matches) ||
                    !CrystallineConflictMapStatisticsRules.TryCreateSnapshot(wins, losses, out _))
                {
                    result = new PvpStatsHistoryMergeResult(
                        false,
                        false,
                        0,
                        0,
                        0,
                        false,
                        "Imported player counters could not be merged; nothing was saved.");
                    return false;
                }

                combinedPlayers[update.PlayerKey] = existing with
                {
                    Wins = wins,
                    Losses = losses,
                    Matches = matches,
                    LastSeenUnixSeconds = Math.Max(
                        existing.LastSeenUnixSeconds,
                        update.LastSeenUnixSeconds),
                    HasImportedContribution = true,
                };
            }
            else
            {
                combinedPlayers.Add(
                    update.PlayerKey,
                    new ImportMergePlayerRecord(
                        update.PlayerKey,
                        update.Wins,
                        update.Losses,
                        update.Matches,
                        update.LastSeenUnixSeconds,
                        HasImportedContribution: true));
            }
        }

        var selectedPlayers = combinedPlayers.Values
            .OrderByDescending(static player => player.Matches)
            .ThenByDescending(static player => player.LastSeenUnixSeconds)
            .ThenBy(static player => player.PlayerKey, StringComparer.Ordinal)
            .Take(MaximumObservedPlayersPerCharacter)
            .ToArray();
        var importedPlayers = selectedPlayers.Count(static player => player.HasImportedContribution);
        var skippedPlayers = normalized.Count - importedPlayers;
        character.ObservedPlayers = selectedPlayers.ToDictionary(
            static player => player.PlayerKey,
            static player => new ObservedPlayerWinLossRecord
            {
                Wins = player.Wins,
                Losses = player.Losses,
            },
            StringComparer.Ordinal);

        if (importedPlayers == 0)
        {
            result = new PvpStatsHistoryMergeResult(
                false,
                false,
                0,
                0,
                skippedPlayers,
                false,
                "No imported player record fit the local history store; nothing was saved.");
            return false;
        }

        if (!CrystallineConflictMapStatisticsRules.TryCreateSnapshot(
                character.Overall.Wins,
                character.Overall.Losses,
                out var existingLocalRecord) ||
            !CrystallineConflictMapStatisticsRules.TryCreateSnapshot(
                import.LocalWins,
                import.LocalLosses,
                out var importedLocalSnapshot))
        {
            result = new PvpStatsHistoryMergeResult(
                false,
                false,
                0,
                0,
                skippedPlayers,
                false,
                "The local W/L aggregate failed validation; nothing was saved.");
            return false;
        }

        // A legacy map-W/L total and a full PvpStats total can overlap. Before
        // native player history starts, keep the larger complete sample rather
        // than adding both. After that epoch, retain Seiton's own record so no
        // pre-epoch import can double-count its legacy portion.
        var importedLocalRecord =
            character.PlayerHistoryStartedAtUnixSeconds == 0 &&
            importedLocalSnapshot.Matches > existingLocalRecord.Matches;
        if (importedLocalRecord)
        {
            character.Overall.Wins = import.LocalWins;
            character.Overall.Losses = import.LocalLosses;
        }

        character.PvpStatsHistoryImported = true;
        character.PvpStatsImportedMatches = import.MatchesImported;
        character.PvpStatsImportedPlayers = importedPlayers;
        character.PvpStatsImportedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        character.PvpStatsImportBeforeUnixSecondsExclusive = importBeforeUnixSecondsExclusive;
        if (!TrySave(candidate))
        {
            result = new PvpStatsHistoryMergeResult(
                false,
                false,
                0,
                0,
                skippedPlayers,
                false,
                "Imported history could not be saved; the old file was left unchanged.");
            return false;
        }

        document = candidate;
        mutationGeneration = NextGeneration(mutationGeneration);
        var ownRecordStatus = importedLocalRecord
            ? " Your own W/L was filled from that history."
            : " Your existing own W/L was kept to avoid duplicate matches.";
        result = new PvpStatsHistoryMergeResult(
            true,
            false,
            import.MatchesImported,
            importedPlayers,
            skippedPlayers,
            importedLocalRecord,
            skippedPlayers == 0
                ? $"Imported {import.MatchesImported:N0} PvpStats matches for {importedPlayers:N0} players.{ownRecordStatus}"
                : $"Imported {import.MatchesImported:N0} matches and kept the {importedPlayers:N0} most useful imported player records; {skippedPlayers:N0} did not fit.{ownRecordStatus}");
        return true;
    }

    internal bool TryRecord(
        CapturedMapResult sample,
        bool recordMap,
        bool recordObservedPlayers)
    {
        if (!StorageAvailable ||
            (!recordMap && !recordObservedPlayers) ||
            sample.CapturedAtUnixSeconds <= 0 ||
            sample.Participants is null)
        {
            return false;
        }

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

        var recent = character.RecentResults
            .Where(result =>
                string.Equals(result.Fingerprint, fingerprint, StringComparison.Ordinal) &&
                Math.Abs((double)result.CapturedAtUnixSeconds - sample.CapturedAtUnixSeconds) <=
                DuplicateWindowSeconds)
            .OrderByDescending(static result => result.CapturedAtUnixSeconds)
            .FirstOrDefault();
        var shouldRecordMap = recordMap && !(recent?.RecordedMap ?? false);
        var shouldRecordPlayers = recordObservedPlayers && !(recent?.RecordedPlayers ?? false);
        if (!shouldRecordMap && !shouldRecordPlayers)
        {
            log.Warning("Ignored a duplicate local CC map result payload.");
            return false;
        }

        var mapRecorded = shouldRecordMap && TryRecordMap(character, confirmed);
        if (shouldRecordMap && !mapRecorded)
        {
            log.Warning("Skipped a confirmed local CC map counter because its bounded counter was exhausted.");
        }

        var playersRecorded = shouldRecordPlayers &&
            TryRecordObservedPlayers(
                character,
                sample.LocalContentId,
                sample.Result,
                sample.Participants);
        if (playersRecorded &&
            (character.PlayerHistoryStartedAtUnixSeconds <= 0 ||
             sample.CapturedAtUnixSeconds < character.PlayerHistoryStartedAtUnixSeconds))
        {
            character.PlayerHistoryStartedAtUnixSeconds = sample.CapturedAtUnixSeconds;
        }
        if (shouldRecordPlayers && !playersRecorded)
        {
            log.Warning("Skipped local CC player history because one or more exact identities or bounded counters were unavailable.");
        }

        // Map and player history are independent consumers of one packet. A
        // bad identity row or a saturated history must not discard a valid map
        // update (and vice versa), while each individual update remains all-or-
        // nothing inside this cloned document.
        if (!mapRecorded && !playersRecorded)
        {
            return false;
        }

        if (recent is null)
        {
            character.RecentResults.Add(new MapRecentResult
            {
                Fingerprint = fingerprint,
                CapturedAtUnixSeconds = sample.CapturedAtUnixSeconds,
                RecordedMap = mapRecorded,
                RecordedPlayers = playersRecorded,
            });
        }
        else
        {
            recent.RecordedMap |= mapRecorded;
            recent.RecordedPlayers |= playersRecorded;
        }

        character.RecentResults = character.RecentResults
            .OrderByDescending(static result => result.CapturedAtUnixSeconds)
            .Take(MaximumRecentResults)
            .OrderBy(static result => result.CapturedAtUnixSeconds)
            .ToList();

        if (!TrySave(candidate)) return false;
        document = candidate;
        mutationGeneration = NextGeneration(mutationGeneration);
        return true;
    }

    private static bool TryRecordMap(
        MapCharacterStatistics character,
        ConfirmedCrystallineConflictMapResult confirmed)
    {
        var arenaKey = confirmed.Arena.ToString();
        if (!character.Maps.TryGetValue(arenaKey, out var record))
        {
            record = new MapWinLossRecord();
            character.Maps.Add(arenaKey, record);
        }

        return TryIncrement(record, confirmed.IsWin);
    }

    private bool TryRecordObservedPlayers(
        MapCharacterStatistics character,
        ulong localContentId,
        byte localResult,
        IReadOnlyList<CapturedMapResultParticipant> participants)
    {
        var localMatches = participants
            .Where(participant => participant.ContentId == localContentId)
            .ToArray();
        if (localMatches.Length != 1) return false;

        var localTeam = localMatches[0].Team;
        if (!CanIncrement(character.Overall)) return false;

        var updates = new List<(string PlayerKey, bool PlayerWon)>(
            CrystallineConflictMapStatisticsRules.ExpectedParticipantCount - 1);
        var exactPlayerKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var participant in participants)
        {
            if (participant.ContentId == localContentId) continue;

            if (!TryNormalizeObservedIdentity(
                    participant.PlayerName,
                    participant.WorldId,
                    out var identity))
            {
                return false;
            }

            var playerKey = ComputeObservedPlayerKey(identity);
            if (!exactPlayerKeys.Add(playerKey) ||
                !CrystallineConflictPredictionRules.TryResolveObservedPlayerWin(
                    localResult,
                    localTeam,
                    participant.Team,
                    out var playerWon))
            {
                return false;
            }

            if (character.ObservedPlayers.TryGetValue(playerKey, out var existing) &&
                !CanIncrement(existing))
                return false;

            updates.Add((playerKey, playerWon));
        }

        if (updates.Count != CrystallineConflictMapStatisticsRules.ExpectedParticipantCount - 1)
            return false;

        // Stage the complete nine-player update before touching the cloned
        // character. A full PvpStats import can occupy all 4096 slots; a later
        // native match must still land as one atomic observation. Current-match
        // identities are protected, then the least-observed old hashes are
        // evicted. Hash-descending is the deterministic tie-break, matching the
        // importer's hash-ascending Top-K retention without persisting names.
        var stagedPlayers = character.ObservedPlayers.ToDictionary(
            static player => player.Key,
            static player => new ObservedPlayerWinLossRecord
            {
                Wins = player.Value.Wins,
                Losses = player.Value.Losses,
            },
            StringComparer.Ordinal);
        var missingRecords = updates.Count(update => !stagedPlayers.ContainsKey(update.PlayerKey));
        var evictionCount = Math.Max(
            0,
            stagedPlayers.Count + missingRecords - MaximumObservedPlayersPerCharacter);
        if (evictionCount > 0)
        {
            var evictions = stagedPlayers
                .Where(player => !exactPlayerKeys.Contains(player.Key))
                .Select(player => new
                {
                    PlayerKey = player.Key,
                    Valid = CrystallineConflictMapStatisticsRules.TryCreateSnapshot(
                        player.Value.Wins,
                        player.Value.Losses,
                        out var snapshot),
                    Matches = snapshot.Matches,
                })
                .Where(static player => player.Valid)
                .OrderBy(static player => player.Matches)
                .ThenByDescending(static player => player.PlayerKey, StringComparer.Ordinal)
                .Take(evictionCount)
                .ToArray();
            if (evictions.Length != evictionCount) return false;
            foreach (var eviction in evictions)
                stagedPlayers.Remove(eviction.PlayerKey);
        }

        foreach (var update in updates)
        {
            if (!stagedPlayers.TryGetValue(update.PlayerKey, out var record))
            {
                record = new ObservedPlayerWinLossRecord();
                stagedPlayers.Add(update.PlayerKey, record);
            }

            if (!TryIncrement(record, update.PlayerWon)) return false;
        }

        if (stagedPlayers.Count > MaximumObservedPlayersPerCharacter ||
            !TryIncrement(character.Overall, localResult == 1))
            return false;

        character.ObservedPlayers = stagedPlayers;

        return true;
    }

    private static bool CanIncrement(MapWinLossRecord record) =>
        CrystallineConflictMapStatisticsRules.TryCreateSnapshot(
            record.Wins,
            record.Losses,
            out var existing) &&
        existing.Matches < long.MaxValue;

    private static bool CanIncrement(ObservedPlayerWinLossRecord record) =>
        CrystallineConflictMapStatisticsRules.TryCreateSnapshot(
            record.Wins,
            record.Losses,
            out var existing) &&
        existing.Matches < long.MaxValue;

    private static bool TryIncrement(MapWinLossRecord record, bool isWin)
    {
        if (!CanIncrement(record)) return false;

        if (isWin) record.Wins++;
        else record.Losses++;
        return true;
    }

    private static bool TryIncrement(ObservedPlayerWinLossRecord record, bool playerWon)
    {
        if (!CanIncrement(record)) return false;

        if (playerWon) record.Wins++;
        else record.Losses++;

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
        mutationGeneration = NextGeneration(mutationGeneration);
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
        var temporaryPath = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{filePath}.{Guid.NewGuid():N}.tmp");
        try
        {
            var json = JsonSerializer.Serialize(candidate, JsonOptions);
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       4_096,
                       FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false, true)))
            {
                writer.Write(json);
                writer.Flush();
                stream.Flush(true);
            }

            if (File.Exists(filePath))
                File.Replace(temporaryPath, filePath, null, true);
            else
                File.Move(temporaryPath, filePath);
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
        var sourceSchema = candidate.Schema;
        var discardedSchemaTwoPlayers = 0;
        if (sourceSchema is < 1 or > CurrentSchema ||
            string.IsNullOrEmpty(candidate.Salt) ||
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

            if (sourceSchema == 1)
            {
                if (!TrySumMapRecords(pair.Value.Maps.Values, out var overall))
                    return false;

                // Schema 1 contained only per-map counters. Their exact sum is
                // the backward-compatible personal overall record.
                pair.Value.Overall = overall;
                pair.Value.ObservedPlayers = new Dictionary<string, ObservedPlayerWinLossRecord>(
                    StringComparer.Ordinal);
            }
            else
            {
                if (pair.Value.Overall is null ||
                    pair.Value.ObservedPlayers is null ||
                    pair.Value.ObservedPlayers.Count > MaximumObservedPlayersPerCharacter ||
                    !CrystallineConflictMapStatisticsRules.TryCreateSnapshot(
                        pair.Value.Overall.Wins,
                        pair.Value.Overall.Losses,
                        out _))
                {
                    return false;
                }

                foreach (var player in pair.Value.ObservedPlayers)
                {
                    if (!IsHash(player.Key) || player.Value is null)
                        return false;

                    if (sourceSchema == 2)
                    {
                        if (player.Value.Wins != 0 ||
                            player.Value.Losses != 0 ||
                            !CrystallineConflictMapStatisticsRules.TryCreateSnapshot(
                                player.Value.AllyWins,
                                player.Value.AllyLosses,
                                out _) ||
                            !CrystallineConflictMapStatisticsRules.TryCreateSnapshot(
                                player.Value.EnemyWins,
                                player.Value.EnemyLosses,
                                out _) ||
                            !TryAddCounters(
                                player.Value.AllyWins,
                                player.Value.EnemyWins,
                                out var wins) ||
                            !TryAddCounters(
                                player.Value.AllyLosses,
                                player.Value.EnemyLosses,
                                out var losses) ||
                            !CrystallineConflictMapStatisticsRules.TryCreateSnapshot(
                                wins,
                                losses,
                                out _))
                        {
                            return false;
                        }
                    }
                    else if (player.Value.HasLegacyRelationshipCounters ||
                             !CrystallineConflictMapStatisticsRules.TryCreateSnapshot(
                                 player.Value.Wins,
                                 player.Value.Losses,
                                 out _))
                    {
                        return false;
                    }
                }

                if (sourceSchema == 2)
                {
                    // Schema 2 keyed observed players from their Content IDs.
                    // Current history keys the normalized name/world identity,
                    // and the one-way legacy HMAC cannot be mapped safely. Keep
                    // independent own/map totals, but never present these rows
                    // as current player history under an unresolved identity.
                    discardedSchemaTwoPlayers += pair.Value.ObservedPlayers.Count;
                    pair.Value.ObservedPlayers =
                        new Dictionary<string, ObservedPlayerWinLossRecord>(
                            StringComparer.Ordinal);
                }
            }

            foreach (var result in pair.Value.RecentResults)
            {
                if (result is null ||
                    !IsHash(result.Fingerprint) ||
                    result.CapturedAtUnixSeconds <= 0)
                {
                    return false;
                }

                if (sourceSchema == 1)
                {
                    result.RecordedMap = true;
                    result.RecordedPlayers = false;
                }
                else if (sourceSchema == 2)
                {
                    // Schema 2 did not persist per-consumer flags. Treat its
                    // recent entries as fully consumed to avoid double counts.
                    result.RecordedMap = true;
                    result.RecordedPlayers = true;
                }
                else if (!result.RecordedMap && !result.RecordedPlayers)
                {
                    return false;
                }
            }

            if (sourceSchema < 4)
            {
                // Older public releases never stored player history, so there
                // is no native player-history epoch to overlap an import.
                pair.Value.PlayerHistoryStartedAtUnixSeconds = 0;
                pair.Value.PvpStatsHistoryImported = false;
                pair.Value.PvpStatsImportedMatches = 0;
                pair.Value.PvpStatsImportedPlayers = 0;
                pair.Value.PvpStatsImportedAtUnixSeconds = 0;
                pair.Value.PvpStatsImportBeforeUnixSecondsExclusive = 0;
            }
            else if (pair.Value.PlayerHistoryStartedAtUnixSeconds < 0)
            {
                return false;
            }

            if (pair.Value.PvpStatsHistoryImported)
            {
                if (pair.Value.PvpStatsImportedMatches <= 0 ||
                    pair.Value.PvpStatsImportedPlayers <= 0 ||
                    pair.Value.PvpStatsImportedPlayers > MaximumObservedPlayersPerCharacter ||
                    pair.Value.PvpStatsImportedAtUnixSeconds <= 0 ||
                    pair.Value.PvpStatsImportBeforeUnixSecondsExclusive <= 0)
                {
                    return false;
                }
            }
            else if (pair.Value.PvpStatsImportedMatches != 0 ||
                     pair.Value.PvpStatsImportedPlayers != 0 ||
                     pair.Value.PvpStatsImportedAtUnixSeconds != 0 ||
                     pair.Value.PvpStatsImportBeforeUnixSecondsExclusive != 0)
            {
                return false;
            }
        }

        candidate.Schema = CurrentSchema;
        if (discardedSchemaTwoPlayers > 0)
        {
            log?.Warning(
                "Discarded {Count} legacy schema-2 CC player-history rows because their Content-ID-derived keys cannot be resolved to current name/world identities. Own overall and map W/L were preserved.",
                discardedSchemaTwoPlayers);
        }
        return true;
    }

    private static bool TrySumMapRecords(
        IEnumerable<MapWinLossRecord> records,
        out MapWinLossRecord total)
    {
        total = new MapWinLossRecord();
        foreach (var record in records)
        {
            if (!TryAddCounters(total.Wins, record.Wins, out var wins) ||
                !TryAddCounters(total.Losses, record.Losses, out var losses) ||
                !CrystallineConflictMapStatisticsRules.TryCreateSnapshot(
                    wins,
                    losses,
                    out _))
            {
                return false;
            }

            total.Wins = wins;
            total.Losses = losses;
        }

        return true;
    }

    private static bool TryAddCounters(long left, long right, out long sum)
    {
        sum = 0;
        if (left < 0 || right < 0 || left > long.MaxValue - right) return false;
        sum = left + right;
        return true;
    }

    private static long NextGeneration(long current) =>
        current == long.MaxValue ? 1 : current + 1;

    private string ComputeCharacterKey(ulong contentId)
    {
        Span<byte> buffer = stackalloc byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64LittleEndian(buffer, contentId);
        return ComputeHash(buffer);
    }

    private string ComputeObservedPlayerKey(string normalizedIdentity) =>
        ComputeHash(Encoding.UTF8.GetBytes(normalizedIdentity));

    private static bool TryNormalizeObservedIdentity(
        string? playerName,
        ushort worldId,
        out string identity)
    {
        identity = string.Empty;
        string? normalizedName;
        try
        {
            normalizedName = playerName?.Trim().Normalize(NormalizationForm.FormC);
        }
        catch (ArgumentException)
        {
            return false;
        }

        if (worldId == 0 ||
            string.IsNullOrWhiteSpace(normalizedName) ||
            normalizedName.Length is < 3 or > 42 ||
            normalizedName.Any(character => char.IsControl(character) || char.IsSurrogate(character)) ||
            Encoding.UTF8.GetByteCount(normalizedName) >
            CrystallineConflictMapResultPlayer.PlayerNameBufferLength - 1)
        {
            return false;
        }

        identity = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{worldId}|{normalizedName.ToUpperInvariant()}");
        return true;
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

    private static bool IsHash(string? value)
    {
        if (string.IsNullOrEmpty(value)) return false;
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
                Overall = new MapWinLossRecord
                {
                    Wins = pair.Value.Overall.Wins,
                    Losses = pair.Value.Overall.Losses,
                },
                Maps = pair.Value.Maps.ToDictionary(
                    static map => map.Key,
                    static map => new MapWinLossRecord
                    {
                        Wins = map.Value.Wins,
                        Losses = map.Value.Losses,
                    },
                    StringComparer.Ordinal),
                ObservedPlayers = pair.Value.ObservedPlayers.ToDictionary(
                    static player => player.Key,
                    static player => new ObservedPlayerWinLossRecord
                    {
                        Wins = player.Value.Wins,
                        Losses = player.Value.Losses,
                    },
                    StringComparer.Ordinal),
                RecentResults = pair.Value.RecentResults.Select(static result => new MapRecentResult
                {
                    Fingerprint = result.Fingerprint,
                    CapturedAtUnixSeconds = result.CapturedAtUnixSeconds,
                    RecordedMap = result.RecordedMap,
                    RecordedPlayers = result.RecordedPlayers,
                }).ToList(),
                PlayerHistoryStartedAtUnixSeconds =
                    pair.Value.PlayerHistoryStartedAtUnixSeconds,
                PvpStatsHistoryImported = pair.Value.PvpStatsHistoryImported,
                PvpStatsImportedMatches = pair.Value.PvpStatsImportedMatches,
                PvpStatsImportedPlayers = pair.Value.PvpStatsImportedPlayers,
                PvpStatsImportedAtUnixSeconds = pair.Value.PvpStatsImportedAtUnixSeconds,
                PvpStatsImportBeforeUnixSecondsExclusive =
                    pair.Value.PvpStatsImportBeforeUnixSecondsExclusive,
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
        public MapWinLossRecord Overall { get; set; } = new();
        public Dictionary<string, MapWinLossRecord> Maps { get; set; } =
            new(StringComparer.Ordinal);
        public Dictionary<string, ObservedPlayerWinLossRecord> ObservedPlayers { get; set; } =
            new(StringComparer.Ordinal);
        public List<MapRecentResult> RecentResults { get; set; } = [];
        public long PlayerHistoryStartedAtUnixSeconds { get; set; }
        public bool PvpStatsHistoryImported { get; set; }
        public int PvpStatsImportedMatches { get; set; }
        public int PvpStatsImportedPlayers { get; set; }
        public long PvpStatsImportedAtUnixSeconds { get; set; }
        public long PvpStatsImportBeforeUnixSecondsExclusive { get; set; }
    }

    private sealed class MapWinLossRecord
    {
        public long Wins { get; set; }
        public long Losses { get; set; }
    }

    private sealed class ObservedPlayerWinLossRecord
    {
        public long Wins { get; set; }
        public long Losses { get; set; }

        // Read-only compatibility surface for schema 2. These relationship
        // counters are validated and then their unresolvable player rows are
        // discarded; zero values remain omitted from current saves.
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public long AllyWins { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public long AllyLosses { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public long EnemyWins { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public long EnemyLosses { get; set; }

        [JsonIgnore]
        internal bool HasLegacyRelationshipCounters =>
            AllyWins != 0 || AllyLosses != 0 || EnemyWins != 0 || EnemyLosses != 0;

    }

    private sealed class MapRecentResult
    {
        public string Fingerprint { get; set; } = string.Empty;
        public long CapturedAtUnixSeconds { get; set; }
        public bool RecordedMap { get; set; }
        public bool RecordedPlayers { get; set; }
    }

    private readonly record struct ImportPlayerUpdate(
        string PlayerKey,
        long Wins,
        long Losses,
        int Matches,
        long LastSeenUnixSeconds);

    private readonly record struct ImportMergePlayerRecord(
        string PlayerKey,
        long Wins,
        long Losses,
        long Matches,
        long LastSeenUnixSeconds,
        bool HasImportedContribution);
}
