using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using SeitonSense.Core;
using SeitonSense.Plugin.Models;

namespace SeitonSense.Plugin.UI;

internal sealed partial class SettingsWindow
{
    private bool DrawActionHelpersPage()
    {
        var changed = false;
        ImGui.Spacing();
        ImGui.TextWrapped(
            "Turn on only the helpers you want. If several things become possible together, Seiton uses " +
            "Purify first, then Recuperate, Auto-Guard, and your job helpers. It sends only one of its own " +
            "actions at a time, then checks again immediately for the next one.");

        if (ImGui.CollapsingHeader(
                "Cast cancellation (experimental)",
                ImGuiTreeNodeFlags.DefaultOpen))
            changed |= DrawHeldActionCastCancellationControls();

        ImGui.Separator();
        if (ImGui.CollapsingHeader("Self-Purify", ImGuiTreeNodeFlags.DefaultOpen))
            changed |= DrawPurifyControls();

        ImGui.Separator();
        if (ImGui.CollapsingHeader("Smart Recuperate", ImGuiTreeNodeFlags.DefaultOpen))
            changed |= DrawSmartRecuperateControls();

        ImGui.Separator();
        if (ImGui.CollapsingHeader("Emergency Teleport", ImGuiTreeNodeFlags.DefaultOpen))
            changed |= DrawEmergencyTeleportControls();

        ImGui.Separator();
        if (ImGui.CollapsingHeader("Ally Rescue: Paean / Aquaveil", ImGuiTreeNodeFlags.DefaultOpen))
        {
            changed |= DrawAllyRescueControls();
            ImGui.Spacing();
            DrawAllyRescueOverview();
        }

        ImGui.Separator();
        if (ImGui.CollapsingHeader(
                "Reactive counter-CC: WHM / BRD / NIN / PLD / RDM / BLM / SAM",
                ImGuiTreeNodeFlags.DefaultOpen))
        {
            changed |= DrawReactiveCcControls();
        }

        ImGui.Separator();
        if (ImGui.CollapsingHeader("Reactive Purify → Guard", ImGuiTreeNodeFlags.DefaultOpen))
            changed |= DrawDefensiveUtilityControls();

        ImGui.Separator();
        if (ImGui.CollapsingHeader("Pressure escape Sprint", ImGuiTreeNodeFlags.DefaultOpen))
            changed |= DrawPressureEscapeSprintControls();

        ImGui.Separator();
        if (ImGui.CollapsingHeader("Smart Sprint", ImGuiTreeNodeFlags.DefaultOpen))
            changed |= DrawSmartSprintControls();

        ImGui.Separator();
        if (ImGui.CollapsingHeader("CC-immunity action brake", ImGuiTreeNodeFlags.DefaultOpen))
            changed |= DrawCcImmunityBrakeControls();

        ImGui.Separator();
        if (ImGui.CollapsingHeader("Team-visible enemy focus sign", ImGuiTreeNodeFlags.DefaultOpen))
            changed |= DrawAutoEnemyFocusMarkControls();

        return changed;
    }

    private bool DrawGeneralActionBufferControls()
    {
        var changed = Checkbox(
            "Enable automatic action buffer",
            configuration.EnableSmartActionBuffer,
            value => configuration.EnableSmartActionBuffer = value);
        changed |= SliderInt(
            "Buffer window",
            configuration.SmartActionBufferWindowMilliseconds,
            SmartActionBufferWindowRules.MinimumMilliseconds,
            SmartActionBufferWindowRules.MaximumMilliseconds,
            value => configuration.SmartActionBufferWindowMilliseconds = value,
            "%d ms");
        ImGui.TextDisabled(
            "Works in PvE and PvP. It supports instant actions on standard keyboard hotbars. Ground-targeted " +
            "skills, movement skills, mouse clicks, controllers, and cross hotbars are not supported here.");

        changed |= Checkbox(
            "Show buffer timing helper",
            configuration.ShowBufferLearningWindow,
            value => configuration.ShowBufferLearningWindow = value);
        changed |= Checkbox(
            "Lock the timing helper position",
            configuration.BufferLearningWindowLocked,
            value => configuration.BufferLearningWindowLocked = value);
        if (ImGui.Button("Reset timing helper position"))
            resetBufferLearningWindowPosition();
        ImGui.TextDisabled(
            "Shows the key or hotbar slot, the remembered action, and the remaining buffer time.");

        ImGui.Spacing();
        ImGui.TextUnformatted("Hold a hotbar key to repeat");
        changed |= Checkbox(
            "Enable hold-to-repeat (Turbo)",
            configuration.EnableNativeHotbarTurbo,
            value => configuration.EnableNativeHotbarTurbo = value);
        changed |= SliderInt(
            "Turbo initial delay",
            configuration.TurboInitialDelayMilliseconds,
            PluginConfiguration.MinimumTurboInitialDelayMilliseconds,
            PluginConfiguration.MaximumTurboInitialDelayMilliseconds,
            value => configuration.TurboInitialDelayMilliseconds = value,
            "%d ms");
        changed |= SliderInt(
            "Turbo repeat interval",
            configuration.TurboRepeatIntervalMilliseconds,
            PluginConfiguration.MinimumTurboRepeatIntervalMilliseconds,
            PluginConfiguration.MaximumTurboRepeatIntervalMilliseconds,
            value => configuration.TurboRepeatIntervalMilliseconds = value,
            "%d ms");
        changed |= Checkbox(
            "Allow Turbo outside combat (PvE / Wolves' Den / dummy testing)",
            configuration.TurboOutsideCombat,
            value => configuration.TurboOutsideCombat = value);
        ImGui.TextDisabled(
            "Works while holding a standard keyboard-hotbar key. Turn on the extra option above if you also " +
            "want to test it outside combat. Mouse clicks, controllers, and cross hotbars are not supported.");
        return changed;
    }

    private bool DrawPvpLatencyResponseControls()
    {
        var changed = Checkbox(
            "Enable the PvP latency response helper",
            configuration.EnablePvpLatencyResponseHelper,
            value => configuration.EnablePvpLatencyResponseHelper = value);
        changed |= SliderInt(
            "Held-helper retry time",
            configuration.PvpLatencyResponseWindowMilliseconds,
            HeldActionRetryRules.MinimumLatencyResponseWindowMilliseconds,
            HeldActionRetryRules.MaximumLatencyResponseWindowMilliseconds,
            value => configuration.PvpLatencyResponseWindowMilliseconds = value,
            "%d ms");

        ImGui.TextColored(
            configuration.EnablePvpLatencyResponseHelper
                ? new Vector4(0.35f, 0.9f, 1f, 1f)
                : new Vector4(0.7f, 0.72f, 0.78f, 1f),
            configuration.EnablePvpLatencyResponseHelper
                ? "ON — held helpers get the selected retry time."
                : "OFF — held helpers use the older short retry time.");
        ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
        ImGui.TextDisabled(
            "When a held helper is almost ready, Seiton keeps its action and target for this long and uses it " +
            "as soon as FFXIV allows it.");
        ImGui.TextDisabled(
            "It does not increase range, move your character, or change the chosen action or target. The held-helper " +
            "extension works in Crystalline Conflict and in the Wolves' Den when testing is enabled.");
        ImGui.PopTextWrapPos();
        return changed;
    }

    private bool DrawHeldActionCastCancellationControls()
    {
        var changed = Checkbox(
            "Cancel my active cast for an otherwise-ready held helper",
            configuration.AllowHeldHelpersToCancelOwnCast,
            value => configuration.AllowHeldHelpersToCancelOwnCast = value);
        ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
        ImGui.TextDisabled(
            "Default off. If a ready held helper is more important than your current cast, Seiton may cancel that " +
            "cast once and use the helper on the next moment it is allowed. It never moves you, changes your target, " +
            "or removes a queued action. This can deliberately sacrifice the cast you were doing.");
        changed |= Checkbox(
            "Allow Auto Purify / Recuperate to cancel BRD/MCH basic shots",
            configuration.AllowAutomaticRecoveryToCancelBasicShotCasts,
            value => configuration.AllowAutomaticRecoveryToCancelBasicShotCasts = value);
        ImGui.TextDisabled(
            "Default off. Auto Purify or Recuperate may cancel only BRD Powerful Shot or MCH Blast Charge. " +
            "If Seiton is not completely sure which cast is active, it waits instead.");
        ImGui.PopTextWrapPos();
        return changed;
    }

    private bool DrawPressureEscapeSprintControls()
    {
        var changed = Checkbox(
            "Sprint once while holding a movement key under 3+ enemy focus",
            configuration.EnablePressureEscapeSprintOnHeldKey,
            value => configuration.EnablePressureEscapeSprintOnHeldKey = value);
        ImGui.TextDisabled(
            "When at least three enemies are currently targeting you, holding WASD or an arrow key can use " +
            "Sprint once. It does not block movement. Survival actions and job helpers still take priority. " +
            "Using another action ends PvP Sprint as usual.");
        return changed;
    }

    private bool DrawSmartSprintControls()
    {
        var changed = Checkbox(
            "Do not cancel active Sprint by pressing Sprint again",
            configuration.ProtectActiveSprintFromRepeatPress,
            value => configuration.ProtectActiveSprintFromRepeatPress = value);
        ImGui.TextDisabled(
            "Default on. A second Sprint press is ignored while Sprint is already active. Other actions " +
            "still end PvP Sprint normally.");

        changed |= Checkbox(
            "Use Sprint after I stop using actions while holding a gameplay key",
            configuration.EnableIdleSmartSprintOnHeldKey,
            value => configuration.EnableIdleSmartSprintOnHeldKey = value);
        changed |= SliderInt(
            "Action-bar inactivity",
            configuration.SmartSprintInactivityMilliseconds,
            SmartSprintRules.MinimumInactivityMilliseconds,
            SmartSprintRules.MaximumInactivityMilliseconds,
            value => configuration.SmartSprintInactivityMilliseconds = value,
            "%d ms");
        ImGui.TextDisabled(
            "Optional; default 4000 ms. Only action-bar input resets this timer; the action does not have to succeed. " +
            "Running, WASD, camera movement, and target changes do not. Keep any gameplay key held and Seiton uses Sprint " +
            "once after the selected quiet time. Guard, crowd control, and survival helpers still win.");
        return changed;
    }

    private bool DrawPurifyControls()
    {
        var changed = false;
        changed |= Checkbox(
            "Automatically Purify enabled removable CC (no key required)",
            configuration.EnableAutomaticPurify,
            value => configuration.EnableAutomaticPurify = value);
        changed |= Checkbox(
            "Enable legacy fresh/held-key Purify consent",
            configuration.ExperimentalPurifyOnNextKey,
            value => configuration.ExperimentalPurifyOnNextKey = value);
        changed |= Checkbox(
            "Legacy mode: allow a key already held when the debuff appeared (includes WASD)",
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
            "Only the debuffs selected above can trigger Purify. Automatic mode needs no key and has the highest " +
            "priority. Guard, Resilience, Ninja stealth, typing, or an unavailable Purify will stop or delay it. " +
            "The legacy options keep the older fresh/held-key behavior. If both modes are enabled, automatic mode wins. " +
            "The optional BRD/MCH cast setting above can interrupt only their basic shots for emergency recovery.");
        ImGui.PopTextWrapPos();
        return changed;
    }

    private bool DrawDefensiveUtilityControls()
    {
        var changed = false;
        changed |= Checkbox(
            "Enable automatic high-pressure Purify → Guard",
            configuration.EnableDefensiveUtilities,
            value => configuration.EnableDefensiveUtilities = value);
        ImGui.TextColored(
            configuration.EnableDefensiveUtilities
                ? new Vector4(0.35f, 0.9f, 1f, 1f)
                : new Vector4(0.7f, 0.72f, 0.78f, 1f),
            configuration.EnableDefensiveUtilities
                ? "ON — the Purify → Guard chain runs automatically in CC; no key is required."
                : "OFF — this option will not use Purify or Guard for high-pressure Stun.");
        changed |= Checkbox(
            "At 3+ incoming enemies and Stun: auto-Purify, then auto-Guard after Resilience",
            configuration.GuardOnStunPressure,
            value => configuration.GuardOnStunPressure = value);
        changed |= Checkbox(
            "Show the Auto-Guard activation card",
            configuration.ShowAutoGuardActivationNotification,
            value => configuration.ShowAutoGuardActivationNotification = value);
        changed |= Checkbox(
            "Play a small sound when Auto-Guard is confirmed and protected",
            configuration.PlayAutoGuardActivationSound,
            value => configuration.PlayAutoGuardActivationSound = value);
        changed |= SliderInt(
            "Auto-Guard sound",
            configuration.AutoGuardActivationSoundId,
            1,
            16,
            value => configuration.AutoGuardActivationSoundId = value,
            "Sound %d");
        if (ImGui.Button("Test Auto-Guard sound"))
            personalStatus.PlayAutoGuardActivationSoundPreview();
        ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
        ImGui.TextDisabled(
            "Crystalline Conflict only and off by default. At 3+ enemies, Seiton can Purify a selected Stun and use " +
            "Guard after Resilience confirms the cleanse. The card, sound, and press protection start only after Guard " +
            "is visibly active. While it is active, Seiton blocks its other automatic actions so they cannot cancel it. " +
            "A second Guard press is ignored for two seconds; after that you can end Guard normally. /panicshu remains " +
            "an intentional emergency override.");
        ImGui.PopTextWrapPos();
        return changed;
    }

    private bool DrawSmartRecuperateControls()
    {
        var changed = false;
        changed |= Checkbox(
            "Automatically use Recuperate at 16,000+ missing HP (no key required)",
            configuration.EnableAutomaticRecuperate,
            value => configuration.EnableAutomaticRecuperate = value);
        changed |= Checkbox(
            "Use Recuperate from a held gameplay key at 16,000+ missing HP",
            configuration.EnableSmartRecuperateOnHeldKey,
            value => configuration.EnableSmartRecuperateOnHeldKey = value);
        ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
        ImGui.TextDisabled(
            "Off by default. At 16,000+ missing HP and at least 2,000 MP, Seiton can use Recuperate on you. " +
            "Automatic mode needs no key and wins if both modes are enabled; held mode also accepts WASD. " +
            "Wolves' Den needs the separate testing option. The optional BRD/MCH setting above can interrupt only " +
            "their basic shots for this emergency heal.");
        ImGui.TextDisabled(
            "Purify is the only helper above Recuperate. Active Guard and Ninja stealth block it. If Recuperate is " +
            "temporarily unavailable, lower helpers can still run. Seiton waits for the emergency to end before " +
            "starting a new heal attempt.");
        ImGui.PopTextWrapPos();
        return changed;
    }

    private bool DrawEmergencyTeleportControls()
    {
        var changed = false;
        changed |= Checkbox(
            "Emergency Teleport on held gameplay key (MNK / BLM / SGE / VPR)",
            configuration.EnableEmergencyTeleportOnHeldKey,
            value => configuration.EnableEmergencyTeleportOnHeldKey = value);
        changed |= SliderInt(
            "Trigger below HP",
            configuration.EmergencyTeleportHpPercent,
            10,
            90,
            value => configuration.EmergencyTeleportHpPercent = value,
            "%d%%");
        changed |= SliderInt(
            "Trigger below MP",
            configuration.EmergencyTeleportMpThreshold,
            0,
            10_000,
            value => configuration.EmergencyTeleportMpThreshold = value,
            "%d MP");
        changed |= SliderInt(
            "Minimum enemies focusing you",
            configuration.EmergencyTeleportMinimumFocusedEnemies,
            1,
            5,
            value => configuration.EmergencyTeleportMinimumFocusedEnemies = value,
            "%d");
        changed |= Slider(
            "Minimum jump distance",
            configuration.EmergencyTeleportMinimumTravelYalms,
            3f,
            25f,
            value => configuration.EmergencyTeleportMinimumTravelYalms = value,
            "%.1f y");
        changed |= Slider(
            "Enemy safety radius at destination",
            configuration.EmergencyTeleportEnemySafetyRadiusYalms,
            3f,
            20f,
            value => configuration.EmergencyTeleportEnemySafetyRadiusYalms = value,
            "%.1f y");
        changed |= SliderInt(
            "Maximum enemies inside safety radius",
            configuration.EmergencyTeleportMaximumNearbyEnemies,
            0,
            5,
            value => configuration.EmergencyTeleportMaximumNearbyEnemies = value,
            "%d");
        ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
        ImGui.TextDisabled(
            "Off by default. On MNK, BLM, SGE, or VPR, holding a gameplay key can jump to a safer ally when your HP, " +
            "MP, and enemy-focus settings are met. It runs after Recuperate and never visibly changes your target. " +
            "Wolves' Den needs the separate testing option.");
        ImGui.TextDisabled(
            "It chooses the reachable ally with the fewest nearby enemies, then prefers more travel distance and " +
            "more space from enemies. If no ally passes your safety settings, nothing happens.");
        ImGui.TextDisabled(
            "It tries once per danger moment and does not fall back to an unsafe target. The cast-cancel option can " +
            "interrupt your current cast only when this escape is otherwise ready.");
        ImGui.PopTextWrapPos();
        return changed;
    }

    private bool DrawAllyRescueControls()
    {
        var changed = false;
        changed |= Checkbox(
            "Use Paean/Aquaveil on eligible gameplay-key consent",
            configuration.ExperimentalAllyRescueOnNextKey,
            value => configuration.ExperimentalAllyRescueOnNextKey = value);
        changed |= Checkbox(
            "A held gameplay key may trigger when an ally is crowd-controlled (includes WASD)",
            configuration.AllyRescueOnHeldGameplayKey,
            value => configuration.AllyRescueOnHeldGameplayKey = value);
        ImGui.TextUnformatted("Exact ally triggers: Stun, Silence, Deep Freeze, Miracle of Nature");
        ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
        ImGui.TextDisabled(
            "Crystalline Conflict only. BRD uses The Warden's Paean and WHM uses Aquaveil on a reachable controlled " +
            "ally. It prefers lower HP, then more enemy focus, lower MP, and shorter distance. Heavy and Bind do not " +
            "trigger it. The blue CLEANSED card appears only when FFXIV confirms that the effect was removed.");
        ImGui.PopTextWrapPos();
        return changed;
    }

    private void DrawAllyRescueOverview()
    {
        var rescue = personalStatus.AllyRescueDiagnostics;
        var session = rescue.SessionConfirmations;
        var match = rescue.MatchConfirmations;

        ImGui.TextColored(new Vector4(0.34f, 0.82f, 1f, 1f), "ALLY RESCUE RESULTS");
        ImGui.TextUnformatted(
            $"Session: {rescue.AttemptCount} attempts  •  {rescue.AcceptedCount} FFXIV accepted  •  " +
            $"{session.TotalConfirmed} confirmed cleanses");
        ImGui.TextUnformatted($"This CC: {match.TotalConfirmed} confirmed cleanses");
        ImGui.TextDisabled(
            $"Confirmed cleanses by action: Paean " +
            $"{session.CountForAction(AllyRescueConfirmationRules.WardensPaeanActionId)}  •  " +
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
        ImGui.TextDisabled("Confirmed means FFXIV reported that the status was removed.");
    }

    private bool DrawReactiveCcControls()
    {
        var changed = false;
        changed |= Checkbox(
            "Enable reactive counter-CC",
            configuration.EnableReactiveCcUtilities,
            value => configuration.EnableReactiveCcUtilities = value);
        changed |= Checkbox(
            "Hold a gameplay key to react to eligible opportunities (includes WASD)",
            configuration.ReactiveCcOnHeldKey,
            value => configuration.ReactiveCcOnHeldKey = value);
        ImGui.TextColored(
            configuration.EnableReactiveCcUtilities
                ? new Vector4(0.35f, 0.9f, 1f, 1f)
                : new Vector4(0.7f, 0.72f, 0.78f, 1f),
            configuration.EnableReactiveCcUtilities
                ? "ON — the enabled job action can react to a matching threat or protection ending."
                : "OFF — reactive counter-CC is disabled.");

        ImGui.TextUnformatted("Shared protection-end triggers:");
        changed |= Checkbox(
            "DNC Contradance startup",
            configuration.ReactiveCcDancerLimitBreak,
            value => configuration.ReactiveCcDancerLimitBreak = value);
        changed |= Checkbox(
            "After enemy Purify: react when protection ends",
            configuration.ReactiveCcAfterEnemyPurify,
            value => configuration.ReactiveCcAfterEnemyPurify = value);
        changed |= Checkbox(
            "After enemy Guard: react when protection ends",
            configuration.ReactiveCcAfterEnemyGuard,
            value => configuration.ReactiveCcAfterEnemyGuard = value);

        ImGui.TextUnformatted("Optional job-specific counter actions:");
        changed |= Checkbox(
            "PLD Intervene after enemy Purify / Guard",
            configuration.ReactiveCcPaladinIntervene,
            value => configuration.ReactiveCcPaladinIntervene = value);
        changed |= Slider(
            "PLD Intervene maximum range",
            configuration.ReactiveCcPaladinInterveneMaximumRangeYalms,
            ReactiveCounterCcProfileRules.MinimumConfiguredInterveneRangeYalms,
            ReactiveCounterCcProfileRules.InterveneMaximumRangeYalms,
            value => configuration.ReactiveCcPaladinInterveneMaximumRangeYalms = value,
            "%.0f yalm");
        changed |= Checkbox(
            "RDM Resolution after enemy Purify / Guard",
            configuration.ReactiveCcRedMageResolution,
            value => configuration.ReactiveCcRedMageResolution = value);
        changed |= Checkbox(
            "RDM Vice of Thorns proc after enemy Purify / Guard",
            configuration.ReactiveCcRedMageViceOfThorns,
            value => configuration.ReactiveCcRedMageViceOfThorns = value);
        changed |= Checkbox(
            "BLM Frost Star proc after enemy Purify / Guard",
            configuration.ReactiveCcBlackMageFrostStar,
            value => configuration.ReactiveCcBlackMageFrostStar = value);
        changed |= Checkbox(
            "SAM Soten -> Mineuchi after enemy Purify / Guard",
            configuration.ReactiveCcSamuraiSotenMineuchi,
            value => configuration.ReactiveCcSamuraiSotenMineuchi = value);
        changed |= Slider(
            "SAM Soten maximum range",
            configuration.ReactiveCcSamuraiSotenMaximumRangeYalms,
            SamuraiReactiveCounterCcRules.MineuchiMaximumRangeYalms,
            SamuraiReactiveCounterCcRules.SotenMaximumRangeYalms,
            value => configuration.ReactiveCcSamuraiSotenMaximumRangeYalms = value,
            "%.0f yalm");

        ImGui.TextUnformatted("Additional WHM / BRD / NIN urgent startup triggers:");
        changed |= Checkbox(
            "MCH Marksman's Spite",
            configuration.MiracleInterceptMchLimitBreak,
            value => configuration.MiracleInterceptMchLimitBreak = value);
        changed |= Checkbox(
            "Interrupt an enemy SAM Zantetsuken (WHM / BRD / NIN)",
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
            "Experimental and off by default. In CC, hold a gameplay key and Seiton can use the enabled job action " +
            "when an enemy starts a selected threat or when their Purify/Guard protection ends. It uses each skill's " +
            "normal range and line of sight and never visibly changes your target. Wolves' Den testing uses only your " +
            "current target. Viper waits until Hardened Scales is gone.");
        ImGui.TextDisabled(
            "If several enemies become available together, Seiton first checks whether each action can actually reach. " +
            "It then prefers team pressure, low HP, and low MP. Pressure helps the choice but is never required. " +
            "Actions with travel time learn when to start so their effect can land just after protection ends. Until " +
            "enough safe timing samples exist, Seiton waits for the protection icon to disappear.");
        ImGui.TextDisabled(
            "SAM uses Mineuchi directly inside 5 yalms or Soten first from farther away. It learns the travel timing " +
            "before trying to land the stun just after Purify or Guard ends. Once Soten starts, it keeps that same enemy " +
            "for Mineuchi even if you release the key. Typing cancels the sequence.");
        ImGui.TextDisabled(
            "Purify always stays first. Each reaction keeps one action and enemy; it will not switch to another target " +
            "mid-attempt. The blue AUTO CC LANDED flash appears only when FFXIV confirms that Seiton's own Silence, " +
            "Miracle, or Stun landed. It does not guarantee that an instant Limit Break was stopped in time.");
        ImGui.PopTextWrapPos();
        return changed;
    }

    private bool DrawCcImmunityBrakeControls()
    {
        var changed = false;
        changed |= Checkbox(
            "Block selected CC actions against confirmed immunity",
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
            (29395, "Stumme Nocturne / Silent Nocturne"),
            (29399, "Repelling Shot"));
        changed |= DrawCcBrakeJob(
            24,
            "WHITE MAGE",
            (29228, "Wunder der Natur / Miracle of Nature"));
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
            "Crystalline Conflict only; no macro is needed. If the chosen enemy is currently immune to the selected " +
            "action's crowd control, Seiton blocks that press. It does not save or repeat the blocked action.");
        ImGui.TextDisabled(
            "Press again after protection ends. The whole action is blocked, including any damage or movement it also " +
            "does, so enable only the actions you want. Broad cones, ground attacks, and unclear area attacks are not " +
            "covered. Unknown immunity information lets the action pass.");
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
            "Crystalline Conflict only and off by default. It marks an enemy whose Guard is unavailable and who has " +
            "50% HP or less, low MP, or both. It prefers both resources low, then lower HP/MP and more team focus. " +
            "This uses the normal shared Attack1 marker and never changes your target.");
        ImGui.TextDisabled(
            "An existing Attack1 marker is never overwritten. Seiton clears only the marker it placed itself; if that " +
            "cannot be confirmed, it leaves the marker alone.");
        ImGui.PopTextWrapPos();
        return changed;
    }
}
