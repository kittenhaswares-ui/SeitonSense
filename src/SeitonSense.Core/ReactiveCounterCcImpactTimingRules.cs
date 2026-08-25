namespace SeitonSense.Core;

public sealed record ReactiveCounterCcImpactSample
{
    public ReactiveCounterCcImpactSample()
    {
    }

    public ReactiveCounterCcImpactSample(
        int delayMilliseconds,
        int edgeDistanceCentiyalms)
    {
        DelayMilliseconds = delayMilliseconds;
        EdgeDistanceCentiyalms = edgeDistanceCentiyalms;
    }

    public int DelayMilliseconds { get; set; }
    public int EdgeDistanceCentiyalms { get; set; }
}

/// <summary>
/// Pure calibration policy for protection-end counter-CC. The server
/// ActionEffect boundary is the only timing sample: animation-sheet rows do
/// not encode the real request-to-impact delay. Each persisted sample keeps
/// its exact measured distance. A current target can use only equal-or-nearer
/// samples, so a slower far-range projectile can never teach a premature lead
/// for a near-range use.
/// </summary>
public static class ReactiveCounterCcImpactTimingRules
{
    public const int CalibrationRevision = 1;
    public const int DistanceUnitsPerYalm = 100;
    public const int MaximumStoredEdgeDistanceCentiyalms = 5_000;
    public const int MaximumSamplesPerAction = 24;
    public const int MinimumSamplesForPrediction = 5;
    public const int MinimumSampleMilliseconds = 50;
    public const int MaximumSampleMilliseconds = 1_500;
    public const int LandingSafetyMarginMilliseconds = 175;
    public const int MinimumUsefulLeadMilliseconds = 25;
    public const int ScheduleDriftToleranceMilliseconds = 175;

    public static bool TryCreateCalibrationSample(
        int delayMilliseconds,
        float edgeDistanceYalms,
        out ReactiveCounterCcImpactSample sample)
    {
        sample = new ReactiveCounterCcImpactSample();
        if (!IsValidDelay(delayMilliseconds) ||
            !TryCeilingDistance(edgeDistanceYalms, out var distanceCentiyalms))
        {
            return false;
        }

        sample = new ReactiveCounterCcImpactSample(
            delayMilliseconds,
            distanceCentiyalms);
        return true;
    }

    public static bool IsSupportedAction(uint actionId) => actionId is
        MiracleInterceptConfirmationRules.MiracleOfNatureActionId or
        MiracleInterceptConfirmationRules.SilentNocturneActionId or
        MiracleInterceptConfirmationRules.ForkedRaijuActionId or
        MiracleInterceptConfirmationRules.FleetingRaijuActionId or
        MiracleInterceptConfirmationRules.InterveneActionId or
        SamuraiReactiveCounterCcRules.SotenActionId or
        MiracleInterceptConfirmationRules.MineuchiActionId or
        MiracleInterceptConfirmationRules.ResolutionActionId or
        MiracleInterceptConfirmationRules.ViceOfThornsActionId or
        MiracleInterceptConfirmationRules.FrostStarActionId;

    public static bool TryMeasureSample(
        uint expectedActionId,
        uint expectedTargetEntityId,
        ushort expectedSourceSequence,
        long attemptedAtMilliseconds,
        uint observedActionId,
        uint observedTargetEntityId,
        ushort observedSourceSequence,
        long observedAtMilliseconds,
        out int sampleMilliseconds)
    {
        sampleMilliseconds = 0;
        if (!IsSupportedAction(expectedActionId) ||
            expectedActionId != observedActionId ||
            !MiracleInterceptConfirmationRules.IsValidEntityId(expectedTargetEntityId) ||
            expectedTargetEntityId != observedTargetEntityId ||
            expectedSourceSequence == 0 ||
            observedSourceSequence == 0 ||
            expectedSourceSequence != observedSourceSequence ||
            attemptedAtMilliseconds < 0 ||
            observedAtMilliseconds < attemptedAtMilliseconds)
        {
            return false;
        }

        var elapsed = observedAtMilliseconds - attemptedAtMilliseconds;
        if (elapsed is < MinimumSampleMilliseconds or > MaximumSampleMilliseconds)
            return false;

        sampleMilliseconds = (int)elapsed;
        return true;
    }

    public static ReactiveCounterCcImpactSample[] AppendBoundedSample(
        IReadOnlyList<ReactiveCounterCcImpactSample>? previous,
        ReactiveCounterCcImpactSample sample)
    {
        if (!IsValidSample(sample)) return NormalizeSamples(previous);

        var valid = NormalizeSamples(previous)
            .TakeLast(MaximumSamplesPerAction - 1)
            .Append(new ReactiveCounterCcImpactSample(
                sample.DelayMilliseconds,
                sample.EdgeDistanceCentiyalms))
            .Where(IsValidSample)
            .TakeLast(MaximumSamplesPerAction)
            .ToArray();
        return valid;
    }

    public static ReactiveCounterCcImpactSample[] NormalizeSamples(
        IReadOnlyList<ReactiveCounterCcImpactSample>? samples) =>
        (samples ?? Array.Empty<ReactiveCounterCcImpactSample>())
            .Where(IsValidSample)
            .TakeLast(MaximumSamplesPerAction)
            .Select(static sample => new ReactiveCounterCcImpactSample(
                sample.DelayMilliseconds,
                sample.EdgeDistanceCentiyalms))
            .ToArray();

    /// <summary>
    /// Uses the fastest observed server effect as the conservative network
    /// floor. A slow sample can therefore never teach an earlier request; a
    /// newly observed faster path can only move the request later. Five exact
    /// source-sequence samples reduce sparse first-use stall risk; they cannot
    /// mathematically bound an as-yet unseen faster effect. The generic lane
    /// therefore also requires one directionally eligible sample from the
    /// current runtime session before persisted history may predict.
    /// The additional margin deliberately aims just after immunity expiry.
    /// </summary>
    public static bool TryGetSafeLeadMilliseconds(
        IReadOnlyList<ReactiveCounterCcImpactSample>? samples,
        float currentEdgeDistanceYalms,
        out int leadMilliseconds)
    {
        leadMilliseconds = 0;
        if (!TryFloorDistance(
                currentEdgeDistanceYalms,
                out var currentDistanceCentiyalms))
        {
            return false;
        }

        var sorted = (samples ?? Array.Empty<ReactiveCounterCcImpactSample>())
            .Where(IsValidSample)
            .Where(sample =>
                sample.EdgeDistanceCentiyalms <= currentDistanceCentiyalms)
            .Select(static sample => sample.DelayMilliseconds)
            .Order()
            .Take(MaximumSamplesPerAction)
            .ToArray();
        if (sorted.Length < MinimumSamplesForPrediction) return false;

        var lead = sorted[0] - LandingSafetyMarginMilliseconds;
        if (lead < MinimumUsefulLeadMilliseconds) return false;
        leadMilliseconds = Math.Min(lead, MaximumSampleMilliseconds);
        return true;
    }

    public static bool TryGetSafeLeadMilliseconds(
        IReadOnlyList<ReactiveCounterCcImpactSample>? persistedAndCurrentSamples,
        IReadOnlyList<ReactiveCounterCcImpactSample>? currentSessionSamples,
        float currentEdgeDistanceYalms,
        out int leadMilliseconds)
    {
        leadMilliseconds = 0;
        if (!HasEligibleCurrentSessionSample(
                currentSessionSamples,
                currentEdgeDistanceYalms))
        {
            return false;
        }

        return TryGetSafeLeadMilliseconds(
            persistedAndCurrentSamples,
            currentEdgeDistanceYalms,
            out leadMilliseconds);
    }

    public static bool HasEligibleCurrentSessionSample(
        IReadOnlyList<ReactiveCounterCcImpactSample>? currentSessionSamples,
        float currentEdgeDistanceYalms)
    {
        if (!TryFloorDistance(
                currentEdgeDistanceYalms,
                out var currentDistanceCentiyalms))
        {
            return false;
        }

        return (currentSessionSamples ?? Array.Empty<ReactiveCounterCcImpactSample>())
            .Any(sample =>
                IsValidSample(sample) &&
                sample.EdgeDistanceCentiyalms <= currentDistanceCentiyalms);
    }

    public static bool ShouldPreDispatch(
        int exactProtectionStatusCount,
        long protectionRemainingMilliseconds,
        int safeLeadMilliseconds) =>
        exactProtectionStatusCount == 1 &&
        protectionRemainingMilliseconds > 0 &&
        safeLeadMilliseconds >= MinimumUsefulLeadMilliseconds &&
        protectionRemainingMilliseconds <= safeLeadMilliseconds;

    public static bool IsScheduledProtectionStillValid(
        long scheduledProtectionEndAtMilliseconds,
        long nowMilliseconds,
        long currentProtectionRemainingMilliseconds,
        int safeLeadMilliseconds)
    {
        if (scheduledProtectionEndAtMilliseconds <= 0 ||
            nowMilliseconds < 0 ||
            currentProtectionRemainingMilliseconds <= 0 ||
            safeLeadMilliseconds < MinimumUsefulLeadMilliseconds)
        {
            return false;
        }

        var currentEnd = SaturatingAdd(
            nowMilliseconds,
            currentProtectionRemainingMilliseconds);
        var drift = currentEnd >= scheduledProtectionEndAtMilliseconds
            ? currentEnd - scheduledProtectionEndAtMilliseconds
            : scheduledProtectionEndAtMilliseconds - currentEnd;
        return currentProtectionRemainingMilliseconds <= safeLeadMilliseconds &&
               drift <= ScheduleDriftToleranceMilliseconds;
    }

    private static bool IsValidSample(ReactiveCounterCcImpactSample? sample) =>
        sample is not null &&
        IsValidDelay(sample.DelayMilliseconds) &&
        sample.EdgeDistanceCentiyalms is >= 0 and
            <= MaximumStoredEdgeDistanceCentiyalms;

    private static bool IsValidDelay(int value) =>
        value is >= MinimumSampleMilliseconds and <= MaximumSampleMilliseconds;

    private static bool TryCeilingDistance(
        float edgeDistanceYalms,
        out int distanceCentiyalms) =>
        TryQuantizeDistance(edgeDistanceYalms, useCeiling: true, out distanceCentiyalms);

    private static bool TryFloorDistance(
        float edgeDistanceYalms,
        out int distanceCentiyalms) =>
        TryQuantizeDistance(edgeDistanceYalms, useCeiling: false, out distanceCentiyalms);

    private static bool TryQuantizeDistance(
        float edgeDistanceYalms,
        bool useCeiling,
        out int distanceCentiyalms)
    {
        distanceCentiyalms = 0;
        if (!float.IsFinite(edgeDistanceYalms) || edgeDistanceYalms < 0f)
            return false;

        var scaled = (double)edgeDistanceYalms * DistanceUnitsPerYalm;
        if (scaled > MaximumStoredEdgeDistanceCentiyalms) return false;
        distanceCentiyalms = (int)(useCeiling
            ? Math.Ceiling(scaled)
            : Math.Floor(scaled));
        return true;
    }

    private static long SaturatingAdd(long left, long right) =>
        left > long.MaxValue - right ? long.MaxValue : left + right;
}
