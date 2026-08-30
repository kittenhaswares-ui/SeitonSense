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
    public const long MaximumResultAgeMilliseconds = 30_000;

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
        // The client can briefly raise BetweenAreas on the result boundary
        // before the normal leave boundary becomes available. That flag alone
        // is therefore not authoritative proof that this exact match context
        // has exited. Territory/content drift and the lifecycle callbacks below
        // remain the re-arm boundaries.
        var confirmedContextExit =
            (liveTerritoryId != 0 && liveTerritoryId != state.TerritoryId) ||
            (liveLocalContentId != 0 && liveLocalContentId != state.LocalContentId);

        if (state.Phase == CrystallineConflictInstantLeavePhase.LeaveRequested)
        {
            if (confirmedContextExit)
                return ResetSpentContext(state);

            return Unchanged(
                state,
                CrystallineConflictInstantLeaveDecision.None,
                CrystallineConflictInstantLeaveReason.LeaveReserved);
        }

        if (state.Phase == CrystallineConflictInstantLeavePhase.Cancelled)
        {
            if (confirmedContextExit)
                return ResetSpentContext(state);

            return Unchanged(state, CrystallineConflictInstantLeaveDecision.None, state.Reason);
        }

        if (state.Phase != CrystallineConflictInstantLeavePhase.WaitingForNativeBoundary)
            return Unchanged(state, CrystallineConflictInstantLeaveDecision.None, state.Reason);

        if (!enabled)
            return Cancel(state, CrystallineConflictInstantLeaveReason.FeatureDisabled);
        if (nowMilliseconds > state.ExpiresAtMilliseconds)
            return Cancel(state, CrystallineConflictInstantLeaveReason.ResultExpired);
        if (betweenAreas)
        {
            var waitingForStableContext = state with
            {
                Reason = CrystallineConflictInstantLeaveReason.TransitionStarted,
            };
            return new CrystallineConflictInstantLeaveTransition(
                waitingForStableContext,
                CrystallineConflictInstantLeaveDecision.Waiting,
                CrystallineConflictInstantLeaveReason.TransitionStarted);
        }

        if (!exactLiveContext)
            return Cancel(state, CrystallineConflictInstantLeaveReason.ContextDrift);
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

    /// <summary>
    /// A nonzero territory-change event is authoritative even when framework
    /// updates were paused for the complete loading transition. This closes the
    /// spent result context without trusting ambiguous zero-valued telemetry.
    /// </summary>
    public static CrystallineConflictInstantLeaveTransition ObserveTerritoryChanged(
        CrystallineConflictInstantLeaveState state,
        uint territoryId)
    {
        if (!state.ContextSpent || territoryId == 0 || territoryId == state.TerritoryId)
            return Unchanged(state, CrystallineConflictInstantLeaveDecision.None, state.Reason);

        return ResetSpentContext(state);
    }

    /// <summary>
    /// Starting a public CC duty proves that any previously spent result belongs
    /// to an older match. This rearms same-map consecutive matches even if the
    /// client emitted no observable framework frame during either zone change.
    /// </summary>
    public static CrystallineConflictInstantLeaveTransition ObserveDutyStarted(
        CrystallineConflictInstantLeaveState state,
        bool liveIsPvpExcludingWolvesDen,
        uint liveTerritoryId,
        ulong liveLocalContentId)
    {
        if (!state.ContextSpent ||
            !liveIsPvpExcludingWolvesDen ||
            !PvPMatchRules.IsPublicCrystallineConflictTerritory(liveTerritoryId) ||
            liveLocalContentId == 0)
        {
            return Unchanged(state, CrystallineConflictInstantLeaveDecision.None, state.Reason);
        }

        return ResetSpentContext(state);
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

    private static CrystallineConflictInstantLeaveTransition ResetSpentContext(
        CrystallineConflictInstantLeaveState state)
    {
        var requested = state.Phase == CrystallineConflictInstantLeavePhase.LeaveRequested;
        return new CrystallineConflictInstantLeaveTransition(
            CrystallineConflictInstantLeaveState.Idle,
            requested
                ? CrystallineConflictInstantLeaveDecision.ExitConfirmed
                : CrystallineConflictInstantLeaveDecision.ContextReset,
            requested
                ? CrystallineConflictInstantLeaveReason.ExitConfirmed
                : CrystallineConflictInstantLeaveReason.ContextReset);
    }

    private static CrystallineConflictInstantLeaveTransition Unchanged(
        CrystallineConflictInstantLeaveState state,
        CrystallineConflictInstantLeaveDecision decision,
        CrystallineConflictInstantLeaveReason reason) =>
        new(state, decision, reason);
}
