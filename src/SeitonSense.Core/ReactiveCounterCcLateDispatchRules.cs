namespace SeitonSense.Core;

public readonly record struct ReactiveCounterCcLateReservation(
    uint ActionId,
    ulong TargetGameObjectId,
    uint TargetEntityId,
    uint ScheduledProtectionStatusId,
    long IdealRequestAtMilliseconds)
{
    public bool IsValid =>
        ReactiveCounterCcProfileRules.UsesMainGlobalCooldown(ActionId) &&
        TargetHighlightRules.IsValidGameObjectId(TargetGameObjectId) &&
        MiracleInterceptConfirmationRules.IsValidEntityId(TargetEntityId) &&
        (ScheduledProtectionStatusId == 0 ||
         PredictiveCcBrakeBypassRules.IsSupportedProtectionStatus(
             ScheduledProtectionStatusId)) &&
        IdealRequestAtMilliseconds >= 0;
}

/// <summary>
/// A true main-GCD counter may wait without claiming the held-input scheduler,
/// but only inside one frozen one-second lease measured from its original
/// ideal request frame. No target, action, or protection episode may change.
/// </summary>
public static class ReactiveCounterCcLateDispatchRules
{
    public const long MaximumLateMilliseconds = 1_000;

    public static bool IsInsideWindow(
        ReactiveCounterCcLateReservation reservation,
        long nowMilliseconds) =>
        reservation.IsValid &&
        nowMilliseconds >= reservation.IdealRequestAtMilliseconds &&
        nowMilliseconds - reservation.IdealRequestAtMilliseconds <
        MaximumLateMilliseconds;

    public static bool CanDispatch(
        ReactiveCounterCcLateReservation reservation,
        long nowMilliseconds,
        uint currentActionId,
        ulong currentTargetGameObjectId,
        uint currentTargetEntityId,
        uint currentProtectionStatusId,
        bool protectionStateValid,
        bool heldKeyGenerationValid,
        bool rangeAndLineOfSightValid,
        bool structurallyReady,
        bool globalQueueReady) =>
        IsInsideWindow(reservation, nowMilliseconds) &&
        currentActionId == reservation.ActionId &&
        currentTargetGameObjectId == reservation.TargetGameObjectId &&
        currentTargetEntityId == reservation.TargetEntityId &&
        (currentProtectionStatusId == 0 ||
         currentProtectionStatusId == reservation.ScheduledProtectionStatusId) &&
        protectionStateValid &&
        heldKeyGenerationValid &&
        rangeAndLineOfSightValid &&
        structurallyReady &&
        globalQueueReady;
}
