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
            "Moves and scales your normal warning cards, including MCH LB, CLEANSED, AUTO CC LANDED, and GUARDIAN. " +
            "The DRG air warning keeps its own top position but uses the same size and background. Focus and isolation " +
            "warnings have their own positions.");

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
            "These sounds are only for your MP. Each plays once when you fall below its limit. If you cross both limits " +
            "at once, only the urgent 2,000 MP sound plays.");

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
            "Long Limit Breaks show their icon and timer until they end; instant LBs flash briefly. Ally cards show " +
            "who hit whom and the damage seen by the plugin.");

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
            "Counts enemies currently targeting or casting at you; old hits do not count. The sound plays once when " +
            "the focus begins and can be enabled separately from the warning.");

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
            "CC only. Warns when no party member is within 20 yalms and visible to you. If the party data is unclear, " +
            "it stays silent.");
        ImGui.Separator();
        ImGui.TextUnformatted("Dangerous enemy Limit Breaks");
        changed |= Checkbox(
            "Show MCH-on-you and airborne DRG LB warnings",
            configuration.WarnMarksmanSpite,
            value => configuration.WarnMarksmanSpite = value);
        changed |= Checkbox(
            "Show enemy Summoner Bahamut / Phoenix LB warning",
            configuration.WarnSummonerLimitBreak,
            value => configuration.WarnSummonerLimitBreak = value);
        changed |= Checkbox(
            "Show enemy Samurai Chiten warning and nameplate icon",
            configuration.WarnEnemyChiten,
            value => configuration.WarnEnemyChiten = value);
        changed |= Slider(
            "Dangerous LB warning size",
            configuration.MarksmanSpiteWarningScale,
            1f,
            2f,
            value => configuration.MarksmanSpiteWarningScale = value,
            "%.2f x");
        changed |= Checkbox(
            "Play a sound for confirmed enemy danger warnings",
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
            "Shows only dangers Seiton can confirm. Chiten also gets a timer above the Samurai's nameplate. The sound " +
            "plays once per danger, and these warnings never press an action for you.");

        return changed;
    }
}
