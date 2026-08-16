namespace SeitonSense.Core;

public readonly record struct ScholarCriticalStrategyCandidate(
    int EnemySlot,
    TargetPressureActorIdentity Actor,
    bool ExactCanonicalIdentity,
    bool Alive,
    bool Targetable,
    uint CurrentHp,
    uint MaximumHp,
    bool GuardActive,
    bool NativeTargetValid,
    bool NativeRangeAndLineOfSight,
    bool PressureKnown,
    int TeamTargetCount);

public readonly record struct ScholarCriticalStrategyIntent(
    uint ActionId,
    int EnemySlot,
    TargetPressureActorIdentity LocalPlayer,
    TargetPressureActorIdentity Target,
    bool PressureKnown,
    int TeamTargetCount)
{
    public bool IsValid =>
        ActionId == ScholarCriticalStrategyRules.ActionId &&
        EnemySlotRules.IsValidSlot(EnemySlot) &&
        LocalPlayer.IsValid &&
        Target.IsValid &&
        LocalPlayer != Target;
}

public readonly record struct ScholarCriticalStrategyObservation(
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
    bool CompleteCanonicalEnemySet,
    IReadOnlyList<ScholarCriticalStrategyCandidate>? Candidates,
    bool HardReset = false);

public enum ScholarCriticalStrategyDecisionKind
{
    None = 0,
    Dispatch = 1,
    Cancelled = 2,
}

public enum ScholarCriticalStrategyDecisionReason
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
    IncompleteCanonicalEnemySet = 15,
    NoExactEligibleTarget = 16,
}

public readonly record struct ScholarCriticalStrategyDecision(
    ScholarCriticalStrategyDecisionKind Kind,
    ScholarCriticalStrategyDecisionReason Reason,
    int SelectedCandidateIndex = -1,
    ScholarCriticalStrategyIntent? Intent = null)
{
    public bool ShouldDispatch =>
        Kind == ScholarCriticalStrategyDecisionKind.Dispatch &&
        Intent is { IsValid: true };

    /// <summary>
    /// The shared physical input generation must be consumed before the caller
    /// revalidates the frozen intent or crosses its sole native action boundary.
    /// Every failed revalidation or client request is terminal for that generation.
    /// </summary>
    public bool ShouldConsumeInputGeneration => ShouldDispatch;
}

/// <summary>
/// Pure policy for the default-off Scholar PvP Critical Strategy helper. A held
/// gameplay-key generation can select only one guarded, reachable actor from a
/// complete exact canonical S1-S5 set. Pressure is used wholesale only when it
/// is known and nonnegative for every eligible candidate and at least one count
/// is positive; otherwise exact HP ratio is the first ordering key. Selection
/// freezes one intent and never substitutes, reranks, buffers, or retries it.
/// </summary>
public static class ScholarCriticalStrategyRules
{
    public const uint ScholarJobId = 28;
    public const uint ActionId = 29_716;
    public const uint GuardStatusId = 3_054;
    public const uint GuardStatusLargeScaleId = 3_673;

    public static ScholarCriticalStrategyDecision Observe(
        ScholarCriticalStrategyObservation observation)
    {
        var gateFailure = GetGateFailure(observation);
        if (gateFailure != ScholarCriticalStrategyDecisionReason.None)
        {
            return new ScholarCriticalStrategyDecision(
                ScholarCriticalStrategyDecisionKind.Cancelled,
                gateFailure);
        }

        if (!HasCompleteExactCanonicalSet(
                observation.Candidates,
                observation.LocalPlayer))
        {
            return new ScholarCriticalStrategyDecision(
                ScholarCriticalStrategyDecisionKind.Cancelled,
                ScholarCriticalStrategyDecisionReason.IncompleteCanonicalEnemySet);
        }

        var selectedIndex = SelectBestCandidateIndex(
            observation.Candidates,
            observation.LocalPlayer);
        if (selectedIndex < 0)
        {
            return new ScholarCriticalStrategyDecision(
                ScholarCriticalStrategyDecisionKind.None,
                ScholarCriticalStrategyDecisionReason.NoExactEligibleTarget);
        }

        var candidate = observation.Candidates![selectedIndex];
        var intent = new ScholarCriticalStrategyIntent(
            observation.ResolvedActionId,
            candidate.EnemySlot,
            observation.LocalPlayer,
            candidate.Actor,
            candidate.PressureKnown,
            candidate.TeamTargetCount);
        return new ScholarCriticalStrategyDecision(
            ScholarCriticalStrategyDecisionKind.Dispatch,
            ScholarCriticalStrategyDecisionReason.None,
            selectedIndex,
            intent);
    }

    public static bool IsExactGuardStatus(uint statusId) =>
        statusId is GuardStatusId or GuardStatusLargeScaleId;

    public static bool HasCompleteExactCanonicalSet(
        IReadOnlyList<ScholarCriticalStrategyCandidate>? candidates,
        TargetPressureActorIdentity localPlayer)
    {
        if (candidates is null ||
            candidates.Count != EnemySlotRules.LastSlot ||
            !localPlayer.IsValid)
        {
            return false;
        }

        var occupiedSlots = new HashSet<int>();
        var occupiedActors = new HashSet<TargetPressureActorIdentity>();
        foreach (var candidate in candidates)
        {
            if (!EnemySlotRules.IsValidSlot(candidate.EnemySlot) ||
                !candidate.ExactCanonicalIdentity ||
                !candidate.Actor.IsValid ||
                candidate.Actor == localPlayer ||
                !occupiedSlots.Add(candidate.EnemySlot) ||
                !occupiedActors.Add(candidate.Actor))
            {
                return false;
            }
        }

        for (var slot = EnemySlotRules.FirstSlot;
             slot <= EnemySlotRules.LastSlot;
             slot++)
        {
            if (!occupiedSlots.Contains(slot)) return false;
        }

        return true;
    }

    public static bool IsEligibleCandidate(
        ScholarCriticalStrategyCandidate candidate,
        TargetPressureActorIdentity localPlayer) =>
        localPlayer.IsValid &&
        candidate.Actor.IsValid &&
        candidate.Actor != localPlayer &&
        EnemySlotRules.IsValidSlot(candidate.EnemySlot) &&
        candidate.ExactCanonicalIdentity &&
        candidate.Alive &&
        candidate.Targetable &&
        candidate.CurrentHp > 0 &&
        candidate.MaximumHp > 0 &&
        candidate.CurrentHp <= candidate.MaximumHp &&
        candidate.GuardActive &&
        candidate.NativeTargetValid &&
        candidate.NativeRangeAndLineOfSight;

    public static int SelectBestCandidateIndex(
        IReadOnlyList<ScholarCriticalStrategyCandidate>? candidates,
        TargetPressureActorIdentity localPlayer)
    {
        if (!HasCompleteExactCanonicalSet(candidates, localPlayer)) return -1;

        var eligibleIndices = new List<int>(EnemySlotRules.LastSlot);
        for (var index = 0; index < candidates!.Count; index++)
        {
            if (IsEligibleCandidate(candidates[index], localPlayer))
                eligibleIndices.Add(index);
        }

        if (eligibleIndices.Count == 0) return -1;

        var useTeamPressure = eligibleIndices.All(index =>
                                  candidates[index].PressureKnown &&
                                  candidates[index].TeamTargetCount >= 0) &&
                              eligibleIndices.Any(index =>
                                  candidates[index].TeamTargetCount > 0);
        var bestIndex = eligibleIndices[0];
        for (var index = 1; index < eligibleIndices.Count; index++)
        {
            var candidateIndex = eligibleIndices[index];
            if (Compare(
                    candidates[candidateIndex],
                    candidates[bestIndex],
                    useTeamPressure) < 0)
            {
                bestIndex = candidateIndex;
            }
        }

        return bestIndex;
    }

    /// <summary>
    /// Revalidates only the frozen actor and action. Current HP may change and no
    /// other candidate is reconsidered. Pressure is frozen for diagnostics only
    /// and deliberately is not reread or used as a post-consumption gate.
    /// </summary>
    public static bool CanUseExactIntent(
        ScholarCriticalStrategyIntent intent,
        ScholarCriticalStrategyCandidate currentCandidate,
        TargetPressureActorIdentity currentLocal,
        uint resolvedActionId,
        bool actionLocallyReady) =>
        intent.IsValid &&
        currentLocal.IsValid &&
        currentLocal == intent.LocalPlayer &&
        actionLocallyReady &&
        resolvedActionId == intent.ActionId &&
        currentCandidate.EnemySlot == intent.EnemySlot &&
        currentCandidate.Actor == intent.Target &&
        IsEligibleCandidate(currentCandidate, currentLocal);

    private static ScholarCriticalStrategyDecisionReason GetGateFailure(
        ScholarCriticalStrategyObservation observation)
    {
        if (observation.HardReset)
            return ScholarCriticalStrategyDecisionReason.HardReset;
        if (!observation.ConfigurationEnabled)
            return ScholarCriticalStrategyDecisionReason.ConfigurationDisabled;
        if (!observation.IsCrystallineConflict)
            return ScholarCriticalStrategyDecisionReason.OutsideCrystallineConflict;
        if (!observation.LocalPlayer.IsValid)
            return ScholarCriticalStrategyDecisionReason.LocalPlayerIdentityInvalid;
        if (!observation.IsLocalPlayerAlive)
            return ScholarCriticalStrategyDecisionReason.LocalPlayerDead;
        if (observation.LocalJobId != ScholarJobId)
            return ScholarCriticalStrategyDecisionReason.LocalJobInvalid;
        if (!observation.MetadataVerified)
            return ScholarCriticalStrategyDecisionReason.MetadataUnverified;
        if (observation.ActionHelpersSuppressedByGuard)
            return ScholarCriticalStrategyDecisionReason.GuardSuppressed;
        if (observation.HigherPriorityClaimed)
            return ScholarCriticalStrategyDecisionReason.HigherPriorityClaimed;
        if (!observation.InputProbeSucceeded)
            return ScholarCriticalStrategyDecisionReason.InputProbeUnavailable;
        if (observation.IsTextInputActive)
            return ScholarCriticalStrategyDecisionReason.TextInputActive;
        if (!observation.HeldGameplayKeyEligible)
            return ScholarCriticalStrategyDecisionReason.NoHeldGameplayKey;
        if (observation.ResolvedActionId != ActionId)
            return ScholarCriticalStrategyDecisionReason.ResolvedActionInvalid;
        if (!observation.ActionLocallyReady)
            return ScholarCriticalStrategyDecisionReason.ActionNotReady;
        if (!observation.CompleteCanonicalEnemySet)
            return ScholarCriticalStrategyDecisionReason.IncompleteCanonicalEnemySet;

        return ScholarCriticalStrategyDecisionReason.None;
    }

    private static int Compare(
        ScholarCriticalStrategyCandidate left,
        ScholarCriticalStrategyCandidate right,
        bool useTeamPressure)
    {
        if (useTeamPressure)
        {
            var pressure = right.TeamTargetCount.CompareTo(left.TeamTargetCount);
            if (pressure != 0) return pressure;
        }

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
