using Dalamud.Bindings.ImGui;

namespace SeitonSense.Plugin.UI;

internal sealed partial class SettingsWindow
{
    private bool DrawOfficialRankControls()
    {
        ImGui.Separator();
        var changed = Checkbox("Show official tiers in the Prediction panel",
            configuration.ShowOfficialCrystallineConflictRanks,
            value => configuration.ShowOfficialCrystallineConflictRanks = value);
        ImGui.TextWrapped("Adds the published tier for listed allies and enemies. Lodestone only publishes the top 300 overall and top 10 per tier, so many players will show Unknown. This does not change the win estimate.");
        ImGui.TextWrapped("Updates once per 24 hours, only outside combat and duties. A small file stays on this PC. No player searches, match uploads, or downloads during a match.");
        ImGui.TextDisabled(officialRanks.Status.Message);
        if (officialRanks.Status.Cache is { Season: > 0 } cache)
            ImGui.TextDisabled($"Official snapshot: season {cache.Season}, {cache.SourceUpdatedText}");
        return changed;
    }
}
