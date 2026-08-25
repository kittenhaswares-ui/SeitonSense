namespace SeitonSense.Core;

public readonly record struct GunbreakerContinuationExposureState(
    long Generation,
    uint EpisodeActionId,
    uint EpisodeProcStatusId,
    bool IsCurrentlyExposed,
    bool IsSpent,
    int ConsecutiveNonFollowUpObservations)
{
    public static GunbreakerContinuationExposureState Initial => default;

    public bool HasTrackedEpisode =>
        Generation > 0 &&
        GunbreakerContinuationRules.IsExactFollowUpAction(EpisodeActionId) &&
        EpisodeProcStatusId ==
            GunbreakerContinuationRules.GetExpectedProcStatusId(EpisodeActionId);

    public bool HasCurrentFollowUp =>
        HasTrackedEpisode && IsCurrentlyExposed;

    public uint CurrentActionId => HasCurrentFollowUp ? EpisodeActionId : 0;

    public uint CurrentProcStatusId =>
        HasCurrentFollowUp ? EpisodeProcStatusId : 0;

    public bool IsValid =>
        Generation >= 0 &&
        ConsecutiveNonFollowUpObservations is >= 0 and <= 2 &&
        (Generation == 0
            ? EpisodeActionId == 0 && EpisodeProcStatusId == 0 &&
              !IsCurrentlyExposed && !IsSpent
            : EpisodeActionId == 0
                ? EpisodeProcStatusId == 0 && !IsCurrentlyExposed && !IsSpent &&
                  ConsecutiveNonFollowUpObservations == 2
                : GunbreakerContinuationRules.IsExactFollowUpAction(EpisodeActionId) &&
                  EpisodeProcStatusId ==
                      GunbreakerContinuationRules.GetExpectedProcStatusId(EpisodeActionId) &&
                  (IsCurrentlyExposed
                      ? ConsecutiveNonFollowUpObservations == 0
                      : ConsecutiveNonFollowUpObservations == 1));
}

public readonly record struct GunbreakerContinuationCandidate(
    SupportedPvPContext Context,
    int EnemySlot,
    TargetPressureActorIdentity Actor,
    bool ExactCanonicalIdentity,
    bool Alive,
    bool Targetable,
    uint CurrentHp,
    uint MaxHp,
    bool HasValidActionTarget,
    bool HasNativeRangeAndLineOfSight);

public readonly record struct GunbreakerContinuationIntent(
    long ExposureGeneration,
    SupportedPvPContext Context,
    int EnemySlot,
    TargetPressureActorIdentity LocalPlayer,
    TargetPressureActorIdentity Target,
    uint ActionId,
    uint ProcStatusId,
    int FrozenKeyCode)
{
    public bool IsValid =>
        ExposureGeneration > 0 &&
        GunbreakerContinuationRules.IsContextSlotValid(Context, EnemySlot) &&
        LocalPlayer.IsValid &&
        Target.IsValid &&
        Target != LocalPlayer &&
        GunbreakerContinuationRules.IsExactFollowUpAction(ActionId) &&
        ProcStatusId == GunbreakerContinuationRules.GetExpectedProcStatusId(ActionId) &&
        FrozenKeyCode > 0;
}

public enum GunbreakerContinuationPhase : byte
{
    Waiting = 0,
    Buffered = 1,
}

public readonly record struct GunbreakerContinuationState(
    GunbreakerContinuationPhase Phase,
    GunbreakerContinuationIntent? Intent,
    HeldActionRetryState Retry,
    long LastObservedAtMilliseconds,
    ClientActionAttemptOutcome LastNativeOutcome)
{
    public static GunbreakerContinuationState Initial => new(
        GunbreakerContinuationPhase.Waiting,
        null,
        HeldActionRetryState.Initial,
        -1,
        ClientActionAttemptOutcome.None);
}

public readonly record struct GunbreakerContinuationObservation(
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
    GunbreakerContinuationExposureState Exposure,
    bool ActionLocallyReady,
    bool NativeBoundaryReady,
    GunbreakerContinuationCandidate? Candidate,
    bool HardReset,
    long NowMilliseconds);

public enum GunbreakerContinuationDecisionKind : byte
{
    None = 0,
    Armed = 1,
    Dispatch = 2,
    Cancelled = 3,
}

public enum GunbreakerContinuationDecisionReason : byte
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

public readonly record struct GunbreakerContinuationDecision(
    GunbreakerContinuationState NextState,
    GunbreakerContinuationDecisionKind Kind,
    GunbreakerContinuationDecisionReason Reason,
    GunbreakerContinuationIntent? Intent = null,
    bool InputClaimed = false)
{
    public bool ShouldDispatch =>
        Kind == GunbreakerContinuationDecisionKind.Dispatch &&
        Intent is { IsValid: true };
}

public readonly record struct GunbreakerContinuationNativeAttemptDecision(
    GunbreakerContinuationState NextState,
    GunbreakerContinuationDecisionReason Reason,
    HeldActionRetryDisposition Disposition,
    bool RetryScheduled,
    bool ClientAccepted,
    bool Terminal,
    bool SpendExposure,
    bool SoftWait = false);

public static class GunbreakerContinuationRules
{
    public const uint GunbreakerJobId = 37;
    public const uint CarrierActionId = 29_106;
    public const uint HypervelocityActionId = 29_107;
    public const uint JugularRipActionId = 29_108;
    public const uint AbdomenTearActionId = 29_109;
    public const uint EyeGougeActionId = 29_110;
    public const uint FatedBrandActionId = 41_442;

    public const uint ReadyToBlastStatusId = 3_041;
    public const uint ReadyToRipStatusId = 2_002;
    public const uint ReadyToTearStatusId = 2_003;
    public const uint ReadyToGougeStatusId = 2_004;
    public const uint ReadyToRazeStatusId = 4_293;
    public const float FatedBrandRadiusYalms = 6f;

    public const uint HypervelocityProcRowId = 82;
    public const uint JugularRipProcRowId = 42;
    public const uint AbdomenTearProcRowId = 43;
    public const uint EyeGougeProcRowId = 44;
    public const uint FatedBrandProcRowId = 232;

    public static bool IsExactFollowUpAction(uint actionId) => actionId is
        HypervelocityActionId or
        JugularRipActionId or
        AbdomenTearActionId or
        EyeGougeActionId or
        FatedBrandActionId;

    public static uint GetExpectedProcStatusId(uint actionId) => actionId switch
    {
        HypervelocityActionId => ReadyToBlastStatusId,
        JugularRipActionId => ReadyToRipStatusId,
        AbdomenTearActionId => ReadyToTearStatusId,
        EyeGougeActionId => ReadyToGougeStatusId,
        FatedBrandActionId => ReadyToRazeStatusId,
        _ => 0,
    };

    public static uint GetExpectedProcRowId(uint actionId) => actionId switch
    {
        HypervelocityActionId => HypervelocityProcRowId,
        JugularRipActionId => JugularRipProcRowId,
        AbdomenTearActionId => AbdomenTearProcRowId,
        EyeGougeActionId => EyeGougeProcRowId,
        FatedBrandActionId => FatedBrandProcRowId,
        _ => 0,
    };

    public static bool IsSelfCenteredAction(uint actionId) =>
        actionId == FatedBrandActionId;

    public static int GetMaximumRangeYalms(uint actionId) => actionId switch
    {
        HypervelocityActionId or JugularRipActionId or AbdomenTearActionId or
            EyeGougeActionId => 5,
        FatedBrandActionId => (int)FatedBrandRadiusYalms,
        _ => 0,
    };

    /// <summary>
    /// Converts the carrier's currently adjusted action into a monotone local
    /// exposure generation. A different exact follow-up is immediately a new
    /// episode. One non-follow-up sample retains the old episode as a flicker;
    /// two consecutive samples reset it so the same action may later rearm.
    /// </summary>
    public static GunbreakerContinuationExposureState ObserveCarrierExposure(
        GunbreakerContinuationExposureState previous,
        uint resolvedCarrierActionId,
        uint observedExactProcStatusId,
        bool hardReset = false)
    {
        if (hardReset) return GunbreakerContinuationExposureState.Initial;
        if (!previous.IsValid) previous = GunbreakerContinuationExposureState.Initial;

        var expectedProcStatusId = GetExpectedProcStatusId(resolvedCarrierActionId);
        if (IsExactFollowUpAction(resolvedCarrierActionId) &&
            expectedProcStatusId != 0 &&
            observedExactProcStatusId == expectedProcStatusId)
        {
            if (previous.HasTrackedEpisode &&
                previous.EpisodeActionId == resolvedCarrierActionId &&
                previous.EpisodeProcStatusId == observedExactProcStatusId)
            {
                return previous with
                {
                    IsCurrentlyExposed = true,
                    ConsecutiveNonFollowUpObservations = 0,
                };
            }

            var nextGeneration = NextExposureGeneration(previous.Generation);
            return nextGeneration > 0
                ? new GunbreakerContinuationExposureState(
                    nextGeneration,
                    resolvedCarrierActionId,
                    observedExactProcStatusId,
                    IsCurrentlyExposed: true,
                    IsSpent: false,
                    ConsecutiveNonFollowUpObservations: 0)
                : new GunbreakerContinuationExposureState(
                    previous.Generation,
                    0,
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

        return new GunbreakerContinuationExposureState(
            previous.Generation,
            0,
            0,
            IsCurrentlyExposed: false,
            IsSpent: false,
            ConsecutiveNonFollowUpObservations: misses);
    }

    public static GunbreakerContinuationExposureState MarkCarrierExposureSpent(
        GunbreakerContinuationExposureState state,
        long generation,
        uint actionId,
        uint procStatusId) =>
        state.IsValid &&
        state.HasTrackedEpisode &&
        generation > 0 &&
        state.Generation == generation &&
        state.EpisodeActionId == actionId &&
        state.EpisodeProcStatusId == procStatusId
            ? state with { IsSpent = true }
            : state;

    public static bool IsCurrentUnspentExposure(
        GunbreakerContinuationExposureState exposure,
        long generation,
        uint actionId,
        uint procStatusId) =>
        IsTrackedUnspentExposure(exposure, generation, actionId, procStatusId) &&
        exposure.IsCurrentlyExposed;

    public static bool IsTrackedUnspentExposure(
        GunbreakerContinuationExposureState exposure,
        long generation,
        uint actionId,
        uint procStatusId) =>
        exposure.IsValid &&
        exposure.HasTrackedEpisode &&
        !exposure.IsSpent &&
        generation > 0 &&
        exposure.Generation == generation &&
        exposure.EpisodeActionId == actionId &&
        exposure.EpisodeProcStatusId == procStatusId;

    /// <summary>
    /// A false return is retryable only when the exact target-aware status is
    /// ready both before and after, the carrier still resolves to the frozen
    /// action, and the complete native fingerprint stayed stable.
    /// </summary>
    public static ClientActionAttemptOutcome ClassifyFollowUpBoundary(
        bool clientReturnedAccepted,
        uint expectedActionId,
        uint expectedProcStatusId,
        uint procStatusBefore,
        uint procStatusAfter,
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
            expectedProcStatusId != GetExpectedProcStatusId(expectedActionId) ||
            procStatusBefore != expectedProcStatusId ||
            procStatusAfter != expectedProcStatusId ||
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

    /// <summary>
    /// Selects one exact reachable actor. CC ranks only canonical S1-S5
    /// identities by HP ratio and then stable slot/identity order. Any repeated
    /// canonical identity makes the whole observation ambiguous and fails
    /// closed. Wolves' Den accepts only the caller-provided current &lt;t&gt;
    /// candidate at slot zero.
    /// </summary>
    public static GunbreakerContinuationCandidate? SelectBestCandidate(
        SupportedPvPContext context,
        IReadOnlyList<GunbreakerContinuationCandidate> candidates)
    {
        if (context is not (SupportedPvPContext.CrystallineConflict or
                            SupportedPvPContext.WolvesDen) ||
            candidates is null)
        {
            return null;
        }

        GunbreakerContinuationCandidate? best = null;
        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            if (!IsSelectableCandidate(context, candidate)) continue;

            for (var otherIndex = index + 1;
                 otherIndex < candidates.Count;
                 otherIndex++)
            {
                var other = candidates[otherIndex];
                if (IsSelectableCandidate(context, other) &&
                    candidate.Actor == other.Actor)
                {
                    return null;
                }
            }

            if (context == SupportedPvPContext.WolvesDen)
            {
                // The runtime supplies exactly the native current hard target.
                // More than one eligible slot-zero candidate is ambiguous.
                if (best.HasValue) return null;
                best = candidate;
                continue;
            }

            if (!best.HasValue || CompareCandidates(candidate, best.Value) < 0)
                best = candidate;
        }

        return best;
    }

    public static GunbreakerContinuationDecision Observe(
        GunbreakerContinuationState previous,
        GunbreakerContinuationObservation observation)
    {
        if (observation.HardReset)
            return Cancelled(GunbreakerContinuationState.Initial, GunbreakerContinuationDecisionReason.HardReset);
        if (observation.NowMilliseconds < 0 ||
            (previous.LastObservedAtMilliseconds >= 0 &&
             observation.NowMilliseconds < previous.LastObservedAtMilliseconds))
        {
            return Cancelled(
                GunbreakerContinuationState.Initial,
                GunbreakerContinuationDecisionReason.ClockMovedBackwards);
        }

        var permanentFailure = GetPermanentGateFailure(observation);
        if (permanentFailure != GunbreakerContinuationDecisionReason.None)
            return None(GunbreakerContinuationState.Initial, permanentFailure);

        if (previous.Phase != GunbreakerContinuationPhase.Buffered)
            return TryCreateIntent(observation);

        if (previous.Intent is not { IsValid: true } intent)
        {
            return Cancelled(
                GunbreakerContinuationState.Initial,
                GunbreakerContinuationDecisionReason.NativeAcceptanceUnknown);
        }

        if (!IsTrackedUnspentExposure(
                observation.Exposure,
                intent.ExposureGeneration,
                intent.ActionId,
                intent.ProcStatusId))
        {
            // A genuinely different exact carrier action is a new proc now,
            // not one framework frame later.
            if (observation.Exposure.HasCurrentFollowUp &&
                !observation.Exposure.IsSpent)
            {
                return TryCreateIntent(observation);
            }

            return Cancelled(
                GunbreakerContinuationState.Initial,
                observation.Exposure.IsSpent &&
                observation.Exposure.Generation == intent.ExposureGeneration
                    ? GunbreakerContinuationDecisionReason.ExposureSpent
                    : GunbreakerContinuationDecisionReason.ExposureSuperseded);
        }

        return ObserveBuffered(previous, observation);
    }

    public static GunbreakerContinuationNativeAttemptDecision ApplyNativeAttemptOutcome(
        GunbreakerContinuationState current,
        ClientActionAttemptOutcome outcome,
        long nowMilliseconds)
    {
        if (current.Phase != GunbreakerContinuationPhase.Buffered ||
            current.Intent is not { IsValid: true } || nowMilliseconds < 0)
        {
            return TerminalUnknown();
        }

        var shared = HeldActionRetryRules.Complete(current.Retry, nowMilliseconds, outcome);
        return shared.Disposition switch
        {
            HeldActionRetryDisposition.SoftWait => new(
                Stamp(current with { LastNativeOutcome = outcome }, nowMilliseconds),
                GunbreakerContinuationDecisionReason.NativeBoundaryUnavailable,
                shared.Disposition,
                false, false, false, false, true),
            HeldActionRetryDisposition.AcceptedTerminal => new(
                GunbreakerContinuationState.Initial,
                GunbreakerContinuationDecisionReason.None,
                shared.Disposition,
                false, true, true, true),
            HeldActionRetryDisposition.RetryScheduled => new(
                Stamp(current with
                {
                    Retry = shared.NextState,
                    LastNativeOutcome = outcome,
                }, nowMilliseconds),
                GunbreakerContinuationDecisionReason.NativeRetryThrottle,
                shared.Disposition,
                true, false, false, false),
            HeldActionRetryDisposition.RejectedTerminal => new(
                GunbreakerContinuationState.Initial,
                GunbreakerContinuationDecisionReason.NativeRetryLimitReached,
                shared.Disposition,
                false, false, true, true),
            _ => TerminalUnknown(),
        };
    }

    public static bool CanUseFrozenIntent(
        GunbreakerContinuationIntent intent,
        bool configurationEnabled,
        SupportedPvPContext context,
        TargetPressureActorIdentity localPlayer,
        bool localAlive,
        uint localJobId,
        bool metadataVerified,
        bool guardSuppressed,
        bool higherPriorityClaimed,
        GunbreakerContinuationExposureState exposure,
        bool actionLocallyReady,
        int currentHeldKeyCode,
        bool frozenKeyStillDown,
        GunbreakerContinuationCandidate candidate) =>
        intent.IsValid && configurationEnabled && context == intent.Context &&
        localPlayer == intent.LocalPlayer && localAlive && localJobId == GunbreakerJobId &&
        metadataVerified && !guardSuppressed && !higherPriorityClaimed &&
        IsCurrentUnspentExposure(
            exposure,
            intent.ExposureGeneration,
            intent.ActionId,
            intent.ProcStatusId) &&
        actionLocallyReady &&
        currentHeldKeyCode == intent.FrozenKeyCode && frozenKeyStillDown &&
        IsExactCandidate(intent, candidate) && candidate.HasValidActionTarget &&
        candidate.HasNativeRangeAndLineOfSight;

    private static GunbreakerContinuationDecision TryCreateIntent(
        GunbreakerContinuationObservation observation)
    {
        if (observation.HigherPriorityClaimed)
            return None(GunbreakerContinuationState.Initial, GunbreakerContinuationDecisionReason.HigherPriorityClaimed);
        if (observation.ActionHelpersSuppressedByGuard)
            return None(GunbreakerContinuationState.Initial, GunbreakerContinuationDecisionReason.GuardSuppressed);
        if (!observation.Exposure.IsValid || !observation.Exposure.HasCurrentFollowUp)
            return None(GunbreakerContinuationState.Initial, GunbreakerContinuationDecisionReason.CarrierUnavailable);
        if (observation.Exposure.IsSpent)
            return None(GunbreakerContinuationState.Initial, GunbreakerContinuationDecisionReason.ExposureSpent);
        if (observation.Candidate is not { } candidate)
            return None(GunbreakerContinuationState.Initial, GunbreakerContinuationDecisionReason.CandidateUnavailable);
        if (!IsExactCandidate(observation, candidate))
            return None(GunbreakerContinuationState.Initial, GunbreakerContinuationDecisionReason.CandidateInvalid);
        if (!observation.HeldGameplayKeyEligible || observation.HeldGameplayKeyCode <= 0)
            return None(GunbreakerContinuationState.Initial, GunbreakerContinuationDecisionReason.NoHeldGameplayKey);

        var intent = new GunbreakerContinuationIntent(
            observation.Exposure.Generation,
            observation.Context,
            candidate.EnemySlot,
            observation.LocalPlayer,
            candidate.Actor,
            observation.Exposure.CurrentActionId,
            observation.Exposure.CurrentProcStatusId,
            observation.HeldGameplayKeyCode);
        if (!intent.IsValid)
            return Cancelled(GunbreakerContinuationState.Initial, GunbreakerContinuationDecisionReason.ExposureSuperseded);

        var buffered = new GunbreakerContinuationState(
            GunbreakerContinuationPhase.Buffered,
            intent,
            HeldActionRetryState.Initial,
            observation.NowMilliseconds,
            ClientActionAttemptOutcome.None);
        return EvaluateReadyIntent(buffered, intent, observation);
    }

    private static GunbreakerContinuationDecision ObserveBuffered(
        GunbreakerContinuationState previous,
        GunbreakerContinuationObservation observation)
    {
        var intent = previous.Intent;
        if (intent is not { IsValid: true })
            return Cancelled(GunbreakerContinuationState.Initial, GunbreakerContinuationDecisionReason.NativeAcceptanceUnknown);
        if (!observation.Exposure.IsCurrentlyExposed)
        {
            return Armed(
                Stamp(previous, observation.NowMilliseconds),
                GunbreakerContinuationDecisionReason.CarrierUnavailable,
                inputClaimed: false);
        }
        if (!observation.FrozenKeyStillDown)
            return None(GunbreakerContinuationState.Initial, GunbreakerContinuationDecisionReason.ExactKeyReleased);
        if (observation.HigherPriorityClaimed)
            return None(Stamp(previous, observation.NowMilliseconds), GunbreakerContinuationDecisionReason.HigherPriorityClaimed);
        if (observation.ActionHelpersSuppressedByGuard)
            return None(Stamp(previous, observation.NowMilliseconds), GunbreakerContinuationDecisionReason.GuardSuppressed);
        if (observation.Candidate is not { } candidate || !IsExactCandidate(intent.Value, candidate))
            return Cancelled(GunbreakerContinuationState.Initial, GunbreakerContinuationDecisionReason.CandidateUnavailable);
        if (!candidate.Alive || !candidate.Targetable || !candidate.ExactCanonicalIdentity)
            return Cancelled(GunbreakerContinuationState.Initial, GunbreakerContinuationDecisionReason.CandidateInvalid);

        return EvaluateReadyIntent(
            Stamp(previous, observation.NowMilliseconds),
            intent.Value,
            observation);
    }

    private static GunbreakerContinuationDecision EvaluateReadyIntent(
        GunbreakerContinuationState buffered,
        GunbreakerContinuationIntent intent,
        GunbreakerContinuationObservation observation)
    {
        if (!observation.ActionLocallyReady)
        {
            return Armed(
                buffered,
                GunbreakerContinuationDecisionReason.ActionNotReady,
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
                GunbreakerContinuationDecisionReason.TargetNotReady,
                inputClaimed: false);
        }

        if (!observation.NativeBoundaryReady)
        {
            return Armed(
                buffered,
                GunbreakerContinuationDecisionReason.NativeBoundaryUnavailable,
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
                GunbreakerContinuationDecisionReason.NativeRetryThrottle,
                HeldActionRetryRules.RetainsSchedulerFrame(
                    buffered.Retry,
                    observation.NowMilliseconds,
                    exactIntentValid: true,
                    actionSpecificReady: true,
                    targetSpecificReady: true));
        }

        return Dispatch(buffered, intent);
    }

    private static GunbreakerContinuationDecisionReason GetPermanentGateFailure(
        GunbreakerContinuationObservation observation)
    {
        if (!observation.ConfigurationEnabled) return GunbreakerContinuationDecisionReason.ConfigurationDisabled;
        if (observation.Context is not (SupportedPvPContext.CrystallineConflict or SupportedPvPContext.WolvesDen))
            return GunbreakerContinuationDecisionReason.OutsideSupportedPvPContext;
        if (!observation.LocalPlayer.IsValid) return GunbreakerContinuationDecisionReason.LocalPlayerIdentityInvalid;
        if (!observation.IsLocalPlayerAlive) return GunbreakerContinuationDecisionReason.LocalPlayerDead;
        if (observation.LocalJobId != GunbreakerJobId) return GunbreakerContinuationDecisionReason.LocalJobInvalid;
        if (!observation.MetadataVerified) return GunbreakerContinuationDecisionReason.MetadataUnverified;
        if (!observation.InputProbeSucceeded) return GunbreakerContinuationDecisionReason.InputProbeUnavailable;
        if (observation.IsTextInputActive) return GunbreakerContinuationDecisionReason.TextInputActive;
        return GunbreakerContinuationDecisionReason.None;
    }

    private static bool IsExactCandidate(
        GunbreakerContinuationObservation observation,
        GunbreakerContinuationCandidate candidate) =>
        observation.Exposure.HasCurrentFollowUp &&
        candidate.Context == observation.Context &&
        IsContextSlotValid(candidate.Context, candidate.EnemySlot) &&
        candidate.Actor.IsValid &&
        candidate.Actor != observation.LocalPlayer &&
        candidate.ExactCanonicalIdentity &&
        candidate.Alive &&
        candidate.Targetable &&
        candidate.MaxHp > 0 &&
        candidate.CurrentHp is > 0 && candidate.CurrentHp <= candidate.MaxHp;

    private static bool IsExactCandidate(
        GunbreakerContinuationIntent intent,
        GunbreakerContinuationCandidate candidate) =>
        candidate.Context == intent.Context &&
        candidate.EnemySlot == intent.EnemySlot &&
        candidate.Actor == intent.Target &&
        candidate.ExactCanonicalIdentity &&
        candidate.Alive &&
        candidate.Targetable &&
        candidate.MaxHp > 0 &&
        candidate.CurrentHp is > 0 && candidate.CurrentHp <= candidate.MaxHp;

    private static bool IsSelectableCandidate(
        SupportedPvPContext context,
        GunbreakerContinuationCandidate candidate) =>
        candidate.Context == context &&
        IsContextSlotValid(context, candidate.EnemySlot) &&
        candidate.Actor.IsValid &&
        candidate.ExactCanonicalIdentity &&
        candidate.Alive &&
        candidate.Targetable &&
        candidate.CurrentHp is > 0 &&
        candidate.MaxHp > 0 &&
        candidate.CurrentHp <= candidate.MaxHp &&
        candidate.HasValidActionTarget &&
        candidate.HasNativeRangeAndLineOfSight;

    private static int CompareCandidates(
        GunbreakerContinuationCandidate left,
        GunbreakerContinuationCandidate right)
    {
        var leftRatio = (ulong)left.CurrentHp * right.MaxHp;
        var rightRatio = (ulong)right.CurrentHp * left.MaxHp;
        var ratioComparison = leftRatio.CompareTo(rightRatio);
        if (ratioComparison != 0) return ratioComparison;

        var slotComparison = left.EnemySlot.CompareTo(right.EnemySlot);
        if (slotComparison != 0) return slotComparison;

        var objectComparison = left.Actor.GameObjectId.CompareTo(
            right.Actor.GameObjectId);
        return objectComparison != 0
            ? objectComparison
            : left.Actor.EntityId.CompareTo(right.Actor.EntityId);
    }

    private static long NextExposureGeneration(long current) =>
        current is >= 0 and < long.MaxValue ? current + 1 : 0;

    private static GunbreakerContinuationState Stamp(GunbreakerContinuationState state, long now) =>
        state with { LastObservedAtMilliseconds = now };

    private static GunbreakerContinuationNativeAttemptDecision TerminalUnknown() => new(
        GunbreakerContinuationState.Initial,
        GunbreakerContinuationDecisionReason.NativeAcceptanceUnknown,
        HeldActionRetryDisposition.AmbiguousTerminal,
        false, false, true, true);

    private static GunbreakerContinuationDecision Dispatch(
        GunbreakerContinuationState state,
        GunbreakerContinuationIntent intent) => new(
        state,
        GunbreakerContinuationDecisionKind.Dispatch,
        GunbreakerContinuationDecisionReason.None,
        intent,
        InputClaimed: true);

    private static GunbreakerContinuationDecision Armed(
        GunbreakerContinuationState state,
        GunbreakerContinuationDecisionReason reason,
        bool inputClaimed) => new(
        state,
        GunbreakerContinuationDecisionKind.Armed,
        reason,
        state.Intent,
        InputClaimed: inputClaimed);

    private static GunbreakerContinuationDecision None(
        GunbreakerContinuationState state,
        GunbreakerContinuationDecisionReason reason) => new(
        state,
        GunbreakerContinuationDecisionKind.None,
        reason);

    private static GunbreakerContinuationDecision Cancelled(
        GunbreakerContinuationState state,
        GunbreakerContinuationDecisionReason reason) => new(
        state,
        GunbreakerContinuationDecisionKind.Cancelled,
        reason);
}
