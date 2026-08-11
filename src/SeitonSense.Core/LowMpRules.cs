namespace SeitonSense.Core;

public readonly record struct LowMpState(
    bool HasTrustedSample,
    bool IsUnavailable,
    bool HasPendingTransition,
    bool PendingUnavailable,
    long PendingSinceMilliseconds)
{
    public static LowMpState Initial => new(false, false, false, false, -1);
}

public static class LowMpRules
{
    public const int RecuperateCost = 2_000;
    public const int ExitThreshold = 2_300;
    public const long TransitionDebounceMilliseconds = 150;

    public static LowMpState Observe(
        LowMpState state,
        int currentMp,
        bool trustedSample,
        long nowMilliseconds,
        bool hardReset = false,
        int enterThreshold = RecuperateCost,
        int exitThreshold = ExitThreshold,
        long debounceMilliseconds = TransitionDebounceMilliseconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(enterThreshold);
        ArgumentOutOfRangeException.ThrowIfLessThan(exitThreshold, enterThreshold);
        ArgumentOutOfRangeException.ThrowIfNegative(debounceMilliseconds);

        if (hardReset)
            return LowMpState.Initial;

        if (!trustedSample || currentMp < 0)
            return state with
            {
                HasPendingTransition = false,
                PendingSinceMilliseconds = -1,
            };

        var targetUnavailable = state.IsUnavailable
            ? currentMp < exitThreshold
            : currentMp < enterThreshold;
        var trustedState = state with { HasTrustedSample = true };

        if (targetUnavailable == state.IsUnavailable)
        {
            return trustedState with
            {
                HasPendingTransition = false,
                PendingSinceMilliseconds = -1,
            };
        }

        var pendingSince = state.HasPendingTransition &&
                           state.PendingUnavailable == targetUnavailable &&
                           nowMilliseconds >= state.PendingSinceMilliseconds
            ? state.PendingSinceMilliseconds
            : nowMilliseconds;

        if (nowMilliseconds - pendingSince < debounceMilliseconds)
        {
            return trustedState with
            {
                HasPendingTransition = true,
                PendingUnavailable = targetUnavailable,
                PendingSinceMilliseconds = pendingSince,
            };
        }

        return new LowMpState(true, targetUnavailable, false, false, -1);
    }

    public static bool ShouldShowCrossedIcon(LowMpState state) =>
        state.HasTrustedSample && state.IsUnavailable;
}
