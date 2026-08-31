namespace SeitonSense.Core;

/// <summary>
/// Immutable identity of one exact action which originated at a certified
/// physical standard-hotbar press. The chase buffer never substitutes any
/// action, target, context, or physical-press generation.
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
/// Facts captured around the original physical action call. Arming is allowed
/// only when native range/line of sight is the sole unavailable boundary.
/// </summary>
public readonly record struct HeldChaseBufferArmInput(
    HeldChaseBufferIntent Intent,
    bool Enabled,
    bool IsCertifiedPhysicalStandardHotbarRoot,
    bool InputHeld,
    bool ActionEligible,
    bool SafetyValid,
    bool RangeProbeAvailable,
    bool HasRangeAndLineOfSight,
    bool OtherNativeGatesReady);

/// <summary>
/// Current facts for the already-frozen intent. The runtime supplies native
/// range/LoS and all non-spatial gates independently so spatial waiting cannot
/// hide a later action, target, context, input, or safety change.
/// </summary>
public readonly record struct HeldChaseBufferLiveInput(
    bool Enabled,
    bool IsExactPhysicalStandardHotbarHold,
    bool InputHeld,
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
    bool WithinDeadline = true);

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
}

public enum HeldChaseBufferDecisionKind : byte
{
    None = 0,
    WaitingForRange = 1,
    Dispatch = 2,
    Cancelled = 3,
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
/// Thread-safe one-shot state machine for a held, exact, range-blocked action.
/// It never retries a native rejection: it only waits before the first later
/// native request, and consumes the intent before returning Dispatch.
/// </summary>
public sealed class HeldChaseBufferEngine
{
    private readonly object gate = new();
    private HeldChaseBufferIntent? pending;

    public HeldChaseBufferIntent? Pending
    {
        get
        {
            lock (gate) return pending;
        }
    }

    public HeldChaseBufferCancelReason LastCancelReason { get; private set; }

    public bool Arm(HeldChaseBufferArmInput input)
    {
        lock (gate)
        {
            if (pending is not null)
            {
                pending = null;
                LastCancelReason = HeldChaseBufferCancelReason.Replaced;
            }

            var rejection = HeldChaseBufferRules.GetArmRejection(input);
            if (rejection != HeldChaseBufferCancelReason.None)
            {
                LastCancelReason = rejection;
                return false;
            }

            pending = input.Intent;
            LastCancelReason = HeldChaseBufferCancelReason.None;
            return true;
        }
    }

    public void Cancel(HeldChaseBufferCancelReason reason)
    {
        if (reason is HeldChaseBufferCancelReason.None or
            HeldChaseBufferCancelReason.Dispatched)
        {
            throw new ArgumentOutOfRangeException(nameof(reason));
        }

        lock (gate)
        {
            pending = null;
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
                pending = null;
                LastCancelReason = cancellation;
                return new HeldChaseBufferDecision(
                    HeldChaseBufferDecisionKind.Cancelled,
                    null,
                    cancellation);
            }

            if (!input.HasRangeAndLineOfSight)
            {
                return new HeldChaseBufferDecision(
                    HeldChaseBufferDecisionKind.WaitingForRange,
                    intent,
                    HeldChaseBufferCancelReason.None);
            }

            // Consume before returning so concurrent or re-entrant evaluation
            // cannot dispatch the same frozen intent twice.
            pending = null;
            LastCancelReason = HeldChaseBufferCancelReason.Dispatched;
            return new HeldChaseBufferDecision(
                HeldChaseBufferDecisionKind.Dispatch,
                intent,
                HeldChaseBufferCancelReason.Dispatched);
        }
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
        if (!input.IsCertifiedPhysicalStandardHotbarRoot)
            return HeldChaseBufferCancelReason.NotPhysicalStandardHotbar;
        if (!input.InputHeld)
            return HeldChaseBufferCancelReason.Released;
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
        if (!input.InputHeld)
            return HeldChaseBufferCancelReason.Released;
        if (!input.IsExactPhysicalStandardHotbarHold)
            return HeldChaseBufferCancelReason.NotPhysicalStandardHotbar;
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
