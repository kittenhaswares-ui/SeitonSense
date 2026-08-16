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
    private readonly IsolationAwarenessService isolationAwareness;
    private readonly PressureCounterWindow pressureCounter;

    public SettingsWindow(
        PluginConfiguration configuration,
        ExecuteTracker tracker,
        PersonalStatusService personalStatus,
        OverlayRenderer overlay,
        TargetPressureTracker pressureTracker,
        IsolationAwarenessService isolationAwareness,
        PressureCounterWindow pressureCounter)
        : base("Seiton Sense###SeitonSenseSettings")
    {
        this.configuration = configuration;
        this.tracker = tracker;
        this.personalStatus = personalStatus;
        this.overlay = overlay;
        this.pressureTracker = pressureTracker;
        this.isolationAwareness = isolationAwareness;
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
            value =>
            {
                configuration.Enabled = value;
                if (value) return;
                overlay.PreviewEnabled = false;
                overlay.CcProtectionPreviewEnabled = false;
                overlay.ResourceAuraPreviewEnabled = false;
                overlay.IsolationWarningPreviewEnabled = false;
                pressureCounter.PreviewEnabled = false;
            });

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

            if (ImGui.BeginTabItem("Jobs"))
            {
                changed |= DrawJobsTab();
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
        overlay.ResourceAuraPreviewEnabled = false;
        overlay.IsolationWarningPreviewEnabled = false;
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
        ImGui.TextUnformatted("Preview and reset");
        if (ImGui.Button(overlay.PreviewEnabled ? "Stop preview" : "Preview HUD + warnings"))
        {
            overlay.PreviewEnabled = !overlay.PreviewEnabled;
            if (overlay.PreviewEnabled)
            {
                overlay.CcProtectionPreviewEnabled = false;
                overlay.ResourceAuraPreviewEnabled = false;
            }
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
            overlay.ResourceAuraPreviewEnabled = false;
            overlay.IsolationWarningPreviewEnabled = false;
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
        ImGui.TextUnformatted("Spatial awareness");
        changed |= Checkbox(
            "Warn when no party ally is within 20y and line of sight",
            configuration.WarnWhenIsolated,
            value => configuration.WarnWhenIsolated = value);
        changed |= Slider(
            "Isolation warning size",
            configuration.IsolationWarningScale,
            0.75f,
            1.75f,
            value => configuration.IsolationWarningScale = value,
            "%.2f x");
        if (ImGui.Button(
                overlay.IsolationWarningPreviewEnabled
                    ? "Stop isolation warning preview"
                    : "Preview isolation warning"))
        {
            overlay.IsolationWarningPreviewEnabled = !overlay.IsolationWarningPreviewEnabled;
        }
        ImGui.TextDisabled(
            "CC only. Requires an exact five-player party and FFXIV's native 20y range/line-of-sight result. " +
            "Unknown data stays silent.");
        ImGui.TextDisabled(isolationAwareness.Diagnostics.ToChatLine());

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
            if (overlay.CcProtectionPreviewEnabled)
            {
                overlay.PreviewEnabled = false;
                overlay.ResourceAuraPreviewEnabled = false;
            }
        }
        ImGui.TextDisabled(
            "A large static crossed-CC emblem is anchored above the native job icon for Guard, Resilience, " +
            "SAM, WAR, VPR and large-scale PvP immunity.");
        ImGui.TextDisabled("Ambiguous one-hit wards are intentionally not labelled as full immunity.");

        ImGui.Separator();
        ImGui.TextUnformatted("General native-nameplate resource cues");
        changed |= DrawGeneralResourceNameplateControls();

        return changed;
    }

    private bool DrawDefensiveUtilityControls()
    {
        var changed = false;
        changed |= Checkbox(
            "Enable defensive one-action utilities",
            configuration.EnableDefensiveUtilities,
            value => configuration.EnableDefensiveUtilities = value);
        changed |= Checkbox(
            "A held gameplay key may supply the one physical input generation (includes WASD)",
            configuration.DefensiveUtilitiesOnHeldKey,
            value => configuration.DefensiveUtilitiesOnHeldKey = value);
        ImGui.TextColored(
            configuration.EnableDefensiveUtilities
                ? new Vector4(0.35f, 0.9f, 1f, 1f)
                : new Vector4(0.7f, 0.72f, 0.78f, 1f),
            configuration.EnableDefensiveUtilities
                ? "ON — exact defensive rules may claim one eligible physical input in CC."
                : "OFF — no Purify, Guard, or Guardian request is added by this module.");
        ImGui.TextUnformatted("Rules:");
        changed |= Checkbox(
            "At 3+ incoming enemies and Stun: Purify, then Guard on a later input",
            configuration.GuardOnStunPressure,
            value => configuration.GuardOnStunPressure = value);
        changed |= Checkbox(
            "Pre-Guard at 50% HP or lower with 3+ incoming enemies",
            configuration.PreGuardOnLowHpPressure,
            value => configuration.PreGuardOnLowHpPressure = value);
        changed |= Checkbox(
            "PLD Guardian for an ally at 20% HP or lower",
            configuration.PaladinGuardianLowAlly,
            value => configuration.PaladinGuardianLowAlly = value);
        changed |= Checkbox(
            "After accepted Auto Guardian: Quick Chat + Bind pair (party-visible)",
            configuration.PaladinGuardianAnnounceAndMark,
            value => configuration.PaladinGuardianAnnounceAndMark = value);
        ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
        ImGui.TextDisabled(
            "Crystalline Conflict only and disabled by default. One physical key generation can produce at most one " +
            "Seiton Sense action request. If high-pressure Stun triggers Purify, Guard is allowed only after live " +
            "Resilience confirms the cleanse, the removable CC is gone, and you release/repress for a new physical " +
            "generation; Purify and Guard never fire from the same generation. Pre-Guard is a risk reaction, not a " +
            "prediction of an instant future stun, and it yields while removable CC is already present.");
        ImGui.TextDisabled(
            "Guardian additionally requires PLD, the exact non-self party ally alive and targetable within FFXIV's " +
            "native 20-yalm action range/line of sight, and both your own Guard and Guardian available. There is no " +
            "custom center-distance cap; the 10-yalm condition is the protection leash after the jump. Lowest HP% " +
            "wins, then known higher incoming pressure and shorter distance. An accepted automatic Guardian request " +
            "shows a 1.5-second GUARDIAN TRIGGERED card for the selected party slot; CLIENT ACCEPTED does not prove " +
            "that the server applied protection. While your own Guard is active, and for " +
            "the bounded 1.5-second status-propagation interval after an exact Guard request, every Seiton Sense " +
            "action-request helper is blocked, so none can cancel Guard. Manual game actions and another plugin's " +
            "repeats remain outside that boundary and can still end Guard normally.");
        ImGui.TextDisabled(
            "The separate communication opt-in runs only after this module's automatic Guardian request is client-" +
            "accepted in exact Crystalline Conflict. It uses localized CC Quick Chat row 35 (Ziel decken, displayed " +
            "as Ich decke ...) for the frozen exact P-slot, then places Bind2 on that slot followed by Bind1 on self. " +
            "If either sign is occupied or marker state is uncertain, the marker sequence is not started. Bind2 must " +
            "be confirmed on the exact ally before Bind1 is attempted; if Bind1 then fails, only the proven-owned " +
            "Bind2 may be cleaned. A complete pair expires nine seconds after Guardian acceptance. Cleanup tries " +
            "Bind2 and then Bind1, each only while its exact actor/sign/timestamp ownership remains proven; drift is " +
            "relinquished rather than cleared.");
        ImGui.TextDisabled(
            "Communication never changes a hard, soft, focus, or mouseover target, initiates another combat action, " +
            "selects an alternate, falls back, or retries. A command issued is not proof that chat or signs appeared. " +
            "Localized Quick Chat syntax, party visibility, pair placement, and cleanup remain current-patch live-" +
            "confirmation boundaries.");
        ImGui.PopTextWrapPos();
        return changed;
    }

    private bool DrawReactiveCcControls()
    {
        var changed = false;
        changed |= Checkbox(
            "Enable reactive counter-CC",
            configuration.EnableReactiveCcUtilities,
            value => configuration.EnableReactiveCcUtilities = value);
        changed |= Checkbox(
            "A held gameplay key may trigger when an opportunity appears (includes WASD)",
            configuration.ReactiveCcOnHeldKey,
            value => configuration.ReactiveCcOnHeldKey = value);
        ImGui.TextColored(
            configuration.EnableReactiveCcUtilities
                ? new Vector4(0.35f, 0.9f, 1f, 1f)
                : new Vector4(0.7f, 0.72f, 0.78f, 1f),
            configuration.EnableReactiveCcUtilities
                ? "ON — WHM Miracle or BRD Silent Nocturne may claim one eligible input in CC."
                : "OFF — threat capture is inactive and no counter-CC attempt can occur.");

        ImGui.TextUnformatted("BRD / WHM triggers:");
        changed |= Checkbox(
            "DNC Contradance startup",
            configuration.ReactiveCcDancerLimitBreak,
            value => configuration.ReactiveCcDancerLimitBreak = value);
        changed |= Checkbox(
            "After enemy Purify: all six removable CC types, team focus 2+",
            configuration.ReactiveCcAfterEnemyPurify,
            value => configuration.ReactiveCcAfterEnemyPurify = value);

        ImGui.TextUnformatted("Additional WHM-only urgent startup triggers:");
        changed |= Checkbox(
            "MCH Marksman's Spite",
            configuration.MiracleInterceptMchLimitBreak,
            value => configuration.MiracleInterceptMchLimitBreak = value);
        changed |= Checkbox(
            "SAM Zantetsuken",
            configuration.MiracleInterceptSamZantetsuken,
            value => configuration.MiracleInterceptSamZantetsuken = value);
        changed |= Checkbox(
            "VPR Furious Backlash / Nest der Blutschuppen",
            configuration.MiracleInterceptViperNest,
            value => configuration.MiracleInterceptViperNest = value);

        if (ImGui.Button("Preview AUTO CC LANDED flash"))
            overlay.TriggerMiracleInterceptConfirmationPreview();
        ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
        ImGui.TextDisabled(
            "Experimental, CC-only, and disabled by default. WHM uses Miracle of Nature at its native 10-yalm range; " +
            "BRD uses Silent Nocturne at its native 20-yalm range. The enemy must remain the exact canonical opponent, " +
            "alive, targetable, in native range and line of sight, and free of verified protection for that counter. " +
            "Contradance uses its exact startup signal. The post-Purify rule accepts Stun, Heavy, Bind, Silence, Deep " +
            "Freeze, or Miracle of Nature, observes real Resilience and waits for its stable disappearance. It also " +
            "requires that enemy to be your exact hard target and at least one ally's hard target (team focus 2+). " +
            "Viper waits until Hardened Scales is actually absent.");
        ImGui.TextDisabled(
            "One already-eligible physical generation makes at most one exact-target attempt; Turbo pulses add no " +
            "intent. Self Purify, defensive utilities, and Ally Rescue have priority. Input is consumed before the " +
            "native call, with no selected-target change, fallback, or retry. The blue AUTO CC LANDED flash appears " +
            "only after the matching Miracle or Silence status is captured on that exact pending enemy. It confirms " +
            "the counter-CC landed, not conclusively that Contradance, another LB, or its damage was interrupted.");
        ImGui.PopTextWrapPos();
        return changed;
    }

    private bool DrawAutoEnemyFocusMarkControls()
    {
        var changed = false;
        changed |= Checkbox(
            "Automatically place the team-visible Attack1 enemy sign",
            configuration.EnableAutoEnemyFocusMark,
            value => configuration.EnableAutoEnemyFocusMark = value);
        ImGui.TextColored(
            configuration.EnableAutoEnemyFocusMark
                ? new Vector4(1f, 0.78f, 0.3f, 1f)
                : new Vector4(0.7f, 0.72f, 0.78f, 1f),
            configuration.EnableAutoEnemyFocusMark
                ? "ON — this can visibly change the shared Attack1 sign for your team."
                : "OFF — no party-visible enemy sign command is issued.");
        ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
        ImGui.TextDisabled(
            "Exact Crystalline Conflict only and disabled by default. A target is eligible only when this client " +
            "observed its Guard on cooldown and it is at 50% HP or lower and/or has trusted low MP. The MP state enters " +
            "after 150 ms below 2,000 and clears after 150 ms at or above 2,300 to prevent threshold flicker. Priority is " +
            "both resources low, then HP-only, then MP-only; ties use lowest HP%, lowest trusted MP%, highest known " +
            "team-target count, and stable enemy slot. This sends the normal /mk attack1 <eN> command and never changes " +
            "your target.");
        ImGui.TextDisabled(
            "An existing Attack1 sign is never overwritten. Seiton Sense clears only a sign whose empty-to-exact-target " +
            "transition it confirmed and whose enemy identity and marker timestamp are still unchanged. If ownership " +
            "cannot be proven, it deliberately leaves the sign alone.");
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

    private bool DrawJobsTab()
    {
        var changed = false;
        ImGui.Spacing();
        ImGui.TextColored(new Vector4(0.3f, 0.8f, 1f, 1f), "ALL JOBS / GENERAL QUALITY OF LIFE");
        changed |= DrawResourceAuraControls();

        ImGui.Separator();
        if (ImGui.CollapsingHeader("All jobs: Defensive utilities", ImGuiTreeNodeFlags.DefaultOpen))
            changed |= DrawDefensiveUtilityControls();

        ImGui.Separator();
        if (ImGui.CollapsingHeader("All jobs: Team-visible enemy focus sign", ImGuiTreeNodeFlags.DefaultOpen))
            changed |= DrawAutoEnemyFocusMarkControls();

        ImGui.Separator();
        if (ImGui.CollapsingHeader("All jobs: CC-immunity brake", ImGuiTreeNodeFlags.DefaultOpen))
            changed |= DrawCcImmunityBrakeControls();

        ImGui.Separator();
        if (ImGui.CollapsingHeader("All jobs: Purify helper", ImGuiTreeNodeFlags.DefaultOpen))
            changed |= DrawPurifyControls();

        ImGui.Separator();
        ImGui.TextColored(new Vector4(0.8f, 0.65f, 1f, 1f), "NINJA");
        changed |= Checkbox(
            "Seiton on fresh gameplay key (experimental)",
            configuration.EnableNinjaSeitonOnFreshGameplayKey,
            value => configuration.EnableNinjaSeitonOnFreshGameplayKey = value);
        ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
        ImGui.TextDisabled(
            "Default off and exact Crystalline Conflict only. On PvP Ninja, one fresh physical gameplay-key down " +
            "edge can request the currently adjusted Seiton Tenchu (29515 or Unsealed follow-up 29516). It considers " +
            "exact canonical S1-S5 enemies that are living, targetable, below 50% HP, and accepted by FFXIV's native " +
            "range/line-of-sight check; the lowest exact HP ratio wins, then stable slot/actor identity. Own Guard or " +
            "its bounded propagation gate blocks the helper, and existing higher-priority helpers win the shared " +
            "input generation.");
        ImGui.TextDisabled(
            "State and input are consumed before at most one native attempt. Seiton Sense never changes the target, " +
            "selects again, chooses an alternate, falls back, replays, or retries; the original gameplay key is not " +
            "swallowed. A client-accepted return is dispatch feedback only, not proof that Seiton landed or killed " +
            "the target.");
        ImGui.PopTextWrapPos();

        ImGui.Spacing();
        changed |= Checkbox(
            "Seiton-ready icon + S-slot (NIN)",
            configuration.ShowNameplateSeiton,
            value => configuration.ShowNameplateSeiton = value);

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

        ImGui.Separator();
        ImGui.TextColored(new Vector4(0.55f, 0.85f, 1f, 1f), "SCHOLAR");
        changed |= Checkbox(
            "Critical Strategy on held gameplay key (Guard targets only, experimental)",
            configuration.EnableScholarCriticalStrategyOnHeldKey,
            value => configuration.EnableScholarCriticalStrategyOnHeldKey = value);
        ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
        ImGui.TextDisabled(
            "Default off, PvP Scholar, and exact Crystalline Conflict only. One shared held physical gameplay-key " +
            "generation may request Critical Strategy (29716) only against a living, targetable exact canonical " +
            "S1-S5 enemy with live Guard (3054 or 3673), verified readiness, and FFXIV's native 25-yalm range/line " +
            "of sight. It is never spent as the ordinary 10% damage-taken debuff: on Guard, the current official " +
            "effect instead halves Guard's defensive bonus for 10 seconds.");
        ImGui.TextDisabled(
            "Selection requires the complete unique S1-S5 set. If every eligible guarded candidate has one active, " +
            "exact, non-negative team-pressure count and any count is positive, highest team pressure wins, then " +
            "lowest exact HP ratio. Any unknown/negative pressure, or all-zero pressure, makes the entire selection " +
            "HP-first. Stable S-slot, entity ID, and game-object ID break exact ties. Pressure is selection-only and " +
            "is not revalidated as a final dispatch gate.");
        ImGui.TextDisabled(
            "The frozen intent and shared held-key generation are consumed before at most one native attempt. It " +
            "then revalidates only exact identity, action readiness, live Guard, and native range/line of sight. It " +
            "never changes a hard, soft, focus, or mouseover target, reranks, selects an alternate after drift, " +
            "substitutes another action, falls back, replays, or retries. The original key is not swallowed, and " +
            "client acceptance does not prove that Critical Strategy landed or changed Guard.");
        ImGui.PopTextWrapPos();

        ImGui.Separator();
        ImGui.TextColored(new Vector4(0.92f, 0.7f, 0.35f, 1f), "MONK");
        changed |= DrawMonkEarthReplyControls();

        ImGui.Separator();
        ImGui.TextColored(new Vector4(0.4f, 0.85f, 1f, 1f), "BARD / WHITE MAGE");
        if (ImGui.CollapsingHeader("Ally Rescue: Paean / Aquaveil", ImGuiTreeNodeFlags.DefaultOpen))
        {
            changed |= DrawAllyRescueControls();
            ImGui.Spacing();
            DrawAllyRescueOverview();
        }

        ImGui.Separator();
        if (ImGui.CollapsingHeader(
                "Reactive counter-CC: Silent Nocturne / Miracle of Nature",
                ImGuiTreeNodeFlags.DefaultOpen))
        {
            changed |= DrawReactiveCcControls();
        }

        return changed;
    }

    private bool DrawCcImmunityBrakeControls()
    {
        var changed = false;
        changed |= Checkbox(
            "Brake selected CC actions against verified immunity",
            configuration.EnableCcImmunityBrake,
            value => configuration.EnableCcImmunityBrake = value);
        ImGui.TextColored(
            configuration.EnableCcImmunityBrake
                ? new Vector4(0.35f, 0.9f, 1f, 1f)
                : new Vector4(0.7f, 0.72f, 0.78f, 1f),
            configuration.EnableCcImmunityBrake
                ? "ON — enabled jobs and actions are checked on every real press/pulse."
                : "OFF — all action attempts pass through unchanged.");

        ImGui.Spacing();
        changed |= DrawCcBrakeJob(19, "PALADIN", (29065, "Intervene"));
        changed |= DrawCcBrakeJob(21, "WARRIOR", (29081, "Blota"));
        changed |= DrawCcBrakeJob(
            23,
            "BARD",
            (29395, "Silent Nocturne"),
            (29399, "Repelling Shot"));
        changed |= DrawCcBrakeJob(24, "WHITE MAGE", (29228, "Miracle of Nature"));
        changed |= DrawCcBrakeJob(25, "BLACK MAGE", (41510, "Lethargy"));
        changed |= DrawCcBrakeJob(
            30,
            "NINJA",
            (29510, "Forked Raiju"),
            (29707, "Fleeting Raiju"));
        changed |= DrawCcBrakeJob(31, "MACHINIST", (29407, "Air Anchor"));
        changed |= DrawCcBrakeJob(33, "ASTROLOGIAN", (29244, "Gravity II (including Double Cast)"));
        changed |= DrawCcBrakeJob(34, "SAMURAI", (29535, "Mineuchi"));

        ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
        ImGui.TextDisabled(
            "Crystalline Conflict only. This works directly from a hotbar; no macro is required. For the reviewed " +
            "single/primary-target list above, an enabled action aimed at an exactly identified enemy with verified " +
            "protection against that exact CC is stopped before the downstream game action for that one incoming " +
            "attempt. Miracle uses its own verified matrix, including VPR-only Hardened Scales. The action, target and " +
            "input are never stored, replayed, changed to an alternative, or retried by Seiton Sense.");
        ImGui.TextDisabled(
            "A later real press or Turbo Hotbar pulse is checked again and can pass as soon as protection is gone. " +
            "Vanilla key holding does not generate repeats by itself. The brake blocks the whole selected action, " +
            "including any damage or movement attached to it, so disable individual actions to taste. Broad cone, " +
            "ground and ambiguous multi-target CC is deliberately excluded. A downstream plugin that rewrites the " +
            "target after Seiton Sense can override this safety boundary; test plugin order before relying on it. " +
            "Unknown or ambiguous state passes through unchanged.");
        ImGui.PopTextWrapPos();
        return changed;
    }

    private bool DrawCcBrakeJob(
        uint jobId,
        string jobName,
        params (uint ActionId, string Name)[] actions)
    {
        var changed = false;
        changed |= Checkbox(
            $"{jobName}##CcBrakeJob{jobId}",
            configuration.IsCcBrakeJobEnabled(jobId),
            value => configuration.SetCcBrakeJobEnabled(jobId, value));

        ImGui.Indent(24f * ImGuiHelpers.GlobalScale);
        foreach (var (actionId, name) in actions)
        {
            changed |= Checkbox(
                $"{name}##CcBrakeAction{actionId}",
                configuration.IsCcBrakeActionEnabled(actionId),
                value => configuration.SetCcBrakeActionEnabled(actionId, value));
        }

        ImGui.Unindent(24f * ImGuiHelpers.GlobalScale);
        return changed;
    }

    private bool DrawResourceAuraControls()
    {
        var changed = false;
        changed |= Checkbox(
            "Show low-resource aura",
            configuration.EnableResourceAura,
            value => configuration.EnableResourceAura = value);
        changed |= Checkbox(
            "Native action-hotbar aura",
            configuration.ResourceAuraOnSelfHotbars,
            value => configuration.ResourceAuraOnSelfHotbars = value);
        changed |= Checkbox(
            "Party-list row aura",
            configuration.ResourceAuraOnPartyRows,
            value => configuration.ResourceAuraOnPartyRows = value);
        ImGui.SameLine();
        changed |= Checkbox(
            "CC team-list row aura",
            configuration.ResourceAuraOnCcTeamRows,
            value => configuration.ResourceAuraOnCcTeamRows = value);
        changed |= SliderInt(
            "Low HP threshold",
            configuration.ResourceAuraHpPercent,
            10,
            80,
            value => configuration.ResourceAuraHpPercent = value,
            "%d%%");
        changed |= SliderInt(
            "Low MP threshold",
            configuration.ResourceAuraMpThreshold,
            0,
            10_000,
            value => configuration.ResourceAuraMpThreshold = value,
            "%d MP");
        changed |= Slider(
            "Resource aura intensity",
            configuration.ResourceAuraIntensity,
            0.1f,
            1.5f,
            value => configuration.ResourceAuraIntensity = value,
            "%.2f x");
        changed |= Slider(
            "Resource aura pulse speed",
            configuration.ResourceAuraPulseSpeed,
            0.2f,
            2f,
            value => configuration.ResourceAuraPulseSpeed = value,
            "%.2f Hz");
        if (ImGui.Button(overlay.ResourceAuraPreviewEnabled ? "Stop resource-aura preview" : "Preview resource aura"))
        {
            overlay.ResourceAuraPreviewEnabled = !overlay.ResourceAuraPreviewEnabled;
            if (overlay.ResourceAuraPreviewEnabled)
            {
                overlay.PreviewEnabled = false;
                overlay.CcProtectionPreviewEnabled = false;
            }
        }
        ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
        ImGui.TextDisabled(
            "Red means low HP, blue means trusted low MP, and purple means both. The module draws a read-only " +
            "aura around native action hotbars and the selected team-list rows; it never changes a bar, target, or action. " +
            "Each surface can be disabled independently. Unknown MP never produces a blue warning.");
        ImGui.PopTextWrapPos();
        return changed;
    }

    private bool DrawMonkEarthReplyControls()
    {
        var changed = false;
        changed |= Checkbox(
            "Use Earth's Reply automatically while Earth Resonance is active",
            configuration.EnableMonkEarthReplyHelper,
            value => configuration.EnableMonkEarthReplyHelper = value);
        changed |= Checkbox(
            "Reply at low HP",
            configuration.MonkEarthReplyOnLowHp,
            value => configuration.MonkEarthReplyOnLowHp = value);
        ImGui.SameLine();
        changed |= Checkbox(
            "Reply shortly before the effect expires",
            configuration.MonkEarthReplyBeforeExpiry,
            value => configuration.MonkEarthReplyBeforeExpiry = value);
        changed |= SliderInt(
            "Earth's Reply HP threshold",
            configuration.MonkEarthReplyHpPercent,
            10,
            80,
            value => configuration.MonkEarthReplyHpPercent = value,
            "%d%%");
        changed |= Slider(
            "Reply before expiry",
            configuration.MonkEarthReplyExpirySeconds,
            0.5f,
            2.5f,
            value => configuration.MonkEarthReplyExpirySeconds = value,
            "%.2f s");
        ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
        ImGui.TextDisabled(
            "PvP MNK only and disabled by default. This detonates the already-active Earth's Reply / Echo der Erde " +
            "at the enabled low-HP threshold or before the 8-second Earth Resonance timer is lost. It never starts " +
            "Riddle of Earth / Steinernes Enigma, changes your target, or retries a rejected action.");
        ImGui.PopTextWrapPos();
        return changed;
    }

    private bool DrawGeneralResourceNameplateControls()
    {
        var changed = false;
        changed |= Checkbox(
            "Crossed Guard while observed on cooldown",
            configuration.ShowGuardUnavailable,
            value => configuration.ShowGuardUnavailable = value);
        ImGui.SameLine();
        changed |= Checkbox(
            "Guard countdown",
            configuration.ShowGuardCountdown,
            value => configuration.ShowGuardCountdown = value);
        changed |= Checkbox(
            "Crossed blue elixir below 2,000 trusted MP",
            configuration.ShowLowMp,
            value => configuration.ShowLowMp = value);
        ImGui.TextDisabled(
            "Guard appears only after this client observed the enemy use it; low MP requires a trusted value. " +
            "Unknown cooldowns or resources are never guessed.");
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
        ImGui.TextUnformatted("Near Assist preferences");
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

        ImGui.TextUnformatted("Near Help survival preference");
        changed |= Checkbox(
            "Prefer incoming pressure near the lowest-health target",
            configuration.NearHelpPreferIncomingPressure,
            value => configuration.NearHelpPreferIncomingPressure = value);
        ImGui.TextDisabled(
            "Lowest exact HP is the anchor and always wins at 25% HP or lower. Otherwise, a trusted live pressure " +
            "view may prefer the highest incoming enemy count only within 10 HP percentage points of that anchor; " +
            "lower HP and distance break ties. Missing data inside that window falls back to lowest HP.");

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
        ImGui.TextUnformatted("Lowest-health survival target, with vanilla <t> fallback:");
        ImGui.TextColored(new Vector4(0.5f, 1f, 0.65f, 1f), "/mlock");
        ImGui.TextColored(new Vector4(0.85f, 0.9f, 1f, 1f), "/nearhelp");
        ImGui.TextColored(new Vector4(0.85f, 0.9f, 1f, 1f), "/pvpac \"Ability\" <2>");
        ImGui.TextColored(new Vector4(0.85f, 0.9f, 1f, 1f), "/pvpac \"Ability\" <t>");
        ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
        ImGui.TextDisabled(
            "Near Help resolves the actual friendly PvP action first. It considers exact live party members and may " +
            "also consider you only when that resolved action explicitly supports self-targeting and its native " +
            "target/range/line-of-sight check succeeds. The pressure option uses the bounded survival ranking above. " +
            "The <2> line is only a carrier. If no valid target exists, Seiton invalidates that carrier so the " +
            "authored <t> line remains the normal fallback. /mlock prevents Turbo Hotbar from restarting the macro " +
            "before its fallback line. Near Help redirects only that one incoming action; it does not invent an " +
            "action, visibly change your target, try an alternate candidate, or retry.");
        ImGui.PopTextWrapPos();

        ImGui.Separator();
        ImGui.TextUnformatted("Farthest reachable mobility ally (no target fallback):");
        ImGui.TextColored(new Vector4(0.5f, 1f, 0.65f, 1f), "/mlock");
        ImGui.TextColored(new Vector4(0.85f, 0.9f, 1f, 1f), "/farhelp");
        ImGui.TextColored(new Vector4(0.85f, 0.9f, 1f, 1f), "/pvpac \"Mobility Ability\" <me>");
        ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
        ImGui.TextDisabled(
            "Far Help accepts only the reviewed PvP movement actions Guardian, Icarus, Thunderclap, " +
            "Aetherial Manipulation, and Slither. It checks the actual action's native range and line of sight. " +
            "At action time all five exact native <e1>-<e5> enemy slots must be valid and unique. Confirmed dead " +
            "enemies are ignored for clearance; live enemies count even while untargetable. A destination must have " +
            "strictly more than 10 yalms of horizontal hitbox-edge clearance from every live enemy to enter the preferred " +
            "backline group. The farthest member of that group wins. If none can be certified, or enemy data is missing, " +
            "ambiguous, invalid, or has no live enemy, Far Help instead uses the farthest otherwise valid reachable ally. " +
            "Only an exact distance tie prefers healer, then physical/magical ranged or caster, then every other job. " +
            "This map-agnostic preference cannot guarantee tactical safety. Guardian uses FFXIV's native, hitbox-aware " +
            "20-yalm action range and line of sight with no custom center-distance cap; its 10-yalm condition is the " +
            "protection leash after the jump. " +
            "Use exactly the three lines shown: there is deliberately no <t> fallback. All five actions cannot " +
            "target self, so <me> stays intrinsically invalid without a valid redirect, even if no token or hook is " +
            "available. Only no valid reachable ally means no movement; Far Help never uses your selected target or self " +
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
        ImGui.TextWrapped(
            $"{tracker.Diagnostics.ToChatLine()}, native-anchors={overlay.NativeAnchorCount}, " +
            $"resource-anchors={overlay.ResourceAuraAnchorCount} " +
            $"(hotbar {overlay.ResourceAuraSelfHotbarCount}, party {overlay.ResourceAuraPartyRowCount}, " +
            $"CC rows {overlay.ResourceAuraCcRowCount})");
        var personal = personalStatus.Snapshot;
        var mchLimitBreak = personalStatus.MachinistLimitBreakDiagnostics;
        var defense = personalStatus.DefensiveUtilityDiagnostics;
        var rescue = personalStatus.AllyRescueDiagnostics;
        var miracle = personalStatus.MiracleInterceptDiagnostics;
        var monk = personalStatus.MonkEarthReplyDiagnostics;
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
            $"Defensive utility: active={defense.Active}, action/trigger={defense.Action}/{defense.Trigger}, " +
            $"pressure={defense.PressureKnown}/{defense.IncomingEnemyCount}, guard={defense.GuardActive}, " +
            $"stun={defense.HighPressureStunObserved}, post-Purify={defense.WaitingForPostPurifyGuard}/" +
            $"{defense.PostPurifyGuardRemainingMilliseconds} ms, Guardian candidates={defense.GuardianCandidateCount}, " +
            $"target={defense.TargetGameObjectId:X}/{defense.TargetEntityId:X}, " +
            $"key={defense.FreshGameplayKey}/{defense.HeldGameplayKey}, claim={defense.InputClaimed}, " +
            $"attempt={defense.UseActionAttempted}/{defense.UseActionAccepted}, " +
            $"Guardian popup={defense.GuardianPopup?.PartySlot ?? 0}/" +
            $"{Math.Max(0, (defense.GuardianPopup?.EndsAtMilliseconds ?? 0) - Environment.TickCount64)} ms, " +
            $"count={defense.AttemptCount}/{defense.AcceptedCount}, metadata=" +
            $"{defense.GuardMetadataVerified}/{defense.GuardianMetadataVerified}, last={defense.LastEvent}");
        ImGui.TextWrapped(
            $"Ally Rescue: {rescue.Phase}/{rescue.Decision}, cancel={rescue.CancelReason}, " +
            $"trigger={rescue.InputTrigger}, candidates={rescue.CandidateCount}, action={rescue.ActionId}, " +
            $"target={rescue.TargetGameObjectId:X}, status={rescue.TargetStatusId}, ready={rescue.LocallyReady}, " +
            $"attempt={rescue.UseActionAttempted}/{rescue.UseActionAccepted}, " +
            $"count={rescue.AttemptCount}/{rescue.AcceptedCount}, confirm-pending={rescue.ConfirmationPending}, " +
            $"confirmed={rescue.MatchConfirmations.TotalConfirmed}/{rescue.SessionConfirmations.TotalConfirmed}, " +
            $"capture/drop={rescue.ConfirmationCaptureCount}/{rescue.ConfirmationDropCount}");
        ImGui.TextWrapped(
            $"Reactive CC: {miracle.Phase}/{miracle.Threat}, action={miracle.CounterActionId}, " +
            $"target={miracle.TargetGameObjectId:X}/" +
            $"{miracle.TargetEntityId:X}, job={miracle.TargetJobId}, remaining={miracle.ThreatRemainingMilliseconds} ms, " +
            $"blocker scales/other={miracle.HardenedScalesPresent}/{miracle.OtherCcProtectionPresent}, " +
            $"range/LoS={miracle.HasNativeRangeAndLineOfSight}, key={miracle.InputKey}, " +
            $"attempt={miracle.UseActionAttempted}/{miracle.UseActionAccepted}, " +
            $"count={miracle.AttemptCount}/{miracle.AcceptedCount}, " +
            $"capture/queue/drop={miracle.CapturedThreatCount}/{miracle.CaptureQueueDepth}/{miracle.DroppedThreatCount}, " +
            $"seen/armed/rejected={miracle.RecognizedThreatCount}/{miracle.ArmedThreatCount}/" +
            $"{miracle.RejectedThreatCount}, waits protection/range/input/priority=" +
            $"{miracle.ProtectionWaitCount}/{miracle.RangeWaitCount}/{miracle.NoInputWaitCount}/" +
            $"{miracle.PriorityWaitCount}, expired={miracle.ExpiredThreatCount}, " +
            $"landed={miracle.ConfirmedLandingCount}, confirm-capture/queue/drop=" +
            $"{miracle.CapturedConfirmationCount}/{miracle.ConfirmationQueueDepth}/{miracle.DroppedConfirmationCount}, " +
            $"last={miracle.LastEvent}, last-opportunity={miracle.LastOpportunity}, " +
            $"cleanse-followup={miracle.CleanseFollowupPhase}, removed=" +
            $"{miracle.CleanseFollowupRemovedStatusId}, team-focus={miracle.CleanseFollowupTeamPressure}, target=" +
            $"{miracle.CleanseFollowupTargetGameObjectId:X}/{miracle.CleanseFollowupTargetEntityId:X}, " +
            $"resilience-seen={miracle.CleanseFollowupResilienceObserved}, signal/promote/cancel=" +
            $"{miracle.CleanseFollowupSignalCount}/{miracle.CleanseFollowupPromotionCount}/" +
            $"{miracle.CleanseFollowupCancellationCount}, cleanse-last={miracle.CleanseFollowupLastEvent}");
        ImGui.TextWrapped(
            $"Monk Earth's Reply: {monk.Phase}/{monk.Decision}, reason={monk.Reason}, trigger={monk.Trigger}, " +
            $"resonance={monk.ResonancePresent}/{monk.ResonanceRemainingMilliseconds} ms, " +
            $"HP={monk.CurrentHp}/{monk.MaximumHp}, adjusted={monk.AdjustedActionId}, " +
            $"priority={monk.HigherPriorityClaimed}, attempt={monk.UseActionAttempted}/{monk.UseActionAccepted}, " +
            $"count={monk.AttemptCount}/{monk.AcceptedCount}");

        ImGui.Separator();
        ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
        ImGui.TextDisabled(
            "Guard cooldown is shown only after this client actually observed that enemy's Guard. Unknown " +
            "cooldowns are never guessed. Seiton Sense never changes your selected hard, soft, or focus target " +
            "and uploads no gameplay data to an external service. Near Assist, Near Help, and Far Help may replace only " +
            "the target ID on one armed macro action. The optional CC brake can invalidate only one already incoming, " +
            "enabled action attempt against an exact protected enemy; it adds no action, repeat, or retry. " +
            "Purify, defensive utilities, Ally Rescue, and reactive counter-CC share one physical input generation and " +
            "can initiate at most one exact action attempt, in that priority order. Guard after Purify requires a later " +
            "physical generation, and every action-request helper is blocked while your own Guard is active. Monk Earth's " +
            "Reply is a separate automatic follow-up that yields whenever an earlier helper already attempted an action " +
            "in the same update. Automatic action helpers and the team-visible Attack1 marker are disabled by default. " +
            "Like all third-party modifications, use " +
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
