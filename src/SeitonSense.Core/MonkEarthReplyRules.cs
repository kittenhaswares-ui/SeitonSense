namespace SeitonSense.Core;

public enum MonkEarthReplyPhase
{
    WaitingForResonance = 0,
    TrackingResonance = 1,
    SpentUntilResonanceGone = 2,
}

public enum MonkEarthReplyDecisionKind
{
    None = 0,
    ResonanceObserved = 1,
    Waiting = 2,
    Dispatch = 3,
    Cancelled = 4,
}

public enum MonkEarthReplyTrigger
{
    None = 0,
    LowHp = 1,
    Expiry = 2,
}

public enum MonkEarthReplyDecisionReason
{
    None = 0,
    ConfigurationDisabled = 1,
    OutsideSupportedPvPContext = 2,
    LocalMonkInvalid = 3,
    LocalPlayerIdentityInvalid = 4,
    MetadataUnverified = 5,
    ResonanceGone = 6,
    InvalidHealth = 7,
    InvalidResonanceTime = 8,
    FollowUpNotAdjusted = 9,
    HigherPriorityClaimed = 10,
    NoEnabledTrigger = 11,
    TriggerThresholdNotReached = 12,
    HardReset = 13,
    ClockMovedBackwards = 14,
}

public readonly record struct MonkEarthReplyState(
    MonkEarthReplyPhase Phase,
    long ResonanceObservedAtMilliseconds,
    long MissingObservedAtMilliseconds,
    long LastObservedAtMilliseconds,
    MonkEarthReplyTrigger SpentTrigger)
{
    public static MonkEarthReplyState Initial => new(
        MonkEarthReplyPhase.WaitingForResonance,
        -1,
        -1,
        -1,
        MonkEarthReplyTrigger.None);
}

public readonly record struct MonkEarthReplyObservation(
    bool ConfigurationEnabled,
    bool IsSupportedPvPContext,
    bool IsLocalMonkValid,
    bool IsLocalPlayerIdentityValid,
    bool MetadataVerified,
    bool HigherPriorityClaimed,
    bool ResonancePresent,
    uint CurrentHp,
    uint MaximumHp,
    float ResonanceRemainingSeconds,
    uint AdjustedActionId,
    bool TriggerOnLowHp,
    bool TriggerBeforeExpiry,
    int LowHpThresholdPercent,
    float ExpiryThresholdSeconds,
    long NowMilliseconds,
    bool HardReset = false);

public readonly record struct MonkEarthReplyDecision(
    MonkEarthReplyState NextState,
    MonkEarthReplyDecisionKind Kind,
    MonkEarthReplyDecisionReason Reason,
    MonkEarthReplyTrigger Trigger = MonkEarthReplyTrigger.None)
{
    /// <summary>
    /// Dispatch decisions already contain a spent state. The caller must store
    /// <see cref="NextState"/> before invoking the native action so a false,
    /// throwing, or rejected attempt can never be retried for this resonance.
    /// </summary>
    public bool ShouldDispatch => Kind == MonkEarthReplyDecisionKind.Dispatch;
}

/// <summary>
/// Pure one-attempt policy for Monk's PvP Earth's Reply follow-up. It never
/// activates Riddle of Earth: the exact transformed action and the continuous
/// Earth Resonance observation are both mandatory before a dispatch.
/// </summary>
public static class MonkEarthReplyRules
{
    public const uint MonkJobId = 20;
    public const uint RiddleOfEarthActionId = 29_482;
    public const uint EarthsReplyActionId = 29_483;
    public const uint EarthResonanceStatusId = 3_171;
    public const uint RiddleOfEarthIconId = 9_161;
    public const uint EarthsReplyIconId = 9_644;
    public const uint EarthResonanceIconId = 212_527;
    public const uint EarthsReplyProcStatusRowId = 94;
    public const long ResonanceMissingGraceMilliseconds = 150;

    public static MonkEarthReplyDecision Observe(
        MonkEarthReplyState previous,
        MonkEarthReplyObservation observation)
    {
        if (observation.HardReset)
            return Cancelled(MonkEarthReplyState.Initial, MonkEarthReplyDecisionReason.HardReset);

        if (observation.NowMilliseconds < 0 ||
            (previous.LastObservedAtMilliseconds >= 0 &&
             observation.NowMilliseconds < previous.LastObservedAtMilliseconds))
        {
            return Cancelled(
                MonkEarthReplyState.Initial,
                MonkEarthReplyDecisionReason.ClockMovedBackwards);
        }

        var gateFailure = GetGateFailure(observation);
        if (gateFailure != MonkEarthReplyDecisionReason.None)
        {
            // An already-spent continuous resonance remains spent across a
            // temporary option/metadata gate. Toggling the helper cannot turn
            // one still-present buff into a second native attempt.
            if (previous.Phase == MonkEarthReplyPhase.SpentUntilResonanceGone &&
                observation.ResonancePresent)
            {
                return Waiting(
                    previous with
                    {
                        MissingObservedAtMilliseconds = -1,
                        LastObservedAtMilliseconds = observation.NowMilliseconds,
                    },
                    gateFailure);
            }

            return Cancelled(MonkEarthReplyState.Initial, gateFailure);
        }

        if (!observation.ResonancePresent)
            return ObserveMissing(previous, observation.NowMilliseconds);

        var current = previous.Phase == MonkEarthReplyPhase.WaitingForResonance
            ? new MonkEarthReplyState(
                MonkEarthReplyPhase.TrackingResonance,
                observation.NowMilliseconds,
                -1,
                observation.NowMilliseconds,
                MonkEarthReplyTrigger.None)
            : previous with
            {
                MissingObservedAtMilliseconds = -1,
                LastObservedAtMilliseconds = observation.NowMilliseconds,
            };

        if (current.Phase == MonkEarthReplyPhase.SpentUntilResonanceGone)
            return Waiting(current, MonkEarthReplyDecisionReason.None);

        if (!IsValidHealth(observation.CurrentHp, observation.MaximumHp))
            return Waiting(current, MonkEarthReplyDecisionReason.InvalidHealth);
        if (!float.IsFinite(observation.ResonanceRemainingSeconds) ||
            observation.ResonanceRemainingSeconds <= 0f)
        {
            return Waiting(current, MonkEarthReplyDecisionReason.InvalidResonanceTime);
        }

        if (!observation.TriggerOnLowHp && !observation.TriggerBeforeExpiry)
            return Waiting(current, MonkEarthReplyDecisionReason.NoEnabledTrigger);

        if (observation.AdjustedActionId != EarthsReplyActionId)
            return Waiting(current, MonkEarthReplyDecisionReason.FollowUpNotAdjusted);

        var lowHpTriggered = observation.TriggerOnLowHp &&
                             IsAtOrBelowHealthThreshold(
                                 observation.CurrentHp,
                                 observation.MaximumHp,
                                 observation.LowHpThresholdPercent);
        var expiryTriggered = observation.TriggerBeforeExpiry &&
                              IsInsideExpiryWindow(
                                  observation.ResonanceRemainingSeconds,
                                  observation.ExpiryThresholdSeconds);
        var trigger = lowHpTriggered
            ? MonkEarthReplyTrigger.LowHp
            : expiryTriggered
                ? MonkEarthReplyTrigger.Expiry
                : MonkEarthReplyTrigger.None;
        if (trigger == MonkEarthReplyTrigger.None)
        {
            return previous.Phase == MonkEarthReplyPhase.WaitingForResonance
                ? new MonkEarthReplyDecision(
                    current,
                    MonkEarthReplyDecisionKind.ResonanceObserved,
                    MonkEarthReplyDecisionReason.TriggerThresholdNotReached)
                : Waiting(current, MonkEarthReplyDecisionReason.TriggerThresholdNotReached);
        }

        if (observation.HigherPriorityClaimed)
            return Waiting(current, MonkEarthReplyDecisionReason.HigherPriorityClaimed);

        var spent = current with
        {
            Phase = MonkEarthReplyPhase.SpentUntilResonanceGone,
            LastObservedAtMilliseconds = observation.NowMilliseconds,
            SpentTrigger = trigger,
        };
        return new MonkEarthReplyDecision(
            spent,
            MonkEarthReplyDecisionKind.Dispatch,
            MonkEarthReplyDecisionReason.None,
            trigger);
    }

    public static bool IsAtOrBelowHealthThreshold(
        uint currentHp,
        uint maximumHp,
        int thresholdPercent) =>
        IsValidHealth(currentHp, maximumHp) &&
        thresholdPercent is >= 1 and <= 100 &&
        (ulong)currentHp * 100UL <= (ulong)maximumHp * (uint)thresholdPercent;

    public static bool IsInsideExpiryWindow(
        float remainingSeconds,
        float thresholdSeconds) =>
        float.IsFinite(remainingSeconds) &&
        remainingSeconds > 0f &&
        float.IsFinite(thresholdSeconds) &&
        thresholdSeconds > 0f &&
        remainingSeconds <= thresholdSeconds;

    private static MonkEarthReplyDecision ObserveMissing(
        MonkEarthReplyState previous,
        long nowMilliseconds)
    {
        if (previous.Phase == MonkEarthReplyPhase.WaitingForResonance)
            return new MonkEarthReplyDecision(
                MonkEarthReplyState.Initial with { LastObservedAtMilliseconds = nowMilliseconds },
                MonkEarthReplyDecisionKind.None,
                MonkEarthReplyDecisionReason.None);

        var missingObservedAt = previous.MissingObservedAtMilliseconds;
        if (missingObservedAt < 0 || nowMilliseconds < missingObservedAt)
            missingObservedAt = nowMilliseconds;

        if (nowMilliseconds - missingObservedAt < ResonanceMissingGraceMilliseconds)
        {
            return Waiting(
                previous with
                {
                    MissingObservedAtMilliseconds = missingObservedAt,
                    LastObservedAtMilliseconds = nowMilliseconds,
                },
                MonkEarthReplyDecisionReason.ResonanceGone);
        }

        return Cancelled(
            MonkEarthReplyState.Initial with { LastObservedAtMilliseconds = nowMilliseconds },
            MonkEarthReplyDecisionReason.ResonanceGone);
    }

    private static MonkEarthReplyDecisionReason GetGateFailure(
        MonkEarthReplyObservation observation)
    {
        if (!observation.ConfigurationEnabled)
            return MonkEarthReplyDecisionReason.ConfigurationDisabled;
        if (!observation.IsSupportedPvPContext)
            return MonkEarthReplyDecisionReason.OutsideSupportedPvPContext;
        if (!observation.IsLocalMonkValid)
            return MonkEarthReplyDecisionReason.LocalMonkInvalid;
        if (!observation.IsLocalPlayerIdentityValid)
            return MonkEarthReplyDecisionReason.LocalPlayerIdentityInvalid;
        if (!observation.MetadataVerified)
            return MonkEarthReplyDecisionReason.MetadataUnverified;
        return MonkEarthReplyDecisionReason.None;
    }

    private static bool IsValidHealth(uint currentHp, uint maximumHp) =>
        currentHp > 0 && maximumHp > 0 && currentHp <= maximumHp;

    private static MonkEarthReplyDecision Waiting(
        MonkEarthReplyState state,
        MonkEarthReplyDecisionReason reason) =>
        new(state, MonkEarthReplyDecisionKind.Waiting, reason);

    private static MonkEarthReplyDecision Cancelled(
        MonkEarthReplyState state,
        MonkEarthReplyDecisionReason reason) =>
        new(state, MonkEarthReplyDecisionKind.Cancelled, reason);
}
