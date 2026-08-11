using SeitonSense.Core;

var tests = new (string Name, Action Run)[]
{
    ("Ninja job gate is exact", NinjaJobGateIsExact),
    ("execute threshold is strictly below half", ExecuteThresholdIsStrict),
    ("invalid HP fails closed", InvalidHpFailsClosed),
    ("enemy slot labels are exact", EnemySlotLabelsAreExact),
    ("enemy validation fails closed", EnemyValidationFailsClosed),
    ("native range result accepts facing-only failure", NativeRangeResultIsExact),
    ("known CC territories are complete", KnownCcTerritoriesAreComplete),
    ("CC matching remains fail closed", CcMatchingIsFailClosed),
    ("supported PvP context keeps Wolves' Den opt-in", SupportedPvpContextIsExact),
    ("Wolves' Den opponent selection is strict", WolvesDenOpponentSelectionIsStrict),
    ("visibility ignores a short false sample", VisibilityHasFalseGrace),
    ("visibility hard reset is immediate", VisibilityHardResetIsImmediate),
    ("unknown Guard never claims cooldown", UnknownGuardFailsClosed),
    ("Guard action starts a 30 second cooldown", GuardActionTracksCooldown),
    ("Guard status infers a stable deadline", GuardStatusInfersDeadline),
    ("Guard status tracks a later activation", GuardStatusTracksLaterActivation),
    ("Guard revive resets the cooldown", GuardReviveResetsCooldown),
    ("low MP requires a stable trusted sample", LowMpRequiresStableTrustedSample),
    ("low MP uses exit hysteresis", LowMpUsesExitHysteresis),
    ("low MP trust loss cancels only a pending transition", LowMpTrustLossIsStable),
    ("low MP thresholds are exact", LowMpThresholdsAreExact),
    ("Seiton popup is one shot and rearms", StablePopupIsOneShot),
    ("Seiton range jitter cannot rearm", SeitonRangeJitterCannotRearm),
    ("Seiton popup rearm must remain stable", SeitonPopupRearmMustRemainStable),
    ("persistent Seiton cue enters once and remains visible", PersistentSeitonCueEntersOnce),
    ("persistent Seiton cue ignores range jitter", PersistentSeitonCueIgnoresRangeJitter),
    ("persistent Seiton cue rearms only from semantic recovery", PersistentSeitonCueRearmsSemantically),
    ("Seiton preparation band is optional and exact", SeitonPreparationBandIsExact),
    ("personal debuff alerts deduplicate and order by urgency", PersonalDebuffsDeduplicateAndOrder),
    ("personal debuff refresh does not repulse", PersonalDebuffRefreshDoesNotRepulse),
    ("personal debuff missing grace prevents flicker", PersonalDebuffMissingGracePreventsFlicker),
    ("personal debuff escalation pulses once", PersonalDebuffEscalationPulsesOnce),
    ("personal debuff lifecycle fails closed", PersonalDebuffLifecycleFailsClosed),
    ("physical key priming and release define generations", PhysicalGameplayKeySelfTests.PrimingAndReleaseDefineGenerations),
    ("physical key consumption survives until release", PhysicalGameplayKeySelfTests.ConsumptionSurvivesUntilRelease),
    ("text input cannot become a held gameplay trigger", PhysicalGameplayKeySelfTests.TextInputPoisonsOnlyTheCurrentHold),
    ("physical key hard reset requires release", PhysicalGameplayKeySelfTests.HardResetRequiresAnotherRelease),
    ("one physical hold cannot cross Purify status generations", PhysicalGameplayKeySelfTests.OneHoldCannotCrossStatusGenerations),
    ("Purify accepts a same-frame fresh key", EmergencyPurifyBufferSelfTests.SameFrameFreshKeyCanDispatch),
    ("Purify held-key entry is explicit and one shot", EmergencyPurifyBufferSelfTests.HeldKeyAtStatusEntryIsExplicitAndOneShot),
    ("Purify held-key level only counts at status entry", EmergencyPurifyBufferSelfTests.HeldKeyOnlyCountsAtStatusEntry),
    ("Purify fresh edge wins over coincident held input", EmergencyPurifyBufferSelfTests.FreshEdgeWinsWhenFreshAndHeldCoincide),
    ("Purify held generation is consumed when armed", EmergencyPurifyBufferSelfTests.HeldKeyIsConsumedWhenItOnlyArms),
    ("Purify dispatch consumes before the attempt", EmergencyPurifyBufferSelfTests.DispatchConsumesBeforeAttempt),
    ("ready Purify dispatches once at the key edge", EmergencyPurifyBufferSelfTests.ReadyAtArmDispatchesExactlyOnce),
    ("Purify timeout without an attempt can rearm", EmergencyPurifyBufferSelfTests.TimeoutWithoutAttemptCanRearm),
    ("Purify rearms only after status absence", EmergencyPurifyBufferSelfTests.StatusAbsenceIsTheOnlyRearmForSameInstance),
    ("Purify tracks the exact status instance", EmergencyPurifyBufferSelfTests.ExactStatusReplacementNeedsANewKey),
    ("Purify temporary gates do not spend an attempt", EmergencyPurifyBufferSelfTests.TemporarySafetyGatesDoNotSpendAnAttempt),
    ("Purify hard reset and invalid inputs fail closed", EmergencyPurifyBufferSelfTests.HardResetAndInvalidInputsFailClosed),
    ("target and focus on the same actor are combined", TargetHighlightRulesSelfTests.SameObjectIsCombined),
    ("different current and focus targets stay ordered", TargetHighlightRulesSelfTests.DifferentObjectsRemainOrdered),
    ("target PvP-only gating is per source", TargetHighlightRulesSelfTests.PvpGateIsPerSource),
    ("invalid target identities fail closed", TargetHighlightRulesSelfTests.InvalidIdentitiesFailClosed),
    ("target HP formatting is safe", TargetHighlightRulesSelfTests.HpFormattingIsSafe),
    ("target distance formatting is safe", TargetHighlightRulesSelfTests.DistanceFormattingIsSafe),
    ("target S-slot formatting is exact", TargetHighlightRulesSelfTests.EnemySlotFormattingIsExact),
    ("combined target info uses safe same-identity fallbacks", TargetHighlightRulesSelfTests.CombinedPlanUsesOnlySafeFallbacks),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception exception)
    {
        failures.Add($"FAIL {test.Name}: {exception.Message}");
    }
}

foreach (var failure in failures) Console.Error.WriteLine(failure);
if (failures.Count > 0)
{
    Environment.ExitCode = 1;
    return;
}

Console.WriteLine($"PASS all {tests.Length} core tests");

static void NinjaJobGateIsExact()
{
    True(ExecuteThreshold.IsNinja(30), "NIN row");
    False(ExecuteThreshold.IsNinja(29), "non-NIN row");
    False(ExecuteThreshold.IsNinja(0), "invalid row");
}

static void ExecuteThresholdIsStrict()
{
    True(ExecuteThreshold.IsBelowHalf(49, 100), "49 percent");
    True(ExecuteThreshold.IsBelowHalf(49_999, 100_000), "fraction below half");
    False(ExecuteThreshold.IsBelowHalf(50, 100), "exactly 50 percent");
    False(ExecuteThreshold.IsBelowHalf(51, 100), "above half");
}

static void InvalidHpFailsClosed()
{
    False(ExecuteThreshold.IsBelowHalf(0, 100), "dead target");
    False(ExecuteThreshold.IsBelowHalf(1, 0), "zero maximum");
    False(ExecuteThreshold.IsBelowHalf(101, 100), "impossible current HP");
    True(ExecuteThreshold.IsBelowHalf(uint.MaxValue / 4, uint.MaxValue), "wide arithmetic");
}

static void EnemySlotLabelsAreExact()
{
    for (var slot = 1; slot <= 5; slot++)
    {
        True(EnemySlotRules.IsValidSlot(slot), $"slot {slot}");
        Equal($"S{slot}", EnemySlotRules.Label(slot), $"label {slot}");
    }

    False(EnemySlotRules.IsValidSlot(0), "slot zero");
    False(EnemySlotRules.IsValidSlot(6), "slot six");
    Equal(string.Empty, EnemySlotRules.Label(0), "invalid label");
}

static void EnemyValidationFailsClosed()
{
    True(EnemySlotRules.CanUseResolvedEnemy(false, false, true, false, true, true, 10, 100), "hostile enemy");
    True(EnemySlotRules.CanUseResolvedEnemy(false, false, false, true, true, true, 10, 100), "complete CC fallback");
    False(EnemySlotRules.CanUseResolvedEnemy(true, false, true, true, true, true, 10, 100), "self");
    False(EnemySlotRules.CanUseResolvedEnemy(false, true, true, true, true, true, 10, 100), "ally precedence");
    False(EnemySlotRules.CanUseResolvedEnemy(false, false, false, false, true, true, 10, 100), "unknown relation");
    False(EnemySlotRules.CanUseResolvedEnemy(false, false, true, false, false, true, 10, 100), "dead");
    False(EnemySlotRules.CanUseResolvedEnemy(false, false, true, false, true, false, 10, 100), "untargetable");
}

static void NativeRangeResultIsExact()
{
    True(SeitonRangeRules.HasNativeRangeAndLineOfSight(0), "native success");
    True(SeitonRangeRules.HasNativeRangeAndLineOfSight(565), "not-facing still has range and line of sight");
    False(SeitonRangeRules.HasNativeRangeAndLineOfSight(562), "line of sight failure");
    False(SeitonRangeRules.HasNativeRangeAndLineOfSight(566), "out of range");
    False(SeitonRangeRules.HasNativeRangeAndLineOfSight(uint.MaxValue), "unknown result");
}

static void KnownCcTerritoriesAreComplete()
{
    var publicTerritories = new uint[] { 1032, 1033, 1034, 1116, 1138, 1293, 1357 };
    var customTerritories = new uint[] { 1058, 1059, 1060, 1117, 1139, 1294, 1358 };
    True(publicTerritories.All(PvPMatchRules.IsPublicCrystallineConflictTerritory), "public territories");
    True(publicTerritories.Concat(customTerritories).All(PvPMatchRules.IsKnownCrystallineConflictTerritory), "all CC territories");
    False(customTerritories.Any(PvPMatchRules.IsPublicCrystallineConflictTerritory), "custom not public");
}

static void CcMatchingIsFailClosed()
{
    True(PvPMatchRules.IsCrystallineConflict(true, 1032, false, false, 0, false, false), "known territory");
    True(PvPMatchRules.IsCrystallineConflict(true, 9999, true, true, 43, false, false), "category 43");
    True(PvPMatchRules.IsCrystallineConflict(true, 9999, true, true, 44, false, false), "category 44");
    True(PvPMatchRules.IsCrystallineConflict(true, 9999, true, true, 0, true, false), "casual roulette");
    True(PvPMatchRules.IsCrystallineConflict(true, 9999, true, true, 0, false, true), "ranked roulette");
    False(PvPMatchRules.IsCrystallineConflict(false, 1032, true, true, 43, true, true), "not live PvP");
    False(PvPMatchRules.IsCrystallineConflict(true, 9999, false, true, 43, false, false), "invalid condition");
    False(PvPMatchRules.IsCrystallineConflict(true, 9999, true, false, 43, false, false), "non-PvP condition");
    False(PvPMatchRules.IsCrystallineConflict(true, 9999, true, true, 12, false, false), "unrelated mode");
}

static void SupportedPvpContextIsExact()
{
    Equal(
        SupportedPvPContext.CrystallineConflict,
        PvPMatchRules.ResolveSupportedContext(
            isPvP: true,
            isPvPExcludingWolvesDen: true,
            includeWolvesDenTesting: false,
            territoryId: 1032,
            conditionValid: false,
            conditionPvP: false,
            contentUiCategoryId: 0,
            casualRoulette: false,
            rankedRoulette: false),
        "known CC remains supported without Wolves' Den opt-in");

    Equal(
        SupportedPvPContext.WolvesDen,
        PvPMatchRules.ResolveSupportedContext(
            isPvP: true,
            isPvPExcludingWolvesDen: false,
            includeWolvesDenTesting: true,
            territoryId: 250,
            conditionValid: false,
            conditionPvP: false,
            contentUiCategoryId: 0,
            casualRoulette: false,
            rankedRoulette: false),
        "Wolves' Den flags plus opt-in");

    Equal(
        SupportedPvPContext.None,
        PvPMatchRules.ResolveSupportedContext(
            isPvP: true,
            isPvPExcludingWolvesDen: false,
            includeWolvesDenTesting: false,
            territoryId: 250,
            conditionValid: false,
            conditionPvP: false,
            contentUiCategoryId: 0,
            casualRoulette: false,
            rankedRoulette: false),
        "Wolves' Den is disabled without opt-in");

    Equal(
        SupportedPvPContext.None,
        PvPMatchRules.ResolveSupportedContext(
            isPvP: false,
            isPvPExcludingWolvesDen: false,
            includeWolvesDenTesting: true,
            territoryId: 250,
            conditionValid: false,
            conditionPvP: false,
            contentUiCategoryId: 0,
            casualRoulette: false,
            rankedRoulette: false),
        "non-PvP cannot become Wolves' Den");

    Equal(
        SupportedPvPContext.None,
        PvPMatchRules.ResolveSupportedContext(
            isPvP: true,
            isPvPExcludingWolvesDen: false,
            includeWolvesDenTesting: true,
            territoryId: 9999,
            conditionValid: false,
            conditionPvP: false,
            contentUiCategoryId: 0,
            casualRoulette: false,
            rankedRoulette: false),
        "Wolves' Den flags outside territory 250 fail closed");

    Equal(
        SupportedPvPContext.None,
        PvPMatchRules.ResolveSupportedContext(
            isPvP: true,
            isPvPExcludingWolvesDen: true,
            includeWolvesDenTesting: true,
            territoryId: 9999,
            conditionValid: true,
            conditionPvP: true,
            contentUiCategoryId: 45,
            casualRoulette: false,
            rankedRoulette: false),
        "Frontline or Rival Wings remains excluded");
}

static void WolvesDenOpponentSelectionIsStrict()
{
    var enemy = new WolvesDenOpponentCandidate(
        EntityId: 100,
        GameObjectId: 1_000,
        MatchesNativeDuelEnemyId: true,
        HasValidAddress: true,
        IsPlayerCharacter: true,
        IsSelf: false,
        HasHostileFlag: true,
        IsTargetable: true);

    var resolved = WolvesDenOpponentRules.ResolveSingleSlot([enemy]);
    True(resolved.HasValue, "one strict hostile resolves");
    Equal(EnemySlotRules.FirstSlot, resolved!.Value.Slot, "duel opponent uses S1");
    Equal(100u, resolved.Value.EntityId, "resolved entity is preserved");

    False(WolvesDenOpponentRules.ResolveSingleSlot([]).HasValue, "zero candidates fail closed");
    False(
        WolvesDenOpponentRules.ResolveSingleSlot([enemy, enemy with { EntityId = 101 }]).HasValue,
        "multiple hostile candidates fail closed");

    var rejectedCandidates = new[]
    {
        enemy with { EntityId = 0 },
        enemy with { EntityId = 0xE0000000 },
        enemy with { GameObjectId = 0 },
        enemy with { GameObjectId = 0xE0000000 },
        enemy with { MatchesNativeDuelEnemyId = false },
        enemy with { HasValidAddress = false },
        enemy with { IsPlayerCharacter = false },
        enemy with { IsSelf = true },
        enemy with { HasHostileFlag = false },
        enemy with { IsTargetable = false },
    };

    foreach (var candidate in rejectedCandidates)
    {
        False(
            WolvesDenOpponentRules.ResolveSingleSlot([candidate]).HasValue,
            $"ineligible candidate fails closed: {candidate}");
    }

    resolved = WolvesDenOpponentRules.ResolveSingleSlot([
        enemy with { MatchesNativeDuelEnemyId = false },
        enemy,
        enemy with { HasHostileFlag = false },
    ]);
    True(resolved.HasValue, "ineligible bystanders do not block one strict hostile");
    Equal(100u, resolved!.Value.EntityId, "strict hostile remains S1");
}

static void VisibilityHasFalseGrace()
{
    var state = DebouncedVisibilityRules.Observe(DebouncedVisibilityState.Initial, true, 1_000);
    state = DebouncedVisibilityRules.Observe(state, false, 1_050);
    True(state.IsVisible, "first false sample remains visible");
    state = DebouncedVisibilityRules.Observe(state, true, 1_100);
    True(state.IsVisible, "true sample cancels pending hide");
    state = DebouncedVisibilityRules.Observe(state, false, 1_150);
    state = DebouncedVisibilityRules.Observe(state, false, 1_349);
    True(state.IsVisible, "inside grace");
    state = DebouncedVisibilityRules.Observe(state, false, 1_350);
    False(state.IsVisible, "grace elapsed");

    state = DebouncedVisibilityRules.Observe(DebouncedVisibilityState.Initial, true, 2_000);
    state = DebouncedVisibilityRules.Observe(
        state,
        false,
        2_050,
        falseGraceMilliseconds: PersonalDebuffAlertRules.MissingGraceMilliseconds);
    True(state.IsVisible, "Resilience-style status gap remains active inside 150 ms grace");
    state = DebouncedVisibilityRules.Observe(
        state,
        false,
        2_199,
        falseGraceMilliseconds: PersonalDebuffAlertRules.MissingGraceMilliseconds);
    True(state.IsVisible, "custom grace remains active before its exact boundary");
    state = DebouncedVisibilityRules.Observe(
        state,
        false,
        2_200,
        falseGraceMilliseconds: PersonalDebuffAlertRules.MissingGraceMilliseconds);
    False(state.IsVisible, "custom grace expires at its exact boundary");
}

static void VisibilityHardResetIsImmediate()
{
    var state = DebouncedVisibilityRules.Observe(DebouncedVisibilityState.Initial, true, 1_000);
    state = DebouncedVisibilityRules.Observe(state, true, 1_001, hardReset: true);
    False(state.IsVisible, "hard reset overrides a visible observation");
}

static void UnknownGuardFailsClosed()
{
    Equal(GuardAvailability.Unknown, GuardCooldownRules.GetAvailability(GuardCooldownState.Initial, 1_000), "unknown state");
    False(GuardCooldownRules.ShouldShowCrossedIcon(GuardCooldownState.Initial, 1_000), "unknown has no crossed icon");
}

static void GuardActionTracksCooldown()
{
    var state = GuardCooldownRules.ObserveAction(GuardCooldownState.Initial, 1_000);
    Equal(GuardAvailability.Unavailable, GuardCooldownRules.GetAvailability(state, 30_999), "before deadline");
    Equal(1L, GuardCooldownRules.RemainingMilliseconds(state, 30_999), "remaining time");
    Equal(GuardAvailability.Ready, GuardCooldownRules.GetAvailability(state, 31_000), "at deadline");
}

static void GuardStatusInfersDeadline()
{
    var state = GuardCooldownRules.ObserveStatus(GuardCooldownState.Initial, 10_000, 2_000);
    Equal(38_000L, state.ReadyAtMilliseconds, "now plus remaining plus 26 seconds");
    state = GuardCooldownRules.ObserveStatus(state, 10_100, 2_000);
    Equal(38_000L, state.ReadyAtMilliseconds, "stale remaining time cannot extend deadline");
    state = GuardCooldownRules.ObserveStatus(state, 10_200, 1_600);
    Equal(38_000L, state.ReadyAtMilliseconds, "later status cannot move the inferred deadline");
}

static void GuardStatusTracksLaterActivation()
{
    var state = GuardCooldownRules.ObserveStatus(GuardCooldownState.Initial, 10_000, 2_000);
    state = GuardCooldownRules.ObserveStatus(state, 38_000, 4_000);
    Equal(68_000L, state.ReadyAtMilliseconds, "new status after prior recast gets a new deadline");
}

static void GuardReviveResetsCooldown()
{
    var state = GuardCooldownRules.ObserveAction(GuardCooldownState.Initial, 1_000);
    state = GuardCooldownRules.ObserveRevive();
    Equal(GuardAvailability.Ready, GuardCooldownRules.GetAvailability(state, 1_001), "recast reset on revive");
}

static void LowMpRequiresStableTrustedSample()
{
    var state = LowMpRules.Observe(LowMpState.Initial, 1_500, trustedSample: false, 1_000);
    False(LowMpRules.ShouldShowCrossedIcon(state), "untrusted low sample");
    state = LowMpRules.Observe(state, 1_500, trustedSample: true, 1_050);
    False(LowMpRules.ShouldShowCrossedIcon(state), "debounce begins");
    state = LowMpRules.Observe(state, 1_500, trustedSample: true, 1_199);
    False(LowMpRules.ShouldShowCrossedIcon(state), "inside debounce");
    state = LowMpRules.Observe(state, 1_500, trustedSample: true, 1_200);
    True(LowMpRules.ShouldShowCrossedIcon(state), "stable low MP");
}

static void LowMpUsesExitHysteresis()
{
    var state = LowMpRules.Observe(LowMpState.Initial, 1_000, true, 1_000, debounceMilliseconds: 0);
    True(LowMpRules.ShouldShowCrossedIcon(state), "entered below 2000");
    state = LowMpRules.Observe(state, 2_100, true, 1_050, debounceMilliseconds: 0);
    True(LowMpRules.ShouldShowCrossedIcon(state), "2100 remains unavailable");
    state = LowMpRules.Observe(state, 2_300, true, 1_100);
    state = LowMpRules.Observe(state, 2_300, true, 1_250);
    False(LowMpRules.ShouldShowCrossedIcon(state), "2300 exits after debounce");
}

static void LowMpTrustLossIsStable()
{
    var pending = LowMpRules.Observe(LowMpState.Initial, 1_500, true, 1_000);
    pending = LowMpRules.Observe(pending, 0, false, 1_100);
    pending = LowMpRules.Observe(pending, 1_500, true, 1_200);
    False(LowMpRules.ShouldShowCrossedIcon(pending), "untrusted gap restarts pending entry");
    pending = LowMpRules.Observe(pending, 1_500, true, 1_350);
    True(LowMpRules.ShouldShowCrossedIcon(pending), "restarted entry completes after full debounce");

    var latched = LowMpRules.Observe(LowMpState.Initial, 1_000, true, 2_000, debounceMilliseconds: 0);
    latched = LowMpRules.Observe(latched, 0, false, 2_100);
    True(LowMpRules.ShouldShowCrossedIcon(latched), "missing sample preserves an established low-MP icon");
}

static void LowMpThresholdsAreExact()
{
    var ready = LowMpRules.Observe(LowMpState.Initial, 2_000, true, 1_000, debounceMilliseconds: 0);
    False(LowMpRules.ShouldShowCrossedIcon(ready), "exactly 2000 can afford Recuperate");

    var low = LowMpRules.Observe(LowMpState.Initial, 1_999, true, 1_000, debounceMilliseconds: 0);
    True(LowMpRules.ShouldShowCrossedIcon(low), "1999 cannot afford Recuperate");
    low = LowMpRules.Observe(low, 2_299, true, 1_050, debounceMilliseconds: 0);
    True(LowMpRules.ShouldShowCrossedIcon(low), "2299 remains inside recovery hysteresis");
    low = LowMpRules.Observe(low, 2_300, true, 1_100, debounceMilliseconds: 0);
    False(LowMpRules.ShouldShowCrossedIcon(low), "exactly 2300 clears hysteresis");
}

static void StablePopupIsOneShot()
{
    var decision = StablePopupRules.Observe(StablePopupState.Initial, true, false, 1_000);
    False(decision.TriggerPopup, "first true sample");
    decision = StablePopupRules.Observe(decision.NextState, true, false, 1_049);
    False(decision.TriggerPopup, "not stable long enough");
    decision = StablePopupRules.Observe(decision.NextState, true, false, 1_050);
    True(decision.TriggerPopup, "stable rising edge");
    decision = StablePopupRules.Observe(decision.NextState, true, false, 1_500);
    False(decision.TriggerPopup, "latched one shot");
    decision = StablePopupRules.Observe(decision.NextState, false, true, 1_600);
    decision = StablePopupRules.Observe(decision.NextState, false, true, 1_900);
    True(decision.NextState.Armed, "stable false rearms");
    decision = StablePopupRules.Observe(decision.NextState, true, false, 2_000, stableTrueMilliseconds: 0);
    True(decision.TriggerPopup, "new rising edge triggers");
}

static void SeitonRangeJitterCannotRearm()
{
    var decision = StablePopupRules.Observe(
        StablePopupState.Initial,
        candidate: true,
        rearmCondition: false,
        nowMilliseconds: 1_000,
        stableTrueMilliseconds: 0);
    True(decision.TriggerPopup, "initial popup");

    decision = StablePopupRules.Observe(decision.NextState, false, false, 1_100);
    decision = StablePopupRules.Observe(decision.NextState, false, false, 2_000);
    False(decision.NextState.Armed, "range loss remains latched");

    decision = StablePopupRules.Observe(decision.NextState, true, false, 2_050, stableTrueMilliseconds: 0);
    False(decision.TriggerPopup, "walking back into range cannot pop again");
}

static void SeitonPopupRearmMustRemainStable()
{
    var decision = StablePopupRules.Observe(
        StablePopupState.Initial,
        candidate: true,
        rearmCondition: false,
        nowMilliseconds: 1_000,
        stableTrueMilliseconds: 0);
    True(decision.TriggerPopup, "initial popup");

    decision = StablePopupRules.Observe(decision.NextState, false, true, 1_100);
    decision = StablePopupRules.Observe(decision.NextState, false, false, 1_399);
    False(decision.NextState.Armed, "interrupted rearm is cancelled");
    decision = StablePopupRules.Observe(decision.NextState, false, true, 1_400);
    decision = StablePopupRules.Observe(decision.NextState, false, true, 1_699);
    False(decision.NextState.Armed, "new rearm has not reached boundary");
    decision = StablePopupRules.Observe(decision.NextState, false, true, 1_700);
    True(decision.NextState.Armed, "exact 300 ms rearms");

    decision = StablePopupRules.Observe(decision.NextState, true, false, 2_000, hardReset: true);
    True(decision.NextState.Armed, "hard reset restores initial armed state");
    False(decision.TriggerPopup, "hard reset never emits a popup");
}

static void PersistentSeitonCueEntersOnce()
{
    var decision = ObserveSeiton(PersistentSeitonCueState.Initial, hp: 49, now: 1_000);
    Equal(SeitonCueKind.Hidden, decision.Cue, "execute entry waits for stability");
    False(decision.TriggerEntryPulse, "first sample does not pulse");

    decision = ObserveSeiton(decision.NextState, hp: 49, now: 1_050);
    Equal(SeitonCueKind.Execute, decision.Cue, "execute cue appears at stable boundary");
    True(decision.TriggerEntryPulse, "execute entry pulses once");

    decision = ObserveSeiton(decision.NextState, hp: 48, now: 5_000);
    Equal(SeitonCueKind.Execute, decision.Cue, "cue remains visible while eligible");
    False(decision.TriggerEntryPulse, "persistent cue does not repeatedly pulse");
}

static void PersistentSeitonCueIgnoresRangeJitter()
{
    var decision = ObserveSeiton(
        PersistentSeitonCueState.Initial,
        hp: 49,
        now: 1_000,
        stableExecuteMilliseconds: 0);
    True(decision.TriggerEntryPulse, "initial entry pulse");

    decision = ObserveSeiton(decision.NextState, hp: 49, now: 1_100, inRange: false);
    Equal(SeitonCueKind.Execute, decision.Cue, "short range loss retains cue");
    decision = ObserveSeiton(decision.NextState, hp: 49, now: 1_300, inRange: false);
    Equal(SeitonCueKind.Hidden, decision.Cue, "sustained range loss hides cue");

    decision = ObserveSeiton(decision.NextState, hp: 49, now: 1_350, inRange: true);
    Equal(SeitonCueKind.Execute, decision.Cue, "return to range restores persistent cue");
    False(decision.TriggerEntryPulse, "range return never repulses");

    decision = ObserveSeiton(decision.NextState, hp: 52, now: 1_400, inRange: false);
    Equal(SeitonCueKind.Hidden, decision.Cue, "healing out of execute range clears a stale cue immediately");
}

static void PersistentSeitonCueRearmsSemantically()
{
    var decision = ObserveSeiton(
        PersistentSeitonCueState.Initial,
        hp: 49,
        now: 1_000,
        stableExecuteMilliseconds: 0);
    True(decision.TriggerEntryPulse, "first execute entry");

    decision = ObserveSeiton(decision.NextState, hp: 51, now: 1_100);
    Equal(SeitonCueKind.Preparation, decision.Cue, "51 percent is no longer falsely actionable");
    False(decision.TriggerEntryPulse, "threshold jitter does not repulse");

    decision = ObserveSeiton(
        decision.NextState,
        hp: 49,
        now: 1_150,
        stableExecuteMilliseconds: 0);
    Equal(SeitonCueKind.Execute, decision.Cue, "falling below half restores the execute cue");
    False(decision.TriggerEntryPulse, "sub-52 percent jitter does not rearm the entry pulse");

    decision = ObserveSeiton(decision.NextState, hp: 52, now: 1_200);
    Equal(SeitonCueKind.Preparation, decision.Cue, "52 percent rearms into preparation");
    decision = ObserveSeiton(
        decision.NextState,
        hp: 49,
        now: 1_300,
        stableExecuteMilliseconds: 0);
    True(decision.TriggerEntryPulse, "new execute entry after semantic recovery pulses");

    decision = ObserveSeiton(decision.NextState, hp: 49, now: 1_400, resourceReady: false);
    Equal(SeitonCueKind.Hidden, decision.Cue, "resource use clears cue");
    decision = ObserveSeiton(
        decision.NextState,
        hp: 49,
        now: 1_500,
        stableExecuteMilliseconds: 0);
    True(decision.TriggerEntryPulse, "new resource activation rearms pulse");
}

static void SeitonPreparationBandIsExact()
{
    True(PersistentSeitonCueRules.IsPreparationBand(50, 100), "exactly 50 percent");
    True(PersistentSeitonCueRules.IsPreparationBand(59_999, 100_000), "below 60 percent");
    False(PersistentSeitonCueRules.IsPreparationBand(49, 100), "execute band");
    False(PersistentSeitonCueRules.IsPreparationBand(60, 100), "exactly 60 percent");
    False(PersistentSeitonCueRules.IsPreparationBand(0, 100), "dead target");

    var shown = ObserveSeiton(PersistentSeitonCueState.Initial, hp: 55, now: 1_000);
    Equal(SeitonCueKind.Preparation, shown.Cue, "preparation enabled");
    var hidden = ObserveSeiton(
        PersistentSeitonCueState.Initial,
        hp: 55,
        now: 1_000,
        showPreparation: false);
    Equal(SeitonCueKind.Hidden, hidden.Cue, "preparation disabled");

    hidden = ObserveSeiton(
        shown.NextState,
        hp: 55,
        now: 1_050,
        inRange: false,
        showPreparation: false);
    Equal(SeitonCueKind.Hidden, hidden.Cue, "disabled preparation never survives range grace");

    hidden = ObserveSeiton(shown.NextState, hp: 60, now: 1_050, inRange: false);
    Equal(SeitonCueKind.Hidden, hidden.Cue, "leaving the preparation band clears stale grace");
}

static void PersonalDebuffsDeduplicateAndOrder()
{
    var observations = new[]
    {
        new PersonalDebuffObservation(10, PersonalDebuffAlertKind.Warning, 7_000),
        new PersonalDebuffObservation(20, PersonalDebuffAlertKind.CleanseUrgent, 4_000),
        new PersonalDebuffObservation(10, PersonalDebuffAlertKind.Warning, 8_000),
        new PersonalDebuffObservation(30, PersonalDebuffAlertKind.Warning, 3_000),
    };
    var decision = PersonalDebuffAlertRules.Observe([], observations, 1_000);

    Equal(3, decision.Alerts.Length, "duplicate status collapsed");
    Equal(20u, decision.Alerts[0].StatusId, "cleanse warning ordered first");
    Equal(30u, decision.Alerts[1].StatusId, "shorter regular warning ordered next");
    Equal(10u, decision.Alerts[2].StatusId, "longer warning ordered last");
    Equal(7_000L, decision.Alerts[2].RemainingMilliseconds, "newest duplicate expiry retained");
    True(decision.Alerts.All(alert => alert.TriggerEntryPulse), "each unique entry pulses once");
}

static void PersonalDebuffRefreshDoesNotRepulse()
{
    var first = PersonalDebuffAlertRules.Observe(
        [],
        [new PersonalDebuffObservation(10, PersonalDebuffAlertKind.Warning, 3_000)],
        1_000);
    True(first.Alerts[0].TriggerEntryPulse, "first observation pulses");

    var refreshed = PersonalDebuffAlertRules.Observe(
        first.NextStates,
        [new PersonalDebuffObservation(10, PersonalDebuffAlertKind.Warning, 5_000)],
        1_100);
    Equal(3_900L, refreshed.Alerts[0].RemainingMilliseconds, "countdown uses refreshed expiry");
    False(refreshed.Alerts[0].TriggerEntryPulse, "duration refresh is not a new application");
}

static void PersonalDebuffMissingGracePreventsFlicker()
{
    var first = PersonalDebuffAlertRules.Observe(
        [],
        [new PersonalDebuffObservation(10, PersonalDebuffAlertKind.Warning, 5_000)],
        1_000);
    var missing = PersonalDebuffAlertRules.Observe(first.NextStates, [], 1_050);
    Equal(1, missing.Alerts.Length, "first missing sample remains visible");
    False(missing.Alerts[0].TriggerEntryPulse, "missing grace never pulses");

    var returned = PersonalDebuffAlertRules.Observe(
        missing.NextStates,
        [new PersonalDebuffObservation(10, PersonalDebuffAlertKind.Warning, 5_000)],
        1_199);
    False(returned.Alerts[0].TriggerEntryPulse, "return inside grace is same application");

    missing = PersonalDebuffAlertRules.Observe(returned.NextStates, [], 1_300);
    var removed = PersonalDebuffAlertRules.Observe(missing.NextStates, [], 1_450);
    Equal(0, removed.Alerts.Length, "missing grace boundary removes warning");
    var reapplied = PersonalDebuffAlertRules.Observe(
        removed.NextStates,
        [new PersonalDebuffObservation(10, PersonalDebuffAlertKind.Warning, 6_000)],
        1_500);
    True(reapplied.Alerts[0].TriggerEntryPulse, "application after removal pulses again");
}

static void PersonalDebuffEscalationPulsesOnce()
{
    var first = PersonalDebuffAlertRules.Observe(
        [],
        [new PersonalDebuffObservation(10, PersonalDebuffAlertKind.Warning, 5_000)],
        1_000);
    var escalated = PersonalDebuffAlertRules.Observe(
        first.NextStates,
        [new PersonalDebuffObservation(10, PersonalDebuffAlertKind.CleanseUrgent, 5_000)],
        1_100);
    True(escalated.Alerts[0].TriggerEntryPulse, "urgency escalation gets attention once");
    var stable = PersonalDebuffAlertRules.Observe(
        escalated.NextStates,
        [new PersonalDebuffObservation(10, PersonalDebuffAlertKind.CleanseUrgent, 5_000)],
        1_200);
    False(stable.Alerts[0].TriggerEntryPulse, "stable urgent alert does not repulse");
}

static void PersonalDebuffLifecycleFailsClosed()
{
    var decision = PersonalDebuffAlertRules.Observe(
        [],
        [
            new PersonalDebuffObservation(0, PersonalDebuffAlertKind.Warning, 5_000),
            new PersonalDebuffObservation(10, (PersonalDebuffAlertKind)99, 5_000),
            new PersonalDebuffObservation(20, PersonalDebuffAlertKind.Warning, 1_000),
        ],
        1_000);
    Equal(0, decision.Alerts.Length, "invalid and expired observations are ignored");

    decision = PersonalDebuffAlertRules.Observe(
        [],
        [new PersonalDebuffObservation(30, PersonalDebuffAlertKind.CleanseUrgent, 5_000)],
        1_100,
        hardReset: true);
    Equal(0, decision.Alerts.Length, "hard reset clears alerts immediately");
    Equal(0, decision.NextStates.Length, "hard reset clears lifecycle state");
}

static PersistentSeitonCueDecision ObserveSeiton(
    PersistentSeitonCueState state,
    uint hp,
    long now,
    bool resourceReady = true,
    bool inRange = true,
    bool showPreparation = true,
    long stableExecuteMilliseconds = PersistentSeitonCueRules.StableExecuteMilliseconds) =>
    PersistentSeitonCueRules.Observe(
        state,
        resourceReady,
        targetPresent: true,
        trustedHealthSample: true,
        hp,
        maximumHp: 100,
        inRange,
        showPreparation,
        now,
        stableExecuteMilliseconds: stableExecuteMilliseconds);

static void True(bool condition, string label)
{
    if (!condition) throw new InvalidOperationException($"Expected true: {label}");
}

static void False(bool condition, string label) => True(!condition, label);

static void Equal<T>(T expected, T actual, string label)
    where T : notnull
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
}
