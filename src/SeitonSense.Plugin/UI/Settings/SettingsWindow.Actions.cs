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
            "All action-initiating helpers are opt-in. The current request priority is: " +
            "Purify > Smart Recuperate > automatic Guard > AST same-target heal chain > SAM staged counter-CC / Zantetsuken > NIN Seiton > VPR Serpentiner Geist > GNB Continuation > reactive counter-CC > Ally Rescue > PLD Guardian > NIN Guard-Shukuchi > SCH Critical Strategy > " +
            "DRK Shadowbringer (Dark Arts) > DRK Hiebsprung > DRK Shadowbringer (safe fallback) > Monk combo > Emergency Teleport > pressure Sprint > event Kardia > event Monk. " +
            "The job-specific physical-hold helpers use this deterministic order. Smart Recuperate runs directly after Purify, automatic Guard follows recovery, AST follows defense, and SAM follows AST; " +
            "on BRD/WHM, reactive counter-CC remains ahead of ally cleanse because its windows are shorter. A continuously held " +
            "key remains consent for later distinct exact episodes, with at most one held native boundary per framework " +
            "frame. Kardia and Monk retain their separate event-driven origins.");

        if (ImGui.CollapsingHeader(
                "General action buffer / native Turbo",
                ImGuiTreeNodeFlags.DefaultOpen))
            changed |= DrawGeneralActionBufferControls();

        ImGui.Separator();

        if (ImGui.CollapsingHeader(
                "Held-helper latency response (experimental)",
                ImGuiTreeNodeFlags.DefaultOpen))
            changed |= DrawPvpLatencyResponseControls();

        ImGui.Separator();
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
            "Enable the one-shot smart action buffer",
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
            "Default 1000 ms, maximum 1500 ms. The buffer is generic: it is available in PvE, PvP, " +
            "Crystalline Conflict, the Wolves' Den, and ordinary duty/open-world contexts. It has no PvP-only gate.");
        ImGui.TextDisabled(
            "Current scope is instant, non-ground, non-movement actions on standard keyboard hotbars. " +
            "Cast-time spells, ground targeting, movement actions, mouse clicks, and cross-hotbar/controller input are excluded.");

        changed |= Checkbox(
            "Show the live buffer learning window",
            configuration.ShowBufferLearningWindow,
            value => configuration.ShowBufferLearningWindow = value);
        changed |= Checkbox(
            "Lock the learning window position",
            configuration.BufferLearningWindowLocked,
            value => configuration.BufferLearningWindowLocked = value);
        if (ImGui.Button("Reset learning window position"))
            resetBufferLearningWindowPosition();
        ImGui.TextDisabled(
            "The movable panel shows the observed key or standard-hotbar slot, resolved action, and live buffer countdown.");

        ImGui.Spacing();
        ImGui.TextUnformatted("Native held-input Turbo (standard keyboard hotbars)");
        changed |= Checkbox(
            "Enable native held-input Turbo",
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
            "Turbo is opt-in. In combat it is not territory-gated and can be used in PvE, PvP, and the Wolves' Den. " +
            "Outside-combat repeating requires the separate option above.");
        ImGui.TextDisabled(
            "Current scope is held logical inputs on standard keyboard hotbars. Cross-hotbar/controller input and " +
            "direct mouse clicks do not provide a supported held input in this version.");
        return changed;
    }

    private bool DrawPvpLatencyResponseControls()
    {
        var changed = Checkbox(
            "Enable the PvP latency response helper",
            configuration.EnablePvpLatencyResponseHelper,
            value => configuration.EnablePvpLatencyResponseHelper = value);
        changed |= SliderInt(
            "Exact held-intent retry window",
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
                ? "ON — exact held-helper retries use the configured bounded window."
                : "OFF — exact held-helper retries keep the legacy bounded budget.");
        ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
        ImGui.TextDisabled(
            "Default off. This keeps the existing one-queue Seiton scheduler and extends only the bounded clean-client-false " +
            "budget for the same frozen action, target, key, context, and episode. The 50 ms cadence stays unchanged; " +
            "the legacy eight-call budget is never reduced. Client acceptance or ambiguous acceptance remains terminal, " +
            "and temporary native/GCD/animation waits spend no retry call. Action-specific Purify, Guard-end, and " +
            "projectile-impact deadlines remain authoritative.");
        ImGui.TextDisabled(
            "The integrated smart buffer and Turbo yield whenever Seiton's critical held scheduler owns the native " +
            "action boundary. This never writes position or animation lock, extends range, changes a target/action, " +
            "or creates a second queue. Held-helper retry expansion itself works in exact Crystalline Conflict and in " +
            "Wolves' Den only when the separate Wolves' Den testing option is enabled; the generic buffer remains " +
            "available in PvE as described above.");
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
            "Default off. When the highest-priority held helper has already frozen an exact valid intent and your " +
            "own cast is the remaining shared native-boundary blocker, Seiton Sense may request FFXIV's native cast " +
            "cancel exactly once for that observed cast. It never synthesizes movement or Escape, clears a queued " +
            "action, changes a target, or combines cancel and UseAction in the same framework frame. The frozen " +
            "helper revalidates normally on a later frame. This is intended to cover stationary casts and mobile " +
            "BRD Powerful Shot / MCH Blast Charge, but current-patch in-game behavior still needs live testing; an " +
            "action the client refuses to cancel simply continues. Enabling this can deliberately sacrifice your " +
            "current cast for the held helper.");
        changed |= Checkbox(
            "Allow Auto Purify / Recuperate to cancel verified BRD/MCH basic shots",
            configuration.AllowAutomaticRecoveryToCancelBasicShotCasts,
            value => configuration.AllowAutomaticRecoveryToCancelBasicShotCasts = value);
        ImGui.TextDisabled(
            "Default off and independent from the held-helper option. When the corresponding automatic helper is " +
            "enabled, only an exact BRD Powerful Shot (29391) or MCH Blast Charge (29402) may be cancelled. The " +
            "current job, active cast, unchanged adjusted action, and startup metadata must all match; transformed " +
            "or uncertain actions wait. Cancellation owns one framework frame, and Purify or Recuperate revalidates " +
            "normally before acting on a later clear-cast frame.");
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
            "Exact current hard/cast targets only; recent hits do not count. This option is independent from the " +
            "visual and sound. It listens only to held WASD/arrow movement keys and does not swallow that key. " +
            "Purify, Smart Recuperate, automatic Guard, the job-specific tier, and Emergency Teleport keep priority. Known " +
            "unavailability waits for free; only an explicit client rejection may retry the same exact Sprint " +
            "episode. Any later manual action ends FFXIV's native PvP Sprint.");
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
            "Only the exact enabled debuff types can trigger this. Automatic mode freezes the real live CC instance and " +
            "exact self, then gives ready Purify absolute priority without consuming a physical hold generation. It " +
            "normally waits for a clear cast; with the separate automatic-recovery cast-cancel toggle it may cancel " +
            "only a verified BRD/MCH basic-shot cast. Purify is sent only on a later clear-cast " +
            "frame. Cooldown or resource " +
            "unavailability does not starve lower helpers; queue and animation-lock waits retain priority without " +
            "spending an attempt. Guard, text input, Resilience, and NIN Shukuchi Hidden suppress it. The legacy mode " +
            "still accepts a fresh physical key after CC, optionally including a key already held at status entry. " +
            "If both modes are enabled for the same debuff, automatic consent wins that status episode and leaves the " +
            "physical key generation untouched. " +
            "Only an explicit client rejection may retry the same frozen self intent after 50 ms. The default is eight " +
            "native calls; the separate PvP latency-response option can freeze its extended budget for that exact CC " +
            "episode. Acceptance or ambiguity ends it. Disable " +
            "rules in other plugins that rewrite Purify or its target while testing.");
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
                ? "ON — the exact reactive chain runs automatically in CC; no held key is required."
                : "OFF — this group adds no pressure-triggered Purify or later Guard request.");
        changed |= Checkbox(
            "At exact 3+ incoming enemies and Stun: auto-Purify, then auto-Guard after Resilience",
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
            "Crystalline Conflict only and disabled by default. If high-pressure Stun triggers Purify, Guard is " +
            "allowed only after live Resilience confirms the cleanse and the removable CC is gone. Both requests " +
            "are keyless; enabled per-status Purify rules still apply. Client acceptance of Purify remains terminal " +
            "for the Purify episode. The former speculative 50%-HP " +
            "pre-Guard rule has been removed. While Guard is active, and during its bounded propagation interval, " +
            "every Seiton Sense action-request helper is blocked so none can cancel it. A client-true Guard request " +
            "is provisional: it creates no card, sound, or native input shield until exact live Guard is visible. " +
            "If status confirmation times out while Guard is still exactly ready, one bounded retry is allowed " +
            "inside the original lease. Once confirmed, ordinary Action/PvPAction presses are ignored through the " +
            "exact live Guard status. A second Guard press is also ignored for two seconds from confirmation, then becomes the " +
            "deliberate release path again. Manual Guard is never owned, the explicit /panicshu emergency-location " +
            "command remains an intentional override, and stale " +
            "ownership expires after six seconds. Auto-Guard waits instead of dispatching if the protection hook is " +
            "unavailable.");
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
            "Default off. Available in exact Crystalline Conflict and, only with the separate Wolves' Den testing " +
            "toggle, in Wolves' Den for controlled testing. Automatic mode wins when both options are enabled and uses " +
            "no physical-key generation. Held mode listens to the shared continuous gameplay-key consent, including " +
            "WASD. At exactly 16,000 or more missing HP and at least 2,000 observed MP, the selected mode may request one " +
            "self-targeted PvP Recuperate (29711). Automatic Recuperate normally waits for a clear native boundary. " +
            "With the separate automatic-recovery cast-cancel toggle, it may cancel only a verified BRD Powerful Shot " +
            "or MCH Blast Charge, then revalidates and acts on a later frame. If MP or the action is not ready, it does " +
            "not block a usable lower-priority helper.");
        ImGui.TextDisabled(
            "Only Purify keeps priority over Smart Recuperate. Its current-frame claim is propagated first to " +
            "automatic Guard, then every later job helper, Emergency Teleport, and pressure Sprint. Exact active " +
            "Guard blocks Recuperate; the provisional propagation latch is kept only for later helpers, so a rejected " +
            "Guard request cannot suppress higher-priority recovery. The " +
            "exact self epoch is revalidated before every call. A clean client rejection may retry after 50 ms up " +
            "to the budget frozen from the current PvP latency-response setting (eight calls by default). Pre-native " +
            "validation drift and temporary readiness/MP, higher-priority, or Guard states wait without spending " +
            "a call; dropping below the HP threshold cancels the current intent. Acceptance starts an exact verified " +
            "1.0-second anti-duplicate recast; after it elapses, current positive readiness may rearm even if the brief " +
            "cooldown-unavailable frame was missed. NIN Shukuchi Hidden suppresses " +
            "both modes. Retry exhaustion or an ambiguous outcome remains latched until automatic danger ends or the " +
            "held mode's frozen key is released.");
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
            "Default off. In exact Crystalline Conflict (or Wolves' Den with the explicit test toggle), on MNK, " +
             "BLM, SGE, or VPR, a continuously held physical gameplay key " +
             "including WASD may create one escape episode after Smart Recuperate. Own HP and MP must both be " +
             "strictly below the configured limits, and fresh direct enemy hard/cast targeting must meet the " +
             "focus count. It uses Thunderclap, Aetherial Manipulation, Icarus, or Slither on one exact non-self " +
             "party member without visibly changing your target. An MP limit of 0 can never pass the strict below " +
             "check; turn the helper off instead when you do not want it.");
        ImGui.TextDisabled(
            "Destinations must pass the action's native target-specific usability, range and line of sight, the " +
            "minimum real hitbox-edge travel distance, and a complete exact enemy-safety snapshot. Duplicate party " +
            "identity fails closed. Fewest nearby enemies wins, then the " +
            "farthest ally and greatest enemy clearance. With the default maximum of zero, no enemy may stand " +
            "inside the configured destination radius; if no safe ally exists, nothing happens.");
        ImGui.TextDisabled(
            "One danger episode makes at most one native action call: accepted, rejected, ambiguous, or thrown " +
            "outcomes all stop it. There is no target fallback and no retry. The episode rearms only after the " +
            "danger condition has clearly ended. Purify and the job-specific tier remain above Recup; Emergency " +
            "Teleport is directly after Recup and before generic Guard/Sprint. Optional held-cast cancellation " +
            "applies to this frozen emergency intent.");
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
            "CC-only and self-excluding. BRD uses The Warden's Paean; WHM uses Aquaveil, independent of client " +
            "language. The target must be an exact party member in the action's native range and line of sight. " +
            "Priority is lowest HP%, then highest current incoming enemy pressure, then lowest trusted MP%, then " +
            "distance and stable party order. Purify wins globally; on BRD/WHM, reactive counter-CC wins before Ally " +
            "Rescue then wins before Guardian, NIN, SCH, DRK, Recuperate, Guard, and Sprint. " +
            "Known action-specific cooldown, resource, and reachability blocks wait in the background without " +
            "starving a usable lower helper. Global cast, occupied-queue, blocking-animation-lock waits and the " +
            "brief explicit-false throttle retain the scheduler frame; none spends the retry budget. A clean client " +
            "rejection may retry only the frozen " +
            "actor/status intent after 50 ms, up to eight calls; acceptance is terminal. A blue CLEANSED card and the counters advance only for the exact server " +
            "RecoveredFromStatusEffect result (effect type 0x10). Heavy and Bind intentionally do not trigger this " +
            "experiment.");
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
            $"Session: {rescue.AttemptCount} attempts  •  {rescue.AcceptedCount} client accepted  •  " +
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
        ImGui.TextDisabled("Confirmed means the exact server 0x10 status-removal result was captured.");
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
                ? "ON — enabled WHM Miracle, BRD Silent Nocturne, NIN Raiju, PLD Intervene, RDM Resolution/Vice of Thorns, BLM Frost Star, or SAM Soten/Mineuchi " +
                  "may schedule one frozen exact-target intent " +
                  "for an eligible CC opportunity."
                : "OFF — threat capture is inactive and no counter-CC attempt can occur.");

        ImGui.TextUnformatted("Shared protection-end triggers:");
        changed |= Checkbox(
            "DNC Contradance startup",
            configuration.ReactiveCcDancerLimitBreak,
            value => configuration.ReactiveCcDancerLimitBreak = value);
        changed |= Checkbox(
            "After enemy Purify: all six removable CC types, ranked exact release",
            configuration.ReactiveCcAfterEnemyPurify,
            value => configuration.ReactiveCcAfterEnemyPurify = value);
        changed |= Checkbox(
            "After enemy Guard ends: ranked exact release",
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
            "Experimental and disabled by default. Exact Crystalline Conflict is supported; the separate Wolves' Den " +
            "testing toggle uses only the exact current hard target. WHM Wunder der Natur / Miracle of Nature uses its " +
            "native 10-yalm range; BRD Stumme Nocturne / Silent Nocturne and both NIN Raiju stun variants use " +
            "their native 20-yalm range. The enemy " +
            "must remain the exact canonical opponent, " +
            "alive, targetable, in native range and line of sight, and free of verified protection for that counter. " +
            "The enemy-SAM trigger here is separate from your own Samurai Zantetsuken held helper under Job Tools. " +
            "MCH, SAM, VPR, and Contradance each use their existing exact bounded startup signal. The post-Purify rule " +
            "accepts an exact enemy self-Purify action packet with or without an exposed Stun, Heavy, Bind, Silence, Deep " +
            "Freeze, or Miracle of Nature recovery tuple, observes real Resilience and remembers the exact enemy episode " +
            "without binding a key. A validated duration is only a wake-up hint. It binds the current eligible held/fresh " +
            "generation only at authoritative protection end or inside the original 500-ms release opportunity. " +
            "At or after the expected end, the first absent frame is " +
            "eligible immediately; an early or untimed absence keeps the 150-ms anti-flicker check. It uses " +
            "the exact S1-S5 actor directly, does not require that actor to be your selected target, and never changes " +
            "your target. There is no minimum team-pressure count, and distinct S-slots are tracked independently. " +
            "Viper waits until Hardened Scales is actually absent.");
        ImGui.TextDisabled(
            "For simultaneous post-Purify or post-Guard releases, candidates are evaluated before pressure ranking. " +
            "Native blocker, range, and line-of-sight eligibility " +
            "is checked before ranking. Only fresh exact team pressure above zero earns a " +
            "ranking bonus, highest-first. Zero, unknown, or stale pressure is neutral and never gates a candidate. " +
            "Lowest HP ratio follows, then lowest trusted MP ratio and stable S-slot identity. Exactly one winner is " +
            "selected; simultaneous losers " +
            "are terminal and never become fallback attempts. Post-Guard binds an exact S1-S5 actor only after Guard " +
            "3054/3673 was observed present and then verified absent. Early Guard cancellation releases immediately. " +
            "Ordinary protection-end held episodes retain the shared 3-second lease. A true main-GCD counter that is busy at its learned ideal request frame reserves only that exact action, actor, and protection episode for at most 1000 ms from that frozen frame; it never claims input or cancels your cast while waiting. " +
            "PLD uses the configured Intervene cap up to its native 20 yalms; RDM Resolution, the exact Forte-to-Vice proc, and the exact Soul Resonance-to-Frost Star proc use native 25-yalm targeting. " +
            "Each exact action learns only from exact source-sequence server ActionEffect timing at its measured edge distance. Prediction needs five safe current-or-nearer samples including at least one from the current runtime session; otherwise it waits for authoritative protection absence. Learned attempts may request early so impact aims just after natural expiry. Early Guard cancellation remains immediate. " +
            "In enabled Wolves' Den testing, these helpers act only on the exact current hard target matching the observed episode.");
        ImGui.TextDisabled(
            "SAM's separate staged option mirrors the exact enemy self-Purify/Guard packet from the same shared hook, " +
            "requires the matching live Resilience/Guard status, the complete verified Mineuchi blocker family, and " +
            "freezes that exact actor, status, end time, and held-key generation. It first learns exact sequence-bound " +
            "Soten arrival and Mineuchi ActionEffect timing. After warm-up it may start Soten early and requests Mineuchi " +
            "only inside its measured final window so the stun lands just after protection expires; without enough safe " +
            "samples it conservatively waits for authoritative absence. Early Guard cancellation remains immediate. " +
            "Inside 5 yalms it uses Mineuchi directly; otherwise it may use Soten once up to the configured cap. A " +
            "client-accepted Soten commits only that actor/episode's Mineuchi completion even if the initiating key is " +
            "released or changed; text input still cancels it. It never changes target, reranks, substitutes, or retries " +
            "a completed native boundary. Wolves' Den requires the observed actor to be the exact current duel target or " +
            "the reviewed current striking dummy.");
        ImGui.TextDisabled(
            "While a gameplay key remains held, each selected exact startup or protection-end episode keeps one " +
            "frozen target intent. A later distinct episode may authorize another action without a key release; no " +
            "simultaneous loser can. Purify remains first; AST, SAM, NIN Seiton, and VPR Serpentiner Geist follow in the documented job-gated order, while reactive counter-CC leads " +
            "the BRD/WHM helpers because its LB and protection-end windows are shorter. Known action-specific " +
            "unavailability waits without blocking a usable lower helper; only a clean client rejection may retry " +
            "that same intent after 50 ms, up to eight calls. Acceptance is terminal. There is no selected-target " +
            "change, alternate, fallback, or replay. The blue AUTO CC " +
            "LANDED flash appears " +
            "only after the matching Miracle, Silence, or Stun status is captured on that exact pending enemy with the " +
            "same source sequence created by the plugin request; a manual use cannot claim it. It confirms " +
            "the counter-CC landed, not conclusively that Contradance, another LB, or its damage was interrupted. In " +
            "particular, an instant LB already accepted by the server may be too late to stop even when Silence lands.");
        ImGui.PopTextWrapPos();
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
            "Crystalline Conflict only. This works directly from a hotbar; no macro is required. For the reviewed " +
            "single/primary-target list above, an enabled action aimed at an exactly identified enemy with verified " +
            "protection against that exact CC is stopped before the downstream game action for that one incoming " +
            "attempt. Wunder der Natur / Miracle of Nature uses its own verified matrix, including VPR-only Hardened " +
            "Scales. The action, target and " +
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
}
