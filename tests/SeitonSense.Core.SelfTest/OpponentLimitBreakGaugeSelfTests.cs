using SeitonSense.Core;

internal static class OpponentLimitBreakGaugeSelfTests
{
    internal static void DirectValuesAndLocalProofFailClosed()
    {
        True(
            OpponentLimitBreakGaugeRules.MatchesNativeScale(0, 10_000, 0, 10_000),
            "identical native scales match");
        False(
            OpponentLimitBreakGaugeRules.MatchesNativeScale(0, 10_000, 0, 1_000),
            "a different row scale fails closed");

        var actor = new TargetPressureActorIdentity(10, 20);
        True(
            OpponentLimitBreakGaugeRules.TryCreateValue(
                actor,
                1,
                30,
                0,
                75,
                100,
                out var value),
            "valid direct GaugeBar values");
        NearlyEqual(0.75f, value.Fraction, "direct fraction");
        False(value.IsReady, "partial gauge");
        False(TryValue(actor, 0, 30, 0, 75, 100), "invalid slot");
        False(TryValue(actor, 1, 0, 0, 75, 100), "missing job");
        False(TryValue(actor, 1, 30, 100, 75, 0), "inverted range");
        False(TryValue(actor, 1, 30, 0, 101, 100), "current beyond maximum");

        True(
            OpponentLimitBreakGaugeRules.MatchesLocalController(0, 75, 100, 7_500, 10_000),
            "equal normalized values prove layout");
        True(
            OpponentLimitBreakGaugeRules.MatchesLocalController(0, 74, 100, 7_500, 10_000),
            "one native row quantization unit is bounded");
        False(
            OpponentLimitBreakGaugeRules.MatchesLocalController(0, 60, 100, 7_500, 10_000),
            "contradictory local row fails closed");
        False(
            OpponentLimitBreakGaugeRules.MatchesLocalController(0, 75, 100, 10_001, 10_000),
            "invalid controller fails closed");
    }

    internal static void CalibratedValuesRemainBoundedAndExactAtReady()
    {
        var actor = new TargetPressureActorIdentity(10, 20);
        True(
            OpponentLimitBreakGaugeRules.TryCreateCalibratedValue(
                actor,
                3,
                30,
                0.5f,
                out var half),
            "bounded calibrated half value");
        Equal(0, half.MinimumValue, "calibrated minimum");
        Equal(
            OpponentLimitBreakGaugeRules.CalibratedMaximumValue,
            half.MaximumValue,
            "calibrated maximum");
        NearlyEqual(0.5f, half.Fraction, "calibrated half fraction");
        False(half.IsReady, "partial calibrated value is not ready");

        True(
            OpponentLimitBreakGaugeRules.TryCreateCalibratedValue(
                actor,
                3,
                30,
                1f,
                out var full),
            "exact calibrated full value");
        True(full.IsReady, "only exact calibrated full is ready");

        True(
            OpponentLimitBreakGaugeRules.TryCreateCalibratedValue(
                actor,
                3,
                30,
                0.99999f,
                out var nearFull),
            "near-full calibrated value");
        False(nearFull.IsReady, "near-full rounding cannot synthesize ready");
        False(TryCalibrated(actor, 3, 30, -0.01f), "negative calibrated fraction");
        False(TryCalibrated(actor, 3, 30, 1.01f), "overflow calibrated fraction");
        False(TryCalibrated(actor, 3, 30, float.NaN), "non-finite calibrated fraction");
    }

    internal static void CompleteSetFreshnessAndPulseAreBounded()
    {
        var values = Enumerable.Range(1, 5)
            .Select(slot => new OpponentLimitBreakGaugeValue(
                new TargetPressureActorIdentity((ulong)slot, (uint)slot),
                slot,
                (uint)(18 + slot),
                0,
                slot == 5 ? 100 : slot * 15,
                100))
            .ToArray();
        True(OpponentLimitBreakGaugeRules.IsCompleteExactEnemySet(values), "exact S1-S5 set");
        False(
            OpponentLimitBreakGaugeRules.IsCompleteExactEnemySet(values[..4]),
            "missing row hides complete set");
        var duplicate = values.ToArray();
        duplicate[4] = duplicate[4] with { Actor = duplicate[0].Actor };
        False(
            OpponentLimitBreakGaugeRules.IsCompleteExactEnemySet(duplicate),
            "duplicate actor hides complete set");

        const long now = 10_000;
        True(OpponentLimitBreakGaugeRules.IsFresh(now - 250, now), "freshness boundary inclusive");
        False(OpponentLimitBreakGaugeRules.IsFresh(now - 251, now), "stale snapshot hidden");
        Equal(1f, OpponentLimitBreakGaugeRules.ReadyPulseAlpha(false, now, false), "partial static");
        Equal(1f, OpponentLimitBreakGaugeRules.ReadyPulseAlpha(true, now, true), "reduced motion static");
        var pulse = OpponentLimitBreakGaugeRules.ReadyPulseAlpha(true, now + 250, false);
        True(pulse is >= 0.78f and <= 1f, "ready pulse alpha bounded");
    }

    private static bool TryValue(
        TargetPressureActorIdentity actor,
        int slot,
        uint job,
        int minimum,
        int current,
        int maximum) =>
        OpponentLimitBreakGaugeRules.TryCreateValue(
            actor,
            slot,
            job,
            minimum,
            current,
            maximum,
            out _);

    private static bool TryCalibrated(
        TargetPressureActorIdentity actor,
        int slot,
        uint job,
        float fraction) =>
        OpponentLimitBreakGaugeRules.TryCreateCalibratedValue(
            actor,
            slot,
            job,
            fraction,
            out _);

    private static void NearlyEqual(float expected, float actual, string message)
    {
        if (Math.Abs(expected - actual) > 0.001f)
            throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
    }

    private static void Equal<T>(T expected, T actual, string message)
        where T : IEquatable<T>
    {
        if (!actual.Equals(expected))
            throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
    }

    private static void True(bool value, string message)
    {
        if (!value) throw new InvalidOperationException($"Expected true: {message}");
    }

    private static void False(bool value, string message)
    {
        if (value) throw new InvalidOperationException($"Expected false: {message}");
    }
}
