using SeitonSense.Core;

internal static class DarkKnightPlungeSelfTests
{
    private static readonly TargetPressureActorIdentity LocalPlayer =
        new(10_000, 1_000);

    public static void ExactIdentityThresholdAndRangeArePinned()
    {
        Equal(32u, DarkKnightPlungeRules.DarkKnightJobId, "DRK job row");
        Equal(29_092u, DarkKnightPlungeRules.ActionId, "PvP Plunge action");
        Equal(9_150u, DarkKnightPlungeRules.IconId, "current icon");
        Equal(30u, DarkKnightPlungeRules.MaximumHpPercent, "inclusive HP threshold");
        Equal(10f, DarkKnightPlungeRules.MaximumCenterDistanceYalms, "strict center range");

        var valid = Candidate(1, 20_001, 2_001, 30, 100, 100f);
        True(
            DarkKnightPlungeRules.IsEligibleCandidate(valid, LocalPlayer),
            "exactly 30 percent at exactly ten yalms");
        False(
            DarkKnightPlungeRules.IsEligibleCandidate(
                valid with { CurrentHp = 30_001, MaximumHp = 100_000 },
                LocalPlayer),
            "above 30 percent");
        False(
            DarkKnightPlungeRules.IsEligibleCandidate(valid with { CurrentHp = 0 }, LocalPlayer),
            "dead HP sample");
        False(
            DarkKnightPlungeRules.IsEligibleCandidate(valid with { MaximumHp = 0 }, LocalPlayer),
            "zero maximum HP");
        False(
            DarkKnightPlungeRules.IsEligibleCandidate(
                valid with { CenterDistanceSquared = 100.001f },
                LocalPlayer),
            "over ten center yalms");
        False(
            DarkKnightPlungeRules.IsEligibleCandidate(
                valid with { CenterDistanceSquared = float.NaN },
                LocalPlayer),
            "unknown center distance");
        False(
            DarkKnightPlungeRules.IsEligibleCandidate(
                valid with { HasNativeRangeAndLineOfSight = false },
                LocalPlayer),
            "native range and LoS remains mandatory");
        False(
            DarkKnightPlungeRules.IsEligibleCandidate(
                valid with { TargetGuardActive = true },
                LocalPlayer),
            "a guarded target is not an execute opportunity");
        True(
            DarkKnightPlungeRules.IsAtOrBelowExecuteThreshold(
                uint.MaxValue / 4,
                uint.MaxValue),
            "threshold arithmetic is overflow safe");
    }

    public static void CandidateRankingAndAmbiguityAreDeterministic()
    {
        var candidates = new[]
        {
            Candidate(4, 20_004, 2_004, 30, 100, 1f),
            Candidate(3, 20_003, 2_003, 20, 100, 90f),
            Candidate(2, 20_002, 2_002, 20, 100, 4f),
        };
        Equal(
            2,
            DarkKnightPlungeRules.SelectBestCandidateIndex(candidates, LocalPlayer),
            "lowest exact HP ratio then stable slot; distance never reranks");

        var duplicateSlot = new[]
        {
            Candidate(1, 20_001, 2_001, 20, 100, 10f),
            Candidate(1, 20_002, 2_002, 10, 100, 10f),
        };
        Equal(
            -1,
            DarkKnightPlungeRules.SelectBestCandidateIndex(duplicateSlot, LocalPlayer),
            "duplicate canonical S-slot fails closed");

        var duplicateActor = new[]
        {
            Candidate(1, 20_001, 2_001, 20, 100, 10f),
            Candidate(2, 20_001, 2_001, 10, 100, 10f),
        };
        Equal(
            -1,
            DarkKnightPlungeRules.SelectBestCandidateIndex(duplicateActor, LocalPlayer),
            "duplicate canonical actor fails closed");
    }

    public static void ContinuousHoldRequiresAProvenCooldownEpoch()
    {
        var owned = DarkKnightPlungeRules.BeginOwnedHold(65);
        True(owned.OwnsHold, "client-accepted first request owns exact key");
        False(owned.HasAvailableReadyEpoch, "first accepted request spends initial epoch");

        var stillReady = ObserveHold(owned, cooldownKnown: true, cooldownReady: true);
        Equal(
            DarkKnightPlungeHoldOutcome.ReadyEpochUnchanged,
            stillReady.Outcome,
            "ready propagation cannot duplicate the request");
        False(stillReady.State.HasAvailableReadyEpoch, "same ready level stays spent");

        var unknown = ObserveHold(stillReady.State, cooldownKnown: false, cooldownReady: false);
        Equal(
            DarkKnightPlungeHoldOutcome.PreservedUnknown,
            unknown.Outcome,
            "unknown cooldown cannot prove a reset");
        False(unknown.State.HasAvailableReadyEpoch, "unknown state cannot rearm");

        var unavailable = ObserveHold(unknown.State, cooldownKnown: true, cooldownReady: false);
        Equal(
            DarkKnightPlungeHoldOutcome.WaitingForReady,
            unavailable.Outcome,
            "real active cooldown is observed");
        var reset = ObserveHold(unavailable.State, cooldownKnown: true, cooldownReady: true);
        Equal(
            DarkKnightPlungeHoldOutcome.OpenedReadyEpoch,
            reset.Outcome,
            "kill reset or natural recast opens one proven epoch");
        Equal(2UL, reset.State.CurrentReadyEpochToken, "second epoch token");
        True(reset.State.HasAvailableReadyEpoch, "new epoch is available once");

        True(
            DarkKnightPlungeRules.TrySpendReadyEpoch(
                reset.State,
                reset.State.CurrentReadyEpochToken,
                out var spent),
            "epoch is spent before terminal validation");
        False(spent.HasAvailableReadyEpoch, "spent epoch cannot retry");
        False(
            DarkKnightPlungeRules.TrySpendReadyEpoch(
                spent,
                reset.State.CurrentReadyEpochToken,
                out _),
            "same epoch cannot be spent twice");

        var released = ObserveHold(spent, exactHeldKeyStillDown: false);
        Equal(DarkKnightPlungeHoldOutcome.Reset, released.Outcome, "key release ends ownership");
        False(released.State.OwnsHold, "released hold is empty");
    }

    public static void InitialAndRepeatDispatchUseDistinctOwnership()
    {
        var initial = DarkKnightPlungeRules.Evaluate(Observation());
        Dispatch(initial, "initial held generation");
        True(initial.ShouldConsumeSharedInputGeneration, "first request consumes shared input");
        False(initial.ShouldSpendReadyEpoch, "first request has no repeat epoch");
        Equal(0UL, initial.Intent!.Value.ReadyEpochToken, "first intent token");

        var initialIntent = initial.Intent.Value;
        var castWaitRequest = new HeldCastCancellationRequest(
            HeldCastCancellationHelperKind.DarkKnightPlunge,
            initialIntent.ActionId,
            LocalPlayer,
            initialIntent.Target,
            initialIntent.HeldKeyCode,
            IntentEpochToken: 1);
        True(castWaitRequest.IsValid, "cast wait retains the exact frozen Plunge intent");
        True(
            HeldActionRetryRules.RetainsSchedulerFrame(
                HeldActionRetryState.Initial,
                nowMilliseconds: 0,
                exactIntentValid: true,
                actionSpecificReady: true,
                targetSpecificReady: true),
            "active cast soft-wait keeps initial Plunge priority without an attempt");
        Equal(
            HeldActionRetryState.Initial,
            HeldActionRetryRules.Complete(
                HeldActionRetryState.Initial,
                nowMilliseconds: 0,
                ClientActionAttemptOutcome.SoftUnavailable).NextState,
            "active cast soft-wait spends no Plunge attempt budget");

        var owned = DarkKnightPlungeRules.BeginOwnedHold(65);
        owned = ObserveHold(owned, cooldownKnown: true, cooldownReady: false).State;
        owned = ObserveHold(owned, cooldownKnown: true, cooldownReady: true).State;
        var repeated = DarkKnightPlungeRules.Evaluate(Observation() with
        {
            HeldGameplayKeyEligible = false,
            HeldGameplayKeyCode = 0,
            ExactOwnedKeyStillDown = true,
            HoldState = owned,
        });
        Dispatch(repeated, "proven reset epoch");
        False(repeated.ShouldConsumeSharedInputGeneration, "repeat uses retained exact hold");
        True(repeated.ShouldSpendReadyEpoch, "repeat spends its proven epoch");
        Equal(owned.CurrentReadyEpochToken, repeated.Intent!.Value.ReadyEpochToken, "frozen epoch");

        None(
            Observation() with { HigherPriorityClaimed = true },
            DarkKnightPlungeDecisionReason.HigherPriorityClaimed,
            "strictly lower priority");
        None(
            Observation() with { ActionHelpersSuppressedByGuard = true },
            DarkKnightPlungeDecisionReason.GuardSuppressed,
            "own Guard suppression");
        None(
            Observation() with { LocalJobId = 30 },
            DarkKnightPlungeDecisionReason.LocalJobInvalid,
            "exact DRK job");
        None(
            Observation() with { CooldownStateKnown = false },
            DarkKnightPlungeDecisionReason.CooldownStateUnknown,
            "unknown cooldown");
        None(
            Observation() with { CooldownReady = false },
            DarkKnightPlungeDecisionReason.ActionNotReady,
            "active cooldown");
        None(
            Observation() with { ActionStructurallyReady = false },
            DarkKnightPlungeDecisionReason.ActionStructurallyUnavailable,
            "bound, animation lock, casting, or resources");
    }

    public static void FrozenIntentRequiresEveryTerminalGate()
    {
        var candidate = Candidate(2, 20_002, 2_002, 20, 100, 81f);
        var intent = new DarkKnightPlungeIntent(
            DarkKnightPlungeRules.ActionId,
            candidate.EnemySlot,
            candidate.Actor,
            HeldKeyCode: 65,
            ReadyEpochToken: 2);

        True(CanUse(intent, candidate), "unchanged frozen intent");
        False(CanUse(intent, candidate with { CurrentHp = 31 }), "target healed above threshold");
        False(CanUse(intent, candidate with { CenterDistanceSquared = 100.01f }), "target moved beyond ten yalms");
        False(CanUse(intent, candidate with { HasNativeRangeAndLineOfSight = false }), "native range drift");
        False(CanUse(intent, candidate with { EnemySlot = 3 }), "slot drift");
        False(CanUse(intent, candidate, exactHeldKeyStillDown: false), "exact key release");
        False(CanUse(intent, candidate, actionHelpersSuppressedByGuard: true), "Guard drift");
        False(CanUse(intent, candidate, higherPriorityClaimed: true), "priority drift");
        False(CanUse(intent, candidate, resolvedActionId: 1), "adjusted action drift");
        False(CanUse(intent, candidate, cooldownReady: false), "cooldown drift");
        False(CanUse(intent, candidate, actionStructurallyReady: false), "structural drift");
    }

    private static DarkKnightPlungeObservation Observation() => new(
        ConfigurationEnabled: true,
        IsCrystallineConflict: true,
        LocalJobId: DarkKnightPlungeRules.DarkKnightJobId,
        LocalPlayer,
        IsLocalPlayerAlive: true,
        IsLocalPlayerTargetable: true,
        MetadataVerified: true,
        ActionHelpersSuppressedByGuard: false,
        HigherPriorityClaimed: false,
        InputProbeSucceeded: true,
        IsTextInputActive: false,
        HeldGameplayKeyEligible: true,
        HeldGameplayKeyCode: 65,
        ExactOwnedKeyStillDown: false,
        HoldState: DarkKnightPlungeHoldState.Initial,
        ResolvedActionId: DarkKnightPlungeRules.ActionId,
        CooldownStateKnown: true,
        CooldownReady: true,
        ActionStructurallyReady: true,
        Candidates: [Candidate(1, 20_001, 2_001, 30, 100, 100f)]);

    private static DarkKnightPlungeCandidate Candidate(
        int slot,
        ulong gameObjectId,
        uint entityId,
        uint hp,
        uint maxHp,
        float centerDistanceSquared) => new(
        slot,
        new TargetPressureActorIdentity(gameObjectId, entityId),
        ExactCanonicalIdentity: true,
        Alive: true,
        Targetable: true,
        hp,
        maxHp,
        centerDistanceSquared,
        TargetGuardActive: false,
        HasValidActionTarget: true,
        HasNativeRangeAndLineOfSight: true);

    private static DarkKnightPlungeHoldDecision ObserveHold(
        DarkKnightPlungeHoldState state,
        bool ownershipContextValid = true,
        bool inputProbeSucceeded = true,
        bool isTextInputActive = false,
        int heldKeyCode = 65,
        bool exactHeldKeyStillDown = true,
        bool cooldownKnown = true,
        bool cooldownReady = true,
        bool hardReset = false) =>
        DarkKnightPlungeRules.ObserveOwnedHold(
            state,
            new DarkKnightPlungeHoldObservation(
                hardReset,
                ownershipContextValid,
                inputProbeSucceeded,
                isTextInputActive,
                heldKeyCode,
                exactHeldKeyStillDown,
                cooldownKnown,
                cooldownReady));

    private static bool CanUse(
        DarkKnightPlungeIntent intent,
        DarkKnightPlungeCandidate candidate,
        bool configurationEnabled = true,
        bool isCrystallineConflict = true,
        uint localJobId = DarkKnightPlungeRules.DarkKnightJobId,
        bool localAliveAndTargetable = true,
        bool metadataVerified = true,
        bool actionHelpersSuppressedByGuard = false,
        bool higherPriorityClaimed = false,
        bool inputProbeSucceeded = true,
        bool isTextInputActive = false,
        bool exactHeldKeyStillDown = true,
        uint resolvedActionId = DarkKnightPlungeRules.ActionId,
        bool cooldownStateKnown = true,
        bool cooldownReady = true,
        bool actionStructurallyReady = true) =>
        DarkKnightPlungeRules.CanUseExactIntent(
            intent,
            candidate,
            LocalPlayer,
            configurationEnabled,
            isCrystallineConflict,
            localJobId,
            localAliveAndTargetable,
            metadataVerified,
            actionHelpersSuppressedByGuard,
            higherPriorityClaimed,
            inputProbeSucceeded,
            isTextInputActive,
            exactHeldKeyStillDown,
            resolvedActionId,
            cooldownStateKnown,
            cooldownReady,
            actionStructurallyReady);

    private static void Dispatch(DarkKnightPlungeDecision decision, string label)
    {
        Equal(DarkKnightPlungeDecisionKind.Dispatch, decision.Kind, label);
        Equal(DarkKnightPlungeDecisionReason.None, decision.Reason, $"{label} reason");
        True(decision.ShouldDispatch, $"{label} flag");
    }

    private static void None(
        DarkKnightPlungeObservation observation,
        DarkKnightPlungeDecisionReason reason,
        string label)
    {
        var decision = DarkKnightPlungeRules.Evaluate(observation);
        Equal(DarkKnightPlungeDecisionKind.None, decision.Kind, label);
        Equal(reason, decision.Reason, $"{label} reason");
        False(decision.ShouldDispatch, $"{label} dispatch");
        False(decision.ShouldConsumeSharedInputGeneration, $"{label} shared input");
        False(decision.ShouldSpendReadyEpoch, $"{label} epoch");
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
