using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SeitonSense.Core;

namespace SeitonSense.Plugin.Services;

internal enum OpponentLimitBreakGaugeFailure : byte
{
    None = 0,
    Disabled = 1,
    Context = 2,
    Controller = 3,
    Addon = 4,
    Identity = 5,
    Hierarchy = 6,
    Unstable = 7,
    LocalMismatch = 8,
}

internal readonly record struct OpponentLimitBreakGaugeDiagnostics(
    bool Active,
    bool ControllerStable,
    bool AllyRowsStable,
    bool EnemyRowsStable,
    bool LocalCrossCheckPassed,
    int KnownEnemyCount,
    OpponentLimitBreakGaugeFailure Failure)
{
    internal static OpponentLimitBreakGaugeDiagnostics Inactive(
        OpponentLimitBreakGaugeFailure failure) =>
        new(false, false, false, false, false, 0, failure);

    internal string ToChatLine() =>
        $"LB-bars active={Active},controller={ControllerStable},rows={AllyRowsStable}/{EnemyRowsStable}," +
        $"local-proof={LocalCrossCheckPassed},known={KnownEnemyCount}/5,reason={Failure}";
}

internal sealed record OpponentLimitBreakGaugeSnapshot(
    bool Active,
    long PublishedAtMilliseconds,
    IReadOnlyList<OpponentLimitBreakGaugeValue> Enemies,
    OpponentLimitBreakGaugeDiagnostics Diagnostics)
{
    internal static OpponentLimitBreakGaugeSnapshot Inactive(
        OpponentLimitBreakGaugeFailure failure = OpponentLimitBreakGaugeFailure.Disabled) =>
        new(false, -1, [], OpponentLimitBreakGaugeDiagnostics.Inactive(failure));
}

/// <summary>
/// Reads direct current/min/max values from the native CC GaugeBar components.
/// Every publication requires stable before/after row captures and a same-frame
/// proof against the local LimitBreakController. Native nodes are never changed
/// and pointer fingerprints never leave the capture call.
/// </summary>
internal sealed class OpponentLimitBreakGaugeService : IDisposable
{
    private const long UpdateIntervalMilliseconds = 50;
    private const long ErrorLogThrottleMilliseconds = 10_000;
    private const string AllyAddonName = "PvPMKSPartyList1";
    private const string EnemyAddonName = "PvPMKSPartyList3";
    private const uint FirstRowNodeId = 6;
    private const uint GaugeComponentId = 2;

    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly IFramework framework;
    private readonly IGameGui gameGui;
    private readonly ExecuteTracker executeTracker;
    private readonly IPluginLog log;
    private readonly Func<bool> enabledProvider;
    private OpponentLimitBreakGaugeSnapshot snapshot = OpponentLimitBreakGaugeSnapshot.Inactive();
    private long nextUpdateAtMilliseconds;
    private long nextErrorLogAtMilliseconds;
    private bool started;
    private bool disposed;

    internal OpponentLimitBreakGaugeService(
        IClientState clientState,
        IObjectTable objectTable,
        IFramework framework,
        IGameGui gameGui,
        ExecuteTracker executeTracker,
        IPluginLog log,
        Func<bool> enabledProvider)
    {
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.framework = framework;
        this.gameGui = gameGui;
        this.executeTracker = executeTracker;
        this.log = log;
        this.enabledProvider = enabledProvider ?? throw new ArgumentNullException(nameof(enabledProvider));
    }

    internal OpponentLimitBreakGaugeSnapshot Snapshot => Volatile.Read(ref snapshot);
    internal OpponentLimitBreakGaugeDiagnostics Diagnostics => Snapshot.Diagnostics;

    internal void Start()
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
        PublishInactive(OpponentLimitBreakGaugeFailure.Disabled);
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        var now = Environment.TickCount64;
        if (disposed || now < nextUpdateAtMilliseconds) return;
        nextUpdateAtMilliseconds = now + UpdateIntervalMilliseconds;

        try
        {
            UpdateSnapshot(now);
        }
        catch (Exception exception)
        {
            PublishInactive(OpponentLimitBreakGaugeFailure.Unstable);
            if (now < nextErrorLogAtMilliseconds) return;
            nextErrorLogAtMilliseconds = now + ErrorLogThrottleMilliseconds;
            log.Error(exception, "Seiton Sense opponent LB gauge observer failed closed.");
        }
    }

    private unsafe void UpdateSnapshot(long nowMilliseconds)
    {
        if (!enabledProvider())
        {
            PublishInactive(OpponentLimitBreakGaugeFailure.Disabled);
            return;
        }

        var tracker = executeTracker.Diagnostics;
        var localPlayer = objectTable.LocalPlayer;
        if (!IsExactCrystallineConflictContext(tracker) || !HasExactObjectIdentity(localPlayer))
        {
            PublishInactive(OpponentLimitBreakGaugeFailure.Context);
            return;
        }

        if (!TryCaptureController(out var controllerBefore))
        {
            PublishInactive(OpponentLimitBreakGaugeFailure.Controller);
            return;
        }

        var allyFailure = TryCaptureStableTeam(AllyAddonName, true, localPlayer!, out var allyRows);
        if (allyFailure != OpponentLimitBreakGaugeFailure.None)
        {
            PublishInactive(allyFailure);
            return;
        }

        var enemyFailure = TryCaptureStableTeam(EnemyAddonName, false, localPlayer!, out var enemyRows);
        if (enemyFailure != OpponentLimitBreakGaugeFailure.None)
        {
            PublishInactive(enemyFailure);
            return;
        }

        if (!TryCaptureController(out var controllerAfter) || controllerBefore != controllerAfter)
        {
            PublishInactive(OpponentLimitBreakGaugeFailure.Controller);
            return;
        }

        var localIdentity = CreateIdentity(localPlayer);
        var localMatches = allyRows.Where(row => row.Actor == localIdentity).ToArray();
        if (localMatches.Length != 1 ||
            !OpponentLimitBreakGaugeRules.MatchesLocalController(
                localMatches[0].MinimumValue,
                localMatches[0].CurrentValue,
                localMatches[0].MaximumValue,
                controllerAfter.CurrentUnits,
                controllerAfter.MaximumUnits))
        {
            PublishInactive(OpponentLimitBreakGaugeFailure.LocalMismatch);
            return;
        }


        var localGauge = localMatches[0];
        if (allyRows.Any(row => !OpponentLimitBreakGaugeRules.MatchesNativeScale(
                localGauge.MinimumValue,
                localGauge.MaximumValue,
                row.MinimumValue,
                row.MaximumValue)) ||
            enemyRows.Any(row => !OpponentLimitBreakGaugeRules.MatchesNativeScale(
                localGauge.MinimumValue,
                localGauge.MaximumValue,
                row.MinimumValue,
                row.MaximumValue)))
        {
            PublishInactive(OpponentLimitBreakGaugeFailure.LocalMismatch);
            return;
        }

        var enemies = new OpponentLimitBreakGaugeValue[OpponentLimitBreakGaugeRules.EnemyCount];
        foreach (var row in enemyRows)
        {
            if (!OpponentLimitBreakGaugeRules.TryCreateValue(
                    row.Actor,
                    row.Slot,
                    row.JobId,
                    row.MinimumValue,
                    row.CurrentValue,
                    row.MaximumValue,
                    out var value))
            {
                PublishInactive(OpponentLimitBreakGaugeFailure.Hierarchy);
                return;
            }

            enemies[row.Slot - OpponentLimitBreakGaugeRules.FirstEnemySlot] = value;
        }

        if (!OpponentLimitBreakGaugeRules.IsCompleteExactEnemySet(enemies))
        {
            PublishInactive(OpponentLimitBreakGaugeFailure.Identity);
            return;
        }

        Volatile.Write(
            ref snapshot,
            new OpponentLimitBreakGaugeSnapshot(
                true,
                nowMilliseconds,
                enemies,
                new OpponentLimitBreakGaugeDiagnostics(
                    true,
                    true,
                    true,
                    true,
                    true,
                    enemies.Length,
                    OpponentLimitBreakGaugeFailure.None)));
    }

    private bool IsExactCrystallineConflictContext(TrackerDiagnostics tracker) =>
        clientState.IsPvP &&
        clientState.IsPvPExcludingDen &&
        tracker.Active &&
        tracker.IsPvP &&
        tracker.IsCrystallineConflict &&
        !tracker.IsWolvesDen &&
        tracker.TerritoryId == clientState.TerritoryType &&
        tracker.SlotCapacity == OpponentLimitBreakGaugeRules.EnemyCount &&
        tracker.ResolvedSlots == OpponentLimitBreakGaugeRules.EnemyCount;

    private unsafe OpponentLimitBreakGaugeFailure TryCaptureStableTeam(
        string addonName,
        bool friendly,
        IPlayerCharacter localPlayer,
        out HudGaugeRowCapture[] stableRows)
    {
        stableRows = [];
        var addon = gameGui.GetAddonByName<AtkUnitBase>(addonName);
        if (!IsVisible(addon)) return OpponentLimitBreakGaugeFailure.Addon;

        var first = new HudGaugeRowCapture[OpponentLimitBreakGaugeRules.EnemyCount];
        var actors = new HashSet<TargetPressureActorIdentity>();
        var addresses = new HashSet<nint>();
        for (var slot = 1; slot <= OpponentLimitBreakGaugeRules.EnemyCount; slot++)
        {
            var player = friendly
                ? PartySlotResolver.Resolve(objectTable, slot)
                : EnemySlotResolver.Resolve(objectTable, slot);
            if (!HasExactObjectIdentity(player) ||
                !actors.Add(CreateIdentity(player)) ||
                !addresses.Add(player!.Address) ||
                !friendly &&
                (player.GameObjectId == localPlayer.GameObjectId ||
                 player.EntityId == localPlayer.EntityId ||
                 player.Address == localPlayer.Address))
            {
                return OpponentLimitBreakGaugeFailure.Identity;
            }

            var failure = TryCaptureRow(addon, slot, player, out first[slot - 1]);
            if (failure != OpponentLimitBreakGaugeFailure.None) return failure;
        }

        var addonAfter = gameGui.GetAddonByName<AtkUnitBase>(addonName);
        if (!IsVisible(addonAfter) || addonAfter != addon)
            return OpponentLimitBreakGaugeFailure.Unstable;

        var second = new HudGaugeRowCapture[OpponentLimitBreakGaugeRules.EnemyCount];
        for (var slot = 1; slot <= OpponentLimitBreakGaugeRules.EnemyCount; slot++)
        {
            var player = friendly
                ? PartySlotResolver.Resolve(objectTable, slot)
                : EnemySlotResolver.Resolve(objectTable, slot);
            if (!HasExactObjectIdentity(player)) return OpponentLimitBreakGaugeFailure.Identity;
            var failure = TryCaptureRow(addonAfter, slot, player!, out second[slot - 1]);
            if (failure != OpponentLimitBreakGaugeFailure.None) return failure;
            if (first[slot - 1] != second[slot - 1])
                return OpponentLimitBreakGaugeFailure.Unstable;
        }

        stableRows = second;
        return OpponentLimitBreakGaugeFailure.None;
    }

    private static unsafe OpponentLimitBreakGaugeFailure TryCaptureRow(
        AtkUnitBase* addon,
        int slot,
        IPlayerCharacter player,
        out HudGaugeRowCapture capture)
    {
        capture = default;
        var row = addon->GetComponentByNodeId(FirstRowNodeId + (uint)(slot - 1));
        if (row == null || row->AtkResNode == null || !IsNodeVisible(row->AtkResNode))
            return OpponentLimitBreakGaugeFailure.Hierarchy;

        var name = row->GetTextNodeById(21);
        if (name == null ||
            !string.Equals(name->GetText().ToString(), player.Name.TextValue, StringComparison.Ordinal))
        {
            return OpponentLimitBreakGaugeFailure.Identity;
        }

        var component = row->GetComponentById(GaugeComponentId);
        if (component == null ||
            component->AtkResNode == null ||
            component->OwnerNode == null ||
            component->GetComponentType() != ComponentType.GaugeBar ||
            !IsNodeVisible(component->AtkResNode))
        {
            return OpponentLimitBreakGaugeFailure.Hierarchy;
        }

        var gauge = (AtkComponentGaugeBar*)component;
        var minimum = gauge->MinValue;
        var maximum = gauge->MaxValue;
        var current = gauge->Values[0].ValueInt;
        if (maximum <= minimum || current < minimum || current > maximum)
            return OpponentLimitBreakGaugeFailure.Hierarchy;

        var jobId = player.ClassJob.IsValid ? player.ClassJob.RowId : 0;
        var fingerprint = CreateFingerprint(addon, row, component, player);
        if (jobId == 0 || fingerprint == 0) return OpponentLimitBreakGaugeFailure.Identity;

        capture = new HudGaugeRowCapture(
            slot,
            CreateIdentity(player),
            jobId,
            player.Address,
            fingerprint,
            minimum,
            current,
            maximum);
        return OpponentLimitBreakGaugeFailure.None;
    }

    private static unsafe bool TryCaptureController(out LimitControllerCapture capture)
    {
        capture = default;
        var controller = LimitBreakController.Instance();
        if (controller == null ||
            !controller->IsPvP ||
            controller->BarCount != 1 ||
            controller->BarUnits == 0 ||
            controller->CurrentUnits > controller->BarUnits)
        {
            return false;
        }

        capture = new LimitControllerCapture(
            unchecked((ulong)(nuint)controller),
            controller->CurrentUnits,
            controller->BarUnits);
        return capture.InstanceFingerprint != 0;
    }

    private bool HasExactObjectIdentity(IPlayerCharacter? player)
    {
        if (player is null ||
            player.Address == 0 ||
            !CreateIdentity(player).IsValid ||
            string.IsNullOrEmpty(player.Name.TextValue))
        {
            return false;
        }

        var current = objectTable.SearchByEntityId(player.EntityId) as IPlayerCharacter;
        return current is not null &&
               current.Address == player.Address &&
               current.GameObjectId == player.GameObjectId &&
               current.EntityId == player.EntityId;
    }

    private static TargetPressureActorIdentity CreateIdentity(IPlayerCharacter? player) =>
        player is null
            ? default
            : new TargetPressureActorIdentity(player.GameObjectId, player.EntityId);

    private static unsafe ulong CreateFingerprint(
        AtkUnitBase* addon,
        AtkComponentBase* row,
        AtkComponentBase* gauge,
        IPlayerCharacter player)
    {
        var hash = 14_695_981_039_346_656_037UL;
        hash = Add(hash, unchecked((ulong)(nuint)addon));
        hash = Add(hash, unchecked((ulong)(nuint)row));
        hash = Add(hash, unchecked((ulong)(nuint)gauge));
        hash = Add(hash, unchecked((ulong)(nuint)gauge->OwnerNode));
        hash = Add(hash, unchecked((ulong)player.Address));
        hash = Add(hash, player.GameObjectId);
        hash = Add(hash, player.EntityId);
        hash = Add(hash, (ulong)row->GetComponentType());
        hash = Add(hash, (ulong)gauge->GetComponentType());
        return hash == 0 ? 1 : hash;
    }

    private static ulong Add(ulong hash, ulong value)
    {
        const ulong prime = 1_099_511_628_211UL;
        for (var index = 0; index < sizeof(ulong); index++)
        {
            hash ^= (byte)(value >> (index * 8));
            hash *= prime;
        }

        return hash;
    }

    private static unsafe bool IsVisible(AtkUnitBase* addon) =>
        addon != null &&
        addon->IsVisible &&
        addon->VisibilityFlags != 1 &&
        IsNodeVisible(addon->RootNode);

    private static unsafe bool IsNodeVisible(AtkResNode* node)
    {
        var depth = 0;
        while (node != null && depth++ < 64)
        {
            if (!node->IsVisible()) return false;
            node = node->ParentNode;
        }

        return depth is > 0 and < 64;
    }

    private void PublishInactive(OpponentLimitBreakGaugeFailure failure) =>
        Volatile.Write(ref snapshot, OpponentLimitBreakGaugeSnapshot.Inactive(failure));

    private readonly record struct LimitControllerCapture(
        ulong InstanceFingerprint,
        uint CurrentUnits,
        uint MaximumUnits);

    private readonly record struct HudGaugeRowCapture(
        int Slot,
        TargetPressureActorIdentity Actor,
        uint JobId,
        nint PlayerAddress,
        ulong InstanceFingerprint,
        int MinimumValue,
        int CurrentValue,
        int MaximumValue);
}
