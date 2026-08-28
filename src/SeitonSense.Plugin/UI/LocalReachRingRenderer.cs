using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using SeitonSense.Core;
using SeitonSense.Plugin.Models;

namespace SeitonSense.Plugin.UI;

/// <summary>
/// Draws two read-only flat world rings around the local PvP actor. The rings
/// never participate in targeting or action dispatch and deliberately avoid
/// terrain raycasts and object-table scans.
/// </summary>
internal sealed class LocalReachRingRenderer
{
    private const int SegmentCount = 48;
    private const float TwoPi = MathF.PI * 2f;
    private static readonly Vector2[] UnitCircle = CreateUnitCircle();

    private readonly PluginConfiguration configuration;
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly IGameGui gameGui;

    public LocalReachRingRenderer(
        PluginConfiguration configuration,
        IDalamudPluginInterface pluginInterface,
        IClientState clientState,
        IObjectTable objectTable,
        IGameGui gameGui)
    {
        this.configuration = configuration;
        this.pluginInterface = pluginInterface;
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.gameGui = gameGui;
    }

    public void Draw()
    {
        if (!configuration.Enabled ||
            !configuration.ShowPvpRangeHelper ||
            !pluginInterface.UiBuilder.ShouldModifyUi ||
            !clientState.IsLoggedIn ||
            gameGui.GameUiHidden ||
            (!clientState.IsPvP && clientState.TerritoryType != PvPMatchRules.WolvesDenPierTerritoryId))
        {
            return;
        }

        var local = objectTable.LocalPlayer;
        if (local is null ||
            local.Address == nint.Zero ||
            !local.ClassJob.IsValid ||
            !IsFinite(local.Position) ||
            !float.IsFinite(local.HitboxRadius) ||
            local.HitboxRadius is < 0f or > 10f ||
            !PvpRangeHelperRules.TryGetWorldRadii(
                local.ClassJob.RowId,
                local.HitboxRadius,
                out var meleeRadius,
                out var maximumRadius))
        {
            return;
        }

        var viewport = ImGui.GetIO().DisplaySize;
        if (!float.IsFinite(viewport.X) ||
            !float.IsFinite(viewport.Y) ||
            viewport.X <= 1f ||
            viewport.Y <= 1f)
        {
            return;
        }

        var draw = configuration.PvpRangeHelperDrawInForeground
            ? ImGui.GetForegroundDrawList()
            : ImGui.GetBackgroundDrawList();
        var opacity = SafeFloat(configuration.PvpRangeHelperOpacity, 0.72f, 0.08f, 1f);
        var lineWidth = SafeFloat(configuration.PvpRangeHelperLineWidth, 2.2f, 0.75f, 6f);
        var center = local.Position + new Vector3(0f, 0.07f, 0f);

        draw.PushClipRect(Vector2.Zero, viewport, true);
        try
        {
            DrawRing(
                draw,
                center,
                meleeRadius,
                SanitizeColor(configuration.PvpRangeHelperMeleeColor, new Vector4(0.1f, 0.95f, 1f, 1f)),
                opacity,
                lineWidth,
                configuration.PvpRangeHelperShowLabels ? "MELEE 5y" : null,
                viewport);
            DrawRing(
                draw,
                center,
                maximumRadius,
                SanitizeColor(configuration.PvpRangeHelperMaximumColor, new Vector4(1f, 0.62f, 0.08f, 1f)),
                opacity,
                lineWidth,
                configuration.PvpRangeHelperShowLabels
                    ? $"MAX {PvpRangeHelperRules.GetMaximumActionRangeYalms(local.ClassJob.RowId):0}y"
                    : null,
                viewport);
        }
        finally
        {
            draw.PopClipRect();
        }
    }

    private void DrawRing(
        ImDrawListPtr draw,
        Vector3 center,
        float radius,
        Vector4 color,
        float opacity,
        float lineWidth,
        string? label,
        Vector2 viewport)
    {
        Span<Vector2> points = stackalloc Vector2[SegmentCount];
        Span<byte> inFront = stackalloc byte[SegmentCount];
        inFront.Clear();
        var labelPoint = Vector2.Zero;
        var hasLabelPoint = false;

        for (var index = 0; index < SegmentCount; index++)
        {
            var unit = UnitCircle[index];
            var worldPoint = center + new Vector3(unit.X * radius, 0f, unit.Y * radius);
            var projected = gameGui.WorldToScreen(worldPoint, out points[index], out var visibleInViewport);
            if (!projected || !IsFinite(points[index])) continue;

            inFront[index] = 1;
            if (visibleInViewport && (!hasLabelPoint || points[index].Y < labelPoint.Y))
            {
                labelPoint = points[index];
                hasLabelPoint = true;
            }
        }

        var viewportDiagonal = MathF.Sqrt((viewport.X * viewport.X) + (viewport.Y * viewport.Y));
        var maximumSegmentLength = MathF.Max(500f, viewportDiagonal * 0.38f);
        var maximumSegmentLengthSquared = maximumSegmentLength * maximumSegmentLength;
        for (var index = 0; index < SegmentCount; index++)
        {
            var next = (index + 1) % SegmentCount;
            if (inFront[index] == 0 || inFront[next] == 0) continue;
            if (Vector2.DistanceSquared(points[index], points[next]) > maximumSegmentLengthSquared) continue;

            DrawGlowLine(draw, points[index], points[next], color, opacity, lineWidth);
        }

        if (!string.IsNullOrWhiteSpace(label) && hasLabelPoint)
            DrawLabel(draw, labelPoint, label, color, opacity);
    }

    private static void DrawGlowLine(
        ImDrawListPtr draw,
        Vector2 start,
        Vector2 end,
        Vector4 color,
        float opacity,
        float lineWidth)
    {
        draw.AddLine(start, end, Pack(color, opacity * 0.12f), lineWidth + 8f);
        draw.AddLine(start, end, Pack(color, opacity * 0.34f), lineWidth + 3.5f);
        draw.AddLine(start, end, Pack(color, opacity), lineWidth);
        draw.AddLine(start, end, Pack(new Vector4(1f, 1f, 1f, 1f), opacity * 0.72f),
            Math.Max(0.6f, lineWidth * 0.32f));
    }

    private static void DrawLabel(
        ImDrawListPtr draw,
        Vector2 anchor,
        string label,
        Vector4 color,
        float opacity)
    {
        var size = ImGui.CalcTextSize(label);
        var position = anchor - new Vector2(size.X * 0.5f, size.Y + 5f);
        draw.AddText(position + Vector2.One, Pack(new Vector4(0f, 0f, 0f, 1f), opacity), label);
        draw.AddText(position, Pack(color, opacity), label);
    }

    private static Vector2[] CreateUnitCircle()
    {
        var points = new Vector2[SegmentCount];
        for (var index = 0; index < SegmentCount; index++)
        {
            var angle = index * TwoPi / SegmentCount;
            points[index] = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        }

        return points;
    }

    private static bool IsFinite(Vector2 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y);

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private static float SafeFloat(float value, float fallback, float minimum, float maximum) =>
        float.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : fallback;

    private static Vector4 SanitizeColor(Vector4 color, Vector4 fallback) =>
        float.IsFinite(color.X) &&
        float.IsFinite(color.Y) &&
        float.IsFinite(color.Z) &&
        float.IsFinite(color.W)
            ? Vector4.Clamp(color, Vector4.Zero, Vector4.One)
            : fallback;

    private static uint Pack(Vector4 color, float alpha) =>
        ImGui.ColorConvertFloat4ToU32(new Vector4(
            color.X,
            color.Y,
            color.Z,
            Math.Clamp(alpha * color.W, 0f, 1f)));
}
