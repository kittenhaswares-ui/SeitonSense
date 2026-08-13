namespace SeitonSense.Core;

public enum FarHelpAllyRole
{
    Other = 0,
    PreferredHealerOrRanged = 1,
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
    bool HasRangeAndLineOfSight);

/// <summary>
/// Selects the farthest action-valid party member. Healers, physical ranged,
/// and casters form a strict preferred tier; only when that tier has no valid
/// candidate are tanks, melee, or unknown jobs considered. Ties are independent
/// of object-table enumeration order.
/// </summary>
public static class FarHelpSelectionRules
{
    public const int FirstPartySlot = 1;
    public const int LastPartySlot = 8;

    public static FarHelpAllyRole ClassifyPlayableJob(uint jobId) => jobId switch
    {
        // Healers: WHM, SCH, AST, SGE.
        24 or 28 or 33 or 40 => FarHelpAllyRole.PreferredHealerOrRanged,
        // Physical ranged: BRD, MCH, DNC. Casters: BLM, SMN, RDM, PCT.
        23 or 31 or 38 or 25 or 27 or 35 or 42 =>
            FarHelpAllyRole.PreferredHealerOrRanged,
        // Tanks, melee, classes, limited jobs, and unknown future rows stay in
        // the fallback tier until explicitly reviewed.
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
        Enum.IsDefined(candidate.Role) &&
        candidate.IsExactPartyMember &&
        !candidate.IsSelf &&
        candidate.IsTargetable &&
        candidate.HasValidActionTarget &&
        candidate.HasRangeAndLineOfSight;

    private static bool IsBetter(
        FarHelpSelectionCandidate candidate,
        FarHelpSelectionCandidate current)
    {
        if (candidate.Role != current.Role)
            return candidate.Role > current.Role;

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
