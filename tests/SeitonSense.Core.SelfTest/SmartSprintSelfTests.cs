using SeitonSense.Core;

internal static class SmartSprintSelfTests
{
    public static void ExactIdsDefaultsAndBoundsArePinned()
    {
        Equal(3U, SmartSprintRules.BaseSprintActionId, "base Sprint action carrier");
        Equal(29057U, SmartSprintRules.PvPSprintActionId, "PvP Sprint action");
        Equal(1342U, SmartSprintRules.PvPSprintStatusId, "PvP Sprint status");
        True(SmartSprintRules.RepeatProtectionDefaultEnabled, "repeat protection default");
        False(SmartSprintRules.IdleSprintDefaultEnabled, "idle Sprint is opt-in");
        Equal(3_000, SmartSprintRules.MinimumInactivityMilliseconds, "minimum inactivity");
        Equal(4_000, SmartSprintRules.DefaultInactivityMilliseconds, "default inactivity");
        Equal(5_000, SmartSprintRules.MaximumInactivityMilliseconds, "maximum inactivity");
        Equal(3_000, SmartSprintRules.NormalizeInactivityMilliseconds(-1), "low clamp");
        Equal(4_250, SmartSprintRules.NormalizeInactivityMilliseconds(4_250), "interior value");
        Equal(5_000, SmartSprintRules.NormalizeInactivityMilliseconds(99_999), "high clamp");
    }

    public static void RepeatProtectionNeedsAnExactPositiveSprint()
    {
        var valid = new SprintRepeatProtectionObservation(
            Enabled: true,
            IsActionRequest: true,
            SprintCarrierVerified: true,
            ResolvedActionId: SmartSprintRules.PvPSprintActionId,
            SprintMetadataVerified: true,
            SprintStatusKnown: true,
            ActiveSprintStatusId: SmartSprintRules.PvPSprintStatusId,
            SprintActive: true);

        True(SmartSprintRules.ShouldBlockRepeatPress(valid), "exact active Sprint re-press");
        False(SmartSprintRules.ShouldBlockRepeatPress(valid with { Enabled = false }), "toggle off");
        False(SmartSprintRules.ShouldBlockRepeatPress(valid with { IsActionRequest = false }), "non-action call");
        False(SmartSprintRules.ShouldBlockRepeatPress(valid with { SprintCarrierVerified = false }), "unverified raw carrier");
        False(SmartSprintRules.ShouldBlockRepeatPress(valid with { ResolvedActionId = 3 }), "different resolved action");
        False(SmartSprintRules.ShouldBlockRepeatPress(valid with { SprintMetadataVerified = false }), "metadata uncertainty");
        False(SmartSprintRules.ShouldBlockRepeatPress(valid with { SprintStatusKnown = false }), "status uncertainty");
        False(SmartSprintRules.ShouldBlockRepeatPress(valid with { ActiveSprintStatusId = 50 }), "different active status");
        False(SmartSprintRules.ShouldBlockRepeatPress(valid with { SprintActive = false }), "Sprint inactive");
    }

    public static void MovementDoesNotResetActionBarInactivity()
    {
        var first = SmartSprintRules.ObserveIdle(
            SmartSprintIdleState.Initial,
            ValidObservation(now: 1_000, activityToken: 0, heldGameplayKey: false));
        False(first.ShouldDispatch, "known token zero establishes the initial action-bar baseline");

        var before = SmartSprintRules.ObserveIdle(
            first.NextState,
            ValidObservation(now: 4_999, activityToken: 0, heldGameplayKey: true));
        False(before.ShouldDispatch, "3,999 ms remains below the default");

        var atBoundary = SmartSprintRules.ObserveIdle(
            before.NextState,
            ValidObservation(now: 5_000, activityToken: 0, heldGameplayKey: true));
        True(atBoundary.ShouldDispatch, "held movement/input does not reset action-bar inactivity");
        True(atBoundary.NextState.IdleEpisodeSpent, "the idle episode is spent before native dispatch");
    }

    public static void ActionBarActivityResetsAndRearms()
    {
        var state = SmartSprintRules.ObserveIdle(
            SmartSprintIdleState.Initial,
            ValidObservation(now: 0, activityToken: 0)).NextState;
        var dispatched = SmartSprintRules.ObserveIdle(
            state,
            ValidObservation(now: 4_000, activityToken: 0));
        True(dispatched.ShouldDispatch, "first idle episode dispatches once");

        var repeated = SmartSprintRules.ObserveIdle(
            dispatched.NextState,
            ValidObservation(now: 8_000, activityToken: 0));
        False(repeated.ShouldDispatch, "same action-bar idle episode cannot repeat");
        True(repeated.IdleThresholdReached, "the episode remains idle but spent");

        var action = SmartSprintRules.ObserveIdle(
            repeated.NextState,
            ValidObservation(now: 8_001, activityToken: 1));
        False(action.ShouldDispatch, "new action-bar input starts a new idle clock");
        True(action.ActionBarActivityChanged, "action-bar input change is explicit");
        False(action.NextState.IdleEpisodeSpent, "action-bar input rearms the helper");

        var later = SmartSprintRules.ObserveIdle(
            action.NextState,
            ValidObservation(now: 12_001, activityToken: 1));
        True(later.ShouldDispatch, "new action-bar episode can Sprint later");
    }

    public static void EveryDispatchGateWaitsWithoutSpending()
    {
        var baseline = SmartSprintRules.ObserveIdle(
            SmartSprintIdleState.Initial,
            ValidObservation(now: 0, activityToken: 1)).NextState;
        var eligible = ValidObservation(now: 4_000, activityToken: 1);
        var blocked = new[]
        {
            eligible with { HeldGameplayKeyEligible = false },
            eligible with { GuardStateKnown = false },
            eligible with { GuardActive = true },
            eligible with { IncapacitationStateKnown = false },
            eligible with { Incapacitated = true },
            eligible with { HigherPriorityClaimed = true },
            eligible with { SprintMetadataVerified = false },
            eligible with { SprintStatusKnown = false },
            eligible with { SprintActive = true },
            eligible with { SprintLocallyReady = false },
        };

        foreach (var observation in blocked)
        {
            var wait = SmartSprintRules.ObserveIdle(baseline, observation);
            False(wait.ShouldDispatch, $"gate waits: {observation}");
            False(wait.NextState.IdleEpisodeSpent, $"gate does not spend: {observation}");
            True(wait.IdleThresholdReached, $"idle ownership remains: {observation}");
        }

        var cleared = SmartSprintRules.ObserveIdle(
            baseline,
            eligible with { NowMilliseconds = 4_001 });
        True(cleared.ShouldDispatch, "cleared gates use the still-open idle episode");
    }

    public static void UnknownActivityAndContextDriftResetSafely()
    {
        var state = new SmartSprintIdleState(
            HasActionBarActivityBaseline: true,
            ActionBarActivityToken: 5,
            LastActionBarActivityAtMilliseconds: 10_000,
            IdleEpisodeSpent: true);
        var unknown = SmartSprintRules.ObserveIdle(
            state,
            ValidObservation(now: 11_000, activityToken: 5) with
            {
                ActionBarActivityKnown = false,
            });
        Equal(SmartSprintIdleState.Initial, unknown.NextState, "unknown action telemetry resets");
        False(unknown.ShouldDispatch, "unknown action telemetry cannot Sprint");

        var outside = SmartSprintRules.ObserveIdle(
            state,
            ValidObservation(now: 11_000, activityToken: 5) with
            {
                IsSupportedPvpContext = false,
            });
        Equal(SmartSprintIdleState.Initial, outside.NextState, "context exit resets");

        var rolledBack = SmartSprintRules.ObserveIdle(
            state,
            ValidObservation(now: 9_999, activityToken: 5));
        True(rolledBack.NextState.HasActionBarActivityBaseline, "clock rollback makes a fresh baseline");
        Equal(9_999L, rolledBack.NextState.LastActionBarActivityAtMilliseconds, "rollback baseline time");
        False(rolledBack.NextState.IdleEpisodeSpent, "rollback cannot inherit a spent episode");
        False(rolledBack.ShouldDispatch, "rollback is silent");
    }

    private static SmartSprintIdleObservation ValidObservation(
        long now,
        ulong activityToken,
        bool heldGameplayKey = true) =>
        new(
            Enabled: true,
            IsSupportedPvpContext: true,
            IsLocalPlayerValidAndAlive: true,
            ActionBarActivityKnown: true,
            ActionBarActivityToken: activityToken,
            HeldGameplayKeyEligible: heldGameplayKey,
            GuardStateKnown: true,
            GuardActive: false,
            IncapacitationStateKnown: true,
            Incapacitated: false,
            HigherPriorityClaimed: false,
            SprintMetadataVerified: true,
            SprintStatusKnown: true,
            SprintActive: false,
            SprintLocallyReady: true,
            InactivityMilliseconds: SmartSprintRules.DefaultInactivityMilliseconds,
            NowMilliseconds: now);

    private static void True(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void False(bool condition, string message) => True(!condition, message);

    private static void Equal<T>(T expected, T actual, string message) where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
    }
}
