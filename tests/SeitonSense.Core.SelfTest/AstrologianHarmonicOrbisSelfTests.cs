using SeitonSense.Core;

internal static class AstrologianHarmonicOrbisSelfTests
{
    internal static void ExactIdsAndNearHelpThresholdArePinned()
    {
        Equal(33u, AstrologianHarmonicOrbisRules.AstrologianJobId, "AST job row");
        Equal(29_243u, AstrologianHarmonicOrbisRules.HarmonicOrbisActionId,
            "Harmonic Orbis PvP action");
        Equal(29_245u, AstrologianHarmonicOrbisRules.DoubleCastCarrierActionId,
            "Double Cast carrier");
        Equal(29_246u,
            AstrologianHarmonicOrbisRules.DoubleCastFallMaleficActionId,
            "Double Cast Fall Malefic adjusted action");
        Equal(29_247u,
            AstrologianHarmonicOrbisRules.DoubleCastHarmonicOrbisActionId,
            "Double Cast Harmonic Orbis adjusted action");
        Equal(29_248u,
            AstrologianHarmonicOrbisRules.DoubleCastGravityIiActionId,
            "Double Cast Gravity II adjusted action");
        Equal(2u, AstrologianHarmonicOrbisRules.MaximumHarmonicOrbisCharges,
            "Harmonic Orbis maximum charges");
        Equal(60, AstrologianHarmonicOrbisRules.MaximumTargetHealthPercent,
            "inclusive selection threshold");

        var atBoundary = Candidate(1, 60, 100, pressure: 1);
        var aboveBoundary = Candidate(2, 60_001, 100_000, pressure: 9);
        True(NearHelpSelectionRules.IsAtOrBelowHealthPercent(
                atBoundary,
                AstrologianHarmonicOrbisRules.MaximumTargetHealthPercent),
            "exactly 60 percent is eligible");
        False(NearHelpSelectionRules.IsAtOrBelowHealthPercent(
                aboveBoundary,
                AstrologianHarmonicOrbisRules.MaximumTargetHealthPercent),
            "60.001 percent is excluded");

        var candidates = new[] { atBoundary, aboveBoundary };
        Equal(1, NearHelpSelectionRules.SelectBestIndex(
                candidates,
                preferIncomingPressure: true,
                hasTrustedPressureView: true),
            "ordinary Near Help may prefer pressured target inside its window");
        Equal(0, AstrologianHarmonicOrbisRules.SelectBestTargetIndex(
                candidates,
                preferIncomingPressure: true,
                hasTrustedPressureView: true),
            "AST filters above-60 target before the exact Near Help ranking");
    }

    internal static void MetadataAndDispatchContractAreExact()
    {
        True(AstrologianHarmonicOrbisRules.HasExpectedPlayerActionFlag(
                AstrologianHarmonicOrbisRules.HarmonicOrbisActionId,
                isPlayerAction: true),
            "base row is a player action");
        True(AstrologianHarmonicOrbisRules.HasExpectedPlayerActionFlag(
                AstrologianHarmonicOrbisRules.DoubleCastCarrierActionId,
                isPlayerAction: true),
            "Double Cast carrier row is a player action");
        True(AstrologianHarmonicOrbisRules.HasExpectedPlayerActionFlag(
                AstrologianHarmonicOrbisRules.DoubleCastHarmonicOrbisActionId,
                isPlayerAction: false),
            "adjusted follow-up row is not a player action");
        False(AstrologianHarmonicOrbisRules.HasExpectedPlayerActionFlag(
                AstrologianHarmonicOrbisRules.DoubleCastHarmonicOrbisActionId,
                isPlayerAction: true),
            "adjusted follow-up cannot masquerade as a player action");

        var baseDispatch = AstrologianHarmonicOrbisRules.BaseDispatchAction;
        Equal(AstrologianHarmonicOrbisRules.HarmonicOrbisActionId,
            baseDispatch.RawActionId, "base raw action");
        Equal(AstrologianHarmonicOrbisRules.HarmonicOrbisActionId,
            baseDispatch.ExpectedAdjustedActionId, "base adjusted action");
        True(baseDispatch.IsValid, "base dispatch pair");

        var followUpDispatch =
            AstrologianHarmonicOrbisRules.DoubleCastDispatchAction;
        Equal(AstrologianHarmonicOrbisRules.DoubleCastCarrierActionId,
            followUpDispatch.RawActionId, "follow-up raw carrier");
        Equal(AstrologianHarmonicOrbisRules.DoubleCastHarmonicOrbisActionId,
            followUpDispatch.ExpectedAdjustedActionId,
            "follow-up adjusted action");
        True(followUpDispatch.IsValid, "follow-up dispatch pair");
        False(new AstrologianHarmonicOrbisDispatchAction(
                AstrologianHarmonicOrbisRules.DoubleCastHarmonicOrbisActionId,
                AstrologianHarmonicOrbisRules.DoubleCastHarmonicOrbisActionId)
            .IsValid, "adjusted row is never a raw dispatch action");

        True(AstrologianHarmonicOrbisRules.IsDoubleCastAvailableBeforeBase(
                AstrologianHarmonicOrbisRules.DoubleCastCarrierActionId,
                carrierOffCooldown: true,
                currentCharges: 1),
            "one exact carrier charge proves pre-base availability");
        True(AstrologianHarmonicOrbisRules.IsDoubleCastAvailableBeforeBase(
                AstrologianHarmonicOrbisRules.DoubleCastCarrierActionId,
                carrierOffCooldown: true,
                currentCharges: 2),
            "two exact carrier charges prove pre-base availability");
        True(AstrologianHarmonicOrbisRules.IsDoubleCastAvailableBeforeBase(
                AstrologianHarmonicOrbisRules.DoubleCastHarmonicOrbisActionId,
                carrierOffCooldown: true,
                currentCharges: 1),
            "a ready carrier may still show its previously stored Orbis form");
        True(AstrologianHarmonicOrbisRules.IsDoubleCastAvailableBeforeBase(
                AstrologianHarmonicOrbisRules.DoubleCastFallMaleficActionId,
                carrierOffCooldown: true,
                currentCharges: 1),
            "a ready carrier may still show its previously stored Malefic form");
        True(AstrologianHarmonicOrbisRules.IsDoubleCastAvailableBeforeBase(
                AstrologianHarmonicOrbisRules.DoubleCastGravityIiActionId,
                carrierOffCooldown: true,
                currentCharges: 1),
            "a ready carrier may still show its previously stored Gravity form");
        False(AstrologianHarmonicOrbisRules.IsDoubleCastAvailableBeforeBase(
                0,
                carrierOffCooldown: true,
                currentCharges: 1),
            "missing adjusted carrier telemetry fails closed");
        False(AstrologianHarmonicOrbisRules.IsDoubleCastAvailableBeforeBase(
                99_999,
                carrierOffCooldown: true,
                currentCharges: 1),
            "an unrelated adjusted action fails closed");
        False(AstrologianHarmonicOrbisRules.IsDoubleCastAvailableBeforeBase(
                AstrologianHarmonicOrbisRules.DoubleCastCarrierActionId,
                carrierOffCooldown: false,
                currentCharges: 1),
            "cooldown must be proven ready");
        False(AstrologianHarmonicOrbisRules.IsDoubleCastAvailableBeforeBase(
                AstrologianHarmonicOrbisRules.DoubleCastCarrierActionId,
                carrierOffCooldown: true,
                currentCharges: 0),
            "zero charges fail closed");
        False(AstrologianHarmonicOrbisRules.IsDoubleCastAvailableBeforeBase(
                AstrologianHarmonicOrbisRules.DoubleCastCarrierActionId,
                carrierOffCooldown: true,
                currentCharges: 3),
            "out-of-contract charge count fails closed");
    }

    internal static void BaseChargeEpochRequiresDistinctObservedCount()
    {
        var first = AstrologianHarmonicOrbisRules.ObserveBaseChargeEpoch(
            AstrologianHarmonicOrbisBaseChargeEpochState.Initial,
            chargeCountKnown: true,
            currentCharges: 2);
        Equal(1UL, first.CurrentEpochToken, "first positive charge epoch");
        True(first.HasAvailableEpoch, "first charge is available once");
        True(AstrologianHarmonicOrbisRules.TrySpendBaseChargeEpoch(
                first,
                first.CurrentEpochToken,
                out var firstSpent),
            "accepted first Orbis spends its exact epoch");
        False(firstSpent.HasAvailableEpoch, "spent first epoch is unavailable");

        var unchanged = AstrologianHarmonicOrbisRules.ObserveBaseChargeEpoch(
            firstSpent,
            chargeCountKnown: true,
            currentCharges: 2);
        Equal(1UL, unchanged.CurrentEpochToken,
            "unchanged ready propagation cannot create another epoch");
        False(unchanged.HasAvailableEpoch,
            "continuous hold cannot reuse one unchanged charge count");

        var unknown = AstrologianHarmonicOrbisRules.ObserveBaseChargeEpoch(
            unchanged,
            chargeCountKnown: false,
            currentCharges: 0);
        False(unknown.HasAvailableEpoch, "unknown charge telemetry fails closed");
        var restoredSame = AstrologianHarmonicOrbisRules.ObserveBaseChargeEpoch(
            unknown,
            chargeCountKnown: true,
            currentCharges: 2);
        Equal(1UL, restoredSame.CurrentEpochToken,
            "same count after telemetry gap is not a new charge");
        False(restoredSame.HasAvailableEpoch,
            "telemetry gap cannot rearm the spent epoch");

        var remainingCharge = AstrologianHarmonicOrbisRules.ObserveBaseChargeEpoch(
            restoredSame,
            chargeCountKnown: true,
            currentCharges: 1);
        Equal(2UL, remainingCharge.CurrentEpochToken,
            "2-to-1 transition proves a distinct remaining charge");
        True(remainingCharge.HasAvailableEpoch,
            "distinct remaining charge is available once");
        True(AstrologianHarmonicOrbisRules.TrySpendBaseChargeEpoch(
                remainingCharge,
                remainingCharge.CurrentEpochToken,
                out var remainingSpent),
            "remaining charge epoch spends exactly once");
        False(AstrologianHarmonicOrbisRules.TrySpendBaseChargeEpoch(
                remainingSpent,
                remainingCharge.CurrentEpochToken,
                out _),
            "same epoch cannot be spent twice");

        var empty = AstrologianHarmonicOrbisRules.ObserveBaseChargeEpoch(
            remainingSpent,
            chargeCountKnown: true,
            currentCharges: 0);
        False(empty.HasAvailableEpoch, "zero charges has no epoch");
        var recovered = AstrologianHarmonicOrbisRules.ObserveBaseChargeEpoch(
            empty,
            chargeCountKnown: true,
            currentCharges: 1);
        Equal(3UL, recovered.CurrentEpochToken,
            "0-to-1 recovery opens the next exact epoch");
        True(recovered.HasAvailableEpoch, "recovered charge is available");
    }

    internal static void FollowUpRequiresAcceptedOrbisAndLaterFrame()
    {
        var selected = Candidate(2, 60, 100);
        True(AstrologianHarmonicOrbisRules.TryCreateIntent(
                selected,
                doubleCastWasReady: true,
                baseChargeEpochToken: 7,
                orbisFrameworkFrame: 100,
                out var intent),
            "inclusive target creates exact sequence intent");
        Equal(7UL, intent.BaseChargeEpochToken, "base epoch is frozen");

        var rejected = Evaluate(
            intent,
            ClientActionAttemptOutcome.ClientRejected,
            frame: 101,
            target: intent.Target,
            resolvedActionId:
                AstrologianHarmonicOrbisRules.DoubleCastHarmonicOrbisActionId);
        Equal(AstrologianHarmonicOrbisFollowUpKind.Cancelled, rejected.Kind,
            "client rejection cannot arm Double Cast");
        Equal(AstrologianHarmonicOrbisFollowUpReason.OrbisNotAccepted,
            rejected.Reason, "accepted-only reason");

        var sameFrame = Evaluate(
            intent,
            ClientActionAttemptOutcome.ClientAccepted,
            frame: 100,
            target: intent.Target,
            resolvedActionId:
                AstrologianHarmonicOrbisRules.DoubleCastHarmonicOrbisActionId);
        Equal(AstrologianHarmonicOrbisFollowUpKind.Waiting, sameFrame.Kind,
            "accepted Orbis cannot dispatch a second action in the same frame");
        Equal(AstrologianHarmonicOrbisFollowUpReason.LaterFrameworkFrameRequired,
            sameFrame.Reason, "later-frame reason");

        var carrierPropagation = Evaluate(
            intent,
            ClientActionAttemptOutcome.ClientAccepted,
            frame: 101,
            target: intent.Target,
            resolvedActionId:
                AstrologianHarmonicOrbisRules.DoubleCastCarrierActionId);
        Equal(AstrologianHarmonicOrbisFollowUpKind.Waiting,
            carrierPropagation.Kind, "unadjusted carrier is a soft wait");

        var stalePreparedSpell = Evaluate(
            intent,
            ClientActionAttemptOutcome.ClientAccepted,
            frame: 101,
            target: intent.Target,
            resolvedActionId:
                AstrologianHarmonicOrbisRules.DoubleCastGravityIiActionId);
        Equal(AstrologianHarmonicOrbisFollowUpKind.Waiting,
            stalePreparedSpell.Kind,
            "a stale prepared spell waits for the exact Orbis transition");

        var accepted = Evaluate(
            intent,
            ClientActionAttemptOutcome.ClientAccepted,
            frame: 101,
            target: intent.Target,
            resolvedActionId:
                AstrologianHarmonicOrbisRules.DoubleCastHarmonicOrbisActionId);
        True(accepted.ShouldDispatch, "adjusted Double Cast dispatches later");
        Equal(intent.Target, accepted.Target, "same frozen target is retained");
        Equal(AstrologianHarmonicOrbisRules.DoubleCastCarrierActionId,
            accepted.Action.RawActionId, "raw Double Cast carrier is retained");
        Equal(AstrologianHarmonicOrbisRules.DoubleCastHarmonicOrbisActionId,
            accepted.Action.ExpectedAdjustedActionId,
            "exact adjusted follow-up is retained");
    }

    internal static void DoubleCastSnapshotAndSelectionThresholdAreOneShot()
    {
        var selected = Candidate(3, 60, 100);
        True(AstrologianHarmonicOrbisRules.TryCreateIntent(
                selected,
                doubleCastWasReady: false,
                baseChargeEpochToken: 11,
                orbisFrameworkFrame: 200,
                out var orbisOnly),
            "Orbis-only intent remains valid");
        var complete = Evaluate(
            orbisOnly,
            ClientActionAttemptOutcome.ClientAccepted,
            frame: 201,
            target: orbisOnly.Target,
            resolvedActionId:
                AstrologianHarmonicOrbisRules.DoubleCastHarmonicOrbisActionId);
        Equal(AstrologianHarmonicOrbisFollowUpKind.Complete, complete.Kind,
            "initially unavailable Double Cast never joins later");
        Equal(AstrologianHarmonicOrbisFollowUpReason.DoubleCastWasUnavailable,
            complete.Reason, "snapshot reason");

        False(AstrologianHarmonicOrbisRules.TryCreateIntent(
                selected with { CurrentHp = 61 },
                doubleCastWasReady: true,
                baseChargeEpochToken: 12,
                orbisFrameworkFrame: 300,
                out _),
            "above-60 target cannot start Orbis");
        False(AstrologianHarmonicOrbisRules.TryCreateIntent(
                selected,
                doubleCastWasReady: true,
                baseChargeEpochToken: 0,
                orbisFrameworkFrame: 300,
                out _),
            "missing base-charge epoch fails closed");

        True(AstrologianHarmonicOrbisRules.TryCreateIntent(
                selected,
                doubleCastWasReady: true,
                baseChargeEpochToken: 13,
                orbisFrameworkFrame: 400,
                out var pair),
            "paired sequence intent");
        var healedAboveThreshold = selected with { CurrentHp = 100 };
        False(NearHelpSelectionRules.IsAtOrBelowHealthPercent(
                healedAboveThreshold,
                AstrologianHarmonicOrbisRules.MaximumTargetHealthPercent),
            "first heal may lift target over initial threshold");
        var followUp = Evaluate(
            pair,
            ClientActionAttemptOutcome.ClientAccepted,
            frame: 401,
            target: new TargetPressureActorIdentity(
                healedAboveThreshold.GameObjectId,
                healedAboveThreshold.EntityId),
            resolvedActionId:
                AstrologianHarmonicOrbisRules.DoubleCastHarmonicOrbisActionId);
        True(followUp.ShouldDispatch,
            "follow-up does not reapply the selection-only HP threshold");

        var changedTarget = Evaluate(
            pair,
            ClientActionAttemptOutcome.ClientAccepted,
            frame: 401,
            target: new TargetPressureActorIdentity(99_999, 9_999),
            resolvedActionId:
                AstrologianHarmonicOrbisRules.DoubleCastHarmonicOrbisActionId);
        Equal(AstrologianHarmonicOrbisFollowUpReason.TargetChanged,
            changedTarget.Reason, "follow-up cannot rerank or substitute");

        var missingAdjusted = Evaluate(
            pair,
            ClientActionAttemptOutcome.ClientAccepted,
            frame: 401,
            target: pair.Target,
            resolvedActionId: 0);
        Equal(AstrologianHarmonicOrbisFollowUpReason.WrongAdjustedAction,
            missingAdjusted.Reason, "missing adjusted carrier fails closed");

        var unrelatedAdjusted = Evaluate(
            pair,
            ClientActionAttemptOutcome.ClientAccepted,
            frame: 401,
            target: pair.Target,
            resolvedActionId: 99_999);
        Equal(AstrologianHarmonicOrbisFollowUpReason.WrongAdjustedAction,
            unrelatedAdjusted.Reason,
            "an unrelated adjusted action fails closed");
    }

    internal static void NativeGuardBoundaryIsExactAndFailClosed()
    {
        var local = new TargetPressureActorIdentity(10_001, 1_001);
        const ulong target = 20_002;
        False(AstrologianHarmonicOrbisRules.ShouldVetoNativeBoundaryForOwnGuard(
                AstrologianHarmonicOrbisRules.HarmonicOrbisActionId,
                AstrologianHarmonicOrbisRules.HarmonicOrbisActionId,
                AstrologianHarmonicOrbisRules.HarmonicOrbisActionId,
                local,
                local,
                target,
                target,
                ownGuardActiveOrPropagating: false),
            "exact base action may cross the clear-Guard native boundary");
        False(AstrologianHarmonicOrbisRules.ShouldVetoNativeBoundaryForOwnGuard(
                AstrologianHarmonicOrbisRules.DoubleCastCarrierActionId,
                AstrologianHarmonicOrbisRules.DoubleCastHarmonicOrbisActionId,
                AstrologianHarmonicOrbisRules.DoubleCastHarmonicOrbisActionId,
                local,
                local,
                target,
                target,
                ownGuardActiveOrPropagating: false),
            "exact follow-up may cross the clear-Guard native boundary");
        True(AstrologianHarmonicOrbisRules.ShouldVetoNativeBoundaryForOwnGuard(
                AstrologianHarmonicOrbisRules.HarmonicOrbisActionId,
                AstrologianHarmonicOrbisRules.HarmonicOrbisActionId,
                AstrologianHarmonicOrbisRules.HarmonicOrbisActionId,
                local,
                local,
                target,
                target,
                ownGuardActiveOrPropagating: true),
            "active or propagating own Guard vetoes the base action");
        True(AstrologianHarmonicOrbisRules.ShouldVetoNativeBoundaryForOwnGuard(
                AstrologianHarmonicOrbisRules.DoubleCastCarrierActionId,
                AstrologianHarmonicOrbisRules.DoubleCastHarmonicOrbisActionId,
                AstrologianHarmonicOrbisRules.DoubleCastHarmonicOrbisActionId,
                local,
                local,
                target,
                target,
                ownGuardActiveOrPropagating: true),
            "active or propagating own Guard vetoes the follow-up");
        True(AstrologianHarmonicOrbisRules.ShouldVetoNativeBoundaryForOwnGuard(
                AstrologianHarmonicOrbisRules.HarmonicOrbisActionId,
                AstrologianHarmonicOrbisRules.HarmonicOrbisActionId,
                AstrologianHarmonicOrbisRules.HarmonicOrbisActionId,
                local,
                new TargetPressureActorIdentity(10_099, 1_099),
                target,
                target,
                ownGuardActiveOrPropagating: false),
            "local actor drift fails closed");
        True(AstrologianHarmonicOrbisRules.ShouldVetoNativeBoundaryForOwnGuard(
                AstrologianHarmonicOrbisRules.HarmonicOrbisActionId,
                AstrologianHarmonicOrbisRules.HarmonicOrbisActionId,
                AstrologianHarmonicOrbisRules.HarmonicOrbisActionId,
                local,
                local,
                target,
                target + 1,
                ownGuardActiveOrPropagating: false),
            "target drift fails closed");
        True(AstrologianHarmonicOrbisRules.ShouldVetoNativeBoundaryForOwnGuard(
                29_248,
                AstrologianHarmonicOrbisRules.DoubleCastHarmonicOrbisActionId,
                AstrologianHarmonicOrbisRules.DoubleCastHarmonicOrbisActionId,
                local,
                local,
                target,
                target,
                ownGuardActiveOrPropagating: false),
            "unrelated adjusted action cannot inherit the AST scope");
        True(AstrologianHarmonicOrbisRules.ShouldVetoNativeBoundaryForOwnGuard(
                AstrologianHarmonicOrbisRules.DoubleCastCarrierActionId,
                AstrologianHarmonicOrbisRules.DoubleCastHarmonicOrbisActionId,
                AstrologianHarmonicOrbisRules.DoubleCastCarrierActionId,
                local,
                local,
                target,
                target,
                ownGuardActiveOrPropagating: false),
            "carrier adjustment drift fails closed at the native boundary");
    }

    private static AstrologianHarmonicOrbisFollowUpDecision Evaluate(
        AstrologianHarmonicOrbisIntent intent,
        ClientActionAttemptOutcome outcome,
        ulong frame,
        TargetPressureActorIdentity target,
        uint resolvedActionId) =>
        AstrologianHarmonicOrbisRules.EvaluateFollowUp(
            intent,
            outcome,
            frame,
            target,
            targetStillEligible: true,
            resolvedActionId);

    private static NearHelpSelectionCandidate Candidate(
        int partySlot,
        uint currentHp,
        uint maximumHp,
        int pressure = 0) => new(
        GameObjectId: (ulong)(10_000 + partySlot),
        EntityId: (uint)(1_000 + partySlot),
        partySlot,
        currentHp,
        maximumHp,
        DistanceSquared: partySlot,
        IsExactFriendly: true,
        IsSelf: false,
        HasValidActionTarget: true,
        HasRangeAndLineOfSight: true,
        UniqueIncomingEnemyPressureCount: pressure);

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
