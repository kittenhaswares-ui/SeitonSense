using SeitonSense.Core;

internal static class SmartActionSafetyLeaseSelfTests
{
    private static readonly TargetPressureActorIdentity Local = new(0x100, 0x100);

    public static void ExactFallbackRemainsInspectableUntilExpiry()
    {
        var armed = Arm();
        var exact = SmartActionSafetyLeaseRules.Observe(
            armed,
            250,
            Local,
            1,
            29_507,
            29_507,
            1_001);
        True(exact.ShouldInspect, "the exact authored fallback remains under inspection");
        True(exact.NextState.IsArmed, "inspection does not open the quarantine");

        var repeated = SmartActionSafetyLeaseRules.Observe(
            exact.NextState,
            250,
            Local,
            1,
            29_507,
            29_507,
            1_749);
        True(repeated.ShouldInspect, "Turbo duplicates remain inspectable through the deadline");
    }

    public static void UnrelatedActionsDoNotConsumeTheLease()
    {
        var unrelated = SmartActionSafetyLeaseRules.Observe(
            Arm(),
            250,
            Local,
            1,
            29_999,
            29_999,
            1_001);
        Equal(SmartActionSafetyLeaseDecisionKind.Waiting, unrelated.Kind,
            "a different action passes without consuming safety ownership");
        True(unrelated.NextState.IsArmed, "the exact fallback remains protected");

        var rawAliasOfExactAction = SmartActionSafetyLeaseRules.Observe(
            Arm(),
            250,
            Local,
            rawActionType: 2,
            rawActionId: 4_242,
            resolvedActionId: 29_507,
            nowMilliseconds: 1_001);
        True(rawAliasOfExactAction.ShouldInspect,
            "a different raw carrier for the exact resolved action remains inspected");
        True(rawAliasOfExactAction.NextState.IsArmed,
            "raw alias inspection keeps the exact lease closed");

        var rawOnly = SmartActionSafetyLeaseRules.Arm(
            250,
            Local,
            rawActionType: 1,
            rawActionId: 29_507,
            resolvedActionId: 0,
            nowMilliseconds: 1_000,
            expiresAtMilliseconds: 1_750);
        True(rawOnly.IsArmed,
            "unresolved metadata still creates an exact raw fail-closed lease");
        var rawOnlyExact = SmartActionSafetyLeaseRules.Observe(
            rawOnly,
            250,
            Local,
            rawActionType: 1,
            rawActionId: 29_507,
            resolvedActionId: 0,
            nowMilliseconds: 1_001);
        True(rawOnlyExact.ShouldRejectDrift,
            "the exact unresolved raw fallback is blocked instead of inspected");
        var rawOnlyAlias = SmartActionSafetyLeaseRules.Observe(
            rawOnlyExact.NextState,
            250,
            Local,
            rawActionType: 2,
            rawActionId: 4_242,
            resolvedActionId: 0,
            nowMilliseconds: 1_002);
        True(rawOnlyAlias.ShouldRejectDrift,
            "an unresolved Action/PvPAction alias cannot escape the blockade");
        var rawOnlyKnownOther = SmartActionSafetyLeaseRules.Observe(
            rawOnlyAlias.NextState,
            250,
            Local,
            rawActionType: 1,
            rawActionId: 29_999,
            resolvedActionId: 29_999,
            nowMilliseconds: 1_003);
        True(rawOnlyKnownOther.ShouldRejectDrift,
            "an unresolved lease stays closed because semantic aliases are unknowable");

        var adjustedDrift = SmartActionSafetyLeaseRules.Observe(
            Arm(),
            250,
            Local,
            1,
            29_507,
            29_508,
            1_001);
        True(adjustedDrift.ShouldRejectDrift,
            "the same authored action cannot escape through adjusted-action drift");
        True(adjustedDrift.NextState.IsArmed,
            "adjusted-action drift keeps the exact lease closed");

        var unresolvedAlias = SmartActionSafetyLeaseRules.Observe(
            Arm(),
            250,
            Local,
            rawActionType: 2,
            rawActionId: 4_242,
            resolvedActionId: 0,
            nowMilliseconds: 1_001);
        True(unresolvedAlias.ShouldRejectDrift,
            "resolution loss on a different raw alias fails closed");
    }

    public static void DriftAndExpiryClearFailClosedOwnership()
    {
        True(SmartActionSafetyLeaseRules.IsCurrent(Arm(), 250, Local, 1_749),
            "the safety lease remains current immediately before its deadline");
        False(SmartActionSafetyLeaseRules.IsCurrent(Arm(), 250, Local, 1_750),
            "the exact safety lease deadline is exclusive");
        False(SmartActionSafetyLeaseRules.IsCurrent(Arm(), 251, Local, 1_001),
            "territory drift is never current");
        False(SmartActionSafetyLeaseRules.IsCurrent(
                Arm(),
                250,
                new TargetPressureActorIdentity(0x101, 0x101),
                1_001),
            "local-player identity drift is never current");

        var territoryDrift = SmartActionSafetyLeaseRules.Observe(
            Arm(),
            251,
            Local,
            1,
            29_507,
            29_507,
            1_001);
        Equal(SmartActionSafetyLeaseDecisionKind.Cleared, territoryDrift.Kind,
            "territory drift clears stale ownership");

        var actorDrift = SmartActionSafetyLeaseRules.Observe(
            Arm(),
            250,
            new TargetPressureActorIdentity(0x101, 0x101),
            1,
            29_507,
            29_507,
            1_001);
        Equal(SmartActionSafetyLeaseDecisionKind.Cleared, actorDrift.Kind,
            "local identity drift clears stale ownership");

        var expired = SmartActionSafetyLeaseRules.Observe(
            Arm(),
            250,
            Local,
            1,
            29_507,
            29_507,
            1_750);
        Equal(SmartActionSafetyLeaseDecisionKind.Cleared, expired.Kind,
            "the post-claim safety lease deadline is exclusive");
    }

    private static SmartActionSafetyLeaseState Arm() =>
        SmartActionSafetyLeaseRules.Arm(
            250,
            Local,
            rawActionType: 1,
            rawActionId: 29_507,
            resolvedActionId: 29_507,
            nowMilliseconds: 1_000,
            expiresAtMilliseconds: 1_750);

    private static void True(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException($"Expected true: {label}");
    }

    private static void False(bool condition, string label) =>
        True(!condition, label);

    private static void Equal<T>(T expected, T actual, string label)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
    }
}
