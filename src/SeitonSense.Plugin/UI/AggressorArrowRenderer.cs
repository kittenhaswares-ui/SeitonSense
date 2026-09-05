using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using SeitonSense.Core;
using SeitonSense.Plugin.Models;
using SeitonSense.Plugin.Services;

namespace SeitonSense.Plugin.UI;

/// <summary>
/// Short read-only screen-space cues from the pressure tracker's existing scan.
/// No native VFX, targeting, input window, object enumeration, or extra hooks.
/// </summary>
internal sealed class AggressorArrowRenderer(
    PluginConfiguration configuration,
    TargetPressureTracker tracker,
    IDalamudPluginInterface pluginInterface,
    IClientState clientState,
    IObjectTable objectTable,
    IGameGui gameGui,
    ITextureProvider textureProvider)
{
    internal void Draw()
    {
        if (!configuration.Enabled || !configuration.ShowCcAggressorArrows ||
            !pluginInterface.UiBuilder.ShouldModifyUi || gameGui.GameUiHidden ||
            !clientState.IsLoggedIn || !clientState.IsPvPExcludingDen)
            return;

        var current = tracker.Snapshot;
        var now = Environment.TickCount64;
        if (!current.Active || !current.PressureActive || !current.CcAggressorArrowsActive ||
            current.TerritoryId != clientState.TerritoryType ||
            current.PublishedAtMilliseconds < 0 || now < current.PublishedAtMilliseconds ||
            now - current.PublishedAtMilliseconds > AggressorArrowRules.MaximumSnapshotAgeMilliseconds ||
            current.AggressorArrows.Count == 0)
            return;

        // Validate only the local actor; all enemy positions/identities come from
        // the very same immutable pressure publication that owns the pulses.
        var local = objectTable.LocalPlayer;
        if (local is null || local.Address == nint.Zero || local.IsDead || local.CurrentHp == 0 ||
            current.LocalPlayer != new TargetPressureActorIdentity(local.GameObjectId, local.EntityId) ||
            !IsFinite(current.LocalWorldPosition))
            return;

        var viewport = ImGui.GetIO().DisplaySize;
        if (!float.IsFinite(viewport.X) || !float.IsFinite(viewport.Y) ||
            viewport.X < 64f || viewport.Y < 64f)
            return;
        if (!gameGui.WorldToScreen(current.LocalWorldPosition + new Vector3(0f, 1.05f, 0f),
                out var to, out var localInViewport) || !localInViewport)
            return;

        var duration = SafeFloat(configuration.CcAggressorArrowDurationSeconds,
            AggressorArrowRules.DefaultDurationSeconds, 0.35f, 1.5f);
        var opacity = SafeFloat(configuration.CcAggressorArrowOpacity, 0.78f, 0.15f, 1f);
        var scale = SafeFloat(ImGuiHelpers.GlobalScale, 1f, 0.75f, 2f);
        var width = SafeFloat(configuration.CcAggressorArrowThickness, 2.4f, 1f, 5f) * scale;
        var iconSize = SafeFloat(configuration.CcAggressorArrowJobIconSize, 28f, 20f, 44f) * scale;
        var iconRadius = iconSize * 0.5f + 2f * scale;
        if (viewport.X <= 2f * iconRadius + 2f || viewport.Y <= 2f * iconRadius + 2f) return;
        var reducedMotion = pluginInterface.UiBuilder.ShouldUseReducedMotion;
        var draw = ImGui.GetBackgroundDrawList();
        draw.PushClipRect(Vector2.Zero, viewport, true);
        try
        {
            foreach (var pulse in current.AggressorArrows)
            {
                var alpha = AggressorArrowRules.PulseAlpha(
                    pulse.StartedAtMilliseconds, now, duration, reducedMotion) * opacity;
                if (alpha <= 0f) continue;
                var opponent = current.Find(pulse.Actor.GameObjectId, pulse.Actor.EntityId);
                if (opponent is null || !opponent.IsAliveAndTargetable || !opponent.IsIncoming ||
                    !IsFinite(opponent.WorldPosition) || opponent.JobId == 0 ||
                    !textureProvider.TryGetFromGameIcon(new GameIconLookup(62000u + opponent.JobId), out var shared) ||
                    !shared.TryGetWrap(out var icon, out _) ||
                    !gameGui.WorldToScreen(opponent.WorldPosition + new Vector3(0f, 1.05f, 0f),
                        out var from, out var enemyInViewport) || !enemyInViewport ||
                    !AggressorArrowRules.IsValidProjectedSegment(from, to, Vector2.Zero, viewport))
                    continue;

                var progress = Math.Clamp((now - pulse.StartedAtMilliseconds) / (duration * 1000f), 0f, 1f);
                var delta = to - from;
                var distance = delta.Length();
                var direction = delta / distance;
                var start = from + direction * Math.Min(15f * scale, distance * 0.1f);
                var end = to - direction * Math.Min(22f * scale, distance * 0.16f);
                var control = (start + end) * 0.5f - new Vector2(0f, Math.Min(62f * scale, distance * 0.18f));
                var headT = reducedMotion ? 0.98f : 0.48f + 0.5f * MathF.Sqrt(progress);
                DrawCurve(draw, start, control, end, headT, width, alpha);

                // The source job is always attached to its own arrow, even if
                // the separate pressure counter is hidden or moved elsewhere.
                var half = new Vector2(iconSize * 0.5f);
                var iconCenter = Curve(start, control, end, 0.16f) + new Vector2(0f, -iconRadius - 2f * scale);
                var iconMargin = new Vector2(iconRadius + 1f);
                iconCenter = Vector2.Clamp(iconCenter, iconMargin, viewport - iconMargin);
                draw.AddCircleFilled(iconCenter, iconRadius, Pack(0.08f, 0.04f, 0.04f, alpha * 0.9f), 24);
                draw.AddImage(icon.Handle, iconCenter - half, iconCenter + half,
                    Vector2.Zero, Vector2.One, Pack(1f, 1f, 1f, alpha));
                draw.AddCircle(iconCenter, iconRadius, Pack(1f, 0.5f, 0.14f, alpha), 24, 1.5f * scale);
            }
        }
        finally
        {
            draw.PopClipRect();
        }
    }

    private static void DrawCurve(ImDrawListPtr draw, Vector2 start, Vector2 control,
        Vector2 end, float headT, float width, float alpha)
    {
        const int segments = 18;
        var previous = start;
        for (var index = 1; index <= segments; index++)
        {
            var t = headT * index / segments;
            var next = Curve(start, control, end, t);
            var fade = alpha * (0.35f + 0.65f * index / segments);
            draw.AddLine(previous, next, Pack(0.9f, 0.09f, 0.03f, fade * 0.18f), width * 3.5f);
            draw.AddLine(previous, next, Pack(1f, 0.26f + 0.27f * t, 0.08f, fade), width);
            previous = next;
        }

        var tangent = Vector2.Normalize(2f * (1f - headT) * (control - start) + 2f * headT * (end - control));
        var normal = new Vector2(-tangent.Y, tangent.X);
        var wing = Math.Max(7f, width * 3.2f);
        var back = previous - tangent * wing;
        draw.AddLine(previous, back + normal * wing * 0.62f, Pack(1f, 0.62f, 0.18f, alpha), width * 1.25f);
        draw.AddLine(previous, back - normal * wing * 0.62f, Pack(1f, 0.62f, 0.18f, alpha), width * 1.25f);
    }

    private static Vector2 Curve(Vector2 start, Vector2 control, Vector2 end, float t) =>
        (1f - t) * (1f - t) * start + 2f * (1f - t) * t * control + t * t * end;

    private static uint Pack(float red, float green, float blue, float alpha) =>
        ImGui.ColorConvertFloat4ToU32(new Vector4(red, green, blue, Math.Clamp(alpha, 0f, 1f)));

    private static bool IsFinite(Vector3 point) =>
        float.IsFinite(point.X) && float.IsFinite(point.Y) && float.IsFinite(point.Z);

    private static float SafeFloat(float value, float fallback, float minimum, float maximum) =>
        Math.Clamp(float.IsFinite(value) ? value : fallback, minimum, maximum);
}
