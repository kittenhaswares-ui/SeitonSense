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
            "These controls move and scale ordinary personal warning cards, including the MCH LB card, plus CLEANSED, " +
            "AUTO CC LANDED, and GUARDIAN TRIGGERED feedback. The DRG airborne card stays in its separate top-center " +
            "lane but shares warning scale and background opacity. They do not move the top-center focus alert or the " +
            "top-left isolation alert. At 0 opacity, text, icons, and borders remain visible.");

        ImGui.Separator();
        ImGui.TextUnformatted("Your MP");
        changed |= Checkbox(
            "Play local MP threshold sounds",
            configuration.PlayLocalMpWarningSounds,
            value => configuration.PlayLocalMpWarningSounds = value);
        changed |= SliderInt(
            "4,000 MP warning sound",
            configuration.LocalMpWarning4000SoundId,
            1,
            16,
            value => configuration.LocalMpWarning4000SoundId = value,
            "Sound %d");
        if (ImGui.Button("Test 4,000 MP sound"))
            personalStatus.PlayLocalMpWarning4000SoundPreview();
        changed |= SliderInt(
            "2,000 MP critical sound",
            configuration.LocalMpWarning2000SoundId,
            1,
            16,
            value => configuration.LocalMpWarning2000SoundId = value,
            "Sound %d");
        if (ImGui.Button("Test 2,000 MP sound"))
            personalStatus.PlayLocalMpWarning2000SoundPreview();
        ImGui.TextDisabled(
            "Local player only. Each cue plays once when your trusted 10,000-MP value crosses downward through the " +
            "threshold, then rearms after recovery. A direct drop through both thresholds plays only the urgent 2,000-MP cue.");

        ImGui.Separator();
        ImGui.TextUnformatted("Limit Break activations");
        changed |= Checkbox(
            "Show my LB ACTIVATED banner",
            configuration.ShowLimitBreakActivationMessages,
            value => configuration.ShowLimitBreakActivationMessages = value);
        changed |= Checkbox(
            "Show ally LB damage cards on the left",
            configuration.ShowAllyLimitBreakDamageEvents,
            value => configuration.ShowAllyLimitBreakDamageEvents = value);
        ImGui.SameLine();
        changed |= Checkbox(
            "Use player names",
            configuration.LimitBreakFeedShowNames,
            value => configuration.LimitBreakFeedShowNames = value);
        ImGui.TextDisabled(
            "Duration LBs keep the banner, icon, and verified timer until they end. Instant LBs flash briefly. " +
            "Ally damage cards show player -> enemy and the captured damage. The banner uses a separate top-center " +
            "lane and never replaces or covers the removed combat-frame HP/MP panel.");

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
        ImGui.TextUnformatted("Dangerous enemy Limit Breaks");
        changed |= Checkbox(
            "Show MCH-on-you and airborne DRG LB warnings",
            configuration.WarnMarksmanSpite,
            value => configuration.WarnMarksmanSpite = value);
        changed |= Slider(
            "Dangerous LB warning size",
            configuration.MarksmanSpiteWarningScale,
            1f,
            2f,
            value => configuration.MarksmanSpiteWarningScale = value,
            "%.2f x");
        changed |= Checkbox(
            "Play a sound for verified MCH / airborne DRG LB warnings",
            configuration.MchLimitBreakSoundEnabled,
            value => configuration.MchLimitBreakSoundEnabled = value);
        changed |= SliderInt(
            "Dangerous LB warning sound",
            configuration.MchLimitBreakSoundId,
            1,
            16,
            value => configuration.MchLimitBreakSoundId = value,
            "Sound %d");
        if (ImGui.Button("Test dangerous LB warning sound"))
            personalStatus.PlayMachinistLimitBreakSoundPreview();
        ImGui.TextDisabled(
            "The MCH alert requires its exact early marker on you. The DRG alert starts from exact enemy Sky High " +
            "activation while airborne and never waits for impact. Both require the personal-warning master and this " +
            "toggle. Sound is one-shot per verified threat; neither alert presses Guard or another action.");

        return changed;
    }
}
