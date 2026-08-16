using SeitonSense.Core;

internal static class NearHelpSelectionSelfTests
{
    public static void LowestExactHealthRatioWinsBeforeDistance()
    {
        var candidates = new[]
        {
            Candidate(10, currentHp: 7_000, maximumHp: 10_000, distance: 1f, partySlot: 1),
            Candidate(20, currentHp: 2_000, maximumHp: 10_000, distance: 20f, partySlot: 2),
            Candidate(30, currentHp: 5_000, maximumHp: 10_000, distance: 5f, partySlot: 3),
        };

        Equal(1, NearHelpSelectionRules.SelectBestIndex(candidates), "lowest HP ratio before proximity");
    }

    public static void HealthRatioComparisonIsExactAndOverflowSafe()
    {
        var candidates = new[]
        {
            Candidate(10, currentHp: 1, maximumHp: 3, distance: 1f, partySlot: 1),
            Candidate(20, currentHp: 33, maximumHp: 100, distance: 20f, partySlot: 2),
            Candidate(30, currentHp: uint.MaxValue - 1, maximumHp: uint.MaxValue, distance: 0f, partySlot: 3),
        };

        Equal(1, NearHelpSelectionRules.SelectBestIndex(candidates), "33/100 is lower than exact 1/3");

        candidates =
        [
            Candidate(10, uint.MaxValue - 1, uint.MaxValue, 5f, 1),
            Candidate(20, uint.MaxValue - 2, uint.MaxValue, 6f, 2),
        ];
        Equal(1, NearHelpSelectionRules.SelectBestIndex(candidates), "wide cross multiplication does not overflow");
    }

    public static void EqualHealthUsesDistanceThenStableIdentity()
    {
        var candidates = new[]
        {
            Candidate(40, 5_000, 10_000, 4f, partySlot: 4, gameObjectId: 400),
            Candidate(30, 1_000, 2_000, 3f, partySlot: 3, gameObjectId: 300),
            Candidate(20, 50, 100, 3f, partySlot: 2, gameObjectId: 200),
            Candidate(10, 1, 2, 3f, partySlot: 2, gameObjectId: 100),
        };

        Equal(3, NearHelpSelectionRules.SelectBestIndex(candidates), "distance, party slot, then entity identity");

        candidates =
        [
            Candidate(10, 1, 2, 3f, partySlot: 0, gameObjectId: 100),
            Candidate(20, 1, 2, 3f, partySlot: 5, gameObjectId: 200),
        ];
        Equal(1, NearHelpSelectionRules.SelectBestIndex(candidates), "known party slot sorts before unknown");
    }

    public static void SelfRequiresActionSpecificTargetability()
    {
        var other = Candidate(10, 50, 100, 5f, partySlot: 1);
        var self = Candidate(
            20,
            10,
            100,
            0f,
            partySlot: 0,
            isSelf: true);

        Equal(
            0,
            NearHelpSelectionRules.SelectBestIndex([other, self]),
            "self is rejected without action-specific proof");
        False(NearHelpSelectionRules.IsEligible(self), "unproven self targetability fails closed");

        self = self with { IsActionSelfTargetable = true };
        Equal(
            1,
            NearHelpSelectionRules.SelectBestIndex([other, self]),
            "proven self-targetable action may select the low-health player");
        True(NearHelpSelectionRules.IsEligible(self), "exact self targetability is accepted");
        False(
            NearHelpSelectionRules.IsEligible(self with { HasValidActionTarget = false }),
            "self still requires an exact valid action target");
        False(
            NearHelpSelectionRules.IsEligible(self with { HasRangeAndLineOfSight = false }),
            "self still requires native reachability");
    }

    public static void CriticalHealthAnchorAlwaysWins()
    {
        NearHelpSelectionCandidate[] candidates =
        [
            Candidate(10, 25, 100, 10f, 1, pressure: 0),
            Candidate(20, 35, 100, 1f, 2, pressure: 999),
        ];

        var decision = NearHelpSelectionRules.SelectBest(
            candidates,
            preferIncomingPressure: true,
            hasTrustedPressureView: true);
        Equal(0, decision.SelectedIndex, "exactly 25 percent remains health-first");
        Equal(0, decision.HealthAnchorIndex, "critical candidate is the exact HP anchor");
        Equal(
            NearHelpSelectionReason.CriticalHealthAnchor,
            decision.Reason,
            "inclusive critical boundary is diagnosed");

        candidates =
        [
            Candidate(10, 25_000_001, 100_000_000, 10f, 1, pressure: 0),
            Candidate(20, 35_000_001, 100_000_000, 1f, 2, pressure: 1),
        ];
        decision = NearHelpSelectionRules.SelectBest(candidates, true, true);
        Equal(1, decision.SelectedIndex, "above 25 percent permits pressure refinement");
        Equal(NearHelpSelectionReason.IncomingPressure, decision.Reason, "pressure path used");
    }

    public static void PressureWindowBoundaryIsExactAndOverflowSafe()
    {
        NearHelpSelectionCandidate[] candidates =
        [
            Candidate(10, 1_200_000_000, 4_000_000_000, 10f, 1, pressure: 0),
            Candidate(20, 1_600_000_000, 4_000_000_000, 20f, 2, pressure: 1),
            Candidate(30, 1_600_000_001, 4_000_000_000, 1f, 3, pressure: 999),
        ];

        var decision = NearHelpSelectionRules.SelectBest(candidates, true, true);
        Equal(1, decision.SelectedIndex, "exact anchor plus ten points is included");
        Equal(
            NearHelpSelectionReason.IncomingPressure,
            decision.Reason,
            "wide exact arithmetic reaches the pressure path");

        var reversed = candidates.Reverse().ToArray();
        decision = NearHelpSelectionRules.SelectBest(reversed, true, true);
        Equal(20u, reversed[decision.SelectedIndex].EntityId, "input order cannot change boundary result");
    }

    public static void PressureUsesCountThenExistingStableOrder()
    {
        NearHelpSelectionCandidate[] candidates =
        [
            Candidate(40, 30, 100, 1f, 4, pressure: 0),
            Candidate(30, 32, 100, 20f, 3, pressure: 2),
            Candidate(20, 39, 100, 10f, 2, pressure: 3),
            Candidate(10, 38, 100, 30f, 1, pressure: 3),
        ];

        var decision = NearHelpSelectionRules.SelectBest(candidates, true, true);
        Equal(3, decision.SelectedIndex, "higher pressure then lower exact health");
        Equal(0, decision.HealthAnchorIndex, "health anchor remains observable");

        candidates =
        [
            Candidate(30, 35, 100, 5f, 3, pressure: 2),
            Candidate(20, 35, 100, 4f, 2, pressure: 2, gameObjectId: 200),
            Candidate(10, 35, 100, 4f, 2, pressure: 2, gameObjectId: 100),
            Candidate(40, 30, 100, 10f, 4, pressure: 0),
        ];
        decision = NearHelpSelectionRules.SelectBest(candidates, true, true);
        Equal(2, decision.SelectedIndex, "distance then stable identity resolves pressure ties");

        var reversed = candidates.Reverse().ToArray();
        decision = NearHelpSelectionRules.SelectBest(reversed, true, true);
        Equal(100UL, reversed[decision.SelectedIndex].GameObjectId, "pressure ordering is permutation invariant");
    }

    public static void UnknownOrUntrustedPressureFallsBackExactly()
    {
        NearHelpSelectionCandidate[] incomplete =
        [
            Candidate(10, 30, 100, 10f, 1, pressure: 0),
            Candidate(20, 35, 100, 1f, 2, pressure: null),
            Candidate(30, 40, 100, 2f, 3, pressure: 5),
        ];

        var decision = NearHelpSelectionRules.SelectBest(incomplete, true, true);
        Equal(0, decision.SelectedIndex, "unknown inside the window is never synthetic zero");
        Equal(NearHelpSelectionReason.PressureDataIncomplete, decision.Reason, "incomplete view reason");

        var invalid = incomplete.ToArray();
        invalid[1] = invalid[1] with { UniqueIncomingEnemyPressureCount = -1 };
        decision = NearHelpSelectionRules.SelectBest(invalid, true, true);
        Equal(0, decision.SelectedIndex, "negative pressure is unknown and preserves health-first");
        Equal(NearHelpSelectionReason.PressureDataIncomplete, decision.Reason, "invalid count reason");

        var complete = incomplete.ToArray();
        complete[1] = complete[1] with { UniqueIncomingEnemyPressureCount = 0 };
        decision = NearHelpSelectionRules.SelectBest(complete, true, false);
        Equal(0, decision.SelectedIndex, "untrusted snapshot preserves exact old selection");
        Equal(NearHelpSelectionReason.PressureViewUntrusted, decision.Reason, "untrusted view reason");

        complete[2] = complete[2] with { UniqueIncomingEnemyPressureCount = 0 };
        decision = NearHelpSelectionRules.SelectBest(complete, true, true);
        Equal(0, decision.SelectedIndex, "all-zero pressure preserves exact old selection");
        Equal(NearHelpSelectionReason.NoPositivePressure, decision.Reason, "no pressure reason");

        complete =
        [
            Candidate(10, 30, 100, 10f, 1, pressure: 0),
            Candidate(20, 40, 100, 1f, 2, pressure: 2),
            Candidate(30, 41, 100, 0f, 3, pressure: null),
        ];
        decision = NearHelpSelectionRules.SelectBest(complete, true, true);
        Equal(1, decision.SelectedIndex, "unknown outside the exact window is irrelevant");
        Equal(NearHelpSelectionReason.IncomingPressure, decision.Reason, "complete relevant window can refine");

        decision = NearHelpSelectionRules.SelectBest(complete, false, true);
        Equal(0, decision.SelectedIndex, "disabled preference delegates to exact health-first");
        Equal(NearHelpSelectionReason.PressurePreferenceDisabled, decision.Reason, "disabled reason");
    }

    public static void ReachabilityAndFriendlyIdentityFailClosed()
    {
        var valid = Candidate(90, 50, 100, 8f, partySlot: 1);
        var candidates = new[]
        {
            valid with { GameObjectId = 0 },
            valid with { EntityId = 0xE0000000 },
            valid with { PartySlot = 9 },
            valid with { CurrentHp = 0 },
            valid with { CurrentHp = 101 },
            valid with { DistanceSquared = float.NaN },
            valid with { IsExactFriendly = false },
            valid with { IsSelf = true },
            valid with { HasValidActionTarget = false },
            valid with { HasRangeAndLineOfSight = false },
        };

        Equal(-1, NearHelpSelectionRules.SelectBestIndex(candidates), "every unsafe candidate is rejected");
        Equal(-1, NearHelpSelectionRules.SelectBestIndex(null), "missing candidate snapshot fails closed");
        Equal(-1, NearHelpSelectionRules.SelectBestIndex([]), "empty candidate snapshot fails closed");
    }

    private static NearHelpSelectionCandidate Candidate(
        uint entityId,
        uint currentHp,
        uint maximumHp,
        float distance,
        int partySlot,
        ulong? gameObjectId = null,
        int? pressure = null,
        bool isSelf = false,
        bool isActionSelfTargetable = false) =>
        new(
            gameObjectId ?? entityId,
            entityId,
            partySlot,
            currentHp,
            maximumHp,
            distance * distance,
            IsExactFriendly: true,
            IsSelf: isSelf,
            HasValidActionTarget: true,
            HasRangeAndLineOfSight: true,
            UniqueIncomingEnemyPressureCount: pressure,
            IsActionSelfTargetable: isActionSelfTargetable);

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
