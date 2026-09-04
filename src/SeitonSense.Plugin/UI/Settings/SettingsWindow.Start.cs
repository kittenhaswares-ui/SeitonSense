using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace SeitonSense.Plugin.UI;

internal sealed partial class SettingsWindow
{
    private bool DrawStartPage()
    {
        var changed = false;
        ImGui.Spacing();
        ImGui.TextWrapped(
            "Seiton Sense adds PvP warnings, pressure and nameplate information, clearer targets, Limit Break cues, " +
            "and optional action helpers.");
        ImGui.TextWrapped(
            "Crystalline Conflict is the main supported mode. Wolves' Den helpers are for testing only. Frontline and Rival " +
            "Wings are not supported by the enemy-slot features.");

        ImGui.Spacing();
        if (ImGui.Button("Why is a helper waiting?")) selectedPage = SettingsPage.Diagnostics;
        ImGui.SameLine();
        if (ImGui.Button("Settings for my job")) selectedPage = SettingsPage.JobTools;

        ImGui.Spacing();
        ImGui.TextUnformatted("Testing scope");
        changed |= Checkbox(
            "Enable Wolves' Den testing for supported Seiton Sense features and helpers",
            configuration.EnableWolvesDenTesting,
            value => configuration.EnableWolvesDenTesting = value);
        changed |= Checkbox(
            "Include Wolves' Den testing in the pressure counter",
            configuration.PressureIncludeWolvesDen,
            value => configuration.PressureIncludeWolvesDen = value);
        ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
        ImGui.TextDisabled(
            "These two testing switches are separate. Job helpers in Wolves' Den use only your current duel target; " +
            "they do not search for a different enemy. The pressure switch changes only the pressure counter.");
        ImGui.PopTextWrapPos();

        ImGui.Separator();
        ImGui.TextUnformatted("Post-match convenience");
        changed |= Checkbox(
            "Immediately leave after a confirmed public CC match",
            configuration.EnableInstantLeaveAfterCrystallineConflict,
            value => configuration.EnableInstantLeaveAfterCrystallineConflict = value);
        ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
        ImGui.TextDisabled(
            "Off by default. After a completed public CC match, Seiton waits until FFXIV allows leaving and sends one " +
            "normal Leave Duty request. It does not work in custom matches or other PvP modes and never queues again " +
            "for you. If leaving is not available within 30 seconds, it gives up.");
        ImGui.PopTextWrapPos();

        ImGui.Separator();
        ImGui.TextUnformatted("Preview and reset");
        if (ImGui.Button(overlay.PreviewEnabled ? "Stop preview" : "Preview nameplate cues + center cards"))
        {
            overlay.PreviewEnabled = !overlay.PreviewEnabled;
            if (overlay.PreviewEnabled)
            {
                overlay.CcProtectionPreviewEnabled = false;
                overlay.ResourceAuraPreviewEnabled = false;
            }
        }
        ImGui.SameLine();
        if (ImGui.Button(pressureCounter.PreviewEnabled ? "Stop pressure preview" : "Preview pressure"))
            pressureCounter.PreviewEnabled = !pressureCounter.PreviewEnabled;
        ImGui.SameLine();
        if (ImGui.Button("Preview Seiton popup")) overlay.TriggerPreviewPopup();
        ImGui.SameLine();
        if (ImGui.Button("Reset defaults"))
        {
            configuration.ResetToDefaults();
            overlay.PreviewEnabled = false;
            overlay.CcProtectionPreviewEnabled = false;
            overlay.ResourceAuraPreviewEnabled = false;
            overlay.IsolationWarningPreviewEnabled = false;
            overlay.HighPressureWarningPreviewEnabled = false;
            pressureCounter.PreviewEnabled = false;
            pressureCounter.ResetWindowPosition();
            changed = true;
        }

        return changed;
    }
}
