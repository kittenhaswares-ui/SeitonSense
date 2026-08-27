namespace SeitonSense.Core;

/// <summary>
/// Bounded retry bookkeeping for one already-frozen held-action intent. The
/// caller owns the exact key, actor, target, context, range, and episode lease;
/// this state owns only native attempt count and throttle timing.
/// </summary>
public readonly record struct HeldActionRetryState(
    int NativeAttemptCount,
    long NextNativeAttemptAtMilliseconds,
    int NativeAttemptLimit = 0)
{
    public static HeldActionRetryState Initial => new(0, -1);

    public bool IsPending => HeldActionRetryRules.IsPending(this);
}

public enum HeldActionRetryDisposition : byte
{
    None = 0,
    RetryScheduled = 1,
    AcceptedTerminal = 2,
    RejectedTerminal = 3,
    AmbiguousTerminal = 4,
    CancelledTerminal = 5,
    SoftWait = 6,
}

public readonly record struct HeldActionRetryDecision(
    HeldActionRetryState NextState,
    HeldActionRetryDisposition Disposition)
{
    public bool RetryScheduled =>
        Disposition == HeldActionRetryDisposition.RetryScheduled;

    public bool IsTerminal => Disposition is
        HeldActionRetryDisposition.AcceptedTerminal or
        HeldActionRetryDisposition.RejectedTerminal or
        HeldActionRetryDisposition.AmbiguousTerminal or
        HeldActionRetryDisposition.CancelledTerminal;
}

/// <summary>
/// Shared proven-false retry policy for held helpers. Only an explicit false
/// return advances the retry budget. A known soft-unavailable preflight retains
/// the frozen intent without spending an attempt; true, exact-intent drift, and
/// exception/unknown acceptance are terminal. No policy here permits selection
/// or fallback.
/// </summary>
public static class HeldActionRetryRules
{
    public const long NativeRetryThrottleMilliseconds = 50;
    public const int MaximumNativeAttempts = 8;
    public const int MinimumLatencyResponseWindowMilliseconds = 100;
    public const int MaximumLatencyResponseWindowMilliseconds = 1_500;
    public const int DefaultLatencyResponseWindowMilliseconds = 1_000;
    public const int MaximumSupportedNativeAttempts =
        1 + (MaximumLatencyResponseWindowMilliseconds /
             (int)NativeRetryThrottleMilliseconds);
    public const float MaximumNearQueueableAnimationLockSeconds = 0.050f;
    private static int latencyResponseWindowMilliseconds;

    /// <summary>
    /// Configures the one process-wide policy used by the single Seiton Sense
    /// held scheduler. Zero preserves the legacy eight-call budget. The
    /// configured window can only extend that legacy budget, never reduce it.
    /// </summary>
    public static void ConfigureLatencyResponsePolicy(
        bool enabled,
        int retryWindowMilliseconds)
    {
        var value = enabled
            ? Math.Clamp(
                retryWindowMilliseconds,
                MinimumLatencyResponseWindowMilliseconds,
                MaximumLatencyResponseWindowMilliseconds)
            : 0;
        Volatile.Write(ref latencyResponseWindowMilliseconds, value);
    }

    public static int CurrentLatencyResponseWindowMilliseconds =>
        Volatile.Read(ref latencyResponseWindowMilliseconds);

    public static int CurrentMaximumNativeAttempts
    {
        get
        {
            var window = CurrentLatencyResponseWindowMilliseconds;
            if (window <= 0) return MaximumNativeAttempts;
            var windowCalls = 1 +
                              (int)Math.Ceiling(
                                  window / (double)NativeRetryThrottleMilliseconds);
            return Math.Max(MaximumNativeAttempts, windowCalls);
        }
    }

    public static int ResolveAttemptLimit(HeldActionRetryState state)
    {
        if (state.NativeAttemptLimit == 0) return CurrentMaximumNativeAttempts;
        return state.NativeAttemptLimit is >= MaximumNativeAttempts and
            <= MaximumSupportedNativeAttempts
                ? state.NativeAttemptLimit
                : 0;
    }

    public static bool IsValidState(HeldActionRetryState state) =>
        state.NativeAttemptCount >= 0 &&
        state.NextNativeAttemptAtMilliseconds >= -1 &&
        ResolveAttemptLimit(state) > 0;

    public static bool IsRetryBudgetExhausted(HeldActionRetryState state)
    {
        var limit = ResolveAttemptLimit(state);
        return limit > 0 && state.NativeAttemptCount >= limit;
    }

    public static bool IsPending(HeldActionRetryState state)
    {
        var limit = ResolveAttemptLimit(state);
        return state.NativeAttemptCount > 0 &&
               limit > 0 &&
               state.NativeAttemptCount < limit &&
               state.NextNativeAttemptAtMilliseconds >= 0;
    }

    public static bool IsNativeBoundaryNearQueueable(
        float animationLockSeconds,
        bool localPlayerIsCasting,
        uint castActionId,
        bool actionQueued) =>
        float.IsFinite(animationLockSeconds) &&
        animationLockSeconds >= 0f &&
        animationLockSeconds <= MaximumNearQueueableAnimationLockSeconds &&
        !localPlayerIsCasting &&
        castActionId == 0 &&
        !actionQueued;

    public static bool CanAttempt(
        HeldActionRetryState state,
        long nowMilliseconds) =>
        state.IsPending &&
        nowMilliseconds >= 0 &&
        nowMilliseconds >= state.NextNativeAttemptAtMilliseconds;

    public static bool CanAttemptFrozenIntent(
        HeldActionRetryState state,
        long nowMilliseconds) =>
        state == HeldActionRetryState.Initial
            ? nowMilliseconds >= 0
            : CanAttempt(state, nowMilliseconds);

    /// <summary>
    /// A still-valid frozen intent retains the current scheduler frame while it
    /// waits only for the shared native boundary or its proven-false throttle.
    /// Action/resource and actor/range gates are deliberately supplied by the
    /// caller so their unavailability can leave the frame to a usable lower
    /// helper.
    /// </summary>
    public static bool RetainsSchedulerFrame(
        HeldActionRetryState state,
        long nowMilliseconds,
        bool exactIntentValid,
        bool actionSpecificReady,
        bool targetSpecificReady = true) =>
        nowMilliseconds >= 0 &&
        exactIntentValid &&
        actionSpecificReady &&
        targetSpecificReady &&
        (state == HeldActionRetryState.Initial || state.IsPending);

    public static HeldActionRetryDecision Complete(
        HeldActionRetryState previous,
        long nowMilliseconds,
        ClientActionAttemptOutcome outcome)
    {
        if (nowMilliseconds < 0 ||
            !IsValidState(previous) ||
            IsRetryBudgetExhausted(previous))
        {
            return Terminal(HeldActionRetryDisposition.AmbiguousTerminal);
        }

        return outcome switch
        {
            ClientActionAttemptOutcome.ClientAccepted =>
                Terminal(HeldActionRetryDisposition.AcceptedTerminal),
            ClientActionAttemptOutcome.AcceptanceUnknown =>
                Terminal(HeldActionRetryDisposition.AmbiguousTerminal),
            ClientActionAttemptOutcome.ClientRejected =>
                CompleteRejected(previous, nowMilliseconds),
            ClientActionAttemptOutcome.SoftUnavailable =>
                new HeldActionRetryDecision(previous, HeldActionRetryDisposition.SoftWait),
            _ => Terminal(HeldActionRetryDisposition.CancelledTerminal),
        };
    }

    /// <summary>
    /// Only an exhausted explicit-false retry budget or ambiguous acceptance
    /// latches the exact key as a duplicate-safety circuit breaker. Accepted
    /// and ordinary cancelled episodes are spent by their own episode/epoch
    /// token and must not revoke consent for a later distinct episode.
    /// </summary>
    public static bool ShouldLatchHeldKeyUntilRelease(
        HeldActionRetryDisposition disposition) =>
        disposition is HeldActionRetryDisposition.RejectedTerminal or
            HeldActionRetryDisposition.AmbiguousTerminal;

    private static HeldActionRetryDecision CompleteRejected(
        HeldActionRetryState previous,
        long nowMilliseconds)
    {
        var attemptLimit = ResolveAttemptLimit(previous);
        var attemptCount = previous.NativeAttemptCount + 1;
        if (attemptCount >= attemptLimit)
            return Terminal(HeldActionRetryDisposition.RejectedTerminal);

        return new HeldActionRetryDecision(
            new HeldActionRetryState(
                attemptCount,
                SaturatingAdd(nowMilliseconds, NativeRetryThrottleMilliseconds),
                attemptLimit),
            HeldActionRetryDisposition.RetryScheduled);
    }

    private static HeldActionRetryDecision Terminal(
        HeldActionRetryDisposition disposition) =>
        new(HeldActionRetryState.Initial, disposition);

    private static long SaturatingAdd(long left, long right) =>
        left > long.MaxValue - right ? long.MaxValue : left + right;
}
