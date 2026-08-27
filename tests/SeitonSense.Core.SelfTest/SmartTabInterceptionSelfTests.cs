using SeitonSense.Core;

internal static class SmartTabInterceptionSelfTests
{
    public static void ExactNativeForwardTargetIsConsumed()
    {
        True(
            SmartTabInterceptionRules.ShouldConsumeNativeForwardTarget(Valid()),
            "the exact enabled native forward-target request is owned");
    }

    public static void ToggleOffAndUnsupportedContextsStayVanilla()
    {
        False(Owns(Valid() with { PluginEnabled = false }), "global off stays vanilla");
        False(Owns(Valid() with { FeatureEnabled = false }), "toggle off stays vanilla");
        False(Owns(Valid() with { HookAvailable = false }), "missing hook stays vanilla");
        False(Owns(Valid() with { ExactCrystallineConflict = false }), "outside exact CC stays vanilla");
        False(Owns(Valid() with { ReviewedSmartTabJob = false }), "unsupported jobs stay vanilla");
        False(Owns(Valid() with { NativeLineOfSightProbeVerified = false }),
            "unverified native line-of-sight probe stays vanilla");
        False(Owns(Valid() with { LocalPlayerAvailable = false }), "missing player stays vanilla");
    }

    public static void OtherNativePathsStayVanilla()
    {
        False(Owns(Valid() with { InsideNativeTargetingHandler = false }),
            "a direct cycle caller outside FFXIV's target handler stays vanilla");
        False(Owns(Valid() with { NativeWorldForwardCycle = false }),
            "reverse and non-forward native paths stay vanilla");
    }

    private static bool Owns(SmartTabInterceptionObservation observation) =>
        SmartTabInterceptionRules.ShouldConsumeNativeForwardTarget(observation);

    private static SmartTabInterceptionObservation Valid() => new(
        PluginEnabled: true,
        FeatureEnabled: true,
        HookAvailable: true,
        InsideNativeTargetingHandler: true,
        ExactCrystallineConflict: true,
        ReviewedSmartTabJob: true,
        NativeLineOfSightProbeVerified: true,
        LocalPlayerAvailable: true,
        NativeWorldForwardCycle: true);

    private static void True(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"Expected true: {message}");
    }

    private static void False(bool condition, string message) => True(!condition, message);
}
