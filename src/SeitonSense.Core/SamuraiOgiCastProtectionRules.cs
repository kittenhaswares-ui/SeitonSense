namespace SeitonSense.Core;

/// <summary>
/// Closed identities for the short movement/action shield owned by one
/// /seitonsam cast. Native cast loss, crowd control and range remain game-owned.
/// </summary>
public static class SamuraiOgiCastProtectionRules
{
    public const long StartPropagationMilliseconds = 750;
    public const long MaximumLeaseMilliseconds = 4_000;

    public static bool IsReviewedCastAction(uint resolvedActionId) =>
        resolvedActionId is SamuraiSmartActionCastRules.OgiNamikiriActionId or
            SamuraiSmartActionCastRules.TendoSetsugekkaActionId;

    public static bool ShouldSuppressMovement(
        bool exactSeitonSamRequestInFlight,
        bool acceptedOwnedCastActive) =>
        exactSeitonSamRequestInFlight ||
        acceptedOwnedCastActive;

    public static bool CanBeginExactInFlightRequest(
        bool castMetadataVerified,
        bool exactSeitonSamOwner,
        long tapGeneration,
        long currentSeitonSamTapGeneration,
        uint rawActionId,
        uint resolvedActionId,
        bool exactNetworkTarget) =>
        castMetadataVerified &&
        exactSeitonSamOwner &&
        tapGeneration > 0 &&
        tapGeneration == currentSeitonSamTapGeneration &&
        SamuraiSmartActionCastRules.IsReviewedBaseCastPair(
            rawActionId,
            resolvedActionId) &&
        exactNetworkTarget;

    public static bool IsMovementInputId(uint inputId) =>
        inputId is >= 321 and <= 327 or // keyboard/remapped movement commands
            348 or                     // jump
            349 or 350 or              // keyboard/gamepad autorun toggles
            >= 448 and <= 451 or        // retained/vertical movement commands
            526 or                     // gamepad jump / cancel cast
            >= 671 and <= 674;          // digital left-stick directions
}
