using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using SeitonSense.Plugin.Models;
using SeitonSense.Plugin.Services;

namespace SeitonSense.Plugin.UI;

internal sealed partial class SettingsWindow : Window
{
    private readonly PluginConfiguration configuration;
    private readonly ExecuteTracker tracker;
    private readonly PersonalStatusService personalStatus;
    private readonly OverlayRenderer overlay;
    private readonly TargetPressureTracker pressureTracker;
    private readonly IsolationAwarenessService isolationAwareness;
    private readonly PressureCounterWindow pressureCounter;
    private readonly CrystallineConflictInstantLeaveService crystallineConflictInstantLeave;
    private readonly CrystallineConflictPvpStatsHistoryImportService pvpStatsHistoryImport;
    private readonly IPlayerState playerState;
    private readonly IDataManager dataManager;
    private readonly CrystallineConflictMapStatisticsService mapStatistics;
    private readonly Action resetBufferLearningWindowPosition;
    private readonly Action resetWolvesDenRotationWindowPosition;
    private readonly Action resetCrystallineConflictPredictionWindowPosition;
    private readonly Func<bool> resetCrystallineConflictMapStatistics;
    private string crystallineConflictMapStatisticsResetFeedback = string.Empty;
    private bool crystallineConflictMapStatisticsResetSucceeded;
    private long crystallineConflictMapStatisticsResetFeedbackUntil;
    private SettingsPage selectedPage = SettingsPage.Start;
    private bool showAllJobTools;

    public SettingsWindow(
        PluginConfiguration configuration,
        ExecuteTracker tracker,
        PersonalStatusService personalStatus,
        OverlayRenderer overlay,
        TargetPressureTracker pressureTracker,
        IsolationAwarenessService isolationAwareness,
        PressureCounterWindow pressureCounter,
        CrystallineConflictInstantLeaveService crystallineConflictInstantLeave,
        CrystallineConflictPvpStatsHistoryImportService pvpStatsHistoryImport,
        IPlayerState playerState,
        IDataManager dataManager,
        CrystallineConflictMapStatisticsService mapStatistics,
        Action? resetBufferLearningWindowPosition = null,
        Action? resetWolvesDenRotationWindowPosition = null,
        Func<bool>? resetCrystallineConflictMapStatistics = null,
        Action? resetCrystallineConflictPredictionWindowPosition = null)
        : base("Seiton Sense###SeitonSenseSettings")
    {
        this.configuration = configuration;
        this.tracker = tracker;
        this.personalStatus = personalStatus;
        this.overlay = overlay;
        this.pressureTracker = pressureTracker;
        this.isolationAwareness = isolationAwareness;
        this.pressureCounter = pressureCounter;
        this.crystallineConflictInstantLeave = crystallineConflictInstantLeave;
        this.pvpStatsHistoryImport = pvpStatsHistoryImport;
        this.playerState = playerState;
        this.dataManager = dataManager;
        this.mapStatistics = mapStatistics;
        this.resetBufferLearningWindowPosition =
            resetBufferLearningWindowPosition ?? (() => { });
        this.resetWolvesDenRotationWindowPosition =
            resetWolvesDenRotationWindowPosition ?? (() => { });
        this.resetCrystallineConflictPredictionWindowPosition =
            resetCrystallineConflictPredictionWindowPosition ?? (() => { });
        this.resetCrystallineConflictMapStatistics =
            resetCrystallineConflictMapStatistics ?? (() => false);
        Size = new Vector2(880f, 760f);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        ImGui.TextColored(new Vector4(0.98f, 0.2f, 0.48f, 1f), "PvP REACTION CUES");
        ImGui.SameLine();
        ImGui.TextDisabled("Seiton, pressure, warnings and target clarity in one place");

        var changed = Checkbox(
            "Enable Seiton Sense",
            configuration.Enabled,
            value =>
            {
                configuration.Enabled = value;
                if (value) return;
                overlay.PreviewEnabled = false;
                overlay.CcProtectionPreviewEnabled = false;
                overlay.ResourceAuraPreviewEnabled = false;
                overlay.IsolationWarningPreviewEnabled = false;
                overlay.HighPressureWarningPreviewEnabled = false;
                pressureCounter.PreviewEnabled = false;
            });

        ImGui.Separator();
        var sidebarWidth = 174f * ImGuiHelpers.GlobalScale;
        if (ImGui.BeginChild("##SeitonSenseSettingsSidebar", new Vector2(sidebarWidth, 0f)))
            DrawSidebar();
        ImGui.EndChild();

        ImGui.SameLine();
        if (ImGui.BeginChild($"##SeitonSenseSettingsContent{selectedPage}", Vector2.Zero))
        {
            changed |= selectedPage switch
            {
                SettingsPage.Start => DrawStartPage(),
                SettingsPage.Alerts => DrawAlertsPage(),
                SettingsPage.HudAndNameplates => DrawHudAndNameplatesPage(),
                SettingsPage.PlayerStats => DrawPlayerStatsPage(),
                SettingsPage.ActionHelpers => DrawActionHelpersPage(),
                SettingsPage.PingHelpers => DrawPingHelpersPage(),
                SettingsPage.JobTools => DrawJobToolsPage(),
                SettingsPage.MacroHelpers => DrawMacroHelpersPage(),
                SettingsPage.Targets => DrawTargetsPage(),
                SettingsPage.Diagnostics => DrawDiagnosticsPage(),
                _ => false,
            };
        }

        ImGui.EndChild();
        if (changed) configuration.Save();
    }

    public override void OnClose()
    {
        crystallineConflictMapStatisticsResetFeedback = string.Empty;
        crystallineConflictMapStatisticsResetFeedbackUntil = 0;
        overlay.PreviewEnabled = false;
        overlay.CcProtectionPreviewEnabled = false;
        overlay.ResourceAuraPreviewEnabled = false;
        overlay.IsolationWarningPreviewEnabled = false;
        overlay.HighPressureWarningPreviewEnabled = false;
        pressureCounter.PreviewEnabled = false;
    }

    private void DrawSidebar()
    {
        ImGui.TextDisabled("OVERVIEW");
        DrawPageChoice(SettingsPage.Start, "Start");
        DrawPageChoice(SettingsPage.Diagnostics, "Helper Status");
        ImGui.Spacing();
        ImGui.TextDisabled("GAMEPLAY");
        DrawPageChoice(SettingsPage.ActionHelpers, "Action Helpers");
        DrawPageChoice(SettingsPage.JobTools, "Job Tools");
        DrawPageChoice(SettingsPage.MacroHelpers, "Macro Helpers");
        DrawPageChoice(SettingsPage.PingHelpers, "Ping Helpers");
        DrawPageChoice(SettingsPage.Targets, "Targets");
        ImGui.Spacing();
        ImGui.TextDisabled("DISPLAY & HISTORY");
        DrawPageChoice(SettingsPage.Alerts, "Alerts");
        DrawPageChoice(SettingsPage.HudAndNameplates, "HUD & Nameplates");
        DrawPageChoice(SettingsPage.PlayerStats, "Player Stats");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextColored(
            configuration.Enabled
                ? new Vector4(0.4f, 0.9f, 0.62f, 1f)
                : new Vector4(0.72f, 0.74f, 0.8f, 1f),
            configuration.Enabled ? "PLUGIN ENABLED" : "PLUGIN DISABLED");
    }

    private void DrawPageChoice(SettingsPage page, string label)
    {
        if (ImGui.Selectable(label, selectedPage == page))
            selectedPage = page;
    }

    private enum SettingsPage
    {
        Start,
        Alerts,
        HudAndNameplates,
        PlayerStats,
        ActionHelpers,
        PingHelpers,
        JobTools,
        MacroHelpers,
        Targets,
        Diagnostics,
    }
}
