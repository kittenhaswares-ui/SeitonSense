namespace SeitonSense.Core;

/// <summary>
/// An exact friendly-player observation for the PvP action that is currently
/// being attempted after a /nearhelp macro line.
/// </summary>
public readonly record struct NearHelpSelectionCandidate(
    ulong GameObjectId,
    uint EntityId,
    int PartySlot,
    uint CurrentHp,
    uint MaximumHp,
    float DistanceSquared,
    bool IsExactFriendly,
    bool IsSelf,
    bool HasValidActionTarget,
    bool HasRangeAndLineOfSight);

/// <summary>
/// Selects only candidates proven valid for the actual friendly action. Health
/// is compared as an exact fraction, so rounded percentages cannot change the
/// result. The remaining ordering is deterministic across object-table order.
/// </summary>
public static class NearHelpSelectionRules
{
    public const int FirstPartySlot = 1;
    public const int LastPartySlot = 8;
    public const int UnknownPartySlot = 0;

    public static int SelectBestIndex(
        IReadOnlyList<NearHelpSelectionCandidate>? candidates)
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

    public static bool IsEligible(NearHelpSelectionCandidate candidate) =>
        TargetHighlightRules.IsValidGameObjectId(candidate.GameObjectId) &&
        IsValidEntityId(candidate.EntityId) &&
        IsValidPartySlot(candidate.PartySlot) &&
        candidate.CurrentHp > 0 &&
        candidate.MaximumHp >= candidate.CurrentHp &&
        float.IsFinite(candidate.DistanceSquared) &&
        candidate.DistanceSquared >= 0f &&
        candidate.IsExactFriendly &&
        !candidate.IsSelf &&
        candidate.HasValidActionTarget &&
        candidate.HasRangeAndLineOfSight;

    public static bool IsValidPartySlot(int partySlot) =>
        partySlot == UnknownPartySlot ||
        partySlot is >= FirstPartySlot and <= LastPartySlot;

    private static bool IsBetter(
        NearHelpSelectionCandidate candidate,
        NearHelpSelectionCandidate current)
    {
        // Each uint multiplication fits in ulong, including uint.MaxValue².
        var candidateRatio = (ulong)candidate.CurrentHp * current.MaximumHp;
        var currentRatio = (ulong)current.CurrentHp * candidate.MaximumHp;
        var health = candidateRatio.CompareTo(currentRatio);
        if (health != 0) return health < 0;

        var distance = candidate.DistanceSquared.CompareTo(current.DistanceSquared);
        if (distance != 0) return distance < 0;

        var candidatePartyOrder = StablePartyOrder(candidate.PartySlot);
        var currentPartyOrder = StablePartyOrder(current.PartySlot);
        if (candidatePartyOrder != currentPartyOrder)
            return candidatePartyOrder < currentPartyOrder;

        if (candidate.EntityId != current.EntityId)
            return candidate.EntityId < current.EntityId;

        return candidate.GameObjectId < current.GameObjectId;
    }

    private static int StablePartyOrder(int partySlot) =>
        partySlot == UnknownPartySlot ? int.MaxValue : partySlot;

    private static bool IsValidEntityId(uint entityId) =>
        entityId is not 0 and not 0xE0000000 and not uint.MaxValue;
}
