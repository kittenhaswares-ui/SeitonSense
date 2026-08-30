using SeitonSense.Core;

internal static class EmergencyPurifyBufferSelfTests
{
    private const int HeldKey = 65;
    private static readonly PurifyCcStatusInstance StatusA = new(1343, 1);
    private static readonly PurifyCcStatusInstance StatusB = new(4325, 2);

    public static void AutomaticStatusArmsAndDispatchesWithoutAPhysicalKey()
    {
        var armed = Observe(
            EmergencyPurifyBufferState.Initial,
            StatusA,
            locallyReady: false,
            now: 1_000,
            automaticStatusTriggerEnabled: true);
        Equal(EmergencyPurifyBufferDecisionKind.Armed, armed.Kind, "automatic status arms");
        Equal(EmergencyPurifyInputTrigger.AutomaticStatus, armed.InputTrigger, "automatic trigger");
        Equal(0, armed.NextState.FrozenKeyCode, "automatic intent has no physical key");
        False(armed.ShouldClaimInputFrame, "automatic intent does not claim a physical key frame");
        False(armed.ShouldConsumeInputGeneration, "automatic intent does not consume a key generation");
        True(
            EmergencyPurifyBufferRules.ClaimsSchedulerPriority(
                armed.NextState,
                StatusA,
                exactStatusCurrentlyObserved: true,
                exactFrozenKeyStillDown: false),
            "automatic intent still claims scheduler priority");

        var dispatched = Observe(
            armed.NextState,
            StatusA,
            locallyReady: true,
            now: 1_001,
            frozenKeyStillDown: false,
            automaticStatusTriggerEnabled: true);
        True(dispatched.ShouldDispatch, "automatic intent dispatches on first ready frame");
        Equal(EmergencyPurifyInputTrigger.AutomaticStatus, dispatched.InputTrigger, "dispatch trigger");
        Equal(0, dispatched.NextState.FrozenKeyCode, "dispatch remains keyless");
        False(dispatched.ShouldClaimInputFrame, "keyless dispatch does not claim a physical key frame");
    }

    public static void AutomaticPreNativeSoftWaitRetainsExactStatusAndRetriesNextFrame()
    {
        var dispatched = Observe(
            EmergencyPurifyBufferState.Initial,
            StatusA,
            locallyReady: true,
            now: 1_000,
            automaticStatusTriggerEnabled: true);
        True(dispatched.ShouldDispatch, "automatic status reaches the first attempt boundary");

        var soft = Complete(
            dispatched.NextState,
            ClientActionAttemptOutcome.SoftUnavailable,
            1_000);
        True(soft.SoftWait, "pre-native drift is a soft wait");
        False(soft.Terminal, "pre-native drift is not terminal");
        Equal(EmergencyPurifyBufferPhase.Buffered, soft.NextState.Phase, "exact CC episode remains buffered");
        Equal(StatusA, soft.NextState.StatusInstance!.Value, "exact CC identity is retained");
        Equal(EmergencyPurifyInputTrigger.AutomaticStatus, soft.NextState.FrozenInputTrigger, "automatic consent is retained");
        Equal(0, soft.NextState.NativeAttemptCount, "pre-native wait spends no native call");

        var retry = Observe(
            soft.NextState,
            StatusA,
            locallyReady: true,
            now: 1_001,
            frozenKeyStillDown: false,
            automaticStatusTriggerEnabled: true);
        True(retry.ShouldDispatch, "same automatic CC episode retries on the next ready frame");
        Equal(EmergencyPurifyInputTrigger.AutomaticStatus, retry.InputTrigger, "retry remains keyless automatic consent");
    }

    public static void DisablingAutomaticModeCancelsTheKeylessIntent()
    {
        var armed = Observe(
            EmergencyPurifyBufferState.Initial,
            StatusA,
            locallyReady: false,
            now: 1_000,
            automaticStatusTriggerEnabled: true);

        var disabled = Observe(
            armed.NextState,
            StatusA,
            locallyReady: true,
            now: 1_001,
            frozenKeyStillDown: false,
            automaticStatusTriggerEnabled: false);
        Equal(EmergencyPurifyBufferDecisionKind.Cancelled, disabled.Kind, "mode disable cancels");
        Equal(
            EmergencyPurifyBufferCancelReason.TriggerModeDisabled,
            disabled.CancelReason,
            "mode disable reason");
        Equal(EmergencyPurifyBufferPhase.WaitingForFreshKey, disabled.NextState.Phase, "status remains observed");
        False(disabled.ShouldDispatch, "disabled automatic intent cannot dispatch");
        False(
            EmergencyPurifyBufferRules.ClaimsSchedulerPriority(
                disabled.NextState,
                StatusA,
                exactStatusCurrentlyObserved: true,
                exactFrozenKeyStillDown: false),
            "cancelled keyless intent releases scheduler priority");

        var stillDisabled = Observe(
            disabled.NextState,
            StatusA,
            locallyReady: true,
            now: 1_002,
            automaticStatusTriggerEnabled: false);
        False(stillDisabled.ShouldDispatch, "same status cannot revive disabled automatic intent");
    }

    public static void AutomaticStatusIsOneShotButAReplacementIsANewEpisode()
    {
        var first = Observe(
            EmergencyPurifyBufferState.Initial,
            StatusA,
            locallyReady: true,
            now: 1_000,
            automaticStatusTriggerEnabled: true);
        True(first.ShouldDispatch, "first automatic status dispatches");
        var accepted = Complete(
            first.NextState,
            ClientActionAttemptOutcome.ClientAccepted,
            1_000);

        var suppressed = EmergencyPurifyBufferRules.Observe(
            accepted.NextState,
            ValidObservation(StatusA, 1_001) with
            {
                ConfigurationEnabled = false,
                AutomaticStatusTriggerEnabled = true,
                PurifyLocallyReady = true,
            });
        False(suppressed.ShouldDispatch, "temporary automatic suppression cannot duplicate acceptance");
        Equal(
            EmergencyPurifyBufferPhase.SpentUntilStatusGone,
            suppressed.NextState.Phase,
            "temporary suppression preserves the terminal exact-status latch");

        var duplicate = Observe(
            suppressed.NextState,
            StatusA,
            locallyReady: true,
            now: 1_002,
            automaticStatusTriggerEnabled: true);
        False(duplicate.ShouldDispatch, "restoring automatic mode cannot duplicate the lingering status");
        Equal(EmergencyPurifyBufferPhase.SpentUntilStatusGone, duplicate.NextState.Phase, "same status stays spent");

        var goneWhileSuppressed = EmergencyPurifyBufferRules.Observe(
            accepted.NextState,
            ValidObservation(null, 1_003) with
            {
                ConfigurationEnabled = false,
                AutomaticStatusTriggerEnabled = true,
            });
        Equal(
            EmergencyPurifyBufferState.Initial,
            goneWhileSuppressed.NextState,
            "real status absence still clears a suppressed terminal episode");

        var disabledWithFreshKey = Observe(
            duplicate.NextState,
            StatusA,
            freshKey: true,
            locallyReady: true,
            now: 1_004,
            automaticStatusTriggerEnabled: false);
        False(disabledWithFreshKey.ShouldDispatch, "mode change cannot revive the spent status");
        Equal(
            EmergencyPurifyBufferPhase.SpentUntilStatusGone,
            disabledWithFreshKey.NextState.Phase,
            "terminal identity survives automatic mode disable");

        var replacement = Observe(
            disabledWithFreshKey.NextState,
            StatusB,
            heldKeyEligible: true,
            allowHeldKey: true,
            locallyReady: true,
            now: 1_005,
            automaticStatusTriggerEnabled: false);
        True(replacement.ShouldDispatch, "replacement status may take held consent on its entry frame");
        Equal(StatusB, replacement.NextState.StatusInstance!.Value, "replacement identity frozen");
        Equal(HeldKey, replacement.NextState.FrozenKeyCode, "replacement freezes the live held key");
        Equal(
            EmergencyPurifyInputTrigger.HeldKeyAtStatusEntry,
            replacement.InputTrigger,
            "replacement resolves the currently enabled trigger lane");
    }

    public static void SameFrameFreshKeyCanDispatch()
    {
        var decision = Observe(
            EmergencyPurifyBufferState.Initial,
            StatusA,
            freshKey: true,
            locallyReady: true,
            now: 1_000);
        True(decision.ShouldDispatch, "same-frame fresh edge");
        Equal(EmergencyPurifyBufferPhase.Buffered, decision.NextState.Phase, "intent remains exact until outcome");

        var accepted = Complete(
            decision.NextState,
            ClientActionAttemptOutcome.ClientAccepted,
            1_000);
        Equal(EmergencyPurifyBufferPhase.SpentUntilStatusGone, accepted.NextState.Phase, "accepted status spent");
        var repeated = Observe(
            accepted.NextState,
            StatusA,
            freshKey: true,
            heldKeyEligible: true,
            allowHeldKey: true,
            locallyReady: true,
            now: 1_001);
        False(repeated.ShouldDispatch, "same exact CC cannot duplicate acceptance");
    }

    public static void HeldKeyAtStatusEntryIsExplicitAndOneShot()
    {
        var disabled = Observe(
            EmergencyPurifyBufferState.Initial,
            StatusA,
            heldKeyEligible: true,
            allowHeldKey: false,
            locallyReady: true,
            now: 1_000);
        False(disabled.ShouldDispatch, "held level requires opt-in");

        var enabled = Observe(
            EmergencyPurifyBufferState.Initial,
            StatusA,
            heldKeyEligible: true,
            allowHeldKey: true,
            locallyReady: true,
            now: 2_000);
        True(enabled.ShouldDispatch, "held key catches status entry");
        Equal(EmergencyPurifyInputTrigger.HeldKeyAtStatusEntry, enabled.InputTrigger, "held trigger");
        True(enabled.ShouldClaimInputFrame, "held Purify owns only this frame");

        var accepted = Complete(
            enabled.NextState,
            ClientActionAttemptOutcome.ClientAccepted,
            2_000);
        var sameStatus = Observe(
            accepted.NextState,
            StatusA,
            heldKeyEligible: true,
            allowHeldKey: true,
            locallyReady: true,
            now: 2_001);
        False(sameStatus.ShouldDispatch, "same CC stays one-shot");
    }

    public static void HeldKeyOnlyCountsAtStatusEntry()
    {
        var waiting = Observe(
            EmergencyPurifyBufferState.Initial,
            StatusA,
            allowHeldKey: true,
            locallyReady: true,
            now: 1_000);
        var heldLater = Observe(
            waiting.NextState,
            StatusA,
            heldKeyEligible: true,
            allowHeldKey: true,
            locallyReady: true,
            now: 1_001);
        False(heldLater.ShouldDispatch, "late held level is not a synthetic edge");

        var freshLater = Observe(
            heldLater.NextState,
            StatusA,
            freshKey: true,
            heldKeyEligible: true,
            allowHeldKey: true,
            locallyReady: true,
            now: 1_002);
        True(freshLater.ShouldDispatch, "real later down edge works");
        Equal(EmergencyPurifyInputTrigger.FreshKeyPress, freshLater.InputTrigger, "fresh trigger");
    }

    public static void StableHoldWinsWhenFreshAndHeldCoincide()
    {
        var decision = EmergencyPurifyBufferRules.Observe(
            EmergencyPurifyBufferState.Initial,
            ValidObservation(StatusA, now: 1_000) with
            {
                FreshKeyPressed = true,
                HeldKeyEligible = true,
                AllowHeldKeyAtStatusEntry = true,
                PurifyLocallyReady = false,
                FreshKeyCode = 49,
                HeldKeyCode = 87,
            });
        Equal(EmergencyPurifyBufferDecisionKind.Armed, decision.Kind, "coincident input arms once");
        Equal(EmergencyPurifyInputTrigger.HeldKeyAtStatusEntry, decision.InputTrigger, "stable hold wins");
        Equal(87, decision.NextState.FrozenKeyCode, "stable held key is frozen");

        var afterFreshTapReleased = EmergencyPurifyBufferRules.Observe(
            decision.NextState,
            ValidObservation(StatusA, now: 1_001) with
            {
                PurifyLocallyReady = true,
                FrozenKeyStillDown = true,
            });
        True(afterFreshTapReleased.ShouldDispatch, "released fresh tap cannot cancel stable Purify intent");
        Equal(87, afterFreshTapReleased.NextState.FrozenKeyCode, "dispatch retains exact W intent");
    }

    public static void HeldKeyIsConsumedWhenItOnlyArms()
    {
        var armed = Observe(
            EmergencyPurifyBufferState.Initial,
            StatusA,
            heldKeyEligible: true,
            allowHeldKey: true,
            locallyReady: false,
            now: 1_000);
        Equal(EmergencyPurifyBufferDecisionKind.Armed, armed.Kind, "locked Purify arms");
        True(armed.ShouldClaimInputFrame, "armed Purify owns current frame");

        var ready = Observe(
            armed.NextState,
            StatusA,
            locallyReady: true,
            now: 1_001,
            frozenKeyStillDown: true);
        True(ready.ShouldDispatch, "first ready frame dispatches");
        True(ready.ShouldClaimInputFrame, "dispatch frame is owned too");
    }

    public static void DispatchConsumesBeforeAttempt()
    {
        var first = Observe(
            EmergencyPurifyBufferState.Initial,
            StatusA,
            freshKey: true,
            locallyReady: true,
            now: 1_000);
        var rejected = Complete(
            first.NextState,
            ClientActionAttemptOutcome.ClientRejected,
            1_000);
        True(rejected.RetryScheduled, "clean false retains exact intent");
        Equal(1, rejected.NextState.NativeAttemptCount, "first native call counted");

        var throttled = Observe(
            rejected.NextState,
            StatusA,
            locallyReady: true,
            now: 1_049,
            frozenKeyStillDown: true);
        Equal(EmergencyPurifyBufferDecisionKind.Armed, throttled.Kind, "49 ms throttled");
        var retry = Observe(
            throttled.NextState,
            StatusA,
            locallyReady: true,
            now: 1_050,
            frozenKeyStillDown: true);
        True(retry.ShouldDispatch, "50 ms retry boundary");
    }

    public static void ReadyAtArmDispatchesExactlyOnce()
    {
        var decision = Observe(
            EmergencyPurifyBufferState.Initial,
            StatusA,
            freshKey: true,
            locallyReady: true,
            now: 1_000);
        var accepted = Complete(
            decision.NextState,
            ClientActionAttemptOutcome.ClientAccepted,
            1_000);
        True(accepted.ClientAccepted, "accepted exposed");
        var next = Observe(
            accepted.NextState,
            StatusA,
            freshKey: true,
            locallyReady: true,
            now: 1_001);
        False(next.ShouldDispatch, "accepted exact CC is terminal");
    }

    public static void TimeoutWithoutAttemptCanRearm()
    {
        Equal(750L, EmergencyPurifyBufferRules.DefaultBufferMilliseconds, "default buffer");
        Equal(100L, EmergencyPurifyBufferRules.NormalizeBufferMilliseconds(-1), "minimum clamp");
        Equal(1_000L, EmergencyPurifyBufferRules.NormalizeBufferMilliseconds(50_000), "maximum clamp");

        var armed = Observe(
            EmergencyPurifyBufferState.Initial,
            StatusA,
            freshKey: true,
            locallyReady: false,
            now: 1_100,
            bufferMilliseconds: 750);
        Equal(long.MaxValue, armed.NextState.ExpiresAtMilliseconds, "status/key bounded lease");
        var afterThreeSeconds = Observe(
            armed.NextState,
            StatusA,
            locallyReady: false,
            now: 4_500,
            frozenKeyStillDown: true);
        Equal(EmergencyPurifyBufferDecisionKind.Armed, afterThreeSeconds.Kind, ">3s soft wait retained");
        Equal(0, afterThreeSeconds.NextState.NativeAttemptCount, "wait spends no attempt budget");
        var ready = Observe(
            afterThreeSeconds.NextState,
            StatusA,
            locallyReady: true,
            now: 4_501,
            frozenKeyStillDown: true);
        True(ready.ShouldDispatch, "first ready frame after >3s dispatches");

        var released = Observe(
            armed.NextState,
            StatusA,
            locallyReady: true,
            now: 4_502,
            frozenKeyStillDown: false);
        Equal(EmergencyPurifyBufferCancelReason.ExactKeyReleased, released.CancelReason, "key release bounds lease");
    }

    public static void StatusAbsenceIsTheOnlyRearmForSameInstance()
    {
        var first = Observe(
            EmergencyPurifyBufferState.Initial,
            StatusA,
            heldKeyEligible: true,
            allowHeldKey: true,
            locallyReady: true,
            now: 1_000);
        var accepted = Complete(first.NextState, ClientActionAttemptOutcome.ClientAccepted, 1_000);
        False(
            EmergencyPurifyBufferRules.ClaimsSchedulerPriority(
                accepted.NextState,
                StatusA,
                exactStatusCurrentlyObserved: true,
                exactFrozenKeyStillDown: true),
            "accepted-but-lingering exact CC releases lower helpers");
        var stillPresent = Observe(
            accepted.NextState,
            StatusA,
            heldKeyEligible: true,
            allowHeldKey: true,
            locallyReady: true,
            now: 1_100);
        False(stillPresent.ShouldDispatch, "same status stays spent");
        False(
            EmergencyPurifyBufferRules.ClaimsSchedulerPriority(
                stillPresent.NextState,
                StatusA,
                exactStatusCurrentlyObserved: true,
                exactFrozenKeyStillDown: true),
            "terminal duplicate latch does not claim scheduler priority");
        var gone = Observe(stillPresent.NextState, null, now: 1_101);
        Equal(EmergencyPurifyBufferState.Initial, gone.NextState, "absence resets lifecycle");
        False(
            EmergencyPurifyBufferRules.ClaimsSchedulerPriority(
                gone.NextState,
                null,
                exactStatusCurrentlyObserved: false,
                exactFrozenKeyStillDown: true),
            "status disappearance releases lower helpers");
        var newStatus = Observe(
            gone.NextState,
            StatusB,
            heldKeyEligible: true,
            allowHeldKey: true,
            locallyReady: true,
            now: 1_102);
        True(newStatus.ShouldDispatch, "same still-held key can own a distinct CC status");
    }

    public static void ExactStatusReplacementNeedsANewKey()
    {
        var armedA = Observe(
            EmergencyPurifyBufferState.Initial,
            StatusA,
            heldKeyEligible: true,
            allowHeldKey: true,
            locallyReady: false,
            now: 1_000);
        var replacement = Observe(
            armedA.NextState,
            StatusB,
            heldKeyEligible: true,
            allowHeldKey: true,
            locallyReady: true,
            now: 1_001,
            frozenKeyStillDown: true);
        True(replacement.ShouldDispatch, "same exact hold may catch distinct replacement CC");
        Equal(StatusB, replacement.NextState.StatusInstance!.Value, "replacement identity frozen");
    }

    public static void TemporarySafetyGatesDoNotSpendAnAttempt()
    {
        AssertGate(
            observation => observation with { ConfigurationEnabled = false },
            EmergencyPurifyBufferCancelReason.ConfigurationDisabled);
        AssertGate(
            observation => observation with { IsSupportedPvPContext = false },
            EmergencyPurifyBufferCancelReason.OutsideSupportedPvPContext);
        AssertGate(
            observation => observation with { IsAlive = false },
            EmergencyPurifyBufferCancelReason.PlayerDead);
        AssertGate(
            observation => observation with { IsLocalPlayerIdentityValid = false },
            EmergencyPurifyBufferCancelReason.LocalPlayerIdentityInvalid);
        AssertGate(
            observation => observation with { IsResilienceActive = true },
            EmergencyPurifyBufferCancelReason.ResilienceActive);
        AssertGate(
            observation => observation with { IsTextInputActive = true },
            EmergencyPurifyBufferCancelReason.TextInputActive);
    }

    public static void HardResetAndInvalidInputsFailClosed()
    {
        var armed = Observe(
            EmergencyPurifyBufferState.Initial,
            StatusA,
            freshKey: true,
            locallyReady: false,
            now: 1_000);
        var reset = Observe(
            armed.NextState,
            StatusA,
            locallyReady: true,
            now: 1_001,
            hardReset: true,
            frozenKeyStillDown: true);
        Equal(EmergencyPurifyBufferState.Initial, reset.NextState, "hard reset clears");
        False(reset.ShouldDispatch, "reset never dispatches");

        var invalid = Observe(
            EmergencyPurifyBufferState.Initial,
            new PurifyCcStatusInstance(1343, 0),
            freshKey: true,
            locallyReady: true,
            now: 2_000);
        Equal(EmergencyPurifyBufferCancelReason.InvalidStatusInstance, invalid.CancelReason, "invalid status");

        var observed = Observe(EmergencyPurifyBufferState.Initial, StatusA, now: 3_000);
        var backwards = Observe(
            observed.NextState,
            StatusA,
            freshKey: true,
            locallyReady: true,
            now: 2_999);
        Equal(EmergencyPurifyBufferCancelReason.ClockMovedBackwards, backwards.CancelReason, "clock regression");
    }

    public static void NativeOutcomesUseSharedRetryPolicy()
    {
        var first = Observe(
            EmergencyPurifyBufferState.Initial,
            StatusA,
            heldKeyEligible: true,
            allowHeldKey: true,
            locallyReady: true,
            now: 1_000);
        var soft = Complete(
            first.NextState,
            ClientActionAttemptOutcome.SoftUnavailable,
            1_000);
        True(soft.SoftWait, "known unavailable is a soft wait");
        Equal(0, soft.NextState.NativeAttemptCount, "soft wait spends zero calls");

        var state = soft.NextState;
        for (var attempt = 1; attempt <= HeldActionRetryRules.MaximumNativeAttempts; attempt++)
        {
            var now = 1_000L + ((attempt - 1) * HeldActionRetryRules.NativeRetryThrottleMilliseconds);
            var completion = Complete(state, ClientActionAttemptOutcome.ClientRejected, now);
            state = completion.NextState;
            if (attempt < HeldActionRetryRules.MaximumNativeAttempts)
            {
                True(completion.RetryScheduled, $"retry {attempt}");
                state = Observe(
                    state,
                    StatusA,
                    locallyReady: true,
                    now: now + 50,
                    frozenKeyStillDown: true).NextState;
            }
            else
            {
                Equal(EmergencyPurifyBufferCancelReason.NativeRetryLimitReached, completion.CancelReason, "retry cap");
            }
        }

        HeldActionRetryRules.ConfigureLatencyResponsePolicy(true, 1_000);
        try
        {
            var extended = Observe(
                EmergencyPurifyBufferState.Initial,
                StatusA,
                heldKeyEligible: true,
                allowHeldKey: true,
                locallyReady: true,
                now: 3_000).NextState;
            for (var attempt = 1; attempt <= 9; attempt++)
            {
                var now = 3_000L +
                          ((attempt - 1) * HeldActionRetryRules.NativeRetryThrottleMilliseconds);
                var completion = Complete(
                    extended,
                    ClientActionAttemptOutcome.ClientRejected,
                    now);
                True(completion.RetryScheduled, $"extended Purify retry {attempt}");
                Equal(21, completion.NextState.NativeAttemptLimit, "Purify preserves the frozen extended budget");
                extended = Observe(
                    completion.NextState,
                    StatusA,
                    locallyReady: true,
                    now: now + HeldActionRetryRules.NativeRetryThrottleMilliseconds,
                    frozenKeyStillDown: true).NextState;
            }
        }
        finally
        {
            HeldActionRetryRules.ConfigureLatencyResponsePolicy(false, 0);
        }

        first = Observe(
            EmergencyPurifyBufferState.Initial,
            StatusA,
            heldKeyEligible: true,
            allowHeldKey: true,
            locallyReady: true,
            now: 2_000);
        var unknown = Complete(
            first.NextState,
            ClientActionAttemptOutcome.AcceptanceUnknown,
            2_000);
        True(unknown.Terminal, "unknown acceptance terminal");
        Equal(EmergencyPurifyBufferPhase.SpentUntilStatusGone, unknown.NextState.Phase, "unknown spent");
    }

    private static void AssertGate(
        Func<EmergencyPurifyBufferObservation, EmergencyPurifyBufferObservation> mutate,
        EmergencyPurifyBufferCancelReason expected)
    {
        var armed = Observe(
            EmergencyPurifyBufferState.Initial,
            StatusA,
            freshKey: true,
            locallyReady: false,
            now: 1_000);
        var cancelled = EmergencyPurifyBufferRules.Observe(
            armed.NextState,
            mutate(ValidObservation(StatusA, 1_001) with
            {
                FrozenKeyStillDown = true,
            }));
        Equal(expected, cancelled.CancelReason, expected.ToString());
        Equal(0, cancelled.NextState.NativeAttemptCount, "gate spends zero native calls");
    }

    private static EmergencyPurifyNativeAttemptDecision Complete(
        EmergencyPurifyBufferState state,
        ClientActionAttemptOutcome outcome,
        long now) =>
        EmergencyPurifyBufferRules.ApplyNativeAttemptOutcome(state, outcome, now);

    private static EmergencyPurifyBufferDecision Observe(
        EmergencyPurifyBufferState state,
        PurifyCcStatusInstance? status,
        bool freshKey = false,
        bool heldKeyEligible = false,
        bool allowHeldKey = false,
        bool locallyReady = false,
        long now = 0,
        bool hardReset = false,
        long bufferMilliseconds = EmergencyPurifyBufferRules.DefaultBufferMilliseconds,
        bool? frozenKeyStillDown = null,
        bool automaticStatusTriggerEnabled = false) =>
        EmergencyPurifyBufferRules.Observe(
            state,
            ValidObservation(status, now) with
            {
                FreshKeyPressed = freshKey,
                HeldKeyEligible = heldKeyEligible,
                AllowHeldKeyAtStatusEntry = allowHeldKey,
                PurifyLocallyReady = locallyReady,
                HardReset = hardReset,
                BufferMilliseconds = bufferMilliseconds,
                FreshKeyCode = freshKey ? HeldKey : 0,
                HeldKeyCode = heldKeyEligible ? HeldKey : 0,
                FrozenKeyStillDown = frozenKeyStillDown ??
                    state.Phase == EmergencyPurifyBufferPhase.Buffered,
                AutomaticStatusTriggerEnabled = automaticStatusTriggerEnabled,
            });

    private static EmergencyPurifyBufferObservation ValidObservation(
        PurifyCcStatusInstance? status,
        long now) =>
        new(
            ConfigurationEnabled: true,
            IsSupportedPvPContext: true,
            IsAlive: true,
            IsLocalPlayerIdentityValid: true,
            IsResilienceActive: false,
            IsTextInputActive: false,
            StatusInstance: status,
            FreshKeyPressed: false,
            HeldKeyEligible: false,
            AllowHeldKeyAtStatusEntry: false,
            PurifyLocallyReady: false,
            NowMilliseconds: now);

    private static void True(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException($"Expected true: {label}");
    }

    private static void False(bool condition, string label) => True(!condition, label);

    private static void Equal<T>(T expected, T actual, string label)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
    }
}
