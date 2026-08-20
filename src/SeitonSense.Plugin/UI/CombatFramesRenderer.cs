using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;
using SeitonSense.Core;
using SeitonSense.Plugin.Services;

namespace SeitonSense.Plugin.UI;

/// <summary>
/// Draws one fixed self frame and a fixed S1-S5 stack in screen space above the
/// game world but behind regular ImGui windows. It never projects world
/// positions, acquires targets, handles input, or reads/writes native UI nodes.
/// </summary>
internal sealed class CombatFramesRenderer
{
    private const float EnemyWidth = 420f;
    private const float EnemyHeight = 74f;
    private const float EnemyGap = 6f;
    private const float SelfWidth = 460f;
    private const float SelfHeight = 92f;
    private const int MaximumStatusIcons = 3;

    private static readonly Vector4 BackgroundColor = new(0.008f, 0.012f, 0.025f, 1f);
    private static readonly Vector4 UnknownColor = new(0.34f, 0.38f, 0.46f, 1f);
    private static readonly Vector4 DeadColor = new(0.48f, 0.18f, 0.23f, 1f);
    private static readonly Vector4 SelfColor = new(0.22f, 0.82f, 0.67f, 1f);
    private static readonly Vector4 CurrentTargetColor = new(0.05f, 0.9f, 1f, 1f);
    private static readonly Vector4 FocusTargetColor = new(1f, 0.12f, 0.64f, 1f);
    private static readonly Vector4 PressureColor = new(1f, 0.16f, 0.12f, 1f);
    private static readonly Vector4 PressureMediumColor = new(1f, 0.68f, 0.12f, 1f);
    private static readonly Vector4 TeamPressureColor = new(0.12f, 0.9f, 1f, 1f);
    private static readonly Vector4 HealthHighColor = new(0.2f, 0.82f, 0.43f, 1f);
    private static readonly Vector4 HealthMediumColor = new(1f, 0.68f, 0.12f, 1f);
    private static readonly Vector4 HealthLowColor = new(1f, 0.12f, 0.2f, 1f);
    private static readonly Vector4 ManaColor = new(0.18f, 0.48f, 1f, 1f);
    private static readonly Vector4 ProtectionColor = new(0.66f, 0.28f, 1f, 1f);
    private static readonly Vector4 TextColor = new(0.96f, 0.98f, 1f, 1f);
    private static readonly Vector4 OpaqueBlack = new(0f, 0f, 0f, 1f);
    private static readonly CombatFrameActorSnapshot[] UnknownEnemies =
        CombatFrameRules.CreateUnknownEnemyRows()
            .Select(static row => CombatFrameActorSnapshot.Unknown(row.Slot))
            .ToArray();

    private readonly CombatFramesSnapshotService snapshots;
    private readonly IGameGui gameGui;
    private readonly ITextureProvider textureProvider;
    private readonly IPluginLog log;
    private readonly Func<CombatFramesOptions> optionsProvider;
    private long nextErrorLogAtMilliseconds;
    private int previewEnabled;

    internal CombatFramesRenderer(
        CombatFramesSnapshotService snapshots,
        IGameGui gameGui,
        ITextureProvider textureProvider,
        IPluginLog log,
        Func<CombatFramesOptions> optionsProvider)
    {
        this.snapshots = snapshots;
        this.gameGui = gameGui;
        this.textureProvider = textureProvider;
        this.log = log;
        this.optionsProvider = optionsProvider ?? throw new ArgumentNullException(nameof(optionsProvider));
    }

    /// <summary>
    /// Ephemeral settings-preview state. The integration layer must clear it
    /// when settings close or the plugin master switch is turned off.
    /// </summary>
    internal bool PreviewEnabled
    {
        get => Volatile.Read(ref previewEnabled) != 0;
        set => Volatile.Write(ref previewEnabled, value ? 1 : 0);
    }

    internal void Draw()
    {
        if (gameGui.GameUiHidden) return;

        try
        {
            var options = Sanitize(optionsProvider());
            if (PreviewEnabled) options = options with { PreviewEnabled = true };
            if (!options.Enabled && !options.PreviewEnabled) return;

            var now = Environment.TickCount64;
            var snapshot = options.PreviewEnabled ? BuildPreview(now) : snapshots.Snapshot;
            if (!snapshot.Active) return;

            var fresh = CombatFrameRules.IsSnapshotFresh(snapshot.PublishedAtMilliseconds, now);
            var self = fresh
                ? snapshot.Self
                : CombatFrameActorSnapshot.Unknown(CombatFrameRules.SelfSlot);
            var enemies = fresh ? snapshot.Enemies : UnknownEnemies;
            DrawFrames(self, enemies, options, now);
        }
        catch (Exception exception)
        {
            var now = Environment.TickCount64;
            if (now < nextErrorLogAtMilliseconds) return;
            nextErrorLogAtMilliseconds = now + 10_000;
            log.Error(exception, "Seiton Sense combat-frame renderer failed closed.");
        }
    }

    private void DrawFrames(
        CombatFrameActorSnapshot self,
        IReadOnlyList<CombatFrameActorSnapshot> enemies,
        CombatFramesOptions options,
        long nowMilliseconds)
    {
        var uiScale = Math.Max(0.5f, ImGuiHelpers.GlobalScale);
        var scale = options.Scale * uiScale;
        var screen = ImGui.GetIO().DisplaySize;
        if (screen.X <= 0f || screen.Y <= 0f) return;

        var draw = ImGui.GetBackgroundDrawList();
        var enemySize = new Vector2(EnemyWidth, EnemyHeight) * scale;
        var enemyGap = EnemyGap * scale;
        var stackHeight = (enemySize.Y * CombatFrameRules.EnemySlotCount) +
                          (enemyGap * (CombatFrameRules.EnemySlotCount - 1));
        var enemyTopLeft = CenteredTopLeft(
            screen,
            new Vector2(options.EnemyScreenX, options.EnemyScreenY),
            new Vector2(enemySize.X, stackHeight),
            8f * uiScale);
        for (var index = 0; index < CombatFrameRules.EnemySlotCount; index++)
        {
            var topLeft = PixelSnap(enemyTopLeft + new Vector2(0f, index * (enemySize.Y + enemyGap)));
            DrawActorFrame(draw, enemies[index], topLeft, enemySize, false, options, scale, nowMilliseconds);
        }

        var selfSize = new Vector2(SelfWidth, SelfHeight) * scale;
        var selfTopLeft = CenteredTopLeft(
            screen,
            new Vector2(options.SelfScreenX, options.SelfScreenY),
            selfSize,
            8f * uiScale);
        DrawActorFrame(draw, self, PixelSnap(selfTopLeft), selfSize, true, options, scale, nowMilliseconds);
    }

    private void DrawActorFrame(
        ImDrawListPtr draw,
        CombatFrameActorSnapshot actor,
        Vector2 topLeft,
        Vector2 size,
        bool self,
        CombatFramesOptions options,
        float scale,
        long nowMilliseconds)
    {
        var bottomRight = topLeft + size;
        var frame = actor.Frame;
        var rounding = 8f * scale;
        var border = ResolveBorderColor(frame, self);
        draw.AddRectFilled(
            topLeft,
            bottomRight,
            Pack(BackgroundColor, options.BackgroundOpacity),
            rounding);
        draw.AddRect(
            topLeft,
            bottomRight,
            Pack(border, 0.96f),
            rounding,
            ImDrawFlags.None,
            Math.Max(1.5f, 2f * scale));

        if (frame.IsFocusTarget)
        {
            draw.AddRectFilled(
                topLeft,
                new Vector2(bottomRight.X, topLeft.Y + (4f * scale)),
                Pack(FocusTargetColor, 1f),
                rounding);
        }

        var iconSize = (self ? 60f : 48f) * scale;
        var iconMin = topLeft + new Vector2(10f, self ? 16f : 13f) * scale;
        var iconMax = iconMin + new Vector2(iconSize);
        DrawJobIcon(draw, frame.JobId, iconMin, iconMax, frame.Availability);

        var contentLeft = iconMax.X + (10f * scale);
        var badgeWidth = (self ? 126f : 114f) * scale;
        var contentRight = Math.Max(contentLeft + (80f * scale), bottomRight.X - badgeWidth);
        var header = BuildHeader(actor, self, options.ShowNames);
        DrawText(
            draw,
            new Vector2(contentLeft, topLeft.Y + (6f * scale)),
            header,
            self ? 0.88f * options.Scale : 0.76f * options.Scale,
            frame.Availability == CombatFrameAvailability.Unknown ? UnknownColor : TextColor);

        if (frame.Availability != CombatFrameAvailability.Alive)
        {
            var label = frame.Availability == CombatFrameAvailability.Dead ? "DEAD" : "UNAVAILABLE";
            DrawText(
                draw,
                new Vector2(contentLeft, topLeft.Y + ((self ? 42f : 36f) * scale)),
                label,
                0.9f * options.Scale,
                frame.Availability == CombatFrameAvailability.Dead ? DeadColor : UnknownColor);
            return;
        }

        var hpMin = new Vector2(contentLeft, topLeft.Y + ((self ? 31f : 27f) * scale));
        var hpMax = new Vector2(contentRight, hpMin.Y + ((self ? 22f : 18f) * scale));
        DrawResourceBar(
            draw,
            hpMin,
            hpMax,
            frame.HpFraction,
            HealthColor(frame.HpFraction),
            options.ShowExactValues ? $"{frame.CurrentHp:N0} / {frame.MaximumHp:N0}" : $"{frame.HpFraction:P0}",
            scale,
            0.68f * options.Scale,
            []);

        var mpMin = new Vector2(contentLeft, hpMax.Y + (6f * scale));
        var mpMax = new Vector2(contentRight, mpMin.Y + ((self ? 16f : 13f) * scale));
        if (frame.HasTrustedMp)
        {
            var divisions = BuildMpDivisions(frame.MaximumMp);
            DrawResourceBar(
                draw,
                mpMin,
                mpMax,
                frame.MpFraction,
                ManaColor,
                options.ShowExactValues
                    ? $"MP {frame.CurrentMp:N0} / {frame.MaximumMp:N0}  x{frame.AffordableRecuperates}"
                    : $"MP x{frame.AffordableRecuperates}",
                scale,
                0.68f * options.Scale,
                divisions);
        }
        else
        {
            DrawResourceBar(draw, mpMin, mpMax, 0f, ManaColor, "MP —", scale, 0.68f * options.Scale, []);
        }

        DrawRightBadges(draw, actor, topLeft, bottomRight, self, options, scale, nowMilliseconds);
    }

    private void DrawRightBadges(
        ImDrawListPtr draw,
        CombatFrameActorSnapshot actor,
        Vector2 topLeft,
        Vector2 bottomRight,
        bool self,
        CombatFramesOptions options,
        float scale,
        long nowMilliseconds)
    {
        var frame = actor.Frame;
        var iconSize = (self ? 28f : 24f) * scale;
        var gap = 4f * scale;
        var iconRight = bottomRight.X - (8f * scale);
        var iconTop = topLeft.Y + (7f * scale);
        var iconIndex = 0;

        if (options.ShowStatuses)
        {
            foreach (var status in actor.Statuses
                         .Where(status => status.ExpiresAtMilliseconds > nowMilliseconds)
                         .Take(MaximumStatusIcons))
            {
                var max = new Vector2(iconRight - (iconIndex * (iconSize + gap)), iconTop + iconSize);
                var min = max - new Vector2(iconSize);
                DrawStatusBadge(draw, min, max, status, scale, options.Scale, nowMilliseconds);
                iconIndex++;
            }

            var hasActiveGuard = actor.Statuses.Any(status =>
                status.StatusId == EnemyCombatConstants.GuardStatusId &&
                status.ExpiresAtMilliseconds > nowMilliseconds);
            if (!hasActiveGuard &&
                iconIndex < MaximumStatusIcons + 1 &&
                actor.GuardUnavailable &&
                actor.GuardReadyAtMilliseconds > nowMilliseconds)
            {
                var max = new Vector2(iconRight - (iconIndex * (iconSize + gap)), iconTop + iconSize);
                var min = max - new Vector2(iconSize);
                DrawIconBadge(
                    draw,
                    min,
                    max,
                    EnemyCombatConstants.GuardIconId,
                    new Vector4(0.25f, 0.72f, 1f, 1f),
                    Math.Ceiling((actor.GuardReadyAtMilliseconds - nowMilliseconds) / 1000d).ToString("0"),
                    true,
                    scale,
                    options.Scale);
                iconIndex++;
            }

            if (iconIndex < MaximumStatusIcons + 1 && actor.SeitonEligible)
            {
                var max = new Vector2(iconRight - (iconIndex * (iconSize + gap)), iconTop + iconSize);
                var min = max - new Vector2(iconSize);
                DrawIconBadge(
                    draw,
                    min,
                    max,
                    EnemyCombatConstants.SeitonIconId,
                    new Vector4(1f, 0.2f, 0.54f, 1f),
                    null,
                    false,
                    scale,
                    options.Scale);
            }
        }

        if (!options.ShowPressure) return;

        var badgeY = bottomRight.Y - ((self ? 29f : 25f) * scale);
        var badgeRight = bottomRight.X - (8f * scale);
        if (!frame.PressureTrusted)
        {
            DrawTextBadge(
                draw,
                new Vector2(badgeRight - ((self ? 92f : 40f) * scale), badgeY),
                new Vector2(badgeRight, bottomRight.Y - (6f * scale)),
                self ? "FOCUS ?" : "P?",
                UnknownColor,
                scale,
                options.Scale);
            return;
        }

        if (self && frame.DirectPressureCount > 0)
        {
            DrawTextBadge(
                draw,
                new Vector2(badgeRight - (92f * scale), badgeY),
                new Vector2(badgeRight, bottomRight.Y - (6f * scale)),
                $"FOCUS x{frame.DirectPressureCount}",
                frame.DirectPressureCount >= 3 ? PressureColor : PressureMediumColor,
                scale,
                options.Scale);
            return;
        }

        var incomingLabel = IncomingLabel(frame.IncomingEvidence);
        if (!string.IsNullOrEmpty(incomingLabel))
        {
            var width = 48f * scale;
            DrawTextBadge(
                draw,
                new Vector2(badgeRight - width, badgeY),
                new Vector2(badgeRight, bottomRight.Y - (6f * scale)),
                incomingLabel,
                incomingLabel == "HIT" ? PressureMediumColor : PressureColor,
                scale,
                options.Scale);
            badgeRight -= width + (4f * scale);
        }

        if (frame.TeamTargetCount > 0)
        {
            DrawTextBadge(
                draw,
                new Vector2(badgeRight - (40f * scale), badgeY),
                new Vector2(badgeRight, bottomRight.Y - (6f * scale)),
                $"P{frame.TeamTargetCount}",
                TeamPressureColor,
                scale,
                options.Scale);
        }
    }

    private void DrawStatusBadge(
        ImDrawListPtr draw,
        Vector2 minimum,
        Vector2 maximum,
        CombatFrameStatusSnapshot status,
        float scale,
        float configuredScale,
        long nowMilliseconds)
    {
        var accent = status.Category switch
        {
            CombatFrameStatusCategory.CrowdControl => PressureColor,
            CombatFrameStatusCategory.Danger => PressureMediumColor,
            _ => ProtectionColor,
        };
        var countdown = CcProtectionCountdownFormatter.Format(
            (status.ExpiresAtMilliseconds - nowMilliseconds) / 1000f);
        DrawIconBadge(draw, minimum, maximum, status.IconId, accent, countdown, false, scale, configuredScale);
    }

    private void DrawIconBadge(
        ImDrawListPtr draw,
        Vector2 minimum,
        Vector2 maximum,
        uint iconId,
        Vector4 accent,
        string? countdown,
        bool crossed,
        float scale,
        float configuredScale)
    {
        draw.AddRectFilled(minimum, maximum, Pack(new Vector4(0.025f, 0.035f, 0.065f, 1f), 0.98f), 4f * scale);
        if (!TryDrawGameIcon(draw, iconId, minimum, maximum))
            draw.AddRectFilled(minimum + new Vector2(2f * scale), maximum - new Vector2(2f * scale), Pack(accent, 0.28f), 3f * scale);
        draw.AddRect(minimum, maximum, Pack(accent, 1f), 4f * scale, ImDrawFlags.None, Math.Max(1f, 1.5f * scale));

        if (crossed)
        {
            draw.AddLine(minimum + new Vector2(3f * scale), maximum - new Vector2(3f * scale), Pack(PressureColor, 1f), Math.Max(2f, 3f * scale));
            draw.AddLine(
                new Vector2(maximum.X - (3f * scale), minimum.Y + (3f * scale)),
                new Vector2(minimum.X + (3f * scale), maximum.Y - (3f * scale)),
                Pack(PressureColor, 1f),
                Math.Max(2f, 3f * scale));
        }

        if (string.IsNullOrEmpty(countdown)) return;
        var font = ImGui.GetFont();
        var fontScale = Math.Max(0.35f, 0.62f * configuredScale);
        var fontSize = ImGui.GetFontSize() * fontScale;
        var textSize = ImGui.CalcTextSize(countdown) * fontScale;
        var textPosition = new Vector2(
            maximum.X - textSize.X - (2f * scale),
            maximum.Y - textSize.Y - (1f * scale));
        draw.AddRectFilled(textPosition - new Vector2(1f * scale), maximum, Pack(OpaqueBlack, 0.82f), 2f * scale);
        draw.AddText(font, fontSize, textPosition, 0xFFFFFFFF, countdown);
    }

    private static void DrawResourceBar(
        ImDrawListPtr draw,
        Vector2 minimum,
        Vector2 maximum,
        float fraction,
        Vector4 fill,
        string label,
        float scale,
        float fontScale,
        IReadOnlyList<float> divisions)
    {
        var rounding = 3f * scale;
        draw.AddRectFilled(minimum, maximum, Pack(new Vector4(0.035f, 0.05f, 0.085f, 1f), 1f), rounding);
        if (fraction > 0f)
        {
            draw.AddRectFilled(
                minimum,
                new Vector2(minimum.X + ((maximum.X - minimum.X) * Math.Clamp(fraction, 0f, 1f)), maximum.Y),
                Pack(fill, 0.94f),
                rounding);
        }

        foreach (var division in divisions)
        {
            var x = minimum.X + ((maximum.X - minimum.X) * division);
            draw.AddLine(new Vector2(x, minimum.Y), new Vector2(x, maximum.Y), Pack(OpaqueBlack, 0.72f), Math.Max(1f, scale));
        }

        var font = ImGui.GetFont();
        var safeFontScale = Math.Max(0.35f, fontScale);
        var fontSize = ImGui.GetFontSize() * safeFontScale;
        var labelSize = ImGui.CalcTextSize(label) * safeFontScale;
        var text = new Vector2(
            minimum.X + Math.Max(3f * scale, ((maximum.X - minimum.X) - labelSize.X) * 0.5f),
            minimum.Y + Math.Max(0f, ((maximum.Y - minimum.Y) - labelSize.Y) * 0.5f));
        draw.AddText(
            font,
            fontSize,
            text + new Vector2(Math.Max(1f, scale)),
            Pack(OpaqueBlack, 0.92f),
            label);
        draw.AddText(font, fontSize, text, 0xFFFFFFFF, label);
    }

    private static void DrawTextBadge(
        ImDrawListPtr draw,
        Vector2 minimum,
        Vector2 maximum,
        string label,
        Vector4 accent,
        float scale,
        float configuredScale)
    {
        draw.AddRectFilled(minimum, maximum, Pack(new Vector4(0.015f, 0.022f, 0.045f, 1f), 0.98f), 4f * scale);
        draw.AddRect(minimum, maximum, Pack(accent, 1f), 4f * scale, ImDrawFlags.None, Math.Max(1f, 1.5f * scale));
        var font = ImGui.GetFont();
        var fontScale = Math.Max(0.35f, 0.64f * configuredScale);
        var fontSize = ImGui.GetFontSize() * fontScale;
        var textSize = ImGui.CalcTextSize(label) * fontScale;
        var text = new Vector2(
            minimum.X + ((maximum.X - minimum.X - textSize.X) * 0.5f),
            minimum.Y + ((maximum.Y - minimum.Y - textSize.Y) * 0.5f));
        draw.AddText(font, fontSize, text, 0xFFFFFFFF, label);
    }

    private void DrawJobIcon(
        ImDrawListPtr draw,
        uint jobId,
        Vector2 minimum,
        Vector2 maximum,
        CombatFrameAvailability availability)
    {
        var alpha = availability == CombatFrameAvailability.Alive ? 1f : 0.38f;
        if (jobId == 0 || !TryDrawGameIcon(draw, EnemyCombatConstants.JobIconBaseId + jobId, minimum, maximum, alpha))
        {
            draw.AddRectFilled(minimum, maximum, Pack(new Vector4(0.1f, 0.12f, 0.18f, 1f), alpha), 6f);
            draw.AddRect(minimum, maximum, Pack(UnknownColor, alpha), 6f, ImDrawFlags.None, 1.5f);
        }
    }

    private bool TryDrawGameIcon(
        ImDrawListPtr draw,
        uint iconId,
        Vector2 minimum,
        Vector2 maximum,
        float alpha = 1f)
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
            Pack(new Vector4(1f, 1f, 1f, 1f), alpha));
        return true;
    }

    private static string BuildHeader(CombatFrameActorSnapshot actor, bool self, bool showName)
    {
        var prefix = self ? "SELF" : $"S{actor.Frame.Slot}";
        if (!showName || string.IsNullOrWhiteSpace(actor.DisplayName)) return prefix;
        var name = actor.DisplayName.Length <= 20
            ? actor.DisplayName
            : actor.DisplayName[..19] + "…";
        return $"{prefix}  {name}";
    }

    private static string IncomingLabel(CombatFrameIncomingEvidence evidence)
    {
        if ((evidence & CombatFrameIncomingEvidence.LimitBreakMarker) != 0) return "LB";
        if ((evidence & (CombatFrameIncomingEvidence.HardTarget | CombatFrameIncomingEvidence.CastTarget)) != 0)
            return "YOU";
        return (evidence & CombatFrameIncomingEvidence.RecentHarmfulAction) != 0 ? "HIT" : string.Empty;
    }

    private static IReadOnlyList<float> BuildMpDivisions(uint maximumMp)
    {
        if (maximumMp != CombatFrameRules.ExpectedMaximumMp) return Array.Empty<float>();

        var divisions = new List<float>(4);
        for (var value = LowMpRules.RecuperateCost;
             value < (int)CombatFrameRules.ExpectedMaximumMp;
             value += LowMpRules.RecuperateCost)
        {
            divisions.Add(value / (float)maximumMp);
        }

        return divisions;
    }

    private static Vector4 ResolveBorderColor(CombatFramePlanRow frame, bool self)
    {
        if (frame.Availability == CombatFrameAvailability.Unknown) return UnknownColor;
        if (frame.Availability == CombatFrameAvailability.Dead) return DeadColor;
        if (self && frame.DirectPressureCount >= 3) return PressureColor;
        if (self && frame.DirectPressureCount >= 2) return PressureMediumColor;
        if (frame.IsCurrentTarget) return CurrentTargetColor;
        if (frame.IsFocusTarget) return FocusTargetColor;
        return self ? SelfColor : new Vector4(0.36f, 0.48f, 0.66f, 1f);
    }

    private static Vector4 HealthColor(float fraction) => fraction switch
    {
        <= 0.3f => HealthLowColor,
        <= 0.6f => HealthMediumColor,
        _ => HealthHighColor,
    };

    private static CombatFramesOptions Sanitize(CombatFramesOptions options) => options with
    {
        EnemyScreenX = SafeFloat(options.EnemyScreenX, 0.82f, 0.02f, 0.98f),
        EnemyScreenY = SafeFloat(options.EnemyScreenY, 0.48f, 0.02f, 0.98f),
        SelfScreenX = SafeFloat(options.SelfScreenX, 0.5f, 0.02f, 0.98f),
        SelfScreenY = SafeFloat(options.SelfScreenY, 0.78f, 0.02f, 0.98f),
        Scale = SafeFloat(options.Scale, 1f, 0.55f, 1.8f),
        BackgroundOpacity = SafeFloat(options.BackgroundOpacity, 0.92f, 0.35f, 1f),
    };

    private static Vector2 CenteredTopLeft(
        Vector2 screen,
        Vector2 normalizedCenter,
        Vector2 size,
        float padding)
    {
        var result = new Vector2(screen.X * normalizedCenter.X, screen.Y * normalizedCenter.Y) - (size * 0.5f);
        result.X = Math.Clamp(result.X, padding, Math.Max(padding, screen.X - size.X - padding));
        result.Y = Math.Clamp(result.Y, padding, Math.Max(padding, screen.Y - size.Y - padding));
        return PixelSnap(result);
    }

    private static CombatFramesSnapshot BuildPreview(long nowMilliseconds)
    {
        var selfRow = CombatFrameRules.BuildSelfRow(new CombatFrameObservation(
            0,
            new TargetPressureActorIdentity(10, 20),
            40,
            34_000,
            60_000,
            6_000,
            10_000,
            true,
            false,
            true,
            true,
            false,
            false,
            3,
            0,
            CombatFrameIncomingEvidence.None));
        var enemyRows = CombatFrameRules.BuildEnemyRows(
        [
            PreviewObservation(1, 19, 44_000, 62_000, 8_000, false, false, 1, CombatFrameIncomingEvidence.None),
            PreviewObservation(2, 30, 21_000, 52_000, 2_000, true, false, 3, CombatFrameIncomingEvidence.HardTarget),
            PreviewObservation(3, 31, 13_000, 48_000, 0, false, false, 0, CombatFrameIncomingEvidence.LimitBreakMarker),
            PreviewObservation(4, 28, 39_000, 50_000, 4_000, false, true, 2, CombatFrameIncomingEvidence.RecentHarmfulAction),
            PreviewObservation(5, 41, 0, 54_000, 0, false, false, 0, CombatFrameIncomingEvidence.None, true),
        ]);
        var enemies = enemyRows
            .Select(row => new CombatFrameActorSnapshot(
                row,
                $"Preview {row.Slot}",
                row.Slot == 3,
                row.Slot == 3 ? nowMilliseconds + 14_000 : -1,
                row.Slot == 2,
                row.Slot == 1
                    ? Array.AsReadOnly(new[]
                    {
                        new CombatFrameStatusSnapshot(
                            EnemyCombatConstants.GuardStatusId,
                            "Guard",
                            214890,
                            CombatFrameStatusCategory.Protection,
                            nowMilliseconds + 3_200),
                    })
                    : Array.Empty<CombatFrameStatusSnapshot>()))
            .ToArray();
        var self = new CombatFrameActorSnapshot(
            selfRow,
            "Preview Self",
            false,
            -1,
            false,
            Array.AsReadOnly(new[]
            {
                new CombatFrameStatusSnapshot(
                    EnemyCombatConstants.PvPStunStatusId,
                    "Stun",
                    EnemyCombatConstants.StunStatusIconId,
                    CombatFrameStatusCategory.CrowdControl,
                    nowMilliseconds + 2_400),
            }));
        return new CombatFramesSnapshot(true, nowMilliseconds, self, enemies);
    }

    private static CombatFrameObservation PreviewObservation(
        int slot,
        uint job,
        uint hp,
        uint maxHp,
        uint mp,
        bool current,
        bool focus,
        int teamPressure,
        CombatFrameIncomingEvidence incoming,
        bool dead = false) => new(
        slot,
        new TargetPressureActorIdentity((ulong)(100 + slot), (uint)(200 + slot)),
        job,
        hp,
        maxHp,
        mp,
        10_000,
        slot != 3 || mp > 0,
        dead,
        !dead,
        true,
        current,
        focus,
        0,
        teamPressure,
        incoming);

    private static void DrawText(
        ImDrawListPtr draw,
        Vector2 position,
        string text,
        float fontScale,
        Vector4 color)
    {
        var font = ImGui.GetFont();
        var fontSize = ImGui.GetFontSize() * Math.Max(0.35f, fontScale);
        var shadow = Math.Max(1f, ImGuiHelpers.GlobalScale);
        draw.AddText(font, fontSize, position + new Vector2(shadow), Pack(OpaqueBlack, 0.96f), text);
        draw.AddText(font, fontSize, position, Pack(color, 1f), text);
    }

    private static float SafeFloat(float value, float fallback, float minimum, float maximum) =>
        Math.Clamp(float.IsFinite(value) ? value : fallback, minimum, maximum);

    private static Vector2 PixelSnap(Vector2 value) => new(MathF.Round(value.X), MathF.Round(value.Y));

    private static uint Pack(Vector4 color, float alpha)
    {
        var safe = new Vector4(
            SafeFloat(color.X, 1f, 0f, 1f),
            SafeFloat(color.Y, 1f, 0f, 1f),
            SafeFloat(color.Z, 1f, 0f, 1f),
            SafeFloat(color.W, 1f, 0f, 1f));
        safe.W *= Math.Clamp(alpha, 0f, 1f);
        return ImGui.ColorConvertFloat4ToU32(safe);
    }
}
