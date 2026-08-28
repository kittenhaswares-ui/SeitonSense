using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace SeitonSense.Plugin.UI;

internal sealed partial class SettingsWindow
{
    private bool DrawMacroHelpersPage()
    {
        var changed = false;
        ImGui.Spacing();
        ImGui.TextColored(new Vector4(0.3f, 0.8f, 1f, 1f), "MACRO TARGET HELPERS (OPT-IN)");
        changed |= Checkbox(
            "Enable one-shot /nearassist, /nearhelp, and /farhelp targeting",
            configuration.EnableNearAssistMacro,
            value => configuration.EnableNearAssistMacro = value);
        changed |= Checkbox(
            "Enable optional /smartaction harmful-action targeting",
            configuration.EnableSmartActionMacro,
            value => configuration.EnableSmartActionMacro = value);
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
        if (ImGui.CollapsingHeader("Smart Action macro — optional harmful-action redirect", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.TextUnformatted("Smart target first, current <t> only as the authored fallback:");
            ImGui.TextColored(new Vector4(0.5f, 1f, 0.65f, 1f), "/mlock");
            ImGui.TextColored(new Vector4(0.85f, 0.9f, 1f, 1f), "/smartaction");
            ImGui.TextColored(new Vector4(0.85f, 0.9f, 1f, 1f), "/pvpac \"Ability\" <e1>");
            ImGui.TextColored(new Vector4(0.85f, 0.9f, 1f, 1f), "/pvpac \"Ability\" <t>");
            ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
            ImGui.TextDisabled(
                "Crystalline Conflict only. /smartaction arms one 750 ms token and resolves the actual harmful PvP " +
                "action on the next line. No selected target is required. Only living, targetable exact S1-S5 " +
                "enemies inside that action's native range and line of sight are considered; live Guard is excluded.");
            ImGui.TextDisabled(
                "Inside the relevant reach tier, ranking is lowest exact HP%, then highest fresh team pressure, " +
                "observed Guard cooldown unavailable, lowest trusted MP%, and stable S-slot. Melee jobs first prefer " +
                "5-yalm melee reach, then enemies no farther than that job's own reviewed gap-closer range. " +
                "The <e1> line is only a carrier. When no exact smart target survives final revalidation, Seiton " +
                "invalidates that carrier and leaves the following <t> line as the only fallback. It never visibly " +
                "changes your target, retries, reranks after commitment, or sends an action by itself. /ssaction is " +
                "the collision-free alias.");
            ImGui.PopTextWrapPos();
        }

        ImGui.Separator();
        if (ImGui.CollapsingHeader("Near Assist macro — hostile ally-target assist", ImGuiTreeNodeFlags.DefaultOpen))
        {
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
        }

        ImGui.Separator();
        if (ImGui.CollapsingHeader("Near Help macro — survival target", ImGuiTreeNodeFlags.DefaultOpen))
        {
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
        }

        ImGui.Separator();
        if (ImGui.CollapsingHeader("Far Help macro — mobility destination", ImGuiTreeNodeFlags.DefaultOpen))
        {
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
        }

        ImGui.Separator();
        ImGui.TextColored(new Vector4(0.7f, 0.45f, 1f, 1f), "NIN PANIC SHUKUCHI MACRO (MANUAL)");
        if (ImGui.CollapsingHeader("Panic Shukuchi — straight-ahead ground jump", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.TextUnformatted("Use this single macro line:");
            ImGui.TextColored(new Vector4(0.5f, 1f, 0.65f, 1f), "/panicshu");
            ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
            ImGui.TextDisabled(
                "This is an explicit NIN-only macro command, never an automatic, proc-driven, or held-key helper. " +
                "Each command computes the terrain point 19.5 yalms along your character's current facing and immediately " +
                "tries Shukuchi exactly once. It neither " +
                "opens or moves the ground cursor nor reads, changes, or substitutes a target.");
            ImGui.TextDisabled(
                "Exact Crystalline Conflict is supported directly. Wolves' Den additionally requires the existing " +
                "Start-page testing option. Frontline and Rival Wings remain blocked. The command is intentionally allowed " +
                "from your own Guard so Shukuchi may break it. Three Mudra changing Shukuchi into Doton still rejects the command.");
            ImGui.TextDisabled(
                "There is no pending state, 500-ms lease, wait, expiry, scheduler priority, or automatic retry. The command " +
                "first requires Shukuchi's exact native recast to be positively ready, preventing a predicted startup that the " +
                "server would roll back. FFXIV then accepts or rejects that one request in the current Guard/cast/queue/animation state. " +
                "Normal results stay out of chat and remain visible in /seiton debug. A wall or invalid terrain never causes " +
                "a shorter fallback, new point, alternate action, or later jump.");
            ImGui.PopTextWrapPos();
        }

        ImGui.Spacing();
        changed |= Checkbox(
            "Enable /seitonbw camera-back job dash",
            configuration.EnableBackwardPanicShukuchiCommand,
            value => configuration.EnableBackwardPanicShukuchiCommand = value);
        if (ImGui.CollapsingHeader("Camera-back dash — NIN / AST / DNC / DRG / RPR / PCT", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.TextUnformatted("Use this single macro line:");
            ImGui.TextColored(new Vector4(0.5f, 1f, 0.65f, 1f), "/seitonbw");
            ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
            ImGui.TextDisabled(
                "This default-off command makes one immediate camera-back escape with the current job's reviewed PvP " +
                "self dash: NIN Shukuchi, AST Epicycle, DNC En Avant, DRG Elusive Jump, RPR Hell's Ingress, or PCT Smudge. " +
                "It never rotates the camera and never reads, changes, or substitutes your hard target.");
            ImGui.TextDisabled(
                "Normal first-person and regular third-person camera modes are supported. Missing or non-finite camera " +
                "data, a cutscene/event, spectator, aiming, or lock-on camera, an unreviewed job, transformed action, " +
                "occupied action boundary, or non-ready native charge ends that command without an action.");
            ImGui.TextDisabled(
                "Crystalline Conflict is supported directly; Wolves' Den still requires the Start-page testing option. " +
                "The command may deliberately break your own Guard and makes at most one immediate native request. NIN " +
                "keeps its exact 19.5-yalm ground point; the other jobs briefly align only character facing so their native " +
                "forward/backward dash travels screen-back. It has no queue, pending lease, retry, fallback, or later replay.");
            ImGui.PopTextWrapPos();
        }

        return changed;
    }
}
