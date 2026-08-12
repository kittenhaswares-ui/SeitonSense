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
}

/// <summary>
/// Gives the self-Purify decision first ownership of the shared physical input
/// before an Ally Rescue decision is observed in the same frame.
/// </summary>
public static class EmergencyActionPriorityRules
{
    public static bool SelfPurifyClaimsPriority(EmergencyPurifyBufferDecision decision) =>
        SelfPurifyClaimsPriority(decision.Kind, decision.InputTrigger);

    public static bool SelfPurifyClaimsPriority(
        EmergencyPurifyBufferDecisionKind kind,
        EmergencyPurifyInputTrigger inputTrigger) =>
        kind == EmergencyPurifyBufferDecisionKind.Dispatch ||
        (kind == EmergencyPurifyBufferDecisionKind.Armed &&
         inputTrigger != EmergencyPurifyInputTrigger.None);

    public static bool AllowAllyRescue(EmergencyPurifyBufferDecision decision) =>
        !SelfPurifyClaimsPriority(decision);
}

public readonly record struct AllyRescueBufferState(
    AllyRescueBufferPhase Phase,
    AllyRescueIntent? TrackedIntent,
    long CandidateObservedAtMilliseconds,
    long ArmedAtMilliseconds,
    long ExpiresAtMilliseconds,
    long LastObservedAtMilliseconds,
    ImmutableArray<AllyRescueIntent> SpentIntents)
{
    public static AllyRescueBufferState Initial => new(
        AllyRescueBufferPhase.WaitingForCandidate,
        null,
        -1,
        -1,
        -1,
        -1,
        ImmutableArray<AllyRescueIntent>.Empty);

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
    long BufferMilliseconds = AllyRescueBufferRules.DefaultBufferMilliseconds);

public readonly record struct AllyRescueBufferDecision(
    AllyRescueBufferState NextState,
    AllyRescueBufferDecisionKind Kind,
    AllyRescueBufferCancelReason CancelReason,
    int SelectedCandidateIndex = -1,
    AllyRescueInputTrigger InputTrigger = AllyRescueInputTrigger.None)
{
    public bool ShouldDispatch => Kind == AllyRescueBufferDecisionKind.Dispatch;

    public bool ShouldConsumeInputGeneration =>
        InputTrigger != AllyRescueInputTrigger.None &&
        Kind is AllyRescueBufferDecisionKind.Armed or AllyRescueBufferDecisionKind.Dispatch;

    public AllyRescueIntent? DispatchIntent => ShouldDispatch
        ? NextState.SpentIntents[^1]
        : null;
}

/// <summary>
/// Converts one physical gameplay-key generation into at most one ally rescue
/// action attempt. A Dispatch decision already records the exact actor/status
/// intent as spent; callers must store NextState before invoking native code.
/// A rejected, false, or throwing action call is therefore never retried.
/// </summary>
public static class AllyRescueBufferRules
{
    public const long DefaultBufferMilliseconds = 750;
    public const long MinimumBufferMilliseconds = 100;
    public const long MaximumBufferMilliseconds = 750;

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
        if (current.Phase == AllyRescueBufferPhase.Buffered)
        {
            if (observation.NowMilliseconds >= current.ExpiresAtMilliseconds)
            {
                return Cancelled(
                    WaitingForFreshKey(current, intent, observation.NowMilliseconds),
                    AllyRescueBufferCancelReason.TimedOut,
                    selectedIndex);
            }

            if (observation.ActionLocallyReady)
                return Dispatch(current, intent, observation.NowMilliseconds, selectedIndex);

            return NoDecision(current, selectedIndex);
        }

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
        Math.Clamp(requestedMilliseconds, MinimumBufferMilliseconds, MaximumBufferMilliseconds);

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
        var buffered = current with
        {
            Phase = AllyRescueBufferPhase.Buffered,
            ArmedAtMilliseconds = observation.NowMilliseconds,
            ExpiresAtMilliseconds = SaturatingAdd(
                observation.NowMilliseconds,
                NormalizeBufferMilliseconds(observation.BufferMilliseconds)),
            LastObservedAtMilliseconds = observation.NowMilliseconds,
        };

        return observation.ActionLocallyReady
            ? Dispatch(
                buffered,
                current.TrackedIntent!.Value,
                observation.NowMilliseconds,
                selectedIndex,
                trigger)
            : new AllyRescueBufferDecision(
                buffered,
                AllyRescueBufferDecisionKind.Armed,
                AllyRescueBufferCancelReason.None,
                selectedIndex,
                trigger);
    }

    private static AllyRescueBufferDecision Dispatch(
        AllyRescueBufferState current,
        AllyRescueIntent intent,
        long nowMilliseconds,
        int selectedIndex,
        AllyRescueInputTrigger trigger = AllyRescueInputTrigger.None)
    {
        var spent = current.HasSpent(intent)
            ? current.SpentIntents
            : current.SpentIntents.Add(intent);
        var consumed = current with
        {
            Phase = AllyRescueBufferPhase.WaitingForCandidate,
            TrackedIntent = null,
            CandidateObservedAtMilliseconds = -1,
            ArmedAtMilliseconds = -1,
            ExpiresAtMilliseconds = -1,
            LastObservedAtMilliseconds = nowMilliseconds,
            SpentIntents = spent,
        };

        return new AllyRescueBufferDecision(
            consumed,
            AllyRescueBufferDecisionKind.Dispatch,
            AllyRescueBufferCancelReason.None,
            selectedIndex,
            trigger);
    }

    private static AllyRescueInputTrigger ResolveCandidateEntryTrigger(
        AllyRescueBufferObservation observation)
    {
        if (observation.FreshKeyPressed)
            return AllyRescueInputTrigger.FreshKeyPress;
        if (observation.AllowHeldKeyAtCandidateEntry && observation.HeldKeyEligible)
            return AllyRescueInputTrigger.HeldKeyAtCandidateEntry;

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
        };

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

    private static long SaturatingAdd(long left, long right) =>
        left > long.MaxValue - right ? long.MaxValue : left + right;
}
