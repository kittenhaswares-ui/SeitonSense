namespace SeitonSense.Core;

public enum BackwardDashMovementKind : byte
{
    ForwardFromActorFacing = 1,
    BackwardFromActorFacing = 2,
}

public readonly record struct ReviewedBackwardDashProfile(
    uint JobId,
    uint ActionId,
    string Name,
    uint IconId,
    ushort Recast100Milliseconds,
    byte SheetCooldownGroup,
    byte SheetAdditionalCooldownGroup,
    byte MaximumAccessibleCharges,
    byte BehaviourType,
    bool SheetAffectsPosition,
    BackwardDashMovementKind MovementKind)
{
    public int RuntimeRecastGroupIndex => SheetCooldownGroup - 1;

    public int AdjustedRecastMilliseconds => Recast100Milliseconds * 100;

    public bool IsValid =>
        JobId != 0 &&
        ActionId != 0 &&
        !string.IsNullOrWhiteSpace(Name) &&
        IconId != 0 &&
        Recast100Milliseconds != 0 &&
        SheetCooldownGroup != 0 &&
        MaximumAccessibleCharges != 0 &&
        BehaviourType > 1 &&
        MovementKind is BackwardDashMovementKind.ForwardFromActorFacing or
            BackwardDashMovementKind.BackwardFromActorFacing;
}

/// <summary>
/// Closed catalog and heading policy for the non-ground-target branches of
/// /seitonbw. These are the only current PvP self-actions whose movement is
/// defined by actor facing. Target hops, hostile-target backsteps, stored-origin
/// returns, transformed follow-ups, LBs, and PvE rows are intentionally absent.
/// </summary>
public static class BackwardDashRules
{
    public const uint AstrologianJobId = 33;
    public const uint DancerJobId = 38;
    public const uint DragoonJobId = 22;
    public const uint ReaperJobId = 39;
    public const uint PictomancerJobId = 42;

    public const float MaximumHeadingReadbackErrorRadians = 0.01f;
    public const float MaximumImmediateAnimationLockSeconds = 0.05f;

    private static readonly ReviewedBackwardDashProfile[] Profiles =
    [
        new(
            AstrologianJobId,
            41_506,
            "Epicycle",
            9_069,
            240,
            11,
            0,
            1,
            50,
            true,
            BackwardDashMovementKind.ForwardFromActorFacing),
        new(
            DancerJobId,
            29_430,
            "En Avant",
            9_450,
            100,
            5,
            71,
            4,
            19,
            true,
            BackwardDashMovementKind.ForwardFromActorFacing),
        new(
            DragoonJobId,
            29_494,
            "Elusive Jump",
            9_176,
            200,
            5,
            0,
            1,
            4,
            false,
            BackwardDashMovementKind.BackwardFromActorFacing),
        new(
            ReaperJobId,
            29_550,
            "Hell's Ingress",
            9_562,
            100,
            6,
            0,
            1,
            50,
            true,
            BackwardDashMovementKind.ForwardFromActorFacing),
        new(
            PictomancerJobId,
            39_210,
            "Smudge",
            9_750,
            150,
            3,
            0,
            1,
            50,
            true,
            BackwardDashMovementKind.ForwardFromActorFacing),
    ];

    public static IReadOnlyList<ReviewedBackwardDashProfile> DirectionalProfiles => Profiles;

    public static bool TryGetDirectionalProfile(
        uint jobId,
        out ReviewedBackwardDashProfile profile)
    {
        foreach (var candidate in Profiles)
        {
            if (candidate.JobId != jobId) continue;
            profile = candidate;
            return candidate.IsValid;
        }

        profile = default;
        return false;
    }

    public static bool IsReviewedDirectionalAction(uint actionId)
    {
        foreach (var profile in Profiles)
        {
            if (profile.ActionId == actionId) return true;
        }

        return false;
    }

    public static bool TryResolveActorFacing(
        float screenBackHeadingRadians,
        BackwardDashMovementKind movementKind,
        out float actorFacingRadians)
    {
        actorFacingRadians = 0f;
        if (!float.IsFinite(screenBackHeadingRadians) ||
            movementKind is not
                (BackwardDashMovementKind.ForwardFromActorFacing or
                 BackwardDashMovementKind.BackwardFromActorFacing))
        {
            return false;
        }

        var raw = movementKind == BackwardDashMovementKind.ForwardFromActorFacing
            ? screenBackHeadingRadians
            : screenBackHeadingRadians + MathF.PI;
        actorFacingRadians = NormalizeRadians(raw);
        return float.IsFinite(actorFacingRadians);
    }

    public static bool AreHeadingsEquivalent(float left, float right)
    {
        if (!float.IsFinite(left) || !float.IsFinite(right)) return false;
        var delta = NormalizeRadians(left - right);
        return MathF.Abs(delta) <= MaximumHeadingReadbackErrorRadians;
    }

    private static float NormalizeRadians(float radians)
    {
        if (!float.IsFinite(radians)) return float.NaN;
        var normalized = MathF.IEEERemainder(radians, 2f * MathF.PI);
        return normalized <= -MathF.PI ? normalized + (2f * MathF.PI) : normalized;
    }
}
