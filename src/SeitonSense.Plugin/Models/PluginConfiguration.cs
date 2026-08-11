using Dalamud.Configuration;
using Dalamud.Plugin;

namespace SeitonSense.Plugin.Models;

public sealed class PluginConfiguration : IPluginConfiguration
{
    public int Version { get; set; } = 2;
    public bool Enabled { get; set; } = true;
    public bool ShowNameplateSeiton { get; set; } = true;
    public bool ShowGuardUnavailable { get; set; } = true;
    public bool ShowGuardCountdown { get; set; } = true;
    public bool ShowLowMp { get; set; } = true;
    public bool ShowSeitonPopup { get; set; } = true;
    public float NameplateIconScale { get; set; } = 0.92f;
    public float NameplateIconSpacing { get; set; } = 2f;
    public float NameplateBackgroundOpacity { get; set; } = 0.84f;
    public float PopupDurationMilliseconds { get; set; } = 850f;
    public float PopupIconSize { get; set; } = 76f;
    public float PopupScreenX { get; set; } = 0.5f;
    public float PopupScreenY { get; set; } = 0.2f;
    public float PopupBackgroundOpacity { get; set; } = 0.88f;

    [NonSerialized]
    private IDalamudPluginInterface? pluginInterface;

    public void Initialize(IDalamudPluginInterface value)
    {
        pluginInterface = value;
        if (Version >= 2) return;

        // v0.2 replaces the world-projected overlay with native-nameplate anchors.
        // Newly added properties already carry the new safe defaults.
        Version = 2;
        Save();
    }

    public void Save() => pluginInterface?.SavePluginConfig(this);

    public void ResetToDefaults()
    {
        Version = 2;
        Enabled = true;
        ShowNameplateSeiton = true;
        ShowGuardUnavailable = true;
        ShowGuardCountdown = true;
        ShowLowMp = true;
        ShowSeitonPopup = true;
        NameplateIconScale = 0.92f;
        NameplateIconSpacing = 2f;
        NameplateBackgroundOpacity = 0.84f;
        PopupDurationMilliseconds = 850f;
        PopupIconSize = 76f;
        PopupScreenX = 0.5f;
        PopupScreenY = 0.2f;
        PopupBackgroundOpacity = 0.88f;
    }
}
