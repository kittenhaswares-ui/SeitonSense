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
    private readonly OverlayRenderer overlay;

    public SettingsWindow(
        PluginConfiguration configuration,
        ExecuteTracker tracker,
        OverlayRenderer overlay)
        : base("Seiton Sense###SeitonSenseSettings")
    {
        this.configuration = configuration;
        this.tracker = tracker;
        this.overlay = overlay;
        Size = new Vector2(585f, 600f);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        var changed = false;

        ImGui.TextColored(new Vector4(0.98f, 0.2f, 0.48f, 1f), "STABLE PVP NAMEPLATE INFO");
        ImGui.TextWrapped(
            "Active for every job in Crystalline Conflict. Extra icons are anchored to the game's native " +
            "job icon above each enemy instead of projecting a separate marker from the 3D world.");
        ImGui.TextWrapped(
            "Ninja additionally gets a one-shot job-icon + S1-S5 popup when a target enters a stable " +
            "Seiton execute window.");

        ImGui.Spacing();
        changed |= Checkbox("Enable Seiton Sense", configuration.Enabled, value => configuration.Enabled = value);

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
        ImGui.TextUnformatted("Seiton popup");
        changed |= Checkbox(
            "Job icon + S1-S5 popup",
            configuration.ShowSeitonPopup,
            value => configuration.ShowSeitonPopup = value);
        changed |= Slider("Popup duration", configuration.PopupDurationMilliseconds, 300f, 2000f, value => configuration.PopupDurationMilliseconds = value, "%.0f ms");
        changed |= Slider("Popup size", configuration.PopupIconSize, 36f, 140f, value => configuration.PopupIconSize = value, "%.0f px");
        changed |= Slider("Popup horizontal position", configuration.PopupScreenX, 0.05f, 0.95f, value => configuration.PopupScreenX = value, "%.2f");
        changed |= Slider("Popup vertical position", configuration.PopupScreenY, 0.05f, 0.9f, value => configuration.PopupScreenY = value, "%.2f");
        changed |= Slider("Popup background", configuration.PopupBackgroundOpacity, 0f, 1f, value => configuration.PopupBackgroundOpacity = value, "%.2f");

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

        ImGui.Spacing();
        ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
        ImGui.TextDisabled(
            "Guard cooldown is shown only after this client actually observed that enemy's Guard. Unknown " +
            "cooldowns are never guessed. The plugin does not target, press actions, change game UI nodes, " +
            "or send gameplay data.");
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
}
