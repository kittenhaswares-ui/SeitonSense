using System.Numerics;
using Dalamud.Configuration;
using Dalamud.Plugin;

namespace SeitonSense.Plugin.Models;

public sealed class PluginConfiguration : IPluginConfiguration
{
    public int Version { get; set; } = 14;
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
    public bool WarnMarksmanSpite { get; set; } = true;
    public bool WarnPurifiableCrowdControl { get; set; } = true;
    public float PersonalWarningScreenX { get; set; } = 0.5f;
    public float PersonalWarningScreenY { get; set; } = 0.34f;
    public float PersonalWarningScale { get; set; } = 1f;
    public float PersonalWarningBackgroundOpacity { get; set; } = 0.92f;
    public float MarksmanSpiteWarningScale { get; set; } = 1.45f;
    public bool MchLimitBreakSoundEnabled { get; set; } = true;
    public int MchLimitBreakSoundId { get; set; } = 6;
    public bool ExperimentalPurifyOnNextKey { get; set; }
    public int ExperimentalPurifyBufferMilliseconds { get; set; } = 750;
    public bool PurifyOnHeldGameplayKey { get; set; }
    public bool PurifyOnStun { get; set; } = true;
    public bool PurifyOnHeavy { get; set; } = true;
    public bool PurifyOnBind { get; set; } = true;
    public bool PurifyOnSilence { get; set; } = true;
    public bool PurifyOnDeepFreeze { get; set; } = true;
    public bool PurifyOnMiracleOfNature { get; set; } = true;
    public bool EnableResourceAura { get; set; } = true;
    public bool ResourceAuraOnSelfHotbars { get; set; } = true;
    public bool ResourceAuraOnPartyRows { get; set; } = true;
    public bool ResourceAuraOnCcTeamRows { get; set; } = true;
    public int ResourceAuraHpPercent { get; set; } = 30;
    public int ResourceAuraMpThreshold { get; set; } = 2000;
    public float ResourceAuraIntensity { get; set; } = 0.8f;
    public float ResourceAuraPulseSpeed { get; set; } = 0.75f;
    public bool ExperimentalAllyRescueOnNextKey { get; set; }
    public bool AllyRescueOnHeldGameplayKey { get; set; }
    public bool ExperimentalMiracleInterceptOnHeldKey { get; set; }
    public bool MiracleInterceptMchLimitBreak { get; set; } = true;
    public bool MiracleInterceptSamZantetsuken { get; set; } = true;
    public bool MiracleInterceptViperNest { get; set; } = true;
    public bool EnableMonkEarthReplyHelper { get; set; }
    public bool MonkEarthReplyOnLowHp { get; set; } = true;
    public bool MonkEarthReplyBeforeExpiry { get; set; } = true;
    public int MonkEarthReplyHpPercent { get; set; } = 30;
    public float MonkEarthReplyExpirySeconds { get; set; } = 1.25f;
    public bool EnableFocusGlow { get; set; }
    public bool FocusHideWithGameUi { get; set; } = true;
    public bool FocusDrawInForeground { get; set; } = true;
    public bool FocusShowGroundRing { get; set; } = true;
    public bool FocusShowTargetHalo { get; set; } = true;
    public bool FocusShowRays { get; set; } = true;
    public bool FocusShowChevron { get; set; } = true;
    public bool FocusShowLabel { get; set; } = true;
    public bool FocusRainbowMode { get; set; }
    public bool FocusReducedMotion { get; set; }
    public Vector4 FocusGlowColor { get; set; } = new(1f, 0f, 0f, 1f);
    public float FocusIntensity { get; set; } = 0.55f;
    public float FocusSizeScale { get; set; } = 1.18f;
    public float FocusAuraRadius { get; set; } = 56f;
    public float FocusPulseSpeed { get; set; } = 0.6f;
    public float FocusPulseAmount { get; set; } = 0.2f;
    public float FocusGroundPadding { get; set; } = 0.75f;
    public float FocusVerticalOffset { get; set; } = 0.15f;
    public bool EnableCurrentTargetHighlight { get; set; }
    public bool CurrentTargetPvPOnly { get; set; } = true;
    public bool CurrentTargetDrawInForeground { get; set; } = true;
    public bool CurrentTargetShowGroundRing { get; set; } = true;
    public bool CurrentTargetShowTargetHalo { get; set; } = true;
    public bool CurrentTargetShowRays { get; set; }
    public bool CurrentTargetShowChevron { get; set; } = true;
    public bool CurrentTargetShowLabel { get; set; } = true;
    public bool CurrentTargetRainbowMode { get; set; }
    public bool CurrentTargetReducedMotion { get; set; }
    public Vector4 CurrentTargetGlowColor { get; set; } = new(0.05f, 0.9f, 1f, 1f);
    public float CurrentTargetIntensity { get; set; } = 0.9f;
    public float CurrentTargetSizeScale { get; set; } = 1f;
    public float CurrentTargetAuraRadius { get; set; } = 52f;
    public float CurrentTargetPulseSpeed { get; set; } = 0.5f;
    public float CurrentTargetPulseAmount { get; set; } = 0.12f;
    public float CurrentTargetGroundPadding { get; set; } = 0.55f;
    public float CurrentTargetVerticalOffset { get; set; } = 0.15f;
    // This is a fixed screen-space card, not a nameplate/job-icon attachment.
    public bool ShowCurrentTargetInfoHud { get; set; }
    public float CurrentTargetInfoScreenX { get; set; } = 0.5f;
    public float CurrentTargetInfoScreenY { get; set; } = 0.7f;
    public float CurrentTargetInfoScale { get; set; } = 1f;
    public bool EnableNearAssistMacro { get; set; }
    public float NearAssistMaxAllyDistance { get; set; } = 25f;
    public bool NearAssistPreferDamageRoles { get; set; } = true;
    public bool NearAssistPreferTeamPressure { get; set; }
    public bool ShowPressureCounter { get; set; } = true;
    public bool PressureLocked { get; set; }
    public bool PressureClickThroughWhenLocked { get; set; } = true;
    public bool PressureShowBackground { get; set; } = true;
    public bool PressureShowJobIcons { get; set; } = true;
    public bool PressureShowEnemySlots { get; set; } = true;
    public bool PressureUseThreatColors { get; set; } = true;
    public bool PressureIncludeWolvesDen { get; set; }
    public float PressureNumberPixelSize { get; set; } = 80f;
    public float PressureIconSize { get; set; } = 38f;
    public float PressureIconSpacing { get; set; } = 4f;
    public float PressureBackgroundOpacity { get; set; } = 0.62f;
    public int PressureIconsPerRow { get; set; } = 5;
    public float PressureWindowSeconds { get; set; } = 3f;
    public bool ShowIncomingPressureOnNameplates { get; set; } = true;
    public bool ShowTeamPressureOnNameplates { get; set; } = true;
    public bool ShowCcProtection { get; set; } = true;
    public bool ShowCcProtectionCountdown { get; set; } = true;
    public float CcProtectionEmblemScale { get; set; } = 1f;

    [NonSerialized]
    private IDalamudPluginInterface? pluginInterface;

    public void Initialize(IDalamudPluginInterface value)
    {
        pluginInterface = value;
        var repaired = ClampSettings();
        if (Version >= 14)
        {
            if (repaired) Save();
            return;
        }

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

        if (Version < 7)
        {
            // Both target-overlay modules are explicit opt-ins. Upgrading users keep
            // their existing HUD and Purify behavior without a surprise overlay.
            ApplyFocusGlowDefaults(false);
            ApplyCurrentTargetHighlightDefaults(false);
        }

        if (Version < 8)
        {
            // Near Assist can rewrite the target ID of one explicitly armed macro action,
            // so every existing installation must opt in deliberately after updating.
            EnableNearAssistMacro = false;
            NearAssistMaxAllyDistance = 25f;
            NearAssistPreferDamageRoles = true;
        }

        if (Version < 9)
        {
            // Warning-only feature: existing users receive the same opt-out behavior
            // as Wildfire and Death Warrant. It never presses Guard or another action.
            WarnMarksmanSpite = true;
        }

        if (Version < 10)
        {
            // Early test builds preserved 10-14y values that excluded ordinary ranged
            // teammates. The user explicitly requested the smarter 25y default.
            if (!float.IsFinite(NearAssistMaxAllyDistance) || NearAssistMaxAllyDistance <= 15f)
                NearAssistMaxAllyDistance = 25f;
            ShowPressureCounter = true;
            PressureLocked = false;
            PressureClickThroughWhenLocked = true;
            PressureShowBackground = true;
            PressureShowJobIcons = true;
            PressureShowEnemySlots = true;
            PressureUseThreatColors = true;
            PressureIncludeWolvesDen = false;
            PressureNumberPixelSize = 80f;
            PressureIconSize = 38f;
            PressureIconSpacing = 4f;
            PressureBackgroundOpacity = 0.62f;
            PressureIconsPerRow = 5;
            PressureWindowSeconds = 3f;
            ShowIncomingPressureOnNameplates = true;
            ShowTeamPressureOnNameplates = true;
            NearAssistPreferTeamPressure = false;
            ShowCcProtection = true;
            ShowCcProtectionCountdown = true;
            PersonalWarningBackgroundOpacity = 0.92f;
            MarksmanSpiteWarningScale = 1.45f;
            MchLimitBreakSoundEnabled = true;
            MchLimitBreakSoundId = 6;
        }

        if (Version < 11)
        {
            // Active protection used to share the tiny auxiliary-icon row. The
            // dedicated native-nameplate emblem is deliberately larger.
            CcProtectionEmblemScale = 1f;
        }

        if (Version < 12)
        {
            // Ally Rescue can issue one friendly action attempt. Existing users
            // must explicitly opt in to both the helper and held-key behavior.
            ExperimentalAllyRescueOnNextKey = false;
            AllyRescueOnHeldGameplayKey = false;
        }

        if (Version < 13)
        {
            // This helper can issue one hostile WHM action attempt in response
            // to an enemy start marker. Existing users must opt in explicitly.
            ExperimentalMiracleInterceptOnHeldKey = false;
            MiracleInterceptMchLimitBreak = true;
            MiracleInterceptSamZantetsuken = true;
            MiracleInterceptViperNest = true;
        }

        if (Version < 14)
        {
            // Visual-only resource auras are enabled for existing users so the new
            // low-resource readability upgrade is immediately visible.
            EnableResourceAura = true;
            ResourceAuraOnSelfHotbars = true;
            ResourceAuraOnPartyRows = true;
            ResourceAuraOnCcTeamRows = true;
            ResourceAuraHpPercent = 30;
            ResourceAuraMpThreshold = 2000;
            ResourceAuraIntensity = 0.8f;
            ResourceAuraPulseSpeed = 0.75f;

            // Earth's Reply can issue one exact self action attempt. Existing users
            // must opt in explicitly, while both trigger types retain useful defaults.
            EnableMonkEarthReplyHelper = false;
            MonkEarthReplyOnLowHp = true;
            MonkEarthReplyBeforeExpiry = true;
            MonkEarthReplyHpPercent = 30;
            MonkEarthReplyExpirySeconds = 1.25f;
        }

        Version = 14;
        ClampSettings();
        Save();
    }

    public void Save() => pluginInterface?.SavePluginConfig(this);

    public void ResetToDefaults()
    {
        Version = 14;
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
        WarnMarksmanSpite = true;
        WarnPurifiableCrowdControl = true;
        PersonalWarningScreenX = 0.5f;
        PersonalWarningScreenY = 0.34f;
        PersonalWarningScale = 1f;
        PersonalWarningBackgroundOpacity = 0.92f;
        MarksmanSpiteWarningScale = 1.45f;
        MchLimitBreakSoundEnabled = true;
        MchLimitBreakSoundId = 6;
        ExperimentalPurifyOnNextKey = false;
        ExperimentalPurifyBufferMilliseconds = 750;
        PurifyOnHeldGameplayKey = false;
        PurifyOnStun = true;
        PurifyOnHeavy = true;
        PurifyOnBind = true;
        PurifyOnSilence = true;
        PurifyOnDeepFreeze = true;
        PurifyOnMiracleOfNature = true;
        EnableResourceAura = true;
        ResourceAuraOnSelfHotbars = true;
        ResourceAuraOnPartyRows = true;
        ResourceAuraOnCcTeamRows = true;
        ResourceAuraHpPercent = 30;
        ResourceAuraMpThreshold = 2000;
        ResourceAuraIntensity = 0.8f;
        ResourceAuraPulseSpeed = 0.75f;
        ExperimentalAllyRescueOnNextKey = false;
        AllyRescueOnHeldGameplayKey = false;
        ExperimentalMiracleInterceptOnHeldKey = false;
        MiracleInterceptMchLimitBreak = true;
        MiracleInterceptSamZantetsuken = true;
        MiracleInterceptViperNest = true;
        EnableMonkEarthReplyHelper = false;
        MonkEarthReplyOnLowHp = true;
        MonkEarthReplyBeforeExpiry = true;
        MonkEarthReplyHpPercent = 30;
        MonkEarthReplyExpirySeconds = 1.25f;
        ApplyFocusGlowDefaults(false);
        ApplyCurrentTargetHighlightDefaults(false);
        EnableNearAssistMacro = false;
        NearAssistMaxAllyDistance = 25f;
        NearAssistPreferDamageRoles = true;
        NearAssistPreferTeamPressure = false;
        ShowPressureCounter = true;
        PressureLocked = false;
        PressureClickThroughWhenLocked = true;
        PressureShowBackground = true;
        PressureShowJobIcons = true;
        PressureShowEnemySlots = true;
        PressureUseThreatColors = true;
        PressureIncludeWolvesDen = false;
        PressureNumberPixelSize = 80f;
        PressureIconSize = 38f;
        PressureIconSpacing = 4f;
        PressureBackgroundOpacity = 0.62f;
        PressureIconsPerRow = 5;
        PressureWindowSeconds = 3f;
        ShowIncomingPressureOnNameplates = true;
        ShowTeamPressureOnNameplates = true;
        ShowCcProtection = true;
        ShowCcProtectionCountdown = true;
        CcProtectionEmblemScale = 1f;
        ClampSettings();
    }

    public void ApplyFocusGlowPreset() => ApplyFocusGlowDefaults(true);

    public void ApplyCurrentTargetHighlightPreset()
    {
        // Restoring the world-highlight appearance must not silently disable or move the
        // independently configured fixed information HUD.
        var showInfoHud = ShowCurrentTargetInfoHud;
        var infoScreenX = CurrentTargetInfoScreenX;
        var infoScreenY = CurrentTargetInfoScreenY;
        var infoScale = CurrentTargetInfoScale;

        ApplyCurrentTargetHighlightDefaults(true);

        ShowCurrentTargetInfoHud = showInfoHud;
        CurrentTargetInfoScreenX = infoScreenX;
        CurrentTargetInfoScreenY = infoScreenY;
        CurrentTargetInfoScale = infoScale;
    }

    private void ApplyFocusGlowDefaults(bool enabled)
    {
        EnableFocusGlow = enabled;
        FocusHideWithGameUi = true;
        FocusDrawInForeground = true;
        FocusShowGroundRing = true;
        FocusShowTargetHalo = true;
        FocusShowRays = true;
        FocusShowChevron = true;
        FocusShowLabel = true;
        FocusRainbowMode = false;
        FocusReducedMotion = false;
        FocusGlowColor = new Vector4(1f, 0f, 0f, 1f);
        FocusIntensity = 0.55f;
        FocusSizeScale = 1.18f;
        FocusAuraRadius = 56f;
        FocusPulseSpeed = 0.6f;
        FocusPulseAmount = 0.2f;
        FocusGroundPadding = 0.75f;
        FocusVerticalOffset = 0.15f;
    }

    private void ApplyCurrentTargetHighlightDefaults(bool enabled)
    {
        EnableCurrentTargetHighlight = enabled;
        CurrentTargetPvPOnly = true;
        CurrentTargetDrawInForeground = true;
        CurrentTargetShowGroundRing = true;
        CurrentTargetShowTargetHalo = true;
        CurrentTargetShowRays = false;
        CurrentTargetShowChevron = true;
        CurrentTargetShowLabel = true;
        CurrentTargetRainbowMode = false;
        CurrentTargetReducedMotion = false;
        CurrentTargetGlowColor = new Vector4(0.05f, 0.9f, 1f, 1f);
        CurrentTargetIntensity = 0.9f;
        CurrentTargetSizeScale = 1f;
        CurrentTargetAuraRadius = 52f;
        CurrentTargetPulseSpeed = 0.5f;
        CurrentTargetPulseAmount = 0.12f;
        CurrentTargetGroundPadding = 0.55f;
        CurrentTargetVerticalOffset = 0.15f;
        ShowCurrentTargetInfoHud = false;
        CurrentTargetInfoScreenX = 0.5f;
        CurrentTargetInfoScreenY = 0.7f;
        CurrentTargetInfoScale = 1f;
    }

    private bool ClampSettings()
    {
        var changed = false;
        var clamped = float.IsFinite(NearAssistMaxAllyDistance)
            ? Math.Clamp(NearAssistMaxAllyDistance, 5f, 30f)
            : 25f;
        changed |= AssignIfChanged(NearAssistMaxAllyDistance, clamped, value => NearAssistMaxAllyDistance = value);
        changed |= Clamp(PressureNumberPixelSize, 36f, 128f, 80f, value => PressureNumberPixelSize = value);
        changed |= Clamp(PressureIconSize, 16f, 72f, 38f, value => PressureIconSize = value);
        changed |= Clamp(PressureIconSpacing, 0f, 16f, 4f, value => PressureIconSpacing = value);
        changed |= Clamp(PressureBackgroundOpacity, 0f, 1f, 0.62f, value => PressureBackgroundOpacity = value);
        changed |= Clamp(PressureWindowSeconds, 0.5f, 8f, 3f, value => PressureWindowSeconds = value);
        changed |= Clamp(PersonalWarningBackgroundOpacity, 0f, 1f, 0.92f, value => PersonalWarningBackgroundOpacity = value);
        changed |= Clamp(MarksmanSpiteWarningScale, 1f, 2f, 1.45f, value => MarksmanSpiteWarningScale = value);
        changed |= Clamp(CcProtectionEmblemScale, 0.75f, 1.75f, 1f, value => CcProtectionEmblemScale = value);
        changed |= Clamp(ResourceAuraIntensity, 0.1f, 1.5f, 0.8f, value => ResourceAuraIntensity = value);
        changed |= Clamp(ResourceAuraPulseSpeed, 0.2f, 2f, 0.75f, value => ResourceAuraPulseSpeed = value);
        changed |= Clamp(
            MonkEarthReplyExpirySeconds,
            0.5f,
            2.5f,
            1.25f,
            value => MonkEarthReplyExpirySeconds = value);

        var monkEarthReplyHpPercent = Math.Clamp(MonkEarthReplyHpPercent, 10, 80);
        if (monkEarthReplyHpPercent != MonkEarthReplyHpPercent)
        {
            MonkEarthReplyHpPercent = monkEarthReplyHpPercent;
            changed = true;
        }

        var resourceAuraHpPercent = Math.Clamp(ResourceAuraHpPercent, 10, 80);
        if (resourceAuraHpPercent != ResourceAuraHpPercent)
        {
            ResourceAuraHpPercent = resourceAuraHpPercent;
            changed = true;
        }

        var resourceAuraMpThreshold = Math.Clamp(ResourceAuraMpThreshold, 0, 10_000);
        if (resourceAuraMpThreshold != ResourceAuraMpThreshold)
        {
            ResourceAuraMpThreshold = resourceAuraMpThreshold;
            changed = true;
        }

        var iconsPerRow = Math.Clamp(PressureIconsPerRow, 1, 16);
        if (iconsPerRow != PressureIconsPerRow)
        {
            PressureIconsPerRow = iconsPerRow;
            changed = true;
        }

        var soundId = Math.Clamp(MchLimitBreakSoundId, 1, 16);
        if (soundId != MchLimitBreakSoundId)
        {
            MchLimitBreakSoundId = soundId;
            changed = true;
        }

        return changed;
    }

    private static bool Clamp(
        float value,
        float minimum,
        float maximum,
        float fallback,
        Action<float> apply)
    {
        var clamped = float.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : fallback;
        return AssignIfChanged(value, clamped, apply);
    }

    private static bool AssignIfChanged(float value, float replacement, Action<float> apply)
    {
        if (Math.Abs(replacement - value) < 0.001f) return false;
        apply(replacement);
        return true;
    }
}
