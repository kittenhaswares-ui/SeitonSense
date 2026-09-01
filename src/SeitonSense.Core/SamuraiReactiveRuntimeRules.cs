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

/// <summary>
/// Pre-selection collection state for automatic Zantetsuken. The first exact
/// own-source Kuzushi observation starts one fixed window; no target is frozen
/// until that window is ready.
/// </summary>
public readonly record struct SamuraiZantetsukenCollectionState(
    SupportedPvPContext Context,
    TargetPressureActorIdentity LocalPlayer,
    long FirstExactOwnSourceKuzushiAtMilliseconds)
{
    public static SamuraiZantetsukenCollectionState Initial => new(
        SupportedPvPContext.None,
        default,
        -1);

    public bool IsCollecting =>
        Context is (SupportedPvPContext.CrystallineConflict or
            SupportedPvPContext.WolvesDen) &&
        LocalPlayer.IsValid &&
        FirstExactOwnSourceKuzushiAtMilliseconds >= 0;
}

public readonly record struct SamuraiZantetsukenCollectionObservation(
    bool Enabled,
    bool HardReset,
    SupportedPvPContext Context,
    TargetPressureActorIdentity LocalPlayer,
    bool ExactOwnSourceKuzushiPresent,
    long NowMilliseconds);

public readonly record struct SamuraiZantetsukenCollectionDecision(
    SamuraiZantetsukenCollectionState NextState,
    bool CanSelectAndFreezeTarget);

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
    long ArmedAtMilliseconds,
    bool AllowJoblessWolvesDenTarget = false)
{
    public static SamuraiZantetsukenState Initial => new(
        SamuraiZantetsukenPhase.Waiting,
        default,
        -1,
        false);

    public bool IsActive =>
        Phase == SamuraiZantetsukenPhase.Armed &&
        (Target.IsValid ||
         (AllowJoblessWolvesDenTarget && Target.HasValidActorIdentity)) &&
        ArmedAtMilliseconds >= 0;
}

public readonly record struct SamuraiZantetsukenObservation(
    bool Enabled,
    bool HardReset,
    bool ExactTargetStillCurrent,
    bool TargetAliveAndTargetable,
    bool ExactOwnSourceKuzushiPresent,
    int ExecuteBlockingProtectionCount,
    bool BoundPresent,
    bool ZantetsukenReady,
    bool HasNativeRangeAndLineOfSight);

public readonly record struct SamuraiZantetsukenDecision(
    SamuraiZantetsukenState NextState,
    SamuraiZantetsukenDecisionKind Kind,
    uint ActionId);

/// <summary>
/// One frozen automatic target and one native Zantetsuken boundary. The frozen
/// primary target must retain exact own-source Kuzushi. Exact Covered, Hallowed
/// Ground, or Undead Redemption presence cancels without a fallback at that
/// boundary; Guard and Chiten deliberately remain eligible.
/// </summary>
public static class SamuraiZantetsukenRules
{
    public const uint ActionId = 29_537;
    public const uint KuzushiStatusId = 3_202;
    public const float MaximumRangeYalms = 20f;
    public const long CollectionDelayMilliseconds = 1_500;

    /// <summary>
    /// Live Kuzushi ownership evidence. A status slot which is already expired
    /// or carries a non-finite timer is not a current proc even if the client
    /// has not removed row 3202 from the actor yet.
    /// </summary>
    public static bool IsExactCurrentOwnSourceKuzushi(
        uint statusId,
        uint sourceEntityId,
        float remainingSeconds,
        uint localSamuraiEntityId) =>
        statusId == KuzushiStatusId &&
        localSamuraiEntityId is not 0 and not 0xE0000000 and not uint.MaxValue &&
        sourceEntityId == localSamuraiEntityId &&
        float.IsFinite(remainingSeconds) &&
        remainingSeconds > 0f;

    /// <summary>
    /// Waits a fixed 1.5 seconds after the first current exact own-source
    /// Kuzushi evidence before allowing target ranking. A temporary evidence
    /// gap cannot restart the window; missing current evidence at maturity,
    /// feature reset, identity/context drift, or invalid time resets it. Target
    /// selection and freezing happen only after this gate opens, so later
    /// Kuzushi applications can join the current cluster.
    /// </summary>
    public static SamuraiZantetsukenCollectionDecision ObserveCollection(
        SamuraiZantetsukenCollectionState state,
        SamuraiZantetsukenCollectionObservation observation)
    {
        if (!observation.Enabled ||
            observation.HardReset ||
            observation.Context is not
                (SupportedPvPContext.CrystallineConflict or
                    SupportedPvPContext.WolvesDen) ||
            !observation.LocalPlayer.IsValid ||
            observation.NowMilliseconds < 0 ||
            (state != SamuraiZantetsukenCollectionState.Initial &&
             (!state.IsCollecting ||
              state.Context != observation.Context ||
              state.LocalPlayer != observation.LocalPlayer ||
              state.FirstExactOwnSourceKuzushiAtMilliseconds >
                  observation.NowMilliseconds)))
        {
            return ResetCollection();
        }

        if (state == SamuraiZantetsukenCollectionState.Initial)
        {
            if (!observation.ExactOwnSourceKuzushiPresent)
                return ResetCollection();

            return new SamuraiZantetsukenCollectionDecision(
                new SamuraiZantetsukenCollectionState(
                    observation.Context,
                    observation.LocalPlayer,
                    observation.NowMilliseconds),
                false);
        }

        if (!HasCollectionDelayElapsed(state, observation.NowMilliseconds))
        {
            // A transient status-list gap must not move the first-evidence
            // timestamp or shorten/restart the original collection window.
            return new SamuraiZantetsukenCollectionDecision(state, false);
        }

        if (!CanSelectAndFreezeTarget(state, observation))
            return ResetCollection();

        return new SamuraiZantetsukenCollectionDecision(
            state,
            true);
    }

    /// <summary>
    /// Pure monotonic-time gate shared by collection and the final dispatch
    /// boundary. Equality at exactly 1,500 ms is intentionally eligible.
    /// </summary>
    public static bool HasCollectionDelayElapsed(
        SamuraiZantetsukenCollectionState state,
        long nowMilliseconds) =>
        state.IsCollecting &&
        nowMilliseconds >=
            state.FirstExactOwnSourceKuzushiAtMilliseconds &&
        nowMilliseconds - state.FirstExactOwnSourceKuzushiAtMilliseconds >=
            CollectionDelayMilliseconds;

    /// <summary>
    /// Final collection gate. Callers must still rank a current candidate and
    /// apply the frozen target's existing Kuzushi, protection, and reachability
    /// checks before the native action boundary.
    /// </summary>
    public static bool CanSelectAndFreezeTarget(
        SamuraiZantetsukenCollectionState state,
        SamuraiZantetsukenCollectionObservation observation) =>
        observation.Enabled &&
        !observation.HardReset &&
        observation.Context is (SupportedPvPContext.CrystallineConflict or
            SupportedPvPContext.WolvesDen) &&
        observation.LocalPlayer.IsValid &&
        state.Context == observation.Context &&
        state.LocalPlayer == observation.LocalPlayer &&
        observation.ExactOwnSourceKuzushiPresent &&
        HasCollectionDelayElapsed(state, observation.NowMilliseconds);

    public static SamuraiZantetsukenState Arm(
        SamuraiReactiveCounterCcTarget target,
        long nowMilliseconds,
        bool allowJoblessWolvesDenTarget = false) =>
        (target.IsValid ||
         (allowJoblessWolvesDenTarget && target.HasValidActorIdentity)) &&
        nowMilliseconds >= 0
            ? new SamuraiZantetsukenState(
                SamuraiZantetsukenPhase.Armed,
                target,
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
            !observation.ExactOwnSourceKuzushiPresent ||
            observation.ExecuteBlockingProtectionCount != 0 ||
            !observation.HasNativeRangeAndLineOfSight)
        {
            return Cancelled();
        }

        return !observation.BoundPresent &&
               observation.ZantetsukenReady
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

    private static SamuraiZantetsukenCollectionDecision ResetCollection() =>
        new(SamuraiZantetsukenCollectionState.Initial, false);
}
