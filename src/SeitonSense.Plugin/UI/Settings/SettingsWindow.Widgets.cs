using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace SeitonSense.Plugin.UI;

internal sealed partial class SettingsWindow
{
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
