using System.Numerics;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Common.Component.BGCollision;
using SeitonSense.Core;
using SeitonSense.Plugin.Models;

namespace SeitonSense.Plugin.Services;

internal readonly record struct PanicShukuchiDiagnostics(
    bool Enabled,
    bool MetadataVerified,
    bool BackwardCommandEnabled,
    SupportedPvPContext LastContext,
    PanicShukuchiPoint LastOrigin,
    PanicShukuchiPoint LastDestination,
    string LastCommand,
    BackwardPanicShukuchiCameraObservation LastCamera,
    uint LastAdjustedActionId,
    long CommandCount,
    long AttemptCount,
    long AcceptedCount,
    long RejectedCount,
    long RefusedCount,
    string LastEvent)
{
    internal string ToChatLine() =>
        $"mode=immediate,enabled={Enabled},bw-enabled={BackwardCommandEnabled},meta={MetadataVerified}," +
        $"context={LastContext},command={LastCommand},origin=" +
        $"{LastOrigin.X:0.00}/{LastOrigin.Y:0.00}/{LastOrigin.Z:0.00},destination=" +
        $"{LastDestination.X:0.00}/{LastDestination.Y:0.00}/{LastDestination.Z:0.00}," +
        $"camera={LastCamera.CameraManagerAvailable}/{LastCamera.NormalCameraAvailable}/" +
        $"{LastCamera.ActiveCameraMatchesNormal}/index-{LastCamera.ActiveCameraIndex}/" +
        $"mode-{LastCamera.ControlMode}/zoom-{LastCamera.ZoomMode}/" +
        $"event-{LastCamera.EventCameraAutoControl}/yaw-{LastCamera.DirectionRadians:0.000}," +
        $"adjusted={LastAdjustedActionId}," +
        $"count={CommandCount}/{AttemptCount}/{AcceptedCount}/{RejectedCount}/{RefusedCount}," +
        $"last={LastEvent}";
}

/// <summary>
/// Executes the explicit /panicshu command and its default-off /seitonbw sister.
/// Every invocation computes one exact 19.5-yalm ground point and immediately
/// makes at most one native UseActionLocation call. /panicshu keeps character
/// facing; /seitonbw reads only the normal gameplay camera and uses its exact
/// camera-relative screen-back direction. The shared path deliberately has no scheduler,
/// Guard/CC gate, Purify priority, cast/queue/animation wait, or pending state,
/// retry, shorter-point fallback, cursor movement, or target mutation.
/// A positively ready native cooldown is required before the call so an active
/// recast cannot start a client-predicted animation which later rolls back.
/// </summary>
internal sealed class PanicShukuchiService
{
    private enum DestinationMode
    {
        CharacterFacingForward,
        CameraFacingBackward,
    }

    internal const string Command = "/panicshu";
    internal const string BackwardCameraCommand = "/seitonbw";

    private const ulong DefaultTargetSentinel = 0xE0000000UL;
    private const float GroundProbeStartAboveYalms = 5f;
    private const float GroundProbeMaximumDistanceYalms = 10f;

    private readonly object diagnosticsGate = new();
    private readonly PluginConfiguration configuration;
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly IDutyState dutyState;
    private readonly NearAssistRedirector nearAssist;
    private readonly IPluginLog log;
    private readonly bool metadataVerified;

    private long commandCount;
    private long attemptCount;
    private long acceptedCount;
    private long rejectedCount;
    private long refusedCount;
    private SupportedPvPContext lastContext;
    private PanicShukuchiPoint lastOrigin;
    private PanicShukuchiPoint lastDestination;
    private string lastCommand;
    private BackwardPanicShukuchiCameraObservation lastCamera;
    private uint lastAdjustedActionId;
    private string lastEvent;

    internal PanicShukuchiService(
        PluginConfiguration configuration,
        IClientState clientState,
        IObjectTable objectTable,
        IDutyState dutyState,
        NearAssistRedirector nearAssist,
        IPluginLog log,
        PvPMetadataValidation metadata)
    {
        this.configuration = configuration;
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.dutyState = dutyState;
        this.nearAssist = nearAssist;
        this.log = log;
        metadataVerified = metadata.PanicShukuchiVerified;
        lastCommand = "none";
        lastEvent = metadataVerified
            ? "Ready for immediate explicit /panicshu"
            : "Metadata mismatch; disabled";
    }

    internal PanicShukuchiDiagnostics Diagnostics
    {
        get
        {
            lock (diagnosticsGate)
            {
                return new PanicShukuchiDiagnostics(
                    configuration.Enabled,
                    metadataVerified,
                    configuration.EnableBackwardPanicShukuchiCommand,
                    lastContext,
                    lastOrigin,
                    lastDestination,
                    lastCommand,
                    lastCamera,
                    lastAdjustedActionId,
                    commandCount,
                    attemptCount,
                    acceptedCount,
                    rejectedCount,
                    refusedCount,
                    lastEvent);
            }
        }
    }

    internal void Execute(string arguments) =>
        ExecuteImmediate(arguments, DestinationMode.CharacterFacingForward);

    internal void ExecuteBackwardCamera(string arguments) =>
        ExecuteImmediate(arguments, DestinationMode.CameraFacingBackward);

    private unsafe void ExecuteImmediate(string arguments, DestinationMode mode)
    {
        var command = mode == DestinationMode.CameraFacingBackward
            ? BackwardCameraCommand
            : Command;
        lock (diagnosticsGate)
        {
            commandCount++;
            lastCommand = command;
            lastCamera = default;
        }

        if (!string.IsNullOrWhiteSpace(arguments))
        {
            RecordRefused($"Arguments rejected; use {command} without arguments");
            return;
        }

        if (mode == DestinationMode.CameraFacingBackward &&
            !configuration.EnableBackwardPanicShukuchiCommand)
        {
            RecordRefused("/seitonbw is disabled in Macro Helpers");
            return;
        }

        try
        {
            var context = ResolveContext();
            var local = objectTable.LocalPlayer;
            var localValid = HasValidLocalIdentity(local) &&
                             !local!.IsDead &&
                             local.CurrentHp > 0 &&
                             local.MaxHp > 0 &&
                             local.ClassJob.IsValid;

            var actionManager = ActionManager.Instance();
            if (actionManager == null)
            {
                RecordRefused("Action manager unavailable");
                return;
            }

            // This remains an immediate command, but an active recast must end
            // before the native location call. Otherwise the client may briefly
            // predict the animation/recast and then roll it back, occupying the
            // same action boundary another helper (notably Auto-Seiton) needs.
            var adjustedActionId = actionManager->GetAdjustedActionId(
                PanicShukuchiRules.ActionId);
            var readiness = ClientActionAttemptBoundary.Capture(
                actionManager,
                PanicShukuchiRules.ActionId);
            var recastGroup = actionManager->GetRecastGroup(
                (int)ActionType.Action,
                PanicShukuchiRules.ActionId);
            var recast = recastGroup == PanicShukuchiRules.ExpectedRuntimeRecastGroupIndex
                ? actionManager->GetRecastGroupDetail(recastGroup)
                : null;
            if (!readiness.Captured ||
                adjustedActionId != PanicShukuchiRules.ActionId ||
                readiness.AdjustedActionId != PanicShukuchiRules.ActionId ||
                !readiness.IsActionOffCooldown ||
                readiness.ResourceStatus != 0 ||
                recast == null ||
                recast->IsActive ||
                actionManager->GetAdditionalRecastGroup(
                    ActionType.Action,
                    PanicShukuchiRules.ActionId) >= 0 ||
                ActionManager.GetAdjustedRecastTime(
                    ActionType.Action,
                    PanicShukuchiRules.ActionId,
                    true) != PanicShukuchiRules.ExpectedAdjustedRecastMilliseconds)
            {
                RecordRefused("Shukuchi cooldown is not positively ready; command ended");
                return;
            }

            var origin = default(PanicShukuchiPoint);
            var candidate = default(PanicShukuchiCandidate);
            var cameraObservation = default(BackwardPanicShukuchiCameraObservation);
            if (localValid)
            {
                var position = local!.Position;
                origin = new PanicShukuchiPoint(position.X, position.Y, position.Z);
                var directionRotation = default(float);
                var groundProbe = default(PanicShukuchiPoint);
                bool probeCreated;
                if (mode == DestinationMode.CharacterFacingForward)
                {
                    directionRotation = local.Rotation;
                    probeCreated = PanicShukuchiRules.TryCreateForwardProbe(
                        origin,
                        directionRotation,
                        out groundProbe);
                }
                else
                {
                    var cameraManager = CameraManager.Instance();
                    Camera* normalCamera = cameraManager != null
                        ? cameraManager->Camera
                        : null;
                    var activeCameraIndex = cameraManager != null
                        ? cameraManager->ActiveCameraIndex
                        : -1;
                    Camera* activeCamera = null;
                    if (cameraManager != null &&
                        normalCamera != null &&
                        activeCameraIndex ==
                        BackwardPanicShukuchiRules.NormalGameplayCameraIndex)
                    {
                        activeCamera = cameraManager->GetActiveCamera();
                    }

                    cameraObservation = new BackwardPanicShukuchiCameraObservation(
                        CameraManagerAvailable: cameraManager != null,
                        NormalCameraAvailable: normalCamera != null,
                        ActiveCameraMatchesNormal:
                            normalCamera != null && activeCamera == normalCamera,
                        ActiveCameraIndex: activeCameraIndex,
                        ControlMode: normalCamera != null
                            ? (int)normalCamera->ControlMode
                            : -1,
                        ZoomMode: normalCamera != null
                            ? (int)normalCamera->ZoomMode
                            : -1,
                        EventCameraAutoControl:
                            normalCamera != null && normalCamera->IsEventCameraAutoControl,
                        DirectionRadians: normalCamera != null
                            ? normalCamera->DirH
                            : float.NaN);
                    probeCreated =
                        BackwardPanicShukuchiRules.TryCreateBackwardCameraProbe(
                            origin,
                            cameraObservation,
                            out directionRotation,
                            out groundProbe);
                }

                if (probeCreated)
                {
                    var rayOrigin = new Vector3(
                        groundProbe.X,
                        groundProbe.Y + GroundProbeStartAboveYalms,
                        groundProbe.Z);
                    if (BGCollisionModule.RaycastMaterialFilter(
                            rayOrigin,
                            -Vector3.UnitY,
                            out var groundHit,
                            GroundProbeMaximumDistanceYalms) &&
                        float.IsFinite(groundHit.Distance) &&
                        groundHit.Distance >= 0f &&
                        groundHit.Distance <= GroundProbeMaximumDistanceYalms)
                    {
                        var hit = groundHit.Point;
                        candidate = new PanicShukuchiCandidate(
                            origin,
                            directionRotation,
                            new PanicShukuchiGroundHit(
                                true,
                                new PanicShukuchiPoint(hit.X, hit.Y, hit.Z)));
                    }
                }
            }

            var decision = PanicShukuchiRules.Evaluate(
                new PanicShukuchiCommandObservation(
                    configuration.Enabled,
                    metadataVerified,
                    context,
                    configuration.EnableWolvesDenTesting,
                    localValid && local!.ClassJob.IsValid ? local.ClassJob.RowId : 0,
                    localValid,
                    adjustedActionId,
                    candidate));

            lock (diagnosticsGate)
            {
                lastContext = context;
                lastOrigin = origin;
                lastDestination = candidate.GroundHit.Position;
                lastCamera = cameraObservation;
                lastAdjustedActionId = adjustedActionId;
            }

            if (!decision.ShouldAttempt || decision.Intent is not { } intent)
            {
                RecordRefused(mode == DestinationMode.CameraFacingBackward
                    ? $"Immediate /seitonbw command refused: {decision.Reason}"
                    : $"Immediate command refused: {decision.Reason}");
                return;
            }

            if (mode == DestinationMode.CameraFacingBackward &&
                !configuration.EnableBackwardPanicShukuchiCommand)
            {
                RecordRefused("/seitonbw was disabled before its native boundary");
                return;
            }

            var destination = new Vector3(
                intent.Destination.X,
                intent.Destination.Y,
                intent.Destination.Z);

            // The user-authored command reaches its only native boundary now.
            // No state exists that could wait, expire, retry, or replay it.
            lock (diagnosticsGate) attemptCount++;
            bool accepted;
            try
            {
                using var explicitGuardBreak = nearAssist.EnterExplicitAutoGuardBreak(
                    PanicShukuchiRules.ActionId);
                accepted = actionManager->UseActionLocation(
                    ActionType.Action,
                    PanicShukuchiRules.ActionId,
                    DefaultTargetSentinel,
                    &destination,
                    0,
                    0);
            }
            catch (Exception exception)
            {
                lock (diagnosticsGate)
                {
                    rejectedCount++;
                    lastEvent = mode == DestinationMode.CameraFacingBackward
                        ? "Immediate /seitonbw native Shukuchi threw; command ended"
                        : "Immediate native Shukuchi threw; command ended";
                }

                if (mode == DestinationMode.CameraFacingBackward)
                {
                    log.Error(
                        exception,
                        "Seiton Sense immediate /seitonbw Panic Shukuchi native call failed.");
                }
                else
                {
                    log.Error(exception, "Seiton Sense immediate Panic Shukuchi native call failed.");
                }
                return;
            }

            lock (diagnosticsGate)
            {
                if (accepted)
                {
                    acceptedCount++;
                    lastEvent = mode == DestinationMode.CameraFacingBackward
                        ? "Immediate /seitonbw native Shukuchi accepted"
                        : "Immediate native Shukuchi accepted";
                }
                else
                {
                    rejectedCount++;
                    lastEvent = mode == DestinationMode.CameraFacingBackward
                        ? "Immediate /seitonbw native Shukuchi rejected"
                        : "Immediate native Shukuchi rejected";
                }
            }
        }
        catch (Exception exception)
        {
            lock (diagnosticsGate)
            {
                refusedCount++;
                lastEvent = mode == DestinationMode.CameraFacingBackward
                    ? "Immediate /seitonbw validation failed closed"
                    : "Immediate command validation failed closed";
            }

            if (mode == DestinationMode.CameraFacingBackward)
            {
                log.Error(
                    exception,
                    "Seiton Sense immediate /seitonbw Panic Shukuchi command failed closed.");
            }
            else
            {
                log.Error(exception, "Seiton Sense immediate Panic Shukuchi command failed closed.");
            }
        }
    }

    private void RecordRefused(string eventText)
    {
        lock (diagnosticsGate)
        {
            refusedCount++;
            lastEvent = eventText;
        }
    }

    private SupportedPvPContext ResolveContext()
    {
        var condition = dutyState.ContentFinderCondition;
        return PvPMatchRules.ResolveSupportedContext(
            clientState.IsPvP,
            clientState.IsPvPExcludingDen,
            configuration.EnableWolvesDenTesting,
            clientState.TerritoryType,
            condition.IsValid,
            condition.IsValid && condition.Value.PvP,
            condition.IsValid ? condition.Value.ContentUICategory.RowId : 0,
            condition.IsValid && condition.Value.CrystallineConflictCasualRoulette,
            condition.IsValid && condition.Value.CrystallineConflictRankedRoulette);
    }

    private static bool HasValidLocalIdentity(IPlayerCharacter? local) =>
        local is not null &&
        local.Address != nint.Zero &&
        local.IsValid() &&
        local.GameObjectId is not 0 and not DefaultTargetSentinel and not ulong.MaxValue &&
        local.EntityId is not 0 and not (uint)DefaultTargetSentinel and not uint.MaxValue;
}
