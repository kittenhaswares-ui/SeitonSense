using SeitonSense.Core;

internal static class SmartActionBufferSelfTests
{
    private static readonly SmartActionBufferAction BaseAction = new(
        RequestedActionId: 10_001,
        ResolvedActionId: 10_002,
        TargetId: 0xAABBCC,
        TerritoryId: 250,
        InstanceId: 77);

    internal static void WindowDefaultsAndBoundsAreExact()
    {
        Equal(1_000, SmartActionBufferWindowRules.DefaultMilliseconds, "default");
        Equal(100, SmartActionBufferWindowRules.MinimumMilliseconds, "minimum");
        Equal(1_500, SmartActionBufferWindowRules.MaximumMilliseconds, "maximum");
        Equal(100, SmartActionBufferWindowRules.Normalize(int.MinValue), "low clamp");
        Equal(1_234, SmartActionBufferWindowRules.Normalize(1_234), "interior value");
        Equal(1_500, SmartActionBufferWindowRules.Normalize(int.MaxValue), "high clamp");
    }

    internal static void OnlyEligibleTransientFailuresArm()
    {
        foreach (var failure in new[]
                 {
                     SmartActionBufferFailure.GlobalCooldown,
                     SmartActionBufferFailure.AnimationLock,
                     SmartActionBufferFailure.Cooldown,
                 })
        {
            var engine = new SmartActionBufferEngine();
            True(engine.Arm(Intent(failure), 1_000), $"{failure} arms");
        }

        var ineligible = new SmartActionBufferEngine();
        False(ineligible.Arm(Intent(isEligible: false), 1_000), "explicitly ineligible");
        Equal(SmartActionBufferCancelReason.Ineligible, ineligible.LastCancelReason, "ineligible reason");

        var localFailure = new SmartActionBufferEngine();
        False(localFailure.Arm(Intent(SmartActionBufferFailure.OutOfRange), 1_000), "non-transient failure");
        Equal(
            SmartActionBufferCancelReason.NonTransientFailure,
            localFailure.LastCancelReason,
            "non-transient reason");

        var serverFailure = new SmartActionBufferEngine();
        False(serverFailure.Arm(Intent(SmartActionBufferFailure.ServerRejected), 1_000), "server failure");
        Equal(
            SmartActionBufferCancelReason.ServerRejected,
            serverFailure.LastCancelReason,
            "server rejection is terminal");
    }

    internal static void FrozenIdentityNeverRetargetsOrSubstitutes()
    {
        var drifts = new (SmartActionBufferSafety Safety, SmartActionBufferCancelReason Reason)[]
        {
            (Safe() with { TargetId = BaseAction.TargetId + 1 }, SmartActionBufferCancelReason.TargetChange),
            (Safe() with { RequestedActionId = BaseAction.RequestedActionId + 1 }, SmartActionBufferCancelReason.RequestedActionChange),
            (Safe() with { ResolvedActionId = BaseAction.ResolvedActionId + 1 }, SmartActionBufferCancelReason.ResolvedActionChange),
            (Safe() with { TerritoryId = BaseAction.TerritoryId + 1 }, SmartActionBufferCancelReason.TerritoryChange),
            (Safe() with { InstanceId = BaseAction.InstanceId + 1 }, SmartActionBufferCancelReason.InstanceChange),
        };

        foreach (var drift in drifts)
        {
            var engine = Armed();
            var decision = engine.Evaluate(
                new SmartActionBufferContext(drift.Safety, ActionIsExecutable: true),
                nowMilliseconds: 1_001);

            Equal(SmartActionBufferDecisionKind.Cancelled, decision.Kind, $"{drift.Reason} kind");
            Equal(drift.Reason, decision.Reason, $"{drift.Reason} reason");
            False(decision.Intent.HasValue, $"{drift.Reason} cannot return a substitute");
            False(engine.Pending.HasValue, $"{drift.Reason} clears pending");
        }

        var exactEngine = Armed();
        var exactDecision = exactEngine.Evaluate(
            new SmartActionBufferContext(Safe(), ActionIsExecutable: true),
            nowMilliseconds: 1_001);
        Equal(SmartActionBufferDecisionKind.Dispatch, exactDecision.Kind, "exact dispatch");
        Equal(BaseAction, exactDecision.Intent.GetValueOrDefault().Action, "frozen action and target");
    }

    internal static void InternalPriorityPausesOnlyFinalDispatch()
    {
        var engine = Armed(holdMilliseconds: 1_500);
        var paused = engine.Evaluate(
            new SmartActionBufferContext(
                Safe(),
                ActionIsExecutable: true,
                InternalPriorityClaimed: true),
            nowMilliseconds: 2_499);

        Equal(SmartActionBufferDecisionKind.None, paused.Kind, "internal priority pauses dispatch");
        True(engine.Pending.HasValue, "safe unexpired intent remains pending");

        var resumed = engine.Evaluate(
            new SmartActionBufferContext(Safe(), ActionIsExecutable: true),
            nowMilliseconds: 2_499);
        Equal(SmartActionBufferDecisionKind.Dispatch, resumed.Kind, "dispatch resumes after priority clears");

        var unsafeEngine = Armed(holdMilliseconds: 1_500);
        var unsafeDecision = unsafeEngine.Evaluate(
            new SmartActionBufferContext(
                Safe() with { IsStunned = true },
                ActionIsExecutable: true,
                DispatchPaused: true),
            nowMilliseconds: 1_001);
        Equal(SmartActionBufferDecisionKind.Cancelled, unsafeDecision.Kind, "pause does not bypass safety");
        Equal(SmartActionBufferCancelReason.Stun, unsafeDecision.Reason, "stun cancellation");
    }

    internal static void EveryRuntimeSafetyGateCancels()
    {
        var unsafeStates = new (SmartActionBufferSafety Safety, SmartActionBufferCancelReason Reason)[]
        {
            (Safe() with { Enabled = false }, SmartActionBufferCancelReason.Disabled),
            (Safe() with { ConflictDetected = true }, SmartActionBufferCancelReason.Conflict),
            (Safe() with { LoggedIn = false }, SmartActionBufferCancelReason.Logout),
            (Safe() with { IsAlive = false }, SmartActionBufferCancelReason.Death),
            (Safe() with { IsMounted = true }, SmartActionBufferCancelReason.Mounted),
            (Safe() with { IsStunned = true }, SmartActionBufferCancelReason.Stun),
            (Safe() with { IsKnockbackActive = true }, SmartActionBufferCancelReason.Knockback),
        };

        foreach (var unsafeState in unsafeStates)
        {
            var engine = Armed();
            var decision = engine.Evaluate(
                new SmartActionBufferContext(
                    unsafeState.Safety,
                    ActionIsExecutable: false,
                    InternalPriorityClaimed: true),
                nowMilliseconds: 1_001);

            Equal(SmartActionBufferDecisionKind.Cancelled, decision.Kind, $"{unsafeState.Reason} kind");
            Equal(unsafeState.Reason, decision.Reason, $"{unsafeState.Reason} reason");
            False(engine.Pending.HasValue, $"{unsafeState.Reason} clears while dispatch is paused");
        }
    }

    internal static void DefaultWindowExpiresAtItsExactDeadline()
    {
        var engine = Armed();
        var beforeDeadline = engine.Evaluate(
            new SmartActionBufferContext(Safe(), ActionIsExecutable: false),
            nowMilliseconds: 1_999);
        Equal(SmartActionBufferDecisionKind.None, beforeDeadline.Kind, "still live before deadline");

        var atDeadline = engine.Evaluate(
            new SmartActionBufferContext(
                Safe(),
                ActionIsExecutable: true,
                InternalPriorityClaimed: true),
            nowMilliseconds: 2_000);
        Equal(SmartActionBufferDecisionKind.Expired, atDeadline.Kind, "expiry wins at deadline");
        Equal(SmartActionBufferCancelReason.Expired, atDeadline.Reason, "expiry reason");
        False(engine.Pending.HasValue, "expired intent clears even while priority owns dispatch");
    }

    internal static void ConcurrentEvaluationDispatchesExactlyOnce()
    {
        const int ContenderCount = 24;
        var engine = Armed();
        var context = new SmartActionBufferContext(Safe(), ActionIsExecutable: true);
        using var start = new ManualResetEventSlim(initialState: false);
        var contenders = Enumerable.Range(0, ContenderCount)
            .Select(_ => Task.Run(() =>
            {
                start.Wait();
                return engine.Evaluate(context, nowMilliseconds: 1_001);
            }))
            .ToArray();

        start.Set();
        Task.WaitAll(contenders);

        var decisions = contenders.Select(task => task.Result).ToArray();
        Equal(
            1,
            decisions.Count(decision => decision.Kind == SmartActionBufferDecisionKind.Dispatch),
            "one dispatch");
        Equal(
            ContenderCount - 1,
            decisions.Count(decision => decision.Kind == SmartActionBufferDecisionKind.None),
            "all later evaluations are empty");
        False(engine.Pending.HasValue, "one-shot clears before returning");
    }

    private static SmartActionBufferEngine Armed(
        int holdMilliseconds = SmartActionBufferWindowRules.DefaultMilliseconds)
    {
        var engine = new SmartActionBufferEngine();
        True(engine.Arm(Intent(), 1_000, holdMilliseconds), "arm");
        return engine;
    }

    private static SmartActionBufferIntent Intent(
        SmartActionBufferFailure failure = SmartActionBufferFailure.GlobalCooldown,
        bool isEligible = true) =>
        new(BaseAction, failure, isEligible);

    private static SmartActionBufferSafety Safe() =>
        SmartActionBufferSafety.SafeFor(BaseAction);

    private static void True(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException($"Expected true: {label}");
    }

    private static void False(bool condition, string label) => True(!condition, label);

    private static void Equal<T>(T expected, T actual, string label)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException(
                $"Expected {label} to be {expected}, got {actual}.");
        }
    }
}
