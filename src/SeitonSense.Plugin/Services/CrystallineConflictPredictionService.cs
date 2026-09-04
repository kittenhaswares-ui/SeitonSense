using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Party;
using Dalamud.Game.DutyState;
using Dalamud.Plugin.Services;
using SeitonSense.Core;
using SeitonSense.Plugin.Models;

namespace SeitonSense.Plugin.Services;

internal sealed record CrystallineConflictPredictionPlayerSnapshot(
    int Slot,
    string Name,
    uint JobId,
    bool IsLocal,
    bool IsCurrentlyDead,
    long Wins,
    long Losses,
    int Deaths,
    long DamageDealt,
    long HealingDone,
    int? CrystalSeconds,
    bool IsFinal);

internal sealed record CrystallineConflictPredictionSnapshot(
    bool IsActive,
    bool IsComplete,
    bool HasCombatStarted,
    bool IsFinal,
    bool LiveTotalsIncomplete,
    double StartWinChance,
    double CurrentWinChance,
    int KnownAllyRecords,
    int KnownEnemyRecords,
    CrystallineConflictPredictionPlayerSnapshot[] Allies,
    CrystallineConflictPredictionPlayerSnapshot[] Enemies,
    string Status)
{
    internal static CrystallineConflictPredictionSnapshot Inactive(string status = "Waiting for CC") =>
        new(false, false, false, false, false, 0.5d, 0.5d, 0, 0, [], [], status);

    internal static CrystallineConflictPredictionSnapshot Preparing(
        string status = "Waiting for exact 5 + 5 roster") =>
        new(true, false, false, false, false, 0.5d, 0.5d, 0, 0, [], [], status);
}

/// <summary>
/// Builds one local, playful CC estimate from locally observed player history.
/// Current team arrangement and combat totals stay in memory. When local player
/// history is enabled, the shared statistics store also keeps bounded opponent names,
/// Home Worlds, aggregate W/L, enemy-only head-to-head W/L, and HMAC lookup keys
/// on this PC; none of that history is uploaded.
/// </summary>
internal sealed class CrystallineConflictPredictionService : IDisposable
{
    private const long UpdateIntervalMilliseconds = 100;
    private const long MaximumObservedStatTotal = 100_000_000;

    private readonly PluginConfiguration configuration;
    private readonly IClientState clientState;
    private readonly IPlayerState playerState;
    private readonly IObjectTable objectTable;
    private readonly IPartyList partyList;
    private readonly IFramework framework;
    private readonly IDutyState dutyState;
    private readonly ICondition condition;
    private readonly CrystallineConflictMapStatisticsService mapStatistics;
    private readonly CrystallineConflictPredictionCaptureBuffer captureBuffer;
    private readonly IPluginLog log;
    private PlayerRuntime[]? roster;
    private CrystallineConflictPredictionSnapshot snapshot =
        CrystallineConflictPredictionSnapshot.Inactive();
    private long matchGeneration;
    private long nextUpdateAt;
    private uint activeTerritoryId;
    private CrystallineConflictStartPrediction openingPrediction;
    private bool hasOpeningPrediction;
    private bool combatStarted;
    private bool finalResultObserved;
    private bool started;
    private bool disposed;

    internal CrystallineConflictPredictionService(
        PluginConfiguration configuration,
        IClientState clientState,
        IPlayerState playerState,
        IObjectTable objectTable,
        IPartyList partyList,
        IFramework framework,
        IDutyState dutyState,
        ICondition condition,
        CrystallineConflictMapStatisticsService mapStatistics,
        CrystallineConflictPredictionCaptureBuffer captureBuffer,
        IPluginLog log)
    {
        this.configuration = configuration;
        this.clientState = clientState;
        this.playerState = playerState;
        this.objectTable = objectTable;
        this.partyList = partyList;
        this.framework = framework;
        this.dutyState = dutyState;
        this.condition = condition;
        this.mapStatistics = mapStatistics;
        this.captureBuffer = captureBuffer;
        this.log = log;
    }

    internal CrystallineConflictPredictionSnapshot Snapshot => Volatile.Read(ref snapshot);

    internal void Start()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (started) return;
        started = true;
        framework.Update += OnFrameworkUpdate;
        dutyState.DutyStarted += OnDutyStarted;
        mapStatistics.ConfirmedMatch += OnConfirmedMatch;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (started)
        {
            framework.Update -= OnFrameworkUpdate;
            dutyState.DutyStarted -= OnDutyStarted;
            mapStatistics.ConfirmedMatch -= OnConfirmedMatch;
        }

        captureBuffer.SetEnabled(false);
        roster = null;
        Volatile.Write(ref snapshot, CrystallineConflictPredictionSnapshot.Inactive("Disposed"));
    }

    private void OnDutyStarted(IDutyStateEventArgs _)
    {
        Reset("New duty started");
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        if (disposed || Environment.TickCount64 < nextUpdateAt) return;
        nextUpdateAt = Environment.TickCount64 + UpdateIntervalMilliseconds;

        try
        {
            if (!IsExactContext())
            {
                if (roster is not null || Snapshot.IsActive) Reset("Outside public CC");
                return;
            }

            if (!configuration.Enabled || !configuration.ShowCrystallineConflictPredictionPanel)
            {
                // Drop the frozen roster as well as the capture generation.
                // Otherwise re-enabling the panel in the same duty would keep
                // the old roster but never turn ActionEffect capture back on.
                if (roster is not null || Snapshot.IsActive || captureBuffer.Enabled)
                    Reset("Prediction panel disabled");
                return;
            }

            if (activeTerritoryId != clientState.TerritoryType)
                Reset("CC territory changed");
            activeTerritoryId = clientState.TerritoryType;

            if (roster is null)
            {
                if (!TryFreezeRoster(out var frozenRoster))
                {
                    Volatile.Write(
                        ref snapshot,
                        CrystallineConflictPredictionSnapshot.Preparing());
                    return;
                }

                // A failed read produces an empty candidate array. Publish it
                // only on success so preparation can retry missing actors.
                roster = frozenRoster;
                matchGeneration = matchGeneration == long.MaxValue ? 1 : matchGeneration + 1;
                finalResultObserved = false;
            }

            // The exact roster is useful as soon as team reveal completes.
            // Live counters remain closed until the first combat frame, then
            // stay open for the rest of this match even if the flag flickers.
            combatStarted |= condition[ConditionFlag.InCombat];
            var liveInputsEnabled =
                CrystallineConflictPredictionRules.CanUseLiveMatchInputs(
                    exactRosterAvailable: roster is not null,
                    combatStarted,
                    finalResultObserved);
            captureBuffer.SetEnabled(liveInputsEnabled);
            if (liveInputsEnabled)
            {
                DrainObservedEffects();
                UpdateDeaths();
            }
            else
            {
                captureBuffer.Clear();
            }
            PublishSnapshot();
        }
        catch (Exception exception)
        {
            // A transient roster/object-table failure must not leave a valid
            // roster paired with a permanently disabled capture generation.
            // Resetting makes the next clean update freeze all ten players
            // again before direct combat totals can resume.
            Reset("Prediction failed closed");
            log.Error(exception, "The local CC prediction failed closed for one update.");
        }
    }

    private void OnConfirmedMatch(ConfirmedCrystallineConflictMatchResult result)
    {
        try
        {
            if (disposed ||
                !result.IsPvpExcludingWolvesDen ||
                result.LocalContentId == 0 ||
                result.LocalContentId != playerState.ContentId ||
                result.TerritoryId != clientState.TerritoryType)
            {
                return;
            }

            if (roster is null)
            {
                if (!TryCreateRosterFromResult(result, out var resultRoster)) return;
                roster = resultRoster;
            }
            var byIdentity = new Dictionary<string, CapturedMapResultParticipant>(
                result.Participants.Length,
                StringComparer.Ordinal);
            foreach (var participant in result.Participants)
            {
                if (!TryIdentityKey(
                        participant.PlayerName,
                        participant.WorldId,
                        out var identity) ||
                    !byIdentity.TryAdd(identity, participant))
                {
                    return;
                }
            }

            if (byIdentity.Count != 10) return;
            foreach (var player in roster)
            {
                if (!byIdentity.TryGetValue(player.IdentityKey, out var final) ||
                    (final.Team == result.LocalTeam) != player.IsAllied ||
                    (final.ContentId == result.LocalContentId) != player.IsLocal ||
                    final.ClassJobId != player.JobId)
                {
                    // Never label a mixture of exact and live-observed rows as
                    // the final scoreboard. Validate the entire frozen roster
                    // before mutating even one row.
                    return;
                }
            }

            foreach (var player in roster)
            {
                var final = byIdentity[player.IdentityKey];
                player.Deaths = final.Deaths;
                player.DamageDealt = Math.Max(0, final.DamageDealt);
                player.HealingDone = Math.Max(0, final.HpRestored);
                player.CrystalSeconds = final.TimeOnCrystal;
                player.IsCurrentlyDead = false;
            }

            finalResultObserved = true;
            captureBuffer.SetEnabled(false);
            PublishSnapshot();
        }
        catch (Exception exception)
        {
            log.Error(exception, "The confirmed CC result could not finalize the local prediction panel.");
        }
    }

    private bool TryFreezeRoster(out PlayerRuntime[] exactRoster)
    {
        exactRoster = [];
        var local = objectTable.LocalPlayer;
        if (local is null || playerState.ContentId == 0) return false;

        var rows = new List<PlayerRuntime>(10);
        var seenEntities = new HashSet<uint>();
        var seenIdentities = new HashSet<string>(StringComparer.Ordinal);
        var partyMembers = partyList
            .Where(member => IsNetworkEntityId(member.EntityId))
            .ToArray();
        if (partyMembers.Length != 5) return false;

        for (var index = 0; index < partyMembers.Length; index++)
        {
            var member = partyMembers[index];
            var player = objectTable.SearchByEntityId(member.EntityId) as IPlayerCharacter;
            if (!TryCreateRuntime(
                    player,
                    index + 1,
                    allied: true,
                    local.EntityId,
                    out var runtime) ||
                !seenEntities.Add(runtime.EntityId) ||
                !seenIdentities.Add(runtime.IdentityKey))
            {
                return false;
            }

            rows.Add(runtime);
        }

        if (rows.Count(player => player.IsLocal) != 1) return false;
        for (var slot = 1; slot <= 5; slot++)
        {
            var enemy = EnemySlotResolver.Resolve(objectTable, slot);
            if (!TryCreateRuntime(
                    enemy,
                    slot,
                    allied: false,
                    local.EntityId,
                    out var runtime) ||
                !seenEntities.Add(runtime.EntityId) ||
                !seenIdentities.Add(runtime.IdentityKey))
            {
                return false;
            }

            rows.Add(runtime);
        }

        exactRoster = rows.ToArray();
        return exactRoster.Count(player => player.IsAllied) == 5 &&
               exactRoster.Count(player => !player.IsAllied) == 5;
    }

    private bool TryCreateRuntime(
        IPlayerCharacter? player,
        int slot,
        bool allied,
        uint localEntityId,
        out PlayerRuntime runtime)
    {
        runtime = null!;
        if (player is null ||
            !IsNetworkEntityId(player.EntityId) ||
            player.Address == 0 ||
            !player.ClassJob.IsValid ||
            !PvpRangeHelperRules.TryGetProfile(player.ClassJob.RowId, out _) ||
            player.HomeWorld.RowId is 0 or > ushort.MaxValue ||
            !TryIdentityKey(player.Name.ToString(), (ushort)player.HomeWorld.RowId, out var identity))
        {
            return false;
        }

        runtime = new PlayerRuntime(
            slot,
            player.EntityId,
            identity,
            player.Name.ToString().Trim(),
            (ushort)player.HomeWorld.RowId,
            player.ClassJob.RowId,
            allied,
            player.EntityId == localEntityId);
        return true;
    }

    private bool TryCreateRosterFromResult(
        ConfirmedCrystallineConflictMatchResult result,
        out PlayerRuntime[] resultRoster)
    {
        resultRoster = [];
        var rows = new List<PlayerRuntime>(10);
        var seenIdentities = new HashSet<string>(StringComparer.Ordinal);
        var allySlot = 0;
        var enemySlot = 0;
        foreach (var participant in result.Participants)
        {
            if (!TryIdentityKey(participant.PlayerName, participant.WorldId, out var identity) ||
                !seenIdentities.Add(identity))
                return false;
            var allied = participant.Team == result.LocalTeam;
            var slot = allied ? ++allySlot : ++enemySlot;
            rows.Add(new PlayerRuntime(
                slot,
                0,
                identity,
                participant.PlayerName.Trim(),
                participant.WorldId,
                participant.ClassJobId,
                allied,
                participant.ContentId == result.LocalContentId));
        }

        if (allySlot != 5 || enemySlot != 5 || rows.Count(row => row.IsLocal) != 1)
            return false;
        resultRoster = rows.ToArray();
        return true;
    }

    private void DrainObservedEffects()
    {
        if (roster is null || finalResultObserved)
        {
            captureBuffer.Clear();
            return;
        }

        var byEntity = roster
            .Where(player => IsNetworkEntityId(player.EntityId))
            .ToDictionary(player => player.EntityId);
        while (captureBuffer.TryDequeue(out var effect))
        {
            if (effect.AppliedToSource || !byEntity.ContainsKey(effect.TargetEntityId))
                continue;

            if (!byEntity.TryGetValue(effect.CasterEntityId, out var source))
            {
                var caster = objectTable.SearchByEntityId(effect.CasterEntityId);
                if (caster is null ||
                    !IsNetworkEntityId(caster.OwnerId) ||
                    !byEntity.TryGetValue(caster.OwnerId, out source))
                {
                    continue;
                }
            }

            if (effect.Kind == CrystallineConflictObservedEffectKind.Damage)
            {
                source.DamageDealt = SaturatingAdd(source.DamageDealt, effect.Amount);
            }
            else if (effect.Kind == CrystallineConflictObservedEffectKind.Healing)
            {
                source.HealingDone = SaturatingAdd(source.HealingDone, effect.Amount);
            }
        }
    }

    private void UpdateDeaths()
    {
        if (roster is null || finalResultObserved) return;
        foreach (var player in roster)
        {
            var actor = ResolvePlayer(player);
            var exactIdentity = actor is not null &&
                                TryIdentityKey(
                                    actor.Name.ToString(),
                                    (ushort)actor.HomeWorld.RowId,
                                    out var identity) &&
                                string.Equals(identity, player.IdentityKey, StringComparison.Ordinal);
            var dead = exactIdentity && (actor!.IsDead || actor.CurrentHp == 0);
            player.DeathState = CrystallineConflictPredictionRules.ObserveDeathEdge(
                player.DeathState,
                matchGeneration,
                exactContext: true,
                exactIdentity,
                dead);
            player.Deaths = player.DeathState.ObservedDeaths;
            player.IsCurrentlyDead = exactIdentity && dead;
            if (exactIdentity) player.EntityId = actor!.EntityId;
        }
    }

    private IPlayerCharacter? ResolvePlayer(PlayerRuntime player)
    {
        if (IsNetworkEntityId(player.EntityId) &&
            objectTable.SearchByEntityId(player.EntityId) is IPlayerCharacter direct)
        {
            return direct;
        }

        return objectTable.PlayerObjects
            .OfType<IPlayerCharacter>()
            .FirstOrDefault(candidate =>
                candidate.HomeWorld.RowId == player.WorldId &&
                string.Equals(
                    candidate.Name.ToString().Trim(),
                    player.Name,
                    StringComparison.OrdinalIgnoreCase));
    }

    private void PublishSnapshot()
    {
        if (roster is null) return;
        var localContentId = playerState.ContentId;
        var allied = roster.Where(player => player.IsAllied).OrderBy(player => player.Slot).ToArray();
        var enemies = roster.Where(player => !player.IsAllied).OrderBy(player => player.Slot).ToArray();
        if (allied.Length != 5 || enemies.Length != 5) return;

        var allyRecords = allied.Select(player => ReadRecord(localContentId, player)).ToArray();
        var enemyRecords = enemies.Select(player => ReadRecord(localContentId, player)).ToArray();
        if (!CrystallineConflictPredictionRules.TryCalculateStartPrediction(
                allyRecords,
                enemyRecords,
                out var calculatedOpening))
        {
            return;
        }

        // The opening estimate is a prediction, so it must not quietly learn
        // from the result that it is meant to predict. The shared store records
        // a confirmed result before publishing the final packet; retain the
        // first complete 5 + 5 calculation for the whole match generation.
        if (!hasOpeningPrediction)
        {
            openingPrediction = calculatedOpening;
            hasOpeningPrediction = true;
        }

        var opening = openingPrediction;

        var currentChance = opening.OwnTeamWinProbability;
        var hasProgress = false;
        var ownProgress = 0;
        var enemyProgress = 0;
        var local = objectTable.LocalPlayer;
        var liveInputsEnabled =
            CrystallineConflictPredictionRules.CanUseLiveMatchInputs(
                exactRosterAvailable: true,
                combatStarted,
                finalResultObserved);
        if (liveInputsEnabled && local is not null)
        {
            hasProgress = CrystallineConflictPredictionDirectorReader.TryReadTeamProgress(
                local.EntityId,
                out ownProgress,
                out enemyProgress);
        }

        if (configuration.EnableDynamicCrystallineConflictPrediction &&
            liveInputsEnabled &&
            CrystallineConflictPredictionRules.TryApplyLiveAdjustment(
                opening,
                new CrystallineConflictLivePredictionObservation(
                    hasProgress,
                    ownProgress,
                    enemyProgress,
                    HasDeathCounts: true,
                    allied.Sum(player => player.Deaths),
                    enemies.Sum(player => player.Deaths)),
                out var live))
        {
            currentChance = live.OwnTeamWinProbability;
        }

        var next = new CrystallineConflictPredictionSnapshot(
            true,
            true,
            combatStarted,
            finalResultObserved,
            liveInputsEnabled,
            opening.OwnTeamWinProbability,
            currentChance,
            opening.KnownOwnPlayers,
            opening.KnownEnemyPlayers,
            allied.Select((player, index) => ToSnapshot(player, allyRecords[index])).ToArray(),
            enemies.Select((player, index) => ToSnapshot(player, enemyRecords[index])).ToArray(),
            finalResultObserved
                ? "FINAL SCOREBOARD"
                : combatStarted
                    ? "LOCAL LIVE ESTIMATE"
                    : "LOCAL START ESTIMATE");
        Volatile.Write(ref snapshot, next);
    }

    private CrystallineConflictMapWinLossSnapshot ReadRecord(
        ulong localContentId,
        PlayerRuntime player)
    {
        return configuration.EnableLocalCrystallineConflictPlayerHistory &&
               mapStatistics.TryGetObservedPlayerRecord(
                   localContentId,
                   player.Name,
                   player.WorldId,
                   player.IsLocal,
                   player.IsAllied,
                   out var record)
            ? record
            : default;
    }

    private CrystallineConflictPredictionPlayerSnapshot ToSnapshot(
        PlayerRuntime player,
        CrystallineConflictMapWinLossSnapshot record) =>
        new(
            player.Slot,
            player.Name,
            player.JobId,
            player.IsLocal,
            player.IsCurrentlyDead,
            record.Wins,
            record.Losses,
            player.Deaths,
            player.DamageDealt,
            player.HealingDone,
            player.CrystalSeconds,
            finalResultObserved);

    private bool IsExactContext() =>
        clientState.IsPvPExcludingDen &&
        PvPMatchRules.IsPublicCrystallineConflictTerritory(clientState.TerritoryType);

    private void Reset(string status)
    {
        captureBuffer.SetEnabled(false);
        roster = null;
        activeTerritoryId = clientState.TerritoryType;
        openingPrediction = default;
        hasOpeningPrediction = false;
        combatStarted = false;
        finalResultObserved = false;
        Volatile.Write(ref snapshot, CrystallineConflictPredictionSnapshot.Inactive(status));
    }

    private static bool TryIdentityKey(string? name, ushort worldId, out string identity)
    {
        identity = string.Empty;
        var normalized = name?.Trim();
        if (worldId == 0 ||
            string.IsNullOrWhiteSpace(normalized) ||
            normalized.Length is < 3 or > 42 ||
            normalized.Any(char.IsControl))
        {
            return false;
        }

        identity = $"{worldId}|{normalized.ToUpperInvariant()}";
        return true;
    }

    private static long SaturatingAdd(long current, uint amount) =>
        Math.Min(MaximumObservedStatTotal, current + amount);

    private static bool IsNetworkEntityId(uint entityId) =>
        entityId is not (0 or 0xE0000000u);

    private sealed class PlayerRuntime(
        int slot,
        uint entityId,
        string identityKey,
        string name,
        ushort worldId,
        uint jobId,
        bool isAllied,
        bool isLocal)
    {
        internal int Slot { get; } = slot;
        internal uint EntityId { get; set; } = entityId;
        internal string IdentityKey { get; } = identityKey;
        internal string Name { get; } = name;
        internal ushort WorldId { get; } = worldId;
        internal uint JobId { get; } = jobId;
        internal bool IsAllied { get; } = isAllied;
        internal bool IsLocal { get; } = isLocal;
        internal CrystallineConflictObservedDeathState DeathState { get; set; } =
            CrystallineConflictObservedDeathState.Initial;
        internal bool IsCurrentlyDead { get; set; }
        internal int Deaths { get; set; }
        internal long DamageDealt { get; set; }
        internal long HealingDone { get; set; }
        internal int? CrystalSeconds { get; set; }
    }
}
