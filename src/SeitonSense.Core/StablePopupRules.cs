namespace SeitonSense.Core;

public readonly record struct StablePopupState(
    bool Armed,
    long TrueObservedAtMilliseconds,
    long FalseObservedAtMilliseconds)
{
    public static StablePopupState Initial => new(true, -1, -1);
}

public readonly record struct StablePopupDecision(
    StablePopupState NextState,
    bool TriggerPopup);

public static class StablePopupRules
{
    public const long StableTrueMilliseconds = 50;
    public const long StableFalseToRearmMilliseconds = 300;

    public static StablePopupDecision Observe(
        StablePopupState state,
        bool candidate,
        bool rearmCondition,
        long nowMilliseconds,
        bool hardReset = false,
        long stableTrueMilliseconds = StableTrueMilliseconds,
        long stableFalseToRearmMilliseconds = StableFalseToRearmMilliseconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(stableTrueMilliseconds);
        ArgumentOutOfRangeException.ThrowIfNegative(stableFalseToRearmMilliseconds);

        if (hardReset)
            return new StablePopupDecision(StablePopupState.Initial, false);

        if (candidate)
        {
            if (!state.Armed)
                return new StablePopupDecision(state with { FalseObservedAtMilliseconds = -1 }, false);

            var trueSince = state.TrueObservedAtMilliseconds;
            if (trueSince < 0 || nowMilliseconds < trueSince)
                trueSince = nowMilliseconds;

            var trigger = nowMilliseconds - trueSince >= stableTrueMilliseconds;
            return new StablePopupDecision(
                new StablePopupState(!trigger, trueSince, -1),
                trigger);
        }

        if (state.Armed)
        {
            return new StablePopupDecision(
                new StablePopupState(true, -1, -1),
                false);
        }

        if (!rearmCondition)
        {
            return new StablePopupDecision(
                new StablePopupState(false, -1, -1),
                false);
        }

        var falseSince = state.FalseObservedAtMilliseconds;
        if (falseSince < 0 || nowMilliseconds < falseSince)
            falseSince = nowMilliseconds;

        var rearmed = nowMilliseconds - falseSince >= stableFalseToRearmMilliseconds;
        return new StablePopupDecision(
            rearmed
                ? StablePopupState.Initial
                : new StablePopupState(false, -1, falseSince),
            false);
    }
}
