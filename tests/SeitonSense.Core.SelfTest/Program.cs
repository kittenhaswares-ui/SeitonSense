using SeitonSense.Core;

var tests = new (string Name, Action Run)[]
{
    ("Ninja job gate is exact", NinjaJobGateIsExact),
    ("execute threshold is strictly below half", ExecuteThresholdIsStrict),
    ("invalid HP fails closed", InvalidHpFailsClosed),
    ("enemy slot labels are exact", EnemySlotLabelsAreExact),
    ("enemy validation fails closed", EnemyValidationFailsClosed),
    ("label appears on first actionable sample", LabelAppearsImmediately),
    ("flash requires two actionable samples", FlashRequiresTwoSamples),
    ("flash does not repeat in the same window", FlashDoesNotRepeat),
    ("range or readiness loss does not rearm", ReadinessLossDoesNotRearm),
    ("stable healing rearms once", StableHealingRearms),
    ("flash timeline is bounded", FlashTimelineIsBounded),
    ("native range result accepts facing-only failure", NativeRangeResultIsExact),
    ("known CC territories are complete", KnownCcTerritoriesAreComplete),
    ("CC matching remains fail closed", CcMatchingIsFailClosed),
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

static void LabelAppearsImmediately()
{
    var decision = ExecuteAlertRules.Observe(ExecuteAlertState.Initial, 49, 100, true, 1000);
    True(decision.ShowLabel, "label");
    False(decision.TriggerFlash, "first sample flash");
}

static void FlashRequiresTwoSamples()
{
    var first = ExecuteAlertRules.Observe(ExecuteAlertState.Initial, 49, 100, true, 1000);
    var second = ExecuteAlertRules.Observe(first.NextState, 49, 100, true, 1050);
    True(second.ShowLabel, "second label");
    True(second.TriggerFlash, "second sample flash");
    False(second.NextState.Armed, "latch consumed");
}

static void FlashDoesNotRepeat()
{
    var state = ExecuteAlertState.Initial;
    state = ExecuteAlertRules.Observe(state, 49, 100, true, 1000).NextState;
    state = ExecuteAlertRules.Observe(state, 49, 100, true, 1050).NextState;
    var third = ExecuteAlertRules.Observe(state, 10, 100, true, 1100);
    True(third.ShowLabel, "continued label");
    False(third.TriggerFlash, "no repeat");
}

static void ReadinessLossDoesNotRearm()
{
    var state = ExecuteAlertState.Initial;
    state = ExecuteAlertRules.Observe(state, 49, 100, true, 1000).NextState;
    state = ExecuteAlertRules.Observe(state, 49, 100, true, 1050).NextState;
    state = ExecuteAlertRules.Observe(state, 49, 100, false, 1100).NextState;
    var availableAgain = ExecuteAlertRules.Observe(state, 49, 100, true, 1150);
    False(availableAgain.TriggerFlash, "range/readiness toggle cannot rearm");
}

static void StableHealingRearms()
{
    var state = ExecuteAlertState.Initial;
    state = ExecuteAlertRules.Observe(state, 49, 100, true, 1000).NextState;
    state = ExecuteAlertRules.Observe(state, 49, 100, true, 1050).NextState;
    state = ExecuteAlertRules.Observe(state, 52, 100, false, 1100).NextState;
    state = ExecuteAlertRules.Observe(state, 52, 100, false, 1499).NextState;
    False(state.Armed, "not yet rearmed");
    state = ExecuteAlertRules.Observe(state, 52, 100, false, 1500).NextState;
    True(state.Armed, "rearmed after 400ms");
    state = ExecuteAlertRules.Observe(state, 49, 100, true, 1550).NextState;
    var secondWindow = ExecuteAlertRules.Observe(state, 49, 100, true, 1600);
    True(secondWindow.TriggerFlash, "new execute window flashes once");
}

static void FlashTimelineIsBounded()
{
    Equal(0f, FlashTimeline.Remaining01(0, 0, 0), "empty timeline");
    Equal(0f, FlashTimeline.Remaining01(999, 1000, 1400), "before start");
    Equal(1f, FlashTimeline.Remaining01(1000, 1000, 1400), "start");
    Equal(0.5f, FlashTimeline.Remaining01(1200, 1000, 1400), "middle");
    Equal(0f, FlashTimeline.Remaining01(1400, 1000, 1400), "end");
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
