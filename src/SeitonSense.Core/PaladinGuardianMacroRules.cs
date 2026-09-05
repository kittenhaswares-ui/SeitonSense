namespace SeitonSense.Core;

/// <summary>
/// Target selection for an explicit /seitonpld request. This does not apply the
/// automatic helper's own-resource gates or retain an action for a later frame.
/// Runtime must freeze and revalidate the returned exact party actor before use.
/// </summary>
public static class PaladinGuardianMacroRules
{
    public const float NearbyFallbackRangeYalms = 6f;

    public static int SelectCandidateIndex(
        IReadOnlyList<PaladinGuardianCandidate>? candidates,
        long nowMilliseconds,
        long pressurePublishedAtMilliseconds)
    {
        if (candidates is null || candidates.Count == 0) return -1;

        var pressureFresh = DefensiveUtilityRules.IsFreshGuardianPressurePublication(
            nowMilliseconds, pressurePublishedAtMilliseconds);
        var eligible = new PaladinGuardianCandidate[candidates.Count];
        var nearest = -1;
        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = WithFreshPressure(candidates[index], pressureFresh);
            if (!IsValidReachablePartyMember(candidate)) continue;

            // Keep original indexes so the caller freezes the exact input actor.
            eligible[index] = candidate;
            if (IsNearby(candidate) &&
                (nearest < 0 || CompareNearby(candidate, eligible[nearest]) < 0))
            {
                nearest = index;
            }
        }

        // Reuse the existing danger thresholds and every danger tie-break.
        // Native reachability, not the nearby fallback's six yalms, limits rescue.
        var endangered = DefensiveUtilityRules.SelectGuardianCandidateIndex(eligible);
        return endangered >= 0 ? endangered : nearest;
    }

    public static bool IsEligibleCandidate(
        PaladinGuardianCandidate candidate,
        long nowMilliseconds,
        long pressurePublishedAtMilliseconds)
    {
        candidate = WithFreshPressure(candidate,
            DefensiveUtilityRules.IsFreshGuardianPressurePublication(
                nowMilliseconds, pressurePublishedAtMilliseconds));
        return IsValidReachablePartyMember(candidate) &&
               (DefensiveUtilityRules.ClassifyGuardianRisk(candidate) != PaladinGuardianRiskTier.None ||
                IsNearby(candidate));
    }

    private static PaladinGuardianCandidate WithFreshPressure(
        PaladinGuardianCandidate candidate,
        bool pressureFresh) =>
        pressureFresh ? candidate : candidate with { IncomingEnemyCount = null };

    private static bool IsValidReachablePartyMember(PaladinGuardianCandidate candidate) =>
        candidate.Actor.IsValid &&
        candidate.PartySlot is >= 1 and <= 8 &&
        candidate.IsExactPartyMember &&
        !candidate.IsSelf &&
        candidate.IsAlive &&
        candidate.IsTargetable &&
        candidate.HasValidNativeTarget &&
        candidate.HasNativeRangeAndLineOfSight &&
        DefensiveUtilityRules.IsAtOrBelowHpPercent(candidate.CurrentHp, candidate.MaximumHp, 100) &&
        float.IsFinite(candidate.DistanceSquared) &&
        candidate.DistanceSquared >= 0f;

    private static bool IsNearby(PaladinGuardianCandidate candidate) =>
        candidate.DistanceSquared <= NearbyFallbackRangeYalms * NearbyFallbackRangeYalms;

    private static int CompareNearby(PaladinGuardianCandidate left, PaladinGuardianCandidate right)
    {
        var distance = left.DistanceSquared.CompareTo(right.DistanceSquared);
        if (distance != 0) return distance;
        var slot = left.PartySlot.CompareTo(right.PartySlot);
        if (slot != 0) return slot;
        var entity = left.EntityId.CompareTo(right.EntityId);
        return entity != 0 ? entity : left.GameObjectId.CompareTo(right.GameObjectId);
    }
}
