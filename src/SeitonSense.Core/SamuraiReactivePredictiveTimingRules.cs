namespace SeitonSense.Core;

public readonly record struct SamuraiReactivePredictiveTiming(
    int SotenTransitMilliseconds,
    int MineuchiSafeLeadMilliseconds,
    int CombinedSotenLeadMilliseconds)
{
    public bool IsValid =>
        SotenTransitMilliseconds >=
            ReactiveCounterCcImpactTimingRules.MinimumSampleMilliseconds &&
        MineuchiSafeLeadMilliseconds >=
            ReactiveCounterCcImpactTimingRules.MinimumUsefulLeadMilliseconds &&
        CombinedSotenLeadMilliseconds ==
            SotenTransitMilliseconds + MineuchiSafeLeadMilliseconds;
}

/// <summary>
/// Measured-only scheduler for SAM's two-stage Soten -> Mineuchi path.
/// Soten needs a conservative arrival estimate, so only equally distant or
/// farther exact samples may teach the current approach and the slower of the
/// two nearest qualifying observations wins. Mineuchi uses the shared
/// fastest-observed impact floor so its ActionEffect is aimed just after the
/// exact protection end. Missing evidence never invents a travel delay.
/// </summary>
public static class SamuraiReactivePredictiveTimingRules
{
    // Prediction is intentionally warmed from exact sequence-bound effects.
    // Soten needs two current-session transit observations; Mineuchi needs five
    // total observations plus the caller's current-session distance proof.
    public const int MinimumSotenTransitSamplesForPrediction = 2;
    public const int MinimumMineuchiSamplesForPrediction = 5;

    public static bool TryGetSotenTransitMilliseconds(
        IReadOnlyList<ReactiveCounterCcImpactSample>? samples,
        float currentEdgeDistanceYalms,
        out int transitMilliseconds)
    {
        transitMilliseconds = 0;
        if (!TryCeilingDistanceCentiyalms(
                currentEdgeDistanceYalms,
                out var currentDistanceCentiyalms))
        {
            return false;
        }

        var nearestSafeSamples = ReactiveCounterCcImpactTimingRules
            .NormalizeSamples(samples)
            .Where(sample =>
                sample.EdgeDistanceCentiyalms >= currentDistanceCentiyalms)
            .OrderBy(static sample => sample.EdgeDistanceCentiyalms)
            .ThenByDescending(static sample => sample.DelayMilliseconds)
            .Take(MinimumSotenTransitSamplesForPrediction)
            .ToArray();
        if (nearestSafeSamples.Length < MinimumSotenTransitSamplesForPrediction)
            return false;

        transitMilliseconds = nearestSafeSamples.Max(
            static sample => sample.DelayMilliseconds);
        return transitMilliseconds >=
               ReactiveCounterCcImpactTimingRules.MinimumSampleMilliseconds;
    }

    public static bool TryGetCombinedTiming(
        IReadOnlyList<ReactiveCounterCcImpactSample>? sotenSamples,
        float currentSotenEdgeDistanceYalms,
        IReadOnlyList<ReactiveCounterCcImpactSample>? mineuchiSamples,
        out SamuraiReactivePredictiveTiming timing)
    {
        timing = default;
        if (!TryGetSotenTransitMilliseconds(
                sotenSamples,
                currentSotenEdgeDistanceYalms,
                out var sotenTransitMilliseconds) ||
            !TryGetMineuchiSafeLeadMilliseconds(
                mineuchiSamples,
                SamuraiReactiveCounterCcRules.MineuchiMaximumRangeYalms,
                out var mineuchiSafeLeadMilliseconds))
        {
            return false;
        }

        var combined = (long)sotenTransitMilliseconds +
                       mineuchiSafeLeadMilliseconds;
        if (combined > int.MaxValue) return false;
        timing = new SamuraiReactivePredictiveTiming(
            sotenTransitMilliseconds,
            mineuchiSafeLeadMilliseconds,
            (int)combined);
        return timing.IsValid;
    }

    public static bool TryGetMineuchiSafeLeadMilliseconds(
        IReadOnlyList<ReactiveCounterCcImpactSample>? samples,
        float currentEdgeDistanceYalms,
        out int leadMilliseconds)
    {
        leadMilliseconds = 0;
        if (!TryFloorDistanceCentiyalms(
                currentEdgeDistanceYalms,
                out var currentDistanceCentiyalms))
        {
            return false;
        }

        var eligibleSamples = ReactiveCounterCcImpactTimingRules
            .NormalizeSamples(samples)
            .Where(sample =>
                sample.EdgeDistanceCentiyalms <= currentDistanceCentiyalms)
            .ToArray();
        return eligibleSamples.Length >= MinimumMineuchiSamplesForPrediction &&
               ReactiveCounterCcImpactTimingRules.TryGetSafeLeadMilliseconds(
                   eligibleSamples,
                   currentEdgeDistanceYalms,
                   out leadMilliseconds);
    }

    public static int CountEligibleSotenTransitSamples(
        IReadOnlyList<ReactiveCounterCcImpactSample>? samples,
        float currentEdgeDistanceYalms)
    {
        if (!TryCeilingDistanceCentiyalms(
                currentEdgeDistanceYalms,
                out var currentDistanceCentiyalms))
        {
            return 0;
        }

        return ReactiveCounterCcImpactTimingRules.NormalizeSamples(samples)
            .Count(sample =>
                sample.EdgeDistanceCentiyalms >= currentDistanceCentiyalms);
    }

    public static int CountEligibleMineuchiSamples(
        IReadOnlyList<ReactiveCounterCcImpactSample>? samples,
        float currentEdgeDistanceYalms)
    {
        if (!TryFloorDistanceCentiyalms(
                currentEdgeDistanceYalms,
                out var currentDistanceCentiyalms))
        {
            return 0;
        }

        return ReactiveCounterCcImpactTimingRules.NormalizeSamples(samples)
            .Count(sample =>
                sample.EdgeDistanceCentiyalms <= currentDistanceCentiyalms);
    }

    public static bool ShouldStartPredictiveSoten(
        int exactProtectionStatusCount,
        long protectionRemainingMilliseconds,
        SamuraiReactivePredictiveTiming timing) =>
        timing.IsValid &&
        ReactiveCounterCcImpactTimingRules.ShouldPreDispatch(
            exactProtectionStatusCount,
            protectionRemainingMilliseconds,
            timing.CombinedSotenLeadMilliseconds);

    public static bool ShouldStartPredictiveMineuchi(
        int exactProtectionStatusCount,
        long protectionRemainingMilliseconds,
        int mineuchiSafeLeadMilliseconds) =>
        ReactiveCounterCcImpactTimingRules.ShouldPreDispatch(
            exactProtectionStatusCount,
            protectionRemainingMilliseconds,
            mineuchiSafeLeadMilliseconds);

    private static bool TryCeilingDistanceCentiyalms(
        float edgeDistanceYalms,
        out int distanceCentiyalms)
    {
        distanceCentiyalms = 0;
        if (!float.IsFinite(edgeDistanceYalms) || edgeDistanceYalms < 0f)
            return false;
        var scaled = (double)edgeDistanceYalms *
                     ReactiveCounterCcImpactTimingRules.DistanceUnitsPerYalm;
        if (scaled > ReactiveCounterCcImpactTimingRules
                .MaximumStoredEdgeDistanceCentiyalms)
        {
            return false;
        }

        distanceCentiyalms = (int)Math.Ceiling(scaled);
        return true;
    }

    private static bool TryFloorDistanceCentiyalms(
        float edgeDistanceYalms,
        out int distanceCentiyalms)
    {
        distanceCentiyalms = 0;
        if (!float.IsFinite(edgeDistanceYalms) || edgeDistanceYalms < 0f)
            return false;
        var scaled = (double)edgeDistanceYalms *
                     ReactiveCounterCcImpactTimingRules.DistanceUnitsPerYalm;
        if (scaled > ReactiveCounterCcImpactTimingRules
                .MaximumStoredEdgeDistanceCentiyalms)
        {
            return false;
        }

        distanceCentiyalms = (int)Math.Floor(scaled);
        return true;
    }
}
