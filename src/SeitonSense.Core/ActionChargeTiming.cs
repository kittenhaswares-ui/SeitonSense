namespace SeitonSense.Core;

/// <summary>
/// Computes the next local charge boundary from FFXIV's aggregate recast data.
/// Invalid observations fail closed by returning positive infinity.
/// </summary>
public static class ActionChargeTiming
{
    public static double GetNextChargeRemainingMilliseconds(
        double totalSeconds,
        double elapsedSeconds,
        int maximumCharges)
    {
        if (!double.IsFinite(totalSeconds) ||
            !double.IsFinite(elapsedSeconds) ||
            totalSeconds < 0 ||
            elapsedSeconds < 0 ||
            maximumCharges < 1)
        {
            return double.PositiveInfinity;
        }

        var perChargeSeconds = totalSeconds / maximumCharges;
        return Math.Max(0, perChargeSeconds - elapsedSeconds) * 1000.0;
    }
}
