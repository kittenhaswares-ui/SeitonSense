namespace SeitonSense.Core;

/// <summary>
/// Closed identities for the short movement/action shield owned by one
/// /seitonsam cast. Native cast loss, crowd control and range remain game-owned.
/// </summary>
public static class SamuraiOgiCastProtectionRules
{
    public const long StartPropagationMilliseconds = 750;
    public const long MaximumLeaseMilliseconds = 4_000;

    // Experimental lead relative to the observed cast bar, not a server
    // snapshot constant. The earlier default precedes reported .25-.33s
    // interruptions and the roughly .5s slidecast estimate; see timing review.
    public const float MinimumFacingLeadSeconds = 0.05f;
    public const float MaximumFacingLeadSeconds = 1.00f;
    public const float DefaultFacingLeadSeconds = 0.60f;

    public static float MigrateLegacyFacingLead(float seconds) =>
        seconds == 0.15f ? DefaultFacingLeadSeconds : seconds;

    public static bool IsReviewedCastAction(uint resolvedActionId) =>
        resolvedActionId is SamuraiSmartActionCastRules.OgiNamikiriActionId or
            SamuraiSmartActionCastRules.TendoSetsugekkaActionId;

    public static bool ShouldSuppressMovement(
        bool exactSeitonSamRequestInFlight,
        bool acceptedOwnedCastActive) =>
        exactSeitonSamRequestInFlight ||
        acceptedOwnedCastActive;

    public static bool CanMaintainCastProtection(
        bool started,
        bool disposed,
        bool pluginEnabled,
        bool smartActionEnabled,
        bool loggedIn,
        SupportedPvPContext context,
        bool wolvesDenTestingEnabled) =>
        started && !disposed && pluginEnabled && smartActionEnabled && loggedIn &&
        SmartActionContextRules.IsSupported(context, wolvesDenTestingEnabled);

    /// <summary>
    /// A delayed exact replay retains movement protection only when the
    /// original tap belonged to /seitonsam and still owns the same generation.
    /// Ordinary Smart Action replays and instant follow-ups never acquire it.
    /// </summary>
    public static long GetExactReplayTapGeneration(
        bool exactReplayScope,
        bool requiresSmartActionProtection,
        long capturedSamuraiTapGeneration,
        long currentSamuraiTapGeneration,
        uint rawActionId,
        uint resolvedActionId) =>
        exactReplayScope && requiresSmartActionProtection &&
        capturedSamuraiTapGeneration > 0 &&
        capturedSamuraiTapGeneration == currentSamuraiTapGeneration &&
        SamuraiSmartActionCastRules.IsReviewedBaseCastPair(rawActionId, resolvedActionId)
            ? capturedSamuraiTapGeneration
            : 0;

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

    // InputManager's gameplay-control codes are distinct from InputData's
    // remappable UI input IDs above. Camera/target/confirm codes stay native.
    public static bool IsMovementControlCode(uint inputCode) =>
        inputCode is >= 107 and <= 110 or >= 112 and <= 117;
}
