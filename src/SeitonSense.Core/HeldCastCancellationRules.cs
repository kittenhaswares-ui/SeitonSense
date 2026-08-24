namespace SeitonSense.Core;

/// <summary>
/// Identifies the held helper which owns the single prioritized cancellation
/// request. Numeric order mirrors the canonical physical-held scheduler order;
/// it never selects another action or target.
/// </summary>
public enum HeldCastCancellationHelperKind : byte
{
    None = 0,
    Purify = 1,
    ReactiveCounterCc = 2,
    AllyRescue = 3,
    Guardian = 4,
    NinjaGuardShukuchi = 5,
    NinjaSeiton = 6,
    ScholarCriticalStrategy = 7,
    DarkKnightPlunge = 8,
    SmartRecuperate = 9,
    EmergencyTeleport = 10,
    Guard = 11,
    PressureEscapeSprint = 12,
}

/// <summary>
/// One exact, already frozen held-helper intent. The producer remains
/// responsible for all helper-specific status, target, range, resource, and
/// deadline checks. This token only permits the central coordinator to request
/// cancellation of the currently observed cast for that same intent.
/// </summary>
public readonly record struct HeldCastCancellationRequest(
    HeldCastCancellationHelperKind HelperKind,
    uint HelperActionId,
    TargetPressureActorIdentity LocalPlayer,
    TargetPressureActorIdentity Target,
    int FrozenKeyCode,
    ulong IntentEpochToken)
{
    public bool IsValid =>
        HelperKind != HeldCastCancellationHelperKind.None &&
        HelperActionId != 0 &&
        LocalPlayer.IsValid &&
        Target.IsValid &&
        FrozenKeyCode > 0 &&
        IntentEpochToken != 0;
}

public enum HeldCastCancellationDecisionKind : byte
{
    Inactive = 0,
    ObservingCast = 1,
    CancelRequested = 2,
    WaitingForCastEnd = 3,
    CastEnded = 4,
}

public enum HeldCastCancellationDecisionReason : byte
{
    None = 0,
    NoActiveCast = 1,
    HardReset = 2,
    FeatureDisabled = 3,
    UnsupportedContext = 4,
    TextInputActive = 5,
    GuardActive = 6,
    NoPrioritizedIntent = 7,
    InvalidRequest = 8,
    IntentNotOtherwiseReady = 9,
    FrozenKeyReleased = 10,
    LocalPlayerInvalid = 11,
    LocalPlayerChanged = 12,
    LocalPlayerDead = 13,
    LocalPlayerUntargetable = 14,
    ActionIdentityChanged = 15,
    ActionOnCooldown = 16,
    ActionResourcesUnavailable = 17,
    CastSignalIncomplete = 18,
    CastSignalChangedWithoutClear = 19,
    NativeQueueOccupied = 20,
    InvalidAnimationLock = 21,
    AnimationLockBusy = 22,
    AlreadyRequested = 23,
}

/// <summary>
/// Consecutive nonzero cast signals form one epoch. A later request may be made
/// within that epoch when its exact intent becomes ready, but a native request
/// is terminal until a consistent no-cast frame has been observed.
/// </summary>
public readonly record struct HeldCastCancellationState(
    ulong LastCastEpochToken,
    bool CastEpochActive,
    bool CancellationRequested,
    bool CastSignalMismatch,
    uint ObservedCastActionId,
    TargetPressureActorIdentity ObservedLocalPlayer,
    bool LocalPlayerIdentityMismatch)
{
    public static HeldCastCancellationState Initial => default;
}

public readonly record struct HeldCastCancellationObservation(
    bool HardReset,
    bool FeatureEnabled,
    bool SupportedContext,
    bool TextInputActive,
    bool GuardActive,
    bool PrioritizedInputClaimed,
    bool IntentOtherwiseReady,
    HeldCastCancellationRequest? Request,
    bool FrozenKeyStillDown,
    bool LocalPlayerIdentityValid,
    TargetPressureActorIdentity CurrentLocalPlayer,
    bool LocalPlayerAlive,
    bool LocalPlayerTargetable,
    uint ResolvedHelperActionId,
    bool HelperActionOffCooldown,
    bool HelperActionResourcesReady,
    bool LocalPlayerIsCasting,
    uint CastActionId,
    bool ActionQueued,
    float AnimationLockSeconds);

public readonly record struct HeldCastCancellationDecision(
    HeldCastCancellationState NextState,
    HeldCastCancellationDecisionKind Kind,
    HeldCastCancellationDecisionReason Reason)
{
    public bool ShouldInvokeNative =>
        Kind == HeldCastCancellationDecisionKind.CancelRequested;
}

public static class HeldCastCancellationRules
{
    public const float MaximumCancellationAnimationLockSeconds = 0.050f;

    public static HeldCastCancellationDecision Observe(
        HeldCastCancellationState state,
        HeldCastCancellationObservation observation)
    {
        var anyCastSignal = observation.LocalPlayerIsCasting ||
                            observation.CastActionId != 0;
        if (!anyCastSignal)
        {
            var castEnded = state.CastEpochActive;
            return new HeldCastCancellationDecision(
                state with
                {
                    CastEpochActive = false,
                    CancellationRequested = false,
                    CastSignalMismatch = false,
                    ObservedCastActionId = 0,
                    ObservedLocalPlayer = default,
                    LocalPlayerIdentityMismatch = false,
                },
                castEnded
                    ? HeldCastCancellationDecisionKind.CastEnded
                    : HeldCastCancellationDecisionKind.Inactive,
                HeldCastCancellationDecisionReason.NoActiveCast);
        }

        var next = state;
        if (!state.CastEpochActive)
        {
            next = new HeldCastCancellationState(
                NextToken(state.LastCastEpochToken),
                CastEpochActive: true,
                CancellationRequested: false,
                CastSignalMismatch: false,
                observation.CastActionId,
                observation.LocalPlayerIdentityValid
                    ? observation.CurrentLocalPlayer
                    : default,
                LocalPlayerIdentityMismatch: false);
        }
        else if (state.ObservedCastActionId == 0 && observation.CastActionId != 0)
        {
            next = state with { ObservedCastActionId = observation.CastActionId };
        }
        else if (state.ObservedCastActionId != 0 &&
                 observation.CastActionId != 0 &&
                 state.ObservedCastActionId != observation.CastActionId)
        {
            next = state with { CastSignalMismatch = true };
        }

        if (observation.LocalPlayerIdentityValid &&
            observation.CurrentLocalPlayer.IsValid)
        {
            if (!next.ObservedLocalPlayer.IsValid)
            {
                next = next with
                {
                    ObservedLocalPlayer = observation.CurrentLocalPlayer,
                };
            }
            else if (next.ObservedLocalPlayer != observation.CurrentLocalPlayer)
            {
                next = next with { LocalPlayerIdentityMismatch = true };
            }
        }

        if (next.CancellationRequested)
            return Waiting(next, HeldCastCancellationDecisionReason.AlreadyRequested);

        if (next.CastSignalMismatch)
            return Waiting(
                next,
                HeldCastCancellationDecisionReason.CastSignalChangedWithoutClear);

        if (next.LocalPlayerIdentityMismatch)
            return Waiting(next, HeldCastCancellationDecisionReason.LocalPlayerChanged);

        var blocked = FindBlocker(observation);
        if (blocked != HeldCastCancellationDecisionReason.None)
            return Observing(next, blocked);

        next = next with { CancellationRequested = true };
        return new HeldCastCancellationDecision(
            next,
            HeldCastCancellationDecisionKind.CancelRequested,
            HeldCastCancellationDecisionReason.None);
    }

    private static HeldCastCancellationDecisionReason FindBlocker(
        HeldCastCancellationObservation observation)
    {
        if (observation.HardReset)
            return HeldCastCancellationDecisionReason.HardReset;
        if (!observation.FeatureEnabled)
            return HeldCastCancellationDecisionReason.FeatureDisabled;
        if (!observation.SupportedContext)
            return HeldCastCancellationDecisionReason.UnsupportedContext;
        if (observation.TextInputActive)
            return HeldCastCancellationDecisionReason.TextInputActive;
        if (observation.GuardActive)
            return HeldCastCancellationDecisionReason.GuardActive;
        if (!observation.PrioritizedInputClaimed)
            return HeldCastCancellationDecisionReason.NoPrioritizedIntent;
        if (observation.Request is not { IsValid: true } request)
            return HeldCastCancellationDecisionReason.InvalidRequest;
        if (!observation.IntentOtherwiseReady)
            return HeldCastCancellationDecisionReason.IntentNotOtherwiseReady;
        if (!observation.FrozenKeyStillDown)
            return HeldCastCancellationDecisionReason.FrozenKeyReleased;
        if (!observation.LocalPlayerIdentityValid ||
            !observation.CurrentLocalPlayer.IsValid)
        {
            return HeldCastCancellationDecisionReason.LocalPlayerInvalid;
        }
        if (request.LocalPlayer != observation.CurrentLocalPlayer)
            return HeldCastCancellationDecisionReason.LocalPlayerChanged;
        if (!observation.LocalPlayerAlive)
            return HeldCastCancellationDecisionReason.LocalPlayerDead;
        if (!observation.LocalPlayerTargetable)
            return HeldCastCancellationDecisionReason.LocalPlayerUntargetable;
        if (observation.ResolvedHelperActionId != request.HelperActionId)
            return HeldCastCancellationDecisionReason.ActionIdentityChanged;
        if (!observation.HelperActionOffCooldown)
            return HeldCastCancellationDecisionReason.ActionOnCooldown;
        if (!observation.HelperActionResourcesReady)
            return HeldCastCancellationDecisionReason.ActionResourcesUnavailable;
        if (!observation.LocalPlayerIsCasting || observation.CastActionId == 0)
            return HeldCastCancellationDecisionReason.CastSignalIncomplete;
        if (observation.ActionQueued)
            return HeldCastCancellationDecisionReason.NativeQueueOccupied;
        if (!float.IsFinite(observation.AnimationLockSeconds) ||
            observation.AnimationLockSeconds < 0f)
        {
            return HeldCastCancellationDecisionReason.InvalidAnimationLock;
        }
        if (observation.AnimationLockSeconds > MaximumCancellationAnimationLockSeconds)
            return HeldCastCancellationDecisionReason.AnimationLockBusy;

        return HeldCastCancellationDecisionReason.None;
    }

    private static HeldCastCancellationDecision Observing(
        HeldCastCancellationState state,
        HeldCastCancellationDecisionReason reason) =>
        new(state, HeldCastCancellationDecisionKind.ObservingCast, reason);

    private static HeldCastCancellationDecision Waiting(
        HeldCastCancellationState state,
        HeldCastCancellationDecisionReason reason) =>
        new(state, HeldCastCancellationDecisionKind.WaitingForCastEnd, reason);

    private static ulong NextToken(ulong token) =>
        token == ulong.MaxValue ? 1 : token + 1;
}
