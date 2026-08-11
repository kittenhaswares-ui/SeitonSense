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
}

public readonly record struct EmergencyPurifyBufferState(
    EmergencyPurifyBufferPhase Phase,
    PurifyCcStatusInstance? StatusInstance,
    long StatusObservedAtMilliseconds,
    long ArmedAtMilliseconds,
    long ExpiresAtMilliseconds,
    long LastObservedAtMilliseconds)
{
    public static EmergencyPurifyBufferState Initial => new(
        EmergencyPurifyBufferPhase.WaitingForStatus,
        null,
        -1,
        -1,
        -1,
        -1);
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
    bool PurifyLocallyReady,
    long NowMilliseconds,
    bool HardReset = false,
    long BufferMilliseconds = EmergencyPurifyBufferRules.DefaultBufferMilliseconds);

public readonly record struct EmergencyPurifyBufferDecision(
    EmergencyPurifyBufferState NextState,
    EmergencyPurifyBufferDecisionKind Kind,
    EmergencyPurifyBufferCancelReason CancelReason)
{
    // The caller must store NextState before attempting the action. Dispatch decisions
    // already carry a spent state, so a failed or rejected attempt cannot be retried.
    public bool ShouldDispatch => Kind == EmergencyPurifyBufferDecisionKind.Dispatch;
}

public static class EmergencyPurifyBufferRules
{
    public const long DefaultBufferMilliseconds = 750;
    public const long MinimumBufferMilliseconds = 100;
    public const long MaximumBufferMilliseconds = 1_000;

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
            if (!observation.FreshKeyPressed)
            {
                return new EmergencyPurifyBufferDecision(
                    waiting,
                    EmergencyPurifyBufferDecisionKind.StatusObserved,
                    EmergencyPurifyBufferCancelReason.None);
            }

            return ArmOrDispatch(waiting, observation);
        }

        if (previous.StatusInstance != status)
        {
            var replacement = WaitingForFreshKey(status.Value, observation.NowMilliseconds);
            if (observation.FreshKeyPressed)
                return ArmOrDispatch(replacement, observation);

            return Cancelled(
                replacement,
                EmergencyPurifyBufferCancelReason.StatusInstanceChanged);
        }

        var current = previous with { LastObservedAtMilliseconds = observation.NowMilliseconds };
        if (current.Phase == EmergencyPurifyBufferPhase.SpentUntilStatusGone)
            return NoDecision(current);

        if (current.Phase == EmergencyPurifyBufferPhase.Buffered)
        {
            if (observation.NowMilliseconds >= current.ExpiresAtMilliseconds)
            {
                return Cancelled(
                    WaitingForFreshKey(status.Value, observation.NowMilliseconds),
                    EmergencyPurifyBufferCancelReason.TimedOut);
            }

            if (observation.PurifyLocallyReady)
            {
                return new EmergencyPurifyBufferDecision(
                    Spend(current, observation.NowMilliseconds),
                    EmergencyPurifyBufferDecisionKind.Dispatch,
                    EmergencyPurifyBufferCancelReason.None);
            }

            return NoDecision(current);
        }

        if (!observation.FreshKeyPressed)
            return NoDecision(current);

        return ArmOrDispatch(current, observation);
    }

    public static long NormalizeBufferMilliseconds(long requestedMilliseconds) =>
        Math.Clamp(requestedMilliseconds, MinimumBufferMilliseconds, MaximumBufferMilliseconds);

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
        EmergencyPurifyBufferObservation observation)
    {
        var bufferMilliseconds = NormalizeBufferMilliseconds(observation.BufferMilliseconds);
        var buffered = current with
        {
            Phase = EmergencyPurifyBufferPhase.Buffered,
            ArmedAtMilliseconds = observation.NowMilliseconds,
            ExpiresAtMilliseconds = SaturatingAdd(observation.NowMilliseconds, bufferMilliseconds),
        };

        return observation.PurifyLocallyReady
            ? new EmergencyPurifyBufferDecision(
                Spend(buffered, observation.NowMilliseconds),
                EmergencyPurifyBufferDecisionKind.Dispatch,
                EmergencyPurifyBufferCancelReason.None)
            : new EmergencyPurifyBufferDecision(
                buffered,
                EmergencyPurifyBufferDecisionKind.Armed,
                EmergencyPurifyBufferCancelReason.None);
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
            nowMilliseconds);

    private static EmergencyPurifyBufferState Spend(
        EmergencyPurifyBufferState state,
        long nowMilliseconds) =>
        state with
        {
            Phase = EmergencyPurifyBufferPhase.SpentUntilStatusGone,
            LastObservedAtMilliseconds = nowMilliseconds,
        };

    private static EmergencyPurifyBufferDecision NoDecision(EmergencyPurifyBufferState state) =>
        new(
            state,
            EmergencyPurifyBufferDecisionKind.None,
            EmergencyPurifyBufferCancelReason.None);

    private static EmergencyPurifyBufferDecision Cancelled(
        EmergencyPurifyBufferState state,
        EmergencyPurifyBufferCancelReason reason) =>
        new(state, EmergencyPurifyBufferDecisionKind.Cancelled, reason);

    private static long SaturatingAdd(long left, long right) =>
        left > long.MaxValue - right ? long.MaxValue : left + right;
}
