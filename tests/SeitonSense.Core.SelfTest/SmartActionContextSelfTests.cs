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
            SmartActionContextRules.ShouldPreserveExactVisibleTargetToken(
                SupportedPvPContext.WolvesDen,
                wolvesDenTestingEnabled: true,
                combatPriorityMode: true,
                eligibleHarmfulAction: true,
                exactCurrentHardTarget: false,
                alreadyPreservedForTapGeneration: false),
            "a non-exact Den e1 carrier preserves the token for the visible t line");
        False(
            SmartActionContextRules.ShouldPreserveExactVisibleTargetToken(
                SupportedPvPContext.WolvesDen,
                wolvesDenTestingEnabled: true,
                combatPriorityMode: true,
                eligibleHarmfulAction: true,
                exactCurrentHardTarget: false,
                alreadyPreservedForTapGeneration: true),
            "the same Den token never preserves a second non-exact carrier");
        False(
            SmartActionContextRules.ShouldPreserveExactVisibleTargetToken(
                SupportedPvPContext.WolvesDen,
                wolvesDenTestingEnabled: true,
                combatPriorityMode: true,
                eligibleHarmfulAction: true,
                exactCurrentHardTarget: true,
                alreadyPreservedForTapGeneration: false),
            "the exact visible Den target consumes the token normally");
        False(
            SmartActionContextRules.ShouldPreserveExactVisibleTargetToken(
                SupportedPvPContext.CrystallineConflict,
                wolvesDenTestingEnabled: true,
                combatPriorityMode: true,
                eligibleHarmfulAction: true,
                exactCurrentHardTarget: false,
                alreadyPreservedForTapGeneration: false),
            "CC never enters the Den token-preservation lane");

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

    public static void WolvesDenMacroPreservesE1ThenConsumesExactVisibleTarget()
    {
        foreach (var castTimeProven in new[] { false, true })
        {
            var tokenArmed = true;

            var preserveForHiddenEnemySlot =
                SmartActionContextRules.ShouldPreserveExactVisibleTargetToken(
                    SupportedPvPContext.WolvesDen,
                    wolvesDenTestingEnabled: true,
                    combatPriorityMode: true,
                    eligibleHarmfulAction: true,
                    exactCurrentHardTarget: false,
                    alreadyPreservedForTapGeneration: false);
            if (!preserveForHiddenEnemySlot) tokenArmed = false;
            True(
                tokenArmed,
                $"the non-exact e1 line preserves the {(castTimeProven ? "cast" : "instant")} token");

            var preserveForVisibleTarget =
                SmartActionContextRules.ShouldPreserveExactVisibleTargetToken(
                    SupportedPvPContext.WolvesDen,
                    wolvesDenTestingEnabled: true,
                    combatPriorityMode: true,
                    eligibleHarmfulAction: true,
                    exactCurrentHardTarget: true,
                    alreadyPreservedForTapGeneration: true);
            if (!preserveForVisibleTarget) tokenArmed = false;
            False(
                tokenArmed,
                $"the exact t line consumes the {(castTimeProven ? "cast" : "instant")} token");
        }

        False(
            SmartActionContextRules.ShouldPreserveExactVisibleTargetToken(
                SupportedPvPContext.WolvesDen,
                wolvesDenTestingEnabled: false,
                combatPriorityMode: true,
                eligibleHarmfulAction: true,
                exactCurrentHardTarget: false,
                alreadyPreservedForTapGeneration: false),
            "the disabled Den test path never captures a macro carrier");
        False(
            SmartActionContextRules.ShouldPreserveExactVisibleTargetToken(
                SupportedPvPContext.WolvesDen,
                wolvesDenTestingEnabled: true,
                combatPriorityMode: true,
                eligibleHarmfulAction: false,
                exactCurrentHardTarget: false,
                alreadyPreservedForTapGeneration: false),
            "an unrelated macro action never borrows the Den token");
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
            SmartActionContextRules.IsEligibleExactVisibleWolvesDenTarget(
                isPlayerCharacter: true,
                hostileFlag: true,
                exactVerifiedStrikingDummy: false),
            "an exact hostile player hard target needs no duel-manager slot");
        False(
            SmartActionContextRules.IsEligibleExactVisibleWolvesDenTarget(
                isPlayerCharacter: true,
                hostileFlag: false,
                exactVerifiedStrikingDummy: false),
            "a non-hostile player cannot enter the direct Wolves' Den lane");
        False(
            SmartActionContextRules.IsEligibleExactVisibleWolvesDenTarget(
                isPlayerCharacter: false,
                hostileFlag: true,
                exactVerifiedStrikingDummy: false),
            "an unverified hostile non-player cannot enter the direct lane");
        True(
            SmartActionContextRules.IsEligibleExactVisibleWolvesDenTarget(
                isPlayerCharacter: false,
                hostileFlag: false,
                exactVerifiedStrikingDummy: true),
            "the separately verified exact dummy remains eligible");

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

    public static void WolvesDenRuntimeTargetProofUsesIndependentExactSignals()
    {
        True(
            SmartActionContextRules.HasExactWolvesDenDuelHostilityProof(
                hostileFlag: true,
                exactNativeDuelOpponent: false),
            "the exact hostile flag remains sufficient");
        True(
            SmartActionContextRules.HasExactWolvesDenDuelHostilityProof(
                hostileFlag: false,
                exactNativeDuelOpponent: true),
            "the exact native duel opponent survives a stale hostile flag");
        False(
            SmartActionContextRules.HasExactWolvesDenDuelHostilityProof(
                hostileFlag: false,
                exactNativeDuelOpponent: false),
            "an unrelated non-hostile selected player remains rejected");

        True(
            SmartActionContextRules.HasCanonicalNativeTargetIdentity(
                objectLookupPresent: true,
                objectLookupMatches: true,
                entityLookupPresent: false,
                entityLookupMatches: false),
            "one exact object-ID lookup is sufficient");
        True(
            SmartActionContextRules.HasCanonicalNativeTargetIdentity(
                objectLookupPresent: false,
                objectLookupMatches: false,
                entityLookupPresent: true,
                entityLookupMatches: true),
            "one exact entity-ID lookup is sufficient");
        True(
            SmartActionContextRules.HasCanonicalNativeTargetIdentity(
                objectLookupPresent: true,
                objectLookupMatches: true,
                entityLookupPresent: true,
                entityLookupMatches: true),
            "two agreeing exact indexes are sufficient");
        False(
            SmartActionContextRules.HasCanonicalNativeTargetIdentity(
                objectLookupPresent: true,
                objectLookupMatches: true,
                entityLookupPresent: true,
                entityLookupMatches: false),
            "two present but disagreeing indexes fail closed");
        False(
            SmartActionContextRules.HasCanonicalNativeTargetIdentity(
                objectLookupPresent: false,
                objectLookupMatches: false,
                entityLookupPresent: false,
                entityLookupMatches: false),
            "no canonical lookup never proves a target");
    }

    public static void WolvesDenCastsKeepTheExactSelectedTargetFallback()
    {
        const uint representativeResolvedAction = 41_480;
        static bool CanContinueRankedCast(SupportedPvPContext context) =>
            CastedMacroRedirectRules.CanContinueSmartActionCast(
                SmartActionContextRules.CanUseSmartTargetRanking(context),
                ownedBySmartAction: true,
                supportedActionType: true,
                resolvedActionId: representativeResolvedAction,
                exactActionMetadata: true,
                metadataRowId: representativeResolvedAction,
                isPvp: true,
                canTargetHostile: true,
                isGroundTargeted: false,
                range: 25f);

        True(
            CanContinueRankedCast(SupportedPvPContext.CrystallineConflict),
            "CC casts continue into ordinary Smart Target ranking");
        False(
            CanContinueRankedCast(SupportedPvPContext.WolvesDen),
            "Wolves' Den casts stay on the exact selected-target fallback");

        Equal(
            CastedMacroRedirectDecision.NotApplicable,
            CastedMacroRedirectRules.Evaluate(
                redirectTokenArmed: true,
                supportedActionType: true,
                exactActionMetadata: true,
                adjustedCastTimeMilliseconds: 0,
                baseCastTime100Milliseconds: 0,
                authoredTargetMatchesVisibleTarget: false,
                allowSmartActionCastRedirect: false),
            "a CC instant action with an alternate authored carrier enters ordinary ranking");
        Equal(
            CastedMacroRedirectDecision.RedirectSmartActionCast,
            CastedMacroRedirectRules.Evaluate(
                redirectTokenArmed: true,
                supportedActionType: true,
                exactActionMetadata: true,
                adjustedCastTimeMilliseconds: 1_500,
                baseCastTime100Milliseconds: 15,
                authoredTargetMatchesVisibleTarget: false,
                allowSmartActionCastRedirect:
                    CanContinueRankedCast(SupportedPvPContext.CrystallineConflict)),
            "a CC cast with an alternate authored carrier enters the same ranking");

        True(
            SmartActionContextRules.CanUseSameCallVisibleTargetFallback(
                SupportedPvPContext.WolvesDen,
                wolvesDenTestingEnabled: true,
                combatPriorityMode: true,
                redirectApplied: false,
                rankedWinnerSelected: false,
                exactCurrentHardTarget: true),
            "a Den instant action retains its exact visible-target fallback");

        var denVisibleDecision = CastedMacroRedirectRules.Evaluate(
            redirectTokenArmed: true,
            supportedActionType: true,
            exactActionMetadata: true,
            adjustedCastTimeMilliseconds: 1_500,
            baseCastTime100Milliseconds: 15,
            authoredTargetMatchesVisibleTarget: true,
            allowSmartActionCastRedirect:
                CanContinueRankedCast(SupportedPvPContext.WolvesDen));
        Equal(
            CastedMacroRedirectDecision.PreserveAuthoredTarget,
            denVisibleDecision,
            "an exact Den cast retains the e018196 authored-target safety lease path");

        foreach (var carrier in new[]
                 {
                     0UL,
                     SmartActionContextRules.NativeSelectedTargetSentinel,
                 })
        {
            True(
                SmartActionContextRules.IsNativeSelectedTargetCarrier(carrier),
                $"documented native selected-target carrier {carrier:X} is admitted");
        }
        foreach (var invalidOrExplicit in new[]
                 {
                     1UL,
                     (ulong)uint.MaxValue,
                     ulong.MaxValue,
                 })
        {
            False(
                SmartActionContextRules.IsNativeSelectedTargetCarrier(invalidOrExplicit),
                $"unknown or explicit ID {invalidOrExplicit:X} cannot borrow the native carrier lane");
        }
    }

    private static void True(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static void False(bool value, string message) => True(!value, message);

    private static void Equal<T>(T expected, T actual, string message)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
    }
}
