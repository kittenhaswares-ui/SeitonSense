using SeitonSense.Core;

internal static class HeldChaseBufferSelfTests
{
    private static readonly HeldChaseBufferIntent Intent = new(
        RequestedActionId: 10_001,
        ResolvedActionId: 10_002,
        TargetFingerprint: 0xAABBCC,
        TerritoryId: 250,
        InstanceFingerprint: 77,
        PressGeneration: 9);

    internal static void OnlyRangeOrLineOfSightCanArm()
    {
        True(
            SmartActionFallbackInvocationRules.IsSupportedCarrier(
                explicitMacroCarrier: true,
                directCarrier: false,
                queueCarrier: false),
            "explicit Smart Action macro carrier is admitted");
        True(
            SmartActionFallbackInvocationRules.IsSupportedCarrier(
                explicitMacroCarrier: false,
                directCarrier: true,
                queueCarrier: false),
            "normal-mode Smart Action macro carrier is admitted after exact ownership proof");
        False(
            SmartActionFallbackInvocationRules.IsSupportedCarrier(
                explicitMacroCarrier: true,
                directCarrier: true,
                queueCarrier: true),
            "queued Smart Action carrier is rejected");
        False(
            SmartActionFallbackInvocationRules.IsSupportedCarrier(
                explicitMacroCarrier: false,
                directCarrier: false,
                queueCarrier: false),
            "unknown Smart Action carrier is rejected");

        Equal(2_200, HeldChaseBufferWindowRules.DefaultMilliseconds, "default reservation window");
        Equal(0, HeldChaseBufferWindowRules.Normalize(-1), "negative window disables");
        Equal(0, HeldChaseBufferWindowRules.Normalize(0), "zero window disables");
        Equal(2_200, HeldChaseBufferWindowRules.Normalize(2_200), "default window remains exact");
        Equal(3_000, HeldChaseBufferWindowRules.Normalize(9_000), "window is capped");
        Equal(0, HeldChaseBufferWindowRules.ResolveNativeAttemptLimit(0), "disabled window has no attempts");
        Equal(45, HeldChaseBufferWindowRules.ResolveNativeAttemptLimit(2_200), "default window freezes 45 attempts");
        Equal(61, HeldChaseBufferWindowRules.ResolveNativeAttemptLimit(3_000), "maximum window freezes 61 attempts");

        var valid = ArmInput();
        var engine = new HeldChaseBufferEngine();
        True(engine.Arm(valid), "range-only failure arms");
        Equal(Intent, engine.Pending.GetValueOrDefault(), "frozen intent");
        Equal(45, engine.RetryState.NativeAttemptLimit, "arm freezes default attempt cap");

        var macroFallback = valid with
        {
            IsCertifiedPhysicalStandardHotbarRoot = false,
            IsCertifiedSmartActionMacroFallback = true,
        };
        var macroEngine = new HeldChaseBufferEngine();
        True(macroEngine.Arm(macroFallback), "certified Smart Action fallback can arm");
        Equal(Intent, macroEngine.Pending.GetValueOrDefault(), "macro fallback freezes the exact intent");

        var rejected = new (HeldChaseBufferArmInput Input, HeldChaseBufferCancelReason Reason)[]
        {
            (valid with { Enabled = false }, HeldChaseBufferCancelReason.Disabled),
            (valid with { ReservationWindowMilliseconds = 0 }, HeldChaseBufferCancelReason.Disabled),
            (valid with { IsCertifiedPhysicalStandardHotbarRoot = false }, HeldChaseBufferCancelReason.NotPhysicalStandardHotbar),
            (valid with { IsCertifiedSmartActionMacroFallback = true }, HeldChaseBufferCancelReason.AmbiguousInputOrigin),
            (valid with { ActionEligible = false }, HeldChaseBufferCancelReason.Ineligible),
            (valid with { SafetyValid = false }, HeldChaseBufferCancelReason.SafetyDrift),
            (valid with { RangeProbeAvailable = false }, HeldChaseBufferCancelReason.RangeUnavailable),
            (valid with { HasRangeAndLineOfSight = true }, HeldChaseBufferCancelReason.RangeAlreadyAvailable),
            (valid with { OtherNativeGatesReady = false }, HeldChaseBufferCancelReason.OtherNativeGateUnavailable),
            (valid with { Intent = Intent with { TargetFingerprint = 0 } }, HeldChaseBufferCancelReason.InvalidIntent),
        };

        foreach (var candidate in rejected)
        {
            var rejectedEngine = new HeldChaseBufferEngine();
            False(rejectedEngine.Arm(candidate.Input), candidate.Reason.ToString());
            Equal(candidate.Reason, rejectedEngine.LastCancelReason, $"{candidate.Reason} reason");
            False(rejectedEngine.Pending.HasValue, $"{candidate.Reason} remains empty");
        }
    }

    internal static void ReleaseNewInputAndFrozenIdentityDriftCancel()
    {
        var released = Armed();
        var first = released.Evaluate(LiveInput(nowMilliseconds: 100));
        Equal(HeldChaseBufferDecisionKind.WaitingForRange, first.Kind, "initial tap waits for range");

        // No held-level fact exists in the live contract. A later observation
        // with the same press generation therefore models key-up and preserves
        // the exact reservation until another explicit terminal fact appears.
        var afterKeyUp = released.Evaluate(LiveInput(nowMilliseconds: 1_100));
        Equal(HeldChaseBufferDecisionKind.WaitingForRange, afterKeyUp.Kind, "release alone remains reserved");
        Equal(Intent, afterKeyUp.Intent.GetValueOrDefault(), "release preserves exact tap identity");
        True(released.Pending.HasValue, "release does not clear pending tap");
        released.Cancel(HeldChaseBufferCancelReason.Released);
        True(released.Pending.HasValue, "legacy release callback is explicitly ignored");

        var live = LiveInput();
        var cancellations = new (HeldChaseBufferLiveInput Input, HeldChaseBufferCancelReason Reason)[]
        {
            (live with { PressGeneration = Intent.PressGeneration + 1 }, HeldChaseBufferCancelReason.Replaced),
            (live with { RequestedActionId = Intent.RequestedActionId + 1 }, HeldChaseBufferCancelReason.ActionChanged),
            (live with { ResolvedActionId = Intent.ResolvedActionId + 1 }, HeldChaseBufferCancelReason.ActionChanged),
            (live with { TargetFingerprint = Intent.TargetFingerprint + 1 }, HeldChaseBufferCancelReason.TargetChanged),
            (live with { TerritoryId = Intent.TerritoryId + 1 }, HeldChaseBufferCancelReason.ContextChanged),
            (live with { InstanceFingerprint = Intent.InstanceFingerprint + 1 }, HeldChaseBufferCancelReason.ContextChanged),
        };

        foreach (var cancellation in cancellations)
        {
            var engine = Armed();
            var decision = engine.Evaluate(cancellation.Input);
            Equal(HeldChaseBufferDecisionKind.Cancelled, decision.Kind, $"{cancellation.Reason} kind");
            Equal(cancellation.Reason, decision.Reason, $"{cancellation.Reason} reason");
            False(decision.Intent.HasValue, $"{cancellation.Reason} cannot return intent");
            False(engine.Pending.HasValue, $"{cancellation.Reason} clears pending");
        }

        var oldIntent = Intent;
        var newIntent = Intent with { PressGeneration = Intent.PressGeneration + 1 };
        var replaced = Armed();
        Equal(
            HeldChaseBufferDecisionKind.Dispatch,
            replaced.Evaluate(LiveInput(hasRange: true)).Kind,
            "old intent reserves native boundary");
        True(replaced.Arm(ArmInput(newIntent)), "new tap replaces outstanding old tap");
        var staleCompletion = replaced.CompleteNativeAttempt(
            oldIntent,
            100,
            ClientActionAttemptOutcome.ClientAccepted);
        Equal(HeldActionRetryDisposition.CancelledTerminal, staleCompletion.Disposition, "stale completion is ignored");
        Equal(newIntent, replaced.Pending.GetValueOrDefault(), "stale completion cannot erase replacement");
    }

    internal static void EveryLiveSafetyDriftCancels()
    {
        var live = LiveInput();
        var cancellations = new (HeldChaseBufferLiveInput Input, HeldChaseBufferCancelReason Reason)[]
        {
            (live with { Enabled = false }, HeldChaseBufferCancelReason.Disabled),
            (live with { WithinDeadline = false }, HeldChaseBufferCancelReason.Expired),
            (live with { ActionEligible = false }, HeldChaseBufferCancelReason.Ineligible),
            (live with { SafetyValid = false }, HeldChaseBufferCancelReason.SafetyDrift),
            (live with { RangeProbeAvailable = false }, HeldChaseBufferCancelReason.RangeUnavailable),
            (live with { OtherNativeGatesReady = false }, HeldChaseBufferCancelReason.OtherNativeGateUnavailable),
        };

        foreach (var cancellation in cancellations)
        {
            var engine = Armed();
            var decision = engine.Evaluate(cancellation.Input);
            Equal(HeldChaseBufferDecisionKind.Cancelled, decision.Kind, $"{cancellation.Reason} kind");
            Equal(cancellation.Reason, decision.Reason, $"{cancellation.Reason} reason");
            False(engine.Pending.HasValue, $"{cancellation.Reason} clears pending");
        }
    }

    internal static void FirstReachableEdgeDispatchesExactlyOnce()
    {
        var engine = Armed();
        var waiting = engine.Evaluate(LiveInput(nowMilliseconds: 100));
        Equal(HeldChaseBufferDecisionKind.WaitingForRange, waiting.Kind, "waiting kind");
        Equal(Intent, waiting.Intent.GetValueOrDefault(), "waiting preserves exact intent");
        True(engine.Pending.HasValue, "waiting stays armed");

        var reachable = engine.Evaluate(LiveInput(hasRange: true, nowMilliseconds: 100));
        Equal(HeldChaseBufferDecisionKind.Dispatch, reachable.Kind, "first reachable edge");
        Equal(Intent, reachable.Intent.GetValueOrDefault(), "dispatched exact intent");
        Equal(HeldChaseBufferCancelReason.None, reachable.Reason, "attempt is not terminal before outcome");
        True(engine.Pending.HasValue, "intent remains frozen across native boundary");
        True(engine.NativeAttemptOutstanding, "native boundary is reserved");

        var duplicate = engine.Evaluate(LiveInput(hasRange: true, nowMilliseconds: 100));
        Equal(HeldChaseBufferDecisionKind.WaitingForNativeOutcome, duplicate.Kind, "duplicate waits for outcome");
        Equal(Intent, duplicate.Intent.GetValueOrDefault(), "outstanding attempt preserves exact intent");

        var accepted = engine.CompleteNativeAttempt(
            Intent,
            100,
            ClientActionAttemptOutcome.ClientAccepted);
        Equal(HeldActionRetryDisposition.AcceptedTerminal, accepted.Disposition, "native true is terminal");
        False(engine.Pending.HasValue, "accepted intent clears");
        False(engine.NativeAttemptOutstanding, "accepted boundary clears outstanding state");

        var afterAccepted = engine.Evaluate(LiveInput(hasRange: true, nowMilliseconds: 101));
        Equal(HeldChaseBufferDecisionKind.None, afterAccepted.Kind, "accepted intent never repeats");

        const int ContenderCount = 24;
        var concurrent = Armed();
        using var start = new ManualResetEventSlim(initialState: false);
        var contenders = Enumerable.Range(0, ContenderCount)
            .Select(_ => Task.Run(() =>
            {
                start.Wait();
                return concurrent.Evaluate(
                    LiveInput(hasRange: true, nowMilliseconds: 200));
            }))
            .ToArray();
        start.Set();
        Task.WaitAll(contenders);

        Equal(
            1,
            contenders.Count(task =>
                task.Result.Kind == HeldChaseBufferDecisionKind.Dispatch),
            "one concurrent dispatch");
        Equal(
            ContenderCount - 1,
            contenders.Count(task =>
                task.Result.Kind == HeldChaseBufferDecisionKind.WaitingForNativeOutcome),
            "all concurrent followers wait for the one outcome");
        Equal(
            HeldActionRetryDisposition.AcceptedTerminal,
            concurrent.CompleteNativeAttempt(
                Intent,
                200,
                ClientActionAttemptOutcome.ClientAccepted).Disposition,
            "concurrent winner completes once");

        var retry = Armed();
        Equal(
            HeldChaseBufferDecisionKind.Dispatch,
            retry.Evaluate(LiveInput(hasRange: true, nowMilliseconds: 300)).Kind,
            "first retry episode attempt");
        var rejected = retry.CompleteNativeAttempt(
            Intent,
            300,
            ClientActionAttemptOutcome.ClientRejected);
        True(rejected.RetryScheduled, "only clean client false schedules retry");
        Equal(1, rejected.NextState.NativeAttemptCount, "clean false spends one attempt");
        Equal(350L, rejected.NextState.NextNativeAttemptAtMilliseconds, "clean false applies exact throttle");
        Equal(45, rejected.NextState.NativeAttemptLimit, "default retry cap remains frozen");
        Equal(
            HeldChaseBufferDecisionKind.WaitingForRetry,
            retry.Evaluate(LiveInput(hasRange: true, nowMilliseconds: 349)).Kind,
            "retry cannot run before throttle");
        Equal(
            HeldChaseBufferDecisionKind.Dispatch,
            retry.Evaluate(LiveInput(hasRange: true, nowMilliseconds: 350)).Kind,
            "retry runs at exact throttle boundary");
        var ambiguous = retry.CompleteNativeAttempt(
            Intent,
            350,
            ClientActionAttemptOutcome.AcceptanceUnknown);
        Equal(HeldActionRetryDisposition.AmbiguousTerminal, ambiguous.Disposition, "ambiguity is terminal");
        False(retry.Pending.HasValue, "ambiguity cannot retry");

        foreach (var nonRetryable in new[]
                 {
                     ClientActionAttemptOutcome.NotInvoked,
                     ClientActionAttemptOutcome.SoftUnavailable,
                     ClientActionAttemptOutcome.None,
                 })
        {
            var terminal = Armed();
            terminal.Evaluate(LiveInput(hasRange: true, nowMilliseconds: 400));
            var completion = terminal.CompleteNativeAttempt(Intent, 400, nonRetryable);
            Equal(HeldActionRetryDisposition.CancelledTerminal, completion.Disposition, $"{nonRetryable} terminal");
            False(terminal.Pending.HasValue, $"{nonRetryable} cannot retain reservation");
        }

        var bounded = Armed();
        var now = 500L;
        for (var attempt = 1; attempt <= 45; attempt++)
        {
            Equal(
                HeldChaseBufferDecisionKind.Dispatch,
                bounded.Evaluate(LiveInput(hasRange: true, nowMilliseconds: now)).Kind,
                $"bounded clean-false attempt {attempt}");
            var completion = bounded.CompleteNativeAttempt(
                Intent,
                now,
                ClientActionAttemptOutcome.ClientRejected);
            if (attempt < 45)
            {
                True(completion.RetryScheduled, $"bounded retry {attempt}");
                True(bounded.Pending.HasValue, $"bounded retry {attempt} retains exact intent");
            }
            else
            {
                Equal(HeldActionRetryDisposition.RejectedTerminal, completion.Disposition, "45th false exhausts default cap");
                False(bounded.Pending.HasValue, "exhausted cap clears reservation");
            }

            now += HeldChaseBufferWindowRules.NativeRetryThrottleMilliseconds;
        }
    }

    internal static void SmartActionMacroTailIsExactAndGenerationBound()
    {
        var ranked = new SmartActionChaseMacroTailObservation(
            PendingChase: true,
            TailBudgetAvailable: true,
            CertifiedSmartActionMacroRoot: true,
            FrozenTapGeneration: 9,
            SafetyLeaseTapGeneration: 9,
            FrozenActionType: 11,
            IncomingActionType: 11,
            FrozenRequestedActionId: 10_001,
            IncomingRequestedActionId: 10_001,
            FrozenResolvedActionId: 10_002,
            IncomingResolvedActionId: 10_002,
            CapturedVisibleGameObjectId: 0,
            CapturedVisibleEntityId: 0,
            IncomingTargetId: 0,
            IsMacroCarrier: true,
            IsQueueCarrier: false);
        True(SmartActionChaseMacroTailRules.ShouldSuppress(ranked),
            "ranked hidden target suppresses only its generation-bound authored macro tail");
        True(SmartActionChaseMacroTailRules.ShouldSuppress(
                ranked with { IncomingTargetId = 0xE0000000 }),
            "missing authored target accepts the native invalid carrier sentinel");
        False(SmartActionChaseMacroTailRules.ShouldSuppress(
                ranked with { IncomingTargetId = 0x999 }),
            "an unrelated visible target is never mistaken for the authored tail");

        var visibleFallback = ranked with
        {
            CapturedVisibleGameObjectId = 0x700,
            CapturedVisibleEntityId = 0x701,
            IncomingTargetId = 0x700,
        };
        True(SmartActionChaseMacroTailRules.ShouldSuppress(visibleFallback),
            "visible fallback requires the exact frozen target");
        True(SmartActionChaseMacroTailRules.ShouldSuppress(
                visibleFallback with { IncomingTargetId = 0x701 }),
            "either exact native identity form can carry the authored target");
        False(SmartActionChaseMacroTailRules.ShouldSuppress(
                visibleFallback with { IncomingTargetId = 0x999 }),
            "another visible target is never swallowed");
        False(SmartActionChaseMacroTailRules.ShouldSuppress(
                visibleFallback with { CapturedVisibleGameObjectId = 0 }),
            "a partial visible identity fails closed");
        False(SmartActionChaseMacroTailRules.ShouldSuppress(
                visibleFallback with { CapturedVisibleEntityId = 0xE0000000 }),
            "an invalid visible identity sentinel fails closed");

        foreach (var drift in new[]
                 {
                     ranked with { PendingChase = false },
                     ranked with { TailBudgetAvailable = false },
                     ranked with { CertifiedSmartActionMacroRoot = false },
                     ranked with { SafetyLeaseTapGeneration = 10 },
                     ranked with { IncomingActionType = 12 },
                     ranked with { IncomingRequestedActionId = 10_003 },
                     ranked with { IncomingResolvedActionId = 10_004 },
                     ranked with { IsMacroCarrier = false },
                     ranked with { IsQueueCarrier = true },
                 })
        {
            False(SmartActionChaseMacroTailRules.ShouldSuppress(drift),
                "tail ownership drift preserves newer or unrelated input");
        }
    }

    private static HeldChaseBufferEngine Armed(
        int windowMilliseconds = HeldChaseBufferWindowRules.DefaultMilliseconds)
    {
        var engine = new HeldChaseBufferEngine();
        True(engine.Arm(ArmInput(windowMilliseconds: windowMilliseconds)), "arm");
        return engine;
    }

    private static HeldChaseBufferArmInput ArmInput(
        HeldChaseBufferIntent? intent = null,
        int windowMilliseconds = HeldChaseBufferWindowRules.DefaultMilliseconds) => new(
        intent ?? Intent,
        Enabled: true,
        IsCertifiedPhysicalStandardHotbarRoot: true,
        ActionEligible: true,
        SafetyValid: true,
        RangeProbeAvailable: true,
        HasRangeAndLineOfSight: false,
        OtherNativeGatesReady: true,
        ReservationWindowMilliseconds: windowMilliseconds);

    private static HeldChaseBufferLiveInput LiveInput(
        HeldChaseBufferIntent? intent = null,
        bool hasRange = false,
        long nowMilliseconds = 100)
    {
        var exact = intent ?? Intent;
        return new HeldChaseBufferLiveInput(
            Enabled: true,
            exact.PressGeneration,
            exact.RequestedActionId,
            exact.ResolvedActionId,
            exact.TargetFingerprint,
            exact.TerritoryId,
            exact.InstanceFingerprint,
            ActionEligible: true,
            SafetyValid: true,
            RangeProbeAvailable: true,
            HasRangeAndLineOfSight: hasRange,
            OtherNativeGatesReady: true,
            WithinDeadline: true,
            NowMilliseconds: nowMilliseconds);
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
