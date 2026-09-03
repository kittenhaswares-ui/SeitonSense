namespace SeitonSense.Core;

public enum FarHelpAllyRole
{
    Other = 0,
    RangedOrCaster = 1,
    Healer = 2,
}

/// <summary>
/// An exact party-member observation for the friendly movement action that is
/// currently being attempted after a /farhelp macro line.
/// </summary>
public readonly record struct FarHelpSelectionCandidate(
    ulong GameObjectId,
    uint EntityId,
    int PartySlot,
    uint CurrentHp,
    uint MaximumHp,
    float DistanceSquared,
    FarHelpAllyRole Role,
    bool IsExactPartyMember,
    bool IsSelf,
    bool IsTargetable,
    bool HasValidActionTarget,
    bool HasRangeAndLineOfSight,
    bool HasCompleteCanonicalEnemySnapshot,
    int CanonicalLiveEnemyCount,
    float MinimumCanonicalEnemyEdgeDistance);

/// <summary>
/// Selects the farthest action-valid party member. Role and enemy-clearance
/// observations remain available for diagnostics, but never influence target
/// choice. Exact-distance ties use only stable party and actor identity.
/// </summary>
public static class FarHelpSelectionRules
{
    public const int FirstPartySlot = 1;
    public const int LastPartySlot = 8;
    public const int MaximumCanonicalEnemyCount = 5;
    public const float MinimumBacklineEnemyEdgeClearance = 10f;

    public static FarHelpAllyRole ClassifyPlayableJob(uint jobId) => jobId switch
    {
        // Healers: WHM, SCH, AST, SGE.
        24 or 28 or 33 or 40 => FarHelpAllyRole.Healer,
        // Physical ranged: BRD, MCH, DNC. Casters: BLM, SMN, RDM, PCT.
        23 or 31 or 38 or 25 or 27 or 35 or 42 =>
            FarHelpAllyRole.RangedOrCaster,
        // Tanks, melee, classes, limited jobs, and unknown future rows use the
        // neutral diagnostic label. This label never affects eligibility or rank.
        _ => FarHelpAllyRole.Other,
    };

    public static int SelectBestIndex(
        IReadOnlyList<FarHelpSelectionCandidate>? candidates)
    {
        if (candidates is null || candidates.Count == 0) return -1;

        var bestIndex = -1;
        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            if (!IsEligible(candidate)) continue;
            if (bestIndex < 0 || IsBetter(candidate, candidates[bestIndex]))
                bestIndex = index;
        }

        return bestIndex;
    }

    public static bool IsEligible(FarHelpSelectionCandidate candidate) =>
        TargetHighlightRules.IsValidGameObjectId(candidate.GameObjectId) &&
        IsValidEntityId(candidate.EntityId) &&
        candidate.PartySlot is >= FirstPartySlot and <= LastPartySlot &&
        candidate.CurrentHp > 0 &&
        candidate.MaximumHp >= candidate.CurrentHp &&
        float.IsFinite(candidate.DistanceSquared) &&
        candidate.DistanceSquared >= 0f &&
        candidate.IsExactPartyMember &&
        !candidate.IsSelf &&
        candidate.IsTargetable &&
        candidate.HasValidActionTarget &&
        candidate.HasRangeAndLineOfSight;

    /// <summary>
    /// The runtime snapshot must contain every unambiguously identified live
    /// canonical CC enemy. The supplied value is the minimum horizontal
    /// hitbox-edge clearance across that complete set; an empty, oversized,
    /// missing, negative, or non-finite snapshot is not considered safe. This
    /// signal is diagnostic only and never influences selection or eligibility.
    /// </summary>
    public static bool IsBacklineSafe(FarHelpSelectionCandidate candidate) =>
        candidate.HasCompleteCanonicalEnemySnapshot &&
        candidate.CanonicalLiveEnemyCount is >= 1 and <= MaximumCanonicalEnemyCount &&
        float.IsFinite(candidate.MinimumCanonicalEnemyEdgeDistance) &&
        candidate.MinimumCanonicalEnemyEdgeDistance >= 0f &&
        candidate.MinimumCanonicalEnemyEdgeDistance > MinimumBacklineEnemyEdgeClearance;

    private static bool IsBetter(
        FarHelpSelectionCandidate candidate,
        FarHelpSelectionCandidate current)
    {
        var distance = candidate.DistanceSquared.CompareTo(current.DistanceSquared);
        if (distance != 0) return distance > 0;

        if (candidate.PartySlot != current.PartySlot)
            return candidate.PartySlot < current.PartySlot;

        if (candidate.EntityId != current.EntityId)
            return candidate.EntityId < current.EntityId;

        return candidate.GameObjectId < current.GameObjectId;
    }

    private static bool IsValidEntityId(uint entityId) =>
        entityId is not 0 and not 0xE0000000 and not uint.MaxValue;
}
