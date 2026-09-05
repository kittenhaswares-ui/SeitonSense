using SeitonSense.Core;

internal static class SmartRecuperateSelfTests
{
    private const int HeldKey = 65;
    private static readonly TargetPressureActorIdentity LocalPlayer =
        new(10_001, 1_001);

    public static void ExactIdsAndInclusiveThresholdsArePinned()
    {
        Equal(29_711u, SmartRecuperateRules.ActionId, "PvP Recuperate action");
        Equal(16_000u, SmartRecuperateRules.MinimumMissingHp, "missing HP threshold");
        Equal(2_000u, SmartRecuperateRules.MpCost, "exact MP cost");
        Equal((ushort)10, SmartRecuperateRules.RecastHundredMilliseconds, "verified recast row");
        Equal(1_000L, SmartRecuperateRules.RecastMilliseconds, "accepted recast latch");
        False(
            SmartRecuperateRules.ShouldSuppressForOwnGuard(exactGuardActiveOrPropagating: false),
            "a rejected or ambiguous Guard request cannot suppress Recuperate");
        True(
            SmartRecuperateRules.ShouldSuppressForOwnGuard(exactGuardActiveOrPropagating: true),
            "exact live Guard or accepted propagation suppresses Recuperate");

        var below = Observe(Observation() with { CurrentHp = 84_001 });
        None(below, SmartRecuperateDecisionReason.MissingHealthBelowThreshold, "15,999 missing");

        var exact = Observe(Observation());
        Dispatch(exact, "inclusive HP and MP thresholds");
        var intent = exact.Intent ?? throw new InvalidOperationException("missing intent");
        Equal(SmartRecuperateRules.ActionId, intent.ActionId, "frozen action");
        Equal(LocalPlayer, intent.LocalPlayer, "frozen self");
        Equal(SupportedPvPContext.CrystallineConflict, intent.Context, "frozen context");
        Equal(HeldKey, intent.FrozenKeyCode, "frozen exact key");
        Equal(
            SmartRecuperateTriggerKind.HeldGameplayKey,
            intent.TriggerKind,
            "legacy held trigger");
        Equal(84_000u, intent.TriggerCurrentHp, "frozen HP event");
        Equal(100_000u, intent.TriggerMaximumHp, "frozen max HP");
        Equal(1UL, intent.HealthEventToken, "first event token");
    }

    public static void MpTickWaitDoesNotConsumeTheHold()
    {
        var before = Observe(Observation() with { CurrentMp = 1_999 });
        None(before, SmartRecuperateDecisionReason.InsufficientMp, "insufficient MP");
        False(before.InputClaimed, "own resource wait does not starve lower helpers");

        var after = SmartRecuperateRules.Observe(
            before.NextState,
            Observation() with { NowMilliseconds = 1_001 });
        Dispatch(after, "same hold becomes eligible on the real MP tick");

        var cooldown = Observe(Observation() with
        {
            ActionLocallyReady = false,
            ActionCooldownReady = false,
        });
        None(cooldown, SmartRecuperateDecisionReason.ActionNotReady, "own cooldown wait");
        False(cooldown.InputClaimed, "own cooldown does not starve lower helpers");
    }

    public static void EveryInitialSafetyGateFailsClosed()
    {
        Gate(Observation() with { ConfigurationEnabled = false }, SmartRecuperateDecisionReason.ConfigurationDisabled);
        Gate(Observation() with { Context = SupportedPvPContext.None }, SmartRecuperateDecisionReason.OutsideSupportedPvPContext);
        Dispatch(
            Observe(Observation() with { Context = SupportedPvPContext.WolvesDen }),
            "explicit Wolves' Den test context");
        Gate(Observation() with { LocalPlayer = default }, SmartRecuperateDecisionReason.LocalPlayerIdentityInvalid);
        Gate(Observation() with { IsLocalPlayerAlive = false }, SmartRecuperateDecisionReason.LocalPlayerDead);
        Gate(Observation() with { IsLocalPlayerTargetable = false }, SmartRecuperateDecisionReason.LocalPlayerUntargetable);
        Gate(Observation() with { MetadataVerified = false }, SmartRecuperateDecisionReason.MetadataUnverified);
        Gate(Observation() with { ActionHelpersSuppressedByGuard = true }, SmartRecuperateDecisionReason.GuardSuppressed);
        Gate(Observation() with { HigherPriorityClaimed = true }, SmartRecuperateDecisionReason.HigherPriorityClaimed);
        Gate(Observation() with { InputProbeSucceeded = false }, SmartRecuperateDecisionReason.InputProbeUnavailable);
        Gate(Observation() with { IsTextInputActive = true }, SmartRecuperateDecisionReason.TextInputActive);
        Gate(Observation() with { HeldGameplayKeyEligible = false }, SmartRecuperateDecisionReason.NoHeldGameplayKey);
        Gate(Observation() with { HeldGameplayKeyCode = 0 }, SmartRecuperateDecisionReason.NoHeldGameplayKey);
        Gate(Observation() with { ResolvedActionId = 1 }, SmartRecuperateDecisionReason.ResolvedActionInvalid);
        Gate(Observation() with { ActionLocallyReady = false }, SmartRecuperateDecisionReason.ActionNotReady);
        Gate(Observation() with { CurrentHp = 0 }, SmartRecuperateDecisionReason.HealthTelemetryInvalid);
        Gate(Observation() with { MaximumHp = 0 }, SmartRecuperateDecisionReason.HealthTelemetryInvalid);
        Gate(Observation() with { CurrentHp = 100_001 }, SmartRecuperateDecisionReason.HealthTelemetryInvalid);
        Gate(Observation() with { MaximumMp = 0 }, SmartRecuperateDecisionReason.MpTelemetryInvalid);
        Gate(Observation() with { CurrentMp = 10_001 }, SmartRecuperateDecisionReason.MpTelemetryInvalid);

        var reset = Observe(Observation() with { HardReset = true });
        Equal(SmartRecuperateDecisionKind.Cancelled, reset.Kind, "hard reset");
        Equal(SmartRecuperateState.Initial, reset.NextState, "reset state");
    }

    public static void FrozenIntentRequiresEveryTerminalGate()
    {
        var intent = Intent();
        True(CanUse(intent), "exact frozen intent");
        False(CanUse(intent, configurationEnabled: false), "configuration drift");
        False(CanUse(intent, currentContext: SupportedPvPContext.None), "context ended");
        False(CanUse(intent, currentContext: SupportedPvPContext.WolvesDen), "context drift");
        False(CanUse(intent, currentLocalPlayer: new(10_002, 1_002)), "identity drift");
        False(CanUse(intent, isLocalPlayerAlive: false), "death drift");
        False(CanUse(intent, isLocalPlayerTargetable: false), "targetability drift");
        False(CanUse(intent, metadataVerified: false), "metadata drift");
        False(CanUse(intent, actionHelpersSuppressedByGuard: true), "Guard drift");
        False(CanUse(intent, higherPriorityClaimed: true), "Purify priority drift");
        False(CanUse(intent, resolvedActionId: 1), "action drift");
        False(CanUse(intent, actionLocallyReady: false), "readiness drift");
        False(CanUse(intent, currentHp: 84_001), "health event ended");
        False(CanUse(intent, maximumHp: 100_001), "max HP identity drift");
        False(CanUse(intent, currentMp: 1_999), "MP drift");
        False(CanUse(intent, currentHeldKeyCode: 66), "key substitution");
        False(CanUse(intent, frozenKeyStillDown: false), "key released");
        False(CanUse(default), "missing intent");

        var bufferedCc = Observe(Observation() with { NativeBoundaryReady = false });
        var ccToDen = SmartRecuperateRules.Observe(
            bufferedCc.NextState,
            Observation() with
            {
                Context = SupportedPvPContext.WolvesDen,
                NowMilliseconds = 1_001,
            });
        Equal(SmartRecuperateDecisionKind.Cancelled, ccToDen.Kind, "buffered CC-to-Den drift cancels");
        Equal(SmartRecuperateDecisionReason.ContextChanged, ccToDen.Reason, "buffered context reason");
        False(ccToDen.InputClaimed, "context cancellation never claims input");

        var bufferedDen = Observe(Observation() with
        {
            Context = SupportedPvPContext.WolvesDen,
            NativeBoundaryReady = false,
        });
        var denToCc = SmartRecuperateRules.Observe(
            bufferedDen.NextState,
            Observation() with { NowMilliseconds = 1_001 });
        Equal(SmartRecuperateDecisionKind.Cancelled, denToCc.Kind, "buffered Den-to-CC drift cancels");
        Equal(SmartRecuperateDecisionReason.ContextChanged, denToCc.Reason, "reverse context reason");

        var acceptedStart = Observe(Observation());
        var acceptedCooldown = SmartRecuperateRules.ApplyNativeAttemptOutcome(
            acceptedStart.NextState,
            ClientActionAttemptOutcome.ClientAccepted,
            1_000);
        var acceptedDrift = SmartRecuperateRules.Observe(
            acceptedCooldown.NextState,
            Observation() with
            {
                Context = SupportedPvPContext.WolvesDen,
                NowMilliseconds = 1_001,
            });
        Equal(SmartRecuperateDecisionKind.Cancelled, acceptedDrift.Kind, "accepted cooldown drift cancels");
        Equal(SmartRecuperateDecisionReason.ContextChanged, acceptedDrift.Reason, "accepted context reason");
    }

    public static void CleanFalseRetriesAreBounded()
    {
        var decision = Observe(Observation());
        var state = decision.NextState;
        for (var attempt = 1; attempt <= HeldActionRetryRules.MaximumNativeAttempts; attempt++)
        {
            var now = 1_000L + ((attempt - 1) * HeldActionRetryRules.NativeRetryThrottleMilliseconds);
            var completion = SmartRecuperateRules.ApplyNativeAttemptOutcome(
                state,
                ClientActionAttemptOutcome.ClientRejected,
                now);
            state = completion.NextState;
            if (attempt < HeldActionRetryRules.MaximumNativeAttempts)
            {
                True(completion.RetryScheduled, $"retry {attempt} scheduled");
                var early = SmartRecuperateRules.Observe(
                    state,
                    Observation() with { NowMilliseconds = now + 49 });
                Equal(SmartRecuperateDecisionKind.Armed, early.Kind, $"retry {attempt} throttled");
                True(early.InputClaimed, "short retry throttle owns only this frame");
                decision = SmartRecuperateRules.Observe(
                    early.NextState,
                    Observation() with { NowMilliseconds = now + 50 });
                Dispatch(decision, $"retry {attempt} released at 50 ms");
                state = decision.NextState;
            }
            else
            {
                True(completion.Terminal, "eighth clean false is terminal");
                Equal(SmartRecuperatePhase.SpentUntilKeyRelease, state.Phase, "spent phase");
            }
        }

        var sameHold = SmartRecuperateRules.Observe(
            state,
            Observation() with { NowMilliseconds = 1_500, FrozenKeyStillDown = true });
        False(sameHold.ShouldDispatch, "same exact episode cannot start another retry batch");
    }

    public static void SoftUnavailableIsFreeAndAcceptedCooldownDefinesRepeat()
    {
        var first = Observe(Observation());
        var soft = SmartRecuperateRules.ApplyNativeAttemptOutcome(
            first.NextState,
            ClientActionAttemptOutcome.SoftUnavailable,
            1_000);
        True(soft.SoftWait, "known boundary wait exposed");
        Equal(0, soft.NextState.Retry.NativeAttemptCount, "soft wait spends zero calls");

        var ready = SmartRecuperateRules.Observe(
            soft.NextState,
            Observation() with { NowMilliseconds = 1_001 });
        Dispatch(ready, "first ready frame retries immediately");
        var accepted = SmartRecuperateRules.ApplyNativeAttemptOutcome(
            ready.NextState,
            ClientActionAttemptOutcome.ClientAccepted,
            1_001);
        Equal(
            SmartRecuperatePhase.WaitingForAcceptedCooldownUnavailable,
            accepted.NextState.Phase,
            "accepted request awaits negative cooldown evidence");

        var stillReady = SmartRecuperateRules.Observe(
            accepted.NextState,
            Observation() with { NowMilliseconds = 1_002 });
        False(stillReady.ShouldDispatch, "propagation delay cannot duplicate acceptance");
        False(stillReady.InputClaimed, "accepted cooldown tracking does not starve lower helpers");

        var unavailable = SmartRecuperateRules.Observe(
            stillReady.NextState,
            Observation() with
            {
                ActionLocallyReady = false,
                ActionCooldownReady = false,
                NowMilliseconds = 1_500,
            });
        Equal(
            SmartRecuperatePhase.WaitingForAcceptedCooldownReady,
            unavailable.NextState.Phase,
            "accepted cooldown unavailable edge observed");

        var readyBeforeRecast = SmartRecuperateRules.Observe(
            unavailable.NextState,
            Observation() with
            {
                HeldGameplayKeyCode = 66,
                NowMilliseconds = 2_000,
            });
        False(readyBeforeRecast.ShouldDispatch, "observed cooldown cannot bypass exact recast");

        var second = SmartRecuperateRules.Observe(
            readyBeforeRecast.NextState,
            Observation() with
            {
                HeldGameplayKeyCode = 66,
                NowMilliseconds = 2_001,
            });
        Dispatch(second, "same hold may authorize the distinct ready cooldown epoch");
        Equal(2UL, second.Intent!.Value.HealthEventToken, "distinct event token");
        Equal(66, second.Intent.Value.FrozenKeyCode, "new cooldown epoch freezes the current eligible key");
    }

    public static void AcceptedCooldownMissedUnavailableEdgeFallsBackAtVerifiedRecast()
    {
        var first = Observe(AutomaticObservation());
        var accepted = SmartRecuperateRules.ApplyNativeAttemptOutcome(
            first.NextState,
            ClientActionAttemptOutcome.ClientAccepted,
            1_000);
        Equal(1_000L, accepted.NextState.AcceptedAtMilliseconds, "acceptance time frozen separately");

        var propagation = SmartRecuperateRules.Observe(
            accepted.NextState,
            AutomaticObservation() with { NowMilliseconds = 1_001 });
        None(
            propagation,
            SmartRecuperateDecisionReason.WaitingForAcceptedCooldownUnavailable,
            "ready propagation frame cannot duplicate acceptance");
        False(propagation.InputClaimed, "passive recast wait leaves lower helpers free");

        var lastEarlyFrame = SmartRecuperateRules.Observe(
            propagation.NextState,
            AutomaticObservation() with { NowMilliseconds = 1_999 });
        None(
            lastEarlyFrame,
            SmartRecuperateDecisionReason.WaitingForAcceptedCooldownUnavailable,
            "999 ms remains inside verified recast");

        var recovered = SmartRecuperateRules.Observe(
            lastEarlyFrame.NextState,
            AutomaticObservation() with { NowMilliseconds = 2_000 });
        Dispatch(recovered, "current readiness rearms at exact recast without a false edge");
        Equal(2UL, recovered.Intent!.Value.HealthEventToken, "fallback creates one new health event");
        Equal(-1L, recovered.NextState.AcceptedAtMilliseconds, "new intent clears accepted timestamp");

        var acceptedNearClockEnd = SmartRecuperateRules.ApplyNativeAttemptOutcome(
            Observe(AutomaticObservation() with { NowMilliseconds = long.MaxValue }).NextState,
            ClientActionAttemptOutcome.ClientAccepted,
            long.MaxValue);
        var clockEnd = SmartRecuperateRules.Observe(
            acceptedNearClockEnd.NextState,
            AutomaticObservation() with { NowMilliseconds = long.MaxValue });
        False(clockEnd.ShouldDispatch, "accepted recast cannot overflow into immediate eligibility");
    }

    public static void PurifyPriorityNeverGetsStarved()
    {
        var first = Observe(Observation());
        var rejected = SmartRecuperateRules.ApplyNativeAttemptOutcome(
            first.NextState,
            ClientActionAttemptOutcome.ClientRejected,
            1_000);
        var blocked = SmartRecuperateRules.Observe(
            rejected.NextState,
            Observation() with
            {
                HigherPriorityClaimed = true,
                NowMilliseconds = 1_050,
            });
        False(blocked.ShouldDispatch, "active Purify prevents Recup native work");
        False(blocked.InputClaimed, "Recup never steals Purify's frame");
        Equal(rejected.NextState.Intent!.Value, blocked.NextState.Intent!.Value, "exact Recup intent retained");
    }

    public static void AutomaticModeFreezesOneKeylessIntent()
    {
        var automatic = Observe(AutomaticObservation());
        Dispatch(automatic, "automatic keyless opportunity");
        var intent = automatic.Intent ??
                     throw new InvalidOperationException("missing automatic intent");
        Equal(
            SmartRecuperateTriggerKind.Automatic,
            intent.TriggerKind,
            "automatic trigger frozen");
        Equal(0, intent.FrozenKeyCode, "automatic intent freezes no key");
        Equal(LocalPlayer, intent.LocalPlayer, "automatic intent remains self-only");
        Equal(1UL, intent.HealthEventToken, "automatic first event token");
        Equal(
            HeldActionRetryRules.CurrentMaximumNativeAttempts,
            automatic.NextState.Retry.NativeAttemptLimit,
            "automatic retry cap frozen at intent creation");
        True(
            CanUse(
                intent,
                currentHeldKeyCode: 0,
                frozenKeyStillDown: false,
                heldModeEnabled: false,
                automaticModeEnabled: true),
            "automatic final gate requires no physical key");
        False(
            CanUse(
                intent,
                currentHeldKeyCode: 0,
                frozenKeyStillDown: false,
                heldModeEnabled: true,
                automaticModeEnabled: false),
            "automatic intent cannot be substituted by held consent");

        var bothModes = Observe(AutomaticObservation() with
        {
            HeldModeEnabled = true,
            HeldGameplayKeyEligible = true,
            HeldGameplayKeyCode = HeldKey,
        });
        Dispatch(bothModes, "automatic mode deterministically wins");
        Equal(
            SmartRecuperateTriggerKind.Automatic,
            bothModes.Intent!.Value.TriggerKind,
            "one shared state selects automatic consent");
        Equal(0, bothModes.Intent.Value.FrozenKeyCode, "held key is not captured by automatic mode");

        Dispatch(
            Observe(AutomaticObservation() with
            {
                Context = SupportedPvPContext.WolvesDen,
            }),
            "automatic mode accepts the explicit Wolves' Den test context");
        Gate(
            AutomaticObservation() with { Context = SupportedPvPContext.None },
            SmartRecuperateDecisionReason.OutsideSupportedPvPContext);

        var insufficientMp = Observe(AutomaticObservation() with { CurrentMp = 1_999 });
        None(
            insufficientMp,
            SmartRecuperateDecisionReason.InsufficientMp,
            "automatic mode keeps the exact MP gate");
        False(insufficientMp.InputClaimed, "automatic MP wait leaves the frame free");
        var actionUnavailable = Observe(AutomaticObservation() with
        {
            ActionLocallyReady = false,
            ActionCooldownReady = false,
        });
        None(
            actionUnavailable,
            SmartRecuperateDecisionReason.ActionNotReady,
            "automatic mode keeps exact local readiness");
        False(actionUnavailable.InputClaimed, "automatic action wait leaves the frame free");
        var purifyPriority = Observe(AutomaticObservation() with
        {
            HigherPriorityClaimed = true,
        });
        None(
            purifyPriority,
            SmartRecuperateDecisionReason.HigherPriorityClaimed,
            "automatic mode remains below Purify");
        False(purifyPriority.InputClaimed, "automatic mode never steals Purify's frame");
        False(
            CanUse(
                intent,
                actionHelpersSuppressedByGuard: true,
                currentHeldKeyCode: 0,
                frozenKeyStillDown: false,
                heldModeEnabled: false,
                automaticModeEnabled: true),
            "automatic final gate preserves own Guard");
    }

    public static void AutomaticPreNativeSoftWaitRetainsHealthEpisodeAndRetriesNextFrame()
    {
        var dispatched = Observe(AutomaticObservation());
        Dispatch(dispatched, "automatic health opportunity reaches the first attempt boundary");
        var frozenIntent = dispatched.Intent ??
                           throw new InvalidOperationException("missing automatic intent");

        var soft = SmartRecuperateRules.ApplyNativeAttemptOutcome(
            dispatched.NextState,
            ClientActionAttemptOutcome.SoftUnavailable,
            1_000);
        True(soft.SoftWait, "pre-native drift is a soft wait");
        False(soft.Terminal, "pre-native drift is not terminal");
        Equal(SmartRecuperatePhase.Buffered, soft.NextState.Phase, "health episode remains buffered");
        Equal(frozenIntent, soft.NextState.Intent!.Value, "exact health episode is retained");
        Equal(0, soft.NextState.Retry.NativeAttemptCount, "pre-native wait spends no native call");

        var retry = SmartRecuperateRules.Observe(
            soft.NextState,
            AutomaticObservation() with { NowMilliseconds = 1_001 });
        Dispatch(retry, "same automatic health episode retries on the next ready frame");
        Equal(frozenIntent, retry.Intent!.Value, "retry cannot substitute the frozen health episode");
    }

    public static void AutomaticTerminalNeedsANewHpOpportunity()
    {
        var first = Observe(AutomaticObservation());
        var terminal = SmartRecuperateRules.ApplyNativeAttemptOutcome(
            first.NextState,
            ClientActionAttemptOutcome.AcceptanceUnknown,
            1_000);
        Equal(
            SmartRecuperatePhase.SpentUntilKeyRelease,
            terminal.NextState.Phase,
            "automatic ambiguity enters the shared terminal phase");

        var sameOpportunity = SmartRecuperateRules.Observe(
            terminal.NextState,
            AutomaticObservation() with { NowMilliseconds = 1_001 });
        None(
            sameOpportunity,
            SmartRecuperateDecisionReason.NativeAcceptanceUnknown,
            "same low-HP opportunity cannot rearm automatic Recuperate");
        Equal(
            SmartRecuperatePhase.SpentUntilKeyRelease,
            sameOpportunity.NextState.Phase,
            "same automatic opportunity remains terminal");

        var unreadableHealth = SmartRecuperateRules.Observe(
            sameOpportunity.NextState,
            AutomaticObservation() with
            {
                CurrentHp = 0,
                NowMilliseconds = 1_002,
            });
        None(
            unreadableHealth,
            SmartRecuperateDecisionReason.HealthTelemetryInvalid,
            "invalid telemetry cannot manufacture a new HP opportunity");
        Equal(
            SmartRecuperatePhase.SpentUntilKeyRelease,
            unreadableHealth.NextState.Phase,
            "invalid telemetry preserves terminal latch");

        var thresholdCleared = SmartRecuperateRules.Observe(
            unreadableHealth.NextState,
            AutomaticObservation() with
            {
                CurrentHp = 84_001,
                NowMilliseconds = 1_003,
            });
        None(
            thresholdCleared,
            SmartRecuperateDecisionReason.MissingHealthBelowThreshold,
            "HP recovery clears the automatic terminal opportunity");
        Equal(
            SmartRecuperatePhase.Waiting,
            thresholdCleared.NextState.Phase,
            "automatic lane waits for a later HP opportunity");

        var nextOpportunity = SmartRecuperateRules.Observe(
            thresholdCleared.NextState,
            AutomaticObservation() with { NowMilliseconds = 1_004 });
        Dispatch(nextOpportunity, "later low-HP opportunity may rearm");
        Equal(2UL, nextOpportunity.Intent!.Value.HealthEventToken, "new HP opportunity gets a new token");
    }

    public static void AutomaticFalseRetriesUseFrozenLatencyExpansion()
    {
        HeldActionRetryRules.ConfigureLatencyResponsePolicy(true, 1_500);
        try
        {
            var attemptLimit = HeldActionRetryRules.CurrentMaximumNativeAttempts;
            True(
                attemptLimit > HeldActionRetryRules.MaximumNativeAttempts,
                "test precondition: held retry policy is expanded");
            var decision = Observe(AutomaticObservation());
            var state = decision.NextState;
            Equal(
                attemptLimit,
                state.Retry.NativeAttemptLimit,
                "automatic intent freezes the configured retry cap");

            var held = Observe(Observation());
            var heldRejected = SmartRecuperateRules.ApplyNativeAttemptOutcome(
                held.NextState,
                ClientActionAttemptOutcome.ClientRejected,
                1_000);
            Equal(
                attemptLimit,
                heldRejected.NextState.Retry.NativeAttemptLimit,
                "held mode inherits the same configured retry window");

            for (var attempt = 1;
                 attempt <= attemptLimit;
                 attempt++)
            {
                var now = 1_000L +
                          ((attempt - 1) *
                           HeldActionRetryRules.NativeRetryThrottleMilliseconds);
                var completion = SmartRecuperateRules.ApplyNativeAttemptOutcome(
                    state,
                    ClientActionAttemptOutcome.ClientRejected,
                    now);
                state = completion.NextState;
                if (attempt < attemptLimit)
                {
                    True(completion.RetryScheduled, $"automatic retry {attempt} scheduled");
                    decision = SmartRecuperateRules.Observe(
                        state,
                        AutomaticObservation() with
                        {
                            NowMilliseconds = now +
                                HeldActionRetryRules.NativeRetryThrottleMilliseconds,
                        });
                    Dispatch(decision, $"automatic retry {attempt} released");
                    state = decision.NextState;
                }
                else
                {
                    True(completion.Terminal, "configured final automatic clean false is terminal");
                    Equal(
                        SmartRecuperatePhase.SpentUntilKeyRelease,
                        state.Phase,
                        "automatic retry exhaustion latches the HP opportunity");
                }
            }

            var noSecondBatch = SmartRecuperateRules.Observe(
                state,
                AutomaticObservation() with
                {
                    NowMilliseconds = 1_000L +
                                      (attemptLimit *
                                       HeldActionRetryRules.NativeRetryThrottleMilliseconds),
                });
            None(
                noSecondBatch,
                SmartRecuperateDecisionReason.NativeRetryLimitReached,
                "same automatic HP opportunity cannot start another retry batch");
        }
        finally
        {
            HeldActionRetryRules.ConfigureLatencyResponsePolicy(false, 0);
        }
    }

    public static void AcceptedCooldownLatchIsPassiveUntilTheRealEpochEnds()
    {
        var first = Observe(AutomaticObservation());
        var accepted = SmartRecuperateRules.ApplyNativeAttemptOutcome(
            first.NextState,
            ClientActionAttemptOutcome.ClientAccepted,
            1_000);

        var unavailableWhileSuppressed = SmartRecuperateRules.Observe(
            accepted.NextState,
            AutomaticObservation() with
            {
                ConfigurationEnabled = false,
                HeldModeEnabled = false,
                AutomaticModeEnabled = false,
                InputProbeSucceeded = false,
                IsTextInputActive = true,
                FrozenKeyStillDown = false,
                ActionHelpersSuppressedByGuard = true,
                HigherPriorityClaimed = true,
                ActionLocallyReady = false,
                ActionCooldownReady = false,
                NowMilliseconds = 1_001,
            });
        Equal(
            SmartRecuperatePhase.WaitingForAcceptedCooldownReady,
            unavailableWhileSuppressed.NextState.Phase,
            "temporary gates cannot hide the accepted cooldown unavailable edge");
        False(unavailableWhileSuppressed.InputClaimed, "passive cooldown observation claims no frame");

        var readyButGuarded = SmartRecuperateRules.Observe(
            unavailableWhileSuppressed.NextState,
            AutomaticObservation() with
            {
                ActionHelpersSuppressedByGuard = true,
                HigherPriorityClaimed = true,
                NowMilliseconds = 2_000,
            });
        None(
            readyButGuarded,
            SmartRecuperateDecisionReason.HigherPriorityClaimed,
            "ready epoch waits behind Purify before Guard");
        Equal(
            SmartRecuperatePhase.WaitingForAcceptedCooldownReady,
            readyButGuarded.NextState.Phase,
            "priority and Guard cannot discard the completed cooldown latch");

        var second = SmartRecuperateRules.Observe(
            readyButGuarded.NextState,
            AutomaticObservation() with { NowMilliseconds = 2_001 });
        Dispatch(second, "next automatic action starts only after the real cooldown epoch");
        Equal(2UL, second.Intent!.Value.HealthEventToken, "accepted cooldown creates one later event");

        var heldFirst = Observe(Observation());
        var heldAccepted = SmartRecuperateRules.ApplyNativeAttemptOutcome(
            heldFirst.NextState,
            ClientActionAttemptOutcome.ClientAccepted,
            2_000);
        var heldUnavailableAfterRelease = SmartRecuperateRules.Observe(
            heldAccepted.NextState,
            Observation() with
            {
                FrozenKeyStillDown = false,
                ActionLocallyReady = false,
                ActionCooldownReady = false,
                NowMilliseconds = 2_001,
            });
        Equal(
            SmartRecuperatePhase.WaitingForAcceptedCooldownReady,
            heldUnavailableAfterRelease.NextState.Phase,
            "held key release cannot hide the accepted cooldown edge");
        var heldReadyAfterRelease = SmartRecuperateRules.Observe(
            heldUnavailableAfterRelease.NextState,
            Observation() with
            {
                FrozenKeyStillDown = false,
                NowMilliseconds = 3_000,
            });
        None(
            heldReadyAfterRelease,
            SmartRecuperateDecisionReason.ExactKeyReleased,
            "legacy held consent still ends after the cooldown epoch is safe");
        Equal(SmartRecuperatePhase.Waiting, heldReadyAfterRelease.NextState.Phase, "held lane reset");

        var staleFirst = Observe(Observation());
        var staleAccepted = SmartRecuperateRules.ApplyNativeAttemptOutcome(
            staleFirst.NextState,
            ClientActionAttemptOutcome.ClientAccepted,
            3_000);
        var staleUnavailable = SmartRecuperateRules.Observe(
            staleAccepted.NextState,
            Observation() with
            {
                HeldModeEnabled = false,
                HeldGameplayKeyEligible = false,
                HeldGameplayKeyCode = 0,
                FrozenKeyStillDown = true,
                ActionLocallyReady = false,
                ActionCooldownReady = false,
                NowMilliseconds = 3_001,
            });
        Equal(
            SmartRecuperatePhase.WaitingForAcceptedCooldownReady,
            staleUnavailable.NextState.Phase,
            "disabled held lane still observes the real cooldown edge");

        var staleReadyAfterReenable = SmartRecuperateRules.Observe(
            staleUnavailable.NextState,
            Observation() with
            {
                HeldGameplayKeyEligible = false,
                HeldGameplayKeyCode = 0,
                FrozenKeyStillDown = true,
                NowMilliseconds = 4_000,
            });
        None(
            staleReadyAfterReenable,
            SmartRecuperateDecisionReason.NoHeldGameplayKey,
            "re-enabling cannot resurrect the pre-disable key generation");
        Equal(
            SmartRecuperatePhase.WaitingForAcceptedCooldownReady,
            staleReadyAfterReenable.NextState.Phase,
            "ready cooldown remains available for genuine new consent");

        var staleReleased = SmartRecuperateRules.Observe(
            staleReadyAfterReenable.NextState,
            Observation() with
            {
                HeldGameplayKeyEligible = false,
                HeldGameplayKeyCode = 0,
                FrozenKeyStillDown = false,
                NowMilliseconds = 4_001,
            });
        None(
            staleReleased,
            SmartRecuperateDecisionReason.ExactKeyReleased,
            "release retires the stale accepted hold");
        Equal(SmartRecuperatePhase.Waiting, staleReleased.NextState.Phase, "release resets the lane");

        var freshAfterRelease = SmartRecuperateRules.Observe(
            staleReleased.NextState,
            Observation() with
            {
                HeldGameplayKeyCode = 66,
                NowMilliseconds = 4_002,
            });
        Dispatch(freshAfterRelease, "a genuinely eligible new generation may dispatch");
        Equal(66, freshAfterRelease.Intent!.Value.FrozenKeyCode, "new generation identity frozen");
    }

    private static SmartRecuperateIntent Intent() => new(
        SmartRecuperateRules.ActionId,
        LocalPlayer,
        SupportedPvPContext.CrystallineConflict,
        HeldKey,
        84_000,
        100_000,
        1);

    private static SmartRecuperateObservation Observation() => new(
        ConfigurationEnabled: true,
        Context: SupportedPvPContext.CrystallineConflict,
        LocalPlayer,
        IsLocalPlayerAlive: true,
        IsLocalPlayerTargetable: true,
        MetadataVerified: true,
        ActionHelpersSuppressedByGuard: false,
        HigherPriorityClaimed: false,
        InputProbeSucceeded: true,
        IsTextInputActive: false,
        HeldGameplayKeyEligible: true,
        ResolvedActionId: SmartRecuperateRules.ActionId,
        ActionLocallyReady: true,
        CurrentHp: 84_000,
        MaximumHp: 100_000,
        CurrentMp: 2_000,
        MaximumMp: 10_000,
        HeldGameplayKeyCode: HeldKey,
        FrozenKeyStillDown: true,
        NativeBoundaryReady: true,
        ActionCooldownReady: true,
        NowMilliseconds: 1_000);

    private static SmartRecuperateObservation AutomaticObservation() =>
        Observation() with
        {
            InputProbeSucceeded = false,
            HeldGameplayKeyEligible = false,
            HeldGameplayKeyCode = 0,
            FrozenKeyStillDown = false,
            HeldModeEnabled = false,
            AutomaticModeEnabled = true,
        };

    private static SmartRecuperateDecision Observe(SmartRecuperateObservation observation) =>
        SmartRecuperateRules.Observe(SmartRecuperateState.Initial, observation);

    private static bool CanUse(
        SmartRecuperateIntent intent,
        bool configurationEnabled = true,
        SupportedPvPContext currentContext = SupportedPvPContext.CrystallineConflict,
        TargetPressureActorIdentity? currentLocalPlayer = null,
        bool isLocalPlayerAlive = true,
        bool isLocalPlayerTargetable = true,
        bool metadataVerified = true,
        bool actionHelpersSuppressedByGuard = false,
        bool higherPriorityClaimed = false,
        uint resolvedActionId = SmartRecuperateRules.ActionId,
        bool actionLocallyReady = true,
        uint currentHp = 84_000,
        uint maximumHp = 100_000,
        uint currentMp = 2_000,
        uint maximumMp = 10_000,
        int currentHeldKeyCode = HeldKey,
        bool frozenKeyStillDown = true,
        bool heldModeEnabled = true,
        bool automaticModeEnabled = false) =>
        SmartRecuperateRules.CanUseFrozenIntent(
            intent,
            configurationEnabled,
            currentContext,
            currentLocalPlayer ?? LocalPlayer,
            isLocalPlayerAlive,
            isLocalPlayerTargetable,
            metadataVerified,
            actionHelpersSuppressedByGuard,
            higherPriorityClaimed,
            resolvedActionId,
            actionLocallyReady,
            currentHp,
            maximumHp,
            currentMp,
            maximumMp,
            currentHeldKeyCode,
            frozenKeyStillDown,
            heldModeEnabled,
            automaticModeEnabled);

    private static void Gate(
        SmartRecuperateObservation observation,
        SmartRecuperateDecisionReason reason) =>
        None(Observe(observation), reason, reason.ToString());

    private static void Dispatch(SmartRecuperateDecision decision, string label)
    {
        Equal(SmartRecuperateDecisionKind.Dispatch, decision.Kind, label);
        Equal(SmartRecuperateDecisionReason.None, decision.Reason, $"{label} reason");
        True(decision.ShouldDispatch, $"{label} dispatch");
        True(decision.InputClaimed, $"{label} frame claim");
    }

    private static void None(
        SmartRecuperateDecision decision,
        SmartRecuperateDecisionReason reason,
        string label)
    {
        Equal(SmartRecuperateDecisionKind.None, decision.Kind, label);
        Equal(reason, decision.Reason, $"{label} reason");
        False(decision.ShouldDispatch, $"{label} dispatch");
    }

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
