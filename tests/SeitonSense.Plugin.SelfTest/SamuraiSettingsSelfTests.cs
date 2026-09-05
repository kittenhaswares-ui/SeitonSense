using System.Text.Json;
using SeitonSense.Plugin.Models;

internal static class SamuraiSettingsSelfTests
{
    internal static void LateFacingIsOptInBoundedAndResettable()
    {
        var config = JsonSerializer.Deserialize<PluginConfiguration>("{}")!;
        Require(!config.EnableSamuraiLateCastFacing && config.SamuraiLateCastFacingWindowSeconds == 0.15f,
            "Existing configurations do not silently enable experimental facing.");
        foreach (var invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            config.SamuraiLateCastFacingWindowSeconds = invalid;
            config.Initialize(null!);
            Require(config.SamuraiLateCastFacingWindowSeconds == 0.15f,
                "Nonfinite timing resets to the test default.");
        }
        config.SamuraiLateCastFacingWindowSeconds = 1f;
        config.Initialize(null!);
        Require(config.SamuraiLateCastFacingWindowSeconds == 0.30f, "Upper timing bound is enforced.");
        config.SamuraiLateCastFacingWindowSeconds = -1f;
        config.Initialize(null!);
        Require(config.SamuraiLateCastFacingWindowSeconds == 0.05f, "Lower timing bound is enforced.");
        config.EnableSamuraiLateCastFacing = true;
        config.SamuraiLateCastFacingWindowSeconds = 0.2f;
        var restored = JsonSerializer.Deserialize<PluginConfiguration>(JsonSerializer.Serialize(config))!;
        Require(restored.EnableSamuraiLateCastFacing && restored.SamuraiLateCastFacingWindowSeconds == 0.2f,
            "The user's test setting survives serialization.");
        restored.ResetToDefaults();
        Require(!restored.EnableSamuraiLateCastFacing && restored.SamuraiLateCastFacingWindowSeconds == 0.15f,
            "Reset returns facing to disabled with the initial window.");
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
