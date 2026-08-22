namespace SeitonSense.Core;

public readonly record struct NinjaGuardShukuchiPoint(float X, float Y, float Z)
{
    public bool IsFinite =>
        float.IsFinite(X) &&
        float.IsFinite(Y) &&
        float.IsFinite(Z);
}

public readonly record struct NinjaGuardShukuchiCandidate(
    int EnemySlot,
    TargetPressureActorIdentity Actor,
    bool ExactCanonicalIdentity,
    bool Alive,
    bool Targetable,
    uint CurrentHp,
    uint MaximumHp,
    bool GuardActive,
    NinjaGuardShukuchiPoint Position,
    bool WithinNativeRange,
    bool PressureKnown,
    int TeamTargetCount);

public readonly record struct NinjaGuardShukuchiIntent(
    uint ActionId,
    int EnemySlot,
    TargetPressureActorIdentity LocalPlayer,
    TargetPressureActorIdentity Target,
    bool PressureKnown,
    int TeamTargetCount)
{
    public bool IsValid =>
        ActionId == NinjaGuardShukuchiRules.ActionId &&
        EnemySlotRules.IsValidSlot(EnemySlot) &&
        LocalPlayer.IsValid &&
        Target.IsValid &&
        LocalPlayer != Target;
}

public readonly record struct NinjaGuardShukuchiHoldState(
    bool OwnsHold,
    int HeldKeyCode,
    bool ObservedCooldownUnavailable,
    ulong CurrentReadyEpochToken,
    ulong SpentReadyEpochToken)
{
    public static NinjaGuardShukuchiHoldState Initial => default;

    public bool HasAvailableReadyEpoch =>
        OwnsHold &&
        CurrentReadyEpochToken != 0 &&
        CurrentReadyEpochToken != SpentReadyEpochToken;
}

public readonly record struct NinjaGuardShukuchiObservation(
    bool ConfigurationEnabled,
    bool IsCrystallineConflict,
    uint LocalJobId,
    TargetPressureActorIdentity LocalPlayer,
    bool IsLocalPlayerAliveAndTargetable,
    bool MetadataVerified,
    bool ActionHelpersSuppressedByGuard,
    bool HigherPriorityClaimed,
    bool InputProbeSucceeded,
    bool IsTextInputActive,
    bool HeldGameplayKeyEligible,
    uint ResolvedActionId,
    bool ActionLocallyReady,
    IReadOnlyList<NinjaGuardShukuchiCandidate>? Candidates,
    bool HardReset = false);

public enum NinjaGuardShukuchiDecisionKind
{
    None = 0,
    Dispatch = 1,
    Cancelled = 2,
}

public enum NinjaGuardShukuchiDecisionReason
{
    None = 0,
    HardReset = 1,
    ConfigurationDisabled = 2,
    OutsideCrystallineConflict = 3,
    LocalPlayerIdentityInvalid = 4,
    LocalPlayerDeadOrUntargetable = 5,
    LocalJobInvalid = 6,
    MetadataUnverified = 7,
    GuardSuppressed = 8,
    HigherPriorityClaimed = 9,
    InputProbeUnavailable = 10,
    TextInputActive = 11,
    NoHeldGameplayKey = 12,
    ResolvedActionInvalid = 13,
    ActionNotReady = 14,
    AmbiguousCandidates = 15,
    NoExactGuardedLowHpTarget = 16,
}

public readonly record struct NinjaGuardShukuchiDecision(
    NinjaGuardShukuchiDecisionKind Kind,
    NinjaGuardShukuchiDecisionReason Reason,
    int SelectedCandidateIndex = -1,
    NinjaGuardShukuchiIntent? Intent = null)
{
    public bool ShouldDispatch =>
        Kind == NinjaGuardShukuchiDecisionKind.Dispatch &&
        Intent is { IsValid: true };

    public bool ShouldConsumeInputGeneration => ShouldDispatch;
}

/// <summary>
/// Pure fail-closed policy for the default-off NIN held Guard-Shukuchi helper.
/// It selects one exact canonical enemy only while that enemy has a live Guard
/// row and strictly less than 20 percent HP. Positive fresh pressure may improve
/// ranking but is never required. The frozen actor can never be replaced.
/// </summary>
public static class NinjaGuardShukuchiRules
{
    public const uint NinjaJobId = 30;
    public const uint ActionId = 29_513;
    public const uint GuardStatusId = 3_054;
    public const uint GuardStatusAlternateId = 3_673;
    public const float NativeMaximumRangeYalms = 20f;

    public static NinjaGuardShukuchiHoldState BeginAcceptedHold(int heldKeyCode) =>
        heldKeyCode <= 0
            ? NinjaGuardShukuchiHoldState.Initial
            : new NinjaGuardShukuchiHoldState(true, heldKeyCode, false, 1, 1);

    public static NinjaGuardShukuchiHoldState ObserveAcceptedHold(
        NinjaGuardShukuchiHoldState state,
        bool hardReset,
        bool ownershipContextValid,
        bool exactHeldKeyStillDown,
        bool cooldownStateKnown,
        bool cooldownReady)
    {
        if (!state.OwnsHold) return state;
        if (hardReset || !ownershipContextValid || !exactHeldKeyStillDown)
            return NinjaGuardShukuchiHoldState.Initial;
        if (!cooldownStateKnown) return state;
        if (!cooldownReady)
            return state with { ObservedCooldownUnavailable = true };
        if (!state.ObservedCooldownUnavailable) return state;

        return state with
        {
            ObservedCooldownUnavailable = false,
            CurrentReadyEpochToken = NextToken(state.CurrentReadyEpochToken),
        };
    }

    public static bool TrySpendReadyEpoch(
        NinjaGuardShukuchiHoldState state,
        ulong expectedToken,
        out NinjaGuardShukuchiHoldState spent)
    {
        spent = state;
        if (!state.HasAvailableReadyEpoch ||
            expectedToken == 0 ||
            state.CurrentReadyEpochToken != expectedToken)
        {
            return false;
        }

        spent = state with { SpentReadyEpochToken = expectedToken };
        return true;
    }

    public static bool IsExactGuardStatus(uint statusId) =>
        statusId is GuardStatusId or GuardStatusAlternateId;

    public static bool IsStrictlyBelowTwentyPercent(uint currentHp, uint maximumHp) =>
        currentHp > 0 &&
        maximumHp > 0 &&
        currentHp <= maximumHp &&
        (ulong)currentHp * 100UL < (ulong)maximumHp * 20UL;

    public static bool IsWithinNativeRange(
        NinjaGuardShukuchiPoint origin,
        NinjaGuardShukuchiPoint destination)
    {
        if (!origin.IsFinite || !destination.IsFinite) return false;

        var deltaX = (double)destination.X - origin.X;
        var deltaY = (double)destination.Y - origin.Y;
        var deltaZ = (double)destination.Z - origin.Z;
        var distanceSquared = (deltaX * deltaX) +
                              (deltaY * deltaY) +
                              (deltaZ * deltaZ);
        return double.IsFinite(distanceSquared) &&
               distanceSquared <=
               (double)NativeMaximumRangeYalms * NativeMaximumRangeYalms;
    }

    public static bool IsEligibleCandidate(
        NinjaGuardShukuchiCandidate candidate,
        TargetPressureActorIdentity localPlayer) =>
        localPlayer.IsValid &&
        candidate.Actor.IsValid &&
        candidate.Actor != localPlayer &&
        EnemySlotRules.IsValidSlot(candidate.EnemySlot) &&
        candidate.ExactCanonicalIdentity &&
        candidate.Alive &&
        candidate.Targetable &&
        IsStrictlyBelowTwentyPercent(candidate.CurrentHp, candidate.MaximumHp) &&
        candidate.GuardActive &&
        candidate.Position.IsFinite &&
        candidate.WithinNativeRange &&
        (!candidate.PressureKnown || candidate.TeamTargetCount >= 0);

    public static bool HasUnambiguousCandidateIdentities(
        IReadOnlyList<NinjaGuardShukuchiCandidate>? candidates,
        TargetPressureActorIdentity localPlayer)
    {
        if (candidates is null || !localPlayer.IsValid) return false;

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

        return true;
    }

    public static int SelectBestCandidateIndex(
        IReadOnlyList<NinjaGuardShukuchiCandidate>? candidates,
        TargetPressureActorIdentity localPlayer)
    {
        if (!HasUnambiguousCandidateIdentities(candidates, localPlayer)) return -1;

        var bestIndex = -1;
        for (var index = 0; index < candidates!.Count; index++)
        {
            if (!IsEligibleCandidate(candidates[index], localPlayer)) continue;
            if (bestIndex < 0 || Compare(candidates[index], candidates[bestIndex]) < 0)
                bestIndex = index;
        }

        return bestIndex;
    }

    public static NinjaGuardShukuchiDecision Observe(
        NinjaGuardShukuchiObservation observation)
    {
        var failure = GetGateFailure(observation);
        if (failure != NinjaGuardShukuchiDecisionReason.None)
        {
            return new NinjaGuardShukuchiDecision(
                NinjaGuardShukuchiDecisionKind.Cancelled,
                failure);
        }

        if (!HasUnambiguousCandidateIdentities(
                observation.Candidates,
                observation.LocalPlayer))
        {
            return new NinjaGuardShukuchiDecision(
                NinjaGuardShukuchiDecisionKind.Cancelled,
                NinjaGuardShukuchiDecisionReason.AmbiguousCandidates);
        }

        var selectedIndex = SelectBestCandidateIndex(
            observation.Candidates,
            observation.LocalPlayer);
        if (selectedIndex < 0)
        {
            return new NinjaGuardShukuchiDecision(
                NinjaGuardShukuchiDecisionKind.None,
                NinjaGuardShukuchiDecisionReason.NoExactGuardedLowHpTarget);
        }

        var candidate = observation.Candidates![selectedIndex];
        return new NinjaGuardShukuchiDecision(
            NinjaGuardShukuchiDecisionKind.Dispatch,
            NinjaGuardShukuchiDecisionReason.None,
            selectedIndex,
            new NinjaGuardShukuchiIntent(
                observation.ResolvedActionId,
                candidate.EnemySlot,
                observation.LocalPlayer,
                candidate.Actor,
                candidate.PressureKnown,
                candidate.TeamTargetCount));
    }

    /// <summary>
    /// Revalidates only the frozen actor. HP, Guard, range, and current position
    /// are reread, while pressure remains diagnostic and cannot cancel or rerank.
    /// </summary>
    public static bool CanUseExactIntent(
        NinjaGuardShukuchiIntent intent,
        NinjaGuardShukuchiCandidate currentCandidate,
        TargetPressureActorIdentity currentLocal,
        uint resolvedActionId,
        bool actionLocallyReady) =>
        intent.IsValid &&
        currentLocal.IsValid &&
        currentLocal == intent.LocalPlayer &&
        resolvedActionId == intent.ActionId &&
        actionLocallyReady &&
        currentCandidate.EnemySlot == intent.EnemySlot &&
        currentCandidate.Actor == intent.Target &&
        IsEligibleCandidate(currentCandidate, currentLocal);

    private static NinjaGuardShukuchiDecisionReason GetGateFailure(
        NinjaGuardShukuchiObservation observation)
    {
        if (observation.HardReset)
            return NinjaGuardShukuchiDecisionReason.HardReset;
        if (!observation.ConfigurationEnabled)
            return NinjaGuardShukuchiDecisionReason.ConfigurationDisabled;
        if (!observation.IsCrystallineConflict)
            return NinjaGuardShukuchiDecisionReason.OutsideCrystallineConflict;
        if (!observation.LocalPlayer.IsValid)
            return NinjaGuardShukuchiDecisionReason.LocalPlayerIdentityInvalid;
        if (!observation.IsLocalPlayerAliveAndTargetable)
            return NinjaGuardShukuchiDecisionReason.LocalPlayerDeadOrUntargetable;
        if (observation.LocalJobId != NinjaJobId)
            return NinjaGuardShukuchiDecisionReason.LocalJobInvalid;
        if (!observation.MetadataVerified)
            return NinjaGuardShukuchiDecisionReason.MetadataUnverified;
        if (observation.ActionHelpersSuppressedByGuard)
            return NinjaGuardShukuchiDecisionReason.GuardSuppressed;
        if (observation.HigherPriorityClaimed)
            return NinjaGuardShukuchiDecisionReason.HigherPriorityClaimed;
        if (!observation.InputProbeSucceeded)
            return NinjaGuardShukuchiDecisionReason.InputProbeUnavailable;
        if (observation.IsTextInputActive)
            return NinjaGuardShukuchiDecisionReason.TextInputActive;
        if (!observation.HeldGameplayKeyEligible)
            return NinjaGuardShukuchiDecisionReason.NoHeldGameplayKey;
        if (observation.ResolvedActionId != ActionId)
            return NinjaGuardShukuchiDecisionReason.ResolvedActionInvalid;
        if (!observation.ActionLocallyReady)
            return NinjaGuardShukuchiDecisionReason.ActionNotReady;

        return NinjaGuardShukuchiDecisionReason.None;
    }

    private static int Compare(
        NinjaGuardShukuchiCandidate left,
        NinjaGuardShukuchiCandidate right)
    {
        var leftPositivePressure = left.PressureKnown && left.TeamTargetCount > 0;
        var rightPositivePressure = right.PressureKnown && right.TeamTargetCount > 0;
        var pressurePresence = rightPositivePressure.CompareTo(leftPositivePressure);
        if (pressurePresence != 0) return pressurePresence;
        if (leftPositivePressure)
        {
            var pressure = right.TeamTargetCount.CompareTo(left.TeamTargetCount);
            if (pressure != 0) return pressure;
        }

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
