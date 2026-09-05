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
    private bool previewEnabled;
    private long previewStartedAt;
    internal bool PreviewAllies { get; private set; }

    internal bool PreviewEnabled
    {
        get => previewEnabled;
        set
        {
            if (previewEnabled == value) return;
            previewEnabled = value;
            previewStartedAt = value ? Environment.TickCount64 : 0;
            if (!value) PreviewAllies = false;
        }
    }

    internal void StartPreview(bool allies)
    {
        PreviewAllies = allies;
        PreviewEnabled = true;
        previewStartedAt = Environment.TickCount64;
    }

    internal void Draw()
    {
        if (!configuration.Enabled)
        {
            PreviewEnabled = false;
            return;
        }
        if ((!configuration.ShowCcAggressorArrows && !configuration.ShowCcAllyTargetArrows) ||
            !pluginInterface.UiBuilder.ShouldModifyUi || gameGui.GameUiHidden ||
            !clientState.IsLoggedIn || !clientState.IsPvPExcludingDen)
            return;

        var current = tracker.Snapshot;
        var now = Environment.TickCount64;
        if (!current.Active || !current.PressureActive ||
            current.TerritoryId != clientState.TerritoryType ||
            current.PublishedAtMilliseconds < 0 || now < current.PublishedAtMilliseconds ||
            now - current.PublishedAtMilliseconds > AggressorArrowRules.MaximumSnapshotAgeMilliseconds ||
            (current.AggressorArrows.Count == 0 && current.AllyTargetArrows.Count == 0))
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
        var localVisible = gameGui.WorldToScreen(current.LocalWorldPosition + new Vector3(0f, 1.05f, 0f),
            out var to, out var localInViewport) && localInViewport;

        var appearance = GetAppearance();
        var draw = ImGui.GetBackgroundDrawList();
        draw.PushClipRect(Vector2.Zero, viewport, true);
        try
        {
            foreach (var pulse in current.AggressorArrows)
            {
                if (!configuration.ShowCcAggressorArrows || !current.CcAggressorArrowsActive || !localVisible)
                    break;
                var alpha = AggressorArrowRules.PulseAlpha(
                    pulse.StartedAtMilliseconds, now, appearance.Duration, appearance.ReducedMotion) * appearance.Opacity;
                if (alpha <= 0f) continue;
                var opponent = current.Find(pulse.Actor.GameObjectId, pulse.Actor.EntityId);
                if (opponent is null || !opponent.IsAliveAndTargetable || !opponent.IsIncoming ||
                    !IsFinite(opponent.WorldPosition) || opponent.JobId == 0 ||
                    !gameGui.WorldToScreen(opponent.WorldPosition + new Vector3(0f, 1.05f, 0f),
                        out var from, out var enemyInViewport) || !enemyInViewport ||
                    !AggressorArrowRules.IsValidProjectedSegment(from, to, Vector2.Zero, viewport))
                    continue;

                var progress = Math.Clamp((now - pulse.StartedAtMilliseconds) / (appearance.Duration * 1000f), 0f, 1f);
                DrawArrow(draw, from, to, Vector2.Zero, viewport, opponent.JobId, progress, alpha, appearance);
            }
            foreach (var pulse in current.AllyTargetArrows)
            {
                if (!configuration.ShowCcAllyTargetArrows || !current.CcAllyTargetArrowsActive) break;
                var alpha = AggressorArrowRules.PulseAlpha(pulse.StartedAtMilliseconds, now,
                    appearance.Duration, appearance.ReducedMotion) * appearance.Opacity;
                if (alpha <= 0f) continue;
                var source = FindAlly(current.AllyArrowSources, pulse.Ally);
                var target = current.Find(pulse.Target.GameObjectId, pulse.Target.EntityId);
                if (source is not { } ally || !ally.IsAliveAndTargetable ||
                    ally.HostileTarget != pulse.Target || ally.JobId == 0 ||
                    target is null || !target.IsAliveAndTargetable ||
                    !IsFinite(ally.WorldPosition) || !IsFinite(target.WorldPosition) ||
                    !gameGui.WorldToScreen(ally.WorldPosition + new Vector3(0f, 1.05f, 0f),
                        out var from, out var allyInViewport) || !allyInViewport ||
                    !gameGui.WorldToScreen(target.WorldPosition + new Vector3(0f, 1.05f, 0f),
                        out var enemyTo, out var enemyInViewport) || !enemyInViewport)
                    continue;
                var progress = Math.Clamp((now - pulse.StartedAtMilliseconds) /
                    (appearance.Duration * 1000f), 0f, 1f);
                DrawArrow(draw, from, enemyTo, Vector2.Zero, viewport, ally.JobId,
                    progress, alpha, appearance, allyArrow: true);
            }
        }
        finally
        {
            draw.PopClipRect();
        }
    }

    /// <summary>Explicit sample canvas; it never creates live pressure actors or pulses.</summary>
    internal void DrawSettingsPreview()
    {
        if (!PreviewEnabled || !configuration.Enabled)
        {
            PreviewEnabled = false;
            return;
        }

        ImGui.TextColored(PreviewAllies ? new Vector4(.3f, .8f, 1f, 1f) : new Vector4(1f, .76f, .28f, 1f),
            PreviewAllies ? "PREVIEW - ally MCH / SCH targeting an enemy, not live" :
                "PREVIEW - enemy MCH / SCH targeting you, not live");
        ImGui.TextDisabled("Repeats automatically while these settings are open. Size changes apply immediately.");
        var appearance = GetAppearance();
        var minimum = ImGui.GetCursorScreenPos();
        var size = new Vector2(Math.Max(180f, ImGui.GetContentRegionAvail().X), 240f * appearance.UiScale);
        var maximum = minimum + size;
        var draw = ImGui.GetWindowDrawList();
        draw.AddRectFilled(minimum, maximum, Pack(0.035f, 0.035f, 0.055f, 0.95f), 6f);
        draw.PushClipRect(minimum, maximum, true);
        try
        {
            var now = Environment.TickCount64;
            if (now < previewStartedAt) previewStartedAt = now;
            var cycle = (long)(appearance.Duration * 1000f) + 450L;
            var elapsed = now - previewStartedAt;
            var target = minimum + new Vector2(size.X * 0.5f, size.Y * 0.83f);
            draw.AddCircle(target, 10f * appearance.UiScale, Pack(0.7f, 0.75f, 0.82f, 0.8f), 20, 1.5f);
            draw.AddText(target + new Vector2(14f, -7f), Pack(0.75f, 0.8f, 0.88f, 1f),
                PreviewAllies ? "ENEMY (sample)" : "YOU (sample)");
            for (var index = 0; index < 2; index++)
            {
                var age = (elapsed + cycle - (index * 200L % cycle)) % cycle;
                var alpha = AggressorArrowRules.PulseAlpha(now - age, now, appearance.Duration,
                    appearance.ReducedMotion) * appearance.Opacity;
                var source = minimum + new Vector2(size.X * (index == 0 ? 0.16f : 0.84f), size.Y * 0.38f);
                var progress = Math.Clamp(age / (appearance.Duration * 1000f), 0f, 1f);
                DrawArrow(draw, source, target, minimum, maximum, index == 0 ? 31u : 28u,
                    progress, alpha, appearance, PreviewAllies);
            }
        }
        finally
        {
            draw.PopClipRect();
        }
        ImGui.Dummy(size);
    }

    private void DrawArrow(ImDrawListPtr draw, Vector2 from, Vector2 to,
        Vector2 clipMinimum, Vector2 clipMaximum, uint jobId, float progress, float alpha,
        in ArrowAppearance appearance, bool allyArrow = false)
    {
        var scale = appearance.VisualScale;
        var iconRadius = appearance.IconSize * 0.5f + 2f * scale;
        var clipSize = clipMaximum - clipMinimum;
        if (alpha <= 0f || clipSize.X <= 2f * iconRadius + 2f || clipSize.Y <= 2f * iconRadius + 2f ||
            !AggressorArrowRules.IsValidProjectedSegment(from, to, clipMinimum, clipMaximum) ||
            !textureProvider.TryGetFromGameIcon(new GameIconLookup(62000u + jobId), out var shared) ||
            !shared.TryGetWrap(out var icon, out _)) return;

        var delta = to - from;
        var distance = delta.Length();
        var direction = delta / distance;
        var start = from + direction * Math.Min(15f * scale, distance * 0.1f);
        var end = to - direction * Math.Min(22f * scale, distance * 0.16f);
        var curveHeight = Math.Min(distance * 0.45f,
            Math.Min(62f * appearance.UiScale, distance * 0.18f) * appearance.OverallScale);
        var control = (start + end) * 0.5f - new Vector2(0f, curveHeight);
        var headT = appearance.ReducedMotion ? 0.98f : 0.48f + 0.5f * MathF.Sqrt(progress);
        DrawCurve(draw, start, control, end, headT, appearance.Width, alpha, scale, allyArrow);

        var half = new Vector2(appearance.IconSize * 0.5f);
        var iconCenter = Curve(start, control, end, 0.16f) + new Vector2(0f, -iconRadius - 2f * scale);
        var iconMargin = new Vector2(iconRadius + 1f);
        iconCenter = Vector2.Clamp(iconCenter, clipMinimum + iconMargin, clipMaximum - iconMargin);
        draw.AddCircleFilled(iconCenter, iconRadius,
            allyArrow ? Pack(.02f, .05f, .12f, alpha * .9f) : Pack(.08f, .04f, .04f, alpha * .9f), 24);
        draw.AddImage(icon.Handle, iconCenter - half, iconCenter + half,
            Vector2.Zero, Vector2.One, Pack(1f, 1f, 1f, alpha));
        draw.AddCircle(iconCenter, iconRadius,
            allyArrow ? Pack(.2f, .7f, 1f, alpha) : Pack(1f, .5f, .14f, alpha), 24, 1.5f * scale);
    }

    private ArrowAppearance GetAppearance()
    {
        var uiScale = SafeFloat(ImGuiHelpers.GlobalScale, 1f, 0.75f, 2f);
        var overall = SafeFloat(configuration.CcAggressorArrowScale, AggressorArrowRules.DefaultOverallScale,
            AggressorArrowRules.MinimumOverallScale, AggressorArrowRules.MaximumOverallScale);
        var scale = AggressorArrowRules.ResolveVisualScale(uiScale, overall);
        return new ArrowAppearance(
            SafeFloat(configuration.CcAggressorArrowDurationSeconds, AggressorArrowRules.DefaultDurationSeconds,
                AggressorArrowRules.MinimumDurationSeconds, AggressorArrowRules.MaximumDurationSeconds),
            SafeFloat(configuration.CcAggressorArrowOpacity, 0.78f, 0.15f, 1f), uiScale, overall, scale,
            SafeFloat(configuration.CcAggressorArrowThickness, 2.4f, 1f, 5f) * scale,
            SafeFloat(configuration.CcAggressorArrowJobIconSize, 28f, 20f, 44f) * scale,
            pluginInterface.UiBuilder.ShouldUseReducedMotion);
    }

    private readonly record struct ArrowAppearance(float Duration, float Opacity, float UiScale,
        float OverallScale, float VisualScale, float Width, float IconSize, bool ReducedMotion);

    private static void DrawCurve(ImDrawListPtr draw, Vector2 start, Vector2 control,
        Vector2 end, float headT, float width, float alpha, float scale, bool allyArrow)
    {
        const int segments = 18;
        var previous = start;
        for (var index = 1; index <= segments; index++)
        {
            var t = headT * index / segments;
            var next = Curve(start, control, end, t);
            var fade = alpha * (0.35f + 0.65f * index / segments);
            draw.AddLine(previous, next, allyArrow ? Pack(.08f, .35f, .95f, fade * .18f) :
                Pack(.9f, .09f, .03f, fade * .18f), width * 3.5f);
            draw.AddLine(previous, next, allyArrow ? Pack(.15f, .6f + .2f * t, 1f, fade) :
                Pack(1f, .26f + .27f * t, .08f, fade), width);
            previous = next;
        }

        var tangent = Vector2.Normalize(2f * (1f - headT) * (control - start) + 2f * headT * (end - control));
        var normal = new Vector2(-tangent.Y, tangent.X);
        var wing = Math.Max(7f * scale, width * 3.2f);
        var back = previous - tangent * wing;
        var headColor = allyArrow ? Pack(.3f, .85f, 1f, alpha) : Pack(1f, .62f, .18f, alpha);
        draw.AddLine(previous, back + normal * wing * 0.62f, headColor, width * 1.25f);
        draw.AddLine(previous, back - normal * wing * 0.62f, headColor, width * 1.25f);
    }

    private static AllyTargetArrowSourceSnapshot? FindAlly(
        IReadOnlyList<AllyTargetArrowSourceSnapshot> sources, TargetPressureActorIdentity identity)
    {
        AllyTargetArrowSourceSnapshot? match = null;
        foreach (var source in sources)
        {
            if (source.Ally != identity) continue;
            if (match is not null) return null;
            match = source;
        }
        return match;
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
