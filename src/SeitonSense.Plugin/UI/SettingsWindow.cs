using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using SeitonSense.Plugin.Models;
using SeitonSense.Plugin.Services;

namespace SeitonSense.Plugin.UI;

internal sealed class SettingsWindow : Window
{
    private readonly PluginConfiguration configuration;
    private readonly ExecuteTracker tracker;
    private readonly PersonalStatusService personalStatus;
    private readonly OverlayRenderer overlay;

    public SettingsWindow(
        PluginConfiguration configuration,
        ExecuteTracker tracker,
        PersonalStatusService personalStatus,
        OverlayRenderer overlay)
        : base("Seiton Sense###SeitonSenseSettings")
    {
        this.configuration = configuration;
        this.tracker = tracker;
        this.personalStatus = personalStatus;
        this.overlay = overlay;
        Size = new Vector2(640f, 760f);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        var changed = false;

        ImGui.TextColored(new Vector4(0.98f, 0.2f, 0.48f, 1f), "PVP REACTION CUES");
        ImGui.TextWrapped(
            "Active for every job in Crystalline Conflict and, while the test option is enabled, Wolves' Den " +
            "duels. Extra icons are anchored to the game's native job icon above each enemy.");
        ImGui.TextWrapped(
            "Ninja additionally gets a persistent, center-adjacent SHIFT + 1-5 cue while a target is " +
            "inside the verified Seiton window. The short pop is only the entry signal.");

        ImGui.Spacing();
        changed |= Checkbox("Enable Seiton Sense", configuration.Enabled, value => configuration.Enabled = value);
        changed |= Checkbox(
            "Enable Wolves' Den duel testing",
            configuration.EnableWolvesDenTesting,
            value => configuration.EnableWolvesDenTesting = value);
        ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
        ImGui.TextDisabled(
            "FFXIV's exact hostile duel opponent is shown as synthetic S1, including party-member duels. " +
            "This is only a visual label; the CC <e1> macro placeholder may not exist in a duel. " +
            "Frontline and Rival Wings stay excluded.");
        ImGui.PopTextWrapPos();

        ImGui.Separator();
        if (ImGui.CollapsingHeader("Focus and current-target highlights", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.TextWrapped(
                "Focus Glow is the former Super Focus Glow renderer inside Seiton Sense. Current Target reads " +
                "only your manually selected hard target; it never selects or changes a target.");

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
                ImGui.TreePop();
            }

            ImGui.Spacing();
            changed |= Checkbox(
                "Highlight current hard target",
                configuration.EnableCurrentTargetHighlight,
                value => configuration.EnableCurrentTargetHighlight = value);
            ImGui.SameLine();
            if (ImGui.Button("Restore target preset"))
            {
                configuration.ApplyCurrentTargetHighlightPreset();
                changed = true;
            }

            changed |= Checkbox(
                "Current target: PvP only",
                configuration.CurrentTargetPvPOnly,
                value => configuration.CurrentTargetPvPOnly = value);
            ImGui.SameLine();
            changed |= Checkbox(
                "Current target: foreground",
                configuration.CurrentTargetDrawInForeground,
                value => configuration.CurrentTargetDrawInForeground = value);
            changed |= Checkbox(
                "Separate fixed target-information HUD",
                configuration.ShowCurrentTargetInfoHud,
                value => configuration.ShowCurrentTargetInfoHud = value);

            ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
            ImGui.TextDisabled(
                "The target-information HUD is a separate fixed card. Nothing from this module is attached to " +
                "nameplates, native job icons, native health bars, or Seiton's Guard/MP/Seiton indicator slots.");
            ImGui.PopTextWrapPos();

            changed |= Slider("Target HUD horizontal position", configuration.CurrentTargetInfoScreenX, 0.02f, 0.98f, value => configuration.CurrentTargetInfoScreenX = value, "%.2f");
            changed |= Slider("Target HUD vertical position", configuration.CurrentTargetInfoScreenY, 0.02f, 0.98f, value => configuration.CurrentTargetInfoScreenY = value, "%.2f");
            changed |= Slider("Target HUD scale", configuration.CurrentTargetInfoScale, 0.55f, 1.8f, value => configuration.CurrentTargetInfoScale = value, "%.2f x");

            if (ImGui.TreeNode("Current-target appearance"))
            {
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
                ImGui.TreePop();
            }
        }

        ImGui.Separator();
        ImGui.TextUnformatted("Nameplate indicators");
        changed |= Checkbox(
            "Seiton-ready icon + S-slot (NIN)",
            configuration.ShowNameplateSeiton,
            value => configuration.ShowNameplateSeiton = value);
        changed |= Checkbox(
            "Crossed Guard while observed on cooldown",
            configuration.ShowGuardUnavailable,
            value => configuration.ShowGuardUnavailable = value);
        ImGui.SameLine();
        changed |= Checkbox(
            "Countdown",
            configuration.ShowGuardCountdown,
            value => configuration.ShowGuardCountdown = value);
        changed |= Checkbox(
            "Crossed blue elixir below 2,000 MP",
            configuration.ShowLowMp,
            value => configuration.ShowLowMp = value);

        ImGui.Separator();
        ImGui.TextUnformatted("Seiton decision cue (NIN)");
        changed |= Checkbox(
            "Persistent SHIFT + slot cue",
            configuration.ShowPersistentSeitonCue,
            value => configuration.ShowPersistentSeitonCue = value);
        changed |= Checkbox(
            "Show PREP between 50% and 60% HP",
            configuration.ShowSeitonPreparation,
            value => configuration.ShowSeitonPreparation = value);
        var keyLabel = configuration.SeitonKeyLabel ?? string.Empty;
        ImGui.SetNextItemWidth(150f * ImGuiHelpers.GlobalScale);
        if (ImGui.InputText("Key label", ref keyLabel, 12))
        {
            configuration.SeitonKeyLabel = keyLabel;
            changed = true;
        }
        changed |= Slider("Persistent cue scale", configuration.PersistentCueScale, 0.55f, 1.8f, value => configuration.PersistentCueScale = value, "%.2f x");

        changed |= Checkbox(
            "Entry pop animation",
            configuration.ShowSeitonPopup,
            value => configuration.ShowSeitonPopup = value);
        changed |= Slider("Popup duration", configuration.PopupDurationMilliseconds, 300f, 2000f, value => configuration.PopupDurationMilliseconds = value, "%.0f ms");
        changed |= Slider("Entry pop size", configuration.PopupIconSize, 48f, 140f, value => configuration.PopupIconSize = value, "%.0f px");
        changed |= Slider("Cue horizontal position", configuration.PopupScreenX, 0.05f, 0.95f, value => configuration.PopupScreenX = value, "%.2f");
        changed |= Slider("Cue vertical position", configuration.PopupScreenY, 0.08f, 0.9f, value => configuration.PopupScreenY = value, "%.2f");
        changed |= Slider("Cue background", configuration.PopupBackgroundOpacity, 0f, 1f, value => configuration.PopupBackgroundOpacity = value, "%.2f");

        ImGui.Separator();
        ImGui.TextUnformatted("Warnings on you");
        changed |= Checkbox(
            "Show personal debuff warnings",
            configuration.ShowPersonalWarnings,
            value => configuration.ShowPersonalWarnings = value);
        changed |= Checkbox(
            "Wildfire",
            configuration.WarnWildfire,
            value => configuration.WarnWildfire = value);
        ImGui.SameLine();
        changed |= Checkbox(
            "Death Warrant / Richtbefehl",
            configuration.WarnDeathWarrant,
            value => configuration.WarnDeathWarrant = value);
        changed |= Checkbox(
            "All Purify-removable debuff warnings",
            configuration.WarnPurifiableCrowdControl,
            value => configuration.WarnPurifiableCrowdControl = value);
        changed |= Slider("Warning horizontal position", configuration.PersonalWarningScreenX, 0.05f, 0.95f, value => configuration.PersonalWarningScreenX = value, "%.2f");
        changed |= Slider("Warning vertical position", configuration.PersonalWarningScreenY, 0.08f, 0.9f, value => configuration.PersonalWarningScreenY = value, "%.2f");
        changed |= Slider("Warning scale", configuration.PersonalWarningScale, 0.55f, 1.8f, value => configuration.PersonalWarningScale = value, "%.2f x");

        ImGui.Spacing();
        ImGui.TextColored(new Vector4(1f, 0.7f, 0.12f, 1f), "EXPERIMENTAL PURIFY ON NEXT KEY");
        changed |= Checkbox(
            "Use one Purify attempt on the next fresh gameplay key",
            configuration.ExperimentalPurifyOnNextKey,
            value => configuration.ExperimentalPurifyOnNextKey = value);
        changed |= Checkbox(
            "A held gameplay key may trigger when the debuff appears (includes WASD)",
            configuration.PurifyOnHeldGameplayKey,
            value => configuration.PurifyOnHeldGameplayKey = value);
        ImGui.TextUnformatted("Trigger separately for:");
        changed |= Checkbox("Stun", configuration.PurifyOnStun, value => configuration.PurifyOnStun = value);
        ImGui.SameLine();
        changed |= Checkbox("Heavy", configuration.PurifyOnHeavy, value => configuration.PurifyOnHeavy = value);
        ImGui.SameLine();
        changed |= Checkbox("Bind", configuration.PurifyOnBind, value => configuration.PurifyOnBind = value);
        changed |= Checkbox("Silence", configuration.PurifyOnSilence, value => configuration.PurifyOnSilence = value);
        ImGui.SameLine();
        changed |= Checkbox("Deep Freeze", configuration.PurifyOnDeepFreeze, value => configuration.PurifyOnDeepFreeze = value);
        ImGui.SameLine();
        changed |= Checkbox(
            "Miracle of Nature",
            configuration.PurifyOnMiracleOfNature,
            value => configuration.PurifyOnMiracleOfNature = value);
        ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
        ImGui.TextDisabled(
            "Only the exact enabled debuff types can trigger this. By default, an already-held key does not count. " +
            "Enable the separate held-key option if a physical key pressed before the debuff should count once. " +
            "ReAction Turbo pulses do not create new physical presses. The original key is not swallowed. Seiton Sense " +
            "sends one native Purify attempt immediately, and FFXIV decides whether it can queue or execute it. " +
            "The same physical hold cannot trigger again until released, and there is no retry after rejection. Disable " +
            "rules in other plugins that rewrite Purify or its target while testing.");
        ImGui.PopTextWrapPos();

        ImGui.Separator();
        ImGui.TextUnformatted("Nameplate appearance");
        changed |= Slider("Extra icon size", configuration.NameplateIconScale, 0.55f, 1.5f, value => configuration.NameplateIconScale = value, "%.2f x native");
        changed |= Slider("Extra icon spacing", configuration.NameplateIconSpacing, 0f, 12f, value => configuration.NameplateIconSpacing = value, "%.1f px");
        changed |= Slider("Extra icon background", configuration.NameplateBackgroundOpacity, 0f, 1f, value => configuration.NameplateBackgroundOpacity = value, "%.2f");

        ImGui.Spacing();
        if (ImGui.Button(overlay.PreviewEnabled ? "Stop preview" : "Preview nameplate"))
            overlay.PreviewEnabled = !overlay.PreviewEnabled;
        ImGui.SameLine();
        if (ImGui.Button("Preview Seiton popup")) overlay.TriggerPreviewPopup();
        ImGui.SameLine();
        if (ImGui.Button("Reset defaults"))
        {
            configuration.ResetToDefaults();
            overlay.PreviewEnabled = false;
            changed = true;
        }

        ImGui.Separator();
        ImGui.TextUnformatted("Live diagnostics");
        ImGui.TextWrapped($"{tracker.Diagnostics.ToChatLine()}, native-anchors={overlay.NativeAnchorCount}");
        var personal = personalStatus.Snapshot;
        ImGui.TextWrapped(
            $"Personal statuses={personal.Statuses.Length}, Purify={personal.Purify.Phase}/" +
            $"{personal.Purify.Decision}, cancel={personal.Purify.CancelReason}, " +
            $"trigger={personal.Purify.InputTrigger}, ready={personal.Purify.LocallyReady}, " +
            $"fresh={personal.Purify.FreshGameplayKey}, held={personal.Purify.HeldGameplayKey}, " +
            $"attempt={personal.Purify.UseActionAttempted}/{personal.Purify.UseActionAccepted}, " +
            $"buffered={personal.Purify.BufferRemainingMilliseconds} ms");

        ImGui.Spacing();
        ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
        ImGui.TextDisabled(
            "Guard cooldown is shown only after this client actually observed that enemy's Guard. Unknown " +
            "cooldowns are never guessed. Seiton Sense never changes a target and uploads no gameplay data " +
            "to an external service. The optional Purify experiment is the only feature that can request an " +
            "action, and it is disabled by default. Like all third-party modifications, use it at your own risk.");
        ImGui.PopTextWrapPos();

        if (changed) configuration.Save();
    }

    public override void OnClose() => overlay.PreviewEnabled = false;

    private static bool Checkbox(string label, bool current, Action<bool> apply)
    {
        var value = current;
        if (!ImGui.Checkbox(label, ref value)) return false;
        apply(value);
        return true;
    }

    private static bool Slider(
        string label,
        float current,
        float minimum,
        float maximum,
        Action<float> apply,
        string format)
    {
        var value = current;
        ImGui.SetNextItemWidth(270f * ImGuiHelpers.GlobalScale);
        if (!ImGui.SliderFloat(label, ref value, minimum, maximum, format)) return false;
        apply(value);
        return true;
    }

    private static bool SliderInt(
        string label,
        int current,
        int minimum,
        int maximum,
        Action<int> apply,
        string format)
    {
        var value = current;
        ImGui.SetNextItemWidth(270f * ImGuiHelpers.GlobalScale);
        if (!ImGui.SliderInt(label, ref value, minimum, maximum, format)) return false;
        apply(value);
        return true;
    }
}
