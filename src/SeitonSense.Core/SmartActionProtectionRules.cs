using System.Numerics;

namespace SeitonSense.Core;

/// <summary>
/// Exact, reviewed protection semantics which make a harmful Smart Action
/// target blocked. Unknown status rows never acquire one of these meanings.
/// </summary>
public enum SmartActionProtectionKind : byte
{
    None = 0,
    Chiten = 1,
    Guard = 2,
    Covered = 3,
    Invulnerability = 4,
}

/// <summary>
/// Reviewed harmful-action geometry. Unsupported area shapes are deliberately
/// distinct so callers can fail closed whenever any protected actor exists.
/// </summary>
public enum SmartActionAttackShape : byte
{
    DirectSingleTarget = 1,
    TargetCenteredCircle = 2,
    UnsupportedAreaOfEffect = 3,
}

/// <summary>
/// Caller-resolved canonical CC actor geometry. Positions and hitboxes remain
/// value-only so the pure policy never reaches into native game state.
/// </summary>
public readonly record struct SmartActionActorGeometry(
    int EnemySlot,
    TargetPressureActorIdentity Actor,
    bool ExactCanonicalIdentity,
    Vector3 Position,
    float HitboxRadius);

/// <summary>
/// One canonical actor carrying one exact reviewed protection kind.
/// </summary>
public readonly record struct SmartActionProtectedActor(
    SmartActionActorGeometry Geometry,
    SmartActionProtectionKind Kind);

/// <summary>
/// Pure fail-closed protection policy for one Smart Action target. The caller
/// must provide the complete current set of exact protected enemy actors.
/// </summary>
public static class SmartActionProtectionRules
{
    public const uint ChitenStatusId = 1_240;
    public const uint GuardStatusId = 3_054;
    public const uint GuardLargeScaleStatusId = 3_673;

    public static SmartActionProtectionKind ClassifyExactStatus(uint statusId) =>
        statusId switch
        {
            ChitenStatusId => SmartActionProtectionKind.Chiten,
            GuardStatusId or GuardLargeScaleStatusId => SmartActionProtectionKind.Guard,
            NinjaSeitonProtectionStatusCatalog.CoveredLegacyStatusId or
                NinjaSeitonProtectionStatusCatalog.CoveredStatusId or
                NinjaSeitonProtectionStatusCatalog.CoveredPvpStatusId or
                NinjaSeitonProtectionStatusCatalog.CoveredPvpAlternateStatusId =>
                SmartActionProtectionKind.Covered,
            NinjaSeitonProtectionStatusCatalog.HallowedGroundStatusId or
                NinjaSeitonProtectionStatusCatalog.UndeadRedemptionStatusId =>
                SmartActionProtectionKind.Invulnerability,
            _ => SmartActionProtectionKind.None,
        };

    public static bool IsExactProtectionKind(SmartActionProtectionKind kind) =>
        kind is SmartActionProtectionKind.Chiten or
            SmartActionProtectionKind.Guard or
            SmartActionProtectionKind.Covered or
            SmartActionProtectionKind.Invulnerability;

    /// <summary>
    /// Maps only reviewed Action-sheet geometry to a supported attack shape.
    /// Any new or drifting CastType/EffectRange combination remains an
    /// unsupported area shape so protected actors make the action fail closed.
    /// </summary>
    public static SmartActionAttackShape ClassifyAttackShape(byte effectRange, byte castType) =>
        (effectRange, castType) switch
        {
            (0, 1) => SmartActionAttackShape.DirectSingleTarget,
            (> 0, 2) => SmartActionAttackShape.TargetCenteredCircle,
            _ => SmartActionAttackShape.UnsupportedAreaOfEffect,
        };

    /// <summary>
    /// A direct action is safe only when the exact target is not protected.
    /// Other protected actors do not block a genuinely single-target action.
    /// </summary>
    public static bool IsDirectTargetSafe(
        SmartActionActorGeometry target,
        IReadOnlyList<SmartActionProtectedActor>? protectedActors)
    {
        if (!TryValidateInputs(target, protectedActors, out var actors)) return false;

        foreach (var protectedActor in actors)
        {
            if (SharesSlotOrEitherId(target, protectedActor.Geometry))
                return false;
        }

        return true;
    }

    /// <summary>
    /// A target-centered circle is blocked when the circle reaches any part of
    /// a protected actor's hitbox. EffectRange is measured from the selected
    /// target's center; the selected target's own hitbox does not enlarge it.
    /// </summary>
    public static bool IsTargetCenteredCircleSafe(
        SmartActionActorGeometry target,
        float effectRange,
        IReadOnlyList<SmartActionProtectedActor>? protectedActors)
    {
        if (!IsFiniteNonNegative(effectRange) || effectRange <= 0f ||
            !TryValidateInputs(target, protectedActors, out var actors))
        {
            return false;
        }

        foreach (var protectedActor in actors)
        {
            var protectedGeometry = protectedActor.Geometry;
            if (SharesSlotOrEitherId(target, protectedGeometry)) return false;

            var deltaX = (double)target.Position.X - protectedGeometry.Position.X;
            var deltaZ = (double)target.Position.Z - protectedGeometry.Position.Z;
            var distanceSquared = (deltaX * deltaX) + (deltaZ * deltaZ);
            var hitRadius = (double)effectRange + protectedGeometry.HitboxRadius;
            if (distanceSquared <= hitRadius * hitRadius) return false;
        }

        return true;
    }

    public static bool IsActionProtectionSafe(
        SmartActionAttackShape shape,
        SmartActionActorGeometry target,
        float effectRange,
        IReadOnlyList<SmartActionProtectedActor>? protectedActors)
    {
        if (!TryValidateInputs(target, protectedActors, out var actors) ||
            !IsFiniteNonNegative(effectRange))
        {
            return false;
        }

        return shape switch
        {
            SmartActionAttackShape.DirectSingleTarget =>
                effectRange == 0f && IsDirectTargetSafe(target, actors),
            SmartActionAttackShape.TargetCenteredCircle =>
                IsTargetCenteredCircleSafe(target, effectRange, actors),
            SmartActionAttackShape.UnsupportedAreaOfEffect => actors.Count == 0,
            _ => false,
        };
    }

    private static bool TryValidateInputs(
        SmartActionActorGeometry target,
        IReadOnlyList<SmartActionProtectedActor>? protectedActors,
        out IReadOnlyList<SmartActionProtectedActor> actors)
    {
        actors = protectedActors ?? Array.Empty<SmartActionProtectedActor>();
        if (protectedActors is null || !IsValidGeometry(target)) return false;

        var occupiedSlots = new HashSet<int>();
        var occupiedGameObjectIds = new HashSet<ulong>();
        var occupiedEntityIds = new HashSet<uint>();
        foreach (var protectedActor in actors)
        {
            var geometry = protectedActor.Geometry;
            if (!IsExactProtectionKind(protectedActor.Kind) ||
                !IsValidGeometry(geometry) ||
                !occupiedSlots.Add(geometry.EnemySlot) ||
                !occupiedGameObjectIds.Add(geometry.Actor.GameObjectId) ||
                !occupiedEntityIds.Add(geometry.Actor.EntityId))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidGeometry(SmartActionActorGeometry geometry) =>
        EnemySlotRules.IsValidSlot(geometry.EnemySlot) &&
        geometry.Actor.IsValid &&
        geometry.ExactCanonicalIdentity &&
        IsFinite(geometry.Position) &&
        IsFiniteNonNegative(geometry.HitboxRadius);

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) &&
        float.IsFinite(value.Y) &&
        float.IsFinite(value.Z);

    private static bool IsFiniteNonNegative(float value) =>
        float.IsFinite(value) && value >= 0f;

    private static bool SharesSlotOrEitherId(
        SmartActionActorGeometry left,
        SmartActionActorGeometry right) =>
        left.EnemySlot == right.EnemySlot ||
        left.Actor.GameObjectId == right.Actor.GameObjectId ||
        left.Actor.EntityId == right.Actor.EntityId;
}
