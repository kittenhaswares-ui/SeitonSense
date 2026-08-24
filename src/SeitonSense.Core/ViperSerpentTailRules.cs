namespace SeitonSense.Core;

public readonly record struct ViperSerpentTailExposureState(
    long Generation,
    uint EpisodeActionId,
    bool IsCurrentlyExposed,
    bool IsSpent,
    int ConsecutiveNonFollowUpObservations)
{
    public static ViperSerpentTailExposureState Initial => default;

    public bool HasTrackedEpisode =>
        Generation > 0 &&
        ViperSerpentTailRules.IsExactFollowUpAction(EpisodeActionId);

    public bool HasCurrentFollowUp =>
        HasTrackedEpisode && IsCurrentlyExposed;

    public uint CurrentActionId => HasCurrentFollowUp ? EpisodeActionId : 0;

    public bool IsValid =>
        Generation >= 0 &&
        ConsecutiveNonFollowUpObservations is >= 0 and <= 2 &&
        (Generation == 0
            ? EpisodeActionId == 0 && !IsCurrentlyExposed && !IsSpent
            : EpisodeActionId == 0
                ? !IsCurrentlyExposed && !IsSpent &&
                  ConsecutiveNonFollowUpObservations == 2
                : ViperSerpentTailRules.IsExactFollowUpAction(EpisodeActionId) &&
                  (IsCurrentlyExposed
                      ? ConsecutiveNonFollowUpObservations == 0
                      : ConsecutiveNonFollowUpObservations == 1));
}

public readonly record struct ViperSerpentTailCandidate(
    SupportedPvPContext Context,
    int EnemySlot,
    TargetPressureActorIdentity Actor,
    bool ExactCanonicalIdentity,
    bool Alive,
    bool Targetable,
    bool HasValidActionTarget,
    bool HasNativeRangeAndLineOfSight);

public readonly record struct ViperSerpentTailIntent(
    long ExposureGeneration,
    SupportedPvPContext Context,
    int EnemySlot,
    TargetPressureActorIdentity LocalPlayer,
    TargetPressureActorIdentity Target,
    uint ActionId,
    int FrozenKeyCode)
{
    public bool IsValid =>
        ExposureGeneration > 0 &&
        ViperSerpentTailRules.IsContextSlotValid(Context, EnemySlot) &&
        LocalPlayer.IsValid &&
        Target.IsValid &&
        Target != LocalPlayer &&
        ViperSerpentTailRules.IsExactFollowUpAction(ActionId) &&
        FrozenKeyCode > 0;
}

public enum ViperSerpentTailPhase : byte
{
    Waiting = 0,
    Buffered = 1,
}

public readonly record struct ViperSerpentTailState(
    ViperSerpentTailPhase Phase,
    ViperSerpentTailIntent? Intent,
    HeldActionRetryState Retry,
    long LastObservedAtMilliseconds,
    ClientActionAttemptOutcome LastNativeOutcome)
{
    public static ViperSerpentTailState Initial => new(
        ViperSerpentTailPhase.Waiting,
        null,
        HeldActionRetryState.Initial,
        -1,
        ClientActionAttemptOutcome.None);
}

public readonly record struct ViperSerpentTailObservation(
    bool ConfigurationEnabled,
    SupportedPvPContext Context,
    TargetPressureActorIdentity LocalPlayer,
    bool IsLocalPlayerAlive,
    uint LocalJobId,
    bool MetadataVerified,
    bool ActionHelpersSuppressedByGuard,
    bool HigherPriorityClaimed,
    bool InputProbeSucceeded,
    bool IsTextInputActive,
    bool HeldGameplayKeyEligible,
    int HeldGameplayKeyCode,
    bool FrozenKeyStillDown,
    ViperSerpentTailExposureState Exposure,
    bool ActionLocallyReady,
    bool NativeBoundaryReady,
    ViperSerpentTailCandidate? Candidate,
    bool HardReset,
    long NowMilliseconds);

public enum ViperSerpentTailDecisionKind : byte
{
    None = 0,
    Armed = 1,
    Dispatch = 2,
    Cancelled = 3,
}

public enum ViperSerpentTailDecisionReason : byte
{
    None = 0,
    HardReset,
    ClockMovedBackwards,
    ConfigurationDisabled,
    OutsideSupportedPvPContext,
    LocalPlayerIdentityInvalid,
    LocalPlayerDead,
    LocalJobInvalid,
    MetadataUnverified,
    InputProbeUnavailable,
    TextInputActive,
    GuardSuppressed,
    HigherPriorityClaimed,
    CarrierUnavailable,
    ExposureSpent,
    ExposureSuperseded,
    CandidateUnavailable,
    CandidateInvalid,
    NoHeldGameplayKey,
    ExactKeyReleased,
    ActionNotReady,
    TargetNotReady,
    NativeBoundaryUnavailable,
    NativeRetryThrottle,
    NativeRetryLimitReached,
    NativeAcceptanceUnknown,
}

public readonly record struct ViperSerpentTailDecision(
    ViperSerpentTailState NextState,
    ViperSerpentTailDecisionKind Kind,
    ViperSerpentTailDecisionReason Reason,
    ViperSerpentTailIntent? Intent = null,
    bool InputClaimed = false)
{
    public bool ShouldDispatch =>
        Kind == ViperSerpentTailDecisionKind.Dispatch &&
        Intent is { IsValid: true };
}

public readonly record struct ViperSerpentTailNativeAttemptDecision(
    ViperSerpentTailState NextState,
    ViperSerpentTailDecisionReason Reason,
    HeldActionRetryDisposition Disposition,
    bool RetryScheduled,
    bool ClientAccepted,
    bool Terminal,
    bool SpendExposure,
    bool SoftWait = false);

public static class ViperSerpentTailRules
{
    public const uint ViperJobId = 41;
    public const uint WolvesDenStrikingDummyNameId = 541;
    public const uint CarrierActionId = 39_183;
    public const uint DeathRattleActionId = 39_174;
    public const uint TwinfangBiteActionId = 39_175;
    public const uint TwinbloodBiteActionId = 39_176;
    public const uint UncoiledTwinfangActionId = 39_177;
    public const uint UncoiledTwinbloodActionId = 39_178;
    public const uint FirstLegacyActionId = 39_179;
    public const uint SecondLegacyActionId = 39_180;
    public const uint ThirdLegacyActionId = 39_181;
    public const uint FourthLegacyActionId = 39_182;

    public static bool IsExactFollowUpAction(uint actionId) => actionId is
        DeathRattleActionId or
        TwinfangBiteActionId or
        TwinbloodBiteActionId or
        UncoiledTwinfangActionId or
        UncoiledTwinbloodActionId or
        FirstLegacyActionId or
        SecondLegacyActionId or
        ThirdLegacyActionId or
        FourthLegacyActionId;

    public static int GetMaximumRangeYalms(uint actionId) => actionId switch
    {
        UncoiledTwinfangActionId or UncoiledTwinbloodActionId => 20,
        DeathRattleActionId or TwinfangBiteActionId or TwinbloodBiteActionId or
            FirstLegacyActionId or SecondLegacyActionId or ThirdLegacyActionId or
            FourthLegacyActionId => 5,
        _ => 0,
    };

    /// <summary>
    /// Converts the carrier's currently adjusted action into a monotone local
    /// exposure generation. A different exact follow-up is immediately a new
    /// episode. One non-follow-up sample retains the old episode as a flicker;
    /// two consecutive samples reset it so the same action may later rearm.
    /// </summary>
    public static ViperSerpentTailExposureState ObserveCarrierExposure(
        ViperSerpentTailExposureState previous,
        uint resolvedCarrierActionId,
        bool hardReset = false)
    {
        if (hardReset) return ViperSerpentTailExposureState.Initial;
        if (!previous.IsValid) previous = ViperSerpentTailExposureState.Initial;

        if (IsExactFollowUpAction(resolvedCarrierActionId))
        {
            if (previous.HasTrackedEpisode &&
                previous.EpisodeActionId == resolvedCarrierActionId)
            {
                return previous with
                {
                    IsCurrentlyExposed = true,
                    ConsecutiveNonFollowUpObservations = 0,
                };
            }

            var nextGeneration = NextExposureGeneration(previous.Generation);
            return nextGeneration > 0
                ? new ViperSerpentTailExposureState(
                    nextGeneration,
                    resolvedCarrierActionId,
                    IsCurrentlyExposed: true,
                    IsSpent: false,
                    ConsecutiveNonFollowUpObservations: 0)
                : new ViperSerpentTailExposureState(
                    previous.Generation,
                    0,
                    IsCurrentlyExposed: false,
                    IsSpent: false,
                    ConsecutiveNonFollowUpObservations: 2);
        }

        var misses = Math.Min(
            2,
            previous.ConsecutiveNonFollowUpObservations + 1);
        if (previous.HasTrackedEpisode && misses == 1)
        {
            return previous with
            {
                IsCurrentlyExposed = false,
                ConsecutiveNonFollowUpObservations = 1,
            };
        }

        return new ViperSerpentTailExposureState(
            previous.Generation,
            0,
            IsCurrentlyExposed: false,
            IsSpent: false,
            ConsecutiveNonFollowUpObservations: misses);
    }

    public static ViperSerpentTailExposureState MarkCarrierExposureSpent(
        ViperSerpentTailExposureState state,
        long generation,
        uint actionId) =>
        state.IsValid &&
        state.HasTrackedEpisode &&
        generation > 0 &&
        state.Generation == generation &&
        state.EpisodeActionId == actionId
            ? state with { IsSpent = true }
            : state;

    public static bool IsCurrentUnspentExposure(
        ViperSerpentTailExposureState exposure,
        long generation,
        uint actionId) =>
        IsTrackedUnspentExposure(exposure, generation, actionId) &&
        exposure.IsCurrentlyExposed;

    public static bool IsTrackedUnspentExposure(
        ViperSerpentTailExposureState exposure,
        long generation,
        uint actionId) =>
        exposure.IsValid &&
        exposure.HasTrackedEpisode &&
        !exposure.IsSpent &&
        generation > 0 &&
        exposure.Generation == generation &&
        exposure.EpisodeActionId == actionId;

    /// <summary>
    /// A false return is retryable only when the exact target-aware status is
    /// ready both before and after, the carrier still resolves to the frozen
    /// action, and the complete native fingerprint stayed stable.
    /// </summary>
    public static ClientActionAttemptOutcome ClassifyFollowUpBoundary(
        bool clientReturnedAccepted,
        uint expectedActionId,
        uint targetStatusBefore,
        uint targetStatusAfter,
        uint carrierBefore,
        uint carrierAfter,
        ClientActionAttemptFingerprint before,
        ClientActionAttemptFingerprint after)
    {
        if (clientReturnedAccepted)
            return ClientActionAttemptOutcome.ClientAccepted;
        if (expectedActionId == 0 ||
            targetStatusBefore != 0 ||
            targetStatusAfter != 0 ||
            carrierBefore != expectedActionId ||
            carrierAfter != expectedActionId)
        {
            return ClientActionAttemptOutcome.AcceptanceUnknown;
        }

        return ClientActionAttemptBoundaryRules.Classify(
            false,
            expectedActionId,
            before,
            after);
    }

    public static bool IsContextSlotValid(SupportedPvPContext context, int enemySlot) =>
        context switch
        {
            SupportedPvPContext.CrystallineConflict => EnemySlotRules.IsValidSlot(enemySlot),
            SupportedPvPContext.WolvesDen => enemySlot == 0,
            _ => false,
        };

    public static ViperSerpentTailDecision Observe(
        ViperSerpentTailState previous,
        ViperSerpentTailObservation observation)
    {
        if (observation.HardReset)
            return Cancelled(ViperSerpentTailState.Initial, ViperSerpentTailDecisionReason.HardReset);
        if (observation.NowMilliseconds < 0 ||
            (previous.LastObservedAtMilliseconds >= 0 &&
             observation.NowMilliseconds < previous.LastObservedAtMilliseconds))
        {
            return Cancelled(
                ViperSerpentTailState.Initial,
                ViperSerpentTailDecisionReason.ClockMovedBackwards);
        }

        var permanentFailure = GetPermanentGateFailure(observation);
        if (permanentFailure != ViperSerpentTailDecisionReason.None)
            return None(ViperSerpentTailState.Initial, permanentFailure);

        if (previous.Phase != ViperSerpentTailPhase.Buffered)
            return TryCreateIntent(observation);

        if (previous.Intent is not { IsValid: true } intent)
        {
            return Cancelled(
                ViperSerpentTailState.Initial,
                ViperSerpentTailDecisionReason.NativeAcceptanceUnknown);
        }

        if (!IsTrackedUnspentExposure(
                observation.Exposure,
                intent.ExposureGeneration,
                intent.ActionId))
        {
            // A genuinely different exact carrier action is a new proc now,
            // not one framework frame later.
            if (observation.Exposure.HasCurrentFollowUp &&
                !observation.Exposure.IsSpent)
            {
                return TryCreateIntent(observation);
            }

            return Cancelled(
                ViperSerpentTailState.Initial,
                observation.Exposure.IsSpent &&
                observation.Exposure.Generation == intent.ExposureGeneration
                    ? ViperSerpentTailDecisionReason.ExposureSpent
                    : ViperSerpentTailDecisionReason.ExposureSuperseded);
        }

        return ObserveBuffered(previous, observation);
    }

    public static ViperSerpentTailNativeAttemptDecision ApplyNativeAttemptOutcome(
        ViperSerpentTailState current,
        ClientActionAttemptOutcome outcome,
        long nowMilliseconds)
    {
        if (current.Phase != ViperSerpentTailPhase.Buffered ||
            current.Intent is not { IsValid: true } || nowMilliseconds < 0)
        {
            return TerminalUnknown();
        }

        var shared = HeldActionRetryRules.Complete(current.Retry, nowMilliseconds, outcome);
        return shared.Disposition switch
        {
            HeldActionRetryDisposition.SoftWait => new(
                Stamp(current with { LastNativeOutcome = outcome }, nowMilliseconds),
                ViperSerpentTailDecisionReason.NativeBoundaryUnavailable,
                shared.Disposition,
                false, false, false, false, true),
            HeldActionRetryDisposition.AcceptedTerminal => new(
                ViperSerpentTailState.Initial,
                ViperSerpentTailDecisionReason.None,
                shared.Disposition,
                false, true, true, true),
            HeldActionRetryDisposition.RetryScheduled => new(
                Stamp(current with
                {
                    Retry = shared.NextState,
                    LastNativeOutcome = outcome,
                }, nowMilliseconds),
                ViperSerpentTailDecisionReason.NativeRetryThrottle,
                shared.Disposition,
                true, false, false, false),
            HeldActionRetryDisposition.RejectedTerminal => new(
                ViperSerpentTailState.Initial,
                ViperSerpentTailDecisionReason.NativeRetryLimitReached,
                shared.Disposition,
                false, false, true, true),
            _ => TerminalUnknown(),
        };
    }

    public static bool CanUseFrozenIntent(
        ViperSerpentTailIntent intent,
        bool configurationEnabled,
        SupportedPvPContext context,
        TargetPressureActorIdentity localPlayer,
        bool localAlive,
        uint localJobId,
        bool metadataVerified,
        bool guardSuppressed,
        bool higherPriorityClaimed,
        ViperSerpentTailExposureState exposure,
        bool actionLocallyReady,
        int currentHeldKeyCode,
        bool frozenKeyStillDown,
        ViperSerpentTailCandidate candidate) =>
        intent.IsValid && configurationEnabled && context == intent.Context &&
        localPlayer == intent.LocalPlayer && localAlive && localJobId == ViperJobId &&
        metadataVerified && !guardSuppressed && !higherPriorityClaimed &&
        IsCurrentUnspentExposure(
            exposure,
            intent.ExposureGeneration,
            intent.ActionId) &&
        actionLocallyReady &&
        currentHeldKeyCode == intent.FrozenKeyCode && frozenKeyStillDown &&
        IsExactCandidate(intent, candidate) && candidate.HasValidActionTarget &&
        candidate.HasNativeRangeAndLineOfSight;

    private static ViperSerpentTailDecision TryCreateIntent(
        ViperSerpentTailObservation observation)
    {
        if (observation.HigherPriorityClaimed)
            return None(ViperSerpentTailState.Initial, ViperSerpentTailDecisionReason.HigherPriorityClaimed);
        if (observation.ActionHelpersSuppressedByGuard)
            return None(ViperSerpentTailState.Initial, ViperSerpentTailDecisionReason.GuardSuppressed);
        if (!observation.Exposure.IsValid || !observation.Exposure.HasCurrentFollowUp)
            return None(ViperSerpentTailState.Initial, ViperSerpentTailDecisionReason.CarrierUnavailable);
        if (observation.Exposure.IsSpent)
            return None(ViperSerpentTailState.Initial, ViperSerpentTailDecisionReason.ExposureSpent);
        if (observation.Candidate is not { } candidate)
            return None(ViperSerpentTailState.Initial, ViperSerpentTailDecisionReason.CandidateUnavailable);
        if (!IsExactCandidate(observation, candidate))
            return None(ViperSerpentTailState.Initial, ViperSerpentTailDecisionReason.CandidateInvalid);
        if (!observation.HeldGameplayKeyEligible || observation.HeldGameplayKeyCode <= 0)
            return None(ViperSerpentTailState.Initial, ViperSerpentTailDecisionReason.NoHeldGameplayKey);

        var intent = new ViperSerpentTailIntent(
            observation.Exposure.Generation,
            observation.Context,
            candidate.EnemySlot,
            observation.LocalPlayer,
            candidate.Actor,
            observation.Exposure.CurrentActionId,
            observation.HeldGameplayKeyCode);
        if (!intent.IsValid)
            return Cancelled(ViperSerpentTailState.Initial, ViperSerpentTailDecisionReason.ExposureSuperseded);

        var buffered = new ViperSerpentTailState(
            ViperSerpentTailPhase.Buffered,
            intent,
            HeldActionRetryState.Initial,
            observation.NowMilliseconds,
            ClientActionAttemptOutcome.None);
        return EvaluateReadyIntent(buffered, intent, observation);
    }

    private static ViperSerpentTailDecision ObserveBuffered(
        ViperSerpentTailState previous,
        ViperSerpentTailObservation observation)
    {
        var intent = previous.Intent;
        if (intent is not { IsValid: true })
            return Cancelled(ViperSerpentTailState.Initial, ViperSerpentTailDecisionReason.NativeAcceptanceUnknown);
        if (!observation.Exposure.IsCurrentlyExposed)
        {
            return Armed(
                Stamp(previous, observation.NowMilliseconds),
                ViperSerpentTailDecisionReason.CarrierUnavailable,
                inputClaimed: false);
        }
        if (!observation.FrozenKeyStillDown)
            return None(ViperSerpentTailState.Initial, ViperSerpentTailDecisionReason.ExactKeyReleased);
        if (observation.HigherPriorityClaimed)
            return None(Stamp(previous, observation.NowMilliseconds), ViperSerpentTailDecisionReason.HigherPriorityClaimed);
        if (observation.ActionHelpersSuppressedByGuard)
            return None(Stamp(previous, observation.NowMilliseconds), ViperSerpentTailDecisionReason.GuardSuppressed);
        if (observation.Candidate is not { } candidate || !IsExactCandidate(intent.Value, candidate))
            return Cancelled(ViperSerpentTailState.Initial, ViperSerpentTailDecisionReason.CandidateUnavailable);
        if (!candidate.Alive || !candidate.Targetable || !candidate.ExactCanonicalIdentity)
            return Cancelled(ViperSerpentTailState.Initial, ViperSerpentTailDecisionReason.CandidateInvalid);

        return EvaluateReadyIntent(
            Stamp(previous, observation.NowMilliseconds),
            intent.Value,
            observation);
    }

    private static ViperSerpentTailDecision EvaluateReadyIntent(
        ViperSerpentTailState buffered,
        ViperSerpentTailIntent intent,
        ViperSerpentTailObservation observation)
    {
        if (!observation.ActionLocallyReady)
        {
            return Armed(
                buffered,
                ViperSerpentTailDecisionReason.ActionNotReady,
                inputClaimed: false);
        }

        var targetReady = observation.Candidate is { } candidate &&
                          IsExactCandidate(intent, candidate) &&
                          candidate.HasValidActionTarget &&
                          candidate.HasNativeRangeAndLineOfSight;
        if (!targetReady)
        {
            return Armed(
                buffered,
                ViperSerpentTailDecisionReason.TargetNotReady,
                inputClaimed: false);
        }

        if (!observation.NativeBoundaryReady)
        {
            return Armed(
                buffered,
                ViperSerpentTailDecisionReason.NativeBoundaryUnavailable,
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
                ViperSerpentTailDecisionReason.NativeRetryThrottle,
                HeldActionRetryRules.RetainsSchedulerFrame(
                    buffered.Retry,
                    observation.NowMilliseconds,
                    exactIntentValid: true,
                    actionSpecificReady: true,
                    targetSpecificReady: true));
        }

        return Dispatch(buffered, intent);
    }

    private static ViperSerpentTailDecisionReason GetPermanentGateFailure(
        ViperSerpentTailObservation observation)
    {
        if (!observation.ConfigurationEnabled) return ViperSerpentTailDecisionReason.ConfigurationDisabled;
        if (observation.Context is not (SupportedPvPContext.CrystallineConflict or SupportedPvPContext.WolvesDen))
            return ViperSerpentTailDecisionReason.OutsideSupportedPvPContext;
        if (!observation.LocalPlayer.IsValid) return ViperSerpentTailDecisionReason.LocalPlayerIdentityInvalid;
        if (!observation.IsLocalPlayerAlive) return ViperSerpentTailDecisionReason.LocalPlayerDead;
        if (observation.LocalJobId != ViperJobId) return ViperSerpentTailDecisionReason.LocalJobInvalid;
        if (!observation.MetadataVerified) return ViperSerpentTailDecisionReason.MetadataUnverified;
        if (!observation.InputProbeSucceeded) return ViperSerpentTailDecisionReason.InputProbeUnavailable;
        if (observation.IsTextInputActive) return ViperSerpentTailDecisionReason.TextInputActive;
        return ViperSerpentTailDecisionReason.None;
    }

    private static bool IsExactCandidate(
        ViperSerpentTailObservation observation,
        ViperSerpentTailCandidate candidate) =>
        observation.Exposure.HasCurrentFollowUp &&
        candidate.Context == observation.Context &&
        IsContextSlotValid(candidate.Context, candidate.EnemySlot) &&
        candidate.Actor.IsValid &&
        candidate.Actor != observation.LocalPlayer &&
        candidate.ExactCanonicalIdentity &&
        candidate.Alive &&
        candidate.Targetable;

    private static bool IsExactCandidate(
        ViperSerpentTailIntent intent,
        ViperSerpentTailCandidate candidate) =>
        candidate.Context == intent.Context &&
        candidate.EnemySlot == intent.EnemySlot &&
        candidate.Actor == intent.Target &&
        candidate.ExactCanonicalIdentity &&
        candidate.Alive &&
        candidate.Targetable;

    private static long NextExposureGeneration(long current) =>
        current is >= 0 and < long.MaxValue ? current + 1 : 0;

    private static ViperSerpentTailState Stamp(ViperSerpentTailState state, long now) =>
        state with { LastObservedAtMilliseconds = now };

    private static ViperSerpentTailNativeAttemptDecision TerminalUnknown() => new(
        ViperSerpentTailState.Initial,
        ViperSerpentTailDecisionReason.NativeAcceptanceUnknown,
        HeldActionRetryDisposition.AmbiguousTerminal,
        false, false, true, true);

    private static ViperSerpentTailDecision Dispatch(
        ViperSerpentTailState state,
        ViperSerpentTailIntent intent) => new(
        state,
        ViperSerpentTailDecisionKind.Dispatch,
        ViperSerpentTailDecisionReason.None,
        intent,
        InputClaimed: true);

    private static ViperSerpentTailDecision Armed(
        ViperSerpentTailState state,
        ViperSerpentTailDecisionReason reason,
        bool inputClaimed) => new(
        state,
        ViperSerpentTailDecisionKind.Armed,
        reason,
        state.Intent,
        InputClaimed: inputClaimed);

    private static ViperSerpentTailDecision None(
        ViperSerpentTailState state,
        ViperSerpentTailDecisionReason reason) => new(
        state,
        ViperSerpentTailDecisionKind.None,
        reason);

    private static ViperSerpentTailDecision Cancelled(
        ViperSerpentTailState state,
        ViperSerpentTailDecisionReason reason) => new(
        state,
        ViperSerpentTailDecisionKind.Cancelled,
        reason);
}
