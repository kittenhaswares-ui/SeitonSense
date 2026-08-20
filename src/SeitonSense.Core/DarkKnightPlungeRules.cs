namespace SeitonSense.Core;

public readonly record struct DarkKnightPlungeCandidate(
    int EnemySlot,
    TargetPressureActorIdentity Actor,
    bool ExactCanonicalIdentity,
    bool Alive,
    bool Targetable,
    uint CurrentHp,
    uint MaximumHp,
    float CenterDistanceSquared,
    bool TargetGuardActive,
    bool HasValidActionTarget,
    bool HasNativeRangeAndLineOfSight);

public readonly record struct DarkKnightPlungeIntent(
    uint ActionId,
    int EnemySlot,
    TargetPressureActorIdentity Target,
    int HeldKeyCode,
    ulong ReadyEpochToken)
{
    public bool IsRepeat => ReadyEpochToken != 0;

    public bool IsValid =>
        ActionId == DarkKnightPlungeRules.ActionId &&
        EnemySlotRules.IsValidSlot(EnemySlot) &&
        Target.IsValid &&
        HeldKeyCode > 0;
}

/// <summary>
/// Ownership retained after the first client-accepted Plunge request. The first
/// epoch is already spent. A later epoch becomes available only after the
/// cooldown is independently observed unavailable and then ready again.
/// </summary>
public readonly record struct DarkKnightPlungeHoldState(
    bool OwnsHold,
    int HeldKeyCode,
    bool ObservedCooldownUnavailable,
    ulong CurrentReadyEpochToken,
    ulong SpentReadyEpochToken)
{
    public static DarkKnightPlungeHoldState Initial => default;

    public bool HasAvailableReadyEpoch =>
        OwnsHold &&
        CurrentReadyEpochToken != 0 &&
        CurrentReadyEpochToken != SpentReadyEpochToken;
}

public readonly record struct DarkKnightPlungeHoldObservation(
    bool HardReset,
    bool OwnershipContextValid,
    bool InputProbeSucceeded,
    bool IsTextInputActive,
    int HeldKeyCode,
    bool ExactHeldKeyStillDown,
    bool CooldownStateKnown,
    bool CooldownReady);

public enum DarkKnightPlungeHoldOutcome
{
    None = 0,
    Reset = 1,
    PreservedUnknown = 2,
    WaitingForReady = 3,
    OpenedReadyEpoch = 4,
    ReadyEpochUnchanged = 5,
}

public readonly record struct DarkKnightPlungeHoldDecision(
    DarkKnightPlungeHoldState State,
    DarkKnightPlungeHoldOutcome Outcome);

public readonly record struct DarkKnightPlungeObservation(
    bool ConfigurationEnabled,
    bool IsCrystallineConflict,
    uint LocalJobId,
    TargetPressureActorIdentity LocalPlayer,
    bool IsLocalPlayerAlive,
    bool IsLocalPlayerTargetable,
    bool MetadataVerified,
    bool ActionHelpersSuppressedByGuard,
    bool HigherPriorityClaimed,
    bool InputProbeSucceeded,
    bool IsTextInputActive,
    bool HeldGameplayKeyEligible,
    int HeldGameplayKeyCode,
    bool ExactOwnedKeyStillDown,
    DarkKnightPlungeHoldState HoldState,
    uint ResolvedActionId,
    bool CooldownStateKnown,
    bool CooldownReady,
    bool ActionStructurallyReady,
    IReadOnlyList<DarkKnightPlungeCandidate>? Candidates,
    bool HardReset = false);

public enum DarkKnightPlungeDecisionKind
{
    None = 0,
    Dispatch = 1,
    Cancelled = 2,
}

public enum DarkKnightPlungeDecisionReason
{
    None = 0,
    HardReset = 1,
    ConfigurationDisabled = 2,
    OutsideCrystallineConflict = 3,
    LocalPlayerIdentityInvalid = 4,
    LocalPlayerDead = 5,
    LocalPlayerUntargetable = 6,
    LocalJobInvalid = 7,
    MetadataUnverified = 8,
    GuardSuppressed = 9,
    HigherPriorityClaimed = 10,
    InputProbeUnavailable = 11,
    TextInputActive = 12,
    NoHeldGameplayKey = 13,
    OwnedHeldKeyReleased = 14,
    ResolvedActionInvalid = 15,
    CooldownStateUnknown = 16,
    ActionNotReady = 17,
    ReadyEpochSpent = 18,
    ActionStructurallyUnavailable = 19,
    NoExactEligibleTarget = 20,
}

public readonly record struct DarkKnightPlungeDecision(
    DarkKnightPlungeDecisionKind Kind,
    DarkKnightPlungeDecisionReason Reason,
    int SelectedCandidateIndex = -1,
    DarkKnightPlungeIntent? Intent = null)
{
    public bool ShouldDispatch =>
        Kind == DarkKnightPlungeDecisionKind.Dispatch &&
        Intent is { IsValid: true };

    public bool ShouldConsumeSharedInputGeneration =>
        ShouldDispatch && Intent is { IsRepeat: false };

    public bool ShouldSpendReadyEpoch =>
        ShouldDispatch && Intent is { IsRepeat: true };
}

/// <summary>
/// Pure policy for the default-off DRK PvP Plunge helper. The first request may
/// consume one ordinary held gameplay-key generation. After a client-accepted
/// request, that exact physical key may retain ownership, but another request is
/// possible only after an observed cooldown unavailable-to-ready transition.
/// Guarded enemies are never treated as execute opportunities.
/// </summary>
public static class DarkKnightPlungeRules
{
    public const uint DarkKnightJobId = 32;
    public const uint DarkKnightClassJobCategoryId = 98;
    public const uint ActionId = 29_092;
    public const uint IconId = 9_150;
    public const uint MaximumHpPercent = 30;
    public const float MaximumCenterDistanceYalms = 10f;
    public const float MaximumCenterDistanceSquared =
        MaximumCenterDistanceYalms * MaximumCenterDistanceYalms;
    public const int ExpectedRuntimeRecastGroupIndex = 1;
    public const int ExpectedAdjustedRecastMilliseconds = 12_000;

    public static DarkKnightPlungeHoldState BeginOwnedHold(int heldKeyCode) =>
        heldKeyCode <= 0
            ? DarkKnightPlungeHoldState.Initial
            : new DarkKnightPlungeHoldState(
                OwnsHold: true,
                heldKeyCode,
                ObservedCooldownUnavailable: false,
                CurrentReadyEpochToken: 1,
                SpentReadyEpochToken: 1);

    public static DarkKnightPlungeHoldDecision ObserveOwnedHold(
        DarkKnightPlungeHoldState state,
        DarkKnightPlungeHoldObservation observation)
    {
        if (!state.OwnsHold) return new(state, DarkKnightPlungeHoldOutcome.None);

        if (observation.HardReset ||
            !observation.OwnershipContextValid ||
            !observation.InputProbeSucceeded ||
            observation.IsTextInputActive ||
            observation.HeldKeyCode != state.HeldKeyCode ||
            !observation.ExactHeldKeyStillDown)
        {
            return new(
                DarkKnightPlungeHoldState.Initial,
                DarkKnightPlungeHoldOutcome.Reset);
        }

        if (!observation.CooldownStateKnown)
        {
            return new(state, DarkKnightPlungeHoldOutcome.PreservedUnknown);
        }

        if (!observation.CooldownReady)
        {
            return new(
                state with { ObservedCooldownUnavailable = true },
                DarkKnightPlungeHoldOutcome.WaitingForReady);
        }

        if (!state.ObservedCooldownUnavailable)
        {
            return new(state, DarkKnightPlungeHoldOutcome.ReadyEpochUnchanged);
        }

        return new(
            state with
            {
                ObservedCooldownUnavailable = false,
                CurrentReadyEpochToken = NextToken(state.CurrentReadyEpochToken),
            },
            DarkKnightPlungeHoldOutcome.OpenedReadyEpoch);
    }

    /// <summary>
    /// Spends a proven repeat epoch before final actor/action validation. Any
    /// drift, false return, exception, or server rejection is terminal for that
    /// epoch; it cannot be attempted again until another false-to-true cooldown
    /// transition is observed.
    /// </summary>
    public static bool TrySpendReadyEpoch(
        DarkKnightPlungeHoldState state,
        ulong expectedReadyEpochToken,
        out DarkKnightPlungeHoldState spentState)
    {
        spentState = state;
        if (!state.OwnsHold ||
            expectedReadyEpochToken == 0 ||
            state.CurrentReadyEpochToken != expectedReadyEpochToken ||
            state.SpentReadyEpochToken == expectedReadyEpochToken)
        {
            return false;
        }

        spentState = state with { SpentReadyEpochToken = expectedReadyEpochToken };
        return true;
    }

    public static DarkKnightPlungeDecision Evaluate(
        DarkKnightPlungeObservation observation)
    {
        var failure = GetGateFailure(observation);
        if (failure != DarkKnightPlungeDecisionReason.None)
        {
            return new DarkKnightPlungeDecision(
                observation.HardReset
                    ? DarkKnightPlungeDecisionKind.Cancelled
                    : DarkKnightPlungeDecisionKind.None,
                failure);
        }

        var selectedIndex = SelectBestCandidateIndex(
            observation.Candidates,
            observation.LocalPlayer);
        if (selectedIndex < 0)
        {
            return new DarkKnightPlungeDecision(
                DarkKnightPlungeDecisionKind.None,
                DarkKnightPlungeDecisionReason.NoExactEligibleTarget);
        }

        var candidate = observation.Candidates![selectedIndex];
        var heldKeyCode = observation.HoldState.OwnsHold
            ? observation.HoldState.HeldKeyCode
            : observation.HeldGameplayKeyCode;
        var epochToken = observation.HoldState.OwnsHold
            ? observation.HoldState.CurrentReadyEpochToken
            : 0;
        return new DarkKnightPlungeDecision(
            DarkKnightPlungeDecisionKind.Dispatch,
            DarkKnightPlungeDecisionReason.None,
            selectedIndex,
            new DarkKnightPlungeIntent(
                observation.ResolvedActionId,
                candidate.EnemySlot,
                candidate.Actor,
                heldKeyCode,
                epochToken));
    }

    public static bool IsAtOrBelowExecuteThreshold(uint currentHp, uint maximumHp) =>
        HasValidHp(currentHp, maximumHp) &&
        (ulong)currentHp * 100UL <= (ulong)maximumHp * MaximumHpPercent;

    public static bool IsWithinMaximumCenterDistance(float centerDistanceSquared) =>
        float.IsFinite(centerDistanceSquared) &&
        centerDistanceSquared >= 0f &&
        centerDistanceSquared <= MaximumCenterDistanceSquared;

    public static bool IsEligibleCandidate(
        DarkKnightPlungeCandidate candidate,
        TargetPressureActorIdentity localPlayer) =>
        localPlayer.IsValid &&
        candidate.Actor.IsValid &&
        candidate.Actor != localPlayer &&
        EnemySlotRules.IsValidSlot(candidate.EnemySlot) &&
        candidate.ExactCanonicalIdentity &&
        candidate.Alive &&
        candidate.Targetable &&
        !candidate.TargetGuardActive &&
        IsAtOrBelowExecuteThreshold(candidate.CurrentHp, candidate.MaximumHp) &&
        IsWithinMaximumCenterDistance(candidate.CenterDistanceSquared) &&
        candidate.HasValidActionTarget &&
        candidate.HasNativeRangeAndLineOfSight;

    public static int SelectBestCandidateIndex(
        IReadOnlyList<DarkKnightPlungeCandidate>? candidates,
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

    public static bool CanUseExactIntent(
        DarkKnightPlungeIntent intent,
        DarkKnightPlungeCandidate candidate,
        TargetPressureActorIdentity currentLocalPlayer,
        bool configurationEnabled,
        bool isCrystallineConflict,
        uint localJobId,
        bool localAliveAndTargetable,
        bool metadataVerified,
        bool actionHelpersSuppressedByGuard,
        bool higherPriorityClaimed,
        bool inputProbeSucceeded,
        bool isTextInputActive,
        bool exactHeldKeyStillDown,
        uint resolvedActionId,
        bool cooldownStateKnown,
        bool cooldownReady,
        bool actionStructurallyReady) =>
        intent.IsValid &&
        candidate.EnemySlot == intent.EnemySlot &&
        candidate.Actor == intent.Target &&
        currentLocalPlayer.IsValid &&
        configurationEnabled &&
        isCrystallineConflict &&
        localJobId == DarkKnightJobId &&
        localAliveAndTargetable &&
        metadataVerified &&
        !actionHelpersSuppressedByGuard &&
        !higherPriorityClaimed &&
        inputProbeSucceeded &&
        !isTextInputActive &&
        exactHeldKeyStillDown &&
        resolvedActionId == intent.ActionId &&
        cooldownStateKnown &&
        cooldownReady &&
        actionStructurallyReady &&
        IsEligibleCandidate(candidate, currentLocalPlayer);

    private static DarkKnightPlungeDecisionReason GetGateFailure(
        DarkKnightPlungeObservation observation)
    {
        if (observation.HardReset)
            return DarkKnightPlungeDecisionReason.HardReset;
        if (!observation.ConfigurationEnabled)
            return DarkKnightPlungeDecisionReason.ConfigurationDisabled;
        if (!observation.IsCrystallineConflict)
            return DarkKnightPlungeDecisionReason.OutsideCrystallineConflict;
        if (!observation.LocalPlayer.IsValid)
            return DarkKnightPlungeDecisionReason.LocalPlayerIdentityInvalid;
        if (!observation.IsLocalPlayerAlive)
            return DarkKnightPlungeDecisionReason.LocalPlayerDead;
        if (!observation.IsLocalPlayerTargetable)
            return DarkKnightPlungeDecisionReason.LocalPlayerUntargetable;
        if (observation.LocalJobId != DarkKnightJobId)
            return DarkKnightPlungeDecisionReason.LocalJobInvalid;
        if (!observation.MetadataVerified)
            return DarkKnightPlungeDecisionReason.MetadataUnverified;
        if (observation.ActionHelpersSuppressedByGuard)
            return DarkKnightPlungeDecisionReason.GuardSuppressed;
        if (observation.HigherPriorityClaimed)
            return DarkKnightPlungeDecisionReason.HigherPriorityClaimed;
        if (!observation.InputProbeSucceeded)
            return DarkKnightPlungeDecisionReason.InputProbeUnavailable;
        if (observation.IsTextInputActive)
            return DarkKnightPlungeDecisionReason.TextInputActive;

        if (observation.HoldState.OwnsHold)
        {
            if (!observation.ExactOwnedKeyStillDown)
                return DarkKnightPlungeDecisionReason.OwnedHeldKeyReleased;
        }
        else if (!observation.HeldGameplayKeyEligible ||
                 observation.HeldGameplayKeyCode <= 0)
        {
            return DarkKnightPlungeDecisionReason.NoHeldGameplayKey;
        }

        if (observation.ResolvedActionId != ActionId)
            return DarkKnightPlungeDecisionReason.ResolvedActionInvalid;
        if (!observation.CooldownStateKnown)
            return DarkKnightPlungeDecisionReason.CooldownStateUnknown;
        if (!observation.CooldownReady)
            return DarkKnightPlungeDecisionReason.ActionNotReady;
        if (observation.HoldState.OwnsHold &&
            !observation.HoldState.HasAvailableReadyEpoch)
        {
            return DarkKnightPlungeDecisionReason.ReadyEpochSpent;
        }

        if (!observation.ActionStructurallyReady)
            return DarkKnightPlungeDecisionReason.ActionStructurallyUnavailable;
        return DarkKnightPlungeDecisionReason.None;
    }

    private static bool HasValidHp(uint currentHp, uint maximumHp) =>
        currentHp > 0 && maximumHp > 0 && currentHp <= maximumHp;

    private static int Compare(
        DarkKnightPlungeCandidate left,
        DarkKnightPlungeCandidate right)
    {
        var health = ((ulong)left.CurrentHp * right.MaximumHp).CompareTo(
            (ulong)right.CurrentHp * left.MaximumHp);
        if (health != 0) return health;

        var slot = left.EnemySlot.CompareTo(right.EnemySlot);
        if (slot != 0) return slot;

        var entity = left.Actor.EntityId.CompareTo(right.Actor.EntityId);
        return entity != 0
            ? entity
            : left.Actor.GameObjectId.CompareTo(right.Actor.GameObjectId);
    }

    private static ulong NextToken(ulong current) =>
        current == ulong.MaxValue ? 1UL : current + 1UL;
}
