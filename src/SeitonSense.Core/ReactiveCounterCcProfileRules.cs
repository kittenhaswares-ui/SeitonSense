namespace SeitonSense.Core;

public enum ReactiveCounterCcExecutionShape : byte
{
    None = 0,
    DirectTarget = 1,
    AdjustedComboTarget = 2,
    LineAoeTargeted = 3,
    GroundDashThenDirect = 4,
    TargetCenteredAoe = 5,
}

public readonly record struct ReactiveCounterCcProfile(
    uint JobId,
    uint ActionId,
    ushort ExpectedStatusId,
    ReactiveCounterCcExecutionShape ExecutionShape,
    float NativeMaximumRangeYalms,
    bool CannotExecuteWhileBound)
{
    public bool UsesMainGlobalCooldown { get; init; }

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
    public const uint BlackMageJobId = 25;
    public const uint RedMageJobId = 35;
    public const uint SamuraiJobId = 34;

    public const float MiracleOfNatureMaximumRangeYalms = 10f;
    public const float SilentNocturneMaximumRangeYalms = 20f;
    public const uint NinjaComboCarrierActionId = 29_500;
    public const float RaijuMaximumRangeYalms = 20f;
    public const float InterveneMaximumRangeYalms = 20f;
    public const float ResolutionMaximumRangeYalms = 25f;
    public const uint ResolutionIconId = 9_686;
    public const uint ForteCarrierActionId = 41_496;
    public const uint ViceOfThornsIconId = 9_063;
    public const uint ViceOfThornsProcStatusRowId = 242;
    public const uint ThornedFlourishStatusId = 4_321;
    public const uint ThornedFlourishStatusIconId = 213_411;
    public const float ViceOfThornsMaximumRangeYalms = 25f;
    public const uint SoulResonanceCarrierActionId = 29_662;
    public const uint FrostStarIconId = 9_056;
    public const uint FrostStarProcStatusRowId = 241;
    public const uint ElementalStarStatusId = 4_317;
    public const uint ElementalStarStatusIconId = 214_736;
    public const float FrostStarMaximumRangeYalms = 25f;
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
                CannotExecuteWhileBound: true)
            {
                UsesMainGlobalCooldown = true,
            },
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
            CannotExecuteWhileBound: false)
        {
            UsesMainGlobalCooldown = true,
        },
        MiracleInterceptConfirmationRules.ViceOfThornsActionId => new(
            RedMageJobId,
            actionId,
            MiracleInterceptConfirmationRules.StunStatusId,
            ReactiveCounterCcExecutionShape.TargetCenteredAoe,
            ViceOfThornsMaximumRangeYalms,
            CannotExecuteWhileBound: false),
        MiracleInterceptConfirmationRules.FrostStarActionId => new(
            BlackMageJobId,
            actionId,
            MiracleInterceptConfirmationRules.DeepFreezeStatusId,
            ReactiveCounterCcExecutionShape.TargetCenteredAoe,
            FrostStarMaximumRangeYalms,
            CannotExecuteWhileBound: false)
        {
            UsesMainGlobalCooldown = true,
        },
        _ => null,
    };

    public static uint CarrierActionId(uint executableActionId) =>
        executableActionId switch
        {
            MiracleInterceptConfirmationRules.ViceOfThornsActionId =>
                ForteCarrierActionId,
            MiracleInterceptConfirmationRules.FrostStarActionId =>
                SoulResonanceCarrierActionId,
            _ => executableActionId,
        };

    public static bool UsesMainGlobalCooldown(uint actionId) =>
        actionId == NinjaComboCarrierActionId ||
        Get(actionId)?.UsesMainGlobalCooldown == true;

    public static uint SelectRedMageCounterAction(
        bool viceEnabled,
        bool viceMetadataVerified,
        uint adjustedForteActionId,
        bool resolutionEnabled,
        bool resolutionMetadataVerified)
    {
        if (viceEnabled &&
            viceMetadataVerified &&
            adjustedForteActionId ==
            MiracleInterceptConfirmationRules.ViceOfThornsActionId)
        {
            return MiracleInterceptConfirmationRules.ViceOfThornsActionId;
        }

        if (resolutionEnabled && resolutionMetadataVerified)
            return MiracleInterceptConfirmationRules.ResolutionActionId;

        // Retain the episode/capture lane for a Vice-only configuration while
        // the proc is absent. Native readiness still fails closed until Forte
        // exposes the exact executable action.
        return viceEnabled && viceMetadataVerified
            ? MiracleInterceptConfirmationRules.ViceOfThornsActionId
            : 0;
    }

    public static uint SelectBlackMageCounterAction(
        bool frostStarEnabled,
        bool frostStarMetadataVerified) =>
        frostStarEnabled && frostStarMetadataVerified
            ? MiracleInterceptConfirmationRules.FrostStarActionId
            : 0;

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
            MiracleInterceptConfirmationRules.ResolutionActionId or
            MiracleInterceptConfirmationRules.ViceOfThornsActionId or
            MiracleInterceptConfirmationRules.FrostStarActionId
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
