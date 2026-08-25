namespace SeitonSense.Core;

public enum SamuraiReactiveProtectionKind : byte
{
    None = 0,
    PurifyResilience = 1,
    Guard = 2,
}

public readonly record struct SamuraiReactiveProtectionSignal(
    SamuraiReactiveProtectionKind Kind,
    long ObservedAtMilliseconds,
    uint CasterEntityId,
    uint TargetEntityId,
    uint ActionId,
    byte TargetCount,
    uint GlobalSequence,
    ushort SourceSequence)
{
    public bool IsValid =>
        Kind != SamuraiReactiveProtectionKind.None &&
        ObservedAtMilliseconds >= 0 &&
        Kind == SamuraiReactiveRuntimeRules.ClassifyExactProtectionSignal(
            ActionId,
            CasterEntityId,
            TargetEntityId,
            TargetCount,
            GlobalSequence,
            SourceSequence);
}

public readonly record struct SamuraiReactiveActionEffectSignal(
    long ObservedAtMilliseconds,
    uint CasterEntityId,
    uint TargetEntityId,
    uint ActionId,
    uint GlobalSequence,
    ushort SourceSequence)
{
    public bool IsValid =>
        ObservedAtMilliseconds >= 0 &&
        MiracleInterceptConfirmationRules.IsValidEntityId(CasterEntityId) &&
        MiracleInterceptConfirmationRules.IsValidEntityId(TargetEntityId) &&
        CasterEntityId != TargetEntityId &&
        ActionId is SamuraiReactiveCounterCcRules.SotenActionId or
            SamuraiReactiveCounterCcRules.MineuchiActionId &&
        SourceSequence != 0;
}

public static class SamuraiReactiveRuntimeRules
{
    public const uint GuardActionId = 29_054;
    public const uint PurifyActionId = 29_056;
    public const uint GuardStatusId = 3_054;
    public const uint GuardAlternateStatusId = 3_673;
    public const uint ResilienceStatusId = 3_248;
    public const long SignalStatusObservationLeaseMilliseconds = 1_000;
    public const long PurifyEpisodeLeaseMilliseconds = 5_000;
    public const long GuardEpisodeLeaseMilliseconds = 7_000;
    public const long SotenArrivalLeaseMilliseconds = 1_500;

    public static SamuraiReactiveProtectionKind ClassifyExactProtectionSignal(
        uint actionId,
        uint casterEntityId,
        uint targetEntityId,
        byte targetCount,
        uint globalSequence,
        ushort sourceSequence)
    {
        if (!MiracleInterceptConfirmationRules.IsValidEntityId(casterEntityId) ||
            casterEntityId != targetEntityId ||
            targetCount != 1 ||
            (globalSequence == 0 && sourceSequence == 0))
        {
            return SamuraiReactiveProtectionKind.None;
        }

        return actionId switch
        {
            PurifyActionId => SamuraiReactiveProtectionKind.PurifyResilience,
            GuardActionId => SamuraiReactiveProtectionKind.Guard,
            _ => SamuraiReactiveProtectionKind.None,
        };
    }

    public static uint ExpectedActionId(SamuraiReactiveProtectionKind kind) => kind switch
    {
        SamuraiReactiveProtectionKind.PurifyResilience => PurifyActionId,
        SamuraiReactiveProtectionKind.Guard => GuardActionId,
        _ => 0,
    };

    public static bool IsExpectedProtectionStatus(
        SamuraiReactiveProtectionKind kind,
        uint statusId) => kind switch
        {
            SamuraiReactiveProtectionKind.PurifyResilience =>
                statusId == ResilienceStatusId,
            SamuraiReactiveProtectionKind.Guard =>
                statusId is GuardStatusId or GuardAlternateStatusId,
            _ => false,
        };

    public static bool IsExactWolvesDenCurrentTarget(
        uint localEntityId,
        uint signalCasterEntityId,
        uint currentTargetEntityId) =>
        MiracleInterceptConfirmationRules.IsValidEntityId(localEntityId) &&
        MiracleInterceptConfirmationRules.IsValidEntityId(signalCasterEntityId) &&
        MiracleInterceptConfirmationRules.IsValidEntityId(currentTargetEntityId) &&
        signalCasterEntityId != localEntityId &&
        currentTargetEntityId == signalCasterEntityId;

    public static long EpisodeLeaseMilliseconds(
        SamuraiReactiveProtectionKind kind) => kind switch
        {
            SamuraiReactiveProtectionKind.PurifyResilience =>
                PurifyEpisodeLeaseMilliseconds,
            SamuraiReactiveProtectionKind.Guard => GuardEpisodeLeaseMilliseconds,
            _ => 0,
        };

    public static bool IsInsideLease(
        long observedAtMilliseconds,
        long nowMilliseconds,
        long leaseMilliseconds) =>
        observedAtMilliseconds >= 0 &&
        nowMilliseconds >= observedAtMilliseconds &&
        leaseMilliseconds > 0 &&
        nowMilliseconds - observedAtMilliseconds <= leaseMilliseconds;

    public static bool CanRegisterExactTimingAttempt(
        long attemptedAtMilliseconds,
        long registrationNowMilliseconds) =>
        attemptedAtMilliseconds >= 0 &&
        registrationNowMilliseconds >= attemptedAtMilliseconds;
}

public enum SamuraiZantetsukenPhase : byte
{
    Waiting = 0,
    Armed = 1,
    Spent = 2,
}

public enum SamuraiZantetsukenDecisionKind : byte
{
    None = 0,
    Waiting = 1,
    Attempt = 2,
    Cancelled = 3,
}

public readonly record struct SamuraiZantetsukenState(
    SamuraiZantetsukenPhase Phase,
    SamuraiReactiveCounterCcTarget Target,
    int GameplayKeyToken,
    long ArmedAtMilliseconds,
    bool AllowJoblessWolvesDenTarget = false)
{
    public static SamuraiZantetsukenState Initial => new(
        SamuraiZantetsukenPhase.Waiting,
        default,
        0,
        -1,
        false);

    public bool IsActive =>
        Phase == SamuraiZantetsukenPhase.Armed &&
        (Target.IsValid ||
         (AllowJoblessWolvesDenTarget && Target.HasValidActorIdentity)) &&
        GameplayKeyToken > 0 &&
        ArmedAtMilliseconds >= 0;
}

public readonly record struct SamuraiZantetsukenObservation(
    bool Enabled,
    bool HardReset,
    bool ExactTargetStillCurrent,
    bool TargetAliveAndTargetable,
    bool ExactGameplayKeyStillDown,
    int OwnSourceKuzushiCount,
    byte ShieldPercentage,
    bool BoundPresent,
    bool ZantetsukenReady,
    bool HasNativeRangeAndLineOfSight);

public readonly record struct SamuraiZantetsukenDecision(
    SamuraiZantetsukenState NextState,
    SamuraiZantetsukenDecisionKind Kind,
    uint ActionId);

/// <summary>
/// One frozen target and one native Zantetsuken boundary. Shield presence is
/// authoritative and may clear while Kuzushi remains; target drift, a foreign
/// Kuzushi source, key release, or Kuzushi loss cancels without fallback.
/// </summary>
public static class SamuraiZantetsukenRules
{
    public const uint ActionId = 29_537;
    public const uint KuzushiStatusId = 3_202;
    public const float MaximumRangeYalms = 20f;

    public static SamuraiZantetsukenState Arm(
        SamuraiReactiveCounterCcTarget target,
        int gameplayKeyToken,
        long nowMilliseconds,
        bool allowJoblessWolvesDenTarget = false) =>
        (target.IsValid ||
         (allowJoblessWolvesDenTarget && target.HasValidActorIdentity)) &&
        gameplayKeyToken > 0 &&
        nowMilliseconds >= 0
            ? new SamuraiZantetsukenState(
                SamuraiZantetsukenPhase.Armed,
                target,
                gameplayKeyToken,
                nowMilliseconds,
                allowJoblessWolvesDenTarget)
            : SamuraiZantetsukenState.Initial;

    public static SamuraiZantetsukenDecision Observe(
        SamuraiZantetsukenState state,
        SamuraiZantetsukenObservation observation)
    {
        if (!state.IsActive) return Cancelled();
        if (observation.HardReset ||
            !observation.Enabled ||
            !observation.ExactTargetStillCurrent ||
            !observation.TargetAliveAndTargetable ||
            !observation.ExactGameplayKeyStillDown ||
            observation.OwnSourceKuzushiCount != 1)
        {
            return Cancelled();
        }

        return observation.ShieldPercentage == 0 &&
               !observation.BoundPresent &&
               observation.ZantetsukenReady &&
               observation.HasNativeRangeAndLineOfSight
            ? new SamuraiZantetsukenDecision(
                state,
                SamuraiZantetsukenDecisionKind.Attempt,
                ActionId)
            : new SamuraiZantetsukenDecision(
                state,
                SamuraiZantetsukenDecisionKind.Waiting,
                0);
    }

    public static SamuraiZantetsukenState CompleteAttempt(
        SamuraiZantetsukenState state) =>
        state.IsActive
            ? state with { Phase = SamuraiZantetsukenPhase.Spent }
            : SamuraiZantetsukenState.Initial;

    private static SamuraiZantetsukenDecision Cancelled() => new(
        SamuraiZantetsukenState.Initial,
        SamuraiZantetsukenDecisionKind.Cancelled,
        0);
}
