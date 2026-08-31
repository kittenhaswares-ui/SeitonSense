namespace SeitonSense.Core;

/// <summary>
/// Immutable identity of one exact action which originated at one certified
/// tap root. The tap reservation never substitutes any action, target, context,
/// or root generation.
/// </summary>
public readonly record struct HeldChaseBufferIntent(
    uint RequestedActionId,
    uint ResolvedActionId,
    ulong TargetFingerprint,
    uint TerritoryId,
    ulong InstanceFingerprint,
    long PressGeneration)
{
    public bool IsValid =>
        RequestedActionId != 0 &&
        ResolvedActionId != 0 &&
        TargetFingerprint != 0 &&
        TerritoryId != 0 &&
        InstanceFingerprint != 0 &&
        PressGeneration > 0;
}

/// <summary>
/// Bounds for the release-independent tap-to-land reservation. This is a
/// separate window from the ordinary timing buffer because it deliberately
/// survives key release while the player closes a spatial gap.
/// </summary>
public static class HeldChaseBufferWindowRules
{
    public const int DefaultMilliseconds = 2_200;
    public const int MinimumMilliseconds = 0;
    public const int MaximumMilliseconds = 3_000;
    public const long NativeRetryThrottleMilliseconds =
        HeldActionRetryRules.NativeRetryThrottleMilliseconds;
    public const int MaximumNativeAttempts =
        1 + (MaximumMilliseconds / (int)NativeRetryThrottleMilliseconds);

    public static int Normalize(int configuredMilliseconds) =>
        Math.Clamp(
            configuredMilliseconds,
            MinimumMilliseconds,
            MaximumMilliseconds);

    public static int ResolveNativeAttemptLimit(int configuredMilliseconds)
    {
        var window = Normalize(configuredMilliseconds);
        if (window == 0) return 0;
        return 1 +
               (int)Math.Ceiling(
                   window / (double)NativeRetryThrottleMilliseconds);
    }
}

/// <summary>
/// FFXIV can surface one authored macro action either as an explicit macro
/// carrier or as a normal direct carrier. Queue mode is never an authored tap.
/// The runtime still has to prove the exact Smart Action lease, generation,
/// action, and visible target independently before using this admission rule.
/// </summary>
public static class SmartActionFallbackInvocationRules
{
    public static bool IsSupportedCarrier(
        bool explicitMacroCarrier,
        bool directCarrier,
        bool queueCarrier) =>
        !queueCarrier && (explicitMacroCarrier || directCarrier);
}

public readonly record struct HeldChaseBufferRetryState(
    int NativeAttemptCount,
    long NextNativeAttemptAtMilliseconds,
    int NativeAttemptLimit)
{
    public static HeldChaseBufferRetryState None => new(0, -1, 0);

    public bool IsValid =>
        NativeAttemptCount >= 0 &&
        NativeAttemptCount <= NativeAttemptLimit &&
        NextNativeAttemptAtMilliseconds >= -1 &&
        NativeAttemptLimit >= 1 &&
        NativeAttemptLimit <= HeldChaseBufferWindowRules.MaximumNativeAttempts;

    public bool IsPending =>
        IsValid &&
        NativeAttemptCount > 0 &&
        NativeAttemptCount < NativeAttemptLimit &&
        NextNativeAttemptAtMilliseconds >= 0;
}

public readonly record struct HeldChaseBufferAttemptDecision(
    HeldChaseBufferRetryState NextState,
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
/// Facts captured around the original action call. Arming is allowed only when
/// exactly one reviewed root owns the action and native range/line of sight is
/// the sole unavailable boundary.
/// </summary>
public readonly record struct HeldChaseBufferArmInput(
    HeldChaseBufferIntent Intent,
    bool Enabled,
    bool IsCertifiedPhysicalStandardHotbarRoot,
    bool ActionEligible,
    bool SafetyValid,
    bool RangeProbeAvailable,
    bool HasRangeAndLineOfSight,
    bool OtherNativeGatesReady,
    int ReservationWindowMilliseconds =
        HeldChaseBufferWindowRules.DefaultMilliseconds,
    bool IsCertifiedSmartActionMacroFallback = false);

/// <summary>
/// Current facts for the already-frozen intent. The runtime supplies native
/// range/LoS and all non-spatial gates independently so spatial waiting cannot
/// hide a later action, target, context, input, or safety change. Key-up is
/// intentionally absent: releasing the original key does not revoke the tap.
/// </summary>
public readonly record struct HeldChaseBufferLiveInput(
    bool Enabled,
    long PressGeneration,
    uint RequestedActionId,
    uint ResolvedActionId,
    ulong TargetFingerprint,
    uint TerritoryId,
    ulong InstanceFingerprint,
    bool ActionEligible,
    bool SafetyValid,
    bool RangeProbeAvailable,
    bool HasRangeAndLineOfSight,
    bool OtherNativeGatesReady,
    bool WithinDeadline = true,
    long NowMilliseconds = 0);

public enum HeldChaseBufferCancelReason : byte
{
    None = 0,
    InvalidIntent = 1,
    Disabled = 2,
    NotPhysicalStandardHotbar = 3,
    Released = 4,
    Replaced = 5,
    ActionChanged = 6,
    TargetChanged = 7,
    ContextChanged = 8,
    Ineligible = 9,
    SafetyDrift = 10,
    RangeUnavailable = 11,
    RangeAlreadyAvailable = 12,
    OtherNativeGateUnavailable = 13,
    Dispatched = 14,
    Expired = 15,
    NativeAttemptOutstanding = 16,
    NativeRetryLimitReached = 17,
    AcceptanceAmbiguous = 18,
    NativeAttemptCancelled = 19,
    AmbiguousInputOrigin = 20,
}

public enum HeldChaseBufferDecisionKind : byte
{
    None = 0,
    WaitingForRange = 1,
    Dispatch = 2,
    WaitingForNativeOutcome = 3,
    WaitingForRetry = 4,
    Cancelled = 5,
}

public readonly record struct HeldChaseBufferDecision(
    HeldChaseBufferDecisionKind Kind,
    HeldChaseBufferIntent? Intent,
    HeldChaseBufferCancelReason Reason)
{
    public static HeldChaseBufferDecision None => new(
        HeldChaseBufferDecisionKind.None,
        null,
        HeldChaseBufferCancelReason.None);
}

/// <summary>
/// Thread-safe state machine for an exact, range-blocked certified tap. Release
/// does not cancel the frozen intent. At most one native attempt may be in
/// flight, and only a proven clean client false may schedule a bounded retry.
/// Acceptance, ambiguity, pre-boundary cancellation, and every identity or
/// safety drift are terminal for the exact reservation.
/// </summary>
public sealed class HeldChaseBufferEngine
{
    private readonly object gate = new();
    private HeldChaseBufferIntent? pending;
    private HeldChaseBufferRetryState retryState =
        HeldChaseBufferRetryState.None;
    private bool nativeAttemptOutstanding;

    public HeldChaseBufferIntent? Pending
    {
        get
        {
            lock (gate) return pending;
        }
    }

    public HeldChaseBufferRetryState RetryState
    {
        get
        {
            lock (gate) return retryState;
        }
    }

    public bool NativeAttemptOutstanding
    {
        get
        {
            lock (gate) return nativeAttemptOutstanding;
        }
    }

    public HeldChaseBufferCancelReason LastCancelReason { get; private set; }

    public bool Arm(HeldChaseBufferArmInput input)
    {
        lock (gate)
        {
            if (pending is not null)
            {
                ClearPending();
                LastCancelReason = HeldChaseBufferCancelReason.Replaced;
            }

            var rejection = HeldChaseBufferRules.GetArmRejection(input);
            if (rejection != HeldChaseBufferCancelReason.None)
            {
                LastCancelReason = rejection;
                return false;
            }

            pending = input.Intent;
            retryState = new HeldChaseBufferRetryState(
                NativeAttemptCount: 0,
                NextNativeAttemptAtMilliseconds: -1,
                NativeAttemptLimit:
                    HeldChaseBufferWindowRules.ResolveNativeAttemptLimit(
                        input.ReservationWindowMilliseconds));
            nativeAttemptOutstanding = false;
            LastCancelReason = HeldChaseBufferCancelReason.None;
            return true;
        }
    }

    public void Cancel(HeldChaseBufferCancelReason reason)
    {
        // Key-up is no longer a cancellation fact. Keep this legacy enum value
        // fail-open so an older runtime callback cannot silently reintroduce
        // held-only semantics while the tap reservation is pending.
        if (reason == HeldChaseBufferCancelReason.Released)
            return;

        if (reason is HeldChaseBufferCancelReason.None or
            HeldChaseBufferCancelReason.Dispatched)
        {
            throw new ArgumentOutOfRangeException(nameof(reason));
        }

        lock (gate)
        {
            ClearPending();
            LastCancelReason = reason;
        }
    }

    public HeldChaseBufferDecision Evaluate(HeldChaseBufferLiveInput input)
    {
        lock (gate)
        {
            if (pending is not { } intent)
                return HeldChaseBufferDecision.None;

            var cancellation = HeldChaseBufferRules.GetLiveCancellation(intent, input);
            if (cancellation != HeldChaseBufferCancelReason.None)
            {
                ClearPending();
                LastCancelReason = cancellation;
                return new HeldChaseBufferDecision(
                    HeldChaseBufferDecisionKind.Cancelled,
                    null,
                    cancellation);
            }

            if (nativeAttemptOutstanding)
            {
                return new HeldChaseBufferDecision(
                    HeldChaseBufferDecisionKind.WaitingForNativeOutcome,
                    intent,
                    HeldChaseBufferCancelReason.NativeAttemptOutstanding);
            }

            if (!input.HasRangeAndLineOfSight)
            {
                return new HeldChaseBufferDecision(
                    HeldChaseBufferDecisionKind.WaitingForRange,
                    intent,
                    HeldChaseBufferCancelReason.None);
            }

            if (retryState.NativeAttemptCount > 0 &&
                (!retryState.IsPending ||
                 input.NowMilliseconds <
                 retryState.NextNativeAttemptAtMilliseconds))
            {
                return new HeldChaseBufferDecision(
                    HeldChaseBufferDecisionKind.WaitingForRetry,
                    intent,
                    HeldChaseBufferCancelReason.None);
            }

            // Reserve the boundary before returning so concurrent or re-entrant
            // evaluation cannot dispatch the frozen intent twice. Completion
            // decides whether a clean false may retain it for a bounded retry.
            nativeAttemptOutstanding = true;
            return new HeldChaseBufferDecision(
                HeldChaseBufferDecisionKind.Dispatch,
                intent,
                HeldChaseBufferCancelReason.None);
        }
    }

    /// <summary>
    /// Completes the exact outstanding native boundary. Only an explicit
    /// <see cref="ClientActionAttemptOutcome.ClientRejected"/> retains the
    /// frozen reservation, subject to the shared bounded retry policy.
    /// </summary>
    public HeldChaseBufferAttemptDecision CompleteNativeAttempt(
        HeldChaseBufferIntent intent,
        long nowMilliseconds,
        ClientActionAttemptOutcome outcome)
    {
        lock (gate)
        {
            if (pending != intent || !nativeAttemptOutstanding)
            {
                return new HeldChaseBufferAttemptDecision(
                    HeldChaseBufferRetryState.None,
                    HeldActionRetryDisposition.CancelledTerminal);
            }

            nativeAttemptOutstanding = false;
            var completion = CompleteAttempt(
                retryState,
                nowMilliseconds,
                outcome);

            if (completion.RetryScheduled)
            {
                retryState = completion.NextState;
                LastCancelReason = HeldChaseBufferCancelReason.None;
                return completion;
            }

            ClearPending();
            LastCancelReason = completion.Disposition switch
            {
                HeldActionRetryDisposition.AcceptedTerminal =>
                    HeldChaseBufferCancelReason.Dispatched,
                HeldActionRetryDisposition.RejectedTerminal =>
                    HeldChaseBufferCancelReason.NativeRetryLimitReached,
                HeldActionRetryDisposition.AmbiguousTerminal =>
                    HeldChaseBufferCancelReason.AcceptanceAmbiguous,
                _ => HeldChaseBufferCancelReason.NativeAttemptCancelled,
            };
            return completion;
        }
    }

    private static HeldChaseBufferAttemptDecision CompleteAttempt(
        HeldChaseBufferRetryState previous,
        long nowMilliseconds,
        ClientActionAttemptOutcome outcome)
    {
        if (!previous.IsValid || nowMilliseconds < 0)
        {
            return TerminalAttempt(
                HeldActionRetryDisposition.AmbiguousTerminal);
        }

        if (outcome == ClientActionAttemptOutcome.ClientRejected)
        {
            var attemptCount = previous.NativeAttemptCount + 1;
            if (attemptCount >= previous.NativeAttemptLimit)
            {
                return TerminalAttempt(
                    HeldActionRetryDisposition.RejectedTerminal);
            }

            return new HeldChaseBufferAttemptDecision(
                previous with
                {
                    NativeAttemptCount = attemptCount,
                    NextNativeAttemptAtMilliseconds = SaturatingAdd(
                        nowMilliseconds,
                        HeldChaseBufferWindowRules.NativeRetryThrottleMilliseconds),
                },
                HeldActionRetryDisposition.RetryScheduled);
        }

        return TerminalAttempt(outcome switch
        {
            ClientActionAttemptOutcome.ClientAccepted =>
                HeldActionRetryDisposition.AcceptedTerminal,
            ClientActionAttemptOutcome.AcceptanceUnknown =>
                HeldActionRetryDisposition.AmbiguousTerminal,
            _ => HeldActionRetryDisposition.CancelledTerminal,
        });
    }

    private static HeldChaseBufferAttemptDecision TerminalAttempt(
        HeldActionRetryDisposition disposition) =>
        new(HeldChaseBufferRetryState.None, disposition);

    private static long SaturatingAdd(long left, long right) =>
        left > long.MaxValue - right ? long.MaxValue : left + right;

    private void ClearPending()
    {
        pending = null;
        retryState = HeldChaseBufferRetryState.None;
        nativeAttemptOutstanding = false;
    }
}

public static class HeldChaseBufferRules
{
    public static HeldChaseBufferCancelReason GetArmRejection(
        HeldChaseBufferArmInput input)
    {
        if (!input.Intent.IsValid)
            return HeldChaseBufferCancelReason.InvalidIntent;
        if (!input.Enabled)
            return HeldChaseBufferCancelReason.Disabled;
        if (HeldChaseBufferWindowRules.Normalize(
                input.ReservationWindowMilliseconds) == 0)
        {
            return HeldChaseBufferCancelReason.Disabled;
        }
        if (input.IsCertifiedPhysicalStandardHotbarRoot &&
            input.IsCertifiedSmartActionMacroFallback)
        {
            return HeldChaseBufferCancelReason.AmbiguousInputOrigin;
        }
        if (!input.IsCertifiedPhysicalStandardHotbarRoot &&
            !input.IsCertifiedSmartActionMacroFallback)
        {
            return HeldChaseBufferCancelReason.NotPhysicalStandardHotbar;
        }
        if (!input.ActionEligible)
            return HeldChaseBufferCancelReason.Ineligible;
        if (!input.SafetyValid)
            return HeldChaseBufferCancelReason.SafetyDrift;
        if (!input.RangeProbeAvailable)
            return HeldChaseBufferCancelReason.RangeUnavailable;
        if (input.HasRangeAndLineOfSight)
            return HeldChaseBufferCancelReason.RangeAlreadyAvailable;
        if (!input.OtherNativeGatesReady)
            return HeldChaseBufferCancelReason.OtherNativeGateUnavailable;
        return HeldChaseBufferCancelReason.None;
    }

    public static HeldChaseBufferCancelReason GetLiveCancellation(
        HeldChaseBufferIntent intent,
        HeldChaseBufferLiveInput input)
    {
        if (!intent.IsValid)
            return HeldChaseBufferCancelReason.InvalidIntent;
        if (!input.Enabled)
            return HeldChaseBufferCancelReason.Disabled;
        if (!input.WithinDeadline)
            return HeldChaseBufferCancelReason.Expired;
        if (input.PressGeneration != intent.PressGeneration)
            return HeldChaseBufferCancelReason.Replaced;
        if (input.RequestedActionId != intent.RequestedActionId ||
            input.ResolvedActionId != intent.ResolvedActionId)
        {
            return HeldChaseBufferCancelReason.ActionChanged;
        }

        if (input.TargetFingerprint != intent.TargetFingerprint)
            return HeldChaseBufferCancelReason.TargetChanged;
        if (input.TerritoryId != intent.TerritoryId ||
            input.InstanceFingerprint != intent.InstanceFingerprint)
        {
            return HeldChaseBufferCancelReason.ContextChanged;
        }

        if (!input.ActionEligible)
            return HeldChaseBufferCancelReason.Ineligible;
        if (!input.SafetyValid)
            return HeldChaseBufferCancelReason.SafetyDrift;
        if (!input.RangeProbeAvailable)
            return HeldChaseBufferCancelReason.RangeUnavailable;
        if (!input.OtherNativeGatesReady)
            return HeldChaseBufferCancelReason.OtherNativeGateUnavailable;
        return HeldChaseBufferCancelReason.None;
    }
}
