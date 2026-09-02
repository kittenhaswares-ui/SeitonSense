namespace SeitonSense.Core;

public enum BardRepellingShotDecisionKind : byte
{
    Inactive = 0,
    Waiting = 1,
    CancelPowerfulShot = 2,
    Dispatch = 3,
}

public enum BardRepellingShotDecisionReason : byte
{
    None = 0,
    Disabled = 1,
    UnsupportedContext = 2,
    InvalidLocalPlayer = 3,
    WrongJob = 4,
    MetadataUnverified = 5,
    GuardUnknown = 6,
    GuardActive = 7,
    TextInputUnknown = 8,
    TextInputActive = 9,
    HigherPriorityClaimed = 10,
    InvalidTarget = 11,
    TargetOutOfRange = 12,
    ActionIdentityChanged = 13,
    ActionUnavailable = 14,
    OtherCastActive = 15,
    BasicShotMetadataUnverified = 16,
    NativeBoundaryBusy = 17,
}

public readonly record struct BardRepellingShotObservation(
    bool Enabled,
    bool SupportedContext,
    TargetPressureActorIdentity LocalPlayer,
    uint LocalJobId,
    bool LocalPlayerAliveAndTargetable,
    bool MetadataVerified,
    bool GuardStateKnown,
    bool GuardActive,
    bool TextInputStateKnown,
    bool TextInputActive,
    bool HigherPriorityClaimed,
    TargetPressureActorIdentity Target,
    bool TargetResolvedExactly,
    bool TargetAliveAndTargetable,
    bool TargetInNativeRangeAndLineOfSight,
    uint ResolvedActionId,
    bool ActionOffCooldown,
    bool ActionResourcesReady,
    bool LocalPlayerIsCasting,
    uint CastActionId,
    uint AdjustedCastActionId,
    bool BasicShotMetadataVerified,
    bool NativeBoundaryNearQueueable);

public readonly record struct BardRepellingShotDecision(
    BardRepellingShotDecisionKind Kind,
    BardRepellingShotDecisionReason Reason)
{
    public bool ShouldCancelCast =>
        Kind == BardRepellingShotDecisionKind.CancelPowerfulShot;

    public bool ShouldDispatch => Kind == BardRepellingShotDecisionKind.Dispatch;

    public bool OwnsOpportunity => ShouldCancelCast || ShouldDispatch;
}

/// <summary>
/// Exact policy for the optional automatic BRD Mannstopper helper. The helper
/// may interrupt only the reviewed Powerful Shot cast, never an arbitrary
/// spell, and otherwise waits for the ordinary native action boundary. Target
/// selection and final identity/range checks remain runtime responsibilities.
/// </summary>
public static class BardRepellingShotRules
{
    public const uint BardJobId = AutomaticRecoveryShotCastRules.BardJobId;
    public const uint PowerfulShotActionId =
        AutomaticRecoveryShotCastRules.BardPowerfulShotActionId;
    public const uint RepellingShotActionId = 29_399;
    public const int NativeRangeYalms = 10;

    public static BardRepellingShotDecision Evaluate(
        BardRepellingShotObservation observation)
    {
        var hardBlocker = FindHardBlocker(observation);
        if (hardBlocker != BardRepellingShotDecisionReason.None)
            return Inactive(hardBlocker);

        if (!observation.TargetResolvedExactly ||
            !observation.Target.IsValid ||
            observation.Target == observation.LocalPlayer ||
            !observation.TargetAliveAndTargetable)
        {
            return Waiting(BardRepellingShotDecisionReason.InvalidTarget);
        }

        if (!observation.TargetInNativeRangeAndLineOfSight)
            return Waiting(BardRepellingShotDecisionReason.TargetOutOfRange);

        if (observation.ResolvedActionId != RepellingShotActionId)
            return Waiting(BardRepellingShotDecisionReason.ActionIdentityChanged);

        if (!observation.ActionOffCooldown || !observation.ActionResourcesReady)
            return Waiting(BardRepellingShotDecisionReason.ActionUnavailable);

        if (observation.LocalPlayerIsCasting || observation.CastActionId != 0)
        {
            if (!observation.BasicShotMetadataVerified)
            {
                return Waiting(
                    BardRepellingShotDecisionReason.BasicShotMetadataUnverified);
            }

            if (observation.LocalPlayerIsCasting &&
                observation.CastActionId == PowerfulShotActionId &&
                observation.AdjustedCastActionId == PowerfulShotActionId)
            {
                return new BardRepellingShotDecision(
                    BardRepellingShotDecisionKind.CancelPowerfulShot,
                    BardRepellingShotDecisionReason.None);
            }

            return Waiting(BardRepellingShotDecisionReason.OtherCastActive);
        }

        return observation.NativeBoundaryNearQueueable
            ? new BardRepellingShotDecision(
                BardRepellingShotDecisionKind.Dispatch,
                BardRepellingShotDecisionReason.None)
            : Waiting(BardRepellingShotDecisionReason.NativeBoundaryBusy);
    }

    private static BardRepellingShotDecisionReason FindHardBlocker(
        BardRepellingShotObservation observation)
    {
        if (!observation.Enabled)
            return BardRepellingShotDecisionReason.Disabled;
        if (!observation.SupportedContext)
            return BardRepellingShotDecisionReason.UnsupportedContext;
        if (!observation.LocalPlayer.IsValid ||
            !observation.LocalPlayerAliveAndTargetable)
        {
            return BardRepellingShotDecisionReason.InvalidLocalPlayer;
        }
        if (observation.LocalJobId != BardJobId)
            return BardRepellingShotDecisionReason.WrongJob;
        if (!observation.MetadataVerified)
            return BardRepellingShotDecisionReason.MetadataUnverified;
        if (!observation.GuardStateKnown)
            return BardRepellingShotDecisionReason.GuardUnknown;
        if (observation.GuardActive)
            return BardRepellingShotDecisionReason.GuardActive;
        if (!observation.TextInputStateKnown)
            return BardRepellingShotDecisionReason.TextInputUnknown;
        if (observation.TextInputActive)
            return BardRepellingShotDecisionReason.TextInputActive;
        if (observation.HigherPriorityClaimed)
            return BardRepellingShotDecisionReason.HigherPriorityClaimed;

        return BardRepellingShotDecisionReason.None;
    }

    private static BardRepellingShotDecision Inactive(
        BardRepellingShotDecisionReason reason) =>
        new(BardRepellingShotDecisionKind.Inactive, reason);

    private static BardRepellingShotDecision Waiting(
        BardRepellingShotDecisionReason reason) =>
        new(BardRepellingShotDecisionKind.Waiting, reason);
}
