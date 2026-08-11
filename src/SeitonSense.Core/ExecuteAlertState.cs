namespace SeitonSense.Core;

public readonly record struct ExecuteAlertState(
    bool Armed,
    int ConsecutiveEligibleSamples,
    long RearmStartedAtMilliseconds)
{
    public static ExecuteAlertState Initial => new(true, 0, -1);
}
