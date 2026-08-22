namespace SeitonSense.Core;

public readonly record struct NinjaSeitonDispatchCandidate(
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
    uint ActionId,
    int EnemySlot,
    TargetPressureActorIdentity Target)
{
    public bool IsValid =>
        NinjaSeitonDispatchRules.IsExactSeitonAction(ActionId) &&
        EnemySlotRules.IsValidSlot(EnemySlot) &&
        Target.IsValid;
}

public readonly record struct NinjaSeitonAcceptedHoldState(
    bool OwnsHold,
    int HeldKeyCode,
    uint LastAcceptedActionId,
    bool FollowUpEpochSpent)
{
    public static NinjaSeitonAcceptedHoldState Initial => default;
}

public readonly record struct NinjaSeitonDispatchObservation(
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
    NoExactEligibleTarget = 15,
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
    /// revalidation or the native action boundary. Exact held-key episode
    /// ownership remains a caller-side state machine.
    /// </summary>
    public bool ShouldConsumeInputGeneration => ShouldDispatch;
}

/// <summary>
/// Pure selection policy for the default-off Ninja PvP Seiton helper. A held
/// gameplay-key episode selects the lowest exact HP ratio among currently
/// eligible canonical CC enemy slots, then freezes that one target and action.
/// </summary>
public static class NinjaSeitonDispatchRules
{
    public const uint BaseActionId = 29_515;
    public const uint FollowUpActionId = 29_516;

    public static NinjaSeitonAcceptedHoldState BeginAcceptedHold(
        int heldKeyCode,
        uint acceptedActionId) =>
        heldKeyCode > 0 && IsExactSeitonAction(acceptedActionId)
            ? new NinjaSeitonAcceptedHoldState(
                true,
                heldKeyCode,
                acceptedActionId,
                acceptedActionId == FollowUpActionId)
            : NinjaSeitonAcceptedHoldState.Initial;

    public static NinjaSeitonAcceptedHoldState RetireAdjustedActionEpoch(
        NinjaSeitonAcceptedHoldState state,
        uint actionId) =>
        state.OwnsHold &&
        state.LastAcceptedActionId == BaseActionId &&
        actionId == FollowUpActionId
            ? state with { FollowUpEpochSpent = true }
            : state;

    public static NinjaSeitonAcceptedHoldState ObserveAcceptedHold(
        NinjaSeitonAcceptedHoldState state,
        bool hardReset,
        bool ownershipContextValid,
        bool exactHeldKeyStillDown) =>
        state.OwnsHold &&
        (hardReset || !ownershipContextValid || !exactHeldKeyStillDown)
            ? NinjaSeitonAcceptedHoldState.Initial
            : state;

    public static bool CanOpenAdjustedActionEpoch(
        NinjaSeitonAcceptedHoldState state,
        uint resolvedActionId) =>
        state.OwnsHold &&
        state.LastAcceptedActionId == BaseActionId &&
        !state.FollowUpEpochSpent &&
        resolvedActionId == FollowUpActionId;

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
            observation.LocalPlayer);
        if (selectedIndex < 0)
        {
            return new NinjaSeitonDispatchDecision(
                NinjaSeitonDispatchDecisionKind.None,
                NinjaSeitonDispatchDecisionReason.NoExactEligibleTarget);
        }

        var candidate = observation.Candidates![selectedIndex];
        var intent = new NinjaSeitonDispatchIntent(
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

    public static int SelectBestCandidateIndex(
        IReadOnlyList<NinjaSeitonDispatchCandidate>? candidates,
        TargetPressureActorIdentity localPlayer)
    {
        if (candidates is null || candidates.Count == 0 || !localPlayer.IsValid)
            return -1;

        var occupiedSlots = new HashSet<int>();
        var occupiedActors = new HashSet<TargetPressureActorIdentity>();
        var bestIndex = -1;
        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            if (!IsEligibleCandidate(candidate, localPlayer)) continue;

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
        uint resolvedActionId,
        bool actionLocallyReady) =>
        intent.IsValid &&
        actionLocallyReady &&
        resolvedActionId == intent.ActionId &&
        candidate.EnemySlot == intent.EnemySlot &&
        candidate.Actor == intent.Target &&
        IsEligibleCandidate(candidate, localPlayer);

    private static NinjaSeitonDispatchDecisionReason GetGateFailure(
        NinjaSeitonDispatchObservation observation)
    {
        if (observation.HardReset)
            return NinjaSeitonDispatchDecisionReason.HardReset;
        if (!observation.ConfigurationEnabled)
            return NinjaSeitonDispatchDecisionReason.ConfigurationDisabled;
        if (!observation.IsCrystallineConflict)
            return NinjaSeitonDispatchDecisionReason.OutsideCrystallineConflict;
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
        if (!observation.InputProbeSucceeded)
            return NinjaSeitonDispatchDecisionReason.InputProbeUnavailable;
        if (observation.IsTextInputActive)
            return NinjaSeitonDispatchDecisionReason.TextInputActive;
        if (!observation.HeldGameplayKeyEligible)
            return NinjaSeitonDispatchDecisionReason.NoHeldGameplayKey;
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
