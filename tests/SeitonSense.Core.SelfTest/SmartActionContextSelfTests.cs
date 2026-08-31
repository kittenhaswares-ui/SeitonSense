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

    private static void True(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static void False(bool value, string message) => True(!value, message);
}
