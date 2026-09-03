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
            "Enable optional /smartaction and /seitonfar harmful-action targeting",
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
            "If no useful team-focus target exists, Near Assist uses its normal nearest choice and finally your <t> target.");

        ImGui.TextUnformatted("Near Help survival preference");
        changed |= Checkbox(
            "Prefer incoming pressure near the lowest-health target",
            configuration.NearHelpPreferIncomingPressure,
            value => configuration.NearHelpPreferIncomingPressure = value);
        ImGui.TextDisabled(
            "Near Help normally chooses the lowest-HP ally. Below 25% HP, that always wins. Above that, this option " +
            "may prefer a nearby ally under more enemy focus when their HP is within 10 percentage points.");

        ImGui.Separator();
        if (ImGui.CollapsingHeader("Smart Action macro — choose a better enemy", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.TextUnformatted("Smart Action chooses a safe enemy for your next attack. If it cannot find one, the macro uses your normal <t> target.");
            ImGui.TextColored(new Vector4(0.5f, 1f, 0.65f, 1f), "/mlock");
            ImGui.TextColored(new Vector4(0.85f, 0.9f, 1f, 1f), "/smartaction");
            ImGui.TextColored(new Vector4(0.85f, 0.9f, 1f, 1f), "/pvpac \"Ability\" <e1>");
            ImGui.TextColored(new Vector4(0.85f, 0.9f, 1f, 1f), "/pvpac \"Ability\" <t>");
            ImGui.TextUnformatted("Use /seitonfar instead of /smartaction to choose the farthest reachable safe enemy.");
            ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
            ImGui.TextDisabled(
                "Crystalline Conflict only. Seiton chooses a living, reachable, safe enemy. It prefers low HP, team " +
                "focus, unavailable Guard, and low MP. Melee jobs prefer melee range first. /seitonfar chooses the " +
                "farthest safe enemy instead. Your visible target does not change. /ssaction is an alias.");
            ImGui.TextDisabled(
                "Most cast-time attacks keep your visible target so FFXIV does not turn you unexpectedly. Instant " +
                "attacks use Smart Action. Ogi Namikiri and Tendo Setsugekka are the supported cast exceptions.");
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
                "Crystalline Conflict only. This attack follows a nearby ally's target. If that is not possible, the " +
                "<t> line uses your target. Your visible target does not change. Keep /mlock at the top, disable the " +
                "separate NearAssist plugin, and use /ssassist if you prefer the alias.");
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
                "Near Help sends this action to the reachable party member who needs it most, which may include you. " +
                "Heals with a cast time use the same selection. The ally is chosen once before the cast starts. " +
                "If nobody is valid, the <t> line uses your normal target. Your visible target does not change. Keep " +
                "/mlock at the top so Turbo does not restart the macro.");
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
                "Works with Guardian, Icarus, Thunderclap, Aetherial Manipulation, and Slither. Far Help always picks " +
                "the farthest reachable ally. Healer, role, job, and nearby-enemy information never change that choice. " +
                "Use exactly the three lines above: there is no <t> fallback, and no valid ally means no movement. " +
                "Your visible target does not change. Keep /mlock at the top; /ssfar is an alias.");
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
                "Ninja only. Each press tries Shukuchi once, 19.5 yalms straight ahead of your character. It does not " +
                "open the ground cursor or read/change a target.");
            ImGui.TextDisabled(
                "Works in CC. Wolves' Den needs the testing option; Frontline and Rival Wings are blocked. It can break " +
                "your own Guard. It does nothing while Three Mudra turns Shukuchi into Doton.");
            ImGui.TextDisabled(
                "The command first checks that Shukuchi is really ready, then sends one request. It does not wait or " +
                "retry. A wall or invalid ground means no jump. Technical results remain available in /seiton debug.");
            ImGui.PopTextWrapPos();
        }

        ImGui.Spacing();
        changed |= Checkbox(
            "Enable directional dash macros (/seitonbw, /seitonenavant)",
            configuration.EnableBackwardPanicShukuchiCommand,
            value => configuration.EnableBackwardPanicShukuchiCommand = value);
        if (ImGui.CollapsingHeader("Directional dashes — NIN / AST / DNC / DRG / RPR / PCT", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.TextUnformatted("Camera-back macro:");
            ImGui.TextColored(new Vector4(0.5f, 1f, 0.65f, 1f), "/seitonbw");
            ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
            ImGui.TextDisabled(
                "Off by default. Uses the current job's supported dash once toward the back of your camera without " +
                "turning the camera or changing your target. Supports NIN, AST, DNC, DRG, RPR, and PCT.");
            ImGui.TextDisabled(
                "Works with normal first- and third-person cameras. It does nothing in lock-on/aiming mode, events, " +
                "spectator mode, on unsupported jobs, or when the dash is not ready.");
            ImGui.TextDisabled(
                "Works in CC; Wolves' Den needs the testing option. It may break your Guard and tries only once. NIN " +
                "jumps 19.5 yalms to the ground point; other jobs briefly turn only the character so the dash travels " +
                "screen-back. If another plugin owns or rewrites that dash unsafely, Seiton does nothing.");
            ImGui.Spacing();
            ImGui.TextUnformatted("DNC current-movement macro:");
            ImGui.TextColored(new Vector4(0.5f, 1f, 0.65f, 1f), "/seitonenavant");
            ImGui.TextDisabled(
                "Dancer only. While you are already moving, this uses En Avant once in the direction your character is " +
                "actually traveling: forward, backward, sideways, or diagonal. It works with keyboard remaps and both " +
                "movement modes. If Seiton cannot read a clear current movement direction, it does nothing. Controller " +
                "and autorun behavior still need live testing.");
            ImGui.PopTextWrapPos();
        }

        return changed;
    }
}
