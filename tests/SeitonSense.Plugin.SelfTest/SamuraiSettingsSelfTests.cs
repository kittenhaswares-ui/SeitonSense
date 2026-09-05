using System.Text.Json;
using SeitonSense.Plugin.Models;

internal static class SamuraiSettingsSelfTests
{
    internal static void LateFacingIsOptInBoundedAndResettable()
    {
        var config = JsonSerializer.Deserialize<PluginConfiguration>("{}")!;
        Require(config.Version == 55 && !config.EnableSamuraiLateCastFacing &&
                config.SamuraiLateCastFacingWindowSeconds == 0.60f,
            "Fresh configurations use the new timing without enabling experimental facing.");

        foreach (var enabled in new[] { false, true })
        {
            var legacy = new PluginConfiguration
            {
                Version = 54,
                EnableSamuraiLateCastFacing = enabled,
                SamuraiLateCastFacingWindowSeconds = 0.15f,
            };
            legacy.Initialize(null!); // No game service or configuration file is used.
            Require(legacy.Version == 55 && legacy.EnableSamuraiLateCastFacing == enabled &&
                    legacy.SamuraiLateCastFacingWindowSeconds == 0.60f,
                "Schema 54 upgrades only the old timing default and preserves the opt-in choice.");
            var initialized = JsonSerializer.Serialize(legacy);
            legacy.Initialize(null!);
            Require(JsonSerializer.Serialize(legacy) == initialized,
                "Repeated initialization does not migrate or change settings again.");

            var customized = new PluginConfiguration
            {
                Version = 54,
                EnableSamuraiLateCastFacing = enabled,
                SamuraiLateCastFacingWindowSeconds = 0.20f,
            };
            customized.Initialize(null!);
            Require(customized.Version == 55 && customized.EnableSamuraiLateCastFacing == enabled &&
                    customized.SamuraiLateCastFacingWindowSeconds == 0.20f,
                "A customized legacy timing and opt-in choice survive migration.");
            foreach (var (input, expected) in new[]
            {
                (MathF.BitDecrement(0.15f), MathF.BitDecrement(0.15f)),
                (MathF.BitIncrement(0.15f), MathF.BitIncrement(0.15f)),
                (-1f, 0.05f),
                (2f, 1.00f),
            })
            {
                customized.Version = 54;
                customized.SamuraiLateCastFacingWindowSeconds = input;
                customized.Initialize(null!);
                Require(customized.EnableSamuraiLateCastFacing == enabled &&
                        customized.SamuraiLateCastFacingWindowSeconds == expected,
                    "Only the exact legacy default migrates; other timing values retain the normal clamp.");
            }

            var missingTiming = JsonSerializer.Deserialize<PluginConfiguration>(
                enabled
                    ? "{\"Version\":54,\"EnableSamuraiLateCastFacing\":true}"
                    : "{\"Version\":54,\"EnableSamuraiLateCastFacing\":false}")!;
            missingTiming.Initialize(null!);
            Require(missingTiming.Version == 55 && missingTiming.EnableSamuraiLateCastFacing == enabled &&
                    missingTiming.SamuraiLateCastFacingWindowSeconds == 0.60f,
                "Missing legacy timing uses the new default without changing opt-in state.");

            var current = new PluginConfiguration
            {
                Version = 55,
                EnableSamuraiLateCastFacing = enabled,
                SamuraiLateCastFacingWindowSeconds = 0.15f,
            };
            current.Initialize(null!);
            Require(current.EnableSamuraiLateCastFacing == enabled &&
                    current.SamuraiLateCastFacingWindowSeconds == 0.15f,
                "An explicitly chosen 150 ms value in schema 55 is not migrated.");
        }

        foreach (var invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            config.SamuraiLateCastFacingWindowSeconds = invalid;
            config.Initialize(null!);
            Require(config.SamuraiLateCastFacingWindowSeconds == 0.60f,
                "Nonfinite timing resets to the test default.");
        }
        config.SamuraiLateCastFacingWindowSeconds = 2f;
        config.Initialize(null!);
        Require(config.SamuraiLateCastFacingWindowSeconds == 1.00f, "Upper timing bound is enforced.");
        config.SamuraiLateCastFacingWindowSeconds = -1f;
        config.Initialize(null!);
        Require(config.SamuraiLateCastFacingWindowSeconds == 0.05f, "Lower timing bound is enforced.");
        foreach (var valid in new[] { 0.05f, 0.15f, 0.20f, 0.60f, 1.00f })
        {
            config.SamuraiLateCastFacingWindowSeconds = valid;
            config.Initialize(null!);
            Require(config.SamuraiLateCastFacingWindowSeconds == valid,
                "Finite timing values, including both exact bounds, remain unchanged.");
        }
        config.EnableSamuraiLateCastFacing = true;
        config.SamuraiLateCastFacingWindowSeconds = 0.85f;
        var restored = JsonSerializer.Deserialize<PluginConfiguration>(JsonSerializer.Serialize(config))!;
        restored.Initialize(null!);
        Require(restored.EnableSamuraiLateCastFacing && restored.SamuraiLateCastFacingWindowSeconds == 0.85f,
            "The user's test setting survives serialization.");
        restored.ResetToDefaults();
        Require(restored.Version == 55 && !restored.EnableSamuraiLateCastFacing &&
                restored.SamuraiLateCastFacingWindowSeconds == 0.60f,
            "Reset returns facing to disabled with the initial window.");
        var resetRoundTrip = JsonSerializer.Deserialize<PluginConfiguration>(JsonSerializer.Serialize(restored))!;
        resetRoundTrip.Initialize(null!);
        Require(!resetRoundTrip.EnableSamuraiLateCastFacing &&
                resetRoundTrip.SamuraiLateCastFacingWindowSeconds == 0.60f,
            "Reset defaults remain disabled and stable after serialization and initialization.");
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
