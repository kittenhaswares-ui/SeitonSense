namespace SeitonSense.Core;

public static class MachinistLimitBreakMarkerRules
{
    public const uint MarksmanSpiteActionId = 29_415;
    public const byte TargetMarkerEffectType = 0x1B;
    public const int MaximumTargets = 32;

    public static bool IsExactEarlyTargetMarker(
        uint actionId,
        int targetCount,
        bool localPlayerIsTarget,
        byte firstEffectType,
        bool hasAdditionalEffects) =>
        actionId == MarksmanSpiteActionId &&
        targetCount is >= 1 and <= MaximumTargets &&
        localPlayerIsTarget &&
        firstEffectType == TargetMarkerEffectType &&
        !hasAdditionalEffects;
}
