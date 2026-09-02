using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace SeitonSense.Plugin.Services;

internal sealed record PvpStatsHistoryImportSnapshot(
    bool IsBusy,
    bool IsComplete,
    bool Success,
    double Progress,
    int DocumentsScanned,
    int TotalDocuments,
    int ImportedMatches,
    int ImportedPlayers,
    string Status)
{
    internal static PvpStatsHistoryImportSnapshot Ready { get; } =
        new(false, false, false, 0d, 0, 0, 0, 0, "Ready for an optional one-time PvpStats import.");
}

/// <summary>
/// Orchestrates the explicit one-time PvpStats history import. All game and
/// Lumina reads happen before the background scan. The aggregate is merged on
/// the framework thread through the existing atomic local-history store.
/// </summary>
internal sealed class CrystallineConflictPvpStatsHistoryImportService : IDisposable
{
    private readonly object gate = new();
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IClientState clientState;
    private readonly IPlayerState playerState;
    private readonly IObjectTable objectTable;
    private readonly IDataManager dataManager;
    private readonly IFramework framework;
    private readonly ICondition condition;
    private readonly CrystallineConflictMapStatisticsService mapStatistics;
    private readonly IPluginLog log;
    private PvpStatsHistoryImportSnapshot snapshot = PvpStatsHistoryImportSnapshot.Ready;
    private Task<PvpStatsHistoryReadResult>? runningTask;
    private CancellationTokenSource? cancellation;
    private ulong destinationContentId;
    private long expectedStoreGeneration;
    private long importBeforeUnixSecondsExclusive;
    private long requestGeneration;
    private long runningRequestGeneration;
    private bool cancelRequested;
    private bool started;
    private bool disposed;

    internal CrystallineConflictPvpStatsHistoryImportService(
        IDalamudPluginInterface pluginInterface,
        IClientState clientState,
        IPlayerState playerState,
        IObjectTable objectTable,
        IDataManager dataManager,
        IFramework framework,
        ICondition condition,
        CrystallineConflictMapStatisticsService mapStatistics,
        IPluginLog log)
    {
        this.pluginInterface = pluginInterface;
        this.clientState = clientState;
        this.playerState = playerState;
        this.objectTable = objectTable;
        this.dataManager = dataManager;
        this.framework = framework;
        this.condition = condition;
        this.mapStatistics = mapStatistics;
        this.log = log;
    }

    internal PvpStatsHistoryImportSnapshot Snapshot => Volatile.Read(ref snapshot);

    internal void Start()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (started) return;
        started = true;
        framework.Update += OnFrameworkUpdate;
    }

    internal bool TryStart()
    {
        if (disposed || !started) return false;
        lock (gate)
        {
            if (runningTask is { IsCompleted: false }) return false;
        }

        if (!clientState.IsLoggedIn ||
            condition[ConditionFlag.InCombat] ||
            condition[ConditionFlag.BoundByDuty] ||
            playerState.ContentId == 0 ||
            objectTable.LocalPlayer is not { } localPlayer)
        {
            SetStatus("Log in on the destination character and leave combat and duties before importing.");
            return false;
        }

        if (!mapStatistics.TryGetPvpStatsImportPlan(playerState.ContentId, out var plan))
        {
            SetStatus("The local Seiton history store is unavailable; nothing was changed.");
            return false;
        }

        if (plan.AlreadyImported)
        {
            Volatile.Write(
                ref snapshot,
                new PvpStatsHistoryImportSnapshot(
                    false,
                    true,
                    true,
                    1d,
                    0,
                    0,
                    plan.PreviouslyImportedMatches,
                    plan.PreviouslyImportedPlayers,
                    $"Already imported {plan.PreviouslyImportedMatches:N0} PvpStats matches for {plan.PreviouslyImportedPlayers:N0} players."));
            return false;
        }

        if (plan.ImportBeforeUnixSecondsExclusive <= 0)
        {
            SetStatus(
                "This character has player history without a safe import boundary. Clear saved W/L first, then import again.");
            return false;
        }

        var worldSheet = dataManager.GetExcelSheet<World>();
        var worldIds = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase);
        foreach (var world in worldSheet)
        {
            if (world.RowId is 0 or > ushort.MaxValue) continue;
            var worldName = world.Name.ToString().Trim();
            if (string.IsNullOrWhiteSpace(worldName) ||
                !worldIds.TryAdd(worldName, (ushort)world.RowId))
            {
                continue;
            }
        }

        if (localPlayer.HomeWorld.RowId is 0 or > ushort.MaxValue ||
            !worldSheet.TryGetRow(localPlayer.HomeWorld.RowId, out var localWorld) ||
            string.IsNullOrWhiteSpace(localWorld.Name.ToString()) ||
            worldIds.Count == 0)
        {
            SetStatus("The current Home World could not be verified; nothing was changed.");
            return false;
        }

        var pluginConfigRoot = Directory.GetParent(pluginInterface.GetPluginConfigDirectory())?.FullName;
        if (string.IsNullOrWhiteSpace(pluginConfigRoot))
        {
            SetStatus("The local PvpStats data folder could not be located.");
            return false;
        }

        var databasePath = Path.Combine(pluginConfigRoot, "PvpStats", "data.db");
        var nextCancellation = new CancellationTokenSource();
        var contentId = playerState.ContentId;
        var cutoff = plan.ImportBeforeUnixSecondsExclusive;
        var progress = new ImportProgress(this);
        var task = CrystallineConflictPvpStatsHistoryReader.ReadAsync(
            databasePath,
            localPlayer.Name.ToString(),
            localWorld.Name.ToString(),
            worldIds,
            cutoff,
            progress,
            nextCancellation.Token);

        lock (gate)
        {
            cancellation?.Dispose();
            cancellation = nextCancellation;
            destinationContentId = contentId;
            expectedStoreGeneration = plan.StoreGeneration;
            importBeforeUnixSecondsExclusive = cutoff;
            requestGeneration = requestGeneration == long.MaxValue ? 1 : requestGeneration + 1;
            runningRequestGeneration = requestGeneration;
            cancelRequested = false;
            runningTask = task;
        }

        Volatile.Write(
            ref snapshot,
            new PvpStatsHistoryImportSnapshot(
                true,
                false,
                false,
                0d,
                0,
                0,
                0,
                0,
                "Reading PvpStats history through an exclusive read-only lock..."));
        return true;
    }

    internal void Cancel()
    {
        lock (gate)
        {
            if (runningTask is null) return;
            cancelRequested = true;
            cancellation?.Cancel();
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (started) framework.Update -= OnFrameworkUpdate;
        lock (gate)
        {
            cancellation?.Cancel();
            cancellation?.Dispose();
            cancellation = null;
            runningTask = null;
            cancelRequested = true;
        }
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        Task<PvpStatsHistoryReadResult>? completed;
        ulong contentId;
        long storeGeneration;
        long cutoff;
        long completedRequestGeneration;
        bool wasCancelled;
        lock (gate)
        {
            if (runningTask is not { IsCompleted: true }) return;
            completed = runningTask;
            runningTask = null;
            cancellation?.Dispose();
            cancellation = null;
            contentId = destinationContentId;
            storeGeneration = expectedStoreGeneration;
            cutoff = importBeforeUnixSecondsExclusive;
            completedRequestGeneration = runningRequestGeneration;
            wasCancelled = cancelRequested;
            destinationContentId = 0;
            expectedStoreGeneration = 0;
            importBeforeUnixSecondsExclusive = 0;
            runningRequestGeneration = 0;
            cancelRequested = false;
        }

        PvpStatsHistoryReadResult read;
        try
        {
            read = completed.GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            log.Warning(exception, "The optional PvpStats history task failed closed.");
            SetStatus("The PvpStats import failed; nothing was changed.", complete: true);
            return;
        }

        if (disposed) return;
        if (wasCancelled || completedRequestGeneration <= 0)
        {
            SetStatus("PvpStats history import was cancelled; nothing was changed.", complete: true);
            return;
        }
        if (!read.Success)
        {
            Volatile.Write(
                ref snapshot,
                new PvpStatsHistoryImportSnapshot(
                    false,
                    true,
                    false,
                    Snapshot.Progress,
                    read.DocumentsScanned,
                    Math.Max(Snapshot.TotalDocuments, read.DocumentsScanned),
                    0,
                    0,
                    read.Status));
            return;
        }

        if (playerState.ContentId != contentId ||
            contentId == 0 ||
            condition[ConditionFlag.InCombat] ||
            condition[ConditionFlag.BoundByDuty])
        {
            SetStatus("Character or duty state changed before saving; nothing was changed.", complete: true);
            return;
        }

        if (!mapStatistics.TryMergePvpStatsHistory(
                contentId,
                storeGeneration,
                cutoff,
                read,
                out var merged))
        {
            SetStatus(merged.Status, complete: true);
            return;
        }

        Volatile.Write(
            ref snapshot,
            new PvpStatsHistoryImportSnapshot(
                false,
                true,
                merged.Success,
                1d,
                read.DocumentsScanned,
                read.DocumentsScanned,
                merged.ImportedMatches,
                merged.ImportedPlayers,
                merged.Status));
    }

    private void SetStatus(string status, bool complete = true)
    {
        Volatile.Write(
            ref snapshot,
            new PvpStatsHistoryImportSnapshot(
                false,
                complete,
                false,
                0d,
                0,
                0,
                0,
                0,
                status));
    }

    private void OnProgress(PvpStatsHistoryReadProgress progress)
    {
        if (disposed) return;
        Volatile.Write(
            ref snapshot,
            new PvpStatsHistoryImportSnapshot(
                true,
                false,
                false,
                Math.Clamp(progress.Fraction, 0d, 1d),
                progress.DocumentsScanned,
                progress.TotalDocuments,
                0,
                0,
                progress.TotalDocuments > 0
                    ? $"Reading PvpStats: {progress.DocumentsScanned:N0} / {progress.TotalDocuments:N0} CC matches..."
                    : $"Reading PvpStats: {progress.DocumentsScanned:N0} CC matches..."));
    }

    private sealed class ImportProgress(CrystallineConflictPvpStatsHistoryImportService owner)
        : IProgress<PvpStatsHistoryReadProgress>
    {
        public void Report(PvpStatsHistoryReadProgress value) => owner.OnProgress(value);
    }
}
