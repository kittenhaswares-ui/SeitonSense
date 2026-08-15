using SeitonSense.Core;

internal static class DefensiveUtilitySelfTests
{
    public static void ExactThresholdsAreInclusiveAndSafe()
    {
        True(DefensiveUtilityRules.IsHighPressure(true, 3), "three enemies is high pressure");
        False(DefensiveUtilityRules.IsHighPressure(true, 2), "two enemies is not high pressure");
        False(DefensiveUtilityRules.IsHighPressure(false, 99), "unknown pressure fails closed");

        True(DefensiveUtilityRules.IsAtOrBelowHpPercent(5_000, 10_000, 50), "50 percent inclusive");
        False(DefensiveUtilityRules.IsAtOrBelowHpPercent(5_001, 10_000, 50), "above 50 percent");
        True(DefensiveUtilityRules.IsAtOrBelowHpPercent(2_000, 10_000, 20), "20 percent inclusive");
        False(DefensiveUtilityRules.IsAtOrBelowHpPercent(2_001, 10_000, 20), "above 20 percent");
        False(DefensiveUtilityRules.IsAtOrBelowHpPercent(0, 10_000, 50), "dead actor rejected");
        False(DefensiveUtilityRules.IsAtOrBelowHpPercent(1, 0, 50), "zero maximum rejected");
        False(DefensiveUtilityRules.IsAtOrBelowHpPercent(101, 100, 50), "invalid health rejected");

        True(
            DefensiveUtilityRules.IsAtOrBelowHpPercent(uint.MaxValue / 5, uint.MaxValue, 20),
            "large health values remain overflow safe");
    }

    public static void PreGuardRiskRequiresEveryExactGate()
    {
        True(
            DefensiveUtilityRules.IsPreGuardRisk(true, 3, 50, 100, false, false),
            "exact low-health high-pressure risk");
        False(
            DefensiveUtilityRules.IsPreGuardRisk(false, 3, 50, 100, false, false),
            "unknown pressure");
        False(
            DefensiveUtilityRules.IsPreGuardRisk(true, 2, 50, 100, false, false),
            "insufficient pressure");
        False(
            DefensiveUtilityRules.IsPreGuardRisk(true, 3, 51, 100, false, false),
            "health above threshold");
        False(
            DefensiveUtilityRules.IsPreGuardRisk(true, 3, 50, 100, true, false),
            "Purify-removable CC gives Purify priority");
        False(
            DefensiveUtilityRules.IsPreGuardRisk(true, 3, 50, 100, false, true),
            "active Guard cannot be reused");
    }

    public static void PostPurifyGuardRequiresPositiveConfirmation()
    {
        True(
            DefensiveUtilityRules.CanDispatchPostPurifyGuard(
                false,
                true,
                false,
                3_000,
                2_000),
            "positive Resilience plus CC absence inside the window");
        False(
            DefensiveUtilityRules.CanDispatchPostPurifyGuard(
                true,
                false,
                true,
                3_000,
                2_000),
            "an attempted Purify alone never releases Guard");
        False(
            DefensiveUtilityRules.CanDispatchPostPurifyGuard(
                false,
                true,
                true,
                3_000,
                2_000),
            "remaining CC blocks Guard despite Resilience");
        False(
            DefensiveUtilityRules.CanDispatchPostPurifyGuard(
                false,
                true,
                false,
                3_000,
                3_000),
            "expiry boundary fails closed");
    }

    public static void GuardPropagationLatchIsBoundedAndNonRearming()
    {
        var armed = DefensiveUtilityRules.ObserveGuardPropagation(
            GuardPropagationState.Initial,
            exactGuardActive: false,
            observedGuardAttemptAtMilliseconds: 1_000,
            nowMilliseconds: 1_000);
        True(armed.PropagationLatchActive, "a real Guard attempt arms propagation suppression");
        True(armed.SuppressDirectActionHelpers, "the latch blocks direct helpers");
        Equal(1_500L, armed.RemainingMilliseconds, "the latch is bounded from the attempt timestamp");

        var duplicate = DefensiveUtilityRules.ObserveGuardPropagation(
            armed.NextState,
            exactGuardActive: false,
            observedGuardAttemptAtMilliseconds: 1_000,
            nowMilliseconds: 1_400);
        Equal(1_100L, duplicate.RemainingMilliseconds, "re-observing one attempt cannot extend its deadline");

        var exact = DefensiveUtilityRules.ObserveGuardPropagation(
            duplicate.NextState,
            exactGuardActive: true,
            observedGuardAttemptAtMilliseconds: 1_000,
            nowMilliseconds: 1_500);
        True(exact.SuppressDirectActionHelpers, "exact live Guard owns the gate once visible");
        False(exact.PropagationLatchActive, "the propagation latch retires when exact Guard appears");

        var ended = DefensiveUtilityRules.ObserveGuardPropagation(
            exact.NextState,
            exactGuardActive: false,
            observedGuardAttemptAtMilliseconds: 1_000,
            nowMilliseconds: 2_600);
        False(ended.SuppressDirectActionHelpers, "an old observation cannot rearm after Guard ends");

        var timedOut = DefensiveUtilityRules.ObserveGuardPropagation(
            armed.NextState,
            exactGuardActive: false,
            observedGuardAttemptAtMilliseconds: 1_000,
            nowMilliseconds: 2_500);
        False(timedOut.SuppressDirectActionHelpers, "the exact timeout boundary releases suppression");

        var future = DefensiveUtilityRules.ObserveGuardPropagation(
            GuardPropagationState.Initial,
            exactGuardActive: false,
            observedGuardAttemptAtMilliseconds: 1_001,
            nowMilliseconds: 1_000);
        False(future.SuppressDirectActionHelpers, "a future timestamp fails closed without inventing a latch");
    }

    public static void GuardianEligibilityUsesNativeReachability()
    {
        var valid = Candidate(10, hp: 20, maxHp: 100, distance: 15f);
        True(DefensiveUtilityRules.IsGuardianCandidate(valid), "ten-to-twenty yalms accepted when native reachability succeeds");
        True(
            DefensiveUtilityRules.IsGuardianCandidate(valid with { DistanceSquared = 21f * 21f }),
            "native hitbox-aware reachability remains authoritative above a raw center-distance boundary");
        False(
            DefensiveUtilityRules.IsGuardianCandidate(valid with { CurrentHp = 21 }),
            "above twenty percent rejected");
        False(
            DefensiveUtilityRules.IsGuardianCandidate(valid with { IsExactPartyMember = false }),
            "non-party actor rejected");
        False(
            DefensiveUtilityRules.IsGuardianCandidate(valid with { HasNativeRangeAndLineOfSight = false }),
            "native reachability required");
        False(
            DefensiveUtilityRules.IsGuardianCandidate(valid with { DistanceSquared = float.NaN }),
            "non-finite distance rejected");
        False(
            DefensiveUtilityRules.IsGuardianCandidate(valid with { DistanceSquared = -1f }),
            "negative distance rejected");
        False(
            DefensiveUtilityRules.IsGuardianCandidate(valid with { GameObjectId = 0 }),
            "invalid identity rejected");
    }

    public static void GuardianRankingIsDeterministic()
    {
        var candidates = new[]
        {
            Candidate(10, hp: 20, maxHp: 100, pressure: 5, distance: 3f, partySlot: 2),
            Candidate(20, hp: 10, maxHp: 100, pressure: 1, distance: 8f, partySlot: 3),
            Candidate(30, hp: 10, maxHp: 100, pressure: 4, distance: 7f, partySlot: 4),
            Candidate(40, hp: 10, maxHp: 100, pressure: 4, distance: 4f, partySlot: 5),
        };

        Equal(3, DefensiveUtilityRules.SelectGuardianCandidateIndex(candidates),
            "health, pressure, then distance decide");

        var spent = new HashSet<TargetPressureActorIdentity> { candidates[3].Actor };
        Equal(2, DefensiveUtilityRules.SelectGuardianCandidateIndex(candidates, spent),
            "spent exact actor is excluded without changing target identity");
        Equal(-1, DefensiveUtilityRules.SelectGuardianCandidateIndex(null),
            "missing candidates fail closed");
    }

    public static void GuardianTriggerPopupIsAcceptedOnlyAndBounded()
    {
        var rejected = DefensiveUtilityRules.ObserveGuardianTriggerPopup(
            previous: null,
            runtimeEnabled: true,
            DefensiveUtilityActionKind.Guardian,
            DefensiveUtilityTrigger.PaladinGuardianLowAlly,
            useActionAttempted: true,
            useActionAccepted: false,
            selectedPartySlot: 3,
            nowMilliseconds: 1_000);
        True(rejected is null, "a rejected request never creates a popup");

        var wrongAction = DefensiveUtilityRules.ObserveGuardianTriggerPopup(
            previous: null,
            runtimeEnabled: true,
            DefensiveUtilityActionKind.Guard,
            DefensiveUtilityTrigger.PreGuardLowHpPressure,
            useActionAttempted: true,
            useActionAccepted: true,
            selectedPartySlot: 3,
            nowMilliseconds: 1_000);
        True(wrongAction is null, "Guard acceptance cannot masquerade as Guardian");

        var wrongTrigger = DefensiveUtilityRules.ObserveGuardianTriggerPopup(
            previous: null,
            runtimeEnabled: true,
            DefensiveUtilityActionKind.Guardian,
            DefensiveUtilityTrigger.PreGuardLowHpPressure,
            useActionAttempted: true,
            useActionAccepted: true,
            selectedPartySlot: 3,
            nowMilliseconds: 1_000);
        True(wrongTrigger is null, "a non-Guardian trigger cannot create the card");

        var invalidSlot = DefensiveUtilityRules.ObserveGuardianTriggerPopup(
            previous: null,
            runtimeEnabled: true,
            DefensiveUtilityActionKind.Guardian,
            DefensiveUtilityTrigger.PaladinGuardianLowAlly,
            useActionAttempted: true,
            useActionAccepted: true,
            selectedPartySlot: 0,
            nowMilliseconds: 1_000);
        True(invalidSlot is null, "invalid party slot fails closed");

        var invalidTime = DefensiveUtilityRules.ObserveGuardianTriggerPopup(
            previous: null,
            runtimeEnabled: true,
            DefensiveUtilityActionKind.Guardian,
            DefensiveUtilityTrigger.PaladinGuardianLowAlly,
            useActionAttempted: true,
            useActionAccepted: true,
            selectedPartySlot: 3,
            nowMilliseconds: -1);
        True(invalidTime is null, "invalid time fails closed");

        var accepted = DefensiveUtilityRules.ObserveGuardianTriggerPopup(
            previous: null,
            runtimeEnabled: true,
            DefensiveUtilityActionKind.Guardian,
            DefensiveUtilityTrigger.PaladinGuardianLowAlly,
            useActionAttempted: true,
            useActionAccepted: true,
            selectedPartySlot: 3,
            nowMilliseconds: 1_000);
        True(accepted is not null, "an accepted automatic Guardian creates a popup");
        var popup = accepted!.Value;
        Equal(3, popup.PartySlot, "popup retains only the selected party slot");
        Equal(2_500L, popup.EndsAtMilliseconds, "popup lifetime is exactly 1500 ms");

        var retained = DefensiveUtilityRules.ObserveGuardianTriggerPopup(
            accepted,
            runtimeEnabled: true,
            DefensiveUtilityActionKind.None,
            DefensiveUtilityTrigger.None,
            useActionAttempted: false,
            useActionAccepted: false,
            selectedPartySlot: 0,
            nowMilliseconds: 2_499);
        Equal(2_500L, retained!.Value.EndsAtMilliseconds, "later idle frames cannot extend the popup");

        var expired = DefensiveUtilityRules.ObserveGuardianTriggerPopup(
            retained,
            runtimeEnabled: true,
            DefensiveUtilityActionKind.None,
            DefensiveUtilityTrigger.None,
            useActionAttempted: false,
            useActionAccepted: false,
            selectedPartySlot: 0,
            nowMilliseconds: 2_500);
        True(expired is null, "popup expires at the exact duration boundary");

        var disabled = DefensiveUtilityRules.ObserveGuardianTriggerPopup(
            accepted,
            runtimeEnabled: false,
            DefensiveUtilityActionKind.None,
            DefensiveUtilityTrigger.None,
            useActionAttempted: false,
            useActionAccepted: false,
            selectedPartySlot: 0,
            nowMilliseconds: 1_100);
        True(disabled is null, "disabling the runtime clears the popup immediately");

        var reset = DefensiveUtilityRules.ObserveGuardianTriggerPopup(
            accepted,
            runtimeEnabled: true,
            DefensiveUtilityActionKind.None,
            DefensiveUtilityTrigger.None,
            useActionAttempted: false,
            useActionAccepted: false,
            selectedPartySlot: 0,
            nowMilliseconds: 1_100,
            hardReset: true);
        True(reset is null, "hard reset clears the popup immediately");
    }

    private static PaladinGuardianCandidate Candidate(
        uint entityId,
        uint hp,
        uint maxHp,
        int? pressure = 0,
        float distance = 5f,
        int partySlot = 2) =>
        new(
            0x1000UL + entityId,
            entityId,
            partySlot,
            hp,
            maxHp,
            pressure,
            distance * distance,
            IsExactPartyMember: true,
            IsSelf: false,
            IsAlive: true,
            IsTargetable: true,
            HasValidNativeTarget: true,
            HasNativeRangeAndLineOfSight: true);

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
