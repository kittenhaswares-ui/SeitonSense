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

        var below = Observe(Observation() with { CurrentHp = 84_001 });
        None(below, SmartRecuperateDecisionReason.MissingHealthBelowThreshold, "15,999 missing");

        var exact = Observe(Observation());
        Dispatch(exact, "inclusive HP and MP thresholds");
        var intent = exact.Intent ?? throw new InvalidOperationException("missing intent");
        Equal(SmartRecuperateRules.ActionId, intent.ActionId, "frozen action");
        Equal(LocalPlayer, intent.LocalPlayer, "frozen self");
        Equal(HeldKey, intent.FrozenKeyCode, "frozen exact key");
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
        Gate(Observation() with { IsCrystallineConflict = false }, SmartRecuperateDecisionReason.OutsideCrystallineConflict);
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
        False(CanUse(intent, isCrystallineConflict: false), "context drift");
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
                NowMilliseconds = 1_003,
            });
        Equal(
            SmartRecuperatePhase.WaitingForAcceptedCooldownReady,
            unavailable.NextState.Phase,
            "accepted cooldown unavailable edge observed");

        var second = SmartRecuperateRules.Observe(
            unavailable.NextState,
            Observation() with
            {
                HeldGameplayKeyCode = 66,
                NowMilliseconds = 1_004,
            });
        Dispatch(second, "same hold may authorize the distinct ready cooldown epoch");
        Equal(2UL, second.Intent!.Value.HealthEventToken, "distinct event token");
        Equal(HeldKey, second.Intent.Value.FrozenKeyCode, "repeat retains original exact hold key");
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

    private static SmartRecuperateIntent Intent() => new(
        SmartRecuperateRules.ActionId,
        LocalPlayer,
        HeldKey,
        84_000,
        100_000,
        1);

    private static SmartRecuperateObservation Observation() => new(
        ConfigurationEnabled: true,
        IsCrystallineConflict: true,
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

    private static SmartRecuperateDecision Observe(SmartRecuperateObservation observation) =>
        SmartRecuperateRules.Observe(SmartRecuperateState.Initial, observation);

    private static bool CanUse(
        SmartRecuperateIntent intent,
        bool configurationEnabled = true,
        bool isCrystallineConflict = true,
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
        bool frozenKeyStillDown = true) =>
        SmartRecuperateRules.CanUseFrozenIntent(
            intent,
            configurationEnabled,
            isCrystallineConflict,
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
            frozenKeyStillDown);

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
