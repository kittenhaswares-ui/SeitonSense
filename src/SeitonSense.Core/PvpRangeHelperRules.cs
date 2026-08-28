namespace SeitonSense.Core;

/// <summary>
/// Read-only PvP reach profile for the local range visualization.
/// The outer envelope is the furthest reviewed hostile non-LB job action,
/// including hostile gap closers. Ground-targeted actions and Limit Breaks do
/// not define the envelope. The nominal values are action-sheet edge ranges;
/// renderers add only the local actor hitbox radius when drawing from its center.
/// </summary>
public readonly record struct PvpRangeHelperProfile(
    uint JobId,
    float MeleeRangeYalms,
    float MaximumActionRangeYalms)
{
    public bool IsValid =>
        JobId > 0 &&
        float.IsFinite(MeleeRangeYalms) &&
        float.IsFinite(MaximumActionRangeYalms) &&
        MeleeRangeYalms > 0f &&
        MaximumActionRangeYalms > MeleeRangeYalms;
}

/// <summary>
/// Patch-7.5-reviewed PvP job range catalog. Unknown classes, limited jobs, and
/// future job rows fail closed instead of receiving an invented range.
/// </summary>
public static class PvpRangeHelperRules
{
    public const float MeleeRangeYalms = 5f;

    public static bool TryGetProfile(uint jobId, out PvpRangeHelperProfile profile)
    {
        var maximumRange = GetMaximumActionRangeYalms(jobId);
        profile = new PvpRangeHelperProfile(jobId, MeleeRangeYalms, maximumRange);
        if (profile.IsValid) return true;

        profile = default;
        return false;
    }

    public static float GetMaximumActionRangeYalms(uint jobId) => jobId switch
    {
        // Tanks: Holy Spirit, Primal Rend/Onslaught, Plunge, Rough Divide.
        19 => 25f, // PLD
        21 => 20f, // WAR
        32 => 20f, // DRK
        37 => 20f, // GNB

        // Healers: their furthest hostile spell is 25 yalms.
        24 => 25f, // WHM
        28 => 25f, // SCH
        33 => 25f, // AST
        40 => 25f, // SGE

        // Melee DPS: furthest ordinary hostile action, including gap closers.
        20 => 20f, // MNK
        22 => 20f, // DRG
        30 => 20f, // NIN
        34 => 20f, // SAM
        39 => 25f, // RPR Harvest Moon
        41 => 20f, // VPR

        // Physical ranged DPS. DNC reaches 25 yalms through Starfall Dance.
        23 => 25f, // BRD
        31 => 25f, // MCH
        38 => 25f, // DNC

        // Magical ranged DPS.
        25 => 25f, // BLM
        27 => 25f, // SMN
        35 => 25f, // RDM
        42 => 25f, // PCT

        _ => 0f,
    };

    public static bool TryGetWorldRadii(
        uint jobId,
        float localHitboxRadius,
        out float meleeWorldRadius,
        out float maximumWorldRadius)
    {
        meleeWorldRadius = 0f;
        maximumWorldRadius = 0f;
        if (!TryGetProfile(jobId, out var profile) ||
            !float.IsFinite(localHitboxRadius) ||
            localHitboxRadius < 0f)
        {
            return false;
        }

        meleeWorldRadius = localHitboxRadius + profile.MeleeRangeYalms;
        maximumWorldRadius = localHitboxRadius + profile.MaximumActionRangeYalms;
        return float.IsFinite(meleeWorldRadius) &&
               float.IsFinite(maximumWorldRadius) &&
               maximumWorldRadius > meleeWorldRadius;
    }
}
