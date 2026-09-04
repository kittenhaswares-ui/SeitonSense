using SeitonSense.Core;

internal static class BardRepellingShotRetrySelfTests
{
    public static void BusyToReadyWakesOnlyTheSameFrozenIntent()
    {
        var gate = new BardRepellingShotRetryGate();
        var retry = HeldActionRetryState.Initial;
        Require(Sample(gate, 1, retry, true, 10, 1_000),
            "The first ready Mannstopper attempt is immediate.");
        gate.ReserveAttemptFrame(10);
        retry = HeldActionRetryRules.Complete(
            retry, 1_000, ClientActionAttemptOutcome.ClientRejected).NextState;

        Require(!Sample(gate, 1, retry, false, 11, 1_010),
            "A busy native boundary waits without spending a retry.");
        Require(Sample(gate, 1, retry, true, 12, 1_020),
            "The same intent wakes on readiness before its 50 ms timer.");
        gate.ReserveAttemptFrame(12);
        Require(!Sample(gate, 1, retry, true, 12, 1_021),
            "Another observation in the attempted frame cannot dispatch twice.");

        retry = HeldActionRetryRules.Complete(
            retry, 1_020, ClientActionAttemptOutcome.ClientRejected).NextState;
        Require(!Sample(gate, 1, retry, false, 13, 1_025), "The boundary is busy again.");
        Require(!Sample(gate, 2, retry, true, 14, 1_030),
            "Another target/intent cannot inherit the previous intent's busy edge.");
    }

    public static void StableReadyUnknownAndClearedEpisodesKeepTheThrottle()
    {
        var gate = new BardRepellingShotRetryGate();
        var retry = HeldActionRetryRules.Complete(
            HeldActionRetryState.Initial, 1_000,
            ClientActionAttemptOutcome.ClientRejected).NextState;
        gate.ReserveAttemptFrame(10);
        Require(!Sample(gate, 1, retry, true, 11, 1_010), "First sample establishes readiness.");
        Require(!Sample(gate, 1, retry, true, 12, 1_020), "Already-ready drift is not an edge.");
        Require(!gate.Observe(1, retry, false, false, true, 13, 1_025),
            "Unknown native state cannot dispatch.");
        Require(!Sample(gate, 1, retry, true, 14, 1_030),
            "Unknown-to-ready does not invent a busy-to-ready edge.");
        Require(!Sample(gate, 1, retry, false, 15, 1_035), "A real busy sample waits.");
        gate.ClearEpisode();
        Require(!Sample(gate, 1, retry, true, 16, 1_040),
            "Clearing an episode removes its remembered busy sample.");
        Require(Sample(gate, 1, retry, true, 17, 1_050),
            "The normal retry timer still releases a stable-ready intent.");
    }

    public static void DisabledEdgesAndTerminalAttemptsNeverGainExtraRetries()
    {
        var gate = new BardRepellingShotRetryGate();
        var retry = HeldActionRetryRules.Complete(
            HeldActionRetryState.Initial, 1_000,
            ClientActionAttemptOutcome.ClientRejected).NextState;
        Require(!gate.Observe(1, retry, true, false, false, 10, 1_010), "Busy waits.");
        Require(!gate.Observe(1, retry, true, true, false, 11, 1_020),
            "With adaptive response off, readiness keeps the original throttle.");

        var exhausted = new HeldActionRetryState(
            HeldActionRetryRules.MaximumNativeAttempts,
            1_050,
            HeldActionRetryRules.MaximumNativeAttempts);
        Require(!Sample(gate, 1, exhausted, false, 12, 1_055), "An exhausted intent waits.");
        Require(!Sample(gate, 1, exhausted, true, 13, 1_060),
            "Even a true edge cannot exceed the frozen attempt budget.");

        foreach (var outcome in new[]
                 {
                     ClientActionAttemptOutcome.ClientAccepted,
                     ClientActionAttemptOutcome.AcceptanceUnknown,
                 })
        {
            var completion = HeldActionRetryRules.Complete(retry, 1_070, outcome);
            Require(completion.IsTerminal && !completion.RetryScheduled,
                $"{outcome} retires the native attempt.");
            gate.ClearEpisode();
            Require(!gate.Observe(0, completion.NextState, true, true, true, 14, 1_080),
                "A retired probe episode cannot dispatch from a readiness observation.");
        }

        gate.Reset();
        Require(Sample(gate, 2, HeldActionRetryState.Initial, true, 1, 100),
            "A hard lifecycle reset permits the new episode's first ready attempt.");
    }

    private static bool Sample(
        BardRepellingShotRetryGate gate,
        ulong epoch,
        HeldActionRetryState retry,
        bool ready,
        long frame,
        long now) => gate.Observe(epoch, retry, true, ready, true, frame, now);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
