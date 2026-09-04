using SeitonSense.Core;

internal static class SamuraiOgiCastProtectionSelfTests
{
    public static void RuntimeDisableReleasesProtection()
    {
        static bool CanMaintain(
            bool started = true,
            bool disposed = false,
            bool enabled = true,
            bool smartAction = true,
            bool loggedIn = true,
            SupportedPvPContext context = SupportedPvPContext.CrystallineConflict,
            bool denTesting = false) =>
            SamuraiOgiCastProtectionRules.CanMaintainCastProtection(
                started, disposed, enabled, smartAction, loggedIn, context, denTesting);

        True(CanMaintain(), "an enabled CC cast keeps its protection");
        False(CanMaintain(started: false), "stopping the runtime releases movement");
        False(CanMaintain(disposed: true), "disposal releases movement");
        False(CanMaintain(enabled: false), "disabling Seiton releases an existing cast");
        False(CanMaintain(smartAction: false), "disabling Smart Action releases an existing cast");
        False(CanMaintain(loggedIn: false), "logout releases an existing cast");
        False(CanMaintain(context: SupportedPvPContext.None), "leaving supported PvP releases movement");
        False(CanMaintain(context: SupportedPvPContext.WolvesDen), "turning Den testing off releases movement");
        True(CanMaintain(context: SupportedPvPContext.WolvesDen, denTesting: true),
            "enabled Den tests keep exact cast protection");
    }

    public static void ExactSamuraiReplayRetainsProtection()
    {
        const long tap = 42;
        static long Replay(
            long captured = tap,
            long current = tap,
            bool scope = true,
            bool smartProtection = true,
            uint raw = SamuraiSmartActionCastRules.OgiNamikiriActionId,
            uint resolved = SamuraiSmartActionCastRules.OgiNamikiriActionId) =>
            SamuraiOgiCastProtectionRules.GetExactReplayTapGeneration(
                scope, smartProtection, captured, current, raw, resolved);

        True(Replay() == tap,
            "an out-of-range Seiton SAM tap retains ownership when the exact delayed cast begins");
        True(Replay(raw: SamuraiSmartActionCastRules.TendoSetsugekkaCarrierActionId,
            resolved: SamuraiSmartActionCastRules.TendoSetsugekkaActionId) == tap,
            "a transformed Tendo carrier retains exact delayed ownership");
        True(Replay(captured: 0) == 0, "ordinary Smart Action does not gain movement protection");
        True(Replay(current: tap + 1) == 0, "a replaced SAM tap cannot acquire a later cast");
        True(Replay(scope: false) == 0, "an unproven native replay cannot own movement");
        True(Replay(smartProtection: false) == 0, "an ordinary direct replay cannot own movement");
        True(Replay(resolved: SamuraiSmartActionCastRules.OgiNamikiriFollowUpActionId) == 0,
            "an adjusted instant follow-up cannot inherit cast protection");
    }

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
