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
            "Seiton Sense combines pressure awareness, personal alerts, native-nameplate cues, target clarity, " +
            "Limit Break activation cues, macro helpers, and explicitly enabled PvP target/action helpers.");
        ImGui.TextWrapped(
            "Crystalline Conflict is supported directly. Wolves' Den support is an explicit testing option; " +
            "Frontline and Rival Wings remain excluded from the original Seiton slot tracker.");

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
            "These are independent test scopes. Each feature still requires its own opt-in and keeps its own strict " +
            "target rules. Seiton uses the exact hostile duel opponent as synthetic S1, including party-member duels. " +
            "Smart Recuperate is self-only. Supported held job helpers use only your exact current hard target in " +
            "Wolves' Den; they never scan e-slots or silently substitute another actor. " +
            "The pressure option controls only the counter.");
        ImGui.PopTextWrapPos();

        ImGui.Separator();
        ImGui.TextUnformatted("Post-match convenience");
        changed |= Checkbox(
            "Immediately leave after a confirmed public CC match",
            configuration.EnableInstantLeaveAfterCrystallineConflict,
            value => configuration.EnableInstantLeaveAfterCrystallineConflict = value);
        ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
        ImGui.TextDisabled(
            "Default off. After the complete local 10-player result is confirmed, Seiton Sense waits for FFXIV's " +
            "native leave-ready boundary and sends one normal, non-forced Leave Duty request. Public Crystalline " +
            "Conflict only: never Wolves' Den, custom matches, Frontline, Rival Wings, or automatic re-queueing.");
        ImGui.TextDisabled(crystallineConflictInstantLeave.Diagnostics.ToChatLine());
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
