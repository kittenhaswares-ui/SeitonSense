using System.Numerics;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Common.Component.BGCollision;
using SeitonSense.Core;
using SeitonSense.Plugin.Models;

namespace SeitonSense.Plugin.Services;

internal readonly record struct PanicShukuchiDiagnostics(
    bool Enabled,
    bool MetadataVerified,
    bool BackwardCommandEnabled,
    bool DirectionalRotationHookAvailable,
    bool LastDirectionalCompatibilityPassed,
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
        $"bw-hook={DirectionalRotationHookAvailable}," +
        $"bw-compat={LastDirectionalCompatibilityPassed}," +
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
/// NIN keeps the exact 19.5-yalm ground-point/UseActionLocation implementation.
/// AST, DNC, DRG, RPR, and PCT use one reviewed self-action: /seitonbw reads the
/// normal gameplay camera, writes only the local actor facing required for that
/// action to travel screen-back, and immediately makes at most one UseAction
/// call. It never rotates the camera or changes a target. Both branches have no
/// scheduler, Guard/CC gate, Purify priority, wait, pending state, retry, target
/// search, or alternate action. Exact current metadata, base-action identity,
/// charge/resource state, and an immediately clean native boundary are required.
/// </summary>
internal sealed unsafe class PanicShukuchiService : IDisposable
{
    private sealed class DirectionalRotationOverrideScope : IDisposable
    {
        private PanicShukuchiService? owner;

        internal DirectionalRotationOverrideScope(
            PanicShukuchiService owner,
            BackwardDashRotationOverrideLease lease)
        {
            this.owner = owner;
            Lease = lease;
        }

        internal BackwardDashRotationOverrideLease Lease { get; }

        public void Dispose()
        {
            var currentOwner = Interlocked.Exchange(ref owner, null);
            currentOwner?.ReleaseDirectionalRotationOverride(this);
        }

        internal void Retire() => Interlocked.Exchange(ref owner, null);
    }

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
    private readonly IntegratedActionBufferRuntime actionBuffer;
    private readonly IPluginLog log;
    private readonly Hook<GameObject.Delegates.SetRotation>? setRotationHook;
    private readonly bool panicShukuchiMetadataVerified;
    private readonly BackwardDashMetadataCatalog backwardDashMetadata;

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
    private bool lastMetadataVerified;
    private bool lastDirectionalCompatibilityPassed;
    private string lastEvent;
    private DirectionalRotationOverrideScope? directionalRotationOverride;
    private bool started;
    private bool disposed;

    internal PanicShukuchiService(
        PluginConfiguration configuration,
        IClientState clientState,
        IObjectTable objectTable,
        IDutyState dutyState,
        NearAssistRedirector nearAssist,
        IntegratedInputRuntime integratedInput,
        IGameInteropProvider interop,
        IPluginLog log,
        PvPMetadataValidation metadata)
    {
        this.configuration = configuration;
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.dutyState = dutyState;
        this.nearAssist = nearAssist;
        actionBuffer = integratedInput.ActionBuffer;
        this.log = log;
        panicShukuchiMetadataVerified = metadata.PanicShukuchiVerified;
        backwardDashMetadata = metadata.BackwardDashActions;
        lastMetadataVerified = panicShukuchiMetadataVerified;
        lastCommand = "none";
        lastEvent = panicShukuchiMetadataVerified
            ? "Ready for immediate explicit /panicshu"
            : "Metadata mismatch; disabled";

        try
        {
            setRotationHook = interop.HookFromAddress<GameObject.Delegates.SetRotation>(
                GameObject.MemberFunctionPointers.SetRotation,
                SetRotationDetour);
        }
        catch (Exception exception)
        {
            log.Error(
                exception,
                "Seiton Sense directional /seitonbw rotation hook is unavailable; non-NIN mappings remain off.");
        }
    }

    internal PanicShukuchiDiagnostics Diagnostics
    {
        get
        {
            lock (diagnosticsGate)
            {
                return new PanicShukuchiDiagnostics(
                    configuration.Enabled,
                    lastMetadataVerified,
                    configuration.EnableBackwardPanicShukuchiCommand,
                    setRotationHook?.IsEnabled == true,
                    lastDirectionalCompatibilityPassed,
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

    internal void ExecuteBackwardCamera(string arguments)
    {
        var local = objectTable.LocalPlayer;
        if (local?.ClassJob.IsValid == true &&
            local.ClassJob.RowId == PanicShukuchiRules.NinjaJobId)
        {
            ExecuteImmediate(arguments, DestinationMode.CameraFacingBackward);
            return;
        }

        ExecuteBackwardDirectional(arguments);
    }

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
            lastMetadataVerified = panicShukuchiMetadataVerified;
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
                    cameraObservation = CaptureBackwardCameraObservation();
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
                    panicShukuchiMetadataVerified,
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

    internal void Start()
    {
        if (started || disposed) return;
        started = true;
    }

    private bool EnsureDirectionalRotationHookEnabled()
    {
        if (!started || disposed || setRotationHook is null) return false;
        if (setRotationHook.IsEnabled) return true;

        try
        {
            setRotationHook.Enable();
            return setRotationHook.IsEnabled;
        }
        catch (Exception exception)
        {
            log.Warning(
                exception,
                "Seiton Sense directional /seitonbw rotation hook could not be enabled; this attempt failed closed.");
            return false;
        }
    }

    private unsafe void ExecuteBackwardDirectional(string arguments)
    {
        lock (diagnosticsGate)
        {
            commandCount++;
            lastCommand = BackwardCameraCommand;
            lastCamera = default;
            lastMetadataVerified = false;
            lastDirectionalCompatibilityPassed = false;
        }

        if (!string.IsNullOrWhiteSpace(arguments))
        {
            RecordRefused($"Arguments rejected; use {BackwardCameraCommand} without arguments");
            return;
        }

        if (!configuration.Enabled)
        {
            RecordRefused("Seiton Sense is disabled");
            return;
        }

        if (!configuration.EnableBackwardPanicShukuchiCommand)
        {
            RecordRefused("/seitonbw is disabled in Macro Helpers");
            return;
        }

        if (!EnsureDirectionalRotationHookEnabled())
        {
            RecordRefused("Directional rotation hook unavailable; non-NIN /seitonbw failed closed");
            return;
        }

        GameObject* nativeLocal = null;
        var originalHeading = 0f;
        var facingWritten = false;
        var nativeBoundaryEntered = false;
        try
        {
            var context = ResolveContext();
            var local = objectTable.LocalPlayer;
            var localValid = HasValidLocalIdentity(local) &&
                             !local!.IsDead &&
                             local.IsTargetable &&
                             local.CurrentHp > 0 &&
                             local.MaxHp >= local.CurrentHp &&
                             local.ClassJob.IsValid;
            var localJobId = localValid ? local!.ClassJob.RowId : 0;
            if (!localValid)
            {
                RecordRefused("Local player is unavailable or invalid");
                return;
            }

            if (!PanicShukuchiRules.IsSupportedContext(
                    context,
                    configuration.EnableWolvesDenTesting))
            {
                RecordRefused("/seitonbw is available only in CC or enabled Wolves' Den testing");
                return;
            }

            if (!BackwardDashRules.TryGetDirectionalProfile(localJobId, out var profile))
            {
                RecordRefused("Current job has no reviewed camera-back self dash");
                return;
            }

            var profileMetadataVerified = backwardDashMetadata.Contains(profile.ActionId);
            lock (diagnosticsGate) lastMetadataVerified = profileMetadataVerified;
            if (!profileMetadataVerified)
            {
                RecordRefused($"{profile.Name} metadata mismatch; this job mapping is disabled");
                return;
            }

            var actionManager = ActionManager.Instance();
            if (actionManager == null)
            {
                RecordRefused("Action manager unavailable");
                return;
            }

            var cameraObservation = CaptureBackwardCameraObservation();
            var position = local!.Position;
            var origin = new PanicShukuchiPoint(position.X, position.Y, position.Z);
            if (!BackwardPanicShukuchiRules.TryCreateBackwardCameraProbe(
                    origin,
                    cameraObservation,
                    out var screenBackHeading,
                    out _) ||
                !BackwardDashRules.TryResolveActorFacing(
                    screenBackHeading,
                    profile.MovementKind,
                    out var desiredActorHeading))
            {
                RecordRefused("Normal gameplay camera direction is unavailable");
                return;
            }

            var adjustedActionId = actionManager->GetAdjustedActionId(profile.ActionId);
            var readiness = ClientActionAttemptBoundary.Capture(
                actionManager,
                profile.ActionId);
            var recastGroup = actionManager->GetRecastGroup(
                (int)ActionType.Action,
                profile.ActionId);
            var recast = recastGroup == profile.RuntimeRecastGroupIndex
                ? actionManager->GetRecastGroupDetail(recastGroup)
                : null;
            var currentCharges = actionManager->GetCurrentCharges(profile.ActionId);
            var targetStatus = actionManager->GetActionStatus(
                ActionType.Action,
                profile.ActionId,
                local.GameObjectId,
                checkRecastActive: true,
                checkCastingActive: true);

            lock (diagnosticsGate)
            {
                lastContext = context;
                lastOrigin = origin;
                lastDestination = default;
                lastCamera = cameraObservation;
                lastAdjustedActionId = adjustedActionId;
            }

            if (adjustedActionId != profile.ActionId ||
                !readiness.IsExactActionReady(profile.ActionId) ||
                readiness.AnimationLockSeconds >
                    BackwardDashRules.MaximumImmediateAnimationLockSeconds ||
                local.IsCasting ||
                recast == null ||
                ActionManager.GetAdjustedRecastTime(
                    ActionType.Action,
                    profile.ActionId,
                    true) != profile.AdjustedRecastMilliseconds ||
                currentCharges is 0 ||
                currentCharges > profile.MaximumAccessibleCharges ||
                targetStatus != 0)
            {
                RecordRefused($"{profile.Name} is not positively ready for one immediate attempt");
                return;
            }

            nativeLocal = (GameObject*)local.Address;
            if (nativeLocal == null ||
                nativeLocal->EntityId != local.EntityId ||
                !float.IsFinite(nativeLocal->Rotation))
            {
                RecordRefused("Exact local native actor is unavailable");
                return;
            }

            originalHeading = nativeLocal->Rotation;
            nativeLocal->SetRotation(desiredActorHeading);
            facingWritten = true;
            if (!BackwardDashRules.AreHeadingsEquivalent(
                    nativeLocal->Rotation,
                    desiredActorHeading))
            {
                TryRestoreHeading(nativeLocal, originalHeading);
                facingWritten = false;
                RecordRefused("Camera-back actor-facing write was not confirmed");
                return;
            }

            var finalLocal = objectTable.LocalPlayer;
            var before = ClientActionAttemptBoundary.Capture(
                actionManager,
                profile.ActionId);
            var beforeCharges = actionManager->GetCurrentCharges(profile.ActionId);
            if (!configuration.Enabled ||
                !configuration.EnableBackwardPanicShukuchiCommand ||
                ResolveContext() != context ||
                !HasValidLocalIdentity(finalLocal) ||
                finalLocal!.Address != local.Address ||
                finalLocal.GameObjectId != local.GameObjectId ||
                finalLocal.EntityId != local.EntityId ||
                !finalLocal.ClassJob.IsValid ||
                finalLocal.ClassJob.RowId != profile.JobId ||
                !backwardDashMetadata.Contains(profile.ActionId) ||
                !before.IsExactActionReady(profile.ActionId) ||
                before.AnimationLockSeconds >
                    BackwardDashRules.MaximumImmediateAnimationLockSeconds ||
                beforeCharges is 0 ||
                beforeCharges > profile.MaximumAccessibleCharges ||
                actionManager->GetActionStatus(
                    ActionType.Action,
                    profile.ActionId,
                    local.GameObjectId,
                    checkRecastActive: true,
                    checkCastingActive: true) != 0)
            {
                TryRestoreHeading(nativeLocal, originalHeading);
                facingWritten = false;
                RecordRefused("/seitonbw state changed before its native boundary");
                return;
            }

            if (!actionBuffer.CanDispatchExactExternalAction(
                    profile.ActionId,
                    adjustedActionId,
                    out var compatibilityReason))
            {
                TryRestoreHeading(nativeLocal, originalHeading);
                facingWritten = false;
                RecordRefused($"/seitonbw foreign action ownership blocked: {compatibilityReason}");
                return;
            }

            lock (diagnosticsGate) lastDirectionalCompatibilityPassed = true;

            bool accepted;
            try
            {
                using var rotationOverride = EnterDirectionalRotationOverride(
                    nativeLocal,
                    local.EntityId,
                    profile.ActionId,
                    desiredActorHeading);
                using var explicitGuardBreak = nearAssist.EnterExplicitAutoGuardBreak(
                    profile.ActionId,
                    ExplicitAutoGuardBreakBoundary.StandardAction);
                lock (diagnosticsGate) attemptCount++;
                nativeBoundaryEntered = true;
                accepted = nearAssist.RunWithoutRedirect(() =>
                    actionManager->UseAction(
                        ActionType.Action,
                        profile.ActionId,
                        local.GameObjectId,
                        0,
                        ActionManager.UseActionMode.None,
                        0));
            }
            catch (Exception exception)
            {
                if (!nativeBoundaryEntered)
                {
                    TryRestoreHeading(nativeLocal, originalHeading);
                    facingWritten = false;
                    lock (diagnosticsGate)
                    {
                        refusedCount++;
                        lastEvent = $"Immediate /seitonbw {profile.Name} rotation boundary failed closed";
                    }

                    log.Error(
                        exception,
                        "Seiton Sense immediate /seitonbw {ActionName} rotation boundary failed closed.",
                        profile.Name);
                    return;
                }

                lock (diagnosticsGate)
                {
                    rejectedCount++;
                    lastEvent = $"Immediate /seitonbw {profile.Name} native call threw; acceptance unknown";
                }

                log.Error(
                    exception,
                    "Seiton Sense immediate /seitonbw {ActionName} native call failed.",
                    profile.Name);
                return;
            }

            var after = ClientActionAttemptBoundary.Capture(
                actionManager,
                profile.ActionId);
            var afterCharges = actionManager->GetCurrentCharges(profile.ActionId);
            var outcome = ClientActionAttemptBoundaryRules.Classify(
                accepted,
                profile.ActionId,
                before,
                after);
            if (!accepted &&
                outcome == ClientActionAttemptOutcome.ClientRejected &&
                afterCharges != beforeCharges)
            {
                outcome = ClientActionAttemptOutcome.AcceptanceUnknown;
            }
            if (outcome == ClientActionAttemptOutcome.ClientRejected)
            {
                TryRestoreHeading(nativeLocal, originalHeading);
                facingWritten = false;
            }

            lock (diagnosticsGate)
            {
                if (accepted)
                {
                    acceptedCount++;
                    lastEvent = $"Immediate /seitonbw {profile.Name} accepted";
                }
                else
                {
                    rejectedCount++;
                    lastEvent = $"Immediate /seitonbw {profile.Name} returned {outcome}";
                }
            }
        }
        catch (Exception exception)
        {
            if (facingWritten && !nativeBoundaryEntered)
                TryRestoreHeading(nativeLocal, originalHeading);

            lock (diagnosticsGate)
            {
                refusedCount++;
                lastEvent = nativeBoundaryEntered
                    ? "Immediate /seitonbw directional validation faulted after native entry"
                    : "Immediate /seitonbw directional validation failed closed";
            }

            log.Error(
                exception,
                "Seiton Sense immediate directional /seitonbw command failed closed.");
        }
    }

    private void SetRotationDetour(GameObject* actor, float requestedHeading)
    {
        var effectiveHeading = requestedHeading;
        var scope = Volatile.Read(ref directionalRotationOverride);
        if (actor != null &&
            scope is not null &&
            BackwardDashRules.ShouldOverrideRotation(
                scope.Lease,
                Environment.CurrentManagedThreadId,
                (ulong)(nuint)actor,
                actor->EntityId))
        {
            effectiveHeading = scope.Lease.DesiredActorHeading;
        }

        setRotationHook!.Original(actor, effectiveHeading);
    }

    private IDisposable EnterDirectionalRotationOverride(
        GameObject* local,
        uint localEntityId,
        uint actionId,
        float desiredActorHeading)
    {
        if (!started || disposed || setRotationHook?.IsEnabled != true || local == null)
            throw new InvalidOperationException("Directional rotation hook is unavailable.");

        var lease = new BackwardDashRotationOverrideLease(
            Environment.CurrentManagedThreadId,
            actionId,
            (ulong)(nuint)local,
            localEntityId,
            desiredActorHeading);
        if (!lease.IsValid)
            throw new InvalidOperationException("Directional rotation identity is invalid.");

        var scope = new DirectionalRotationOverrideScope(this, lease);
        if (Interlocked.CompareExchange(
                ref directionalRotationOverride,
                scope,
                comparand: null) is not null)
        {
            scope.Retire();
            throw new InvalidOperationException(
                "A directional rotation override is already active.");
        }

        return scope;
    }

    private void ReleaseDirectionalRotationOverride(
        DirectionalRotationOverrideScope scope) =>
        Interlocked.CompareExchange(
            ref directionalRotationOverride,
            null,
            scope);

    private static unsafe BackwardPanicShukuchiCameraObservation
        CaptureBackwardCameraObservation()
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
            activeCameraIndex == BackwardPanicShukuchiRules.NormalGameplayCameraIndex)
        {
            activeCamera = cameraManager->GetActiveCamera();
        }

        return new BackwardPanicShukuchiCameraObservation(
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
    }

    private static unsafe void TryRestoreHeading(
        GameObject* local,
        float originalHeading)
    {
        if (local == null || !float.IsFinite(originalHeading)) return;
        try
        {
            local->SetRotation(originalHeading);
        }
        catch
        {
            // A best-effort facing restore must never create a second action.
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

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        started = false;
        Interlocked.Exchange(ref directionalRotationOverride, null)?.Retire();
        setRotationHook?.Dispose();
    }
}
