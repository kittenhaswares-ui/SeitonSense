using SeitonSense.Core;

internal static class AutoLowMpFocusTargetSelfTests
{
    private static readonly TargetPressureActorIdentity LocalPlayer = new(0x100, 0x200);

    public static void CanonicalSetAndEligibilityAreStrict()
    {
        var candidates = Candidates();
        True(
            AutoLowMpFocusTargetRules.HasCompleteExactCanonicalSet(candidates),
            "five exact unique canonical enemies are complete");
        True(
            AutoLowMpFocusTargetRules.IsEligibleCandidate(candidates[0], LocalPlayer),
            "trusted low MP below 2000 with native reachability is eligible");

        var duplicate = candidates.ToArray();
        duplicate[4] = duplicate[4] with { Actor = duplicate[0].Actor };
        False(
            AutoLowMpFocusTargetRules.HasCompleteExactCanonicalSet(duplicate),
            "duplicate actor identity fails the whole set");
        False(
            AutoLowMpFocusTargetRules.HasCompleteExactCanonicalSet(candidates[..4]),
            "missing canonical slot fails the whole set");
        False(
            AutoLowMpFocusTargetRules.IsEligibleCandidate(
                candidates[0] with { TrustedLowMp = false },
                LocalPlayer),
            "raw MP without the trusted low-MP latch is ineligible");
        True(
            AutoLowMpFocusTargetRules.IsEligibleCandidate(
                candidates[0] with { CurrentMp = LowMpRules.RecuperateCost },
                LocalPlayer),
            "exactly 2000 MP is eligible for Auto Focus");
        False(
            AutoLowMpFocusTargetRules.IsEligibleCandidate(
                candidates[0] with { CurrentMp = LowMpRules.RecuperateCost + 1 },
                LocalPlayer),
            "2001 MP is above the Auto Focus entry boundary");
        False(
            AutoLowMpFocusTargetRules.IsEligibleCandidate(
                candidates[0] with { NativeRangeAndLineOfSight = false },
                LocalPlayer),
            "native range or line-of-sight failure is ineligible");
    }

    public static void RankingIsMpThenHpThenStableIdentity()
    {
        var candidates = Candidates();
        candidates[0] = candidates[0] with { CurrentMp = 1_500, CurrentHp = 20 };
        candidates[1] = candidates[1] with { CurrentMp = 1_000, CurrentHp = 80 };
        Equal(
            1,
            AutoLowMpFocusTargetRules.SelectBestCandidateIndex(candidates, LocalPlayer),
            "lower exact MP ratio wins before HP");

        candidates[0] = candidates[0] with { CurrentMp = 1_000, CurrentHp = 30 };
        Equal(
            0,
            AutoLowMpFocusTargetRules.SelectBestCandidateIndex(candidates, LocalPlayer),
            "lower exact HP ratio breaks an MP tie");

        candidates[0] = candidates[0] with { CurrentHp = 80 };
        Equal(
            0,
            AutoLowMpFocusTargetRules.SelectBestCandidateIndex(candidates, LocalPlayer),
            "stable lower enemy slot breaks an exact resource tie");
    }

    public static void InclusiveThresholdUsesAnIndependentTrustedLatch()
    {
        var global = LowMpRules.Observe(LowMpState.Initial, 2_000, true, 1_000);
        global = LowMpRules.Observe(global, 2_000, true, 1_150);
        False(global.IsUnavailable, "the existing global low-MP display remains strict below 2000");

        var focus = LowMpRules.Observe(
            LowMpState.Initial,
            2_000,
            true,
            2_000,
            enterThreshold: AutoLowMpFocusTargetRules.ObservationEnterThreshold);
        focus = LowMpRules.Observe(
            focus,
            2_000,
            true,
            2_150,
            enterThreshold: AutoLowMpFocusTargetRules.ObservationEnterThreshold);
        True(focus.IsUnavailable, "Auto Focus enters after a trusted stable exact-2000 sample");

        var above = LowMpRules.Observe(
            LowMpState.Initial,
            2_001,
            true,
            3_000,
            enterThreshold: AutoLowMpFocusTargetRules.ObservationEnterThreshold);
        above = LowMpRules.Observe(
            above,
            2_001,
            true,
            3_150,
            enterThreshold: AutoLowMpFocusTargetRules.ObservationEnterThreshold);
        False(above.IsUnavailable, "2001 cannot enter the independent Auto Focus latch");

        focus = LowMpRules.Observe(
            focus,
            2_300,
            true,
            3_200,
            enterThreshold: AutoLowMpFocusTargetRules.ObservationEnterThreshold);
        focus = LowMpRules.Observe(
            focus,
            2_300,
            true,
            3_350,
            enterThreshold: AutoLowMpFocusTargetRules.ObservationEnterThreshold);
        False(focus.IsUnavailable, "the independent latch retains the existing 2300 exit boundary");
    }

    public static void EmptyFocusMustBeStableAndWaveIsOneShot()
    {
        var first = Observe(
            AutoLowMpFocusTargetState.Initial,
            1_000,
            AutoLowMpFocusObservedState.Empty,
            Candidates());
        Equal(
            AutoLowMpFocusTargetDecisionReason.FocusNotStableEmpty,
            first.Reason,
            "first empty focus sample starts the stability window");

        var early = Observe(first.State, 1_099, AutoLowMpFocusObservedState.Empty, Candidates());
        False(early.ShouldSetFocus, "99 ms is below the empty-focus boundary");

        var ready = Observe(early.State, 1_100, AutoLowMpFocusObservedState.Empty, Candidates());
        True(ready.ShouldSetFocus, "100 ms of empty focus allows one exact intent");
        Equal(1, ready.Intent?.EnemySlot ?? 0, "the selected exact slot is frozen");
        True(ready.State.AttemptSpentForWave, "the wave is spent before runtime mutation");

        var repeated = Observe(ready.State, 2_500, AutoLowMpFocusObservedState.Empty, Candidates());
        False(repeated.ShouldSetFocus, "the same continuous low-MP wave cannot retry");
        Equal(
            AutoLowMpFocusTargetDecisionReason.WaveAlreadySpent,
            repeated.Reason,
            "same-wave suppression is explicit");
    }

    public static void OccupiedFocusSpendsWaveWithoutDelayedMutation()
    {
        var occupied = Observe(
            AutoLowMpFocusTargetState.Initial,
            3_000,
            AutoLowMpFocusObservedState.Occupied,
            Candidates(),
            new TargetPressureActorIdentity(0x900, 0x901));
        False(occupied.ShouldSetFocus, "an existing focus is never overwritten");
        True(occupied.State.AttemptSpentForWave, "occupied focus consumes the old wave");

        var cleared = Observe(
            occupied.State,
            3_500,
            AutoLowMpFocusObservedState.Empty,
            Candidates());
        False(cleared.ShouldSetFocus, "clearing a manual focus cannot trigger a delayed set");
        Equal(
            AutoLowMpFocusTargetDecisionReason.WaveAlreadySpent,
            cleared.Reason,
            "cleared manual focus leaves the current wave spent");
    }

    public static void ASeparatedWaveCanRearmWithoutRetryingFailure()
    {
        var state = Observe(
            AutoLowMpFocusTargetState.Initial,
            4_000,
            AutoLowMpFocusObservedState.Empty,
            Candidates()).State;
        var attempt = Observe(state, 4_100, AutoLowMpFocusObservedState.Empty, Candidates());
        True(attempt.ShouldSetFocus, "first wave emits one setter intent");
        state = AutoLowMpFocusTargetRules.ApplySetOutcome(
            attempt.State,
            attempt.Intent!.Value,
            AutoLowMpFocusTargetSetOutcome.TerminalFailure);

        var failedRepeat = Observe(state, 6_000, AutoLowMpFocusObservedState.Empty, Candidates());
        False(failedRepeat.ShouldSetFocus, "terminal setter failure never retries the wave");

        var noWave = Observe(
            failedRepeat.State,
            6_100,
            AutoLowMpFocusObservedState.Empty,
            Candidates(lowMp: false));
        False(noWave.State.LowMpWaveActive, "a complete known-safe separation closes the wave");

        var rearmed = Observe(noWave.State, 6_200, AutoLowMpFocusObservedState.Empty, Candidates());
        True(rearmed.ShouldSetFocus, "a later distinct low-MP wave can emit one new intent");
    }

    public static void IntermediateMpCannotRearmASpentWave()
    {
        var primed = Observe(
            AutoLowMpFocusTargetState.Initial,
            6_500,
            AutoLowMpFocusObservedState.Empty,
            Candidates());
        var attempt = Observe(primed.State, 6_600, AutoLowMpFocusObservedState.Empty, Candidates());
        True(attempt.ShouldSetFocus, "the initial trusted wave emits one intent");

        var intermediate = Candidates()
            .Select(candidate => candidate with
            {
                TrustedLowMp = false,
                CurrentMp = 2_001,
            })
            .ToArray();
        var stillSpent = Observe(
            attempt.State,
            6_700,
            AutoLowMpFocusObservedState.Empty,
            intermediate);
        True(stillSpent.State.LowMpWaveActive, "2001 through 2299 remains inside the exit-hysteresis wave");
        True(stillSpent.State.AttemptSpentForWave, "the spent bit survives intermediate MP");

        var returnedLow = Observe(
            stillSpent.State,
            6_800,
            AutoLowMpFocusObservedState.Empty,
            Candidates());
        False(returnedLow.ShouldSetFocus, "2000 after an intermediate sample cannot produce a second intent");
        Equal(
            AutoLowMpFocusTargetDecisionReason.WaveAlreadySpent,
            returnedLow.Reason,
            "the original wave remains spent");
    }

    public static void UnknownMpCannotRearmASpentWave()
    {
        var primed = Observe(
            AutoLowMpFocusTargetState.Initial,
            6_900,
            AutoLowMpFocusObservedState.Empty,
            Candidates());
        var attempt = Observe(primed.State, 7_000, AutoLowMpFocusObservedState.Empty, Candidates());
        True(attempt.ShouldSetFocus, "the initial trusted wave emits one intent");

        var unknown = Candidates()
            .Select(candidate => candidate with
            {
                TrustedLowMp = false,
                CurrentMp = 0,
                MaximumMp = 0,
            })
            .ToArray();
        var stillSpent = Observe(
            attempt.State,
            7_100,
            AutoLowMpFocusObservedState.Empty,
            unknown);
        True(stillSpent.State.LowMpWaveActive, "unknown telemetry preserves an established low-MP wave");
        True(stillSpent.State.AttemptSpentForWave, "unknown telemetry cannot clear the spent bit");

        var returnedLow = Observe(
            stillSpent.State,
            7_200,
            AutoLowMpFocusObservedState.Empty,
            Candidates());
        False(returnedLow.ShouldSetFocus, "trusted low MP after an unknown gap cannot retry");
    }

    public static void ConfirmedFocusDriftLatchesUntilExplicitReset()
    {
        var primed = Observe(
            AutoLowMpFocusTargetState.Initial,
            7_000,
            AutoLowMpFocusObservedState.Empty,
            Candidates());
        var attempt = Observe(primed.State, 7_100, AutoLowMpFocusObservedState.Empty, Candidates());
        var intent = attempt.Intent!.Value;
        var confirmed = AutoLowMpFocusTargetRules.ApplySetOutcome(
            attempt.State,
            intent,
            AutoLowMpFocusTargetSetOutcome.ExactReadbackConfirmed);
        Equal(intent.Target, confirmed.LastConfirmedFocusTarget, "exact readback is diagnostic ownership only");

        var unchanged = Observe(
            confirmed,
            7_200,
            AutoLowMpFocusObservedState.Occupied,
            Candidates(),
            intent.Target);
        False(unchanged.State.ManualOverrideLatched, "unchanged exact focus does not latch");

        var drifted = Observe(
            unchanged.State,
            7_300,
            AutoLowMpFocusObservedState.Empty,
            Candidates());
        True(drifted.State.ManualOverrideLatched, "manual, game, or external clear latches automation off");
        False(drifted.ShouldSetFocus, "drift never restores the old focus");

        var reset = AutoLowMpFocusTargetRules.Observe(
            drifted.State,
            Observation(
                7_400,
                AutoLowMpFocusObservedState.Empty,
                Candidates(),
                configurationEnabled: false));
        Equal(
            AutoLowMpFocusTargetState.Initial,
            reset.State,
            "explicit disable resets only internal state and authorizes a later re-arm");
    }

    public static void FrozenIntentRequiresEveryFinalGate()
    {
        var candidate = Candidates()[0];
        var intent = new AutoLowMpFocusTargetIntent(
            candidate.EnemySlot,
            LocalPlayer,
            candidate.Actor,
            candidate.CurrentMp,
            candidate.MaximumMp);

        True(
            AutoLowMpFocusTargetRules.CanSetFrozenIntent(
                intent,
                candidate,
                true,
                true,
                true,
                LocalPlayer,
                true,
                AutoLowMpFocusObservedState.Empty),
            "all frozen final gates allow the sole setter");
        False(
            AutoLowMpFocusTargetRules.CanSetFrozenIntent(
                intent,
                candidate with { CurrentMp = 2_001 },
                true,
                true,
                true,
                LocalPlayer,
                true,
                AutoLowMpFocusObservedState.Empty),
            "MP recovery above 2000 at the boundary cancels the spent intent");
        False(
            AutoLowMpFocusTargetRules.CanSetFrozenIntent(
                intent,
                candidate,
                true,
                true,
                true,
                LocalPlayer,
                true,
                AutoLowMpFocusObservedState.Occupied),
            "a focus appearing at the boundary blocks without overwrite");
        False(
            AutoLowMpFocusTargetRules.CanSetFrozenIntent(
                intent,
                candidate with { Actor = new TargetPressureActorIdentity(0x777, 0x888) },
                true,
                true,
                true,
                LocalPlayer,
                true,
                AutoLowMpFocusObservedState.Empty),
            "frozen identity drift cannot choose an alternate");
    }

    private static AutoLowMpFocusTargetDecision Observe(
        AutoLowMpFocusTargetState state,
        long now,
        AutoLowMpFocusObservedState focusState,
        IReadOnlyList<AutoLowMpFocusTargetCandidate> candidates,
        TargetPressureActorIdentity focusTarget = default) =>
        AutoLowMpFocusTargetRules.Observe(
            state,
            Observation(now, focusState, candidates, focusTarget));

    private static AutoLowMpFocusTargetObservation Observation(
        long now,
        AutoLowMpFocusObservedState focusState,
        IReadOnlyList<AutoLowMpFocusTargetCandidate> candidates,
        TargetPressureActorIdentity focusTarget = default,
        bool configurationEnabled = true) =>
        new(
            configurationEnabled,
            IsCrystallineConflict: true,
            LocalPlayerExactAndAlive: true,
            LocalPlayer,
            MetadataVerified: true,
            TextInputStateKnown: true,
            TextInputActive: false,
            CompleteCanonicalEnemySet: true,
            focusState,
            focusTarget,
            now,
            candidates,
            HardReset: false);

    private static AutoLowMpFocusTargetCandidate[] Candidates(bool lowMp = true) =>
        Enumerable.Range(EnemySlotRules.FirstSlot, EnemySlotRules.LastSlot)
            .Select(slot => new AutoLowMpFocusTargetCandidate(
                slot,
                new TargetPressureActorIdentity((ulong)(0x1000 + slot), (uint)(0x2000 + slot)),
                ExactCanonicalIdentity: true,
                Alive: true,
                Targetable: true,
                CurrentHp: 80,
                MaximumHp: 100,
                LowMpWaveLatched: lowMp,
                TrustedLowMp: lowMp,
                CurrentMp: lowMp ? 1_000u : 5_000u,
                MaximumMp: 10_000,
                NativeTargetValid: true,
                NativeRangeAndLineOfSight: true))
            .ToArray();

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
