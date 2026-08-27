using SeitonSense.Core;

internal static class CriticalUtilityCoordinationSelfTests
{
    internal static void IntegratedReservationIgnoresExternalPublicationToggle()
    {
        True(Reserve(), "exact CC held-frame ownership reserves integrated input");
        True(
            Reserve(context: SupportedPvPContext.WolvesDen),
            "enabled Wolves' Den context reserves integrated input");
        False(Reserve(pluginEnabled: false), "disabled plugin");
        False(Reserve(context: SupportedPvPContext.None), "unsupported context");
        False(Reserve(localPlayerAlive: false), "dead or missing local player");
        False(Reserve(hardReset: true), "context transition");
        False(Reserve(sharedHeldFrameConsumed: false), "no Seiton scheduler owner");

        False(
            Publish(coordinationEnabled: false),
            "external publication remains independently opt-in");
    }

    internal static void PublicationRequiresEveryExactGate()
    {
        True(Publish(), "exact CC held-frame ownership publishes");
        True(Publish(context: SupportedPvPContext.WolvesDen), "enabled Wolves' Den context publishes");
        False(Publish(pluginEnabled: false), "disabled plugin");
        False(Publish(coordinationEnabled: false), "explicit opt-in is required");
        False(Publish(context: SupportedPvPContext.None), "unsupported context");
        False(Publish(localPlayerAlive: false), "dead or missing local player");
        False(Publish(hardReset: true), "context transition");
        False(Publish(sharedHeldFrameConsumed: false), "no Seiton scheduler owner");
    }

    private static bool Publish(
        bool pluginEnabled = true,
        bool coordinationEnabled = true,
        SupportedPvPContext context = SupportedPvPContext.CrystallineConflict,
        bool localPlayerAlive = true,
        bool hardReset = false,
        bool sharedHeldFrameConsumed = true) =>
        CriticalUtilityCoordinationRules.ShouldPublish(
            pluginEnabled,
            coordinationEnabled,
            context,
            localPlayerAlive,
            hardReset,
            sharedHeldFrameConsumed);

    private static bool Reserve(
        bool pluginEnabled = true,
        SupportedPvPContext context = SupportedPvPContext.CrystallineConflict,
        bool localPlayerAlive = true,
        bool hardReset = false,
        bool sharedHeldFrameConsumed = true) =>
        CriticalUtilityCoordinationRules.ShouldReserveIntegratedInput(
            pluginEnabled,
            context,
            localPlayerAlive,
            hardReset,
            sharedHeldFrameConsumed);

    private static void True(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException($"Expected true: {label}");
    }

    private static void False(bool condition, string label) => True(!condition, label);
}
