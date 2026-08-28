namespace SeitonSense.Core;

public enum RedMageGuardObservationKind : byte
{
    Ambiguous = 0,
    Absent = 1,
    ExactActive = 2,
}

/// <summary>
/// Per-actor Guard edge memory. An actor first observed with Guard is latched as
/// already active; only a later exact absent-to-present edge can open an
/// automation episode.
/// </summary>
public readonly record struct RedMageGuardEpisodeState(
    bool Initialized,
    bool ObservedAbsent,
    bool GuardActive,
    ulong CurrentEpisodeToken,
    ulong SpentEpisodeToken)
{
    public static RedMageGuardEpisodeState Initial => default;

    public bool HasUnspentEpisode =>
        GuardActive &&
        CurrentEpisodeToken != 0 &&
        CurrentEpisodeToken != SpentEpisodeToken;
}

public readonly record struct RedMageGuardEngageCandidate(
    SupportedPvPContext Context,
    int EnemySlot,
    TargetPressureActorIdentity Actor,
    bool ExactCanonicalIdentity,
    bool Alive,
    bool Targetable,
    uint CurrentHp,
    uint MaximumHp,
    int ExactGuardStatusCount,
    int GuardRemainingMilliseconds,
    ulong GuardEpisodeToken,
    bool GuardEpisodeUnspent,
    bool HasOtherReviewedProtection,
    bool HasValidActionTarget,
    bool HasNativeRangeAndLineOfSight);

public readonly record struct RedMageGuardEngageIntent(
    SupportedPvPContext Context,
    uint ActionId,
    uint ComboCarrierActionId,
    int EnemySlot,
    TargetPressureActorIdentity Target,
    int HeldKeyCode,
    ulong GuardEpisodeToken,
    long ExpiresAtMilliseconds)
{
    public bool IsValid =>
        Context is SupportedPvPContext.CrystallineConflict or SupportedPvPContext.WolvesDen &&
        ActionId == RedMageGuardEngageRules.CorpsACorpsActionId &&
        ComboCarrierActionId == RedMageGuardEngageRules.MeleeComboCarrierActionId &&
        (Context == SupportedPvPContext.WolvesDen || EnemySlotRules.IsValidSlot(EnemySlot)) &&
        Target.IsValid &&
        HeldKeyCode > 0 &&
        GuardEpisodeToken != 0 &&
        ExpiresAtMilliseconds >= 0;
}

public enum RedMageGuardEngageDecisionReason : byte
{
    None = 0,
    ConfigurationDisabled = 1,
    UnsupportedContext = 2,
    LocalPlayerInvalid = 3,
    LocalPlayerDeadOrUntargetable = 4,
    WrongJob = 5,
    MetadataUnverified = 6,
    GuardSuppressed = 7,
    LocalHealthBelowThreshold = 8,
    LocalMpBelowThreshold = 9,
    HigherPriorityClaimed = 10,
    InputUnavailable = 11,
    TextInputActive = 12,
    NoHeldGameplayKey = 13,
    CorpsUnavailable = 14,
    MeleeStarterUnavailable = 15,
    NoExactFreshGuardTarget = 16,
    HardReset = 17,
}

public readonly record struct RedMageGuardEngageObservation(
    bool ConfigurationEnabled,
    SupportedPvPContext Context,
    uint LocalJobId,
    TargetPressureActorIdentity LocalPlayer,
    bool LocalAliveAndTargetable,
    uint LocalCurrentHp,
    uint LocalMaximumHp,
    uint LocalCurrentMp,
    uint LocalMaximumMp,
    int MinimumHpPercent,
    int MinimumMpPercent,
    bool MetadataVerified,
    bool ActionHelpersSuppressedByGuard,
    bool HigherPriorityClaimed,
    bool InputProbeSucceeded,
    bool IsTextInputActive,
    bool HeldGameplayKeyEligible,
    int HeldGameplayKeyCode,
    uint ResolvedActionId,
    bool CorpsReady,
    uint ResolvedComboCarrierActionId,
    bool MeleeStarterReady,
    long NowMilliseconds,
    IReadOnlyList<RedMageGuardEngageCandidate>? Candidates,
    bool HardReset = false);

public readonly record struct RedMageGuardEngageDecision(
    RedMageGuardEngageDecisionReason Reason,
    int SelectedCandidateIndex = -1,
    RedMageGuardEngageIntent? Intent = null)
{
    public bool ShouldDispatch =>
        Reason == RedMageGuardEngageDecisionReason.None &&
        Intent is { IsValid: true };
}

/// <summary>
/// Pure fail-closed policy for the default-off RDM held Guard engage. It opens
/// only on an exact absent-to-present Guard edge and freezes one actor for no
/// longer than the first second of that exact Guard episode.
/// </summary>
public static class RedMageGuardEngageRules
{
    public const uint RedMageJobId = 35;
    public const uint RedMageClassJobCategoryId = 112;
    public const uint CorpsACorpsActionId = 29_699;
    public const uint CorpsACorpsIconId = 9_266;
    public const uint MeleeComboCarrierActionId = 41_488;
    public const uint EnchantedZwerchhauActionId = 41_489;
    public const uint EnchantedRedoublementActionId = 41_490;
    public const uint ScorchActionId = 41_491;
    public const uint MeleeComboCarrierIconId = 9_263;
    public const uint GuardStatusId = 3_054;
    public const uint GuardAlternateStatusId = 3_673;
    public const int DefaultMinimumHpPercent = 80;
    public const int DefaultMinimumMpPercent = 50;
    public const int MinimumConfigurablePercent = 1;
    public const int MaximumConfigurablePercent = 100;
    public const int ExpectedMaximumPvpMp = 10_000;
    public const int GuardFullDurationMilliseconds = 4_000;
    public const int GuardFreshRemainingFloorExclusiveMilliseconds = 3_000;
    public const int GuardTelemetryCeilingMilliseconds = 4_250;
    public const int MaximumGuardAgeMilliseconds = 1_000;
    public const float CorpsACorpsMaximumRangeYalms = 25f;

    public static bool IsExactGuardStatus(uint statusId) =>
        statusId is GuardStatusId or GuardAlternateStatusId;

    public static RedMageGuardEpisodeState ObserveGuardEpisode(
        RedMageGuardEpisodeState state,
        RedMageGuardObservationKind observation)
    {
        if (observation == RedMageGuardObservationKind.Ambiguous)
            return state;

        if (!state.Initialized)
        {
            return observation == RedMageGuardObservationKind.Absent
                ? new RedMageGuardEpisodeState(
                    Initialized: true,
                    ObservedAbsent: true,
                    GuardActive: false,
                    CurrentEpisodeToken: 0,
                    SpentEpisodeToken: 0)
                : new RedMageGuardEpisodeState(
                    Initialized: true,
                    ObservedAbsent: false,
                    GuardActive: true,
                    CurrentEpisodeToken: 0,
                    SpentEpisodeToken: 0);
        }

        if (observation == RedMageGuardObservationKind.Absent)
        {
            return state with
            {
                ObservedAbsent = true,
                GuardActive = false,
            };
        }

        if (state.GuardActive) return state;
        if (!state.ObservedAbsent)
            return state with { GuardActive = true };

        return state with
        {
            GuardActive = true,
            CurrentEpisodeToken = NextToken(state.CurrentEpisodeToken),
        };
    }

    public static bool TrySpendGuardEpisode(
        RedMageGuardEpisodeState state,
        ulong expectedEpisodeToken,
        out RedMageGuardEpisodeState spent)
    {
        spent = state;
        if (!state.HasUnspentEpisode ||
            expectedEpisodeToken == 0 ||
            state.CurrentEpisodeToken != expectedEpisodeToken)
        {
            return false;
        }

        spent = state with { SpentEpisodeToken = expectedEpisodeToken };
        return true;
    }

    public static bool TryComputeGuardDeadline(
        long nowMilliseconds,
        int guardRemainingMilliseconds,
        out long deadlineMilliseconds)
    {
        deadlineMilliseconds = -1;
        if (nowMilliseconds < 0 ||
            guardRemainingMilliseconds <= GuardFreshRemainingFloorExclusiveMilliseconds ||
            guardRemainingMilliseconds > GuardTelemetryCeilingMilliseconds)
        {
            return false;
        }

        var remainingBudget = Math.Min(
            MaximumGuardAgeMilliseconds,
            guardRemainingMilliseconds - GuardFreshRemainingFloorExclusiveMilliseconds);
        if (remainingBudget <= 0) return false;
        deadlineMilliseconds = SaturatingAdd(nowMilliseconds, remainingBudget);
        return true;
    }

    public static bool MeetsInclusivePercent(
        uint current,
        uint maximum,
        int minimumPercent) =>
        current > 0 &&
        maximum > 0 &&
        current <= maximum &&
        minimumPercent is >= MinimumConfigurablePercent and <= MaximumConfigurablePercent &&
        (ulong)current * 100UL >= (ulong)maximum * (uint)minimumPercent;

    public static RedMageGuardEngageDecision Evaluate(
        RedMageGuardEngageObservation observation)
    {
        var failure = GateFailure(observation);
        if (failure != RedMageGuardEngageDecisionReason.None)
            return new RedMageGuardEngageDecision(failure);

        var selected = SelectBestCandidateIndex(
            observation.Candidates,
            observation.NowMilliseconds);
        if (selected < 0)
        {
            return new RedMageGuardEngageDecision(
                RedMageGuardEngageDecisionReason.NoExactFreshGuardTarget);
        }

        var candidate = observation.Candidates![selected];
        if (!TryComputeGuardDeadline(
                observation.NowMilliseconds,
                candidate.GuardRemainingMilliseconds,
                out var deadline))
        {
            return new RedMageGuardEngageDecision(
                RedMageGuardEngageDecisionReason.NoExactFreshGuardTarget);
        }

        return new RedMageGuardEngageDecision(
            RedMageGuardEngageDecisionReason.None,
            selected,
            new RedMageGuardEngageIntent(
                observation.Context,
                CorpsACorpsActionId,
                MeleeComboCarrierActionId,
                candidate.EnemySlot,
                candidate.Actor,
                observation.HeldGameplayKeyCode,
                candidate.GuardEpisodeToken,
                deadline));
    }

    public static bool IsEligibleCandidate(
        RedMageGuardEngageCandidate candidate,
        long nowMilliseconds) =>
        candidate.Context is SupportedPvPContext.CrystallineConflict or SupportedPvPContext.WolvesDen &&
        (candidate.Context == SupportedPvPContext.WolvesDen ||
         EnemySlotRules.IsValidSlot(candidate.EnemySlot)) &&
        candidate.Actor.IsValid &&
        candidate.ExactCanonicalIdentity &&
        candidate.Alive &&
        candidate.Targetable &&
        candidate.CurrentHp > 0 &&
        candidate.MaximumHp >= candidate.CurrentHp &&
        candidate.ExactGuardStatusCount == 1 &&
        candidate.GuardEpisodeToken != 0 &&
        candidate.GuardEpisodeUnspent &&
        !candidate.HasOtherReviewedProtection &&
        candidate.HasValidActionTarget &&
        candidate.HasNativeRangeAndLineOfSight &&
        TryComputeGuardDeadline(
            nowMilliseconds,
            candidate.GuardRemainingMilliseconds,
            out _);

    public static bool CanUseFrozenIntent(
        RedMageGuardEngageIntent intent,
        RedMageGuardEngageCandidate candidate,
        long nowMilliseconds,
        bool exactHeldKeyStillDown,
        uint resolvedActionId,
        bool corpsReady,
        uint resolvedComboCarrierActionId,
        bool meleeStarterReady) =>
        intent.IsValid &&
        nowMilliseconds >= 0 &&
        nowMilliseconds <= intent.ExpiresAtMilliseconds &&
        exactHeldKeyStillDown &&
        candidate.Context == intent.Context &&
        candidate.EnemySlot == intent.EnemySlot &&
        candidate.Actor == intent.Target &&
        candidate.GuardEpisodeToken == intent.GuardEpisodeToken &&
        resolvedActionId == CorpsACorpsActionId &&
        corpsReady &&
        resolvedComboCarrierActionId == MeleeComboCarrierActionId &&
        meleeStarterReady &&
        IsEligibleCandidate(
            candidate with { GuardEpisodeUnspent = true },
            nowMilliseconds);

    private static RedMageGuardEngageDecisionReason GateFailure(
        RedMageGuardEngageObservation observation)
    {
        if (observation.HardReset)
            return RedMageGuardEngageDecisionReason.HardReset;
        if (!observation.ConfigurationEnabled)
            return RedMageGuardEngageDecisionReason.ConfigurationDisabled;
        if (observation.Context is not
            (SupportedPvPContext.CrystallineConflict or SupportedPvPContext.WolvesDen))
        {
            return RedMageGuardEngageDecisionReason.UnsupportedContext;
        }
        if (!observation.LocalPlayer.IsValid)
            return RedMageGuardEngageDecisionReason.LocalPlayerInvalid;
        if (!observation.LocalAliveAndTargetable)
            return RedMageGuardEngageDecisionReason.LocalPlayerDeadOrUntargetable;
        if (observation.LocalJobId != RedMageJobId)
            return RedMageGuardEngageDecisionReason.WrongJob;
        if (!observation.MetadataVerified)
            return RedMageGuardEngageDecisionReason.MetadataUnverified;
        if (observation.ActionHelpersSuppressedByGuard)
            return RedMageGuardEngageDecisionReason.GuardSuppressed;
        if (!MeetsInclusivePercent(
                observation.LocalCurrentHp,
                observation.LocalMaximumHp,
                observation.MinimumHpPercent))
        {
            return RedMageGuardEngageDecisionReason.LocalHealthBelowThreshold;
        }
        if (observation.LocalMaximumMp != ExpectedMaximumPvpMp ||
            !MeetsInclusivePercent(
                observation.LocalCurrentMp,
                observation.LocalMaximumMp,
                observation.MinimumMpPercent))
        {
            return RedMageGuardEngageDecisionReason.LocalMpBelowThreshold;
        }
        if (observation.HigherPriorityClaimed)
            return RedMageGuardEngageDecisionReason.HigherPriorityClaimed;
        if (!observation.InputProbeSucceeded)
            return RedMageGuardEngageDecisionReason.InputUnavailable;
        if (observation.IsTextInputActive)
            return RedMageGuardEngageDecisionReason.TextInputActive;
        if (!observation.HeldGameplayKeyEligible || observation.HeldGameplayKeyCode <= 0)
            return RedMageGuardEngageDecisionReason.NoHeldGameplayKey;
        if (observation.ResolvedActionId != CorpsACorpsActionId || !observation.CorpsReady)
            return RedMageGuardEngageDecisionReason.CorpsUnavailable;
        if (observation.ResolvedComboCarrierActionId != MeleeComboCarrierActionId ||
            !observation.MeleeStarterReady)
        {
            return RedMageGuardEngageDecisionReason.MeleeStarterUnavailable;
        }

        return RedMageGuardEngageDecisionReason.None;
    }

    private static int SelectBestCandidateIndex(
        IReadOnlyList<RedMageGuardEngageCandidate>? candidates,
        long nowMilliseconds)
    {
        if (candidates is null || candidates.Count == 0) return -1;
        var selected = -1;
        for (var i = 0; i < candidates.Count; i++)
        {
            if (!IsEligibleCandidate(candidates[i], nowMilliseconds)) continue;
            if (selected < 0 || Compare(candidates[i], candidates[selected]) < 0)
                selected = i;
        }

        return selected;
    }

    private static int Compare(
        RedMageGuardEngageCandidate left,
        RedMageGuardEngageCandidate right)
    {
        var health = ((ulong)left.CurrentHp * right.MaximumHp).CompareTo(
            (ulong)right.CurrentHp * left.MaximumHp);
        if (health != 0) return health;

        var youth = right.GuardRemainingMilliseconds.CompareTo(
            left.GuardRemainingMilliseconds);
        if (youth != 0) return youth;

        var slot = left.EnemySlot.CompareTo(right.EnemySlot);
        if (slot != 0) return slot;

        var entity = left.Actor.EntityId.CompareTo(right.Actor.EntityId);
        return entity != 0
            ? entity
            : left.Actor.GameObjectId.CompareTo(right.Actor.GameObjectId);
    }

    private static ulong NextToken(ulong token) =>
        token == ulong.MaxValue ? 1UL : token + 1UL;

    private static long SaturatingAdd(long left, long right) =>
        left > long.MaxValue - right ? long.MaxValue : left + right;
}
