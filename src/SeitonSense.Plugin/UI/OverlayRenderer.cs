using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;
using SeitonSense.Core;
using SeitonSense.Plugin.Models;
using SeitonSense.Plugin.Services;

namespace SeitonSense.Plugin.UI;

internal sealed class OverlayRenderer
{
    private const long MaximumAnchorAgeMilliseconds = 250;

    private static readonly Vector4 SeitonColor = new(1f, 0.2f, 0.54f, 1f);
    private static readonly Vector4 SeitonPreparationColor = new(1f, 0.68f, 0.13f, 1f);
    private static readonly Vector4 GuardColor = new(0.25f, 0.72f, 1f, 1f);
    private static readonly Vector4 ManaColor = new(0.24f, 0.48f, 1f, 1f);
    private static readonly Vector4 WarningColor = new(1f, 0.43f, 0.1f, 1f);
    private static readonly Vector4 CleanseColor = new(0.34f, 0.82f, 1f, 1f);
    private static readonly Vector4 CrossColor = new(1f, 0.12f, 0.12f, 1f);
    private static readonly Vector4 TextColor = new(1f, 0.98f, 1f, 1f);
    private static readonly Vector4 ShadowColor = new(0f, 0f, 0f, 0.96f);

    private readonly PluginConfiguration configuration;
    private readonly ExecuteTracker tracker;
    private readonly PersonalStatusService personalStatus;
    private readonly NamePlateAnchorTracker namePlateAnchors;
    private readonly IGameGui gameGui;
    private readonly ITextureProvider textureProvider;
    private SeitonPopupSnapshot? previewPopup;

    public OverlayRenderer(
        PluginConfiguration configuration,
        ExecuteTracker tracker,
        PersonalStatusService personalStatus,
        NamePlateAnchorTracker namePlateAnchors,
        IGameGui gameGui,
        ITextureProvider textureProvider)
    {
        this.configuration = configuration;
        this.tracker = tracker;
        this.personalStatus = personalStatus;
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

        if (!configuration.Enabled) return;

        var now = Environment.TickCount64;
        if (tracker.IsActive)
        {
            DrawLiveNameplateIndicators(now);
            DrawLiveSeitonDecisionStack(now);
        }

        if (configuration.ShowPersonalWarnings)
            DrawPersonalWarnings(now);
    }

    private void DrawPersonalWarnings(long now)
    {
        var personal = personalStatus.Snapshot;
        if (!personal.Active) return;

        var statuses = personal.Statuses
            .Where(status => status.ExpiresAtMilliseconds > now)
            .Take(4)
            .ToArray();
        for (var index = 0; index < statuses.Length; index++)
            DrawPersonalWarningCard(statuses[index], personal.Purify, index, statuses.Length, now);
    }

    private void DrawPersonalWarningCard(
        PersonalStatusSnapshot status,
        EmergencyPurifyProbeSnapshot purify,
        int index,
        int count,
        long now)
    {
        var remaining = Math.Max(0, status.ExpiresAtMilliseconds - now);
        if (remaining <= 0) return;

        var uiScale = ImGuiHelpers.GlobalScale;
        var configuredScale = Math.Clamp(configuration.PersonalWarningScale, 0.55f, 1.8f);
        var pulseAge = status.PulseStartedAtMilliseconds < 0
            ? long.MaxValue
            : Math.Max(0, now - status.PulseStartedAtMilliseconds);
        var pulse = status.IsEntryPulseActive(now)
            ? 1f - (pulseAge / (float)PersonalStatusSnapshot.EntryPulseDurationMilliseconds)
            : 0f;
        var scale = configuredScale * uiScale;
        var pulseScale = 1f + (pulse * 0.1f);
        var cardSize = new Vector2(286f, 64f) * scale * pulseScale;
        var screen = ImGui.GetIO().DisplaySize;
        var center = new Vector2(
            screen.X * Math.Clamp(configuration.PersonalWarningScreenX, 0.05f, 0.95f),
            screen.Y * Math.Clamp(configuration.PersonalWarningScreenY, 0.08f, 0.9f));
        center.Y += (index - ((count - 1) * 0.5f)) * (70f * scale);

        var topLeft = center - (cardSize * 0.5f);
        var bottomRight = center + (cardSize * 0.5f);
        var accent = status.AlertKind == PersonalDebuffAlertKind.CleanseUrgent
            ? CleanseColor
            : WarningColor;
        var draw = ImGui.GetForegroundDrawList();
        var rounding = 10f * scale;
        draw.AddRectFilled(
            topLeft,
            bottomRight,
            Pack(new Vector4(0.018f, 0.012f, 0.026f, 0.92f)),
            rounding);
        draw.AddRect(
            topLeft,
            bottomRight,
            Pack(new Vector4(accent.X, accent.Y, accent.Z, 1f)),
            rounding,
            ImDrawFlags.None,
            Math.Max(2f, (2.4f + (pulse * 2.5f)) * scale));

        var iconSize = 44f * scale;
        var iconMin = new Vector2(topLeft.X + (11f * scale), center.Y - (iconSize * 0.5f));
        var iconMax = iconMin + new Vector2(iconSize);
        if (!TryDrawGameIcon(draw, status.IconId, iconMin, iconMax, 1f))
        {
            draw.AddRectFilled(
                iconMin,
                iconMax,
                Pack(new Vector4(accent.X * 0.22f, accent.Y * 0.22f, accent.Z * 0.22f, 1f)),
                5f * scale);
        }

        var textCenterX = iconMax.X + ((bottomRight.X - iconMax.X) * 0.5f) - (4f * scale);
        var title = status.StatusId switch
        {
            EnemyCombatConstants.DeathWarrantStatusId => "DEATH WARRANT / RICHTBEFEHL",
            EnemyCombatConstants.MiracleOfNatureStatusId => "MIRACLE OF NATURE",
            _ => status.Name.ToUpperInvariant(),
        };
        var seconds = remaining / 1000f;
        var countdown = seconds < 10f ? $"{seconds:0.0}s" : $"{Math.Ceiling(seconds):0}s";
        var subtitle = BuildPersonalWarningSubtitle(status, purify, countdown);
        var titleScale = title.Length > 20 ? 0.72f : 0.93f;
        DrawOutlinedText(
            draw,
            new Vector2(textCenterX, center.Y - (19f * scale)),
            title,
            titleScale * configuredScale,
            true);
        DrawOutlinedText(
            draw,
            new Vector2(textCenterX, center.Y + (10f * scale)),
            subtitle,
            0.8f * configuredScale,
            true);
    }

    private string BuildPersonalWarningSubtitle(
        PersonalStatusSnapshot status,
        EmergencyPurifyProbeSnapshot purify,
        string countdown)
    {
        if (status.AlertKind != PersonalDebuffAlertKind.CleanseUrgent)
            return $"DANGER  •  {countdown}";
        if (!configuration.ExperimentalPurifyOnNextKey)
            return $"PURIFY!  •  {countdown}";

        var matchesTracked = purify.StatusInstance is { } tracked &&
                             tracked.StatusId == status.StatusId &&
                             tracked.InstanceToken == status.InstanceToken;
        if (!matchesTracked) return $"PURIFY!  •  {countdown}";

        return purify.Phase switch
        {
            EmergencyPurifyBufferPhase.WaitingForFreshKey => $"PRESS A KEY → PURIFY  •  {countdown}",
            EmergencyPurifyBufferPhase.Buffered =>
                $"PURIFY BUFFERED {Math.Max(0, purify.BufferRemainingMilliseconds) / 1000f:0.0}s  •  {countdown}",
            EmergencyPurifyBufferPhase.SpentUntilStatusGone => $"PURIFY WINDOW CLOSED  •  {countdown}",
            _ => $"PURIFY!  •  {countdown}",
        };
    }

    private void DrawLiveSeitonDecisionStack(long now)
    {
        var cards = new List<LiveSeitonDecisionCard>(10);
        var persistentByObjectId = configuration.ShowPersistentSeitonCue
            ? tracker.Enemies
                .Where(static enemy => enemy.SeitonCue != SeitonCueKind.Hidden)
                .GroupBy(static enemy => enemy.GameObjectId)
                .ToDictionary(
                    static group => group.Key,
                    static group => group
                        .OrderByDescending(enemy => enemy.SeitonCue)
                        .ThenBy(enemy => enemy.Slot)
                        .First())
            : [];

        foreach (var enemy in persistentByObjectId.Values)
        {
            var age = enemy.SeitonPulseStartedAtMilliseconds < 0
                ? long.MaxValue
                : Math.Max(0, now - enemy.SeitonPulseStartedAtMilliseconds);
            var pulse = age < 260 ? 1f - (age / 260f) : 0f;
            cards.Add(new LiveSeitonDecisionCard(
                enemy.GameObjectId,
                enemy.Slot,
                enemy.JobId,
                enemy.SeitonCue,
                LiveSeitonDecisionSource.PersistentCue,
                configuration.PersistentCueScale,
                1f,
                pulse));
        }

        if (configuration.ShowSeitonPopup)
        {
            foreach (var popup in tracker.Popups.Where(popup =>
                         now >= popup.StartedAtMilliseconds &&
                         now < popup.EndsAtMilliseconds))
            {
                var duration = Math.Max(1L, popup.EndsAtMilliseconds - popup.StartedAtMilliseconds);
                var progress = Math.Clamp(
                    (now - popup.StartedAtMilliseconds) / (float)duration,
                    0f,
                    1f);
                var settle = MathF.Exp(-progress * 10f);
                var hasPersistentHandoff = persistentByObjectId.ContainsKey(popup.GameObjectId);
                var alpha = hasPersistentHandoff || progress < 0.78f
                    ? 1f
                    : Math.Clamp((1f - progress) / 0.22f, 0f, 1f);
                var popupScale = Math.Clamp(configuration.PopupIconSize / 88f, 0.55f, 1.75f);
                var configuredScale = hasPersistentHandoff
                    ? SmoothStep(
                        popupScale,
                        Math.Clamp(configuration.PersistentCueScale, 0.55f, 1.8f),
                        progress)
                    : popupScale;
                cards.Add(new LiveSeitonDecisionCard(
                    popup.GameObjectId,
                    popup.Slot,
                    popup.JobId,
                    SeitonCueKind.Execute,
                    LiveSeitonDecisionSource.EntryPopup,
                    configuredScale,
                    alpha,
                    Math.Clamp(settle * 1.2f, 0f, 1f)));
            }
        }

        var selected = MergeLiveSeitonCards(cards);
        if (selected.Length == 0) return;
        var heights = selected
            .Select(static card => DecisionCardHeight(card.ConfiguredScale, card.Pulse))
            .ToArray();
        var offsets = BuildCenteredOffsets(
            heights,
            6f * ImGuiHelpers.GlobalScale);
        var stackCenterY = ImGui.GetIO().DisplaySize.Y *
                           Math.Clamp(configuration.PopupScreenY, 0.08f, 0.9f);

        for (var index = 0; index < selected.Length; index++)
        {
            var card = selected[index];
            DrawSeitonDecisionCard(
                card.Slot,
                card.JobId,
                card.Cue,
                0,
                1,
                card.ConfiguredScale,
                card.Alpha,
                card.Pulse,
                stackCenterY + offsets[index]);
        }
    }

    private static float DecisionCardHeight(float configuredScale, float pulse)
    {
        var scale = Math.Clamp(configuredScale, 0.55f, 1.8f) * ImGuiHelpers.GlobalScale;
        var pulseScale = 1f + (Math.Clamp(pulse, 0f, 1f) * 0.13f);
        return 76f * scale * pulseScale;
    }

    private static float SmoothStep(float start, float end, float progress)
    {
        var value = Math.Clamp(progress, 0f, 1f);
        value = value * value * (3f - (2f * value));
        return start + ((end - start) * value);
    }

    private static LiveSeitonDecisionCard[] MergeLiveSeitonCards(
        IReadOnlyList<LiveSeitonDecisionCard> cards)
    {
        var selected = new Dictionary<ulong, LiveSeitonDecisionCard>();
        foreach (var card in cards)
        {
            if (card.GameObjectId == 0 ||
                card.Slot is < 1 or > 5 ||
                card.Cue is not (SeitonCueKind.Preparation or SeitonCueKind.Execute))
            {
                continue;
            }

            if (!selected.TryGetValue(card.GameObjectId, out var current) ||
                IsPreferredForSameObject(card, current))
            {
                selected[card.GameObjectId] = card;
            }
        }

        return selected.Values
            .OrderByDescending(static card => card.Cue)
            .ThenBy(static card => card.Slot)
            .ThenByDescending(static card => card.Source)
            .ThenBy(static card => card.GameObjectId)
            .Take(5)
            .ToArray();
    }

    private static bool IsPreferredForSameObject(
        LiveSeitonDecisionCard candidate,
        LiveSeitonDecisionCard current)
    {
        if (candidate.Source != current.Source)
            return candidate.Source == LiveSeitonDecisionSource.EntryPopup;
        if (candidate.Cue != current.Cue)
            return candidate.Cue > current.Cue;
        if (candidate.Slot != current.Slot)
            return candidate.Slot < current.Slot;
        return candidate.JobId != 0 && (current.JobId == 0 || candidate.JobId < current.JobId);
    }

    private static float[] BuildCenteredOffsets(
        IReadOnlyList<float> itemHeights,
        float gap)
    {
        if (itemHeights.Count == 0) return [];

        var totalHeight = itemHeights.Sum() + (gap * (itemHeights.Count - 1));
        var offsets = new float[itemHeights.Count];
        var cursor = totalHeight * -0.5f;
        for (var index = 0; index < itemHeights.Count; index++)
        {
            offsets[index] = cursor + (itemHeights[index] * 0.5f);
            cursor += itemHeights[index] + gap;
        }

        return offsets;
    }

    private enum LiveSeitonDecisionSource
    {
        PersistentCue = 0,
        EntryPopup = 1,
    }

    private sealed record LiveSeitonDecisionCard(
        ulong GameObjectId,
        int Slot,
        uint JobId,
        SeitonCueKind Cue,
        LiveSeitonDecisionSource Source,
        float ConfiguredScale,
        float Alpha,
        float Pulse);

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
        // When the persistent cue is enabled, the entry card hands off at full
        // opacity instead of fading to zero and visibly blinking back on.
        var fade = configuration.ShowPersistentSeitonCue || progress < 0.78f
            ? 1f
            : Math.Clamp((1f - progress) / 0.22f, 0f, 1f);
        var popupScale = Math.Clamp(configuration.PopupIconSize / 88f, 0.55f, 1.75f);
        DrawSeitonDecisionCard(
            popup.Slot,
            popup.JobId,
            SeitonCueKind.Execute,
            index,
            count,
            popupScale,
            fade,
            Math.Clamp(settle * 1.2f, 0f, 1f));
    }

    private void DrawSeitonDecisionCard(
        int slot,
        uint jobId,
        SeitonCueKind cue,
        int index,
        int count,
        float configuredScale,
        float alpha,
        float pulse,
        float? centerYOverride = null)
    {
        if (slot is < 1 or > 5 || cue == SeitonCueKind.Hidden || alpha <= 0f) return;

        var uiScale = ImGuiHelpers.GlobalScale;
        var scale = Math.Clamp(configuredScale, 0.55f, 1.8f) * uiScale;
        var pulseScale = 1f + (Math.Clamp(pulse, 0f, 1f) * 0.13f);
        var cardSize = new Vector2(276f, 76f) * scale * pulseScale;
        var screen = ImGui.GetIO().DisplaySize;
        var center = new Vector2(
            screen.X * Math.Clamp(configuration.PopupScreenX, 0.05f, 0.95f),
            screen.Y * Math.Clamp(configuration.PopupScreenY, 0.08f, 0.9f));
        var spacing = (82f * scale);
        center.Y = centerYOverride ??
                   (center.Y + ((index - ((count - 1) * 0.5f)) * spacing));

        var topLeft = center - (cardSize * 0.5f);
        var bottomRight = center + (cardSize * 0.5f);
        var rounding = 11f * scale;
        var accent = cue == SeitonCueKind.Execute ? SeitonColor : SeitonPreparationColor;
        var draw = ImGui.GetForegroundDrawList();

        draw.AddRectFilled(
            topLeft,
            bottomRight,
            Pack(new Vector4(0.018f, 0.012f, 0.03f, configuration.PopupBackgroundOpacity * alpha)),
            rounding);
        draw.AddRectFilled(
            topLeft,
            new Vector2(topLeft.X + (7f * scale), bottomRight.Y),
            Pack(new Vector4(accent.X, accent.Y, accent.Z, alpha)),
            rounding);
        draw.AddRect(
            topLeft,
            bottomRight,
            Pack(new Vector4(accent.X, accent.Y, accent.Z, alpha)),
            rounding,
            ImDrawFlags.None,
            Math.Max(2f, (2.5f + (pulse * 2f)) * scale));

        var iconPadding = 12f * scale;
        var iconSize = 50f * scale;
        var iconMin = new Vector2(topLeft.X + iconPadding, center.Y - (iconSize * 0.5f));
        var iconMax = iconMin + new Vector2(iconSize);
        if (!TryDrawGameIcon(
                draw,
                EnemyCombatConstants.JobIconBaseId + jobId,
                iconMin,
                iconMax,
                alpha))
        {
            draw.AddRectFilled(
                iconMin,
                iconMax,
                Pack(new Vector4(accent.X * 0.2f, accent.Y * 0.2f, accent.Z * 0.2f, alpha)),
                6f * scale);
        }

        var key = new string((configuration.SeitonKeyLabel ?? string.Empty)
            .Where(static character => !char.IsControl(character))
            .Take(12)
            .ToArray())
            .Trim()
            .ToUpperInvariant();
        if (key.Length > 12) key = key[..12];
        var mainText = string.IsNullOrWhiteSpace(key) ? $"S{slot}" : $"{key} + {slot}";
        var mainScale = 1.72f * scale / uiScale;
        var subScale = 0.83f * scale / uiScale;
        var textCenterX = iconMax.X + ((bottomRight.X - iconMax.X) * 0.5f) - (3f * scale);
        DrawOutlinedText(
            draw,
            new Vector2(textCenterX, center.Y - (21f * scale)),
            mainText,
            mainScale,
            true,
            alpha);
        DrawOutlinedText(
            draw,
            new Vector2(textCenterX, center.Y + (13f * scale)),
            cue == SeitonCueKind.Execute ? "SEITON WINDOW" : "PREP < 60%",
            subScale,
            true,
            alpha);
    }

    private void DrawPreview()
    {
        var screen = ImGui.GetIO().DisplaySize;
        var nativeSize = 28f * ImGuiHelpers.GlobalScale;
        var nativeMin = new Vector2(screen.X * 0.54f, screen.Y * 0.38f);
        var nativeMax = nativeMin + new Vector2(nativeSize);
        var anchor = new NamePlateAnchorSnapshot(0, nativeMin, nativeMax, Environment.TickCount64);
        var enemy = new EnemyHudSnapshot(
            3,
            0,
            0,
            30,
            SeitonCueKind.Execute,
            Environment.TickCount64,
            true,
            18.4f,
            true,
            1200,
            10000);

        TryDrawGameIcon(
            ImGui.GetForegroundDrawList(),
            EnemyCombatConstants.JobIconBaseId + enemy.JobId,
            nativeMin,
            nativeMax,
            1f);
        DrawIndicatorSlots(anchor, enemy);

        var now = Environment.TickCount64;
        var warning = new PersonalStatusSnapshot(
            EnemyCombatConstants.MiracleOfNatureStatusId,
            "Miracle of Nature",
            EnemyCombatConstants.MiracleOfNatureStatusIconId,
            PersonalDebuffAlertKind.CleanseUrgent,
            1,
            1,
            6_000,
            now + 6_000,
            now,
            true);
        DrawPersonalWarningCard(
            warning,
            EmergencyPurifyProbeSnapshot.Initial with
            {
                Phase = EmergencyPurifyBufferPhase.WaitingForFreshKey,
                StatusInstance = new PurifyCcStatusInstance(
                    EnemyCombatConstants.MiracleOfNatureStatusId,
                    1),
            },
            0,
            1,
            now);
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
