using Dalamud.Bindings.ImGui;

namespace SeitonSense.Plugin.UI;

internal sealed partial class SettingsWindow
{
    private bool DrawHudAndNameplatesPage()
    {
        var changed = false;
        ImGui.Spacing();
        ImGui.TextWrapped(
            "Read-only pressure, resource, and protection cues. These controls never select a target or issue an action.");

        if (ImGui.CollapsingHeader("Pressure counter and pressure nameplates", ImGuiTreeNodeFlags.DefaultOpen))
            changed |= DrawPressureControls();

        ImGui.Separator();
        if (ImGui.CollapsingHeader("Enemy Guard, MP, and CC protection", ImGuiTreeNodeFlags.DefaultOpen))
        {
            changed |= DrawGeneralResourceNameplateControls();
            ImGui.Spacing();
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
                "CC protection emblem size",
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
                "SAM, WAR, VPR, and large-scale PvP immunity. Ambiguous one-hit wards are not labelled as full immunity.");
        }

        ImGui.Separator();
        if (ImGui.CollapsingHeader("Enemy Limit Break activations", ImGuiTreeNodeFlags.DefaultOpen))
        {
            changed |= Checkbox(
                "Show active enemy LB icons above exact native nameplates",
                configuration.ShowEnemyLimitBreaksOnNameplates,
                value => configuration.ShowEnemyLimitBreaksOnNameplates = value);
            changed |= Slider(
                "LB nameplate icon size",
                configuration.LimitBreakNameplateScale,
                0.75f,
                1.75f,
                value => configuration.LimitBreakNameplateScale = value,
                "%.2f x");
            ImGui.TextDisabled(
                "Duration LBs keep their verified countdown. Instant LBs flash briefly. Exact actor identity and a " +
                "fresh native nameplate are required; unknown duration is never guessed.");
        }

        ImGui.Separator();
        if (ImGui.CollapsingHeader("Low-resource aura", ImGuiTreeNodeFlags.DefaultOpen))
            changed |= DrawResourceAuraControls();

        ImGui.Separator();
        if (ImGui.CollapsingHeader("Shared native-nameplate appearance", ImGuiTreeNodeFlags.DefaultOpen))
        {
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
        }

        return changed;
    }

    private bool DrawPressureControls()
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
}
