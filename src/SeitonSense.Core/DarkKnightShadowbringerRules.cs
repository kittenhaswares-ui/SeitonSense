namespace SeitonSense.Core;

public enum DarkKnightShadowbringerOpportunityKind : byte
{
    None = 0,
    DarkArts = 1,
    SafeHpCost = 2,
}

public enum DarkKnightShadowbringerDispatchPolicy : byte
{
    Any = 0,
    DarkArtsOnly = 1,
    SafeHpCostOnly = 2,
}

/// <summary>
/// One exact Dark Arts exposure. A single missing sample is treated as native
/// status-list flicker; two consecutive missing samples close the generation.
/// </summary>
public readonly record struct DarkKnightShadowbringerDarkArtsState(
    long Generation,
    bool IsCurrentlyExposed,
    bool IsSpent,
    int ConsecutiveAbsentObservations)
{
    public static DarkKnightShadowbringerDarkArtsState Initial => default;

    public bool HasTrackedEpisode =>
        Generation > 0 && ConsecutiveAbsentObservations < 2;

    public bool IsValid =>
        Generation >= 0 &&
        ConsecutiveAbsentObservations is >= 0 and <= 2 &&
        (Generation != 0 || (!IsCurrentlyExposed && !IsSpent)) &&
        (IsCurrentlyExposed
            ? ConsecutiveAbsentObservations == 0
            : true) &&
        (ConsecutiveAbsentObservations == 2
            ? !IsCurrentlyExposed && !IsSpent
            : true);
}

/// <summary>
/// One safe HP-cost eligibility episode. Cooldown changes deliberately do not
/// open a new episode. Two consecutive ineligible samples are required before
/// the same continuously held key can receive a later episode.
/// </summary>
public readonly record struct DarkKnightShadowbringerFallbackState(
    long Generation,
    bool IsCurrentlyEligible,
    bool IsSpent,
    int ConsecutiveIneligibleObservations)
{
    public static DarkKnightShadowbringerFallbackState Initial => default;

    public bool HasTrackedEpisode =>
        Generation > 0 && ConsecutiveIneligibleObservations < 2;

    public bool IsValid =>
        Generation >= 0 &&
        ConsecutiveIneligibleObservations is >= 0 and <= 2 &&
        (Generation != 0 || (!IsCurrentlyEligible && !IsSpent)) &&
        (IsCurrentlyEligible
            ? ConsecutiveIneligibleObservations == 0
            : true) &&
        (ConsecutiveIneligibleObservations == 2
            ? !IsCurrentlyEligible && !IsSpent
            : true);
}

public readonly record struct DarkKnightShadowbringerCandidate(
    SupportedPvPContext Context,
    int EnemySlot,
    TargetPressureActorIdentity Actor,
    bool ExactCanonicalIdentity,
    bool Alive,
    bool Targetable,
    uint CurrentHp,
    uint MaximumHp,
    bool TargetGuardActive,
    bool HasValidActionTarget,
    bool HasNativeRangeAndLineOfSight);

public readonly record struct DarkKnightShadowbringerIntent(
    DarkKnightShadowbringerOpportunityKind Opportunity,
    long OpportunityGeneration,
    SupportedPvPContext Context,
    int EnemySlot,
    TargetPressureActorIdentity LocalPlayer,
    TargetPressureActorIdentity Target,
    uint ExpectedAdjustedActionId,
    int FrozenKeyCode)
{
    public bool IsValid =>
        Opportunity is DarkKnightShadowbringerOpportunityKind.DarkArts or
            DarkKnightShadowbringerOpportunityKind.SafeHpCost &&
        OpportunityGeneration > 0 &&
        DarkKnightShadowbringerRules.IsContextSlotValid(Context, EnemySlot) &&
        LocalPlayer.IsValid &&
        Target.IsValid &&
        Target != LocalPlayer &&
        ExpectedAdjustedActionId == DarkKnightShadowbringerRules
            .GetExpectedAdjustedActionId(Opportunity) &&
        FrozenKeyCode > 0;
}

public enum DarkKnightShadowbringerPhase : byte
{
    Waiting = 0,
    Buffered = 1,
}

public readonly record struct DarkKnightShadowbringerState(
    DarkKnightShadowbringerPhase Phase,
    DarkKnightShadowbringerIntent? Intent,
    HeldActionRetryState Retry,
    long LastObservedAtMilliseconds,
    ClientActionAttemptOutcome LastNativeOutcome)
{
    public static DarkKnightShadowbringerState Initial => new(
        DarkKnightShadowbringerPhase.Waiting,
        null,
        HeldActionRetryState.Initial,
        -1,
        ClientActionAttemptOutcome.None);
}

public readonly record struct DarkKnightShadowbringerObservation(
    bool ConfigurationEnabled,
    SupportedPvPContext Context,
    TargetPressureActorIdentity LocalPlayer,
    bool IsLocalPlayerAlive,
    bool IsLocalPlayerTargetable,
    uint LocalJobId,
    bool MetadataVerified,
    bool ActionHelpersSuppressedByGuard,
    bool HigherPriorityClaimed,
    bool InputProbeSucceeded,
    bool IsTextInputActive,
    bool HeldGameplayKeyEligible,
    int HeldGameplayKeyCode,
    bool FrozenKeyStillDown,
    DarkKnightShadowbringerDarkArtsState DarkArts,
    DarkKnightShadowbringerFallbackState Fallback,
    DarkKnightShadowbringerDispatchPolicy DispatchPolicy,
    uint ResolvedAdjustedActionId,
    bool ActionLocallyReady,
    bool NativeBoundaryReady,
    DarkKnightShadowbringerCandidate? Candidate,
    bool HardReset,
    long NowMilliseconds);

public enum DarkKnightShadowbringerDecisionKind : byte
{
    None = 0,
    Armed = 1,
    Dispatch = 2,
    Cancelled = 3,
}

public enum DarkKnightShadowbringerDecisionReason : byte
{
    None = 0,
    HardReset,
    ClockMovedBackwards,
    ConfigurationDisabled,
    OutsideSupportedPvPContext,
    LocalPlayerIdentityInvalid,
    LocalPlayerDead,
    LocalPlayerUntargetable,
    LocalJobInvalid,
    MetadataUnverified,
    InputProbeUnavailable,
    TextInputActive,
    GuardSuppressed,
    HigherPriorityClaimed,
    DeferredBySchedulerPolicy,
    DeferredFrameInvalid,
    OpportunityUnavailable,
    OpportunitySpent,
    OpportunitySuperseded,
    NoHeldGameplayKey,
    ExactKeyReleased,
    ActionIdentityChanged,
    ActionNotReady,
    CandidateUnavailable,
    CandidateInvalid,
    TargetNotReady,
    NativeBoundaryUnavailable,
    NativeRetryThrottle,
    NativeRetryLimitReached,
    NativeAcceptanceUnknown,
}

public readonly record struct DarkKnightShadowbringerDecision(
    DarkKnightShadowbringerState NextState,
    DarkKnightShadowbringerDecisionKind Kind,
    DarkKnightShadowbringerDecisionReason Reason,
    DarkKnightShadowbringerIntent? Intent = null,
    bool InputClaimed = false,
    bool SpendOpportunity = false)
{
    public bool ShouldDispatch =>
        Kind == DarkKnightShadowbringerDecisionKind.Dispatch &&
        Intent is { IsValid: true };
}

public readonly record struct DarkKnightShadowbringerNativeAttemptDecision(
    DarkKnightShadowbringerState NextState,
    DarkKnightShadowbringerDecisionReason Reason,
    HeldActionRetryDisposition Disposition,
    bool RetryScheduled,
    bool ClientAccepted,
    bool Terminal,
    bool SpendOpportunity,
    bool SoftWait = false);

/// <summary>
/// Pure policy for the default-off held Shadowbringer helper. Dark Arts is
/// always preferred over the configured high-HP/low-pressure fallback. Every
/// native attempt remains bound to one exact key, opportunity and actor.
/// </summary>
public static class DarkKnightShadowbringerRules
{
    public const uint DarkKnightJobId = 32;
    public const uint DarkKnightClassJobCategoryId = 98;
    public const uint ShadowbringerActionId = 29_091;
    public const uint DarkArtsShadowbringerActionId = 29_738;
    public const uint TheBlackestNightActionId = 29_093;
    public const uint DarkArtsStatusId = 3_034;
    public const uint ShadowbringerIconId = 9_594;
    public const uint TheBlackestNightIconId = 9_152;
    public const uint DarkArtsStatusIconId = 213_107;
    public const uint ShadowbringerHpCost = 12_000;
    public const uint WolvesDenStrikingDummyNameId = 541;
    public const int MaximumRangeYalms = 10;
    public const int ExpectedRuntimeRecastGroupIndex = 0;
    public const int ExpectedAdjustedRecastMilliseconds = 1_000;
    public const long MaximumPressureAgeMilliseconds = 250;
    public const int DefaultMinimumHpPercent = 85;
    public const int MinimumConfigurableHpPercent = 1;
    public const int MaximumConfigurableHpPercent = 100;
    public const int DefaultPressureLimitExclusive = 2;
    public const int MinimumPressureLimitExclusive = 1;
    public const int MaximumPressureLimitExclusive = 6;

    public static DarkKnightShadowbringerDarkArtsState ObserveDarkArts(
        DarkKnightShadowbringerDarkArtsState previous,
        bool exactDarkArtsExposure,
        bool hardReset = false)
    {
        if (hardReset) return DarkKnightShadowbringerDarkArtsState.Initial;
        if (!previous.IsValid)
            previous = DarkKnightShadowbringerDarkArtsState.Initial;

        if (exactDarkArtsExposure)
        {
            if (previous.HasTrackedEpisode)
            {
                return previous with
                {
                    IsCurrentlyExposed = true,
                    ConsecutiveAbsentObservations = 0,
                };
            }

            var generation = NextGeneration(previous.Generation);
            return generation == 0
                ? DarkKnightShadowbringerDarkArtsState.Initial
                : new DarkKnightShadowbringerDarkArtsState(
                    generation,
                    IsCurrentlyExposed: true,
                    IsSpent: false,
                    ConsecutiveAbsentObservations: 0);
        }

        var misses = Math.Min(
            2,
            previous.ConsecutiveAbsentObservations + 1);
        if (previous.HasTrackedEpisode && misses == 1)
        {
            return previous with
            {
                IsCurrentlyExposed = false,
                ConsecutiveAbsentObservations = 1,
            };
        }

        return new DarkKnightShadowbringerDarkArtsState(
            previous.Generation,
            IsCurrentlyExposed: false,
            IsSpent: false,
            ConsecutiveAbsentObservations: misses);
    }

    public static DarkKnightShadowbringerFallbackState ObserveFallback(
        DarkKnightShadowbringerFallbackState previous,
        bool exactFallbackEligibility,
        bool hardReset = false)
    {
        if (hardReset) return DarkKnightShadowbringerFallbackState.Initial;
        if (!previous.IsValid)
            previous = DarkKnightShadowbringerFallbackState.Initial;

        if (exactFallbackEligibility)
        {
            if (previous.HasTrackedEpisode)
            {
                return previous with
                {
                    IsCurrentlyEligible = true,
                    ConsecutiveIneligibleObservations = 0,
                };
            }

            var generation = NextGeneration(previous.Generation);
            return generation == 0
                ? DarkKnightShadowbringerFallbackState.Initial
                : new DarkKnightShadowbringerFallbackState(
                    generation,
                    IsCurrentlyEligible: true,
                    IsSpent: false,
                    ConsecutiveIneligibleObservations: 0);
        }

        var misses = Math.Min(
            2,
            previous.ConsecutiveIneligibleObservations + 1);
        if (previous.HasTrackedEpisode && misses == 1)
        {
            return previous with
            {
                IsCurrentlyEligible = false,
                ConsecutiveIneligibleObservations = 1,
            };
        }

        return new DarkKnightShadowbringerFallbackState(
            previous.Generation,
            IsCurrentlyEligible: false,
            IsSpent: false,
            ConsecutiveIneligibleObservations: misses);
    }

    public static DarkKnightShadowbringerDarkArtsState MarkDarkArtsSpent(
        DarkKnightShadowbringerDarkArtsState state,
        long generation) =>
        state.IsValid && state.HasTrackedEpisode &&
        generation > 0 && state.Generation == generation
            ? state with { IsSpent = true }
            : state;

    public static DarkKnightShadowbringerFallbackState MarkFallbackSpent(
        DarkKnightShadowbringerFallbackState state,
        long generation) =>
        state.IsValid && state.HasTrackedEpisode &&
        generation > 0 && state.Generation == generation
            ? state with { IsSpent = true }
            : state;

    public static bool IsSafeFallbackEligible(
        uint currentHp,
        uint maximumHp,
        bool pressureKnown,
        int incomingPressure,
        int minimumHpPercent,
        int pressureLimitExclusive) =>
        currentHp > ShadowbringerHpCost &&
        maximumHp > 0 &&
        currentHp <= maximumHp &&
        minimumHpPercent is >= MinimumConfigurableHpPercent and
            <= MaximumConfigurableHpPercent &&
        (ulong)currentHp * 100UL >
            (ulong)maximumHp * (uint)minimumHpPercent &&
        pressureKnown &&
        pressureLimitExclusive is >= MinimumPressureLimitExclusive and
            <= MaximumPressureLimitExclusive &&
        incomingPressure >= 0 &&
        incomingPressure < pressureLimitExclusive;

    public static bool IsContextSlotValid(
        SupportedPvPContext context,
        int enemySlot) => context switch
        {
            SupportedPvPContext.CrystallineConflict =>
                EnemySlotRules.IsValidSlot(enemySlot),
            SupportedPvPContext.WolvesDen => enemySlot == 0,
            _ => false,
        };

    public static uint GetExpectedAdjustedActionId(
        DarkKnightShadowbringerOpportunityKind opportunity) => opportunity switch
        {
            DarkKnightShadowbringerOpportunityKind.DarkArts =>
                DarkArtsShadowbringerActionId,
            DarkKnightShadowbringerOpportunityKind.SafeHpCost =>
                ShadowbringerActionId,
            _ => 0,
        };

    public static bool TrySelectOpportunity(
        DarkKnightShadowbringerDarkArtsState darkArts,
        DarkKnightShadowbringerFallbackState fallback,
        out DarkKnightShadowbringerOpportunityKind opportunity,
        out long generation,
        out uint expectedAdjustedActionId,
        DarkKnightShadowbringerDispatchPolicy dispatchPolicy =
            DarkKnightShadowbringerDispatchPolicy.Any)
    {
        opportunity = DarkKnightShadowbringerOpportunityKind.None;
        generation = 0;
        expectedAdjustedActionId = 0;
        if (Allows(
                dispatchPolicy,
                DarkKnightShadowbringerOpportunityKind.DarkArts) &&
            darkArts.IsValid && darkArts.HasTrackedEpisode &&
            darkArts.IsCurrentlyExposed && !darkArts.IsSpent)
        {
            opportunity = DarkKnightShadowbringerOpportunityKind.DarkArts;
            generation = darkArts.Generation;
            expectedAdjustedActionId = DarkArtsShadowbringerActionId;
            return true;
        }

        if (Allows(
                dispatchPolicy,
                DarkKnightShadowbringerOpportunityKind.SafeHpCost) &&
            fallback.IsValid && fallback.HasTrackedEpisode &&
            fallback.IsCurrentlyEligible && !fallback.IsSpent)
        {
            opportunity = DarkKnightShadowbringerOpportunityKind.SafeHpCost;
            generation = fallback.Generation;
            expectedAdjustedActionId = ShadowbringerActionId;
            return true;
        }

        return false;
    }

    public static bool IsTrackedOpportunity(
        DarkKnightShadowbringerIntent intent,
        DarkKnightShadowbringerDarkArtsState darkArts,
        DarkKnightShadowbringerFallbackState fallback,
        bool requireCurrent) => intent.Opportunity switch
        {
            DarkKnightShadowbringerOpportunityKind.DarkArts =>
                darkArts.IsValid && darkArts.HasTrackedEpisode &&
                darkArts.Generation == intent.OpportunityGeneration &&
                (!requireCurrent || darkArts.IsCurrentlyExposed),
            DarkKnightShadowbringerOpportunityKind.SafeHpCost =>
                fallback.IsValid && fallback.HasTrackedEpisode &&
                fallback.Generation == intent.OpportunityGeneration &&
                (!requireCurrent || fallback.IsCurrentlyEligible),
            _ => false,
        };

    public static int SelectBestCandidateIndex(
        IReadOnlyList<DarkKnightShadowbringerCandidate>? candidates,
        SupportedPvPContext context,
        TargetPressureActorIdentity localPlayer)
    {
        if (candidates is null || candidates.Count == 0 || !localPlayer.IsValid)
            return -1;

        var slots = new HashSet<int>();
        var actors = new HashSet<TargetPressureActorIdentity>();
        var best = -1;
        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            if (candidate.Context != context ||
                !IsContextSlotValid(candidate.Context, candidate.EnemySlot) ||
                !candidate.Actor.IsValid ||
                !slots.Add(candidate.EnemySlot) ||
                !actors.Add(candidate.Actor))
            {
                return -1;
            }

            if (!IsEligibleCandidate(candidate, localPlayer)) continue;
            if (best < 0 || Compare(candidate, candidates[best]) < 0)
                best = index;
        }

        return best;
    }

    public static bool IsEligibleCandidate(
        DarkKnightShadowbringerCandidate candidate,
        TargetPressureActorIdentity localPlayer) =>
        localPlayer.IsValid &&
        candidate.Actor.IsValid &&
        candidate.Actor != localPlayer &&
        IsContextSlotValid(candidate.Context, candidate.EnemySlot) &&
        candidate.ExactCanonicalIdentity &&
        candidate.Alive &&
        candidate.Targetable &&
        HasValidHp(candidate.CurrentHp, candidate.MaximumHp) &&
        !candidate.TargetGuardActive &&
        candidate.HasValidActionTarget &&
        candidate.HasNativeRangeAndLineOfSight;

    public static DarkKnightShadowbringerDecision Observe(
        DarkKnightShadowbringerState previous,
        DarkKnightShadowbringerObservation observation)
    {
        if (observation.HardReset)
        {
            return Cancelled(
                DarkKnightShadowbringerState.Initial,
                DarkKnightShadowbringerDecisionReason.HardReset);
        }

        if (observation.NowMilliseconds < 0 ||
            (previous.LastObservedAtMilliseconds >= 0 &&
             observation.NowMilliseconds < previous.LastObservedAtMilliseconds))
        {
            return Cancelled(
                DarkKnightShadowbringerState.Initial,
                DarkKnightShadowbringerDecisionReason.ClockMovedBackwards);
        }

        var permanentFailure = GetPermanentGateFailure(observation);
        if (permanentFailure != DarkKnightShadowbringerDecisionReason.None)
            return None(DarkKnightShadowbringerState.Initial, permanentFailure);

        if (previous.Phase != DarkKnightShadowbringerPhase.Buffered)
            return TryCreateIntent(observation);

        if (previous.Intent is not { IsValid: true } intent)
        {
            return Cancelled(
                DarkKnightShadowbringerState.Initial,
                DarkKnightShadowbringerDecisionReason.NativeAcceptanceUnknown,
                spendOpportunity: true);
        }

        // A newly exposed free proc supersedes an older buffered HP-cost intent
        // before the scheduler-policy gate. This preserves the audited local
        // order even when the fallback was waiting on a cast last frame.
        if (intent.Opportunity ==
                DarkKnightShadowbringerOpportunityKind.SafeHpCost &&
            Allows(
                observation.DispatchPolicy,
                DarkKnightShadowbringerOpportunityKind.DarkArts) &&
            observation.DarkArts.IsValid &&
            observation.DarkArts.HasTrackedEpisode &&
            observation.DarkArts.IsCurrentlyExposed &&
            !observation.DarkArts.IsSpent)
        {
            return TryCreateIntent(observation);
        }

        if (!Allows(observation.DispatchPolicy, intent.Opportunity))
        {
            return Armed(
                Stamp(previous, observation.NowMilliseconds),
                DarkKnightShadowbringerDecisionReason
                    .DeferredBySchedulerPolicy,
                inputClaimed: false);
        }

        if (!IsTrackedOpportunity(
                intent,
                observation.DarkArts,
                observation.Fallback,
                requireCurrent: false))
        {
            if (TrySelectOpportunity(
                    observation.DarkArts,
                    observation.Fallback,
                    out _,
                    out _,
                    out _,
                    observation.DispatchPolicy))
            {
                return TryCreateIntent(observation);
            }

            return Cancelled(
                DarkKnightShadowbringerState.Initial,
                DarkKnightShadowbringerDecisionReason.OpportunitySuperseded,
                spendOpportunity: true);
        }

        return ObserveBuffered(previous, intent, observation);
    }

    public static DarkKnightShadowbringerNativeAttemptDecision
        ApplyNativeAttemptOutcome(
            DarkKnightShadowbringerState current,
            ClientActionAttemptOutcome outcome,
            long nowMilliseconds)
    {
        if (current.Phase != DarkKnightShadowbringerPhase.Buffered ||
            current.Intent is not { IsValid: true } || nowMilliseconds < 0)
        {
            return TerminalUnknown();
        }

        var shared = HeldActionRetryRules.Complete(
            current.Retry,
            nowMilliseconds,
            outcome);
        return shared.Disposition switch
        {
            HeldActionRetryDisposition.SoftWait => new(
                Stamp(current with { LastNativeOutcome = outcome }, nowMilliseconds),
                DarkKnightShadowbringerDecisionReason.NativeBoundaryUnavailable,
                shared.Disposition,
                false, false, false, false, true),
            HeldActionRetryDisposition.AcceptedTerminal => new(
                DarkKnightShadowbringerState.Initial,
                DarkKnightShadowbringerDecisionReason.None,
                shared.Disposition,
                false, true, true, true),
            HeldActionRetryDisposition.RetryScheduled => new(
                Stamp(current with
                {
                    Retry = shared.NextState,
                    LastNativeOutcome = outcome,
                }, nowMilliseconds),
                DarkKnightShadowbringerDecisionReason.NativeRetryThrottle,
                shared.Disposition,
                true, false, false, false),
            HeldActionRetryDisposition.RejectedTerminal => new(
                DarkKnightShadowbringerState.Initial,
                DarkKnightShadowbringerDecisionReason.NativeRetryLimitReached,
                shared.Disposition,
                false, false, true, true),
            _ => TerminalUnknown(),
        };
    }

    public static bool CanUseFrozenIntent(
        DarkKnightShadowbringerIntent intent,
        bool configurationEnabled,
        SupportedPvPContext context,
        TargetPressureActorIdentity localPlayer,
        bool localAliveAndTargetable,
        uint localJobId,
        bool metadataVerified,
        bool guardSuppressed,
        bool higherPriorityClaimed,
        DarkKnightShadowbringerDarkArtsState darkArts,
        DarkKnightShadowbringerFallbackState fallback,
        uint resolvedAdjustedActionId,
        bool actionLocallyReady,
        int currentHeldKeyCode,
        bool frozenKeyStillDown,
        DarkKnightShadowbringerCandidate candidate) =>
        intent.IsValid &&
        configurationEnabled &&
        context == intent.Context &&
        localPlayer == intent.LocalPlayer &&
        localAliveAndTargetable &&
        localJobId == DarkKnightJobId &&
        metadataVerified &&
        !guardSuppressed &&
        !higherPriorityClaimed &&
        IsTrackedOpportunity(intent, darkArts, fallback, requireCurrent: true) &&
        resolvedAdjustedActionId == intent.ExpectedAdjustedActionId &&
        actionLocallyReady &&
        currentHeldKeyCode == intent.FrozenKeyCode &&
        frozenKeyStillDown &&
        IsExactCandidate(intent, candidate) &&
        candidate.HasValidActionTarget &&
        candidate.HasNativeRangeAndLineOfSight;

    /// <summary>
    /// False is retryable only when target readiness, the base carrier's
    /// adjusted identity and the complete native fingerprint remain exact.
    /// </summary>
    public static ClientActionAttemptOutcome ClassifyBoundary(
        bool clientReturnedAccepted,
        uint expectedAdjustedActionId,
        uint targetStatusBefore,
        uint targetStatusAfter,
        uint adjustedActionBefore,
        uint adjustedActionAfter,
        ClientActionAttemptFingerprint before,
        ClientActionAttemptFingerprint after)
    {
        if (clientReturnedAccepted)
            return ClientActionAttemptOutcome.ClientAccepted;
        if (expectedAdjustedActionId is not
                (ShadowbringerActionId or DarkArtsShadowbringerActionId) ||
            targetStatusBefore != 0 ||
            targetStatusAfter != 0 ||
            adjustedActionBefore != expectedAdjustedActionId ||
            adjustedActionAfter != expectedAdjustedActionId)
        {
            return ClientActionAttemptOutcome.AcceptanceUnknown;
        }

        return ClientActionAttemptBoundaryRules.Classify(
            false,
            expectedAdjustedActionId,
            before,
            after);
    }

    private static DarkKnightShadowbringerDecision TryCreateIntent(
        DarkKnightShadowbringerObservation observation)
    {
        if (observation.HigherPriorityClaimed)
            return None(DarkKnightShadowbringerState.Initial,
                DarkKnightShadowbringerDecisionReason.HigherPriorityClaimed);
        if (observation.ActionHelpersSuppressedByGuard)
            return None(DarkKnightShadowbringerState.Initial,
                DarkKnightShadowbringerDecisionReason.GuardSuppressed);
        if (!TrySelectOpportunity(
                observation.DarkArts,
                observation.Fallback,
                out var opportunity,
                out var generation,
                out var expectedAdjustedActionId,
                observation.DispatchPolicy))
        {
            return None(DarkKnightShadowbringerState.Initial,
                DarkKnightShadowbringerDecisionReason.OpportunityUnavailable);
        }

        if (observation.ResolvedAdjustedActionId != expectedAdjustedActionId)
            return None(DarkKnightShadowbringerState.Initial,
                DarkKnightShadowbringerDecisionReason.ActionIdentityChanged);
        if (!observation.ActionLocallyReady)
            return None(DarkKnightShadowbringerState.Initial,
                DarkKnightShadowbringerDecisionReason.ActionNotReady);
        if (!observation.HeldGameplayKeyEligible ||
            observation.HeldGameplayKeyCode <= 0)
        {
            return None(DarkKnightShadowbringerState.Initial,
                DarkKnightShadowbringerDecisionReason.NoHeldGameplayKey);
        }

        if (observation.Candidate is not { } candidate)
            return None(DarkKnightShadowbringerState.Initial,
                DarkKnightShadowbringerDecisionReason.CandidateUnavailable);
        if (!IsEligibleCandidate(candidate, observation.LocalPlayer) ||
            candidate.Context != observation.Context)
        {
            return None(DarkKnightShadowbringerState.Initial,
                DarkKnightShadowbringerDecisionReason.CandidateInvalid);
        }

        var intent = new DarkKnightShadowbringerIntent(
            opportunity,
            generation,
            observation.Context,
            candidate.EnemySlot,
            observation.LocalPlayer,
            candidate.Actor,
            expectedAdjustedActionId,
            observation.HeldGameplayKeyCode);
        if (!intent.IsValid)
        {
            return Cancelled(
                DarkKnightShadowbringerState.Initial,
                DarkKnightShadowbringerDecisionReason.OpportunitySuperseded);
        }

        var buffered = new DarkKnightShadowbringerState(
            DarkKnightShadowbringerPhase.Buffered,
            intent,
            HeldActionRetryState.Initial,
            observation.NowMilliseconds,
            ClientActionAttemptOutcome.None);
        return EvaluateReadyIntent(buffered, intent, observation);
    }

    private static DarkKnightShadowbringerDecision ObserveBuffered(
        DarkKnightShadowbringerState previous,
        DarkKnightShadowbringerIntent intent,
        DarkKnightShadowbringerObservation observation)
    {
        if (!observation.FrozenKeyStillDown)
        {
            return None(
                DarkKnightShadowbringerState.Initial,
                DarkKnightShadowbringerDecisionReason.ExactKeyReleased);
        }

        if (observation.HigherPriorityClaimed)
        {
            return None(
                Stamp(previous, observation.NowMilliseconds),
                DarkKnightShadowbringerDecisionReason.HigherPriorityClaimed);
        }

        if (observation.ActionHelpersSuppressedByGuard)
        {
            return None(
                Stamp(previous, observation.NowMilliseconds),
                DarkKnightShadowbringerDecisionReason.GuardSuppressed);
        }

        if (!IsTrackedOpportunity(
                intent,
                observation.DarkArts,
                observation.Fallback,
                requireCurrent: true))
        {
            return Armed(
                Stamp(previous, observation.NowMilliseconds),
                DarkKnightShadowbringerDecisionReason.OpportunityUnavailable,
                inputClaimed: false);
        }

        if (observation.ResolvedAdjustedActionId !=
            intent.ExpectedAdjustedActionId)
        {
            return Cancelled(
                DarkKnightShadowbringerState.Initial,
                DarkKnightShadowbringerDecisionReason.ActionIdentityChanged,
                spendOpportunity: true);
        }

        if (observation.Candidate is not { } candidate ||
            !IsExactCandidate(intent, candidate))
        {
            return Cancelled(
                DarkKnightShadowbringerState.Initial,
                DarkKnightShadowbringerDecisionReason.CandidateUnavailable,
                spendOpportunity: true);
        }

        return EvaluateReadyIntent(
            Stamp(previous, observation.NowMilliseconds),
            intent,
            observation);
    }

    private static DarkKnightShadowbringerDecision EvaluateReadyIntent(
        DarkKnightShadowbringerState buffered,
        DarkKnightShadowbringerIntent intent,
        DarkKnightShadowbringerObservation observation)
    {
        if (!observation.ActionLocallyReady)
        {
            return Armed(
                buffered,
                DarkKnightShadowbringerDecisionReason.ActionNotReady,
                inputClaimed: false);
        }

        if (observation.Candidate is not { } candidate ||
            !IsExactCandidate(intent, candidate) ||
            !candidate.HasValidActionTarget ||
            !candidate.HasNativeRangeAndLineOfSight)
        {
            return Cancelled(
                DarkKnightShadowbringerState.Initial,
                DarkKnightShadowbringerDecisionReason.TargetNotReady,
                spendOpportunity: true);
        }

        if (!observation.NativeBoundaryReady)
        {
            return Armed(
                buffered,
                DarkKnightShadowbringerDecisionReason.NativeBoundaryUnavailable,
                HeldActionRetryRules.RetainsSchedulerFrame(
                    buffered.Retry,
                    observation.NowMilliseconds,
                    exactIntentValid: true,
                    actionSpecificReady: true,
                    targetSpecificReady: true));
        }

        if (!HeldActionRetryRules.CanAttemptFrozenIntent(
                buffered.Retry,
                observation.NowMilliseconds))
        {
            return Armed(
                buffered,
                DarkKnightShadowbringerDecisionReason.NativeRetryThrottle,
                HeldActionRetryRules.RetainsSchedulerFrame(
                    buffered.Retry,
                    observation.NowMilliseconds,
                    exactIntentValid: true,
                    actionSpecificReady: true,
                    targetSpecificReady: true));
        }

        return new DarkKnightShadowbringerDecision(
            buffered,
            DarkKnightShadowbringerDecisionKind.Dispatch,
            DarkKnightShadowbringerDecisionReason.None,
            intent,
            InputClaimed: true);
    }

    private static DarkKnightShadowbringerDecisionReason GetPermanentGateFailure(
        DarkKnightShadowbringerObservation observation)
    {
        if (!observation.ConfigurationEnabled)
            return DarkKnightShadowbringerDecisionReason.ConfigurationDisabled;
        if (observation.Context is not
            (SupportedPvPContext.CrystallineConflict or SupportedPvPContext.WolvesDen))
        {
            return DarkKnightShadowbringerDecisionReason.OutsideSupportedPvPContext;
        }

        if (!observation.LocalPlayer.IsValid)
            return DarkKnightShadowbringerDecisionReason.LocalPlayerIdentityInvalid;
        if (!observation.IsLocalPlayerAlive)
            return DarkKnightShadowbringerDecisionReason.LocalPlayerDead;
        if (!observation.IsLocalPlayerTargetable)
            return DarkKnightShadowbringerDecisionReason.LocalPlayerUntargetable;
        if (observation.LocalJobId != DarkKnightJobId)
            return DarkKnightShadowbringerDecisionReason.LocalJobInvalid;
        if (!observation.MetadataVerified)
            return DarkKnightShadowbringerDecisionReason.MetadataUnverified;
        if (!observation.InputProbeSucceeded)
            return DarkKnightShadowbringerDecisionReason.InputProbeUnavailable;
        if (observation.IsTextInputActive)
            return DarkKnightShadowbringerDecisionReason.TextInputActive;
        return DarkKnightShadowbringerDecisionReason.None;
    }

    private static bool IsExactCandidate(
        DarkKnightShadowbringerIntent intent,
        DarkKnightShadowbringerCandidate candidate) =>
        candidate.Context == intent.Context &&
        candidate.EnemySlot == intent.EnemySlot &&
        candidate.Actor == intent.Target &&
        IsEligibleCandidate(candidate, intent.LocalPlayer);

    private static bool Allows(
        DarkKnightShadowbringerDispatchPolicy policy,
        DarkKnightShadowbringerOpportunityKind opportunity) => policy switch
        {
            DarkKnightShadowbringerDispatchPolicy.Any =>
                opportunity is DarkKnightShadowbringerOpportunityKind.DarkArts or
                    DarkKnightShadowbringerOpportunityKind.SafeHpCost,
            DarkKnightShadowbringerDispatchPolicy.DarkArtsOnly =>
                opportunity == DarkKnightShadowbringerOpportunityKind.DarkArts,
            DarkKnightShadowbringerDispatchPolicy.SafeHpCostOnly =>
                opportunity == DarkKnightShadowbringerOpportunityKind.SafeHpCost,
            _ => false,
        };

    private static int Compare(
        DarkKnightShadowbringerCandidate left,
        DarkKnightShadowbringerCandidate right)
    {
        var hp = ((ulong)left.CurrentHp * right.MaximumHp).CompareTo(
            (ulong)right.CurrentHp * left.MaximumHp);
        if (hp != 0) return hp;

        var slot = left.EnemySlot.CompareTo(right.EnemySlot);
        if (slot != 0) return slot;

        var entity = left.Actor.EntityId.CompareTo(right.Actor.EntityId);
        return entity != 0
            ? entity
            : left.Actor.GameObjectId.CompareTo(right.Actor.GameObjectId);
    }

    private static bool HasValidHp(uint currentHp, uint maximumHp) =>
        currentHp > 0 && maximumHp > 0 && currentHp <= maximumHp;

    private static long NextGeneration(long current) =>
        current is >= 0 and < long.MaxValue ? current + 1 : 0;

    private static DarkKnightShadowbringerState Stamp(
        DarkKnightShadowbringerState state,
        long nowMilliseconds) =>
        state with { LastObservedAtMilliseconds = nowMilliseconds };

    private static DarkKnightShadowbringerNativeAttemptDecision TerminalUnknown() =>
        new(
            DarkKnightShadowbringerState.Initial,
            DarkKnightShadowbringerDecisionReason.NativeAcceptanceUnknown,
            HeldActionRetryDisposition.AmbiguousTerminal,
            false, false, true, true);

    private static DarkKnightShadowbringerDecision Armed(
        DarkKnightShadowbringerState state,
        DarkKnightShadowbringerDecisionReason reason,
        bool inputClaimed) => new(
        state,
        DarkKnightShadowbringerDecisionKind.Armed,
        reason,
        state.Intent,
        inputClaimed);

    private static DarkKnightShadowbringerDecision None(
        DarkKnightShadowbringerState state,
        DarkKnightShadowbringerDecisionReason reason) => new(
        state,
        DarkKnightShadowbringerDecisionKind.None,
        reason);

    private static DarkKnightShadowbringerDecision Cancelled(
        DarkKnightShadowbringerState state,
        DarkKnightShadowbringerDecisionReason reason,
        bool spendOpportunity = false) => new(
        state,
        DarkKnightShadowbringerDecisionKind.Cancelled,
        reason,
        SpendOpportunity: spendOpportunity);
}
