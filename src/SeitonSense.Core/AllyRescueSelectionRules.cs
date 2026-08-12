namespace SeitonSense.Core;

/// <summary>
/// The exact status application that makes one party member eligible for an
/// ally rescue action. InstanceToken is supplied by the runtime and must stay
/// stable for one application while changing for a refresh or replacement.
/// </summary>
public readonly record struct AllyRescueStatusInstance(
    uint StatusId,
    ulong InstanceToken)
{
    public bool IsValid =>
        InstanceToken != 0 &&
        AllyRescueStatusRules.IsTriggerStatus(StatusId);
}

/// <summary>
/// Exact actor and status identity. This is also the no-retry key retained
/// before native action dispatch.
/// </summary>
public readonly record struct AllyRescueIntent(
    ulong GameObjectId,
    uint EntityId,
    AllyRescueStatusInstance Status)
{
    public bool IsValid =>
        TargetHighlightRules.IsValidGameObjectId(GameObjectId) &&
        AllyRescueSelectionRules.IsValidEntityId(EntityId) &&
        Status.IsValid;
}

/// <summary>
/// An action-time observation for one exact non-self party member and one
/// active rescue-trigger status application.
/// </summary>
public readonly record struct AllyRescueSelectionCandidate(
    ulong GameObjectId,
    uint EntityId,
    int PartySlot,
    AllyRescueStatusInstance Status,
    uint CurrentHp,
    uint MaximumHp,
    int? UniqueIncomingEnemyPressureCount,
    uint CurrentMp,
    uint MaximumMp,
    bool HasTrustedMp,
    float DistanceSquared,
    bool IsExactPartyMember,
    bool IsSelf,
    bool IsAlive,
    bool IsTargetable,
    bool HasValidActionTarget,
    bool HasNativeRangeAndLineOfSight)
{
    public AllyRescueIntent Intent => new(GameObjectId, EntityId, Status);
}

/// <summary>
/// Exact allowlist for ally rescue. Heavy and Bind deliberately do not count.
/// </summary>
public static class AllyRescueStatusRules
{
    public const uint StunStatusId = 1343;
    public const uint SilenceStatusId = 1347;
    public const uint MiracleOfNatureStatusId = 3085;
    public const uint DeepFreezeStatusId = 3219;

    public static bool IsTriggerStatus(uint statusId) =>
        statusId is
            StunStatusId or
            SilenceStatusId or
            MiracleOfNatureStatusId or
            DeepFreezeStatusId;
}

/// <summary>
/// Produces a trusted, deduplicated pressure count from exact enemy actor
/// identities. A missing snapshot remains unknown instead of receiving a
/// synthetic advantage. Ambiguous identities are excluded fail-closed.
/// </summary>
public static class AllyRescuePressureRules
{
    public static int? CountUniqueIncomingEnemies(
        IEnumerable<TargetPressureActorIdentity>? incomingEnemies)
    {
        if (incomingEnemies is null) return null;

        var identities = incomingEnemies
            .Where(identity => identity.IsValid)
            .Distinct()
            .ToArray();
        if (identities.Length == 0) return 0;

        var ambiguous = new HashSet<TargetPressureActorIdentity>();
        foreach (var group in identities.GroupBy(identity => identity.GameObjectId))
        {
            if (group.Select(identity => identity.EntityId).Distinct().Skip(1).Any())
                ambiguous.UnionWith(group);
        }

        foreach (var group in identities.GroupBy(identity => identity.EntityId))
        {
            if (group.Select(identity => identity.GameObjectId).Distinct().Skip(1).Any())
                ambiguous.UnionWith(group);
        }

        return identities.Count(identity => !ambiguous.Contains(identity));
    }
}

/// <summary>
/// Selects only exact, alive, targetable party members that the chosen action
/// can natively target in range and line of sight. Ranking is exact HP ratio,
/// then known unique incoming pressure, trusted MP ratio, distance, and stable
/// party/actor/status identity.
/// </summary>
public static class AllyRescueSelectionRules
{
    public const int FirstPartySlot = 1;
    public const int LastPartySlot = 8;

    public static int SelectBestIndex(
        IReadOnlyList<AllyRescueSelectionCandidate>? candidates,
        IReadOnlySet<AllyRescueIntent>? excludedIntents = null)
    {
        if (candidates is null || candidates.Count == 0) return -1;

        var bestIndex = -1;
        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            if (!IsEligible(candidate) ||
                (excludedIntents?.Contains(candidate.Intent) ?? false))
            {
                continue;
            }

            if (bestIndex < 0 || IsBetter(candidate, candidates[bestIndex]))
                bestIndex = index;
        }

        return bestIndex;
    }

    public static bool IsEligible(AllyRescueSelectionCandidate candidate) =>
        candidate.Intent.IsValid &&
        candidate.PartySlot is >= FirstPartySlot and <= LastPartySlot &&
        candidate.CurrentHp > 0 &&
        candidate.MaximumHp >= candidate.CurrentHp &&
        float.IsFinite(candidate.DistanceSquared) &&
        candidate.DistanceSquared >= 0f &&
        candidate.IsExactPartyMember &&
        !candidate.IsSelf &&
        candidate.IsAlive &&
        candidate.IsTargetable &&
        candidate.HasValidActionTarget &&
        candidate.HasNativeRangeAndLineOfSight;

    public static bool IsValidEntityId(uint entityId) =>
        entityId is not 0 and not 0xE0000000 and not uint.MaxValue;

    private static bool IsBetter(
        AllyRescueSelectionCandidate candidate,
        AllyRescueSelectionCandidate current)
    {
        // Each uint multiplication fits in ulong, including uint.MaxValue squared.
        var candidateHpRatio = (ulong)candidate.CurrentHp * current.MaximumHp;
        var currentHpRatio = (ulong)current.CurrentHp * candidate.MaximumHp;
        var hp = candidateHpRatio.CompareTo(currentHpRatio);
        if (hp != 0) return hp < 0;

        var pressure = ComparePressure(candidate, current);
        if (pressure != 0) return pressure < 0;

        var mp = CompareMp(candidate, current);
        if (mp != 0) return mp < 0;

        var distance = candidate.DistanceSquared.CompareTo(current.DistanceSquared);
        if (distance != 0) return distance < 0;
        if (candidate.PartySlot != current.PartySlot)
            return candidate.PartySlot < current.PartySlot;
        if (candidate.EntityId != current.EntityId)
            return candidate.EntityId < current.EntityId;
        if (candidate.GameObjectId != current.GameObjectId)
            return candidate.GameObjectId < current.GameObjectId;
        if (candidate.Status.StatusId != current.Status.StatusId)
            return candidate.Status.StatusId < current.Status.StatusId;

        return candidate.Status.InstanceToken < current.Status.InstanceToken;
    }

    private static int ComparePressure(
        AllyRescueSelectionCandidate candidate,
        AllyRescueSelectionCandidate current)
    {
        var candidateKnown = HasKnownPressure(candidate);
        var currentKnown = HasKnownPressure(current);
        if (candidateKnown != currentKnown) return candidateKnown ? -1 : 1;
        if (!candidateKnown) return 0;

        // Higher pressure wins, hence the reversed comparison.
        return current.UniqueIncomingEnemyPressureCount!.Value.CompareTo(
            candidate.UniqueIncomingEnemyPressureCount!.Value);
    }

    private static int CompareMp(
        AllyRescueSelectionCandidate candidate,
        AllyRescueSelectionCandidate current)
    {
        var candidateKnown = HasTrustedMpRatio(candidate);
        var currentKnown = HasTrustedMpRatio(current);
        if (candidateKnown != currentKnown) return candidateKnown ? -1 : 1;
        if (!candidateKnown) return 0;

        var candidateRatio = (ulong)candidate.CurrentMp * current.MaximumMp;
        var currentRatio = (ulong)current.CurrentMp * candidate.MaximumMp;
        return candidateRatio.CompareTo(currentRatio);
    }

    private static bool HasKnownPressure(AllyRescueSelectionCandidate candidate) =>
        candidate.UniqueIncomingEnemyPressureCount is >= 0;

    private static bool HasTrustedMpRatio(AllyRescueSelectionCandidate candidate) =>
        candidate.HasTrustedMp &&
        candidate.MaximumMp > 0 &&
        candidate.CurrentMp <= candidate.MaximumMp;
}
