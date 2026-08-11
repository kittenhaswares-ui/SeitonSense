namespace SeitonSense.Core;

public static class ExecuteAlertRules
{
    public const int RequiredEligibleSamples = 2;
    public const long RearmDelayMilliseconds = 400;

    public static ExecuteAlertDecision Observe(
        ExecuteAlertState state,
        uint currentHp,
        uint maxHp,
        bool actionable,
        long nowMilliseconds)
    {
        var eligible = ExecuteThreshold.IsBelowHalf(currentHp, maxHp);
        if (eligible && actionable)
        {
            var samples = Math.Min(
                RequiredEligibleSamples,
                state.ConsecutiveEligibleSamples + 1);
            var trigger = state.Armed && samples >= RequiredEligibleSamples;
            return new ExecuteAlertDecision(
                new ExecuteAlertState(trigger ? false : state.Armed, samples, -1),
                true,
                trigger);
        }

        var armed = state.Armed;
        var rearmStarted = -1L;
        if (!armed && ExecuteThreshold.IsAtOrAboveRearm(currentHp, maxHp))
        {
            rearmStarted = state.RearmStartedAtMilliseconds >= 0
                ? state.RearmStartedAtMilliseconds
                : nowMilliseconds;
            if (nowMilliseconds - rearmStarted >= RearmDelayMilliseconds)
            {
                armed = true;
                rearmStarted = -1;
            }
        }

        return new ExecuteAlertDecision(
            new ExecuteAlertState(armed, 0, rearmStarted),
            false,
            false);
    }
}
