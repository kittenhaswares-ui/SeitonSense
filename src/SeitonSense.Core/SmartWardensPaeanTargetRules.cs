namespace SeitonSense.Core;

/// <summary>
/// One exact Crystalline Conflict party-list observation available while the
/// client's original Warden's Paean action call is being handled.
/// </summary>
public readonly record struct SmartWardensPaeanCandidate(
    int PartySlot,
    TargetPressureActorIdentity Actor,
    bool ExactPartyIdentity,
    bool IsSelf,
    bool Alive,
    bool Targetable,
    bool HasWardensPaeanWard,
    uint CurrentHp,
    uint MaximumHp,
    bool NativeTargetValid,
    bool NativeRangeAndLineOfSight,
    bool PressureKnown,
    int UniqueIncomingEnemyCount);

/// <summary>
/// The one selected target identity. Runtime code must re-resolve only this
/// party slot and exact actor before changing the target argument of the
/// already incoming action call.
/// </summary>
public readonly record struct SmartWardensPaeanIntent(
    uint ActionId,
    int PartySlot,
    TargetPressureActorIdentity LocalPlayer,
    TargetPressureActorIdentity Target,
    int SelectedIncomingEnemyCount)
{
    public bool IsValid =>
        ActionId == SmartWardensPaeanTargetRules.ActionId &&
        PartySlot is >= SmartWardensPaeanTargetRules.FirstPartySlot and
            <= SmartWardensPaeanTargetRules.LastPartySlot &&
        LocalPlayer.IsValid &&
        Target.IsValid &&
        LocalPlayer != Target &&
        SelectedIncomingEnemyCount >=
            SmartWardensPaeanTargetRules.MinimumIncomingEnemyCount;
}

public readonly record struct SmartWardensPaeanObservation(
    bool ConfigurationEnabled,
    bool IsCrystallineConflict,
    uint LocalJobId,
    TargetPressureActorIdentity LocalPlayer,
    bool IsLocalPlayerAlive,
    bool MetadataVerified,
    uint ResolvedActionId,
    bool CompleteExactPartyView,
    IReadOnlyList<SmartWardensPaeanCandidate>? Candidates);

public enum SmartWardensPaeanDecisionKind
{
    Vanilla = 0,
    Redirect = 1,
}

public enum SmartWardensPaeanDecisionReason
{
    None = 0,
    ConfigurationDisabled = 1,
    OutsideCrystallineConflict = 2,
    LocalPlayerIdentityInvalid = 3,
    LocalPlayerDead = 4,
    LocalJobInvalid = 5,
    MetadataUnverified = 6,
    ResolvedActionInvalid = 7,
    IncompleteExactPartyView = 8,
    NoKnownPressureTarget = 9,
}

public readonly record struct SmartWardensPaeanDecision(
    SmartWardensPaeanDecisionKind Kind,
    SmartWardensPaeanDecisionReason Reason,
    int SelectedCandidateIndex = -1,
    SmartWardensPaeanIntent? Intent = null)
{
    public bool ShouldRedirect =>
        Kind == SmartWardensPaeanDecisionKind.Redirect &&
        Intent is { IsValid: true };
}

/// <summary>
/// Pure, stateless policy for the default-off Smart Warden's Paean helper.
/// A complete and unambiguous exact five-member CC party view is required
/// before selection. Only a non-self ally without the live PvP ward and with
/// current, known pressure from at least three unique enemies may be selected.
/// A redirect freezes one P-slot and actor identity; failed final revalidation
/// is terminal and must suppress that original call rather than fall back,
/// substitute, rerank, or retry.
/// Every non-redirect decision preserves the original action call unchanged.
/// </summary>
public static class SmartWardensPaeanTargetRules
{
    public const uint BardJobId = 23;
    public const uint ActionId = 29_400;
    public const uint WardensPaeanWardStatusId = 3_143;
    public const int MinimumIncomingEnemyCount = 3;
    public const int MinimumExactPartyViewSize = 2;
    public const int RequiredCrystallineConflictPartySize = 5;
    public const int FirstPartySlot = 1;
    public const int LastPartySlot = 8;

    public static SmartWardensPaeanDecision Observe(
        SmartWardensPaeanObservation observation)
    {
        var gateFailure = GetGateFailure(observation);
        if (gateFailure != SmartWardensPaeanDecisionReason.None)
            return Vanilla(gateFailure);

        if (!observation.CompleteExactPartyView ||
            !HasCompleteExactPartyView(
                observation.Candidates,
                observation.LocalPlayer))
        {
            return Vanilla(
                SmartWardensPaeanDecisionReason.IncompleteExactPartyView);
        }

        var selectedIndex = SelectBestCandidateIndex(
            observation.Candidates,
            observation.LocalPlayer);
        if (selectedIndex < 0)
        {
            return Vanilla(
                SmartWardensPaeanDecisionReason.NoKnownPressureTarget);
        }

        var candidate = observation.Candidates![selectedIndex];
        var intent = new SmartWardensPaeanIntent(
            observation.ResolvedActionId,
            candidate.PartySlot,
            observation.LocalPlayer,
            candidate.Actor,
            candidate.UniqueIncomingEnemyCount);
        return new SmartWardensPaeanDecision(
            SmartWardensPaeanDecisionKind.Redirect,
            SmartWardensPaeanDecisionReason.None,
            selectedIndex,
            intent);
    }

    /// <summary>
    /// A usable CC party view contains a stable exact subset of two to five
    /// actors, exactly one local-player entry, and no duplicate or partially
    /// conflicting P-slot, GameObjectId, or EntityId identity. A temporarily
    /// incomplete PartyList must not globally disable a safe exact ally which
    /// is present in both snapshots.
    /// </summary>
    public static bool HasCompleteExactPartyView(
        IReadOnlyList<SmartWardensPaeanCandidate>? candidates,
        TargetPressureActorIdentity localPlayer)
    {
        if (candidates is null ||
            candidates.Count is < MinimumExactPartyViewSize or
                > RequiredCrystallineConflictPartySize ||
            !localPlayer.IsValid)
        {
            return false;
        }

        var occupiedSlots = new HashSet<int>();
        var occupiedGameObjectIds = new HashSet<ulong>();
        var occupiedEntityIds = new HashSet<uint>();
        var localEntries = 0;
        foreach (var candidate in candidates)
        {
            var isExactLocal = candidate.Actor == localPlayer;
            if (candidate.PartySlot is < FirstPartySlot or > LastPartySlot ||
                !candidate.ExactPartyIdentity ||
                !candidate.Actor.IsValid ||
                candidate.IsSelf != isExactLocal ||
                !occupiedSlots.Add(candidate.PartySlot) ||
                !occupiedGameObjectIds.Add(candidate.Actor.GameObjectId) ||
                !occupiedEntityIds.Add(candidate.Actor.EntityId))
            {
                return false;
            }

            if (isExactLocal) localEntries++;
        }

        return localEntries == 1;
    }

    public static bool IsEligibleCandidate(
        SmartWardensPaeanCandidate candidate,
        TargetPressureActorIdentity localPlayer) =>
        localPlayer.IsValid &&
        candidate.PartySlot is >= FirstPartySlot and <= LastPartySlot &&
        candidate.ExactPartyIdentity &&
        candidate.Actor.IsValid &&
        candidate.Actor != localPlayer &&
        !candidate.IsSelf &&
        candidate.Alive &&
        candidate.Targetable &&
        !candidate.HasWardensPaeanWard &&
        candidate.CurrentHp > 0 &&
        candidate.MaximumHp > 0 &&
        candidate.CurrentHp <= candidate.MaximumHp &&
        candidate.NativeTargetValid &&
        candidate.NativeRangeAndLineOfSight &&
        candidate.PressureKnown &&
        candidate.UniqueIncomingEnemyCount >= MinimumIncomingEnemyCount;

    public static bool IsWardensPaeanWardStatus(uint statusId) =>
        statusId == WardensPaeanWardStatusId;

    public static int SelectBestCandidateIndex(
        IReadOnlyList<SmartWardensPaeanCandidate>? candidates,
        TargetPressureActorIdentity localPlayer)
    {
        if (!HasCompleteExactPartyView(candidates, localPlayer)) return -1;

        var bestIndex = -1;
        for (var index = 0; index < candidates!.Count; index++)
        {
            var candidate = candidates[index];
            if (!IsEligibleCandidate(candidate, localPlayer)) continue;

            if (bestIndex < 0 ||
                Compare(candidate, candidates[bestIndex]) < 0)
            {
                bestIndex = index;
            }
        }

        return bestIndex;
    }

    /// <summary>
    /// Revalidates only the frozen P-slot and actor. Pressure is read again and
    /// must still be exactly known at or above the threshold, but its count and
    /// every other party member are deliberately not used to rerank or replace
    /// the frozen intent.
    /// </summary>
    public static bool CanUseFrozenIntent(
        SmartWardensPaeanIntent intent,
        SmartWardensPaeanCandidate currentCandidate,
        bool configurationEnabled,
        bool isCrystallineConflict,
        uint currentLocalJobId,
        TargetPressureActorIdentity currentLocalPlayer,
        bool isLocalPlayerAlive,
        bool metadataVerified,
        uint resolvedActionId) =>
        intent.IsValid &&
        configurationEnabled &&
        isCrystallineConflict &&
        currentLocalJobId == BardJobId &&
        currentLocalPlayer == intent.LocalPlayer &&
        isLocalPlayerAlive &&
        metadataVerified &&
        resolvedActionId == intent.ActionId &&
        currentCandidate.PartySlot == intent.PartySlot &&
        currentCandidate.Actor == intent.Target &&
        IsEligibleCandidate(currentCandidate, currentLocalPlayer);

    private static SmartWardensPaeanDecisionReason GetGateFailure(
        SmartWardensPaeanObservation observation)
    {
        if (!observation.ConfigurationEnabled)
            return SmartWardensPaeanDecisionReason.ConfigurationDisabled;
        if (!observation.IsCrystallineConflict)
            return SmartWardensPaeanDecisionReason.OutsideCrystallineConflict;
        if (!observation.LocalPlayer.IsValid)
            return SmartWardensPaeanDecisionReason.LocalPlayerIdentityInvalid;
        if (!observation.IsLocalPlayerAlive)
            return SmartWardensPaeanDecisionReason.LocalPlayerDead;
        if (observation.LocalJobId != BardJobId)
            return SmartWardensPaeanDecisionReason.LocalJobInvalid;
        if (!observation.MetadataVerified)
            return SmartWardensPaeanDecisionReason.MetadataUnverified;
        if (observation.ResolvedActionId != ActionId)
            return SmartWardensPaeanDecisionReason.ResolvedActionInvalid;

        return SmartWardensPaeanDecisionReason.None;
    }

    private static SmartWardensPaeanDecision Vanilla(
        SmartWardensPaeanDecisionReason reason) =>
        new(SmartWardensPaeanDecisionKind.Vanilla, reason);

    private static int Compare(
        SmartWardensPaeanCandidate left,
        SmartWardensPaeanCandidate right)
    {
        var pressure = right.UniqueIncomingEnemyCount.CompareTo(
            left.UniqueIncomingEnemyCount);
        if (pressure != 0) return pressure;

        var health = ((ulong)left.CurrentHp * right.MaximumHp).CompareTo(
            (ulong)right.CurrentHp * left.MaximumHp);
        if (health != 0) return health;

        var partySlot = left.PartySlot.CompareTo(right.PartySlot);
        if (partySlot != 0) return partySlot;

        var entityId = left.Actor.EntityId.CompareTo(right.Actor.EntityId);
        return entityId != 0
            ? entityId
            : left.Actor.GameObjectId.CompareTo(right.Actor.GameObjectId);
    }
}
