namespace SeitonSense.Core;

/// <summary>
/// Frozen local identity for movement sampling and one explicit
/// /seitonenavant command. A sample from another actor, territory, or job can
/// never supply a dash direction.
/// </summary>
public readonly record struct MovementDirectedEnAvantFingerprint(
    uint TerritoryId,
    ulong LocalActorAddress,
    ulong LocalGameObjectId,
    uint LocalEntityId,
    uint LocalJobId)
{
    public bool IsValid =>
        TerritoryId != 0 &&
        LocalActorAddress != 0 &&
        LocalGameObjectId is not 0 and not 0xE0000000UL and not ulong.MaxValue &&
        LocalEntityId is not 0 and not 0xE0000000U and not uint.MaxValue &&
        LocalJobId == BackwardDashRules.DancerJobId;
}

public readonly record struct MovementDirectedEnAvantSample(
    MovementDirectedEnAvantFingerprint Fingerprint,
    float PositionX,
    float PositionZ,
    long ObservedAtMilliseconds)
{
    public bool IsValid =>
        Fingerprint.IsValid &&
        float.IsFinite(PositionX) &&
        float.IsFinite(PositionZ) &&
        ObservedAtMilliseconds >= 0;
}

public readonly record struct MovementDirectedEnAvantState(
    MovementDirectedEnAvantSample LastSample,
    float HeadingRadians,
    float ConsistentDistanceYalms,
    int ConsistentSegmentCount,
    long LastMovementAtMilliseconds)
{
    public static MovementDirectedEnAvantState Initial =>
        new(default, float.NaN, 0f, 0, -1);

    public bool HasDirection =>
        LastSample.IsValid &&
        float.IsFinite(HeadingRadians) &&
        float.IsFinite(ConsistentDistanceYalms) &&
        ConsistentDistanceYalms >=
            MovementDirectedEnAvantRules.MinimumConsistentDistanceYalms &&
        ConsistentSegmentCount >=
            MovementDirectedEnAvantRules.RequiredConsistentSegmentCount &&
        LastMovementAtMilliseconds >= 0;
}

public readonly record struct MovementDirectedEnAvantSnapshot(
    MovementDirectedEnAvantFingerprint Fingerprint,
    float HeadingRadians,
    long ObservedAtMilliseconds,
    int ConsistentSegmentCount,
    float ConsistentDistanceYalms)
{
    public bool IsValid =>
        Fingerprint.IsValid &&
        float.IsFinite(HeadingRadians) &&
        ObservedAtMilliseconds >= 0 &&
        ConsistentSegmentCount >=
            MovementDirectedEnAvantRules.RequiredConsistentSegmentCount &&
        float.IsFinite(ConsistentDistanceYalms) &&
        ConsistentDistanceYalms >=
            MovementDirectedEnAvantRules.MinimumConsistentDistanceYalms;
}

/// <summary>
/// Pure world-displacement policy for /seitonenavant. It deliberately uses
/// the character's recent actual horizontal path rather than camera facing,
/// actor facing, hard-coded keys, or a target. Two consecutive meaningful
/// displacement segments must agree before a direction becomes available;
/// finite sub-threshold frame deltas accumulate against the last meaningful
/// anchor. Stale, stationary,
/// discontinuous, teleport-sized, non-finite, or cross-identity observations
/// expose no fallback direction.
/// </summary>
public static class MovementDirectedEnAvantRules
{
    public const uint ActionId = 29_430;
    public const int RequiredConsistentSegmentCount = 2;
    public const long MaximumSampleGapMilliseconds = 150;
    public const long MaximumDirectionAgeMilliseconds = 150;
    public const float MinimumSegmentDistanceYalms = 0.005f;
    public const float MaximumSegmentDistanceYalms = 1.5f;
    public const float MinimumConsistentDistanceYalms = 0.025f;
    public const float MaximumSegmentHeadingDeltaRadians = MathF.PI / 4f;

    public static MovementDirectedEnAvantState Observe(
        MovementDirectedEnAvantState previous,
        MovementDirectedEnAvantSample sample)
    {
        if (!sample.IsValid) return MovementDirectedEnAvantState.Initial;

        if (!previous.LastSample.IsValid ||
            previous.LastSample.Fingerprint != sample.Fingerprint)
        {
            return Baseline(sample);
        }

        var elapsed = sample.ObservedAtMilliseconds -
                      previous.LastSample.ObservedAtMilliseconds;
        if (elapsed <= 0 || elapsed > MaximumSampleGapMilliseconds)
            return Baseline(sample);

        var deltaX = (double)sample.PositionX - previous.LastSample.PositionX;
        var deltaZ = (double)sample.PositionZ - previous.LastSample.PositionZ;
        var distanceSquared = (deltaX * deltaX) + (deltaZ * deltaZ);
        if (!double.IsFinite(distanceSquared)) return Baseline(sample);

        var distance = Math.Sqrt(distanceSquared);
        if (!double.IsFinite(distance) || distance > MaximumSegmentDistanceYalms)
            return Baseline(sample);

        if (distance < MinimumSegmentDistanceYalms)
        {
            var directionAge = sample.ObservedAtMilliseconds -
                               previous.LastMovementAtMilliseconds;
            var isCleanBaseline =
                previous.ConsistentSegmentCount == 0 &&
                previous.ConsistentDistanceYalms == 0f &&
                !float.IsFinite(previous.HeadingRadians) &&
                previous.LastMovementAtMilliseconds < 0;
            var hasFreshPartialDirection =
                previous.ConsistentSegmentCount > 0 &&
                float.IsFinite(previous.HeadingRadians) &&
                previous.LastMovementAtMilliseconds >= 0;
            if (isCleanBaseline ||
                (hasFreshPartialDirection &&
                 directionAge >= 0 &&
                 directionAge <= MaximumDirectionAgeMilliseconds))
            {
                // Keep the last meaningful displacement anchor. Replacing it
                // on every tiny frame delta would make slow analog movement
                // permanently invisible instead of allowing finite movement
                // to accumulate into one reviewed segment.
                return previous;
            }

            return Baseline(sample);
        }

        var heading = NormalizeRadians((float)Math.Atan2(deltaX, deltaZ));
        if (!float.IsFinite(heading)) return Baseline(sample);

        var continuesDirection =
            previous.ConsistentSegmentCount > 0 &&
            HeadingDistance(previous.HeadingRadians, heading) <=
                MaximumSegmentHeadingDeltaRadians;
        var segmentCount = continuesDirection
            ? Math.Min(
                RequiredConsistentSegmentCount,
                previous.ConsistentSegmentCount + 1)
            : 1;
        var consistentDistance = continuesDirection
            ? Math.Min(
                MinimumConsistentDistanceYalms + MaximumSegmentDistanceYalms,
                previous.ConsistentDistanceYalms + (float)distance)
            : (float)distance;

        return new MovementDirectedEnAvantState(
            sample,
            heading,
            consistentDistance,
            segmentCount,
            sample.ObservedAtMilliseconds);
    }

    public static bool TryCapture(
        MovementDirectedEnAvantState state,
        MovementDirectedEnAvantFingerprint currentFingerprint,
        long nowMilliseconds,
        out MovementDirectedEnAvantSnapshot snapshot)
    {
        snapshot = default;
        if (!state.HasDirection ||
            !currentFingerprint.IsValid ||
            state.LastSample.Fingerprint != currentFingerprint)
        {
            return false;
        }

        var age = nowMilliseconds - state.LastMovementAtMilliseconds;
        if (age < 0 || age > MaximumDirectionAgeMilliseconds) return false;

        snapshot = new MovementDirectedEnAvantSnapshot(
            currentFingerprint,
            state.HeadingRadians,
            state.LastMovementAtMilliseconds,
            state.ConsistentSegmentCount,
            state.ConsistentDistanceYalms);
        return snapshot.IsValid;
    }

    public static bool MatchesCurrentIdentity(
        MovementDirectedEnAvantSnapshot snapshot,
        uint territoryId,
        ulong localActorAddress,
        ulong localGameObjectId,
        uint localEntityId,
        uint localJobId) =>
        snapshot.IsValid &&
        snapshot.Fingerprint == new MovementDirectedEnAvantFingerprint(
            territoryId,
            localActorAddress,
            localGameObjectId,
            localEntityId,
            localJobId);

    public static bool IsFreshSnapshot(
        MovementDirectedEnAvantSnapshot snapshot,
        long nowMilliseconds)
    {
        if (!snapshot.IsValid) return false;
        var age = nowMilliseconds - snapshot.ObservedAtMilliseconds;
        return age >= 0 && age <= MaximumDirectionAgeMilliseconds;
    }

    private static MovementDirectedEnAvantState Baseline(
        MovementDirectedEnAvantSample sample) =>
        new(sample, float.NaN, 0f, 0, -1);

    private static float HeadingDistance(float left, float right)
    {
        if (!float.IsFinite(left) || !float.IsFinite(right))
            return float.PositiveInfinity;
        return MathF.Abs(NormalizeRadians(left - right));
    }

    private static float NormalizeRadians(float radians)
    {
        if (!float.IsFinite(radians)) return float.NaN;
        var normalized = MathF.IEEERemainder(radians, 2f * MathF.PI);
        return normalized <= -MathF.PI ? normalized + (2f * MathF.PI) : normalized;
    }
}
