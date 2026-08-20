using SeitonSense.Core;

internal static class SmartRecuperateSelfTests
{
    private static readonly TargetPressureActorIdentity LocalPlayer =
        new(10_001, 1_001);

    public static void ExactIdsAndInclusiveThresholdsArePinned()
    {
        Equal(29_711u, SmartRecuperateRules.ActionId, "PvP Recuperate action");
        Equal(16_000u, SmartRecuperateRules.MinimumMissingHp, "missing HP threshold");
        Equal(2_000u, SmartRecuperateRules.MpCost, "exact MP cost");

        var belowHealth = SmartRecuperateRules.Observe(
            Observation() with { CurrentHp = 84_001 });
        None(
            belowHealth,
            SmartRecuperateDecisionReason.MissingHealthBelowThreshold,
            "15,999 missing HP");

        var exact = SmartRecuperateRules.Observe(Observation());
        Dispatch(exact, "16,000 missing HP and 2,000 MP are inclusive");
        True(exact.ShouldConsumeInputGeneration, "dispatch owns the held generation");
        var intent = exact.Intent ??
            throw new InvalidOperationException("dispatch did not freeze an intent");
        Equal(SmartRecuperateRules.ActionId, intent.ActionId, "frozen exact action");
        Equal(LocalPlayer, intent.LocalPlayer, "frozen exact self identity");

        var above = SmartRecuperateRules.Observe(
            Observation() with { CurrentHp = 1, CurrentMp = 10_000 });
        Dispatch(above, "health and MP above their thresholds");

        True(
            SmartRecuperateRules.HasMinimumMissingHp(
                uint.MaxValue - SmartRecuperateRules.MinimumMissingHp,
                uint.MaxValue),
            "large HP values remain overflow safe");
    }

    public static void MpTickWaitDoesNotConsumeTheHold()
    {
        var beforeTick = SmartRecuperateRules.Observe(
            Observation() with { CurrentMp = 1_999 });
        None(
            beforeTick,
            SmartRecuperateDecisionReason.InsufficientMp,
            "one MP below the exact action cost");
        False(
            beforeTick.ShouldConsumeInputGeneration,
            "insufficient MP keeps this physical hold eligible");

        // This models the same still-held, still-unconsumed physical generation
        // on the first frame after a real server MP tick.
        var afterTick = SmartRecuperateRules.Observe(
            Observation() with { CurrentMp = 2_000 });
        Dispatch(afterTick, "the real MP tick may release the same hold");
        True(
            afterTick.ShouldConsumeInputGeneration,
            "the generation becomes terminal only when dispatchable");

        var noReserve = SmartRecuperateRules.Observe(
            Observation() with { CurrentMp = 2_000, MaximumMp = 2_000 });
        Dispatch(noReserve, "there is deliberately no extra Purify MP reserve");
    }

    public static void EveryInitialSafetyGateFailsClosed()
    {
        None(
            SmartRecuperateRules.Observe(
                Observation() with { ConfigurationEnabled = false }),
            SmartRecuperateDecisionReason.ConfigurationDisabled,
            "configuration");
        None(
            SmartRecuperateRules.Observe(
                Observation() with { IsCrystallineConflict = false }),
            SmartRecuperateDecisionReason.OutsideCrystallineConflict,
            "exact CC context");
        None(
            SmartRecuperateRules.Observe(
                Observation() with { LocalPlayer = default }),
            SmartRecuperateDecisionReason.LocalPlayerIdentityInvalid,
            "exact local identity");
        None(
            SmartRecuperateRules.Observe(
                Observation() with { IsLocalPlayerAlive = false }),
            SmartRecuperateDecisionReason.LocalPlayerDead,
            "live self");
        None(
            SmartRecuperateRules.Observe(
                Observation() with { IsLocalPlayerTargetable = false }),
            SmartRecuperateDecisionReason.LocalPlayerUntargetable,
            "targetable self");
        None(
            SmartRecuperateRules.Observe(
                Observation() with { MetadataVerified = false }),
            SmartRecuperateDecisionReason.MetadataUnverified,
            "current metadata");
        None(
            SmartRecuperateRules.Observe(
                Observation() with { ActionHelpersSuppressedByGuard = true }),
            SmartRecuperateDecisionReason.GuardSuppressed,
            "active Guard or propagation latch");
        None(
            SmartRecuperateRules.Observe(
                Observation() with { HigherPriorityClaimed = true }),
            SmartRecuperateDecisionReason.HigherPriorityClaimed,
            "higher-priority owner");
        None(
            SmartRecuperateRules.Observe(
                Observation() with { InputProbeSucceeded = false }),
            SmartRecuperateDecisionReason.InputProbeUnavailable,
            "physical input probe");
        None(
            SmartRecuperateRules.Observe(
                Observation() with { IsTextInputActive = true }),
            SmartRecuperateDecisionReason.TextInputActive,
            "text input");
        None(
            SmartRecuperateRules.Observe(
                Observation() with { HeldGameplayKeyEligible = false }),
            SmartRecuperateDecisionReason.NoHeldGameplayKey,
            "held generation");
        None(
            SmartRecuperateRules.Observe(
                Observation() with { ResolvedActionId = 1 }),
            SmartRecuperateDecisionReason.ResolvedActionInvalid,
            "adjusted exact action");
        None(
            SmartRecuperateRules.Observe(
                Observation() with { ActionLocallyReady = false }),
            SmartRecuperateDecisionReason.ActionNotReady,
            "local readiness");
        None(
            SmartRecuperateRules.Observe(
                Observation() with { CurrentHp = 0 }),
            SmartRecuperateDecisionReason.HealthTelemetryInvalid,
            "dead health telemetry");
        None(
            SmartRecuperateRules.Observe(
                Observation() with { MaximumHp = 0 }),
            SmartRecuperateDecisionReason.HealthTelemetryInvalid,
            "zero maximum HP");
        None(
            SmartRecuperateRules.Observe(
                Observation() with { CurrentHp = 100_001 }),
            SmartRecuperateDecisionReason.HealthTelemetryInvalid,
            "HP above maximum");
        None(
            SmartRecuperateRules.Observe(
                Observation() with { MaximumMp = 0 }),
            SmartRecuperateDecisionReason.MpTelemetryInvalid,
            "zero maximum MP");
        None(
            SmartRecuperateRules.Observe(
                Observation() with { CurrentMp = 10_001 }),
            SmartRecuperateDecisionReason.MpTelemetryInvalid,
            "MP above maximum");

        var reset = SmartRecuperateRules.Observe(
            Observation() with { HardReset = true });
        Equal(
            SmartRecuperateDecisionKind.Cancelled,
            reset.Kind,
            "hard reset is explicitly cancelled");
        Equal(
            SmartRecuperateDecisionReason.HardReset,
            reset.Reason,
            "hard reset reason");
        False(reset.ShouldConsumeInputGeneration, "every failed gate preserves input");
    }

    public static void FrozenIntentRequiresEveryTerminalGate()
    {
        var intent = new SmartRecuperateIntent(
            SmartRecuperateRules.ActionId,
            LocalPlayer);
        True(CanUse(intent), "exact frozen self intent");

        False(CanUse(intent, configurationEnabled: false), "configuration drift");
        False(CanUse(intent, isCrystallineConflict: false), "context drift");
        False(
            CanUse(
                intent,
                currentLocalPlayer: new TargetPressureActorIdentity(10_002, 1_002)),
            "local identity drift");
        False(CanUse(intent, isLocalPlayerAlive: false), "death drift");
        False(CanUse(intent, isLocalPlayerTargetable: false), "targetability drift");
        False(CanUse(intent, metadataVerified: false), "metadata drift");
        False(
            CanUse(intent, actionHelpersSuppressedByGuard: true),
            "Guard or propagation latch appears");
        False(CanUse(intent, higherPriorityClaimed: true), "priority ownership drift");
        False(CanUse(intent, resolvedActionId: 1), "adjusted action drift");
        False(CanUse(intent, actionLocallyReady: false), "cooldown drift");
        False(CanUse(intent, currentHp: 84_001), "health rises below threshold");
        False(CanUse(intent, currentMp: 1_999), "MP drops below exact cost");
        False(CanUse(intent with { ActionId = 1 }), "frozen action is not exact");
        False(CanUse(default), "missing frozen intent");
    }

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
        MaximumMp: 10_000);

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
        uint maximumMp = 10_000) =>
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
            maximumMp);

    private static void Dispatch(
        SmartRecuperateDecision decision,
        string label)
    {
        Equal(SmartRecuperateDecisionKind.Dispatch, decision.Kind, label);
        Equal(SmartRecuperateDecisionReason.None, decision.Reason, $"{label} reason");
        True(decision.ShouldDispatch, $"{label} dispatch flag");
    }

    private static void None(
        SmartRecuperateDecision decision,
        SmartRecuperateDecisionReason reason,
        string label)
    {
        Equal(SmartRecuperateDecisionKind.None, decision.Kind, label);
        Equal(reason, decision.Reason, $"{label} reason");
        False(decision.ShouldDispatch, $"{label} dispatch flag");
        False(decision.ShouldConsumeInputGeneration, $"{label} consume flag");
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
            throw new InvalidOperationException(
                $"{label}: expected {expected}, got {actual}");
    }
}
