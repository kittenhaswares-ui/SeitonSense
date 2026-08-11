namespace SeitonSense.Core;

public enum GuardAvailability
{
    Unknown,
    Ready,
    Unavailable,
}

public readonly record struct GuardCooldownState(
    bool IsKnown,
    long ReadyAtMilliseconds)
{
    public static GuardCooldownState Initial => new(false, -1);
}

public static class GuardCooldownRules
{
    public const long CooldownMilliseconds = 30_000;
    public const long ActiveDurationMilliseconds = 4_000;

    public static GuardCooldownState ObserveAction(
        GuardCooldownState state,
        long observedAtMilliseconds,
        long cooldownMilliseconds = CooldownMilliseconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(cooldownMilliseconds);
        if (observedAtMilliseconds < 0)
            return state;

        return ExtendDeadline(state, SaturatingAdd(observedAtMilliseconds, cooldownMilliseconds));
    }

    public static GuardCooldownState ObserveStatus(
        GuardCooldownState state,
        long nowMilliseconds,
        long remainingActiveMilliseconds,
        long cooldownMilliseconds = CooldownMilliseconds,
        long activeDurationMilliseconds = ActiveDurationMilliseconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(cooldownMilliseconds);
        ArgumentOutOfRangeException.ThrowIfNegative(activeDurationMilliseconds);
        if (nowMilliseconds < 0 || cooldownMilliseconds < activeDurationMilliseconds)
            return state;

        var remaining = Math.Clamp(remainingActiveMilliseconds, 0, activeDurationMilliseconds);
        var postStatusCooldown = cooldownMilliseconds - activeDurationMilliseconds;
        var inferredDeadline = SaturatingAdd(
            SaturatingAdd(nowMilliseconds, remaining),
            postStatusCooldown);
        return InferStatusDeadline(state, nowMilliseconds, inferredDeadline);
    }

    public static GuardCooldownState ObserveRevive() => new(true, long.MinValue);

    public static GuardCooldownState HardReset() => GuardCooldownState.Initial;

    public static GuardAvailability GetAvailability(
        GuardCooldownState state,
        long nowMilliseconds)
    {
        if (!state.IsKnown)
            return GuardAvailability.Unknown;

        return nowMilliseconds < state.ReadyAtMilliseconds
            ? GuardAvailability.Unavailable
            : GuardAvailability.Ready;
    }

    public static bool ShouldShowCrossedIcon(
        GuardCooldownState state,
        long nowMilliseconds) =>
        GetAvailability(state, nowMilliseconds) == GuardAvailability.Unavailable;

    public static long RemainingMilliseconds(
        GuardCooldownState state,
        long nowMilliseconds) =>
        GetAvailability(state, nowMilliseconds) == GuardAvailability.Unavailable
            ? state.ReadyAtMilliseconds - nowMilliseconds
            : 0;

    private static GuardCooldownState ExtendDeadline(
        GuardCooldownState state,
        long candidateReadyAtMilliseconds)
    {
        var deadline = state.IsKnown
            ? Math.Max(state.ReadyAtMilliseconds, candidateReadyAtMilliseconds)
            : candidateReadyAtMilliseconds;
        return new GuardCooldownState(true, deadline);
    }

    private static GuardCooldownState InferStatusDeadline(
        GuardCooldownState state,
        long observedAtMilliseconds,
        long inferredReadyAtMilliseconds)
    {
        // RemainingTime can be unchanged across several client frames. Infer a
        // Guard activation once so those stale samples cannot push its recast
        // deadline forward. Once the prior recast has elapsed, a later status
        // is necessarily a new activation and may establish a new deadline.
        return !state.IsKnown || observedAtMilliseconds >= state.ReadyAtMilliseconds
            ? new GuardCooldownState(true, inferredReadyAtMilliseconds)
            : state;
    }

    private static long SaturatingAdd(long left, long right) =>
        left > long.MaxValue - right ? long.MaxValue : left + right;
}
