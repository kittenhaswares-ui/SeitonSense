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
            "Focus Glow renders FFXIV's native Focus Target. Current Target reads only your manually selected hard " +
            "target. Smart Tab and the separate low-MP Focus helper are the only opt-ins on this page that may set " +
            "a local target.");

        ImGui.TextColored(new Vector4(0.35f, 0.88f, 1f, 1f), "MELEE SMART TAB (OPT-IN)");
        changed |= Checkbox(
            "Replace FFXIV's forward Tab targeting with Smart Targeting",
            configuration.EnableSmartTabTargeting,
            value => configuration.EnableSmartTabTargeting = value);
        ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
        ImGui.TextDisabled(
            "Toggle ON replaces FFXIV's normal forward-target command in exact Crystalline Conflict on reviewed " +
            "melee jobs. Your usual Tab key and any remapped forward-target binding use Smart Targeting directly. " +
            "Toggle OFF is fully vanilla. Shift+Tab/reverse targeting, chat/UI Tab input, other targeting commands, " +
            "unsupported jobs, and all other content remain unchanged. /smarttab and /sstarget toggle this option.");
        ImGui.TextDisabled(
            "One press first considers enemies within 5 yalms of hitbox-edge melee reach, then only enemies inside " +
            "the reviewed range of that melee job's gap closer. Inside the first non-empty tier it ranks lowest HP%, " +
            "highest fresh team pressure, observed Wehr cooldown unavailable, lowest trusted MP%, then stable S-slot. " +
            "An enemy with live Wehr is excluded.");
        ImGui.TextDisabled(
            "Because no combat action is being attempted, Smart Tab uses exact geometric reach rather than pretending " +
            "to have an action-specific native range/line-of-sight result. For an owned Tab press it freezes one exact " +
            "S1-S5 actor, revalidates it, sets the hard target once, and verifies readback. If no valid candidate " +
            "exists, the current target remains unchanged instead of running the vanilla cycle. There is no action, " +
            "retry, rerank, or alternate target.");
        ImGui.PopTextWrapPos();

        ImGui.Separator();

        ImGui.TextColored(new Vector4(1f, 0.72f, 0.3f, 1f), "NATIVE FOCUS TARGET SETTER (OPT-IN)");
        changed |= Checkbox(
            "Set an empty Focus Target to an exact enemy at 2,000 MP or lower",
            configuration.EnableAutoLowMpFocusTarget,
            value => configuration.EnableAutoLowMpFocusTarget = value);
        ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
        ImGui.TextDisabled(
            "Default off and exact Crystalline Conflict only. It requires a complete unique native S1-S5 view, " +
            "150 ms of trusted MP at 2,000 or lower, and FFXIV's native 20-yalm range/line-of-sight result. The " +
            "low-MP wave clears only after 150 ms at 2,300 MP or higher. If several enemies qualify, lowest MP " +
            "ratio wins, then lowest HP ratio and stable S-slot/identity.");
        ImGui.TextDisabled(
            "It can fill only an empty native Focus Target and never clears, replaces, restores, or retries one. " +
            "An occupied or manually changed Focus always wins. Any confirmed external change or clear after a " +
            "plugin-set Focus latches the manual override until the option is toggled off/on or a new exact match " +
            "lifetime begins.");
        ImGui.TextDisabled(
            "This local Focus Target feeds FFXIV's Focus Target HUD and <f>. It is independent of the party-visible " +
            "Attack1 sign and never changes your hard or soft target. FFXIV exposes no atomic compare-and-set API, " +
            "so the immediately adjacent empty-check/set/readback boundary still requires a live current-patch CC " +
            "A/B test.");
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
            "The target-information HUD is a separate fixed card. PvP-only, foreground, and target color are shared " +
            "with the world highlight. Restoring the shared preset preserves the HUD's enable, position, and scale, " +
            "but restores those shared visual settings. Nothing is attached to nameplates or native health bars.");
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
