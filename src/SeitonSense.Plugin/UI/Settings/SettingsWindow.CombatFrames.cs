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
        changed |= Checkbox(
            "Enemy rows: click to target + native <mo> on hover",
            configuration.CombatFramesEnableInteraction,
            value => configuration.CombatFramesEnableInteraction = value);

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
            "Fresh living enemy rows are interactive: left-clicking one row makes exactly that canonical S-slot " +
            "your hard target, and hovering publishes that exact actor to FFXIV's native <mo> target slots. The " +
            "self frame, preview, dead or unknown rows, stale snapshots, and gaps remain click-through. A click is " +
            "revalidated and written once with no retry; external mouseover replacement always wins. No soft or " +
            "focus target is changed.");
        ImGui.TextDisabled(
            "Unknown HP, MP, slot, pressure, or status data stays visibly unknown instead of being guessed. The " +
            "native FFXIV HUD is never hidden or edited; hide its parameter/enemy-list elements manually in HUD " +
            "Layout if you want these frames to visually replace them.");
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
        changed |= Checkbox(
            "Show LB gauges, activations, and live countdowns",
            configuration.CombatFramesShowLimitBreaks,
            value => configuration.CombatFramesShowLimitBreaks = value);
        changed |= Checkbox(
            "Show direct ally LB damage events",
            configuration.ShowAllyLimitBreakDamageEvents,
            value => configuration.ShowAllyLimitBreakDamageEvents = value);
        ImGui.TextDisabled(
            "MP bars use trusted observations and 2,000-MP divisions. Enemy order is always canonical S1-S5; " +
            "dead or temporarily unknown actors keep their row instead of making the list jump.");
        ImGui.TextDisabled(
            "Self uses the exact native LB controller gauge. S1-S5 stay LB ? until the current native HUD instance " +
            "has completed live calibration against Self; charge time is never estimated. Exact activation evidence " +
            "opens the LB card. A duration countdown originates only from a matching live RemainingTime value. One " +
            "missing sample of at most 150 ms may preserve the last exact expiry but never extend it; instant LBs use " +
            "a fixed 1.8-second card.");
        ImGui.TextDisabled(
            "The ally feed shows only direct damage amounts attributed to an exact ally caster and reviewed LB action " +
            "in ActionEffect. It never infers damage from HP changes; pet, periodic, or ambiguous attribution stays " +
            "silent. These detail switches do not enable the Combat Frames master.");

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
