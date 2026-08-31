namespace SeitonSense.Core;

public readonly record struct SprintRepeatProtectionObservation(
    bool Enabled,
    bool IsActionRequest,
    bool SprintCarrierVerified,
    uint ResolvedActionId,
    bool SprintMetadataVerified,
    bool SprintStatusKnown,
    uint ActiveSprintStatusId,
    bool SprintActive);

public readonly record struct SmartSprintIdleState(
    bool HasActionBarActivityBaseline,
    ulong ActionBarActivityToken,
    long LastActionBarActivityAtMilliseconds,
    bool IdleEpisodeSpent)
{
    public static SmartSprintIdleState Initial => new(false, 0, -1, false);
}

public readonly record struct SmartSprintIdleObservation(
    bool Enabled,
    bool IsSupportedPvpContext,
    bool IsLocalPlayerValidAndAlive,
    bool ActionBarActivityKnown,
    ulong ActionBarActivityToken,
    bool HeldGameplayKeyEligible,
    bool GuardStateKnown,
    bool GuardActive,
    bool IncapacitationStateKnown,
    bool Incapacitated,
    bool HigherPriorityClaimed,
    bool SprintMetadataVerified,
    bool SprintStatusKnown,
    bool SprintActive,
    bool SprintLocallyReady,
    int InactivityMilliseconds,
    long NowMilliseconds,
    bool HardReset = false);

public readonly record struct SmartSprintIdleDecision(
    SmartSprintIdleState NextState,
    bool ShouldDispatch,
    bool ActionBarActivityChanged,
    bool IdleThresholdReached);

/// <summary>
/// Exact, independent policies for PvP Sprint repeat protection and the
/// optional action-bar inactivity helper. Physical movement, camera, and
/// target input are intentionally absent from the activity clock.
/// </summary>
public static class SmartSprintRules
{
    public const uint BaseSprintActionId = 3;
    public const uint PvPSprintActionId = 29057;
    public const uint PvPSprintStatusId = 1342;

    public const bool RepeatProtectionDefaultEnabled = true;
    public const bool IdleSprintDefaultEnabled = false;
    public const int MinimumInactivityMilliseconds = 3_000;
    public const int MaximumInactivityMilliseconds = 5_000;
    public const int DefaultInactivityMilliseconds = 4_000;

    public static int NormalizeInactivityMilliseconds(int milliseconds) =>
        Math.Clamp(
            milliseconds,
            MinimumInactivityMilliseconds,
            MaximumInactivityMilliseconds);

    public static bool ShouldBlockRepeatPress(
        SprintRepeatProtectionObservation observation) =>
        observation.Enabled &&
        observation.IsActionRequest &&
        observation.SprintCarrierVerified &&
        observation.ResolvedActionId == PvPSprintActionId &&
        observation.SprintMetadataVerified &&
        observation.SprintStatusKnown &&
        observation.ActiveSprintStatusId == PvPSprintStatusId &&
        observation.SprintActive;

    public static SmartSprintIdleDecision ObserveIdle(
        SmartSprintIdleState previous,
        SmartSprintIdleObservation observation)
    {
        if (observation.HardReset ||
            !observation.Enabled ||
            !observation.IsSupportedPvpContext ||
            !observation.IsLocalPlayerValidAndAlive ||
            !observation.ActionBarActivityKnown ||
            observation.NowMilliseconds < 0)
        {
            return new SmartSprintIdleDecision(
                SmartSprintIdleState.Initial,
                ShouldDispatch: false,
                ActionBarActivityChanged: false,
                IdleThresholdReached: false);
        }

        var baselineInvalid =
            !previous.HasActionBarActivityBaseline ||
            previous.LastActionBarActivityAtMilliseconds < 0 ||
            previous.LastActionBarActivityAtMilliseconds > observation.NowMilliseconds;
        var activityChanged =
            previous.HasActionBarActivityBaseline &&
            previous.ActionBarActivityToken != observation.ActionBarActivityToken;
        if (baselineInvalid || activityChanged)
        {
            var baseline = new SmartSprintIdleState(
                HasActionBarActivityBaseline: true,
                observation.ActionBarActivityToken,
                observation.NowMilliseconds,
                IdleEpisodeSpent: false);
            return new SmartSprintIdleDecision(
                baseline,
                ShouldDispatch: false,
                ActionBarActivityChanged: activityChanged,
                IdleThresholdReached: false);
        }

        var threshold = NormalizeInactivityMilliseconds(observation.InactivityMilliseconds);
        var idleThresholdReached =
            observation.NowMilliseconds - previous.LastActionBarActivityAtMilliseconds >= threshold;
        if (!idleThresholdReached || previous.IdleEpisodeSpent)
        {
            return new SmartSprintIdleDecision(
                previous,
                ShouldDispatch: false,
                ActionBarActivityChanged: false,
                IdleThresholdReached: idleThresholdReached);
        }

        var eligible =
            observation.HeldGameplayKeyEligible &&
            observation.GuardStateKnown &&
            !observation.GuardActive &&
            observation.IncapacitationStateKnown &&
            !observation.Incapacitated &&
            !observation.HigherPriorityClaimed &&
            observation.SprintMetadataVerified &&
            observation.SprintStatusKnown &&
            !observation.SprintActive &&
            observation.SprintLocallyReady;
        if (!eligible)
        {
            return new SmartSprintIdleDecision(
                previous,
                ShouldDispatch: false,
                ActionBarActivityChanged: false,
                IdleThresholdReached: true);
        }

        return new SmartSprintIdleDecision(
            previous with { IdleEpisodeSpent = true },
            ShouldDispatch: true,
            ActionBarActivityChanged: false,
            IdleThresholdReached: true);
    }
}
