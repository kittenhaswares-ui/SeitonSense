using Dalamud.Configuration;
using Dalamud.Plugin;

namespace SeitonSense.Plugin.Models;

public sealed class PluginConfiguration : IPluginConfiguration
{
    public int Version { get; set; } = 6;
    public bool Enabled { get; set; } = true;
    public bool EnableWolvesDenTesting { get; set; } = true;
    public bool ShowNameplateSeiton { get; set; } = true;
    public bool ShowGuardUnavailable { get; set; } = true;
    public bool ShowGuardCountdown { get; set; } = true;
    public bool ShowLowMp { get; set; } = true;
    public bool ShowSeitonPopup { get; set; } = true;
    public bool ShowPersistentSeitonCue { get; set; } = true;
    public bool ShowSeitonPreparation { get; set; } = true;
    public string SeitonKeyLabel { get; set; } = "SHIFT";
    public float NameplateIconScale { get; set; } = 0.92f;
    public float NameplateIconSpacing { get; set; } = 2f;
    public float NameplateBackgroundOpacity { get; set; } = 0.84f;
    public float PopupDurationMilliseconds { get; set; } = 850f;
    public float PopupIconSize { get; set; } = 96f;
    public float PopupScreenX { get; set; } = 0.5f;
    public float PopupScreenY { get; set; } = 0.55f;
    public float PopupBackgroundOpacity { get; set; } = 0.88f;
    public float PersistentCueScale { get; set; } = 1f;
    public bool ShowPersonalWarnings { get; set; } = true;
    public bool WarnWildfire { get; set; } = true;
    public bool WarnDeathWarrant { get; set; } = true;
    public bool WarnPurifiableCrowdControl { get; set; } = true;
    public float PersonalWarningScreenX { get; set; } = 0.5f;
    public float PersonalWarningScreenY { get; set; } = 0.34f;
    public float PersonalWarningScale { get; set; } = 1f;
    public bool ExperimentalPurifyOnNextKey { get; set; }
    public int ExperimentalPurifyBufferMilliseconds { get; set; } = 750;
    public bool PurifyOnHeldGameplayKey { get; set; }
    public bool PurifyOnStun { get; set; } = true;
    public bool PurifyOnHeavy { get; set; } = true;
    public bool PurifyOnBind { get; set; } = true;
    public bool PurifyOnSilence { get; set; } = true;
    public bool PurifyOnDeepFreeze { get; set; } = true;
    public bool PurifyOnMiracleOfNature { get; set; } = true;

    [NonSerialized]
    private IDalamudPluginInterface? pluginInterface;

    public void Initialize(IDalamudPluginInterface value)
    {
        pluginInterface = value;
        if (Version >= 6) return;

        if (Version < 3)
        {
            if (Math.Abs(PopupScreenY - 0.2f) < 0.001f) PopupScreenY = 0.55f;
            if (Math.Abs(PopupIconSize - 76f) < 0.001f) PopupIconSize = 96f;
        }

        if (Version < 4)
        {
            // This release is explicitly a Wolves' Den test build. Existing users get
            // the test context immediately, while the separate Purify experiment stays off.
            EnableWolvesDenTesting = true;
        }

        if (Version < 5)
        {
            PurifyOnStun = true;
            PurifyOnHeavy = true;
            PurifyOnBind = true;
            PurifyOnSilence = true;
            PurifyOnDeepFreeze = true;
            PurifyOnMiracleOfNature = true;
        }

        if (Version < 6)
        {
            // Held-key activation is a separate, deliberately explicit opt-in.
            // Existing users keep the proven fresh-key behavior after updating.
            PurifyOnHeldGameplayKey = false;
        }

        Version = 6;
        Save();
    }

    public void Save() => pluginInterface?.SavePluginConfig(this);

    public void ResetToDefaults()
    {
        Version = 6;
        Enabled = true;
        EnableWolvesDenTesting = true;
        ShowNameplateSeiton = true;
        ShowGuardUnavailable = true;
        ShowGuardCountdown = true;
        ShowLowMp = true;
        ShowSeitonPopup = true;
        ShowPersistentSeitonCue = true;
        ShowSeitonPreparation = true;
        SeitonKeyLabel = "SHIFT";
        NameplateIconScale = 0.92f;
        NameplateIconSpacing = 2f;
        NameplateBackgroundOpacity = 0.84f;
        PopupDurationMilliseconds = 850f;
        PopupIconSize = 96f;
        PopupScreenX = 0.5f;
        PopupScreenY = 0.55f;
        PopupBackgroundOpacity = 0.88f;
        PersistentCueScale = 1f;
        ShowPersonalWarnings = true;
        WarnWildfire = true;
        WarnDeathWarrant = true;
        WarnPurifiableCrowdControl = true;
        PersonalWarningScreenX = 0.5f;
        PersonalWarningScreenY = 0.34f;
        PersonalWarningScale = 1f;
        ExperimentalPurifyOnNextKey = false;
        ExperimentalPurifyBufferMilliseconds = 750;
        PurifyOnHeldGameplayKey = false;
        PurifyOnStun = true;
        PurifyOnHeavy = true;
        PurifyOnBind = true;
        PurifyOnSilence = true;
        PurifyOnDeepFreeze = true;
        PurifyOnMiracleOfNature = true;
    }
}
