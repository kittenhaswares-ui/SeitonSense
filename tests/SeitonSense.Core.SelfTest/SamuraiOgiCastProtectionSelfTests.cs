using SeitonSense.Core;

internal static class SamuraiOgiCastProtectionSelfTests
{
    public static void ReviewedCastActionsAreExact()
    {
        True(SamuraiOgiCastProtectionRules.IsReviewedCastAction(
            SamuraiSmartActionCastRules.OgiNamikiriActionId), "Ogi is protected");
        True(SamuraiOgiCastProtectionRules.IsReviewedCastAction(
            SamuraiSmartActionCastRules.TendoSetsugekkaActionId), "Tendo Setsugekka is protected");
        False(SamuraiOgiCastProtectionRules.IsReviewedCastAction(
            SamuraiSmartActionCastRules.TendoSetsugekkaFollowUpActionId), "instant follow-up is not protected");
        False(SamuraiOgiCastProtectionRules.IsReviewedCastAction(0), "unknown action fails closed");
    }

    public static void MovementInputsAreNarrowAndTimingIsBounded()
    {
        foreach (var inputId in new uint[] { 321, 327, 348, 349, 350, 448, 451, 526, 671, 674 })
            True(SamuraiOgiCastProtectionRules.IsMovementInputId(inputId), $"movement {inputId}");

        foreach (var inputId in new uint[] { 0, 320, 328, 347, 351, 447, 452, 525, 527, 670, 675 })
            False(SamuraiOgiCastProtectionRules.IsMovementInputId(inputId), $"non-movement {inputId}");

        True(SamuraiOgiCastProtectionRules.StartPropagationMilliseconds > 0,
            "start propagation is positive");
        True(SamuraiOgiCastProtectionRules.MaximumLeaseMilliseconds >
             SamuraiOgiCastProtectionRules.StartPropagationMilliseconds,
            "maximum lease remains bounded beyond startup");

        False(
            SamuraiOgiCastProtectionRules.ShouldSuppressMovement(
                exactSeitonSamRequestInFlight: false,
                acceptedOwnedCastActive: false),
            "an armed Seiton SAM token alone never suppresses movement");
        True(
            SamuraiOgiCastProtectionRules.ShouldSuppressMovement(
                exactSeitonSamRequestInFlight: true,
                acceptedOwnedCastActive: false),
            "the consumed exact Seiton SAM request suppresses movement inside native Original");
        True(
            SamuraiOgiCastProtectionRules.ShouldSuppressMovement(
                exactSeitonSamRequestInFlight: false,
                acceptedOwnedCastActive: true),
            "the accepted owned cast keeps movement suppressed");
        False(
            SamuraiOgiCastProtectionRules.ShouldSuppressMovement(
                exactSeitonSamRequestInFlight: false,
                acceptedOwnedCastActive: false),
            "ordinary Smart Action never gains SAM movement suppression");

        True(
            SamuraiOgiCastProtectionRules.CanBeginExactInFlightRequest(
                castMetadataVerified: true,
                exactSeitonSamOwner: true,
                tapGeneration: 7,
                currentSeitonSamTapGeneration: 7,
                rawActionId: SamuraiSmartActionCastRules.OgiNamikiriActionId,
                resolvedActionId: SamuraiSmartActionCastRules.OgiNamikiriActionId,
                exactNetworkTarget: true),
            "the exact Ogi tap can own the synchronous native boundary");
        True(
            SamuraiOgiCastProtectionRules.CanBeginExactInFlightRequest(
                castMetadataVerified: true,
                exactSeitonSamOwner: true,
                tapGeneration: 8,
                currentSeitonSamTapGeneration: 8,
                rawActionId: SamuraiSmartActionCastRules.TendoSetsugekkaCarrierActionId,
                resolvedActionId: SamuraiSmartActionCastRules.TendoSetsugekkaActionId,
                exactNetworkTarget: true),
            "the exact transformed Tendo tap can own the synchronous native boundary");
        False(
            SamuraiOgiCastProtectionRules.CanBeginExactInFlightRequest(
                castMetadataVerified: true,
                exactSeitonSamOwner: false,
                tapGeneration: 7,
                currentSeitonSamTapGeneration: 7,
                rawActionId: SamuraiSmartActionCastRules.OgiNamikiriActionId,
                resolvedActionId: SamuraiSmartActionCastRules.OgiNamikiriActionId,
                exactNetworkTarget: true),
            "ordinary Smart Action cannot own the synchronous SAM boundary");
        False(
            SamuraiOgiCastProtectionRules.CanBeginExactInFlightRequest(
                castMetadataVerified: true,
                exactSeitonSamOwner: true,
                tapGeneration: 6,
                currentSeitonSamTapGeneration: 7,
                rawActionId: SamuraiSmartActionCastRules.OgiNamikiriActionId,
                resolvedActionId: SamuraiSmartActionCastRules.OgiNamikiriActionId,
                exactNetworkTarget: true),
            "a stale Seiton SAM tap cannot own the synchronous boundary");
        False(
            SamuraiOgiCastProtectionRules.CanBeginExactInFlightRequest(
                castMetadataVerified: true,
                exactSeitonSamOwner: true,
                tapGeneration: 7,
                currentSeitonSamTapGeneration: 7,
                rawActionId: SamuraiSmartActionCastRules.OgiNamikiriActionId,
                resolvedActionId: SamuraiSmartActionCastRules.OgiNamikiriActionId,
                exactNetworkTarget: false),
            "a missing exact target cannot own the synchronous boundary");
        False(
            SamuraiOgiCastProtectionRules.CanBeginExactInFlightRequest(
                castMetadataVerified: true,
                exactSeitonSamOwner: true,
                tapGeneration: 7,
                currentSeitonSamTapGeneration: 7,
                rawActionId: SamuraiSmartActionCastRules.OgiNamikiriFollowUpActionId,
                resolvedActionId: SamuraiSmartActionCastRules.OgiNamikiriFollowUpActionId,
                exactNetworkTarget: true),
            "an instant follow-up cannot own the synchronous cast boundary");
        False(
            SamuraiOgiCastProtectionRules.CanBeginExactInFlightRequest(
                castMetadataVerified: false,
                exactSeitonSamOwner: true,
                tapGeneration: 7,
                currentSeitonSamTapGeneration: 7,
                rawActionId: SamuraiSmartActionCastRules.OgiNamikiriActionId,
                resolvedActionId: SamuraiSmartActionCastRules.OgiNamikiriActionId,
                exactNetworkTarget: true),
            "unverified cast metadata cannot own the synchronous boundary");
    }

    private static void True(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException($"Expected true: {label}");
    }

    private static void False(bool condition, string label) => True(!condition, label);
}
