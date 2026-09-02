using SeitonSense.Core;

internal static class CombatFrameLimitGaugeSelfTests
{
    internal static void RenderedBoundsAreNormalizedFailClosed()
    {
        var hidden = Measurement(0f, fillVisible: false);
        True(
            CombatFrameLimitGaugeRules.TryGetRenderedFraction(hidden, out var zero, out var tolerance) &&
            zero == 0d &&
            tolerance > 0d,
            "a hidden layer is the native zero state only when its track is valid");

        var half = Measurement(0.5f);
        True(
            CombatFrameLimitGaugeRules.TryGetRenderedFraction(half, out var fraction, out _) &&
            Math.Abs(fraction - 0.5d) < 0.0001d,
            "rendered fill width is normalized by the rendered track width");

        False(
            CombatFrameLimitGaugeRules.TryGetRenderedFraction(
                new CombatFrameLimitGaugeNativeMeasurement(
                    Bounds(0f, 0f, 38f, 16f),
                    true,
                    Bounds(-5f, 0f, 24f, 16f)),
                out _,
                out _),
            "a fill outside the native track is rejected");
        False(
            CombatFrameLimitGaugeRules.TryGetRenderedFraction(
                new CombatFrameLimitGaugeNativeMeasurement(default, false, default),
                out _,
                out _),
            "hidden fill cannot manufacture zero without a valid rendered track");
    }

    internal static void RemoteGaugeRequiresCompleteLocalProof()
    {
        var calibrator = new CombatFrameLimitGaugeCalibrator();
        False(Project(calibrator, 0.4f), "remote gauge starts unknown");

        Equal(
            CombatFrameLimitGaugeCalibrationResult.Accepted,
            calibrator.Observe(Observation(0, 1_000, 0f, fillVisible: false)),
            "zero endpoint");
        Equal(
            CombatFrameLimitGaugeCalibrationResult.Accepted,
            calibrator.Observe(Observation(250, 1_000, 0.25f)),
            "first partial point");
        False(Project(calibrator, 0.4f), "one partial point cannot publish remote values");
        Equal(
            CombatFrameLimitGaugeCalibrationResult.Calibrated,
            calibrator.Observe(Observation(600, 1_000, 0.6f)),
            "second separated partial point completes proof without a sampled full frame");
        True(Project(calibrator, 0.4f, out var projected), "calibrated remote projection");
        Near(0.4f, projected, 0.0001f, "projected native fraction");

        var diagnostics = calibrator.Diagnostics;
        True(diagnostics.Calibrated, "diagnostic calibrated state");
        True(diagnostics.SeenZero, "diagnostic zero proof");
        False(diagnostics.SeenFull, "full is diagnostic evidence, not a liveness dependency");
        Equal(2, diagnostics.DistinctNonTerminalSamples, "diagnostic partial proof count");
        True(diagnostics.HasSeparatedNonTerminalSamples, "diagnostic partial separation");
    }

    internal static void DuplicateAndNearIdenticalPartialsDoNotCompleteProof()
    {
        var calibrator = new CombatFrameLimitGaugeCalibrator();
        calibrator.Observe(Observation(0, 1_000, 0f, fillVisible: false));
        calibrator.Observe(Observation(1_000, 1_000, 1f));
        calibrator.Observe(Observation(250, 1_000, 0.25f));
        calibrator.Observe(Observation(250, 1_000, 0.25f));
        False(calibrator.IsCalibrated, "duplicate controller units are one sample");

        calibrator.Observe(Observation(260, 1_000, 0.26f));
        False(
            calibrator.IsCalibrated,
            "two partial samples inside rendered quantization tolerance are not separated proof");
    }

    internal static void FingerprintDriftInvalidatesCalibration()
    {
        var context = FullyCalibrated();
        Equal(
            CombatFrameLimitGaugeCalibrationResult.Invalidated,
            context.Observe(Observation(700, 1_000, 0.7f, context: 99)),
            "context fingerprint drift");
        False(context.IsCalibrated, "context drift clears proof");
        Equal(
            CombatFrameLimitGaugeInvalidationReason.ContextChanged,
            context.Diagnostics.LastInvalidationReason,
            "context invalidation reason");

        var addon = FullyCalibrated();
        Equal(
            CombatFrameLimitGaugeCalibrationResult.Invalidated,
            addon.Observe(Observation(700, 1_000, 0.7f, instance: 99)),
            "native addon/row instance drift");
        Equal(
            CombatFrameLimitGaugeInvalidationReason.AddonChanged,
            addon.Diagnostics.LastInvalidationReason,
            "addon invalidation reason");

        var hierarchy = FullyCalibrated();
        Equal(
            CombatFrameLimitGaugeCalibrationResult.Invalidated,
            hierarchy.Observe(Observation(700, 1_000, 0.7f, shape: 99)),
            "layout hierarchy drift");
        Equal(
            CombatFrameLimitGaugeInvalidationReason.HierarchyChanged,
            hierarchy.Diagnostics.LastInvalidationReason,
            "hierarchy invalidation reason");
    }

    internal static void ContradictoryGeometryInvalidatesCalibration()
    {
        var direct = FullyCalibrated();
        Equal(
            CombatFrameLimitGaugeCalibrationResult.Invalidated,
            direct.Observe(Observation(700, 1_000, 0.25f)),
            "rendered ratio contradicts exact native controller units");
        False(direct.IsCalibrated, "direct contradiction clears proof");
        Equal(1L, direct.Diagnostics.Contradictions, "contradiction is counted");

        var monotonic = new CombatFrameLimitGaugeCalibrator();
        monotonic.Observe(Observation(0, 1_000, 0f, fillVisible: false));
        monotonic.Observe(Observation(300, 1_000, 0.3f));
        Equal(
            CombatFrameLimitGaugeCalibrationResult.Invalidated,
            monotonic.Observe(Observation(700, 1_000, 0.2f)),
            "increasing native units cannot render a materially smaller fill");
        False(monotonic.Diagnostics.Bound, "monotonic contradiction clears binding");
    }

    internal static void ReadingFactoriesNeverClaimInvalidTelemetry()
    {
        True(CombatFrameLimitGaugeRules.ExactSelf(0.5f).IsKnown, "exact local controller value");
        False(CombatFrameLimitGaugeRules.ExactSelf(float.NaN).IsKnown, "invalid local fraction");
        True(CombatFrameLimitGaugeRules.CalibratedEnemy(1, 1f).IsKnown, "valid S1 value");
        False(CombatFrameLimitGaugeRules.CalibratedEnemy(0, 0.5f).IsKnown, "self is not an enemy slot");
        False(CombatFrameLimitGaugeRules.CalibratedEnemy(6, 0.5f).IsKnown, "S6 does not exist");
        False(CombatFrameLimitGaugeRules.CalibratedEnemy(1, 1.01f).IsKnown, "out-of-range value");
        True(
            CombatFrameLimitGaugeRules.UnknownEnemies().All(static reading => !reading.IsKnown),
            "uncalibrated S1-S5 are explicitly unknown");
    }

    private static CombatFrameLimitGaugeCalibrator FullyCalibrated()
    {
        var calibrator = new CombatFrameLimitGaugeCalibrator();
        calibrator.Observe(Observation(0, 1_000, 0f, fillVisible: false));
        calibrator.Observe(Observation(250, 1_000, 0.25f));
        calibrator.Observe(Observation(600, 1_000, 0.6f));
        calibrator.Observe(Observation(1_000, 1_000, 1f));
        True(calibrator.IsCalibrated, "test setup calibration");
        return calibrator;
    }

    private static CombatFrameLimitGaugeCalibrationObservation Observation(
        uint current,
        uint maximum,
        float ratio,
        bool fillVisible = true,
        ulong context = 10,
        ulong instance = 20,
        ulong shape = 30) =>
        new(context, instance, shape, current, maximum, Measurement(ratio, fillVisible));

    private static CombatFrameLimitGaugeNativeMeasurement Measurement(
        float ratio,
        bool fillVisible = true) =>
        new(
            Bounds(100f, 200f, 138f, 216f),
            fillVisible,
            fillVisible
                ? Bounds(100f, 200f, 100f + (38f * ratio), 216f)
                : default);

    private static CombatFrameLimitGaugeRenderedBounds Bounds(
        float minimumX,
        float minimumY,
        float maximumX,
        float maximumY) =>
        new(minimumX, minimumY, maximumX, maximumY);

    private static bool Project(
        CombatFrameLimitGaugeCalibrator calibrator,
        float ratio) =>
        Project(calibrator, ratio, out _);

    private static bool Project(
        CombatFrameLimitGaugeCalibrator calibrator,
        float ratio,
        out float projected) =>
        calibrator.TryProjectRemote(10, 30, Measurement(ratio), out projected);

    private static void Near(float expected, float actual, float tolerance, string label)
    {
        if (!float.IsFinite(actual) || Math.Abs(expected - actual) > tolerance)
            throw new InvalidOperationException($"{label}: expected {expected} +/- {tolerance}, got {actual}");
    }

    private static void True(bool value, string label)
    {
        if (!value) throw new InvalidOperationException($"Expected true: {label}");
    }

    private static void False(bool value, string label) => True(!value, label);

    private static void Equal<T>(T expected, T actual, string label)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
    }
}
