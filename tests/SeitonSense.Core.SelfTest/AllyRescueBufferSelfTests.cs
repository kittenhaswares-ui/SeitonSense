using SeitonSense.Core;

internal static class AllyRescueBufferSelfTests
{
    private static readonly AllyRescueSelectionCandidate CandidateA = Candidate(
        gameObjectId: 0x200,
        entityId: 0x20,
        instanceToken: 1,
        partySlot: 2);

    private static readonly AllyRescueSelectionCandidate CandidateB = Candidate(
        gameObjectId: 0x300,
        entityId: 0x30,
        instanceToken: 2,
        partySlot: 3);

    public static void SameFrameFreshDispatchConsumesBeforeCallAndNeverRetries()
    {
        var dispatched = Observe(
            AllyRescueBufferState.Initial,
            [CandidateA],
            freshKey: true,
            locallyReady: true,
            now: 1_000);

        True(dispatched.ShouldDispatch, "same-frame status and fresh key dispatch");
        True(dispatched.ShouldConsumeInputGeneration, "the owning input generation is consumed");
        Equal(CandidateA.Intent, dispatched.DispatchIntent!.Value, "exact actor and status are dispatched");
        True(dispatched.NextState.HasSpent(CandidateA.Intent), "intent is spent before native action call");

        var afterFalseOrException = Observe(
            dispatched.NextState,
            [CandidateA],
            freshKey: true,
            locallyReady: true,
            now: 1_001);
        False(afterFalseOrException.ShouldDispatch, "false or throwing native call is not retried");

        var missing = Observe(afterFalseOrException.NextState, [], now: 1_002);
        var sameInstanceReturns = Observe(
            missing.NextState,
            [CandidateA],
            freshKey: true,
            locallyReady: true,
            now: 1_003);
        False(sameInstanceReturns.ShouldDispatch, "an observation gap cannot rearm the exact spent instance");
    }

    public static void BufferWaitsForReadinessAndExpiresAt750Milliseconds()
    {
        Equal(750L, AllyRescueBufferRules.DefaultBufferMilliseconds, "default buffer");
        Equal(100L, AllyRescueBufferRules.NormalizeBufferMilliseconds(-1), "minimum clamp");
        Equal(750L, AllyRescueBufferRules.NormalizeBufferMilliseconds(50_000), "750 ms maximum clamp");

        var armed = Observe(
            AllyRescueBufferState.Initial,
            [CandidateA],
            freshKey: true,
            locallyReady: false,
            now: 1_000,
            bufferMilliseconds: 50_000);
        Equal(AllyRescueBufferDecisionKind.Armed, armed.Kind, "locked action buffers one intent");
        True(armed.ShouldConsumeInputGeneration, "input is consumed when armed, not when later ready");
        Equal(1_750L, armed.NextState.ExpiresAtMilliseconds, "buffer is capped at 750 ms");

        var waiting = Observe(armed.NextState, [CandidateA], locallyReady: false, now: 1_749);
        False(waiting.ShouldDispatch, "inside buffer waits");

        var timeout = Observe(waiting.NextState, [CandidateA], locallyReady: true, now: 1_750);
        Equal(AllyRescueBufferCancelReason.TimedOut, timeout.CancelReason, "deadline is exclusive");
        False(timeout.ShouldDispatch, "readiness at the expired boundary does not dispatch");
        False(timeout.NextState.HasSpent(CandidateA.Intent), "timeout without attempt is not marked spent");

        var rearmed = Observe(
            timeout.NextState,
            [CandidateA],
            freshKey: true,
            locallyReady: true,
            now: 1_751);
        True(rearmed.ShouldDispatch, "a genuinely new physical generation may rearm after no attempt");
    }

    public static void HeldInputOnlyCountsAtCandidateAppearanceOrReplacement()
    {
        var observed = Observe(
            AllyRescueBufferState.Initial,
            [CandidateA],
            allowHeldKey: true,
            locallyReady: true,
            now: 1_000);
        Equal(AllyRescueBufferDecisionKind.CandidateObserved, observed.Kind, "candidate is tracked without input");

        var heldLater = Observe(
            observed.NextState,
            [CandidateA],
            heldKeyEligible: true,
            allowHeldKey: true,
            locallyReady: true,
            now: 1_001);
        False(heldLater.ShouldDispatch, "held level cannot arm after the entry observation");

        var freshLater = Observe(
            heldLater.NextState,
            [CandidateA],
            freshKey: true,
            heldKeyEligible: true,
            allowHeldKey: true,
            locallyReady: true,
            now: 1_002);
        True(freshLater.ShouldDispatch, "later real down-edge dispatches");
        Equal(AllyRescueInputTrigger.FreshKeyPress, freshLater.InputTrigger, "fresh edge wins over held level");

        var heldAtReplacement = Observe(
            AllyRescueBufferState.Initial,
            [CandidateA],
            heldKeyEligible: true,
            allowHeldKey: true,
            locallyReady: true,
            now: 2_000);
        True(heldAtReplacement.ShouldDispatch, "explicit eligible hold may own candidate appearance");
        Equal(
            AllyRescueInputTrigger.HeldKeyAtCandidateEntry,
            heldAtReplacement.InputTrigger,
            "held entry trigger is explicit");
    }

    public static void OnePhysicalGenerationCannotCrossActorOrStatusReplacement()
    {
        var keyState = PhysicalGameplayKeyRules.Observe(
            PhysicalGameplayKeyState.Initial,
            new PhysicalGameplayKeyObservation(IsDown: false, IsTextInputActive: false)).NextState;
        var press = PhysicalGameplayKeyRules.Observe(
            keyState,
            new PhysicalGameplayKeyObservation(IsDown: true, IsTextInputActive: false));

        var first = Observe(
            AllyRescueBufferState.Initial,
            [CandidateA],
            freshKey: press.IsFreshPress,
            heldKeyEligible: press.IsHeldEligible,
            allowHeldKey: true,
            locallyReady: true,
            now: 1_000);
        True(first.ShouldDispatch, "first actor owns the press");
        keyState = PhysicalGameplayKeyRules.Consume(press.NextState);

        var stillHeld = PhysicalGameplayKeyRules.Observe(
            keyState,
            new PhysicalGameplayKeyObservation(IsDown: true, IsTextInputActive: false));
        var replacement = Observe(
            first.NextState,
            [CandidateB],
            freshKey: stillHeld.IsFreshPress,
            heldKeyEligible: stillHeld.IsHeldEligible,
            allowHeldKey: true,
            locallyReady: true,
            now: 1_001);
        False(replacement.ShouldDispatch, "same held generation cannot jump to a new actor");

        keyState = PhysicalGameplayKeyRules.Observe(
            stillHeld.NextState,
            new PhysicalGameplayKeyObservation(IsDown: false, IsTextInputActive: false)).NextState;
        var newPress = PhysicalGameplayKeyRules.Observe(
            keyState,
            new PhysicalGameplayKeyObservation(IsDown: true, IsTextInputActive: false));
        var second = Observe(
            replacement.NextState,
            [CandidateB],
            freshKey: newPress.IsFreshPress,
            heldKeyEligible: newPress.IsHeldEligible,
            allowHeldKey: true,
            locallyReady: true,
            now: 1_002);
        True(second.ShouldDispatch, "release and repress may rescue a new actor");

        var refreshedStatus = CandidateB with
        {
            Status = new AllyRescueStatusInstance(
                AllyRescueStatusRules.StunStatusId,
                InstanceToken: 3),
        };
        var withoutNewGeneration = Observe(
            second.NextState,
            [refreshedStatus],
            allowHeldKey: true,
            locallyReady: true,
            now: 1_003);
        False(withoutNewGeneration.ShouldDispatch, "new status application still requires a new physical generation");
    }

    public static void CandidateChangesCancelBufferedIntentWithoutTargetDrift()
    {
        var armed = Observe(
            AllyRescueBufferState.Initial,
            [CandidateA],
            freshKey: true,
            locallyReady: false,
            now: 1_000);
        var changed = Observe(
            armed.NextState,
            [CandidateB],
            locallyReady: true,
            now: 1_001);

        Equal(AllyRescueBufferCancelReason.CandidateChanged, changed.CancelReason, "target change cancels old buffer");
        False(changed.ShouldDispatch, "old input never drifts onto replacement actor");
        Equal(CandidateB.Intent, changed.NextState.TrackedIntent!.Value, "replacement waits for its own key");
    }

    public static void RankingIsResolvedBeforeTheInputIsOwned()
    {
        var higherHp = CandidateA with
        {
            CurrentHp = 80,
            MaximumHp = 100,
            UniqueIncomingEnemyPressureCount = 5,
        };
        var lowerHp = CandidateB with
        {
            CurrentHp = 20,
            MaximumHp = 100,
            UniqueIncomingEnemyPressureCount = 0,
        };
        var decision = Observe(
            AllyRescueBufferState.Initial,
            [higherHp, lowerHp],
            freshKey: true,
            locallyReady: true,
            now: 1_000);

        True(decision.ShouldDispatch, "valid best candidate dispatches");
        Equal(1, decision.SelectedCandidateIndex, "lowest exact HP is chosen before pressure");
        Equal(lowerHp.Intent, decision.DispatchIntent!.Value, "dispatch identity matches selected candidate");
    }

    public static void SafetyGatesAndHardResetFailClosed()
    {
        var armed = Observe(
            AllyRescueBufferState.Initial,
            [CandidateA],
            freshKey: true,
            locallyReady: false,
            now: 1_000);
        var textInput = AllyRescueBufferRules.Observe(
            armed.NextState,
            ValidObservation([CandidateA], now: 1_001) with
            {
                IsTextInputActive = true,
                FreshKeyPressed = true,
                ActionLocallyReady = true,
            });
        Equal(AllyRescueBufferCancelReason.TextInputActive, textInput.CancelReason, "typing cancels buffered intent");
        False(textInput.ShouldDispatch, "typing wins over key and readiness");

        var invalidClock = Observe(
            textInput.NextState,
            [CandidateA],
            freshKey: true,
            locallyReady: true,
            now: 1_000);
        Equal(AllyRescueBufferCancelReason.InvalidClock, invalidClock.CancelReason, "clock regression fails closed");

        var reset = AllyRescueBufferRules.Observe(
            invalidClock.NextState,
            ValidObservation([CandidateA], now: 2_000) with
            {
                FreshKeyPressed = true,
                ActionLocallyReady = true,
                HardReset = true,
            });
        Equal(AllyRescueBufferCancelReason.HardReset, reset.CancelReason, "hard reset reason");
        Equal(AllyRescueBufferState.Initial, reset.NextState, "hard reset clears tracking and ledger");
        False(reset.ShouldDispatch, "hard reset never dispatches");
    }

    public static void SelfPurifyOwnsTheSharedInputBeforeAllyRescue()
    {
        var armedPurify = new EmergencyPurifyBufferDecision(
            EmergencyPurifyBufferState.Initial,
            EmergencyPurifyBufferDecisionKind.Armed,
            EmergencyPurifyBufferCancelReason.None,
            EmergencyPurifyInputTrigger.FreshKeyPress);
        False(
            EmergencyActionPriorityRules.AllowAllyRescue(armedPurify),
            "a newly armed self-Purify blocks Ally Rescue in the same frame");

        var bufferedPurifyDispatch = new EmergencyPurifyBufferDecision(
            EmergencyPurifyBufferState.Initial,
            EmergencyPurifyBufferDecisionKind.Dispatch,
            EmergencyPurifyBufferCancelReason.None,
            EmergencyPurifyInputTrigger.None);
        False(
            EmergencyActionPriorityRules.AllowAllyRescue(bufferedPurifyDispatch),
            "an older buffered self-Purify dispatch still blocks Ally Rescue");

        var bufferedRescue = Observe(
            AllyRescueBufferState.Initial,
            [CandidateA],
            freshKey: true,
            locallyReady: false,
            now: 1_000);
        var cancelledRescue = AllyRescueBufferRules.Observe(
            bufferedRescue.NextState,
            ValidObservation([CandidateA], now: 1_001) with
            {
                ConfigurationEnabled = EmergencyActionPriorityRules.AllowAllyRescue(
                    bufferedPurifyDispatch),
                ActionLocallyReady = true,
            });
        False(cancelledRescue.ShouldDispatch, "a buffered rescue cannot dispatch beside self-Purify");
        Equal(
            AllyRescueBufferCancelReason.ConfigurationDisabled,
            cancelledRescue.CancelReason,
            "self-Purify cancels the older rescue intent for that frame");

        var purifyWaiting = new EmergencyPurifyBufferDecision(
            EmergencyPurifyBufferState.Initial,
            EmergencyPurifyBufferDecisionKind.StatusObserved,
            EmergencyPurifyBufferCancelReason.None,
            EmergencyPurifyInputTrigger.None);
        True(
            EmergencyActionPriorityRules.AllowAllyRescue(purifyWaiting),
            "a status observation without input ownership does not steal another generation");
    }

    private static AllyRescueBufferDecision Observe(
        AllyRescueBufferState state,
        IReadOnlyList<AllyRescueSelectionCandidate> candidates,
        bool freshKey = false,
        bool heldKeyEligible = false,
        bool allowHeldKey = false,
        bool locallyReady = false,
        long now = 0,
        long bufferMilliseconds = AllyRescueBufferRules.DefaultBufferMilliseconds) =>
        AllyRescueBufferRules.Observe(
            state,
            ValidObservation(candidates, now) with
            {
                FreshKeyPressed = freshKey,
                HeldKeyEligible = heldKeyEligible,
                AllowHeldKeyAtCandidateEntry = allowHeldKey,
                ActionLocallyReady = locallyReady,
                BufferMilliseconds = bufferMilliseconds,
            });

    private static AllyRescueBufferObservation ValidObservation(
        IReadOnlyList<AllyRescueSelectionCandidate> candidates,
        long now) =>
        new(
            ConfigurationEnabled: true,
            IsSupportedPvPContext: true,
            IsLocalPlayerAlive: true,
            IsLocalPlayerIdentityValid: true,
            IsTextInputActive: false,
            Candidates: candidates,
            FreshKeyPressed: false,
            HeldKeyEligible: false,
            AllowHeldKeyAtCandidateEntry: false,
            ActionLocallyReady: false,
            NowMilliseconds: now);

    private static AllyRescueSelectionCandidate Candidate(
        ulong gameObjectId,
        uint entityId,
        ulong instanceToken,
        int partySlot) =>
        new(
            gameObjectId,
            entityId,
            partySlot,
            new AllyRescueStatusInstance(AllyRescueStatusRules.StunStatusId, instanceToken),
            CurrentHp: 50,
            MaximumHp: 100,
            UniqueIncomingEnemyPressureCount: 0,
            CurrentMp: 5_000,
            MaximumMp: 10_000,
            HasTrustedMp: true,
            DistanceSquared: 25,
            IsExactPartyMember: true,
            IsSelf: false,
            IsAlive: true,
            IsTargetable: true,
            HasValidActionTarget: true,
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
