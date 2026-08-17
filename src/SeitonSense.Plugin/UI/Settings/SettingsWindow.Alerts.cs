using Dalamud.Bindings.ImGui;

namespace SeitonSense.Plugin.UI;

internal sealed partial class SettingsWindow
{
    private bool DrawAlertsPage()
    {
        var changed = false;
        ImGui.Spacing();
        ImGui.TextUnformatted("Warnings on you");
        changed |= Checkbox(
            "Show personal debuff warnings",
            configuration.ShowPersonalWarnings,
            value => configuration.ShowPersonalWarnings = value);
        changed |= Checkbox("Wildfire", configuration.WarnWildfire, value => configuration.WarnWildfire = value);
        ImGui.SameLine();
        changed |= Checkbox(
            "Death Warrant / Richtbefehl",
            configuration.WarnDeathWarrant,
            value => configuration.WarnDeathWarrant = value);
        changed |= Checkbox(
            "All Purify-removable debuff warnings",
            configuration.WarnPurifiableCrowdControl,
            value => configuration.WarnPurifiableCrowdControl = value);

        ImGui.Separator();
        ImGui.TextUnformatted("Center warning and action-feedback stack");
        changed |= Slider(
            "Warning horizontal position",
            configuration.PersonalWarningScreenX,
            0.05f,
            0.95f,
            value => configuration.PersonalWarningScreenX = value,
            "%.2f");
        changed |= Slider(
            "Warning vertical position",
            configuration.PersonalWarningScreenY,
            0.08f,
            0.9f,
            value => configuration.PersonalWarningScreenY = value,
            "%.2f");
        changed |= Slider(
            "Warning scale",
            configuration.PersonalWarningScale,
            0.55f,
            1.8f,
            value => configuration.PersonalWarningScale = value,
            "%.2f x");
        changed |= Slider(
            "Warning background opacity",
            configuration.PersonalWarningBackgroundOpacity,
            0f,
            1f,
            value => configuration.PersonalWarningBackgroundOpacity = value,
            "%.2f");
        ImGui.TextDisabled(
            "These controls move and scale personal warning cards, including the MCH LB card, plus CLEANSED, " +
            "AUTO CC LANDED, and GUARDIAN TRIGGERED feedback. They do not move the top-center focus alert or " +
            "the top-left isolation alert. At 0 opacity, text, icons, and borders remain visible.");

        ImGui.Separator();
        ImGui.TextUnformatted("Focused by several enemies");
        changed |= Checkbox(
            "Warn when 3+ enemies are directly targeting you",
            configuration.ShowHighPressureWarning,
            value => configuration.ShowHighPressureWarning = value);
        changed |= Checkbox(
            "Play one FFXIV system sound when the focus begins",
            configuration.PlayHighPressureWarningSound,
            value => configuration.PlayHighPressureWarningSound = value);
        changed |= SliderInt(
            "High-pressure warning sound",
            configuration.HighPressureWarningSoundId,
            1,
            16,
            value => configuration.HighPressureWarningSoundId = value,
            "Sound %d");
        if (ImGui.Button("Test high-pressure warning sound"))
            personalStatus.PlayHighPressureWarningSoundPreview();
        if (ImGui.Button(
                overlay.HighPressureWarningPreviewEnabled
                    ? "Stop high-pressure warning preview"
                    : "Preview high-pressure warning"))
        {
            overlay.HighPressureWarningPreviewEnabled = !overlay.HighPressureWarningPreviewEnabled;
        }
        ImGui.TextDisabled(
            "Exact current hard/cast targets only; recent hits do not count. The sound is independent from the " +
            "visual and is one-shot per focus episode. The alert stays top-center; isolation remains top-left " +
            "unless a narrow work area requires non-overlapping vertical stacking.");

        ImGui.Separator();
        ImGui.TextUnformatted("Spatial awareness");
        changed |= Checkbox(
            "Warn when no party ally is within 20y and line of sight",
            configuration.WarnWhenIsolated,
            value => configuration.WarnWhenIsolated = value);
        changed |= Slider(
            "Isolation warning size",
            configuration.IsolationWarningScale,
            0.75f,
            1.75f,
            value => configuration.IsolationWarningScale = value,
            "%.2f x");
        if (ImGui.Button(
                overlay.IsolationWarningPreviewEnabled
                    ? "Stop isolation warning preview"
                    : "Preview isolation warning"))
        {
            overlay.IsolationWarningPreviewEnabled = !overlay.IsolationWarningPreviewEnabled;
        }
        ImGui.TextDisabled(
            "CC only. Requires an exact five-player party and FFXIV's native 20y range/line-of-sight result. " +
            "Unknown data stays silent.");
        ImGui.TextDisabled(isolationAwareness.Diagnostics.ToChatLine());

        ImGui.Separator();
        ImGui.TextUnformatted("Marksman's Spite");
        changed |= Checkbox(
            "Show Marksman's Spite / MCH LB warning",
            configuration.WarnMarksmanSpite,
            value => configuration.WarnMarksmanSpite = value);
        changed |= Slider(
            "MCH LB warning size",
            configuration.MarksmanSpiteWarningScale,
            1f,
            2f,
            value => configuration.MarksmanSpiteWarningScale = value,
            "%.2f x");
        changed |= Checkbox(
            "Play a sound for a verified MCH LB warning",
            configuration.MchLimitBreakSoundEnabled,
            value => configuration.MchLimitBreakSoundEnabled = value);
        changed |= SliderInt(
            "MCH warning sound",
            configuration.MchLimitBreakSoundId,
            1,
            16,
            value => configuration.MchLimitBreakSoundId = value,
            "Sound %d");
        if (ImGui.Button("Test MCH warning sound"))
            personalStatus.PlayMachinistLimitBreakSoundPreview();
        ImGui.TextDisabled(
            "Live MCH capture and its sound require both the personal-debuff warning master and this Marksman's " +
            "Spite warning. The Test button remains available for setup. The alert never presses Guard or another action.");

        return changed;
    }
}
