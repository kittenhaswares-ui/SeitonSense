using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace SeitonSense.Plugin.UI;

internal sealed partial class SettingsWindow
{
    private bool DrawTargetsPage()
    {
        var changed = false;
        ImGui.Spacing();
        ImGui.TextWrapped(
            "Focus Glow follows your normal Focus Target. Current Target follows the enemy you selected. Only Smart " +
            "Tab and the low-MP Focus helper can choose a local target from this page.");

        ImGui.TextColored(new Vector4(0.35f, 0.88f, 1f, 1f), "SMART TAB (OPT-IN)");
        changed |= Checkbox(
            "Replace FFXIV's forward Tab targeting with Smart Targeting",
            configuration.EnableSmartTabTargeting,
            value => configuration.EnableSmartTabTargeting = value);
        ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
        ImGui.TextDisabled(
            "In Crystalline Conflict, this replaces forward Tab on supported DPS jobs. Your normal Tab binding keeps " +
            "working; it simply uses Seiton's target order. Turn it off for normal FFXIV targeting. Reverse Tab, chat " +
            "input, unsupported jobs, and other content are unchanged. /smarttab and /sstarget also toggle it.");
        ImGui.TextDisabled(
            "Melee jobs first search melee range, then their gap-closer range. Ranged jobs use their normal attack range. " +
            "Inside that range, Seiton prefers low HP, team focus, unavailable Guard, and low MP. Enemies currently in " +
            "Guard are skipped.");
        ImGui.TextDisabled(
            "BRD, MCH, BLM, SMN, RDM, and PCT use 25 yalms. DNC uses 15 yalms. Dead, untargetable, out-of-range, " +
            "or obstructed enemies are skipped.");
        ImGui.TextDisabled(
            "Pressing Tab again moves to the next valid enemy and wraps around. Manually choosing a target starts the " +
            "cycle from there. Smart Tab changes only your target; it never uses an action.");
        ImGui.PopTextWrapPos();

        ImGui.Separator();

        ImGui.TextColored(new Vector4(1f, 0.72f, 0.3f, 1f), "NATIVE FOCUS TARGET SETTER (OPT-IN)");
        changed |= Checkbox(
            "Set an empty Focus Target to an enemy at 2,000 MP or lower",
            configuration.EnableAutoLowMpFocusTarget,
            value => configuration.EnableAutoLowMpFocusTarget = value);
        ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
        ImGui.TextDisabled(
            "Off by default and CC only. If your Focus Target is empty, Seiton can set it to a visible enemy within " +
            "20 yalms who has 2,000 MP or less. It prefers lower MP, then lower HP.");
        ImGui.TextDisabled(
            "It never replaces or clears an existing Focus Target. If you change or clear the Focus yourself, Seiton " +
            "leaves it alone until the next match or until you turn this option off and on again.");
        ImGui.TextDisabled(
            "This uses FFXIV's normal Focus Target and <f>. It does not change your selected target or the shared " +
            "Attack1 team marker.");
        ImGui.PopTextWrapPos();

        ImGui.Separator();
        ImGui.TextUnformatted("Focus Target display");
        changed |= Checkbox(
            "Enable focus-target glow",
            configuration.EnableFocusGlow,
            value => configuration.EnableFocusGlow = value);
        ImGui.SameLine();
        if (ImGui.Button("Restore focus preset"))
        {
            configuration.ApplyFocusGlowPreset();
            changed = true;
        }

        changed |= Checkbox(
            "Focus: hide with game UI",
            configuration.FocusHideWithGameUi,
            value => configuration.FocusHideWithGameUi = value);
        ImGui.SameLine();
        changed |= Checkbox(
            "Focus: foreground",
            configuration.FocusDrawInForeground,
            value => configuration.FocusDrawInForeground = value);
        if (ImGui.TreeNode("Focus appearance"))
        {
            changed |= DrawFocusAppearance();
            ImGui.TreePop();
        }

        ImGui.Separator();
        ImGui.TextUnformatted("Current hard target");
        changed |= Checkbox(
            "Highlight current hard target",
            configuration.EnableCurrentTargetHighlight,
            value => configuration.EnableCurrentTargetHighlight = value);
        ImGui.SameLine();
        if (ImGui.Button("Restore shared target preset"))
        {
            configuration.ApplyCurrentTargetHighlightPreset();
            changed = true;
        }

        changed |= Checkbox(
            "Current target and target HUD: PvP only",
            configuration.CurrentTargetPvPOnly,
            value => configuration.CurrentTargetPvPOnly = value);
        ImGui.SameLine();
        changed |= Checkbox(
            "Current target and target HUD: foreground",
            configuration.CurrentTargetDrawInForeground,
            value => configuration.CurrentTargetDrawInForeground = value);
        changed |= Checkbox(
            "Separate fixed target-information HUD",
            configuration.ShowCurrentTargetInfoHud,
            value => configuration.ShowCurrentTargetInfoHud = value);
        ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
        ImGui.TextDisabled(
            "This is a separate movable target card. PvP-only, foreground, and color settings are shared with the " +
            "world highlight. Restoring the preset keeps the card enabled and keeps its position and size.");
        ImGui.PopTextWrapPos();
        changed |= Slider(
            "Target HUD horizontal position",
            configuration.CurrentTargetInfoScreenX,
            0.02f,
            0.98f,
            value => configuration.CurrentTargetInfoScreenX = value,
            "%.2f");
        changed |= Slider(
            "Target HUD vertical position",
            configuration.CurrentTargetInfoScreenY,
            0.02f,
            0.98f,
            value => configuration.CurrentTargetInfoScreenY = value,
            "%.2f");
        changed |= Slider(
            "Target HUD scale",
            configuration.CurrentTargetInfoScale,
            0.55f,
            1.8f,
            value => configuration.CurrentTargetInfoScale = value,
            "%.2f x");
        if (ImGui.TreeNode("Current-target appearance"))
        {
            changed |= DrawCurrentTargetAppearance();
            ImGui.TreePop();
        }

        return changed;
    }

    private bool DrawFocusAppearance()
    {
        var changed = false;
        changed |= Checkbox("Focus ground ring", configuration.FocusShowGroundRing, value => configuration.FocusShowGroundRing = value);
        ImGui.SameLine();
        changed |= Checkbox("Focus halo", configuration.FocusShowTargetHalo, value => configuration.FocusShowTargetHalo = value);
        changed |= Checkbox("Focus rotating rays", configuration.FocusShowRays, value => configuration.FocusShowRays = value);
        ImGui.SameLine();
        changed |= Checkbox("Focus chevrons", configuration.FocusShowChevron, value => configuration.FocusShowChevron = value);
        ImGui.SameLine();
        changed |= Checkbox("Focus label", configuration.FocusShowLabel, value => configuration.FocusShowLabel = value);
        changed |= Checkbox("Focus rainbow", configuration.FocusRainbowMode, value => configuration.FocusRainbowMode = value);
        ImGui.SameLine();
        changed |= Checkbox("Focus reduced motion", configuration.FocusReducedMotion, value => configuration.FocusReducedMotion = value);
        var focusColor = configuration.FocusGlowColor;
        if (ImGui.ColorEdit4("Focus color", ref focusColor))
        {
            configuration.FocusGlowColor = focusColor;
            changed = true;
        }

        changed |= Slider("Focus intensity", configuration.FocusIntensity, 0.25f, 2.5f, value => configuration.FocusIntensity = value, "%.2f");
        changed |= Slider("Focus size", configuration.FocusSizeScale, 0.6f, 2f, value => configuration.FocusSizeScale = value, "%.2f x");
        changed |= Slider("Focus halo radius", configuration.FocusAuraRadius, 24f, 120f, value => configuration.FocusAuraRadius = value, "%.0f px");
        changed |= Slider("Focus pulse speed", configuration.FocusPulseSpeed, 0.1f, 2f, value => configuration.FocusPulseSpeed = value, "%.2f Hz");
        changed |= Slider("Focus pulse strength", configuration.FocusPulseAmount, 0f, 0.45f, value => configuration.FocusPulseAmount = value, "%.2f");
        changed |= Slider("Focus hitbox padding", configuration.FocusGroundPadding, 0f, 4f, value => configuration.FocusGroundPadding = value, "%.2f yalm");
        changed |= Slider("Focus vertical offset", configuration.FocusVerticalOffset, -1f, 5f, value => configuration.FocusVerticalOffset = value, "%.2f yalm");
        return changed;
    }

    private bool DrawCurrentTargetAppearance()
    {
        var changed = false;
        changed |= Checkbox("Target ground ring", configuration.CurrentTargetShowGroundRing, value => configuration.CurrentTargetShowGroundRing = value);
        ImGui.SameLine();
        changed |= Checkbox("Target halo", configuration.CurrentTargetShowTargetHalo, value => configuration.CurrentTargetShowTargetHalo = value);
        changed |= Checkbox("Target rotating rays", configuration.CurrentTargetShowRays, value => configuration.CurrentTargetShowRays = value);
        ImGui.SameLine();
        changed |= Checkbox("Target chevrons", configuration.CurrentTargetShowChevron, value => configuration.CurrentTargetShowChevron = value);
        ImGui.SameLine();
        changed |= Checkbox("Target label", configuration.CurrentTargetShowLabel, value => configuration.CurrentTargetShowLabel = value);
        changed |= Checkbox("Target rainbow", configuration.CurrentTargetRainbowMode, value => configuration.CurrentTargetRainbowMode = value);
        ImGui.SameLine();
        changed |= Checkbox("Target reduced motion", configuration.CurrentTargetReducedMotion, value => configuration.CurrentTargetReducedMotion = value);
        var targetColor = configuration.CurrentTargetGlowColor;
        if (ImGui.ColorEdit4("Target color", ref targetColor))
        {
            configuration.CurrentTargetGlowColor = targetColor;
            changed = true;
        }

        changed |= Slider("Target intensity", configuration.CurrentTargetIntensity, 0.25f, 2.5f, value => configuration.CurrentTargetIntensity = value, "%.2f");
        changed |= Slider("Target size", configuration.CurrentTargetSizeScale, 0.6f, 2f, value => configuration.CurrentTargetSizeScale = value, "%.2f x");
        changed |= Slider("Target halo radius", configuration.CurrentTargetAuraRadius, 24f, 120f, value => configuration.CurrentTargetAuraRadius = value, "%.0f px");
        changed |= Slider("Target pulse speed", configuration.CurrentTargetPulseSpeed, 0.1f, 2f, value => configuration.CurrentTargetPulseSpeed = value, "%.2f Hz");
        changed |= Slider("Target pulse strength", configuration.CurrentTargetPulseAmount, 0f, 0.45f, value => configuration.CurrentTargetPulseAmount = value, "%.2f");
        changed |= Slider("Target hitbox padding", configuration.CurrentTargetGroundPadding, 0f, 4f, value => configuration.CurrentTargetGroundPadding = value, "%.2f yalm");
        changed |= Slider("Target vertical offset", configuration.CurrentTargetVerticalOffset, -1f, 5f, value => configuration.CurrentTargetVerticalOffset = value, "%.2f yalm");
        return changed;
    }
}
