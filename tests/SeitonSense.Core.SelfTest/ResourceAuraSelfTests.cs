using SeitonSense.Core;

internal static class ResourceAuraSelfTests
{
    public static void ExactThresholdsAndCombinedState()
    {
        Equal(ResourceAuraKind.LowHp, Resolve(30, 100, 2_000, true), "HP enters at the exact configured percentage");
        Equal(ResourceAuraKind.None, Resolve(31, 100, 2_000, true), "HP above threshold is clear");
        Equal(ResourceAuraKind.LowMp, Resolve(100, 100, 1_999, true), "MP enters strictly below the configured amount");
        Equal(ResourceAuraKind.None, Resolve(100, 100, 2_000, true), "MP at the configured amount is clear");
        Equal(ResourceAuraKind.LowHpAndMp, Resolve(30, 100, 1_999, true), "simultaneous warnings combine into one state");
    }

    public static void InvalidAndUntrustedTelemetryFailsClosed()
    {
        Equal(ResourceAuraKind.None, Resolve(100, 100, 0, false), "initial zero MP is not trusted");
        Equal(ResourceAuraKind.None, ResourceAuraRules.Resolve(new ResourceAuraObservation(0, 100, 0, 10000, true, true, true), 30, 2_000), "zero HP is not a live alert");
        Equal(ResourceAuraKind.None, ResourceAuraRules.Resolve(new ResourceAuraObservation(101, 100, 0, 10000, true, true, true), 30, 2_000), "impossible HP fails closed");
        Equal(ResourceAuraKind.None, ResourceAuraRules.Resolve(new ResourceAuraObservation(30, 100, 0, 10000, true, true, false), 30, 2_000), "dead actor fails closed");
        Equal(ResourceAuraKind.None, ResourceAuraRules.Resolve(new ResourceAuraObservation(30, 100, -1, 10000, true, true, true), 30, 2_000), "negative MP fails closed");
    }

    private static ResourceAuraKind Resolve(uint hp, uint maxHp, int mp, bool trusted) =>
        ResourceAuraRules.Resolve(
            new ResourceAuraObservation(hp, maxHp, mp, 10_000, trusted, trusted && mp < 2_000, true),
            30,
            2_000);

    private static void Equal<T>(T expected, T actual, string message) where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
    }
}
