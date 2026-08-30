using System.Numerics;

namespace SeitonSense.Core;

/// <summary>
/// Closed current-PvP catalog for the two reviewed Samurai cast starters which
/// may deliberately retain Smart Action's frozen hidden target. Follow-up
/// actions are instant and remain on the ordinary Smart Action path.
/// </summary>
public static class SamuraiSmartActionCastRules
{
    public const uint SamuraiJobId = 34;

    public const uint OgiNamikiriActionId = 29_530;
    public const uint OgiNamikiriFollowUpActionId = 29_531;
    public const float OgiNamikiriEffectRangeYalms = 8f;
    public const float OgiNamikiriConeHalfAngleDegrees = 45f;

    public const uint TendoSetsugekkaCarrierActionId = 29_536;
    public const uint TendoSetsugekkaActionId = 41_454;
    public const uint TendoSetsugekkaFollowUpActionId = 41_455;

    public static bool IsOgiNamikiriConeAction(uint resolvedActionId) =>
        resolvedActionId is OgiNamikiriActionId or OgiNamikiriFollowUpActionId;

    public static bool IsReviewedBaseCastPair(
        uint rawActionId,
        uint resolvedActionId) =>
        (rawActionId == OgiNamikiriActionId &&
         resolvedActionId == OgiNamikiriActionId) ||
        ((rawActionId == TendoSetsugekkaCarrierActionId ||
          rawActionId == TendoSetsugekkaActionId) &&
         resolvedActionId == TendoSetsugekkaActionId);

    /// <summary>
    /// Candidate-local protection policy for the reviewed Ogi Namikiri cone.
    /// The selected actor may not carry any blocking protection. Incidental
    /// Guard, Cover, or invulnerability actors do not veto an otherwise useful
    /// cone, while an incidental Chiten actor that the cone can actually reach
    /// still blocks that candidate.
    /// </summary>
    public static bool IsOgiNamikiriProtectionSafe(
        Vector3 sourcePosition,
        SmartActionActorGeometry target,
        float effectRange,
        IReadOnlyList<SmartActionProtectedActor>? protectedActors,
        bool actionIgnoresGuard = false)
    {
        if (!IsFinite(sourcePosition) ||
            effectRange != OgiNamikiriEffectRangeYalms ||
            !SmartActionProtectionRules.IsDirectTargetSafe(
                target,
                protectedActors,
                actionIgnoresGuard))
        {
            return false;
        }

        var directionX = (double)target.Position.X - sourcePosition.X;
        var directionZ = (double)target.Position.Z - sourcePosition.Z;
        var directionLengthSquared =
            (directionX * directionX) + (directionZ * directionZ);
        if (!double.IsFinite(directionLengthSquared) ||
            directionLengthSquared <= 1e-12d)
        {
            return false;
        }

        var directionLength = Math.Sqrt(directionLengthSquared);
        var unitX = directionX / directionLength;
        var unitZ = directionZ / directionLength;
        var halfAngleRadians =
            OgiNamikiriConeHalfAngleDegrees * Math.PI / 180d;
        var coneSlope = Math.Tan(halfAngleRadians);
        var coneCosine = Math.Cos(halfAngleRadians);

        foreach (var protectedActor in protectedActors!)
        {
            if ((protectedActor.Kind & SmartActionProtectionKind.Chiten) == 0)
                continue;

            if (IntersectsOgiCone(
                    sourcePosition,
                    unitX,
                    unitZ,
                    coneSlope,
                    coneCosine,
                    effectRange,
                    protectedActor.Geometry))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IntersectsOgiCone(
        Vector3 sourcePosition,
        double unitX,
        double unitZ,
        double coneSlope,
        double coneCosine,
        double effectRange,
        SmartActionActorGeometry actor)
    {
        var offsetX = (double)actor.Position.X - sourcePosition.X;
        var offsetZ = (double)actor.Position.Z - sourcePosition.Z;
        var radius = (double)actor.HitboxRadius;
        var distanceSquared = (offsetX * offsetX) + (offsetZ * offsetZ);
        var maximumDistance = effectRange + radius;
        if (!double.IsFinite(distanceSquared) ||
            distanceSquared > maximumDistance * maximumDistance)
        {
            return false;
        }

        var forward = (offsetX * unitX) + (offsetZ * unitZ);
        if (forward < -radius) return false;

        var lateral = Math.Abs((offsetX * unitZ) - (offsetZ * unitX));
        // Expand the sloped cone edge by the actor circle's perpendicular
        // radius. Dividing by cos converts that distance to this lateral axis.
        var maximumLateral =
            Math.Max(0d, forward) * coneSlope + (radius / coneCosine);
        return lateral <= maximumLateral;
    }

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) &&
        float.IsFinite(value.Y) &&
        float.IsFinite(value.Z);
}
