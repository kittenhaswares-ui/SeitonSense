using System.Collections.Immutable;

namespace SeitonSense.Core;

public enum MiracleInterceptThreatKind
{
    None = 0,
    MarksmanSpite = 1,
    Zantetsuken = 2,
    FuriousBacklash = 3,
    PostPurifyCrowdControl = 4,
    Contradance = 5,
}

public enum MiracleInterceptDecisionKind
{
    None = 0,
    ThreatObserved = 1,
    Waiting = 2,
    Dispatch = 3,
    Cancelled = 4,
}

public enum MiracleInterceptInputTrigger
{
    None = 0,
    FreshKeyPress = 1,
    HeldPhysicalKey = 2,
}

public enum MiracleInterceptCancelReason
{
    None = 0,
    ConfigurationDisabled = 1,
    OutsideCrystallineConflict = 2,
    LocalCounterJobInvalid = 3,
    HigherPriorityClaimed = 4,
    InvalidSignal = 5,
    CandidateIdentityInvalid = 6,
    ThreatExpired = 7,
    ClockMovedBackwards = 8,
    HardReset = 9,
}

public readonly record struct MiracleInterceptSignalKey(
    uint CasterEntityId,
    uint ActionId,
    uint GlobalSequence,
    ushort SourceSequence);

public readonly record struct MiracleInterceptThreat(
    MiracleInterceptThreatKind Kind,
    MiracleInterceptSignalKey Signal,
    ulong TargetGameObjectId,
    uint TargetEntityId,
    uint TargetJobId,
    long ObservedAtMilliseconds);

public readonly record struct MiracleInterceptState(
    MiracleInterceptThreat? ActiveThreat,
    ImmutableArray<MiracleInterceptSignalKey> ObservedSignals,
    long LastObservedAtMilliseconds)
{
    public static MiracleInterceptState Initial => new(null, [], -1);
}

public readonly record struct MiracleInterceptObservation(
    bool ConfigurationEnabled,
    bool IsCrystallineConflict,
    bool IsLocalCounterJobValid,
    bool HigherPriorityClaimed,
    MiracleInterceptThreat? NewThreat,
    bool CandidateIdentityValid,
    bool CandidateAliveAndTargetable,
    bool HasHardenedScales,
    bool HasOtherVerifiedCcProtection,
    bool HasNativeRangeAndLineOfSight,
    bool IsTextInputActive,
    bool FreshKeyPressed,
    bool HeldKeyEligible,
    long NowMilliseconds,
    bool HardReset = false);

public readonly record struct MiracleInterceptDecision(
    MiracleInterceptState NextState,
    MiracleInterceptDecisionKind Kind,
    MiracleInterceptCancelReason CancelReason,
    MiracleInterceptInputTrigger InputTrigger = MiracleInterceptInputTrigger.None)
{
    public bool ShouldDispatch => Kind == MiracleInterceptDecisionKind.Dispatch;
    public bool ShouldConsumeInputGeneration => ShouldDispatch && InputTrigger != MiracleInterceptInputTrigger.None;
}

/// <summary>
/// Pure one-event/one-physical-generation policy for the experimental WHM/BRD
/// reactive-CC helper. A dispatch clears the active event while retaining its
/// bounded signal key, so a false or throwing native call cannot re-arm it.
/// </summary>
public static class MiracleInterceptRules
{
    public const uint MarksmanSpiteActionId = 29_415;
    public const uint ZantetsukenActionId = 29_537;
    public const uint FuriousBacklashActionId = 39_188;
    public const uint ContradanceActionId = 29_432;
    public const uint HardenedScalesStatusId = 4_096;
    public const uint MachinistJobId = 31;
    public const uint SamuraiJobId = 34;
    public const uint ViperJobId = 41;
    public const uint DancerJobId = 38;
    public const long MarksmanSpiteThreatLifetimeMilliseconds = 500;
    public const long ZantetsukenThreatLifetimeMilliseconds = 500;
    public const long FuriousBacklashThreatLifetimeMilliseconds = 250;
    // The exact variation-0 Contradance start marker precedes the variation-2
    // impact by roughly two seconds in the fixed runtime sample. Keep the
    // helper opportunity much shorter: it must react to the start, not trail it.
    public const long ContradanceThreatLifetimeMilliseconds = 750;
    public const int MaximumObservedSignals = 128;

    public static MiracleInterceptThreatKind ClassifyExactStartSignal(
        uint actionId,
        uint casterEntityId,
        uint targetEntityId,
        int targetCount,
        byte firstEffectType,
        bool firstEffectIsCompletelyEmpty,
        bool additionalEffectsAreCompletelyEmpty,
        byte animationVariation = 0)
    {
        if (!IsNetworkEntityId(casterEntityId) ||
            !IsNetworkEntityId(targetEntityId) ||
            targetCount != 1 ||
            !additionalEffectsAreCompletelyEmpty)
        {
            return MiracleInterceptThreatKind.None;
        }

        return actionId switch
        {
            MarksmanSpiteActionId when targetEntityId != casterEntityId &&
                                          firstEffectType == 0x1B =>
                MiracleInterceptThreatKind.MarksmanSpite,
            ZantetsukenActionId when targetEntityId != casterEntityId &&
                                     firstEffectIsCompletelyEmpty =>
                MiracleInterceptThreatKind.Zantetsuken,
            FuriousBacklashActionId when targetEntityId == casterEntityId &&
                                         firstEffectIsCompletelyEmpty =>
                MiracleInterceptThreatKind.FuriousBacklash,
            ContradanceActionId when targetEntityId == casterEntityId &&
                                     animationVariation == 0 &&
                                     firstEffectIsCompletelyEmpty =>
                MiracleInterceptThreatKind.Contradance,
            _ => MiracleInterceptThreatKind.None,
        };
    }

    public static MiracleInterceptDecision Observe(
        MiracleInterceptState previous,
        MiracleInterceptObservation observation)
    {
        previous = Normalize(previous);
        if (observation.HardReset)
            return Cancelled(MiracleInterceptState.Initial, MiracleInterceptCancelReason.HardReset);

        if (observation.NowMilliseconds < 0 ||
            (previous.LastObservedAtMilliseconds >= 0 &&
             observation.NowMilliseconds < previous.LastObservedAtMilliseconds))
        {
            return Cancelled(MiracleInterceptState.Initial, MiracleInterceptCancelReason.ClockMovedBackwards);
        }

        var gateFailure = GateFailure(observation);
        if (gateFailure != MiracleInterceptCancelReason.None)
            return Cancelled(MiracleInterceptState.Initial, gateFailure);

        var state = previous with { LastObservedAtMilliseconds = observation.NowMilliseconds };
        var observedNewThreat = false;
        if (observation.NewThreat is { } newThreat)
        {
            if (!IsValidThreat(newThreat, observation.NowMilliseconds))
                return Cancelled(state with { ActiveThreat = null }, MiracleInterceptCancelReason.InvalidSignal);

            if (!state.ObservedSignals.Contains(newThreat.Signal))
            {
                state = state with
                {
                    ActiveThreat = newThreat,
                    ObservedSignals = AddBounded(state.ObservedSignals, newThreat.Signal),
                };
                observedNewThreat = true;
            }
        }

        if (state.ActiveThreat is not { } threat)
            return None(state);

        var age = observation.NowMilliseconds - threat.ObservedAtMilliseconds;
        if (age < 0)
            return Cancelled(state with { ActiveThreat = null }, MiracleInterceptCancelReason.InvalidSignal);
        if (age >= GetThreatLifetimeMilliseconds(threat.Kind))
            return Cancelled(state with { ActiveThreat = null }, MiracleInterceptCancelReason.ThreatExpired);

        if (!observation.CandidateIdentityValid || !observation.CandidateAliveAndTargetable)
        {
            return Cancelled(
                state with { ActiveThreat = null },
                MiracleInterceptCancelReason.CandidateIdentityInvalid);
        }

        var trigger = ResolveInput(observation);
        var blocked = observation.HasHardenedScales ||
                      observation.HasOtherVerifiedCcProtection ||
                      !observation.HasNativeRangeAndLineOfSight ||
                      observation.IsTextInputActive ||
                      trigger == MiracleInterceptInputTrigger.None;
        if (blocked)
        {
            return new MiracleInterceptDecision(
                state,
                observedNewThreat
                    ? MiracleInterceptDecisionKind.ThreatObserved
                    : MiracleInterceptDecisionKind.Waiting,
                MiracleInterceptCancelReason.None);
        }

        // No prior 'Hardened Scales present' framework sample is required here.
        // The exact 39188 event and first live post-event absence are sufficient:
        // current logs place status removal only milliseconds after that event.
        return new MiracleInterceptDecision(
            state with { ActiveThreat = null },
            MiracleInterceptDecisionKind.Dispatch,
            MiracleInterceptCancelReason.None,
            trigger);
    }

    public static bool IsExpectedJob(MiracleInterceptThreatKind kind, uint jobId) =>
        kind switch
        {
            MiracleInterceptThreatKind.MarksmanSpite => jobId == MachinistJobId,
            MiracleInterceptThreatKind.Zantetsuken => jobId == SamuraiJobId,
            MiracleInterceptThreatKind.FuriousBacklash => jobId == ViperJobId,
            MiracleInterceptThreatKind.Contradance => jobId == DancerJobId,
            _ => false,
        };

    public static long GetThreatLifetimeMilliseconds(MiracleInterceptThreatKind kind) =>
        kind switch
        {
            MiracleInterceptThreatKind.MarksmanSpite => MarksmanSpiteThreatLifetimeMilliseconds,
            MiracleInterceptThreatKind.Zantetsuken => ZantetsukenThreatLifetimeMilliseconds,
            MiracleInterceptThreatKind.FuriousBacklash => FuriousBacklashThreatLifetimeMilliseconds,
            MiracleInterceptThreatKind.Contradance => ContradanceThreatLifetimeMilliseconds,
            _ => 0,
        };

    public static int GetDispatchPriority(MiracleInterceptThreatKind kind) =>
        kind switch
        {
            MiracleInterceptThreatKind.MarksmanSpite or
            MiracleInterceptThreatKind.Zantetsuken or
            MiracleInterceptThreatKind.FuriousBacklash => 3,
            MiracleInterceptThreatKind.Contradance => 2,
            MiracleInterceptThreatKind.PostPurifyCrowdControl => 1,
            _ => 0,
        };

    private static MiracleInterceptCancelReason GateFailure(MiracleInterceptObservation observation)
    {
        if (!observation.ConfigurationEnabled)
            return MiracleInterceptCancelReason.ConfigurationDisabled;
        if (!observation.IsCrystallineConflict)
            return MiracleInterceptCancelReason.OutsideCrystallineConflict;
        if (!observation.IsLocalCounterJobValid)
            return MiracleInterceptCancelReason.LocalCounterJobInvalid;
        if (observation.HigherPriorityClaimed)
            return MiracleInterceptCancelReason.HigherPriorityClaimed;
        return MiracleInterceptCancelReason.None;
    }

    private static MiracleInterceptInputTrigger ResolveInput(MiracleInterceptObservation observation)
    {
        if (observation.IsTextInputActive) return MiracleInterceptInputTrigger.None;
        if (observation.FreshKeyPressed) return MiracleInterceptInputTrigger.FreshKeyPress;
        if (observation.HeldKeyEligible) return MiracleInterceptInputTrigger.HeldPhysicalKey;
        return MiracleInterceptInputTrigger.None;
    }

    private static bool IsValidThreat(MiracleInterceptThreat threat, long nowMilliseconds) =>
        threat.Kind != MiracleInterceptThreatKind.None &&
        IsNetworkEntityId(threat.Signal.CasterEntityId) &&
        threat.Signal.CasterEntityId == threat.TargetEntityId &&
        threat.Signal.ActionId == ActionFor(threat.Kind) &&
        IsExpectedJob(threat.Kind, threat.TargetJobId) &&
        threat.TargetGameObjectId is not 0 and not 0xE0000000UL &&
        threat.ObservedAtMilliseconds >= 0 &&
        threat.ObservedAtMilliseconds <= nowMilliseconds &&
        nowMilliseconds - threat.ObservedAtMilliseconds < GetThreatLifetimeMilliseconds(threat.Kind);

    private static uint ActionFor(MiracleInterceptThreatKind kind) =>
        kind switch
        {
            MiracleInterceptThreatKind.MarksmanSpite => MarksmanSpiteActionId,
            MiracleInterceptThreatKind.Zantetsuken => ZantetsukenActionId,
            MiracleInterceptThreatKind.FuriousBacklash => FuriousBacklashActionId,
            MiracleInterceptThreatKind.Contradance => ContradanceActionId,
            _ => 0,
        };

    private static ImmutableArray<MiracleInterceptSignalKey> AddBounded(
        ImmutableArray<MiracleInterceptSignalKey> signals,
        MiracleInterceptSignalKey signal)
    {
        signals = signals.Add(signal);
        return signals.Length <= MaximumObservedSignals
            ? signals
            : signals.RemoveRange(0, signals.Length - MaximumObservedSignals);
    }

    private static MiracleInterceptState Normalize(MiracleInterceptState state) =>
        state.ObservedSignals.IsDefault
            ? state with { ObservedSignals = [] }
            : state;

    private static bool IsNetworkEntityId(uint entityId) =>
        entityId is not 0 and not 0xE0000000u;

    private static MiracleInterceptDecision None(MiracleInterceptState state) =>
        new(state, MiracleInterceptDecisionKind.None, MiracleInterceptCancelReason.None);

    private static MiracleInterceptDecision Cancelled(
        MiracleInterceptState state,
        MiracleInterceptCancelReason reason) =>
        new(state, MiracleInterceptDecisionKind.Cancelled, reason);
}
