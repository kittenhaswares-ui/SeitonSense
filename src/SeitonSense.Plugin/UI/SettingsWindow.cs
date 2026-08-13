using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using SeitonSense.Core;
using SeitonSense.Plugin.Models;
using SeitonSense.Plugin.Services;

namespace SeitonSense.Plugin.UI;

internal sealed class SettingsWindow : Window
{
    private readonly PluginConfiguration configuration;
    private readonly ExecuteTracker tracker;
    private readonly PersonalStatusService personalStatus;
    private readonly OverlayRenderer overlay;
    private readonly TargetPressureTracker pressureTracker;
    private readonly PressureCounterWindow pressureCounter;

    public SettingsWindow(
        PluginConfiguration configuration,
        ExecuteTracker tracker,
        PersonalStatusService personalStatus,
        OverlayRenderer overlay,
        TargetPressureTracker pressureTracker,
        PressureCounterWindow pressureCounter)
        : base("Seiton Sense###SeitonSenseSettings")
    {
        this.configuration = configuration;
        this.tracker = tracker;
        this.personalStatus = personalStatus;
        this.overlay = overlay;
        this.pressureTracker = pressureTracker;
        this.pressureCounter = pressureCounter;
        Size = new Vector2(700f, 760f);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        ImGui.TextColored(new Vector4(0.98f, 0.2f, 0.48f, 1f), "PVP REACTION CUES");
        ImGui.SameLine();
        ImGui.TextDisabled("Seiton, pressure, warnings and target clarity in one place");

        var changed = Checkbox(
            "Enable Seiton Sense",
            configuration.Enabled,
            value => configuration.Enabled = value);

        ImGui.Separator();
        if (ImGui.BeginTabBar("SeitonSenseSettingsTabs"))
        {
            if (ImGui.BeginTabItem("Overview"))
            {
                changed |= DrawOverviewTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Pressure"))
            {
                changed |= DrawPressureTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Warnings"))
            {
                changed |= DrawWarningsTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Seiton"))
            {
                changed |= DrawSeitonTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Assist"))
            {
                changed |= DrawAssistTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Targets"))
            {
                changed |= DrawTargetsTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Advanced"))
            {
                changed |= DrawAdvancedTab();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        if (changed) configuration.Save();
    }

    public override void OnClose()
    {
        overlay.PreviewEnabled = false;
        overlay.CcProtectionPreviewEnabled = false;
        pressureCounter.PreviewEnabled = false;
    }

    private bool DrawOverviewTab()
    {
        var changed = false;
        ImGui.Spacing();
        ImGui.TextWrapped(
            "Seiton Sense combines stable native-nameplate cues, personal warnings, target highlights, " +
            "pressure information and an optional one-shot Near Assist macro helper.");
        ImGui.TextWrapped(
            "Crystalline Conflict is supported directly. Wolves' Den support is an explicit testing option; " +
            "Frontline and Rival Wings remain excluded from the original Seiton slot tracker.");

        ImGui.Spacing();
        changed |= Checkbox(
            "Enable Wolves' Den duel testing",
            configuration.EnableWolvesDenTesting,
            value => configuration.EnableWolvesDenTesting = value);
        ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
        ImGui.TextDisabled(
            "FFXIV's exact hostile duel opponent is shown as synthetic S1, including party-member duels. " +
            "This is only a visual label; the CC <e1> macro placeholder may not exist in a duel.");
        ImGui.PopTextWrapPos();

        ImGui.Separator();
        DrawAllyRescueOverview();

        ImGui.Separator();
        ImGui.TextUnformatted("Preview and reset");
        if (ImGui.Button(overlay.PreviewEnabled ? "Stop preview" : "Preview HUD + warnings"))
        {
            overlay.PreviewEnabled = !overlay.PreviewEnabled;
            if (overlay.PreviewEnabled) overlay.CcProtectionPreviewEnabled = false;
        }
        ImGui.SameLine();
        if (ImGui.Button(pressureCounter.PreviewEnabled ? "Stop pressure preview" : "Preview pressure"))
            pressureCounter.PreviewEnabled = !pressureCounter.PreviewEnabled;
        ImGui.SameLine();
        if (ImGui.Button("Preview Seiton popup")) overlay.TriggerPreviewPopup();
        ImGui.SameLine();
        if (ImGui.Button("Reset defaults"))
        {
            configuration.ResetToDefaults();
            overlay.PreviewEnabled = false;
            overlay.CcProtectionPreviewEnabled = false;
            pressureCounter.PreviewEnabled = false;
            pressureCounter.ResetWindowPosition();
            changed = true;
        }

        return changed;
    }

    private void DrawAllyRescueOverview()
    {
        var rescue = personalStatus.AllyRescueDiagnostics;
        var session = rescue.SessionConfirmations;
        var match = rescue.MatchConfirmations;

        ImGui.TextColored(new Vector4(0.34f, 0.82f, 1f, 1f), "ALLY RESCUE RESULTS");
        ImGui.TextUnformatted(
            $"Session: {rescue.AttemptCount} attempts  •  {rescue.AcceptedCount} client accepted  •  " +
            $"{session.TotalConfirmed} confirmed");
        ImGui.TextUnformatted($"This CC: {match.TotalConfirmed} confirmed");
        ImGui.TextDisabled(
            $"Actions: Paean {session.CountForAction(AllyRescueConfirmationRules.WardensPaeanActionId)}  •  " +
            $"Aquaveil {session.CountForAction(AllyRescueConfirmationRules.AquaveilActionId)}");
        ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
        ImGui.TextDisabled(
            $"Removed: Stun {session.CountForStatus(AllyRescueConfirmationRules.StunStatusId)}  •  " +
            $"Heavy {session.CountForStatus(AllyRescueConfirmationRules.HeavyStatusId)}  •  " +
            $"Bind {session.CountForStatus(AllyRescueConfirmationRules.BindStatusId)}  •  " +
            $"Silence {session.CountForStatus(AllyRescueConfirmationRules.SilenceStatusId)}  •  " +
            $"Miracle {session.CountForStatus(AllyRescueConfirmationRules.MiracleOfNatureStatusId)}  •  " +
            $"Deep Freeze {session.CountForStatus(AllyRescueConfirmationRules.DeepFreezeStatusId)}");
        ImGui.PopTextWrapPos();
        if (ImGui.Button("Reset Ally Rescue statistics"))
            personalStatus.ResetAllyRescueStatistics();
        ImGui.SameLine();
        if (ImGui.Button("Preview CLEANSED popup"))
            overlay.TriggerAllyRescueConfirmationPreview();
        ImGui.TextDisabled("Confirmed means the exact server 0x10 status-removal result was captured.");
    }

    private bool DrawPressureTab()
    {
        var changed = false;
        ImGui.Spacing();
        ImGui.TextWrapped(
            "Incoming pressure counts enemies currently committed to you. Team pressure shows how many allies " +
            "are hard-targeting each enemy. Recent harmful actions are kept briefly so the counter stays readable.");

        changed |= Checkbox(
            "Show incoming-pressure counter",
            configuration.ShowPressureCounter,
            value => configuration.ShowPressureCounter = value);
        changed |= Checkbox(
            "Lock pressure counter",
            configuration.PressureLocked,
            value => configuration.PressureLocked = value);
        ImGui.SameLine();
        changed |= Checkbox(
            "Click-through while locked",
            configuration.PressureClickThroughWhenLocked,
            value => configuration.PressureClickThroughWhenLocked = value);
        changed |= Checkbox(
            "Show counter background",
            configuration.PressureShowBackground,
            value => configuration.PressureShowBackground = value);
        ImGui.SameLine();
        changed |= Checkbox(
            "Use threat colors",
            configuration.PressureUseThreatColors,
            value => configuration.PressureUseThreatColors = value);
        changed |= Checkbox(
            "Show attacker job icons",
            configuration.PressureShowJobIcons,
            value => configuration.PressureShowJobIcons = value);
        ImGui.SameLine();
        changed |= Checkbox(
            "Show CC enemy slots",
            configuration.PressureShowEnemySlots,
            value => configuration.PressureShowEnemySlots = value);
        changed |= Checkbox(
            "Include Wolves' Den pressure testing",
            configuration.PressureIncludeWolvesDen,
            value => configuration.PressureIncludeWolvesDen = value);

        ImGui.Separator();
        ImGui.TextUnformatted("Counter appearance");
        changed |= Slider(
            "Sharp number size",
            configuration.PressureNumberPixelSize,
            36f,
            128f,
            value => configuration.PressureNumberPixelSize = value,
            "%.0f px");
        changed |= Slider(
            "Job icon size",
            configuration.PressureIconSize,
            16f,
            72f,
            value => configuration.PressureIconSize = value,
            "%.0f px");
        changed |= Slider(
            "Job icon spacing",
            configuration.PressureIconSpacing,
            0f,
            16f,
            value => configuration.PressureIconSpacing = value,
            "%.1f px");
        changed |= Slider(
            "Counter background opacity",
            configuration.PressureBackgroundOpacity,
            0f,
            1f,
            value => configuration.PressureBackgroundOpacity = value,
            "%.2f");
        changed |= SliderInt(
            "Icons per row",
            configuration.PressureIconsPerRow,
            1,
            16,
            value => configuration.PressureIconsPerRow = value,
            "%d");
        changed |= Slider(
            "Recent-pressure memory",
            configuration.PressureWindowSeconds,
            0.5f,
            8f,
            value => configuration.PressureWindowSeconds = value,
            "%.1f s");

        ImGui.Separator();
        ImGui.TextUnformatted("Native nameplates");
        changed |= Checkbox(
            "Show incoming pressure on nameplates",
            configuration.ShowIncomingPressureOnNameplates,
            value => configuration.ShowIncomingPressureOnNameplates = value);
        changed |= Checkbox(
            "Show team pressure on enemy nameplates",
            configuration.ShowTeamPressureOnNameplates,
            value => configuration.ShowTeamPressureOnNameplates = value);
        ImGui.TextDisabled(
            "Hard-target/cast pressure and recent-action pressure are displayed as distinct states; neither " +
            "changes your selected target.");
        if (ImGui.Button(pressureCounter.PreviewEnabled ? "Stop counter preview" : "Preview counter"))
            pressureCounter.PreviewEnabled = !pressureCounter.PreviewEnabled;
        ImGui.SameLine();
        if (ImGui.Button("Reset counter position")) pressureCounter.ResetWindowPosition();
        ImGui.TextDisabled(pressureTracker.Diagnostics.ToChatLine());

        return changed;
    }

    private bool DrawWarningsTab()
    {
        var changed = false;
        ImGui.Spacing();
        ImGui.TextUnformatted("Warnings on you");
        changed |= Checkbox(
            "Show personal debuff warnings",
            configuration.ShowPersonalWarnings,
            value => configuration.ShowPersonalWarnings = value);
        changed |= Checkbox("Wildfire", configuration.WarnWildfire, value => configuration.WarnWildfire = value);
        ImGui.SameLine();
        changed |= Checkbox(
            "Death Warrant / Richtbefehl",
            configuration.WarnDeathWarrant,
            value => configuration.WarnDeathWarrant = value);
        changed |= Checkbox(
            "Marksman's Spite / MCH LB aimed at you",
            configuration.WarnMarksmanSpite,
            value => configuration.WarnMarksmanSpite = value);
        changed |= Checkbox(
            "All Purify-removable debuff warnings",
            configuration.WarnPurifiableCrowdControl,
            value => configuration.WarnPurifiableCrowdControl = value);

        ImGui.Separator();
        ImGui.TextUnformatted("Warning appearance");
        changed |= Slider(
            "Warning horizontal position",
            configuration.PersonalWarningScreenX,
            0.05f,
            0.95f,
            value => configuration.PersonalWarningScreenX = value,
            "%.2f");
        changed |= Slider(
            "Warning vertical position",
            configuration.PersonalWarningScreenY,
            0.08f,
            0.9f,
            value => configuration.PersonalWarningScreenY = value,
            "%.2f");
        changed |= Slider(
            "Warning scale",
            configuration.PersonalWarningScale,
            0.55f,
            1.8f,
            value => configuration.PersonalWarningScale = value,
            "%.2f x");
        changed |= Slider(
            "Warning background opacity",
            configuration.PersonalWarningBackgroundOpacity,
            0f,
            1f,
            value => configuration.PersonalWarningBackgroundOpacity = value,
            "%.2f");
        ImGui.TextDisabled("At 0, the warning text, icon and border remain visible while the card fill disappears.");

        ImGui.Separator();
        ImGui.TextUnformatted("Marksman's Spite");
        changed |= Slider(
            "MCH LB warning size",
            configuration.MarksmanSpiteWarningScale,
            1f,
            2f,
            value => configuration.MarksmanSpiteWarningScale = value,
            "%.2f x");
        changed |= Checkbox(
            "Play a sound for a verified MCH LB warning",
            configuration.MchLimitBreakSoundEnabled,
            value => configuration.MchLimitBreakSoundEnabled = value);
        changed |= SliderInt(
            "MCH warning sound",
            configuration.MchLimitBreakSoundId,
            1,
            16,
            value => configuration.MchLimitBreakSoundId = value,
            "Sound %d");
        if (ImGui.Button("Test MCH warning sound"))
            personalStatus.PlayMachinistLimitBreakSoundPreview();
        ImGui.TextDisabled("The alert is warning-only. It never presses Guard or another action.");

        ImGui.Separator();
        ImGui.TextUnformatted("Enemy CC protection");
        changed |= Checkbox(
            "Show visible CC protection above native nameplates",
            configuration.ShowCcProtection,
            value => configuration.ShowCcProtection = value);
        ImGui.SameLine();
        changed |= Checkbox(
            "Countdown",
            configuration.ShowCcProtectionCountdown,
            value => configuration.ShowCcProtectionCountdown = value);
        changed |= Slider(
            "CC immunity emblem size",
            configuration.CcProtectionEmblemScale,
            0.75f,
            1.75f,
            value => configuration.CcProtectionEmblemScale = value,
            "%.2f x");
        if (ImGui.Button(overlay.CcProtectionPreviewEnabled ? "Stop CC emblem preview" : "Preview CC emblem"))
        {
            overlay.CcProtectionPreviewEnabled = !overlay.CcProtectionPreviewEnabled;
            if (overlay.CcProtectionPreviewEnabled) overlay.PreviewEnabled = false;
        }
        ImGui.TextDisabled(
            "A large static crossed-CC emblem is anchored above the native job icon for Guard, Resilience, " +
            "SAM, WAR, VPR and large-scale PvP immunity.");
        ImGui.TextDisabled("Ambiguous one-hit wards are intentionally not labelled as full immunity.");

        ImGui.Spacing();
        if (ImGui.CollapsingHeader("Advanced: experimental Purify on next key"))
            changed |= DrawPurifyControls();
        if (ImGui.CollapsingHeader("Advanced: experimental ally rescue on next key"))
            changed |= DrawAllyRescueControls();
        if (ImGui.CollapsingHeader("Advanced: experimental WHM Miracle intercept"))
            changed |= DrawMiracleInterceptControls();

        return changed;
    }

    private bool DrawMiracleInterceptControls()
    {
        var changed = false;
        changed |= Checkbox(
            "Use Miracle of Nature once from a held gameplay key",
            configuration.ExperimentalMiracleInterceptOnHeldKey,
            value => configuration.ExperimentalMiracleInterceptOnHeldKey = value);
        ImGui.TextColored(
            configuration.ExperimentalMiracleInterceptOnHeldKey
                ? new Vector4(0.35f, 0.9f, 1f, 1f)
                : new Vector4(1f, 0.4f, 0.35f, 1f),
            configuration.ExperimentalMiracleInterceptOnHeldKey
                ? "ON — threat capture is active in CC while playing WHM."
                : "OFF — no threat is captured and no Miracle attempt can occur.");
        ImGui.TextUnformatted("Trigger separately for:");
        changed |= Checkbox(
            "MCH Marksman's Spite startup",
            configuration.MiracleInterceptMchLimitBreak,
            value => configuration.MiracleInterceptMchLimitBreak = value);
        changed |= Checkbox(
            "SAM Zantetsuken startup",
            configuration.MiracleInterceptSamZantetsuken,
            value => configuration.MiracleInterceptSamZantetsuken = value);
        changed |= Checkbox(
            "VPR Furious Backlash / Nest der Blutschuppen",
            configuration.MiracleInterceptViperNest,
            value => configuration.MiracleInterceptViperNest = value);
        ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
        ImGui.TextDisabled(
            "Experimental and CC-only. WHM uses the exact early action marker and one already-eligible physical " +
            "gameplay-key generation; Turbo repeats do not create extra intent. The enemy must still be the exact " +
            "canonical opponent, alive, targetable, within Miracle's native 10-yalm range and line of sight. " +
            "The MCH/SAM opportunity lasts 500 ms and the VPR opportunity 250 ms. " +
            "Nest waits until Hardened Scales is actually absent, so Miracle is never deliberately spent into " +
            "Viper's CC immunity. Self Purify wins first, then Ally Rescue, then this helper. State and input are " +
            "consumed before one native Miracle attempt; there is no selected-target change, fallback, or retry. " +
            "Client acceptance does not prove the startup was interrupted; live validation is still required.");
        ImGui.PopTextWrapPos();
        return changed;
    }

    private bool DrawAllyRescueControls()
    {
        var changed = false;
        changed |= Checkbox(
            "Use Paean/Aquaveil once on the next fresh gameplay key",
            configuration.ExperimentalAllyRescueOnNextKey,
            value => configuration.ExperimentalAllyRescueOnNextKey = value);
        changed |= Checkbox(
            "A held gameplay key may trigger when an ally is crowd-controlled (includes WASD)",
            configuration.AllyRescueOnHeldGameplayKey,
            value => configuration.AllyRescueOnHeldGameplayKey = value);
        ImGui.TextUnformatted("Exact ally triggers: Stun, Silence, Deep Freeze, Miracle of Nature");
        ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
        ImGui.TextDisabled(
            "CC-only and self-excluding. BRD uses The Warden's Paean; WHM uses Aquaveil, independent of client " +
            "language. The target must be an exact party member in the action's native range and line of sight. " +
            "Priority is lowest HP%, then highest current incoming enemy pressure, then lowest trusted MP%, then " +
            "distance and stable party order. Self Purify wins if both helpers could claim the same physical input. " +
            "There is no extra local cooldown gate: after these checks, FFXIV's native action call decides whether " +
            "the attempt can queue or execute. One input generation makes at most one attempt; it is consumed before " +
            "the call and is never retried. A blue CLEANSED card and the counters advance only for the exact server " +
            "RecoveredFromStatusEffect result (effect type 0x10). Heavy and Bind intentionally do not trigger this " +
            "experiment.");
        ImGui.PopTextWrapPos();
        return changed;
    }

    private bool DrawPurifyControls()
    {
        var changed = false;
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
        changed |= Checkbox(
            "Deep Freeze",
            configuration.PurifyOnDeepFreeze,
            value => configuration.PurifyOnDeepFreeze = value);
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
        return changed;
    }

    private bool DrawSeitonTab()
    {
        var changed = false;
        ImGui.Spacing();
        ImGui.TextUnformatted("Native nameplate indicators");
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

        changed |= Slider(
            "Persistent cue scale",
            configuration.PersistentCueScale,
            0.55f,
            1.8f,
            value => configuration.PersistentCueScale = value,
            "%.2f x");
        changed |= Checkbox(
            "Entry pop animation",
            configuration.ShowSeitonPopup,
            value => configuration.ShowSeitonPopup = value);
        changed |= Slider(
            "Popup duration",
            configuration.PopupDurationMilliseconds,
            300f,
            2000f,
            value => configuration.PopupDurationMilliseconds = value,
            "%.0f ms");
        changed |= Slider(
            "Entry pop size",
            configuration.PopupIconSize,
            48f,
            140f,
            value => configuration.PopupIconSize = value,
            "%.0f px");
        changed |= Slider(
            "Cue horizontal position",
            configuration.PopupScreenX,
            0.05f,
            0.95f,
            value => configuration.PopupScreenX = value,
            "%.2f");
        changed |= Slider(
            "Cue vertical position",
            configuration.PopupScreenY,
            0.08f,
            0.9f,
            value => configuration.PopupScreenY = value,
            "%.2f");
        changed |= Slider(
            "Cue background",
            configuration.PopupBackgroundOpacity,
            0f,
            1f,
            value => configuration.PopupBackgroundOpacity = value,
            "%.2f");

        return changed;
    }

    private bool DrawAssistTab()
    {
        var changed = false;
        ImGui.Spacing();
        ImGui.TextColored(new Vector4(0.3f, 0.8f, 1f, 1f), "CC MACRO TARGET HELPERS (OPT-IN)");
        changed |= Checkbox(
            "Enable one-shot /nearassist, /nearhelp, and /farhelp targeting",
            configuration.EnableNearAssistMacro,
            value => configuration.EnableNearAssistMacro = value);
        changed |= Slider(
            "Near Assist ally search distance",
            configuration.NearAssistMaxAllyDistance,
            5f,
            30f,
            value => configuration.NearAssistMaxAllyDistance = value,
            "%.0f yalm");
        changed |= Checkbox(
            "Smart preference: nearby ranged/caster DPS, then melee DPS",
            configuration.NearAssistPreferDamageRoles,
            value => configuration.NearAssistPreferDamageRoles = value);
        changed |= Checkbox(
            "Prefer allies attacking the highest team-pressure target",
            configuration.NearAssistPreferTeamPressure,
            value => configuration.NearAssistPreferTeamPressure = value);
        ImGui.TextDisabled(
            "Team-pressure preference is independent and opt-in. If no valid pressure candidate exists, the " +
            "normal smart/nearest selection and then your original <t> target remain the fallback.");

        ImGui.Separator();
        ImGui.TextUnformatted("Assist-first macro with vanilla <t> fallback:");
        ImGui.TextColored(new Vector4(0.5f, 1f, 0.65f, 1f), "/mlock");
        ImGui.TextColored(new Vector4(0.85f, 0.9f, 1f, 1f), "/nearassist");
        ImGui.TextColored(new Vector4(0.85f, 0.9f, 1f, 1f), "/pvpac \"Ability\" <e1>");
        ImGui.TextColored(new Vector4(0.85f, 0.9f, 1f, 1f), "/pvpac \"Ability\" <t>");
        ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
        ImGui.TextDisabled(
            "Crystalline Conflict only. /nearassist arms one 750 ms token for the immediately following hostile " +
            "macro action. Smart preference considers only allies whose distance from you is at most the nearest " +
            "valid candidate's distance plus 8 yalms, then favors ranged/caster DPS, melee DPS, and finally support; " +
            "disabling it uses strict nearest distance. Only that ally's exact native <e1>-<e5> hard target is " +
            "considered. The chosen enemy and native range/line-of-sight are checked for the actual action. The <e1> " +
            "line is only a reliable carrier: Seiton replaces its target with the selected ally's exact e-slot. If no " +
            "redirect is possible, only that carrier attempt is invalidated and the following vanilla <t> line remains " +
            "your fallback. This also works when you started without an own target. " +
            "The compact two-line /nearassist + <t> form remains supported when you already have a target. /mlock " +
            "prevents Turbo Hotbar from restarting this macro before its fallback line. Turbo Hotbar " +
            "may repeat the authored macro, but Seiton adds no repeat or retry " +
            "of its own. It never visibly changes your selected target or sends an action by itself. Disable the " +
            "standalone NearAssist plugin before using this command; /ssassist remains the collision-free alias.");
        ImGui.PopTextWrapPos();

        ImGui.Separator();
        ImGui.TextUnformatted("Lowest-health ally first, with vanilla <t> fallback:");
        ImGui.TextColored(new Vector4(0.5f, 1f, 0.65f, 1f), "/mlock");
        ImGui.TextColored(new Vector4(0.85f, 0.9f, 1f, 1f), "/nearhelp");
        ImGui.TextColored(new Vector4(0.85f, 0.9f, 1f, 1f), "/pvpac \"Ability\" <2>");
        ImGui.TextColored(new Vector4(0.85f, 0.9f, 1f, 1f), "/pvpac \"Ability\" <t>");
        ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
        ImGui.TextDisabled(
            "Near Help considers live non-self party members only after it sees the actual friendly PvP action. " +
            "It keeps only targets inside that action's native range and line of sight, then chooses the lowest " +
            "HP percentage; equal health uses shorter distance and stable actor identity. The <2> line is only a " +
            "carrier. If no valid ally exists, Seiton invalidates that carrier so the authored <t> line remains " +
            "the normal fallback. /mlock prevents Turbo Hotbar from restarting the macro before its fallback line. " +
            "No visible target change, direct action, retry, or automatic self-heal is performed.");
        ImGui.PopTextWrapPos();

        ImGui.Separator();
        ImGui.TextUnformatted("Farthest reachable mobility ally (no target fallback):");
        ImGui.TextColored(new Vector4(0.5f, 1f, 0.65f, 1f), "/mlock");
        ImGui.TextColored(new Vector4(0.85f, 0.9f, 1f, 1f), "/farhelp");
        ImGui.TextColored(new Vector4(0.85f, 0.9f, 1f, 1f), "/pvpac \"Mobility Ability\" <me>");
        ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
        ImGui.TextDisabled(
            "Far Help accepts only the reviewed PvP movement actions Guardian, Icarus, Thunderclap, " +
            "Aetherial Manipulation, and Slither. It checks the actual action's " +
            "native range and line of sight against exact live non-self party members, prefers healers and " +
            "physical/magical ranged jobs, then all other jobs, and chooses the farthest reachable ally inside " +
            "that tier. Guardian additionally requires a strict distance below 10 yalms. " +
            "Use exactly the three lines shown: there is deliberately no <t> fallback. All five actions cannot " +
            "target self, so <me> stays intrinsically invalid without a valid redirect, even if no token or hook is " +
            "available. No valid ally therefore means no movement; Far Help never uses your selected target or self " +
            "instead. One immediately following legacy same-action <t> call is suppressed for migration; remove that " +
            "old fourth line. /mlock prevents Turbo Hotbar from restarting the held macro. No visible target change, " +
            "direct action, or retry is added. /ssfar is the collision-free alias.");
        ImGui.PopTextWrapPos();

        return changed;
    }

    private bool DrawTargetsTab()
    {
        var changed = false;
        ImGui.Spacing();
        ImGui.TextWrapped(
            "Focus Glow is the former Super Focus Glow renderer inside Seiton Sense. Current Target reads only " +
            "your manually selected hard target; it never selects or changes a target.");

        ImGui.TextUnformatted("Focus target");
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
            changed |= DrawFocusAppearance();
            ImGui.TreePop();
        }

        ImGui.Separator();
        ImGui.TextUnformatted("Current hard target");
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
            "nameplates, native job icons, native health bars, or Seiton's indicator slots.");
        ImGui.PopTextWrapPos();
        changed |= Slider(
            "Target HUD horizontal position",
            configuration.CurrentTargetInfoScreenX,
            0.02f,
            0.98f,
            value => configuration.CurrentTargetInfoScreenX = value,
            "%.2f");
        changed |= Slider(
            "Target HUD vertical position",
            configuration.CurrentTargetInfoScreenY,
            0.02f,
            0.98f,
            value => configuration.CurrentTargetInfoScreenY = value,
            "%.2f");
        changed |= Slider(
            "Target HUD scale",
            configuration.CurrentTargetInfoScale,
            0.55f,
            1.8f,
            value => configuration.CurrentTargetInfoScale = value,
            "%.2f x");
        if (ImGui.TreeNode("Current-target appearance"))
        {
            changed |= DrawCurrentTargetAppearance();
            ImGui.TreePop();
        }

        return changed;
    }

    private bool DrawFocusAppearance()
    {
        var changed = false;
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
        return changed;
    }

    private bool DrawCurrentTargetAppearance()
    {
        var changed = false;
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
        return changed;
    }

    private bool DrawAdvancedTab()
    {
        var changed = false;
        ImGui.Spacing();
        ImGui.TextUnformatted("Shared native-nameplate appearance");
        changed |= Slider(
            "Extra icon size",
            configuration.NameplateIconScale,
            0.55f,
            1.5f,
            value => configuration.NameplateIconScale = value,
            "%.2f x native");
        changed |= Slider(
            "Extra icon spacing",
            configuration.NameplateIconSpacing,
            0f,
            12f,
            value => configuration.NameplateIconSpacing = value,
            "%.1f px");
        changed |= Slider(
            "Extra icon background",
            configuration.NameplateBackgroundOpacity,
            0f,
            1f,
            value => configuration.NameplateBackgroundOpacity = value,
            "%.2f");

        ImGui.Separator();
        ImGui.TextUnformatted("Live diagnostics");
        ImGui.TextWrapped($"{tracker.Diagnostics.ToChatLine()}, native-anchors={overlay.NativeAnchorCount}");
        var personal = personalStatus.Snapshot;
        var mchLimitBreak = personalStatus.MachinistLimitBreakDiagnostics;
        var rescue = personalStatus.AllyRescueDiagnostics;
        var miracle = personalStatus.MiracleInterceptDiagnostics;
        ImGui.TextWrapped(
            $"Personal statuses={personal.Statuses.Length}, Purify={personal.Purify.Phase}/" +
            $"{personal.Purify.Decision}, cancel={personal.Purify.CancelReason}, " +
            $"trigger={personal.Purify.InputTrigger}, ready={personal.Purify.LocallyReady}, " +
            $"fresh={personal.Purify.FreshGameplayKey}, held={personal.Purify.HeldGameplayKey}, " +
            $"attempt={personal.Purify.UseActionAttempted}/{personal.Purify.UseActionAccepted}, " +
            $"buffered={personal.Purify.BufferRemainingMilliseconds} ms");
        ImGui.TextWrapped(
            $"MCH LB capture: hook={mchLimitBreak.CaptureRunning}, queue={mchLimitBreak.QueueDepth}, " +
            $"accepted={mchLimitBreak.AcceptedWarnings}, active={mchLimitBreak.WarningActive}, " +
            $"errors={mchLimitBreak.CaptureErrors}, drops={mchLimitBreak.DroppedWarnings}");
        ImGui.TextWrapped(
            $"Ally Rescue: {rescue.Phase}/{rescue.Decision}, cancel={rescue.CancelReason}, " +
            $"trigger={rescue.InputTrigger}, candidates={rescue.CandidateCount}, action={rescue.ActionId}, " +
            $"target={rescue.TargetGameObjectId:X}, status={rescue.TargetStatusId}, ready={rescue.LocallyReady}, " +
            $"attempt={rescue.UseActionAttempted}/{rescue.UseActionAccepted}, " +
            $"count={rescue.AttemptCount}/{rescue.AcceptedCount}, confirm-pending={rescue.ConfirmationPending}, " +
            $"confirmed={rescue.MatchConfirmations.TotalConfirmed}/{rescue.SessionConfirmations.TotalConfirmed}, " +
            $"capture/drop={rescue.ConfirmationCaptureCount}/{rescue.ConfirmationDropCount}");
        ImGui.TextWrapped(
            $"Miracle intercept: {miracle.Phase}/{miracle.Threat}, target={miracle.TargetGameObjectId:X}/" +
            $"{miracle.TargetEntityId:X}, job={miracle.TargetJobId}, remaining={miracle.ThreatRemainingMilliseconds} ms, " +
            $"protection={miracle.HardenedScalesPresent}/{miracle.OtherCcProtectionPresent}, " +
            $"range/LoS={miracle.HasNativeRangeAndLineOfSight}, key={miracle.InputKey}, " +
            $"attempt={miracle.UseActionAttempted}/{miracle.UseActionAccepted}, " +
            $"count={miracle.AttemptCount}/{miracle.AcceptedCount}, " +
            $"capture/queue/drop={miracle.CapturedThreatCount}/{miracle.CaptureQueueDepth}/{miracle.DroppedThreatCount}, " +
            $"last={miracle.LastEvent}");

        ImGui.Separator();
        ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
        ImGui.TextDisabled(
            "Guard cooldown is shown only after this client actually observed that enemy's Guard. Unknown " +
            "cooldowns are never guessed. Seiton Sense never changes your selected hard, soft, or focus target " +
            "and uploads no gameplay data to an external service. Near Assist, Near Help, and Far Help may replace only " +
            "the target ID on one armed macro action. The optional Purify, Ally Rescue, and Miracle experiments " +
            "can each initiate at most one exact action attempt from one shared physical input generation, in " +
            "that priority order. All helpers are " +
            "disabled by default. Like all third-party modifications, use " +
            "it at your own risk.");
        ImGui.PopTextWrapPos();
        return changed;
    }

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
