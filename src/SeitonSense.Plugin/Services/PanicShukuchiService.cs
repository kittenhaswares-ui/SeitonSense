using System.Numerics;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Common.Component.BGCollision;
using SeitonSense.Core;
using SeitonSense.Plugin.Models;

namespace SeitonSense.Plugin.Services;

internal readonly record struct PanicShukuchiDiagnostics(
    bool Started,
    bool Enabled,
    bool MetadataVerified,
    bool Pending,
    long RemainingMilliseconds,
    SupportedPvPContext LastContext,
    PanicShukuchiPoint LastOrigin,
    PanicShukuchiPoint LastDestination,
    uint LastAdjustedActionId,
    ushort LastSequenceBefore,
    ushort LastSequenceAfter,
    long ArmedCount,
    long AttemptCount,
    long AcceptedCount,
    long RejectedCount,
    long CancelledCount,
    string LastEvent)
{
    internal string ToChatLine() =>
        $"started={Started},enabled={Enabled},meta={MetadataVerified},pending={Pending}/" +
        $"{RemainingMilliseconds}ms,context={LastContext},origin=" +
        $"{LastOrigin.X:0.00}/{LastOrigin.Y:0.00}/{LastOrigin.Z:0.00},destination=" +
        $"{LastDestination.X:0.00}/{LastDestination.Y:0.00}/{LastDestination.Z:0.00}," +
        $"adjusted={LastAdjustedActionId},seq={LastSequenceBefore}/{LastSequenceAfter}," +
        $"count={ArmedCount}/{AttemptCount}/{AcceptedCount}/{RejectedCount}/{CancelledCount}," +
        $"last={LastEvent}";
}

/// <summary>
/// Executes only an explicit /panicshu command. The command freezes one exact
/// 19.5-yalm forward ground point. A short lease may wait for an already-owned
/// cast, action queue, or animation lock, then spends itself before the sole
/// UseActionLocation call. There is no automatic trigger, retry, shorter-point
/// fallback, cursor movement, or target mutation.
/// </summary>
internal sealed class PanicShukuchiService : IDisposable
{
    internal const string Command = "/panicshu";

    private const ulong DefaultTargetSentinel = 0xE0000000UL;
    private const float GroundProbeStartAboveYalms = 5f;
    private const float GroundProbeMaximumDistanceYalms = 10f;
    private const float AnimationLockClearEpsilonSeconds = 0.0005f;

    private readonly object stateGate = new();
    private readonly PluginConfiguration configuration;
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly IDutyState dutyState;
    private readonly IFramework framework;
    private readonly IChatGui chatGui;
    private readonly IPluginLog log;
    private readonly NearAssistRedirector nearAssist;
    private readonly Func<bool> isPurifyPriorityClaimed;
    private readonly bool metadataVerified;

    private PanicShukuchiPendingState pendingState = PanicShukuchiPendingState.Initial;
    private nint pendingLocalAddress;
    private bool started;
    private bool disposed;
    private long armedCount;
    private long attemptCount;
    private long acceptedCount;
    private long rejectedCount;
    private long cancelledCount;
    private SupportedPvPContext lastContext;
    private PanicShukuchiPoint lastOrigin;
    private PanicShukuchiPoint lastDestination;
    private uint lastAdjustedActionId;
    private ushort lastSequenceBefore;
    private ushort lastSequenceAfter;
    private string lastEvent;

    internal PanicShukuchiService(
        PluginConfiguration configuration,
        IClientState clientState,
        IObjectTable objectTable,
        IDutyState dutyState,
        IFramework framework,
        IChatGui chatGui,
        IPluginLog log,
        NearAssistRedirector nearAssist,
        Func<bool> isPurifyPriorityClaimed,
        PvPMetadataValidation metadata)
    {
        this.configuration = configuration;
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.dutyState = dutyState;
        this.framework = framework;
        this.chatGui = chatGui;
        this.log = log;
        this.nearAssist = nearAssist;
        this.isPurifyPriorityClaimed = isPurifyPriorityClaimed;
        metadataVerified = metadata.PanicShukuchiVerified;
        lastEvent = metadataVerified ? "Ready for explicit /panicshu" : "Metadata mismatch; disabled";
    }

    internal PanicShukuchiDiagnostics Diagnostics
    {
        get
        {
            lock (stateGate)
            {
                var remaining = pendingState.Pending is { } current
                    ? Math.Max(0, current.ExpiresAtMilliseconds - Environment.TickCount64)
                    : 0;
                return new PanicShukuchiDiagnostics(
                    started,
                    configuration.Enabled,
                    metadataVerified,
                    pendingState.IsPending,
                    remaining,
                    lastContext,
                    lastOrigin,
                    lastDestination,
                    lastAdjustedActionId,
                    lastSequenceBefore,
                    lastSequenceAfter,
                    armedCount,
                    attemptCount,
                    acceptedCount,
                    rejectedCount,
                    cancelledCount,
                    lastEvent);
            }
        }
    }

    internal void Start()
    {
        if (disposed || started) return;
        started = true;
        framework.Update += OnFrameworkUpdate;
    }

    internal unsafe void Arm(string arguments)
    {
        if (!string.IsNullOrWhiteSpace(arguments))
        {
            RecordCancelled("Arguments rejected");
            chatGui.PrintError("[Seiton Sense] Usage: /panicshu");
            return;
        }

        PanicShukuchiPendingState? stateObservedAtCommand = null;
        try
        {
            var now = Environment.TickCount64;
            var pendingAlreadyLive = false;
            lock (stateGate)
            {
                stateObservedAtCommand = pendingState;
                if (pendingState.Pending is { } existing &&
                    existing.IsValid &&
                    now >= existing.ArmedAtMilliseconds &&
                    now < existing.ExpiresAtMilliseconds)
                {
                    lastEvent = "Second command rejected; exact pending intent preserved";
                    pendingAlreadyLive = true;
                }
            }

            if (pendingAlreadyLive)
            {
                chatGui.PrintError(
                    "[Seiton Sense] Panic Shukuchi is already waiting; the original point was preserved.");
                return;
            }

            if (!configuration.Enabled)
            {
                FailArm("Plugin disabled", "Panic Shukuchi is disabled while Seiton Sense is hidden.");
                return;
            }

            if (!metadataVerified)
            {
                FailArm(
                    "Metadata mismatch",
                    "Panic Shukuchi metadata did not verify for this game patch; nothing was attempted.");
                return;
            }

            var context = ResolveContext();
            if (!PanicShukuchiRules.IsSupportedContext(
                    context,
                    configuration.EnableWolvesDenTesting))
            {
                FailArm(
                    "Unsupported PvP context",
                    "Panic Shukuchi works only in Crystalline Conflict or enabled Wolves' Den testing.");
                return;
            }

            var local = objectTable.LocalPlayer;
            if (!HasValidLocalIdentity(local) || local!.IsDead || local.CurrentHp == 0 || local.MaxHp == 0)
            {
                FailArm("Invalid local player", "Panic Shukuchi could not verify the local player.");
                return;
            }

            if (!local.ClassJob.IsValid || local.ClassJob.RowId != PanicShukuchiRules.NinjaJobId)
            {
                FailArm("Wrong job", "Panic Shukuchi is NIN-only.");
                return;
            }

            var ownGuardClear = !nearAssist.IsLocalGuardActiveOrPropagatingForPanicShukuchi();
            if (!ownGuardClear)
            {
                FailArm("Own Guard active", "Panic Shukuchi will not break or overlap your own Guard.");
                return;
            }

            var incapacitated = IsIncapacitated(local);
            if (incapacitated)
            {
                FailArm(
                    "Purify priority / incapacitated",
                    "Panic Shukuchi did not arm while crowd-controlled; Purify keeps priority.");
                return;
            }

            var actionManager = ActionManager.Instance();
            var actionState = ClientActionAttemptBoundary.Capture(
                actionManager,
                PanicShukuchiRules.ActionId);
            if (!actionState.Captured)
            {
                FailArm("Action manager unavailable", "Panic Shukuchi could not read the action state.");
                return;
            }

            if (actionState.AdjustedActionId != PanicShukuchiRules.ActionId)
            {
                FailArm(
                    $"Adjusted action {actionState.AdjustedActionId}",
                    "Shukuchi is currently another action (for example Doton during Three Mudra); nothing was attempted.");
                return;
            }

            if (!actionState.IsActionOffCooldown || actionState.ResourceStatus != 0)
            {
                FailArm("Action not ready", "Shukuchi is not ready; nothing was queued or retried.");
                return;
            }

            var position = local.Position;
            var origin = new PanicShukuchiPoint(position.X, position.Y, position.Z);
            if (!PanicShukuchiRules.TryCreateForwardProbe(
                    origin,
                    local.Rotation,
                    out var forwardProbe))
            {
                FailArm("Invalid forward vector", "Panic Shukuchi could not verify your facing direction.");
                return;
            }

            var rayOrigin = new Vector3(
                forwardProbe.X,
                forwardProbe.Y + GroundProbeStartAboveYalms,
                forwardProbe.Z);
            if (!BGCollisionModule.RaycastMaterialFilter(
                    rayOrigin,
                    -Vector3.UnitY,
                    out var groundHit,
                    GroundProbeMaximumDistanceYalms) ||
                !float.IsFinite(groundHit.Distance) ||
                groundHit.Distance < 0f ||
                groundHit.Distance > GroundProbeMaximumDistanceYalms)
            {
                FailArm(
                    "No exact ground collision",
                    "Panic Shukuchi found no safe ground point 19.5 yalms straight ahead.");
                return;
            }

            var hit = groundHit.Point;
            var destination = new PanicShukuchiPoint(hit.X, hit.Y, hit.Z);
            var candidate = new PanicShukuchiCandidate(
                origin,
                local.Rotation,
                new PanicShukuchiGroundHit(true, destination));
            var identity = new TargetPressureActorIdentity(local.GameObjectId, local.EntityId);
            var observation = new PanicShukuchiArmObservation(
                now,
                configuration.Enabled,
                metadataVerified,
                context,
                configuration.EnableWolvesDenTesting,
                clientState.TerritoryType,
                local.ClassJob.RowId,
                identity,
                true,
                ownGuardClear,
                incapacitated,
                actionState.AdjustedActionId,
                candidate);

            PanicShukuchiArmDecision arm;
            lock (stateGate)
            {
                arm = PanicShukuchiRules.Arm(pendingState, observation);
                pendingState = arm.NextState;
                if (arm.DidArm)
                {
                    pendingLocalAddress = local.Address;
                    armedCount++;
                    lastContext = context;
                    lastOrigin = origin;
                    lastDestination = destination;
                    lastAdjustedActionId = actionState.AdjustedActionId;
                    lastSequenceBefore = actionState.LastUsedActionSequence;
                    lastSequenceAfter = actionState.LastUsedActionSequence;
                    lastEvent = "Explicit macro armed exact 19.5y forward point";
                }
                else
                {
                    cancelledCount++;
                    lastEvent = $"Arm rejected: {arm.Reason}";
                }
            }

            if (arm.Kind == PanicShukuchiArmDecisionKind.ExistingPendingPreserved)
            {
                chatGui.PrintError(
                    "[Seiton Sense] Panic Shukuchi is already waiting; the original point was preserved.");
            }
            else if (!arm.DidArm)
            {
                chatGui.PrintError(
                    $"[Seiton Sense] Panic Shukuchi did not arm ({arm.Reason}); nothing was attempted.");
            }
        }
        catch (Exception exception)
        {
            lock (stateGate)
            {
                if (stateObservedAtCommand is { } observed && pendingState == observed)
                {
                    pendingState = PanicShukuchiPendingState.Initial;
                    pendingLocalAddress = nint.Zero;
                    lastEvent = "Arm failed closed";
                }
                cancelledCount++;
            }

            log.Error(exception, "Seiton Sense Panic Shukuchi arm failed closed.");
            chatGui.PrintError("[Seiton Sense] Panic Shukuchi failed closed; nothing was attempted.");
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (started) framework.Update -= OnFrameworkUpdate;
        started = false;
        lock (stateGate)
        {
            pendingState = PanicShukuchiPendingState.Initial;
            pendingLocalAddress = nint.Zero;
            lastEvent = "Disposed";
        }
    }

    private unsafe void OnFrameworkUpdate(IFramework _)
    {
        PanicShukuchiPendingState previous;
        nint expectedAddress;
        lock (stateGate)
        {
            previous = pendingState;
            expectedAddress = pendingLocalAddress;
        }

        if (previous.Pending is not { } pending) return;

        try
        {
            var local = objectTable.LocalPlayer;
            var validLocal = HasValidLocalIdentity(local) && local!.Address == expectedAddress;
            var alive = validLocal && !local!.IsDead && local.CurrentHp > 0 && local.MaxHp > 0;
            var identity = validLocal
                ? new TargetPressureActorIdentity(local!.GameObjectId, local.EntityId)
                : default;
            var context = ResolveContext();
            var actionManager = ActionManager.Instance();
            var before = ClientActionAttemptBoundary.Capture(
                actionManager,
                PanicShukuchiRules.ActionId);
            var observation = new PanicShukuchiPendingObservation(
                Environment.TickCount64,
                configuration.Enabled,
                metadataVerified,
                context,
                configuration.EnableWolvesDenTesting,
                clientState.TerritoryType,
                validLocal && local!.ClassJob.IsValid ? local.ClassJob.RowId : 0,
                identity,
                alive,
                validLocal && !nearAssist.IsLocalGuardActiveOrPropagatingForPanicShukuchi(),
                validLocal && IsIncapacitated(local!),
                IsPurifyPriorityClaimed(),
                before.Captured && before.CastActionId == 0,
                before.Captured && !before.ActionQueued,
                before.Captured &&
                float.IsFinite(before.AnimationLockSeconds) &&
                before.AnimationLockSeconds >= 0f &&
                before.AnimationLockSeconds <= AnimationLockClearEpsilonSeconds,
                before.AdjustedActionId,
                before.Captured,
                before.IsActionOffCooldown,
                before.Captured && before.ResourceStatus == 0,
                pending.Intent.Destination);
            var decision = PanicShukuchiRules.ObservePending(previous, observation);

            lock (stateGate)
            {
                if (pendingState != previous) return;
                pendingState = decision.NextState;
                if (!pendingState.IsPending) pendingLocalAddress = nint.Zero;
                lastContext = context;
                lastAdjustedActionId = before.AdjustedActionId;
                lastSequenceAfter = before.LastUsedActionSequence;
                lastEvent = $"{decision.Kind}: {decision.Reason}";
                if (decision.Kind == PanicShukuchiPendingDecisionKind.Cleared)
                    cancelledCount++;
                if (decision.ShouldAttempt)
                    attemptCount++;
            }

            if (decision.Kind == PanicShukuchiPendingDecisionKind.Cleared)
            {
                chatGui.PrintError(
                    $"[Seiton Sense] Panic Shukuchi cancelled ({decision.Reason}); nothing was attempted.");
                return;
            }

            if (!decision.ShouldAttempt || decision.Intent is not { } intent) return;

            // ObservePending cleared the state before this sole native boundary.
            // False, ambiguity, or an exception can therefore never retry it.
            var destination = new Vector3(
                intent.Destination.X,
                intent.Destination.Y,
                intent.Destination.Z);
            var accepted = actionManager != null &&
                           actionManager->UseActionLocation(
                               ActionType.Action,
                               PanicShukuchiRules.ActionId,
                               DefaultTargetSentinel,
                               &destination,
                               0,
                               0);
            var after = ClientActionAttemptBoundary.Capture(
                actionManager,
                PanicShukuchiRules.ActionId);
            var outcome = ClientActionAttemptBoundaryRules.Classify(
                accepted,
                PanicShukuchiRules.ActionId,
                before,
                after);

            lock (stateGate)
            {
                var newerCommandIsPending = pendingState.IsPending;
                if (accepted)
                {
                    acceptedCount++;
                }
                else
                {
                    rejectedCount++;
                }

                if (!newerCommandIsPending)
                {
                    lastAdjustedActionId = after.AdjustedActionId;
                    lastSequenceBefore = before.LastUsedActionSequence;
                    lastSequenceAfter = after.LastUsedActionSequence;
                    lastEvent = accepted
                        ? "Client accepted exact forward Shukuchi"
                        : $"One native attempt ended {outcome}; no retry";
                }
            }

            if (accepted)
            {
                chatGui.Print("[Seiton Sense] Panic Shukuchi accepted: 19.5 yalms straight ahead.");
            }
            else
            {
                chatGui.PrintError(
                    $"[Seiton Sense] Panic Shukuchi was not accepted ({outcome}); it was not retried.");
            }
        }
        catch (Exception exception)
        {
            lock (stateGate)
            {
                if (pendingState == previous)
                {
                    pendingState = PanicShukuchiPendingState.Initial;
                    pendingLocalAddress = nint.Zero;
                }
                rejectedCount++;
                if (!pendingState.IsPending)
                    lastEvent = "Sole dispatch failed closed; no retry";
            }

            log.Error(exception, "Seiton Sense Panic Shukuchi dispatch failed closed.");
            chatGui.PrintError("[Seiton Sense] Panic Shukuchi failed closed; it was not retried.");
        }
    }

    private void FailArm(string eventText, string userText)
    {
        RecordCancelled(eventText);
        chatGui.PrintError($"[Seiton Sense] {userText}");
    }

    private void RecordCancelled(string eventText)
    {
        lock (stateGate)
        {
            cancelledCount++;
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

    private static bool IsIncapacitated(IPlayerCharacter local) =>
        local.StatusList.Any(static status => status.StatusId is
            EnemyCombatConstants.PvPStunStatusId or
            EnemyCombatConstants.PvPHeavyStatusId or
            EnemyCombatConstants.PvPBindStatusId or
            EnemyCombatConstants.PvPSilenceStatusId or
            EnemyCombatConstants.DeepFreezeStatusId or
            EnemyCombatConstants.MiracleOfNatureStatusId);

    private bool IsPurifyPriorityClaimed()
    {
        try
        {
            return isPurifyPriorityClaimed();
        }
        catch
        {
            // An uncertain scheduler view must never let the lower-priority
            // manual movement request race Purify.
            return true;
        }
    }
}
