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
            "Stun and Miracle of Nature",
            configuration.WarnPurifiableCrowdControl,
            value => configuration.WarnPurifiableCrowdControl = value);
        changed |= Slider("Warning horizontal position", configuration.PersonalWarningScreenX, 0.05f, 0.95f, value => configuration.PersonalWarningScreenX = value, "%.2f");
        changed |= Slider("Warning vertical position", configuration.PersonalWarningScreenY, 0.08f, 0.9f, value => configuration.PersonalWarningScreenY = value, "%.2f");
        changed |= Slider("Warning scale", configuration.PersonalWarningScale, 0.55f, 1.8f, value => configuration.PersonalWarningScale = value, "%.2f x");

        ImGui.Spacing();
        ImGui.TextColored(new Vector4(1f, 0.7f, 0.12f, 1f), "EXPERIMENTAL PURIFY BUFFER");
        changed |= Checkbox(
            "Use one Purify attempt on the next fresh gameplay key",
            configuration.ExperimentalPurifyOnNextKey,
            value => configuration.ExperimentalPurifyOnNextKey = value);
        changed |= SliderInt(
            "Maximum wait after that key",
            configuration.ExperimentalPurifyBufferMilliseconds,
            100,
            1000,
            value => configuration.ExperimentalPurifyBufferMilliseconds = value,
            "%d ms");
        ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
        ImGui.TextDisabled(
            "Only exact Stun or Miracle of Nature can arm this. A key that was already held does not count. " +
            "The original key is not swallowed. Purify is attempted once at the first locally usable moment; " +
            "there is no retry after rejection, timeout, death, chat focus, or leaving the supported PvP context. Disable " +
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
            $"ready={personal.Purify.LocallyReady}, key={personal.Purify.FreshGameplayKey}, " +
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
