namespace SeitonSense.Core;

public readonly record struct PurifyCcStatusInstance(uint StatusId, ulong InstanceToken)
{
    public bool IsValid => StatusId != 0 && InstanceToken != 0;
}

public enum EmergencyPurifyBufferPhase
{
    WaitingForStatus = 0,
    WaitingForFreshKey = 1,
    Buffered = 2,
    SpentUntilStatusGone = 3,
}

public enum EmergencyPurifyBufferDecisionKind
{
    None = 0,
    StatusObserved = 1,
    Armed = 2,
    Dispatch = 3,
    Cancelled = 4,
}

public enum EmergencyPurifyInputTrigger
{
    None = 0,
    FreshKeyPress = 1,
    HeldKeyAtStatusEntry = 2,
}

public enum EmergencyPurifyBufferCancelReason
{
    None = 0,
    StatusGone = 1,
    StatusInstanceChanged = 2,
    OutsideSupportedPvPContext = 3,
    PlayerDead = 4,
    TextInputActive = 5,
    ConfigurationDisabled = 6,
    TimedOut = 7,
    HardReset = 8,
    InvalidStatusInstance = 9,
    ClockMovedBackwards = 10,
    LocalPlayerIdentityInvalid = 11,
    ResilienceActive = 12,
    ExactKeyReleased = 13,
    NativeRetryLimitReached = 14,
    NativeAcceptanceUnknown = 15,
}

public readonly record struct EmergencyPurifyBufferState(
    EmergencyPurifyBufferPhase Phase,
    PurifyCcStatusInstance? StatusInstance,
    long StatusObservedAtMilliseconds,
    long ArmedAtMilliseconds,
    long ExpiresAtMilliseconds,
    long LastObservedAtMilliseconds,
    int FrozenKeyCode,
    EmergencyPurifyInputTrigger FrozenInputTrigger,
    int NativeAttemptCount,
    long NextNativeAttemptAtMilliseconds,
    ClientActionAttemptOutcome LastNativeOutcome)
{
    public static EmergencyPurifyBufferState Initial => new(
        EmergencyPurifyBufferPhase.WaitingForStatus,
        null,
        -1,
        -1,
        -1,
        -1,
        0,
        EmergencyPurifyInputTrigger.None,
        0,
        -1,
        ClientActionAttemptOutcome.None);
}

public readonly record struct EmergencyPurifyBufferObservation(
    bool ConfigurationEnabled,
    bool IsSupportedPvPContext,
    bool IsAlive,
    bool IsLocalPlayerIdentityValid,
    bool IsResilienceActive,
    bool IsTextInputActive,
    PurifyCcStatusInstance? StatusInstance,
    bool FreshKeyPressed,
    bool HeldKeyEligible,
    bool AllowHeldKeyAtStatusEntry,
    bool PurifyLocallyReady,
    long NowMilliseconds,
    bool HardReset = false,
    long BufferMilliseconds = EmergencyPurifyBufferRules.DefaultBufferMilliseconds,
    int FreshKeyCode = 0,
    int HeldKeyCode = 0,
    bool FrozenKeyStillDown = true);

public readonly record struct EmergencyPurifyBufferDecision(
    EmergencyPurifyBufferState NextState,
    EmergencyPurifyBufferDecisionKind Kind,
    EmergencyPurifyBufferCancelReason CancelReason,
    EmergencyPurifyInputTrigger InputTrigger = EmergencyPurifyInputTrigger.None)
{
    public bool ShouldDispatch => Kind == EmergencyPurifyBufferDecisionKind.Dispatch;

    /// <summary>
    /// Claims only the current shared-helper frame. The physical key generation
    /// deliberately remains eligible while it is still held.
    /// </summary>
    public bool ShouldClaimInputFrame =>
        InputTrigger != EmergencyPurifyInputTrigger.None &&
        Kind is EmergencyPurifyBufferDecisionKind.Armed or EmergencyPurifyBufferDecisionKind.Dispatch;

    // Compatibility name retained for existing callers. This no longer means
    // consuming the physical generation through release.
    public bool ShouldConsumeInputGeneration => ShouldClaimInputFrame;
}

public readonly record struct EmergencyPurifyNativeAttemptDecision(
    EmergencyPurifyBufferState NextState,
    EmergencyPurifyBufferCancelReason CancelReason,
    bool RetryScheduled,
    bool ClientAccepted,
    bool Terminal,
    bool SoftWait = false);

public static class EmergencyPurifyBufferRules
{
    public const long DefaultBufferMilliseconds = 750;
    public const long MinimumBufferMilliseconds = 100;
    public const long MaximumBufferMilliseconds = 1_000;
    // Compatibility constant retained for older diagnostics. Once an exact
    // key and exact CC instance are frozen, the status/key lifecycle itself is
    // the bounded lease; no arbitrary timer may strand the player in CC.
    public const long HeldStatusLeaseMilliseconds = long.MaxValue;
    public const long NativeRetryIntervalMilliseconds =
        HeldActionRetryRules.NativeRetryThrottleMilliseconds;
    public const int MaximumNativeAttempts =
        HeldActionRetryRules.MaximumNativeAttempts;

    public static EmergencyPurifyBufferDecision Observe(
        EmergencyPurifyBufferState previous,
        EmergencyPurifyBufferObservation observation)
    {
        if (observation.HardReset)
        {
            return Cancelled(
                EmergencyPurifyBufferState.Initial,
                EmergencyPurifyBufferCancelReason.HardReset);
        }

        var status = observation.StatusInstance;
        if (status is { IsValid: false })
        {
            return Cancelled(
                EmergencyPurifyBufferState.Initial,
                EmergencyPurifyBufferCancelReason.InvalidStatusInstance);
        }

        if (previous.Phase != EmergencyPurifyBufferPhase.WaitingForStatus &&
            previous.LastObservedAtMilliseconds >= 0 &&
            observation.NowMilliseconds < previous.LastObservedAtMilliseconds)
        {
            return CancelAndWaitIfPresent(
                status ?? previous.StatusInstance,
                observation.NowMilliseconds,
                EmergencyPurifyBufferCancelReason.ClockMovedBackwards);
        }

        var gateFailure = GetGateFailure(observation);
        if (gateFailure != EmergencyPurifyBufferCancelReason.None)
        {
            if (previous.Phase == EmergencyPurifyBufferPhase.SpentUntilStatusGone &&
                status is { IsValid: true } &&
                previous.StatusInstance == status)
            {
                return NoDecision(previous with
                {
                    LastObservedAtMilliseconds = observation.NowMilliseconds,
                });
            }

            return CancelAndWaitIfPresent(status, observation.NowMilliseconds, gateFailure);
        }

        if (status is null)
        {
            return previous.Phase == EmergencyPurifyBufferPhase.WaitingForStatus
                ? NoDecision(EmergencyPurifyBufferState.Initial)
                : Cancelled(
                    EmergencyPurifyBufferState.Initial,
                    EmergencyPurifyBufferCancelReason.StatusGone);
        }

        if (previous.Phase == EmergencyPurifyBufferPhase.WaitingForStatus)
        {
            var waiting = WaitingForFreshKey(status.Value, observation.NowMilliseconds);
            var entryTrigger = ResolveStatusEntryTrigger(observation, out var keyCode);
            if (entryTrigger == EmergencyPurifyInputTrigger.None)
            {
                return new EmergencyPurifyBufferDecision(
                    waiting,
                    EmergencyPurifyBufferDecisionKind.StatusObserved,
                    EmergencyPurifyBufferCancelReason.None);
            }

            return ArmOrDispatch(waiting, observation, entryTrigger, keyCode);
        }

        if (previous.StatusInstance != status)
        {
            var replacement = WaitingForFreshKey(status.Value, observation.NowMilliseconds);
            var replacementTrigger = ResolveStatusEntryTrigger(observation, out var keyCode);
            if (replacementTrigger != EmergencyPurifyInputTrigger.None)
                return ArmOrDispatch(replacement, observation, replacementTrigger, keyCode);

            return Cancelled(
                replacement,
                EmergencyPurifyBufferCancelReason.StatusInstanceChanged);
        }

        var current = previous with { LastObservedAtMilliseconds = observation.NowMilliseconds };
        if (current.Phase == EmergencyPurifyBufferPhase.SpentUntilStatusGone)
            return NoDecision(current);

        if (current.Phase == EmergencyPurifyBufferPhase.Buffered)
        {
            if (current.FrozenKeyCode <= 0 || !observation.FrozenKeyStillDown)
            {
                return Cancelled(
                    WaitingForFreshKey(status.Value, observation.NowMilliseconds),
                    EmergencyPurifyBufferCancelReason.ExactKeyReleased);
            }

            if (current.NativeAttemptCount >= MaximumNativeAttempts)
            {
                return Cancelled(
                    WaitingForFreshKey(status.Value, observation.NowMilliseconds),
                    EmergencyPurifyBufferCancelReason.NativeRetryLimitReached);
            }

            if (!observation.PurifyLocallyReady ||
                observation.NowMilliseconds < current.NextNativeAttemptAtMilliseconds)
            {
                return Armed(current);
            }

            return Dispatch(current);
        }

        if (!observation.FreshKeyPressed || observation.FreshKeyCode <= 0)
            return NoDecision(current);

        return ArmOrDispatch(
            current,
            observation,
            EmergencyPurifyInputTrigger.FreshKeyPress,
            observation.FreshKeyCode);
    }

    public static EmergencyPurifyNativeAttemptDecision ApplyNativeAttemptOutcome(
        EmergencyPurifyBufferState current,
        ClientActionAttemptOutcome outcome,
        long nowMilliseconds)
    {
        if (current.Phase != EmergencyPurifyBufferPhase.Buffered ||
            current.StatusInstance is not { IsValid: true } ||
            current.FrozenKeyCode <= 0 ||
            nowMilliseconds < 0 ||
            (current.LastObservedAtMilliseconds >= 0 &&
             nowMilliseconds < current.LastObservedAtMilliseconds))
        {
            return new EmergencyPurifyNativeAttemptDecision(
                MarkTerminal(
                    current,
                    nowMilliseconds,
                    ClientActionAttemptOutcome.AcceptanceUnknown),
                EmergencyPurifyBufferCancelReason.NativeAcceptanceUnknown,
                false,
                false,
                true);
        }

        if (outcome == ClientActionAttemptOutcome.SoftUnavailable)
        {
            var softWait = current with
            {
                LastObservedAtMilliseconds = nowMilliseconds,
                LastNativeOutcome = outcome,
            };
            return new EmergencyPurifyNativeAttemptDecision(
                softWait,
                EmergencyPurifyBufferCancelReason.None,
                false,
                false,
                false,
                true);
        }

        if (outcome is ClientActionAttemptOutcome.None or
            ClientActionAttemptOutcome.NotInvoked)
        {
            return new EmergencyPurifyNativeAttemptDecision(
                MarkTerminal(
                    current,
                    nowMilliseconds,
                    outcome == ClientActionAttemptOutcome.None
                        ? ClientActionAttemptOutcome.AcceptanceUnknown
                        : outcome),
                EmergencyPurifyBufferCancelReason.NativeAcceptanceUnknown,
                false,
                false,
                true);
        }

        var retry = HeldActionRetryRules.Complete(
            new HeldActionRetryState(
                current.NativeAttemptCount,
                current.NextNativeAttemptAtMilliseconds),
            nowMilliseconds,
            outcome);
        if (retry.Disposition == HeldActionRetryDisposition.AcceptedTerminal)
        {
            return new EmergencyPurifyNativeAttemptDecision(
                MarkTerminal(current, nowMilliseconds, outcome),
                EmergencyPurifyBufferCancelReason.None,
                false,
                true,
                true);
        }

        if (retry.Disposition == HeldActionRetryDisposition.RetryScheduled)
        {
            var pending = current with
            {
                NativeAttemptCount = retry.NextState.NativeAttemptCount,
                NextNativeAttemptAtMilliseconds =
                    retry.NextState.NextNativeAttemptAtMilliseconds,
                LastObservedAtMilliseconds = nowMilliseconds,
                LastNativeOutcome = outcome,
            };
            return new EmergencyPurifyNativeAttemptDecision(
                pending,
                EmergencyPurifyBufferCancelReason.None,
                true,
                false,
                false);
        }

        var cancelReason = retry.Disposition ==
            HeldActionRetryDisposition.RejectedTerminal
            ? EmergencyPurifyBufferCancelReason.NativeRetryLimitReached
            : EmergencyPurifyBufferCancelReason.NativeAcceptanceUnknown;
        return new EmergencyPurifyNativeAttemptDecision(
            MarkTerminal(current, nowMilliseconds, outcome),
            cancelReason,
            false,
            false,
            true);
    }

    public static long NormalizeBufferMilliseconds(long requestedMilliseconds) =>
        Math.Clamp(requestedMilliseconds, MinimumBufferMilliseconds, MaximumBufferMilliseconds);

    /// <summary>
    /// Keeps Purify above every lower held helper while the exact frozen CC and
    /// exact consent key both remain present. This intentionally remains true
    /// after client acceptance, without permitting a second Purify call.
    /// </summary>
    public static bool ClaimsSchedulerPriority(
        EmergencyPurifyBufferState state,
        PurifyCcStatusInstance? observedStatus,
        bool exactStatusCurrentlyObserved,
        bool exactFrozenKeyStillDown) =>
        state.Phase != EmergencyPurifyBufferPhase.WaitingForStatus &&
        state.StatusInstance is { IsValid: true } frozenStatus &&
        observedStatus == frozenStatus &&
        exactStatusCurrentlyObserved &&
        state.FrozenKeyCode > 0 &&
        exactFrozenKeyStillDown;

    private static EmergencyPurifyBufferCancelReason GetGateFailure(
        EmergencyPurifyBufferObservation observation)
    {
        if (!observation.ConfigurationEnabled)
            return EmergencyPurifyBufferCancelReason.ConfigurationDisabled;
        if (!observation.IsSupportedPvPContext)
            return EmergencyPurifyBufferCancelReason.OutsideSupportedPvPContext;
        if (!observation.IsAlive)
            return EmergencyPurifyBufferCancelReason.PlayerDead;
        if (!observation.IsLocalPlayerIdentityValid)
            return EmergencyPurifyBufferCancelReason.LocalPlayerIdentityInvalid;
        if (observation.IsResilienceActive)
            return EmergencyPurifyBufferCancelReason.ResilienceActive;
        if (observation.IsTextInputActive)
            return EmergencyPurifyBufferCancelReason.TextInputActive;

        return EmergencyPurifyBufferCancelReason.None;
    }

    private static EmergencyPurifyBufferDecision CancelAndWaitIfPresent(
        PurifyCcStatusInstance? status,
        long nowMilliseconds,
        EmergencyPurifyBufferCancelReason reason)
    {
        if (status is not { IsValid: true } validStatus)
            return Cancelled(EmergencyPurifyBufferState.Initial, reason);

        return Cancelled(WaitingForFreshKey(validStatus, nowMilliseconds), reason);
    }

    private static EmergencyPurifyBufferDecision ArmOrDispatch(
        EmergencyPurifyBufferState current,
        EmergencyPurifyBufferObservation observation,
        EmergencyPurifyInputTrigger inputTrigger,
        int keyCode)
    {
        if (keyCode <= 0) return NoDecision(current);

        var buffered = current with
        {
            Phase = EmergencyPurifyBufferPhase.Buffered,
            ArmedAtMilliseconds = observation.NowMilliseconds,
            ExpiresAtMilliseconds = long.MaxValue,
            FrozenKeyCode = keyCode,
            FrozenInputTrigger = inputTrigger,
            NativeAttemptCount = 0,
            NextNativeAttemptAtMilliseconds = observation.NowMilliseconds,
            LastNativeOutcome = ClientActionAttemptOutcome.None,
        };

        return observation.PurifyLocallyReady
            ? Dispatch(buffered)
            : Armed(buffered);
    }

    private static EmergencyPurifyInputTrigger ResolveStatusEntryTrigger(
        EmergencyPurifyBufferObservation observation,
        out int keyCode)
    {
        keyCode = 0;
        if (observation.AllowHeldKeyAtStatusEntry &&
            observation.HeldKeyEligible &&
            observation.HeldKeyCode > 0)
        {
            keyCode = observation.HeldKeyCode;
            return EmergencyPurifyInputTrigger.HeldKeyAtStatusEntry;
        }

        if (observation.FreshKeyPressed && observation.FreshKeyCode > 0)
        {
            keyCode = observation.FreshKeyCode;
            return EmergencyPurifyInputTrigger.FreshKeyPress;
        }

        return EmergencyPurifyInputTrigger.None;
    }

    private static EmergencyPurifyBufferState WaitingForFreshKey(
        PurifyCcStatusInstance status,
        long nowMilliseconds) =>
        new(
            EmergencyPurifyBufferPhase.WaitingForFreshKey,
            status,
            nowMilliseconds,
            -1,
            -1,
            nowMilliseconds,
            0,
            EmergencyPurifyInputTrigger.None,
            0,
            -1,
            ClientActionAttemptOutcome.None);

    private static EmergencyPurifyBufferDecision Armed(EmergencyPurifyBufferState state) =>
        new(
            state,
            EmergencyPurifyBufferDecisionKind.Armed,
            EmergencyPurifyBufferCancelReason.None,
            state.FrozenInputTrigger);

    private static EmergencyPurifyBufferDecision Dispatch(EmergencyPurifyBufferState state) =>
        new(
            state,
            EmergencyPurifyBufferDecisionKind.Dispatch,
            EmergencyPurifyBufferCancelReason.None,
            state.FrozenInputTrigger);

    private static EmergencyPurifyBufferDecision NoDecision(EmergencyPurifyBufferState state) =>
        new(
            state,
            EmergencyPurifyBufferDecisionKind.None,
            EmergencyPurifyBufferCancelReason.None);

    private static EmergencyPurifyBufferDecision Cancelled(
        EmergencyPurifyBufferState state,
        EmergencyPurifyBufferCancelReason reason) =>
        new(state, EmergencyPurifyBufferDecisionKind.Cancelled, reason);

    private static EmergencyPurifyBufferState MarkTerminal(
        EmergencyPurifyBufferState current,
        long nowMilliseconds,
        ClientActionAttemptOutcome outcome)
    {
        if (current.StatusInstance is not { IsValid: true })
            return EmergencyPurifyBufferState.Initial;

        return current with
        {
            Phase = EmergencyPurifyBufferPhase.SpentUntilStatusGone,
            NativeAttemptCount = outcome is
                ClientActionAttemptOutcome.ClientAccepted or
                ClientActionAttemptOutcome.ClientRejected or
                ClientActionAttemptOutcome.AcceptanceUnknown
                    ? SaturatingIncrement(current.NativeAttemptCount)
                    : current.NativeAttemptCount,
            NextNativeAttemptAtMilliseconds = -1,
            LastObservedAtMilliseconds = Math.Max(0, nowMilliseconds),
            LastNativeOutcome = outcome,
        };
    }

    private static int SaturatingIncrement(int value) =>
        value == int.MaxValue ? int.MaxValue : value + 1;

}
