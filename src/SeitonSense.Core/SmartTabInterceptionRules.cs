namespace SeitonSense.Core;

/// <summary>
/// Value-only snapshot used to decide whether one native forward-target request
/// belongs to Smart Tab or must remain completely vanilla.
/// </summary>
public readonly record struct SmartTabInterceptionObservation(
    bool PluginEnabled,
    bool FeatureEnabled,
    bool HookAvailable,
    bool InsideNativeTargetingHandler,
    bool ExactCrystallineConflict,
    bool ReviewedSmartTabJob,
    bool LocalPlayerAvailable,
    bool NativeWorldForwardCycle);

/// <summary>
/// Owns only FFXIV's already-gated forward world-target cycle while it is nested
/// inside the native targeting handler. Reverse cycles, direct callers outside
/// that handler, UI Tab navigation, typing, and every other input stay original.
/// </summary>
public static class SmartTabInterceptionRules
{
    public static bool ShouldConsumeNativeForwardTarget(
        SmartTabInterceptionObservation observation) =>
        observation.PluginEnabled &&
        observation.FeatureEnabled &&
        observation.HookAvailable &&
        observation.InsideNativeTargetingHandler &&
        observation.ExactCrystallineConflict &&
        observation.ReviewedSmartTabJob &&
        observation.LocalPlayerAvailable &&
        observation.NativeWorldForwardCycle;
}
