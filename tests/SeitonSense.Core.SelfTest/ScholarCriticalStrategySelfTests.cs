using SeitonSense.Core;

internal static class ScholarCriticalStrategySelfTests
{
    private static readonly TargetPressureActorIdentity LocalPlayer = new(10_000, 1_000);

    internal static void CandidateEligibilityRequiresLiveGuardAndNativeReachability()
    {
        var valid = Candidate(1, hp: 70, guardActive: true);

        True(ScholarCriticalStrategyRules.IsEligibleCandidate(valid, LocalPlayer), "guarded target");
        False(ScholarCriticalStrategyRules.IsEligibleCandidate(valid with { GuardActive = false }, LocalPlayer), "unguarded target");
        False(ScholarCriticalStrategyRules.IsEligibleCandidate(valid with { Alive = false }, LocalPlayer), "dead actor");
        False(ScholarCriticalStrategyRules.IsEligibleCandidate(valid with { Targetable = false }, LocalPlayer), "untargetable actor");
        False(ScholarCriticalStrategyRules.IsEligibleCandidate(valid with { CurrentHp = 0 }, LocalPlayer), "zero HP");
        False(ScholarCriticalStrategyRules.IsEligibleCandidate(valid with { CurrentHp = 101 }, LocalPlayer), "HP exceeds maximum");
        False(ScholarCriticalStrategyRules.IsEligibleCandidate(valid with { MaximumHp = 0 }, LocalPlayer), "zero maximum HP");
        False(ScholarCriticalStrategyRules.IsEligibleCandidate(valid with { NativeTargetValid = false }, LocalPlayer), "native target invalid");
        False(ScholarCriticalStrategyRules.IsEligibleCandidate(valid with { NativeRangeAndLineOfSight = false }, LocalPlayer), "native range or LoS invalid");
        False(ScholarCriticalStrategyRules.IsEligibleCandidate(valid with { ExactCanonicalIdentity = false }, LocalPlayer), "noncanonical actor");
        False(ScholarCriticalStrategyRules.IsEligibleCandidate(valid with { Actor = LocalPlayer }, LocalPlayer), "self actor");
        False(ScholarCriticalStrategyRules.IsEligibleCandidate(valid with { EnemySlot = 0 }, LocalPlayer), "invalid slot");

        True(ScholarCriticalStrategyRules.IsExactGuardStatus(ScholarCriticalStrategyRules.GuardStatusId), "CC Guard status");
        True(ScholarCriticalStrategyRules.IsExactGuardStatus(ScholarCriticalStrategyRules.GuardStatusLargeScaleId), "large-scale Guard status");
        False(ScholarCriticalStrategyRules.IsExactGuardStatus(0), "unknown status");
    }

    internal static void CompleteCanonicalSetIsExactAndUnique()
    {
        var complete = Candidates();
        True(ScholarCriticalStrategyRules.HasCompleteExactCanonicalSet(complete, LocalPlayer), "complete S1-S5");
        False(ScholarCriticalStrategyRules.HasCompleteExactCanonicalSet(complete[..4], LocalPlayer), "missing S5");
        False(ScholarCriticalStrategyRules.HasCompleteExactCanonicalSet(null, LocalPlayer), "null set");
        False(ScholarCriticalStrategyRules.HasCompleteExactCanonicalSet(complete, default), "invalid local");

        var duplicateSlot = complete.ToArray();
        duplicateSlot[4] = duplicateSlot[4] with { EnemySlot = 1 };
        False(ScholarCriticalStrategyRules.HasCompleteExactCanonicalSet(duplicateSlot, LocalPlayer), "duplicate slot");

        var duplicateActor = complete.ToArray();
        duplicateActor[4] = duplicateActor[4] with { Actor = duplicateActor[0].Actor };
        False(ScholarCriticalStrategyRules.HasCompleteExactCanonicalSet(duplicateActor, LocalPlayer), "duplicate actor");

        var noncanonical = complete.ToArray();
        noncanonical[2] = noncanonical[2] with { ExactCanonicalIdentity = false };
        False(ScholarCriticalStrategyRules.HasCompleteExactCanonicalSet(noncanonical, LocalPlayer), "noncanonical slot");
    }

    internal static void TrustedPositivePressureRanksBeforeExactHp()
    {
        var candidates = Candidates();
        candidates[0] = candidates[0] with
        {
            GuardActive = true,
            CurrentHp = 10,
            PressureKnown = true,
            TeamTargetCount = 1,
        };
        candidates[1] = candidates[1] with
        {
            GuardActive = true,
            CurrentHp = 80,
            PressureKnown = true,
            TeamTargetCount = 3,
        };
        candidates[2] = candidates[2] with
        {
            GuardActive = false,
            PressureKnown = false,
            TeamTargetCount = -1,
        };

        Equal(
            1,
            ScholarCriticalStrategyRules.SelectBestCandidateIndex(candidates, LocalPlayer),
            "highest known positive pressure wins among eligible actors");

        candidates[0] = candidates[0] with { TeamTargetCount = 3, CurrentHp = 20 };
        Equal(
            0,
            ScholarCriticalStrategyRules.SelectBestCandidateIndex(candidates, LocalPlayer),
            "equal pressure uses exact HP ratio");
    }

    internal static void UnknownOrAllZeroPressureFallsBackToHp()
    {
        var unknown = Candidates();
        unknown[0] = unknown[0] with
        {
            GuardActive = true,
            CurrentHp = 20,
            PressureKnown = true,
            TeamTargetCount = 1,
        };
        unknown[1] = unknown[1] with
        {
            GuardActive = true,
            CurrentHp = 80,
            PressureKnown = false,
            TeamTargetCount = -1,
        };
        Equal(
            0,
            ScholarCriticalStrategyRules.SelectBestCandidateIndex(unknown, LocalPlayer),
            "one unknown eligible pressure sample makes HP first");

        var allZero = Candidates();
        allZero[0] = allZero[0] with
        {
            GuardActive = true,
            CurrentHp = 75,
            PressureKnown = true,
            TeamTargetCount = 0,
        };
        allZero[2] = allZero[2] with
        {
            GuardActive = true,
            CurrentHp = 25,
            PressureKnown = true,
            TeamTargetCount = 0,
        };
        Equal(
            2,
            ScholarCriticalStrategyRules.SelectBestCandidateIndex(allZero, LocalPlayer),
            "all-zero pressure uses HP first");

        var exactRatio = Candidates();
        exactRatio[0] = exactRatio[0] with { GuardActive = true, CurrentHp = 1, MaximumHp = 4 };
        exactRatio[1] = exactRatio[1] with { GuardActive = true, CurrentHp = 25, MaximumHp = 100 };
        Equal(
            0,
            ScholarCriticalStrategyRules.SelectBestCandidateIndex(exactRatio, LocalPlayer),
            "equivalent HP ratios use stable S-slot");
    }

    internal static void DispatchRequiresEveryGateAndHeldGeneration()
    {
        var valid = Observation();
        Dispatch(valid, "valid held generation");

        Cancel(valid with { ConfigurationEnabled = false }, ScholarCriticalStrategyDecisionReason.ConfigurationDisabled);
        Cancel(valid with { IsCrystallineConflict = false }, ScholarCriticalStrategyDecisionReason.OutsideCrystallineConflict);
        Cancel(valid with { LocalPlayer = default }, ScholarCriticalStrategyDecisionReason.LocalPlayerIdentityInvalid);
        Cancel(valid with { IsLocalPlayerAlive = false }, ScholarCriticalStrategyDecisionReason.LocalPlayerDead);
        Cancel(valid with { LocalJobId = 27 }, ScholarCriticalStrategyDecisionReason.LocalJobInvalid);
        Cancel(valid with { MetadataVerified = false }, ScholarCriticalStrategyDecisionReason.MetadataUnverified);
        Cancel(valid with { ActionHelpersSuppressedByGuard = true }, ScholarCriticalStrategyDecisionReason.GuardSuppressed);
        Cancel(valid with { HigherPriorityClaimed = true }, ScholarCriticalStrategyDecisionReason.HigherPriorityClaimed);
        Cancel(valid with { InputProbeSucceeded = false }, ScholarCriticalStrategyDecisionReason.InputProbeUnavailable);
        Cancel(valid with { IsTextInputActive = true }, ScholarCriticalStrategyDecisionReason.TextInputActive);
        Cancel(valid with { HeldGameplayKeyEligible = false }, ScholarCriticalStrategyDecisionReason.NoHeldGameplayKey);
        Cancel(valid with { ResolvedActionId = 0 }, ScholarCriticalStrategyDecisionReason.ResolvedActionInvalid);
        Cancel(valid with { ActionLocallyReady = false }, ScholarCriticalStrategyDecisionReason.ActionNotReady);
        Cancel(valid with { CompleteCanonicalEnemySet = false }, ScholarCriticalStrategyDecisionReason.IncompleteCanonicalEnemySet);
        Cancel(
            valid with { Candidates = valid.Candidates!.Take(4).ToArray() },
            ScholarCriticalStrategyDecisionReason.IncompleteCanonicalEnemySet);
        Cancel(valid with { HardReset = true }, ScholarCriticalStrategyDecisionReason.HardReset);

        var noGuard = ScholarCriticalStrategyRules.Observe(valid with { Candidates = Candidates() });
        False(noGuard.ShouldDispatch, "no guarded target");
        False(noGuard.ShouldConsumeInputGeneration, "no target does not consume");
        Equal(ScholarCriticalStrategyDecisionReason.NoExactEligibleTarget, noGuard.Reason, "no target reason");
    }

    internal static void DispatchFreezesOneIntentWithoutPressureRevalidation()
    {
        var candidates = Candidates();
        candidates[0] = candidates[0] with
        {
            GuardActive = true,
            CurrentHp = 10,
            PressureKnown = true,
            TeamTargetCount = 1,
        };
        candidates[2] = candidates[2] with
        {
            GuardActive = true,
            CurrentHp = 80,
            PressureKnown = true,
            TeamTargetCount = 4,
        };
        var decision = ScholarCriticalStrategyRules.Observe(Observation() with { Candidates = candidates });
        True(decision.ShouldDispatch, "dispatch");
        True(decision.ShouldConsumeInputGeneration, "consume before final validation");
        Equal(2, decision.SelectedCandidateIndex, "pressure-selected S3");

        var intent = decision.Intent ?? throw new InvalidOperationException("missing intent");
        Equal(ScholarCriticalStrategyRules.ActionId, intent.ActionId, "frozen action");
        Equal(LocalPlayer, intent.LocalPlayer, "frozen local");
        Equal(candidates[2].Actor, intent.Target, "frozen target");
        True(intent.PressureKnown && intent.TeamTargetCount == 4, "frozen pressure diagnostics");

        var castWaitRequest = new HeldCastCancellationRequest(
            HeldCastCancellationHelperKind.ScholarCriticalStrategy,
            intent.ActionId,
            intent.LocalPlayer,
            intent.Target,
            FrozenKeyCode: 0x57,
            IntentEpochToken: 1);
        True(castWaitRequest.IsValid, "cast wait retains the exact frozen Scholar intent");
        True(
            HeldActionRetryRules.RetainsSchedulerFrame(
                HeldActionRetryState.Initial,
                nowMilliseconds: 0,
                exactIntentValid: true,
                actionSpecificReady: true,
                targetSpecificReady: true),
            "active cast soft-wait keeps initial Scholar priority without an attempt");
        Equal(
            HeldActionRetryState.Initial,
            HeldActionRetryRules.Complete(
                HeldActionRetryState.Initial,
                nowMilliseconds: 0,
                ClientActionAttemptOutcome.SoftUnavailable).NextState,
            "active cast soft-wait spends no Scholar attempt budget");

        var current = candidates[2] with
        {
            CurrentHp = 20,
            PressureKnown = false,
            TeamTargetCount = -1,
        };
        True(
            ScholarCriticalStrategyRules.CanUseExactIntent(
                intent,
                current,
                LocalPlayer,
                ScholarCriticalStrategyRules.ActionId,
                actionLocallyReady: true),
            "HP and pressure may drift without reranking");
        False(
            ScholarCriticalStrategyRules.CanUseExactIntent(
                intent,
                candidates[0],
                LocalPlayer,
                ScholarCriticalStrategyRules.ActionId,
                actionLocallyReady: true),
            "alternate actor cannot replace frozen target");
        False(CanUse(intent, current with { EnemySlot = 4 }), "slot drift");
        False(CanUse(intent, current with { Actor = new TargetPressureActorIdentity(90_000, 9_000) }), "actor drift");
        False(CanUse(intent, current with { GuardActive = false }), "Guard expired");
        False(CanUse(intent, current with { NativeTargetValid = false }), "native target drift");
        False(CanUse(intent, current with { NativeRangeAndLineOfSight = false }), "native range drift");
        False(CanUse(intent, current with { Alive = false }), "target died");
        False(CanUse(intent, current with { Targetable = false }), "target untargetable");
        False(CanUse(intent, current with { CurrentHp = 0 }), "HP invalid");
        False(
            ScholarCriticalStrategyRules.CanUseExactIntent(
                intent,
                current,
                new TargetPressureActorIdentity(10_001, 1_001),
                ScholarCriticalStrategyRules.ActionId,
                actionLocallyReady: true),
            "local identity drift");
        False(
            ScholarCriticalStrategyRules.CanUseExactIntent(
                intent,
                current,
                LocalPlayer,
                0,
                actionLocallyReady: true),
            "action drift");
        False(
            ScholarCriticalStrategyRules.CanUseExactIntent(
                intent,
                current,
                LocalPlayer,
                ScholarCriticalStrategyRules.ActionId,
                actionLocallyReady: false),
            "readiness drift");
    }

    internal static void ConsumedHeldGenerationCannotRetry()
    {
        var keyState = PhysicalGameplayKeyRules.Observe(
            PhysicalGameplayKeyState.Initial,
            new PhysicalGameplayKeyObservation(IsDown: false, IsTextInputActive: false)).NextState;
        var pressed = PhysicalGameplayKeyRules.Observe(
            keyState,
            new PhysicalGameplayKeyObservation(IsDown: true, IsTextInputActive: false));
        True(pressed.IsHeldEligible, "held generation eligible");

        var first = ScholarCriticalStrategyRules.Observe(Observation() with
        {
            HeldGameplayKeyEligible = pressed.IsHeldEligible,
        });
        True(first.ShouldDispatch, "first terminal intent");

        keyState = PhysicalGameplayKeyRules.Consume(pressed.NextState);
        var stillHeld = PhysicalGameplayKeyRules.Observe(
            keyState,
            new PhysicalGameplayKeyObservation(IsDown: true, IsTextInputActive: false));
        False(stillHeld.IsHeldEligible, "consumed held generation remains spent");

        var retry = ScholarCriticalStrategyRules.Observe(Observation() with
        {
            HeldGameplayKeyEligible = stillHeld.IsHeldEligible,
        });
        False(retry.ShouldDispatch, "no retry while held");
        False(retry.ShouldConsumeInputGeneration, "no second consumption");
        Equal(ScholarCriticalStrategyDecisionReason.NoHeldGameplayKey, retry.Reason, "spent reason");
    }

    internal static void AcceptedHoldRepeatsOnlyAfterCooldownEpoch()
    {
        var hold = ScholarCriticalStrategyRules.BeginAcceptedHold(0x57);
        True(hold.OwnsHold, "accepted initial request owns the exact key");
        False(hold.HasAvailableReadyEpoch, "accepted initial epoch starts spent");

        hold = ScholarCriticalStrategyRules.ObserveAcceptedHold(
            hold,
            hardReset: false,
            ownershipContextValid: true,
            exactHeldKeyStillDown: true,
            cooldownStateKnown: true,
            cooldownReady: false);
        False(hold.HasAvailableReadyEpoch, "cooldown-unavailable observation only arms transition");
        hold = ScholarCriticalStrategyRules.ObserveAcceptedHold(
            hold,
            hardReset: false,
            ownershipContextValid: true,
            exactHeldKeyStillDown: true,
            cooldownStateKnown: true,
            cooldownReady: true);
        True(hold.HasAvailableReadyEpoch, "unavailable-to-ready opens one repeat epoch");
        var epoch = hold.CurrentReadyEpochToken;
        True(ScholarCriticalStrategyRules.TrySpendReadyEpoch(hold, epoch, out hold), "accepted repeat spends exact epoch");
        False(hold.HasAvailableReadyEpoch, "same ready level cannot repeat again");

        Equal(
            ScholarCriticalStrategyHoldState.Initial,
            ScholarCriticalStrategyRules.ObserveAcceptedHold(
                hold,
                hardReset: false,
                ownershipContextValid: true,
                exactHeldKeyStillDown: false,
                cooldownStateKnown: true,
                cooldownReady: true),
            "exact key release ends ownership");
    }

    private static ScholarCriticalStrategyObservation Observation()
    {
        var candidates = Candidates();
        candidates[0] = candidates[0] with
        {
            GuardActive = true,
            PressureKnown = true,
            TeamTargetCount = 1,
        };
        return new ScholarCriticalStrategyObservation(
            ConfigurationEnabled: true,
            IsCrystallineConflict: true,
            LocalJobId: ScholarCriticalStrategyRules.ScholarJobId,
            LocalPlayer,
            IsLocalPlayerAlive: true,
            MetadataVerified: true,
            ActionHelpersSuppressedByGuard: false,
            HigherPriorityClaimed: false,
            InputProbeSucceeded: true,
            IsTextInputActive: false,
            HeldGameplayKeyEligible: true,
            ResolvedActionId: ScholarCriticalStrategyRules.ActionId,
            ActionLocallyReady: true,
            CompleteCanonicalEnemySet: true,
            Candidates: candidates);
    }

    private static ScholarCriticalStrategyCandidate[] Candidates() =>
        Enumerable.Range(1, EnemySlotRules.LastSlot)
            .Select(slot => Candidate(slot, hp: (uint)(40 + slot)))
            .ToArray();

    private static ScholarCriticalStrategyCandidate Candidate(
        int slot,
        uint hp,
        uint maximumHp = 100,
        bool guardActive = false) =>
        new(
            slot,
            new TargetPressureActorIdentity((ulong)(20_000 + slot), (uint)(2_000 + slot)),
            ExactCanonicalIdentity: true,
            Alive: true,
            Targetable: true,
            hp,
            maximumHp,
            guardActive,
            NativeTargetValid: true,
            NativeRangeAndLineOfSight: true,
            PressureKnown: true,
            TeamTargetCount: 0);

    private static bool CanUse(
        ScholarCriticalStrategyIntent intent,
        ScholarCriticalStrategyCandidate candidate) =>
        ScholarCriticalStrategyRules.CanUseExactIntent(
            intent,
            candidate,
            LocalPlayer,
            ScholarCriticalStrategyRules.ActionId,
            actionLocallyReady: true);

    private static void Dispatch(
        ScholarCriticalStrategyObservation observation,
        string label)
    {
        var decision = ScholarCriticalStrategyRules.Observe(observation);
        True(decision.ShouldDispatch, label);
        True(decision.ShouldConsumeInputGeneration, $"{label} consumes input");
        Equal(ScholarCriticalStrategyRules.ActionId, decision.Intent!.Value.ActionId, $"{label} action");
    }

    private static void Cancel(
        ScholarCriticalStrategyObservation observation,
        ScholarCriticalStrategyDecisionReason expectedReason)
    {
        var decision = ScholarCriticalStrategyRules.Observe(observation);
        False(decision.ShouldDispatch, expectedReason.ToString());
        False(decision.ShouldConsumeInputGeneration, $"{expectedReason} input");
        Equal(expectedReason, decision.Reason, expectedReason.ToString());
    }

    private static void Equal<T>(T expected, T actual, string label)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
    }

    private static void True(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException($"Expected true: {label}");
    }

    private static void False(bool condition, string label)
    {
        if (condition) throw new InvalidOperationException($"Expected false: {label}");
    }
}
