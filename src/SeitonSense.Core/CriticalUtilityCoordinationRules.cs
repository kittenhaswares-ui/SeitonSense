namespace SeitonSense.Core;

/// <summary>
/// Pure publication gate for the optional cross-plugin held-utility claim.
/// The claim reports only an already-owned Seiton Sense scheduler frame; it
/// never creates, delays, retargets, or retries an action.
/// </summary>
public static class CriticalUtilityCoordinationRules
{
    /// <summary>
    /// Internal input arbitration is part of Seiton Sense itself and therefore
    /// must not depend on the optional cross-plugin publication setting.
    /// </summary>
    public static bool ShouldReserveIntegratedInput(
        bool pluginEnabled,
        SupportedPvPContext context,
        bool localPlayerAlive,
        bool hardReset,
        bool sharedHeldFrameConsumed) =>
        pluginEnabled &&
        (context is SupportedPvPContext.CrystallineConflict or
            SupportedPvPContext.WolvesDen) &&
        localPlayerAlive &&
        !hardReset &&
        sharedHeldFrameConsumed;

    public static bool ShouldPublish(
        bool pluginEnabled,
        bool coordinationEnabled,
        SupportedPvPContext context,
        bool localPlayerAlive,
        bool hardReset,
        bool sharedHeldFrameConsumed) =>
        pluginEnabled &&
        coordinationEnabled &&
        (context is SupportedPvPContext.CrystallineConflict or
            SupportedPvPContext.WolvesDen) &&
        localPlayerAlive &&
        !hardReset &&
        sharedHeldFrameConsumed;
}
