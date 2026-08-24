using SeitonSense.Core;

internal static class LocalMpWarningSelfTests
{
    public static void FirstTrustedSampleOnlyEstablishesBaseline()
    {
        var high = Observe(LocalMpWarningState.Initial, 10_000);
        Equal(LocalMpWarningEdge.None, high.Edges, "high baseline is silent");
        True(high.NextState.HasContinuousTrustedSample, "high baseline trusted");

        var low = Observe(LocalMpWarningState.Initial, 1_500);
        Equal(LocalMpWarningEdge.None, low.Edges, "low baseline is also silent");
        False(low.NextState.FourThousandArmed, "low baseline spends 4k edge");
        False(low.NextState.TwoThousandArmed, "low baseline spends 2k edge");
    }

    public static void ThresholdEdgesAreInclusiveAndOneShot()
    {
        var state = Observe(LocalMpWarningState.Initial, 10_000).NextState;
        var aboveFour = Observe(state, LocalMpWarningRules.FourThousandThreshold + 1);
        Equal(LocalMpWarningEdge.None, aboveFour.Edges, "4,001 MP is above warning edge");

        var atFour = Observe(
            aboveFour.NextState,
            LocalMpWarningRules.FourThousandThreshold);
        Equal(LocalMpWarningEdge.FourThousand, atFour.Edges, "exact 4,000 crossing");
        var repeatFour = Observe(atFour.NextState, 3_999);
        Equal(LocalMpWarningEdge.None, repeatFour.Edges, "4k edge never repeats per frame");

        var aboveTwo = Observe(
            repeatFour.NextState,
            LocalMpWarningRules.TwoThousandThreshold + 1);
        Equal(LocalMpWarningEdge.None, aboveTwo.Edges, "2,001 MP is above critical edge");
        var atTwo = Observe(
            aboveTwo.NextState,
            LocalMpWarningRules.TwoThousandThreshold);
        Equal(LocalMpWarningEdge.TwoThousand, atTwo.Edges, "exact 2,000 crossing");
        var repeatTwo = Observe(atTwo.NextState, 1_000);
        Equal(LocalMpWarningEdge.None, repeatTwo.Edges, "2k edge never repeats per frame");
    }

    public static void DirectDropReportsBothCrossedEdges()
    {
        var state = Observe(LocalMpWarningState.Initial, 10_000).NextState;
        var dropped = Observe(state, 1_500);
        True(
            dropped.HasEdge(LocalMpWarningEdge.FourThousand),
            "direct drop crossed 4k");
        True(
            dropped.HasEdge(LocalMpWarningEdge.TwoThousand),
            "direct drop crossed 2k");
        Equal(
            LocalMpWarningEdge.FourThousand | LocalMpWarningEdge.TwoThousand,
            dropped.Edges,
            "only the two exact edges are reported");
        Equal(
            LocalMpWarningEdge.TwoThousand,
            dropped.MostSevereEdge,
            "2k is the one presentation edge for a direct double crossing");
    }

    public static void ThresholdsRearmIndependentlyWithHysteresis()
    {
        var state = Observe(LocalMpWarningState.Initial, 10_000).NextState;
        state = Observe(state, 4_000).NextState;
        state = Observe(state, 2_000).NextState;

        state = Observe(
            state,
            LocalMpWarningRules.TwoThousandRearmThreshold - 1).NextState;
        False(state.TwoThousandArmed, "2k remains spent below rearm boundary");
        False(state.FourThousandArmed, "4k remains spent below its recovery band");

        state = Observe(
            state,
            LocalMpWarningRules.TwoThousandRearmThreshold).NextState;
        True(state.TwoThousandArmed, "2k rearms at 2,300");
        False(state.FourThousandArmed, "2,300 does not rearm 4k");
        var secondCritical = Observe(state, LocalMpWarningRules.TwoThousandThreshold);
        Equal(
            LocalMpWarningEdge.TwoThousand,
            secondCritical.Edges,
            "rearmed critical edge fires once");

        state = Observe(
            secondCritical.NextState,
            LocalMpWarningRules.FourThousandRearmThreshold - 1).NextState;
        False(state.FourThousandArmed, "4k remains spent at 4,299");
        state = Observe(
            state,
            LocalMpWarningRules.FourThousandRearmThreshold).NextState;
        True(state.FourThousandArmed, "4k rearms at 4,300");
        var secondWarning = Observe(state, LocalMpWarningRules.FourThousandThreshold);
        Equal(
            LocalMpWarningEdge.FourThousand,
            secondWarning.Edges,
            "rearmed 4k edge fires once");
    }

    public static void InvalidTelemetryDeathAndResetAreSafe()
    {
        var state = Observe(LocalMpWarningState.Initial, 10_000).NextState;
        var invalid = LocalMpWarningRules.Observe(
            state,
            currentMp: 0,
            maximumMp: CombatFrameRules.ExpectedMaximumMp,
            telemetryTrusted: false,
            localPlayerAlive: true);
        Equal(LocalMpWarningEdge.None, invalid.Edges, "untrusted gap is silent");
        False(
            invalid.NextState.HasContinuousTrustedSample,
            "untrusted gap breaks crossing continuity");

        var lowAfterGap = Observe(invalid.NextState, 1_500);
        Equal(
            LocalMpWarningEdge.None,
            lowAfterGap.Edges,
            "low sample after unknown gap cannot invent crossed edges");

        var impossible = LocalMpWarningRules.Observe(
            state,
            currentMp: 10_001,
            maximumMp: CombatFrameRules.ExpectedMaximumMp,
            telemetryTrusted: true,
            localPlayerAlive: true);
        Equal(LocalMpWarningEdge.None, impossible.Edges, "impossible MP is silent");
        False(impossible.NextState.HasContinuousTrustedSample, "impossible MP breaks continuity");

        var dead = LocalMpWarningRules.Observe(
            state,
            currentMp: 1_500,
            maximumMp: CombatFrameRules.ExpectedMaximumMp,
            telemetryTrusted: true,
            localPlayerAlive: false);
        Equal(LocalMpWarningState.Initial, dead.NextState, "death resets state");
        Equal(LocalMpWarningEdge.None, dead.Edges, "death is silent");

        var reset = LocalMpWarningRules.Observe(
            state,
            currentMp: 1_500,
            maximumMp: CombatFrameRules.ExpectedMaximumMp,
            telemetryTrusted: true,
            localPlayerAlive: true,
            hardReset: true);
        Equal(LocalMpWarningState.Initial, reset.NextState, "hard reset resets state");
        Equal(LocalMpWarningEdge.None, reset.Edges, "hard reset is silent");
    }

    private static LocalMpWarningDecision Observe(
        LocalMpWarningState state,
        uint currentMp) =>
        LocalMpWarningRules.Observe(
            state,
            currentMp,
            CombatFrameRules.ExpectedMaximumMp,
            telemetryTrusted: true,
            localPlayerAlive: true);

    private static void Equal<T>(T expected, T actual, string label)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
    }

    private static void True(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException($"Expected true: {label}");
    }

    private static void False(bool condition, string label)
    {
        if (condition) throw new InvalidOperationException($"Expected false: {label}");
    }
}
