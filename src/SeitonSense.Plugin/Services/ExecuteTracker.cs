using System.Numerics;
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
    private readonly bool metadataVerified;
    private readonly Dictionary<uint, ExecuteAlertState> alertStates = [];
    private EnemyExecuteSnapshot[] enemies = [];
    private FlashSnapshot flash = FlashSnapshot.None;
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
        bool metadataVerified)
    {
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.framework = framework;
        this.dutyState = dutyState;
        this.partyList = partyList;
        this.log = log;
        this.configuration = configuration;
        this.metadataVerified = metadataVerified;
        diagnostics = TrackerDiagnostics.Inactive(clientState.TerritoryType, metadataVerified);
    }

    public bool IsActive => Diagnostics.Active;
    public IReadOnlyList<EnemyExecuteSnapshot> Enemies => Volatile.Read(ref enemies);
    public FlashSnapshot Flash => Volatile.Read(ref flash);
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
            Volatile.Write(ref diagnostics, TrackerDiagnostics.Inactive(clientState.TerritoryType, metadataVerified));
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
        if (!configuration.Enabled || !metadataVerified || !isCc || !isNinja || localPlayer is null)
        {
            ResetRuntime();
            Volatile.Write(ref diagnostics, new TrackerDiagnostics(
                false,
                metadataVerified,
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
                false));
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

        var resolvedEntityIds = new HashSet<uint>();
        var snapshots = new List<EnemyExecuteSnapshot>(5);
        var flashSlots = new List<string>(5);
        var resolvedSlots = 0;
        var validEnemySlots = 0;
        var inRangeSlots = 0;
        var readySlots = 0;
        var resolvedSeitonActionId = 0u;
        for (var slot = EnemySlotRules.FirstSlot; slot <= EnemySlotRules.LastSlot; slot++)
        {
            var player = EnemySlotResolver.Resolve(objectTable, slot);
            if (player is null || !resolvedEntityIds.Add(player.EntityId)) continue;
            resolvedSlots++;

            var isAlly = partyEntityIds.Contains(player.EntityId) ||
                         (player.StatusFlags & (StatusFlags.PartyMember | StatusFlags.AllianceMember)) != 0;
            var usable = EnemySlotRules.CanUseResolvedEnemy(
                player.GameObjectId == localPlayer.GameObjectId,
                isAlly,
                (player.StatusFlags & StatusFlags.Hostile) != 0,
                partyFallbackReady,
                !player.IsDead,
                player.IsTargetable,
                player.CurrentHp,
                player.MaxHp);
            if (!usable)
            {
                if (player.IsDead || player.CurrentHp == 0) alertStates.Remove(player.EntityId);
                continue;
            }

            validEnemySlots++;
            var actionReady = false;
            var inRange = false;
            if (ExecuteThreshold.IsBelowHalf(player.CurrentHp, player.MaxHp))
            {
                actionReady = SeitonReadinessProbe.IsAvailableForTarget(
                    localPlayer,
                    player,
                    out var actionId,
                    out _,
                    out var rangeStatus);
                inRange = rangeStatus == 0;
                if (actionId != 0) resolvedSeitonActionId = actionId;
                if (inRange) inRangeSlots++;
                if (actionReady) readySlots++;
            }

            var previous = alertStates.GetValueOrDefault(player.EntityId, ExecuteAlertState.Initial);
            var decision = ExecuteAlertRules.Observe(
                previous,
                player.CurrentHp,
                player.MaxHp,
                inRange && actionReady,
                now);
            alertStates[player.EntityId] = decision.NextState;
            if (decision.TriggerFlash) flashSlots.Add(EnemySlotRules.Label(slot));
            if (!decision.ShowLabel) continue;

            snapshots.Add(new EnemyExecuteSnapshot(
                slot,
                player.Position,
                player.CurrentHp,
                player.MaxHp));
        }

        if (flashSlots.Count > 0)
        {
            var duration = (long)Math.Clamp(configuration.FlashDurationMilliseconds, 200f, 1000f);
            Volatile.Write(ref flash, new FlashSnapshot(now, now + duration, string.Join(" + ", flashSlots)));
        }
        else if (Flash.EndsAtMilliseconds <= now)
        {
            Volatile.Write(ref flash, FlashSnapshot.None);
        }

        snapshots.Sort(static (left, right) => left.Slot.CompareTo(right.Slot));
        Interlocked.Exchange(ref enemies, snapshots.ToArray());
        Volatile.Write(ref diagnostics, new TrackerDiagnostics(
            true,
            true,
            true,
            true,
            clientState.IsPvP,
            clientState.TerritoryType,
            conditionId,
            resolvedSlots,
            validEnemySlots,
            inRangeSlots,
            readySlots,
            snapshots.Count,
            resolvedSeitonActionId,
            Flash.EndsAtMilliseconds > now));
    }

    private void ResetRuntime()
    {
        alertStates.Clear();
        Interlocked.Exchange(ref enemies, []);
        Volatile.Write(ref flash, FlashSnapshot.None);
    }

    private static bool IsNetworkEntityId(uint entityId) => entityId is not 0 and not 0xE0000000;

}
