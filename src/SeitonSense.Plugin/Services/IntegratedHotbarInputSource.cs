using System.Diagnostics;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.System.Input;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using SeitonSense.Core;

namespace SeitonSense.Plugin.Services;

internal readonly record struct IntegratedHotbarBinding(
    InputId InputId,
    uint HotbarId,
    uint SlotId)
{
    private const int FirstInputId = (int)InputId.HOTBAR_1_1;
    private const int LastInputId = (int)InputId.HOTBAR_10_B;
    private const int SlotsPerHotbar = 12;

    public static int BindingCount => LastInputId - FirstInputId + 1;

    public static bool TryFromInputId(InputId inputId, out IntegratedHotbarBinding binding)
    {
        var raw = (int)inputId;
        if (raw < FirstInputId || raw > LastInputId)
        {
            binding = default;
            return false;
        }

        var offset = raw - FirstInputId;
        binding = new IntegratedHotbarBinding(
            inputId,
            (uint)(offset / SlotsPerHotbar),
            (uint)(offset % SlotsPerHotbar));
        return true;
    }

    public static bool TryFromSlot(uint hotbarId, uint slotId, out IntegratedHotbarBinding binding)
    {
        if (hotbarId >= 10 || slotId >= SlotsPerHotbar)
        {
            binding = default;
            return false;
        }

        var offset = checked((int)((hotbarId * SlotsPerHotbar) + slotId));
        binding = new IntegratedHotbarBinding(
            (InputId)(FirstInputId + offset),
            hotbarId,
            slotId);
        return true;
    }

    public int Index => (int)InputId - FirstInputId;
}

/// <summary>
/// Immutable settings captured once at the beginning of a native standard-
/// hotbar scan. The adapter is intentionally independent of configuration,
/// territory, combat and job policy; its owner supplies that already-resolved
/// policy through the callback.
/// </summary>
internal readonly record struct IntegratedHotbarInputSettings(
    bool RepeatEnabled,
    bool ExternalRepeatOwnerActive,
    bool SuppressAttributedExternalRepeats,
    int InitialDelayMilliseconds,
    int RepeatIntervalMilliseconds)
{
    public static IntegratedHotbarInputSettings Disabled => new(
        RepeatEnabled: false,
        ExternalRepeatOwnerActive: false,
        SuppressAttributedExternalRepeats: false,
        InitialDelayMilliseconds: 0,
        RepeatIntervalMilliseconds: LogicalHotbarRepeatOptions.MinimumRepeatIntervalMilliseconds);

    public IntegratedHotbarInputSettings Normalize()
    {
        var options = ToCoreOptions();
        return this with
        {
            InitialDelayMilliseconds = options.InitialDelayMilliseconds,
            RepeatIntervalMilliseconds = options.RepeatIntervalMilliseconds,
        };
    }

    public LogicalHotbarRepeatOptions ToCoreOptions() => new LogicalHotbarRepeatOptions(
        InitialDelayMilliseconds,
        RepeatIntervalMilliseconds).Normalize();
}

internal enum IntegratedHotbarActivationKind
{
    PhysicalPress = 0,
    InjectedRepeat,
    DelegatedRepeat,
}

internal readonly record struct IntegratedHotbarPress(
    long PressId,
    long LifecycleGeneration,
    IntegratedHotbarBinding Binding,
    SeVirtualKey PhysicalKey,
    KeyModifierFlag RequiredModifiers,
    KeyModifierFlag ActiveModifiers,
    byte KeySettingIndex,
    nint InputDataAddress,
    long ObservedAtMilliseconds);

internal readonly record struct IntegratedHotbarActivation(
    IntegratedHotbarActivationKind Kind,
    IntegratedHotbarPress Press,
    long ObservedAtMilliseconds,
    bool SuppressedByNewerInput = false,
    bool SuppressedByInternalPriority = false)
{
    public IntegratedHotbarBinding Binding => Press.Binding;
}

/// <summary>
/// Read-only diagnostics for the adapter. It exposes no plugin configuration
/// object and is therefore safe for the UI or higher-level runtime to snapshot.
/// </summary>
internal readonly record struct IntegratedHotbarInputSnapshot(
    long Observations,
    long PhysicalPresses,
    long InjectedRepeats,
    long DelegatedRepeats,
    long Releases,
    long HoldsPreempted,
    long SuppressedOlderHolds,
    long FailedOpenEvents,
    long OwnerLogicalInputId,
    uint OwnerHotbarId,
    uint OwnerSlotId,
    long NextRepeatAtMilliseconds,
    IntegratedHotbarInputSettings Settings);

/// <summary>
/// Certifies raw keyboard holds for the ten standard hotbars and arbitrates one
/// logical repeat owner. FFXIV remains authoritative for slot contents, combo
/// transforms, macros, targets and queue semantics.
/// </summary>
internal sealed unsafe class IntegratedHotbarInputSource : IDisposable
{
    private const string CheckHotbarBindingsSignature = "89 54 24 10 53 41 55 41 57";

    [ThreadStatic]
    private static IntegratedHotbarInputSource? activeScanSource;

    [ThreadStatic]
    private static IntegratedHotbarInputSettings activeScanSettings;

    [ThreadStatic]
    private static long activeScanId;

    private readonly Hook<InputData.Delegates.IsInputIdPressed> pressedHook;
    private readonly Hook<CheckHotbarBindingsDelegate> checkHotbarBindingsHook;
    private readonly Func<IntegratedHotbarInputSettings> getSettings;
    private readonly Action<IntegratedHotbarPress> onPhysicalPress;
    private readonly Action<IntegratedHotbarActivation> onUnconsumedInjectedRepeat;
    private readonly object gate = new();
    private readonly PendingActivation?[] pendingActivations =
        new PendingActivation?[IntegratedHotbarBinding.BindingCount];
    private readonly IntegratedHotbarPress?[] currentPresses =
        new IntegratedHotbarPress?[IntegratedHotbarBinding.BindingCount];

    private PhysicalHoldLatch[] holdLatches = CreateHoldLatches();
    private LogicalHotbarRepeatEngine repeatEngine = new();
    private LogicalHotbarRepeatOptions repeatOptions = LogicalHotbarRepeatOptions.Default;
    private IntegratedHotbarInputSettings lastSettings = IntegratedHotbarInputSettings.Disabled;
    private nint currentInputDataAddress;
    private long nextPressId;
    private long nextScanId;
    private long lifecycleGeneration = 1;
    private long observations;
    private long physicalPresses;
    private long injectedRepeats;
    private long delegatedRepeats;
    private long releases;
    private long holdsPreempted;
    private long suppressedOlderHolds;
    private long failedOpenEvents;
    private bool started;
    private bool disposed;

    private delegate void CheckHotbarBindingsDelegate(nint context, byte mode);

    public IntegratedHotbarInputSource(
        IGameInteropProvider interop,
        Func<IntegratedHotbarInputSettings> getSettings,
        Action<IntegratedHotbarPress> onPhysicalPress,
        Action<IntegratedHotbarActivation> onUnconsumedInjectedRepeat)
    {
        ArgumentNullException.ThrowIfNull(interop);
        this.getSettings = getSettings ?? throw new ArgumentNullException(nameof(getSettings));
        this.onPhysicalPress = onPhysicalPress ?? throw new ArgumentNullException(nameof(onPhysicalPress));
        this.onUnconsumedInjectedRepeat = onUnconsumedInjectedRepeat
            ?? throw new ArgumentNullException(nameof(onUnconsumedInjectedRepeat));

        pressedHook = interop.HookFromAddress<InputData.Delegates.IsInputIdPressed>(
            InputData.MemberFunctionPointers.IsInputIdPressed,
            IsInputIdPressedDetour);
        try
        {
            checkHotbarBindingsHook = interop.HookFromSignature<CheckHotbarBindingsDelegate>(
                CheckHotbarBindingsSignature,
                CheckHotbarBindingsDetour);
        }
        catch
        {
            pressedHook.Dispose();
            throw;
        }
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (started) return;

        pressedHook.Enable();
        try
        {
            checkHotbarBindingsHook.Enable();
            started = true;
        }
        catch
        {
            pressedHook.Disable();
            throw;
        }
    }

    public LogicalHotbarRepeatSnapshot RepeatSnapshot
    {
        get
        {
            lock (gate) return repeatEngine.Snapshot;
        }
    }

    public IntegratedHotbarInputSnapshot Snapshot
    {
        get
        {
            lock (gate)
            {
                var repeatSnapshot = repeatEngine.Snapshot;
                var ownerBinding = IntegratedHotbarBinding.TryFromInputId(
                    (InputId)repeatSnapshot.OwnerLogicalInputId,
                    out var binding)
                    ? binding
                    : default;
                return new IntegratedHotbarInputSnapshot(
                    Interlocked.Read(ref observations),
                    Interlocked.Read(ref physicalPresses),
                    Interlocked.Read(ref injectedRepeats),
                    Interlocked.Read(ref delegatedRepeats),
                    Interlocked.Read(ref releases),
                    Interlocked.Read(ref holdsPreempted),
                    Interlocked.Read(ref suppressedOlderHolds),
                    Interlocked.Read(ref failedOpenEvents),
                    repeatSnapshot.OwnerLogicalInputId,
                    ownerBinding.HotbarId,
                    ownerBinding.SlotId,
                    repeatSnapshot.NextRepeatAtMilliseconds,
                    lastSettings);
            }
        }
    }

    public bool TryConsumeActivation(
        RaptureHotbarModule* hotbarModule,
        RaptureHotbarModule.HotbarSlot* slot,
        long nowMilliseconds,
        out IntegratedHotbarActivation activation)
    {
        activation = default;
        if (hotbarModule == null || slot == null) return false;

        lock (gate)
        {
            for (var index = 0; index < pendingActivations.Length; index++)
            {
                var pending = pendingActivations[index];
                if (pending is null) continue;
                if (!IsCurrentScan(pending.Value))
                {
                    pendingActivations[index] = null;
                    continue;
                }

                var candidate = pending.Value.Activation;
                var expected = hotbarModule->GetSlotById(
                    candidate.Binding.HotbarId,
                    candidate.Binding.SlotId);
                if (expected != slot) continue;

                pendingActivations[index] = null;
                if (pending.Value.RequiresOwnerCoalesce)
                {
                    if (!repeatEngine.CoalesceExternalExecution(
                            (long)candidate.Binding.InputId,
                            nowMilliseconds))
                    {
                        activation = candidate with { SuppressedByNewerInput = true };
                        return true;
                    }

                    Interlocked.Increment(ref delegatedRepeats);
                }

                activation = candidate;
                return true;
            }
        }

        return false;
    }

    public bool TryConsumeActivation(
        uint hotbarId,
        uint slotId,
        long nowMilliseconds,
        out IntegratedHotbarActivation activation)
    {
        activation = default;
        if (!IntegratedHotbarBinding.TryFromSlot(hotbarId, slotId, out var binding)) return false;

        lock (gate)
        {
            var pending = pendingActivations[binding.Index];
            if (pending is null || !IsCurrentScan(pending.Value))
            {
                pendingActivations[binding.Index] = null;
                return false;
            }

            pendingActivations[binding.Index] = null;
            var candidate = pending.Value.Activation;
            if (pending.Value.RequiresOwnerCoalesce)
            {
                if (!repeatEngine.CoalesceExternalExecution(
                        (long)candidate.Binding.InputId,
                        nowMilliseconds))
                {
                    activation = candidate with { SuppressedByNewerInput = true };
                    return true;
                }

                Interlocked.Increment(ref delegatedRepeats);
            }

            activation = candidate;
            return true;
        }
    }

    public bool IsStillHeld(IntegratedHotbarPress press)
    {
        if (disposed || press.InputDataAddress == nint.Zero) return false;
        lock (gate)
        {
            if (press.LifecycleGeneration != lifecycleGeneration
                || currentInputDataAddress != press.InputDataAddress)
            {
                return false;
            }
        }

        try
        {
            return IsExactPhysicalControlDown((InputData*)press.InputDataAddress, press);
        }
        catch
        {
            Interlocked.Increment(ref failedOpenEvents);
            return false;
        }
    }

    public void DiscardPending()
    {
        lock (gate) Array.Clear(pendingActivations);
    }

    /// <summary>
    /// Terminates ownership and requires every already-held key to be released
    /// before it can establish a new certified press.
    /// </summary>
    public int CancelAndRequireRelease()
    {
        lock (gate)
        {
            var gatedInputs = repeatEngine.CancelAndRequireRelease();
            Array.Clear(pendingActivations);
            Array.Clear(currentPresses);
            holdLatches = CreateHoldLatches();
            currentInputDataAddress = nint.Zero;
            AdvanceLifecycle();
            return gatedInputs;
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        started = false;

        try
        {
            checkHotbarBindingsHook.Dispose();
        }
        finally
        {
            pressedHook.Dispose();
        }

        lock (gate)
        {
            Array.Clear(pendingActivations);
            Array.Clear(currentPresses);
            currentInputDataAddress = nint.Zero;
        }
    }

    private void CheckHotbarBindingsDetour(nint context, byte mode)
    {
        var previousSource = activeScanSource;
        var previousSettings = activeScanSettings;
        var previousScanId = activeScanId;
        var scanId = NextScanId();
        activeScanSource = this;
        activeScanSettings = ReadSettingsFailOpen();
        activeScanId = scanId;
        IntegratedHotbarActivation? unconsumedInjectedRepeat = null;
        try
        {
            checkHotbarBindingsHook.Original(context, mode);
            lock (gate)
            {
                unconsumedInjectedRepeat = TakeUnconsumedInjectedRepeat(scanId);
            }
        }
        finally
        {
            lock (gate)
            {
                ClearPendingForScan(scanId);
            }

            activeScanSource = previousSource;
            activeScanSettings = previousSettings;
            activeScanId = previousScanId;
        }

        if (unconsumedInjectedRepeat is { } activation)
        {
            try
            {
                // Our cadence returned false to the native scanner. The owner
                // may now execute this exact slot once, and only once, after
                // the scan; there is no synthetic native true to double-fire.
                onUnconsumedInjectedRepeat(activation);
            }
            catch
            {
                Interlocked.Increment(ref failedOpenEvents);
            }
        }
    }

    private bool IsInputIdPressedDetour(InputData* inputData, InputId inputId)
    {
        // Native input is always evaluated first and remains the fallback for
        // every unsupported binding, unavailable raw state or adapter failure.
        var nativePressed = pressedHook.Original(inputData, inputId);
        if (disposed
            || activeScanSource != this
            || inputData == null
            || !IntegratedHotbarBinding.TryFromInputId(inputId, out var binding))
        {
            return nativePressed;
        }

        try
        {
            var physicalState = ReadPhysicalBindingState(inputData, inputId);
            var now = NowMilliseconds;
            var settings = activeScanSettings;
            IntegratedHotbarPress? physicalPress = null;
            bool reportPressed;

            lock (gate)
            {
                currentInputDataAddress = (nint)inputData;
                EnsureEngine(settings);

                var holdDecision = ObservePhysicalHold(
                    binding,
                    inputData,
                    physicalState,
                    nativePressed);
                var held = holdDecision.Kind is
                    PhysicalHoldDecisionKind.Fresh or PhysicalHoldDecisionKind.HeldContinuation;
                var physicalPressed = holdDecision.StartsNewPress;

                var before = repeatEngine.Snapshot;
                var decision = repeatEngine.Observe(new LogicalHotbarRepeatObservation(
                    (long)inputId,
                    nativePressed,
                    held,
                    now,
                    settings.RepeatEnabled,
                    settings.ExternalRepeatOwnerActive,
                    physicalPressed,
                    settings.SuppressAttributedExternalRepeats));
                var after = repeatEngine.Snapshot;

                Interlocked.Increment(ref observations);
                AddPositiveDelta(ref holdsPreempted, before.Counters.HoldsPreempted, after.Counters.HoldsPreempted);
                AddPositiveDelta(ref releases, before.Counters.Releases, after.Counters.Releases);
                AddPositiveDelta(
                    ref suppressedOlderHolds,
                    before.Counters.SuppressedOlderHolds,
                    after.Counters.SuppressedOlderHolds);

                reportPressed = settings.RepeatEnabled || settings.ExternalRepeatOwnerActive
                    ? decision.ShouldReportPressed
                    : nativePressed;

                switch (decision.Kind)
                {
                    case LogicalHotbarRepeatDecisionKind.PhysicalPress:
                        {
                            if (!decision.IsFreshPhysicalEdge || !physicalPressed)
                            {
                                pendingActivations[binding.Index] = null;
                                break;
                            }

                            var certifiedPress = CreatePress(binding, inputData, physicalState, now);
                            currentPresses[binding.Index] = held ? certifiedPress : null;
                            physicalPress = certifiedPress;
                            pendingActivations[binding.Index] = new PendingActivation(
                                new IntegratedHotbarActivation(
                                    IntegratedHotbarActivationKind.PhysicalPress,
                                    certifiedPress,
                                    now),
                                activeScanId,
                                RequiresOwnerCoalesce: false);
                            Interlocked.Increment(ref physicalPresses);
                            break;
                        }

                    case LogicalHotbarRepeatDecisionKind.InjectedRepeat:
                        {
                            if (currentPresses[binding.Index] is not { } injectedPress)
                            {
                                pendingActivations[binding.Index] = null;
                                reportPressed = nativePressed;
                                break;
                            }

                            pendingActivations[binding.Index] = new PendingActivation(
                                new IntegratedHotbarActivation(
                                    IntegratedHotbarActivationKind.InjectedRepeat,
                                    injectedPress,
                                    now),
                                activeScanId,
                                RequiresOwnerCoalesce: false);
                            reportPressed = false;
                            Interlocked.Increment(ref injectedRepeats);
                            break;
                        }

                    case LogicalHotbarRepeatDecisionKind.DelegatedRepeat:
                        {
                            if (currentPresses[binding.Index] is not { } delegatedPress)
                            {
                                pendingActivations[binding.Index] = null;
                                break;
                            }

                            pendingActivations[binding.Index] = new PendingActivation(
                                new IntegratedHotbarActivation(
                                    IntegratedHotbarActivationKind.DelegatedRepeat,
                                    delegatedPress,
                                    now),
                                activeScanId,
                                RequiresOwnerCoalesce: true);
                            break;
                        }

                    case LogicalHotbarRepeatDecisionKind.SuppressedDelegatedRepeat:
                        {
                            if (currentPresses[binding.Index] is not { } delegatedPress)
                            {
                                pendingActivations[binding.Index] = null;
                                reportPressed = nativePressed;
                                break;
                            }

                            pendingActivations[binding.Index] = new PendingActivation(
                                new IntegratedHotbarActivation(
                                    IntegratedHotbarActivationKind.DelegatedRepeat,
                                    delegatedPress,
                                    now,
                                    SuppressedByInternalPriority: true),
                                activeScanId,
                                RequiresOwnerCoalesce: false);
                            reportPressed = false;
                            break;
                        }

                    case LogicalHotbarRepeatDecisionKind.Released:
                        currentPresses[binding.Index] = null;
                        pendingActivations[binding.Index] = null;
                        break;

                    case LogicalHotbarRepeatDecisionKind.SuppressedOlderHold:
                        // An outer repeat hook may still turn false into a pulse.
                        // Retain only same-scan exact-slot provenance so the
                        // execution hook can reject this superseded owner.
                        if (settings.ExternalRepeatOwnerActive && held)
                        {
                            var suppressedPress = currentPresses[binding.Index]
                                ?? CreateUncertifiedHeldPress(binding, inputData, physicalState, now);
                            pendingActivations[binding.Index] = new PendingActivation(
                                new IntegratedHotbarActivation(
                                    IntegratedHotbarActivationKind.DelegatedRepeat,
                                    suppressedPress,
                                    now,
                                    SuppressedByNewerInput: true),
                                activeScanId,
                                RequiresOwnerCoalesce: false);
                        }
                        else
                        {
                            pendingActivations[binding.Index] = null;
                        }
                        break;

                    case LogicalHotbarRepeatDecisionKind.None:
                        // Leave a scan-scoped candidate for a cooperating outer
                        // repeat hook. It is consumable only for the exact owner.
                        if (settings.ExternalRepeatOwnerActive
                            && held
                            && !physicalPressed
                            && repeatEngine.Snapshot.OwnerLogicalInputId == (long)inputId
                            && currentPresses[binding.Index] is { } ownerPress)
                        {
                            var suppress = settings.SuppressAttributedExternalRepeats;
                            pendingActivations[binding.Index] = new PendingActivation(
                                new IntegratedHotbarActivation(
                                    IntegratedHotbarActivationKind.DelegatedRepeat,
                                    ownerPress,
                                    now,
                                    SuppressedByInternalPriority: suppress),
                                activeScanId,
                                RequiresOwnerCoalesce: !suppress);
                        }
                        else
                        {
                            pendingActivations[binding.Index] = null;
                        }
                        break;
                }
            }

            if (physicalPress is { } observed)
            {
                try
                {
                    onPhysicalPress(observed);
                }
                catch
                {
                    lock (gate)
                    {
                        var index = observed.Binding.Index;
                        if (pendingActivations[index] is { } pending
                            && pending.Activation.Kind == IntegratedHotbarActivationKind.PhysicalPress
                            && pending.Activation.Press.PressId == observed.PressId)
                        {
                            pendingActivations[index] = null;
                        }
                    }

                    Interlocked.Increment(ref failedOpenEvents);
                    return nativePressed;
                }
            }

            return reportPressed;
        }
        catch
        {
            Interlocked.Increment(ref failedOpenEvents);
            return nativePressed;
        }
    }

    private IntegratedHotbarInputSettings ReadSettingsFailOpen()
    {
        try
        {
            return getSettings().Normalize();
        }
        catch
        {
            Interlocked.Increment(ref failedOpenEvents);
            return IntegratedHotbarInputSettings.Disabled;
        }
    }

    private void EnsureEngine(IntegratedHotbarInputSettings settings)
    {
        var options = settings.ToCoreOptions();
        lastSettings = settings;
        if (options == repeatOptions) return;

        repeatOptions = options;
        repeatEngine.ReconfigureAndRequireRelease(options);
        Array.Clear(currentPresses);
        Array.Clear(pendingActivations);
        holdLatches = CreateHoldLatches();
        AdvanceLifecycle();
    }

    private PhysicalHoldDecision ObservePhysicalHold(
        IntegratedHotbarBinding binding,
        InputData* inputData,
        PhysicalBindingState physicalState,
        bool nativePressed)
    {
        var latch = holdLatches[binding.Index];
        var latchSnapshot = latch.Snapshot;
        if (latchSnapshot.State == PhysicalHoldLatchState.NeedsRelease &&
            latchSnapshot.Chord.IsValid)
        {
            // A lifecycle/configuration reset deliberately discarded the old
            // press identity. Keep only its raw key-up requirement: the new
            // latch recorded the chord when it first saw the already-held key.
            // Reading the original physical key directly lets a later key-up
            // clear NeedsRelease even though the ordinary binding reader quite
            // correctly returns no active chord on release.
            var rawDown = IsPhysicalKeyDown(
                inputData,
                latchSnapshot.Chord.PhysicalKey);
            return latch.ObserveRequiredRelease(nativePressed, rawDown);
        }

        if (currentPresses[binding.Index] is { } currentPress
            && !IsExactPhysicalControlDown(inputData, currentPress))
        {
            return latch.Observe(new PhysicalHoldObservation(
                CreateRawChord(currentPress),
                LogicalPressed: nativePressed,
                LogicalDown: false,
                RawPressed: false,
                RawDown: false));
        }

        return latch.Observe(new PhysicalHoldObservation(
            CreateRawChord(physicalState),
            LogicalPressed: nativePressed,
            LogicalDown: physicalState.IsDown,
            RawPressed: physicalState.IsPressed,
            RawDown: physicalState.IsDown));
    }

    private static bool IsPhysicalKeyDown(InputData* inputData, int physicalKey)
    {
        if (inputData == null || physicalKey <= 0) return false;
        var keyStates = inputData->KeyboardInputs.KeyState;
        return physicalKey < keyStates.Length &&
            (keyStates[physicalKey] & KeyStateFlags.Down) != 0;
    }

    private IntegratedHotbarPress CreatePress(
        IntegratedHotbarBinding binding,
        InputData* inputData,
        PhysicalBindingState physicalState,
        long nowMilliseconds) =>
        new(
            Interlocked.Increment(ref nextPressId),
            lifecycleGeneration,
            binding,
            physicalState.Key,
            physicalState.RequiredModifiers,
            physicalState.ActiveModifiers,
            physicalState.KeySettingIndex,
            (nint)inputData,
            nowMilliseconds);

    private IntegratedHotbarPress CreateUncertifiedHeldPress(
        IntegratedHotbarBinding binding,
        InputData* inputData,
        PhysicalBindingState physicalState,
        long nowMilliseconds) =>
        CreatePress(binding, inputData, physicalState, nowMilliseconds);

    private static PhysicalBindingState ReadPhysicalBindingState(
        InputData* inputData,
        InputId inputId)
    {
        if (inputData == null) return default;

        var keybind = inputData->GetKeybind(inputId);
        if (keybind == null) return default;

        var activeModifiers = inputData->CurrentKeyModifier;
        var keyStates = inputData->KeyboardInputs.KeyState;
        PhysicalBindingState downFallback = default;
        for (byte index = 0; index < keybind->KeySettings.Length; index++)
        {
            var setting = keybind->KeySettings[index];
            var keyCode = (int)setting.Key;
            if (setting.Key == SeVirtualKey.NO_KEY
                || keyCode < 0
                || keyCode >= keyStates.Length
                || setting.KeyModifier != activeModifiers)
            {
                continue;
            }

            var flags = keyStates[keyCode];
            var isDown = (flags & KeyStateFlags.Down) != 0;
            var isPressed = (flags & KeyStateFlags.Pressed) != 0;
            var state = new PhysicalBindingState(
                setting.Key,
                setting.KeyModifier,
                activeModifiers,
                index,
                isDown,
                isPressed);
            if (isPressed) return state;
            if (isDown) downFallback = state;
        }

        return downFallback;
    }

    private static bool IsExactPhysicalControlDown(
        InputData* inputData,
        IntegratedHotbarPress press)
    {
        if (inputData == null
            || press.PhysicalKey == SeVirtualKey.NO_KEY
            || inputData->CurrentKeyModifier != press.RequiredModifiers)
        {
            return false;
        }

        var keybind = inputData->GetKeybind(press.Binding.InputId);
        if (keybind == null) return false;
        if (press.KeySettingIndex >= keybind->KeySettings.Length) return false;
        var currentSetting = keybind->KeySettings[press.KeySettingIndex];
        if (currentSetting.Key != press.PhysicalKey
            || currentSetting.KeyModifier != press.RequiredModifiers)
        {
            return false;
        }

        var keyStates = inputData->KeyboardInputs.KeyState;
        var keyCode = (int)press.PhysicalKey;
        return keyCode >= 0
            && keyCode < keyStates.Length
            && (keyStates[keyCode] & KeyStateFlags.Down) != 0;
    }

    private bool IsCurrentScan(PendingActivation pending) =>
        activeScanSource == this
        && activeScanId != 0
        && pending.ScanId == activeScanId;

    private IntegratedHotbarActivation? TakeUnconsumedInjectedRepeat(long scanId)
    {
        for (var index = 0; index < pendingActivations.Length; index++)
        {
            if (pendingActivations[index] is not { } pending
                || pending.ScanId != scanId
                || pending.Activation.Kind != IntegratedHotbarActivationKind.InjectedRepeat
                || pending.Activation.SuppressedByNewerInput)
            {
                continue;
            }

            pendingActivations[index] = null;
            return pending.Activation;
        }

        return null;
    }

    private void ClearPendingForScan(long scanId)
    {
        for (var index = 0; index < pendingActivations.Length; index++)
        {
            if (pendingActivations[index] is { ScanId: var pendingScanId }
                && pendingScanId == scanId)
            {
                pendingActivations[index] = null;
            }
        }
    }

    private long NextScanId()
    {
        var scanId = Interlocked.Increment(ref nextScanId);
        if (scanId != 0) return scanId;
        Interlocked.Exchange(ref nextScanId, 1);
        return 1;
    }

    private void AdvanceLifecycle() =>
        lifecycleGeneration = lifecycleGeneration == long.MaxValue
            ? 1
            : lifecycleGeneration + 1;

    private static PhysicalHoldLatch[] CreateHoldLatches()
    {
        var latches = new PhysicalHoldLatch[IntegratedHotbarBinding.BindingCount];
        for (var index = 0; index < latches.Length; index++)
        {
            latches[index] = new PhysicalHoldLatch();
        }

        return latches;
    }

    private static RawPhysicalChord CreateRawChord(PhysicalBindingState state)
    {
        if (state.Key == SeVirtualKey.NO_KEY) return default;
        return new RawPhysicalChord(
            (int)state.Key,
            CreateChordFingerprint(state.Key, state.RequiredModifiers, state.KeySettingIndex));
    }

    private static RawPhysicalChord CreateRawChord(IntegratedHotbarPress press) =>
        new(
            (int)press.PhysicalKey,
            CreateChordFingerprint(
                press.PhysicalKey,
                press.RequiredModifiers,
                press.KeySettingIndex));

    private static ulong CreateChordFingerprint(
        SeVirtualKey key,
        KeyModifierFlag modifiers,
        byte keySettingIndex) =>
        ((ulong)(uint)((int)key + 1))
        | ((ulong)((byte)modifiers + 1) << 16)
        | ((ulong)(keySettingIndex + 1) << 32);

    private static void AddPositiveDelta(ref long target, long before, long after)
    {
        var delta = after - before;
        if (delta > 0) Interlocked.Add(ref target, delta);
    }

    private static long NowMilliseconds =>
        Stopwatch.GetTimestamp() * 1000 / Stopwatch.Frequency;

    private readonly record struct PhysicalBindingState(
        SeVirtualKey Key,
        KeyModifierFlag RequiredModifiers,
        KeyModifierFlag ActiveModifiers,
        byte KeySettingIndex,
        bool IsDown,
        bool IsPressed);

    private readonly record struct PendingActivation(
        IntegratedHotbarActivation Activation,
        long ScanId,
        bool RequiresOwnerCoalesce);
}
