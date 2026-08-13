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
        IEnumerable<uint>? activeStatusIds)
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
            var activeStatuses = activeStatusIds.ToHashSet();
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
                return new CcImmunityBrakeDecision(
                    CcImmunityBrakeDecisionKind.Block,
                    CcImmunityBrakeDecisionReason.VerifiedBlocker,
                    action,
                    statusId);
            }
        }

        return Pass(CcImmunityBrakeDecisionReason.NoVerifiedBlocker, action);
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
}
