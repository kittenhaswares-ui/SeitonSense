using System.Net;
using System.Net.Http;
using System.Text.Json;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using SeitonSense.Core;
using SeitonSense.Plugin.Models;

namespace SeitonSense.Plugin.Services;

internal sealed record OfficialRankCache(
    int Schema, string Region, int Season, string SourceUpdatedText,
    DateTimeOffset FetchedAt, DateTimeOffset LastAttemptAt,
    OfficialCrystallineConflictRankEntry[] Entries);

internal sealed record OfficialRankStatus(OfficialRankCache? Cache, string Message, bool Busy);

/// <summary>Public leaderboard reads only. No player lookup requests or match data leave the PC.</summary>
internal sealed class OfficialCrystallineConflictRankService : IDisposable
{
    private const int MaximumPageBytes = 262_144;
    private const int MaximumCacheBytes = 524_288;
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromHours(24);
    private readonly PluginConfiguration configuration;
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly IDataManager dataManager;
    private readonly IFramework framework;
    private readonly ICondition condition;
    private readonly IPluginLog log;
    private readonly string cacheDirectory;
    private readonly Dictionary<ushort, string> worlds = [];
    private readonly HttpClient http = new(new HttpClientHandler
    {
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        UseCookies = false,
    }) { Timeout = TimeSpan.FromSeconds(15) };
    private OfficialRankStatus status = new(null, "Waiting for your Home World.", false);
    private Task<OfficialRankStatus>? work;
    private CancellationTokenSource? cancellation;
    private string region = string.Empty;
    private string workRegion = string.Empty;
    private DateTimeOffset nextCheck;
    private DateTimeOffset retryAfter;
    private bool refreshing;
    private bool cacheLoaded;
    private bool started;
    private bool disposed;

    internal OfficialCrystallineConflictRankService(
        PluginConfiguration configuration, IDalamudPluginInterface pluginInterface,
        IClientState clientState, IObjectTable objectTable, IDataManager dataManager,
        IFramework framework, ICondition condition, IPluginLog log)
    {
        this.configuration = configuration;
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.dataManager = dataManager;
        this.framework = framework;
        this.condition = condition;
        this.log = log;
        cacheDirectory = pluginInterface.GetPluginConfigDirectory();
    }

    internal OfficialRankStatus Status => status;

    internal void Start()
    {
        if (started || disposed) return;
        started = true;
        foreach (var world in dataManager.GetExcelSheet<World>())
            if (world.RowId is > 0 and <= ushort.MaxValue && !world.Name.IsEmpty)
                worlds[(ushort)world.RowId] = world.Name.ToString().Trim();
        framework.Update += OnUpdate;
    }

    internal OfficialCrystallineConflictRankEntry? Find(string name, ushort homeWorldId)
    {
        var cache = status.Cache;
        if (cache is null || cache.Region != region ||
            DateTimeOffset.UtcNow - cache.FetchedAt > TimeSpan.FromDays(7) ||
            !worlds.TryGetValue(homeWorldId, out var world)) return null;
        return Array.Find(cache.Entries, entry =>
            string.Equals(entry.Name, name.Trim(), StringComparison.OrdinalIgnoreCase) &&
            string.Equals(entry.HomeWorld, world, StringComparison.OrdinalIgnoreCase));
    }

    private void OnUpdate(IFramework _)
    {
        if (disposed) return;
        // Duty transitions cancel network work immediately, without waiting for the slow UI tick.
        var canFetch = configuration.Enabled && configuration.ShowOfficialCrystallineConflictRanks &&
                       clientState.IsLoggedIn && !condition[ConditionFlag.BoundByDuty] &&
                       !condition[ConditionFlag.InCombat] && !condition[ConditionFlag.BetweenAreas];
        if (refreshing && !canFetch) cancellation?.Cancel();
        var now = DateTimeOffset.UtcNow;
        if (now < nextCheck) return;
        nextCheck = now.AddSeconds(1);

        if (work is { IsCompleted: true })
        {
            var result = work.GetAwaiter().GetResult(); // Work catches all failures, including cancellation.
            if (workRegion == region)
            {
                status = result;
                cacheLoaded = true;
                if (refreshing && result.Cache?.LastAttemptAt is { } attempted)
                    retryAfter = attempted + RefreshInterval;
            }
            work = null;
            refreshing = false;
            cancellation?.Dispose();
            cancellation = null;
        }

        if (!configuration.Enabled || !configuration.ShowOfficialCrystallineConflictRanks ||
            !clientState.IsLoggedIn || objectTable.LocalPlayer is not { } local) return;
        if (!local.HomeWorld.IsValid || !local.HomeWorld.Value.DataCenter.IsValid) return;
        var dcName = local.HomeWorld.Value.DataCenter.Value.Name.ToString();
        if (!OfficialCrystallineConflictRankRules.TryGetRegionForDataCenter(dcName, out var nextRegion))
        {
            status = new(null, "Official standings are unavailable for this region.", false);
            return;
        }
        if (region != nextRegion)
        {
            cancellation?.Cancel();
            region = nextRegion;
            cacheLoaded = false;
            retryAfter = default;
            status = new(null, "Loading saved official standings...", false);
        }
        if (work is not null) return;
        if (!cacheLoaded)
        {
            workRegion = region;
            var capturedRegion = region;
            work = Task.Run(() => LoadCache(capturedRegion));
            return;
        }
        var previous = status.Cache;
        if (!canFetch || now < retryAfter ||
            previous is not null && !OfficialCrystallineConflictRankRules.CanRefresh(now, previous.LastAttemptAt)) return;
        retryAfter = now + RefreshInterval;
        workRegion = region;
        refreshing = true;
        cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        var refreshRegion = region;
        status = status with { Busy = true, Message = "Updating public standings outside combat..." };
        work = Task.Run(() => RefreshAsync(refreshRegion, previous, token));
    }

    private OfficialRankStatus LoadCache(string cacheRegion)
    {
        try
        {
            var path = CachePath(cacheRegion);
            if (!File.Exists(path)) return new(null, "Waiting to download standings outside combat and duties.", false);
            using var stream = File.OpenRead(path);
            if (stream.Length > MaximumCacheBytes) throw new InvalidDataException("Rank cache is too large.");
            var cache = JsonSerializer.Deserialize<OfficialRankCache>(stream);
            if (cache is null || cache.Schema != 1 || cache.Region != cacheRegion ||
                !OfficialCrystallineConflictRankRules.IsValidSnapshotEntries(cache.Entries) ||
                cache.Season is < 0 or > 9999 || cache.SourceUpdatedText is null ||
                cache.SourceUpdatedText.Length > 160 ||
                cache.Entries.Length > 0 && (cache.Season == 0 ||
                    cache.FetchedAt <= DateTimeOffset.UnixEpoch || string.IsNullOrWhiteSpace(cache.SourceUpdatedText)) ||
                cache.LastAttemptAt > DateTimeOffset.UtcNow.AddMinutes(5) ||
                cache.LastAttemptAt != default && cache.LastAttemptAt < DateTimeOffset.UnixEpoch ||
                cache.FetchedAt > DateTimeOffset.UtcNow.AddMinutes(5))
                throw new InvalidDataException("Rank cache is invalid.");
            return new(cache, CacheMessage(cache), false);
        }
        catch (Exception exception)
        {
            log.Warning(exception, "Official CC rank cache could not be read.");
            return new(null, "Saved ranks unavailable; a fresh download will be tried outside combat.", false);
        }
    }

    private async Task<OfficialRankStatus> RefreshAsync(
        string refreshRegion, OfficialRankCache? previous, CancellationToken token)
    {
        var attempted = (previous ?? new(1, refreshRegion, 0, string.Empty, default, default, []))
            with { LastAttemptAt = DateTimeOffset.UtcNow };
        try
        {
            // Persist the attempt too: restarting the plugin must not hammer Lodestone on errors.
            SaveCache(attempted);
            var entries = new Dictionary<string, OfficialCrystallineConflictRankEntry>(StringComparer.OrdinalIgnoreCase);
            var first = await FetchPageAsync(refreshRegion, "all", 1, token).ConfigureAwait(false);
            var parsed = OfficialCrystallineConflictRankRules.ParsePage(first.Html);
            if (parsed.Season <= 0 || parsed.Entries.Length == 0 || string.IsNullOrWhiteSpace(parsed.SourceUpdatedText))
                throw new InvalidDataException("Official standings format changed or is not available.");
            AddEntries(parsed.Entries);
            var more = first.More;
            for (var page = 2; more && page <= 6; page++)
            {
                await Task.Delay(1000, token).ConfigureAwait(false);
                var response = await FetchPageAsync(refreshRegion, "all", page, token).ConfigureAwait(false);
                var fragment = OfficialCrystallineConflictRankRules.ParsePage(response.Html);
                if (fragment.Entries.Length == 0) throw new InvalidDataException("Incomplete standings page.");
                if (fragment.Season != 0 && fragment.Season != parsed.Season ||
                    !string.IsNullOrEmpty(fragment.SourceUpdatedText) && fragment.SourceUpdatedText != parsed.SourceUpdatedText)
                    throw new InvalidDataException("Official page context changed during refresh.");
                AddEntries(fragment.Entries);
                more = response.More;
            }
            if (more) throw new InvalidDataException("Official standings exceeded the known page limit.");
            for (var tier = 1; tier <= 8; tier++)
            {
                await Task.Delay(1000, token).ConfigureAwait(false);
                var response = await FetchPageAsync(refreshRegion, tier.ToString(System.Globalization.CultureInfo.InvariantCulture), 1, token).ConfigureAwait(false);
                var tierPage = OfficialCrystallineConflictRankRules.ParsePage(response.Html);
                if (tierPage.Season != parsed.Season || tierPage.SourceUpdatedText != parsed.SourceUpdatedText)
                    throw new InvalidDataException("Official snapshot changed during refresh.");
                AddEntries(tierPage.Entries);
            }
            token.ThrowIfCancellationRequested();
            var updated = new OfficialRankCache(1, refreshRegion, parsed.Season, parsed.SourceUpdatedText,
                DateTimeOffset.UtcNow, attempted.LastAttemptAt, entries.Values.ToArray());
            if (!OfficialCrystallineConflictRankRules.IsValidSnapshotEntries(updated.Entries))
                throw new InvalidDataException("Invalid combined official standings.");
            SaveCache(updated);
            return new(updated, CacheMessage(updated), false);

            void AddEntries(OfficialCrystallineConflictRankEntry[] source)
            {
                foreach (var entry in source)
                {
                    var key = $"{entry.HomeWorld}|{entry.Name}";
                    if (entries.TryGetValue(key, out var existing) &&
                        (existing.Tier != entry.Tier || existing.CharacterId != entry.CharacterId))
                        throw new InvalidDataException("Conflicting public tier snapshots.");
                    if (entries.Values.Any(known => known.CharacterId == entry.CharacterId &&
                        (!string.Equals(known.Name, entry.Name, StringComparison.OrdinalIgnoreCase) ||
                         !string.Equals(known.HomeWorld, entry.HomeWorld, StringComparison.OrdinalIgnoreCase))))
                        throw new InvalidDataException("Conflicting public character identities.");
                    entries.TryAdd(key, entry);
                }
                if (entries.Count > 380) throw new InvalidDataException("Too many official entries.");
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // A match interrupted us, not a failed daily download. Retry after it finishes.
            var deferred = attempted with { LastAttemptAt = previous?.LastAttemptAt ?? default };
            try { SaveCache(deferred); } catch (Exception exception) { log.Warning(exception, "Could not save deferred rank refresh."); }
            return new(deferred, "Rank refresh paused for gameplay; saved standings remain available.", false);
        }
        catch (Exception exception)
        {
            log.Warning(exception, "Official CC standings refresh failed; keeping saved ranks.");
            return new(attempted, "Official standings unavailable. Saved ranks kept; next attempt in 24 hours.", false);
        }
    }

    private async Task<(string Html, bool More)> FetchPageAsync(string dc, string tier, int page, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        var requestToken = timeout.Token;
        if (!OfficialCrystallineConflictRankRules.IsSupportedRegion(dc)) throw new InvalidDataException("Unknown region.");
        var url = $"https://na.finalfantasyxiv.com/lodestone/ranking/crystallineconflict/?dcgroup={dc}&rank_type={tier}&page={page}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (page > 1) request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, requestToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > MaximumPageBytes) throw new InvalidDataException("Official page is too large.");
        await using var stream = await response.Content.ReadAsStreamAsync(requestToken).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(chunk, requestToken).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > MaximumPageBytes) throw new InvalidDataException("Official page exceeded the size limit.");
            buffer.Write(chunk, 0, read);
        }
        var html = System.Text.Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
        var more = response.Headers.TryGetValues("x-more", out var values) && values.Contains("true");
        // The initial full document declares its next page in the official list container.
        if (page == 1 && tier == "all" && html.Contains("data-next-page=\"2\"", StringComparison.Ordinal)) more = true;
        return (html, more);
    }

    private string CachePath(string dc)
    {
        if (!OfficialCrystallineConflictRankRules.IsSupportedRegion(dc)) throw new InvalidDataException("Unknown cache region.");
        return Path.Combine(cacheDirectory, $"official-cc-ranks-{dc}.json");
    }

    private void SaveCache(OfficialRankCache cache)
    {
        var path = CachePath(cache.Region);
        var temporary = path + $".{Guid.NewGuid():N}.tmp";
        Directory.CreateDirectory(cacheDirectory);
        try
        {
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, cache);
                stream.Flush(true);
            }
            File.Move(temporary, path, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static string CacheMessage(OfficialRankCache cache) => cache.Season <= 0
        ? "No saved standings yet. Downloads wait until outside combat and duties."
        : $"Season {cache.Season} · {cache.Entries.Length} listed players · saved {cache.FetchedAt.ToLocalTime():g}";

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (started) framework.Update -= OnUpdate;
        cancellation?.Cancel();
        http.Dispose();
        // Never wait for network/disk work on the game thread.
    }
}
