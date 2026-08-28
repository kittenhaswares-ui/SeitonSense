using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using SeitonSense.Core;
using SeitonSense.Plugin.Models;

namespace SeitonSense.Plugin.UI;

/// <summary>
/// Offline display of the published Crystalline Conflict map cycle. This
/// window deliberately has no queue, roster, HTTP, or IPC surface.
/// </summary>
internal sealed class WolvesDenRotationWindow : Window
{
    private const ImGuiWindowFlags BaseFlags =
        ImGuiWindowFlags.NoTitleBar |
        ImGuiWindowFlags.NoScrollbar |
        ImGuiWindowFlags.NoScrollWithMouse |
        ImGuiWindowFlags.AlwaysAutoResize |
        ImGuiWindowFlags.NoFocusOnAppearing |
        ImGuiWindowFlags.NoNav |
        ImGuiWindowFlags.NoDocking;

    private readonly PluginConfiguration configuration;
    private readonly IClientState clientState;
    private readonly IGameGui gameGui;
    private bool resetPosition;

    internal WolvesDenRotationWindow(
        PluginConfiguration configuration,
        IClientState clientState,
        IGameGui gameGui)
        : base("CC Rotation###SeitonSenseWolvesDenRotation")
    {
        this.configuration = configuration;
        this.clientState = clientState;
        this.gameGui = gameGui;

        IsOpen = true;
        RespectCloseHotkey = false;
        DisableWindowSounds = true;
        Flags = BaseFlags;
        Position = new Vector2(70f, 300f);
        PositionCondition = ImGuiCond.FirstUseEver;
    }

    public override bool DrawConditions() =>
        configuration.Enabled &&
        configuration.ShowWolvesDenRotationPanel &&
        !gameGui.GameUiHidden &&
        CrystallineConflictRotationRules.IsExactWolvesDenContext(
            clientState.IsPvP,
            clientState.IsPvPExcludingDen,
            clientState.TerritoryType);

    public override void PreDraw()
    {
        var flags = BaseFlags;
        if (configuration.WolvesDenRotationPanelLocked) flags |= ImGuiWindowFlags.NoMove;
        if (!configuration.WolvesDenRotationPanelShowBackground) flags |= ImGuiWindowFlags.NoBackground;
        Flags = flags;
        BgAlpha = configuration.WolvesDenRotationPanelShowBackground
            ? Math.Clamp(configuration.WolvesDenRotationPanelBackgroundOpacity, 0f, 1f)
            : 0f;

        var globalScale = Math.Max(0.5f, ImGuiHelpers.GlobalScale);
        var panelScale = Math.Clamp(configuration.WolvesDenRotationPanelScale, 0.75f, 1.75f);
        var uiScale = globalScale * panelScale;
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(11f, 9f) * uiScale);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(7f, 5f) * uiScale);

        if (!resetPosition) return;
        ImGui.SetNextWindowPos(new Vector2(70f, 300f), ImGuiCond.Always);
        resetPosition = false;
    }

    public override void PostDraw() => ImGui.PopStyleVar(2);

    public override void Draw()
    {
        var scale = Math.Clamp(configuration.WolvesDenRotationPanelScale, 0.75f, 1.75f);
        var globalScale = Math.Max(0.5f, ImGuiHelpers.GlobalScale);
        var uiScale = scale * globalScale;
        var width = 320f * uiScale;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        ImGui.SetWindowFontScale(scale);
        ImGui.TextColored(new Vector4(0.36f, 0.88f, 1f, 1f), "CC MAP ROTATION");
        ImGui.SameLine();
        ImGui.TextDisabled("LOCAL / OFFLINE");
        ImGui.SameLine();
        DrawLockButton();

        if (!CrystallineConflictRotationRules.TryResolve(
                clientState.IsPvP,
                clientState.IsPvPExcludingDen,
                clientState.TerritoryType,
                now,
                out var snapshot,
                configuration.WolvesDenRotationOffsetSlots))
        {
            ImGui.TextColored(new Vector4(1f, 0.42f, 0.35f, 1f), "Rotation unavailable");
            ImGui.TextDisabled("No map is guessed outside the supported local schedule.");
            return;
        }

        var currentName = CrystallineConflictRotationRules.GetDisplayName(snapshot.CurrentArena);
        var countdown = CrystallineConflictRotationRules.FormatCountdown(snapshot.RemainingSeconds);
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.07f, 0.10f, 0.16f, 0.98f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.10f, 0.18f, 0.27f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.08f, 0.14f, 0.22f, 1f));
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0.36f, 0.88f, 1f, 0.92f));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1.5f * uiScale);
        var toggleExpanded = ImGui.Button(
            $"CURRENT  ·  {currentName}\n{countdown} remaining##SeitonSenseRotationCurrent",
            new Vector2(width, 48f * uiScale));
        ImGui.PopStyleVar();
        ImGui.PopStyleColor(4);

        if (toggleExpanded)
        {
            configuration.WolvesDenRotationPanelExpanded =
                !configuration.WolvesDenRotationPanelExpanded;
            configuration.Save();
        }

        var elapsedFraction = Math.Clamp(
            1f - (snapshot.RemainingSeconds / (float)CrystallineConflictRotationRules.RotationSeconds),
            0f,
            1f);
        ImGui.ProgressBar(elapsedFraction, new Vector2(width, 8f * uiScale), string.Empty);
        ImGui.TextUnformatted(
            $"NEXT  ·  {CrystallineConflictRotationRules.GetDisplayName(snapshot.NextArena)}");

        if (configuration.WolvesDenRotationPanelExpanded)
            DrawExpandedRotation(snapshot, width, uiScale);

        if (!ImGui.IsWindowHovered()) return;
        ImGui.BeginTooltip();
        ImGui.TextUnformatted("Local Patch 7.5 hourly map schedule");
        ImGui.TextUnformatted("Click the current map to show calibration controls.");
        ImGui.TextUnformatted(configuration.WolvesDenRotationPanelLocked
            ? "Position locked; the panel controls remain clickable."
            : "Drag the panel to move it.");
        ImGui.TextUnformatted("Visible only in Wolves' Den Pier (territory 250).");
        ImGui.EndTooltip();
    }

    internal void ResetWindowPosition() => resetPosition = true;

    private void DrawLockButton()
    {
        var label = configuration.WolvesDenRotationPanelLocked ? "LOCKED" : "MOVE";
        if (!ImGui.SmallButton($"{label}##SeitonSenseRotationLock")) return;

        configuration.WolvesDenRotationPanelLocked =
            !configuration.WolvesDenRotationPanelLocked;
        configuration.Save();
    }

    private void DrawExpandedRotation(
        CrystallineConflictRotationSnapshot snapshot,
        float width,
        float uiScale)
    {
        ImGui.Separator();
        ImGui.TextDisabled("Published order");
        for (var index = 0; index < CrystallineConflictRotationRules.ArenaCount; index++)
        {
            var arena = CrystallineConflictRotationRules.GetArena(index);
            var prefix = arena == snapshot.CurrentArena ? "> " : "  ";
            if (arena == snapshot.CurrentArena)
                ImGui.TextColored(
                    new Vector4(0.36f, 0.88f, 1f, 1f),
                    $"{prefix}{CrystallineConflictRotationRules.GetDisplayName(arena)}");
            else
                ImGui.TextUnformatted($"{prefix}{CrystallineConflictRotationRules.GetDisplayName(arena)}");
        }

        ImGui.Separator();
        ImGui.TextDisabled("Local phase calibration");
        var buttonWidth = 42f * uiScale;
        if (ImGui.Button("<##SeitonSenseRotationPrevious", new Vector2(buttonWidth, 0f)))
            SetPhaseOffset(configuration.WolvesDenRotationOffsetSlots - 1);
        ImGui.SameLine();
        ImGui.TextUnformatted(
            configuration.WolvesDenRotationOffsetSlots == 0
                ? "Default reference phase"
                : $"Correction {FormatSignedOffset(configuration.WolvesDenRotationOffsetSlots)} map(s)");
        ImGui.SameLine();
        if (ImGui.Button(">##SeitonSenseRotationNext", new Vector2(buttonWidth, 0f)))
            SetPhaseOffset(configuration.WolvesDenRotationOffsetSlots + 1);

        if (configuration.WolvesDenRotationOffsetSlots != 0 &&
            ImGui.Button("Reset to default phase##SeitonSenseRotationResetPhase", new Vector2(width, 0f)))
        {
            SetPhaseOffset(0);
        }

        ImGui.TextDisabled("Use < / > only if the in-game map differs; the correction is saved locally.");
    }

    private void SetPhaseOffset(int offset)
    {
        var normalized = offset % CrystallineConflictRotationRules.ArenaCount;
        var halfCycle = CrystallineConflictRotationRules.ArenaCount / 2;
        if (normalized > halfCycle) normalized -= CrystallineConflictRotationRules.ArenaCount;
        if (normalized < -halfCycle) normalized += CrystallineConflictRotationRules.ArenaCount;
        configuration.WolvesDenRotationOffsetSlots = normalized;
        configuration.Save();
    }

    private static string FormatSignedOffset(int offset) => offset > 0 ? $"+{offset}" : offset.ToString();
}
