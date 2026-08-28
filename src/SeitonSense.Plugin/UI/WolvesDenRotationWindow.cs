using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
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
    private readonly ITextureProvider textureProvider;
    private bool resetPosition;
    private CrystallineConflictArena? displayedCurrentArena;
    private CrystallineConflictArena animationFromArena;
    private double animationStartedAt;

    internal WolvesDenRotationWindow(
        PluginConfiguration configuration,
        IClientState clientState,
        IGameGui gameGui,
        ITextureProvider textureProvider)
        : base("CC Rotation###SeitonSenseWolvesDenRotation")
    {
        this.configuration = configuration;
        this.clientState = clientState;
        this.gameGui = gameGui;
        this.textureProvider = textureProvider;

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

        UpdateRotationAnimation(snapshot.CurrentArena);

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
        ImGui.TextUnformatted("Click the current map to show the full card deck and calibration controls.");
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
        ImGui.TextDisabled("Full rotation  ·  local game artwork");
        DrawRotationCardDeck(snapshot, width, uiScale);

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
        float uiScale)
    {
        const int cardCount = CrystallineConflictRotationRules.ArenaCount;
        var cardHeight = 48f * uiScale;
        var cardGap = 5f * uiScale;
        var cardStride = cardHeight + cardGap;
        var stackHeight = (cardHeight * cardCount) + (cardGap * (cardCount - 1));
        var origin = ImGui.GetCursorScreenPos();
        var animationProgress = Math.Clamp(
            (float)((ImGui.GetTime() - animationStartedAt) /
                    CrystallineConflictRotationPresentationRules.CardReorderSeconds),
            0f,
            1f);
        var animationActive =
            animationFromArena != snapshot.CurrentArena &&
            animationProgress < 1f;
        var draw = ImGui.GetWindowDrawList();

        draw.PushClipRect(origin, origin + new Vector2(width, stackHeight), true);
        for (var targetSlot = 0; targetSlot < cardCount; targetSlot++)
        {
            var arena = CrystallineConflictRotationPresentationRules.GetArenaAtForwardSlot(
                snapshot.CurrentArena,
                targetSlot);
            var animatedSlot = animationActive
                ? CrystallineConflictRotationPresentationRules.ResolveAnimatedCardSlot(
                    animationFromArena,
                    snapshot.CurrentArena,
                    arena,
                    animationProgress)
                : targetSlot;
            var minimum = origin + new Vector2(0f, animatedSlot * cardStride);
            var maximum = minimum + new Vector2(width, cardHeight);
            DrawRotationCard(draw, arena, targetSlot, snapshot.RemainingSeconds, minimum, maximum, uiScale);
        }

        draw.PopClipRect();
        ImGui.Dummy(new Vector2(width, stackHeight));
    }

    private void DrawRotationCard(
        ImDrawListPtr draw,
        CrystallineConflictArena arena,
        int targetSlot,
        int remainingSeconds,
        Vector2 minimum,
        Vector2 maximum,
        float uiScale)
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

        var artworkPadding = 4f * uiScale;
        var artworkMinimum = minimum + new Vector2(artworkPadding, artworkPadding);
        var artworkMaximum = new Vector2(
            minimum.X + (128f * uiScale),
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

        var textLeft = artworkMaximum.X + (9f * uiScale);
        var namePosition = new Vector2(textLeft, minimum.Y + (8f * uiScale));
        var sequencePosition = new Vector2(textLeft, minimum.Y + (28f * uiScale));
        var font = ImGui.GetFont();
        var nameFontSize = Math.Max(10f, 11.5f * uiScale);
        var sequenceFontSize = Math.Max(9f, 9.5f * uiScale);
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
            targetSlot switch
            {
                0 => $"NOW  ·  {CrystallineConflictRotationRules.FormatCountdown(remainingSeconds)}",
                1 => "NEXT  ·  +1H",
                _ => $"+{targetSlot}H",
            });

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
