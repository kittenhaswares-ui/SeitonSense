using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SeitonSense.Core;
using NativeBounds = FFXIVClientStructs.FFXIV.Common.Math.Bounds;

namespace SeitonSense.Plugin.Services;

/// <summary>
/// Publishes read-only CC limit-gauge telemetry. Self is sourced only from the
/// native LimitBreakController. S1-S5 remain unknown until the current local
/// ally-row HUD instance proves which of NineGrid 3/4 is track versus fill.
/// Retained native address fingerprints are never dereferenced on a later update.
/// </summary>
internal sealed class CombatFrameLimitGaugeService : IDisposable
{
    private const long UpdateIntervalMilliseconds = 50;
    private const long ErrorLogThrottleMilliseconds = 10_000;
    private const string AllyAddonName = "PvPMKSPartyList1";
    private const string EnemyAddonName = "PvPMKSPartyList3";
    private const int FirstTeamSlot = 1;
    private const int LastTeamSlot = 5;
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
    private CombatFrameLimitGaugeSnapshot snapshot = CombatFrameLimitGaugeSnapshot.Inactive;
    private TargetPressureActorIdentity activeLocalIdentity;
    private uint activeTerritory;
    private uint activeContentFinderCondition;
    private ulong activeContextFingerprint;
    private ulong activeAllyAddonAddress;
    private ulong activeEnemyAddonAddress;
    private ulong activeAllyRosterFingerprint;
    private ulong activeEnemyRosterFingerprint;
    private int activeLocalPartySlot;
    private CombatFrameLimitGaugeNativeMapping mapping;
    private CombatFrameLimitGaugeInvalidationReason lastRuntimeInvalidation =
        CombatFrameLimitGaugeInvalidationReason.ContextLost;
    private long nextUpdateAtMilliseconds;
    private long nextErrorLogAtMilliseconds;
    private bool started;
    private bool disposed;

    internal CombatFrameLimitGaugeService(
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

    internal CombatFrameLimitGaugeSnapshot Snapshot => Volatile.Read(ref snapshot);

    internal CombatFrameLimitGaugeRuntimeDiagnostics Diagnostics => Snapshot.Diagnostics;

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
        ResetRuntime(CombatFrameLimitGaugeInvalidationReason.ContextLost);
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
            ResetRuntime(CombatFrameLimitGaugeInvalidationReason.ContextLost);
            if (now < nextErrorLogAtMilliseconds) return;

            nextErrorLogAtMilliseconds = now + ErrorLogThrottleMilliseconds;
            log.Error(exception, "Seiton Sense combat-frame LB telemetry failed closed.");
        }
    }

    private unsafe void UpdateSnapshot(long nowMilliseconds)
    {
        if (!enabledProvider())
        {
            ResetRuntime(CombatFrameLimitGaugeInvalidationReason.ContextLost);
            return;
        }

        var tracker = executeTracker.Diagnostics;
        var localPlayer = objectTable.LocalPlayer;
        var localIdentity = CreateIdentity(localPlayer);
        if (!IsExactCrystallineConflictContext(tracker) ||
            !HasExactObjectIdentity(localPlayer) ||
            !localIdentity.IsValid)
        {
            ResetRuntime(CombatFrameLimitGaugeInvalidationReason.ContextLost);
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

        var controllerBeforeValid = TryCaptureController(out var controllerBefore);
        var allyResult = TryCaptureTeam(
            AllyAddonName,
            friendly: true,
            localPlayer!,
            out var allyTeam);
        var enemyResult = TryCaptureTeam(
            EnemyAddonName,
            friendly: false,
            localPlayer!,
            out var enemyTeam);
        var controllerAfterValid = TryCaptureController(out var controllerAfter);
        var selfControllerValid = controllerBeforeValid && controllerAfterValid;
        var controllerStable = selfControllerValid && controllerBefore == controllerAfter;
        var self = selfControllerValid
            ? CombatFrameLimitGaugeRules.ExactSelf(controllerAfter.Fraction)
            : CombatFrameLimitGaugeReading.Unknown(CombatFrameLimitGaugeRules.SelfSlot);

        var allyAddonValid = allyResult == HudCaptureFailure.None;
        var enemyAddonValid = enemyResult == HudCaptureFailure.None;
        HudRowCapture localRow = default;
        if (!allyAddonValid)
        {
            InvalidateCalibration(ToInvalidationReason(allyResult));
            ClearAllyHudBinding();
        }
        else if (!TryBindAllyTeam(allyTeam, localPlayer!, out localRow))
        {
            InvalidateCalibration(CombatFrameLimitGaugeInvalidationReason.IdentityChanged);
            ClearAllyHudBinding();
            allyAddonValid = false;
        }

        if (!enemyAddonValid)
        {
            if (activeEnemyAddonAddress != 0 || activeEnemyRosterFingerprint != 0)
                InvalidateCalibration(ToInvalidationReason(enemyResult));
            ClearEnemyHudBinding();
        }
        else if (!TryBindEnemyTeam(enemyTeam))
        {
            InvalidateCalibration(CombatFrameLimitGaugeInvalidationReason.IdentityChanged);
            ClearEnemyHudBinding();
            enemyAddonValid = false;
        }

        if (!selfControllerValid)
        {
            InvalidateCalibration(CombatFrameLimitGaugeInvalidationReason.ContradictorySample);
        }
        else if (allyAddonValid && controllerStable)
        {
            ObserveLocalCalibration(
                contextFingerprint,
                controllerAfter,
                localRow);
        }

        var enemies = CombatFrameLimitGaugeRules.UnknownEnemies();
        if (allyAddonValid && enemyAddonValid && mapping != CombatFrameLimitGaugeNativeMapping.Unknown)
        {
            enemies = ProjectExactEnemyRows(
                contextFingerprint,
                enemyTeam,
                out var projectionValid);
            if (!projectionValid)
            {
                InvalidateCalibration(CombatFrameLimitGaugeInvalidationReason.ContradictorySample);
                enemies = CombatFrameLimitGaugeRules.UnknownEnemies();
            }
        }

        var knownEnemyCount = enemies.Count(static reading => reading.IsKnown);
        var diagnostics = new CombatFrameLimitGaugeRuntimeDiagnostics(
            true,
            self.IsKnown,
            allyAddonValid,
            enemyAddonValid,
            allyAddonValid ? activeLocalPartySlot : 0,
            mapping,
            knownEnemyCount,
            lastRuntimeInvalidation,
            node3TrackCalibration.Diagnostics,
            node4TrackCalibration.Diagnostics);
        Volatile.Write(
            ref snapshot,
            new CombatFrameLimitGaugeSnapshot(
                true,
                nowMilliseconds,
                self,
                enemies,
                diagnostics));
    }

    private bool IsExactCrystallineConflictContext(TrackerDiagnostics tracker) =>
        clientState.IsPvP &&
        clientState.IsPvPExcludingDen &&
        tracker.Active &&
        tracker.IsPvP &&
        tracker.IsCrystallineConflict &&
        !tracker.IsWolvesDen &&
        tracker.TerritoryId == clientState.TerritoryType &&
        tracker.SlotCapacity == CombatFrameLimitGaugeRules.LastEnemySlot &&
        tracker.ResolvedSlots == CombatFrameLimitGaugeRules.LastEnemySlot;

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
        out HudRowCapture localRow)
    {
        localRow = default;
        var localMatches = team.Rows
            .Where(row =>
                row.Identity == activeLocalIdentity &&
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
        if (team.Rows.Any(row => row.Identity == activeLocalIdentity)) return false;
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
        in HudRowCapture localRow)
    {
        var previousMapping = mapping;
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
            ? CombatFrameLimitGaugeNativeMapping.Node3TrackNode4Fill
            : node4Calibrated
                ? CombatFrameLimitGaugeNativeMapping.Node4TrackNode3Fill
                : CombatFrameLimitGaugeNativeMapping.Unknown;
        if (previousMapping == CombatFrameLimitGaugeNativeMapping.Node3TrackNode4Fill &&
            !node3Calibrated)
        {
            lastRuntimeInvalidation = node3TrackCalibration.Diagnostics.LastInvalidationReason;
        }
        else if (previousMapping == CombatFrameLimitGaugeNativeMapping.Node4TrackNode3Fill &&
                 !node4Calibrated)
        {
            lastRuntimeInvalidation = node4TrackCalibration.Diagnostics.LastInvalidationReason;
        }
    }

    private CombatFrameLimitGaugeReading[] ProjectExactEnemyRows(
        ulong contextFingerprint,
        in HudTeamCapture enemyTeam,
        out bool valid)
    {
        valid = false;
        var calibrator = mapping switch
        {
            CombatFrameLimitGaugeNativeMapping.Node3TrackNode4Fill => node3TrackCalibration,
            CombatFrameLimitGaugeNativeMapping.Node4TrackNode3Fill => node4TrackCalibration,
            _ => null,
        };
        if (calibrator is null || !calibrator.IsCalibrated) return CombatFrameLimitGaugeRules.UnknownEnemies();

        var readings = new CombatFrameLimitGaugeReading[CombatFrameLimitGaugeRules.LastEnemySlot];
        foreach (var row in enemyTeam.Rows)
        {
            var measurement = mapping == CombatFrameLimitGaugeNativeMapping.Node3TrackNode4Fill
                ? row.Node3TrackMeasurement
                : row.Node4TrackMeasurement;
            if (!calibrator.TryProjectRemote(
                    contextFingerprint,
                    row.LayoutShapeFingerprint,
                    measurement,
                    out var fraction))
            {
                return CombatFrameLimitGaugeRules.UnknownEnemies();
            }

            readings[row.Slot - CombatFrameLimitGaugeRules.FirstEnemySlot] =
                CombatFrameLimitGaugeRules.CalibratedEnemy(row.Slot, fraction);
            if (!readings[row.Slot - CombatFrameLimitGaugeRules.FirstEnemySlot].IsKnown)
                return CombatFrameLimitGaugeRules.UnknownEnemies();
        }

        valid = readings.Length == CombatFrameLimitGaugeRules.LastEnemySlot &&
                readings.All(static reading => reading.IsKnown);
        return valid ? readings : CombatFrameLimitGaugeRules.UnknownEnemies();
    }

    private unsafe HudCaptureFailure TryCaptureTeam(
        string addonName,
        bool friendly,
        IPlayerCharacter localPlayer,
        out HudTeamCapture capture)
    {
        capture = default;
        var addon = gameGui.GetAddonByName<AtkUnitBase>(addonName);
        if (!IsVisible(addon)) return HudCaptureFailure.Addon;

        var rows = new HudRowCapture[LastTeamSlot];
        var identities = new HashSet<TargetPressureActorIdentity>();
        var addresses = new HashSet<nint>();
        ulong commonShape = 0;
        for (var slot = FirstTeamSlot; slot <= LastTeamSlot; slot++)
        {
            var player = friendly
                ? PartySlotResolver.Resolve(objectTable, slot)
                : EnemySlotResolver.Resolve(objectTable, slot);
            if (!HasExactObjectIdentity(player) ||
                !identities.Add(CreateIdentity(player)) ||
                !addresses.Add(player!.Address))
            {
                return HudCaptureFailure.Identity;
            }

            if (!friendly &&
                (player.GameObjectId == localPlayer.GameObjectId ||
                 player.EntityId == localPlayer.EntityId ||
                 player.Address == localPlayer.Address))
            {
                return HudCaptureFailure.Identity;
            }

            var rowResult = TryCaptureRow(addon, slot, player, out var row);
            if (rowResult != HudCaptureFailure.None) return rowResult;
            if (commonShape == 0)
                commonShape = row.LayoutShapeFingerprint;
            else if (commonShape != row.LayoutShapeFingerprint)
                return HudCaptureFailure.Hierarchy;
            rows[slot - FirstTeamSlot] = row;
        }

        var addonAfter = gameGui.GetAddonByName<AtkUnitBase>(addonName);
        if (!IsVisible(addonAfter) || addonAfter != addon) return HudCaptureFailure.Addon;
        foreach (var row in rows)
        {
            var player = friendly
                ? PartySlotResolver.Resolve(objectTable, row.Slot)
                : EnemySlotResolver.Resolve(objectTable, row.Slot);
            if (!HasExactObjectIdentity(player) ||
                CreateIdentity(player) != row.Identity ||
                player!.Address != row.PlayerAddress)
            {
                return HudCaptureFailure.Identity;
            }

            var stableResult = TryCaptureRow(addonAfter, row.Slot, player, out var stableRow);
            if (stableResult != HudCaptureFailure.None) return stableResult;
            if (stableRow.Identity != row.Identity ||
                stableRow.PlayerAddress != row.PlayerAddress ||
                stableRow.InstanceFingerprint != row.InstanceFingerprint ||
                stableRow.LayoutShapeFingerprint != row.LayoutShapeFingerprint)
            {
                return HudCaptureFailure.Identity;
            }
        }

        var rosterFingerprint = CreateRosterFingerprint(rows);
        capture = new HudTeamCapture(
            PointerFingerprint(addon),
            rosterFingerprint,
            commonShape,
            rows);
        return capture.IsValid ? HudCaptureFailure.None : HudCaptureFailure.Hierarchy;
    }

    private unsafe HudCaptureFailure TryCaptureRow(
        AtkUnitBase* addon,
        int slot,
        IPlayerCharacter player,
        out HudRowCapture capture)
    {
        capture = default;
        var rowNodeId = FirstRowNodeId + (uint)(slot - FirstTeamSlot);
        var row = addon->GetComponentByNodeId(rowNodeId);
        if (row == null || row->AtkResNode == null || !IsNodeVisible(row->AtkResNode))
            return HudCaptureFailure.Hierarchy;

        var name = row->GetTextNodeById(21);
        if (name == null) return HudCaptureFailure.Hierarchy;
        var currentName = name->GetText().ToString();
        if (string.IsNullOrEmpty(currentName) ||
            !string.Equals(currentName, player.Name.TextValue, StringComparison.Ordinal))
        {
            return HudCaptureFailure.Identity;
        }

        var gauge = row->GetComponentById(GaugeComponentId);
        if (gauge == null || gauge->AtkResNode == null || !IsNodeVisible(gauge->AtkResNode))
            return HudCaptureFailure.Hierarchy;
        var node3 = gauge->GetNineGridNodeById(FirstGaugeNodeId);
        var node4 = gauge->GetNineGridNodeById(SecondGaugeNodeId);
        if (node3 == null || node4 == null ||
            node3->AtkResNode.NodeId != FirstGaugeNodeId ||
            node4->AtkResNode.NodeId != SecondGaugeNodeId)
        {
            return HudCaptureFailure.Hierarchy;
        }

        var node3Res = &node3->AtkResNode;
        var node4Res = &node4->AtkResNode;
        if (!TryCapturePotentialTrack(node3Res, out var node3TrackBounds) &&
            !TryCapturePotentialTrack(node4Res, out _))
        {
            return HudCaptureFailure.Hierarchy;
        }

        var instanceFingerprint = CreateInstanceFingerprint(
            addon,
            row,
            gauge,
            node3Res,
            node4Res,
            player);
        var shapeFingerprint = CreateLayoutShapeFingerprint(row, gauge, node3Res, node4Res);
        if (instanceFingerprint == 0 || shapeFingerprint == 0)
            return HudCaptureFailure.Hierarchy;

        capture = new HudRowCapture(
            slot,
            CreateIdentity(player),
            player.Address,
            instanceFingerprint,
            shapeFingerprint,
            CreateMeasurement(node3Res, node4Res, node3TrackBounds),
            CreateMeasurement(node4Res, node3Res));
        return HudCaptureFailure.None;
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
        var fillBounds = fillVisible
            ? CaptureBounds(fill)
            : default;
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
            controller->CurrentUnits,
            controller->BarUnits,
            (float)(controller->CurrentUnits / (double)controller->BarUnits));
        return capture.IsValid;
    }

    private static unsafe ulong CreateInstanceFingerprint(
        AtkUnitBase* addon,
        AtkComponentBase* row,
        AtkComponentBase* gauge,
        AtkResNode* node3,
        AtkResNode* node4,
        IPlayerCharacter player)
    {
        var hash = StartFingerprint();
        hash = AddFingerprint(hash, PointerFingerprint(addon));
        hash = AddFingerprint(hash, PointerFingerprint(row));
        hash = AddFingerprint(hash, PointerFingerprint(gauge));
        hash = AddFingerprint(hash, PointerFingerprint(node3));
        hash = AddFingerprint(hash, PointerFingerprint(node4));
        hash = AddFingerprint(hash, unchecked((ulong)player.Address));
        hash = AddFingerprint(hash, player.GameObjectId);
        hash = AddFingerprint(hash, player.EntityId);
        return FinishFingerprint(hash);
    }

    private static unsafe ulong CreateLayoutShapeFingerprint(
        AtkComponentBase* row,
        AtkComponentBase* gauge,
        AtkResNode* node3,
        AtkResNode* node4)
    {
        var hash = StartFingerprint();
        hash = AddFingerprint(hash, GaugeComponentId);
        hash = AddFingerprint(hash, FirstGaugeNodeId);
        hash = AddFingerprint(hash, SecondGaugeNodeId);
        hash = AddFingerprint(hash, (ulong)row->GetComponentType());
        hash = AddFingerprint(hash, (ulong)gauge->GetComponentType());
        hash = AddFingerprint(hash, row->UldManager.NodeListCount);
        hash = AddFingerprint(hash, gauge->UldManager.NodeListCount);
        hash = AddFingerprint(hash, node3->NodeId);
        hash = AddFingerprint(hash, (ulong)node3->Type);
        hash = AddFingerprint(
            hash,
            node3->ParentNode == null ? 0UL : node3->ParentNode->NodeId);
        hash = AddFingerprint(hash, node4->NodeId);
        hash = AddFingerprint(hash, (ulong)node4->Type);
        hash = AddFingerprint(
            hash,
            node4->ParentNode == null ? 0UL : node4->ParentNode->NodeId);
        return FinishFingerprint(hash);
    }

    private static ulong CreateContextFingerprint(uint territory, uint condition)
    {
        var hash = StartFingerprint();
        hash = AddFingerprint(hash, territory);
        hash = AddFingerprint(hash, condition);
        return FinishFingerprint(hash);
    }

    private static ulong CreateRosterFingerprint(IReadOnlyList<HudRowCapture> rows)
    {
        var hash = StartFingerprint();
        foreach (var row in rows.OrderBy(static row => row.Slot))
        {
            hash = AddFingerprint(hash, (ulong)row.Slot);
            hash = AddFingerprint(hash, row.Identity.GameObjectId);
            hash = AddFingerprint(hash, row.Identity.EntityId);
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
        mapping = CombatFrameLimitGaugeNativeMapping.Unknown;
        lastRuntimeInvalidation = reason;
    }

    private void ResetRuntime(CombatFrameLimitGaugeInvalidationReason reason)
    {
        InvalidateCalibration(reason);
        activeLocalIdentity = default;
        activeTerritory = 0;
        activeContentFinderCondition = 0;
        activeContextFingerprint = 0;
        ClearHudBindings();
        Volatile.Write(ref snapshot, CombatFrameLimitGaugeSnapshot.Inactive);
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
        HudCaptureFailure failure) =>
        failure switch
        {
            HudCaptureFailure.Addon => CombatFrameLimitGaugeInvalidationReason.AddonChanged,
            HudCaptureFailure.Hierarchy => CombatFrameLimitGaugeInvalidationReason.HierarchyChanged,
            HudCaptureFailure.Identity => CombatFrameLimitGaugeInvalidationReason.IdentityChanged,
            _ => CombatFrameLimitGaugeInvalidationReason.None,
        };

    private static TargetPressureActorIdentity CreateIdentity(IPlayerCharacter? player) =>
        player is null
            ? default
            : new TargetPressureActorIdentity(player.GameObjectId, player.EntityId);

    private bool HasExactObjectIdentity(IPlayerCharacter? player)
    {
        if (player is null ||
            player.Address == 0 ||
            !IsNetworkObjectId(player.GameObjectId) ||
            !IsNetworkEntityId(player.EntityId) ||
            string.IsNullOrEmpty(player.Name.TextValue))
        {
            return false;
        }

        var tablePlayer = objectTable.SearchByEntityId(player.EntityId) as IPlayerCharacter;
        return tablePlayer is not null &&
               tablePlayer.Address == player.Address &&
               tablePlayer.GameObjectId == player.GameObjectId &&
               tablePlayer.EntityId == player.EntityId;
    }

    private static bool IsNetworkEntityId(uint entityId) =>
        entityId is not 0 and not 0xE0000000u and not uint.MaxValue;

    private static bool IsNetworkObjectId(ulong gameObjectId) =>
        gameObjectId is not 0 and not 0xE0000000UL and not ulong.MaxValue;

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

    private enum HudCaptureFailure : byte
    {
        None = 0,
        Addon = 1,
        Hierarchy = 2,
        Identity = 3,
    }

    private readonly record struct LimitControllerCapture(
        uint CurrentUnits,
        uint MaximumUnits,
        float Fraction)
    {
        internal bool IsValid =>
            MaximumUnits > 0 &&
            CurrentUnits <= MaximumUnits &&
            float.IsFinite(Fraction) &&
            Fraction is >= 0f and <= 1f;
    }

    private readonly record struct HudRowCapture(
        int Slot,
        TargetPressureActorIdentity Identity,
        nint PlayerAddress,
        ulong InstanceFingerprint,
        ulong LayoutShapeFingerprint,
        CombatFrameLimitGaugeNativeMeasurement Node3TrackMeasurement,
        CombatFrameLimitGaugeNativeMeasurement Node4TrackMeasurement);

    private readonly record struct HudTeamCapture(
        ulong AddonAddress,
        ulong RosterFingerprint,
        ulong LayoutShapeFingerprint,
        IReadOnlyList<HudRowCapture> Rows)
    {
        internal bool IsValid =>
            AddonAddress != 0 &&
            RosterFingerprint != 0 &&
            LayoutShapeFingerprint != 0 &&
            Rows is { Count: LastTeamSlot };
    }
}
