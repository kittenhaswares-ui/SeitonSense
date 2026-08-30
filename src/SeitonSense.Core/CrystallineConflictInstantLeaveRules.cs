namespace SeitonSense.Core;

public enum CrystallineConflictInstantLeavePhase
{
    Idle,
    WaitingForNativeBoundary,
    LeaveRequested,
    Cancelled,
}

public enum CrystallineConflictInstantLeaveDecision
{
    None,
    Armed,
    Waiting,
    RequestLeave,
    Cancelled,
    ExitConfirmed,
    ContextReset,
    DuplicateIgnored,
}

public enum CrystallineConflictInstantLeaveReason
{
    None,
    ExactResultConfirmed,
    FeatureDisabled,
    InvalidResultBoundary,
    DuplicateResult,
    NativeBoundaryNotReady,
    NativeBoundaryUnavailable,
    ContextDrift,
    TransitionStarted,
    ResultExpired,
    LeaveReserved,
    NativeCallFailed,
    ExitConfirmed,
    ContextReset,
}

public readonly record struct CrystallineConflictInstantLeaveState(
    CrystallineConflictInstantLeavePhase Phase,
    CrystallineConflictInstantLeaveReason Reason,
    uint TerritoryId,
    ulong LocalContentId,
    long CapturedAtMilliseconds,
    long ExpiresAtMilliseconds,
    bool ContextSpent)
{
    public static CrystallineConflictInstantLeaveState Idle => new(
        CrystallineConflictInstantLeavePhase.Idle,
        CrystallineConflictInstantLeaveReason.None,
        0,
        0,
        0,
        0,
        false);
}

public readonly record struct CrystallineConflictInstantLeaveTransition(
    CrystallineConflictInstantLeaveState State,
    CrystallineConflictInstantLeaveDecision Decision,
    CrystallineConflictInstantLeaveReason Reason);

/// <summary>
/// Pure one-shot policy for leaving a completed public Crystalline Conflict
/// match. The complete local result packet is the sole arm signal. Native
/// readiness may be polled, but the void leave request is reserved and issued
/// only once.
/// </summary>
public static class CrystallineConflictInstantLeaveRules
{
    public const long MaximumResultAgeMilliseconds = 10_000;

    public static bool ShouldObserveResult(
        bool pluginEnabled,
        bool mapStatisticsEnabled,
        bool instantLeaveEnabled) =>
        pluginEnabled && (mapStatisticsEnabled || instantLeaveEnabled);

    public static CrystallineConflictInstantLeaveTransition ObserveConfirmedResult(
        CrystallineConflictInstantLeaveState state,
        bool enabled,
        bool exactResultConfirmed,
        bool capturedIsPvpExcludingWolvesDen,
        uint capturedTerritoryId,
        ulong capturedLocalContentId,
        long capturedAtMilliseconds,
        long nowMilliseconds)
    {
        nowMilliseconds = Math.Max(0, nowMilliseconds);
        if (!enabled)
        {
            return Unchanged(
                state,
                CrystallineConflictInstantLeaveDecision.None,
                CrystallineConflictInstantLeaveReason.FeatureDisabled);
        }

        if (!exactResultConfirmed ||
            !capturedIsPvpExcludingWolvesDen ||
            !PvPMatchRules.IsPublicCrystallineConflictTerritory(capturedTerritoryId) ||
            capturedLocalContentId == 0 ||
            capturedAtMilliseconds < 0 ||
            capturedAtMilliseconds > long.MaxValue - MaximumResultAgeMilliseconds ||
            capturedAtMilliseconds > nowMilliseconds ||
            nowMilliseconds - capturedAtMilliseconds > MaximumResultAgeMilliseconds)
        {
            return Unchanged(
                state,
                CrystallineConflictInstantLeaveDecision.None,
                CrystallineConflictInstantLeaveReason.InvalidResultBoundary);
        }

        if (state.ContextSpent)
        {
            return Unchanged(
                state,
                CrystallineConflictInstantLeaveDecision.DuplicateIgnored,
                CrystallineConflictInstantLeaveReason.DuplicateResult);
        }

        var armed = new CrystallineConflictInstantLeaveState(
            CrystallineConflictInstantLeavePhase.WaitingForNativeBoundary,
            CrystallineConflictInstantLeaveReason.ExactResultConfirmed,
            capturedTerritoryId,
            capturedLocalContentId,
            capturedAtMilliseconds,
            capturedAtMilliseconds + MaximumResultAgeMilliseconds,
            true);
        return new CrystallineConflictInstantLeaveTransition(
            armed,
            CrystallineConflictInstantLeaveDecision.Armed,
            CrystallineConflictInstantLeaveReason.ExactResultConfirmed);
    }

    public static CrystallineConflictInstantLeaveTransition Evaluate(
        CrystallineConflictInstantLeaveState state,
        bool enabled,
        bool liveIsPvpExcludingWolvesDen,
        uint liveTerritoryId,
        ulong liveLocalContentId,
        bool betweenAreas,
        bool nativeBoundaryAvailable,
        bool canLeaveCurrentContent,
        long nowMilliseconds)
    {
        nowMilliseconds = Math.Max(0, nowMilliseconds);
        if (!state.ContextSpent)
            return Unchanged(state, CrystallineConflictInstantLeaveDecision.None, state.Reason);

        var exactLiveContext =
            liveIsPvpExcludingWolvesDen &&
            PvPMatchRules.IsPublicCrystallineConflictTerritory(liveTerritoryId) &&
            liveTerritoryId == state.TerritoryId &&
            liveLocalContentId != 0 &&
            liveLocalContentId == state.LocalContentId;
        var confirmedContextExit =
            betweenAreas ||
            (liveTerritoryId != 0 && liveTerritoryId != state.TerritoryId) ||
            (liveLocalContentId != 0 && liveLocalContentId != state.LocalContentId);

        if (state.Phase == CrystallineConflictInstantLeavePhase.LeaveRequested)
        {
            if (confirmedContextExit)
            {
                return new CrystallineConflictInstantLeaveTransition(
                    CrystallineConflictInstantLeaveState.Idle,
                    CrystallineConflictInstantLeaveDecision.ExitConfirmed,
                    CrystallineConflictInstantLeaveReason.ExitConfirmed);
            }

            return Unchanged(
                state,
                CrystallineConflictInstantLeaveDecision.None,
                CrystallineConflictInstantLeaveReason.LeaveReserved);
        }

        if (state.Phase == CrystallineConflictInstantLeavePhase.Cancelled)
        {
            if (confirmedContextExit)
            {
                return new CrystallineConflictInstantLeaveTransition(
                    CrystallineConflictInstantLeaveState.Idle,
                    CrystallineConflictInstantLeaveDecision.ContextReset,
                    CrystallineConflictInstantLeaveReason.ContextReset);
            }

            return Unchanged(state, CrystallineConflictInstantLeaveDecision.None, state.Reason);
        }

        if (state.Phase != CrystallineConflictInstantLeavePhase.WaitingForNativeBoundary)
            return Unchanged(state, CrystallineConflictInstantLeaveDecision.None, state.Reason);

        if (!enabled)
            return Cancel(state, CrystallineConflictInstantLeaveReason.FeatureDisabled);
        if (betweenAreas)
            return Cancel(state, CrystallineConflictInstantLeaveReason.TransitionStarted);
        if (!exactLiveContext)
            return Cancel(state, CrystallineConflictInstantLeaveReason.ContextDrift);
        if (nowMilliseconds > state.ExpiresAtMilliseconds)
            return Cancel(state, CrystallineConflictInstantLeaveReason.ResultExpired);
        if (!nativeBoundaryAvailable)
            return Cancel(state, CrystallineConflictInstantLeaveReason.NativeBoundaryUnavailable);
        if (!canLeaveCurrentContent)
        {
            var waiting = state with
            {
                Reason = CrystallineConflictInstantLeaveReason.NativeBoundaryNotReady,
            };
            return new CrystallineConflictInstantLeaveTransition(
                waiting,
                CrystallineConflictInstantLeaveDecision.Waiting,
                CrystallineConflictInstantLeaveReason.NativeBoundaryNotReady);
        }

        // Reserve before the caller crosses the native void boundary. No later
        // frame may issue a second request whose acceptance cannot be proven.
        var reserved = state with
        {
            Phase = CrystallineConflictInstantLeavePhase.LeaveRequested,
            Reason = CrystallineConflictInstantLeaveReason.LeaveReserved,
        };
        return new CrystallineConflictInstantLeaveTransition(
            reserved,
            CrystallineConflictInstantLeaveDecision.RequestLeave,
            CrystallineConflictInstantLeaveReason.LeaveReserved);
    }

    public static CrystallineConflictInstantLeaveState MarkNativeCallFailed(
        CrystallineConflictInstantLeaveState state) =>
        state.Phase == CrystallineConflictInstantLeavePhase.LeaveRequested
            ? state with
            {
                Phase = CrystallineConflictInstantLeavePhase.Cancelled,
                Reason = CrystallineConflictInstantLeaveReason.NativeCallFailed,
            }
            : state;

    private static CrystallineConflictInstantLeaveTransition Cancel(
        CrystallineConflictInstantLeaveState state,
        CrystallineConflictInstantLeaveReason reason) =>
        new(
            state with
            {
                Phase = CrystallineConflictInstantLeavePhase.Cancelled,
                Reason = reason,
            },
            CrystallineConflictInstantLeaveDecision.Cancelled,
            reason);

    private static CrystallineConflictInstantLeaveTransition Unchanged(
        CrystallineConflictInstantLeaveState state,
        CrystallineConflictInstantLeaveDecision decision,
        CrystallineConflictInstantLeaveReason reason) =>
        new(state, decision, reason);
}
