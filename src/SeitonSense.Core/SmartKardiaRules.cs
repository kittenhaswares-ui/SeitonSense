namespace SeitonSense.Core;

/// <summary>
/// One exact Crystalline Conflict party-list actor considered by the optional
/// held-key Smart Kardia helper. Incoming pressure must be the current unique
/// hard/cast target union; historical action hints are not an eligible source.
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
/// The one action and exact party actor selected for the current physical-key
/// generation. Runtime code must never rerank or substitute another actor
/// after this intent has been created.
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
        SelectedIncomingEnemyCount >=
            SmartKardiaRules.MinimumIncomingEnemyCount;
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
    bool InputProbeSucceeded,
    bool IsTextInputActive,
    bool HeldGameplayKeyEligible,
    uint ResolvedActionId,
    bool ActionLocallyReady,
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
    InputProbeUnavailable = 10,
    TextInputActive = 11,
    NoHeldGameplayKey = 12,
    ResolvedActionInvalid = 13,
    ActionNotReady = 14,
    IncompleteExactPartyView = 15,
    NoKnownPressureTarget = 16,
    SelectedKardionStateUnknown = 17,
    SelectedAlreadyHasOwnKardion = 18,
    IncompleteKnownPressureView = 19,
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

    /// <summary>
    /// The shared physical input generation must be consumed before final
    /// revalidation or the sole native action request. Failure after that point
    /// is terminal: there is no alternate target, fallback, or retry.
    /// </summary>
    public bool ShouldConsumeInputGeneration => ShouldDispatch;
}

/// <summary>
/// Pure policy for the default-off Sage PvP Smart Kardia helper. It requires a
/// complete, unambiguous five-member CC party view and one held gameplay-key
/// generation. Self and exact party allies are eligible at known direct
/// incoming pressure from at least two unique enemies. Selection is stable,
/// freezes one exact actor, and never navigates, changes the hard target,
/// substitutes, buffers, or retries.
/// </summary>
public static class SmartKardiaRules
{
    public const uint SageJobId = 40;
    public const uint ActionId = 29_264;
    public const uint KardiaStatusId = 2_871;
    public const uint KardionStatusId = 2_872;
    public const int MinimumIncomingEnemyCount = 2;
    public const int RequiredCrystallineConflictPartySize = 5;
    public const int FirstPartySlot = 1;
    public const int LastPartySlot = 8;

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
            return None(SmartKardiaDecisionReason.NoKnownPressureTarget);

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

    public static bool IsKardiaStatus(uint statusId) =>
        statusId == KardiaStatusId;

    public static bool IsKardionStatus(uint statusId) =>
        statusId == KardionStatusId;

    /// <summary>
    /// A complete CC party view contains five exact unique actors, exactly one
    /// exact local-player entry, and no partial P-slot/GOID/EntityId collision.
    /// </summary>
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

    /// <summary>
    /// Every living, targetable party actor must have an explicit current
    /// pressure count, including known zero. Unknown pressure on a dead or
    /// untargetable actor cannot affect selection and is allowed.
    /// </summary>
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

        return bestIndex;
    }

    /// <summary>
    /// Final validation for only the frozen actor and action. Current pressure
    /// must remain known at threshold and the selected actor must still be
    /// proven not to own our Kardion. Callers must not invoke the selector
    /// again after consuming the physical-key generation.
    /// </summary>
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
        bool actionLocallyReady) =>
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
        currentCandidate.PartySlot == intent.PartySlot &&
        currentCandidate.Actor == intent.Target &&
        currentCandidate.IsSelf == intent.IsSelf &&
        IsEligibleCandidate(currentCandidate, currentLocalPlayer) &&
        currentCandidate.OwnKardionStateKnown &&
        !currentCandidate.HasOwnKardion;

    private static SmartKardiaDecisionReason GetGateFailure(
        SmartKardiaObservation observation)
    {
        if (observation.HardReset)
            return SmartKardiaDecisionReason.HardReset;
        if (!observation.ConfigurationEnabled)
            return SmartKardiaDecisionReason.ConfigurationDisabled;
        if (!observation.IsCrystallineConflict)
            return SmartKardiaDecisionReason.OutsideCrystallineConflict;
        if (!observation.LocalPlayer.IsValid)
            return SmartKardiaDecisionReason.LocalPlayerIdentityInvalid;
        if (!observation.IsLocalPlayerAlive)
            return SmartKardiaDecisionReason.LocalPlayerDead;
        if (observation.LocalJobId != SageJobId)
            return SmartKardiaDecisionReason.LocalJobInvalid;
        if (!observation.MetadataVerified)
            return SmartKardiaDecisionReason.MetadataUnverified;
        if (observation.ActionHelpersSuppressedByGuard)
            return SmartKardiaDecisionReason.GuardSuppressed;
        if (observation.HigherPriorityClaimed)
            return SmartKardiaDecisionReason.HigherPriorityClaimed;
        if (!observation.InputProbeSucceeded)
            return SmartKardiaDecisionReason.InputProbeUnavailable;
        if (observation.IsTextInputActive)
            return SmartKardiaDecisionReason.TextInputActive;
        if (!observation.HeldGameplayKeyEligible)
            return SmartKardiaDecisionReason.NoHeldGameplayKey;
        if (observation.ResolvedActionId != ActionId)
            return SmartKardiaDecisionReason.ResolvedActionInvalid;
        if (!observation.ActionLocallyReady)
            return SmartKardiaDecisionReason.ActionNotReady;
        if (!observation.CompleteExactPartyView)
            return SmartKardiaDecisionReason.IncompleteExactPartyView;

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
