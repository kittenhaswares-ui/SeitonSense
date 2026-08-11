namespace SeitonSense.Core;

public readonly record struct DebouncedVisibilityState(
    bool IsVisible,
    long FalseObservedAtMilliseconds)
{
    public static DebouncedVisibilityState Initial => new(false, -1);
}

public static class DebouncedVisibilityRules
{
    public const long DefaultFalseGraceMilliseconds = 200;

    public static DebouncedVisibilityState Observe(
        DebouncedVisibilityState state,
        bool observedVisible,
        long nowMilliseconds,
        bool hardReset = false,
        long falseGraceMilliseconds = DefaultFalseGraceMilliseconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(falseGraceMilliseconds);

        if (hardReset)
            return DebouncedVisibilityState.Initial;

        if (observedVisible)
            return new DebouncedVisibilityState(true, -1);

        if (!state.IsVisible)
            return DebouncedVisibilityState.Initial;

        var falseSince = state.FalseObservedAtMilliseconds;
        if (falseSince < 0 || nowMilliseconds < falseSince)
            falseSince = nowMilliseconds;

        return nowMilliseconds - falseSince >= falseGraceMilliseconds
            ? DebouncedVisibilityState.Initial
            : new DebouncedVisibilityState(true, falseSince);
    }
}
