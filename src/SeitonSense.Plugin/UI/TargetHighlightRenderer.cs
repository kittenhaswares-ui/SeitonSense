using System.Diagnostics;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using SeitonSense.Core;
using SeitonSense.Plugin.Models;
using SeitonSense.Plugin.Services;

namespace SeitonSense.Plugin.UI;

/// <summary>
/// Draws read-only world highlights for the focus target and current hard target.
/// Target information is deliberately rendered only in a fixed HUD card; this class
/// never attaches anything to a nameplate, native job icon, or native health bar.
/// </summary>
internal sealed class TargetHighlightRenderer
{
    private const int GroundSegments = 48;
    private const float TwoPi = MathF.PI * 2f;

    private readonly PluginConfiguration configuration;
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly ITargetManager targetManager;
    private readonly IGameGui gameGui;
    private readonly ITextureProvider textureProvider;
    private readonly ExecuteTracker tracker;
    private readonly TargetPressureTracker pressureTracker;

    public TargetHighlightRenderer(
        PluginConfiguration configuration,
        IDalamudPluginInterface pluginInterface,
        IClientState clientState,
        IObjectTable objectTable,
        ITargetManager targetManager,
        IGameGui gameGui,
        ITextureProvider textureProvider,
        ExecuteTracker tracker,
        TargetPressureTracker pressureTracker)
    {
        this.configuration = configuration;
        this.pluginInterface = pluginInterface;
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.targetManager = targetManager;
        this.gameGui = gameGui;
        this.textureProvider = textureProvider;
        this.tracker = tracker;
        this.pressureTracker = pressureTracker;
    }

    public void Draw()
    {
        if (!configuration.Enabled ||
            !pluginInterface.UiBuilder.ShouldModifyUi ||
            !clientState.IsLoggedIn)
        {
            return;
        }

        var gameUiHidden = gameGui.GameUiHidden;
        var includeFocus = configuration.EnableFocusGlow &&
                           (!configuration.FocusHideWithGameUi || !gameUiHidden);
        var includeCurrent = (configuration.EnableCurrentTargetHighlight ||
                              configuration.ShowCurrentTargetInfoHud) &&
                             !gameUiHidden &&
                             (!configuration.CurrentTargetPvPOnly || clientState.IsPvP);
        if (!includeFocus && !includeCurrent) return;

        // The fixed card may observe focus identity solely to label a shared target
        // truthfully. It does not authorize focus geometry unless the focus module is on.
        var observeFocusIdentity = includeFocus ||
                                   (includeCurrent && configuration.ShowCurrentTargetInfoHud);

        // These wrappers are intentionally acquired only for enabled modules and remain
        // local to this draw call. Targets may disappear on death, zoning, respawn, or
        // object-table churn and must never be retained.
        var focusTarget = observeFocusIdentity ? ValidOrNull(targetManager.FocusTarget) : null;
        var currentTarget = includeCurrent ? ValidOrNull(targetManager.Target) : null;
        var plan = TargetHighlightRules.BuildPlan(new TargetHighlightObservation(
            IsLoggedIn: true,
            IsPvP: clientState.IsPvP,
            IncludeCurrentTarget: includeCurrent,
            CurrentTargetPvPOnly: configuration.CurrentTargetPvPOnly,
            CurrentTarget: CreateCandidate(currentTarget),
            IncludeFocusTarget: observeFocusIdentity,
            FocusTargetPvPOnly: false,
            FocusTarget: CreateCandidate(focusTarget)));
        if (plan.Length == 0) return;

        var time = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
        foreach (var item in plan)
        {
            switch (item.Relation)
            {
                case TargetHighlightRelation.Current when currentTarget is not null:
                    if (configuration.EnableCurrentTargetHighlight)
                    {
                        DrawWorldHighlight(
                            currentTarget,
                            CurrentTargetStyle(),
                            configuration.CurrentTargetShowLabel ? "TARGET" : null,
                            time);
                    }

                    if (configuration.ShowCurrentTargetInfoHud)
                        DrawCurrentTargetInfoHud(currentTarget, item);
                    break;

                case TargetHighlightRelation.Focus when focusTarget is not null:
                    if (includeFocus)
                    {
                        DrawWorldHighlight(
                            focusTarget,
                            FocusStyle(),
                            configuration.FocusShowLabel ? "FOCUS" : null,
                            time);
                    }
                    break;

                case TargetHighlightRelation.CurrentAndFocus:
                {
                    var combinedTarget = focusTarget ?? currentTarget;
                    if (combinedTarget is null) break;
                    if (includeFocus)
                    {
                        var label = configuration.FocusShowLabel ||
                                    (configuration.EnableCurrentTargetHighlight &&
                                     configuration.CurrentTargetShowLabel)
                            ? "FOCUS + TARGET"
                            : null;
                        DrawWorldHighlight(combinedTarget, FocusStyle(), label, time);
                    }
                    else if (configuration.EnableCurrentTargetHighlight && currentTarget is not null)
                    {
                        DrawWorldHighlight(
                            currentTarget,
                            CurrentTargetStyle(),
                            configuration.CurrentTargetShowLabel ? "TARGET" : null,
                            time);
                    }

                    if (currentTarget is not null && configuration.ShowCurrentTargetInfoHud)
                        DrawCurrentTargetInfoHud(currentTarget, item);
                    break;
                }
            }
        }
    }

    private void DrawWorldHighlight(
        IGameObject target,
        HighlightStyle style,
        string? label,
        double time)
    {
        var modelHeight = Math.Clamp(1.55f + (target.HitboxRadius * 0.35f), 1.55f, 8f);
        var targetCenterWorld = target.Position +
                                new Vector3(0f, (modelHeight * 0.55f) + style.VerticalOffset, 0f);
        if (!gameGui.WorldToScreen(targetCenterWorld, out var targetCenterScreen)) return;

        var reducedMotion = style.ReducedMotion || pluginInterface.UiBuilder.ShouldUseReducedMotion;
        var pulseSpeed = SafeFloat(style.PulseSpeed, 0.5f, 0f, 4f);
        var pulseAmount = SafeFloat(style.PulseAmount, 0f, 0f, 0.75f);
        var wave = reducedMotion ? 0f : MathF.Sin((float)(time * pulseSpeed * TwoPi));
        var pulse = reducedMotion ? 1f : (wave + 1f) * 0.5f;
        var pulseScale = 1f + (wave * pulseAmount);
        var brightness = 0.72f + (pulse * 0.28f);
        var color = GetAnimatedColor(style, time, reducedMotion);
        var drawList = style.DrawInForeground
            ? ImGui.GetForegroundDrawList()
            : ImGui.GetBackgroundDrawList();

        if (style.ShowGroundRing)
            DrawGroundRing(drawList, target, style, color, brightness, pulseScale);

        var globalScale = Math.Max(0.5f, ImGuiHelpers.GlobalScale);
        var auraRadius = SafeFloat(style.AuraRadius, 56f, 24f, 160f);
        var sizeScale = SafeFloat(style.SizeScale, 1f, 0.5f, 2.5f);
        var haloRadius = Math.Clamp(auraRadius * sizeScale * pulseScale * globalScale, 18f, 300f);

        if (style.ShowTargetHalo)
        {
            DrawHalo(
                drawList,
                targetCenterScreen,
                haloRadius,
                style,
                color,
                brightness,
                time,
                reducedMotion,
                globalScale);
        }

        if (style.ShowChevron)
            DrawChevrons(drawList, targetCenterScreen, haloRadius, style, color, brightness, globalScale);

        if (!string.IsNullOrWhiteSpace(label))
            DrawStatusLabel(drawList, targetCenterScreen, haloRadius, label, style, color, brightness, globalScale);
    }

    private void DrawGroundRing(
        ImDrawListPtr drawList,
        IGameObject target,
        HighlightStyle style,
        Vector4 color,
        float brightness,
        float pulseScale)
    {
        Span<Vector2> points = stackalloc Vector2[GroundSegments];
        Span<byte> visible = stackalloc byte[GroundSegments];

        var hitbox = Math.Clamp(target.HitboxRadius, 0.45f, 30f);
        var padding = SafeFloat(style.GroundPadding, 0.75f, 0f, 5f);
        var sizeScale = SafeFloat(style.SizeScale, 1f, 0.5f, 2.5f);
        var radius = Math.Clamp((hitbox + padding) * sizeScale * pulseScale, 0.6f, 40f);
        var center = target.Position + new Vector3(0f, 0.05f, 0f);

        for (var index = 0; index < GroundSegments; index++)
        {
            var angle = index * TwoPi / GroundSegments;
            var worldPoint = center + new Vector3(
                MathF.Cos(angle) * radius,
                0f,
                MathF.Sin(angle) * radius);
            visible[index] = gameGui.WorldToScreen(worldPoint, out points[index]) ? (byte)1 : (byte)0;
        }

        var scale = Math.Max(0.5f, ImGuiHelpers.GlobalScale);
        for (var index = 0; index < GroundSegments; index++)
        {
            var next = (index + 1) % GroundSegments;
            if (visible[index] == 0 || visible[next] == 0) continue;

            // Projection may clip adjacent samples differently near the camera edge.
            if (Vector2.DistanceSquared(points[index], points[next]) > 250_000f) continue;
            DrawGlowLine(drawList, points[index], points[next], style, color, brightness, 2.6f * scale);
        }
    }

    private static void DrawHalo(
        ImDrawListPtr drawList,
        Vector2 center,
        float radius,
        HighlightStyle style,
        Vector4 color,
        float brightness,
        double time,
        bool reducedMotion,
        float globalScale)
    {
        var intensity = SafeFloat(style.Intensity, 1f, 0.25f, 3f);

        drawList.AddCircleFilled(center, radius * 0.72f, PackColor(color, 0.018f * intensity * brightness), 64);
        drawList.AddCircle(center, radius, PackColor(color, 0.075f * intensity * brightness), 64, 24f * intensity * globalScale);
        drawList.AddCircle(center, radius, PackColor(color, 0.18f * intensity * brightness), 64, 11f * intensity * globalScale);
        drawList.AddCircle(center, radius, PackColor(color, 0.92f * brightness), 64, 3.2f * globalScale);
        drawList.AddCircle(
            center,
            radius,
            PackColor(new Vector4(1f, 1f, 1f, color.W), 0.92f * brightness),
            64,
            1.15f * globalScale);

        if (!style.ShowRays) return;

        var rotation = reducedMotion ? 0f : (float)(time * 0.55);
        const int rayCount = 12;
        for (var index = 0; index < rayCount; index++)
        {
            var angle = rotation + (index * TwoPi / rayCount);
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var inner = center + (direction * (radius + (6f * globalScale)));
            var outer = center + (direction * (radius + (18f * globalScale)));
            DrawGlowLine(drawList, inner, outer, style, color, brightness * 0.86f, 2f * globalScale);
        }
    }

    private static void DrawChevrons(
        ImDrawListPtr drawList,
        Vector2 targetCenter,
        float haloRadius,
        HighlightStyle style,
        Vector4 color,
        float brightness,
        float globalScale)
    {
        var anchor = targetCenter - new Vector2(0f, haloRadius + (26f * globalScale));
        var sizeScale = SafeFloat(style.SizeScale, 1f, 0.5f, 2.5f);
        var width = 22f * sizeScale * globalScale;
        var height = 15f * sizeScale * globalScale;

        for (var index = 0; index < 2; index++)
        {
            var offset = new Vector2(0f, index * -15f * globalScale);
            var tip = anchor + offset;
            var left = tip + new Vector2(-width, -height);
            var right = tip + new Vector2(width, -height);
            DrawGlowLine(drawList, left, tip, style, color, brightness, 3f * globalScale);
            DrawGlowLine(drawList, tip, right, style, color, brightness, 3f * globalScale);
        }
    }

    private static void DrawStatusLabel(
        ImDrawListPtr drawList,
        Vector2 targetCenter,
        float haloRadius,
        string label,
        HighlightStyle style,
        Vector4 color,
        float brightness,
        float globalScale)
    {
        var textSize = ImGui.CalcTextSize(label);
        var position = targetCenter - new Vector2(textSize.X * 0.5f, haloRadius + (77f * globalScale));
        var intensity = SafeFloat(style.Intensity, 1f, 0.25f, 3f);
        var glowColor = PackColor(color, 0.32f * intensity * brightness);
        var shadowColor = PackColor(new Vector4(0f, 0f, 0f, color.W), 0.96f);
        var coreColor = PackColor(new Vector4(1f, 1f, 1f, color.W), 1f);
        var glowOffset = 3f * globalScale;

        drawList.AddText(position + new Vector2(-glowOffset, 0f), glowColor, label);
        drawList.AddText(position + new Vector2(glowOffset, 0f), glowColor, label);
        drawList.AddText(position + new Vector2(0f, -glowOffset), glowColor, label);
        drawList.AddText(position + new Vector2(0f, glowOffset), glowColor, label);
        drawList.AddText(position + (new Vector2(1f) * globalScale), shadowColor, label);
        drawList.AddText(position, coreColor, label);
    }

    private static void DrawGlowLine(
        ImDrawListPtr drawList,
        Vector2 start,
        Vector2 end,
        HighlightStyle style,
        Vector4 color,
        float brightness,
        float coreWidth)
    {
        var intensity = SafeFloat(style.Intensity, 1f, 0.25f, 3f);
        var globalScale = Math.Max(0.5f, ImGuiHelpers.GlobalScale);

        drawList.AddLine(start, end, PackColor(color, 0.075f * intensity * brightness), coreWidth + (19f * intensity * globalScale));
        drawList.AddLine(start, end, PackColor(color, 0.20f * intensity * brightness), coreWidth + (8f * intensity * globalScale));
        drawList.AddLine(start, end, PackColor(color, 0.94f * brightness), coreWidth);
        drawList.AddLine(
            start,
            end,
            PackColor(new Vector4(1f, 1f, 1f, color.W), 0.95f * brightness),
            Math.Max(1f, coreWidth * 0.38f));
    }

    private void DrawCurrentTargetInfoHud(IGameObject target, TargetHighlightPlanItem item)
    {
        var uiScale = Math.Max(0.5f, ImGuiHelpers.GlobalScale);
        var configuredScale = SafeFloat(configuration.CurrentTargetInfoScale, 1f, 0.55f, 1.8f);
        var scale = configuredScale * uiScale;
        var screen = ImGui.GetIO().DisplaySize;
        var cardSize = new Vector2(360f, 112f) * scale;
        var desiredCenter = new Vector2(
            screen.X * SafeFloat(configuration.CurrentTargetInfoScreenX, 0.5f, 0.02f, 0.98f),
            screen.Y * SafeFloat(configuration.CurrentTargetInfoScreenY, 0.7f, 0.02f, 0.98f));
        var padding = 8f * uiScale;
        var topLeft = desiredCenter - (cardSize * 0.5f);
        topLeft.X = Math.Clamp(topLeft.X, padding, Math.Max(padding, screen.X - cardSize.X - padding));
        topLeft.Y = Math.Clamp(topLeft.Y, padding, Math.Max(padding, screen.Y - cardSize.Y - padding));
        var bottomRight = topLeft + cardSize;
        var draw = configuration.CurrentTargetDrawInForeground
            ? ImGui.GetForegroundDrawList()
            : ImGui.GetBackgroundDrawList();
        var accent = SanitizeColor(configuration.CurrentTargetGlowColor, new Vector4(0.05f, 0.9f, 1f, 1f));
        var rounding = 10f * scale;

        draw.AddRectFilled(topLeft, bottomRight, PackColor(new Vector4(0.008f, 0.012f, 0.025f, 1f), 0.9f), rounding);
        draw.AddRectFilled(topLeft, new Vector2(topLeft.X + (6f * scale), bottomRight.Y), PackColor(accent, 0.95f), rounding);
        draw.AddRect(topLeft, bottomRight, PackColor(accent, 0.9f), rounding, ImDrawFlags.None, Math.Max(1.5f, 2f * scale));

        const float BaseIconSize = 66f;
        var iconSize = BaseIconSize * scale;
        var iconMin = topLeft + new Vector2(17f, 23f) * scale;
        var iconMax = iconMin + new Vector2(iconSize);
        var character = target as ICharacter;
        var jobId = item.JobId;
        if (jobId == 0 ||
            !TryDrawGameIcon(draw, EnemyCombatConstants.JobIconBaseId + jobId, iconMin, iconMax, 1f))
        {
            draw.AddCircleFilled((iconMin + iconMax) * 0.5f, iconSize * 0.38f, PackColor(accent, 0.25f), 32);
            draw.AddCircle((iconMin + iconMax) * 0.5f, iconSize * 0.38f, PackColor(accent, 0.9f), 32, 2f * scale);
        }

        var textLeft = iconMax.X + (14f * scale);
        var header = item.Relation == TargetHighlightRelation.CurrentAndFocus
            ? "CURRENT TARGET + FOCUS"
            : "CURRENT TARGET";
        if (!string.IsNullOrEmpty(item.EnemySlotLabel)) header += $"  •  {item.EnemySlotLabel}";
        DrawHudText(
            draw,
            new Vector2(textLeft, topLeft.Y + (12f * scale)),
            header,
            0.74f * configuredScale,
            accent);

        var hpLabel = string.IsNullOrEmpty(item.HpLabel) ? "TARGET LOCKED" : $"HP {item.HpLabel}";
        DrawHudText(
            draw,
            new Vector2(textLeft, topLeft.Y + (35f * scale)),
            hpLabel,
            1.03f * configuredScale,
            new Vector4(1f, 0.98f, 1f, 1f));

        var pressure = pressureTracker.Snapshot.Find(target.GameObjectId, target.EntityId);
        var details = BuildTargetDetails(item, character, pressure);
        var detailsScale = FitHudTextScale(
            details,
            0.78f * configuredScale,
            Math.Max(1f, bottomRight.X - textLeft - (14f * scale)));
        DrawHudText(
            draw,
            new Vector2(textLeft, topLeft.Y + (64f * scale)),
            details,
            detailsScale,
            new Vector4(0.82f, 0.88f, 0.98f, 1f));

        if (character is { MaxHp: > 0 } && item.HpPercent is not null)
        {
            var fraction = Math.Clamp(character.CurrentHp / (float)character.MaxHp, 0f, 1f);
            var barMin = new Vector2(textLeft, bottomRight.Y - (20f * scale));
            var barMax = new Vector2(bottomRight.X - (14f * scale), bottomRight.Y - (10f * scale));
            draw.AddRectFilled(barMin, barMax, PackColor(new Vector4(0.04f, 0.055f, 0.09f, 1f), 1f), 3f * scale);
            draw.AddRectFilled(
                barMin,
                new Vector2(barMin.X + ((barMax.X - barMin.X) * fraction), barMax.Y),
                PackColor(accent, 0.9f),
                3f * scale);
        }
    }

    private static string BuildTargetDetails(
        TargetHighlightPlanItem item,
        ICharacter? character,
        TargetPressureOpponentSnapshot? pressure)
    {
        var details = new List<string>(6);
        if (character is { MaxHp: > 0 } && item.HpPercent is not null)
            details.Add($"{character.CurrentHp:N0}/{character.MaxHp:N0}");

        if (!string.IsNullOrEmpty(item.DistanceLabel)) details.Add(item.DistanceLabel);
        if (pressure is { TeamTargetCount: > 0 })
            details.Add($"P{pressure.TeamTargetCount} TEAM");
        if (pressure?.HasDirectIncomingIntent == true)
            details.Add("TARGETING YOU");
        else if (pressure?.IsIncoming == true)
            details.Add("RECENT PRESSURE");

        return details.Count > 0 ? string.Join("  •  ", details) : "READ-ONLY TARGET HIGHLIGHT";
    }

    private static void DrawHudText(
        ImDrawListPtr draw,
        Vector2 position,
        string text,
        float fontScale,
        Vector4 color)
    {
        var font = ImGui.GetFont();
        var fontSize = ImGui.GetFontSize() * Math.Max(0.35f, fontScale);
        var shadowOffset = Math.Max(1f, ImGuiHelpers.GlobalScale);
        draw.AddText(font, fontSize, position + new Vector2(shadowOffset), PackColor(new Vector4(0f, 0f, 0f, 1f), 0.98f), text);
        draw.AddText(font, fontSize, position, PackColor(color, 1f), text);
    }

    private static float FitHudTextScale(string text, float desiredScale, float maximumWidth)
    {
        var width = ImGui.CalcTextSize(text).X;
        if (width <= 0f) return desiredScale;
        var minimumScale = Math.Min(0.35f, desiredScale);
        return Math.Clamp(Math.Min(desiredScale, maximumWidth / width), minimumScale, desiredScale);
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
            PackColor(new Vector4(1f, 1f, 1f, Math.Clamp(alpha, 0f, 1f)), 1f));
        return true;
    }

    private HighlightStyle FocusStyle() => new(
        configuration.FocusDrawInForeground,
        configuration.FocusShowGroundRing,
        configuration.FocusShowTargetHalo,
        configuration.FocusShowRays,
        configuration.FocusShowChevron,
        configuration.FocusRainbowMode,
        configuration.FocusReducedMotion,
        configuration.FocusGlowColor,
        configuration.FocusIntensity,
        configuration.FocusSizeScale,
        configuration.FocusAuraRadius,
        configuration.FocusPulseSpeed,
        configuration.FocusPulseAmount,
        configuration.FocusGroundPadding,
        configuration.FocusVerticalOffset);

    private HighlightStyle CurrentTargetStyle() => new(
        configuration.CurrentTargetDrawInForeground,
        configuration.CurrentTargetShowGroundRing,
        configuration.CurrentTargetShowTargetHalo,
        configuration.CurrentTargetShowRays,
        configuration.CurrentTargetShowChevron,
        configuration.CurrentTargetRainbowMode,
        configuration.CurrentTargetReducedMotion,
        configuration.CurrentTargetGlowColor,
        configuration.CurrentTargetIntensity,
        configuration.CurrentTargetSizeScale,
        configuration.CurrentTargetAuraRadius,
        configuration.CurrentTargetPulseSpeed,
        configuration.CurrentTargetPulseAmount,
        configuration.CurrentTargetGroundPadding,
        configuration.CurrentTargetVerticalOffset);

    private static Vector4 GetAnimatedColor(HighlightStyle style, double time, bool reducedMotion)
    {
        var fallback = new Vector4(1f, 0.06f, 0.72f, 1f);
        var color = SanitizeColor(style.Color, fallback);
        if (!style.RainbowMode) return color;

        var hue = reducedMotion ? 0.52f : (float)((time * 0.09) % 1.0);
        return HsvToRgb(hue, 0.88f, 1f, color.W);
    }

    private static Vector4 HsvToRgb(float hue, float saturation, float value, float alpha)
    {
        var scaled = hue * 6f;
        var sector = (int)MathF.Floor(scaled);
        var fraction = scaled - sector;
        var p = value * (1f - saturation);
        var q = value * (1f - (fraction * saturation));
        var t = value * (1f - ((1f - fraction) * saturation));

        return (sector % 6) switch
        {
            0 => new Vector4(value, t, p, alpha),
            1 => new Vector4(q, value, p, alpha),
            2 => new Vector4(p, value, t, alpha),
            3 => new Vector4(p, q, value, alpha),
            4 => new Vector4(t, p, value, alpha),
            _ => new Vector4(value, p, q, alpha),
        };
    }

    private static IGameObject? ValidOrNull(IGameObject? target) =>
        target is not null && target.IsValid() ? target : null;

    private TargetHighlightCandidate? CreateCandidate(IGameObject? target)
    {
        if (target is null) return null;

        var character = target as ICharacter;
        var localPlayer = objectTable.LocalPlayer;
        var centerDistance = localPlayer is not null && localPlayer.IsValid()
            ? Vector3.Distance(localPlayer.Position, target.Position)
            : -1f;
        var localHitboxRadius = localPlayer?.HitboxRadius ?? -1f;

        return new TargetHighlightCandidate(
            target.GameObjectId,
            target.Address != 0 && target.IsValid(),
            character?.ClassJob.RowId ?? 0,
            character?.CurrentHp ?? 0,
            character?.MaxHp ?? 0,
            centerDistance,
            localHitboxRadius,
            target.HitboxRadius,
            ResolveExactEnemySlot(target.GameObjectId, target.EntityId));
    }

    private int ResolveExactEnemySlot(ulong gameObjectId, uint entityId)
    {
        if (!tracker.IsActive || !TargetHighlightRules.IsValidGameObjectId(gameObjectId)) return 0;

        var slot = 0;
        foreach (var enemy in tracker.Enemies)
        {
            if (enemy.GameObjectId != gameObjectId || enemy.EntityId != entityId) continue;
            if (slot != 0) return 0;
            slot = enemy.Slot;
        }

        return slot;
    }

    private static float SafeFloat(float value, float fallback, float minimum, float maximum) =>
        Math.Clamp(float.IsFinite(value) ? value : fallback, minimum, maximum);

    private static Vector4 SanitizeColor(Vector4 color, Vector4 fallback) => new(
        SafeFloat(color.X, fallback.X, 0f, 1f),
        SafeFloat(color.Y, fallback.Y, 0f, 1f),
        SafeFloat(color.Z, fallback.Z, 0f, 1f),
        SafeFloat(color.W, fallback.W, 0f, 1f));

    private static uint PackColor(Vector4 color, float alpha)
    {
        var safe = SanitizeColor(color, Vector4.One);
        var red = (uint)(safe.X * 255f + 0.5f);
        var green = (uint)(safe.Y * 255f + 0.5f);
        var blue = (uint)(safe.Z * 255f + 0.5f);
        var packedAlpha = (uint)(Math.Clamp(alpha * safe.W, 0f, 1f) * 255f + 0.5f);
        return red | (green << 8) | (blue << 16) | (packedAlpha << 24);
    }

    private readonly record struct HighlightStyle(
        bool DrawInForeground,
        bool ShowGroundRing,
        bool ShowTargetHalo,
        bool ShowRays,
        bool ShowChevron,
        bool RainbowMode,
        bool ReducedMotion,
        Vector4 Color,
        float Intensity,
        float SizeScale,
        float AuraRadius,
        float PulseSpeed,
        float PulseAmount,
        float GroundPadding,
        float VerticalOffset);
}
