using SeitonSense.Core;
using SeitonSense.Plugin.Services;

internal static class IntegratedBufferSafetySelfTests
{
    public static void TimingReservationUsesActualRuntimeSafetyMapping()
    {
        foreach (var snapshot in SupportedSnapshots())
        {
            Require(IntegratedActionBufferRuntime.IsSafeBufferContext(snapshot, enabled: true),
                "an exact live player and target are safe independently of CC, Den, or PvE");
            var action = new SmartActionBufferAction(1001, snapshot.ResolvedActionId,
                snapshot.Target.Fingerprint, snapshot.TerritoryId, snapshot.InstanceFingerprint);
            var engine = new SmartActionBufferEngine();
            Require(engine.Arm(new(action, SmartActionBufferFailure.AnimationLock, true), 1000, 1000),
                "one exact early press arms");
            var safety = IntegratedActionBufferRuntime.CreateCoreSafety(1001, snapshot, enabled: true);
            Require(!safety.IsKnockbackActive,
                "the false native BeingMoved fixture maps to the existing false cancellation input");
            Require(engine.Evaluate(new(safety, ActionIsExecutable: false), 1100).Kind ==
                    SmartActionBufferDecisionKind.None && engine.Pending.HasValue,
                "the early press survives while the native timing gate is still closed");
            Require(engine.Evaluate(new(safety, ActionIsExecutable: true,
                    InternalPriorityClaimed: true), 1150).Kind == SmartActionBufferDecisionKind.None,
                "critical recovery still pauses dispatch without consuming the intent");
            Require(engine.Evaluate(new(safety, ActionIsExecutable: true), 1200).Kind ==
                    SmartActionBufferDecisionKind.Dispatch && !engine.Pending.HasValue,
                "the same action dispatches once when native timing and critical priority allow it");
            Require(engine.Evaluate(new(safety, ActionIsExecutable: true), 1201).Kind ==
                    SmartActionBufferDecisionKind.None,
                "no duplicate dispatch follows");
        }
    }

    public static void ChaseReservationKeepsExactIntentWhileClosingRange()
    {
        foreach (var snapshot in SupportedSnapshots())
        {
            var safe = IntegratedActionBufferRuntime.IsSafeBufferContext(snapshot, enabled: true);
            var intent = new HeldChaseBufferIntent(1001, snapshot.ResolvedActionId,
                snapshot.Target.Fingerprint, snapshot.TerritoryId, snapshot.InstanceFingerprint, 7);
            var engine = new HeldChaseBufferEngine();
            Require(engine.Arm(new(intent, Enabled: true,
                    IsCertifiedPhysicalStandardHotbarRoot: true, ActionEligible: true,
                    SafetyValid: safe, RangeProbeAvailable: true, HasRangeAndLineOfSight: false,
                    OtherNativeGatesReady: true)), "range-only miss arms for the actual runtime context");
            engine.Cancel(HeldChaseBufferCancelReason.Released);
            var live = new HeldChaseBufferLiveInput(true, 7, 1001, snapshot.ResolvedActionId,
                snapshot.Target.Fingerprint, snapshot.TerritoryId, snapshot.InstanceFingerprint,
                ActionEligible: true, SafetyValid: safe, RangeProbeAvailable: true,
                HasRangeAndLineOfSight: false, OtherNativeGatesReady: true,
                WithinDeadline: true, NowMilliseconds: 1100);
            Require(engine.Evaluate(live).Kind == HeldChaseBufferDecisionKind.WaitingForRange,
                "releasing the key while closing distance retains the exact action and target");
            Require(engine.Evaluate(live with { HasRangeAndLineOfSight = true }).Kind ==
                    HeldChaseBufferDecisionKind.Dispatch, "native range becoming ready permits one attempt");
            Require(engine.CompleteNativeAttempt(intent, 1101,
                    ClientActionAttemptOutcome.ClientAccepted).IsTerminal && !engine.Pending.HasValue,
                "accepted chase completes without another attempt");
        }
    }

    public static void GuardCrowdControlAndContextFailuresRemainBlocked()
    {
        var safe = Snapshot();
        foreach (var blocked in new[]
                 {
                     safe with { LoggedIn = false }, safe with { BetweenAreas = true },
                     safe with { Local = default }, safe with { IsAlive = false },
                     safe with { IsMounted = true }, safe with { IsStunned = true },
                     safe with { IsBeingMoved = true },
                     safe with { HasActionBlockingCrowdControl = true }, safe with { HasOwnGuard = true },
                     safe with { Target = safe.Target with { ExplicitTarget = default } },
                 })
        {
            Require(!IntegratedActionBufferRuntime.IsSafeBufferContext(blocked, enabled: true),
                "the managed seam preserves every existing native condition and status veto");
        }
        Require(!IntegratedActionBufferRuntime.IsSafeBufferContext(safe, enabled: false),
            "disable and disposed-runtime gates remain effective");
        var action = new SmartActionBufferAction(1001, safe.ResolvedActionId,
            safe.Target.Fingerprint, safe.TerritoryId, safe.InstanceFingerprint);
        var engine = new SmartActionBufferEngine();
        Require(engine.Arm(new(action, SmartActionBufferFailure.AnimationLock, true), 1000), "arm fixture");
        var stunned = IntegratedActionBufferRuntime.CreateCoreSafety(1001,
            safe with { IsStunned = true }, enabled: true);
        Require(engine.Evaluate(new(stunned, ActionIsExecutable: true), 1001).Reason ==
                SmartActionBufferCancelReason.Stun && !engine.Pending.HasValue,
            "the production mapper still cancels an exact pending action immediately on actual stun");
        Require(engine.Arm(new(action, SmartActionBufferFailure.AnimationLock, true), 2000), "rearm fixture");
        var beingMoved = IntegratedActionBufferRuntime.CreateCoreSafety(1001,
            safe with { IsBeingMoved = true }, enabled: true);
        Require(beingMoved.IsKnockbackActive &&
                engine.Evaluate(new(beingMoved, ActionIsExecutable: true), 2001).Reason ==
                SmartActionBufferCancelReason.Knockback && !engine.Pending.HasValue,
            "the explicit BeingMoved flag retains the existing immediate Core cancellation");
    }

    private static IEnumerable<IntegratedActionBufferRuntime.RuntimeSnapshot> SupportedSnapshots()
    {
        yield return Snapshot();
        yield return Snapshot() with { TerritoryId = 250, MapId = 1, Instance = 0 };
        yield return Snapshot() with { TerritoryId = 100, IsPvP = false };
    }

    private static IntegratedActionBufferRuntime.RuntimeSnapshot Snapshot() => new(
        LoggedIn: true, BetweenAreas: false, TerritoryId: 1032, MapId: 3, Instance: 1,
        JobId: 34, IsPvP: true, InstanceFingerprint: 5,
        Local: new(100, 10, (nint)100),
        Target: default(IntegratedActionBufferTargetSnapshot) with
        {
            RawTargetId = 200,
            ExplicitTarget = new(200, 20, (nint)200),
            Fingerprint = 2000,
        },
        ResolvedActionId: 1001, IsAlive: true, IsMounted: false, IsStunned: false,
        HasActionBlockingCrowdControl: false, HasOwnGuard: false, IsBeingMoved: false);

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
