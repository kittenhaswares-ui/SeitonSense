namespace SeitonSense.Core;

/// <summary>
/// Closed context policy for the authored /smartaction one-shot. Crystalline
/// Conflict is always supported; Wolves' Den is a deliberate local test path
/// and therefore additionally requires its existing configuration opt-in.
/// </summary>
public static class SmartActionContextRules
{
    public const ulong NativeSelectedTargetSentinel = 0xE0000000UL;

    public static bool IsNativeSelectedTargetCarrier(ulong targetId) =>
        targetId is 0 or NativeSelectedTargetSentinel;

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

    /// <summary>
    /// Shapes which can be protection-checked against the exact visible
    /// Wolves' Den target. Every current shape has a closed protection policy:
    /// direct attacks are candidate-local, target-centered circles use exact
    /// hitbox geometry, and unsupported areas retain the conservative global
    /// incidental-Chiten veto. Future enum values remain closed.
    /// </summary>
    public static bool CanInspectExactVisibleTargetTestFallback(
        SupportedPvPContext context,
        bool wolvesDenTestingEnabled,
        bool combatPriorityMode,
        SmartActionAttackShape attackShape) =>
        CanUseExactVisibleTargetTestFallback(
            context,
            wolvesDenTestingEnabled,
            combatPriorityMode) &&
        attackShape switch
        {
            SmartActionAttackShape.DirectSingleTarget => true,
            SmartActionAttackShape.TargetCenteredCircle => true,
            SmartActionAttackShape.UnsupportedAreaOfEffect => true,
            _ => false,
        };

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

    /// <summary>
    /// A native zero/default target is a valid selected-target carrier only
    /// when the client still exposes one exact resolvable hard target. Explicit
    /// target IDs retain their existing exact-identity comparison.
    /// </summary>
    public static bool IsExactCurrentTargetCarrier(
        SupportedPvPContext context,
        bool wolvesDenTestingEnabled,
        bool combatPriorityMode,
        bool incomingIsNativeSelectedTargetCarrier,
        bool exactNativeHardTargetResolved,
        bool explicitTargetMatchesNativeHardTarget) =>
        exactNativeHardTargetResolved &&
        (incomingIsNativeSelectedTargetCarrier
            ? CanUseExactVisibleTargetTestFallback(
                context,
                wolvesDenTestingEnabled,
                combatPriorityMode)
            : explicitTargetMatchesNativeHardTarget);
}
