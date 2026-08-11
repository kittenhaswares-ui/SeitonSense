using SeitonSense.Core;

internal static class MachinistLimitBreakMarkerSelfTests
{
    internal static void ExactMarkerIsAccepted()
    {
        True(MachinistLimitBreakMarkerRules.IsExactEarlyTargetMarker(
            MachinistLimitBreakMarkerRules.MarksmanSpiteActionId,
            1,
            localPlayerIsTarget: true,
            MachinistLimitBreakMarkerRules.TargetMarkerEffectType,
            hasAdditionalEffects: false));
    }

    internal static void DamageAndAmbiguousPacketsFailClosed()
    {
        False(MachinistLimitBreakMarkerRules.IsExactEarlyTargetMarker(
            MachinistLimitBreakMarkerRules.MarksmanSpiteActionId,
            1,
            localPlayerIsTarget: true,
            MachinistLimitBreakMarkerRules.TargetMarkerEffectType,
            hasAdditionalEffects: true));
        False(MachinistLimitBreakMarkerRules.IsExactEarlyTargetMarker(
            MachinistLimitBreakMarkerRules.MarksmanSpiteActionId,
            1,
            localPlayerIsTarget: true,
            firstEffectType: 3,
            hasAdditionalEffects: false));
        False(MachinistLimitBreakMarkerRules.IsExactEarlyTargetMarker(
            actionId: 1,
            targetCount: 1,
            localPlayerIsTarget: true,
            MachinistLimitBreakMarkerRules.TargetMarkerEffectType,
            hasAdditionalEffects: false));
        False(MachinistLimitBreakMarkerRules.IsExactEarlyTargetMarker(
            MachinistLimitBreakMarkerRules.MarksmanSpiteActionId,
            33,
            localPlayerIsTarget: true,
            MachinistLimitBreakMarkerRules.TargetMarkerEffectType,
            hasAdditionalEffects: false));
        False(MachinistLimitBreakMarkerRules.IsExactEarlyTargetMarker(
            MachinistLimitBreakMarkerRules.MarksmanSpiteActionId,
            1,
            localPlayerIsTarget: false,
            MachinistLimitBreakMarkerRules.TargetMarkerEffectType,
            hasAdditionalEffects: false));
    }

    private static void True(bool value)
    {
        if (!value) throw new InvalidOperationException("Expected true.");
    }

    private static void False(bool value)
    {
        if (value) throw new InvalidOperationException("Expected false.");
    }
}
