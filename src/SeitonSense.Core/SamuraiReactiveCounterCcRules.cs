namespace SeitonSense.Core;

public enum SamuraiReactiveCounterCcPhase : byte
{
    Waiting = 0,
    Armed = 1,
    ApproachAccepted = 2,
    Spent = 3,
}

public enum SamuraiReactiveCounterCcDecisionKind : byte
{
    None = 0,
    Waiting = 1,
    AttemptSoten = 2,
    AttemptMineuchi = 3,
    Cancelled = 4,
}

public enum SamuraiReactiveCounterCcNativeInvocationKind : byte
{
    None = 0,
    TargetedUseAction = 1,
}

public readonly record struct SamuraiReactiveCounterCcTarget(
    ulong GameObjectId,
    uint EntityId,
    uint JobId)
{
    public bool HasValidActorIdentity =>
        TargetHighlightRules.IsValidGameObjectId(GameObjectId) &&
        MiracleInterceptConfirmationRules.IsValidEntityId(EntityId);

    public bool IsValid =>
        HasValidActorIdentity &&
        JobId != 0;
}

public readonly record struct SamuraiReactiveCounterCcState(
    SamuraiReactiveCounterCcPhase Phase,
    SamuraiReactiveCounterCcTarget Target,
    int GameplayKeyToken,
    long ArmedAtMilliseconds,
    bool AllowJoblessWolvesDenTarget = false)
{
    public static SamuraiReactiveCounterCcState Initial => new(
        SamuraiReactiveCounterCcPhase.Waiting,
        default,
        0,
        -1,
        false);

    public bool IsActive =>
        (Phase is SamuraiReactiveCounterCcPhase.Armed or
            SamuraiReactiveCounterCcPhase.ApproachAccepted) &&
        (Target.IsValid ||
         (AllowJoblessWolvesDenTarget && Target.HasValidActorIdentity)) &&
        GameplayKeyToken > 0 &&
        ArmedAtMilliseconds >= 0;
}

public readonly record struct SamuraiReactiveCounterCcObservation(
    bool Enabled,
    bool HardReset,
    bool ExactTargetStillCurrent,
    bool TargetAliveAndTargetable,
    bool ExactGameplayKeyStillDown,
    bool ProtectionPresent,
    bool DistanceKnown,
    float TargetEdgeDistanceYalms,
    bool SotenReady,
    bool MineuchiReady,
    bool BoundPresent,
    bool SotenApproachWindowOpen,
    float ConfiguredSotenMaximumRangeYalms,
    bool MineuchiImpactWindowOpen = false);

public readonly record struct SamuraiReactiveCounterCcDecision(
    SamuraiReactiveCounterCcState NextState,
    SamuraiReactiveCounterCcDecisionKind Kind,
    uint ActionId);

/// <summary>
/// Pure staged policy for Soten -> Mineuchi. The caller owns the reviewed
/// direct-target native boundary and decides when measured Soten/Mineuchi
/// impact windows are open. These rules deliberately contain no guessed travel
/// or animation timing and never select a replacement actor.
/// </summary>
public static class SamuraiReactiveCounterCcRules
{
    public const uint SamuraiJobId = 34;
    public const uint SotenActionId = 29_532;
    public const uint MineuchiActionId =
        MiracleInterceptConfirmationRules.MineuchiActionId;
    public const float MineuchiMaximumRangeYalms = 5f;
    public const float SotenMaximumRangeYalms = 20f;

    public static SamuraiReactiveCounterCcNativeInvocationKind
        GetNativeInvocationKind(uint actionId) => actionId switch
        {
            SotenActionId or MineuchiActionId =>
                SamuraiReactiveCounterCcNativeInvocationKind.TargetedUseAction,
            _ => SamuraiReactiveCounterCcNativeInvocationKind.None,
        };

    public static bool CanAcquireProtectionEndConsent(
        bool protectionObserved,
        bool protectionPresent,
        int currentGameplayKeyToken) =>
        protectionObserved &&
        !protectionPresent &&
        currentGameplayKeyToken > 0;

    public static SamuraiReactiveCounterCcState Arm(
        SamuraiReactiveCounterCcTarget target,
        int gameplayKeyToken,
        long nowMilliseconds,
        bool allowJoblessWolvesDenTarget = false) =>
        (target.IsValid ||
         (allowJoblessWolvesDenTarget && target.HasValidActorIdentity)) &&
        gameplayKeyToken > 0 &&
        nowMilliseconds >= 0
            ? new SamuraiReactiveCounterCcState(
                SamuraiReactiveCounterCcPhase.Armed,
                target,
                gameplayKeyToken,
                nowMilliseconds,
                allowJoblessWolvesDenTarget)
            : SamuraiReactiveCounterCcState.Initial;

    public static SamuraiReactiveCounterCcState RebindUncommittedHeldConsent(
        SamuraiReactiveCounterCcState state,
        int currentGameplayKeyToken) =>
        state.IsActive &&
        state.Phase == SamuraiReactiveCounterCcPhase.Armed &&
        currentGameplayKeyToken > 0
            ? state with { GameplayKeyToken = currentGameplayKeyToken }
            : state;

    public static SamuraiReactiveCounterCcDecision Observe(
        SamuraiReactiveCounterCcState state,
        SamuraiReactiveCounterCcObservation observation)
    {
        if (!state.IsActive) return Cancelled();
        if (observation.HardReset ||
            !observation.Enabled ||
            !observation.ExactTargetStillCurrent ||
            !observation.TargetAliveAndTargetable ||
            (state.Phase != SamuraiReactiveCounterCcPhase.ApproachAccepted &&
             !observation.ExactGameplayKeyStillDown) ||
            !observation.DistanceKnown ||
            !float.IsFinite(observation.TargetEdgeDistanceYalms) ||
            observation.TargetEdgeDistanceYalms < 0f)
        {
            return Cancelled();
        }

        if (observation.TargetEdgeDistanceYalms <= MineuchiMaximumRangeYalms)
        {
            return (!observation.ProtectionPresent ||
                    observation.MineuchiImpactWindowOpen) &&
                   observation.MineuchiReady
                ? new SamuraiReactiveCounterCcDecision(
                    state,
                    SamuraiReactiveCounterCcDecisionKind.AttemptMineuchi,
                    MineuchiActionId)
                : Waiting(state);
        }

        if (state.Phase == SamuraiReactiveCounterCcPhase.ApproachAccepted)
            return Waiting(state);

        var maximumSotenRange = NormalizeSotenMaximumRangeYalms(
            observation.ConfiguredSotenMaximumRangeYalms);
        return observation.SotenApproachWindowOpen &&
               observation.SotenReady &&
               !observation.BoundPresent &&
               observation.TargetEdgeDistanceYalms <= maximumSotenRange
            ? new SamuraiReactiveCounterCcDecision(
                state,
                SamuraiReactiveCounterCcDecisionKind.AttemptSoten,
                SotenActionId)
            : Waiting(state);
    }

    public static SamuraiReactiveCounterCcState CompleteAttempt(
        SamuraiReactiveCounterCcState state,
        uint actionId,
        ClientActionAttemptOutcome outcome)
    {
        if (!state.IsActive) return SamuraiReactiveCounterCcState.Initial;
        if (actionId == SotenActionId &&
            state.Phase == SamuraiReactiveCounterCcPhase.Armed &&
            outcome == ClientActionAttemptOutcome.ClientAccepted)
        {
            return state with
            {
                Phase = SamuraiReactiveCounterCcPhase.ApproachAccepted,
            };
        }

        // A Soten rejection/ambiguity and every Mineuchi boundary are terminal.
        // The surrounding protection episode may observe a future distinct
        // activation, but this exact actor/key intent is never replayed.
        return state with { Phase = SamuraiReactiveCounterCcPhase.Spent };
    }

    public static float NormalizeSotenMaximumRangeYalms(float configured) =>
        float.IsFinite(configured)
            ? Math.Clamp(
                configured,
                MineuchiMaximumRangeYalms,
                SotenMaximumRangeYalms)
            : SotenMaximumRangeYalms;

    private static SamuraiReactiveCounterCcDecision Waiting(
        SamuraiReactiveCounterCcState state) =>
        new(state, SamuraiReactiveCounterCcDecisionKind.Waiting, 0);

    private static SamuraiReactiveCounterCcDecision Cancelled() =>
        new(
            SamuraiReactiveCounterCcState.Initial,
            SamuraiReactiveCounterCcDecisionKind.Cancelled,
            0);
}
