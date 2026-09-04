namespace SeitonSense.Core;

public readonly record struct HeldActionErrorSilenceObservation(
    bool Enabled,
    bool PluginOwnedRepeat,
    bool SupportedActionType,
    bool ExactHostileAction,
    bool ExactLocalActor,
    bool ExactTargetActor,
    uint NativeRangeStatus);

/// <summary>
/// Decides whether one plugin-owned held repeat may stop before FFXIV's native
/// action boundary. This is intentionally narrower than muting client audio:
/// the user's first press, range failures, and every unrelated error remain
/// native and audible.
/// </summary>
public static class HeldActionErrorSilenceRules
{
    public static bool ShouldSuppressRepeatedLineOfSightError(
        HeldActionErrorSilenceObservation observation) =>
        observation.Enabled &&
        observation.PluginOwnedRepeat &&
        observation.SupportedActionType &&
        observation.ExactHostileAction &&
        observation.ExactLocalActor &&
        observation.ExactTargetActor &&
        observation.NativeRangeStatus == SeitonRangeRules.LineOfSightBlocked;
}
