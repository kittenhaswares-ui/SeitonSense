namespace SeitonSense.Core;

public enum CombatFrameLimitGaugeTrust : byte
{
    Unknown = 0,
    ExactLocalController = 1,
    CalibratedNativeHud = 2,
}

public readonly record struct CombatFrameLimitGaugeReading(
    int Slot,
    CombatFrameLimitGaugeTrust Trust,
    float Fraction)
{
    public bool IsKnown =>
        Trust != CombatFrameLimitGaugeTrust.Unknown &&
        float.IsFinite(Fraction) &&
        Fraction is >= 0f and <= 1f;

    public static CombatFrameLimitGaugeReading Unknown(int slot) =>
        new(slot, CombatFrameLimitGaugeTrust.Unknown, 0f);
}

public readonly record struct CombatFrameLimitGaugeRenderedBounds(
    float MinimumX,
    float MinimumY,
    float MaximumX,
    float MaximumY)
{
    public float Width => MaximumX - MinimumX;
    public float Height => MaximumY - MinimumY;
}

public readonly record struct CombatFrameLimitGaugeNativeMeasurement(
    CombatFrameLimitGaugeRenderedBounds TrackBounds,
    bool FillVisible,
    CombatFrameLimitGaugeRenderedBounds FillBounds);

public readonly record struct CombatFrameLimitGaugeCalibrationObservation(
    ulong ContextFingerprint,
    ulong CalibrationInstanceFingerprint,
    ulong LayoutShapeFingerprint,
    uint CurrentUnits,
    uint MaximumUnits,
    CombatFrameLimitGaugeNativeMeasurement Measurement);

public enum CombatFrameLimitGaugeCalibrationResult : byte
{
    Rejected = 0,
    Accepted = 1,
    Calibrated = 2,
    Invalidated = 3,
}

public enum CombatFrameLimitGaugeInvalidationReason : byte
{
    None = 0,
    ContextLost = 1,
    ContextChanged = 2,
    AddonChanged = 3,
    HierarchyChanged = 4,
    IdentityChanged = 5,
    ContradictorySample = 6,
    AmbiguousMapping = 7,
}

public readonly record struct CombatFrameLimitGaugeCalibrationDiagnostics(
    bool Bound,
    bool Calibrated,
    bool SeenZero,
    bool SeenFull,
    int DistinctNonTerminalSamples,
    bool HasSeparatedNonTerminalSamples,
    int DistinctSampleCount,
    long AcceptedSamples,
    long Invalidations,
    long Contradictions,
    CombatFrameLimitGaugeInvalidationReason LastInvalidationReason,
    ulong ContextFingerprint,
    ulong CalibrationInstanceFingerprint,
    ulong LayoutShapeFingerprint);

/// <summary>
/// Calibrates the small CC party-list LB layer against the local player's exact
/// native LimitBreakController units. Remote values remain unavailable until the
/// current HUD instance has demonstrated zero and two separated partial fills.
/// Those three exact controller matches prove both the empty-fill convention and
/// the rendered scale without depending on a one-frame full gauge that can be
/// consumed between HUD samples. No elapsed-time or job recharge model is involved.
/// </summary>
public sealed class CombatFrameLimitGaugeCalibrator
{
    private readonly SortedDictionary<uint, CalibrationPoint> points = [];
    private ulong contextFingerprint;
    private ulong calibrationInstanceFingerprint;
    private ulong layoutShapeFingerprint;
    private uint maximumUnits;
    private long acceptedSamples;
    private long invalidations;
    private long contradictions;
    private CombatFrameLimitGaugeInvalidationReason lastInvalidationReason;
    private bool calibrated;

    public bool IsCalibrated => calibrated;

    public CombatFrameLimitGaugeCalibrationDiagnostics Diagnostics
    {
        get
        {
            var nonTerminal = points.Values
                .Where(point => point.CurrentUnits > 0 && point.CurrentUnits < maximumUnits)
                .ToArray();
            return new CombatFrameLimitGaugeCalibrationDiagnostics(
                contextFingerprint != 0,
                calibrated,
                points.ContainsKey(0),
                maximumUnits > 0 && points.ContainsKey(maximumUnits),
                nonTerminal.Length,
                HasSeparatedNonTerminalSamples(nonTerminal),
                points.Count,
                acceptedSamples,
                invalidations,
                contradictions,
                lastInvalidationReason,
                contextFingerprint,
                calibrationInstanceFingerprint,
                layoutShapeFingerprint);
        }
    }

    public CombatFrameLimitGaugeCalibrationResult Observe(
        in CombatFrameLimitGaugeCalibrationObservation observation)
    {
        if (observation.ContextFingerprint == 0 ||
            observation.CalibrationInstanceFingerprint == 0 ||
            observation.LayoutShapeFingerprint == 0 ||
            observation.MaximumUnits == 0 ||
            observation.CurrentUnits > observation.MaximumUnits ||
            !CombatFrameLimitGaugeRules.TryGetRenderedFraction(
                observation.Measurement,
                out var renderedFraction,
                out var tolerance))
        {
            Invalidate(CombatFrameLimitGaugeInvalidationReason.ContradictorySample);
            return CombatFrameLimitGaugeCalibrationResult.Rejected;
        }

        if (contextFingerprint == 0)
        {
            Bind(observation);
        }
        else if (observation.ContextFingerprint != contextFingerprint)
        {
            Invalidate(CombatFrameLimitGaugeInvalidationReason.ContextChanged);
            return CombatFrameLimitGaugeCalibrationResult.Invalidated;
        }
        else if (observation.CalibrationInstanceFingerprint != calibrationInstanceFingerprint)
        {
            Invalidate(CombatFrameLimitGaugeInvalidationReason.AddonChanged);
            return CombatFrameLimitGaugeCalibrationResult.Invalidated;
        }
        else if (observation.LayoutShapeFingerprint != layoutShapeFingerprint)
        {
            Invalidate(CombatFrameLimitGaugeInvalidationReason.HierarchyChanged);
            return CombatFrameLimitGaugeCalibrationResult.Invalidated;
        }
        else if (observation.MaximumUnits != maximumUnits)
        {
            Invalidate(CombatFrameLimitGaugeInvalidationReason.ContradictorySample);
            return CombatFrameLimitGaugeCalibrationResult.Invalidated;
        }

        var exactFraction = observation.CurrentUnits / (double)observation.MaximumUnits;
        if (Math.Abs(renderedFraction - exactFraction) > tolerance)
        {
            Invalidate(CombatFrameLimitGaugeInvalidationReason.ContradictorySample);
            return CombatFrameLimitGaugeCalibrationResult.Invalidated;
        }

        var point = new CalibrationPoint(
            observation.CurrentUnits,
            renderedFraction,
            tolerance);
        if (!IsMonotonicWithExisting(point))
        {
            Invalidate(CombatFrameLimitGaugeInvalidationReason.ContradictorySample);
            return CombatFrameLimitGaugeCalibrationResult.Invalidated;
        }

        if (points.TryGetValue(observation.CurrentUnits, out var existing))
        {
            if (Math.Abs(existing.RenderedFraction - renderedFraction) >
                Math.Max(existing.Tolerance, tolerance))
            {
                Invalidate(CombatFrameLimitGaugeInvalidationReason.ContradictorySample);
                return CombatFrameLimitGaugeCalibrationResult.Invalidated;
            }
        }
        else
        {
            points.Add(observation.CurrentUnits, point);
        }

        acceptedSamples++;
        var wasCalibrated = calibrated;
        calibrated = HasCompleteProof();
        return !wasCalibrated && calibrated
            ? CombatFrameLimitGaugeCalibrationResult.Calibrated
            : CombatFrameLimitGaugeCalibrationResult.Accepted;
    }

    public bool TryProjectRemote(
        ulong context,
        ulong shape,
        in CombatFrameLimitGaugeNativeMeasurement measurement,
        out float fraction)
    {
        fraction = 0f;
        if (!calibrated ||
            context == 0 ||
            shape == 0 ||
            context != contextFingerprint ||
            shape != layoutShapeFingerprint ||
            !CombatFrameLimitGaugeRules.TryGetRenderedFraction(
                measurement,
                out var renderedFraction,
                out _))
        {
            return false;
        }

        fraction = (float)renderedFraction;
        return float.IsFinite(fraction) && fraction is >= 0f and <= 1f;
    }

    public void Invalidate(CombatFrameLimitGaugeInvalidationReason reason)
    {
        if (reason == CombatFrameLimitGaugeInvalidationReason.None) return;

        var hadState = contextFingerprint != 0 || points.Count > 0 || calibrated;
        contextFingerprint = 0;
        calibrationInstanceFingerprint = 0;
        layoutShapeFingerprint = 0;
        maximumUnits = 0;
        points.Clear();
        calibrated = false;
        lastInvalidationReason = reason;
        if (!hadState) return;

        invalidations++;
        if (reason == CombatFrameLimitGaugeInvalidationReason.ContradictorySample)
            contradictions++;
    }

    private void Bind(in CombatFrameLimitGaugeCalibrationObservation observation)
    {
        contextFingerprint = observation.ContextFingerprint;
        calibrationInstanceFingerprint = observation.CalibrationInstanceFingerprint;
        layoutShapeFingerprint = observation.LayoutShapeFingerprint;
        maximumUnits = observation.MaximumUnits;
    }

    private bool IsMonotonicWithExisting(in CalibrationPoint candidate)
    {
        foreach (var existing in points.Values)
        {
            var tolerance = Math.Max(existing.Tolerance, candidate.Tolerance);
            if (candidate.CurrentUnits > existing.CurrentUnits &&
                candidate.RenderedFraction + tolerance < existing.RenderedFraction)
            {
                return false;
            }

            if (candidate.CurrentUnits < existing.CurrentUnits &&
                candidate.RenderedFraction - tolerance > existing.RenderedFraction)
            {
                return false;
            }
        }

        return true;
    }

    private bool HasCompleteProof()
    {
        if (maximumUnits == 0 || !points.ContainsKey(0))
        {
            return false;
        }

        var nonTerminal = points.Values
            .Where(point => point.CurrentUnits > 0 && point.CurrentUnits < maximumUnits)
            .ToArray();
        return nonTerminal.Length >= 2 && HasSeparatedNonTerminalSamples(nonTerminal);
    }

    private static bool HasSeparatedNonTerminalSamples(IReadOnlyList<CalibrationPoint> samples)
    {
        for (var leftIndex = 0; leftIndex < samples.Count; leftIndex++)
        {
            for (var rightIndex = leftIndex + 1; rightIndex < samples.Count; rightIndex++)
            {
                var left = samples[leftIndex];
                var right = samples[rightIndex];
                if (left.CurrentUnits == right.CurrentUnits) continue;

                var separation = Math.Abs(left.RenderedFraction - right.RenderedFraction);
                if (separation > Math.Max(left.Tolerance, right.Tolerance)) return true;
            }
        }

        return false;
    }

    private readonly record struct CalibrationPoint(
        uint CurrentUnits,
        double RenderedFraction,
        double Tolerance);
}

public static class CombatFrameLimitGaugeRules
{
    public const int SelfSlot = 0;
    public const int FirstEnemySlot = 1;
    public const int LastEnemySlot = 5;
    public const float MinimumRenderedExtent = 2f;
    public const float MaximumRenderedExtent = 10_000f;
    public const float BoundsContainmentTolerancePixels = 2f;
    public const float MinimumRatioTolerance = 0.015f;
    public const float MaximumRatioTolerance = 0.08f;
    public const float QuantizationTolerancePixels = 1.5f;

    public static bool TryGetRenderedFraction(
        in CombatFrameLimitGaugeNativeMeasurement measurement,
        out double fraction,
        out double tolerance)
    {
        fraction = 0;
        tolerance = 0;
        if (!IsValidTrackBounds(measurement.TrackBounds)) return false;

        var trackWidth = measurement.TrackBounds.Width;
        tolerance = Math.Clamp(
            QuantizationTolerancePixels / trackWidth,
            MinimumRatioTolerance,
            MaximumRatioTolerance);

        if (!measurement.FillVisible) return true;
        if (!IsValidFillBounds(measurement.FillBounds) ||
            !IsContained(measurement.TrackBounds, measurement.FillBounds))
        {
            return false;
        }

        var rawFraction = measurement.FillBounds.Width / trackWidth;
        if (!double.IsFinite(rawFraction) || rawFraction < 0 || rawFraction > 1d + tolerance)
            return false;

        fraction = Math.Clamp(rawFraction, 0d, 1d);
        return true;
    }

    public static CombatFrameLimitGaugeReading ExactSelf(float fraction) =>
        float.IsFinite(fraction) && fraction is >= 0f and <= 1f
            ? new CombatFrameLimitGaugeReading(
                SelfSlot,
                CombatFrameLimitGaugeTrust.ExactLocalController,
                fraction)
            : CombatFrameLimitGaugeReading.Unknown(SelfSlot);

    public static CombatFrameLimitGaugeReading CalibratedEnemy(int slot, float fraction) =>
        slot is >= FirstEnemySlot and <= LastEnemySlot &&
        float.IsFinite(fraction) &&
        fraction is >= 0f and <= 1f
            ? new CombatFrameLimitGaugeReading(
                slot,
                CombatFrameLimitGaugeTrust.CalibratedNativeHud,
                fraction)
            : CombatFrameLimitGaugeReading.Unknown(slot);

    public static CombatFrameLimitGaugeReading[] UnknownEnemies() =>
    [
        CombatFrameLimitGaugeReading.Unknown(1),
        CombatFrameLimitGaugeReading.Unknown(2),
        CombatFrameLimitGaugeReading.Unknown(3),
        CombatFrameLimitGaugeReading.Unknown(4),
        CombatFrameLimitGaugeReading.Unknown(5),
    ];

    private static bool IsValidTrackBounds(in CombatFrameLimitGaugeRenderedBounds bounds) =>
        float.IsFinite(bounds.MinimumX) &&
        float.IsFinite(bounds.MinimumY) &&
        float.IsFinite(bounds.MaximumX) &&
        float.IsFinite(bounds.MaximumY) &&
        bounds.Width is > MinimumRenderedExtent and < MaximumRenderedExtent &&
        bounds.Height is > MinimumRenderedExtent and < MaximumRenderedExtent;

    private static bool IsValidFillBounds(in CombatFrameLimitGaugeRenderedBounds bounds) =>
        float.IsFinite(bounds.MinimumX) &&
        float.IsFinite(bounds.MinimumY) &&
        float.IsFinite(bounds.MaximumX) &&
        float.IsFinite(bounds.MaximumY) &&
        bounds.Width is >= 0f and < MaximumRenderedExtent &&
        bounds.Height is > MinimumRenderedExtent and < MaximumRenderedExtent;

    private static bool IsContained(
        in CombatFrameLimitGaugeRenderedBounds track,
        in CombatFrameLimitGaugeRenderedBounds fill) =>
        fill.MinimumX >= track.MinimumX - BoundsContainmentTolerancePixels &&
        fill.MaximumX <= track.MaximumX + BoundsContainmentTolerancePixels &&
        fill.MinimumY >= track.MinimumY - BoundsContainmentTolerancePixels &&
        fill.MaximumY <= track.MaximumY + BoundsContainmentTolerancePixels;
}
