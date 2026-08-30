namespace SeitonSense.Core;

public enum CcImmunityBrakeDecisionKind : byte
{
    Pass = 0,
    Block = 1,
}

public enum CcImmunityBrakeDecisionReason : byte
{
    None = 0,
    MasterDisabled = 1,
    JobDisabled = 2,
    ActionDisabled = 3,
    ActionNotCataloged = 4,
    JobMismatch = 5,
    TargetNotResolvedExactly = 6,
    InvalidTargetIdentity = 7,
    IncomingTargetMismatch = 8,
    NoVerifiedBlocker = 9,
    VerifiedBlocker = 10,
}

public readonly record struct CcImmunityBrakeDecision(
    CcImmunityBrakeDecisionKind Kind,
    CcImmunityBrakeDecisionReason Reason,
    CcImmunityBrakeActionDefinition? Action = null,
    uint BlockerStatusId = 0)
{
    public bool ShouldBlock => Kind == CcImmunityBrakeDecisionKind.Block;
}

/// <summary>
/// Stateless policy for one incoming action attempt. It can only return Pass
/// or Block; it never changes the action or target and owns no delayed work,
/// replay, retry, or hold state.
/// </summary>
public static class CcImmunityBrakeRules
{
    public static CcImmunityBrakeDecision Evaluate(
        bool masterEnabled,
        bool jobEnabled,
        bool actionEnabled,
        uint localJobId,
        uint actionId,
        ulong incomingTargetId,
        TargetPressureActorIdentity resolvedTarget,
        uint targetJobId,
        bool targetIdentityResolvedExactly,
        IEnumerable<uint>? activeStatusIds,
        uint permittedPredictiveBlockerStatusId = 0)
    {
        if (!masterEnabled)
            return Pass(CcImmunityBrakeDecisionReason.MasterDisabled);
        if (!jobEnabled)
            return Pass(CcImmunityBrakeDecisionReason.JobDisabled);
        if (!actionEnabled)
            return Pass(CcImmunityBrakeDecisionReason.ActionDisabled);

        if (!CcImmunityBrakeActionCatalog.TryGet(actionId, out var action))
            return Pass(CcImmunityBrakeDecisionReason.ActionNotCataloged);
        if (localJobId != action.JobId)
            return Pass(CcImmunityBrakeDecisionReason.JobMismatch, action);

        if (!targetIdentityResolvedExactly)
            return Pass(CcImmunityBrakeDecisionReason.TargetNotResolvedExactly, action);
        if (!resolvedTarget.IsValid)
            return Pass(CcImmunityBrakeDecisionReason.InvalidTargetIdentity, action);
        if (!IsExactIncomingTarget(incomingTargetId, resolvedTarget))
            return Pass(CcImmunityBrakeDecisionReason.IncomingTargetMismatch, action);

        if (activeStatusIds is not null)
        {
            var activeStatusArray = activeStatusIds.ToArray();
            var mayPermitOnePredictedStatus =
                PredictiveCcBrakeBypassRules.IsSupportedProtectionStatus(
                    permittedPredictiveBlockerStatusId) &&
                activeStatusArray.Count(statusId =>
                    statusId == permittedPredictiveBlockerStatusId) == 1;
            var activeStatuses = activeStatusArray.ToHashSet();
            foreach (var statusId in CcImmunityBrakeActionCatalog.GetBlockerStatusIds(action.BlockerFamily))
            {
                if (!activeStatuses.Contains(statusId) ||
                    !CcImmunityBrakeActionCatalog.IsBlockerStatus(
                        action.BlockerFamily,
                        statusId,
                        targetJobId))
                {
                    continue;
                }

                // Ordinary authored movement must remain usable against a
                // guarded target. This exception is deliberately confined to
                // the closed hostile-movement catalog and to the two exact
                // Guard rows. Resilience, Paean, and job-specific CC immunity
                // still stop the CC component. The predictive helper recheck
                // below does not use this exception, so post-Guard timing
                // remains strict for plugin-owned counter-CC.
                if (SmartActionMovementGuardBypassRules.IsGuardStatus(statusId) &&
                    SmartActionMovementGuardBypassRules.AllowsGuardTarget(
                        action.JobId,
                        action.ActionId))
                {
                    continue;
                }

                if (mayPermitOnePredictedStatus &&
                    statusId == permittedPredictiveBlockerStatusId)
                {
                    continue;
                }

                return new CcImmunityBrakeDecision(
                    CcImmunityBrakeDecisionKind.Block,
                    CcImmunityBrakeDecisionReason.VerifiedBlocker,
                    action,
                    statusId);
            }
        }

        return Pass(CcImmunityBrakeDecisionReason.NoVerifiedBlocker, action);
    }

    /// <summary>
    /// Hook-time safety recheck for one plugin-owned helper call. It applies
    /// only when the detour consumed an exact one-call token and only to the
    /// frozen primary actor. A natural token requires authoritative blocker
    /// absence; a predictive token may carry exactly its still-valid scheduled
    /// protection row. Target-centered/line actions stay outside the user's
    /// ordinary brake catalog, so normal AoE semantics are never broadened.
    /// </summary>
    public static CcImmunityBrakeDecision EvaluatePredictiveHelperExactRecheck(
        uint localJobId,
        uint actionId,
        ulong incomingTargetId,
        TargetPressureActorIdentity resolvedTarget,
        uint targetJobId,
        bool targetIdentityResolvedExactly,
        IEnumerable<PredictiveCcProtectionStatusObservation>? activeStatuses,
        PredictiveCcBrakeBypassIntent intent,
        long nowMilliseconds)
    {
        if (!PredictiveCcBrakeBypassRules.IsValidIntent(intent) ||
            intent.ActionId != actionId ||
            !PredictiveCcBrakeBypassRules.RequiresPredictiveHookRecheck(actionId) ||
            ReactiveCounterCcProfileRules.Get(actionId) is not { } profile)
        {
            return Block(CcImmunityBrakeDecisionReason.ActionNotCataloged);
        }

        if (localJobId != profile.JobId)
            return Block(CcImmunityBrakeDecisionReason.JobMismatch);
        if (!targetIdentityResolvedExactly)
            return Block(CcImmunityBrakeDecisionReason.TargetNotResolvedExactly);
        if (!resolvedTarget.IsValid)
            return Block(CcImmunityBrakeDecisionReason.InvalidTargetIdentity);
        if (!IsExactIncomingTarget(incomingTargetId, resolvedTarget) ||
            intent.TargetGameObjectId != resolvedTarget.GameObjectId ||
            intent.TargetEntityId != resolvedTarget.EntityId ||
            targetJobId == 0 ||
            intent.TargetJobId != targetJobId)
            return Block(CcImmunityBrakeDecisionReason.IncomingTargetMismatch);

        if (activeStatuses is null)
        {
            return Block(CcImmunityBrakeDecisionReason.VerifiedBlocker);
        }

        var blockerFamily = PredictiveCcBrakeBypassRules.BlockerFamilyForPredictiveAction(
            actionId);
        var statuses = activeStatuses.ToArray();
        if (PredictiveCcBrakeBypassRules.IsNaturalHelperIntent(intent))
        {
            var blockerStatusId = statuses
                .Select(static status => status.StatusId)
                .FirstOrDefault(statusId =>
                    CcImmunityBrakeActionCatalog.IsBlockerStatus(
                        blockerFamily,
                        statusId,
                        targetJobId) ||
                    (intent.RequireGuardAbsent &&
                     MiracleGuardFollowupRules.IsExactGuardStatus(statusId)));
            return blockerStatusId == 0
                ? Pass(CcImmunityBrakeDecisionReason.NoVerifiedBlocker)
                : Block(
                    CcImmunityBrakeDecisionReason.VerifiedBlocker,
                    blockerStatusId);
        }

        var protectionSet = PredictiveCcBrakeBypassRules.ClassifyProtectionSet(
            blockerFamily,
            targetJobId,
            intent.ProtectionStatusId,
            statuses);
        if (protectionSet.ScheduledStatusCount > 1)
        {
            return Block(
                CcImmunityBrakeDecisionReason.VerifiedBlocker,
                intent.ProtectionStatusId);
        }

        if (protectionSet.OtherBlockerPresent)
        {
            var otherBlockerStatusId = statuses
                .Select(static status => status.StatusId)
                .FirstOrDefault(statusId =>
                statusId != intent.ProtectionStatusId &&
                (PredictiveCcBrakeBypassRules.IsSupportedProtectionStatus(statusId) ||
                 CcImmunityBrakeActionCatalog.IsBlockerStatus(
                     blockerFamily,
                     statusId,
                     targetJobId)));
            return Block(
                CcImmunityBrakeDecisionReason.VerifiedBlocker,
                otherBlockerStatusId);
        }

        if (protectionSet.ScheduledStatusCount == 1 &&
            !ReactiveCounterCcImpactTimingRules.IsScheduledProtectionStillValid(
                intent.ScheduledProtectionEndAtMilliseconds,
                nowMilliseconds,
                protectionSet.ScheduledRemainingMilliseconds,
                intent.SafeImpactLeadMilliseconds))
        {
            return Block(
                CcImmunityBrakeDecisionReason.VerifiedBlocker,
                intent.ProtectionStatusId);
        }

        return Pass(CcImmunityBrakeDecisionReason.NoVerifiedBlocker);
    }

    private static bool IsExactIncomingTarget(
        ulong incomingTargetId,
        TargetPressureActorIdentity target) =>
        incomingTargetId is not 0 and not 0xE0000000UL and not ulong.MaxValue &&
        (incomingTargetId == target.GameObjectId || incomingTargetId == target.EntityId);

    private static CcImmunityBrakeDecision Pass(
        CcImmunityBrakeDecisionReason reason,
        CcImmunityBrakeActionDefinition? action = null) =>
        new(CcImmunityBrakeDecisionKind.Pass, reason, action);

    private static CcImmunityBrakeDecision Block(
        CcImmunityBrakeDecisionReason reason,
        uint blockerStatusId = 0) =>
        new(
            CcImmunityBrakeDecisionKind.Block,
            reason,
            BlockerStatusId: blockerStatusId);
}
