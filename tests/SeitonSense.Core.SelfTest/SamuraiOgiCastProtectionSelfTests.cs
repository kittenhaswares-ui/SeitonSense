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
        foreach (var inputId in new uint[] { 321, 327, 348, 349, 350, 448, 451, 671, 674 })
            True(SamuraiOgiCastProtectionRules.IsMovementInputId(inputId), $"movement {inputId}");

        foreach (var inputId in new uint[] { 0, 320, 328, 347, 351, 447, 452, 670, 675 })
            False(SamuraiOgiCastProtectionRules.IsMovementInputId(inputId), $"non-movement {inputId}");

        True(SamuraiOgiCastProtectionRules.StartPropagationMilliseconds > 0,
            "start propagation is positive");
        True(SamuraiOgiCastProtectionRules.MaximumLeaseMilliseconds >
             SamuraiOgiCastProtectionRules.StartPropagationMilliseconds,
            "maximum lease remains bounded beyond startup");
    }

    private static void True(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException($"Expected true: {label}");
    }

    private static void False(bool condition, string label) => True(!condition, label);
}
