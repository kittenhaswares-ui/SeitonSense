using SeitonSense.Core;

internal static class IsolationWarningSelfTests
{
    public static void ContinuousIsolationUsesEntryDelay()
    {
        var decision = Observe(IsolationWarningState.Initial, 1_000, DisconnectedAllies());
        False(decision.NextState.IsVisible, "first isolated sample only arms");

        decision = Observe(decision.NextState, 1_499, DisconnectedAllies());
        False(decision.NextState.IsVisible, "entry delay is strict before 500 ms");

        decision = Observe(decision.NextState, 1_500, DisconnectedAllies());
        True(decision.NextState.IsVisible, "entry delay completes at 500 ms");
        Equal(IsolationWarningSignal.Isolated, decision.Signal, "isolated signal is retained");
    }

    public static void StableConnectionUsesClearDelay()
    {
        var visible = new IsolationWarningState(true, -1, -1);
        var decision = Observe(visible, 2_000, ConnectedAllies());
        True(decision.NextState.IsVisible, "first connected sample does not flicker the card");

        decision = Observe(decision.NextState, 2_199, ConnectedAllies());
        True(decision.NextState.IsVisible, "card remains through the 200 ms clear grace");

        decision = Observe(decision.NextState, 2_200, ConnectedAllies());
        False(decision.NextState.IsVisible, "stable connection clears at 200 ms");
        Equal(IsolationWarningSignal.Connected, decision.Signal, "connected signal is diagnostic");
    }

    public static void DeadAlliesDoNotProvideConnection()
    {
        var dead = Enumerable.Range(0, IsolationWarningRules.ExpectedNonSelfPartyMembers)
            .Select(static _ => new IsolationAllyObservation(
                IsExactPartyMember: true,
                IsAlive: false,
                IsTargetable: false,
                IsolationAllyReachability.Unavailable))
            .ToArray();

        Equal(IsolationWarningSignal.Isolated, IsolationWarningRules.ResolveSignal(dead), "four confirmed dead allies leave self isolated");
    }

    public static void AnyReachableAllyPreventsIsolation()
    {
        var allies = DisconnectedAllies();
        allies[2] = allies[2] with { Reachability = IsolationAllyReachability.Connected };
        Equal(IsolationWarningSignal.Connected, IsolationWarningRules.ResolveSignal(allies), "one connected ally is sufficient");
    }

    public static void UnknownAndIncompleteDataFailClosed()
    {
        Equal(IsolationWarningSignal.Unknown, IsolationWarningRules.ResolveSignal(null), "null party is unknown");
        Equal(IsolationWarningSignal.Unknown, IsolationWarningRules.ResolveSignal(DisconnectedAllies()[..3]), "incomplete party is unknown");

        var unknown = DisconnectedAllies();
        unknown[0] = unknown[0] with { Reachability = IsolationAllyReachability.Unknown };
        Equal(IsolationWarningSignal.Unknown, IsolationWarningRules.ResolveSignal(unknown), "unknown native result suppresses isolation");

        var inexact = DisconnectedAllies();
        inexact[0] = inexact[0] with { IsExactPartyMember = false };
        Equal(IsolationWarningSignal.Unknown, IsolationWarningRules.ResolveSignal(inexact), "inexact identity suppresses isolation");

        var visible = new IsolationWarningState(true, -1, -1);
        var reset = IsolationWarningRules.Observe(
            visible,
            Observation(3_000, DisconnectedAllies()) with { HasCompleteExactParty = false });
        False(reset.NextState.IsVisible, "unknown party state clears a visible warning");
        Equal(IsolationWarningSignal.Unknown, reset.Signal, "unknown state is explicit");
    }

    public static void JitterAndClockResetAreSafe()
    {
        var decision = Observe(IsolationWarningState.Initial, 4_000, DisconnectedAllies());
        decision = Observe(decision.NextState, 4_300, ConnectedAllies());
        decision = Observe(decision.NextState, 4_301, DisconnectedAllies());
        False(decision.NextState.IsVisible, "brief isolation before reconnection cannot leak through");

        var rolledBack = Observe(
            new IsolationWarningState(false, 5_000, -1),
            4_999,
            DisconnectedAllies());
        False(rolledBack.NextState.IsVisible, "clock rollback clears pending state");
        Equal(IsolationWarningSignal.Unknown, rolledBack.Signal, "clock rollback fails closed");
    }

    private static IsolationWarningDecision Observe(
        IsolationWarningState state,
        long now,
        IReadOnlyList<IsolationAllyObservation> allies) =>
        IsolationWarningRules.Observe(state, Observation(now, allies));

    private static IsolationWarningObservation Observation(
        long now,
        IReadOnlyList<IsolationAllyObservation> allies) =>
        new(
            Enabled: true,
            IsCrystallineConflict: true,
            IsLocalPlayerValidAndAlive: true,
            HasCompleteExactParty: true,
            now,
            allies);

    private static IsolationAllyObservation[] DisconnectedAllies() =>
        Enumerable.Range(0, IsolationWarningRules.ExpectedNonSelfPartyMembers)
            .Select(static _ => new IsolationAllyObservation(
                IsExactPartyMember: true,
                IsAlive: true,
                IsTargetable: true,
                IsolationAllyReachability.Disconnected))
            .ToArray();

    private static IsolationAllyObservation[] ConnectedAllies()
    {
        var allies = DisconnectedAllies();
        allies[0] = allies[0] with { Reachability = IsolationAllyReachability.Connected };
        return allies;
    }

    private static void True(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static void False(bool value, string message) => True(!value, message);

    private static void Equal<T>(T expected, T actual, string message) where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
    }
}
