using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;
using SeitonSense.Core;
using SeitonSense.Plugin.Services;

namespace SeitonSense.Plugin.UI;

internal readonly record struct LimitBreakNotificationOptions(
    bool Enabled,
    bool ShowSelfActivation,
    bool ShowAllyDamageEvents,
    bool ShowNames,
    float Scale,
    float BackgroundOpacity);

/// <summary>
/// Standalone, non-interactive LB notification surface. It consumes only the
/// immutable exact-runtime snapshot and does not depend on combat frames,
/// their gauges, snapshots, targeting, layout, or configuration model.
/// </summary>
internal sealed class LimitBreakNotificationRenderer
{
    private const long DamageCardFadeMilliseconds = 500;

    private static readonly Vector4 AccentColor = new(1f, 0.7f, 0.1f, 1f);
    private static readonly Vector4 AccentBrightColor = new(1f, 0.93f, 0.58f, 1f);
    private static readonly Vector4 BackgroundColor = new(0.018f, 0.01f, 0.035f, 1f);
    private static readonly Vector4 TextColor = new(0.98f, 0.99f, 1f, 1f);
    private static readonly Vector4 ShadowColor = new(0f, 0f, 0f, 0.98f);

    private readonly CombatLimitBreakRuntimeService runtime;
    private readonly IGameGui gameGui;
    private readonly ITextureProvider textureProvider;
    private readonly IPluginLog log;
    private readonly Func<LimitBreakNotificationOptions> optionsProvider;
    private long nextErrorLogAtMilliseconds;

    internal LimitBreakNotificationRenderer(
        CombatLimitBreakRuntimeService runtime,
        IGameGui gameGui,
        ITextureProvider textureProvider,
        IPluginLog log,
        Func<LimitBreakNotificationOptions> optionsProvider)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        this.gameGui = gameGui ?? throw new ArgumentNullException(nameof(gameGui));
        this.textureProvider = textureProvider ?? throw new ArgumentNullException(nameof(textureProvider));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        this.optionsProvider = optionsProvider ?? throw new ArgumentNullException(nameof(optionsProvider));
    }

    internal void Draw()
    {
        if (gameGui.GameUiHidden) return;

        try
        {
            var options = Sanitize(optionsProvider());
            if (!options.Enabled ||
                (!options.ShowSelfActivation && !options.ShowAllyDamageEvents))
            {
                return;
            }

            var snapshot = runtime.Snapshot;
            if (!snapshot.Active) return;

            var now = Environment.TickCount64;
            var viewport = ImGui.GetMainViewport();
            var configuredScale = options.Scale;
            var scale = Math.Clamp(
                Math.Max(0.5f, ImGuiHelpers.GlobalScale) * configuredScale,
                0.5f,
                4f);
            var draw = ImGui.GetForegroundDrawList();

            if (options.ShowSelfActivation &&
                TryResolveSelfNotification(snapshot, now, out var self) &&
                CombatLimitBreakNotificationRules.TryBuildSelfBannerRectangle(
                    viewport.WorkPos.X,
                    viewport.WorkPos.Y,
                    viewport.WorkSize.X,
                    viewport.WorkSize.Y,
                    scale,
                    out var bannerRectangle))
            {
                DrawSelfBanner(
                    draw,
                    self,
                    bannerRectangle,
                    configuredScale,
                    scale,
                    options.BackgroundOpacity);
            }

            if (!options.ShowAllyDamageEvents) return;
            var damageCards = ResolveDamageCards(snapshot, options.ShowNames, now);
            if (damageCards.Count == 0 ||
                !CombatLimitBreakNotificationRules.TryBuildDamageCardRectangles(
                    viewport.WorkPos.X,
                    viewport.WorkPos.Y,
                    viewport.WorkSize.X,
                    viewport.WorkSize.Y,
                    scale,
                    damageCards.Count,
                    out var damageRectangles))
            {
                return;
            }

            for (var index = 0; index < damageCards.Count; index++)
            {
                DrawDamageCard(
                    draw,
                    damageCards[index],
                    damageRectangles[index],
                    now,
                    configuredScale,
                    scale,
                    options.BackgroundOpacity);
            }
        }
        catch (Exception exception)
        {
            var now = Environment.TickCount64;
            if (now < nextErrorLogAtMilliseconds) return;
            nextErrorLogAtMilliseconds = now + 10_000;
            log.Error(exception, "Seiton Sense LB notification renderer failed closed.");
        }
    }

    private static bool TryResolveSelfNotification(
        CombatLimitBreakRuntimeSnapshot snapshot,
        long nowMilliseconds,
        out SelfNotification notification)
    {
        notification = default;
        var candidates = snapshot.Actors
            .Where(static state =>
                state.Side == CombatLimitBreakRosterSide.Self &&
                state.Slot == CombatLimitBreakNotificationRules.SelfSlot &&
                state.Actor.IsValid)
            .ToArray();
        if (candidates.Length != 1) return false;

        var state = candidates[0];
        if (!CombatLimitBreakCatalog.TryFindByAction(
                state.ActivationActionId,
                out var definition,
                out var action) ||
            !CombatLimitBreakCatalog.IsActivation(action) ||
            definition.JobId != state.JobId ||
            CombatLimitBreakCatalog.ResolveIconId(definition, action) != state.IconId ||
            !CombatLimitBreakNotificationRules.TryBuildSelfPlan(
                new CombatLimitBreakSelfNotificationObservation(
                    true,
                    state.Slot,
                    state.IconId,
                    state.Presentation,
                    state.DurationConfirmed,
                    state.ActivatedAtMilliseconds,
                    state.ExpiresAtMilliseconds,
                    snapshot.PublishedAtMilliseconds),
                nowMilliseconds,
                out var plan))
        {
            return false;
        }

        notification = new SelfNotification(
            definition.Name,
            plan.IconId,
            plan.ShowCountdown,
            plan.RemainingMilliseconds);
        return true;
    }

    private IReadOnlyList<DamageNotification> ResolveDamageCards(
        CombatLimitBreakRuntimeSnapshot snapshot,
        bool showNames,
        long nowMilliseconds)
    {
        var cards = new List<DamageNotification>(
            CombatLimitBreakNotificationRules.MaximumVisibleDamageCards);
        var candidates = snapshot.AllyDamageEvents
            .GroupBy(static damageEvent => damageEvent.EventToken)
            .Where(static group => group.Count() == 1)
            .Select(static group => group.Single())
            .OrderByDescending(static damageEvent => damageEvent.ObservedAtMilliseconds)
            .ThenByDescending(static damageEvent => damageEvent.EventToken);

        foreach (var damageEvent in candidates)
        {
            if (!CombatLimitBreakCatalog.TryFindByAction(
                    damageEvent.ActionId,
                    out var definition,
                    out var action) ||
                !CombatLimitBreakCatalog.IsDirectlyAttributableDamage(action) ||
                definition.JobId != damageEvent.CasterJobId ||
                CombatLimitBreakCatalog.ResolveIconId(definition, action) != damageEvent.IconId ||
                !CombatLimitBreakNotificationRules.TryBuildDamagePlan(
                    new CombatLimitBreakDamageNotificationObservation(
                        damageEvent.Caster,
                        damageEvent.CasterPartySlot,
                        damageEvent.Target,
                        damageEvent.TargetEnemySlot,
                        damageEvent.IconId,
                        damageEvent.Damage,
                        damageEvent.ObservedAtMilliseconds,
                        damageEvent.ExpiresAtMilliseconds,
                        snapshot.PublishedAtMilliseconds,
                        damageEvent.EpisodeToken,
                        damageEvent.EventToken),
                    nowMilliseconds,
                    out var plan))
            {
                continue;
            }

            var casterName = $"P{plan.CasterPartySlot}";
            var targetName = $"S{plan.TargetEnemySlot}";
            if (showNames &&
                runtime.TryResolveCurrentDamageDisplayNames(
                    damageEvent,
                    out var resolvedCasterName,
                    out var resolvedTargetName))
            {
                casterName = resolvedCasterName;
                targetName = resolvedTargetName;
            }

            cards.Add(new DamageNotification(
                casterName,
                targetName,
                plan.IconId,
                plan.Damage,
                damageEvent.ExpiresAtMilliseconds));
            if (cards.Count >= CombatLimitBreakNotificationRules.MaximumVisibleDamageCards)
                break;
        }

        return cards;
    }

    private void DrawSelfBanner(
        ImDrawListPtr draw,
        SelfNotification notification,
        LimitBreakNotificationRectangle rectangle,
        float configuredScale,
        float scale,
        float backgroundOpacity)
    {
        var minimum = PixelSnap(new Vector2(rectangle.Left, rectangle.Top));
        var maximum = PixelSnap(new Vector2(rectangle.Right, rectangle.Bottom));
        var remaining = notification.RemainingMilliseconds;
        if (remaining <= 0) return;

        var confirmedDuration = notification.ShowCountdown;
        var flashFraction = confirmedDuration
            ? 1f
            : Math.Clamp(
                remaining / (float)CombatLimitBreakCatalog.InstantFlashMilliseconds,
                0f,
                1f);
        var fade = confirmedDuration ? 1f : 0.35f + (0.65f * flashFraction);
        var pulsePhase = Math.Max(0L, Environment.TickCount64 % 900L) / 900f;
        var pulse = 0.5f + (0.5f * MathF.Sin(pulsePhase * MathF.PI * 2f));
        var rounding = 12f * scale;

        draw.AddRectFilled(
            minimum - new Vector2(7f * scale),
            maximum + new Vector2(7f * scale),
            Pack(new Vector4(AccentColor.X, AccentColor.Y, AccentColor.Z, (0.08f + (0.08f * pulse)) * fade)),
            rounding + (7f * scale));
        draw.AddRectFilled(
            minimum,
            maximum,
            Pack(new Vector4(
                BackgroundColor.X,
                BackgroundColor.Y,
                BackgroundColor.Z,
                backgroundOpacity * fade)),
            rounding);
        draw.AddRectFilled(
            minimum,
            new Vector2(minimum.X + (8f * scale), maximum.Y),
            Pack(new Vector4(AccentColor.X, AccentColor.Y, AccentColor.Z, fade)),
            rounding);
        draw.AddRect(
            minimum,
            maximum,
            Pack(new Vector4(AccentColor.X, AccentColor.Y, AccentColor.Z, (0.76f + (0.24f * pulse)) * fade)),
            rounding,
            ImDrawFlags.None,
            Math.Max(2f, (2.4f + pulse) * scale));

        var height = maximum.Y - minimum.Y;
        var iconSize = Math.Min(60f * scale, height - (18f * scale));
        var iconMinimum = new Vector2(
            minimum.X + (18f * scale),
            minimum.Y + ((height - iconSize) * 0.5f));
        var iconMaximum = iconMinimum + new Vector2(iconSize);
        draw.AddRectFilled(
            iconMinimum - new Vector2(3f * scale),
            iconMaximum + new Vector2(3f * scale),
            Pack(new Vector4(0f, 0f, 0f, 0.94f * fade)),
            8f * scale);
        if (!TryDrawGameIcon(draw, notification.IconId, iconMinimum, iconMaximum, fade))
        {
            draw.AddRectFilled(
                iconMinimum,
                iconMaximum,
                Pack(new Vector4(AccentColor.X * 0.24f, AccentColor.Y * 0.24f, 0.02f, fade)),
                6f * scale);
        }

        draw.AddRect(
            iconMinimum - new Vector2(2f * scale),
            iconMaximum + new Vector2(2f * scale),
            Pack(new Vector4(AccentColor.X, AccentColor.Y, AccentColor.Z, fade)),
            8f * scale,
            ImDrawFlags.None,
            Math.Max(1.5f, 2f * scale));

        var timerWidth = notification.ShowCountdown ? 92f * scale : 0f;
        var contentLeft = iconMaximum.X + (16f * scale);
        var contentRight = maximum.X - (18f * scale) - timerWidth;
        var headline = FitText("LB ACTIVATED!", contentRight - contentLeft, 1.18f * configuredScale);
        DrawOutlinedText(
            draw,
            new Vector2(contentLeft, minimum.Y + (11f * scale)),
            headline,
            1.18f * configuredScale,
            new Vector4(AccentBrightColor.X, AccentBrightColor.Y, AccentBrightColor.Z, fade));
        var name = FitText(notification.Name, contentRight - contentLeft, 0.78f * configuredScale);
        DrawOutlinedText(
            draw,
            new Vector2(contentLeft, minimum.Y + (49f * scale)),
            name,
            0.78f * configuredScale,
            new Vector4(TextColor.X, TextColor.Y, TextColor.Z, fade));

        if (!notification.ShowCountdown) return;
        var timerMinimum = new Vector2(
            maximum.X - timerWidth - (12f * scale),
            minimum.Y + (15f * scale));
        var timerMaximum = new Vector2(
            maximum.X - (12f * scale),
            maximum.Y - (15f * scale));
        draw.AddRectFilled(
            timerMinimum,
            timerMaximum,
            Pack(new Vector4(0.2f, 0.09f, 0.005f, 0.98f * fade)),
            8f * scale);
        draw.AddRect(
            timerMinimum,
            timerMaximum,
            Pack(new Vector4(AccentColor.X, AccentColor.Y, AccentColor.Z, fade)),
            8f * scale,
            ImDrawFlags.None,
            Math.Max(1.5f, 2f * scale));
        DrawCenteredOutlinedText(
            draw,
            timerMinimum,
            timerMaximum,
            $"{remaining / 1000d:0.0}s",
            0.94f * configuredScale,
            new Vector4(AccentBrightColor.X, AccentBrightColor.Y, AccentBrightColor.Z, fade));
    }

    private void DrawDamageCard(
        ImDrawListPtr draw,
        DamageNotification notification,
        LimitBreakNotificationRectangle rectangle,
        long nowMilliseconds,
        float configuredScale,
        float scale,
        float backgroundOpacity)
    {
        var remaining = Math.Max(0, notification.ExpiresAtMilliseconds - nowMilliseconds);
        if (remaining <= 0) return;
        var alpha = Math.Clamp(
            remaining / (float)DamageCardFadeMilliseconds,
            0f,
            1f);
        var minimum = PixelSnap(new Vector2(rectangle.Left, rectangle.Top));
        var maximum = PixelSnap(new Vector2(rectangle.Right, rectangle.Bottom));
        var rounding = 9f * scale;

        draw.AddRectFilled(
            minimum,
            maximum,
            Pack(new Vector4(
                BackgroundColor.X,
                BackgroundColor.Y,
                BackgroundColor.Z,
                backgroundOpacity * alpha)),
            rounding);
        draw.AddRectFilled(
            minimum,
            new Vector2(minimum.X + (6f * scale), maximum.Y),
            Pack(new Vector4(AccentColor.X, AccentColor.Y, AccentColor.Z, alpha)),
            rounding);
        draw.AddRect(
            minimum,
            maximum,
            Pack(new Vector4(AccentColor.X, AccentColor.Y, AccentColor.Z, 0.92f * alpha)),
            rounding,
            ImDrawFlags.None,
            Math.Max(1.5f, 1.8f * scale));

        var cardHeight = maximum.Y - minimum.Y;
        var iconSize = Math.Min(46f * scale, cardHeight - (16f * scale));
        var iconMinimum = new Vector2(
            minimum.X + (12f * scale),
            minimum.Y + ((cardHeight - iconSize) * 0.5f));
        var iconMaximum = iconMinimum + new Vector2(iconSize);
        if (!TryDrawGameIcon(draw, notification.IconId, iconMinimum, iconMaximum, alpha))
        {
            draw.AddRectFilled(
                iconMinimum,
                iconMaximum,
                Pack(new Vector4(AccentColor.X * 0.24f, AccentColor.Y * 0.24f, 0.02f, alpha)),
                5f * scale);
        }

        draw.AddRect(
            iconMinimum,
            iconMaximum,
            Pack(new Vector4(AccentColor.X, AccentColor.Y, AccentColor.Z, alpha)),
            5f * scale,
            ImDrawFlags.None,
            Math.Max(1f, 1.4f * scale));

        var textLeft = iconMaximum.X + (12f * scale);
        var maximumTextWidth = maximum.X - textLeft - (10f * scale);
        var route = FitText(
            $"{Truncate(notification.CasterName, 17)}  →  {Truncate(notification.TargetName, 17)}",
            maximumTextWidth,
            0.72f * configuredScale);
        DrawOutlinedText(
            draw,
            new Vector2(textLeft, minimum.Y + (9f * scale)),
            route,
            0.72f * configuredScale,
            new Vector4(TextColor.X, TextColor.Y, TextColor.Z, alpha));
        DrawOutlinedText(
            draw,
            new Vector2(textLeft, minimum.Y + (37f * scale)),
            $"{notification.Damage:N0} DMG",
            0.92f * configuredScale,
            new Vector4(AccentBrightColor.X, AccentBrightColor.Y, AccentBrightColor.Z, alpha));
    }

    private bool TryDrawGameIcon(
        ImDrawListPtr draw,
        uint iconId,
        Vector2 minimum,
        Vector2 maximum,
        float alpha)
    {
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

    private static void DrawCenteredOutlinedText(
        ImDrawListPtr draw,
        Vector2 minimum,
        Vector2 maximum,
        string text,
        float textScale,
        Vector4 color)
    {
        var size = ImGui.CalcTextSize(text) * textScale;
        DrawOutlinedText(
            draw,
            new Vector2(
                minimum.X + (((maximum.X - minimum.X) - size.X) * 0.5f),
                minimum.Y + (((maximum.Y - minimum.Y) - size.Y) * 0.5f)),
            text,
            textScale,
            color);
    }

    private static void DrawOutlinedText(
        ImDrawListPtr draw,
        Vector2 position,
        string text,
        float textScale,
        Vector4 color)
    {
        if (string.IsNullOrEmpty(text)) return;
        var safeScale = Math.Max(0.35f, textScale);
        var font = ImGui.GetFont();
        var fontSize = ImGui.GetFontSize() * safeScale;
        var shadow = Math.Max(1f, ImGui.GetIO().DisplayFramebufferScale.X);
        draw.AddText(
            font,
            fontSize,
            position + new Vector2(shadow),
            Pack(new Vector4(ShadowColor.X, ShadowColor.Y, ShadowColor.Z, color.W)),
            text);
        draw.AddText(font, fontSize, position, Pack(color), text);
    }

    private static string FitText(string value, float maximumWidth, float textScale)
    {
        if (string.IsNullOrWhiteSpace(value) || maximumWidth <= 0f) return string.Empty;
        var text = value.Trim();
        var safeScale = Math.Max(0.35f, textScale);
        if ((ImGui.CalcTextSize(text).X * safeScale) <= maximumWidth) return text;

        for (var length = text.Length - 1; length > 0; length--)
        {
            var candidate = text[..length].TrimEnd() + "…";
            if ((ImGui.CalcTextSize(candidate).X * safeScale) <= maximumWidth)
                return candidate;
        }

        return string.Empty;
    }

    private static string Truncate(string value, int maximumCharacters)
    {
        if (string.IsNullOrWhiteSpace(value)) return "?";
        var text = value.Trim();
        return text.Length <= maximumCharacters
            ? text
            : text[..(maximumCharacters - 1)] + "…";
    }

    private static LimitBreakNotificationOptions Sanitize(LimitBreakNotificationOptions options) =>
        options with
        {
            Scale = float.IsFinite(options.Scale)
                ? Math.Clamp(options.Scale, 0.65f, 1.75f)
                : 1f,
            BackgroundOpacity = float.IsFinite(options.BackgroundOpacity)
                ? Math.Clamp(options.BackgroundOpacity, 0.35f, 1f)
                : 0.92f,
        };

    private static Vector2 PixelSnap(Vector2 value) =>
        new(MathF.Round(value.X), MathF.Round(value.Y));

    private static uint Pack(Vector4 color) =>
        ImGui.ColorConvertFloat4ToU32(new Vector4(
            Math.Clamp(color.X, 0f, 1f),
            Math.Clamp(color.Y, 0f, 1f),
            Math.Clamp(color.Z, 0f, 1f),
            Math.Clamp(color.W, 0f, 1f)));

    private readonly record struct SelfNotification(
        string Name,
        uint IconId,
        bool ShowCountdown,
        long RemainingMilliseconds);

    private readonly record struct DamageNotification(
        string CasterName,
        string TargetName,
        uint IconId,
        uint Damage,
        long ExpiresAtMilliseconds);
}
