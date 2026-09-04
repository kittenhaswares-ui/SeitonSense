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

    /// <summary>
    /// The Wolves' Den Smart Action test path may prove its exact visible
    /// target directly from the native hard target. The PvP duel-manager slot
    /// is deliberately not part of this admission rule because it is not
    /// populated consistently for every duel frame.
    /// </summary>
    public static bool IsEligibleExactVisibleWolvesDenTarget(
        bool isPlayerCharacter,
        bool hostileFlag,
        bool exactVerifiedStrikingDummy) =>
        exactVerifiedStrikingDummy || isPlayerCharacter && hostileFlag;

    /// <summary>
    /// A selected Wolves' Den player is hostile when either the object-table
    /// flag says so or the native duel manager independently identifies that
    /// same exact actor. This never admits an unrelated selected player.
    /// </summary>
    public static bool HasExactWolvesDenDuelHostilityProof(
        bool hostileFlag,
        bool exactNativeDuelOpponent) =>
        hostileFlag || exactNativeDuelOpponent;

    /// <summary>
    /// Object-ID and Entity-ID indexes are independent views of one native
    /// actor. One exact live view is sufficient while the other is briefly
    /// absent; if both are present, both must identify the same actor.
    /// </summary>
    public static bool HasCanonicalNativeTargetIdentity(
        bool objectLookupPresent,
        bool objectLookupMatches,
        bool entityLookupPresent,
        bool entityLookupMatches) =>
        objectLookupPresent && entityLookupPresent
            ? objectLookupMatches && entityLookupMatches
            : objectLookupMatches || entityLookupMatches;

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
    /// A Wolves' Den macro can emit an unusable &lt;e1&gt; line before its
    /// authored &lt;t&gt; fallback. Keep the one-shot only for that non-exact
    /// carrier so the following exact visible-target line can still own it.
    /// </summary>
    public static bool ShouldPreserveExactVisibleTargetToken(
        SupportedPvPContext context,
        bool wolvesDenTestingEnabled,
        bool combatPriorityMode,
        bool eligibleHarmfulAction,
        bool exactCurrentHardTarget,
        bool alreadyPreservedForTapGeneration) =>
        CanUseExactVisibleTargetTestFallback(
            context,
            wolvesDenTestingEnabled,
            combatPriorityMode) &&
        eligibleHarmfulAction &&
        !exactCurrentHardTarget &&
        !alreadyPreservedForTapGeneration;

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
