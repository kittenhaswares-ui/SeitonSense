using SeitonSense.Core;

internal static class SmartActionContextSelfTests
{
    public static void WolvesDenRequiresItsExactTestOptIn()
    {
        True(
            SmartActionContextRules.IsSupported(
                SupportedPvPContext.CrystallineConflict,
                wolvesDenTestingEnabled: false),
            "CC remains supported without the test toggle");
        False(
            SmartActionContextRules.IsSupported(
                SupportedPvPContext.WolvesDen,
                wolvesDenTestingEnabled: false),
            "Wolves' Den remains off without its exact toggle");
        True(
            SmartActionContextRules.IsSupported(
                SupportedPvPContext.WolvesDen,
                wolvesDenTestingEnabled: true),
            "Wolves' Den is admitted only by its exact toggle");
        False(
            SmartActionContextRules.IsSupported(
                SupportedPvPContext.None,
                wolvesDenTestingEnabled: true),
            "unknown contexts remain closed");
    }

    public static void WolvesDenUsesOnlyCombatPriorityVisibleTargetFallback()
    {
        True(
            SmartActionContextRules.CanUseSmartTargetRanking(
                SupportedPvPContext.CrystallineConflict),
            "CC keeps ranked S-slot Smart Action");
        False(
            SmartActionContextRules.CanUseSmartTargetRanking(
                SupportedPvPContext.WolvesDen),
            "Wolves' Den never invents S-slot ranking");
        True(
            SmartActionContextRules.CanUseExactVisibleTargetTestFallback(
                SupportedPvPContext.WolvesDen,
                wolvesDenTestingEnabled: true,
                combatPriorityMode: true),
            "ordinary Smart Action may use exact visible target in Den");
        False(
            SmartActionContextRules.CanUseExactVisibleTargetTestFallback(
                SupportedPvPContext.WolvesDen,
                wolvesDenTestingEnabled: true,
                combatPriorityMode: false),
            "Seiton Far remains closed in Den");
        False(
            SmartActionContextRules.CanUseExactVisibleTargetTestFallback(
                SupportedPvPContext.CrystallineConflict,
                wolvesDenTestingEnabled: true,
                combatPriorityMode: true),
            "CC never enters the Den fallback lane");

        True(
            SmartActionContextRules.CanUseSameCallVisibleTargetFallback(
                SupportedPvPContext.WolvesDen,
                wolvesDenTestingEnabled: true,
                combatPriorityMode: true,
                redirectApplied: false,
                rankedWinnerSelected: false,
                exactCurrentHardTarget: true),
            "the first visible Den target call can own the fallback");
        False(
            SmartActionContextRules.CanUseSameCallVisibleTargetFallback(
                SupportedPvPContext.WolvesDen,
                wolvesDenTestingEnabled: true,
                combatPriorityMode: true,
                redirectApplied: false,
                rankedWinnerSelected: false,
                exactCurrentHardTarget: false),
            "a hidden or changed target cannot own the same-call fallback");
    }

    public static void WolvesDenVisibleTargetShapeEligibilityIsExact()
    {
        foreach (var shape in new[]
                 {
                     SmartActionAttackShape.DirectSingleTarget,
                     SmartActionAttackShape.TargetCenteredCircle,
                     SmartActionAttackShape.UnsupportedAreaOfEffect,
                 })
        {
            True(
                SmartActionContextRules.CanInspectExactVisibleTargetTestFallback(
                    SupportedPvPContext.WolvesDen,
                    wolvesDenTestingEnabled: true,
                    combatPriorityMode: true,
                    shape),
                $"reviewed {shape} keeps the exact visible Wolves' Den cast path");
        }
        False(
            SmartActionContextRules.CanInspectExactVisibleTargetTestFallback(
                SupportedPvPContext.WolvesDen,
                wolvesDenTestingEnabled: true,
                combatPriorityMode: true,
                (SmartActionAttackShape)byte.MaxValue),
            "a future attack shape stays closed");

        False(
            SmartActionContextRules.CanInspectExactVisibleTargetTestFallback(
                SupportedPvPContext.CrystallineConflict,
                wolvesDenTestingEnabled: true,
                combatPriorityMode: true,
                SmartActionAttackShape.TargetCenteredCircle),
            "the Den-only shape rule cannot own a CC cast");
        False(
            SmartActionContextRules.CanInspectExactVisibleTargetTestFallback(
                SupportedPvPContext.WolvesDen,
                wolvesDenTestingEnabled: false,
                combatPriorityMode: true,
                SmartActionAttackShape.TargetCenteredCircle),
            "the Den test toggle remains mandatory");
        False(
            SmartActionContextRules.CanInspectExactVisibleTargetTestFallback(
                SupportedPvPContext.WolvesDen,
                wolvesDenTestingEnabled: true,
                combatPriorityMode: false,
                SmartActionAttackShape.TargetCenteredCircle),
            "non-combat Smart Action modes remain closed in Den");
    }

    public static void NativeSelectedTargetCarrierRequiresResolvedHardTarget()
    {
        True(
            SmartActionContextRules.IsExactCurrentTargetCarrier(
                SupportedPvPContext.WolvesDen,
                wolvesDenTestingEnabled: true,
                combatPriorityMode: true,
                incomingIsNativeSelectedTargetCarrier: true,
                exactNativeHardTargetResolved: true,
                explicitTargetMatchesNativeHardTarget: false),
            "zero/default selected-target carrier uses one resolved native hard target");
        False(
            SmartActionContextRules.IsExactCurrentTargetCarrier(
                SupportedPvPContext.WolvesDen,
                wolvesDenTestingEnabled: true,
                combatPriorityMode: true,
                incomingIsNativeSelectedTargetCarrier: true,
                exactNativeHardTargetResolved: false,
                explicitTargetMatchesNativeHardTarget: false),
            "zero/default carrier cannot pass without a resolved native hard target");
        False(
            SmartActionContextRules.IsExactCurrentTargetCarrier(
                SupportedPvPContext.CrystallineConflict,
                wolvesDenTestingEnabled: true,
                combatPriorityMode: true,
                incomingIsNativeSelectedTargetCarrier: true,
                exactNativeHardTargetResolved: true,
                explicitTargetMatchesNativeHardTarget: false),
            "CC and other lanes cannot reinterpret a missing carrier as current target");
        False(
            SmartActionContextRules.IsExactCurrentTargetCarrier(
                SupportedPvPContext.WolvesDen,
                wolvesDenTestingEnabled: false,
                combatPriorityMode: true,
                incomingIsNativeSelectedTargetCarrier: true,
                exactNativeHardTargetResolved: true,
                explicitTargetMatchesNativeHardTarget: false),
            "the Den test toggle still owns native selected-target carriers");
        False(
            SmartActionContextRules.IsExactCurrentTargetCarrier(
                SupportedPvPContext.WolvesDen,
                wolvesDenTestingEnabled: true,
                combatPriorityMode: false,
                incomingIsNativeSelectedTargetCarrier: true,
                exactNativeHardTargetResolved: true,
                explicitTargetMatchesNativeHardTarget: false),
            "non-combat Smart Action modes cannot reinterpret a carrier");

        True(
            SmartActionContextRules.IsExactCurrentTargetCarrier(
                SupportedPvPContext.CrystallineConflict,
                wolvesDenTestingEnabled: false,
                combatPriorityMode: true,
                incomingIsNativeSelectedTargetCarrier: false,
                exactNativeHardTargetResolved: true,
                explicitTargetMatchesNativeHardTarget: true),
            "explicit object/entity identity may match the resolved native hard target");
        False(
            SmartActionContextRules.IsExactCurrentTargetCarrier(
                SupportedPvPContext.CrystallineConflict,
                wolvesDenTestingEnabled: false,
                combatPriorityMode: true,
                incomingIsNativeSelectedTargetCarrier: false,
                exactNativeHardTargetResolved: true,
                explicitTargetMatchesNativeHardTarget: false),
            "an unrelated explicit target cannot borrow the current hard target");
        False(
            SmartActionContextRules.IsExactCurrentTargetCarrier(
                SupportedPvPContext.CrystallineConflict,
                wolvesDenTestingEnabled: false,
                combatPriorityMode: true,
                incomingIsNativeSelectedTargetCarrier: false,
                exactNativeHardTargetResolved: false,
                explicitTargetMatchesNativeHardTarget: true),
            "an explicit comparison cannot pass when native target resolution failed");
    }

    private static void True(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static void False(bool value, string message) => True(!value, message);
}
