namespace SeitonSense.Core;

public enum ReactiveCounterCcExecutionShape : byte
{
    None = 0,
    DirectTarget = 1,
    AdjustedComboTarget = 2,
    LineAoeTargeted = 3,
    GroundDashThenDirect = 4,
}

public readonly record struct ReactiveCounterCcProfile(
    uint JobId,
    uint ActionId,
    ushort ExpectedStatusId,
    ReactiveCounterCcExecutionShape ExecutionShape,
    float NativeMaximumRangeYalms,
    bool CannotExecuteWhileBound)
{
    public bool IsValid =>
        JobId != 0 &&
        MiracleInterceptConfirmationRules.ExpectedStatusForAction(ActionId) ==
        ExpectedStatusId &&
        ExpectedStatusId != 0 &&
        ExecutionShape != ReactiveCounterCcExecutionShape.None &&
        float.IsFinite(NativeMaximumRangeYalms) &&
        NativeMaximumRangeYalms > 0f;
}

/// <summary>
/// Explicitly reviewed counter-CC profiles. This deliberately does not infer
/// actions from localized descriptions: proc, line-AoE, movement, and target
/// semantics must stay auditable per action.
/// </summary>
public static class ReactiveCounterCcProfileRules
{
    public const uint PaladinJobId = 19;
    public const uint BardJobId = 23;
    public const uint WhiteMageJobId = 24;
    public const uint NinjaJobId = 30;
    public const uint RedMageJobId = 35;
    public const uint SamuraiJobId = 34;

    public const float MiracleOfNatureMaximumRangeYalms = 10f;
    public const float SilentNocturneMaximumRangeYalms = 20f;
    public const float RaijuMaximumRangeYalms = 20f;
    public const float InterveneMaximumRangeYalms = 20f;
    public const float ResolutionMaximumRangeYalms = 25f;
    public const uint ResolutionIconId = 9_686;
    public const float MinimumConfiguredInterveneRangeYalms = 1f;

    public static ReactiveCounterCcProfile? Get(uint actionId) => actionId switch
    {
        MiracleInterceptConfirmationRules.MiracleOfNatureActionId => new(
            WhiteMageJobId,
            actionId,
            MiracleInterceptConfirmationRules.MiracleOfNatureStatusId,
            ReactiveCounterCcExecutionShape.DirectTarget,
            MiracleOfNatureMaximumRangeYalms,
            CannotExecuteWhileBound: false),
        MiracleInterceptConfirmationRules.SilentNocturneActionId => new(
            BardJobId,
            actionId,
            MiracleInterceptConfirmationRules.SilenceStatusId,
            ReactiveCounterCcExecutionShape.DirectTarget,
            SilentNocturneMaximumRangeYalms,
            CannotExecuteWhileBound: false),
        MiracleInterceptConfirmationRules.ForkedRaijuActionId or
            MiracleInterceptConfirmationRules.FleetingRaijuActionId => new(
                NinjaJobId,
                actionId,
                MiracleInterceptConfirmationRules.StunStatusId,
                ReactiveCounterCcExecutionShape.AdjustedComboTarget,
                RaijuMaximumRangeYalms,
                CannotExecuteWhileBound: true),
        MiracleInterceptConfirmationRules.InterveneActionId => new(
            PaladinJobId,
            actionId,
            MiracleInterceptConfirmationRules.StunStatusId,
            ReactiveCounterCcExecutionShape.DirectTarget,
            InterveneMaximumRangeYalms,
            CannotExecuteWhileBound: true),
        MiracleInterceptConfirmationRules.MineuchiActionId => new(
            SamuraiJobId,
            actionId,
            MiracleInterceptConfirmationRules.StunStatusId,
            ReactiveCounterCcExecutionShape.GroundDashThenDirect,
            SamuraiReactiveCounterCcRules.MineuchiMaximumRangeYalms,
            CannotExecuteWhileBound: false),
        MiracleInterceptConfirmationRules.ResolutionActionId => new(
            RedMageJobId,
            actionId,
            MiracleInterceptConfirmationRules.SilenceStatusId,
            ReactiveCounterCcExecutionShape.LineAoeTargeted,
            ResolutionMaximumRangeYalms,
            CannotExecuteWhileBound: false),
        _ => null,
    };

    public static float NormalizeInterveneMaximumRangeYalms(float configured) =>
        float.IsFinite(configured)
            ? Math.Clamp(
                configured,
                MinimumConfiguredInterveneRangeYalms,
                InterveneMaximumRangeYalms)
            : InterveneMaximumRangeYalms;

    public static bool IsSupportedContext(
        bool isCrystallineConflict,
        bool isWolvesDenTesting) =>
        isCrystallineConflict || isWolvesDenTesting;

    /// <summary>
    /// PLD Intervene and RDM Resolution were reviewed only as protection-end
    /// follow-ups. They must never inherit the older WHM/BRD/NIN urgent-LB
    /// startup matrix merely because all profiles share one dispatcher.
    /// </summary>
    public static bool IsThreatSupportedByAction(
        uint actionId,
        MiracleInterceptThreatKind threat) =>
        actionId is MiracleInterceptConfirmationRules.InterveneActionId or
            MiracleInterceptConfirmationRules.ResolutionActionId
            ? threat is MiracleInterceptThreatKind.PostPurifyCrowdControl or
                MiracleInterceptThreatKind.PostGuardCrowdControl
            : true;

    public static bool IsExactWolvesDenCurrentTarget(
        uint observedActorEntityId,
        ulong expectedGameObjectId,
        uint expectedEntityId,
        uint expectedJobId,
        ulong currentHardTargetGameObjectId,
        uint currentHardTargetEntityId,
        uint currentHardTargetJobId) =>
        MiracleInterceptConfirmationRules.IsValidEntityId(observedActorEntityId) &&
        TargetHighlightRules.IsValidGameObjectId(expectedGameObjectId) &&
        MiracleInterceptConfirmationRules.IsValidEntityId(expectedEntityId) &&
        expectedJobId != 0 &&
        observedActorEntityId == expectedEntityId &&
        currentHardTargetGameObjectId == expectedGameObjectId &&
        currentHardTargetEntityId == expectedEntityId &&
        currentHardTargetJobId == expectedJobId;
}
