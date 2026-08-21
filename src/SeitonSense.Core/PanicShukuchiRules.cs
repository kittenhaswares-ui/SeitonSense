namespace SeitonSense.Core;

public readonly record struct PanicShukuchiPoint(float X, float Y, float Z)
{
    public bool IsFinite =>
        float.IsFinite(X) &&
        float.IsFinite(Y) &&
        float.IsFinite(Z);
}

/// <summary>
/// A positively observed ground collision at the requested forward probe.
/// Missing or invalid collision data is never replaced with the player's Y
/// coordinate or with a shorter fallback position.
/// </summary>
public readonly record struct PanicShukuchiGroundHit(
    bool ExactGroundHit,
    PanicShukuchiPoint Position);

public readonly record struct PanicShukuchiCandidate(
    PanicShukuchiPoint Origin,
    float RotationRadians,
    PanicShukuchiGroundHit GroundHit);

public readonly record struct PanicShukuchiIntent(
    uint ActionId,
    PanicShukuchiPoint Destination)
{
    public bool IsValid =>
        ActionId == PanicShukuchiRules.ActionId &&
        Destination.IsFinite;
}

/// <summary>
/// Synchronous facts for one explicit /panicshu command. There are deliberately
/// no Guard, crowd-control, cast, queue, animation-lock, clock, or pending-state
/// inputs: the command either produces one immediate intent or ends.
/// </summary>
public readonly record struct PanicShukuchiCommandObservation(
    bool PluginEnabled,
    bool MetadataVerified,
    SupportedPvPContext Context,
    bool WolvesDenTestingEnabled,
    uint LocalJobId,
    bool LocalPlayerAliveAndTargetable,
    uint ResolvedActionId,
    PanicShukuchiCandidate Candidate);

public enum PanicShukuchiDecisionReason
{
    None = 0,
    PluginDisabled = 1,
    MetadataUnverified = 2,
    UnsupportedContext = 3,
    InvalidLocalPlayer = 4,
    WrongJob = 5,
    ResolvedActionInvalid = 6,
    InvalidForwardGroundHit = 7,
    Ready = 8,
}

public readonly record struct PanicShukuchiCommandDecision(
    PanicShukuchiDecisionReason Reason,
    PanicShukuchiIntent? Intent = null)
{
    public bool ShouldAttempt =>
        Reason == PanicShukuchiDecisionReason.Ready &&
        Intent is { IsValid: true };
}

/// <summary>
/// Pure fail-closed policy for one explicit /panicshu invocation. It validates
/// one 19.5-yalm forward terrain point and returns one immediate intent. It has
/// no lease, scheduler, wait, retry, inward fallback, destination recomputation,
/// target substitution, or relationship to held-action helpers.
/// </summary>
public static class PanicShukuchiRules
{
    public const uint NinjaJobId = 30;
    public const uint ActionId = 29_513;
    public const float NativeMaximumRangeYalms = 20f;
    public const float SafeForwardDistanceYalms = 19.5f;

    // Collision implementations can introduce tiny horizontal rounding while
    // returning the surface Y. This tolerance cannot turn a materially shorter
    // or off-axis destination into an eligible command intent.
    public const float MaximumGroundHorizontalErrorYalms = 0.05f;

    public static bool IsSupportedContext(
        SupportedPvPContext context,
        bool wolvesDenTestingEnabled) =>
        context == SupportedPvPContext.CrystallineConflict ||
        (wolvesDenTestingEnabled && context == SupportedPvPContext.WolvesDen);

    /// <summary>
    /// Produces the sole horizontal probe point using FFXIV's rotation axes:
    /// forward X is sin(rotation), forward Z is cos(rotation).
    /// </summary>
    public static bool TryCreateForwardProbe(
        PanicShukuchiPoint origin,
        float rotationRadians,
        out PanicShukuchiPoint probe)
    {
        probe = default;
        if (!origin.IsFinite || !float.IsFinite(rotationRadians)) return false;

        var forwardX = MathF.Sin(rotationRadians);
        var forwardZ = MathF.Cos(rotationRadians);
        if (!float.IsFinite(forwardX) || !float.IsFinite(forwardZ)) return false;

        probe = new PanicShukuchiPoint(
            origin.X + (forwardX * SafeForwardDistanceYalms),
            origin.Y,
            origin.Z + (forwardZ * SafeForwardDistanceYalms));
        if (!probe.IsFinite) return false;

        // Very large finite coordinates can erase the requested displacement
        // when rounded to float. Such telemetry is unusable and fails closed.
        return IsApproximatelySafeHorizontalDistance(origin, probe);
    }

    public static bool IsValidGroundHit(PanicShukuchiCandidate candidate)
    {
        if (!candidate.GroundHit.ExactGroundHit ||
            !candidate.Origin.IsFinite ||
            !candidate.GroundHit.Position.IsFinite ||
            !float.IsFinite(candidate.RotationRadians) ||
            !TryCreateForwardProbe(
                candidate.Origin,
                candidate.RotationRadians,
                out var probe))
        {
            return false;
        }

        var hit = candidate.GroundHit.Position;
        if (Math.Abs((double)hit.X - probe.X) > MaximumGroundHorizontalErrorYalms ||
            Math.Abs((double)hit.Z - probe.Z) > MaximumGroundHorizontalErrorYalms ||
            !IsApproximatelySafeHorizontalDistance(candidate.Origin, hit))
        {
            return false;
        }

        var deltaX = (double)hit.X - candidate.Origin.X;
        var deltaY = (double)hit.Y - candidate.Origin.Y;
        var deltaZ = (double)hit.Z - candidate.Origin.Z;
        var distanceSquared = (deltaX * deltaX) +
                              (deltaY * deltaY) +
                              (deltaZ * deltaZ);
        return double.IsFinite(distanceSquared) &&
               distanceSquared <=
               (double)NativeMaximumRangeYalms * NativeMaximumRangeYalms;
    }

    public static PanicShukuchiCommandDecision Evaluate(
        PanicShukuchiCommandObservation observation)
    {
        if (!observation.PluginEnabled)
            return Rejected(PanicShukuchiDecisionReason.PluginDisabled);
        if (!observation.MetadataVerified)
            return Rejected(PanicShukuchiDecisionReason.MetadataUnverified);
        if (!IsSupportedContext(observation.Context, observation.WolvesDenTestingEnabled))
            return Rejected(PanicShukuchiDecisionReason.UnsupportedContext);
        if (!observation.LocalPlayerAliveAndTargetable)
            return Rejected(PanicShukuchiDecisionReason.InvalidLocalPlayer);
        if (observation.LocalJobId != NinjaJobId)
            return Rejected(PanicShukuchiDecisionReason.WrongJob);
        if (observation.ResolvedActionId != ActionId)
            return Rejected(PanicShukuchiDecisionReason.ResolvedActionInvalid);
        if (!IsValidGroundHit(observation.Candidate))
            return Rejected(PanicShukuchiDecisionReason.InvalidForwardGroundHit);

        return new PanicShukuchiCommandDecision(
            PanicShukuchiDecisionReason.Ready,
            new PanicShukuchiIntent(
                ActionId,
                observation.Candidate.GroundHit.Position));
    }

    private static PanicShukuchiCommandDecision Rejected(
        PanicShukuchiDecisionReason reason) =>
        new(reason);

    private static bool IsApproximatelySafeHorizontalDistance(
        PanicShukuchiPoint origin,
        PanicShukuchiPoint destination)
    {
        var deltaX = (double)destination.X - origin.X;
        var deltaZ = (double)destination.Z - origin.Z;
        var horizontalDistanceSquared = (deltaX * deltaX) + (deltaZ * deltaZ);
        if (!double.IsFinite(horizontalDistanceSquared)) return false;

        var minimum = SafeForwardDistanceYalms - MaximumGroundHorizontalErrorYalms;
        var maximum = SafeForwardDistanceYalms + MaximumGroundHorizontalErrorYalms;
        return horizontalDistanceSquared >= (double)minimum * minimum &&
               horizontalDistanceSquared <= (double)maximum * maximum;
    }
}
