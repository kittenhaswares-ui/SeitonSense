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
        var valid = ArmInput();
        var engine = new HeldChaseBufferEngine();
        True(engine.Arm(valid), "range-only failure arms");
        Equal(Intent, engine.Pending.GetValueOrDefault(), "frozen intent");

        var rejected = new (HeldChaseBufferArmInput Input, HeldChaseBufferCancelReason Reason)[]
        {
            (valid with { Enabled = false }, HeldChaseBufferCancelReason.Disabled),
            (valid with { IsCertifiedPhysicalStandardHotbarRoot = false }, HeldChaseBufferCancelReason.NotPhysicalStandardHotbar),
            (valid with { InputHeld = false }, HeldChaseBufferCancelReason.Released),
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
        var live = LiveInput();
        var cancellations = new (HeldChaseBufferLiveInput Input, HeldChaseBufferCancelReason Reason)[]
        {
            (live with { InputHeld = false }, HeldChaseBufferCancelReason.Released),
            (live with { IsExactPhysicalStandardHotbarHold = false }, HeldChaseBufferCancelReason.NotPhysicalStandardHotbar),
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
        var waiting = engine.Evaluate(LiveInput());
        Equal(HeldChaseBufferDecisionKind.WaitingForRange, waiting.Kind, "waiting kind");
        Equal(Intent, waiting.Intent.GetValueOrDefault(), "waiting preserves exact intent");
        True(engine.Pending.HasValue, "waiting stays armed");

        var reachable = engine.Evaluate(LiveInput() with { HasRangeAndLineOfSight = true });
        Equal(HeldChaseBufferDecisionKind.Dispatch, reachable.Kind, "first reachable edge");
        Equal(Intent, reachable.Intent.GetValueOrDefault(), "dispatched exact intent");
        Equal(HeldChaseBufferCancelReason.Dispatched, reachable.Reason, "terminal reason");
        False(engine.Pending.HasValue, "intent consumed before dispatch return");

        var duplicate = engine.Evaluate(LiveInput() with { HasRangeAndLineOfSight = true });
        Equal(HeldChaseBufferDecisionKind.None, duplicate.Kind, "later reachable observation is empty");
        False(duplicate.Intent.HasValue, "no duplicate intent");

        const int ContenderCount = 24;
        var concurrent = Armed();
        using var start = new ManualResetEventSlim(initialState: false);
        var contenders = Enumerable.Range(0, ContenderCount)
            .Select(_ => Task.Run(() =>
            {
                start.Wait();
                return concurrent.Evaluate(
                    LiveInput() with { HasRangeAndLineOfSight = true });
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
                task.Result.Kind == HeldChaseBufferDecisionKind.None),
            "all concurrent followers are empty");
    }

    private static HeldChaseBufferEngine Armed()
    {
        var engine = new HeldChaseBufferEngine();
        True(engine.Arm(ArmInput()), "arm");
        return engine;
    }

    private static HeldChaseBufferArmInput ArmInput() => new(
        Intent,
        Enabled: true,
        IsCertifiedPhysicalStandardHotbarRoot: true,
        InputHeld: true,
        ActionEligible: true,
        SafetyValid: true,
        RangeProbeAvailable: true,
        HasRangeAndLineOfSight: false,
        OtherNativeGatesReady: true);

    private static HeldChaseBufferLiveInput LiveInput() => new(
        Enabled: true,
        IsExactPhysicalStandardHotbarHold: true,
        InputHeld: true,
        Intent.PressGeneration,
        Intent.RequestedActionId,
        Intent.ResolvedActionId,
        Intent.TargetFingerprint,
        Intent.TerritoryId,
        Intent.InstanceFingerprint,
        ActionEligible: true,
        SafetyValid: true,
        RangeProbeAvailable: true,
        HasRangeAndLineOfSight: false,
        OtherNativeGatesReady: true);

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
