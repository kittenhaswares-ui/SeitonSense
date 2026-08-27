using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;
using SeitonSense.Core;
using SeitonSense.Plugin.Models;
using SeitonSense.Plugin.UI;
using GameAction = Lumina.Excel.Sheets.Action;

namespace SeitonSense.Plugin.Services;

/// <summary>
/// Proof supplied by the standard-hotbar input boundary. The buffer deliberately
/// has no keyboard or hotbar hook of its own: callers may enter this service only
/// after certifying one exact, direct (non-macro) standard-hotbar root.
/// </summary>
internal readonly record struct IntegratedActionBufferHotbarRoot(
    bool IsCertifiedDirectStandardHotbarRoot,
    long PressGeneration,
    int HotbarId,
    int SlotId,
    string InputLabel,
    string LogicalInputName,
    bool InputHeld)
{
    internal bool IsValid =>
        IsCertifiedDirectStandardHotbarRoot &&
        PressGeneration > 0 &&
        HotbarId >= 0 &&
        SlotId >= 0;
}

/// <summary>
/// Opaque token spanning exactly one call through NearAssistRedirector's sole
/// native Original boundary. A stale or ineligible token is harmless.
/// </summary>
internal readonly record struct IntegratedActionBufferAttempt(long Epoch, bool Eligible)
{
    internal static IntegratedActionBufferAttempt None => default;
}

internal readonly record struct IntegratedActionBufferActorIdentity(
    ulong GameObjectId,
    uint EntityId,
    nint Address)
{
    internal static IntegratedActionBufferActorIdentity Empty => default;
}

/// <summary>
/// Every native target resolver which can turn a zero/default carrier into a
/// different actor is frozen with the original root. Explicit targets retain
/// both network identity and object address so object-table reuse fails closed.
/// </summary>
internal readonly record struct IntegratedActionBufferTargetSnapshot(
    ulong RawTargetId,
    IntegratedActionBufferActorIdentity ExplicitTarget,
    IntegratedActionBufferActorIdentity HardTarget,
    IntegratedActionBufferActorIdentity SoftTarget,
    IntegratedActionBufferActorIdentity MouseOverTarget,
    IntegratedActionBufferActorIdentity MouseOverNameplateTarget,
    bool IncludesResolverTargets,
    ulong Fingerprint);

/// <summary>
/// Immutable one-shot dispatch request. The receiver must execute this tuple at
/// most once through its already-owned action boundary; it must not retarget or
/// substitute any field.
/// </summary>
internal readonly record struct IntegratedActionBufferDispatchRequest(
    ActionType ActionType,
    uint RequestedActionId,
    uint ResolvedActionId,
    ulong TargetId,
    uint ExtraParam,
    ActionManager.UseActionMode Mode,
    uint ComboRouteId,
    uint TerritoryId,
    ulong InstanceFingerprint,
    ulong LocalGameObjectId,
    uint LocalEntityId,
    IntegratedActionBufferTargetSnapshot TargetSnapshot,
    IntegratedActionBufferHotbarRoot HotbarRoot,
    bool RequiresSmartActionProtectionRecheck);

internal readonly record struct IntegratedActionBufferDiagnostics(
    bool Started,
    bool DispatcherRegistered,
    bool Pending,
    bool Dispatching,
    uint RequestedActionId,
    uint ResolvedActionId,
    int RemainingMilliseconds,
    long ObservedRootCount,
    long ArmedCount,
    long DispatchedCount,
    long AcceptedDispatchCount,
    long RejectedDispatchCount,
    long CancelledCount,
    string LastEvent,
    IntegratedActionBufferCompatibilityDiagnostics Compatibility);

/// <summary>
/// Hook-free runtime around <see cref="SmartActionBufferEngine"/>. It observes
/// only caller-certified direct standard-hotbar roots, proves that the original
/// native call failed solely behind a short local timing gate, and later asks an
/// injected dispatcher to replay the exact immutable tuple once.
/// </summary>
internal sealed unsafe class IntegratedActionBufferRuntime :
    IDisposable,
    IBufferLearningSnapshotSource
{
    private const ulong InvalidObjectId = 0xE0000000;
    private const double AnimationLockEpsilonSeconds = 0.0005;
    private const double MaximumTimingObservationJitterMilliseconds = 50.0;
    private const uint CameraRelativeMovementExceptionActionId = 29494;
    private const uint GenericStunStatusId = 2;
    private const uint PvpStunStatusId = 1343;

    private readonly PluginConfiguration configuration;
    private readonly IFramework framework;
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly ITargetManager targetManager;
    private readonly ICondition condition;
    private readonly IDataManager dataManager;
    private readonly IPluginLog log;
    private readonly SmartActionBufferEngine engine = new();
    private readonly IntegratedActionBufferCompatibilityService compatibility;
    private readonly object gate = new();

    private Func<IntegratedActionBufferDispatchRequest, bool>? dispatcher;
    private Func<bool>? internalPriorityClaimed;
    private InFlightAttempt? inFlight;
    private BufferedRuntimeAction? pendingRuntime;
    private LearningInput? latestLearningInput;
    private long latestRootEpoch;
    private long observedRootCount;
    private long armedCount;
    private long dispatchedCount;
    private long acceptedDispatchCount;
    private long rejectedDispatchCount;
    private long cancelledCount;
    private long nextErrorLogAt;
    private string lastEvent = "Not started";
    private bool started;
    private bool dispatching;
    private bool disposed;

    internal IntegratedActionBufferRuntime(
        PluginConfiguration configuration,
        IDalamudPluginInterface pluginInterface,
        IFramework framework,
        IClientState clientState,
        IObjectTable objectTable,
        ITargetManager targetManager,
        ICondition condition,
        IDataManager dataManager,
        IPluginLog log,
        Func<bool>? internalPriorityClaimed = null,
        Func<IntegratedActionBufferDispatchRequest, bool>? dispatcher = null)
    {
        this.configuration = configuration;
        this.framework = framework;
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.targetManager = targetManager;
        this.condition = condition;
        this.dataManager = dataManager;
        this.log = log;
        compatibility = new IntegratedActionBufferCompatibilityService(pluginInterface);
        this.internalPriorityClaimed = internalPriorityClaimed;
        this.dispatcher = dispatcher;
    }

    internal bool IsDispatching
    {
        get
        {
            lock (gate) return dispatching;
        }
    }

    internal IntegratedActionBufferDiagnostics Diagnostics
    {
        get
        {
            lock (gate)
            {
                var now = Environment.TickCount64;
                return new IntegratedActionBufferDiagnostics(
                    started,
                    dispatcher is not null,
                    pendingRuntime is not null,
                    dispatching,
                    pendingRuntime?.Request.RequestedActionId ?? 0,
                    pendingRuntime?.Request.ResolvedActionId ?? 0,
                    pendingRuntime is { } pending
                        ? RemainingMilliseconds(pending.ExpiresAtMilliseconds, now)
                        : 0,
                    observedRootCount,
                    armedCount,
                    dispatchedCount,
                    acceptedDispatchCount,
                    rejectedDispatchCount,
                    cancelledCount,
                    lastEvent,
                    compatibility.Diagnostics);
            }
        }
    }

    public BufferLearningSnapshot BufferLearningSnapshot
    {
        get
        {
            lock (gate)
            {
                var configured = CurrentBufferWindowMilliseconds;
                var input = pendingRuntime?.Learning ?? latestLearningInput;
                if (input is null)
                    return BufferLearningSnapshot.Empty(configured);

                var now = Environment.TickCount64;
                var pending = pendingRuntime is not null;
                return new BufferLearningSnapshot(
                    HasInput: true,
                    BufferPending: pending,
                    InputHeld: input.HotbarRoot.InputHeld,
                    InputLabel: SafeLabel(input.HotbarRoot.InputLabel, SlotLabel(input.HotbarRoot)),
                    LogicalInputName: SafeLabel(
                        input.HotbarRoot.LogicalInputName,
                        SlotLabel(input.HotbarRoot)),
                    ActionLabel: input.ActionLabel,
                    ActionId: input.ActionId,
                    ConfiguredBufferMilliseconds: configured,
                    RemainingBufferMilliseconds: pendingRuntime is { } runtime
                        ? RemainingMilliseconds(runtime.ExpiresAtMilliseconds, now)
                        : 0,
                    CapturedEarlyMilliseconds: pendingRuntime is { } captured
                        ? (int)Math.Clamp(
                            Math.Ceiling(captured.InitialTemporalRemainderMilliseconds),
                            0,
                            SmartActionBufferWindowRules.MaximumMilliseconds)
                        : 0);
            }
        }
    }

    internal void Start()
    {
        lock (gate)
        {
            if (started || disposed) return;
            started = true;
            lastEvent = "Ready";
        }

        compatibility.Start();
        framework.Update += OnFrameworkUpdate;
    }

    /// <summary>
    /// Registers the only owner allowed to execute a later buffered tuple.
    /// Replacing or clearing the dispatcher is safe; a missing dispatcher keeps
    /// validation and expiry alive but never consumes the one-shot for dispatch.
    /// </summary>
    internal void RegisterDispatcher(
        Func<IntegratedActionBufferDispatchRequest, bool>? dispatch)
    {
        lock (gate)
        {
            dispatcher = dispatch;
            if (dispatch is null)
                lastEvent = "Dispatcher removed; pending action will not dispatch";
        }
    }

    internal void RegisterInternalPriorityProbe(Func<bool>? probe)
    {
        lock (gate) internalPriorityClaimed = probe;
    }

    /// <summary>
    /// Lets the hotbar input owner refresh only the teaching-window held state.
    /// It does not extend, re-arm, or otherwise affect a pending action.
    /// </summary>
    internal void UpdateInputHeld(long pressGeneration, bool inputHeld)
    {
        lock (gate)
        {
            if (latestLearningInput is { } latest &&
                latest.HotbarRoot.PressGeneration == pressGeneration)
            {
                latestLearningInput = latest with
                {
                    HotbarRoot = latest.HotbarRoot with { InputHeld = inputHeld },
                };
            }

            if (pendingRuntime is { } pending &&
                pending.Learning.HotbarRoot.PressGeneration == pressGeneration)
            {
                pendingRuntime = pending with
                {
                    Learning = pending.Learning with
                    {
                        HotbarRoot = pending.Learning.HotbarRoot with
                        {
                            InputHeld = inputHeld,
                        },
                    },
                };
            }
        }
    }

    /// <summary>
    /// Called immediately before NearAssistRedirector's one native Original
    /// invocation. Calling this method is itself the certification that the root
    /// came from a direct standard-hotbar action slot, never a macro/cross-hotbar
    /// tail, mouse click, plugin replay, or action-queue callback. targetId must
    /// be the final post-redirect ID which that exact Original call will receive.
    /// </summary>
    internal IntegratedActionBufferAttempt BeginExactStandardHotbarRoot(
        ActionManager* actionManager,
        ActionType actionType,
        uint requestedActionId,
        ulong targetId,
        uint extraParam,
        ActionManager.UseActionMode mode,
        uint comboRouteId,
        IntegratedActionBufferHotbarRoot hotbarRoot,
        bool requiresSmartActionProtectionRecheck)
    {
        lock (gate)
        {
            if (disposed || dispatching || !hotbarRoot.IsValid)
                return IntegratedActionBufferAttempt.None;

            // Every newly certified physical root owns the future, even if it
            // later proves ineligible or succeeds immediately.
            latestRootEpoch++;
            observedRootCount++;
            CancelPendingLocked(
                SmartActionBufferCancelReason.Replaced,
                "Replaced by a newer physical standard-hotbar root",
                countCancellation: pendingRuntime is not null);
            inFlight = null;

            var resolvedActionId = ResolveActionId(
                actionManager,
                actionType,
                requestedActionId);
            var learning = new LearningInput(
                hotbarRoot,
                resolvedActionId == 0 ? requestedActionId : resolvedActionId,
                DescribeAction(resolvedActionId == 0 ? requestedActionId : resolvedActionId));
            latestLearningInput = learning;

            var token = new IntegratedActionBufferAttempt(latestRootEpoch, Eligible: false);
            if (!started ||
                !configuration.Enabled ||
                !configuration.EnableSmartActionBuffer ||
                actionManager == null ||
                actionType is not (ActionType.Action or ActionType.PvPAction) ||
                requestedActionId == 0 ||
                resolvedActionId == 0 ||
                mode != ActionManager.UseActionMode.None ||
                !TryGetEligibleActionProfile(
                    actionType,
                    resolvedActionId,
                    targetId,
                    out var includeResolverTargets,
                    out var actionLabel))
            {
                lastEvent = "Latest direct root is outside the buffer scope";
                return token;
            }

            var snapshot = CaptureSnapshot(
                targetId,
                resolvedActionId,
                includeResolverTargets);
            if (!IsSafeSnapshot(snapshot))
            {
                lastEvent = "Latest direct root had an unsafe or incomplete context";
                return token;
            }

            var nativeBefore = CaptureNativeState(
                actionManager,
                actionType,
                requestedActionId,
                resolvedActionId,
                targetId);
            if (!nativeBefore.Captured || nativeBefore.ActionQueued)
            {
                lastEvent = "Latest direct root already had native queue ownership";
                return token;
            }

            learning = learning with { ActionLabel = actionLabel };
            latestLearningInput = learning;
            var request = new IntegratedActionBufferDispatchRequest(
                actionType,
                requestedActionId,
                resolvedActionId,
                targetId,
                extraParam,
                mode,
                comboRouteId,
                snapshot.TerritoryId,
                snapshot.InstanceFingerprint,
                snapshot.Local.GameObjectId,
                snapshot.Local.EntityId,
                snapshot.Target,
                hotbarRoot,
                requiresSmartActionProtectionRecheck);
            inFlight = new InFlightAttempt(
                latestRootEpoch,
                Environment.TickCount64,
                request,
                snapshot,
                nativeBefore,
                learning);
            lastEvent = $"Observed direct action {requestedActionId}->{resolvedActionId}";
            return new IntegratedActionBufferAttempt(latestRootEpoch, Eligible: true);
        }
    }

    /// <summary>
    /// Called immediately after the same Original invocation. A true return,
    /// sequence advance, or any native queue is terminal; only an unchanged
    /// false boundary with a proven short local timing remainder can arm.
    /// </summary>
    internal void CompleteExactStandardHotbarRoot(
        ActionManager* actionManager,
        IntegratedActionBufferAttempt attempt,
        bool clientAccepted)
    {
        InFlightAttempt candidate;
        lock (gate)
        {
            if (disposed || !attempt.Eligible ||
                inFlight is not { } current ||
                current.Epoch != attempt.Epoch ||
                attempt.Epoch != latestRootEpoch)
            {
                return;
            }

            candidate = current;
            inFlight = null;
        }

        try
        {
            var nativeAfter = CaptureNativeState(
                actionManager,
                candidate.Request.ActionType,
                candidate.Request.RequestedActionId,
                candidate.Request.ResolvedActionId,
                candidate.Request.TargetId);
            var snapshotAfter = CaptureSnapshot(
                candidate.Request.TargetId,
                nativeAfter.ResolvedActionId,
                candidate.Snapshot.Target.IncludesResolverTargets);

            SmartActionBufferIntent intent;
            double temporalRemainder;
            var window = CurrentBufferWindowMilliseconds;

            lock (gate)
            {
                if (disposed || candidate.Epoch != latestRootEpoch)
                    return;

                if (clientAccepted)
                {
                    lastEvent = "Original action was accepted; nothing buffered";
                    return;
                }

                if (!IsProvenTemporalFalse(candidate, nativeAfter, snapshotAfter,
                        out var failure, out temporalRemainder, out var reason))
                {
                    lastEvent = reason;
                    return;
                }

                var coreAction = new SmartActionBufferAction(
                    candidate.Request.RequestedActionId,
                    candidate.Request.ResolvedActionId,
                    candidate.Snapshot.Target.Fingerprint,
                    candidate.Snapshot.TerritoryId,
                    candidate.Snapshot.InstanceFingerprint);
                intent = new SmartActionBufferIntent(
                    coreAction,
                    failure,
                    IsEligibleForBuffering: true);
            }

            // Foreign configuration reflection and MOAction IPC must never run
            // under the action-buffer lock. Re-enter only after the live check
            // and prove this remains the same physical root and native outcome.
            if (!compatibility.CanMutateAction(
                    candidate.Request.RequestedActionId,
                    candidate.Request.ResolvedActionId,
                    IntegratedActionBufferCompatibilityCheck.Arm,
                    out var compatibilityReason))
            {
                lock (gate)
                {
                    if (!disposed && candidate.Epoch == latestRootEpoch)
                        lastEvent = $"Buffer arm blocked: {compatibilityReason}";
                }

                return;
            }

            var nativeAfterCompatibility = CaptureNativeState(
                actionManager,
                candidate.Request.ActionType,
                candidate.Request.RequestedActionId,
                candidate.Request.ResolvedActionId,
                candidate.Request.TargetId);
            var snapshotAfterCompatibility = CaptureSnapshot(
                candidate.Request.TargetId,
                nativeAfterCompatibility.ResolvedActionId,
                candidate.Snapshot.Target.IncludesResolverTargets);

            lock (gate)
            {
                if (disposed ||
                    candidate.Epoch != latestRootEpoch ||
                    pendingRuntime is not null ||
                    !IsStableArmBoundaryAfterCompatibility(
                        candidate,
                        nativeAfterCompatibility,
                        snapshotAfterCompatibility))
                {
                    lastEvent = "Buffer arm retired after compatibility revalidation";
                    return;
                }

                if (!engine.Arm(intent, candidate.CapturedAtMilliseconds, window))
                {
                    lastEvent = $"Core buffer declined action ({engine.LastCancelReason})";
                    return;
                }

                pendingRuntime = new BufferedRuntimeAction(
                    candidate.Request,
                    candidate.Snapshot,
                    candidate.NativeBefore.LastUsedActionSequence,
                    temporalRemainder,
                    SaturatingAdd(candidate.CapturedAtMilliseconds, window),
                    candidate.Learning);
                armedCount++;
                lastEvent =
                    $"Buffered {candidate.Request.ResolvedActionId} " +
                    $"{temporalRemainder:0} ms before local readiness";
            }
        }
        catch (Exception exception)
        {
            lock (gate) lastEvent = "Original outcome inspection failed closed";
            LogFailure(exception, "Seiton Sense action-buffer outcome inspection failed closed.");
        }
    }

    private bool IsStableArmBoundaryAfterCompatibility(
        InFlightAttempt candidate,
        NativeState native,
        RuntimeSnapshot snapshot) =>
        native.Captured &&
        !native.ActionQueued &&
        native.LastUsedActionSequence == candidate.NativeBefore.LastUsedActionSequence &&
        native.ResolvedActionId == candidate.Request.ResolvedActionId &&
        native.StructuralStatus == 0 &&
        native.ResourceStatus == 0 &&
        native.CastActionId == 0 &&
        Environment.TickCount64 < SaturatingAdd(
            candidate.CapturedAtMilliseconds,
            CurrentBufferWindowMilliseconds) &&
        HasStableIdentity(candidate.Snapshot, snapshot) &&
        IsSafeSnapshot(snapshot) &&
        ExplicitTargetStillExists(candidate.Snapshot.Target);

    internal void Cancel(SmartActionBufferCancelReason reason, string detail)
    {
        if (reason == SmartActionBufferCancelReason.None)
            throw new ArgumentOutOfRangeException(nameof(reason));

        lock (gate)
        {
            latestRootEpoch++;
            inFlight = null;
            CancelPendingLocked(reason, detail, countCancellation: pendingRuntime is not null);
        }
    }

    /// <summary>
    /// Retires only the matching in-flight observation when the authoritative
    /// native boundary throws before returning an acceptance result.
    /// </summary>
    internal void AbandonExactStandardHotbarRoot(
        IntegratedActionBufferAttempt attempt,
        string detail)
    {
        lock (gate)
        {
            if (!attempt.Eligible ||
                inFlight is not { } current ||
                current.Epoch != attempt.Epoch)
            {
                return;
            }

            inFlight = null;
            lastEvent = detail;
        }
    }
    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            if (started) framework.Update -= OnFrameworkUpdate;
            started = false;
            inFlight = null;
            dispatcher = null;
            internalPriorityClaimed = null;
            CancelPendingLocked(
                SmartActionBufferCancelReason.Explicit,
                "Disposed",
                countCancellation: false);
        }

        compatibility.Dispose();
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        Func<IntegratedActionBufferDispatchRequest, bool>? dispatch = null;
        IntegratedActionBufferDispatchRequest request = default;
        BufferedRuntimeAction? dispatchCandidate = null;
        long dispatchRootEpoch = 0;
        try
        {
            compatibility.OnFrameworkBoundary();

            // Cached only: no reflection and no IPC in the per-frame path.
            if (!compatibility.IsCachedMutationAllowed(out var cachedCompatibilityReason))
            {
                lock (gate)
                {
                    if (pendingRuntime is not null)
                    {
                        CancelPendingLocked(
                            SmartActionBufferCancelReason.Conflict,
                            $"Compatibility blocked: {cachedCompatibilityReason}",
                            countCancellation: true);
                    }
                }

                return;
            }

            lock (gate)
            {
                if (disposed || !started || pendingRuntime is not { } runtime)
                    return;

                var now = Environment.TickCount64;
                var actionManager = ActionManager.Instance();
                if (actionManager == null)
                {
                    CancelPendingLocked(
                        SmartActionBufferCancelReason.Explicit,
                        "ActionManager unavailable",
                        countCancellation: true);
                    return;
                }

                var current = CaptureSnapshot(
                    runtime.Request.TargetId,
                    ResolveActionId(
                        actionManager,
                        runtime.Request.ActionType,
                        runtime.Request.RequestedActionId),
                    runtime.Snapshot.Target.IncludesResolverTargets);
                var safety = ToCoreSafety(runtime, current);

                // Safety and the exact deadline continue to run while an internal
                // helper owns priority. The pause applies only to final dispatch.
                var safetyDecision = engine.Evaluate(
                    new SmartActionBufferContext(safety, ActionIsExecutable: false),
                    now);
                if (FinishCoreCancellationLocked(safetyDecision)) return;

                if (!HasStableIdentity(runtime.Snapshot, current) ||
                    !ExplicitTargetStillExists(runtime.Snapshot.Target))
                {
                    CancelPendingLocked(
                        SmartActionBufferCancelReason.TargetChange,
                        "Target, resolver, local player, or instance identity changed",
                        countCancellation: true);
                    return;
                }

                if (!TryGetEligibleActionProfile(
                        runtime.Request.ActionType,
                        runtime.Request.ResolvedActionId,
                        runtime.Request.TargetId,
                        out var includeResolvers,
                        out var ignoredActionLabel) ||
                    includeResolvers != runtime.Snapshot.Target.IncludesResolverTargets)
                {
                    CancelPendingLocked(
                        SmartActionBufferCancelReason.Ineligible,
                        "Action metadata left the instant non-ground non-movement scope",
                        countCancellation: true);
                    return;
                }

                var native = CaptureNativeState(
                    actionManager,
                    runtime.Request.ActionType,
                    runtime.Request.RequestedActionId,
                    runtime.Request.ResolvedActionId,
                    runtime.Request.TargetId);
                if (!native.Captured || native.ResolvedActionId != runtime.Request.ResolvedActionId)
                {
                    CancelPendingLocked(
                        SmartActionBufferCancelReason.ResolvedActionChange,
                        "Adjusted action identity changed",
                        countCancellation: true);
                    return;
                }

                if (native.ActionQueued ||
                    native.LastUsedActionSequence != runtime.SequenceAtCapture)
                {
                    CancelPendingLocked(
                        SmartActionBufferCancelReason.Replaced,
                        "Native queue or accepted action sequence changed",
                        countCancellation: true);
                    return;
                }

                if (native.StructuralStatus != 0 || native.ResourceStatus != 0)
                {
                    CancelPendingLocked(
                        SmartActionBufferCancelReason.NonTransientFailure,
                        $"Action became structurally invalid ({native.StructuralStatus}/{native.ResourceStatus})",
                        countCancellation: true);
                    return;
                }

                var priorityClaimed = IsInternalPriorityClaimed();
                var executable = dispatcher is not null &&
                    native.FullStatus == 0 &&
                    native.IsActionOffCooldown &&
                    native.CastActionId == 0 &&
                    float.IsFinite(native.AnimationLockSeconds) &&
                    native.AnimationLockSeconds >= 0f &&
                    native.AnimationLockSeconds <= AnimationLockEpsilonSeconds;
                if (!executable || priorityClaimed)
                {
                    // Keep the core token unconsumed while the exact action is
                    // not dispatchable or an internal helper owns this frame.
                    var waitingDecision = engine.Evaluate(
                        new SmartActionBufferContext(
                            safety,
                            ActionIsExecutable: false,
                            InternalPriorityClaimed: priorityClaimed),
                        now);
                    FinishCoreCancellationLocked(waitingDecision);
                    return;
                }

                dispatchCandidate = runtime;
                dispatchRootEpoch = latestRootEpoch;
            }

            if (dispatchCandidate is not { } candidate) return;

            // Live ReAction fields and MOAction ownership are read only once,
            // at the actual executable edge, and never under our runtime lock.
            if (!compatibility.CanMutateAction(
                    candidate.Request.RequestedActionId,
                    candidate.Request.ResolvedActionId,
                    IntegratedActionBufferCompatibilityCheck.Dispatch,
                    out var compatibilityReason))
            {
                lock (gate)
                {
                    if (ReferenceEquals(pendingRuntime, candidate) &&
                        dispatchRootEpoch == latestRootEpoch)
                    {
                        CancelPendingLocked(
                            SmartActionBufferCancelReason.Conflict,
                            $"Dispatch compatibility blocked: {compatibilityReason}",
                            countCancellation: true);
                    }
                }

                return;
            }

            var finalActionManager = ActionManager.Instance();
            if (finalActionManager == null)
            {
                lock (gate)
                {
                    if (ReferenceEquals(pendingRuntime, candidate))
                    {
                        CancelPendingLocked(
                            SmartActionBufferCancelReason.Explicit,
                            "ActionManager unavailable after compatibility check",
                            countCancellation: true);
                    }
                }

                return;
            }

            var finalSnapshot = CaptureSnapshot(
                candidate.Request.TargetId,
                ResolveActionId(
                    finalActionManager,
                    candidate.Request.ActionType,
                    candidate.Request.RequestedActionId),
                candidate.Snapshot.Target.IncludesResolverTargets);
            var finalNative = CaptureNativeState(
                finalActionManager,
                candidate.Request.ActionType,
                candidate.Request.RequestedActionId,
                candidate.Request.ResolvedActionId,
                candidate.Request.TargetId);

            lock (gate)
            {
                if (disposed ||
                    !started ||
                    dispatchRootEpoch != latestRootEpoch ||
                    !ReferenceEquals(pendingRuntime, candidate))
                {
                    return;
                }

                var finalSafety = ToCoreSafety(candidate, finalSnapshot);
                if (!HasStableIdentity(candidate.Snapshot, finalSnapshot) ||
                    !ExplicitTargetStillExists(candidate.Snapshot.Target) ||
                    !finalNative.Captured ||
                    finalNative.ResolvedActionId != candidate.Request.ResolvedActionId ||
                    finalNative.ActionQueued ||
                    finalNative.LastUsedActionSequence != candidate.SequenceAtCapture ||
                    finalNative.StructuralStatus != 0 ||
                    finalNative.ResourceStatus != 0 ||
                    finalNative.FullStatus != 0 ||
                    !finalNative.IsActionOffCooldown ||
                    finalNative.CastActionId != 0 ||
                    !float.IsFinite(finalNative.AnimationLockSeconds) ||
                    finalNative.AnimationLockSeconds < 0f ||
                    finalNative.AnimationLockSeconds > AnimationLockEpsilonSeconds)
                {
                    CancelPendingLocked(
                        SmartActionBufferCancelReason.Replaced,
                        "Final action or context drifted during compatibility validation",
                        countCancellation: true);
                    return;
                }

                var finalPriorityClaimed = IsInternalPriorityClaimed();
                var decision = engine.Evaluate(
                    new SmartActionBufferContext(
                        finalSafety,
                        ActionIsExecutable: true,
                        InternalPriorityClaimed: finalPriorityClaimed),
                    Environment.TickCount64);
                if (FinishCoreCancellationLocked(decision)) return;
                if (decision.Kind != SmartActionBufferDecisionKind.Dispatch) return;

                // The core token is consumed before any callback. Every path from
                // here is terminal and therefore cannot dispatch this intent twice.
                pendingRuntime = null;
                dispatching = true;
                dispatch = dispatcher;
                request = candidate.Request;
                dispatchedCount++;
                lastEvent = $"Dispatching exact buffered action {request.ResolvedActionId}";
            }

            if (dispatch is null) return;

            var accepted = false;
            try
            {
                accepted = dispatch(request);
            }
            finally
            {
                lock (gate)
                {
                    dispatching = false;
                    if (accepted)
                    {
                        acceptedDispatchCount++;
                        lastEvent = $"Buffered action {request.ResolvedActionId} accepted";
                    }
                    else
                    {
                        rejectedDispatchCount++;
                        lastEvent =
                            $"Buffered action {request.ResolvedActionId} was not accepted; one-shot ended";
                    }
                }
            }
        }
        catch (Exception exception)
        {
            lock (gate)
            {
                dispatching = false;
                if (pendingRuntime is not null)
                {
                    CancelPendingLocked(
                        SmartActionBufferCancelReason.Explicit,
                        "Framework validation failed closed",
                        countCancellation: true);
                }
                else
                {
                    lastEvent = "Buffered dispatch callback failed; one-shot ended";
                }
            }

            LogFailure(exception, "Seiton Sense integrated action buffer failed closed.");
        }
    }

    private bool IsProvenTemporalFalse(
        InFlightAttempt candidate,
        NativeState after,
        RuntimeSnapshot snapshotAfter,
        out SmartActionBufferFailure failure,
        out double temporalRemainderMilliseconds,
        out string reason)
    {
        failure = SmartActionBufferFailure.Unknown;
        temporalRemainderMilliseconds = 0;
        reason = "Original false result was ambiguous; nothing buffered";

        var before = candidate.NativeBefore;

        if (!after.Captured ||
            before.ActionQueued ||
            after.ActionQueued ||
            before.LastUsedActionSequence != after.LastUsedActionSequence)
        {
            reason = "Original false result changed native queue or action sequence";
            return false;
        }

        if (after.ResolvedActionId != candidate.Request.ResolvedActionId ||
            !HasStableIdentity(candidate.Snapshot, snapshotAfter) ||
            !IsSafeSnapshot(snapshotAfter) ||
            !ExplicitTargetStillExists(candidate.Snapshot.Target))
        {
            reason = "Original false result changed action, target, or local context";
            return false;
        }

        if (before.StructuralStatus != 0 || before.ResourceStatus != 0 ||
            after.StructuralStatus != 0 || after.ResourceStatus != 0)
        {
            reason =
                "Original false result was structurally invalid " +
                $"({before.StructuralStatus}/{before.ResourceStatus} -> " +
                $"{after.StructuralStatus}/{after.ResourceStatus})";
            return false;
        }

        var window = CurrentBufferWindowMilliseconds;
        var beforeRemainderMilliseconds = before.TemporalRemainderMilliseconds;
        var afterRemainderMilliseconds = after.TemporalRemainderMilliseconds;
        temporalRemainderMilliseconds = beforeRemainderMilliseconds;
        if (!double.IsFinite(beforeRemainderMilliseconds) ||
            beforeRemainderMilliseconds <= 0 ||
            beforeRemainderMilliseconds >= window ||
            !double.IsFinite(afterRemainderMilliseconds) ||
            afterRemainderMilliseconds < 0 ||
            Environment.TickCount64 >= SaturatingAdd(candidate.CapturedAtMilliseconds, window))
        {
            reason =
                "Local timing remainder was not inside the same bounded window " +
                $"({beforeRemainderMilliseconds:0} -> {afterRemainderMilliseconds:0} ms; " +
                $"window {window} ms)";
            return false;
        }

        var beforeAnimationLocked =
            before.AnimationLockSeconds > AnimationLockEpsilonSeconds;
        var beforeCooldownBlocked = !before.IsActionOffCooldown;
        if (!beforeAnimationLocked && !beforeCooldownBlocked)
        {
            reason = "Original false result had no proven pre-call local timing blocker";
            return false;
        }

        // The observations are taken around one synchronous native call. A
        // materially later deadline would prove a different cooldown epoch,
        // not the same early press. One scheduler tick accommodates timer
        // quantization without admitting a newly started recast.
        if (afterRemainderMilliseconds >
            beforeRemainderMilliseconds + MaximumTimingObservationJitterMilliseconds)
        {
            reason = "Local timing blocker advanced to a different cooldown epoch";
            return false;
        }

        if (before.CastActionId != 0 || after.CastActionId != 0)
        {
            reason = "A cast began; cast actions are never buffered";
            return false;
        }

        failure = beforeAnimationLocked
            ? SmartActionBufferFailure.AnimationLock
            : SmartActionBufferFailure.Cooldown;
        reason = string.Empty;
        return true;
    }

    private SmartActionBufferSafety ToCoreSafety(
        BufferedRuntimeAction runtime,
        RuntimeSnapshot current) => new(
        Enabled: configuration.Enabled &&
                 configuration.EnableSmartActionBuffer &&
                 !disposed,
        ConflictDetected: false,
        LoggedIn: current.LoggedIn && !current.BetweenAreas,
        IsAlive: current.IsAlive,
        IsMounted: current.IsMounted,
        IsStunned: current.IsStunned,
        IsKnockbackActive: current.IsBeingMoved,
        TerritoryId: current.TerritoryId,
        InstanceId: current.InstanceFingerprint,
        TargetId: current.Target.Fingerprint,
        RequestedActionId: runtime.Request.RequestedActionId,
        ResolvedActionId: current.ResolvedActionId);

    private bool FinishCoreCancellationLocked(SmartActionBufferDecision decision)
    {
        if (decision.Kind is not (
                SmartActionBufferDecisionKind.Cancelled or
                SmartActionBufferDecisionKind.Expired))
        {
            return false;
        }

        pendingRuntime = null;
        cancelledCount++;
        lastEvent = $"Buffer ended: {decision.Reason}";
        return true;
    }

    private void CancelPendingLocked(
        SmartActionBufferCancelReason reason,
        string detail,
        bool countCancellation)
    {
        if (engine.Pending is not null)
            engine.Cancel(reason);
        pendingRuntime = null;
        if (countCancellation) cancelledCount++;
        lastEvent = detail;
    }

    private bool IsInternalPriorityClaimed()
    {
        var probe = internalPriorityClaimed;
        if (probe is null) return false;
        try
        {
            return probe();
        }
        catch (Exception exception)
        {
            // A broken priority probe must never race a survival helper. Pause
            // this frame and keep normal validation/expiry running.
            LogFailure(exception, "Seiton Sense action-buffer priority probe failed; dispatch paused for this frame.");
            return true;
        }
    }

    private NativeState CaptureNativeState(
        ActionManager* actionManager,
        ActionType actionType,
        uint requestedActionId,
        uint expectedResolvedActionId,
        ulong targetId)
    {
        if (actionManager == null || requestedActionId == 0)
            return default;

        var resolvedActionId = ResolveActionId(
            actionManager,
            actionType,
            requestedActionId);
        var inspectedActionId = resolvedActionId == 0
            ? expectedResolvedActionId
            : resolvedActionId;
        if (inspectedActionId == 0) return default;

        var offCooldown = actionManager->IsActionOffCooldown(
            actionType,
            inspectedActionId);
        var animationLock = actionManager->AnimationLock;
        if (!float.IsFinite(animationLock) || animationLock < 0f)
            return default;
        var temporalRemainder = GetTemporalRemainingMilliseconds(
            actionManager,
            actionType,
            inspectedActionId,
            offCooldown,
            animationLock);
        return new NativeState(
            Captured: true,
            actionManager->ActionQueued,
            actionManager->LastUsedActionSequence,
            resolvedActionId,
            animationLock,
            actionManager->CastActionId,
            offCooldown,
            actionManager->CheckActionResources(actionType, inspectedActionId),
            actionManager->GetActionStatus(
                actionType,
                inspectedActionId,
                targetId,
                false,
                false),
            actionManager->GetActionStatus(
                actionType,
                inspectedActionId,
                targetId,
                true,
                true),
            temporalRemainder);
    }

    private double GetTemporalRemainingMilliseconds(
        ActionManager* actionManager,
        ActionType actionType,
        uint resolvedActionId,
        bool isOffCooldown,
        float animationLockSeconds)
    {
        var animationLock = float.IsFinite(animationLockSeconds)
            ? Math.Max(0, animationLockSeconds * 1000.0)
            : double.NaN;
        var cooldown = 0.0;
        if (!isOffCooldown)
        {
            var total = actionManager->GetRecastTime(actionType, resolvedActionId);
            var elapsed = actionManager->GetRecastTimeElapsed(actionType, resolvedActionId);
            var spellId = ActionManager.GetSpellIdForAction(actionType, resolvedActionId);
            var level = (uint)(objectTable.LocalPlayer?.Level ?? 100);
            var maximumCharges = Math.Max(
                1,
                (int)ActionManager.GetMaxCharges(spellId, level));
            cooldown = ActionChargeTiming.GetNextChargeRemainingMilliseconds(
                total,
                elapsed,
                maximumCharges);
        }

        return Math.Max(animationLock, cooldown);
    }

    private RuntimeSnapshot CaptureSnapshot(
        ulong rawTargetId,
        uint resolvedActionId,
        bool includeResolverTargets)
    {
        var localObject = objectTable.LocalPlayer;
        var local = ToActorIdentity(localObject);
        var target = CaptureTargetSnapshot(rawTargetId, includeResolverTargets);
        var instanceFingerprint = FingerprintContext(
            clientState.MapId,
            clientState.Instance,
            localObject?.ClassJob.RowId ?? 0,
            clientState.IsPvP,
            local);
        return new RuntimeSnapshot(
            clientState.IsLoggedIn,
            IsBetweenAreas,
            clientState.TerritoryType,
            clientState.MapId,
            clientState.Instance,
            localObject?.ClassJob.RowId ?? 0,
            clientState.IsPvP,
            instanceFingerprint,
            local,
            target,
            resolvedActionId,
            localObject is { IsDead: false } &&
                localObject.CurrentHp > 0 &&
                localObject.MaxHp >= localObject.CurrentHp,
            condition[ConditionFlag.Mounted],
            HasActiveStatus(localObject, GenericStunStatusId) ||
                HasActiveStatus(localObject, PvpStunStatusId),
            condition[ConditionFlag.BeingMoved]);
    }

    private IntegratedActionBufferTargetSnapshot CaptureTargetSnapshot(
        ulong rawTargetId,
        bool includeResolverTargets)
    {
        var explicitTarget = rawTargetId is 0 or InvalidObjectId
            ? IntegratedActionBufferActorIdentity.Empty
            : FindActorIdentity(rawTargetId);
        var hard = includeResolverTargets
            ? ToActorIdentity(targetManager.Target)
            : IntegratedActionBufferActorIdentity.Empty;
        var soft = includeResolverTargets
            ? ToActorIdentity(targetManager.SoftTarget)
            : IntegratedActionBufferActorIdentity.Empty;
        var mouseOver = includeResolverTargets
            ? ToActorIdentity(targetManager.MouseOverTarget)
            : IntegratedActionBufferActorIdentity.Empty;
        var nameplate = includeResolverTargets
            ? ToActorIdentity(targetManager.MouseOverNameplateTarget)
            : IntegratedActionBufferActorIdentity.Empty;
        return new IntegratedActionBufferTargetSnapshot(
            rawTargetId,
            explicitTarget,
            hard,
            soft,
            mouseOver,
            nameplate,
            includeResolverTargets,
            FingerprintTarget(
                rawTargetId,
                explicitTarget,
                hard,
                soft,
                mouseOver,
                nameplate,
                includeResolverTargets));
    }

    private bool TryGetEligibleActionProfile(
        ActionType actionType,
        uint resolvedActionId,
        ulong targetId,
        out bool includeResolverTargets,
        out string actionLabel)
    {
        includeResolverTargets = false;
        actionLabel = $"Action {resolvedActionId}";
        if (resolvedActionId == 0 ||
            actionType is not (ActionType.Action or ActionType.PvPAction) ||
            ActionManager.GetAdjustedCastTime(actionType, resolvedActionId) > 0)
        {
            return false;
        }

        var actions = dataManager.GetExcelSheet<GameAction>();
        if (actions is null ||
            !actions.TryGetRow(resolvedActionId, out var action) ||
            action.RowId != resolvedActionId ||
            action.Cast100ms != 0 ||
            action.TargetArea ||
            action.AffectsPosition ||
            resolvedActionId == CameraRelativeMovementExceptionActionId)
        {
            return false;
        }

        var name = action.Name.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(name)) actionLabel = name;
        includeResolverTargets = targetId is 0 or InvalidObjectId &&
            (action.CanTargetAlliance ||
             action.CanTargetAlly ||
             action.CanTargetHostile ||
             action.CanTargetOwnPet ||
             action.CanTargetParty ||
             action.CanTargetPartyPet);
        return true;
    }

    private uint ResolveActionId(
        ActionManager* actionManager,
        ActionType actionType,
        uint actionId)
    {
        if (actionManager == null || actionId == 0) return 0;
        if (actionType == ActionType.Action)
            return actionManager->GetAdjustedActionId(actionId);
        if (actionType != ActionType.PvPAction) return 0;

        var pvpActions = dataManager.GetExcelSheet<PvPAction>();
        if (pvpActions is not null &&
            pvpActions.TryGetRow(actionId, out var pvpAction) &&
            pvpAction.Action.IsValid)
        {
            return pvpAction.Action.RowId;
        }

        var actions = dataManager.GetExcelSheet<GameAction>();
        return actions is not null &&
               actions.TryGetRow(actionId, out var action) &&
               action.IsPvP
            ? actionId
            : 0;
    }

    private bool HasStableIdentity(RuntimeSnapshot expected, RuntimeSnapshot current) =>
        expected.TerritoryId == current.TerritoryId &&
        expected.MapId == current.MapId &&
        expected.Instance == current.Instance &&
        expected.JobId == current.JobId &&
        expected.IsPvP == current.IsPvP &&
        expected.InstanceFingerprint == current.InstanceFingerprint &&
        expected.Local == current.Local &&
        expected.Target == current.Target &&
        expected.ResolvedActionId == current.ResolvedActionId;

    private bool IsSafeSnapshot(RuntimeSnapshot snapshot) =>
        configuration.Enabled &&
        configuration.EnableSmartActionBuffer &&
        !disposed &&
        snapshot.LoggedIn &&
        !snapshot.BetweenAreas &&
        snapshot.Local.GameObjectId is not 0 and not InvalidObjectId &&
        snapshot.Local.EntityId is not 0 and not (uint)InvalidObjectId &&
        snapshot.Local.Address != nint.Zero &&
        snapshot.IsAlive &&
        !snapshot.IsMounted &&
        !snapshot.IsStunned &&
        !snapshot.IsBeingMoved &&
        (snapshot.Target.RawTargetId is 0 or InvalidObjectId ||
         snapshot.Target.ExplicitTarget != IntegratedActionBufferActorIdentity.Empty);

    private bool ExplicitTargetStillExists(
        IntegratedActionBufferTargetSnapshot target)
    {
        if (target.RawTargetId is 0 or InvalidObjectId) return true;
        return FindActorIdentity(target.RawTargetId) == target.ExplicitTarget;
    }

    private IntegratedActionBufferActorIdentity FindActorIdentity(ulong actorId)
    {
        if (actorId is 0 or InvalidObjectId) return default;
        foreach (var gameObject in objectTable)
        {
            if (gameObject.GameObjectId == actorId ||
                actorId <= uint.MaxValue && gameObject.EntityId == (uint)actorId)
            {
                return ToActorIdentity(gameObject);
            }
        }

        return default;
    }

    private static IntegratedActionBufferActorIdentity ToActorIdentity(
        IGameObject? gameObject) => gameObject is null
        ? default
        : new IntegratedActionBufferActorIdentity(
            gameObject.GameObjectId,
            gameObject.EntityId,
            gameObject.Address);

    private static bool HasActiveStatus(IGameObject? gameObject, uint statusId)
    {
        if (gameObject is not Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter player)
            return false;
        foreach (var status in player.StatusList)
        {
            if (status.StatusId == statusId &&
                float.IsFinite(status.RemainingTime) &&
                status.RemainingTime > 0f)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsBetweenAreas =>
        condition[ConditionFlag.BetweenAreas] ||
        condition[ConditionFlag.BetweenAreas51];

    private int CurrentBufferWindowMilliseconds =>
        SmartActionBufferWindowRules.Normalize(
            configuration.SmartActionBufferWindowMilliseconds);

    private string DescribeAction(uint actionId)
    {
        if (actionId == 0) return "No action observed";
        var actions = dataManager.GetExcelSheet<GameAction>();
        if (actions is not null &&
            actions.TryGetRow(actionId, out var action) &&
            action.RowId == actionId)
        {
            var name = action.Name.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(name)) return name;
        }

        return $"Action {actionId}";
    }

    private static string SlotLabel(IntegratedActionBufferHotbarRoot root) =>
        $"HOTBAR {root.HotbarId + 1} · SLOT {root.SlotId + 1}";

    private static string SafeLabel(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;

    private static int RemainingMilliseconds(long expiresAt, long now) =>
        (int)Math.Clamp(expiresAt - now, 0, int.MaxValue);

    private static long SaturatingAdd(long left, int right) =>
        left > long.MaxValue - right ? long.MaxValue : left + right;

    private static ulong FingerprintContext(
        uint mapId,
        uint instance,
        uint jobId,
        bool isPvP,
        IntegratedActionBufferActorIdentity local)
    {
        var hash = StartFingerprint();
        AddFingerprint(ref hash, mapId);
        AddFingerprint(ref hash, instance);
        AddFingerprint(ref hash, jobId);
        AddFingerprint(ref hash, isPvP ? 1UL : 0UL);
        AddActorFingerprint(ref hash, local);
        return FinishFingerprint(hash);
    }

    private static ulong FingerprintTarget(
        ulong rawTargetId,
        IntegratedActionBufferActorIdentity explicitTarget,
        IntegratedActionBufferActorIdentity hard,
        IntegratedActionBufferActorIdentity soft,
        IntegratedActionBufferActorIdentity mouseOver,
        IntegratedActionBufferActorIdentity nameplate,
        bool includesResolvers)
    {
        var hash = StartFingerprint();
        AddFingerprint(ref hash, rawTargetId);
        AddFingerprint(ref hash, includesResolvers ? 1UL : 0UL);
        AddActorFingerprint(ref hash, explicitTarget);
        AddActorFingerprint(ref hash, hard);
        AddActorFingerprint(ref hash, soft);
        AddActorFingerprint(ref hash, mouseOver);
        AddActorFingerprint(ref hash, nameplate);
        return FinishFingerprint(hash);
    }

    private static ulong StartFingerprint() => 14695981039346656037UL;

    private static void AddActorFingerprint(
        ref ulong hash,
        IntegratedActionBufferActorIdentity actor)
    {
        AddFingerprint(ref hash, actor.GameObjectId);
        AddFingerprint(ref hash, actor.EntityId);
        AddFingerprint(ref hash, unchecked((ulong)actor.Address));
    }

    private static void AddFingerprint(ref ulong hash, ulong value)
    {
        hash ^= value;
        hash *= 1099511628211UL;
    }

    private static ulong FinishFingerprint(ulong hash) => hash == 0 ? 1UL : hash;

    private void LogFailure(Exception exception, string message)
    {
        var now = Environment.TickCount64;
        if (now < Interlocked.Read(ref nextErrorLogAt)) return;
        Interlocked.Exchange(ref nextErrorLogAt, SaturatingAdd(now, 5_000));
        log.Warning(exception, message);
    }

    private readonly record struct NativeState(
        bool Captured,
        bool ActionQueued,
        ushort LastUsedActionSequence,
        uint ResolvedActionId,
        float AnimationLockSeconds,
        uint CastActionId,
        bool IsActionOffCooldown,
        uint ResourceStatus,
        uint StructuralStatus,
        uint FullStatus,
        double TemporalRemainderMilliseconds);

    private readonly record struct RuntimeSnapshot(
        bool LoggedIn,
        bool BetweenAreas,
        uint TerritoryId,
        uint MapId,
        uint Instance,
        uint JobId,
        bool IsPvP,
        ulong InstanceFingerprint,
        IntegratedActionBufferActorIdentity Local,
        IntegratedActionBufferTargetSnapshot Target,
        uint ResolvedActionId,
        bool IsAlive,
        bool IsMounted,
        bool IsStunned,
        bool IsBeingMoved);

    private sealed record InFlightAttempt(
        long Epoch,
        long CapturedAtMilliseconds,
        IntegratedActionBufferDispatchRequest Request,
        RuntimeSnapshot Snapshot,
        NativeState NativeBefore,
        LearningInput Learning);

    private sealed record BufferedRuntimeAction(
        IntegratedActionBufferDispatchRequest Request,
        RuntimeSnapshot Snapshot,
        ushort SequenceAtCapture,
        double InitialTemporalRemainderMilliseconds,
        long ExpiresAtMilliseconds,
        LearningInput Learning);

    private sealed record LearningInput(
        IntegratedActionBufferHotbarRoot HotbarRoot,
        uint ActionId,
        string ActionLabel);
}
