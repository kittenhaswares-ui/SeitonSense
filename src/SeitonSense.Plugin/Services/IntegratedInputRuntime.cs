using System.Diagnostics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Hooking;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using SeitonSense.Core;
using SeitonSense.Plugin.Models;

namespace SeitonSense.Plugin.Services;

internal readonly record struct IntegratedInputRuntimeDiagnostics(
    bool Available,
    bool Started,
    bool TurboConfigured,
    bool TurboEnabledForCurrentContext,
    bool ContextValid,
    bool InternalPriorityClaimed,
    long PhysicalRoots,
    long InjectedRepeatsDispatched,
    long InjectedRepeatsRejected,
    long SuppressedNewerInput,
    long SuppressedInternalPriority,
    string LastEvent,
    IntegratedHotbarInputSnapshot? HotbarInput,
    IntegratedActionBufferDiagnostics ActionBuffer);

/// <summary>
/// Single native-input boundary for Seiton Sense's generic action buffer and
/// opt-in standard-keyboard-hotbar Turbo. It deliberately does not hook
/// UseAction: NearAssistRedirector remains the sole owner of that boundary.
/// </summary>
internal sealed unsafe class IntegratedInputRuntime : IDisposable
{
    private const uint DirectActionHotbarSlotType = 1;

    [ThreadStatic]
    private static int hotbarExecutionDepth;

    [ThreadStatic]
    private static int syntheticHotbarRepeatExecutionDepth;

    [ThreadStatic]
    private static ActiveBufferRootScope? activeBufferRoot;

    private readonly PluginConfiguration configuration;
    private readonly IFramework framework;
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly ICondition condition;
    private readonly IPluginLog log;
    private readonly NearAssistRedirector nearAssist;
    private readonly CriticalUtilityCoordinationService criticalUtility;
    private readonly object inputStateGate = new();
    private Hook<RaptureHotbarModule.Delegates.ExecuteSlot>? executeSlotHook;
    private Hook<RaptureHotbarModule.Delegates.ExecuteSlotById>? executeSlotByIdHook;
    private IntegratedHotbarInputSource? hotbarInput;
    private IntegratedHotbarPress? latestPhysicalPress;
    private long physicalRoots;
    private long injectedRepeatsDispatched;
    private long injectedRepeatsRejected;
    private long suppressedNewerInput;
    private long suppressedInternalPriority;
    private long nextErrorLogAt;
    private int turboConfigurationState;
    private int available;
    private int started;
    private string lastEvent = "Not initialized";
    private bool disposed;

    internal IntegratedInputRuntime(
        PluginConfiguration configuration,
        IDalamudPluginInterface pluginInterface,
        IGameInteropProvider interop,
        IFramework framework,
        IClientState clientState,
        IObjectTable objectTable,
        ITargetManager targetManager,
        ICondition condition,
        IDataManager dataManager,
        IPluginLog log,
        NearAssistRedirector nearAssist,
        CriticalUtilityCoordinationService criticalUtility)
    {
        this.configuration = configuration;
        this.framework = framework;
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.condition = condition;
        this.log = log;
        this.nearAssist = nearAssist;
        this.criticalUtility = criticalUtility;
        turboConfigurationState = CaptureTurboConfigurationFingerprint();

        ActionBuffer = new IntegratedActionBufferRuntime(
            configuration,
            pluginInterface,
            framework,
            clientState,
            objectTable,
            targetManager,
            condition,
            dataManager,
            log,
            () => IsInternalPriorityClaimedFailClosed(),
            DispatchBufferedAction);

        ArgumentNullException.ThrowIfNull(interop);
        try
        {
            executeSlotHook = interop.HookFromAddress<RaptureHotbarModule.Delegates.ExecuteSlot>(
                RaptureHotbarModule.MemberFunctionPointers.ExecuteSlot,
                ExecuteSlotDetour);
            executeSlotByIdHook =
                interop.HookFromAddress<RaptureHotbarModule.Delegates.ExecuteSlotById>(
                    RaptureHotbarModule.MemberFunctionPointers.ExecuteSlotById,
                    ExecuteSlotByIdDetour);
            hotbarInput = new IntegratedHotbarInputSource(
                interop,
                GetHotbarInputSettings,
                OnCertifiedPhysicalPress,
                OnUnconsumedInjectedRepeat,
                // BeingMoved also becomes true for ordinary player movement.
                // Gating on it made WASD disable its own cast protection.
                // Native knockback/forced movement does not depend on this
                // digital input result and still cancels the cast normally.
                () => nearAssist.IsOwnedSamuraiCastProtected());
            Volatile.Write(ref available, 1);
            SetLastEvent("Ready to start");
        }
        catch (Exception exception)
        {
            DisableNativeInputBoundary();
            SetLastEvent("Native hotbar hooks unavailable; integrated input features are disabled");
            log.Warning(
                exception,
                "Seiton Sense integrated hotbar input is unavailable; the rest of the plugin will continue loading.");
        }
    }

    internal IntegratedActionBufferRuntime ActionBuffer { get; }

    internal bool IsSyntheticHotbarRepeatExecution =>
        syntheticHotbarRepeatExecutionDepth > 0;

    internal bool CanObserveCompleteActionBarActivity =>
        !disposed &&
        Volatile.Read(ref available) != 0 &&
        Volatile.Read(ref started) != 0 &&
        executeSlotHook?.IsEnabled == true &&
        executeSlotByIdHook?.IsEnabled == true &&
        hotbarInput?.IsOperational == true;

    internal IntegratedInputRuntimeDiagnostics Diagnostics
    {
        get
        {
            var policy = CaptureTurboPolicy();
            return new IntegratedInputRuntimeDiagnostics(
                Volatile.Read(ref available) != 0,
                Volatile.Read(ref started) != 0,
                configuration.EnableNativeHotbarTurbo,
                LogicalHotbarRepeatPolicy.IsRepeatEnabled(policy),
                policy.ContextValid,
                policy.InternalPriorityClaimed,
                Interlocked.Read(ref physicalRoots),
                Interlocked.Read(ref injectedRepeatsDispatched),
                Interlocked.Read(ref injectedRepeatsRejected),
                Interlocked.Read(ref suppressedNewerInput),
                Interlocked.Read(ref suppressedInternalPriority),
                Volatile.Read(ref lastEvent),
                hotbarInput?.Snapshot,
                ActionBuffer.Diagnostics);
        }
    }

    /// <summary>
    /// Enables the coordinated boundary. Plugin starts this only after the
    /// personal-status scheduler, so a same-frame critical claim is already
    /// visible before either Turbo or buffered dispatch evaluates.
    /// </summary>
    internal void Start()
    {
        if (disposed || Interlocked.CompareExchange(ref started, 1, 0) != 0) return;

        // Refresh the current physical hold before the buffer evaluates each
        // framework frame. This makes key release authoritative on the same
        // frame and prevents a chase dispatch from observing stale held state.
        framework.Update += OnFrameworkUpdate;
        ActionBuffer.Start();
        var slotHook = executeSlotHook;
        var slotByIdHook = executeSlotByIdHook;
        var input = hotbarInput;
        if (Volatile.Read(ref available) == 0 ||
            slotHook is null ||
            slotByIdHook is null ||
            input is null)
        {
            SetLastEvent("Integrated input remains disabled because its native boundary is unavailable");
            return;
        }

        try
        {
            slotHook.Enable();
            slotByIdHook.Enable();
            input.Start();
            SetLastEvent("Integrated buffer and native Turbo boundary started");
        }
        catch (Exception exception)
        {
            DisableNativeInputBoundary();
            SetLastEvent("Native hotbar hooks failed to start; integrated input features are disabled");
            log.Warning(
                exception,
                "Seiton Sense integrated hotbar input could not start; the rest of the plugin remains active.");
        }
    }

    /// <summary>
    /// Returns provenance only for the synchronous UseAction call emitted by
    /// one newly certified physical press on a direct-action standard slot.
    /// Delegated/native repeats and Seiton-generated Turbo pulses intentionally
    /// never arm or replace the generic one-shot buffer.
    /// </summary>
    internal bool TryGetActiveBufferRoot(
        ActionType actionType,
        uint requestedActionId,
        out IntegratedActionBufferHotbarRoot root)
    {
        root = default;
        var scope = activeBufferRoot;
        if (disposed ||
            scope is null ||
            !ReferenceEquals(scope.Owner, this) ||
            actionType is not (ActionType.Action or ActionType.PvPAction) ||
            requestedActionId == 0 ||
            requestedActionId != scope.CommandId ||
            !scope.TryConsume())
        {
            return false;
        }

        root = scope.Root;
        return root.IsValid;
    }

    public void Dispose()
    {
        if (disposed) return;
        var finalDiagnostics = Diagnostics;
        var finalHotbar = finalDiagnostics.HotbarInput ?? default;
        disposed = true;
        Volatile.Write(ref started, 0);
        Volatile.Write(ref available, 0);
        SetLastEvent("Disposed");
        framework.Update -= OnFrameworkUpdate;

        if (activeBufferRoot is { } root && ReferenceEquals(root.Owner, this))
            activeBufferRoot = null;

        lock (inputStateGate) latestPhysicalPress = null;

        // Stop input production before removing its execution boundary.
        try
        {
            hotbarInput?.Dispose();
        }
        catch (Exception exception)
        {
            LogFailure(exception, "Seiton Sense integrated hotbar input did not dispose cleanly.");
        }
        finally
        {
            hotbarInput = null;
        }

        try
        {
            executeSlotByIdHook?.Dispose();
        }
        catch (Exception exception)
        {
            LogFailure(exception, "Seiton Sense ExecuteSlotById hook did not dispose cleanly.");
        }
        finally
        {
            executeSlotByIdHook = null;
        }

        try
        {
            executeSlotHook?.Dispose();
        }
        catch (Exception exception)
        {
            LogFailure(exception, "Seiton Sense ExecuteSlot hook did not dispose cleanly.");
        }
        finally
        {
            executeSlotHook = null;
        }

        ActionBuffer.Dispose();
        log.Information(
            "Seiton Sense integrated input session: roots={PhysicalRoots}, " +
            "turbo-consumed/rejected={TurboConsumed}/{TurboRejected}, " +
            "native-press/repeat/delegated/fail-open={NativePresses}/{NativeRepeats}/" +
            "{DelegatedRepeats}/{FailedOpen}, " +
            "buffer-observed/armed/dispatched/accepted/rejected/cancelled=" +
            "{Observed}/{Armed}/{Dispatched}/{Accepted}/{Rejected}/{Cancelled}, " +
            "buffer-last={BufferLast}, input-last={InputLast}.",
            finalDiagnostics.PhysicalRoots,
            finalDiagnostics.InjectedRepeatsDispatched,
            finalDiagnostics.InjectedRepeatsRejected,
            finalHotbar.PhysicalPresses,
            finalHotbar.InjectedRepeats,
            finalHotbar.DelegatedRepeats,
            finalHotbar.FailedOpenEvents,
            finalDiagnostics.ActionBuffer.ObservedRootCount,
            finalDiagnostics.ActionBuffer.ArmedCount,
            finalDiagnostics.ActionBuffer.DispatchedCount,
            finalDiagnostics.ActionBuffer.AcceptedDispatchCount,
            finalDiagnostics.ActionBuffer.RejectedDispatchCount,
            finalDiagnostics.ActionBuffer.CancelledCount,
            finalDiagnostics.ActionBuffer.LastEvent,
            finalDiagnostics.LastEvent);
    }

    private byte ExecuteSlotDetour(
        RaptureHotbarModule* thisPtr,
        RaptureHotbarModule.HotbarSlot* slot)
    {
        var hook = executeSlotHook;
        if (hook is null) return 0;

        var previousRoot = activeBufferRoot;
        var ownsRoot = false;
        var nativeTurboPulse = false;
        var syntheticRepeatExecution = false;
        IntegratedHotbarActivation? nativeTurboActivation = null;
        var nativeTurboActionId = 0u;
        try
        {
            IntegratedHotbarActivation? activation = null;
            if (hotbarInput?.TryConsumeActivation(
                    thisPtr,
                    slot,
                    NowMilliseconds,
                    out var observed) == true)
            {
                activation = observed;
            }

            if (ShouldSuppress(activation)) return 0;
            if (ShouldSuppressActiveSprintRepeat(slot)) return 0;
            nativeTurboPulse = activation is
            {
                Kind: IntegratedHotbarActivationKind.InjectedRepeat,
            };
            syntheticRepeatExecution = activation is
            {
                Kind: IntegratedHotbarActivationKind.InjectedRepeat or
                    IntegratedHotbarActivationKind.DelegatedRepeat,
            };
            if (nativeTurboPulse &&
                activation is { } turboActivation &&
                slot != null &&
                (uint)slot->CommandType == DirectActionHotbarSlotType &&
                slot->CommandId != 0)
            {
                nativeTurboActivation = turboActivation;
                nativeTurboActionId = slot->CommandId;
            }
            ownsRoot = TryCreatePhysicalBufferRoot(activation, slot, out var scope);
            if (ownsRoot) activeBufferRoot = scope;
        }
        catch (Exception exception)
        {
            if (ownsRoot) activeBufferRoot = previousRoot;
            ownsRoot = false;
            LogFailure(
                exception,
                "Seiton Sense ExecuteSlot bookkeeping failed open; native input continues.");
        }
        hotbarExecutionDepth++;
        if (syntheticRepeatExecution) syntheticHotbarRepeatExecutionDepth++;
        try
        {
            var result = hook.Original(thisPtr, slot);
            if (nativeTurboPulse)
            {
                Interlocked.Increment(ref injectedRepeatsDispatched);
                SetLastEvent("Native hotbar scanner consumed an exact Turbo input");
                if (nativeTurboActivation is { } turboActivation)
                    ObserveNativeTurboLearningInput(turboActivation, nativeTurboActionId);
            }

            return result;
        }
        finally
        {
            if (syntheticRepeatExecution) syntheticHotbarRepeatExecutionDepth--;
            hotbarExecutionDepth--;
            if (ownsRoot) activeBufferRoot = previousRoot;
        }
    }

    private byte ExecuteSlotByIdDetour(
        RaptureHotbarModule* thisPtr,
        uint hotbarId,
        uint slotId)
    {
        var hook = executeSlotByIdHook;
        if (hook is null) return 0;

        var previousRoot = activeBufferRoot;
        var ownsRoot = false;
        var nativeTurboPulse = false;
        var syntheticRepeatExecution = false;
        IntegratedHotbarActivation? nativeTurboActivation = null;
        var nativeTurboActionId = 0u;
        try
        {
            IntegratedHotbarActivation? activation = null;
            if (hotbarInput?.TryConsumeActivation(
                    hotbarId,
                    slotId,
                    NowMilliseconds,
                    out var observed) == true)
            {
                activation = observed;
            }

            var slot = thisPtr == null ? null : thisPtr->GetSlotById(hotbarId, slotId);
            if (ShouldSuppress(activation)) return 0;
            if (ShouldSuppressActiveSprintRepeat(slot)) return 0;
            nativeTurboPulse = activation is
            {
                Kind: IntegratedHotbarActivationKind.InjectedRepeat,
            };
            syntheticRepeatExecution = activation is
            {
                Kind: IntegratedHotbarActivationKind.InjectedRepeat or
                    IntegratedHotbarActivationKind.DelegatedRepeat,
            };
            if (nativeTurboPulse &&
                activation is { } turboActivation &&
                slot != null &&
                (uint)slot->CommandType == DirectActionHotbarSlotType &&
                slot->CommandId != 0)
            {
                nativeTurboActivation = turboActivation;
                nativeTurboActionId = slot->CommandId;
            }
            ownsRoot = TryCreatePhysicalBufferRoot(activation, slot, out var scope);
            if (ownsRoot) activeBufferRoot = scope;
        }
        catch (Exception exception)
        {
            if (ownsRoot) activeBufferRoot = previousRoot;
            ownsRoot = false;
            LogFailure(
                exception,
                "Seiton Sense ExecuteSlotById bookkeeping failed open; native input continues.");
        }
        hotbarExecutionDepth++;
        if (syntheticRepeatExecution) syntheticHotbarRepeatExecutionDepth++;
        try
        {
            var result = hook.Original(thisPtr, hotbarId, slotId);
            if (nativeTurboPulse)
            {
                Interlocked.Increment(ref injectedRepeatsDispatched);
                SetLastEvent("Native hotbar scanner consumed an exact Turbo input");
                if (nativeTurboActivation is { } turboActivation)
                    ObserveNativeTurboLearningInput(turboActivation, nativeTurboActionId);
            }

            return result;
        }
        finally
        {
            if (syntheticRepeatExecution) syntheticHotbarRepeatExecutionDepth--;
            hotbarExecutionDepth--;
            if (ownsRoot) activeBufferRoot = previousRoot;
        }
    }

    private bool ShouldSuppressActiveSprintRepeat(
        RaptureHotbarModule.HotbarSlot* slot)
    {
        if (slot == null ||
            (uint)slot->CommandType != DirectActionHotbarSlotType ||
            slot->CommandId == 0 ||
            !nearAssist.ShouldBlockActiveSprintRepeatPress(
                ActionType.Action,
                slot->CommandId))
        {
            return false;
        }

        // Stop the exact direct-hotbar repeat before another hook or the game
        // can interpret it as Sprint's toggle-off request. Macro and other
        // request paths remain covered by the shared UseAction boundary.
        SetLastEvent("Blocked an active PvP Sprint repeat before hotbar execution");
        return true;
    }

    private bool ShouldSuppress(IntegratedHotbarActivation? activation)
    {
        if (activation is not { } observed) return false;
        ActionBuffer.UpdateInputHeld(
            observed.Press.PressId,
            hotbarInput?.IsStillHeld(observed.Press) == true);
        if (observed.SuppressedByNewerInput)
        {
            Interlocked.Increment(ref suppressedNewerInput);
            SetLastEvent("Suppressed an attributable older held input");
            return true;
        }

        if (observed.SuppressedByInternalPriority)
        {
            Interlocked.Increment(ref suppressedInternalPriority);
            SetLastEvent("Suppressed an attributable repeat behind critical Seiton utility");
            return true;
        }

        return false;
    }

    private bool TryCreatePhysicalBufferRoot(
        IntegratedHotbarActivation? activation,
        RaptureHotbarModule.HotbarSlot* slot,
        out ActiveBufferRootScope? scope)
    {
        scope = null;
        if (activation is not
            {
                Kind: IntegratedHotbarActivationKind.PhysicalPress,
                SuppressedByNewerInput: false,
                SuppressedByInternalPriority: false,
            } observed ||
            slot == null ||
            (uint)slot->CommandType != DirectActionHotbarSlotType ||
            slot->CommandId == 0)
        {
            return false;
        }

        var root = CreateBufferRoot(
            observed.Press,
            hotbarInput?.IsStillHeld(observed.Press) == true);
        if (!root.IsValid) return false;

        ActionBuffer.ObserveCertifiedDirectHotbarInput(root, slot->CommandId);
        scope = new ActiveBufferRootScope(this, slot->CommandId, root);
        Interlocked.Increment(ref physicalRoots);
        SetLastEvent(
            $"Certified physical hotbar {observed.Binding.HotbarId + 1}, slot {observed.Binding.SlotId + 1}");
        return true;
    }

    private void ObserveNativeTurboLearningInput(
        IntegratedHotbarActivation activation,
        uint actionId)
    {
        if (activation.Kind != IntegratedHotbarActivationKind.InjectedRepeat ||
            actionId == 0)
        {
            return;
        }

        try
        {
            var root = CreateBufferRoot(
                activation.Press,
                hotbarInput?.IsStillHeld(activation.Press) == true);
            if (root.IsValid)
                ActionBuffer.ObserveCertifiedDirectHotbarInput(root, actionId);
        }
        catch (Exception exception)
        {
            LogFailure(
                exception,
                "Seiton Sense Turbo learning-panel observation failed; native input continues.");
        }
    }

    private void OnCertifiedPhysicalPress(IntegratedHotbarPress press)
    {
        // Every physical action-bar press resets Smart Sprint's inactivity
        // clock, even when the game later rejects the requested action. WASD,
        // camera, and targeting input never enter this hotbar boundary.
        nearAssist.RecordActionBarActivity();

        // A new physical edge is newer intent even when its slot is empty,
        // macro, movement, cast-time, or otherwise outside buffer eligibility.
        ActionBuffer.Cancel(
            SmartActionBufferCancelReason.Replaced,
            $"Replaced by physical hotbar input {press.Binding.HotbarId + 1}:{press.Binding.SlotId + 1}");
        ActionBuffer.UpdateInputHeld(press.PressId, inputHeld: true);
        lock (inputStateGate) latestPhysicalPress = press;
        SetLastEvent(
            $"Observed physical hotbar {press.Binding.HotbarId + 1}, slot {press.Binding.SlotId + 1}");
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        IntegratedHotbarPress? latest;
        lock (inputStateGate) latest = latestPhysicalPress;
        if (latest is not { } press) return;

        var held = hotbarInput?.IsStillHeld(press) == true;
        ActionBuffer.UpdateInputHeld(press.PressId, held);
        if (held) return;

        lock (inputStateGate)
        {
            if (latestPhysicalPress is { PressId: var currentPressId } &&
                currentPressId == press.PressId)
            {
                latestPhysicalPress = null;
            }
        }
    }

    private void OnUnconsumedInjectedRepeat(IntegratedHotbarActivation activation)
    {
        if (activation.Kind != IntegratedHotbarActivationKind.InjectedRepeat) return;

        // The cadence was surfaced as a native pressed binding. If the native
        // scanner did not consume that exact same-scan token, its own slot gates
        // declined it. Record the miss, but never bypass those gates afterward.
        Interlocked.Increment(ref injectedRepeatsRejected);
        SetLastEvent("Native hotbar scanner did not consume the exact due Turbo input");
    }

    private IntegratedHotbarInputSettings GetHotbarInputSettings()
    {
        var configuredState = CaptureTurboConfigurationFingerprint();
        var previousState = Interlocked.Exchange(
            ref turboConfigurationState,
            configuredState);
        if (previousState != configuredState)
        {
            // Configuration transitions establish a new physical lifecycle.
            // Transient combat/context/critical-priority pauses intentionally
            // do not reset ownership, so a still-held key resumes naturally.
            hotbarInput?.CancelAndRequireRelease();
            SetLastEvent(
                configuredState != 0
                    ? "Native Turbo configuration changed; existing holds require release"
                    : "Native Turbo disabled; existing holds require release");
        }

        var policy = CaptureTurboPolicy();
        // Track delegated/native repeats only inside Turbo's base domain. Keep
        // that ownership alive through a transient internal-priority pause, but
        // never suppress normal native input while Turbo is disabled or out of
        // scope.
        var externalRepeatOwnerActive =
            policy.FeatureEnabled &&
            policy.ContextValid &&
            (policy.InCombat || policy.AllowOutsideCombat);
        return new IntegratedHotbarInputSettings(
            LogicalHotbarRepeatPolicy.IsRepeatEnabled(policy),
            ExternalRepeatOwnerActive: externalRepeatOwnerActive,
            LogicalHotbarRepeatPolicy.ShouldSuppressAttributedExternalRepeat(policy),
            configuration.TurboInitialDelayMilliseconds,
            configuration.TurboRepeatIntervalMilliseconds);
    }

    private LogicalHotbarRepeatPolicyInput CaptureTurboPolicy()
    {
        var contextValid = IsInputContextValid();
        var priorityClaimed = IsInternalPriorityClaimedFailClosed();
        return new LogicalHotbarRepeatPolicyInput(
            configuration.Enabled && configuration.EnableNativeHotbarTurbo,
            contextValid,
            condition[ConditionFlag.InCombat],
            configuration.TurboOutsideCombat,
            priorityClaimed);
    }

    private int CaptureTurboConfigurationFingerprint() =>
        LogicalHotbarRepeatPolicy.GetConfigurationFingerprint(
            configuration.Enabled && configuration.EnableNativeHotbarTurbo,
            configuration.TurboOutsideCombat);

    private bool IsInputContextValid()
    {
        var local = objectTable.LocalPlayer;
        return !disposed &&
            clientState.IsLoggedIn &&
            !condition[ConditionFlag.BetweenAreas] &&
            !condition[ConditionFlag.BetweenAreas51] &&
            local is { IsDead: false } &&
            local.CurrentHp > 0 &&
            !condition[ConditionFlag.Unconscious] &&
            !condition[ConditionFlag.Mounted];
    }

    private bool IsInternalPriorityClaimedFailClosed()
    {
        try
        {
            return criticalUtility.IsIntegratedInputPriorityClaimed;
        }
        catch (Exception exception)
        {
            LogFailure(
                exception,
                "Seiton Sense critical-input priority probe failed; integrated dispatch pauses this frame.");
            return true;
        }
    }

    private IntegratedActionBufferDispatchResult DispatchBufferedAction(
        IntegratedActionBufferDispatchRequest request)
    {
        if (disposed ||
            Volatile.Read(ref started) == 0 ||
            IsInternalPriorityClaimedFailClosed())
        {
            return IntegratedActionBufferDispatchResult.NotInvoked;
        }

        try
        {
            var actionManager = ActionManager.Instance();
            if (actionManager == null)
                return IntegratedActionBufferDispatchResult.NotInvoked;

            var boundaryBefore = CaptureBufferedActionBoundary(
                actionManager,
                request);

            // The runtime already revalidated the immutable tuple and reserved
            // one native attempt. Run it through the sole UseAction owner with
            // redirect/token rewriting disabled. NearAssist reports whether its
            // exact protection scope actually reached native Original.
            var replayIntent = new IntegratedBufferedReplayIntent(
                request.ActionType,
                request.RequestedActionId,
                request.ResolvedActionId,
                request.TargetId,
                request.RequiresSmartActionProtectionRecheck);
            var replay = nearAssist.RunExactBufferedReplay(
                replayIntent,
                () => actionManager->UseAction(
                    request.ActionType,
                    request.RequestedActionId,
                    request.TargetId,
                    request.ExtraParam,
                    request.Mode,
                    request.ComboRouteId));
            if (!replay.NativeBoundaryInvoked)
                return IntegratedActionBufferDispatchResult.NotInvoked;

            var boundaryAfter = CaptureBufferedActionBoundary(
                actionManager,
                request);
            var outcome = ClientActionAttemptBoundaryRules.Classify(
                replay.ClientReturnedAccepted,
                request.ResolvedActionId,
                boundaryBefore,
                boundaryAfter);
            return new IntegratedActionBufferDispatchResult(
                NativeBoundaryInvoked: true,
                outcome);
        }
        catch (Exception exception)
        {
            LogFailure(exception, "Seiton Sense exact buffered dispatch failed closed.");
            return IntegratedActionBufferDispatchResult.AcceptanceUnknown;
        }
    }

    private static ClientActionAttemptFingerprint CaptureBufferedActionBoundary(
        ActionManager* actionManager,
        IntegratedActionBufferDispatchRequest request)
    {
        if (actionManager == null ||
            request.RequestedActionId == 0 ||
            request.ResolvedActionId == 0)
        {
            return default;
        }

        var adjustedActionId = request.ActionType == ActionType.Action
            ? actionManager->GetAdjustedActionId(request.RequestedActionId)
            : request.ResolvedActionId;
        return new ClientActionAttemptFingerprint(
            Captured: true,
            actionManager->ActionQueued,
            (uint)actionManager->QueuedActionType,
            actionManager->QueuedActionId,
            (ulong)actionManager->QueuedTargetId,
            actionManager->QueuedExtraParam,
            (uint)actionManager->QueueType,
            actionManager->QueuedComboRouteId,
            actionManager->LastUsedActionSequence,
            actionManager->AnimationLock,
            actionManager->CastActionId,
            adjustedActionId,
            actionManager->IsActionOffCooldown(
                request.ActionType,
                request.ResolvedActionId),
            actionManager->CheckActionResources(
                request.ActionType,
                request.ResolvedActionId));
    }

    private IntegratedActionBufferHotbarRoot CreateBufferRoot(
        IntegratedHotbarPress press,
        bool inputHeld) => new(
        IsCertifiedDirectStandardHotbarRoot: true,
        PressGeneration: press.PressId,
        HotbarId: checked((int)press.Binding.HotbarId),
        SlotId: checked((int)press.Binding.SlotId),
        InputLabel: FormatPhysicalInput(press),
        LogicalInputName:
            $"Hotbar {press.Binding.HotbarId + 1}, slot {press.Binding.SlotId + 1}",
        InputHeld: inputHeld);

    private static string FormatPhysicalInput(IntegratedHotbarPress press)
    {
        var modifiers = press.RequiredModifiers.ToString();
        return string.Equals(modifiers, "None", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(modifiers, "0", StringComparison.Ordinal)
            ? press.PhysicalKey.ToString()
            : $"{modifiers}+{press.PhysicalKey}";
    }

    private void DisableNativeInputBoundary()
    {
        Volatile.Write(ref available, 0);

        try
        {
            hotbarInput?.Dispose();
        }
        catch
        {
            // Preserve the original initialization/start failure.
        }
        finally
        {
            hotbarInput = null;
        }

        try
        {
            executeSlotByIdHook?.Dispose();
        }
        catch
        {
            // Preserve the original initialization/start failure.
        }
        finally
        {
            executeSlotByIdHook = null;
        }

        try
        {
            executeSlotHook?.Dispose();
        }
        catch
        {
            // Preserve the original initialization/start failure.
        }
        finally
        {
            executeSlotHook = null;
        }
    }

    private void SetLastEvent(string value) => Volatile.Write(ref lastEvent, value);

    private static long NowMilliseconds =>
        Stopwatch.GetTimestamp() * 1000 / Stopwatch.Frequency;

    private void LogFailure(Exception exception, string message)
    {
        var now = Environment.TickCount64;
        var next = Interlocked.Read(ref nextErrorLogAt);
        if (now < next) return;
        Interlocked.Exchange(
            ref nextErrorLogAt,
            now > long.MaxValue - 5_000 ? long.MaxValue : now + 5_000);
        log.Warning(exception, message);
    }

    private sealed class ActiveBufferRootScope(
        IntegratedInputRuntime owner,
        uint commandId,
        IntegratedActionBufferHotbarRoot root)
    {
        private int consumed;

        internal IntegratedInputRuntime Owner { get; } = owner;

        internal uint CommandId { get; } = commandId;

        internal IntegratedActionBufferHotbarRoot Root { get; } = root;

        internal bool TryConsume() => Interlocked.Exchange(ref consumed, 1) == 0;
    }
}
