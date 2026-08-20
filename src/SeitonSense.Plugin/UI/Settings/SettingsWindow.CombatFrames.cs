using Dalamud.Bindings.ImGui;

namespace SeitonSense.Plugin.UI;

internal sealed partial class SettingsWindow
{
    private bool DrawCombatFramesPage()
    {
        var changed = false;
        ImGui.Spacing();
        ImGui.TextWrapped(
            "A fixed Gladius-style combat view: one Self frame plus stable S1-S5 enemy rows. " +
            "It is screen-space only, so walls, camera movement, and nameplate projection cannot move or hide it.");

        changed |= Checkbox(
            "Show fixed Combat Frames",
            configuration.ShowCombatFrames,
            value => configuration.ShowCombatFrames = value);

        if (ImGui.Button(combatFrames.PreviewEnabled ? "Stop Combat Frames preview" : "Preview Combat Frames"))
            combatFrames.PreviewEnabled = !combatFrames.PreviewEnabled;
        ImGui.SameLine();
        if (ImGui.Button("Reset frame layout"))
        {
            configuration.ApplyCombatFramesLayoutDefaults();
            changed = true;
        }

        ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
        ImGui.TextDisabled(
            "Display-only: no row is clickable and no hard, soft, focus, or mouseover target is changed. Unknown " +
            "HP, MP, slot, pressure, or status data stays visibly unknown instead of being guessed. The native " +
            "FFXIV HUD is never hidden or edited; hide its parameter/enemy-list elements manually in HUD Layout " +
            "if you want these frames to visually replace them.");
        ImGui.PopTextWrapPos();

        ImGui.Separator();
        ImGui.TextUnformatted("Information");
        changed |= Checkbox(
            "Show character names",
            configuration.CombatFramesShowNames,
            value => configuration.CombatFramesShowNames = value);
        changed |= Checkbox(
            "Show exact HP and MP values",
            configuration.CombatFramesShowExactValues,
            value => configuration.CombatFramesShowExactValues = value);
        changed |= Checkbox(
            "Show relevant Guard / CC / execute statuses",
            configuration.CombatFramesShowStatuses,
            value => configuration.CombatFramesShowStatuses = value);
        changed |= Checkbox(
            "Show direct pressure and team-focus badges",
            configuration.CombatFramesShowPressure,
            value => configuration.CombatFramesShowPressure = value);
        ImGui.TextDisabled(
            "MP bars use trusted observations and 2,000-MP divisions. Enemy order is always canonical S1-S5; " +
            "dead or temporarily unknown actors keep their row instead of making the list jump.");

        ImGui.Separator();
        ImGui.TextUnformatted("Layout");
        changed |= Slider(
            "Enemy stack horizontal position",
            configuration.CombatFramesEnemyScreenX,
            0.02f,
            0.98f,
            value => configuration.CombatFramesEnemyScreenX = value,
            "%.2f screen");
        changed |= Slider(
            "Enemy stack vertical position",
            configuration.CombatFramesEnemyScreenY,
            0.02f,
            0.98f,
            value => configuration.CombatFramesEnemyScreenY = value,
            "%.2f screen");
        changed |= Slider(
            "Self horizontal position",
            configuration.CombatFramesSelfScreenX,
            0.02f,
            0.98f,
            value => configuration.CombatFramesSelfScreenX = value,
            "%.2f screen");
        changed |= Slider(
            "Self vertical position",
            configuration.CombatFramesSelfScreenY,
            0.02f,
            0.98f,
            value => configuration.CombatFramesSelfScreenY = value,
            "%.2f screen");
        changed |= Slider(
            "Combat Frames scale",
            configuration.CombatFramesScale,
            0.55f,
            1.8f,
            value => configuration.CombatFramesScale = value,
            "%.2f x");
        changed |= Slider(
            "Frame background opacity",
            configuration.CombatFramesBackgroundOpacity,
            0.35f,
            1f,
            value => configuration.CombatFramesBackgroundOpacity = value,
            "%.2f");

        ImGui.Separator();
        ImGui.TextUnformatted("Optional clean Seiton layout");
        ImGui.TextDisabled(
            "This only disables older Seiton overlays which duplicate information shown in Combat Frames. " +
            "It does not touch the native FFXIV HUD and does not change any action helper.");
        if (ImGui.Button("Disable duplicate Seiton overlays"))
        {
            configuration.ApplyCombatFramesCleanPreset();
            changed = true;
        }

        return changed;
    }
}
