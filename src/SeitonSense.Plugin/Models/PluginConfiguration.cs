using System.Numerics;
using Dalamud.Configuration;
using Dalamud.Plugin;
using SeitonSense.Core;

namespace SeitonSense.Plugin.Models;

public sealed class PluginConfiguration : IPluginConfiguration
{
    public const int DefaultTurboInitialDelayMilliseconds = 0;
    public const int DefaultTurboRepeatIntervalMilliseconds = 60;
    public const int MinimumTurboInitialDelayMilliseconds = 0;
    public const int MaximumTurboInitialDelayMilliseconds = 1_000;
    public const int MinimumTurboRepeatIntervalMilliseconds = 0;
    public const int MaximumTurboRepeatIntervalMilliseconds = 1_000;

    private static readonly uint[] SupportedCcBrakeJobIds =
    [
        19, // PLD
        21, // WAR
        23, // BRD
        24, // WHM
        25, // BLM
        30, // NIN
        31, // MCH
        33, // AST
        34, // SAM
    ];

    private static readonly uint[] SupportedCcBrakeActionIds =
    [
        29065, // Intervene
        29081, // Blota
        29395, // Silent Nocturne
        29399, // Repelling Shot
        29228, // Miracle of Nature
        41510, // Lethargy
        29510, // Forked Raiju
        29707, // Fleeting Raiju
        29407, // Air Anchor
        29244, // Gravity II
        29248, // Double Cast: Gravity II
        29535, // Mineuchi
    ];

    public int Version { get; set; } = 48;
    public string LastSeenReleaseNotesVersion { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public bool EnableWolvesDenTesting { get; set; } = true;
    public bool ShowNameplateSeiton { get; set; } = true;
    public bool ShowGuardUnavailable { get; set; } = true;
    public bool ShowGuardCountdown { get; set; } = true;
    public bool ShowLowMp { get; set; } = true;
    public bool ShowSeitonPopup { get; set; } = true;
    public bool ShowPersistentSeitonCue { get; set; } = true;
    public bool ShowSeitonPreparation { get; set; } = true;
    // Schema-28 compatibility only. The serialized held-name below is retained
    // so existing /autoseiton opt-ins now arm the automatic availability lane.
    public bool EnableNinjaSeitonOnFreshGameplayKey { get; set; }
    public bool EnableNinjaSeitonOnHeldGameplayKey { get; set; }
    public bool EnableNinjaGuardShukuchiOnHeldGameplayKey { get; set; }
    public bool EnableScholarCriticalStrategyOnHeldKey { get; set; }
    public bool EnableAstrologianHarmonicOrbisOnHeldKey { get; set; }
    public bool EnableRedMageGuardEngageOnHeldKey { get; set; }
    public int RedMageGuardEngageMinimumHpPercent { get; set; } =
        RedMageGuardEngageRules.DefaultMinimumHpPercent;
    public int RedMageGuardEngageMinimumMpPercent { get; set; } =
        RedMageGuardEngageRules.DefaultMinimumMpPercent;
    // Schema-25 compatibility only. Runtime and UI use the Eukrasia-triggered option.
    public bool EnableSageKardiaOnHeldKey { get; set; }
    public bool EnableSageKardiaAfterEukrasia { get; set; }
    public bool EnableSmartRecuperateOnHeldKey { get; set; }
    public bool EnableAutomaticRecuperate { get; set; }
    public bool EnableEmergencyTeleportOnHeldKey { get; set; }
    public int EmergencyTeleportHpPercent { get; set; } = 50;
    public int EmergencyTeleportMpThreshold { get; set; } = 4000;
    public int EmergencyTeleportMinimumFocusedEnemies { get; set; } = 1;
    public float EmergencyTeleportMinimumTravelYalms { get; set; } = 10f;
    public float EmergencyTeleportEnemySafetyRadiusYalms { get; set; } = 10f;
    public int EmergencyTeleportMaximumNearbyEnemies { get; set; }
    public bool EnableViperSerpentTailOnHeldKey { get; set; }
    public bool EnableGunbreakerContinuationOnHeldKey { get; set; }
    public bool EnableMonkHeldComboOnHeldKey { get; set; }
    public bool AllowHeldHelpersToCancelOwnCast { get; set; }
    public bool AllowAutomaticRecoveryToCancelBasicShotCasts { get; set; }
    public bool EnablePvpLatencyResponseHelper { get; set; }
    public int PvpLatencyResponseWindowMilliseconds { get; set; } =
        HeldActionRetryRules.DefaultLatencyResponseWindowMilliseconds;
    public bool EnableSmartActionBuffer { get; set; } = true;
    public int SmartActionBufferWindowMilliseconds { get; set; } =
        SmartActionBufferWindowRules.DefaultMilliseconds;
    public bool ShowBufferLearningWindow { get; set; } = true;
    public bool BufferLearningWindowLocked { get; set; }
    public bool ShowWolvesDenRotationPanel { get; set; } = true;
    public bool EnableLocalCrystallineConflictMapStatisticsCapture { get; set; } = true;
    public bool EnableInstantLeaveAfterCrystallineConflict { get; set; }
    public bool WolvesDenRotationPanelLocked { get; set; }
    public bool WolvesDenRotationPanelShowBackground { get; set; } = true;
    public float WolvesDenRotationPanelScale { get; set; } = 1f;
    public float WolvesDenRotationPanelBackgroundOpacity { get; set; } = 0.88f;
    public int WolvesDenRotationOffsetSlots { get; set; }
    public bool ShowPvpRangeHelper { get; set; } = true;
    public bool PvpRangeHelperDrawInForeground { get; set; }
    public bool PvpRangeHelperShowLabels { get; set; } = true;
    public float PvpRangeHelperOpacity { get; set; } = 0.72f;
    public float PvpRangeHelperLineWidth { get; set; } = 2.2f;
    public Vector4 PvpRangeHelperMeleeColor { get; set; } = new(0.1f, 0.95f, 1f, 1f);
    public Vector4 PvpRangeHelperMaximumColor { get; set; } = new(1f, 0.62f, 0.08f, 1f);
    public bool EnableNativeHotbarTurbo { get; set; }
    public int TurboInitialDelayMilliseconds { get; set; } =
        DefaultTurboInitialDelayMilliseconds;
    public int TurboRepeatIntervalMilliseconds { get; set; } =
        DefaultTurboRepeatIntervalMilliseconds;
    public bool TurboOutsideCombat { get; set; }
    public bool EnableDarkKnightPlungeOnHeldKey { get; set; }
    public bool EnableDarkKnightShadowbringerOnHeldKey { get; set; }
    public bool DarkKnightShadowbringerPreserveBlackblood { get; set; } = true;
    public int DarkKnightShadowbringerMinimumHpPercent { get; set; } = 85;
    public int DarkKnightShadowbringerPressureLimitExclusive { get; set; } = 2;
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
    public bool WarnSummonerLimitBreak { get; set; } = true;
    public bool WarnEnemyChiten { get; set; } = true;
    public bool WarnPurifiableCrowdControl { get; set; } = true;
    public float PersonalWarningScreenX { get; set; } = 0.5f;
    public float PersonalWarningScreenY { get; set; } = 0.34f;
    public float PersonalWarningScale { get; set; } = 1f;
    public float PersonalWarningBackgroundOpacity { get; set; } = 0.92f;
    public bool WarnWhenIsolated { get; set; } = true;
    public float IsolationWarningScale { get; set; } = 1f;
    public bool ShowHighPressureWarning { get; set; } = true;
    public bool PlayHighPressureWarningSound { get; set; }
    public int HighPressureWarningSoundId { get; set; } = 6;
    public bool EnablePressureEscapeSprintOnHeldKey { get; set; }
    public float MarksmanSpiteWarningScale { get; set; } = 1.45f;
    public bool MchLimitBreakSoundEnabled { get; set; } = true;
    public int MchLimitBreakSoundId { get; set; } = 6;
    public bool ShowAutoGuardActivationNotification { get; set; } = true;
    public bool PlayAutoGuardActivationSound { get; set; } = true;
    public int AutoGuardActivationSoundId { get; set; } = 3;
    public bool PlayLocalMpWarningSounds { get; set; } = true;
    public int LocalMpWarning4000SoundId { get; set; } = 4;
    public int LocalMpWarning2000SoundId { get; set; } = 6;
    public bool ExperimentalPurifyOnNextKey { get; set; }
    public int ExperimentalPurifyBufferMilliseconds { get; set; } = 750;
    public bool PurifyOnHeldGameplayKey { get; set; }
    public bool EnableAutomaticPurify { get; set; }
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
    public bool EnableBardWardensPaeanPressureRedirect { get; set; }
    public bool ExperimentalMiracleInterceptOnHeldKey { get; set; }
    public bool MiracleInterceptMchLimitBreak { get; set; } = true;
    public bool MiracleInterceptSamZantetsuken { get; set; } = true;
    public bool MiracleInterceptViperNest { get; set; } = true;
    public bool MiracleInterceptAfterPurifiedStun { get; set; }
    public bool EnableDefensiveUtilities { get; set; }
    public bool DefensiveUtilitiesOnHeldKey { get; set; } = true;
    public bool GuardOnStunPressure { get; set; } = true;
    // Schema-25 compatibility only. The pre-Guard rule is no longer used.
    public bool PreGuardOnLowHpPressure { get; set; }
    public bool PaladinGuardianLowAlly { get; set; }
    public bool PaladinGuardianOnHeldKey { get; set; } = true;
    public bool PaladinGuardianAnnounceAndMark { get; set; }
    public bool EnableReactiveCcUtilities { get; set; }
    public bool ReactiveCcOnHeldKey { get; set; } = true;
    public bool ReactiveCcDancerLimitBreak { get; set; } = true;
    public bool ReactiveCcAfterEnemyPurify { get; set; } = true;
    public bool ReactiveCcAfterEnemyGuard { get; set; } = true;
    public bool ReactiveCcPaladinIntervene { get; set; }
    public float ReactiveCcPaladinInterveneMaximumRangeYalms { get; set; } = 20f;
    public bool ReactiveCcRedMageResolution { get; set; }
    public bool ReactiveCcRedMageViceOfThorns { get; set; }
    public bool ReactiveCcBlackMageFrostStar { get; set; }
    public int ReactiveCcImpactCalibrationRevision { get; set; } =
        ReactiveCounterCcImpactTimingRules.CalibrationRevision;
    public Dictionary<uint, List<ReactiveCounterCcImpactSample>>
        ReactiveCcImpactCalibrationSamples
    { get; set; } = [];
    public bool ReactiveCcSamuraiSotenMineuchi { get; set; }
    public float ReactiveCcSamuraiSotenMaximumRangeYalms { get; set; } = 20f;
    // Serialized legacy name retained for schema compatibility. This default-off
    // option now arms automatic Zantetsuken and no longer observes a held key.
    public bool EnableSamuraiZantetsukenOnHeldKey { get; set; }
    public bool EnableMonkEarthReplyHelper { get; set; }
    public bool MonkEarthReplyOnLowHp { get; set; } = true;
    public bool MonkEarthReplyBeforeExpiry { get; set; } = true;
    public int MonkEarthReplyHpPercent { get; set; } = 30;
    public float MonkEarthReplyExpirySeconds { get; set; } = 1.25f;
    public bool EnableAutoLowMpFocusTarget { get; set; }
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
    public bool ShowCombatFrames { get; set; }
    public bool CombatFramesEnableInteraction { get; set; } = true;
    public bool CombatFramesShowLimitBreaks { get; set; } = true;
    public bool ShowEnemyLimitBreaksOnNameplates { get; set; } = true;
    public bool ShowLimitBreakActivationMessages { get; set; } = true;
    public bool LimitBreakFeedShowNames { get; set; } = true;
    public float LimitBreakNameplateScale { get; set; } = 1f;
    public bool ShowAllyLimitBreakDamageEvents { get; set; } = true;
    public bool CombatFramesShowNames { get; set; } = true;
    public bool CombatFramesShowExactValues { get; set; } = true;
    public bool CombatFramesShowStatuses { get; set; } = true;
    public bool CombatFramesShowPressure { get; set; } = true;
    public float CombatFramesEnemyScreenX { get; set; } = 0.82f;
    public float CombatFramesEnemyScreenY { get; set; } = 0.48f;
    public float CombatFramesSelfScreenX { get; set; } = 0.5f;
    public float CombatFramesSelfScreenY { get; set; } = 0.78f;
    public float CombatFramesScale { get; set; } = 1f;
    public float CombatFramesBackgroundOpacity { get; set; } = 0.92f;
    public bool EnableSmartTabTargeting { get; set; }
    public bool EnableSmartActionMacro { get; set; }
    public bool EnableNearAssistMacro { get; set; }
    public bool EnableBackwardPanicShukuchiCommand { get; set; } = false;
    public float NearAssistMaxAllyDistance { get; set; } = 25f;
    public bool NearAssistPreferDamageRoles { get; set; } = true;
    public bool NearAssistPreferTeamPressure { get; set; }
    public bool NearHelpPreferIncomingPressure { get; set; } = true;
    public bool ShowPressureCounter { get; set; } = true;
    public bool PressureLocked { get; set; }
    public bool PressureClickThroughWhenLocked { get; set; } = true;
    public bool PressureShowBackground { get; set; } = true;
    public bool PressureShowJobIcons { get; set; } = true;
    public bool PressureShowEnemySlots { get; set; } = true;
    public bool ShowOpponentLimitBreakBars { get; set; }
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
    public bool EnableAutoEnemyFocusMark { get; set; }
    public bool EnableCcImmunityBrake { get; set; }
    public Dictionary<uint, bool> CcBrakeJobs { get; set; } = CreateDefaultCcBrakeJobs();
    public Dictionary<uint, bool> CcBrakeActions { get; set; } = CreateDefaultCcBrakeActions();

    [NonSerialized]
    private IDalamudPluginInterface? pluginInterface;

    public void Initialize(IDalamudPluginInterface value)
    {
        pluginInterface = value;
        var repaired = ClampSettings();
        if (Version >= 48)
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

        if (Version < 15)
        {
            // The brake can suppress one incoming action attempt, so every existing
            // installation must deliberately opt in after updating. Once enabled,
            // the reviewed job/action selections start enabled and remain granular.
            EnableCcImmunityBrake = false;
            CcBrakeJobs = CreateDefaultCcBrakeJobs();
            CcBrakeActions = CreateDefaultCcBrakeActions();
        }

        if (Version < 16)
        {
            // This follow-up can issue one hostile WHM action after an observed
            // enemy cleanse, so every existing installation must opt in.
            MiracleInterceptAfterPurifiedStun = false;
        }

        if (Version < 17)
        {
            // Defensive action requests are new and therefore remain an explicit opt-in.
            // Their individual rules are ready when the master switch is enabled.
            EnableDefensiveUtilities = false;
            DefensiveUtilitiesOnHeldKey = true;
            GuardOnStunPressure = true;
            PreGuardOnLowHpPressure = true;
            PaladinGuardianLowAlly = true;

            // Preserve the old Miracle helper opt-in and its post-cleanse choice. The
            // newly supported DNC startup is not silently enabled for upgrading users.
            EnableReactiveCcUtilities = ExperimentalMiracleInterceptOnHeldKey;
            ReactiveCcOnHeldKey = true;
            ReactiveCcDancerLimitBreak = false;
            ReactiveCcAfterEnemyPurify = MiracleInterceptAfterPurifiedStun;
        }

        if (Version < 18)
        {
            // Near Help still remains behind the shared default-off macro-helper master.
            // Once a user deliberately arms it, the requested survival ranking is ready.
            NearHelpPreferIncomingPressure = true;
        }

        if (Version < 19)
        {
            // This helper can initiate one hostile NIN limit-break action attempt,
            // so new and upgrading installations must opt in deliberately.
            EnableNinjaSeitonOnFreshGameplayKey = false;
        }

        if (Version < 20)
        {
            // Party-visible Guardian communication is a separate social side effect.
            // New and upgrading installations must opt in deliberately.
            PaladinGuardianAnnounceAndMark = false;
        }

        if (Version < 21)
        {
            // This helper can initiate one hostile SCH action attempt from held input,
            // so new and upgrading installations must opt in deliberately.
            EnableScholarCriticalStrategyOnHeldKey = false;
        }

        if (Version < 22)
        {
            // This helper can redirect an already incoming friendly action call,
            // so new and upgrading installations must opt in explicitly.
            EnableBardWardensPaeanPressureRedirect = false;
        }

        if (Version < 23)
        {
            // The visual warning is deliberately off for existing installations so an
            // upgrade cannot suddenly add a prominent top-center alert. Native sound and
            // the held-key Sprint action remain separate explicit opt-ins everywhere.
            ShowHighPressureWarning = false;
            PlayHighPressureWarningSound = false;
            HighPressureWarningSoundId = 6;
            EnablePressureEscapeSprintOnHeldKey = false;
        }

        if (Version < 24)
        {
            // This feature may set an empty native Focus Target. Every upgrading
            // user must explicitly opt in after reviewing its exact boundary.
            EnableAutoLowMpFocusTarget = false;
        }

        if (Version < 25)
        {
            // This helper can initiate one SGE Kardia action from held input,
            // so new and upgrading installations must opt in deliberately.
            EnableSageKardiaOnHeldKey = false;
        }

        if (Version < 26)
        {
            // The former broad defensive master implicitly gated Guardian. Preserve
            // only the previously effective opt-in when moving PLD to Job Tools.
            var guardianWasEnabled = EnableDefensiveUtilities && PaladinGuardianLowAlly;
            PaladinGuardianLowAlly = guardianWasEnabled;
            PaladinGuardianOnHeldKey = DefensiveUtilitiesOnHeldKey;

            // The frame-driven held Kardia helper is replaced by one bounded
            // opportunity following an accepted Eukrasia call. Preserve an explicit
            // prior opt-in without leaving the obsolete held path armed.
            EnableSageKardiaAfterEukrasia = EnableSageKardiaOnHeldKey;
            EnableSageKardiaOnHeldKey = false;

            // Generated healing and the removed speculative pre-Guard behavior never
            // turn on silently for an upgrading installation.
            EnableSmartRecuperateOnHeldKey = false;
            PreGuardOnLowHpPressure = false;

            // The fixed screen-space combat frames are a substantial visual
            // addition, so upgrades remain quiet until the user enables them.
            ShowCombatFrames = false;
            CombatFramesShowNames = true;
            CombatFramesShowExactValues = true;
            CombatFramesShowStatuses = true;
            CombatFramesShowPressure = true;
            CombatFramesEnemyScreenX = 0.82f;
            CombatFramesEnemyScreenY = 0.48f;
            CombatFramesSelfScreenX = 0.5f;
            CombatFramesSelfScreenY = 0.78f;
            CombatFramesScale = 1f;
            CombatFramesBackgroundOpacity = 0.92f;
        }

        if (Version < 27)
        {
            // New action and targeting paths stay opt-in for upgrading users.
            // The Combat Frames master remains off unless the user enabled it;
            // its read-only LB detail toggles default on behind that master.
            EnableDarkKnightPlungeOnHeldKey = false;
            CombatFramesEnableInteraction = false;
            CombatFramesShowLimitBreaks = true;
            ShowAllyLimitBreakDamageEvents = true;
        }

        if (Version < 28)
        {
            // This follow-up can issue one hostile BRD/WHM action after an observed
            // enemy Guard ends, so every existing installation must opt in.
            ReactiveCcAfterEnemyGuard = false;
        }

        if (Version < 29)
        {
            // Preserve only an explicit prior NIN opt-in while replacing its
            // fresh-edge contract with the shared continuous-hold scheduler.
            EnableNinjaSeitonOnHeldGameplayKey = EnableNinjaSeitonOnFreshGameplayKey;
            EnableNinjaSeitonOnFreshGameplayKey = false;
        }

        if (Version < 30)
        {
            // Native cast cancellation is a new local action side effect. It
            // remains a separate explicit opt-in for every upgrading user.
            AllowHeldHelpersToCancelOwnCast = false;
        }

        if (Version < 31)
        {
            // This helper can initiate a ground-targeted Shukuchi and then set
            // one exact hard target. Every upgrading user must opt in.
            EnableNinjaGuardShukuchiOnHeldGameplayKey = false;
        }

        if (Version < 32)
        {
            // The unusable fixed Combat Frames are retired. Preserve their
            // trustworthy action/status LB presentation as exact nameplate and
            // notification features, and make the requested local-MP warnings
            // available immediately with explicit toggles.
            ShowCombatFrames = false;
            ShowEnemyLimitBreaksOnNameplates = true;
            ShowLimitBreakActivationMessages = true;
            LimitBreakFeedShowNames = CombatFramesShowNames;
            LimitBreakNameplateScale = 1f;
            ShowAllyLimitBreakDamageEvents = true;
            PlayLocalMpWarningSounds = true;
            LocalMpWarning4000SoundId = 4;
            LocalMpWarning2000SoundId = 6;
        }

        if (Version < 33)
        {
            // Smart Tab is a new explicit native forward-target replacement and
            // visible hard-target mutation, so it must never turn on silently.
            // Preserve the already opt-in harmful-action redirect behind its new
            // independent switch.
            EnableSmartTabTargeting = false;
            EnableSmartActionMacro = EnableNearAssistMacro;
        }

        if (Version < 34)
        {
            // This helper can issue the exact VPR follow-up currently exposed
            // by the transformed Serpent's Tail carrier. It is a new hostile
            // action path, so every upgrading user must opt in.
            EnableViperSerpentTailOnHeldKey = false;
        }

        if (Version < 35)
        {
            // Both helpers initiate new PvP actions and therefore remain
            // explicit opt-ins for every upgrading installation.
            EnableEmergencyTeleportOnHeldKey = false;
            EmergencyTeleportHpPercent = 50;
            EmergencyTeleportMpThreshold = 4000;
            EmergencyTeleportMinimumFocusedEnemies = 1;
            EmergencyTeleportMinimumTravelYalms = 10f;
            EmergencyTeleportEnemySafetyRadiusYalms = 10f;
            EmergencyTeleportMaximumNearbyEnemies = 0;
        }

        if (Version < 36)
        {
            // Auto-Guard is already an explicit action opt-in. Its short local
            // confirmation card and sound make the protected two-second input
            // window visible without enabling any additional action request.
            ShowAutoGuardActivationNotification = true;
            PlayAutoGuardActivationSound = true;
            AutoGuardActivationSoundId = 3;
            EnableGunbreakerContinuationOnHeldKey = false;
        }

        if (Version < 37)
        {
            // Every added hostile action path remains an explicit opt-in for an
            // upgrading installation. Conservative thresholds are ready once
            // the corresponding job helper is deliberately enabled.
            EnableDarkKnightShadowbringerOnHeldKey = false;
            DarkKnightShadowbringerMinimumHpPercent = 85;
            DarkKnightShadowbringerPressureLimitExclusive = 2;
            ReactiveCcPaladinIntervene = false;
            ReactiveCcPaladinInterveneMaximumRangeYalms = 20f;
            ReactiveCcRedMageResolution = false;
            ReactiveCcSamuraiSotenMineuchi = false;
            ReactiveCcSamuraiSotenMaximumRangeYalms = 20f;
            EnableSamuraiZantetsukenOnHeldKey = false;
            EnableMonkHeldComboOnHeldKey = false;
        }

        if (Version < 38)
        {
            // Proc-only counter actions and persistent impact calibration are
            // new hostile paths/state. Upgrading users opt in explicitly, and
            // no unversioned timing evidence is trusted.
            ReactiveCcRedMageViceOfThorns = false;
            ReactiveCcBlackMageFrostStar = false;
            ReactiveCcImpactCalibrationRevision =
                ReactiveCounterCcImpactTimingRules.CalibrationRevision;
            ReactiveCcImpactCalibrationSamples = [];
        }

        if (Version < 39)
        {
            // Cross-plugin scheduling coordination changes when another input
            // plugin yields. Existing installations must opt in deliberately.
            EnablePvpLatencyResponseHelper = false;
            PvpLatencyResponseWindowMilliseconds =
                HeldActionRetryRules.DefaultLatencyResponseWindowMilliseconds;
        }

        if (Version < 40)
        {
            // The general one-shot buffer is useful in PvE, PvP, and Wolves' Den,
            // so it starts enabled with the requested one-second learning window.
            // Native held-input Turbo creates repeated input and therefore remains
            // an explicit opt-in, including its separate outside-combat test scope.
            EnableSmartActionBuffer = true;
            SmartActionBufferWindowMilliseconds =
                SmartActionBufferWindowRules.DefaultMilliseconds;
            ShowBufferLearningWindow = true;
            BufferLearningWindowLocked = false;
            EnableNativeHotbarTurbo = false;
            TurboInitialDelayMilliseconds =
                DefaultTurboInitialDelayMilliseconds;
            TurboRepeatIntervalMilliseconds =
                DefaultTurboRepeatIntervalMilliseconds;
            TurboOutsideCombat = false;
        }

        if (Version < 41)
        {
            // This helper can issue one direct AST heal and, only when it was
            // already available, one same-target Double Cast follow-up. Every
            // upgrading installation must opt in deliberately.
            EnableAstrologianHarmonicOrbisOnHeldKey = false;
        }

        if (Version < 42)
        {
            // Both additions are read-only PvP overlays. Upgrading users receive
            // the requested visible defaults without changing targeting or action
            // behavior; every appearance option remains independently adjustable.
            ShowWolvesDenRotationPanel = true;
            WolvesDenRotationPanelLocked = false;
            WolvesDenRotationPanelShowBackground = true;
            WolvesDenRotationPanelScale = 1f;
            WolvesDenRotationPanelBackgroundOpacity = 0.88f;
            WolvesDenRotationOffsetSlots = 0;
            ShowPvpRangeHelper = true;
            PvpRangeHelperDrawInForeground = false;
            PvpRangeHelperShowLabels = true;
            PvpRangeHelperOpacity = 0.72f;
            PvpRangeHelperLineWidth = 2.2f;
            PvpRangeHelperMeleeColor = new Vector4(0.1f, 0.95f, 1f, 1f);
            PvpRangeHelperMaximumColor = new Vector4(1f, 0.62f, 0.08f, 1f);
        }

        if (Version < 43)
        {
            // Auto Shadowbringer remains an explicit opt-in. Existing users who
            // already enabled it receive the requested duplicate-buff protection,
            // and may independently turn this nested safety option back off.
            DarkKnightShadowbringerPreserveBlackblood = true;
        }

        if (Version < 44)
        {
            // This hostile movement/targeting helper is a new explicit opt-in.
            // Existing installations must never inherit it from another held lane.
            EnableRedMageGuardEngageOnHeldKey = false;
            RedMageGuardEngageMinimumHpPercent =
                RedMageGuardEngageRules.DefaultMinimumHpPercent;
            RedMageGuardEngageMinimumMpPercent =
                RedMageGuardEngageRules.DefaultMinimumMpPercent;
            EnableBackwardPanicShukuchiCommand = false;
            EnableLocalCrystallineConflictMapStatisticsCapture = true;
        }

        if (Version < 45)
        {
            // Both automatic self-actions can issue without a physical key.
            // Existing installations must opt in explicitly after updating.
            EnableAutomaticPurify = false;
            EnableAutomaticRecuperate = false;
        }

        if (Version < 46)
        {
            // Cancelling a mobile BRD/MCH basic-shot cast is a new automatic
            // recovery side effect. Preserve every existing helper opt-in, but
            // require separate consent before either automatic lane may do it.
            AllowAutomaticRecoveryToCancelBasicShotCasts = false;
        }

        if (Version < 47)
        {
            // Status/action warnings are read-only and safe opt-out additions.
            // The native GaugeBar observer remains experimental until its
            // current-client node semantics are confirmed live.
            WarnSummonerLimitBreak = true;
            WarnEnemyChiten = true;
            ShowOpponentLimitBreakBars = false;
        }

        if (Version < 48)
        {
            // Leaving content is a new automatic side effect. Existing users
            // must opt in explicitly after reading its exact public-CC scope.
            EnableInstantLeaveAfterCrystallineConflict = false;
        }

        Version = 48;
        ClampSettings();
        Save();
    }

    public void Save() => pluginInterface?.SavePluginConfig(this);

    public void ResetToDefaults()
    {
        Version = 48;
        Enabled = true;
        EnableWolvesDenTesting = true;
        ShowNameplateSeiton = true;
        ShowGuardUnavailable = true;
        ShowGuardCountdown = true;
        ShowLowMp = true;
        ShowSeitonPopup = true;
        ShowPersistentSeitonCue = true;
        ShowSeitonPreparation = true;
        EnableNinjaSeitonOnFreshGameplayKey = false;
        EnableNinjaSeitonOnHeldGameplayKey = false;
        EnableNinjaGuardShukuchiOnHeldGameplayKey = false;
        EnableScholarCriticalStrategyOnHeldKey = false;
        EnableAstrologianHarmonicOrbisOnHeldKey = false;
        EnableRedMageGuardEngageOnHeldKey = false;
        RedMageGuardEngageMinimumHpPercent =
            RedMageGuardEngageRules.DefaultMinimumHpPercent;
        RedMageGuardEngageMinimumMpPercent =
            RedMageGuardEngageRules.DefaultMinimumMpPercent;
        EnableSageKardiaOnHeldKey = false;
        EnableSageKardiaAfterEukrasia = false;
        EnableSmartRecuperateOnHeldKey = false;
        EnableAutomaticRecuperate = false;
        EnableEmergencyTeleportOnHeldKey = false;
        EmergencyTeleportHpPercent = 50;
        EmergencyTeleportMpThreshold = 4000;
        EmergencyTeleportMinimumFocusedEnemies = 1;
        EmergencyTeleportMinimumTravelYalms = 10f;
        EmergencyTeleportEnemySafetyRadiusYalms = 10f;
        EmergencyTeleportMaximumNearbyEnemies = 0;
        EnableViperSerpentTailOnHeldKey = false;
        EnableGunbreakerContinuationOnHeldKey = false;
        EnableMonkHeldComboOnHeldKey = false;
        AllowHeldHelpersToCancelOwnCast = false;
        AllowAutomaticRecoveryToCancelBasicShotCasts = false;
        EnablePvpLatencyResponseHelper = false;
        PvpLatencyResponseWindowMilliseconds =
            HeldActionRetryRules.DefaultLatencyResponseWindowMilliseconds;
        EnableSmartActionBuffer = true;
        SmartActionBufferWindowMilliseconds =
            SmartActionBufferWindowRules.DefaultMilliseconds;
        ShowBufferLearningWindow = true;
        BufferLearningWindowLocked = false;
        ShowWolvesDenRotationPanel = true;
        EnableLocalCrystallineConflictMapStatisticsCapture = true;
        EnableInstantLeaveAfterCrystallineConflict = false;
        WolvesDenRotationPanelLocked = false;
        WolvesDenRotationPanelShowBackground = true;
        WolvesDenRotationPanelScale = 1f;
        WolvesDenRotationPanelBackgroundOpacity = 0.88f;
        WolvesDenRotationOffsetSlots = 0;
        ShowPvpRangeHelper = true;
        PvpRangeHelperDrawInForeground = false;
        PvpRangeHelperShowLabels = true;
        PvpRangeHelperOpacity = 0.72f;
        PvpRangeHelperLineWidth = 2.2f;
        PvpRangeHelperMeleeColor = new Vector4(0.1f, 0.95f, 1f, 1f);
        PvpRangeHelperMaximumColor = new Vector4(1f, 0.62f, 0.08f, 1f);
        EnableNativeHotbarTurbo = false;
        TurboInitialDelayMilliseconds =
            DefaultTurboInitialDelayMilliseconds;
        TurboRepeatIntervalMilliseconds =
            DefaultTurboRepeatIntervalMilliseconds;
        TurboOutsideCombat = false;
        EnableDarkKnightPlungeOnHeldKey = false;
        EnableDarkKnightShadowbringerOnHeldKey = false;
        DarkKnightShadowbringerPreserveBlackblood = true;
        DarkKnightShadowbringerMinimumHpPercent = 85;
        DarkKnightShadowbringerPressureLimitExclusive = 2;
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
        WarnSummonerLimitBreak = true;
        WarnEnemyChiten = true;
        WarnPurifiableCrowdControl = true;
        PersonalWarningScreenX = 0.5f;
        PersonalWarningScreenY = 0.34f;
        PersonalWarningScale = 1f;
        PersonalWarningBackgroundOpacity = 0.92f;
        WarnWhenIsolated = true;
        IsolationWarningScale = 1f;
        ShowHighPressureWarning = true;
        PlayHighPressureWarningSound = false;
        HighPressureWarningSoundId = 6;
        EnablePressureEscapeSprintOnHeldKey = false;
        MarksmanSpiteWarningScale = 1.45f;
        MchLimitBreakSoundEnabled = true;
        MchLimitBreakSoundId = 6;
        ShowAutoGuardActivationNotification = true;
        PlayAutoGuardActivationSound = true;
        AutoGuardActivationSoundId = 3;
        PlayLocalMpWarningSounds = true;
        LocalMpWarning4000SoundId = 4;
        LocalMpWarning2000SoundId = 6;
        ExperimentalPurifyOnNextKey = false;
        ExperimentalPurifyBufferMilliseconds = 750;
        PurifyOnHeldGameplayKey = false;
        EnableAutomaticPurify = false;
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
        EnableBardWardensPaeanPressureRedirect = false;
        ExperimentalMiracleInterceptOnHeldKey = false;
        MiracleInterceptMchLimitBreak = true;
        MiracleInterceptSamZantetsuken = true;
        MiracleInterceptViperNest = true;
        MiracleInterceptAfterPurifiedStun = false;
        EnableDefensiveUtilities = false;
        DefensiveUtilitiesOnHeldKey = true;
        GuardOnStunPressure = true;
        PreGuardOnLowHpPressure = false;
        PaladinGuardianLowAlly = false;
        PaladinGuardianOnHeldKey = true;
        PaladinGuardianAnnounceAndMark = false;
        EnableReactiveCcUtilities = false;
        ReactiveCcOnHeldKey = true;
        ReactiveCcDancerLimitBreak = true;
        ReactiveCcAfterEnemyPurify = true;
        ReactiveCcAfterEnemyGuard = true;
        ReactiveCcPaladinIntervene = false;
        ReactiveCcPaladinInterveneMaximumRangeYalms = 20f;
        ReactiveCcRedMageResolution = false;
        ReactiveCcRedMageViceOfThorns = false;
        ReactiveCcBlackMageFrostStar = false;
        ReactiveCcImpactCalibrationRevision =
            ReactiveCounterCcImpactTimingRules.CalibrationRevision;
        ReactiveCcImpactCalibrationSamples = [];
        ReactiveCcSamuraiSotenMineuchi = false;
        ReactiveCcSamuraiSotenMaximumRangeYalms = 20f;
        EnableSamuraiZantetsukenOnHeldKey = false;
        EnableMonkEarthReplyHelper = false;
        MonkEarthReplyOnLowHp = true;
        MonkEarthReplyBeforeExpiry = true;
        MonkEarthReplyHpPercent = 30;
        MonkEarthReplyExpirySeconds = 1.25f;
        EnableAutoLowMpFocusTarget = false;
        ApplyFocusGlowDefaults(false);
        ApplyCurrentTargetHighlightDefaults(false);
        ShowCombatFrames = false;
        CombatFramesEnableInteraction = true;
        CombatFramesShowLimitBreaks = true;
        ShowEnemyLimitBreaksOnNameplates = true;
        ShowLimitBreakActivationMessages = true;
        LimitBreakFeedShowNames = true;
        LimitBreakNameplateScale = 1f;
        ShowAllyLimitBreakDamageEvents = true;
        CombatFramesShowNames = true;
        CombatFramesShowExactValues = true;
        CombatFramesShowStatuses = true;
        CombatFramesShowPressure = true;
        CombatFramesEnemyScreenX = 0.82f;
        CombatFramesEnemyScreenY = 0.48f;
        CombatFramesSelfScreenX = 0.5f;
        CombatFramesSelfScreenY = 0.78f;
        CombatFramesScale = 1f;
        CombatFramesBackgroundOpacity = 0.92f;
        EnableSmartTabTargeting = false;
        EnableSmartActionMacro = false;
        EnableNearAssistMacro = false;
        EnableBackwardPanicShukuchiCommand = false;
        NearAssistMaxAllyDistance = 25f;
        NearAssistPreferDamageRoles = true;
        NearAssistPreferTeamPressure = false;
        NearHelpPreferIncomingPressure = true;
        ShowPressureCounter = true;
        PressureLocked = false;
        PressureClickThroughWhenLocked = true;
        PressureShowBackground = true;
        PressureShowJobIcons = true;
        PressureShowEnemySlots = true;
        ShowOpponentLimitBreakBars = false;
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
        EnableAutoEnemyFocusMark = false;
        EnableCcImmunityBrake = false;
        CcBrakeJobs = CreateDefaultCcBrakeJobs();
        CcBrakeActions = CreateDefaultCcBrakeActions();
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

    public void ApplyCombatFramesLayoutDefaults()
    {
        CombatFramesEnemyScreenX = 0.82f;
        CombatFramesEnemyScreenY = 0.48f;
        CombatFramesSelfScreenX = 0.5f;
        CombatFramesSelfScreenY = 0.78f;
        CombatFramesScale = 1f;
        CombatFramesBackgroundOpacity = 0.92f;
    }

    public void ApplyCombatFramesCleanPreset()
    {
        ShowPressureCounter = false;
        ShowNameplateSeiton = false;
        ShowGuardUnavailable = false;
        ShowGuardCountdown = false;
        ShowLowMp = false;
        ShowIncomingPressureOnNameplates = false;
        ShowTeamPressureOnNameplates = false;
        ShowCcProtection = false;
        EnableResourceAura = false;
        ShowCurrentTargetInfoHud = false;
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
        changed |= NormalizeCcBrakeSelections();
        changed |= NormalizeReactiveCcImpactCalibrations();
        var clamped = float.IsFinite(NearAssistMaxAllyDistance)
            ? Math.Clamp(NearAssistMaxAllyDistance, 5f, 30f)
            : 25f;
        changed |= AssignIfChanged(NearAssistMaxAllyDistance, clamped, value => NearAssistMaxAllyDistance = value);
        changed |= Clamp(
            EmergencyTeleportMinimumTravelYalms,
            3f,
            25f,
            10f,
            value => EmergencyTeleportMinimumTravelYalms = value);
        changed |= Clamp(
            EmergencyTeleportEnemySafetyRadiusYalms,
            3f,
            20f,
            10f,
            value => EmergencyTeleportEnemySafetyRadiusYalms = value);
        changed |= Clamp(
            ReactiveCcPaladinInterveneMaximumRangeYalms,
            1f,
            20f,
            20f,
            value => ReactiveCcPaladinInterveneMaximumRangeYalms = value);
        changed |= Clamp(
            ReactiveCcSamuraiSotenMaximumRangeYalms,
            SamuraiReactiveCounterCcRules.MineuchiMaximumRangeYalms,
            SamuraiReactiveCounterCcRules.SotenMaximumRangeYalms,
            SamuraiReactiveCounterCcRules.SotenMaximumRangeYalms,
            value => ReactiveCcSamuraiSotenMaximumRangeYalms = value);
        changed |= Clamp(PressureNumberPixelSize, 36f, 128f, 80f, value => PressureNumberPixelSize = value);
        changed |= Clamp(PressureIconSize, 16f, 72f, 38f, value => PressureIconSize = value);
        changed |= Clamp(PressureIconSpacing, 0f, 16f, 4f, value => PressureIconSpacing = value);
        changed |= Clamp(PressureBackgroundOpacity, 0f, 1f, 0.62f, value => PressureBackgroundOpacity = value);
        changed |= Clamp(PressureWindowSeconds, 0.5f, 8f, 3f, value => PressureWindowSeconds = value);
        changed |= Clamp(
            WolvesDenRotationPanelScale,
            0.75f,
            1.75f,
            1f,
            value => WolvesDenRotationPanelScale = value);
        changed |= Clamp(
            WolvesDenRotationPanelBackgroundOpacity,
            0f,
            1f,
            0.88f,
            value => WolvesDenRotationPanelBackgroundOpacity = value);
        var rotationOffsetSlots = Math.Clamp(
            WolvesDenRotationOffsetSlots,
            -(CrystallineConflictRotationRules.ArenaCount / 2),
            CrystallineConflictRotationRules.ArenaCount / 2);
        if (rotationOffsetSlots != WolvesDenRotationOffsetSlots)
        {
            WolvesDenRotationOffsetSlots = rotationOffsetSlots;
            changed = true;
        }
        changed |= Clamp(
            PvpRangeHelperOpacity,
            0.08f,
            1f,
            0.72f,
            value => PvpRangeHelperOpacity = value);
        changed |= Clamp(
            PvpRangeHelperLineWidth,
            0.75f,
            6f,
            2.2f,
            value => PvpRangeHelperLineWidth = value);
        changed |= Clamp(PersonalWarningBackgroundOpacity, 0f, 1f, 0.92f, value => PersonalWarningBackgroundOpacity = value);
        changed |= Clamp(IsolationWarningScale, 0.75f, 1.75f, 1f, value => IsolationWarningScale = value);
        changed |= Clamp(MarksmanSpiteWarningScale, 1f, 2f, 1.45f, value => MarksmanSpiteWarningScale = value);
        changed |= Clamp(CcProtectionEmblemScale, 0.75f, 1.75f, 1f, value => CcProtectionEmblemScale = value);
        changed |= Clamp(LimitBreakNameplateScale, 0.75f, 1.75f, 1f, value => LimitBreakNameplateScale = value);
        changed |= Clamp(CombatFramesEnemyScreenX, 0.02f, 0.98f, 0.82f, value => CombatFramesEnemyScreenX = value);
        changed |= Clamp(CombatFramesEnemyScreenY, 0.02f, 0.98f, 0.48f, value => CombatFramesEnemyScreenY = value);
        changed |= Clamp(CombatFramesSelfScreenX, 0.02f, 0.98f, 0.5f, value => CombatFramesSelfScreenX = value);
        changed |= Clamp(CombatFramesSelfScreenY, 0.02f, 0.98f, 0.78f, value => CombatFramesSelfScreenY = value);
        changed |= Clamp(CombatFramesScale, 0.55f, 1.8f, 1f, value => CombatFramesScale = value);
        changed |= Clamp(CombatFramesBackgroundOpacity, 0.35f, 1f, 0.92f, value => CombatFramesBackgroundOpacity = value);
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

        var emergencyTeleportHpPercent = Math.Clamp(EmergencyTeleportHpPercent, 10, 90);
        if (emergencyTeleportHpPercent != EmergencyTeleportHpPercent)
        {
            EmergencyTeleportHpPercent = emergencyTeleportHpPercent;
            changed = true;
        }

        var darkKnightShadowbringerMinimumHpPercent = Math.Clamp(
            DarkKnightShadowbringerMinimumHpPercent,
            1,
            100);
        if (darkKnightShadowbringerMinimumHpPercent !=
            DarkKnightShadowbringerMinimumHpPercent)
        {
            DarkKnightShadowbringerMinimumHpPercent =
                darkKnightShadowbringerMinimumHpPercent;
            changed = true;
        }

        var darkKnightShadowbringerPressureLimitExclusive = Math.Clamp(
            DarkKnightShadowbringerPressureLimitExclusive,
            1,
            6);
        if (darkKnightShadowbringerPressureLimitExclusive !=
            DarkKnightShadowbringerPressureLimitExclusive)
        {
            DarkKnightShadowbringerPressureLimitExclusive =
                darkKnightShadowbringerPressureLimitExclusive;
            changed = true;
        }

        var emergencyTeleportMpThreshold = Math.Clamp(EmergencyTeleportMpThreshold, 0, 10_000);
        if (emergencyTeleportMpThreshold != EmergencyTeleportMpThreshold)
        {
            EmergencyTeleportMpThreshold = emergencyTeleportMpThreshold;
            changed = true;
        }

        var redMageGuardEngageMinimumHpPercent = Math.Clamp(
            RedMageGuardEngageMinimumHpPercent,
            RedMageGuardEngageRules.MinimumConfigurablePercent,
            RedMageGuardEngageRules.MaximumConfigurablePercent);
        if (redMageGuardEngageMinimumHpPercent !=
            RedMageGuardEngageMinimumHpPercent)
        {
            RedMageGuardEngageMinimumHpPercent =
                redMageGuardEngageMinimumHpPercent;
            changed = true;
        }

        var redMageGuardEngageMinimumMpPercent = Math.Clamp(
            RedMageGuardEngageMinimumMpPercent,
            RedMageGuardEngageRules.MinimumConfigurablePercent,
            RedMageGuardEngageRules.MaximumConfigurablePercent);
        if (redMageGuardEngageMinimumMpPercent !=
            RedMageGuardEngageMinimumMpPercent)
        {
            RedMageGuardEngageMinimumMpPercent =
                redMageGuardEngageMinimumMpPercent;
            changed = true;
        }

        var pvpLatencyResponseWindowMilliseconds = Math.Clamp(
            PvpLatencyResponseWindowMilliseconds,
            HeldActionRetryRules.MinimumLatencyResponseWindowMilliseconds,
            HeldActionRetryRules.MaximumLatencyResponseWindowMilliseconds);
        if (pvpLatencyResponseWindowMilliseconds !=
            PvpLatencyResponseWindowMilliseconds)
        {
            PvpLatencyResponseWindowMilliseconds =
                pvpLatencyResponseWindowMilliseconds;
            changed = true;
        }

        var smartActionBufferWindowMilliseconds =
            SmartActionBufferWindowRules.Normalize(
                SmartActionBufferWindowMilliseconds);
        if (smartActionBufferWindowMilliseconds !=
            SmartActionBufferWindowMilliseconds)
        {
            SmartActionBufferWindowMilliseconds =
                smartActionBufferWindowMilliseconds;
            changed = true;
        }

        var turboInitialDelayMilliseconds = Math.Clamp(
            TurboInitialDelayMilliseconds,
            MinimumTurboInitialDelayMilliseconds,
            MaximumTurboInitialDelayMilliseconds);
        if (turboInitialDelayMilliseconds !=
            TurboInitialDelayMilliseconds)
        {
            TurboInitialDelayMilliseconds = turboInitialDelayMilliseconds;
            changed = true;
        }

        var turboRepeatIntervalMilliseconds = Math.Clamp(
            TurboRepeatIntervalMilliseconds,
            MinimumTurboRepeatIntervalMilliseconds,
            MaximumTurboRepeatIntervalMilliseconds);
        if (turboRepeatIntervalMilliseconds !=
            TurboRepeatIntervalMilliseconds)
        {
            TurboRepeatIntervalMilliseconds = turboRepeatIntervalMilliseconds;
            changed = true;
        }

        var emergencyTeleportMinimumFocusedEnemies = Math.Clamp(
            EmergencyTeleportMinimumFocusedEnemies,
            1,
            5);
        if (emergencyTeleportMinimumFocusedEnemies != EmergencyTeleportMinimumFocusedEnemies)
        {
            EmergencyTeleportMinimumFocusedEnemies = emergencyTeleportMinimumFocusedEnemies;
            changed = true;
        }

        var emergencyTeleportMaximumNearbyEnemies = Math.Clamp(
            EmergencyTeleportMaximumNearbyEnemies,
            0,
            5);
        if (emergencyTeleportMaximumNearbyEnemies != EmergencyTeleportMaximumNearbyEnemies)
        {
            EmergencyTeleportMaximumNearbyEnemies = emergencyTeleportMaximumNearbyEnemies;
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

        var autoGuardSoundId = Math.Clamp(AutoGuardActivationSoundId, 1, 16);
        if (autoGuardSoundId != AutoGuardActivationSoundId)
        {
            AutoGuardActivationSoundId = autoGuardSoundId;
            changed = true;
        }

        var highPressureSoundId = Math.Clamp(HighPressureWarningSoundId, 1, 16);
        if (highPressureSoundId != HighPressureWarningSoundId)
        {
            HighPressureWarningSoundId = highPressureSoundId;
            changed = true;
        }

        var localMp4000SoundId = Math.Clamp(LocalMpWarning4000SoundId, 1, 16);
        if (localMp4000SoundId != LocalMpWarning4000SoundId)
        {
            LocalMpWarning4000SoundId = localMp4000SoundId;
            changed = true;
        }

        var localMp2000SoundId = Math.Clamp(LocalMpWarning2000SoundId, 1, 16);
        if (localMp2000SoundId != LocalMpWarning2000SoundId)
        {
            LocalMpWarning2000SoundId = localMp2000SoundId;
            changed = true;
        }

        return changed;
    }

    public bool IsCcBrakeJobEnabled(uint jobId) =>
        IsSupportedCcBrakeJob(jobId) &&
        (!CcBrakeJobs.TryGetValue(jobId, out var enabled) || enabled);

    public bool IsCcBrakeActionEnabled(uint actionId) =>
        IsSupportedCcBrakeAction(actionId) &&
        (!CcBrakeActions.TryGetValue(actionId, out var enabled) || enabled);

    public void SetCcBrakeJobEnabled(uint jobId, bool enabled)
    {
        if (IsSupportedCcBrakeJob(jobId)) CcBrakeJobs[jobId] = enabled;
    }

    public void SetCcBrakeActionEnabled(uint actionId, bool enabled)
    {
        if (!IsSupportedCcBrakeAction(actionId)) return;
        CcBrakeActions[actionId] = enabled;

        // Double Cast is the adjusted form of the same AST Gravity II choice.
        if (actionId is 29244 or 29248)
        {
            CcBrakeActions[29244] = enabled;
            CcBrakeActions[29248] = enabled;
        }
    }

    private bool NormalizeCcBrakeSelections()
    {
        var normalizedJobs = NormalizeSelections(CcBrakeJobs, SupportedCcBrakeJobIds);
        var normalizedActions = NormalizeSelections(CcBrakeActions, SupportedCcBrakeActionIds);

        // Keep both runtime forms behind the one visible Gravity II setting.
        var gravityEnabled = normalizedActions[29244];
        normalizedActions[29248] = gravityEnabled;

        var changed = !DictionaryEquals(CcBrakeJobs, normalizedJobs) ||
                      !DictionaryEquals(CcBrakeActions, normalizedActions);
        CcBrakeJobs = normalizedJobs;
        CcBrakeActions = normalizedActions;
        return changed;
    }

    private bool NormalizeReactiveCcImpactCalibrations()
    {
        if (ReactiveCcImpactCalibrationRevision !=
            ReactiveCounterCcImpactTimingRules.CalibrationRevision)
        {
            ReactiveCcImpactCalibrationRevision =
                ReactiveCounterCcImpactTimingRules.CalibrationRevision;
            ReactiveCcImpactCalibrationSamples = [];
            return true;
        }

        var normalized =
            new Dictionary<uint, List<ReactiveCounterCcImpactSample>>();
        foreach (var (actionId, samples) in
                 ReactiveCcImpactCalibrationSamples ?? [])
        {
            if (!ReactiveCounterCcImpactTimingRules.IsSupportedAction(actionId))
                continue;
            var clean = ReactiveCounterCcImpactTimingRules
                .NormalizeSamples(samples)
                .ToList();
            if (clean.Count > 0) normalized[actionId] = clean;
        }

        var changed = !ImpactCalibrationDictionaryEquals(
            ReactiveCcImpactCalibrationSamples,
            normalized);
        ReactiveCcImpactCalibrationSamples = normalized;
        return changed;
    }

    private static Dictionary<uint, bool> CreateDefaultCcBrakeJobs() =>
        SupportedCcBrakeJobIds.ToDictionary(static id => id, static _ => true);

    private static Dictionary<uint, bool> CreateDefaultCcBrakeActions() =>
        SupportedCcBrakeActionIds.ToDictionary(static id => id, static _ => true);

    private static Dictionary<uint, bool> NormalizeSelections(
        IReadOnlyDictionary<uint, bool>? selections,
        IReadOnlyList<uint> supportedIds)
    {
        var normalized = new Dictionary<uint, bool>(supportedIds.Count);
        foreach (var id in supportedIds)
            normalized[id] = selections?.TryGetValue(id, out var enabled) == true ? enabled : true;
        return normalized;
    }

    private static bool DictionaryEquals(
        IReadOnlyDictionary<uint, bool>? left,
        IReadOnlyDictionary<uint, bool> right)
    {
        if (left is null || left.Count != right.Count) return false;
        foreach (var (id, enabled) in right)
        {
            if (!left.TryGetValue(id, out var current) || current != enabled) return false;
        }

        return true;
    }

    private static bool ImpactCalibrationDictionaryEquals(
        IReadOnlyDictionary<uint, List<ReactiveCounterCcImpactSample>>? left,
        IReadOnlyDictionary<uint, List<ReactiveCounterCcImpactSample>> right)
    {
        if (left is null || left.Count != right.Count) return false;
        foreach (var (key, samples) in right)
        {
            if (!left.TryGetValue(key, out var current) ||
                current is null ||
                !current.SequenceEqual(samples))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSupportedCcBrakeJob(uint jobId) =>
        Array.IndexOf(SupportedCcBrakeJobIds, jobId) >= 0;

    private static bool IsSupportedCcBrakeAction(uint actionId) =>
        Array.IndexOf(SupportedCcBrakeActionIds, actionId) >= 0;

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
