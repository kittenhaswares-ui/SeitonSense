using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using SeitonSense.Core;

namespace SeitonSense.Plugin.UI;

internal sealed partial class SettingsWindow
{
    private bool DrawJobToolsPage()
    {
        var changed = false;
        ImGui.Spacing();
        ImGui.TextWrapped(
            "Job-specific PvP cues and helpers. After Purify, the physical-hold helpers share the second priority tier " +
            "in deterministic urgency order: AST same-target heal chain > SAM staged counter-CC / Zantetsuken > NIN Seiton > VPR Serpentiner Geist > GNB Continuation > reactive counter-CC > Ally Rescue > PLD Guardian > " +
            "NIN Guard-Shukuchi > SCH Critical Strategy > DRK Shadowbringer (Dark Arts) > DRK Hiebsprung > DRK Shadowbringer (safe fallback) > Monk combo. AST runs directly after Purify and SAM follows AST; " +
            "reactive counter-CC remains first for BRD/WHM. Cross-job survival and counter-CC controls are grouped under Action Helpers.");

        if (ImGui.CollapsingHeader("Astrologian — Harmonischer Orbis", ImGuiTreeNodeFlags.DefaultOpen))
        {
            changed |= Checkbox(
                "Harmonischer Orbis + optional Zweifacher Zauber on held key (includes WASD)",
                configuration.EnableAstrologianHarmonicOrbisOnHeldKey,
                value => configuration.EnableAstrologianHarmonicOrbisOnHeldKey = value);
            ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
            ImGui.TextDisabled(
                "Default off and PvP Astrologian only. While an eligible gameplay key remains held, this uses the " +
                "same exact friendly selection as /nearhelp, restricted to living self/party players at exactly " +
                "60% HP or lower. Lowest exact HP is the anchor; the shared Near Help pressure preference may refine " +
                "only its existing narrow health window. Every candidate must pass the action's native range and " +
                "line-of-sight check. No hard, soft, focus, or mouseover target is changed.");
            ImGui.TextDisabled(
                "The helper freezes one exact player and uses Harmonischer Orbis / Aspected Benefic (29243). If and " +
                "only if Zweifacher Zauber was already locally available before that Orbis, a client-accepted Orbis " +
                "reserves the next clear scheduler frame for the exact adjusted Orbis repeat (29247) on the same " +
                "player. The heal may raise the player above 60%; that does not rerank or cancel the planned repeat. " +
                "If Double Cast was not ready, the sequence deliberately ends after Orbis.");
            ImGui.TextDisabled(
                "Purify remains absolute priority. Your own Guard suppresses the full sequence and cannot be " +
                "cancelled by this helper or its optional cast-cancel path. Exact actor identity, held key, English action metadata, native " +
                "readiness, range, line of sight, and the 29245-to-29247 Double Cast adjustment are revalidated. A " +
                "clean client rejection may retry only the same frozen action/target; ambiguity, drift, expiry, or " +
                "release ends the sequence without an alternate. The global held-helper cast-cancel test can cancel " +
                "your current cast only for an otherwise-ready frozen Orbis intent.");
            ImGui.PopTextWrapPos();
        }

        ImGui.Separator();

        if (ImGui.CollapsingHeader("Paladin — Guardian rescue", ImGuiTreeNodeFlags.DefaultOpen))
        {
            changed |= Checkbox(
                "Guardian for an exact critical or 3+-pressure ally",
                configuration.PaladinGuardianLowAlly,
                value => configuration.PaladinGuardianLowAlly = value);
            changed |= Checkbox(
                "A held gameplay key may supply the Guardian input (includes WASD)",
                configuration.PaladinGuardianOnHeldKey,
                value => configuration.PaladinGuardianOnHeldKey = value);
            changed |= Checkbox(
                "After accepted Auto Guardian: Quick Chat + Bind pair (party-visible)",
                configuration.PaladinGuardianAnnounceAndMark,
                value => configuration.PaladinGuardianAnnounceAndMark = value);
            ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
            ImGui.TextDisabled(
                "Default off and exact Crystalline Conflict only. The exact non-self party ally must be alive, " +
                "targetable, and accepted by FFXIV's native 20-yalm range/line-of-sight check. The original 20% HP " +
                "boundary is unconditional; 21-35% HP is eligible only with a fresh exact current hard/cast-target " +
                "count of at least three enemies. Critical targets always precede proactive targets; proactive ties " +
                "rank by higher pressure, then lower exact HP. Both Guard and Guardian readiness are revalidated.");
            ImGui.TextDisabled(
                "Purify keeps global priority. On PLD, Guardian follows the unavailable NIN/reactive/cleanse paths and wins before SCH, DRK, " +
                "Smart Recuperate, Emergency Teleport, generic Guard, and pressure Sprint. Continuous " +
                "held consent freezes one exact Guardian intent; only a clean client rejection may use the common " +
                "bounded same-intent retry. There is no selected-target change, alternate, fallback, or replay. " +
                "CLIENT ACCEPTED and the 1.5-second card do not prove server-side protection.");
            ImGui.TextDisabled(
                "The separate communication opt-in uses localized CC Quick Chat row 35 for the frozen party slot, " +
                "then attempts Bind2 on that ally and Bind1 on self with exact ownership checks and bounded cleanup. " +
                "Occupied, unknown, or drifting marker state is relinquished rather than overwritten or cleared.");
            ImGui.PopTextWrapPos();
        }

        ImGui.Separator();

        if (ImGui.CollapsingHeader("Dark Knight — Hiebsprung + Shadowbringer", ImGuiTreeNodeFlags.DefaultOpen))
        {
            changed |= Checkbox(
                "Hiebsprung on held gameplay key at 30% HP or lower (experimental)",
                configuration.EnableDarkKnightPlungeOnHeldKey,
                value => configuration.EnableDarkKnightPlungeOnHeldKey = value);
            ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
            ImGui.TextDisabled(
                "Default off, PvP Dark Knight, and exact Crystalline Conflict only. Hiebsprung / Plunge (29092) " +
                "considers living, targetable canonical S1-S5 enemies at an exact 30% HP or lower. The nearest " +
                "native action may reach 20 yalms, but this helper adds a strict 10-yalm center-distance cap and " +
                "still requires FFXIV's native range and line of sight. Lowest exact HP ratio wins, then S-slot " +
                "and stable actor identity. Your own Bind, either side's Guard, your recent Guard propagation latch, " +
                "animation lock, typing, metadata uncertainty, or identity drift blocks the request.");
            ImGui.TextDisabled(
                "The first eligible epoch freezes one target. After a " +
                "client-accepted request, the same physical key may stay held: each later attempt requires a " +
                "separately observed not-ready to ready cooldown epoch, such as a proven KO reset or the natural " +
                "12-second recast. Every epoch uses final revalidation and only the common bounded explicit-false " +
                "retry, with no target change, alternate, rerank, or replay. A reset that happens entirely between " +
                "two framework frames is deliberately " +
                "missed rather than guessed. Dark Arts Shadowbringer runs before Hiebsprung; the safe HP-cost " +
                "Shadowbringer fallback runs after it, followed by held Monk combo and the cross-job survival helpers.");

            ImGui.Spacing();
            changed |= Checkbox(
                "Shadowbringer on held gameplay key (experimental)",
                configuration.EnableDarkKnightShadowbringerOnHeldKey,
                value => configuration.EnableDarkKnightShadowbringerOnHeldKey = value);
            changed |= SliderInt(
                "Base Shadowbringer minimum HP",
                configuration.DarkKnightShadowbringerMinimumHpPercent,
                DarkKnightShadowbringerRules.MinimumConfigurableHpPercent,
                DarkKnightShadowbringerRules.MaximumConfigurableHpPercent,
                value => configuration.DarkKnightShadowbringerMinimumHpPercent = value,
                "%d%%");
            changed |= SliderInt(
                "Base Shadowbringer pressure must stay below",
                configuration.DarkKnightShadowbringerPressureLimitExclusive,
                DarkKnightShadowbringerRules.MinimumPressureLimitExclusive,
                DarkKnightShadowbringerRules.MaximumPressureLimitExclusive,
                value => configuration.DarkKnightShadowbringerPressureLimitExclusive = value,
                "%d");
            ImGui.TextDisabled(
                "An exact own Dark Arts proc from a broken Blackest Night always gets the first DRK opportunity and " +
                "does not pay HP. Without Dark Arts, base Shadowbringer is allowed only strictly above the configured " +
                "HP threshold and with a fresh known incoming-pressure count strictly below the configured limit. " +
                "Dark Arts runs before Hiebsprung; the HP-cost fallback runs after Hiebsprung. The lowest-HP reachable " +
                "exact enemy inside native 10-yalm range wins. Unknown pressure blocks only the HP-cost fallback.");
            ImGui.TextDisabled(
                "Default off. Exact Crystalline Conflict uses canonical S1-S5 actors without changing target. Enabled " +
                "Wolves' Den testing uses only the current exact duel opponent or reviewed striking dummy. Each proc or " +
                "eligibility episode freezes one actor; explicit client-false is the only retryable result.");
            ImGui.PopTextWrapPos();
        }

        ImGui.Separator();

        if (ImGui.CollapsingHeader("Ninja — Guard Shukuchi + Seiton", ImGuiTreeNodeFlags.DefaultOpen))
        {
            changed |= Checkbox(
                "Shukuchi to a guarded enemy below 20% HP on held key (experimental)",
                configuration.EnableNinjaGuardShukuchiOnHeldGameplayKey,
                value => configuration.EnableNinjaGuardShukuchiOnHeldGameplayKey = value);
            ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
            ImGui.TextDisabled(
                "Default off, PvP Ninja, and exact Crystalline Conflict only. An independently resolved exact S1-S5 " +
                "enemy must be alive, targetable, strictly below 20% HP, and currently have live Guard / Wehr. " +
                "Shukuchi (29513) uses that same actor's current finite ground position within the native 20-yalm " +
                "range. Missing unrelated enemy slots and missing pressure never block an otherwise exact target.");
            ImGui.TextDisabled(
                "Known positive team pressure is only a ranking bonus; otherwise the lowest exact HP ratio wins, then " +
                "stable S-slot and actor identity. The helper freezes one actor and never substitutes or reranks. " +
                "Only a proven client-false result may use the common bounded same-actor retry. Own Guard and its " +
                "propagation latch block this automatic helper; the explicit /panicshu command remains separate.");
            ImGui.TextDisabled(
                "Only after Shukuchi returns client-accepted does Seiton Sense re-resolve and hard-target that exact " +
                "same living enemy once. Rejection, unknown acceptance, identity drift, or target readback failure never " +
                "changes your target. A continuing hold may let enabled NIN Seiton use a later framework frame.");
            ImGui.PopTextWrapPos();

            ImGui.Spacing();
            changed |= Checkbox(
                "Seiton on held gameplay key (experimental)",
                configuration.EnableNinjaSeitonOnHeldGameplayKey,
                value => configuration.EnableNinjaSeitonOnHeldGameplayKey = value);
            ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
            ImGui.TextDisabled(
                "Default off and exact Crystalline Conflict only. On PvP Ninja, continuous held-key consent can request " +
                "the currently adjusted Seiton Tenchu (29515 or Unsealed follow-up 29516). It considers " +
                "exact canonical S1-S5 enemies that are living, targetable, below 50% HP, and accepted by FFXIV's native " +
                "range/line-of-sight check. A target with Guardian's Covered status, a Paladin's Phalanx self-" +
                "invulnerability, or a Dark Knight's Eventide invulnerability is excluded; Guard itself remains valid. " +
                "The lowest exact HP ratio wins, then stable slot/actor identity. Own Guard or " +
                "its bounded propagation gate blocks the helper. On Ninja, Auto-Seiton is the earliest NIN held job " +
                "helper after Purify and can claim before the later NIN/reactive helpers in that framework frame.");
            ImGui.TextDisabled(
                "Each exact adjusted-action epoch freezes one actor. An explicit client rejection may retry that same " +
                "intent after a short delay while every gate and the same key remain valid; client acceptance ends the " +
                "epoch immediately. A later genuine 29515-to-29516 follow-up epoch may use the continuing hold, but a " +
                "rejected base action is never replaced by the follow-up. Seiton Sense never changes the target, selects " +
                "again inside an epoch, chooses an alternate, falls back, or replays. The frozen actor and its HP are " +
                "read again at the latest safe point before every request; exactly 50% or higher or newly observed " +
                "Covered/LB invulnerability cancels the intent. " +
                "The original gameplay key is not swallowed. A client-accepted return is dispatch feedback only, not " +
                "proof that Seiton landed or killed the target; the final client-to-server race cannot be removed.");
            ImGui.TextDisabled(
                "Use /autoseiton (or click the movable action-bar tile) to switch this availability ON/OFF. The tile " +
                "shows separate ON/OFF icons and sparkles when the resolved Seiton action is ready. ON still requires " +
                "a currently held gameplay key; it never creates no-input automatic actions.");
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
        }

        ImGui.Separator();
        if (ImGui.CollapsingHeader("Samurai — Zantetsuken", ImGuiTreeNodeFlags.DefaultOpen))
        {
            changed |= Checkbox(
                "Use your own SAM Zantetsuken on held key when exact Kuzushi has no shield",
                configuration.EnableSamuraiZantetsukenOnHeldKey,
                value => configuration.EnableSamuraiZantetsukenOnHeldKey = value);
            ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
            ImGui.TextDisabled(
                "Default off and PvP Samurai only. The exact target must have exactly one Kuzushi applied by you, " +
                "zero ShieldPercentage at selection and at the final native boundary, be alive, targetable, in native " +
                "range/line of sight, and you must not be Bound. Crystalline Conflict uses one exact canonical S-slot; " +
                "enabled Wolves' Den testing uses only the exact current duel target or reviewed striking dummy. There " +
                "is no visible target change, alternate, fallback, or retry after the one native request.");
            ImGui.TextDisabled(
                "Purify stays absolute priority. The separate SAM post-Purify/Guard Soten-to-Mineuchi option runs first; " +
                "an accepted Soten reserves its bounded Mineuchi arrival window before Zantetsuken or any lower helper. " +
                "With the global held-helper cast-cancel test enabled, an otherwise-ready frozen SAM intent may request " +
                "the same one-shot native cast cancellation used by reactive counter-CC.");
            ImGui.PopTextWrapPos();
        }

        ImGui.Separator();
        if (ImGui.CollapsingHeader("Viper — Serpentiner Geist", ImGuiTreeNodeFlags.DefaultOpen))
        {
            changed |= Checkbox(
                "Use transformed Serpentiner Geist while a gameplay key is held (includes WASD)",
                configuration.EnableViperSerpentTailOnHeldKey,
                value => configuration.EnableViperSerpentTailOnHeldKey = value);
            ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
            ImGui.TextDisabled(
                "Default off and PvP Viper only. While any eligible gameplay key, including WASD, remains held, " +
                "Seiton Sense checks FFXIV's currently transformed Serpent's Tail / Serpentiner Geist carrier (39183) " +
                "each frame. When FFXIV exposes one reviewed follow-up (39174-39182), the helper may use that exact " +
                "action on your exact current hard target once it is usable. The transformed carrier is the complete " +
                "opportunity signal: Seiton Sense does not record, require, or try to prove a preceding Viper action. " +
                "Carrier 39183 itself is never dispatched. The action, held key, and current hard target freeze for the " +
                "exact attempt/retry episode. The helper does not select, rerank, visibly change, or substitute a target " +
                "or a different follow-up.");
            ImGui.TextDisabled(
                "Purify keeps absolute priority; this is Viper's earliest held job helper. Own Guard blocks it, " +
                "while enemy Guard remains valid because these follow-ups natively ignore Guard when dealing damage. " +
                "Action, resource, target-status, or reach waits yield the frame to a usable lower helper; only an " +
                "otherwise-ready native-boundary or retry-throttle wait keeps Viper's priority. Cast cancellation is " +
                "deliberately unavailable. A clean client rejection may only use the shared bounded same-intent retry " +
                "while the exact key, adjusted action, target, range, and line of sight stay valid. Ambiguity or retry " +
                "exhaustion requires releasing that frozen key before another Viper episode.");
            ImGui.TextDisabled(
                "Available in exact Crystalline Conflict. With the separate Wolves' Den testing toggle, testing is " +
                "restricted to your exact current hard target (<t>): the live hostile duel opponent or the reviewed " +
                "striking dummy (NameId 541). Arbitrary NPCs and synthetic enemy slots are rejected. Client acceptance " +
                "is not proof of a server-side hit.");
            ImGui.PopTextWrapPos();
        }

        ImGui.Separator();
        if (ImGui.CollapsingHeader("Gunbreaker — Continuation", ImGuiTreeNodeFlags.DefaultOpen))
        {
            changed |= Checkbox(
                "Use transformed Continuation follow-ups on held gameplay key (includes WASD)",
                configuration.EnableGunbreakerContinuationOnHeldKey,
                value => configuration.EnableGunbreakerContinuationOnHeldKey = value);
            ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
            ImGui.TextDisabled(
                "Default off and PvP Gunbreaker only. The native Continuation carrier plus the exact own proc status " +
                "must expose Hypervelocity, Jugular Rip, Abdomen Tear, Eye Gouge, or Fated Brand. One transformed proc " +
                "can authorize exactly one call while a gameplay key remains held. CC chooses the lowest-HP reachable " +
                "canonical S1-S5 enemy; Wolves' Den uses only the exact current target. Fated Brand is self-centered " +
                "and requires that frozen enemy inside its exact 6-yalm effect radius.");
            ImGui.TextDisabled(
                "Purify and earlier job-specific work keep priority. The action, proc, key, context, and actor freeze " +
                "before any bounded explicit-false retry. It never cancels casts, changes target, substitutes a follow-up, " +
                "or treats client acceptance as proof of a hit.");
            ImGui.PopTextWrapPos();
        }

        ImGui.Separator();
        if (ImGui.CollapsingHeader("Scholar — Critical Strategy", ImGuiTreeNodeFlags.DefaultOpen))
        {
            changed |= Checkbox(
                "Critical Strategy on held gameplay key (Guard targets only, experimental)",
                configuration.EnableScholarCriticalStrategyOnHeldKey,
                value => configuration.EnableScholarCriticalStrategyOnHeldKey = value);
            ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
            ImGui.TextDisabled(
                "Default off, PvP Scholar, and exact Crystalline Conflict only. Continuous held physical gameplay-key " +
                "consent may request Critical Strategy (29716) only against a living, targetable exact canonical " +
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
                "The frozen intent is revalidated before every possible bounded call for exact identity, action " +
                "readiness, live Guard, and native range/line of sight. Only an explicit client rejection may retry " +
                "that same target. It " +
                "never changes a hard, soft, focus, or mouseover target, reranks, selects an alternate after drift, " +
                "substitutes another action, falls back, or replays. The original key is not swallowed, and " +
                "client acceptance does not prove that Critical Strategy landed or changed Guard.");
            ImGui.TextDisabled(
                "Purify, AST, SAM, NIN Seiton, VPR Serpentiner Geist, GNB Continuation, reactive counter-CC, Ally Rescue, Guardian, and Guard-Shukuchi precede SCH. " +
                "DRK Dark Arts, Hiebsprung, the safe DRK fallback, and held Monk combo follow before the cross-job survival helpers.");
            ImGui.PopTextWrapPos();
        }

        ImGui.Separator();
        if (ImGui.CollapsingHeader("Sage — Smart Kardia", ImGuiTreeNodeFlags.DefaultOpen))
        {
            changed |= DrawSageKardiaControls();
        }

        ImGui.Separator();
        if (ImGui.CollapsingHeader("Bard — Smart Paean target", ImGuiTreeNodeFlags.DefaultOpen))
        {
            changed |= DrawBardWardensPaeanPressureRedirectControls();
        }

        ImGui.Separator();
        if (ImGui.CollapsingHeader("Monk — held combo + Earth's Reply", ImGuiTreeNodeFlags.DefaultOpen))
        {
            changed |= Checkbox(
                "Run the PvP Monk combo on held gameplay key (includes WASD)",
                configuration.EnableMonkHeldComboOnHeldKey,
                value => configuration.EnableMonkHeldComboOnHeldKey = value);
            ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
            ImGui.TextDisabled(
                "Default off. CC first prefers reachable melee enemies, then lowest exact HP; Wolves' Den uses only " +
                "the exact current duel opponent or reviewed dummy. The helper follows the native seven-step combo " +
                "carrier exactly and uses Wind's Reply only as the reviewed ranged fallback. It keeps one Rising " +
                "Phoenix charge reserved for the deliberate Pressure Point → Thunderclap when needed → Rising Phoenix " +
                "→ Fire Resonance → Phantom Rush finish. Every proof, range edge, action, actor, and held key is frozen " +
                "and revalidated; it never changes target or guesses a missing combo/status transition.");
            ImGui.PopTextWrapPos();
            ImGui.Spacing();
            changed |= DrawMonkEarthReplyControls();
        }

        return changed;
    }

    private bool DrawSageKardiaControls()
    {
        var changed = Checkbox(
            "After accepted Eukrasia: one Smart Kardia opportunity (experimental)",
            configuration.EnableSageKardiaAfterEukrasia,
            value => configuration.EnableSageKardiaAfterEukrasia = value);
        ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
        ImGui.TextDisabled(
            "Default off, PvP Sage, and exact Crystalline Conflict only. Seiton never alters or suppresses the " +
            "incoming Eukrasia (29258) call. Only after that call is client-accepted and a real local Eukrasia " +
            "charge/status transition is observed does one short-lived Kardia opportunity open. Kardia waits for " +
            "animation lock to clear instead of being fired unreliably inside the Eukrasia call.");
        ImGui.TextDisabled(
            "A fresh complete exact five-player party/pressure publication selects living, targetable candidates at " +
            "2+ direct pressure by highest pressure, then lowest exact HP ratio and stable party/actor identity. If " +
            "nobody qualifies, the exact local Sage is the sole initial self fallback. A non-self target must also " +
            "pass native 30-yalm range and line of sight. An unknown or already-own Kardion state on the selected " +
            "actor ends the opportunity; it never falls through to another ally or self.");
        ImGui.TextDisabled(
            "The opportunity and frozen actor are spent before at most one direct-GOID Kardia request. Urgent " +
            "physical-hold helpers through pressure Sprint still win the frame; event Kardia precedes only event Monk. " +
            "Kardia never changes a hard, soft, focus, or mouseover " +
            "target, reranks after commitment, selects an alternate, replays, or retries. 'Incoming Eukrasia' may " +
            "include another plugin's or Turbo's call because the native hook cannot prove a physical origin. Client " +
            "acceptance does not prove that Kardia or Kardion applied.");
        ImGui.PopTextWrapPos();
        return changed;
    }

    private bool DrawBardWardensPaeanPressureRedirectControls()
    {
        var changed = Checkbox(
            "Smart Paean target for manual or Turbo calls at 3+ incoming enemies",
            configuration.EnableBardWardensPaeanPressureRedirect,
            value => configuration.EnableBardWardensPaeanPressureRedirect = value);
        ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
        ImGui.TextDisabled(
            "Default off and exact Crystalline Conflict only. This never casts Paean by itself. It only examines an " +
            "already incoming The Warden's Paean (29400) ability call from the normal action path or a downstream " +
            "Turbo pulse. A complete, unique, stable exact party view is required. Eligible non-self allies must be " +
            "living, targetable, without the live Paean ward (3143), accepted by native 30-yalm range/line of sight, " +
            "and have a trusted current count of at least three unique enemies hard-targeting or casting at them. " +
            "Highest pressure wins, then lowest exact HP ratio, party slot, entity ID, and game-object ID. An unknown " +
            "count excludes only that ally. No exact known 3+ candidate leaves the original call vanilla.");
        ImGui.TextDisabled(
            "Once a redirect is frozen, final identity, job, exact resolved action/metadata, life, HP, Paean ward, " +
            "native range/line-of-sight, or pressure drift suppresses that one call instead of using the original " +
            "target or another ally. There is deliberately no cooldown/readiness gate. It never changes a selected " +
            "target, creates or substitutes an action, replays, or retries. A later Turbo pulse is a separate call. " +
            "Client acceptance does not prove that Paean applied or removed or nullified CC.");
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
            "Riddle of Earth / Steinernes Enigma, changes your target, or retries a rejected action. Event Monk is " +
            "last in the request order.");
        ImGui.PopTextWrapPos();
        return changed;
    }
}
