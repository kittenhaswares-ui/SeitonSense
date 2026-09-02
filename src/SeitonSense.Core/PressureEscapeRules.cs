namespace SeitonSense.Core;

public enum PressureEscapeWarningSignal
{
    Unknown = 0,
    Safe = 1,
    HighPressure = 2,
}

public readonly record struct PressureEscapeWarningState(
    bool IsVisible,
    bool EpisodeOpen,
    long SafeSinceMilliseconds,
    ulong EpisodeToken)
{
    public static PressureEscapeWarningState Initial => new(false, false, -1, 0);
}

public readonly record struct PressureEscapeWarningObservation(
    bool Enabled,
    bool IsCrystallineConflict,
    bool IsLocalPlayerValidAndAlive,
    bool PressureKnown,
    int DirectEnemyCount,
    long NowMilliseconds,
    bool HardReset = false);

public readonly record struct PressureEscapeWarningDecision(
    PressureEscapeWarningState NextState,
    PressureEscapeWarningSignal Signal,
    bool HighPressure,
    bool EnteredWarning)
{
    public bool WarningActive => NextState.IsVisible;
}

public readonly record struct PressureEscapeSprintObservation(
    bool Enabled,
    bool IsCrystallineConflict,
    bool IsLocalPlayerValidAndAlive,
    bool SprintMetadataVerified,
    bool PressureKnown,
    int DirectEnemyCount,
    bool GuardSuppressed,
    bool SprintActive,
    bool Incapacitated,
    bool HigherPriorityClaimed,
    bool EpisodeAvailable,
    bool HeldMovementKeyEligible,
    int HeldMovementVirtualKey,
    bool SprintLocallyReady);

/// <summary>
/// Exact gates for the high-pressure warning and the optional movement-key
/// Sprint helper. "Pressure" here means only the current unique hard/cast
/// target union for the exact local player; historical action hints are never
/// an eligible input.
/// </summary>
public static class PressureEscapeRules
{
    public const int RequiredDirectEnemyCount = 3;
    public const long MaximumPressureAgeMilliseconds = 250;
    public const long WarningClearGraceMilliseconds = 300;

    // Win32 virtual-key values. The feature intentionally supports only the
    // standard keyboard movement keys; arbitrary gameplay keys may execute an
    // action immediately after Sprint and cancel its effect.
    public const int LeftVirtualKey = 0x25;
    public const int UpVirtualKey = 0x26;
    public const int RightVirtualKey = 0x27;
    public const int DownVirtualKey = 0x28;
    public const int AVirtualKey = 0x41;
    public const int DVirtualKey = 0x44;
    public const int SVirtualKey = 0x53;
    public const int WVirtualKey = 0x57;

    public static bool IsHighPressure(bool pressureKnown, int directEnemyCount) =>
        pressureKnown && directEnemyCount >= RequiredDirectEnemyCount;

    public static bool IsSupportedMovementVirtualKey(int virtualKey) =>
        virtualKey is LeftVirtualKey or UpVirtualKey or RightVirtualKey or DownVirtualKey or
            AVirtualKey or DVirtualKey or SVirtualKey or WVirtualKey;

    public static PressureEscapeWarningDecision ObserveWarning(
        PressureEscapeWarningState previous,
        PressureEscapeWarningObservation observation)
    {
        if (observation.HardReset)
            return Unknown(PressureEscapeWarningState.Initial);

        if (!observation.Enabled ||
            !observation.IsCrystallineConflict ||
            !observation.IsLocalPlayerValidAndAlive ||
            !observation.PressureKnown ||
            observation.DirectEnemyCount < 0 ||
            observation.NowMilliseconds < 0 ||
            previous.SafeSinceMilliseconds > observation.NowMilliseconds)
        {
            // Unknown/stale data hides immediately, but cannot manufacture a
            // safe separation. Preserve the open episode and any spent Sprint
            // ownership; only a fresh <3 observation can start the clear grace.
            return Unknown(new PressureEscapeWarningState(
                false,
                previous.EpisodeOpen,
                -1,
                previous.EpisodeToken));
        }

        var highPressure = IsHighPressure(true, observation.DirectEnemyCount);
        if (highPressure)
        {
            var entered = !previous.EpisodeOpen;
            var token = entered ? NextEpisodeToken(previous.EpisodeToken) : previous.EpisodeToken;
            return new PressureEscapeWarningDecision(
                new PressureEscapeWarningState(
                    true,
                    true,
                    -1,
                    token),
                PressureEscapeWarningSignal.HighPressure,
                true,
                entered);
        }

        if (!previous.EpisodeOpen)
        {
            return new PressureEscapeWarningDecision(
                Hide(previous),
                PressureEscapeWarningSignal.Safe,
                false,
                false);
        }

        var safeSince = previous.SafeSinceMilliseconds >= 0
            ? previous.SafeSinceMilliseconds
            : observation.NowMilliseconds;
        var insideClearGrace = observation.NowMilliseconds - safeSince <
                               WarningClearGraceMilliseconds;
        return new PressureEscapeWarningDecision(
            insideClearGrace
                ? new PressureEscapeWarningState(
                    previous.IsVisible,
                    true,
                    safeSince,
                    previous.EpisodeToken)
                : new PressureEscapeWarningState(
                    false,
                    false,
                    -1,
                    previous.EpisodeToken),
            PressureEscapeWarningSignal.Safe,
            false,
            false);
    }

    public static bool CanFreezeSprintIntent(PressureEscapeSprintObservation observation) =>
        observation.Enabled &&
        observation.IsCrystallineConflict &&
        observation.IsLocalPlayerValidAndAlive &&
        observation.SprintMetadataVerified &&
        IsHighPressure(observation.PressureKnown, observation.DirectEnemyCount) &&
        !observation.GuardSuppressed &&
        !observation.SprintActive &&
        !observation.Incapacitated &&
        !observation.HigherPriorityClaimed &&
        observation.EpisodeAvailable &&
        observation.HeldMovementKeyEligible &&
        IsSupportedMovementVirtualKey(observation.HeldMovementVirtualKey);

    public static bool CanDispatchSprint(PressureEscapeSprintObservation observation) =>
        CanFreezeSprintIntent(observation) && observation.SprintLocallyReady;

    private static PressureEscapeWarningState Hide(PressureEscapeWarningState state) =>
        new(false, false, -1, state.EpisodeToken);

    private static PressureEscapeWarningDecision Unknown(PressureEscapeWarningState state) =>
        new(state, PressureEscapeWarningSignal.Unknown, false, false);

    private static ulong NextEpisodeToken(ulong current) =>
        current == ulong.MaxValue ? 1UL : current + 1;
}
