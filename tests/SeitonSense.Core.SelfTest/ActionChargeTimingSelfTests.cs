using SeitonSense.Core;

internal static class ActionChargeTimingSelfTests
{
    internal static void NextChargeBoundaryUsesPerChargeRecast()
    {
        Equal(10_000d, ActionChargeTiming.GetNextChargeRemainingMilliseconds(40, 10, 2));
        Equal(0d, ActionChargeTiming.GetNextChargeRemainingMilliseconds(40, 25, 2));
        True(double.IsPositiveInfinity(
            ActionChargeTiming.GetNextChargeRemainingMilliseconds(40, 0, 0)));
    }

    private static void True(bool condition)
    {
        if (!condition) throw new InvalidOperationException("Expected true, got false.");
    }

    private static void Equal<T>(T expected, T actual)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"Expected {expected}; got {actual}.");
    }
}
