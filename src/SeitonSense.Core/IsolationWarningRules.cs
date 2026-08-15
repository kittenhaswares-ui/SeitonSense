namespace SeitonSense.Core;

public enum IsolationAllyReachability
{
    Unknown = 0,
    Unavailable = 1,
    Disconnected = 2,
    Connected = 3,
}

public enum IsolationWarningSignal
{
    Unknown = 0,
    Connected = 1,
    Isolated = 2,
}

public readonly record struct IsolationAllyObservation(
    bool IsExactPartyMember,
    bool IsAlive,
    bool IsTargetable,
    IsolationAllyReachability Reachability);

public readonly record struct IsolationWarningObservation(
    bool Enabled,
    bool IsCrystallineConflict,
    bool IsLocalPlayerValidAndAlive,
    bool HasCompleteExactParty,
    long NowMilliseconds,
    IReadOnlyList<IsolationAllyObservation>? Allies);

public readonly record struct IsolationWarningState(
    bool IsVisible,
    long IsolatedSinceMilliseconds,
    long ConnectedSinceMilliseconds)
{
    public static IsolationWarningState Initial => new(false, -1, -1);
}

public readonly record struct IsolationWarningDecision(
    IsolationWarningState NextState,
    IsolationWarningSignal Signal);

/// <summary>
/// Debounces a fail-closed party-connectivity observation. Unknown or incomplete
/// actor data clears the warning instead of asserting that the player is alone.
/// </summary>
public static class IsolationWarningRules
{
    public const int ExpectedNonSelfPartyMembers = 4;
    public const long EnterDelayMilliseconds = 500;
    public const long ClearDelayMilliseconds = 200;

    public static IsolationWarningDecision Observe(
        IsolationWarningState previous,
        IsolationWarningObservation observation)
    {
        if (!observation.Enabled ||
            !observation.IsCrystallineConflict ||
            !observation.IsLocalPlayerValidAndAlive ||
            !observation.HasCompleteExactParty ||
            observation.NowMilliseconds < 0 ||
            HasInvalidClock(previous, observation.NowMilliseconds))
        {
            return Unknown();
        }

        var signal = ResolveSignal(observation.Allies);
        if (signal == IsolationWarningSignal.Unknown)
            return Unknown();

        return signal == IsolationWarningSignal.Isolated
            ? ObserveIsolated(previous, observation.NowMilliseconds)
            : ObserveConnected(previous, observation.NowMilliseconds);
    }

    public static IsolationWarningSignal ResolveSignal(
        IReadOnlyList<IsolationAllyObservation>? allies)
    {
        if (allies is null || allies.Count != ExpectedNonSelfPartyMembers)
            return IsolationWarningSignal.Unknown;

        var connected = false;
        var hasUnknownLiveAlly = false;
        foreach (var ally in allies)
        {
            if (!ally.IsExactPartyMember || !Enum.IsDefined(ally.Reachability))
                return IsolationWarningSignal.Unknown;

            if (!ally.IsAlive)
            {
                if (ally.Reachability != IsolationAllyReachability.Unavailable)
                    return IsolationWarningSignal.Unknown;

                continue;
            }

            if (!ally.IsTargetable ||
                ally.Reachability is IsolationAllyReachability.Unknown or
                    IsolationAllyReachability.Unavailable)
            {
                hasUnknownLiveAlly = true;
                continue;
            }

            if (ally.Reachability == IsolationAllyReachability.Connected)
                connected = true;
        }

        if (connected) return IsolationWarningSignal.Connected;
        return hasUnknownLiveAlly
            ? IsolationWarningSignal.Unknown
            : IsolationWarningSignal.Isolated;
    }

    private static IsolationWarningDecision ObserveIsolated(
        IsolationWarningState previous,
        long nowMilliseconds)
    {
        if (previous.IsVisible)
        {
            return new IsolationWarningDecision(
                new IsolationWarningState(true, -1, -1),
                IsolationWarningSignal.Isolated);
        }

        var isolatedSince = previous.IsolatedSinceMilliseconds >= 0
            ? previous.IsolatedSinceMilliseconds
            : nowMilliseconds;
        var visible = nowMilliseconds - isolatedSince >= EnterDelayMilliseconds;
        return new IsolationWarningDecision(
            visible
                ? new IsolationWarningState(true, -1, -1)
                : new IsolationWarningState(false, isolatedSince, -1),
            IsolationWarningSignal.Isolated);
    }

    private static IsolationWarningDecision ObserveConnected(
        IsolationWarningState previous,
        long nowMilliseconds)
    {
        if (!previous.IsVisible)
        {
            return new IsolationWarningDecision(
                IsolationWarningState.Initial,
                IsolationWarningSignal.Connected);
        }

        var connectedSince = previous.ConnectedSinceMilliseconds >= 0
            ? previous.ConnectedSinceMilliseconds
            : nowMilliseconds;
        var cleared = nowMilliseconds - connectedSince >= ClearDelayMilliseconds;
        return new IsolationWarningDecision(
            cleared
                ? IsolationWarningState.Initial
                : new IsolationWarningState(true, -1, connectedSince),
            IsolationWarningSignal.Connected);
    }

    private static bool HasInvalidClock(
        IsolationWarningState state,
        long nowMilliseconds) =>
        state.IsolatedSinceMilliseconds > nowMilliseconds ||
        state.ConnectedSinceMilliseconds > nowMilliseconds;

    private static IsolationWarningDecision Unknown() =>
        new(IsolationWarningState.Initial, IsolationWarningSignal.Unknown);
}
