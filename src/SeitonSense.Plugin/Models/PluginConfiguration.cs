using Dalamud.Configuration;
using Dalamud.Plugin;

namespace SeitonSense.Plugin.Models;

public sealed class PluginConfiguration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public bool Enabled { get; set; } = true;
    public bool ShowOverheadLabels { get; set; } = true;
    public bool ShowScreenFlash { get; set; } = true;
    public bool ShowFlashSlotText { get; set; } = true;
    public bool ShowHpPercent { get; set; }
    public float LabelScale { get; set; } = 1.65f;
    public float WorldHeight { get; set; } = 2.55f;
    public float BackgroundOpacity { get; set; } = 0.82f;
    public float FlashDurationMilliseconds { get; set; } = 400f;
    public float FlashIntensity { get; set; } = 0.82f;

    [NonSerialized]
    private IDalamudPluginInterface? pluginInterface;

    public void Initialize(IDalamudPluginInterface value)
    {
        pluginInterface = value;
        Version = 1;
    }

    public void Save() => pluginInterface?.SavePluginConfig(this);

    public void ResetToDefaults()
    {
        Version = 1;
        Enabled = true;
        ShowOverheadLabels = true;
        ShowScreenFlash = true;
        ShowFlashSlotText = true;
        ShowHpPercent = false;
        LabelScale = 1.65f;
        WorldHeight = 2.55f;
        BackgroundOpacity = 0.82f;
        FlashDurationMilliseconds = 400f;
        FlashIntensity = 0.82f;
    }
}
