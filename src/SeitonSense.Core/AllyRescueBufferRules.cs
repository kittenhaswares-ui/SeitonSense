using System.Collections.Immutable;

namespace SeitonSense.Core;

public enum AllyRescueBufferPhase
{
    WaitingForCandidate = 0,
    WaitingForFreshKey = 1,
    Buffered = 2,
}

public enum AllyRescueBufferDecisionKind
{
    None = 0,
    CandidateObserved = 1,
    Armed = 2,
    Dispatch = 3,
    Cancelled = 4,
}

public enum AllyRescueInputTrigger
{
    None = 0,
    FreshKeyPress = 1,
    HeldKeyAtCandidateEntry = 2,
}

public enum AllyRescueBufferCancelReason
{
    None = 0,
    CandidateGone = 1,
    CandidateChanged = 2,
    ConfigurationDisabled = 3,
    OutsideSupportedPvPContext = 4,
    LocalPlayerDead = 5,
    LocalPlayerIdentityInvalid = 6,
    TextInputActive = 7,
    TimedOut = 8,
    HardReset = 9,
    InvalidClock = 10,
    HeldKeyReleased = 11,
}

public enum AllyRescueNativeAttemptOutcome
{
    None = 0,
    RetryScheduled = 1,
    AcceptedTerminal = 2,
    RejectedTerminal = 3,
    AmbiguousTerminal = 4,
    Cancelled = 5,
    SoftWait = 6,
}

public readonly record struct AllyRescueBufferState(
    AllyRescueBufferPhase Phase,
    AllyRescueIntent? TrackedIntent,
    long CandidateObservedAtMilliseconds,
    long ArmedAtMilliseconds,
    long ExpiresAtMilliseconds,
    long LastObservedAtMilliseconds,
    ImmutableArray<AllyRescueIntent> SpentIntents,
    int GameplayKeyToken,
    int NativeAttemptCount,
    long NextNativeAttemptAtMilliseconds)
{
    public static AllyRescueBufferState Initial => new(
        AllyRescueBufferPhase.WaitingForCandidate,
        null,
        -1,
        -1,
        -1,
        -1,
        ImmutableArray<AllyRescueIntent>.Empty,
        0,
        0,
        -1);

    public bool HasSpent(AllyRescueIntent intent) =>
        !SpentIntents.IsDefaultOrEmpty && SpentIntents.Contains(intent);
}

public readonly record struct AllyRescueBufferObservation(
    bool ConfigurationEnabled,
    bool IsSupportedPvPContext,
    bool IsLocalPlayerAlive,
    bool IsLocalPlayerIdentityValid,
    bool IsTextInputActive,
    IReadOnlyList<AllyRescueSelectionCandidate>? Candidates,
    bool FreshKeyPressed,
    bool HeldKeyEligible,
    bool AllowHeldKeyAtCandidateEntry,
    bool ActionLocallyReady,
    long NowMilliseconds,
    bool HardReset = false,
    long BufferMilliseconds = AllyRescueBufferRules.DefaultBufferMilliseconds,
    int FreshGameplayKeyToken = 0,
    int HeldGameplayKeyToken = 0,
    bool TrackedGameplayKeyPhysicallyDown = false,
    bool DispatchAllowed = true);

public readonly record struct AllyRescueBufferDecision(
    AllyRescueBufferState NextState,
    AllyRescueBufferDecisionKind Kind,
    AllyRescueBufferCancelReason CancelReason,
    int SelectedCandidateIndex = -1,
    AllyRescueInputTrigger InputTrigger = AllyRescueInputTrigger.None)
{
    public bool ShouldDispatch => Kind == AllyRescueBufferDecisionKind.Dispatch;

    public bool ShouldConsumeInputGeneration =>
        Kind == AllyRescueBufferDecisionKind.Dispatch ||
        (InputTrigger != AllyRescueInputTrigger.None &&
         Kind == AllyRescueBufferDecisionKind.Armed);

    public AllyRescueIntent? DispatchIntent => ShouldDispatch
        ? NextState.TrackedIntent
        : null;
}

public readonly record struct AllyRescueNativeAttemptDecision(
    AllyRescueBufferState NextState,
    AllyRescueNativeAttemptOutcome Outcome)
{
    public bool IsTerminal => Outcome is
        AllyRescueNativeAttemptOutcome.AcceptedTerminal or
        AllyRescueNativeAttemptOutcome.RejectedTerminal or
        AllyRescueNativeAttemptOutcome.AmbiguousTerminal or
        AllyRescueNativeAttemptOutcome.Cancelled;
}

/// <summary>
/// Freezes one exact actor/status/key lease for the lifetime of that status and
/// physical hold. Structural/range/queue waits have no wall-clock timeout and
/// consume no retry budget. A proven native false may retry only this lease.
/// </summary>
public static class AllyRescueBufferRules
{
    public const long StatusBoundBufferMilliseconds = -1;
    public const long DefaultBufferMilliseconds = StatusBoundBufferMilliseconds;
    public const long NativeRetryThrottleMilliseconds =
        HeldActionRetryRules.NativeRetryThrottleMilliseconds;
    public const int MaximumNativeAttempts = HeldActionRetryRules.MaximumNativeAttempts;

    public static AllyRescueBufferDecision Observe(
        AllyRescueBufferState previous,
        AllyRescueBufferObservation observation)
    {
        previous = Normalize(previous);

        if (observation.HardReset)
        {
            return Cancelled(
                AllyRescueBufferState.Initial,
                AllyRescueBufferCancelReason.HardReset);
        }

        if (observation.NowMilliseconds < 0 ||
            (previous.LastObservedAtMilliseconds >= 0 &&
             observation.NowMilliseconds < previous.LastObservedAtMilliseconds))
        {
            return Cancelled(
                StopTracking(previous, observation.NowMilliseconds),
                AllyRescueBufferCancelReason.InvalidClock);
        }

        var gateFailure = GetGateFailure(observation);
        if (gateFailure != AllyRescueBufferCancelReason.None)
        {
            return Cancelled(
                StopTracking(previous, observation.NowMilliseconds),
                gateFailure);
        }

        if (previous.Phase == AllyRescueBufferPhase.Buffered &&
            previous.TrackedIntent is { } leasedIntent)
        {
            var leasedState = previous with
            {
                LastObservedAtMilliseconds = observation.NowMilliseconds,
            };
            if (leasedState.GameplayKeyToken <= 0 ||
                !observation.TrackedGameplayKeyPhysicallyDown)
            {
                return Cancelled(
                    StopTracking(leasedState, observation.NowMilliseconds),
                    AllyRescueBufferCancelReason.HeldKeyReleased);
            }

            var leasedIndex = FindExactCandidateIndex(observation.Candidates, leasedIntent);
            if (leasedIndex < 0)
            {
                return Cancelled(
                    StopTracking(leasedState, observation.NowMilliseconds),
                    AllyRescueBufferCancelReason.CandidateGone);
            }

            if (!AllyRescueSelectionRules.IsEligible(observation.Candidates![leasedIndex]) ||
                !observation.DispatchAllowed ||
                !observation.ActionLocallyReady ||
                observation.NowMilliseconds < leasedState.NextNativeAttemptAtMilliseconds)
            {
                return NoDecision(leasedState, leasedIndex);
            }

            return Dispatch(leasedState, observation.NowMilliseconds, leasedIndex);
        }

        var spent = previous.SpentIntents.ToHashSet();
        var selectedIndex = AllyRescueSelectionRules.SelectBestIndex(
            observation.Candidates,
            spent);
        if (selectedIndex < 0)
        {
            var stopped = StopTracking(previous, observation.NowMilliseconds);
            return previous.TrackedIntent is null
                ? NoDecision(stopped)
                : Cancelled(stopped, AllyRescueBufferCancelReason.CandidateGone);
        }

        var selected = observation.Candidates![selectedIndex];
        var intent = selected.Intent;
        if (previous.TrackedIntent != intent)
        {
            var entry = WaitingForFreshKey(
                previous,
                intent,
                observation.NowMilliseconds);
            var trigger = ResolveCandidateEntryTrigger(observation);
            if (trigger != AllyRescueInputTrigger.None)
                return ArmOrDispatch(entry, observation, selectedIndex, trigger);

            return new AllyRescueBufferDecision(
                entry,
                previous.TrackedIntent is null
                    ? AllyRescueBufferDecisionKind.CandidateObserved
                    : AllyRescueBufferDecisionKind.Cancelled,
                previous.TrackedIntent is null
                    ? AllyRescueBufferCancelReason.None
                    : AllyRescueBufferCancelReason.CandidateChanged,
                selectedIndex);
        }

        var current = previous with
        {
            LastObservedAtMilliseconds = observation.NowMilliseconds,
        };
        // A held level is intentionally ignored after the exact candidate-entry
        // observation. Only a real later down-edge may create a new intent.
        if (!observation.FreshKeyPressed)
            return NoDecision(current, selectedIndex);

        return ArmOrDispatch(
            current,
            observation,
            selectedIndex,
            AllyRescueInputTrigger.FreshKeyPress);
    }

    public static long NormalizeBufferMilliseconds(long requestedMilliseconds) =>
        StatusBoundBufferMilliseconds;

    public static AllyRescueNativeAttemptDecision CompleteNativeAttempt(
        AllyRescueBufferState previous,
        AllyRescueIntent intent,
        long nowMilliseconds,
        ClientActionAttemptOutcome outcome)
    {
        previous = Normalize(previous);
        if (previous.Phase != AllyRescueBufferPhase.Buffered ||
            previous.TrackedIntent != intent ||
            nowMilliseconds < previous.LastObservedAtMilliseconds)
        {
            return new AllyRescueNativeAttemptDecision(
                StopTracking(previous, nowMilliseconds),
                AllyRescueNativeAttemptOutcome.Cancelled);
        }

        if (outcome == ClientActionAttemptOutcome.AcceptanceUnknown)
        {
            return new AllyRescueNativeAttemptDecision(
                Finish(previous, intent, nowMilliseconds),
                AllyRescueNativeAttemptOutcome.AmbiguousTerminal);
        }

        if (outcome == ClientActionAttemptOutcome.ClientAccepted)
        {
            return new AllyRescueNativeAttemptDecision(
                Finish(previous, intent, nowMilliseconds),
                AllyRescueNativeAttemptOutcome.AcceptedTerminal);
        }

        var shared = HeldActionRetryRules.Complete(
            new HeldActionRetryState(
                previous.NativeAttemptCount,
                previous.NextNativeAttemptAtMilliseconds),
            nowMilliseconds,
            outcome);
        if (shared.Disposition == HeldActionRetryDisposition.SoftWait)
        {
            return new AllyRescueNativeAttemptDecision(
                previous with { LastObservedAtMilliseconds = nowMilliseconds },
                AllyRescueNativeAttemptOutcome.SoftWait);
        }

        if (shared.Disposition == HeldActionRetryDisposition.CancelledTerminal)
        {
            return new AllyRescueNativeAttemptDecision(
                Finish(previous, intent, nowMilliseconds),
                AllyRescueNativeAttemptOutcome.Cancelled);
        }

        if (!shared.RetryScheduled)
        {
            return new AllyRescueNativeAttemptDecision(
                Finish(previous, intent, nowMilliseconds),
                AllyRescueNativeAttemptOutcome.RejectedTerminal);
        }

        return new AllyRescueNativeAttemptDecision(
            previous with
            {
                NativeAttemptCount = shared.NextState.NativeAttemptCount,
                NextNativeAttemptAtMilliseconds =
                    shared.NextState.NextNativeAttemptAtMilliseconds,
                LastObservedAtMilliseconds = nowMilliseconds,
            },
            AllyRescueNativeAttemptOutcome.RetryScheduled);
    }

    public static AllyRescueBufferState CancelNativeAttempt(
        AllyRescueBufferState previous,
        AllyRescueIntent intent,
        long nowMilliseconds) =>
        previous.Phase == AllyRescueBufferPhase.Buffered &&
        previous.TrackedIntent == intent
            ? Finish(previous, intent, nowMilliseconds)
            : previous;

    private static AllyRescueBufferCancelReason GetGateFailure(
        AllyRescueBufferObservation observation)
    {
        if (!observation.ConfigurationEnabled)
            return AllyRescueBufferCancelReason.ConfigurationDisabled;
        if (!observation.IsSupportedPvPContext)
            return AllyRescueBufferCancelReason.OutsideSupportedPvPContext;
        if (!observation.IsLocalPlayerAlive)
            return AllyRescueBufferCancelReason.LocalPlayerDead;
        if (!observation.IsLocalPlayerIdentityValid)
            return AllyRescueBufferCancelReason.LocalPlayerIdentityInvalid;
        if (observation.IsTextInputActive)
            return AllyRescueBufferCancelReason.TextInputActive;

        return AllyRescueBufferCancelReason.None;
    }

    private static AllyRescueBufferDecision ArmOrDispatch(
        AllyRescueBufferState current,
        AllyRescueBufferObservation observation,
        int selectedIndex,
        AllyRescueInputTrigger trigger)
    {
        var keyToken = trigger switch
        {
            AllyRescueInputTrigger.FreshKeyPress => observation.FreshGameplayKeyToken,
            AllyRescueInputTrigger.HeldKeyAtCandidateEntry => observation.HeldGameplayKeyToken,
            _ => 0,
        };
        if (keyToken <= 0)
            return NoDecision(current, selectedIndex);

        var buffered = current with
        {
            Phase = AllyRescueBufferPhase.Buffered,
            ArmedAtMilliseconds = observation.NowMilliseconds,
            ExpiresAtMilliseconds = StatusBoundBufferMilliseconds,
            LastObservedAtMilliseconds = observation.NowMilliseconds,
            GameplayKeyToken = keyToken,
            NativeAttemptCount = 0,
            NextNativeAttemptAtMilliseconds = observation.NowMilliseconds,
        };

        return observation.DispatchAllowed && observation.ActionLocallyReady
            ? Dispatch(buffered, observation.NowMilliseconds, selectedIndex, trigger)
            : new AllyRescueBufferDecision(
                buffered,
                AllyRescueBufferDecisionKind.Armed,
                AllyRescueBufferCancelReason.None,
                selectedIndex,
                trigger);
    }

    private static AllyRescueBufferDecision Dispatch(
        AllyRescueBufferState current,
        long nowMilliseconds,
        int selectedIndex,
        AllyRescueInputTrigger trigger = AllyRescueInputTrigger.None)
    {
        var attempting = current with
        {
            LastObservedAtMilliseconds = nowMilliseconds,
        };

        return new AllyRescueBufferDecision(
            attempting,
            AllyRescueBufferDecisionKind.Dispatch,
            AllyRescueBufferCancelReason.None,
            selectedIndex,
            trigger);
    }

    private static AllyRescueInputTrigger ResolveCandidateEntryTrigger(
        AllyRescueBufferObservation observation)
    {
        if (observation.AllowHeldKeyAtCandidateEntry &&
            observation.HeldKeyEligible &&
            observation.HeldGameplayKeyToken > 0)
            return AllyRescueInputTrigger.HeldKeyAtCandidateEntry;
        if (observation.FreshKeyPressed && observation.FreshGameplayKeyToken > 0)
            return AllyRescueInputTrigger.FreshKeyPress;

        return AllyRescueInputTrigger.None;
    }

    private static AllyRescueBufferState WaitingForFreshKey(
        AllyRescueBufferState previous,
        AllyRescueIntent intent,
        long nowMilliseconds) =>
        previous with
        {
            Phase = AllyRescueBufferPhase.WaitingForFreshKey,
            TrackedIntent = intent,
            CandidateObservedAtMilliseconds = nowMilliseconds,
            ArmedAtMilliseconds = -1,
            ExpiresAtMilliseconds = -1,
            LastObservedAtMilliseconds = nowMilliseconds,
            GameplayKeyToken = 0,
            NativeAttemptCount = 0,
            NextNativeAttemptAtMilliseconds = -1,
        };

    private static AllyRescueBufferState StopTracking(
        AllyRescueBufferState previous,
        long nowMilliseconds) =>
        previous with
        {
            Phase = AllyRescueBufferPhase.WaitingForCandidate,
            TrackedIntent = null,
            CandidateObservedAtMilliseconds = -1,
            ArmedAtMilliseconds = -1,
            ExpiresAtMilliseconds = -1,
            LastObservedAtMilliseconds = nowMilliseconds,
            GameplayKeyToken = 0,
            NativeAttemptCount = 0,
            NextNativeAttemptAtMilliseconds = -1,
        };

    private static AllyRescueBufferState Finish(
        AllyRescueBufferState previous,
        AllyRescueIntent intent,
        long nowMilliseconds)
    {
        var spent = previous.HasSpent(intent)
            ? previous.SpentIntents
            : previous.SpentIntents.Add(intent);
        return StopTracking(previous with { SpentIntents = spent }, nowMilliseconds);
    }

    private static int FindExactCandidateIndex(
        IReadOnlyList<AllyRescueSelectionCandidate>? candidates,
        AllyRescueIntent intent)
    {
        if (candidates is null) return -1;
        for (var index = 0; index < candidates.Count; index++)
        {
            if (candidates[index].Intent == intent)
                return index;
        }

        return -1;
    }

    private static AllyRescueBufferState Normalize(AllyRescueBufferState state) =>
        state.SpentIntents.IsDefault
            ? state with { SpentIntents = ImmutableArray<AllyRescueIntent>.Empty }
            : state;

    private static AllyRescueBufferDecision NoDecision(
        AllyRescueBufferState state,
        int selectedIndex = -1) =>
        new(
            state,
            AllyRescueBufferDecisionKind.None,
            AllyRescueBufferCancelReason.None,
            selectedIndex);

    private static AllyRescueBufferDecision Cancelled(
        AllyRescueBufferState state,
        AllyRescueBufferCancelReason reason,
        int selectedIndex = -1) =>
        new(
            state,
            AllyRescueBufferDecisionKind.Cancelled,
            reason,
            selectedIndex);

}
