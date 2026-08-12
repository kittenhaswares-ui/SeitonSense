using System.Globalization;

namespace SeitonSense.Core;

public static class CcProtectionCountdownFormatter
{
    /// <summary>
    /// Formats only active finite durations, rounding upward so the label never
    /// claims less time than remains. Values below five seconds use tenths;
    /// longer values use whole seconds.
    /// </summary>
    public static string Format(float remainingSeconds)
    {
        if (!float.IsFinite(remainingSeconds) || remainingSeconds <= 0f)
        {
            return string.Empty;
        }

        if (remainingSeconds < 5f)
        {
            var tenths = MathF.Ceiling(remainingSeconds * 10f) / 10f;
            return tenths.ToString("0.0", CultureInfo.InvariantCulture);
        }

        return MathF.Ceiling(remainingSeconds).ToString("0", CultureInfo.InvariantCulture);
    }
}
