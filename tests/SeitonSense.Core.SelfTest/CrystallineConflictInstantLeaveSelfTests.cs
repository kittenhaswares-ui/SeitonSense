using SeitonSense.Core;

internal static class CrystallineConflictInstantLeaveSelfTests
{
    public static void ExactResultReservesExactlyOneLeaveRequest()
    {
        var armed = Arm();
        Equal(CrystallineConflictInstantLeaveDecision.Armed, armed.Decision, "exact result arms");
        var request = Evaluate(armed.State, canLeave: true);
        Equal(CrystallineConflictInstantLeaveDecision.RequestLeave, request.Decision, "ready boundary requests");
        Equal(CrystallineConflictInstantLeavePhase.LeaveRequested, request.State.Phase, "reserved before call");

        var second = Evaluate(request.State, canLeave: true);
        Equal(CrystallineConflictInstantLeaveDecision.None, second.Decision, "void request is never retried");

        var ambiguous = Evaluate(
            request.State,
            canLeave: false,
            livePvp: false,
            territory: 0,
            contentId: 0);
        Equal(CrystallineConflictInstantLeavePhase.LeaveRequested, ambiguous.State.Phase, "unknown telemetry preserves request latch");
        True(ambiguous.State.ContextSpent, "unknown telemetry cannot reopen the match context");
        var duplicate = CrystallineConflictInstantLeaveRules.ObserveConfirmedResult(
            ambiguous.State,
            true,
            true,
            true,
            1293,
            1003,
            1_101,
            1_101);
        Equal(CrystallineConflictInstantLeaveDecision.DuplicateIgnored, duplicate.Decision, "unknown telemetry cannot permit a second request");
    }

    public static void NativeNotReadyWaitsWithoutSpendingTheResult()
    {
        var armed = Arm();
        var waiting = Evaluate(armed.State, canLeave: false);
        Equal(CrystallineConflictInstantLeaveDecision.Waiting, waiting.Decision, "not ready waits");
        Equal(CrystallineConflictInstantLeavePhase.WaitingForNativeBoundary, waiting.State.Phase, "wait retains intent");

        var ready = Evaluate(waiting.State, canLeave: true, now: 1_250);
        Equal(CrystallineConflictInstantLeaveDecision.RequestLeave, ready.Decision, "later ready frame requests");
    }

    public static void DuplicateResultCannotRearmTheSameContext()
    {
        var armed = Arm();
        var duplicate = CrystallineConflictInstantLeaveRules.ObserveConfirmedResult(
            armed.State,
            true,
            true,
            true,
            1293,
            1003,
            1_100,
            1_100);
        Equal(CrystallineConflictInstantLeaveDecision.DuplicateIgnored, duplicate.Decision, "duplicate ignored");
        Equal(armed.State, duplicate.State, "duplicate cannot replace frozen identity");
    }

    public static void InvalidResultBoundariesFailClosed()
    {
        var idle = CrystallineConflictInstantLeaveState.Idle;
        foreach (var sample in new (bool Enabled, bool Exact, bool Pvp, uint Territory,
                     ulong Content, long Captured, long Now)[]
                 {
                     (Enabled: false, Exact: true, Pvp: true, Territory: 1293u, Content: 1003ul, Captured: 1_000L, Now: 1_000L),
                     (true, false, true, 1293u, 1003ul, 1_000L, 1_000L),
                     (true, true, false, 1293u, 1003ul, 1_000L, 1_000L),
                     (true, true, true, 1294u, 1003ul, 1_000L, 1_000L),
                     (true, true, true, 250u, 1003ul, 1_000L, 1_000L),
                     (true, true, true, 1293u, 0ul, 1_000L, 1_000L),
                     (true, true, true, 1293u, 1003ul, 1_001L, 1_000L),
                     (true, true, true, 1293u, 1003ul, 1_000L, 11_001L),
                 })
        {
            var result = CrystallineConflictInstantLeaveRules.ObserveConfirmedResult(
                idle,
                sample.Enabled,
                sample.Exact,
                sample.Pvp,
                sample.Territory,
                sample.Content,
                sample.Captured,
                sample.Now);
            Equal(CrystallineConflictInstantLeaveDecision.None, result.Decision, "invalid capture stays idle");
            False(result.State.ContextSpent, "invalid capture cannot spend context");
        }
    }

    public static void EveryLiveSafetyDriftCancelsBeforeNativeRequest()
    {
        var state = Arm().State;
        var disabled = Evaluate(state, canLeave: true, enabled: false);
        Equal(CrystallineConflictInstantLeaveReason.FeatureDisabled, disabled.Reason, "toggle off");
        var pvpDrift = Evaluate(state, canLeave: true, livePvp: false);
        Equal(CrystallineConflictInstantLeaveReason.ContextDrift, pvpDrift.Reason, "PvP drift");
        var territoryDrift = Evaluate(state, canLeave: true, territory: 1032);
        Equal(CrystallineConflictInstantLeaveReason.ContextDrift, territoryDrift.Reason, "territory drift");
        var identityDrift = Evaluate(state, canLeave: true, contentId: 2003);
        Equal(CrystallineConflictInstantLeaveReason.ContextDrift, identityDrift.Reason, "identity drift");
        var transition = Evaluate(state, canLeave: true, betweenAreas: true);
        Equal(CrystallineConflictInstantLeaveReason.TransitionStarted, transition.Reason, "existing transition");
    }

    public static void NativeUnavailableFaultAndExpiryAreTerminal()
    {
        var state = Arm().State;
        var unavailable = Evaluate(state, canLeave: true, nativeAvailable: false);
        Equal(CrystallineConflictInstantLeavePhase.Cancelled, unavailable.State.Phase, "unavailable cancels");
        var expired = Evaluate(state, canLeave: true, now: 11_001);
        Equal(CrystallineConflictInstantLeaveReason.ResultExpired, expired.Reason, "expired result cancels");

        var reserved = Evaluate(state, canLeave: true).State;
        var faulted = CrystallineConflictInstantLeaveRules.MarkNativeCallFailed(reserved);
        Equal(CrystallineConflictInstantLeavePhase.Cancelled, faulted.Phase, "native fault is terminal");
        var noRetry = Evaluate(faulted, canLeave: true);
        Equal(CrystallineConflictInstantLeaveDecision.None, noRetry.Decision, "fault never retries");
    }

    public static void ContextExitConfirmsAndRearmsOnlyANewMatch()
    {
        var requested = Evaluate(Arm().State, canLeave: true).State;
        var confirmed = Evaluate(requested, canLeave: false, livePvp: false, betweenAreas: true);
        Equal(CrystallineConflictInstantLeaveDecision.ExitConfirmed, confirmed.Decision, "context exit confirms");
        False(confirmed.State.ContextSpent, "exit resets context latch");

        var next = CrystallineConflictInstantLeaveRules.ObserveConfirmedResult(
            confirmed.State,
            true,
            true,
            true,
            1032,
            1003,
            2_000,
            2_000);
        Equal(CrystallineConflictInstantLeaveDecision.Armed, next.Decision, "new public match can arm");
    }

    public static void ResultObservationIsIndependentFromMapStatistics()
    {
        False(CrystallineConflictInstantLeaveRules.ShouldObserveResult(false, true, true), "global off");
        False(CrystallineConflictInstantLeaveRules.ShouldObserveResult(true, false, false), "both consumers off");
        True(CrystallineConflictInstantLeaveRules.ShouldObserveResult(true, true, false), "stats alone observes");
        True(CrystallineConflictInstantLeaveRules.ShouldObserveResult(true, false, true), "leave alone observes");
        True(CrystallineConflictInstantLeaveRules.ShouldObserveResult(true, true, true), "both consumers observe once");
    }

    private static CrystallineConflictInstantLeaveTransition Arm() =>
        CrystallineConflictInstantLeaveRules.ObserveConfirmedResult(
            CrystallineConflictInstantLeaveState.Idle,
            true,
            true,
            true,
            1293,
            1003,
            1_000,
            1_000);

    private static CrystallineConflictInstantLeaveTransition Evaluate(
        CrystallineConflictInstantLeaveState state,
        bool canLeave,
        bool enabled = true,
        bool livePvp = true,
        uint territory = 1293,
        ulong contentId = 1003,
        bool betweenAreas = false,
        bool nativeAvailable = true,
        long now = 1_100) =>
        CrystallineConflictInstantLeaveRules.Evaluate(
            state,
            enabled,
            livePvp,
            territory,
            contentId,
            betweenAreas,
            nativeAvailable,
            canLeave,
            now);

    private static void True(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static void False(bool value, string message) => True(!value, message);

    private static void Equal<T>(T expected, T actual, string message)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
    }
}
