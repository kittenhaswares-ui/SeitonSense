using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using SeitonSense.Core;

namespace SeitonSense.Plugin.UI;

internal sealed partial class SettingsWindow
{
    private bool DrawActionHelpersPage()
    {
        var changed = false;
        ImGui.Spacing();
        ImGui.TextWrapped(
            "All action-initiating helpers are opt-in. The current request priority is: " +
            "Purify > NIN Seiton / VPR Serpentiner Geist > reactive counter-CC > Ally Rescue > PLD Guardian > NIN Guard-Shukuchi > SCH Critical Strategy > DRK " +
            "Hiebsprung > Smart Recuperate > generic Guard > pressure Sprint > event Kardia > event Monk. The eight " +
            "job-specific physical-hold helpers share the second tier. NIN Seiton and VPR Serpentiner Geist get their first job slot; " +
            "on BRD/WHM, reactive counter-CC remains ahead of ally cleanse because its windows are shorter. A continuously held " +
            "key remains consent for later distinct exact episodes, with at most one held native boundary per framework " +
            "frame. Kardia and Monk retain their separate event-driven origins.");

        if (ImGui.CollapsingHeader(
                "Held-action cast cancellation (experimental)",
                ImGuiTreeNodeFlags.DefaultOpen))
            changed |= DrawHeldActionCastCancellationControls();

        ImGui.Separator();
        if (ImGui.CollapsingHeader("Self-Purify", ImGuiTreeNodeFlags.DefaultOpen))
            changed |= DrawPurifyControls();

        ImGui.Separator();
        if (ImGui.CollapsingHeader("Smart Recuperate", ImGuiTreeNodeFlags.DefaultOpen))
            changed |= DrawSmartRecuperateControls();

        ImGui.Separator();
        if (ImGui.CollapsingHeader("Ally Rescue: Paean / Aquaveil", ImGuiTreeNodeFlags.DefaultOpen))
        {
            changed |= DrawAllyRescueControls();
            ImGui.Spacing();
            DrawAllyRescueOverview();
        }

        ImGui.Separator();
        if (ImGui.CollapsingHeader(
                "Reactive counter-CC: WHM Wunder der Natur / Miracle of Nature · " +
                "BRD Stumme Nocturne / Silent Nocturne",
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
            "Purify, the job-specific second tier, Smart Recuperate, and generic Guard keep priority. Known " +
            "unavailability waits for free; only an explicit client rejection may retry the same exact Sprint " +
            "episode. Any later manual action ends FFXIV's native PvP Sprint.");
        return changed;
    }

    private bool DrawPurifyControls()
    {
        var changed = false;
        changed |= Checkbox(
            "Enable held-key Purify for enabled removable CC",
            configuration.ExperimentalPurifyOnNextKey,
            value => configuration.ExperimentalPurifyOnNextKey = value);
        changed |= Checkbox(
            "Also allow a key that was already held when the debuff appeared (includes WASD)",
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
            "Only the exact enabled debuff types can trigger this. By default, the helper accepts a fresh physical " +
            "gameplay-key press after the debuff appears. Enable the separate held-key option if a key pressed before " +
            "the debuff should remain continuous consent. " +
            "ReAction Turbo pulses do not create new physical presses. The original key is not swallowed. Seiton Sense " +
            "keeps Purify at absolute priority while the exact enabled CC remains active. Cooldown, resource, cast, " +
            "queue, and animation-lock blocks wait without spending an attempt. Only an explicit client rejection may " +
            "retry the same frozen self intent after 50 ms, at most eight native calls total; acceptance or ambiguity " +
            "ends that CC episode. Disable " +
            "rules in other plugins that rewrite Purify or its target while testing.");
        ImGui.PopTextWrapPos();
        return changed;
    }

    private bool DrawDefensiveUtilityControls()
    {
        var changed = false;
        changed |= Checkbox(
            "Enable the high-pressure Purify → Guard follow-up",
            configuration.EnableDefensiveUtilities,
            value => configuration.EnableDefensiveUtilities = value);
        changed |= Checkbox(
            "A held gameplay key supplies continuous scheduler consent (includes WASD)",
            configuration.DefensiveUtilitiesOnHeldKey,
            value => configuration.DefensiveUtilitiesOnHeldKey = value);
        ImGui.TextColored(
            configuration.EnableDefensiveUtilities
                ? new Vector4(0.35f, 0.9f, 1f, 1f)
                : new Vector4(0.7f, 0.72f, 0.78f, 1f),
            configuration.EnableDefensiveUtilities
                ? "ON — the exact reactive Guard chain may claim one scheduler frame in CC."
                : "OFF — this group adds no pressure-triggered Purify or later Guard request.");
        changed |= Checkbox(
            "At 3+ incoming enemies and Stun: Purify, then Guard as a later exact episode",
            configuration.GuardOnStunPressure,
            value => configuration.GuardOnStunPressure = value);
        ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
        ImGui.TextDisabled(
            "Crystalline Conflict only and disabled by default. If high-pressure Stun triggers Purify, Guard is " +
            "allowed only after live Resilience confirms the cleanse and the removable CC is gone. The same held " +
            "key may authorize that later distinct Guard episode; client acceptance of Purify remains terminal for " +
            "the Purify episode. The former speculative 50%-HP " +
            "pre-Guard rule has been removed. While Guard is active, and during its bounded propagation interval, " +
            "every Seiton Sense action-request helper is blocked so none can cancel it. A client-accepted automatic " +
            "Guard also owns a native input shield: ordinary Action/PvPAction presses are ignored through the exact " +
            "live Guard status, while pressing Guard again remains the deliberate release path. Manual Guard is never " +
            "owned, the explicit /panicshu emergency-location command remains an intentional override, and stale " +
            "ownership expires after six seconds. Auto-Guard waits instead of dispatching if the protection hook is " +
            "unavailable.");
        ImGui.PopTextWrapPos();
        return changed;
    }

    private bool DrawSmartRecuperateControls()
    {
        var changed = Checkbox(
            "Use Recuperate from a held gameplay key at 16,000+ missing HP",
            configuration.EnableSmartRecuperateOnHeldKey,
            value => configuration.EnableSmartRecuperateOnHeldKey = value);
        ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
        ImGui.TextDisabled(
            "Default off. Available in exact Crystalline Conflict and, only with the separate Wolves' Den testing " +
            "toggle, in Wolves' Den for controlled testing. Like held-key Purify, this listens to the shared " +
            "continuous physical gameplay-key consent, including WASD. At exactly 16,000 or more missing HP and at least " +
            "2,000 observed MP, it may request one self-targeted PvP Recuperate (29711). If MP or the native action " +
            "is not ready, it waits without blocking a currently usable lower-priority helper.");
        ImGui.TextDisabled(
            "Purify and the complete job-specific second tier keep priority. Smart Recuperate is evaluated before " +
            "generic Guard and pressure Sprint, while " +
            "active Guard and its short propagation latch block Recuperate so the helper cannot cancel Guard. The " +
            "exact self epoch is revalidated before every call. A clean client rejection may retry after 50 ms, up " +
            "to eight calls total. Temporary readiness/MP, higher-priority, and Guard states wait without spending " +
            "a call; dropping below the HP threshold cancels the current intent. Acceptance ends that epoch, and a " +
            "later one requires an observed cooldown unavailable-to-ready transition. Retry exhaustion or an " +
            "ambiguous/invalid exact outcome latches only this helper until the frozen key is released.");
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
                ? "ON — WHM Wunder der Natur / Miracle of Nature or BRD Stumme Nocturne / " +
                  "Silent Nocturne, plus NIN Forked/Fleeting Raiju, may schedule one frozen exact-target intent " +
                  "for an eligible CC opportunity."
                : "OFF — threat capture is inactive and no counter-CC attempt can occur.");

        ImGui.TextUnformatted("WHM / BRD / NIN triggers:");
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

        ImGui.TextUnformatted("Additional WHM / BRD / NIN urgent startup triggers:");
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
            "Experimental, CC-only, and disabled by default. WHM Wunder der Natur / Miracle of Nature uses its " +
            "native 10-yalm range; BRD Stumme Nocturne / Silent Nocturne and both NIN Raiju stun variants use " +
            "their native 20-yalm range. The enemy " +
            "must remain the exact canonical opponent, " +
            "alive, targetable, in native range and line of sight, and free of verified protection for that counter. " +
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
            "WHM and BRD retain the 1.5-second held lease; NIN uses 3 seconds to cover one verified 2.5-second Raiju recast.");
        ImGui.TextDisabled(
            "While a gameplay key remains held, each selected exact startup or protection-end episode keeps one " +
            "frozen target intent. A later distinct episode may authorize another action without a key release; no " +
            "simultaneous loser can. Purify remains first; enabled NIN Seiton or VPR Serpentiner Geist is next, while reactive counter-CC leads " +
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
