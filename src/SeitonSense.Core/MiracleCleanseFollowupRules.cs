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
    LocalWhiteMageInvalid = 3,
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
}

/// <summary>
/// One exact server ActionEffect result proving that an enemy used Purify on
/// itself and recovered from Stun. Sequence identity makes duplicate packets
/// harmless and is retained before any later native Miracle attempt.
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

/// <summary>
/// Exact no-retry action intent. The Purify ActionEffect signal, not a mutable
/// StatusList slot or remaining-time estimate, is its identity.
/// </summary>
public readonly record struct MiracleCleanseFollowupIntent(
    MiracleCleanseFollowupSignal Signal,
    long ReleasedAtMilliseconds)
{
    public MiracleCleanseFollowupTargetIdentity Target => Signal.Target;

    public bool IsValid =>
        MiracleCleanseFollowupRules.IsValidSignalShape(Signal) &&
        ReleasedAtMilliseconds >= Signal.ObservedAtMilliseconds;
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
    int ActiveResilienceStatusCount);

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
    bool IsLocalWhiteMageValid,
    bool HigherPriorityClaimed,
    MiracleCleanseFollowupSignal? NewSignal,
    MiracleCleanseFollowupCandidate? Candidate,
    long NowMilliseconds,
    bool HardReset = false);

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
/// Pure, opt-in WHM follow-up policy:
/// exact enemy self-Purify recovering Stun -> positive live Resilience latch ->
/// 150ms continuous live absence -> one bounded promotion into the existing
/// Miracle dispatcher. That shared dispatcher owns fresh/held input, native
/// range/LoS, protection checks, input consumption, and the sole action call.
/// RemainingTime is deliberately absent; release is never predicted.
/// </summary>
public static class MiracleCleanseFollowupRules
{
    public const uint PurifyActionId = 29_056;
    public const uint StunStatusId = 1_343;
    public const uint ResilienceStatusId = 3_248;
    public const byte RecoveredFromStatusEffectType = 0x10;

    public const long ResilienceAcquisitionMilliseconds = 750;
    public const long ResilienceReleaseWaitMilliseconds = 3_000;
    public const long ResilienceMissingGraceMilliseconds = 150;
    public const long ReleaseOpportunityMilliseconds = 500;
    public const int MaximumObservedSignals = 128;

    public static bool IsExactStunPurifySignal(
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
        effectType == RecoveredFromStatusEffectType &&
        effectValue == StunStatusId &&
        (globalSequence != 0 || sourceSequence != 0);

    public static bool IsValidEntityId(uint entityId) =>
        entityId is not 0 and not 0xE0000000 and not uint.MaxValue;

    public static bool IsValidSignalShape(MiracleCleanseFollowupSignal signal) =>
        signal.Target.IsValid &&
        signal.Target.EntityId == signal.Key.TargetEntityId &&
        IsExactStunPurifySignal(
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
            return Waiting(state with { ResilienceMissingSinceMilliseconds = -1 });
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

        if (releaseAge >= ReleaseOpportunityMilliseconds)
        {
            return Cancelled(
                StopTracking(state, observation.NowMilliseconds),
                MiracleCleanseFollowupCancelReason.ReleaseOpportunityExpired);
        }

        // Immediate MCH/SAM/VPR events keep priority without destroying this
        // opportunity. Promotion remains bounded by the original release edge.
        if (observation.HigherPriorityClaimed)
            return Waiting(state);

        if (state.ActiveSignal is not { } signal)
        {
            return Cancelled(
                StopTracking(state, observation.NowMilliseconds),
                MiracleCleanseFollowupCancelReason.InvalidSignal);
        }

        var intent = new MiracleCleanseFollowupIntent(
            signal,
            state.ReleasedAtMilliseconds);
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
        if (!observation.IsCrystallineConflict)
            return MiracleCleanseFollowupCancelReason.OutsideCrystallineConflict;
        if (!observation.IsLocalWhiteMageValid)
            return MiracleCleanseFollowupCancelReason.LocalWhiteMageInvalid;
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
            LastObservedAtMilliseconds = nowMilliseconds,
        };

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
