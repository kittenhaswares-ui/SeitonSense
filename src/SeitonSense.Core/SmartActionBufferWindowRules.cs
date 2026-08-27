namespace SeitonSense.Core;

/// <summary>
/// Shared bounds for the optional general action-buffer window.
/// Feature enablement is intentionally separate from this numeric setting.
/// </summary>
public static class SmartActionBufferWindowRules
{
    public const int DefaultMilliseconds = 1_000;
    public const int MinimumMilliseconds = 100;
    public const int MaximumMilliseconds = 1_500;

    public static int Normalize(int configuredMilliseconds) =>
        Math.Clamp(configuredMilliseconds, MinimumMilliseconds, MaximumMilliseconds);
}
