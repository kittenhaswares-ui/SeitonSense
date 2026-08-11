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
        Size = new Vector2(535f, 535f);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        var changed = false;

        ImGui.TextColored(new Vector4(0.98f, 0.2f, 0.48f, 1f), "S1  S2  S3  S4  S5");
        ImGui.TextWrapped(
            "Only active while you are Ninja in Crystalline Conflict. S1-S5 are read from the game's exact " +
            "enemy slots <e1>-<e5>, matching Shift+1 through Shift+5 bindings.");
        ImGui.TextWrapped(
            "A label appears only when that enemy is below 50% HP, passes Seiton's native 20-yalm " +
            "range and line-of-sight check, and the currently adjusted Seiton action is usable.");

        ImGui.Spacing();
        changed |= Checkbox("Enable Seiton Sense", configuration.Enabled, value => configuration.Enabled = value);

        ImGui.Separator();
        ImGui.TextUnformatted("Alerts");
        changed |= Checkbox(
            "Overhead S-slot labels",
            configuration.ShowOverheadLabels,
            value => configuration.ShowOverheadLabels = value);
        changed |= Checkbox(
            "One-shot screen flash",
            configuration.ShowScreenFlash,
            value => configuration.ShowScreenFlash = value);
        ImGui.SameLine();
        changed |= Checkbox(
            "Slot text in flash",
            configuration.ShowFlashSlotText,
            value => configuration.ShowFlashSlotText = value);
        changed |= Checkbox(
            "HP percent below label",
            configuration.ShowHpPercent,
            value => configuration.ShowHpPercent = value);

        ImGui.Separator();
        ImGui.TextUnformatted("Appearance");
        changed |= Slider("Label size", configuration.LabelScale, 0.8f, 3f, value => configuration.LabelScale = value, "%.2f x");
        changed |= Slider("Height above player", configuration.WorldHeight, 1.2f, 4f, value => configuration.WorldHeight = value, "%.2f");
        changed |= Slider("Background opacity", configuration.BackgroundOpacity, 0f, 1f, value => configuration.BackgroundOpacity = value, "%.2f");
        changed |= Slider("Flash duration", configuration.FlashDurationMilliseconds, 200f, 1000f, value => configuration.FlashDurationMilliseconds = value, "%.0f ms");
        changed |= Slider("Flash intensity", configuration.FlashIntensity, 0.1f, 1f, value => configuration.FlashIntensity = value, "%.2f");

        ImGui.Spacing();
        if (ImGui.Button(overlay.PreviewEnabled ? "Stop preview" : "Preview S1-S5"))
            overlay.PreviewEnabled = !overlay.PreviewEnabled;
        ImGui.SameLine();
        if (ImGui.Button("Preview flash")) overlay.TriggerPreviewFlash();
        ImGui.SameLine();
        if (ImGui.Button("Reset defaults"))
        {
            configuration.ResetToDefaults();
            overlay.PreviewEnabled = false;
            changed = true;
        }

        ImGui.Separator();
        ImGui.TextUnformatted("Live diagnostics");
        ImGui.TextWrapped(tracker.Diagnostics.ToChatLine());

        ImGui.Spacing();
        ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
        ImGui.TextDisabled(
            "The alert reports the execute window only. It does not target, press Seiton, predict damage, " +
            "or guarantee line of sight. Display only; no networking or gameplay uploads.");
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
        ImGui.SetNextItemWidth(260f * ImGuiHelpers.GlobalScale);
        if (!ImGui.SliderFloat(label, ref value, minimum, maximum, format)) return false;
        apply(value);
        return true;
    }
}
