using SeitonSense.Core;

internal static class HeldActionErrorSilenceSelfTests
{
    public static void OnlyExactSyntheticLineOfSightFailureIsSuppressed()
    {
        var exactLineOfSightRepeat = new HeldActionErrorSilenceObservation(
            Enabled: true,
            PluginOwnedRepeat: true,
            SupportedActionType: true,
            ExactHostileAction: true,
            ExactLocalActor: true,
            ExactTargetActor: true,
            NativeRangeStatus: SeitonRangeRules.LineOfSightBlocked);

        True(
            HeldActionErrorSilenceRules.ShouldSuppressRepeatedLineOfSightError(
                exactLineOfSightRepeat),
            "exact owned 562 repeat");
        False(
            HeldActionErrorSilenceRules.ShouldSuppressRepeatedLineOfSightError(
                exactLineOfSightRepeat with { Enabled = false }),
            "default-off setting");
        False(
            HeldActionErrorSilenceRules.ShouldSuppressRepeatedLineOfSightError(
                exactLineOfSightRepeat with { PluginOwnedRepeat = false }),
            "first physical press");
        False(
            HeldActionErrorSilenceRules.ShouldSuppressRepeatedLineOfSightError(
                exactLineOfSightRepeat with { SupportedActionType = false }),
            "unsupported action type");
        False(
            HeldActionErrorSilenceRules.ShouldSuppressRepeatedLineOfSightError(
                exactLineOfSightRepeat with { ExactHostileAction = false }),
            "unverified hostile action");
        False(
            HeldActionErrorSilenceRules.ShouldSuppressRepeatedLineOfSightError(
                exactLineOfSightRepeat with { ExactLocalActor = false }),
            "unverified local actor");
        False(
            HeldActionErrorSilenceRules.ShouldSuppressRepeatedLineOfSightError(
                exactLineOfSightRepeat with { ExactTargetActor = false }),
            "unverified target actor");
    }

    public static void RangeFacingAndUnknownErrorsStayNative()
    {
        var observation = new HeldActionErrorSilenceObservation(
            Enabled: true,
            PluginOwnedRepeat: true,
            SupportedActionType: true,
            ExactHostileAction: true,
            ExactLocalActor: true,
            ExactTargetActor: true,
            NativeRangeStatus: SeitonRangeRules.LineOfSightBlocked);

        False(
            HeldActionErrorSilenceRules.ShouldSuppressRepeatedLineOfSightError(
                observation with
                {
                    NativeRangeStatus = SeitonRangeRules.OutOfRange,
                }),
            "out-of-range remains audible");
        False(
            HeldActionErrorSilenceRules.ShouldSuppressRepeatedLineOfSightError(
                observation with
                {
                    NativeRangeStatus = SeitonRangeRules.NotFacingTarget,
                }),
            "facing error remains native");
        False(
            HeldActionErrorSilenceRules.ShouldSuppressRepeatedLineOfSightError(
                observation with { NativeRangeStatus = uint.MaxValue }),
            "unknown error remains native");
    }

    private static void True(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException($"Expected true: {label}");
    }

    private static void False(bool condition, string label) => True(!condition, label);
}
