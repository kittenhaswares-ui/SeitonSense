using System.Collections.Immutable;

namespace SeitonSense.Core;

public enum MiracleCleanseFollowupPhase
{
    WaitingForSignal = 0,
    WaitingForResilience = 1,
    WaitingForResilienceEnd = 2,
    ReleaseOpportunity = 3,
}

public enum MiracleCleanseFollowupDecisionKind
{
    None = 0,
    SignalObserved = 1,
    ResilienceObserved = 2,
    Waiting = 3,
    ReadyForPromotion = 4,
    Cancelled = 5,
}

public enum MiracleCleanseFollowupCancelReason
{
    None = 0,
    ConfigurationDisabled = 1,
    OutsideCrystallineConflict = 2,
    LocalCounterJobInvalid = 3,
    InvalidSignal = 4,
    ConcurrentSignal = 5,
    CandidateIdentityInvalid = 6,
    CandidateChanged = 7,
    ResilienceObservationAmbiguous = 8,
    ResilienceNotObserved = 9,
    ResilienceReleaseTimedOut = 10,
    ResilienceReturnedAfterRelease = 11,
    ReleaseOpportunityExpired = 12,
    ClockMovedBackwards = 13,
    HardReset = 14,
    ReservationKeyReleased = 15,
}

/// <summary>
/// One exact server ActionEffect result proving that an enemy used Purify on
/// itself. A recovered status is retained when the packet exposes one; live
/// Resilience is still required before the episode can progress. Sequence
/// identity makes duplicate packets harmless and is retained before any later
/// native counter-CC attempt.
/// </summary>
public readonly record struct MiracleCleanseFollowupSignalKey(
    uint CasterEntityId,
    uint ActionId,
    uint TargetEntityId,
    byte EffectType,
    ushort EffectValue,
    uint GlobalSequence,
    ushort SourceSequence);

public readonly record struct MiracleCleanseFollowupTargetIdentity(
    ulong GameObjectId,
    uint EntityId,
    uint JobId)
{
    public bool IsValid =>
        TargetHighlightRules.IsValidGameObjectId(GameObjectId) &&
        MiracleCleanseFollowupRules.IsValidEntityId(EntityId) &&
        JobId != 0;
}

public readonly record struct MiracleCleanseFollowupSignal(
    MiracleCleanseFollowupSignalKey Key,
    MiracleCleanseFollowupTargetIdentity Target,
    long ObservedAtMilliseconds);

public readonly record struct MiracleCleanseFollowupSignalLedger(
    ImmutableArray<MiracleCleanseFollowupSignalKey> RetiredSignals)
{
    public static MiracleCleanseFollowupSignalLedger Initial => new([]);
}

public readonly record struct MiracleCleanseFollowupSignalRetirementDecision(
    MiracleCleanseFollowupSignalLedger NextState,
    bool IsNewValidatedSignal);

/// <summary>
/// One already-validated and terminally deduplicated Purify packet whose exact
/// e1-e5 actor row was temporarily unavailable. This carries no gameplay key,
/// target fallback, action intent, or mutable deadline.
/// </summary>
public readonly record struct MiracleCleanseFollowupPendingResolution(
    MiracleCleanseFollowupSignalKey Key,
    long ObservedAtMilliseconds,
    uint LocalEntityId,
    uint LocalCounterJobId,
    int FeatureGeneration)
{
    public bool IsValid =>
        MiracleCleanseFollowupRules.IsExactPurifySignal(
            Key.CasterEntityId,
            Key.ActionId,
            Key.TargetEntityId,
            Key.EffectType,
            Key.EffectValue,
            Key.GlobalSequence,
            Key.SourceSequence) &&
        ObservedAtMilliseconds >= 0 &&
        MiracleCleanseFollowupRules.IsValidEntityId(LocalEntityId) &&
        LocalEntityId != Key.CasterEntityId &&
        LocalCounterJobId != 0;
}

public enum MiracleCleanseFollowupResolutionDecisionKind
{
    Waiting = 0,
    Resolved = 1,
    Retired = 2,
}

public enum MiracleCleanseFollowupResolutionRetireReason
{
    None = 0,
    InvalidSignal = 1,
    ConfigurationDisabled = 2,
    OutsideCrystallineConflict = 3,
    LocalCounterJobInvalid = 4,
    LocalIdentityChanged = 5,
    LocalCounterJobChanged = 6,
    FeatureGenerationChanged = 7,
    AcquisitionExpired = 8,
    ClockMovedBackwards = 9,
    CanonicalIdentityChanged = 10,
    HardReset = 11,
}

/// <summary>
/// UniqueCanonicalTarget must remain null until the runtime proves exactly one
/// canonical e1-e5 row for the original caster. Null therefore means retry the
/// same signal, not select another actor.
/// </summary>
public readonly record struct MiracleCleanseFollowupResolutionObservation(
    bool ConfigurationEnabled,
    bool IsCrystallineConflict,
    bool IsLocalCounterJobValid,
    uint LocalEntityId,
    uint LocalCounterJobId,
    int FeatureGeneration,
    MiracleCleanseFollowupTargetIdentity? UniqueCanonicalTarget,
    long NowMilliseconds,
    bool HardReset = false)
{
    public bool IsWolvesDenTesting { get; init; }

    public bool IsSupportedContext =>
        ReactiveCounterCcProfileRules.IsSupportedContext(
            IsCrystallineConflict,
            IsWolvesDenTesting);
}

public readonly record struct MiracleCleanseFollowupResolutionDecision(
    MiracleCleanseFollowupResolutionDecisionKind Kind,
    MiracleCleanseFollowupPendingResolution? NextPending,
    MiracleCleanseFollowupSignal? ResolvedSignal,
    MiracleCleanseFollowupResolutionRetireReason RetireReason)
{
    public bool ShouldRetry =>
        Kind == MiracleCleanseFollowupResolutionDecisionKind.Waiting &&
        NextPending is { IsValid: true };

    public bool DidResolve =>
        Kind == MiracleCleanseFollowupResolutionDecisionKind.Resolved &&
        NextPending is null &&
        ResolvedSignal is { } signal &&
        MiracleCleanseFollowupRules.IsValidSignalShape(signal);
}

/// <summary>
/// Exact no-retry action intent. The Purify ActionEffect signal, not a mutable
/// StatusList slot or remaining-time estimate, is its identity.
/// </summary>
public readonly record struct MiracleCleanseFollowupIntent(
    MiracleCleanseFollowupSignal Signal,
    long ReleasedAtMilliseconds)
{
    public int GameplayKeyToken { get; init; }
    public long ExpectedProtectionEndAtMilliseconds { get; init; } = -1;

    public MiracleCleanseFollowupTargetIdentity Target => Signal.Target;

    public bool IsValid =>
        MiracleCleanseFollowupRules.IsValidSignalShape(Signal) &&
        ReleasedAtMilliseconds >= Signal.ObservedAtMilliseconds &&
        GameplayKeyToken > 0;
}

/// <summary>
/// One live observation of the exact canonical enemy bound to the server
/// signal. ActiveResilienceStatusCount must be exactly zero or one. More than
/// one matching row is ambiguous and cannot be interpreted as presence/end.
/// </summary>
public readonly record struct MiracleCleanseFollowupCandidate(
    MiracleCleanseFollowupTargetIdentity Target,
    bool IsExactCanonicalEnemy,
    bool IsAliveAndTargetable,
    int ActiveResilienceStatusCount)
{
    /// <summary>
    /// Advisory only. Live StatusList membership remains the authority for
    /// Resilience presence and release.
    /// </summary>
    public long ResilienceRemainingMilliseconds { get; init; }
    public int ReservationGameplayKeyToken { get; init; }
    public bool ReservedGameplayKeyPhysicallyDown { get; init; }
    public bool CounterActionReachable { get; init; }
}

public readonly record struct MiracleCleanseFollowupState(
    MiracleCleanseFollowupPhase Phase,
    MiracleCleanseFollowupSignal? ActiveSignal,
    bool ResiliencePresenceObserved,
    long ResilienceObservedAtMilliseconds,
    long ResilienceMissingSinceMilliseconds,
    long ReleasedAtMilliseconds,
    ImmutableArray<MiracleCleanseFollowupSignalKey> ObservedSignals,
    long LastObservedAtMilliseconds)
{
    public int GameplayKeyToken { get; init; }
    public long ExpectedProtectionEndAtMilliseconds { get; init; } = -1;

    public static MiracleCleanseFollowupState Initial => new(
        MiracleCleanseFollowupPhase.WaitingForSignal,
        null,
        false,
        -1,
        -1,
        -1,
        ImmutableArray<MiracleCleanseFollowupSignalKey>.Empty,
        -1);
}

public readonly record struct MiracleCleanseFollowupObservation(
    bool ConfigurationEnabled,
    bool IsCrystallineConflict,
    bool IsLocalCounterJobValid,
    bool HigherPriorityClaimed,
    MiracleCleanseFollowupSignal? NewSignal,
    MiracleCleanseFollowupCandidate? Candidate,
    bool TeamTargetCountKnown,
    int TeamTargetCount,
    long NowMilliseconds,
    bool HardReset = false)
{
    public bool IsWolvesDenTesting { get; init; }

    public bool IsSupportedContext =>
        ReactiveCounterCcProfileRules.IsSupportedContext(
            IsCrystallineConflict,
            IsWolvesDenTesting);
}

public readonly record struct MiracleCleanseFollowupDecision(
    MiracleCleanseFollowupState NextState,
    MiracleCleanseFollowupDecisionKind Kind,
    MiracleCleanseFollowupCancelReason CancelReason,
    MiracleCleanseFollowupIntent? PromotionIntent = null)
{
    public bool ShouldPromote =>
        Kind == MiracleCleanseFollowupDecisionKind.ReadyForPromotion &&
        PromotionIntent is { IsValid: true };

    /// <summary>
    /// The caller must store NextState before promoting this into the existing
    /// Miracle threat dispatcher. A failed promotion or later native call never
    /// re-arms the same exact Purify signal.
    /// </summary>
    public bool RetiresSignalBeforePromotion => ShouldPromote;
}

/// <summary>
/// Pure, opt-in WHM/BRD/NIN follow-up policy:
/// exact enemy self-Purify -> positive live Resilience latch ->
/// one authoritative live absence at/after a validated expected end, or 150ms
/// continuous early/untimed absence -> one bounded promotion into the existing
/// reactive-CC dispatcher. Positive fresh total-team pressure is an optional
/// bonus for simultaneous releases. Known zero and unavailable/stale pressure
/// are neutral peers and always remain eligible for HP/MP/identity fallback.
/// Exact input may bind only inside the 500-ms release edge; once bound it may
/// wait inside the shared 3-second held lease from that original release time.
/// The shared dispatcher owns native range/LoS, protection checks, input
/// consumption, and the sole action call. RemainingTime is only an advisory
/// wake-up hint; live absence is mandatory.
/// </summary>
public static class MiracleCleanseFollowupRules
{
    public const uint PurifyActionId = 29_056;
    public const uint StunStatusId = 1_343;
    public const uint HeavyStatusId = 1_344;
    public const uint BindStatusId = 1_345;
    public const uint SilenceStatusId = 1_347;
    public const uint MiracleOfNatureStatusId = 3_085;
    public const uint DeepFreezeStatusId = 3_219;
    public const uint ResilienceStatusId = 3_248;
    public const byte RecoveredFromStatusEffectType = 0x10;

    public const long ResilienceAcquisitionMilliseconds = 750;
    public const long ResilienceReleaseWaitMilliseconds = 3_000;
    public const long ResilienceMissingGraceMilliseconds = 150;
    public const long ReleaseOpportunityMilliseconds = 500;
    public const long MaximumResilienceRemainingMilliseconds = 2_250;
    public const int MaximumObservedSignals = 128;
    public const int MaximumPendingResolutions = 5;

    public static bool IsExactPurifySignal(
        uint casterEntityId,
        uint actionId,
        uint targetEntityId,
        byte effectType,
        ushort effectValue,
        uint globalSequence,
        ushort sourceSequence) =>
        IsValidEntityId(casterEntityId) &&
        casterEntityId == targetEntityId &&
        actionId == PurifyActionId &&
        ((effectType == 0 && effectValue == 0) ||
         (effectType == RecoveredFromStatusEffectType &&
          IsPurifyRemovableStatus(effectValue))) &&
        (globalSequence != 0 || sourceSequence != 0);

    public static bool IsPurifyRemovableStatus(uint statusId) =>
        statusId is
            StunStatusId or
            HeavyStatusId or
            BindStatusId or
            SilenceStatusId or
            MiracleOfNatureStatusId or
            DeepFreezeStatusId;

    /// <summary>
    /// Terminally deduplicates one already-validated exact Purify packet before
    /// mutable canonical-actor resolution. Only a separately stored copy of
    /// this first packet may retry resolution; a later duplicate stays inert.
    /// </summary>
    public static MiracleCleanseFollowupSignalRetirementDecision RetireValidatedSignal(
        MiracleCleanseFollowupSignalLedger previous,
        MiracleCleanseFollowupSignalKey signal)
    {
        var retired = previous.RetiredSignals.IsDefault
            ? ImmutableArray<MiracleCleanseFollowupSignalKey>.Empty
            : previous.RetiredSignals;
        if (!IsExactPurifySignal(
                signal.CasterEntityId,
                signal.ActionId,
                signal.TargetEntityId,
                signal.EffectType,
                signal.EffectValue,
                signal.GlobalSequence,
                signal.SourceSequence) ||
            retired.Contains(signal))
        {
            return new MiracleCleanseFollowupSignalRetirementDecision(
                new MiracleCleanseFollowupSignalLedger(retired),
                IsNewValidatedSignal: false);
        }

        var skip = Math.Max(0, retired.Length - MaximumObservedSignals + 1);
        return new MiracleCleanseFollowupSignalRetirementDecision(
            new MiracleCleanseFollowupSignalLedger(
                retired.Skip(skip).Append(signal).ToImmutableArray()),
            IsNewValidatedSignal: true);
    }

    /// <summary>
    /// Retries only canonical identity resolution for one already-retired exact
    /// Purify signal. Every gate and the original acquisition deadline are
    /// terminal. Resolution returns the original signal exactly once to a
    /// caller that removes NextPending before lifecycle dispatch.
    /// </summary>
    public static MiracleCleanseFollowupResolutionDecision ResolvePendingSignal(
        MiracleCleanseFollowupPendingResolution pending,
        MiracleCleanseFollowupResolutionObservation observation)
    {
        if (observation.HardReset)
            return RetiredResolution(MiracleCleanseFollowupResolutionRetireReason.HardReset);
        if (!pending.IsValid)
            return RetiredResolution(MiracleCleanseFollowupResolutionRetireReason.InvalidSignal);
        if (!observation.ConfigurationEnabled)
            return RetiredResolution(
                MiracleCleanseFollowupResolutionRetireReason.ConfigurationDisabled);
        if (!observation.IsSupportedContext)
            return RetiredResolution(
                MiracleCleanseFollowupResolutionRetireReason.OutsideCrystallineConflict);
        if (!observation.IsLocalCounterJobValid || observation.LocalCounterJobId == 0)
            return RetiredResolution(
                MiracleCleanseFollowupResolutionRetireReason.LocalCounterJobInvalid);
        if (observation.LocalEntityId != pending.LocalEntityId)
            return RetiredResolution(
                MiracleCleanseFollowupResolutionRetireReason.LocalIdentityChanged);
        if (observation.LocalCounterJobId != pending.LocalCounterJobId)
            return RetiredResolution(
                MiracleCleanseFollowupResolutionRetireReason.LocalCounterJobChanged);
        if (observation.FeatureGeneration != pending.FeatureGeneration)
            return RetiredResolution(
                MiracleCleanseFollowupResolutionRetireReason.FeatureGenerationChanged);
        if (observation.NowMilliseconds < pending.ObservedAtMilliseconds)
            return RetiredResolution(
                MiracleCleanseFollowupResolutionRetireReason.ClockMovedBackwards);
        if (observation.NowMilliseconds - pending.ObservedAtMilliseconds >=
            ResilienceAcquisitionMilliseconds)
        {
            return RetiredResolution(
                MiracleCleanseFollowupResolutionRetireReason.AcquisitionExpired);
        }

        if (observation.UniqueCanonicalTarget is not { } target)
        {
            return new MiracleCleanseFollowupResolutionDecision(
                MiracleCleanseFollowupResolutionDecisionKind.Waiting,
                pending,
                null,
                MiracleCleanseFollowupResolutionRetireReason.None);
        }

        if (!target.IsValid ||
            target.EntityId != pending.Key.CasterEntityId ||
            target.EntityId != pending.Key.TargetEntityId)
        {
            return RetiredResolution(
                MiracleCleanseFollowupResolutionRetireReason.CanonicalIdentityChanged);
        }

        return new MiracleCleanseFollowupResolutionDecision(
            MiracleCleanseFollowupResolutionDecisionKind.Resolved,
            null,
            new MiracleCleanseFollowupSignal(
                pending.Key,
                target,
                pending.ObservedAtMilliseconds),
            MiracleCleanseFollowupResolutionRetireReason.None);
    }

    public static bool IsValidEntityId(uint entityId) =>
        entityId is not 0 and not 0xE0000000 and not uint.MaxValue;

    public static bool IsValidSignalShape(MiracleCleanseFollowupSignal signal) =>
        signal.Target.IsValid &&
        signal.Target.EntityId == signal.Key.TargetEntityId &&
        IsExactPurifySignal(
            signal.Key.CasterEntityId,
            signal.Key.ActionId,
            signal.Key.TargetEntityId,
            signal.Key.EffectType,
            signal.Key.EffectValue,
            signal.Key.GlobalSequence,
            signal.Key.SourceSequence) &&
        signal.ObservedAtMilliseconds >= 0;

    public static MiracleCleanseFollowupDecision Observe(
        MiracleCleanseFollowupState previous,
        MiracleCleanseFollowupObservation observation)
    {
        previous = Normalize(previous);
        if (observation.HardReset)
        {
            return Cancelled(
                MiracleCleanseFollowupState.Initial,
                MiracleCleanseFollowupCancelReason.HardReset);
        }

        if (observation.NowMilliseconds < 0 ||
            (previous.LastObservedAtMilliseconds >= 0 &&
             observation.NowMilliseconds < previous.LastObservedAtMilliseconds))
        {
            return Cancelled(
                MiracleCleanseFollowupState.Initial,
                MiracleCleanseFollowupCancelReason.ClockMovedBackwards);
        }

        var gateFailure = GateFailure(observation);
        if (gateFailure != MiracleCleanseFollowupCancelReason.None)
        {
            return Cancelled(
                StopTracking(previous, observation.NowMilliseconds),
                gateFailure);
        }

        var state = previous with { LastObservedAtMilliseconds = observation.NowMilliseconds };
        var observedNewSignal = false;
        if (observation.NewSignal is { } newSignal)
        {
            // A known packet is inert even if drained after its acquisition
            // deadline. It cannot cancel a newer active lifecycle.
            if (state.ObservedSignals.Contains(newSignal.Key))
            {
                // Duplicate: deliberately ignored.
            }
            else if (!IsValidNewSignal(newSignal, observation.NowMilliseconds))
            {
                return Cancelled(
                    StopTracking(state, observation.NowMilliseconds),
                    MiracleCleanseFollowupCancelReason.InvalidSignal);
            }
            else
            {
                state = state with
                {
                    ObservedSignals = AddBounded(state.ObservedSignals, newSignal.Key),
                };

                // Keep the first exact lifecycle deterministic. The later
                // signal is retired but cannot replace or destroy it.
                if (state.ActiveSignal is not null)
                {
                    return Cancelled(
                        state,
                        MiracleCleanseFollowupCancelReason.ConcurrentSignal);
                }

                state = state with
                {
                    Phase = MiracleCleanseFollowupPhase.WaitingForResilience,
                    ActiveSignal = newSignal,
                    ResiliencePresenceObserved = false,
                    ResilienceObservedAtMilliseconds = -1,
                    ResilienceMissingSinceMilliseconds = -1,
                    ReleasedAtMilliseconds = -1,
                    GameplayKeyToken = 0,
                    ExpectedProtectionEndAtMilliseconds = -1,
                };
                observedNewSignal = true;
            }
        }

        if (state.ActiveSignal is not { } signal)
            return None(state);

        if (ValidateCandidate(signal.Target, observation.Candidate) is { } candidateFailure)
        {
            return Cancelled(
                StopTracking(state, observation.NowMilliseconds),
                candidateFailure);
        }

        var candidate = observation.Candidate!.Value;
        if (candidate.ActiveResilienceStatusCount is < 0 or > 1)
        {
            return Cancelled(
                StopTracking(state, observation.NowMilliseconds),
                MiracleCleanseFollowupCancelReason.ResilienceObservationAmbiguous);
        }

        return state.Phase switch
        {
            MiracleCleanseFollowupPhase.WaitingForResilience => ObserveResilienceAcquisition(
                state,
                candidate,
                observation.NowMilliseconds,
                observedNewSignal),
            MiracleCleanseFollowupPhase.WaitingForResilienceEnd => ObserveResilienceEnd(
                state,
                candidate,
                observation),
            MiracleCleanseFollowupPhase.ReleaseOpportunity => ObserveReleaseOpportunity(
                state,
                candidate,
                observation),
            _ => Cancelled(
                StopTracking(state, observation.NowMilliseconds),
                MiracleCleanseFollowupCancelReason.InvalidSignal),
        };
    }

    private static MiracleCleanseFollowupDecision ObserveResilienceAcquisition(
        MiracleCleanseFollowupState state,
        MiracleCleanseFollowupCandidate candidate,
        long nowMilliseconds,
        bool observedNewSignal)
    {
        var signal = state.ActiveSignal!.Value;
        var age = nowMilliseconds - signal.ObservedAtMilliseconds;
        if (age < 0)
        {
            return Cancelled(
                StopTracking(state, nowMilliseconds),
                MiracleCleanseFollowupCancelReason.InvalidSignal);
        }

        if (age >= ResilienceAcquisitionMilliseconds)
        {
            return Cancelled(
                StopTracking(state, nowMilliseconds),
                MiracleCleanseFollowupCancelReason.ResilienceNotObserved);
        }

        if (candidate.ActiveResilienceStatusCount == 0)
        {
            return new MiracleCleanseFollowupDecision(
                state,
                observedNewSignal
                    ? MiracleCleanseFollowupDecisionKind.SignalObserved
                    : MiracleCleanseFollowupDecisionKind.Waiting,
                MiracleCleanseFollowupCancelReason.None);
        }

        return new MiracleCleanseFollowupDecision(
            state with
            {
                Phase = MiracleCleanseFollowupPhase.WaitingForResilienceEnd,
                ResiliencePresenceObserved = true,
                ResilienceObservedAtMilliseconds = nowMilliseconds,
                ResilienceMissingSinceMilliseconds = -1,
                ExpectedProtectionEndAtMilliseconds = UpdateExpectedProtectionEnd(
                    state.ExpectedProtectionEndAtMilliseconds,
                    candidate.ResilienceRemainingMilliseconds,
                    nowMilliseconds),
            },
            MiracleCleanseFollowupDecisionKind.ResilienceObserved,
            MiracleCleanseFollowupCancelReason.None);
    }

    private static MiracleCleanseFollowupDecision ObserveResilienceEnd(
        MiracleCleanseFollowupState state,
        MiracleCleanseFollowupCandidate candidate,
        MiracleCleanseFollowupObservation observation)
    {
        if (!state.ResiliencePresenceObserved || state.ResilienceObservedAtMilliseconds < 0)
        {
            return Cancelled(
                StopTracking(state, observation.NowMilliseconds),
                MiracleCleanseFollowupCancelReason.InvalidSignal);
        }

        var age = observation.NowMilliseconds - state.ResilienceObservedAtMilliseconds;
        if (age < 0)
        {
            return Cancelled(
                StopTracking(state, observation.NowMilliseconds),
                MiracleCleanseFollowupCancelReason.ClockMovedBackwards);
        }

        if (age >= ResilienceReleaseWaitMilliseconds)
        {
            return Cancelled(
                StopTracking(state, observation.NowMilliseconds),
                MiracleCleanseFollowupCancelReason.ResilienceReleaseTimedOut);
        }

        if (candidate.ActiveResilienceStatusCount == 1)
        {
            // The same status ID returning inside the grace is conservatively
            // treated as uninterrupted presence, irrespective of slot movement.
            return Waiting(state with
            {
                ResilienceMissingSinceMilliseconds = -1,
                ExpectedProtectionEndAtMilliseconds = UpdateExpectedProtectionEnd(
                    state.ExpectedProtectionEndAtMilliseconds,
                    candidate.ResilienceRemainingMilliseconds,
                    observation.NowMilliseconds),
            });
        }

        if (state.ExpectedProtectionEndAtMilliseconds > 0 &&
            observation.NowMilliseconds >= state.ExpectedProtectionEndAtMilliseconds)
        {
            var predictedRelease = state with
            {
                Phase = MiracleCleanseFollowupPhase.ReleaseOpportunity,
                ResilienceMissingSinceMilliseconds = observation.NowMilliseconds,
                ReleasedAtMilliseconds = observation.NowMilliseconds,
            };
            return ObserveReleaseOpportunity(predictedRelease, candidate, observation);
        }

        if (state.ResilienceMissingSinceMilliseconds < 0)
        {
            return Waiting(state with
            {
                ResilienceMissingSinceMilliseconds = observation.NowMilliseconds,
            });
        }

        var missingAge = observation.NowMilliseconds - state.ResilienceMissingSinceMilliseconds;
        if (missingAge < 0)
        {
            return Cancelled(
                StopTracking(state, observation.NowMilliseconds),
                MiracleCleanseFollowupCancelReason.ClockMovedBackwards);
        }

        if (missingAge < ResilienceMissingGraceMilliseconds)
            return Waiting(state);

        var released = state with
        {
            Phase = MiracleCleanseFollowupPhase.ReleaseOpportunity,
            ReleasedAtMilliseconds = observation.NowMilliseconds,
        };
        return ObserveReleaseOpportunity(released, candidate, observation);
    }

    private static MiracleCleanseFollowupDecision ObserveReleaseOpportunity(
        MiracleCleanseFollowupState state,
        MiracleCleanseFollowupCandidate candidate,
        MiracleCleanseFollowupObservation observation)
    {
        if (candidate.ActiveResilienceStatusCount == 1)
        {
            return Cancelled(
                StopTracking(state, observation.NowMilliseconds),
                MiracleCleanseFollowupCancelReason.ResilienceReturnedAfterRelease);
        }

        var releaseAge = observation.NowMilliseconds - state.ReleasedAtMilliseconds;
        if (releaseAge < 0)
        {
            return Cancelled(
                StopTracking(state, observation.NowMilliseconds),
                MiracleCleanseFollowupCancelReason.ClockMovedBackwards);
        }

        // The unbound release may acquire consent only inside the original
        // 500-ms edge. Once bound, that exact actor/key retains the original
        // ReleasedAt timestamp and may wait behind dispatcher priority for the
        // existing 3-second held lease; this never reopens acquisition.
        var releaseLifetime = state.GameplayKeyToken > 0
            ? MiracleProtectionEndRules.HeldLeaseMilliseconds
            : ReleaseOpportunityMilliseconds;
        if (releaseAge >= releaseLifetime)
        {
            return Cancelled(
                StopTracking(state, observation.NowMilliseconds),
                MiracleCleanseFollowupCancelReason.ReleaseOpportunityExpired);
        }

        // Purify is the remembered enemy episode. Consent is intentionally
        // sampled only when Resilience is authoritatively absent, so ordinary
        // W/A/S/D handoffs during the protection duration cannot kill it. Once
        // acquired, the exact generation remains frozen through dispatch.
        if (state.GameplayKeyToken > 0)
        {
            if (!candidate.ReservedGameplayKeyPhysicallyDown)
            {
                return Cancelled(
                    StopTracking(state, observation.NowMilliseconds),
                    MiracleCleanseFollowupCancelReason.ReservationKeyReleased);
            }
        }
        else if (candidate.ReservationGameplayKeyToken > 0 &&
                 candidate.ReservedGameplayKeyPhysicallyDown)
        {
            state = state with
            {
                GameplayKeyToken = candidate.ReservationGameplayKeyToken,
            };
        }

        // Immediate MCH/SAM/VPR events keep priority without destroying this
        // exact bound lease. The original release timestamp remains authoritative.
        if (observation.HigherPriorityClaimed)
            return Waiting(state);

        // Freeze the exact actor/key at the authoritative Resilience end even
        // when range/LoS is temporarily unavailable. The shared dispatcher
        // revalidates and waits inside the bounded lease; promotion no longer
        // has to land in the same 500-ms window as a cast or movement frame.
        if (state.GameplayKeyToken <= 0)
            return Waiting(state);

        if (state.ActiveSignal is not { } signal)
        {
            return Cancelled(
                StopTracking(state, observation.NowMilliseconds),
                MiracleCleanseFollowupCancelReason.InvalidSignal);
        }

        var intent = new MiracleCleanseFollowupIntent(
            signal,
            state.ReleasedAtMilliseconds)
        {
            GameplayKeyToken = state.GameplayKeyToken,
            ExpectedProtectionEndAtMilliseconds = state.ExpectedProtectionEndAtMilliseconds,
        };
        if (!intent.IsValid)
        {
            return Cancelled(
                StopTracking(state, observation.NowMilliseconds),
                MiracleCleanseFollowupCancelReason.InvalidSignal);
        }

        return new MiracleCleanseFollowupDecision(
            StopTracking(state, observation.NowMilliseconds),
            MiracleCleanseFollowupDecisionKind.ReadyForPromotion,
            MiracleCleanseFollowupCancelReason.None,
            intent);
    }

    private static MiracleCleanseFollowupCancelReason? ValidateCandidate(
        MiracleCleanseFollowupTargetIdentity expected,
        MiracleCleanseFollowupCandidate? candidate)
    {
        if (candidate is not { } value ||
            !value.Target.IsValid ||
            !value.IsExactCanonicalEnemy ||
            !value.IsAliveAndTargetable)
        {
            return MiracleCleanseFollowupCancelReason.CandidateIdentityInvalid;
        }

        return value.Target != expected
            ? MiracleCleanseFollowupCancelReason.CandidateChanged
            : null;
    }

    private static MiracleCleanseFollowupResolutionDecision RetiredResolution(
        MiracleCleanseFollowupResolutionRetireReason reason) =>
        new(
            MiracleCleanseFollowupResolutionDecisionKind.Retired,
            null,
            null,
            reason);

    private static bool IsValidNewSignal(
        MiracleCleanseFollowupSignal signal,
        long nowMilliseconds) =>
        IsValidSignalShape(signal) &&
        signal.ObservedAtMilliseconds <= nowMilliseconds &&
        nowMilliseconds - signal.ObservedAtMilliseconds < ResilienceAcquisitionMilliseconds;

    private static MiracleCleanseFollowupCancelReason GateFailure(
        MiracleCleanseFollowupObservation observation)
    {
        if (!observation.ConfigurationEnabled)
            return MiracleCleanseFollowupCancelReason.ConfigurationDisabled;
        if (!observation.IsSupportedContext)
            return MiracleCleanseFollowupCancelReason.OutsideCrystallineConflict;
        if (!observation.IsLocalCounterJobValid)
            return MiracleCleanseFollowupCancelReason.LocalCounterJobInvalid;
        return MiracleCleanseFollowupCancelReason.None;
    }

    private static MiracleCleanseFollowupState StopTracking(
        MiracleCleanseFollowupState state,
        long nowMilliseconds) =>
        state with
        {
            Phase = MiracleCleanseFollowupPhase.WaitingForSignal,
            ActiveSignal = null,
            ResiliencePresenceObserved = false,
            ResilienceObservedAtMilliseconds = -1,
            ResilienceMissingSinceMilliseconds = -1,
            ReleasedAtMilliseconds = -1,
            GameplayKeyToken = 0,
            ExpectedProtectionEndAtMilliseconds = -1,
            LastObservedAtMilliseconds = nowMilliseconds,
        };

    private static long UpdateExpectedProtectionEnd(
        long currentExpectedEndMilliseconds,
        long remainingMilliseconds,
        long nowMilliseconds)
    {
        if (remainingMilliseconds <= 0 ||
            remainingMilliseconds > MaximumResilienceRemainingMilliseconds ||
            nowMilliseconds < 0)
        {
            return currentExpectedEndMilliseconds;
        }

        var observedEnd = nowMilliseconds > long.MaxValue - remainingMilliseconds
            ? long.MaxValue
            : nowMilliseconds + remainingMilliseconds;
        return currentExpectedEndMilliseconds > 0
            ? Math.Min(currentExpectedEndMilliseconds, observedEnd)
            : observedEnd;
    }

    private static ImmutableArray<MiracleCleanseFollowupSignalKey> AddBounded(
        ImmutableArray<MiracleCleanseFollowupSignalKey> signals,
        MiracleCleanseFollowupSignalKey signal)
    {
        signals = signals.Add(signal);
        return signals.Length <= MaximumObservedSignals
            ? signals
            : signals.RemoveRange(0, signals.Length - MaximumObservedSignals);
    }

    private static MiracleCleanseFollowupState Normalize(MiracleCleanseFollowupState state) =>
        state.ObservedSignals.IsDefault
            ? state with { ObservedSignals = ImmutableArray<MiracleCleanseFollowupSignalKey>.Empty }
            : state;

    private static MiracleCleanseFollowupDecision None(MiracleCleanseFollowupState state) =>
        new(
            state,
            MiracleCleanseFollowupDecisionKind.None,
            MiracleCleanseFollowupCancelReason.None);

    private static MiracleCleanseFollowupDecision Waiting(MiracleCleanseFollowupState state) =>
        new(
            state,
            MiracleCleanseFollowupDecisionKind.Waiting,
            MiracleCleanseFollowupCancelReason.None);

    private static MiracleCleanseFollowupDecision Cancelled(
        MiracleCleanseFollowupState state,
        MiracleCleanseFollowupCancelReason reason) =>
        new(state, MiracleCleanseFollowupDecisionKind.Cancelled, reason);
}
