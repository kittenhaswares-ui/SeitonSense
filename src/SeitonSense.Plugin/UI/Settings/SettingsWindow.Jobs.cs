using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace SeitonSense.Plugin.UI;

internal sealed partial class SettingsWindow
{
    private bool DrawJobToolsPage()
    {
        var changed = false;
        ImGui.Spacing();
        ImGui.TextWrapped(
            "Job-specific PvP cues and helpers. Cross-job survival and counter-CC tools are grouped under Action Helpers.");

        if (ImGui.CollapsingHeader("Ninja — Seiton", ImGuiTreeNodeFlags.DefaultOpen))
        {
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
                "selects again, chooses an alternate, falls back, replays, or retries. The frozen actor and its HP are " +
                "read again at the latest safe point before the request; exactly 50% or higher cancels the spent attempt. " +
                "The original gameplay key is not swallowed. A client-accepted return is dispatch feedback only, not " +
                "proof that Seiton landed or killed the target; the final client-to-server race cannot be removed.");
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
        if (ImGui.CollapsingHeader("Scholar — Critical Strategy", ImGuiTreeNodeFlags.DefaultOpen))
        {
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
        if (ImGui.CollapsingHeader("Monk — Earth's Reply", ImGuiTreeNodeFlags.DefaultOpen))
        {
            changed |= DrawMonkEarthReplyControls();
        }

        return changed;
    }

    private bool DrawSageKardiaControls()
    {
        var changed = Checkbox(
            "Smart Kardia on held gameplay key at 2+ incoming enemies (experimental)",
            configuration.EnableSageKardiaOnHeldKey,
            value => configuration.EnableSageKardiaOnHeldKey = value);
        ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
        ImGui.TextDisabled(
            "Default off, PvP Sage, and exact Crystalline Conflict only. One shared held physical gameplay-key " +
            "generation may request Kardia only after a complete, unique, stable exact five-player party view. " +
            "Self and exact living, targetable party members are eligible at a trusted current count of at least " +
            "two unique enemies directly hard-targeting or casting at them; non-self candidates must also pass " +
            "FFXIV's native 30-yalm range and line-of-sight check.");
        ImGui.TextDisabled(
            "Higher incoming pressure wins, then lower exact HP ratio, party slot, entity ID, and game-object ID. " +
            "If the highest-ranked candidate already has Kardion sourced by you, the helper makes no attempt and " +
            "never falls through to a lower-ranked candidate. The frozen actor, direct pressure, local-source " +
            "Kardion state, exact action metadata/readiness, and native reachability are revalidated at the final " +
            "boundary.");
        ImGui.TextDisabled(
            "Self Purify, Guard or Guardian, pressure Sprint, Ally Rescue, and reactive counter-CC keep priority. " +
            "The intent and shared generation are consumed before at most one native Kardia request. It never " +
            "changes a hard, soft, focus, or mouseover target, selects an alternate after drift, falls back, " +
            "substitutes another action, replays, or retries. The original key is not swallowed, and client " +
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
            "Riddle of Earth / Steinernes Enigma, changes your target, or retries a rejected action.");
        ImGui.PopTextWrapPos();
        return changed;
    }
}
