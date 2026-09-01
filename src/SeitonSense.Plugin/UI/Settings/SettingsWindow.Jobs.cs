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
            "Optional PvP helpers for each job. Emergency recovery stays above job attacks. Auto-Zantetsuken and " +
            "Auto-Seiton work while switched on; the other action helpers still need a held gameplay key. General " +
            "survival and counter-CC settings are under Action Helpers.");

        if (ImGui.CollapsingHeader("Astrologian — Harmonischer Orbis", ImGuiTreeNodeFlags.DefaultOpen))
        {
            changed |= Checkbox(
                "Harmonischer Orbis + optional Zweifacher Zauber on held key (includes WASD)",
                configuration.EnableAstrologianHarmonicOrbisOnHeldKey,
                value => configuration.EnableAstrologianHarmonicOrbisOnHeldKey = value);
            ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
            ImGui.TextDisabled(
                "Off by default and AST only. While you hold a gameplay key, Seiton uses Harmonischer Orbis on a " +
                "reachable party member at 60% HP or lower. It uses the same choice as /nearhelp and does not change " +
                "your selected or mouseover target.");
            ImGui.TextDisabled(
                "If Double Cast was ready before Orbis, Seiton uses the second heal on the same player as soon as it " +
                "can. If Double Cast was not ready, it stops after Orbis. The second heal keeps the original player " +
                "even if the first heal raises them above 60%.");
            ImGui.TextDisabled(
                "Purify stays first, and your Guard blocks the whole sequence. Seiton keeps the same player during the " +
                "attempt and stops if they become invalid or unreachable. The global held-helper option may cancel your " +
                "current cast only when Orbis is otherwise ready.");
            ImGui.PopTextWrapPos();
        }

        ImGui.Separator();

        if (ImGui.CollapsingHeader("Red Mage — fresh Guard engage", ImGuiTreeNodeFlags.DefaultOpen))
        {
            changed |= Checkbox(
                "Corps-a-corps into a freshly started enemy Guard on held key (includes WASD)",
                configuration.EnableRedMageGuardEngageOnHeldKey,
                value => configuration.EnableRedMageGuardEngageOnHeldKey = value);
            changed |= SliderInt(
                "Minimum own HP for Guard engage",
                configuration.RedMageGuardEngageMinimumHpPercent,
                RedMageGuardEngageRules.MinimumConfigurablePercent,
                RedMageGuardEngageRules.MaximumConfigurablePercent,
                value => configuration.RedMageGuardEngageMinimumHpPercent = value,
                "%d%%");
            changed |= SliderInt(
                "Minimum own MP for Guard engage",
                configuration.RedMageGuardEngageMinimumMpPercent,
                RedMageGuardEngageRules.MinimumConfigurablePercent,
                RedMageGuardEngageRules.MaximumConfigurablePercent,
                value => configuration.RedMageGuardEngageMinimumMpPercent = value,
                "%d%%");
            ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
            ImGui.TextDisabled(
                "Off by default and RDM only. While you hold a gameplay key, Corps-a-corps can engage an enemy during " +
                "the first second of their Guard. It requires the Riposte start of your melee combo and your configured " +
                "HP/MP minimums. Your own Guard, Bind, death, or enemy immunity blocks it.");
            ImGui.TextDisabled(
                "It reacts only when Seiton sees Guard begin; an enemy who was already guarded does not count. The " +
                "chance ends after the first second. Wolves' Den testing uses only your current target.");
            ImGui.TextDisabled(
                "Corps-a-corps uses its normal 25-yalm range. After FFXIV accepts the dash, Seiton selects that same " +
                "enemy once so you can continue the combo. It does not perform the melee combo for you or switch to " +
                "another enemy.");
            ImGui.PopTextWrapPos();
        }

        ImGui.Separator();

        if (ImGui.CollapsingHeader("Paladin — Guardian rescue", ImGuiTreeNodeFlags.DefaultOpen))
        {
            changed |= Checkbox(
                "Guardian for a critical or focused ally",
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
                "Off by default and CC only. Guardian can save a reachable ally within 20 yalms at 20% HP or lower. " +
                "At 21–40% HP it needs at least two enemies focusing them; at 41–50% it needs at least three. " +
                "Critical HP wins first, then more enemy focus and lower HP.");
            ImGui.TextDisabled(
                "Hold a gameplay key to allow the save. Purify remains first and Guardian is next. Your own Guard " +
                "must be ready so the jump does not leave you without protection. Seiton keeps one ally during the attempt and does not visibly change your target. " +
                "The activation card means FFXIV accepted the request, not " +
                "that the ally was definitely saved.");
            ImGui.TextDisabled(
                "The communication option sends the localized CC 'Cover target' Quick Chat and places paired Bind " +
                "markers on the ally and you. Existing or changed markers are left alone.");
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
                "Off by default, DRK only, and CC only. While you hold a gameplay key, Plunge can jump to the " +
                "lowest-HP enemy at 30% or lower within 10 yalms. Bind, either side's Guard, typing, or an obstructed " +
                "target blocks it.");
            ImGui.TextDisabled(
                "It can trigger again while the key stays held only after FFXIV shows that Plunge became ready again, " +
                "including a KO reset. Each attempt keeps one enemy and will not switch targets halfway through.");

            ImGui.Spacing();
            changed |= Checkbox(
                "Shadowbringer on held gameplay key (experimental)",
                configuration.EnableDarkKnightShadowbringerOnHeldKey,
                value => configuration.EnableDarkKnightShadowbringerOnHeldKey = value);
            changed |= Checkbox(
                "Preserve Blackblood before another automatic Shadowbringer",
                configuration.DarkKnightShadowbringerPreserveBlackblood,
                value => configuration.DarkKnightShadowbringerPreserveBlackblood = value);
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
                "A Dark Arts proc from The Blackest Night always allows the free Shadowbringer first, even when your " +
                "normal HP/pressure limits would block it. Without Dark Arts, you must be above the HP setting and " +
                "below the enemy-focus limit. Wolves' Den treats focus as zero but still uses your HP limit.");
            ImGui.TextDisabled(
                "Preserve Blackblood is on by default. After Shadowbringer gives you Blackblood, Seiton waits until you " +
                "spend it on the stronger combo attack or let it expire before using Shadowbringer again. Turning this " +
                "off removes only that wait.");
            ImGui.TextDisabled(
                "Automatic Shadowbringer can run at most once every 1.8 seconds. Dark Arts still wins when both the " +
                "free and HP-cost versions are possible.");
            ImGui.TextDisabled(
                "Off by default. In CC, Shadowbringer uses Smart Action's enemy choice without requiring the macro and " +
                "without changing your visible target. Because it is a line attack, any protected enemy in the line " +
                "blocks it. Wolves' Den uses only your duel target or a supported dummy.");
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
                "Off by default, Ninja only, and CC only. While you hold a gameplay key, Shukuchi can jump to a " +
                "guarded enemy below 20% HP within its normal 20-yalm range.");
            ImGui.TextDisabled(
                "It prefers team focus, then lower HP. Each attempt keeps one enemy and will not switch halfway through. " +
                "Your own Guard blocks this helper; /panicshu remains separate.");
            ImGui.TextDisabled(
                "After FFXIV accepts Shukuchi, Seiton selects that same living enemy once. If the jump fails or the enemy " +
                "changes, your target stays untouched. Auto-Seiton may act afterward if enabled.");
            ImGui.PopTextWrapPos();

            ImGui.Spacing();
            changed |= Checkbox(
                "Automatic Seiton when available (experimental)",
                configuration.EnableNinjaSeitonOnHeldGameplayKey,
                value => configuration.EnableNinjaSeitonOnHeldGameplayKey = value);
            ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
            ImGui.TextDisabled(
                "Off by default. While Auto-Seiton is armed, Ninja automatically uses the current base or Unsealed " +
                "Seiton on a reachable enemy below 50% HP. It prefers the lowest HP. Covered, Paladin LB, Dark Knight " +
                "LB, and your own Guard block it; enemy Guard does not. Wolves' Den uses only your current target.");
            ImGui.TextDisabled(
                "Each ready Seiton keeps one enemy and checks their HP and protection again before use. If they return " +
                "to 50% HP or gain immunity, it stops. The Unsealed follow-up is a new chance. Active casts and animation " +
                "lock are waited out, and no gameplay key is required.");
            ImGui.TextDisabled(
                "Use /autoseiton (or click the movable action-bar tile) to switch this availability ON/OFF. The tile " +
                "shows separate ON/OFF icons and sparkles when the resolved Seiton action is ready. ON is fully automatic " +
                "and needs no held or freshly pressed gameplay key.");
            ImGui.PopTextWrapPos();

            ImGui.Spacing();
            changed |= Checkbox(
                "Seiton-ready icon + enemy slot (NIN)",
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
                "Automatically use SAM Zantetsuken on your own Kuzushi target's best 5y cluster",
                configuration.EnableSamuraiZantetsukenOnHeldKey,
                value => configuration.EnableSamuraiZantetsukenOnHeldKey = value);
            ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
            ImGui.TextDisabled(
                "Off by default and Samurai only; no held key is required. Your first exact Kuzushi starts a 1.5-second " +
                "collection window without locking a target. Seiton then checks the current marked enemies and uses " +
                "Zantetsuken on the reachable target whose 5-yalm circle hits the most vulnerable enemies. Covered, " +
                "Paladin LB, and Dark Knight LB are skipped; Guard, Chiten, and shields are allowed. Wolves' Den uses " +
                "only your duel target or a supported dummy and still requires your Kuzushi.");
            ImGui.TextDisabled(
                "Purify and the SAM Soten → Mineuchi counter come first without restarting collection. After the 1.5 " +
                "seconds, Seiton freezes one target while waiting for casts or animation lock and checks Kuzushi again " +
                "just before use.");
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
                "Off by default and Viper only. While you hold a gameplay key, Seiton uses the currently available " +
                "Serpent's Tail follow-up on Smart Action's best reachable enemy. It prefers range, low HP, team focus, " +
                "unavailable Guard, and low MP. Your visible target does not change.");
            ImGui.TextDisabled(
                "Purify stays first. Your Guard, Chiten, Covered, Paladin LB, or Dark Knight LB blocks it. Enemy Guard " +
                "is allowed only for follow-ups that naturally deal damage through Guard. This helper never cancels " +
                "your cast and keeps the same action and enemy for each attempt.");
            ImGui.TextDisabled(
                "Works in CC. Wolves' Den testing uses only your current duel target or a supported striking dummy.");
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
                "Off by default and Gunbreaker only. While you hold a gameplay key, Seiton uses an available " +
                "Continuation follow-up once on the lowest-HP reachable enemy. Wolves' Den uses only your current " +
                "target. Fated Brand requires that enemy within its 6-yalm area.");
            ImGui.TextDisabled(
                "Purify and earlier job helpers stay first. It keeps the same proc and enemy, never cancels your cast, " +
                "and does not visibly change your target.");
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
                "Off by default, Scholar only, and CC only. While you hold a gameplay key, Critical Strategy can target " +
                "a guarded enemy within its normal 25-yalm range. On Guard, it halves Guard's defensive bonus for 10 " +
                "seconds instead of applying its normal damage-taken effect.");
            ImGui.TextDisabled(
                "When pressure data is clear, it prefers the guarded enemy under the most team focus, then lower HP. " +
                "If pressure is unclear or zero, lower HP wins.");
            ImGui.TextDisabled(
                "Each attempt keeps one enemy and checks Guard, range, and line of sight again before use. It never " +
                "changes your selected, focus, soft, or mouseover target.");
            ImGui.TextDisabled(
                "Shorter emergency windows, counter-CC, rescue, and Guardian run before this helper.");
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
                "Off by default. While you hold a gameplay key, Seiton follows Monk's normal PvP combo on a reachable " +
                "low-HP enemy. It uses Wind's Reply in melee and as the ranged backup, and saves one Rising Phoenix for " +
                "the planned Pressure Point → Thunderclap → Rising Phoenix → Fire Resonance → Phantom Rush finish. " +
                "It never visibly changes your target or guesses a missing combo step.");
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
            "Off by default, Sage only, and CC only. After FFXIV accepts Eukrasia and its status appears, Seiton gets " +
            "one short chance to use Kardia. It waits for Eukrasia's animation to finish first.");
        ImGui.TextDisabled(
            "It prefers a reachable party member focused by at least two enemies, then more focus and lower HP. If " +
            "nobody qualifies, it can choose you. A player who already has your Kardion is skipped.");
        ImGui.TextDisabled(
            "It tries once and never changes your selected, focus, soft, or mouseover target. Eukrasia from Turbo or " +
            "another plugin may also open the chance.");
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
            "Off by default and CC only. This does not press Paean for you. When you or Turbo uses Paean, Seiton can " +
            "redirect it to a reachable ally without the ward who is focused by at least three enemies. More focus " +
            "wins, then lower HP. If nobody qualifies, your original Paean target is used.");
        ImGui.TextDisabled(
            "Once redirected, it keeps that ally. If they become invalid before use, that Paean press is stopped " +
            "instead of switching again. Your visible target does not change.");
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
            "Off by default and Monk only. Uses Earth's Reply while Earth Resonance is active at your chosen low-HP " +
            "limit or shortly before the effect expires. It never starts Riddle of Earth or changes your target.");
        ImGui.PopTextWrapPos();
        return changed;
    }
}
