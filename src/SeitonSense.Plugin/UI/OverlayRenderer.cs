using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;
using SeitonSense.Plugin.Models;
using SeitonSense.Plugin.Services;

namespace SeitonSense.Plugin.UI;

internal sealed class OverlayRenderer
{
    private const long MaximumAnchorAgeMilliseconds = 250;

    private static readonly Vector4 SeitonColor = new(1f, 0.2f, 0.54f, 1f);
    private static readonly Vector4 GuardColor = new(0.25f, 0.72f, 1f, 1f);
    private static readonly Vector4 ManaColor = new(0.24f, 0.48f, 1f, 1f);
    private static readonly Vector4 CrossColor = new(1f, 0.12f, 0.12f, 1f);
    private static readonly Vector4 TextColor = new(1f, 0.98f, 1f, 1f);
    private static readonly Vector4 ShadowColor = new(0f, 0f, 0f, 0.96f);

    private readonly PluginConfiguration configuration;
    private readonly ExecuteTracker tracker;
    private readonly NamePlateAnchorTracker namePlateAnchors;
    private readonly IGameGui gameGui;
    private readonly ITextureProvider textureProvider;
    private SeitonPopupSnapshot? previewPopup;

    public OverlayRenderer(
        PluginConfiguration configuration,
        ExecuteTracker tracker,
        NamePlateAnchorTracker namePlateAnchors,
        IGameGui gameGui,
        ITextureProvider textureProvider)
    {
        this.configuration = configuration;
        this.tracker = tracker;
        this.namePlateAnchors = namePlateAnchors;
        this.gameGui = gameGui;
        this.textureProvider = textureProvider;
    }

    public bool PreviewEnabled { get; set; }
    public int NativeAnchorCount => namePlateAnchors.Anchors.Count;

    public void TriggerPreviewPopup()
    {
        var now = Environment.TickCount64;
        var duration = (long)Math.Clamp(configuration.PopupDurationMilliseconds, 300f, 2000f);
        previewPopup = new SeitonPopupSnapshot(0, 3, 30, now, now + duration);
    }

    public void Draw()
    {
        if (gameGui.GameUiHidden) return;

        if (PreviewEnabled) DrawPreview();
        DrawPopup(previewPopup);

        if (!configuration.Enabled || !tracker.IsActive) return;

        var now = Environment.TickCount64;
        DrawLiveNameplateIndicators(now);
        if (!configuration.ShowSeitonPopup) return;

        var livePopups = tracker.Popups
            .Where(popup => popup.EndsAtMilliseconds > Environment.TickCount64)
            .Take(5)
            .ToArray();
        for (var index = 0; index < livePopups.Length; index++)
        {
            DrawPopup(livePopups[index], index, livePopups.Length);
        }
    }

    private void DrawLiveNameplateIndicators(long now)
    {
        var anchors = namePlateAnchors.Anchors;
        if (anchors.Count == 0) return;

        var byObjectId = new Dictionary<ulong, NamePlateAnchorSnapshot>(anchors.Count);
        foreach (var anchor in anchors)
        {
            if (now - anchor.CapturedAtMilliseconds is < 0 or > MaximumAnchorAgeMilliseconds) continue;
            byObjectId[anchor.GameObjectId] = anchor;
        }

        foreach (var enemy in tracker.Enemies)
        {
            if (!byObjectId.TryGetValue(enemy.GameObjectId, out var anchor)) continue;
            DrawIndicatorSlots(anchor, enemy);
        }
    }

    private void DrawIndicatorSlots(NamePlateAnchorSnapshot anchor, EnemyHudSnapshot enemy)
    {
        var nativeHeight = Math.Max(1f, anchor.Height);
        var size = Math.Clamp(nativeHeight * configuration.NameplateIconScale, 12f, 48f);
        var gap = Math.Max(1f, configuration.NameplateIconSpacing * ImGuiHelpers.GlobalScale);
        var centerY = (anchor.JobIconTopLeft.Y + anchor.JobIconBottomRight.Y) * 0.5f;

        if (configuration.ShowNameplateSeiton && enemy.SeitonEligible)
        {
            var rect = IndicatorRect(anchor.JobIconTopLeft.X, centerY, size, gap, 2);
            DrawIconBadge(rect.Min, rect.Max, EnemyCombatConstants.SeitonIconId, SeitonColor, false, enemy.SlotLabel, null);
        }

        if (configuration.ShowGuardUnavailable && enemy.GuardUnavailable)
        {
            var rect = IndicatorRect(anchor.JobIconTopLeft.X, centerY, size, gap, 0);
            var countdown = configuration.ShowGuardCountdown
                ? Math.Max(1, (int)Math.Ceiling(enemy.GuardCooldownRemainingSeconds)).ToString()
                : null;
            DrawIconBadge(rect.Min, rect.Max, EnemyCombatConstants.GuardIconId, GuardColor, true, null, countdown);
        }

        if (configuration.ShowLowMp && enemy.LowMp)
        {
            var rect = IndicatorRect(anchor.JobIconTopLeft.X, centerY, size, gap, 1);
            DrawIconBadge(
                rect.Min,
                rect.Max,
                EnemyCombatConstants.StandardIssueElixirIconId,
                ManaColor,
                true,
                null,
                null);
        }
    }

    private static (Vector2 Min, Vector2 Max) IndicatorRect(
        float nativeIconLeft,
        float centerY,
        float size,
        float gap,
        int fixedSlot)
    {
        var right = nativeIconLeft - gap - (fixedSlot * (size + gap));
        var min = new Vector2(right - size, centerY - (size * 0.5f));
        return (min, min + new Vector2(size));
    }

    private void DrawIconBadge(
        Vector2 topLeft,
        Vector2 bottomRight,
        uint iconId,
        Vector4 borderColor,
        bool crossed,
        string? cornerLabel,
        string? countdown)
    {
        var draw = ImGui.GetForegroundDrawList();
        var scale = ImGuiHelpers.GlobalScale;
        var size = bottomRight.X - topLeft.X;
        var rounding = Math.Max(2f, size * 0.12f);
        var border = Math.Max(1f, size * 0.055f);

        draw.AddRectFilled(
            topLeft - new Vector2(border),
            bottomRight + new Vector2(border),
            Pack(new Vector4(0.01f, 0.015f, 0.03f, configuration.NameplateBackgroundOpacity)),
            rounding + border);

        if (!TryDrawGameIcon(draw, iconId, topLeft, bottomRight, crossed ? 0.72f : 1f))
        {
            draw.AddRectFilled(topLeft, bottomRight, Pack(new Vector4(borderColor.X * 0.22f, borderColor.Y * 0.22f, borderColor.Z * 0.22f, 1f)), rounding);
        }

        draw.AddRect(
            topLeft - new Vector2(border * 0.5f),
            bottomRight + new Vector2(border * 0.5f),
            Pack(borderColor),
            rounding + border,
            ImDrawFlags.None,
            border);

        if (crossed) DrawCross(draw, topLeft, bottomRight);
        if (!string.IsNullOrEmpty(cornerLabel))
        {
            var labelScale = Math.Clamp(size / 24f, 0.65f, 1.35f);
            DrawOutlinedText(
                draw,
                new Vector2((topLeft.X + bottomRight.X) * 0.5f, bottomRight.Y - (ImGui.GetFontSize() * labelScale)),
                cornerLabel,
                labelScale,
                true);
        }

        if (!string.IsNullOrEmpty(countdown))
        {
            var labelScale = Math.Clamp(size / 28f, 0.58f, 1.05f);
            DrawOutlinedText(
                draw,
                new Vector2((topLeft.X + bottomRight.X) * 0.5f, bottomRight.Y - (ImGui.GetFontSize() * labelScale)),
                countdown,
                labelScale,
                true);
        }
    }

    private void DrawPopup(SeitonPopupSnapshot? popup, int index = 0, int count = 1)
    {
        if (popup is null) return;

        var now = Environment.TickCount64;
        if (now < popup.StartedAtMilliseconds || now >= popup.EndsAtMilliseconds)
        {
            if (ReferenceEquals(popup, previewPopup)) previewPopup = null;
            return;
        }

        var duration = Math.Max(1L, popup.EndsAtMilliseconds - popup.StartedAtMilliseconds);
        var progress = Math.Clamp((now - popup.StartedAtMilliseconds) / (float)duration, 0f, 1f);
        var settle = MathF.Exp(-progress * 10f);
        var fade = progress < 0.78f ? 1f : Math.Clamp((1f - progress) / 0.22f, 0f, 1f);
        var uiScale = ImGuiHelpers.GlobalScale;
        var baseSize = Math.Clamp(configuration.PopupIconSize, 36f, 140f) * uiScale;
        var size = baseSize * (1f + (0.24f * settle));
        var screen = ImGui.GetIO().DisplaySize;
        var center = new Vector2(
            screen.X * Math.Clamp(configuration.PopupScreenX, 0.05f, 0.95f),
            screen.Y * Math.Clamp(configuration.PopupScreenY, 0.05f, 0.9f));
        if (count > 1)
        {
            var spacing = baseSize + (26f * uiScale);
            center.X += (index - ((count - 1) * 0.5f)) * spacing;
        }
        var topLeft = center - new Vector2(size * 0.5f);
        var bottomRight = topLeft + new Vector2(size);
        var pad = 9f * uiScale;
        var draw = ImGui.GetForegroundDrawList();

        draw.AddRectFilled(
            topLeft - new Vector2(pad),
            bottomRight + new Vector2(pad),
            Pack(new Vector4(0.025f, 0.008f, 0.035f, configuration.PopupBackgroundOpacity * fade)),
            12f * uiScale);
        draw.AddRect(
            topLeft - new Vector2(pad),
            bottomRight + new Vector2(pad),
            Pack(new Vector4(SeitonColor.X, SeitonColor.Y, SeitonColor.Z, fade)),
            12f * uiScale,
            ImDrawFlags.None,
            Math.Max(2f, 4f * uiScale));

        var jobIcon = EnemyCombatConstants.JobIconBaseId + popup.JobId;
        if (!TryDrawGameIcon(draw, jobIcon, topLeft, bottomRight, fade))
        {
            draw.AddRectFilled(topLeft, bottomRight, Pack(new Vector4(0.16f, 0.03f, 0.12f, fade)), 7f * uiScale);
        }

        var textScale = Math.Clamp(size / 54f, 1.1f, 2.2f);
        DrawOutlinedText(
            draw,
            new Vector2(center.X, bottomRight.Y - (ImGui.GetFontSize() * textScale * 0.92f)),
            popup.SlotLabel,
            textScale,
            true,
            fade);
        DrawOutlinedText(
            draw,
            new Vector2(center.X, topLeft.Y - (27f * uiScale)),
            "SEITON",
            1.05f,
            true,
            fade);
    }

    private void DrawPreview()
    {
        var screen = ImGui.GetIO().DisplaySize;
        var nativeSize = 28f * ImGuiHelpers.GlobalScale;
        var nativeMin = new Vector2(screen.X * 0.54f, screen.Y * 0.38f);
        var nativeMax = nativeMin + new Vector2(nativeSize);
        var anchor = new NamePlateAnchorSnapshot(0, nativeMin, nativeMax, Environment.TickCount64);
        var enemy = new EnemyHudSnapshot(3, 0, 0, 30, true, true, 18.4f, true, 1200, 10000);

        TryDrawGameIcon(
            ImGui.GetForegroundDrawList(),
            EnemyCombatConstants.JobIconBaseId + enemy.JobId,
            nativeMin,
            nativeMax,
            1f);
        DrawIndicatorSlots(anchor, enemy);
    }

    private bool TryDrawGameIcon(
        ImDrawListPtr draw,
        uint iconId,
        Vector2 topLeft,
        Vector2 bottomRight,
        float alpha)
    {
        if (!textureProvider.TryGetFromGameIcon(new GameIconLookup(iconId), out var shared) ||
            !shared.TryGetWrap(out var wrap, out _))
        {
            return false;
        }

        draw.AddImage(
            wrap.Handle,
            topLeft,
            bottomRight,
            Vector2.Zero,
            Vector2.One,
            Pack(new Vector4(1f, 1f, 1f, Math.Clamp(alpha, 0f, 1f))));
        return true;
    }

    private static void DrawCross(ImDrawListPtr draw, Vector2 topLeft, Vector2 bottomRight)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var inset = Math.Max(2f, (bottomRight.X - topLeft.X) * 0.12f);
        var start = topLeft + new Vector2(inset);
        var end = bottomRight - new Vector2(inset);
        draw.AddLine(start, end, Pack(ShadowColor), 5f * scale);
        draw.AddLine(start, end, Pack(CrossColor), 2.6f * scale);
    }

    private static void DrawOutlinedText(
        ImDrawListPtr draw,
        Vector2 position,
        string text,
        float textScale,
        bool centered,
        float alpha = 1f)
    {
        var size = ImGui.CalcTextSize(text) * textScale;
        var origin = centered ? new Vector2(position.X - (size.X * 0.5f), position.Y) : position;
        var offset = Math.Max(1f, ImGuiHelpers.GlobalScale);
        var font = ImGui.GetFont();
        var fontSize = ImGui.GetFontSize() * textScale;
        var shadow = new Vector4(ShadowColor.X, ShadowColor.Y, ShadowColor.Z, ShadowColor.W * alpha);
        var textColor = new Vector4(TextColor.X, TextColor.Y, TextColor.Z, TextColor.W * alpha);
        draw.AddText(font, fontSize, origin + new Vector2(-offset, 0f), Pack(shadow), text);
        draw.AddText(font, fontSize, origin + new Vector2(offset, 0f), Pack(shadow), text);
        draw.AddText(font, fontSize, origin + new Vector2(0f, -offset), Pack(shadow), text);
        draw.AddText(font, fontSize, origin + new Vector2(0f, offset), Pack(shadow), text);
        draw.AddText(font, fontSize, origin, Pack(textColor), text);
    }

    private static uint Pack(Vector4 color) => ImGui.GetColorU32(color);
}
