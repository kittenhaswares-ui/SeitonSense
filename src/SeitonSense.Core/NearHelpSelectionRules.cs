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
    bool HasRangeAndLineOfSight,
    int? UniqueIncomingEnemyPressureCount = null,
    bool IsActionSelfTargetable = false);

public enum NearHelpSelectionReason
{
    None = 0,
    NoEligibleCandidate = 1,
    PressurePreferenceDisabled = 2,
    CriticalHealthAnchor = 3,
    PressureViewUntrusted = 4,
    PressureDataIncomplete = 5,
    NoPositivePressure = 6,
    IncomingPressure = 7,
}

public readonly record struct NearHelpSelectionDecision(
    int SelectedIndex,
    int HealthAnchorIndex,
    NearHelpSelectionReason Reason)
{
    public bool UsedIncomingPressure =>
        Reason == NearHelpSelectionReason.IncomingPressure;
}

/// <summary>
/// Selects only candidates proven valid for the actual friendly action. A
/// self candidate additionally requires proof that this exact action supports
/// self-targeting. Health and the optional pressure window are compared as
/// exact fractions, so rounded percentages cannot change either boundary.
/// </summary>
public static class NearHelpSelectionRules
{
    public const int FirstPartySlot = 1;
    public const int LastPartySlot = 8;
    public const int UnknownPartySlot = 0;
    public const int CriticalHealthPercent = 25;
    public const int PressureWindowPercentagePoints = 10;

    public static int SelectBestIndex(
        IReadOnlyList<NearHelpSelectionCandidate>? candidates,
        bool preferIncomingPressure = false,
        bool hasTrustedPressureView = false) =>
        SelectBest(
            candidates,
            preferIncomingPressure,
            hasTrustedPressureView).SelectedIndex;

    /// <summary>
    /// Applies the normal Near Help ordering to the subset at or below one
    /// exact HP-percentage boundary. Existing Near Help callers remain
    /// unconstrained; held healing helpers can share the same eligibility,
    /// pressure, and deterministic tie policy without copying its comparer.
    /// </summary>
    public static int SelectBestIndexAtOrBelowHealthPercent(
        IReadOnlyList<NearHelpSelectionCandidate>? candidates,
        int maximumHealthPercent,
        bool preferIncomingPressure = false,
        bool hasTrustedPressureView = false) =>
        SelectBestAtOrBelowHealthPercent(
            candidates,
            maximumHealthPercent,
            preferIncomingPressure,
            hasTrustedPressureView).SelectedIndex;

    public static NearHelpSelectionDecision SelectBest(
        IReadOnlyList<NearHelpSelectionCandidate>? candidates,
        bool preferIncomingPressure = false,
        bool hasTrustedPressureView = false) =>
        SelectBestCore(
            candidates,
            maximumHealthPercent: null,
            preferIncomingPressure,
            hasTrustedPressureView);

    public static NearHelpSelectionDecision SelectBestAtOrBelowHealthPercent(
        IReadOnlyList<NearHelpSelectionCandidate>? candidates,
        int maximumHealthPercent,
        bool preferIncomingPressure = false,
        bool hasTrustedPressureView = false) =>
        maximumHealthPercent is >= 1 and <= 100
            ? SelectBestCore(
                candidates,
                maximumHealthPercent,
                preferIncomingPressure,
                hasTrustedPressureView)
            : NoEligibleCandidate();

    public static bool IsAtOrBelowHealthPercent(
        NearHelpSelectionCandidate candidate,
        int maximumHealthPercent) =>
        IsEligible(candidate) &&
        maximumHealthPercent is >= 1 and <= 100 &&
        (ulong)candidate.CurrentHp * 100UL <=
        (ulong)candidate.MaximumHp * (uint)maximumHealthPercent;

    private static NearHelpSelectionDecision SelectBestCore(
        IReadOnlyList<NearHelpSelectionCandidate>? candidates,
        int? maximumHealthPercent,
        bool preferIncomingPressure,
        bool hasTrustedPressureView)
    {
        if (candidates is null || candidates.Count == 0)
            return NoEligibleCandidate();

        var healthAnchorIndex = -1;
        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            if (!IsEligibleForSelection(candidate, maximumHealthPercent)) continue;
            if (healthAnchorIndex < 0 || IsBetterByHealth(candidate, candidates[healthAnchorIndex]))
                healthAnchorIndex = index;
        }

        if (healthAnchorIndex < 0) return NoEligibleCandidate();

        if (!preferIncomingPressure)
        {
            return HealthFirst(
                healthAnchorIndex,
                NearHelpSelectionReason.PressurePreferenceDisabled);
        }

        var healthAnchor = candidates[healthAnchorIndex];
        if (IsAtOrBelowCriticalHealth(healthAnchor))
        {
            return HealthFirst(
                healthAnchorIndex,
                NearHelpSelectionReason.CriticalHealthAnchor);
        }

        if (!hasTrustedPressureView)
        {
            return HealthFirst(
                healthAnchorIndex,
                NearHelpSelectionReason.PressureViewUntrusted);
        }

        var pressureBestIndex = -1;
        var hasPositivePressure = false;
        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            if (!IsEligibleForSelection(candidate, maximumHealthPercent) ||
                !IsInsidePressureWindow(candidate, healthAnchor))
                continue;

            if (candidate.UniqueIncomingEnemyPressureCount is not >= 0)
            {
                return HealthFirst(
                    healthAnchorIndex,
                    NearHelpSelectionReason.PressureDataIncomplete);
            }

            if (candidate.UniqueIncomingEnemyPressureCount > 0)
                hasPositivePressure = true;

            if (pressureBestIndex < 0 || IsBetterByPressure(candidate, candidates[pressureBestIndex]))
                pressureBestIndex = index;
        }

        if (!hasPositivePressure)
        {
            return HealthFirst(
                healthAnchorIndex,
                NearHelpSelectionReason.NoPositivePressure);
        }

        return new NearHelpSelectionDecision(
            pressureBestIndex,
            healthAnchorIndex,
            NearHelpSelectionReason.IncomingPressure);
    }

    private static bool IsEligibleForSelection(
        NearHelpSelectionCandidate candidate,
        int? maximumHealthPercent) =>
        IsEligible(candidate) &&
        (!maximumHealthPercent.HasValue ||
         IsAtOrBelowHealthPercent(candidate, maximumHealthPercent.Value));

    public static bool IsEligible(NearHelpSelectionCandidate candidate) =>
        TargetHighlightRules.IsValidGameObjectId(candidate.GameObjectId) &&
        IsValidEntityId(candidate.EntityId) &&
        IsValidPartySlot(candidate.PartySlot) &&
        candidate.CurrentHp > 0 &&
        candidate.MaximumHp >= candidate.CurrentHp &&
        float.IsFinite(candidate.DistanceSquared) &&
        candidate.DistanceSquared >= 0f &&
        candidate.IsExactFriendly &&
        (!candidate.IsSelf || candidate.IsActionSelfTargetable) &&
        candidate.HasValidActionTarget &&
        candidate.HasRangeAndLineOfSight;

    public static bool IsValidPartySlot(int partySlot) =>
        partySlot == UnknownPartySlot ||
        partySlot is >= FirstPartySlot and <= LastPartySlot;

    private static bool IsBetterByHealth(
        NearHelpSelectionCandidate candidate,
        NearHelpSelectionCandidate current)
    {
        var health = CompareHealth(candidate, current);
        if (health != 0) return health < 0;

        return IsBetterStableTie(candidate, current);
    }

    private static bool IsBetterByPressure(
        NearHelpSelectionCandidate candidate,
        NearHelpSelectionCandidate current)
    {
        var pressure = current.UniqueIncomingEnemyPressureCount!.Value.CompareTo(
            candidate.UniqueIncomingEnemyPressureCount!.Value);
        if (pressure != 0) return pressure < 0;

        var health = CompareHealth(candidate, current);
        if (health != 0) return health < 0;

        return IsBetterStableTie(candidate, current);
    }

    private static int CompareHealth(
        NearHelpSelectionCandidate candidate,
        NearHelpSelectionCandidate current)
    {
        // Each uint multiplication fits in ulong, including uint.MaxValue².
        var candidateRatio = (ulong)candidate.CurrentHp * current.MaximumHp;
        var currentRatio = (ulong)current.CurrentHp * candidate.MaximumHp;
        return candidateRatio.CompareTo(currentRatio);
    }

    private static bool IsBetterStableTie(
        NearHelpSelectionCandidate candidate,
        NearHelpSelectionCandidate current)
    {
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

    private static bool IsAtOrBelowCriticalHealth(
        NearHelpSelectionCandidate candidate) =>
        (ulong)candidate.CurrentHp * 100UL <=
        (ulong)candidate.MaximumHp * CriticalHealthPercent;

    private static bool IsInsidePressureWindow(
        NearHelpSelectionCandidate candidate,
        NearHelpSelectionCandidate healthAnchor)
    {
        // candidateHP / candidateMax <= anchorHP / anchorMax + 10 / 100.
        // Three uint-width factors require UInt128 at the public input limits.
        var left =
            (UInt128)100 * candidate.CurrentHp * healthAnchor.MaximumHp;
        var right =
            (UInt128)100 * healthAnchor.CurrentHp * candidate.MaximumHp +
            (UInt128)PressureWindowPercentagePoints *
            candidate.MaximumHp *
            healthAnchor.MaximumHp;
        return left <= right;
    }

    private static NearHelpSelectionDecision HealthFirst(
        int healthAnchorIndex,
        NearHelpSelectionReason reason) =>
        new(healthAnchorIndex, healthAnchorIndex, reason);

    private static NearHelpSelectionDecision NoEligibleCandidate() =>
        new(-1, -1, NearHelpSelectionReason.NoEligibleCandidate);

    private static int StablePartyOrder(int partySlot) =>
        partySlot == UnknownPartySlot ? int.MaxValue : partySlot;

    private static bool IsValidEntityId(uint entityId) =>
        entityId is not 0 and not 0xE0000000 and not uint.MaxValue;
}
