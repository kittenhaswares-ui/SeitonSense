using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using SeitonSense.Core;
using SeitonSense.Plugin.Models;
using SeitonSense.Plugin.Services;

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
    private readonly IPlayerState playerState;
    private readonly IGameGui gameGui;
    private readonly ITextureProvider textureProvider;
    private readonly CrystallineConflictMapStatisticsService mapStatistics;
    private bool resetPosition;
    private bool showRemainingMaps;
    private CrystallineConflictArena? displayedCurrentArena;
    private CrystallineConflictArena animationFromArena;
    private double animationStartedAt;

    internal WolvesDenRotationWindow(
        PluginConfiguration configuration,
        IClientState clientState,
        IPlayerState playerState,
        IGameGui gameGui,
        ITextureProvider textureProvider,
        CrystallineConflictMapStatisticsService mapStatistics)
        : base("CC Rotation###SeitonSenseWolvesDenRotation")
    {
        this.configuration = configuration;
        this.clientState = clientState;
        this.playerState = playerState;
        this.gameGui = gameGui;
        this.textureProvider = textureProvider;
        this.mapStatistics = mapStatistics;

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
        var width = 610f * uiScale;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        ImGui.SetWindowFontScale(scale * 1.08f);
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

        UpdateRotationAnimation(snapshot.CurrentArena);
        DrawRotationPanel(snapshot, width, uiScale);

        if (!ImGui.IsWindowHovered()) return;
        ImGui.BeginTooltip();
        ImGui.TextUnformatted("Local Patch 7.5 hourly map schedule");
        ImGui.TextUnformatted(
            "The current card changes automatically; when expanded, all seven cards reorder at the hourly rollover.");
        ImGui.TextUnformatted(
            mapStatistics.CaptureAvailable && mapStatistics.StorageAvailable
                ? "W/L starts counting new public CC results from this point on."
                : "New W/L capture is unavailable; no result is guessed or counted.");
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

    private void DrawRotationPanel(
        CrystallineConflictRotationSnapshot snapshot,
        float width,
        float uiScale)
    {
        DrawRotationCardDeck(snapshot, width, uiScale, showRemainingMaps);

        var toggleLabel = showRemainingMaps
            ? "HIDE NEXT 6 MAPS  [-]##SeitonSenseRotationDeckToggle"
            : "SHOW NEXT 6 MAPS  [+]##SeitonSenseRotationDeckToggle";
        if (ImGui.Button(toggleLabel, new Vector2(width, 34f * uiScale)))
            showRemainingMaps = !showRemainingMaps;

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

    private void UpdateRotationAnimation(CrystallineConflictArena currentArena)
    {
        if (displayedCurrentArena is null)
        {
            displayedCurrentArena = currentArena;
            animationFromArena = currentArena;
            return;
        }

        if (displayedCurrentArena.Value == currentArena) return;

        animationFromArena = displayedCurrentArena.Value;
        displayedCurrentArena = currentArena;
        animationStartedAt = ImGui.GetTime();
    }

    private void DrawRotationCardDeck(
        CrystallineConflictRotationSnapshot snapshot,
        float width,
        float uiScale,
        bool showFullRotation)
    {
        var cardCount = showFullRotation
            ? CrystallineConflictRotationRules.ArenaCount
            : 1;
        var cardHeight = 84f * uiScale;
        var cardGap = 7f * uiScale;
        var cardStride = cardHeight + cardGap;
        var stackHeight = (cardHeight * cardCount) + (cardGap * (cardCount - 1));
        var origin = ImGui.GetCursorScreenPos();
        var animationProgress = Math.Clamp(
            (float)((ImGui.GetTime() - animationStartedAt) /
                    CrystallineConflictRotationPresentationRules.CardReorderSeconds),
            0f,
            1f);
        var rolloverActive =
            animationFromArena != snapshot.CurrentArena &&
            animationProgress < 1f;
        var draw = ImGui.GetWindowDrawList();

        draw.PushClipRect(origin, origin + new Vector2(width, stackHeight), true);
        if (!showFullRotation && rolloverActive)
        {
            var easedProgress =
                animationProgress * animationProgress * (3f - (2f * animationProgress));
            var outgoingMinimum = origin - new Vector2(0f, cardHeight * easedProgress);
            var incomingMinimum = origin + new Vector2(0f, cardHeight * (1f - easedProgress));
            DrawRotationCard(
                draw,
                animationFromArena,
                0,
                snapshot.RemainingSeconds,
                playerState.ContentId,
                outgoingMinimum,
                outgoingMinimum + new Vector2(width, cardHeight),
                uiScale,
                rolloverOutgoing: true);
            DrawRotationCard(
                draw,
                snapshot.CurrentArena,
                0,
                snapshot.RemainingSeconds,
                playerState.ContentId,
                incomingMinimum,
                incomingMinimum + new Vector2(width, cardHeight),
                uiScale);
        }
        else
        {
            for (var targetSlot = 0; targetSlot < cardCount; targetSlot++)
            {
                var arena = CrystallineConflictRotationPresentationRules.GetArenaAtForwardSlot(
                    snapshot.CurrentArena,
                    targetSlot);
                var animatedSlot = showFullRotation && rolloverActive
                    ? CrystallineConflictRotationPresentationRules.ResolveAnimatedCardSlot(
                        animationFromArena,
                        snapshot.CurrentArena,
                        arena,
                        animationProgress)
                    : targetSlot;
                var minimum = origin + new Vector2(0f, animatedSlot * cardStride);
                var maximum = minimum + new Vector2(width, cardHeight);
                DrawRotationCard(
                    draw,
                    arena,
                    targetSlot,
                    snapshot.RemainingSeconds,
                    playerState.ContentId,
                    minimum,
                    maximum,
                    uiScale);
            }
        }

        draw.PopClipRect();
        ImGui.Dummy(new Vector2(width, stackHeight));
    }

    private void DrawRotationCard(
        ImDrawListPtr draw,
        CrystallineConflictArena arena,
        int targetSlot,
        int remainingSeconds,
        ulong localContentId,
        Vector2 minimum,
        Vector2 maximum,
        float uiScale,
        bool rolloverOutgoing = false)
    {
        var current = targetSlot == 0;
        var rounding = 5f * uiScale;
        var shadowOffset = new Vector2(0f, 2f * uiScale);
        var background = current
            ? new Vector4(0.055f, 0.13f, 0.19f, 0.98f)
            : new Vector4(0.045f, 0.055f, 0.08f, 0.94f);
        var border = current
            ? new Vector4(0.36f, 0.88f, 1f, 0.96f)
            : new Vector4(0.27f, 0.32f, 0.40f, 0.82f);

        draw.AddRectFilled(
            minimum + shadowOffset,
            maximum + shadowOffset,
            Pack(new Vector4(0f, 0f, 0f, 0.40f)),
            rounding);
        draw.AddRectFilled(minimum, maximum, Pack(background), rounding);

        var artworkPadding = 5f * uiScale;
        var artworkMinimum = minimum + new Vector2(artworkPadding, artworkPadding);
        var artworkMaximum = new Vector2(
            minimum.X + (225f * uiScale),
            maximum.Y - artworkPadding);
        if (!TryDrawArenaArtwork(draw, arena, artworkMinimum, artworkMaximum, current ? 1f : 0.78f))
        {
            draw.AddRectFilled(
                artworkMinimum,
                artworkMaximum,
                Pack(current
                    ? new Vector4(0.12f, 0.34f, 0.43f, 1f)
                    : new Vector4(0.12f, 0.15f, 0.20f, 1f)),
                3f * uiScale);
        }

        draw.AddRectFilled(
            artworkMinimum,
            artworkMaximum,
            Pack(new Vector4(0.01f, 0.02f, 0.04f, current ? 0.08f : 0.20f)),
            3f * uiScale);

        var textLeft = artworkMaximum.X + (12f * uiScale);
        var namePosition = new Vector2(textLeft, minimum.Y + (13f * uiScale));
        var sequencePosition = new Vector2(textLeft, minimum.Y + (53f * uiScale));
        var font = ImGui.GetFont();
        var nameFontSize = Math.Max(12.75f, 17f * uiScale);
        var sequenceFontSize = Math.Max(10.5f, 14f * uiScale);
        draw.AddText(
            font,
            nameFontSize,
            namePosition,
            Pack(current
                ? new Vector4(0.92f, 0.99f, 1f, 1f)
                : new Vector4(0.82f, 0.85f, 0.90f, 1f)),
            CrystallineConflictRotationRules.GetDisplayName(arena));
        draw.AddText(
            font,
            sequenceFontSize,
            sequencePosition,
            Pack(current
                ? new Vector4(0.36f, 0.88f, 1f, 1f)
                : new Vector4(0.55f, 0.61f, 0.70f, 1f)),
            rolloverOutgoing
                ? "PREVIOUS  ·  ROLLOVER"
                : targetSlot switch
                {
                    0 => $"NOW  ·  {CrystallineConflictRotationRules.FormatCountdown(remainingSeconds)}",
                    1 => "NEXT  ·  +1H",
                    _ => $"+{targetSlot}H",
                });

        var hasStatistics = mapStatistics.TryGetStatistics(
            localContentId,
            arena,
            out var statistics);
        var recordText = hasStatistics
            ? CrystallineConflictMapStatisticsRules.FormatRecord(statistics)
            : "NO DATA";
        var rateText = hasStatistics
            ? CrystallineConflictMapStatisticsRules.FormatWinRate(statistics)
            : string.Empty;
        var recordFontSize = Math.Max(11.25f, 15f * uiScale);
        var rateFontSize = Math.Max(10.25f, 13.5f * uiScale);
        var statisticsLeft = maximum.X - (136f * uiScale);
        draw.AddText(
            font,
            recordFontSize,
            new Vector2(statisticsLeft, minimum.Y + (13f * uiScale)),
            Pack(hasStatistics
                ? new Vector4(0.84f, 0.94f, 0.98f, 1f)
                : new Vector4(0.48f, 0.54f, 0.62f, 1f)),
            recordText);
        if (!string.IsNullOrEmpty(rateText))
        {
            draw.AddText(
                font,
                rateFontSize,
                new Vector2(statisticsLeft, minimum.Y + (53f * uiScale)),
                Pack(current
                    ? new Vector4(0.36f, 0.88f, 1f, 1f)
                    : new Vector4(0.55f, 0.68f, 0.76f, 1f)),
                rateText);
        }

        draw.AddRect(
            minimum,
            maximum,
            Pack(border),
            rounding,
            ImDrawFlags.None,
            current ? 1.8f * uiScale : Math.Max(1f, uiScale));
    }

    private bool TryDrawArenaArtwork(
        ImDrawListPtr draw,
        CrystallineConflictArena arena,
        Vector2 minimum,
        Vector2 maximum,
        float alpha)
    {
        var iconId = CrystallineConflictRotationPresentationRules.GetDutyArtworkIconId(arena);
        if (iconId == 0 ||
            !textureProvider.TryGetFromGameIcon(new GameIconLookup(iconId), out var shared) ||
            !shared.TryGetWrap(out var wrap, out _))
        {
            return false;
        }

        draw.AddImage(
            wrap.Handle,
            minimum,
            maximum,
            Vector2.Zero,
            Vector2.One,
            Pack(new Vector4(1f, 1f, 1f, Math.Clamp(alpha, 0f, 1f))));
        return true;
    }

    private static uint Pack(Vector4 color) => ImGui.ColorConvertFloat4ToU32(color);

    private static string FormatSignedOffset(int offset) => offset > 0 ? $"+{offset}" : offset.ToString();
}
