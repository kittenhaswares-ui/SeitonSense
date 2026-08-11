using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Party;
using Dalamud.Plugin.Services;
using SeitonSense.Core;
using SeitonSense.Plugin.Models;

namespace SeitonSense.Plugin.Services;

internal sealed class ExecuteTracker : IDisposable
{
    private const long UpdateIntervalMilliseconds = 50;

    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly IFramework framework;
    private readonly IDutyState dutyState;
    private readonly IPartyList partyList;
    private readonly IPluginLog log;
    private readonly PluginConfiguration configuration;
    private readonly PvPMetadataValidation metadata;
    private readonly Dictionary<uint, EnemyRuntimeState> runtimeStates = [];
    private EnemyHudSnapshot[] enemies = [];
    private SeitonPopupSnapshot[] popups = [];
    private TrackerDiagnostics diagnostics;
    private long nextUpdateAt;
    private long nextErrorLogAt;
    private uint activeTerritory;
    private bool started;
    private bool disposed;

    public ExecuteTracker(
        IClientState clientState,
        IObjectTable objectTable,
        IFramework framework,
        IDutyState dutyState,
        IPartyList partyList,
        IPluginLog log,
        PluginConfiguration configuration,
        PvPMetadataValidation metadata)
    {
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.framework = framework;
        this.dutyState = dutyState;
        this.partyList = partyList;
        this.log = log;
        this.configuration = configuration;
        this.metadata = metadata;
        diagnostics = TrackerDiagnostics.Inactive(clientState.TerritoryType, metadata);
    }

    public bool IsActive => Diagnostics.Active;
    public IReadOnlyList<EnemyHudSnapshot> Enemies => Volatile.Read(ref enemies);
    public IReadOnlyList<SeitonPopupSnapshot> Popups => Volatile.Read(ref popups);
    public TrackerDiagnostics Diagnostics => Volatile.Read(ref diagnostics);

    public void Start()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (started) return;
        started = true;
        framework.Update += OnFrameworkUpdate;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (started) framework.Update -= OnFrameworkUpdate;
        ResetRuntime();
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        if (disposed || Environment.TickCount64 < nextUpdateAt) return;
        nextUpdateAt = Environment.TickCount64 + UpdateIntervalMilliseconds;

        try
        {
            UpdateSnapshot();
        }
        catch (Exception exception)
        {
            ResetRuntime();
            Volatile.Write(ref diagnostics, TrackerDiagnostics.Inactive(clientState.TerritoryType, metadata));
            var now = Environment.TickCount64;
            if (now < nextErrorLogAt) return;
            nextErrorLogAt = now + 10_000;
            log.Error(exception, "Seiton Sense scan failed closed.");
        }
    }

    private void UpdateSnapshot()
    {
        var now = Environment.TickCount64;
        if (clientState.TerritoryType != activeTerritory)
        {
            ResetRuntime();
            activeTerritory = clientState.TerritoryType;
        }

        var condition = dutyState.ContentFinderCondition;
        var conditionId = condition.IsValid ? condition.RowId : 0;
        var categoryId = condition.IsValid ? condition.Value.ContentUICategory.RowId : 0;
        var conditionPvP = condition.IsValid && condition.Value.PvP;
        var casual = condition.IsValid && condition.Value.CrystallineConflictCasualRoulette;
        var ranked = condition.IsValid && condition.Value.CrystallineConflictRankedRoulette;
        var isCc = PvPMatchRules.IsCrystallineConflict(
            clientState.IsPvPExcludingDen,
            clientState.TerritoryType,
            condition.IsValid,
            conditionPvP,
            categoryId,
            casual,
            ranked);

        var localPlayer = objectTable.LocalPlayer;
        var isNinja = localPlayer is not null &&
                      ExecuteThreshold.IsNinja(localPlayer.ClassJob.IsValid ? localPlayer.ClassJob.RowId : 0);
        if (!configuration.Enabled || !isCc || localPlayer is null)
        {
            ResetRuntime();
            Volatile.Write(ref diagnostics, new TrackerDiagnostics(
                false,
                metadata.SeitonVerified,
                metadata.GuardVerified,
                metadata.RecuperateVerified,
                isNinja,
                isCc,
                clientState.IsPvP,
                clientState.TerritoryType,
                conditionId,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0));
            return;
        }

        var partyEntityIds = partyList
            .Select(member => member.EntityId)
            .Where(IsNetworkEntityId)
            .ToHashSet();
        var visibleEntityIds = objectTable.PlayerObjects
            .OfType<IPlayerCharacter>()
            .Select(player => player.EntityId)
            .Where(IsNetworkEntityId)
            .ToHashSet();
        var partyFallbackReady = PvPMatchRules.IsPublicCrystallineConflictTerritory(clientState.TerritoryType) &&
                                 partyEntityIds.Count == 5 &&
                                 partyEntityIds.Contains(localPlayer.EntityId) &&
                                 partyEntityIds.IsSubsetOf(visibleEntityIds);

        var seitonActionId = 0u;
        var localAlive = !localPlayer.IsDead && localPlayer.CurrentHp > 0;
        var seitonResourceReady = isNinja &&
                                  localAlive &&
                                  metadata.SeitonVerified &&
                                  SeitonReadinessProbe.TryGetReadyAction(localPlayer, out seitonActionId);

        var snapshots = new List<EnemyHudSnapshot>(5);
        var activePopups = Popups
            .Where(popup => popup.EndsAtMilliseconds > now)
            .ToList();
        var resolvedEntityIds = new HashSet<uint>();
        var resolvedSlots = 0;
        var validEnemySlots = 0;
        var inRangeSlots = 0;
        var seitonSlots = 0;
        var guardUnavailableSlots = 0;
        var lowMpSlots = 0;

        for (var slot = EnemySlotRules.FirstSlot; slot <= EnemySlotRules.LastSlot; slot++)
        {
            var player = EnemySlotResolver.Resolve(objectTable, slot);
            if (player is null || !resolvedEntityIds.Add(player.EntityId)) continue;
            resolvedSlots++;

            var isAlly = partyEntityIds.Contains(player.EntityId) ||
                         (player.StatusFlags & (StatusFlags.PartyMember | StatusFlags.AllianceMember)) != 0;
            var isEnemy = player.GameObjectId != localPlayer.GameObjectId &&
                          !isAlly &&
                          ((player.StatusFlags & StatusFlags.Hostile) != 0 || partyFallbackReady);
            if (!isEnemy) continue;

            if (!runtimeStates.TryGetValue(player.EntityId, out var state))
            {
                state = new EnemyRuntimeState();
                runtimeStates[player.EntityId] = state;
            }

            var alive = !player.IsDead && player.CurrentHp > 0;
            if (!alive)
            {
                state.WasDead = true;
                state.SeitonCue = PersistentSeitonCueState.Initial;
                state.SeitonPulseStartedAtMilliseconds = -1;
                activePopups.RemoveAll(popup => popup.GameObjectId == player.GameObjectId);
                continue;
            }

            if (state.WasDead)
            {
                state.WasDead = false;
                state.Guard = GuardCooldownRules.ObserveRevive();
                state.LowMp = LowMpState.Initial;
                state.SeitonCue = PersistentSeitonCueState.Initial;
                state.SeitonPulseStartedAtMilliseconds = -1;
            }

            if (!player.IsTargetable || !ExecuteThreshold.HasValidHp(player.CurrentHp, player.MaxHp)) continue;
            validEnemySlots++;

            if (metadata.GuardVerified && TryGetGuardRemainingMilliseconds(player, out var guardRemaining))
            {
                state.Guard = GuardCooldownRules.ObserveStatus(state.Guard, now, guardRemaining);
            }

            if (!metadata.GuardVerified)
            {
                state.Guard = GuardCooldownRules.HardReset();
            }

            var plausibleMp = player.MaxMp > 0 && player.CurrentMp <= player.MaxMp;
            var trustedMp = plausibleMp && (player.CurrentMp > 0 || state.LowMp.HasTrustedSample);
            state.LowMp = metadata.RecuperateVerified
                ? LowMpRules.Observe(
                    state.LowMp,
                    (int)Math.Min(player.CurrentMp, int.MaxValue),
                    trustedMp,
                    now)
                : LowMpRules.Observe(state.LowMp, 0, false, now, hardReset: true);

            var belowHalf = ExecuteThreshold.IsBelowHalf(player.CurrentHp, player.MaxHp);
            var preparationBand = PersistentSeitonCueRules.IsPreparationBand(
                player.CurrentHp,
                player.MaxHp);
            var inRange = false;
            if (seitonResourceReady && (belowHalf || preparationBand))
            {
                inRange = SeitonReadinessProbe.HasRangeAndLineOfSight(
                    localPlayer,
                    player,
                    seitonActionId,
                    out _);
                if (inRange) inRangeSlots++;
            }

            var cueDecision = PersistentSeitonCueRules.Observe(
                state.SeitonCue,
                seitonResourceReady,
                targetPresent: true,
                trustedHealthSample: true,
                player.CurrentHp,
                player.MaxHp,
                inRange,
                configuration.ShowSeitonPreparation,
                now,
                hardReset: !isNinja || !metadata.SeitonVerified);
            state.SeitonCue = cueDecision.NextState;
            var showSeiton = cueDecision.Cue == SeitonCueKind.Execute;
            if (showSeiton) seitonSlots++;

            if (!showSeiton)
                activePopups.RemoveAll(popup => popup.GameObjectId == player.GameObjectId);

            if (cueDecision.TriggerEntryPulse)
            {
                state.SeitonPulseStartedAtMilliseconds = now;
                activePopups.RemoveAll(popup => popup.GameObjectId == player.GameObjectId);
                var duration = (long)Math.Clamp(configuration.PopupDurationMilliseconds, 300f, 2000f);
                activePopups.Add(new SeitonPopupSnapshot(
                    player.GameObjectId,
                    slot,
                    player.ClassJob.IsValid ? player.ClassJob.RowId : 0,
                    now,
                    now + duration));
            }

            var guardUnavailable = metadata.GuardVerified &&
                                   GuardCooldownRules.ShouldShowCrossedIcon(state.Guard, now);
            var guardRemainingSeconds = guardUnavailable
                ? GuardCooldownRules.RemainingMilliseconds(state.Guard, now) / 1000f
                : 0f;
            var lowMp = metadata.RecuperateVerified && LowMpRules.ShouldShowCrossedIcon(state.LowMp);
            if (guardUnavailable) guardUnavailableSlots++;
            if (lowMp) lowMpSlots++;

            snapshots.Add(new EnemyHudSnapshot(
                slot,
                player.GameObjectId,
                player.EntityId,
                player.ClassJob.IsValid ? player.ClassJob.RowId : 0,
                cueDecision.Cue,
                state.SeitonPulseStartedAtMilliseconds,
                guardUnavailable,
                guardRemainingSeconds,
                lowMp,
                player.CurrentMp,
                player.MaxMp));
        }

        snapshots.Sort(static (left, right) => left.Slot.CompareTo(right.Slot));
        activePopups.Sort(static (left, right) => left.Slot.CompareTo(right.Slot));
        Interlocked.Exchange(ref enemies, snapshots.ToArray());
        Interlocked.Exchange(ref popups, activePopups.ToArray());
        Volatile.Write(ref diagnostics, new TrackerDiagnostics(
            true,
            metadata.SeitonVerified,
            metadata.GuardVerified,
            metadata.RecuperateVerified,
            isNinja,
            true,
            clientState.IsPvP,
            clientState.TerritoryType,
            conditionId,
            resolvedSlots,
            validEnemySlots,
            inRangeSlots,
            seitonSlots,
            guardUnavailableSlots,
            lowMpSlots,
            activePopups.Count,
            seitonActionId));
    }

    private static bool TryGetGuardRemainingMilliseconds(
        IPlayerCharacter player,
        out long remainingMilliseconds)
    {
        var bestRemaining = 0f;
        foreach (var status in player.StatusList)
        {
            if (status.StatusId is not (EnemyCombatConstants.GuardStatusId or EnemyCombatConstants.GuardStatusAlternateId) ||
                !float.IsFinite(status.RemainingTime) ||
                status.RemainingTime <= 0f)
            {
                continue;
            }

            bestRemaining = Math.Max(bestRemaining, status.RemainingTime);
        }

        if (bestRemaining <= 0f)
        {
            remainingMilliseconds = 0;
            return false;
        }

        remainingMilliseconds = (long)Math.Round(
            Math.Clamp(bestRemaining, 0f, EnemyCombatConstants.GuardDurationSeconds) * 1000f);
        return true;
    }

    private void ResetRuntime()
    {
        runtimeStates.Clear();
        Interlocked.Exchange(ref enemies, []);
        Interlocked.Exchange(ref popups, []);
    }

    private static bool IsNetworkEntityId(uint entityId) => entityId is not 0 and not 0xE0000000;

    private sealed class EnemyRuntimeState
    {
        public GuardCooldownState Guard { get; set; } = GuardCooldownState.Initial;
        public LowMpState LowMp { get; set; } = LowMpState.Initial;
        public PersistentSeitonCueState SeitonCue { get; set; } = PersistentSeitonCueState.Initial;
        public long SeitonPulseStartedAtMilliseconds { get; set; } = -1;
        public bool WasDead { get; set; }
    }
}
