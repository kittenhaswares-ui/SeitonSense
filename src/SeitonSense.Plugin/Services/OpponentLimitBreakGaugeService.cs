using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SeitonSense.Core;
using NativeBounds = FFXIVClientStructs.FFXIV.Common.Math.Bounds;

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
    Calibration = 9,
}

internal enum OpponentLimitBreakGaugeNativeMapping : byte
{
    Unknown = 0,
    Node3TrackNode4Fill = 1,
    Node4TrackNode3Fill = 2,
}

internal readonly record struct OpponentLimitBreakGaugeDiagnostics(
    bool Active,
    bool ControllerStable,
    bool AllyRowsStable,
    bool EnemyRowsStable,
    bool LocalCrossCheckPassed,
    int KnownEnemyCount,
    OpponentLimitBreakGaugeFailure Failure,
    OpponentLimitBreakGaugeNativeMapping Mapping,
    CombatFrameLimitGaugeInvalidationReason LastInvalidation,
    CombatFrameLimitGaugeCalibrationDiagnostics Node3Calibration,
    CombatFrameLimitGaugeCalibrationDiagnostics Node4Calibration)
{
    internal static OpponentLimitBreakGaugeDiagnostics Inactive(
        OpponentLimitBreakGaugeFailure failure) =>
        new(false, false, false, false, false, 0, failure, default, default, default, default);

    internal string ToChatLine() =>
        $"LB-bars active={Active},controller={ControllerStable},rows={AllyRowsStable}/{EnemyRowsStable}," +
        $"local-proof={LocalCrossCheckPassed},known={KnownEnemyCount}/5,reason={Failure},mapping={Mapping}," +
        $"invalidated={LastInvalidation}," +
        $"cal={Node3Calibration.DistinctSampleCount}/{Node4Calibration.DistinctSampleCount}";
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
/// Reads the rendered CC LB layer without assuming that its containing ULD
/// component exposes direct gauge values. The two bounded NineGrid candidates
/// are calibrated only against the exact local LimitBreakController, then the
/// proven layout is projected to a stable exact S1-S5 enemy set. Native nodes
/// are never changed and pointer fingerprints never leave the capture call.
/// </summary>
internal sealed class OpponentLimitBreakGaugeService : IDisposable
{
    private const long UpdateIntervalMilliseconds = 50;
    private const long ErrorLogThrottleMilliseconds = 10_000;
    private const string AllyAddonName = "PvPMKSPartyList1";
    private const string EnemyAddonName = "PvPMKSPartyList3";
    private const uint FirstRowNodeId = 6;
    private const uint GaugeComponentId = 2;
    private const uint FirstGaugeNodeId = 3;
    private const uint SecondGaugeNodeId = 4;

    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly IFramework framework;
    private readonly IGameGui gameGui;
    private readonly ExecuteTracker executeTracker;
    private readonly IPluginLog log;
    private readonly Func<bool> enabledProvider;
    private readonly CombatFrameLimitGaugeCalibrator node3TrackCalibration = new();
    private readonly CombatFrameLimitGaugeCalibrator node4TrackCalibration = new();
    private OpponentLimitBreakGaugeSnapshot snapshot = OpponentLimitBreakGaugeSnapshot.Inactive();
    private TargetPressureActorIdentity activeLocalIdentity;
    private uint activeTerritory;
    private uint activeContentFinderCondition;
    private ulong activeContextFingerprint;
    private ulong activeAllyAddonAddress;
    private ulong activeEnemyAddonAddress;
    private ulong activeAllyRosterFingerprint;
    private ulong activeEnemyRosterFingerprint;
    private int activeLocalPartySlot;
    private OpponentLimitBreakGaugeNativeMapping mapping;
    private CombatFrameLimitGaugeInvalidationReason lastRuntimeInvalidation =
        CombatFrameLimitGaugeInvalidationReason.ContextLost;
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
        ResetCalibration(CombatFrameLimitGaugeInvalidationReason.ContextLost);
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
            ResetCalibration(CombatFrameLimitGaugeInvalidationReason.HierarchyChanged);
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
            ResetCalibration(CombatFrameLimitGaugeInvalidationReason.ContextLost);
            PublishInactive(OpponentLimitBreakGaugeFailure.Disabled);
            return;
        }

        var tracker = executeTracker.Diagnostics;
        var localPlayer = objectTable.LocalPlayer;
        var localIdentity = CreateIdentity(localPlayer);
        if (!IsExactCrystallineConflictContext(tracker) ||
            !HasExactObjectIdentity(localPlayer) ||
            !localIdentity.IsValid)
        {
            ResetCalibration(CombatFrameLimitGaugeInvalidationReason.ContextLost);
            PublishInactive(OpponentLimitBreakGaugeFailure.Context);
            return;
        }

        var contextFingerprint = CreateContextFingerprint(
            tracker.TerritoryId,
            tracker.ContentFinderConditionId);
        ObserveContext(
            tracker.TerritoryId,
            tracker.ContentFinderConditionId,
            contextFingerprint,
            localIdentity);

        if (!TryCaptureController(out var controllerBefore))
        {
            InvalidateCalibration(CombatFrameLimitGaugeInvalidationReason.ContradictorySample);
            PublishInactive(OpponentLimitBreakGaugeFailure.Controller);
            return;
        }

        var allyFailure = TryCaptureStableTeam(
            AllyAddonName,
            true,
            localPlayer!,
            out var allyTeam);
        if (allyFailure != OpponentLimitBreakGaugeFailure.None)
        {
            InvalidateCalibration(ToInvalidationReason(allyFailure));
            ClearAllyHudBinding();
            PublishInactive(allyFailure, controllerStable: true);
            return;
        }

        var enemyFailure = TryCaptureStableTeam(
            EnemyAddonName,
            false,
            localPlayer!,
            out var enemyTeam);
        if (enemyFailure != OpponentLimitBreakGaugeFailure.None)
        {
            InvalidateCalibration(ToInvalidationReason(enemyFailure));
            ClearEnemyHudBinding();
            PublishInactive(
                enemyFailure,
                controllerStable: true,
                allyRowsStable: true);
            return;
        }

        if (!TryCaptureController(out var controllerAfter) || controllerBefore != controllerAfter)
        {
            InvalidateCalibration(CombatFrameLimitGaugeInvalidationReason.ContradictorySample);
            PublishInactive(
                OpponentLimitBreakGaugeFailure.Controller,
                allyRowsStable: true,
                enemyRowsStable: true);
            return;
        }

        if (!TryBindAllyTeam(allyTeam, localPlayer!, out var localRow))
        {
            InvalidateCalibration(CombatFrameLimitGaugeInvalidationReason.IdentityChanged);
            ClearAllyHudBinding();
            PublishInactive(
                OpponentLimitBreakGaugeFailure.Identity,
                controllerStable: true,
                allyRowsStable: true,
                enemyRowsStable: true);
            return;
        }

        if (!TryBindEnemyTeam(enemyTeam))
        {
            InvalidateCalibration(CombatFrameLimitGaugeInvalidationReason.IdentityChanged);
            ClearEnemyHudBinding();
            PublishInactive(
                OpponentLimitBreakGaugeFailure.Identity,
                controllerStable: true,
                allyRowsStable: true,
                enemyRowsStable: true);
            return;
        }

        ObserveLocalCalibration(contextFingerprint, controllerAfter, localRow);
        if (mapping == OpponentLimitBreakGaugeNativeMapping.Unknown)
        {
            PublishInactive(
                OpponentLimitBreakGaugeFailure.Calibration,
                controllerStable: true,
                allyRowsStable: true,
                enemyRowsStable: true);
            return;
        }

        if (!TryProjectEnemyRows(contextFingerprint, enemyTeam, out var enemies) ||
            !OpponentLimitBreakGaugeRules.IsCompleteExactEnemySet(enemies))
        {
            InvalidateCalibration(CombatFrameLimitGaugeInvalidationReason.ContradictorySample);
            PublishInactive(
                OpponentLimitBreakGaugeFailure.Calibration,
                controllerStable: true,
                allyRowsStable: true,
                enemyRowsStable: true);
            return;
        }

        Volatile.Write(
            ref snapshot,
            new OpponentLimitBreakGaugeSnapshot(
                true,
                nowMilliseconds,
                enemies,
                CreateDiagnostics(
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

    private void ObserveContext(
        uint territory,
        uint contentFinderCondition,
        ulong contextFingerprint,
        TargetPressureActorIdentity localIdentity)
    {
        if (activeContextFingerprint != 0 &&
            (activeContextFingerprint != contextFingerprint ||
             activeTerritory != territory ||
             activeContentFinderCondition != contentFinderCondition))
        {
            InvalidateCalibration(CombatFrameLimitGaugeInvalidationReason.ContextChanged);
            ClearHudBindings();
        }

        if (activeLocalIdentity.IsValid && activeLocalIdentity != localIdentity)
        {
            InvalidateCalibration(CombatFrameLimitGaugeInvalidationReason.IdentityChanged);
            ClearHudBindings();
        }

        activeTerritory = territory;
        activeContentFinderCondition = contentFinderCondition;
        activeContextFingerprint = contextFingerprint;
        activeLocalIdentity = localIdentity;
    }

    private bool TryBindAllyTeam(
        in HudTeamCapture team,
        IPlayerCharacter localPlayer,
        out HudGaugeRowCapture localRow)
    {
        localRow = default;
        var localMatches = team.Rows
            .Where(row =>
                row.Actor == activeLocalIdentity &&
                row.PlayerAddress == localPlayer.Address)
            .ToArray();
        if (localMatches.Length != 1) return false;

        var exactLocalRow = localMatches[0];
        if (activeAllyAddonAddress != 0 && activeAllyAddonAddress != team.AddonAddress)
        {
            InvalidateCalibration(CombatFrameLimitGaugeInvalidationReason.AddonChanged);
            ClearAllyHudBinding();
        }

        if (activeAllyRosterFingerprint != 0 &&
            activeAllyRosterFingerprint != team.RosterFingerprint)
        {
            InvalidateCalibration(CombatFrameLimitGaugeInvalidationReason.IdentityChanged);
            ClearAllyHudBinding();
        }

        if (activeLocalPartySlot != 0 && activeLocalPartySlot != exactLocalRow.Slot)
        {
            InvalidateCalibration(CombatFrameLimitGaugeInvalidationReason.IdentityChanged);
            ClearAllyHudBinding();
        }

        activeAllyAddonAddress = team.AddonAddress;
        activeAllyRosterFingerprint = team.RosterFingerprint;
        activeLocalPartySlot = exactLocalRow.Slot;
        localRow = exactLocalRow;
        return true;
    }

    private bool TryBindEnemyTeam(in HudTeamCapture team)
    {
        if (team.Rows.Any(row => row.Actor == activeLocalIdentity)) return false;
        if (activeEnemyAddonAddress != 0 && activeEnemyAddonAddress != team.AddonAddress)
        {
            InvalidateCalibration(CombatFrameLimitGaugeInvalidationReason.AddonChanged);
            ClearEnemyHudBinding();
        }

        if (activeEnemyRosterFingerprint != 0 &&
            activeEnemyRosterFingerprint != team.RosterFingerprint)
        {
            InvalidateCalibration(CombatFrameLimitGaugeInvalidationReason.IdentityChanged);
            ClearEnemyHudBinding();
        }

        activeEnemyAddonAddress = team.AddonAddress;
        activeEnemyRosterFingerprint = team.RosterFingerprint;
        return true;
    }

    private void ObserveLocalCalibration(
        ulong contextFingerprint,
        in LimitControllerCapture controller,
        in HudGaugeRowCapture localRow)
    {
        node3TrackCalibration.Observe(new CombatFrameLimitGaugeCalibrationObservation(
            contextFingerprint,
            localRow.InstanceFingerprint,
            localRow.LayoutShapeFingerprint,
            controller.CurrentUnits,
            controller.MaximumUnits,
            localRow.Node3TrackMeasurement));
        node4TrackCalibration.Observe(new CombatFrameLimitGaugeCalibrationObservation(
            contextFingerprint,
            localRow.InstanceFingerprint,
            localRow.LayoutShapeFingerprint,
            controller.CurrentUnits,
            controller.MaximumUnits,
            localRow.Node4TrackMeasurement));

        var node3Calibrated = node3TrackCalibration.IsCalibrated;
        var node4Calibrated = node4TrackCalibration.IsCalibrated;
        if (node3Calibrated && node4Calibrated)
        {
            InvalidateCalibration(CombatFrameLimitGaugeInvalidationReason.AmbiguousMapping);
            return;
        }

        mapping = node3Calibrated
            ? OpponentLimitBreakGaugeNativeMapping.Node3TrackNode4Fill
            : node4Calibrated
                ? OpponentLimitBreakGaugeNativeMapping.Node4TrackNode3Fill
                : OpponentLimitBreakGaugeNativeMapping.Unknown;
    }

    private bool TryProjectEnemyRows(
        ulong contextFingerprint,
        in HudTeamCapture enemyTeam,
        out OpponentLimitBreakGaugeValue[] enemies)
    {
        enemies = new OpponentLimitBreakGaugeValue[OpponentLimitBreakGaugeRules.EnemyCount];
        var calibrator = mapping switch
        {
            OpponentLimitBreakGaugeNativeMapping.Node3TrackNode4Fill => node3TrackCalibration,
            OpponentLimitBreakGaugeNativeMapping.Node4TrackNode3Fill => node4TrackCalibration,
            _ => null,
        };
        if (calibrator is null || !calibrator.IsCalibrated) return false;

        foreach (var row in enemyTeam.Rows)
        {
            var measurement = mapping == OpponentLimitBreakGaugeNativeMapping.Node3TrackNode4Fill
                ? row.Node3TrackMeasurement
                : row.Node4TrackMeasurement;
            if (!calibrator.TryProjectRemote(
                    contextFingerprint,
                    row.LayoutShapeFingerprint,
                    measurement,
                    out var fraction) ||
                !OpponentLimitBreakGaugeRules.TryCreateCalibratedValue(
                    row.Actor,
                    row.Slot,
                    row.JobId,
                    fraction,
                    out var value))
            {
                return false;
            }

            enemies[row.Slot - OpponentLimitBreakGaugeRules.FirstEnemySlot] = value;
        }

        return true;
    }

    private unsafe OpponentLimitBreakGaugeFailure TryCaptureStableTeam(
        string addonName,
        bool friendly,
        IPlayerCharacter localPlayer,
        out HudTeamCapture stableTeam)
    {
        stableTeam = default;
        var addon = gameGui.GetAddonByName<AtkUnitBase>(addonName);
        if (!IsVisible(addon)) return OpponentLimitBreakGaugeFailure.Addon;

        var first = new HudGaugeRowCapture[OpponentLimitBreakGaugeRules.EnemyCount];
        var actors = new HashSet<TargetPressureActorIdentity>();
        var addresses = new HashSet<nint>();
        ulong commonShape = 0;
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
            if (commonShape == 0)
                commonShape = first[slot - 1].LayoutShapeFingerprint;
            else if (commonShape != first[slot - 1].LayoutShapeFingerprint)
                return OpponentLimitBreakGaugeFailure.Hierarchy;
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
            var expected = first[slot - 1];
            if (!HasExactObjectIdentity(player) ||
                CreateIdentity(player) != expected.Actor ||
                player!.Address != expected.PlayerAddress)
            {
                return OpponentLimitBreakGaugeFailure.Identity;
            }

            var failure = TryCaptureRow(addonAfter, slot, player, out second[slot - 1]);
            if (failure != OpponentLimitBreakGaugeFailure.None) return failure;
            if (!HasStableIdentityAndLayout(expected, second[slot - 1]))
                return OpponentLimitBreakGaugeFailure.Unstable;
        }

        var rosterFingerprint = CreateRosterFingerprint(second);
        if (commonShape == 0 || rosterFingerprint == 0)
            return OpponentLimitBreakGaugeFailure.Hierarchy;

        stableTeam = new HudTeamCapture(
            PointerFingerprint(addon),
            rosterFingerprint,
            commonShape,
            second);
        return stableTeam.IsValid
            ? OpponentLimitBreakGaugeFailure.None
            : OpponentLimitBreakGaugeFailure.Hierarchy;
    }

    private static bool HasStableIdentityAndLayout(
        in HudGaugeRowCapture first,
        in HudGaugeRowCapture second) =>
        first.Slot == second.Slot &&
        first.Actor == second.Actor &&
        first.JobId == second.JobId &&
        first.PlayerAddress == second.PlayerAddress &&
        first.InstanceFingerprint == second.InstanceFingerprint &&
        first.LayoutShapeFingerprint == second.LayoutShapeFingerprint;

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
        if (component == null || component->AtkResNode == null || !IsNodeVisible(component->AtkResNode))
            return OpponentLimitBreakGaugeFailure.Hierarchy;

        var node3 = component->GetNineGridNodeById(FirstGaugeNodeId);
        var node4 = component->GetNineGridNodeById(SecondGaugeNodeId);
        if (node3 == null || node4 == null ||
            node3->AtkResNode.NodeId != FirstGaugeNodeId ||
            node4->AtkResNode.NodeId != SecondGaugeNodeId)
        {
            return OpponentLimitBreakGaugeFailure.Hierarchy;
        }

        var node3Res = &node3->AtkResNode;
        var node4Res = &node4->AtkResNode;
        var node3Valid = TryCapturePotentialTrack(node3Res, out var node3TrackBounds);
        var node4Valid = TryCapturePotentialTrack(node4Res, out var node4TrackBounds);
        if (!node3Valid && !node4Valid) return OpponentLimitBreakGaugeFailure.Hierarchy;

        var jobId = player.ClassJob.IsValid ? player.ClassJob.RowId : 0;
        var instanceFingerprint = CreateInstanceFingerprint(
            addon,
            row,
            component,
            node3Res,
            node4Res,
            player);
        var shapeFingerprint = CreateLayoutShapeFingerprint(
            row,
            component,
            node3Res,
            node4Res);
        if (jobId == 0 || instanceFingerprint == 0 || shapeFingerprint == 0)
            return OpponentLimitBreakGaugeFailure.Identity;

        capture = new HudGaugeRowCapture(
            slot,
            CreateIdentity(player),
            jobId,
            player.Address,
            instanceFingerprint,
            shapeFingerprint,
            CreateMeasurement(
                node3Res,
                node4Res,
                node3Valid ? node3TrackBounds : null),
            CreateMeasurement(
                node4Res,
                node3Res,
                node4Valid ? node4TrackBounds : null));
        return OpponentLimitBreakGaugeFailure.None;
    }

    private static unsafe CombatFrameLimitGaugeNativeMeasurement CreateMeasurement(
        AtkResNode* track,
        AtkResNode* fill,
        CombatFrameLimitGaugeRenderedBounds? capturedTrack = null)
    {
        var trackBounds = capturedTrack ??
                          (TryCapturePotentialTrack(track, out var observedTrack)
                              ? observedTrack
                              : default);
        var fillVisible = IsNodeVisible(fill);
        var fillBounds = fillVisible ? CaptureBounds(fill) : default;
        return new CombatFrameLimitGaugeNativeMeasurement(trackBounds, fillVisible, fillBounds);
    }

    private static unsafe bool TryCapturePotentialTrack(
        AtkResNode* node,
        out CombatFrameLimitGaugeRenderedBounds bounds)
    {
        bounds = default;
        if (!IsNodeVisible(node)) return false;
        bounds = CaptureBounds(node);
        return float.IsFinite(bounds.MinimumX) &&
               float.IsFinite(bounds.MinimumY) &&
               float.IsFinite(bounds.MaximumX) &&
               float.IsFinite(bounds.MaximumY) &&
               bounds.Width > CombatFrameLimitGaugeRules.MinimumRenderedExtent &&
               bounds.Height > CombatFrameLimitGaugeRules.MinimumRenderedExtent;
    }

    private static unsafe CombatFrameLimitGaugeRenderedBounds CaptureBounds(AtkResNode* node)
    {
        NativeBounds native;
        node->GetBounds(&native);
        return new CombatFrameLimitGaugeRenderedBounds(
            Math.Min(native.Pos1.X, native.Pos2.X),
            Math.Min(native.Pos1.Y, native.Pos2.Y),
            Math.Max(native.Pos1.X, native.Pos2.X),
            Math.Max(native.Pos1.Y, native.Pos2.Y));
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
            PointerFingerprint(controller),
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

    private static unsafe ulong CreateInstanceFingerprint(
        AtkUnitBase* addon,
        AtkComponentBase* row,
        AtkComponentBase* component,
        AtkResNode* node3,
        AtkResNode* node4,
        IPlayerCharacter player)
    {
        var hash = StartFingerprint();
        hash = AddFingerprint(hash, PointerFingerprint(addon));
        hash = AddFingerprint(hash, PointerFingerprint(row));
        hash = AddFingerprint(hash, PointerFingerprint(component));
        hash = AddFingerprint(hash, PointerFingerprint(node3));
        hash = AddFingerprint(hash, PointerFingerprint(node4));
        hash = AddFingerprint(hash, unchecked((ulong)player.Address));
        hash = AddFingerprint(hash, player.GameObjectId);
        hash = AddFingerprint(hash, player.EntityId);
        return FinishFingerprint(hash);
    }

    private static unsafe ulong CreateLayoutShapeFingerprint(
        AtkComponentBase* row,
        AtkComponentBase* component,
        AtkResNode* node3,
        AtkResNode* node4)
    {
        var hash = StartFingerprint();
        hash = AddFingerprint(hash, GaugeComponentId);
        hash = AddFingerprint(hash, FirstGaugeNodeId);
        hash = AddFingerprint(hash, SecondGaugeNodeId);
        hash = AddFingerprint(hash, (ulong)row->GetComponentType());
        hash = AddFingerprint(hash, (ulong)component->GetComponentType());
        hash = AddFingerprint(hash, row->UldManager.NodeListCount);
        hash = AddFingerprint(hash, component->UldManager.NodeListCount);
        hash = AddFingerprint(hash, node3->NodeId);
        hash = AddFingerprint(hash, (ulong)node3->Type);
        hash = AddFingerprint(hash, node3->ParentNode == null ? 0UL : node3->ParentNode->NodeId);
        hash = AddFingerprint(hash, node4->NodeId);
        hash = AddFingerprint(hash, (ulong)node4->Type);
        hash = AddFingerprint(hash, node4->ParentNode == null ? 0UL : node4->ParentNode->NodeId);
        return FinishFingerprint(hash);
    }

    private static ulong CreateContextFingerprint(uint territory, uint condition)
    {
        var hash = StartFingerprint();
        hash = AddFingerprint(hash, territory);
        hash = AddFingerprint(hash, condition);
        return FinishFingerprint(hash);
    }

    private static ulong CreateRosterFingerprint(IReadOnlyList<HudGaugeRowCapture> rows)
    {
        var hash = StartFingerprint();
        foreach (var row in rows.OrderBy(static row => row.Slot))
        {
            hash = AddFingerprint(hash, (ulong)row.Slot);
            hash = AddFingerprint(hash, row.Actor.GameObjectId);
            hash = AddFingerprint(hash, row.Actor.EntityId);
            hash = AddFingerprint(hash, unchecked((ulong)row.PlayerAddress));
        }

        return FinishFingerprint(hash);
    }

    private static ulong StartFingerprint() => 14_695_981_039_346_656_037UL;

    private static ulong AddFingerprint(ulong hash, ulong value)
    {
        const ulong prime = 1_099_511_628_211UL;
        for (var index = 0; index < sizeof(ulong); index++)
        {
            hash ^= (byte)(value >> (index * 8));
            hash *= prime;
        }

        return hash;
    }

    private static ulong FinishFingerprint(ulong hash) => hash == 0 ? 1UL : hash;

    private static unsafe ulong PointerFingerprint(void* pointer) => unchecked((ulong)(nuint)pointer);

    private void InvalidateCalibration(CombatFrameLimitGaugeInvalidationReason reason)
    {
        if (reason == CombatFrameLimitGaugeInvalidationReason.None) return;
        node3TrackCalibration.Invalidate(reason);
        node4TrackCalibration.Invalidate(reason);
        mapping = OpponentLimitBreakGaugeNativeMapping.Unknown;
        lastRuntimeInvalidation = reason;
    }

    private void ResetCalibration(CombatFrameLimitGaugeInvalidationReason reason)
    {
        InvalidateCalibration(reason);
        activeLocalIdentity = default;
        activeTerritory = 0;
        activeContentFinderCondition = 0;
        activeContextFingerprint = 0;
        ClearHudBindings();
    }

    private void ClearHudBindings()
    {
        ClearAllyHudBinding();
        ClearEnemyHudBinding();
    }

    private void ClearAllyHudBinding()
    {
        activeAllyAddonAddress = 0;
        activeAllyRosterFingerprint = 0;
        activeLocalPartySlot = 0;
    }

    private void ClearEnemyHudBinding()
    {
        activeEnemyAddonAddress = 0;
        activeEnemyRosterFingerprint = 0;
    }

    private static CombatFrameLimitGaugeInvalidationReason ToInvalidationReason(
        OpponentLimitBreakGaugeFailure failure) =>
        failure switch
        {
            OpponentLimitBreakGaugeFailure.Addon => CombatFrameLimitGaugeInvalidationReason.AddonChanged,
            OpponentLimitBreakGaugeFailure.Identity => CombatFrameLimitGaugeInvalidationReason.IdentityChanged,
            OpponentLimitBreakGaugeFailure.Hierarchy => CombatFrameLimitGaugeInvalidationReason.HierarchyChanged,
            OpponentLimitBreakGaugeFailure.Unstable => CombatFrameLimitGaugeInvalidationReason.HierarchyChanged,
            _ => CombatFrameLimitGaugeInvalidationReason.ContradictorySample,
        };

    private OpponentLimitBreakGaugeDiagnostics CreateDiagnostics(
        bool active,
        bool controllerStable,
        bool allyRowsStable,
        bool enemyRowsStable,
        bool localCrossCheckPassed,
        int knownEnemyCount,
        OpponentLimitBreakGaugeFailure failure) =>
        new(
            active,
            controllerStable,
            allyRowsStable,
            enemyRowsStable,
            localCrossCheckPassed,
            knownEnemyCount,
            failure,
            mapping,
            lastRuntimeInvalidation,
            node3TrackCalibration.Diagnostics,
            node4TrackCalibration.Diagnostics);

    private void PublishInactive(
        OpponentLimitBreakGaugeFailure failure,
        bool controllerStable = false,
        bool allyRowsStable = false,
        bool enemyRowsStable = false,
        bool localCrossCheckPassed = false) =>
        Volatile.Write(
            ref snapshot,
            new OpponentLimitBreakGaugeSnapshot(
                false,
                -1,
                [],
                CreateDiagnostics(
                    false,
                    controllerStable,
                    allyRowsStable,
                    enemyRowsStable,
                    localCrossCheckPassed,
                    0,
                    failure)));

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
        ulong LayoutShapeFingerprint,
        CombatFrameLimitGaugeNativeMeasurement Node3TrackMeasurement,
        CombatFrameLimitGaugeNativeMeasurement Node4TrackMeasurement);

    private readonly record struct HudTeamCapture(
        ulong AddonAddress,
        ulong RosterFingerprint,
        ulong LayoutShapeFingerprint,
        IReadOnlyList<HudGaugeRowCapture> Rows)
    {
        internal bool IsValid =>
            AddonAddress != 0 &&
            RosterFingerprint != 0 &&
            LayoutShapeFingerprint != 0 &&
            Rows is { Count: OpponentLimitBreakGaugeRules.EnemyCount };
    }
}
