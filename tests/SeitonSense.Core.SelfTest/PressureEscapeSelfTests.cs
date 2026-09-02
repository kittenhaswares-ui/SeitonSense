using SeitonSense.Core;

internal static class PressureEscapeSelfTests
{
    public static void DirectThresholdIsInclusiveAndUnknownFailsClosed()
    {
        False(PressureEscapeRules.IsHighPressure(false, 99), "unknown pressure is never high");
        False(PressureEscapeRules.IsHighPressure(true, 2), "two direct enemies are below threshold");
        True(PressureEscapeRules.IsHighPressure(true, 3), "three direct enemies meet threshold");
    }

    public static void WarningEntryIsImmediateAndClearIsDebounced()
    {
        var decision = Observe(PressureEscapeWarningState.Initial, 1_000, true, 3);
        True(decision.WarningActive, "the first fresh exact high-pressure sample is visible");
        True(decision.EnteredWarning, "the first visible sample owns one entry token");
        Equal(1UL, decision.NextState.EpisodeToken, "first episode token is exact");

        decision = Observe(decision.NextState, 1_001, true, 2);
        True(decision.WarningActive, "fresh low pressure retains the visual clear grace");
        False(decision.HighPressure, "clear grace is never a live Sprint signal");

        decision = Observe(decision.NextState, 1_300, true, 2);
        True(decision.WarningActive, "visual remains until 300 ms of known-safe pressure");

        decision = Observe(decision.NextState, 1_301, true, 2);
        False(decision.WarningActive, "visual clears at the 300 ms boundary");

        decision = Observe(decision.NextState, 1_302, true, 3);
        True(decision.EnteredWarning, "a later exact episode enters once");
        Equal(2UL, decision.NextState.EpisodeToken, "later episode gets a new token");
    }

    public static void UnknownOrStalePressureClearsImmediately()
    {
        var visible = new PressureEscapeWarningState(true, true, -1, 7);
        var unknown = Observe(visible, 2_050, false, 3);
        False(unknown.WarningActive, "unknown publication bypasses visual clear grace");
        Equal(7UL, unknown.NextState.EpisodeToken, "unknown input preserves token monotonicity");

        var resumed = Observe(unknown.NextState, 2_060, true, 3);
        True(resumed.WarningActive, "fresh high pressure restores the same visual episode");
        False(resumed.EnteredWarning, "unknown gap cannot rearm sound or Sprint");
        Equal(7UL, resumed.NextState.EpisodeToken, "unknown gap retains the same episode token");

        var safe = Observe(unknown.NextState, 2_100, true, 2);
        False(safe.WarningActive, "known-safe grace does not redisplay an unknown-hidden card");
        var cleared = Observe(safe.NextState, 2_400, true, 2);
        False(cleared.NextState.EpisodeOpen, "300 ms of known-safe pressure closes the episode");
        var rearmed = Observe(cleared.NextState, 2_401, true, 3);
        True(rearmed.EnteredWarning, "only a completed known-safe separation rearms");
        Equal(8UL, rearmed.NextState.EpisodeToken, "rearmed episode receives a new token");

        var invalidClock = new PressureEscapeWarningState(true, true, 3_000, 7);
        var rolledBack = Observe(invalidClock, 2_999, true, 2);
        False(rolledBack.WarningActive, "clock rollback fails closed");
    }

    public static void SprintRequiresEveryExactGateAndMovementKey()
    {
        var valid = new PressureEscapeSprintObservation(
            Enabled: true,
            IsCrystallineConflict: true,
            IsLocalPlayerValidAndAlive: true,
            SprintMetadataVerified: true,
            PressureKnown: true,
            DirectEnemyCount: 3,
            GuardSuppressed: false,
            SprintActive: false,
            Incapacitated: false,
            HigherPriorityClaimed: false,
            EpisodeAvailable: true,
            HeldMovementKeyEligible: true,
            HeldMovementVirtualKey: PressureEscapeRules.WVirtualKey,
            SprintLocallyReady: true);

        True(PressureEscapeRules.CanDispatchSprint(valid), "all exact gates allow one Sprint intent");
        True(PressureEscapeRules.CanFreezeSprintIntent(
                valid with { SprintLocallyReady = false }),
            "cooldown wait may freeze one exact movement-key intent");
        False(PressureEscapeRules.CanDispatchSprint(valid with { PressureKnown = false }), "unknown pressure blocks");
        False(PressureEscapeRules.CanDispatchSprint(valid with { DirectEnemyCount = 2 }), "two targets block");
        False(PressureEscapeRules.CanDispatchSprint(valid with { GuardSuppressed = true }), "Guard blocks");
        False(PressureEscapeRules.CanDispatchSprint(valid with { SprintActive = true }), "active Sprint blocks");
        False(PressureEscapeRules.CanDispatchSprint(valid with { Incapacitated = true }), "hard CC blocks");
        False(PressureEscapeRules.CanDispatchSprint(valid with { HigherPriorityClaimed = true }), "priority blocks");
        False(PressureEscapeRules.CanDispatchSprint(valid with { EpisodeAvailable = false }), "spent episode blocks");
        False(PressureEscapeRules.CanDispatchSprint(valid with { HeldMovementVirtualKey = 0x31 }), "action key blocks");
        False(PressureEscapeRules.CanDispatchSprint(valid with { SprintLocallyReady = false }), "cooldown blocks");
    }

    public static void MovementKeySetIsNarrow()
    {
        foreach (var key in new[] { 0x25, 0x26, 0x27, 0x28, 0x41, 0x44, 0x53, 0x57 })
            True(PressureEscapeRules.IsSupportedMovementVirtualKey(key), $"movement key {key:X} is allowed");

        foreach (var key in new[] { 0x20, 0x31, 0x45, 0x58, 0x70 })
            False(PressureEscapeRules.IsSupportedMovementVirtualKey(key), $"non-movement key {key:X} is rejected");
    }

    public static void WarningEpisodeTokenWrapsToANonZeroValue()
    {
        var previous = new PressureEscapeWarningState(false, false, -1, ulong.MaxValue);
        var decision = Observe(previous, 9_000, true, 3);
        Equal(1UL, decision.NextState.EpisodeToken, "overflow wraps to the first valid token");
        True(decision.EnteredWarning, "wrapped token still represents one new entry");
    }

    private static PressureEscapeWarningDecision Observe(
        PressureEscapeWarningState state,
        long now,
        bool pressureKnown,
        int directEnemyCount) =>
        PressureEscapeRules.ObserveWarning(
            state,
            new PressureEscapeWarningObservation(
                Enabled: true,
                IsCrystallineConflict: true,
                IsLocalPlayerValidAndAlive: true,
                pressureKnown,
                directEnemyCount,
                now));

    private static void True(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static void False(bool value, string message) => True(!value, message);

    private static void Equal<T>(T expected, T actual, string message) where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
    }
}
