namespace SeitonSense.Core;

public readonly record struct NinjaSeitonDispatchCandidate(
    SupportedPvPContext Context,
    int EnemySlot,
    TargetPressureActorIdentity Actor,
    bool ExactCanonicalIdentity,
    bool Alive,
    bool Targetable,
    uint CurrentHp,
    uint MaximumHp,
    uint ExecuteBlockingStatusId,
    bool HasValidActionTarget,
    bool HasNativeRangeAndLineOfSight)
{
    public bool HasExecuteBlockingProtection =>
        NinjaSeitonProtectionStatusCatalog.IsExecuteBlockingStatus(
            ExecuteBlockingStatusId);
}

/// <summary>
/// Installed Patch 7.55 status rows that make a Ninja Seiton request useless
/// or redirect it away from the selected enemy. All same-semantic Covered
/// rows are retained because exact CC has used duplicate status rows across
/// game-data revisions; the covering Paladin's Cover rows are deliberately
/// excluded.
/// </summary>
public static class NinjaSeitonProtectionStatusCatalog
{
    public const uint CoveredLegacyStatusId = 81;
    public const uint CoveredStatusId = 1_301;
    public const uint CoveredPvpStatusId = 2_413;
    public const uint CoveredPvpAlternateStatusId = 4_352;
    public const uint HallowedGroundStatusId = 1_302;
    public const uint UndeadRedemptionStatusId = 3_039;

    public static bool IsExecuteBlockingStatus(uint statusId) =>
        statusId is CoveredLegacyStatusId or
            CoveredStatusId or
            CoveredPvpStatusId or
            CoveredPvpAlternateStatusId or
            HallowedGroundStatusId or
            UndeadRedemptionStatusId;
}

public readonly record struct NinjaSeitonDispatchIntent(
    SupportedPvPContext Context,
    uint ActionId,
    int EnemySlot,
    TargetPressureActorIdentity Target)
{
    public bool IsValid =>
        Context != SupportedPvPContext.None &&
        NinjaSeitonDispatchRules.IsExactSeitonAction(ActionId) &&
        EnemySlotRules.IsValidSlot(EnemySlot) &&
        Target.IsValid;
}

public readonly record struct NinjaSeitonAvailabilityEpochState(
    bool Active,
    uint ActionId,
    bool Spent)
{
    public static NinjaSeitonAvailabilityEpochState Initial => default;
}

public readonly record struct NinjaSeitonDispatchObservation(
    bool ConfigurationEnabled,
    SupportedPvPContext Context,
    uint LocalJobId,
    TargetPressureActorIdentity LocalPlayer,
    bool IsLocalPlayerAlive,
    bool MetadataVerified,
    bool ActionHelpersSuppressedByGuard,
    bool HigherPriorityClaimed,
    bool AvailabilityEpochOpen,
    uint ResolvedActionId,
    bool ActionLocallyReady,
    IReadOnlyList<NinjaSeitonDispatchCandidate>? Candidates,
    bool HardReset = false);

public enum NinjaSeitonDispatchDecisionKind
{
    None = 0,
    Dispatch = 1,
    Cancelled = 2,
}

public enum NinjaSeitonDispatchDecisionReason
{
    None = 0,
    HardReset = 1,
    ConfigurationDisabled = 2,
    OutsideSupportedPvPContext = 3,
    LocalPlayerIdentityInvalid = 4,
    LocalPlayerDead = 5,
    LocalJobInvalid = 6,
    MetadataUnverified = 7,
    GuardSuppressed = 8,
    HigherPriorityClaimed = 9,
    AvailabilityEpochClosed = 10,
    ResolvedActionInvalid = 11,
    ActionNotReady = 12,
    NoExactEligibleTarget = 13,
}

public readonly record struct NinjaSeitonDispatchDecision(
    NinjaSeitonDispatchDecisionKind Kind,
    NinjaSeitonDispatchDecisionReason Reason,
    int SelectedCandidateIndex = -1,
    NinjaSeitonDispatchIntent? Intent = null)
{
    public bool ShouldDispatch =>
        Kind == NinjaSeitonDispatchDecisionKind.Dispatch &&
        Intent is { IsValid: true };

    /// <summary>
    /// The caller claims only the current scheduler frame before final
    /// revalidation or the native action boundary. Availability-epoch
    /// ownership remains a caller-side state machine.
    /// </summary>
    public bool ShouldConsumeInputGeneration => ShouldDispatch;
}

/// <summary>
/// Pure selection policy for the default-off automatic Ninja PvP Seiton helper.
/// One exact locally-ready adjusted-action availability epoch selects the
/// lowest exact HP ratio among currently eligible canonical enemies, then
/// freezes that one target and action.
/// </summary>
public static class NinjaSeitonDispatchRules
{
    public const uint BaseActionId = 29_515;
    public const uint FollowUpActionId = 29_516;

    public static NinjaSeitonAvailabilityEpochState ObserveAvailabilityEpoch(
        NinjaSeitonAvailabilityEpochState state,
        bool hardReset,
        bool ownershipContextValid,
        bool availabilityReady,
        uint resolvedActionId)
    {
        if (hardReset || !ownershipContextValid ||
            !availabilityReady || !IsExactSeitonAction(resolvedActionId))
        {
            return NinjaSeitonAvailabilityEpochState.Initial;
        }

        // A stable adjusted action remains the same availability epoch even if
        // another scheduler lane temporarily owns a frame. A real 29515/29516
        // transition is a new exact epoch and may dispatch once independently.
        return !state.Active || state.ActionId != resolvedActionId
            ? new NinjaSeitonAvailabilityEpochState(
                Active: true,
                ActionId: resolvedActionId,
                Spent: false)
            : state;
    }

    public static bool CanOpenAdjustedActionEpoch(
        NinjaSeitonAvailabilityEpochState state,
        uint resolvedActionId) =>
        state.Active &&
        !state.Spent &&
        state.ActionId == resolvedActionId &&
        IsExactSeitonAction(resolvedActionId);

    public static NinjaSeitonAvailabilityEpochState SpendAdjustedActionEpoch(
        NinjaSeitonAvailabilityEpochState state,
        uint actionId) =>
        state.Active && state.ActionId == actionId && IsExactSeitonAction(actionId)
            ? state with { Spent = true }
            : state;

    public static NinjaSeitonAvailabilityEpochState CancelFrozenAvailabilityEpoch(
        NinjaSeitonAvailabilityEpochState state,
        uint actionId,
        int priorNativeAttemptCount) =>
        priorNativeAttemptCount == 0
            ? state
            : SpendAdjustedActionEpoch(state, actionId);

    public static NinjaSeitonDispatchDecision Observe(
        NinjaSeitonDispatchObservation observation)
    {
        var gateFailure = GetGateFailure(observation);
        if (gateFailure != NinjaSeitonDispatchDecisionReason.None)
        {
            return new NinjaSeitonDispatchDecision(
                NinjaSeitonDispatchDecisionKind.Cancelled,
                gateFailure);
        }

        var selectedIndex = SelectBestCandidateIndex(
            observation.Candidates,
            observation.LocalPlayer,
            observation.Context);
        if (selectedIndex < 0)
        {
            return new NinjaSeitonDispatchDecision(
                NinjaSeitonDispatchDecisionKind.None,
                NinjaSeitonDispatchDecisionReason.NoExactEligibleTarget);
        }

        var candidate = observation.Candidates![selectedIndex];
        var intent = new NinjaSeitonDispatchIntent(
            observation.Context,
            observation.ResolvedActionId,
            candidate.EnemySlot,
            candidate.Actor);
        return new NinjaSeitonDispatchDecision(
            NinjaSeitonDispatchDecisionKind.Dispatch,
            NinjaSeitonDispatchDecisionReason.None,
            selectedIndex,
            intent);
    }

    public static bool IsExactSeitonAction(uint actionId) =>
        actionId is BaseActionId or FollowUpActionId;

    public static bool IsEligibleCandidate(
        NinjaSeitonDispatchCandidate candidate,
        TargetPressureActorIdentity localPlayer) =>
        localPlayer.IsValid &&
        candidate.Context != SupportedPvPContext.None &&
        candidate.Actor.IsValid &&
        candidate.Actor != localPlayer &&
        EnemySlotRules.IsValidSlot(candidate.EnemySlot) &&
        candidate.ExactCanonicalIdentity &&
        candidate.Alive &&
        candidate.Targetable &&
        ExecuteThreshold.IsBelowHalf(candidate.CurrentHp, candidate.MaximumHp) &&
        !candidate.HasExecuteBlockingProtection &&
        candidate.HasValidActionTarget &&
        candidate.HasNativeRangeAndLineOfSight;

    public static bool IsEligibleCandidate(
        NinjaSeitonDispatchCandidate candidate,
        TargetPressureActorIdentity localPlayer,
        SupportedPvPContext context) =>
        context != SupportedPvPContext.None &&
        candidate.Context == context &&
        IsEligibleCandidate(candidate, localPlayer);

    public static int SelectBestCandidateIndex(
        IReadOnlyList<NinjaSeitonDispatchCandidate>? candidates,
        TargetPressureActorIdentity localPlayer,
        SupportedPvPContext context = SupportedPvPContext.CrystallineConflict)
    {
        if (candidates is null || candidates.Count == 0 || !localPlayer.IsValid)
            return -1;

        var occupiedSlots = new HashSet<int>();
        var occupiedActors = new HashSet<TargetPressureActorIdentity>();
        var bestIndex = -1;
        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            if (!IsEligibleCandidate(candidate, localPlayer, context)) continue;

            // One canonical CC actor must occupy exactly one native enemy slot.
            // Duplicate slots or actors make the whole candidate set ambiguous,
            // so list order is never allowed to invent a target.
            if (!occupiedSlots.Add(candidate.EnemySlot) ||
                !occupiedActors.Add(candidate.Actor))
            {
                return -1;
            }

            if (bestIndex < 0 || Compare(candidate, candidates[bestIndex]) < 0)
                bestIndex = index;
        }

        return bestIndex;
    }

    /// <summary>
    /// Final validation for only the frozen intent. Callers must not run the
    /// selector again after consuming input; drift simply cancels the attempt.
    /// </summary>
    public static bool CanUseExactIntent(
        NinjaSeitonDispatchIntent intent,
        NinjaSeitonDispatchCandidate candidate,
        TargetPressureActorIdentity localPlayer,
        SupportedPvPContext context,
        uint resolvedActionId,
        bool actionLocallyReady) =>
        intent.IsValid &&
        actionLocallyReady &&
        resolvedActionId == intent.ActionId &&
        context == intent.Context &&
        candidate.Context == intent.Context &&
        candidate.EnemySlot == intent.EnemySlot &&
        candidate.Actor == intent.Target &&
        IsEligibleCandidate(candidate, localPlayer, context);

    private static NinjaSeitonDispatchDecisionReason GetGateFailure(
        NinjaSeitonDispatchObservation observation)
    {
        if (observation.HardReset)
            return NinjaSeitonDispatchDecisionReason.HardReset;
        if (!observation.ConfigurationEnabled)
            return NinjaSeitonDispatchDecisionReason.ConfigurationDisabled;
        if (observation.Context == SupportedPvPContext.None)
            return NinjaSeitonDispatchDecisionReason.OutsideSupportedPvPContext;
        if (!observation.LocalPlayer.IsValid)
            return NinjaSeitonDispatchDecisionReason.LocalPlayerIdentityInvalid;
        if (!observation.IsLocalPlayerAlive)
            return NinjaSeitonDispatchDecisionReason.LocalPlayerDead;
        if (!ExecuteThreshold.IsNinja(observation.LocalJobId))
            return NinjaSeitonDispatchDecisionReason.LocalJobInvalid;
        if (!observation.MetadataVerified)
            return NinjaSeitonDispatchDecisionReason.MetadataUnverified;
        if (observation.ActionHelpersSuppressedByGuard)
            return NinjaSeitonDispatchDecisionReason.GuardSuppressed;
        if (observation.HigherPriorityClaimed)
            return NinjaSeitonDispatchDecisionReason.HigherPriorityClaimed;
        if (!observation.AvailabilityEpochOpen)
            return NinjaSeitonDispatchDecisionReason.AvailabilityEpochClosed;
        if (!IsExactSeitonAction(observation.ResolvedActionId))
            return NinjaSeitonDispatchDecisionReason.ResolvedActionInvalid;
        if (!observation.ActionLocallyReady)
            return NinjaSeitonDispatchDecisionReason.ActionNotReady;

        return NinjaSeitonDispatchDecisionReason.None;
    }

    private static int Compare(
        NinjaSeitonDispatchCandidate left,
        NinjaSeitonDispatchCandidate right)
    {
        var health = CompareRatio(
            left.CurrentHp,
            left.MaximumHp,
            right.CurrentHp,
            right.MaximumHp);
        if (health != 0) return health;

        var slot = left.EnemySlot.CompareTo(right.EnemySlot);
        if (slot != 0) return slot;

        var entity = left.Actor.EntityId.CompareTo(right.Actor.EntityId);
        return entity != 0
            ? entity
            : left.Actor.GameObjectId.CompareTo(right.Actor.GameObjectId);
    }

    private static int CompareRatio(
        uint leftCurrent,
        uint leftMaximum,
        uint rightCurrent,
        uint rightMaximum) =>
        ((ulong)leftCurrent * rightMaximum).CompareTo(
            (ulong)rightCurrent * leftMaximum);
}
