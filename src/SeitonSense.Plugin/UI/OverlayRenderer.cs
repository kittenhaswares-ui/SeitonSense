using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;
using SeitonSense.Core;
using SeitonSense.Plugin.Models;
using SeitonSense.Plugin.Services;

namespace SeitonSense.Plugin.UI;

internal sealed class OverlayRenderer
{
    private static readonly Vector4 Accent = new(0.92f, 0.12f, 0.38f, 1f);
    private static readonly Vector4 AccentBright = new(1f, 0.42f, 0.7f, 1f);
    private static readonly Vector4 Text = new(1f, 0.97f, 1f, 1f);
    private static readonly Vector4 Shadow = new(0f, 0f, 0f, 0.96f);

    private readonly PluginConfiguration configuration;
    private readonly ExecuteTracker tracker;
    private readonly IGameGui gameGui;
    private FlashSnapshot previewFlash = FlashSnapshot.None;

    public OverlayRenderer(
        PluginConfiguration configuration,
        ExecuteTracker tracker,
        IGameGui gameGui)
    {
        this.configuration = configuration;
        this.tracker = tracker;
        this.gameGui = gameGui;
    }

    public bool PreviewEnabled { get; set; }

    public void TriggerPreviewFlash()
    {
        var now = Environment.TickCount64;
        var duration = (long)Math.Clamp(configuration.FlashDurationMilliseconds, 200f, 1000f);
        previewFlash = new FlashSnapshot(now, now + duration, "S3");
    }

    public void Draw()
    {
        if (gameGui.GameUiHidden) return;

        if (configuration.ShowScreenFlash) DrawFlash(previewFlash);
        if (PreviewEnabled) DrawPreview();

        if (!configuration.Enabled || !tracker.IsActive) return;
        if (configuration.ShowScreenFlash) DrawFlash(tracker.Flash);
        if (!configuration.ShowOverheadLabels) return;

        foreach (var enemy in tracker.Enemies)
        {
            var worldPosition = enemy.WorldPosition + (Vector3.UnitY * configuration.WorldHeight);
            if (!gameGui.WorldToScreen(worldPosition, out var screenPosition)) continue;
            DrawSlotBadge(screenPosition, enemy.Label, enemy.HpPercent);
        }
    }

    private void DrawPreview()
    {
        var screen = ImGui.GetIO().DisplaySize;
        var center = new Vector2(screen.X * 0.5f, screen.Y * 0.38f);
        var spacing = 95f * ImGuiHelpers.GlobalScale;
        for (var slot = 1; slot <= 5; slot++)
        {
            var offset = (slot - 3) * spacing;
            DrawSlotBadge(center + new Vector2(offset, 0f), $"S{slot}", 49 - slot);
        }

    }

    private void DrawSlotBadge(Vector2 anchor, string label, int hpPercent)
    {
        var draw = ImGui.GetForegroundDrawList();
        var uiScale = ImGuiHelpers.GlobalScale;
        var textScale = Math.Clamp(configuration.LabelScale, 0.8f, 3f);
        var font = ImGui.GetFont();
        var fontSize = ImGui.GetFontSize() * textScale;
        var labelSize = ImGui.CalcTextSize(label) * textScale;
        var padding = new Vector2(10f, 5f) * uiScale;
        var topLeft = new Vector2(
            anchor.X - (labelSize.X * 0.5f) - padding.X,
            anchor.Y - labelSize.Y - (padding.Y * 2f));
        var bottomRight = new Vector2(
            anchor.X + (labelSize.X * 0.5f) + padding.X,
            anchor.Y);
        var rounding = 7f * uiScale;

        draw.AddRectFilled(
            topLeft,
            bottomRight,
            Pack(new Vector4(0.025f, 0.01f, 0.04f, configuration.BackgroundOpacity)),
            rounding);
        draw.AddRect(
            topLeft,
            bottomRight,
            Pack(Accent),
            rounding,
            ImDrawFlags.None,
            2.5f * uiScale);

        var textOrigin = new Vector2(
            anchor.X - (labelSize.X * 0.5f),
            topLeft.Y + padding.Y);
        DrawOutlinedText(draw, font, fontSize, textOrigin, label, Text);

        if (!configuration.ShowHpPercent) return;
        var hp = $"{hpPercent}%";
        var hpSize = ImGui.CalcTextSize(hp);
        var hpOrigin = new Vector2(anchor.X - (hpSize.X * 0.5f), bottomRight.Y + (2f * uiScale));
        DrawOutlinedText(draw, font, ImGui.GetFontSize(), hpOrigin, hp, AccentBright);
    }

    private void DrawFlash(FlashSnapshot state)
    {
        var now = Environment.TickCount64;
        var remaining = FlashTimeline.Remaining01(
            now,
            state.StartedAtMilliseconds,
            state.EndsAtMilliseconds);
        if (remaining <= 0f) return;

        var draw = ImGui.GetForegroundDrawList();
        var screen = ImGui.GetIO().DisplaySize;
        var uiScale = ImGuiHelpers.GlobalScale;
        var eased = remaining * remaining;
        var alpha = Math.Clamp(configuration.FlashIntensity, 0.1f, 1f) * (0.2f + (0.55f * eased));
        var inset = 8f * uiScale;
        var thickness = (4f + (7f * eased)) * uiScale;
        draw.AddRect(
            new Vector2(inset),
            screen - new Vector2(inset),
            Pack(new Vector4(Accent.X, Accent.Y, Accent.Z, alpha)),
            13f * uiScale,
            ImDrawFlags.None,
            thickness);

        if (!configuration.ShowFlashSlotText || string.IsNullOrWhiteSpace(state.SlotText)) return;
        var text = $"SEITON  {state.SlotText}";
        var font = ImGui.GetFont();
        var fontSize = ImGui.GetFontSize() * 1.45f;
        var size = ImGui.CalcTextSize(text) * 1.45f;
        var origin = new Vector2((screen.X - size.X) * 0.5f, screen.Y * 0.21f);
        DrawOutlinedText(
            draw,
            font,
            fontSize,
            origin,
            text,
            new Vector4(1f, 0.48f, 0.72f, Math.Min(1f, 0.45f + eased)));
    }

    private static void DrawOutlinedText(
        ImDrawListPtr draw,
        ImFontPtr font,
        float fontSize,
        Vector2 origin,
        string text,
        Vector4 color)
    {
        var offset = Math.Max(1f, ImGuiHelpers.GlobalScale);
        draw.AddText(font, fontSize, origin + new Vector2(-offset, 0f), Pack(Shadow), text);
        draw.AddText(font, fontSize, origin + new Vector2(offset, 0f), Pack(Shadow), text);
        draw.AddText(font, fontSize, origin + new Vector2(0f, -offset), Pack(Shadow), text);
        draw.AddText(font, fontSize, origin + new Vector2(0f, offset), Pack(Shadow), text);
        draw.AddText(font, fontSize, origin, Pack(color), text);
    }

    private static uint Pack(Vector4 color) => ImGui.GetColorU32(color);
}
