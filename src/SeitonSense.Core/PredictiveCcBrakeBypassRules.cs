namespace SeitonSense.Core;

/// <summary>
/// Exact identity carried by one protection-end counter-CC attempt that has
/// already passed the probe's status-end prediction. This is not a general
/// brake override: it is valid only for one cataloged action and one concrete
/// target.
/// </summary>
public readonly record struct PredictiveCcBrakeBypassIntent(
    uint ActionId,
    ulong TargetGameObjectId,
    uint TargetEntityId,
    uint TargetJobId,
    uint ProtectionStatusId,
    long ScheduledProtectionEndAtMilliseconds,
    int SafeImpactLeadMilliseconds,
    bool RequireGuardAbsent = false);

public readonly record struct PredictiveCcProtectionStatusObservation(
    uint StatusId,
    long RemainingMilliseconds);

public readonly record struct PredictiveCcProtectionSet(
    int ScheduledStatusCount,
    long ScheduledRemainingMilliseconds,
    bool OtherBlockerPresent);

/// <summary>
/// Pure matching policy for the one-call predictive CC-brake scope owned by
/// the native action detour. Every mismatch leaves the ordinary brake active.
/// </summary>
public static class PredictiveCcBrakeBypassRules
{
    public static bool IsValidIntent(PredictiveCcBrakeBypassIntent intent) =>
        IsValidProfileAction(intent.ActionId) &&
        IsConcreteTargetId(intent.TargetGameObjectId) &&
        MiracleInterceptConfirmationRules.IsValidEntityId(intent.TargetEntityId) &&
        intent.TargetJobId != 0 &&
        (IsNaturalHelperIntent(intent) || IsPredictiveIntent(intent));

    /// <summary>
    /// A natural helper token carries only the frozen action/actor identity.
    /// The hook must prove that no applicable live blocker appeared after the
    /// probe's final check. The exact sentinel is deliberately unambiguous.
    /// </summary>
    public static bool IsNaturalHelperIntent(PredictiveCcBrakeBypassIntent intent) =>
        intent.ProtectionStatusId == 0 &&
        intent.ScheduledProtectionEndAtMilliseconds == -1 &&
        intent.SafeImpactLeadMilliseconds == 0;

    public static bool IsPredictiveIntent(PredictiveCcBrakeBypassIntent intent) =>
        IsSupportedProtectionStatus(intent.ProtectionStatusId) &&
        intent.ScheduledProtectionEndAtMilliseconds > 0 &&
        intent.SafeImpactLeadMilliseconds >=
            ReactiveCounterCcImpactTimingRules.MinimumUsefulLeadMilliseconds;

    public static bool IsSupportedProtectionStatus(uint statusId) => statusId is
        MiracleCleanseFollowupRules.ResilienceStatusId or
        MiracleGuardFollowupRules.GuardStatusId or
        MiracleGuardFollowupRules.GuardStatusAlternateId;

    public static bool CanConsume(
        PredictiveCcBrakeBypassIntent intent,
        bool alreadyConsumed,
        uint requestedActionId,
        uint resolvedActionId,
        ulong originalTargetId,
        ulong forwardedTargetId,
        bool targetSuppressedByRedirect,
        bool isActionInvocation,
        bool isNormalMode) =>
        !alreadyConsumed &&
        IsValidIntent(intent) &&
        isActionInvocation &&
        isNormalMode &&
        !targetSuppressedByRedirect &&
        requestedActionId == intent.ActionId &&
        resolvedActionId == intent.ActionId &&
        originalTargetId == intent.TargetGameObjectId &&
        forwardedTargetId == intent.TargetGameObjectId;

    /// <summary>
    /// Classifies the one frozen protection row independently of the action's
    /// ordinary blocker family. This matters for Miracle of Nature: Guard is
    /// intentionally not a normal Miracle blocker, but an exact post-Guard
    /// prediction must still prove that the scheduled Guard row is the only
    /// protection being bypassed. Every other family blocker remains live.
    /// </summary>
    public static PredictiveCcProtectionSet ClassifyProtectionSet(
        CcImmunityBrakeBlockerFamily blockerFamily,
        uint targetJobId,
        uint scheduledProtectionStatusId,
        IEnumerable<PredictiveCcProtectionStatusObservation>? statuses)
    {
        if (!IsSupportedProtectionStatus(scheduledProtectionStatusId) ||
            statuses is null)
        {
            return default;
        }

        var scheduledCount = 0;
        var scheduledRemainingMilliseconds = 0L;
        var otherBlockerPresent = false;
        foreach (var status in statuses)
        {
            if (status.StatusId == scheduledProtectionStatusId)
            {
                scheduledCount++;
                scheduledRemainingMilliseconds = Math.Max(
                    scheduledRemainingMilliseconds,
                    status.RemainingMilliseconds);
                continue;
            }

            if (IsSupportedProtectionStatus(status.StatusId) ||
                CcImmunityBrakeActionCatalog.IsBlockerStatus(
                    blockerFamily,
                    status.StatusId,
                    targetJobId))
            {
                otherBlockerPresent = true;
            }
        }

        return new PredictiveCcProtectionSet(
            scheduledCount,
            scheduledRemainingMilliseconds,
            otherBlockerPresent);
    }

    public static bool RequiresPredictiveHookRecheck(uint actionId) =>
        IsValidProfileAction(actionId);

    public static bool IsHelperOnlyHookRecheckAction(uint actionId) => actionId is
        MiracleInterceptConfirmationRules.ResolutionActionId or
        MiracleInterceptConfirmationRules.ViceOfThornsActionId or
        MiracleInterceptConfirmationRules.FrostStarActionId;

    public static CcImmunityBrakeBlockerFamily BlockerFamilyForPredictiveAction(
        uint actionId) =>
        actionId == MiracleInterceptConfirmationRules.MiracleOfNatureActionId
            ? CcImmunityBrakeBlockerFamily.Miracle
            : IsValidProfileAction(actionId)
                ? CcImmunityBrakeBlockerFamily.StandardPurifyCc
                : CcImmunityBrakeBlockerFamily.StandardPurifyCc;

    private static bool IsValidProfileAction(uint actionId) =>
        ReactiveCounterCcImpactTimingRules.IsSupportedAction(actionId) &&
        ReactiveCounterCcProfileRules.Get(actionId) is { IsValid: true };

    private static bool IsConcreteTargetId(ulong targetId) =>
        targetId is not 0 and
            not CcImmunityBrakeTargetRules.DefaultTargetSentinel and
            not ulong.MaxValue;
}
