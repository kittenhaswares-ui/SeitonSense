namespace SeitonSense.Core;

/// <summary>
/// One exact Crystalline Conflict party-list actor considered by the optional
/// Eukrasia-triggered Smart Kardia helper. Incoming pressure must be the current
/// unique hard/cast target union; historical action hints are not eligible.
/// </summary>
public readonly record struct SmartKardiaCandidate(
    int PartySlot,
    TargetPressureActorIdentity Actor,
    bool ExactPartyIdentity,
    bool IsSelf,
    bool Alive,
    bool Targetable,
    uint CurrentHp,
    uint MaximumHp,
    bool NativeTargetValid,
    bool NativeRangeAndLineOfSight,
    bool PressureKnown,
    int UniqueIncomingEnemyCount,
    bool OwnKardionStateKnown,
    bool HasOwnKardion);

/// <summary>
/// One exact observation of the local Sage's PvP Eukrasia state. Both the
/// adjusted action identity and the source-owned status state must be known.
/// </summary>
public readonly record struct SmartKardiaEukrasiaEvidence(
    uint AdjustedActionId,
    uint CurrentCharges,
    bool OwnStatusStateKnown,
    bool HasOwnEukrasia)
{
    public bool IsValid =>
        AdjustedActionId == SmartKardiaRules.EukrasiaActionId &&
        CurrentCharges <= SmartKardiaRules.EukrasiaMaximumCharges &&
        OwnStatusStateKnown;
}

/// <summary>
/// A bounded, one-shot opportunity created only after the existing native hook
/// forwarded one exact incoming Eukrasia call and the client accepted it.
/// </summary>
public readonly record struct SmartKardiaEukrasiaTrigger(
    long Token,
    long AcceptedAtMilliseconds,
    long ExpiresAtMilliseconds,
    uint TerritoryId,
    TargetPressureActorIdentity LocalPlayer,
    SmartKardiaEukrasiaEvidence Before)
{
    public bool IsValid =>
        Token > 0 &&
        AcceptedAtMilliseconds >= 0 &&
        ExpiresAtMilliseconds > AcceptedAtMilliseconds &&
        TerritoryId != 0 &&
        LocalPlayer.IsValid &&
        Before.IsValid &&
        Before.CurrentCharges > 0;
}

/// <summary>
/// The one action and exact party actor selected for the accepted Eukrasia
/// opportunity. Runtime code must never rerank or substitute another actor.
/// </summary>
public readonly record struct SmartKardiaIntent(
    uint ActionId,
    int PartySlot,
    TargetPressureActorIdentity LocalPlayer,
    TargetPressureActorIdentity Target,
    bool IsSelf,
    int SelectedIncomingEnemyCount)
{
    public bool IsValid =>
        ActionId == SmartKardiaRules.ActionId &&
        PartySlot is >= SmartKardiaRules.FirstPartySlot and
            <= SmartKardiaRules.LastPartySlot &&
        LocalPlayer.IsValid &&
        Target.IsValid &&
        IsSelf == (Target == LocalPlayer) &&
        SelectedIncomingEnemyCount >= 0 &&
        (IsSelf ||
         SelectedIncomingEnemyCount >=
             SmartKardiaRules.MinimumIncomingEnemyCount);
}

public readonly record struct SmartKardiaObservation(
    bool ConfigurationEnabled,
    bool IsCrystallineConflict,
    uint LocalJobId,
    TargetPressureActorIdentity LocalPlayer,
    bool IsLocalPlayerAlive,
    bool MetadataVerified,
    bool ActionHelpersSuppressedByGuard,
    bool HigherPriorityClaimed,
    bool TriggerAvailable,
    bool TriggerEvidenceConfirmed,
    bool FreshPressurePublicationAvailable,
    uint ResolvedActionId,
    bool ActionLocallyReady,
    bool AnimationLockClear,
    bool CompleteExactPartyView,
    IReadOnlyList<SmartKardiaCandidate>? Candidates,
    bool HardReset = false);

public enum SmartKardiaDecisionKind
{
    None = 0,
    Dispatch = 1,
    Cancelled = 2,
}

public enum SmartKardiaDecisionReason
{
    None = 0,
    HardReset = 1,
    ConfigurationDisabled = 2,
    OutsideCrystallineConflict = 3,
    LocalPlayerIdentityInvalid = 4,
    LocalPlayerDead = 5,
    LocalJobInvalid = 6,
    MetadataUnverified = 7,
    GuardSuppressed = 8,
    HigherPriorityClaimed = 9,
    ResolvedActionInvalid = 13,
    ActionNotReady = 14,
    IncompleteExactPartyView = 15,
    SelectedKardionStateUnknown = 17,
    SelectedAlreadyHasOwnKardion = 18,
    IncompleteKnownPressureView = 19,
    NoEligiblePressureOrSelfTarget = 22,
    EukrasiaTriggerUnavailable = 23,
    EukrasiaEvidencePending = 24,
    PressurePublicationPending = 25,
    AnimationLockActive = 26,
}

public readonly record struct SmartKardiaDecision(
    SmartKardiaDecisionKind Kind,
    SmartKardiaDecisionReason Reason,
    int SelectedCandidateIndex = -1,
    SmartKardiaIntent? Intent = null)
{
    public bool ShouldDispatch =>
        Kind == SmartKardiaDecisionKind.Dispatch &&
        Intent is { IsValid: true };
}

/// <summary>
/// Pure policy for the default-off Sage PvP Smart Kardia helper. One accepted
/// Eukrasia call creates at most one short-lived opportunity. The helper then
/// requires causal charge/status evidence and a fresh, coherent five-member CC
/// party-pressure view. Pressure-qualified actors (including self) are ranked
/// by pressure, exact HP ratio, party slot, EntityId, and GOID. If none qualifies,
/// exact self is the sole fallback. No hard/focus target is involved.
/// </summary>
public static class SmartKardiaRules
{
    public const uint SageJobId = 40;
    public const uint ActionId = 29_264;
    public const uint EukrasiaActionId = 29_258;
    public const uint KardiaStatusId = 2_871;
    public const uint KardionStatusId = 2_872;
    public const uint EukrasiaStatusId = 3_107;
    public const int MinimumIncomingEnemyCount = 2;
    public const uint EukrasiaMaximumCharges = 2;
    public const long TriggerLifetimeMilliseconds = 2_000;
    public const int RequiredCrystallineConflictPartySize = 5;
    public const int FirstPartySlot = 1;
    public const int LastPartySlot = 8;

    public static bool TryCreateAcceptedTrigger(
        long token,
        long acceptedAtMilliseconds,
        uint territoryId,
        TargetPressureActorIdentity localPlayer,
        SmartKardiaEukrasiaEvidence before,
        out SmartKardiaEukrasiaTrigger trigger)
    {
        trigger = default;
        if (token <= 0 ||
            acceptedAtMilliseconds < 0 ||
            acceptedAtMilliseconds >= long.MaxValue ||
            territoryId == 0 ||
            !localPlayer.IsValid ||
            !before.IsValid ||
            before.CurrentCharges == 0)
        {
            return false;
        }

        var expiresAt = acceptedAtMilliseconds >
                        long.MaxValue - TriggerLifetimeMilliseconds
            ? long.MaxValue
            : acceptedAtMilliseconds + TriggerLifetimeMilliseconds;
        trigger = new SmartKardiaEukrasiaTrigger(
            token,
            acceptedAtMilliseconds,
            expiresAt,
            territoryId,
            localPlayer,
            before);
        return trigger.IsValid;
    }

    public static bool IsTriggerCurrent(
        SmartKardiaEukrasiaTrigger trigger,
        long nowMilliseconds,
        uint territoryId,
        TargetPressureActorIdentity localPlayer) =>
        trigger.IsValid &&
        nowMilliseconds >= trigger.AcceptedAtMilliseconds &&
        nowMilliseconds < trigger.ExpiresAtMilliseconds &&
        trigger.TerritoryId == territoryId &&
        trigger.LocalPlayer == localPlayer;

    /// <summary>
    /// The accepted call is causal only after either its charge has disappeared
    /// or a previously absent, exact local-source Eukrasia status has appeared.
    /// </summary>
    public static bool HasCausalEukrasiaEvidence(
        SmartKardiaEukrasiaTrigger trigger,
        SmartKardiaEukrasiaEvidence current) =>
        trigger.IsValid &&
        current.IsValid &&
        (current.CurrentCharges < trigger.Before.CurrentCharges ||
         (!trigger.Before.HasOwnEukrasia && current.HasOwnEukrasia));

    public static SmartKardiaDecision Observe(SmartKardiaObservation observation)
    {
        var gateFailure = GetGateFailure(observation);
        if (gateFailure != SmartKardiaDecisionReason.None)
            return Cancelled(gateFailure);

        if (!observation.CompleteExactPartyView ||
            !HasCompleteExactPartyView(
                observation.Candidates,
                observation.LocalPlayer))
        {
            return Cancelled(
                SmartKardiaDecisionReason.IncompleteExactPartyView);
        }

        if (!HasCompleteKnownPressureView(observation.Candidates))
        {
            return None(
                SmartKardiaDecisionReason.IncompleteKnownPressureView);
        }

        var selectedIndex = SelectBestCandidateIndex(
            observation.Candidates,
            observation.LocalPlayer);
        if (selectedIndex < 0)
        {
            return None(
                SmartKardiaDecisionReason.NoEligiblePressureOrSelfTarget);
        }

        // Status ownership is deliberately inspected only after ranking. If
        // the one best actor is unknown or already owns our Kardion, do not
        // fall through to a lower-ranked alternate.
        var candidate = observation.Candidates![selectedIndex];
        if (!candidate.OwnKardionStateKnown)
        {
            return None(
                SmartKardiaDecisionReason.SelectedKardionStateUnknown,
                selectedIndex);
        }

        if (candidate.HasOwnKardion)
        {
            return None(
                SmartKardiaDecisionReason.SelectedAlreadyHasOwnKardion,
                selectedIndex);
        }

        var intent = new SmartKardiaIntent(
            observation.ResolvedActionId,
            candidate.PartySlot,
            observation.LocalPlayer,
            candidate.Actor,
            candidate.IsSelf,
            candidate.UniqueIncomingEnemyCount);
        return new SmartKardiaDecision(
            SmartKardiaDecisionKind.Dispatch,
            SmartKardiaDecisionReason.None,
            selectedIndex,
            intent);
    }

    public static bool IsKardiaStatus(uint statusId) => statusId == KardiaStatusId;
    public static bool IsKardionStatus(uint statusId) => statusId == KardionStatusId;
    public static bool IsEukrasiaStatus(uint statusId) => statusId == EukrasiaStatusId;

    public static bool HasCompleteExactPartyView(
        IReadOnlyList<SmartKardiaCandidate>? candidates,
        TargetPressureActorIdentity localPlayer)
    {
        if (candidates is null ||
            candidates.Count != RequiredCrystallineConflictPartySize ||
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

    public static bool HasCompleteKnownPressureView(
        IReadOnlyList<SmartKardiaCandidate>? candidates) =>
        candidates is not null &&
        candidates.All(static candidate =>
            !candidate.Alive ||
            !candidate.Targetable ||
            (candidate.PressureKnown &&
             candidate.UniqueIncomingEnemyCount >= 0));

    public static bool IsEligibleCandidate(
        SmartKardiaCandidate candidate,
        TargetPressureActorIdentity localPlayer) =>
        localPlayer.IsValid &&
        candidate.PartySlot is >= FirstPartySlot and <= LastPartySlot &&
        candidate.ExactPartyIdentity &&
        candidate.Actor.IsValid &&
        candidate.IsSelf == (candidate.Actor == localPlayer) &&
        candidate.Alive &&
        candidate.Targetable &&
        candidate.CurrentHp > 0 &&
        candidate.MaximumHp > 0 &&
        candidate.CurrentHp <= candidate.MaximumHp &&
        candidate.NativeTargetValid &&
        candidate.NativeRangeAndLineOfSight &&
        candidate.PressureKnown &&
        candidate.UniqueIncomingEnemyCount >= MinimumIncomingEnemyCount;

    public static bool IsEligibleSelfFallback(
        SmartKardiaCandidate candidate,
        TargetPressureActorIdentity localPlayer) =>
        localPlayer.IsValid &&
        candidate.PartySlot is >= FirstPartySlot and <= LastPartySlot &&
        candidate.ExactPartyIdentity &&
        candidate.Actor == localPlayer &&
        candidate.IsSelf &&
        candidate.Alive &&
        candidate.Targetable &&
        candidate.CurrentHp > 0 &&
        candidate.MaximumHp > 0 &&
        candidate.CurrentHp <= candidate.MaximumHp &&
        candidate.NativeTargetValid &&
        candidate.NativeRangeAndLineOfSight &&
        candidate.PressureKnown &&
        candidate.UniqueIncomingEnemyCount >= 0;

    public static int SelectBestCandidateIndex(
        IReadOnlyList<SmartKardiaCandidate>? candidates,
        TargetPressureActorIdentity localPlayer)
    {
        if (!HasCompleteExactPartyView(candidates, localPlayer) ||
            !HasCompleteKnownPressureView(candidates))
        {
            return -1;
        }

        var bestIndex = -1;
        for (var index = 0; index < candidates!.Count; index++)
        {
            var candidate = candidates[index];
            if (!IsEligibleCandidate(candidate, localPlayer)) continue;

            if (bestIndex < 0 || Compare(candidate, candidates[bestIndex]) < 0)
                bestIndex = index;
        }

        if (bestIndex >= 0) return bestIndex;

        for (var index = 0; index < candidates.Count; index++)
        {
            if (IsEligibleSelfFallback(candidates[index], localPlayer))
                return index;
        }

        return -1;
    }

    public static bool CanUseFrozenIntent(
        SmartKardiaIntent intent,
        SmartKardiaCandidate currentCandidate,
        bool configurationEnabled,
        bool isCrystallineConflict,
        uint currentLocalJobId,
        TargetPressureActorIdentity currentLocalPlayer,
        bool isLocalPlayerAlive,
        bool metadataVerified,
        bool actionHelpersSuppressedByGuard,
        uint resolvedActionId,
        bool actionLocallyReady,
        bool animationLockClear,
        bool triggerEvidenceConfirmed) =>
        intent.IsValid &&
        configurationEnabled &&
        isCrystallineConflict &&
        currentLocalJobId == SageJobId &&
        currentLocalPlayer == intent.LocalPlayer &&
        isLocalPlayerAlive &&
        metadataVerified &&
        !actionHelpersSuppressedByGuard &&
        resolvedActionId == intent.ActionId &&
        actionLocallyReady &&
        animationLockClear &&
        triggerEvidenceConfirmed &&
        currentCandidate.PartySlot == intent.PartySlot &&
        currentCandidate.Actor == intent.Target &&
        currentCandidate.IsSelf == intent.IsSelf &&
        (intent.IsSelf
            ? IsEligibleSelfFallback(currentCandidate, currentLocalPlayer)
            : IsEligibleCandidate(currentCandidate, currentLocalPlayer)) &&
        currentCandidate.OwnKardionStateKnown &&
        !currentCandidate.HasOwnKardion;

    private static SmartKardiaDecisionReason GetGateFailure(
        SmartKardiaObservation observation)
    {
        if (observation.HardReset) return SmartKardiaDecisionReason.HardReset;
        if (!observation.ConfigurationEnabled) return SmartKardiaDecisionReason.ConfigurationDisabled;
        if (!observation.IsCrystallineConflict) return SmartKardiaDecisionReason.OutsideCrystallineConflict;
        if (!observation.LocalPlayer.IsValid) return SmartKardiaDecisionReason.LocalPlayerIdentityInvalid;
        if (!observation.IsLocalPlayerAlive) return SmartKardiaDecisionReason.LocalPlayerDead;
        if (observation.LocalJobId != SageJobId) return SmartKardiaDecisionReason.LocalJobInvalid;
        if (!observation.MetadataVerified) return SmartKardiaDecisionReason.MetadataUnverified;
        if (observation.ActionHelpersSuppressedByGuard) return SmartKardiaDecisionReason.GuardSuppressed;
        if (observation.HigherPriorityClaimed) return SmartKardiaDecisionReason.HigherPriorityClaimed;
        if (!observation.TriggerAvailable) return SmartKardiaDecisionReason.EukrasiaTriggerUnavailable;
        if (!observation.TriggerEvidenceConfirmed) return SmartKardiaDecisionReason.EukrasiaEvidencePending;
        if (!observation.FreshPressurePublicationAvailable) return SmartKardiaDecisionReason.PressurePublicationPending;
        if (observation.ResolvedActionId != ActionId) return SmartKardiaDecisionReason.ResolvedActionInvalid;
        if (!observation.ActionLocallyReady) return SmartKardiaDecisionReason.ActionNotReady;
        if (!observation.AnimationLockClear) return SmartKardiaDecisionReason.AnimationLockActive;
        if (!observation.CompleteExactPartyView) return SmartKardiaDecisionReason.IncompleteExactPartyView;

        return SmartKardiaDecisionReason.None;
    }

    private static SmartKardiaDecision None(
        SmartKardiaDecisionReason reason,
        int selectedCandidateIndex = -1) =>
        new(SmartKardiaDecisionKind.None, reason, selectedCandidateIndex);

    private static SmartKardiaDecision Cancelled(
        SmartKardiaDecisionReason reason) =>
        new(SmartKardiaDecisionKind.Cancelled, reason);

    private static int Compare(
        SmartKardiaCandidate left,
        SmartKardiaCandidate right)
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
