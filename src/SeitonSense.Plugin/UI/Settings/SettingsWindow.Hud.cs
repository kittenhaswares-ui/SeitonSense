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

        if (ImGui.CollapsingHeader("Wolves' Den CC map rotation", ImGuiTreeNodeFlags.DefaultOpen))
            changed |= DrawWolvesDenRotationControls();

        ImGui.Separator();
        if (ImGui.CollapsingHeader("CC win prediction", ImGuiTreeNodeFlags.DefaultOpen))
            changed |= DrawCrystallineConflictPredictionControls();

        ImGui.Separator();
        if (ImGui.CollapsingHeader("CC medicine kits", ImGuiTreeNodeFlags.DefaultOpen))
            changed |= DrawCrystallineConflictMedicineKitControls();

        ImGui.Separator();
        if (ImGui.CollapsingHeader("PvP range helper", ImGuiTreeNodeFlags.DefaultOpen))
            changed |= DrawPvpRangeHelperControls();

        ImGui.Separator();
        if (ImGui.CollapsingHeader("Pressure counter and pressure nameplates", ImGuiTreeNodeFlags.DefaultOpen))
            changed |= DrawPressureControls();

        ImGui.Separator();
        if (ImGui.CollapsingHeader("Enemy Guard, MP, and CC protection", ImGuiTreeNodeFlags.DefaultOpen))
        {
            changed |= DrawGeneralResourceNameplateControls();
            ImGui.Spacing();
            changed |= Checkbox(
                "Show visible CC protection above enemy nameplates",
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
                "Shows a crossed-CC icon above the job icon for confirmed Guard, Resilience, and other full CC " +
                "immunities. Single-hit wards are not shown as full immunity.");
        }

        ImGui.Separator();
        if (ImGui.CollapsingHeader("Enemy Limit Break activations", ImGuiTreeNodeFlags.DefaultOpen))
        {
            changed |= Checkbox(
                "Show active enemy LB icons above enemy nameplates",
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
                "Long Limit Breaks show their countdown; instant LBs flash briefly. If Seiton cannot confirm the enemy " +
                "or duration, it shows nothing instead of guessing.");
        }

        ImGui.Separator();
        if (ImGui.CollapsingHeader("Low-resource aura", ImGuiTreeNodeFlags.DefaultOpen))
            changed |= DrawResourceAuraControls();

        ImGui.Separator();
        if (ImGui.CollapsingHeader("Shared nameplate appearance", ImGuiTreeNodeFlags.DefaultOpen))
        {
            changed |= Slider(
                "Extra icon size",
                configuration.NameplateIconScale,
                0.55f,
                1.5f,
                value => configuration.NameplateIconScale = value,
                "%.2f x");
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

    private bool DrawCrystallineConflictMedicineKitControls()
    {
        var changed = false;
        changed |= Checkbox(
            "Show countdown to the first medicine kits",
            configuration.ShowCrystallineConflictMedicineKitCountdown,
            value => configuration.ShowCrystallineConflictMedicineKitCountdown = value);
        changed |= Checkbox(
            "Experimental: show green beacons for detected medicine kits",
            configuration.ShowCrystallineConflictMedicineKitBeacons,
            value => configuration.ShowCrystallineConflictMedicineKitBeacons = value);
        changed |= Slider(
            "Medicine-kit overlay scale",
            configuration.CrystallineConflictMedicineKitOverlayScale,
            0.6f,
            2f,
            value => configuration.CrystallineConflictMedicineKitOverlayScale = value,
            "%.2f x");
        ImGui.TextDisabled(
            "Public CC only. The opening countdown follows the 5:00 match timer. Ready medicine kits get a green " +
            "screen beacon when Seiton can identify them. The beacon can stay visible through terrain, but it does not " +
            "target, move, or use the kit for you. Detection still needs live in-match confirmation.");
        return changed;
    }

    private bool DrawPvpRangeHelperControls()
    {
        var changed = false;
        changed |= Checkbox(
            "Show range rings around yourself",
            configuration.ShowPvpRangeHelper,
            value => configuration.ShowPvpRangeHelper = value);
        changed |= Checkbox(
            "Draw above plugin windows##PvpRangeHelper",
            configuration.PvpRangeHelperDrawInForeground,
            value => configuration.PvpRangeHelperDrawInForeground = value);
        ImGui.SameLine();
        changed |= Checkbox(
            "Show range labels##PvpRangeHelper",
            configuration.PvpRangeHelperShowLabels,
            value => configuration.PvpRangeHelperShowLabels = value);
        changed |= Slider(
            "Range-ring opacity",
            configuration.PvpRangeHelperOpacity,
            0.08f,
            1f,
            value => configuration.PvpRangeHelperOpacity = value,
            "%.2f");
        changed |= Slider(
            "Range-ring line width",
            configuration.PvpRangeHelperLineWidth,
            0.75f,
            6f,
            value => configuration.PvpRangeHelperLineWidth = value,
            "%.2f px");

        var meleeColor = configuration.PvpRangeHelperMeleeColor;
        if (ImGui.ColorEdit4("Melee-ring color", ref meleeColor))
        {
            configuration.PvpRangeHelperMeleeColor = meleeColor;
            changed = true;
        }

        var maximumColor = configuration.PvpRangeHelperMaximumColor;
        if (ImGui.ColorEdit4("Maximum-range color", ref maximumColor))
        {
            configuration.PvpRangeHelperMaximumColor = maximumColor;
            changed = true;
        }

        ImGui.TextDisabled(
            "PvP and Wolves' Den only. Inner ring: 5-yalm melee range. Outer ring: this job's farthest supported attack " +
            "or gap closer, excluding Limit Breaks.");
        ImGui.TextDisabled(
            "This is only a flat distance guide. It cannot show line of sight, terrain, cooldowns, or exact hitboxes and " +
            "never changes your target or action.");
        return changed;
    }

    private bool DrawWolvesDenRotationControls()
    {
        var changed = false;
        changed |= Checkbox(
            "Show local CC rotation panel in Wolves' Den",
            configuration.ShowWolvesDenRotationPanel,
            value => configuration.ShowWolvesDenRotationPanel = value);
        changed |= Checkbox(
            "Record local per-map CC W/L",
            configuration.EnableLocalCrystallineConflictMapStatisticsCapture,
            value => configuration.EnableLocalCrystallineConflictMapStatisticsCapture = value);
        ImGui.SameLine();
        if (ImGui.Button("Clear all characters' saved local W/L"))
        {
            crystallineConflictMapStatisticsResetSucceeded =
                resetCrystallineConflictMapStatistics();
            crystallineConflictMapStatisticsResetFeedback =
                crystallineConflictMapStatisticsResetSucceeded
                    ? "All saved local CC map W/L was cleared."
                    : "Could not clear local CC map W/L; the existing file was left unchanged.";
            crystallineConflictMapStatisticsResetFeedbackUntil =
                Environment.TickCount64 + 6_000;
        }
        if (!string.IsNullOrEmpty(crystallineConflictMapStatisticsResetFeedback) &&
            Environment.TickCount64 <= crystallineConflictMapStatisticsResetFeedbackUntil)
        {
            ImGui.TextColored(
                crystallineConflictMapStatisticsResetSucceeded
                    ? new System.Numerics.Vector4(0.4f, 0.9f, 0.62f, 1f)
                    : new System.Numerics.Vector4(1f, 0.45f, 0.42f, 1f),
                crystallineConflictMapStatisticsResetFeedback);
        }
        else if (!string.IsNullOrEmpty(crystallineConflictMapStatisticsResetFeedback))
        {
            crystallineConflictMapStatisticsResetFeedback = string.Empty;
            crystallineConflictMapStatisticsResetFeedbackUntil = 0;
        }
        changed |= Checkbox(
            "Lock rotation panel",
            configuration.WolvesDenRotationPanelLocked,
            value => configuration.WolvesDenRotationPanelLocked = value);
        ImGui.SameLine();
        changed |= Checkbox(
            "Show background##WolvesDenRotation",
            configuration.WolvesDenRotationPanelShowBackground,
            value => configuration.WolvesDenRotationPanelShowBackground = value);
        changed |= Slider(
            "Rotation panel scale",
            configuration.WolvesDenRotationPanelScale,
            0.75f,
            1.75f,
            value => configuration.WolvesDenRotationPanelScale = value,
            "%.2f x");
        changed |= Slider(
            "Rotation panel background opacity",
            configuration.WolvesDenRotationPanelBackgroundOpacity,
            0f,
            1f,
            value => configuration.WolvesDenRotationPanelBackgroundOpacity = value,
            "%.2f");
        if (ImGui.Button("Reset rotation panel position"))
            resetWolvesDenRotationWindowPosition();
        ImGui.TextDisabled(
            "Uses the Patch 7.5 hourly map order and your locally saved phase adjustment. It works offline.");
        ImGui.TextDisabled(
            "Showing the panel and saving your local W/L are separate options. Results are never uploaded.");
        ImGui.TextDisabled(
            "The seven map cards reorder themselves each hour. Use < / > only if your in-game map does not match.");
        ImGui.TextDisabled(
            "Per-map W/L starts counting after you enable it. Unclear or missing results stay NO DATA.");
        return changed;
    }

    private bool DrawCrystallineConflictPredictionControls()
    {
        var changed = false;
        changed |= Checkbox(
            "Show CC win prediction from team reveal through the match",
            configuration.ShowCrystallineConflictPredictionPanel,
            value => configuration.ShowCrystallineConflictPredictionPanel = value);
        changed |= Checkbox(
            "Save local player W/L history",
            configuration.EnableLocalCrystallineConflictPlayerHistory,
            value => configuration.EnableLocalCrystallineConflictPlayerHistory = value);
        var importSnapshot = pvpStatsHistoryImport.Snapshot;
        if (importSnapshot.IsBusy)
        {
            if (ImGui.Button("Cancel PvpStats import"))
                pvpStatsHistoryImport.Cancel();
            ImGui.ProgressBar(
                (float)Math.Clamp(importSnapshot.Progress, 0d, 1d),
                new System.Numerics.Vector2(420f, 0f));
        }
        else if (ImGui.Button("Import old PvpStats player history"))
        {
            pvpStatsHistoryImport.TryStart();
            importSnapshot = pvpStatsHistoryImport.Snapshot;
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.TextUnformatted("One-time, local import for the character currently logged in.");
            ImGui.TextUnformatted("Wolves' Den is supported while you are out of combat.");
            ImGui.TextUnformatted("Unload PvpStats first so Seiton can prove exclusive read-only access.");
            ImGui.TextUnformatted("Only completed Casual and Ranked 5v5 matches count.");
            ImGui.EndTooltip();
        }
        if (!string.IsNullOrWhiteSpace(importSnapshot.Status))
        {
            if (importSnapshot.IsComplete)
            {
                ImGui.TextColored(
                    importSnapshot.Success
                        ? new System.Numerics.Vector4(0.4f, 0.9f, 0.62f, 1f)
                        : new System.Numerics.Vector4(1f, 0.45f, 0.42f, 1f),
                    importSnapshot.Status);
            }
            else
            {
                ImGui.TextDisabled(importSnapshot.Status);
            }
        }
        changed |= Checkbox(
            "Update the prediction while the match changes",
            configuration.EnableDynamicCrystallineConflictPrediction,
            value => configuration.EnableDynamicCrystallineConflictPrediction = value);
        changed |= Checkbox(
            "Lock prediction panel",
            configuration.CrystallineConflictPredictionPanelLocked,
            value => configuration.CrystallineConflictPredictionPanelLocked = value);
        ImGui.SameLine();
        changed |= Checkbox(
            "Show background##CrystallineConflictPrediction",
            configuration.CrystallineConflictPredictionPanelShowBackground,
            value => configuration.CrystallineConflictPredictionPanelShowBackground = value);
        changed |= Slider(
            "Prediction panel scale",
            configuration.CrystallineConflictPredictionPanelScale,
            0.75f,
            1.75f,
            value => configuration.CrystallineConflictPredictionPanelScale = value,
            "%.2f x");
        changed |= Slider(
            "Prediction panel background opacity",
            configuration.CrystallineConflictPredictionPanelBackgroundOpacity,
            0f,
            1f,
            value => configuration.CrystallineConflictPredictionPanelBackgroundOpacity = value,
            "%.2f");
        if (ImGui.Button("Reset prediction panel position"))
            resetCrystallineConflictPredictionWindowPosition();

        ImGui.TextDisabled(
            "A playful estimate from matches saved on this PC. Unknown players count as 50%; nothing is uploaded.");
        ImGui.TextDisabled(
            "Switch between all five allies and all five enemies directly on the movable match panel.");
        ImGui.TextDisabled(
            "W/L stays saved locally. Deaths, damage, healing, and crystal time reset for every match.");
        ImGui.TextDisabled(
            "The optional PvpStats import never writes to its database and stores only salted player keys plus W/L.");
        return changed;
    }

    private bool DrawPressureControls()
    {
        var changed = false;
        ImGui.Spacing();
        ImGui.TextWrapped(
            "Incoming pressure shows enemies targeting or casting at you. Team pressure shows allies targeting each " +
            "enemy. Recent attacks stay visible briefly so the numbers do not flicker.");

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
        changed |= Checkbox(
            "Experimental: show opponent LB bars above the pressure counter",
            configuration.ShowOpponentLimitBreakBars,
            value => configuration.ShowOpponentLimitBreakBars = value);
        ImGui.TextDisabled(
            "Off by default and CC only. This still needs live confirmation for the current game patch. If Seiton " +
            "cannot read every enemy LB bar safely, it hides all of them instead of guessing.");
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
            "Current targeting/casts and recent attacks use different styles. Neither changes your target.");
        if (ImGui.Button(pressureCounter.PreviewEnabled ? "Stop counter preview" : "Preview counter"))
            pressureCounter.PreviewEnabled = !pressureCounter.PreviewEnabled;
        ImGui.SameLine();
        if (ImGui.Button("Reset counter position")) pressureCounter.ResetWindowPosition();
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
            "Guard cooldown and low MP appear only when Seiton has reliable information. Unknown values are not guessed.");
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
            "Red means low HP, blue means low MP, and purple means both. The glow is visual only; it never changes a " +
            "hotbar, team row, target, or action. Each place can be switched off separately. Unknown MP stays unmarked.");
        ImGui.PopTextWrapPos();
        return changed;
    }
}
