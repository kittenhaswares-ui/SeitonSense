using System.Numerics;
using Dalamud.Bindings.ImGui;
using SeitonSense.Core;
using SeitonSense.Plugin.Services;

namespace SeitonSense.Plugin.UI;

internal sealed partial class CombatFramesRenderer
{
    private const int MaximumVisibleLimitBreakDamageCards = 3;
    private const long LimitBreakDamageCardFadeMilliseconds = 500;

    private static readonly Vector4 LimitBreakTrackColor = new(0.055f, 0.035f, 0.105f, 1f);
    private static readonly Vector4 LimitBreakFillColor = new(0.72f, 0.28f, 1f, 1f);
    private static readonly Vector4 LimitBreakReadyColor = new(1f, 0.76f, 0.18f, 1f);
    private static readonly Vector4 LimitBreakUnknownColor = new(0.38f, 0.34f, 0.48f, 1f);
    private static readonly Vector4 LimitBreakActivationColor = new(1f, 0.7f, 0.1f, 1f);

    private static CombatLimitBreakActorState? ResolveLimitBreakState(
        CombatFrameActorSnapshot actor,
        bool self,
        bool preview,
        CombatLimitBreakRuntimeSnapshot? snapshot,
        long nowMilliseconds)
    {
        if (preview)
        {
            if (self)
            {
                return new CombatLimitBreakActorState(
                    actor.Frame.Actor,
                    CombatLimitBreakRosterSide.Self,
                    CombatFrameRules.SelfSlot,
                    40,
                    29_266,
                    9_624,
                    "Mesotes",
                    CombatLimitBreakPresentationKind.Duration,
                    true,
                    3_118,
                    "Mesotes",
                    nowMilliseconds - 2_000,
                    12_400,
                    nowMilliseconds + 12_400,
                    1);
            }

            if (actor.Frame.Slot == 3)
            {
                return new CombatLimitBreakActorState(
                    actor.Frame.Actor,
                    CombatLimitBreakRosterSide.Enemy,
                    3,
                    31,
                    29_415,
                    9_636,
                    "Marksman's Spite",
                    CombatLimitBreakPresentationKind.Instant,
                    false,
                    0,
                    string.Empty,
                    nowMilliseconds - 700,
                    1_100,
                    nowMilliseconds + 1_100,
                    2);
            }

            return null;
        }

        var frame = actor.Frame;
        if (snapshot is null ||
            !frame.Actor.IsValid ||
            !snapshot.TryFindActor(frame.Actor, out var state) ||
            state.ExpiresAtMilliseconds <= nowMilliseconds ||
            state.JobId != frame.JobId ||
            state.Slot != frame.Slot ||
            state.Side != (self ? CombatLimitBreakRosterSide.Self : CombatLimitBreakRosterSide.Enemy))
        {
            return null;
        }

        return state;
    }

    private static CombatFrameLimitGaugeReading ResolveLimitGauge(
        int slot,
        bool self,
        bool preview,
        CombatFrameLimitGaugeSnapshot? snapshot)
    {
        if (preview)
        {
            if (self) return CombatFrameLimitGaugeRules.ExactSelf(0.64f);
            return slot switch
            {
                1 => CombatFrameLimitGaugeRules.CalibratedEnemy(slot, 0.78f),
                3 => CombatFrameLimitGaugeRules.CalibratedEnemy(slot, 0.05f),
                _ => CombatFrameLimitGaugeReading.Unknown(slot),
            };
        }

        if (snapshot is null) return CombatFrameLimitGaugeReading.Unknown(slot);
        var reading = self ? snapshot.Self : snapshot.FindEnemy(slot);
        var expectedTrust = self
            ? CombatFrameLimitGaugeTrust.ExactLocalController
            : CombatFrameLimitGaugeTrust.CalibratedNativeHud;
        return reading.Slot == slot && reading.IsKnown && reading.Trust == expectedTrust
            ? reading
            : CombatFrameLimitGaugeReading.Unknown(slot);
    }

    private static void DrawLimitBreakGauge(
        ImDrawListPtr draw,
        float contentLeft,
        float contentRight,
        float bottom,
        bool self,
        CombatFrameLimitGaugeReading reading,
        float scale,
        float configuredScale)
    {
        var height = (self ? 11f : 9f) * scale;
        var maximum = new Vector2(contentRight, bottom - (4f * scale));
        var minimum = new Vector2(contentLeft, maximum.Y - height);
        if (maximum.X <= minimum.X || maximum.Y <= minimum.Y) return;

        draw.AddRectFilled(minimum, maximum, Pack(LimitBreakTrackColor, 1f), 3f * scale);
        if (!reading.IsKnown)
        {
            DrawResourceBar(
                draw,
                minimum,
                maximum,
                0f,
                LimitBreakUnknownColor,
                "LB ?",
                scale,
                0.52f * configuredScale,
                []);
            return;
        }

        var fraction = Math.Clamp(reading.Fraction, 0f, 1f);
        var color = fraction >= 0.999f ? LimitBreakReadyColor : LimitBreakFillColor;
        DrawResourceBar(
            draw,
            minimum,
            maximum,
            fraction,
            color,
            $"LB {fraction:P0}",
            scale,
            0.52f * configuredScale,
            []);
        if (fraction < 0.999f) return;

        draw.AddRect(
            minimum - new Vector2(scale),
            maximum + new Vector2(scale),
            Pack(LimitBreakReadyColor, 0.9f),
            3f * scale,
            ImDrawFlags.None,
            Math.Max(1f, 1.5f * scale));
    }

    private void DrawLimitBreakActivation(
        ImDrawListPtr draw,
        CombatLimitBreakActorState state,
        Vector2 topLeft,
        Vector2 bottomRight,
        float scale,
        float configuredScale,
        long nowMilliseconds)
    {
        var remainingMilliseconds = Math.Max(0, state.ExpiresAtMilliseconds - nowMilliseconds);
        if (remainingMilliseconds <= 0) return;

        var durationConfirmed = state.Presentation == CombatLimitBreakPresentationKind.Duration &&
                                state.DurationConfirmed;
        var flashFraction = durationConfirmed
            ? 1f
            : Math.Clamp(
                remainingMilliseconds / (float)CombatLimitBreakCatalog.InstantFlashMilliseconds,
                0f,
                1f);
        var fade = durationConfirmed ? 1f : 0.38f + (0.62f * flashFraction);
        var pulsePhase = Math.Max(0, nowMilliseconds % 850) / 850f;
        var pulse = 0.5f + (0.5f * MathF.Sin(pulsePhase * MathF.PI * 2f));
        var self = state.Side == CombatLimitBreakRosterSide.Self;
        var frameRounding = 8f * scale;
        var borderInset = Math.Max(1f, scale);

        // Activation remains unmistakable without replacing the actor panel.
        // The full-frame treatment is border-only, so HP, MP, LB gauge, and
        // right-side status badges stay readable for both Self and S1-S5.
        draw.AddRect(
            topLeft + new Vector2(borderInset),
            bottomRight - new Vector2(borderInset),
            Pack(LimitBreakActivationColor, (0.72f + (0.28f * pulse)) * fade),
            frameRounding,
            ImDrawFlags.None,
            Math.Max(2f, (2.4f + pulse) * scale));
        draw.AddRect(
            topLeft - new Vector2(2f * scale),
            bottomRight + new Vector2(2f * scale),
            Pack(LimitBreakActivationColor, (0.14f + (0.22f * pulse)) * fade),
            frameRounding + (2f * scale),
            ImDrawFlags.None,
            Math.Max(1f, 1.5f * scale));

        var contentLeftOffset = (self ? 80f : 68f) * scale;
        var rightBadgeWidth = (self ? 126f : 114f) * scale;
        var bannerMinimum = new Vector2(
            topLeft.X + contentLeftOffset,
            topLeft.Y + (3f * scale));
        var bannerMaximum = new Vector2(
            bottomRight.X - rightBadgeWidth,
            topLeft.Y + ((self ? 27f : 23f) * scale));
        if (bannerMaximum.X <= bannerMinimum.X || bannerMaximum.Y <= bannerMinimum.Y) return;

        var bannerRounding = 4f * scale;
        draw.AddRectFilled(
            bannerMinimum,
            bannerMaximum,
            Pack(new Vector4(0.025f, 0.012f, 0.002f, 1f), 0.96f * fade),
            bannerRounding);
        draw.AddRectFilled(
            bannerMinimum,
            new Vector2(bannerMinimum.X + (4f * scale), bannerMaximum.Y),
            Pack(LimitBreakActivationColor, fade),
            bannerRounding);
        draw.AddRect(
            bannerMinimum,
            bannerMaximum,
            Pack(LimitBreakActivationColor, (0.72f + (0.28f * pulse)) * fade),
            bannerRounding,
            ImDrawFlags.None,
            Math.Max(1f, 1.35f * scale));

        var bannerHeight = bannerMaximum.Y - bannerMinimum.Y;
        var iconPadding = 2f * scale;
        var iconSize = Math.Min(
            (self ? 20f : 17f) * scale,
            Math.Max(1f, bannerHeight - (iconPadding * 2f)));
        var iconMinimum = new Vector2(
            bannerMinimum.X + (6f * scale),
            bannerMinimum.Y + ((bannerHeight - iconSize) * 0.5f));
        var iconMaximum = iconMinimum + new Vector2(iconSize);
        draw.AddRectFilled(
            iconMinimum - new Vector2(scale),
            iconMaximum + new Vector2(scale),
            Pack(OpaqueBlack, 0.9f * fade),
            3f * scale);
        if (!TryDrawGameIcon(draw, state.IconId, iconMinimum, iconMaximum, fade))
        {
            draw.AddRectFilled(
                iconMinimum,
                iconMaximum,
                Pack(LimitBreakActivationColor, 0.32f * fade),
                2f * scale);
        }

        draw.AddRect(
            iconMinimum - new Vector2(scale),
            iconMaximum + new Vector2(scale),
            Pack(LimitBreakActivationColor, fade),
            3f * scale,
            ImDrawFlags.None,
            Math.Max(1f, scale));

        var fontScale = (self ? 0.66f : 0.58f) * configuredScale;
        var textMinimum = new Vector2(
            iconMaximum.X + (6f * scale),
            bannerMinimum.Y);
        var textMaximum = bannerMaximum - new Vector2(5f * scale, 0f);
        if (durationConfirmed)
        {
            var countdown = $"{remainingMilliseconds / 1000d:0.0}s";
            var countdownScale = Math.Max(0.35f, (self ? 0.64f : 0.56f) * configuredScale);
            var countdownWidth = (ImGui.CalcTextSize(countdown).X * countdownScale) + (10f * scale);
            var countdownMinimum = new Vector2(
                Math.Max(textMinimum.X, textMaximum.X - countdownWidth),
                bannerMinimum.Y + (2f * scale));
            var countdownMaximum = new Vector2(textMaximum.X, bannerMaximum.Y - (2f * scale));
            if (countdownMaximum.X > countdownMinimum.X)
            {
                draw.AddRectFilled(
                    countdownMinimum,
                    countdownMaximum,
                    Pack(LimitBreakActivationColor, (0.2f + (0.08f * pulse)) * fade),
                    3f * scale);
                DrawLimitBreakBannerText(
                    draw,
                    countdownMinimum,
                    countdownMaximum,
                    countdown,
                    countdownScale,
                    new Vector4(1f, 0.93f, 0.58f, fade),
                    centerHorizontally: true);
                textMaximum.X = countdownMinimum.X - (5f * scale);
            }
        }

        var activationLabel = durationConfirmed ? "LB ACTIVE" : "LB ACTIVATED";
        var label = FitLimitBreakBannerText(
            $"{activationLabel}  {state.LimitBreakName}",
            Math.Max(0f, textMaximum.X - textMinimum.X),
            fontScale);
        DrawLimitBreakBannerText(
            draw,
            textMinimum,
            textMaximum,
            label,
            fontScale,
            new Vector4(1f, 0.93f, 0.58f, fade),
            centerHorizontally: false);
    }

    private void DrawAllyLimitBreakDamageCards(
        ImDrawListPtr draw,
        CombatFramesOptions options,
        float scale,
        long nowMilliseconds,
        Vector2 enemyTopLeft,
        CombatLimitBreakRuntimeSnapshot? snapshot)
    {
        var cards = new List<LimitBreakDamageCard>(MaximumVisibleLimitBreakDamageCards);
        if (options.PreviewEnabled)
        {
            cards.Add(new LimitBreakDamageCard(
                options.ShowNames ? "Preview Ally" : "P2",
                options.ShowNames ? "Preview Enemy" : "S3",
                9_610,
                38_250,
                nowMilliseconds + 3_000));
        }
        else if (snapshot is not null)
        {
            foreach (var damageEvent in snapshot.AllyDamageEvents
                         .Where(damageEvent =>
                             damageEvent.Damage > 0 &&
                             damageEvent.ExpiresAtMilliseconds > nowMilliseconds)
                         .OrderByDescending(static damageEvent => damageEvent.ObservedAtMilliseconds)
                         .ThenByDescending(static damageEvent => damageEvent.EventToken))
            {
                string casterName;
                string targetName;
                if (options.ShowNames)
                {
                    if (!limitBreaks.TryResolveCurrentDamageDisplayNames(
                            damageEvent,
                            out casterName,
                            out targetName))
                    {
                        continue;
                    }
                }
                else
                {
                    casterName = $"P{damageEvent.CasterPartySlot}";
                    targetName = $"S{damageEvent.TargetEnemySlot}";
                }

                cards.Add(new LimitBreakDamageCard(
                    casterName,
                    targetName,
                    damageEvent.IconId,
                    damageEvent.Damage,
                    damageEvent.ExpiresAtMilliseconds));
                if (cards.Count >= MaximumVisibleLimitBreakDamageCards) break;
            }
        }

        if (cards.Count == 0) return;

        var cardWidth = 310f * scale;
        var cardHeight = 64f * scale;
        var gap = 7f * scale;
        var screen = ImGui.GetIO().DisplaySize;
        var left = Math.Clamp(
            enemyTopLeft.X - cardWidth - (12f * scale),
            8f * scale,
            Math.Max(8f * scale, screen.X - cardWidth - (8f * scale)));
        var top = Math.Clamp(
            enemyTopLeft.Y + (8f * scale),
            8f * scale,
            Math.Max(8f * scale, screen.Y - ((cardHeight + gap) * cards.Count)));

        for (var index = 0; index < cards.Count; index++)
        {
            var card = cards[index];
            var remaining = Math.Max(0, card.ExpiresAtMilliseconds - nowMilliseconds);
            var alpha = Math.Clamp(
                remaining / (float)LimitBreakDamageCardFadeMilliseconds,
                0f,
                1f);
            var minimum = PixelSnap(new Vector2(left, top + (index * (cardHeight + gap))));
            var maximum = minimum + new Vector2(cardWidth, cardHeight);
            DrawLimitBreakDamageCard(draw, card, minimum, maximum, scale, options.Scale, alpha);
        }
    }

    private void DrawLimitBreakDamageCard(
        ImDrawListPtr draw,
        LimitBreakDamageCard card,
        Vector2 minimum,
        Vector2 maximum,
        float scale,
        float configuredScale,
        float alpha)
    {
        var rounding = 7f * scale;
        draw.AddRectFilled(minimum, maximum, Pack(BackgroundColor, 0.94f * alpha), rounding);
        draw.AddRectFilled(
            minimum,
            new Vector2(minimum.X + (4f * scale), maximum.Y),
            Pack(LimitBreakActivationColor, alpha),
            rounding);
        draw.AddRect(
            minimum,
            maximum,
            Pack(LimitBreakActivationColor, 0.92f * alpha),
            rounding,
            ImDrawFlags.None,
            Math.Max(1f, 1.5f * scale));

        var iconMinimum = minimum + new Vector2(10f, 10f) * scale;
        var iconMaximum = iconMinimum + new Vector2(44f * scale);
        if (!TryDrawGameIcon(draw, card.IconId, iconMinimum, iconMaximum, alpha))
            draw.AddRectFilled(iconMinimum, iconMaximum, Pack(LimitBreakActivationColor, 0.28f * alpha), 4f * scale);
        draw.AddRect(
            iconMinimum,
            iconMaximum,
            Pack(LimitBreakActivationColor, alpha),
            4f * scale,
            ImDrawFlags.None,
            Math.Max(1f, scale));

        var textX = iconMaximum.X + (10f * scale);
        var route = $"{TruncateDisplayText(card.CasterName, 15)} \u2192 {TruncateDisplayText(card.TargetName, 15)}";
        DrawText(
            draw,
            new Vector2(textX, minimum.Y + (9f * scale)),
            route,
            0.7f * configuredScale,
            new Vector4(1f, 1f, 1f, alpha));
        DrawText(
            draw,
            new Vector2(textX, minimum.Y + (34f * scale)),
            $"{card.Damage:N0} DMG",
            0.88f * configuredScale,
            new Vector4(1f, 0.76f, 0.2f, alpha));
    }

    private static void DrawLimitBreakBannerText(
        ImDrawListPtr draw,
        Vector2 minimum,
        Vector2 maximum,
        string text,
        float fontScale,
        Vector4 color,
        bool centerHorizontally)
    {
        if (maximum.X <= minimum.X || maximum.Y <= minimum.Y || string.IsNullOrEmpty(text)) return;

        var font = ImGui.GetFont();
        var safeScale = Math.Max(0.35f, fontScale);
        var fontSize = ImGui.GetFontSize() * safeScale;
        var textSize = ImGui.CalcTextSize(text) * safeScale;
        var position = new Vector2(
            centerHorizontally
                ? minimum.X + Math.Max(0f, ((maximum.X - minimum.X) - textSize.X) * 0.5f)
                : minimum.X,
            minimum.Y + Math.Max(0f, ((maximum.Y - minimum.Y) - textSize.Y) * 0.5f));
        var shadow = Math.Max(1f, ImGui.GetIO().DisplayFramebufferScale.X);
        draw.AddText(
            font,
            fontSize,
            position + new Vector2(shadow),
            Pack(OpaqueBlack, 0.94f * color.W),
            text);
        draw.AddText(font, fontSize, position, Pack(color, 1f), text);
    }

    private static string FitLimitBreakBannerText(string value, float maximumWidth, float fontScale)
    {
        if (string.IsNullOrWhiteSpace(value) || maximumWidth <= 0f) return string.Empty;

        var trimmed = value.Trim();
        var safeScale = Math.Max(0.35f, fontScale);
        if ((ImGui.CalcTextSize(trimmed).X * safeScale) <= maximumWidth) return trimmed;

        for (var length = trimmed.Length - 1; length > 0; length--)
        {
            var candidate = trimmed[..length].TrimEnd() + "…";
            if ((ImGui.CalcTextSize(candidate).X * safeScale) <= maximumWidth) return candidate;
        }

        return string.Empty;
    }

    private static string TruncateDisplayText(string value, int maximumCharacters)
    {
        if (string.IsNullOrWhiteSpace(value)) return "?";
        var trimmed = value.Trim();
        return trimmed.Length <= maximumCharacters
            ? trimmed
            : trimmed[..(maximumCharacters - 1)] + "…";
    }

    private readonly record struct LimitBreakDamageCard(
        string CasterName,
        string TargetName,
        uint IconId,
        uint Damage,
        long ExpiresAtMilliseconds);
}
