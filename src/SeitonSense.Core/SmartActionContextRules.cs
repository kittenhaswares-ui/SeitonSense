namespace SeitonSense.Core;

/// <summary>
/// Closed context policy for the authored /smartaction one-shot. Crystalline
/// Conflict is always supported; Wolves' Den is a deliberate local test path
/// and therefore additionally requires its existing configuration opt-in.
/// </summary>
public static class SmartActionContextRules
{
    public static bool IsSupported(
        SupportedPvPContext context,
        bool wolvesDenTestingEnabled) =>
        context == SupportedPvPContext.CrystallineConflict ||
        context == SupportedPvPContext.WolvesDen && wolvesDenTestingEnabled;

    public static bool CanUseSmartTargetRanking(
        SupportedPvPContext context) =>
        context == SupportedPvPContext.CrystallineConflict;

    public static bool CanUseExactVisibleTargetTestFallback(
        SupportedPvPContext context,
        bool wolvesDenTestingEnabled,
        bool combatPriorityMode) =>
        context == SupportedPvPContext.WolvesDen &&
        wolvesDenTestingEnabled &&
        combatPriorityMode;

    public static bool CanUseSameCallVisibleTargetFallback(
        SupportedPvPContext context,
        bool wolvesDenTestingEnabled,
        bool combatPriorityMode,
        bool redirectApplied,
        bool rankedWinnerSelected,
        bool exactCurrentHardTarget) =>
        CanUseExactVisibleTargetTestFallback(
            context,
            wolvesDenTestingEnabled,
            combatPriorityMode) &&
        !redirectApplied &&
        !rankedWinnerSelected &&
        exactCurrentHardTarget;
}
