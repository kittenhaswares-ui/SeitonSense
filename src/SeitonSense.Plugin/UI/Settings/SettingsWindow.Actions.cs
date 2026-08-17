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
            "All action-initiating helpers are opt-in. One physical input generation is shared in this order: " +
            "Self Purify > Guard or Guardian > pressure Sprint > Ally Rescue > reactive counter-CC > Ninja > Scholar. " +
            "Monk Earth's Reply is a separate automatic follow-up that yields after an earlier helper attempt.");

        if (ImGui.CollapsingHeader("Self-Purify", ImGuiTreeNodeFlags.DefaultOpen))
            changed |= DrawPurifyControls();

        ImGui.Separator();
        if (ImGui.CollapsingHeader("Guard and Paladin Guardian", ImGuiTreeNodeFlags.DefaultOpen))
            changed |= DrawDefensiveUtilityControls();

        ImGui.Separator();
        if (ImGui.CollapsingHeader("Pressure escape Sprint", ImGuiTreeNodeFlags.DefaultOpen))
            changed |= DrawPressureEscapeSprintControls();

        ImGui.Separator();
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

        ImGui.Separator();
        if (ImGui.CollapsingHeader("CC-immunity action brake", ImGuiTreeNodeFlags.DefaultOpen))
            changed |= DrawCcImmunityBrakeControls();

        ImGui.Separator();
        if (ImGui.CollapsingHeader("Team-visible enemy focus sign", ImGuiTreeNodeFlags.DefaultOpen))
            changed |= DrawAutoEnemyFocusMarkControls();

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
            "Self Purify and Guard/Guardian keep priority; any later manual action ends FFXIV's native PvP Sprint.");
        return changed;
    }

    private bool DrawPurifyControls()
    {
        var changed = false;
        changed |= Checkbox(
            "Enable one Purify attempt from an eligible physical gameplay key",
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
            "Only the exact enabled debuff types can trigger this. By default, the helper waits for a fresh physical " +
            "gameplay-key press after the debuff appears. Enable the separate held-key option if a key pressed before " +
            "the debuff should count once. " +
            "ReAction Turbo pulses do not create new physical presses. The original key is not swallowed. Seiton Sense " +
            "sends one native Purify attempt immediately, and FFXIV decides whether it can queue or execute it. " +
            "The same physical hold cannot trigger again until released, and there is no retry after rejection. Disable " +
            "rules in other plugins that rewrite Purify or its target while testing.");
        ImGui.PopTextWrapPos();
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
                : "OFF — this defensive group adds no pressure-triggered Purify, Guard, or Guardian request.");
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
            "intent. Self-Purify, defensive utilities, pressure Sprint, and Ally Rescue have priority. Input is " +
            "consumed before the native call, with no selected-target change, fallback, or retry. The blue AUTO CC " +
            "LANDED flash appears " +
            "only after the matching Miracle or Silence status is captured on that exact pending enemy. It confirms " +
            "the counter-CC landed, not conclusively that Contradance, another LB, or its damage was interrupted.");
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
