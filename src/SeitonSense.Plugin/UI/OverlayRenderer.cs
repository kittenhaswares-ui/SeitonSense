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
    private static readonly Vector4 LethalWarningColor = new(1f, 0.08f, 0.22f, 1f);
    private static readonly Vector4 CleanseColor = new(0.34f, 0.82f, 1f, 1f);
    private static readonly Vector4 CrossColor = new(1f, 0.12f, 0.12f, 1f);
    private static readonly Vector4 ImmunityColor = new(0.66f, 0.28f, 1f, 1f);
    private static readonly Vector4 TeamPressureColor = new(0.12f, 0.9f, 1f, 1f);
    private static readonly Vector4 IncomingPressureColor = new(1f, 0.18f, 0.12f, 1f);
    private static readonly Vector4 RecentPressureColor = new(1f, 0.68f, 0.12f, 1f);
    private static readonly Vector4 LowHealthAuraColor = new(1f, 0.08f, 0.16f, 1f);
    private static readonly Vector4 LowManaAuraColor = new(0.12f, 0.48f, 1f, 1f);
    private static readonly Vector4 CombinedResourceAuraColor = new(0.72f, 0.16f, 1f, 1f);
    private static readonly Vector4 IsolationColor = new(1f, 0.72f, 0.18f, 1f);
    private static readonly Vector4 HighPressureColor = new(1f, 0.12f, 0.08f, 1f);
    private static readonly Vector4 TextColor = new(1f, 0.98f, 1f, 1f);
    private static readonly Vector4 ShadowColor = new(0f, 0f, 0f, 0.96f);
    private const uint WardensPaeanIconId = 9628;
    private const uint AquaveilIconId = 9607;

    private readonly PluginConfiguration configuration;
    private readonly ExecuteTracker tracker;
    private readonly PersonalStatusService personalStatus;
    private readonly TargetPressureTracker pressureTracker;
    private readonly IsolationAwarenessService isolationAwareness;
    private readonly NamePlateAnchorTracker namePlateAnchors;
    private readonly ResourceAuraAnchorTracker resourceAuraAnchors;
    private readonly IGameGui gameGui;
    private readonly ITextureProvider textureProvider;
    private SeitonPopupSnapshot? previewPopup;
    private AllyRescueConfirmationPopup? previewAllyRescueConfirmation;
    private MiracleInterceptConfirmationPopup? previewMiracleInterceptConfirmation;
    private bool previewEnabled;

    public OverlayRenderer(
        PluginConfiguration configuration,
        ExecuteTracker tracker,
        PersonalStatusService personalStatus,
        TargetPressureTracker pressureTracker,
        IsolationAwarenessService isolationAwareness,
        NamePlateAnchorTracker namePlateAnchors,
        ResourceAuraAnchorTracker resourceAuraAnchors,
        IGameGui gameGui,
        ITextureProvider textureProvider)
    {
        this.configuration = configuration;
        this.tracker = tracker;
        this.personalStatus = personalStatus;
        this.pressureTracker = pressureTracker;
        this.isolationAwareness = isolationAwareness;
        this.namePlateAnchors = namePlateAnchors;
        this.resourceAuraAnchors = resourceAuraAnchors;
        this.gameGui = gameGui;
        this.textureProvider = textureProvider;
    }

    public bool PreviewEnabled
    {
        get => previewEnabled;
        set
        {
            previewEnabled = value;
            if (value)
            {
                previewAllyRescueConfirmation = null;
                previewMiracleInterceptConfirmation = null;
            }
        }
    }
    public bool CcProtectionPreviewEnabled { get; set; }
    public bool ResourceAuraPreviewEnabled { get; set; }
    public bool IsolationWarningPreviewEnabled { get; set; }
    public bool HighPressureWarningPreviewEnabled { get; set; }
    public int NativeAnchorCount => namePlateAnchors.Anchors.Count;
    public int ResourceAuraAnchorCount => resourceAuraAnchors.LastAnchorCount;
    public int ResourceAuraSelfHotbarCount => resourceAuraAnchors.LastSelfHotbarCount;
    public int ResourceAuraPartyRowCount => resourceAuraAnchors.LastPartyRowCount;
    public int ResourceAuraCcRowCount => resourceAuraAnchors.LastCcRowCount;

    public void TriggerPreviewPopup()
    {
        var now = Environment.TickCount64;
        var duration = (long)Math.Clamp(configuration.PopupDurationMilliseconds, 300f, 2000f);
        previewPopup = new SeitonPopupSnapshot(0, 3, 30, now, now + duration);
    }

    public void TriggerAllyRescueConfirmationPreview()
    {
        PreviewEnabled = false;
        previewMiracleInterceptConfirmation = null;
        var now = Environment.TickCount64;
        previewAllyRescueConfirmation = new AllyRescueConfirmationPopup(
            AllyRescueConfirmationRules.AquaveilActionId,
            1,
            1,
            AllyRescueConfirmationRules.StunStatusId,
            now,
            now + AllyRescueConfirmationRules.PopupDurationMilliseconds);
    }

    public void TriggerMiracleInterceptConfirmationPreview()
    {
        PreviewEnabled = false;
        previewAllyRescueConfirmation = null;
        var now = Environment.TickCount64;
        previewMiracleInterceptConfirmation = new MiracleInterceptConfirmationPopup(
            MiracleInterceptConfirmationRules.MiracleOfNatureActionId,
            1,
            1,
            MiracleInterceptThreatKind.MarksmanSpite,
            0,
            now,
            now + MiracleInterceptConfirmationRules.PopupDurationMilliseconds);
    }

    public void Draw()
    {
        if (gameGui.GameUiHidden) return;

        if (PreviewEnabled) DrawPreview();
        if (CcProtectionPreviewEnabled) DrawCcProtectionPreview();
        if (ResourceAuraPreviewEnabled) DrawResourceAuraPreview();
        DrawPopup(previewPopup);

        var now = Environment.TickCount64;
        var pressureEscape = personalStatus.PressureEscapeDiagnostics;
        var highPressureWarningVisible = HighPressureWarningPreviewEnabled ||
                                         (configuration.Enabled &&
                                          configuration.ShowHighPressureWarning &&
                                          pressureEscape.WarningActive);
        if (highPressureWarningVisible)
        {
            var directEnemyCount = HighPressureWarningPreviewEnabled
                ? Math.Max(4, pressureEscape.DirectEnemyCount)
                : pressureEscape.DirectEnemyCount;
            DrawHighPressureWarning(now, directEnemyCount);
        }

        if (IsolationWarningPreviewEnabled || isolationAwareness.Snapshot.Visible)
            DrawIsolationWarning(now, highPressureWarningVisible);
        DrawResourceAuras(now);
        if (!configuration.Enabled)
        {
            DrawAllyRescueConfirmationPreview(now);
            DrawMiracleInterceptConfirmationPreview(now);
            return;
        }

        if (tracker.IsActive)
        {
            DrawLiveSeitonDecisionStack(now);
        }

        if (tracker.IsActive || pressureTracker.IsActive)
            DrawLiveNameplateIndicators(now);

        // The confirmation popup belongs to the explicitly enabled Ally Rescue
        // helper, not to the optional incoming-debuff warning cards. The method
        // filters those cards itself, so a confirmed cleanse stays visible even
        // when ordinary personal warnings are hidden.
        DrawPersonalWarnings(now);
    }

    private void DrawHighPressureWarning(long now, int directEnemyCount)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var (topLeft, bottomRight) = HighPressureWarningBounds();
        var cycle = ((now % 60_000L) / 1000d) * Math.Tau * 1.25d;
        var pulse = (float)((Math.Sin(cycle) + 1d) * 0.5d);
        var accentAlpha = 0.78f + (pulse * 0.22f);
        var fillAlpha = 0.76f + (pulse * 0.08f);
        var rounding = 12f * scale;
        var draw = ImGui.GetForegroundDrawList();

        // The card never changes position or size. Only its border and alpha
        // pulse, keeping the warning prominent without making aim references move.
        draw.AddRectFilled(
            topLeft,
            bottomRight,
            Pack(new Vector4(0.055f, 0.012f, 0.018f, fillAlpha)),
            rounding);
        draw.AddRectFilled(
            topLeft,
            new Vector2(topLeft.X + (9f * scale), bottomRight.Y),
            Pack(new Vector4(HighPressureColor.X, HighPressureColor.Y, HighPressureColor.Z, accentAlpha)),
            rounding);
        draw.AddRect(
            topLeft - new Vector2(2f * scale),
            bottomRight + new Vector2(2f * scale),
            Pack(new Vector4(1f, 0.28f, 0.08f, 0.3f + (pulse * 0.38f))),
            rounding + (2f * scale),
            ImDrawFlags.None,
            Math.Max(3f, 4.2f * scale));
        draw.AddRect(
            topLeft,
            bottomRight,
            Pack(new Vector4(HighPressureColor.X, HighPressureColor.Y, HighPressureColor.Z, accentAlpha)),
            rounding,
            ImDrawFlags.None,
            Math.Max(2f, 2.8f * scale));

        var textLeft = topLeft.X + (25f * scale);
        DrawOutlinedText(
            draw,
            new Vector2(textLeft, topLeft.Y + (11f * scale)),
            $"FOCUSED x{Math.Max(3, directEnemyCount)}",
            1.72f,
            false,
            1f,
            new Vector4(1f, 0.28f, 0.12f, 1f));
        DrawOutlinedText(
            draw,
            new Vector2(textLeft, topLeft.Y + (72f * scale)),
            "3+ ENEMIES TARGETING YOU",
            0.79f,
            false,
            0.96f,
            TextColor);
    }

    private void DrawIsolationWarning(long now, bool avoidHighPressureWarning)
    {
        var configuredScale = Math.Clamp(configuration.IsolationWarningScale, 0.75f, 1.75f);
        var scale = configuredScale * ImGuiHelpers.GlobalScale;
        var viewport = ImGui.GetMainViewport();
        var cardSize = new Vector2(342f, 88f) * scale;
        var workMinimum = viewport.WorkPos;
        var workMaximum = viewport.WorkPos + viewport.WorkSize;
        var maximumTopLeft = Vector2.Max(workMinimum, workMaximum - cardSize);
        var topLeft = Vector2.Clamp(
            viewport.WorkPos + (new Vector2(28f, 42f) * ImGuiHelpers.GlobalScale),
            workMinimum,
            maximumTopLeft);
        var bottomRight = topLeft + cardSize;
        if (avoidHighPressureWarning)
        {
            var (pressureMinimum, pressureMaximum) = HighPressureWarningBounds();
            if (RectanglesOverlap(topLeft, bottomRight, pressureMinimum, pressureMaximum))
            {
                var gap = 18f * ImGuiHelpers.GlobalScale;
                var below = pressureMaximum.Y + gap;
                var above = pressureMinimum.Y - gap - cardSize.Y;
                if (below <= maximumTopLeft.Y)
                    topLeft.Y = below;
                else if (above >= workMinimum.Y)
                    topLeft.Y = above;
                else
                    topLeft.Y = maximumTopLeft.Y;
                bottomRight = topLeft + cardSize;
            }
        }
        var cycle = ((now % 60_000L) / 1000d) * Math.Tau * 0.55d;
        var pulse = (float)((Math.Sin(cycle) + 1d) * 0.5d);
        var accentAlpha = 0.72f + (pulse * 0.2f);
        var fillAlpha = 0.68f + (pulse * 0.06f);
        var rounding = 11f * scale;
        var draw = ImGui.GetForegroundDrawList();

        // Geometry is deliberately static; only alpha breathes so the corner
        // warning remains noticeable without looking like an urgent hit alert.
        draw.AddRectFilled(
            topLeft,
            bottomRight,
            Pack(new Vector4(0.025f, 0.028f, 0.04f, fillAlpha)),
            rounding);
        draw.AddRectFilled(
            topLeft,
            new Vector2(topLeft.X + (7f * scale), bottomRight.Y),
            Pack(new Vector4(IsolationColor.X, IsolationColor.Y, IsolationColor.Z, accentAlpha)),
            rounding);
        draw.AddRect(
            topLeft,
            bottomRight,
            Pack(new Vector4(IsolationColor.X, IsolationColor.Y, IsolationColor.Z, accentAlpha)),
            rounding,
            ImDrawFlags.None,
            Math.Max(2f, 2.6f * scale));

        var textLeft = topLeft.X + (22f * scale);
        DrawOutlinedText(
            draw,
            new Vector2(textLeft, topLeft.Y + (12f * scale)),
            "ISOLATED",
            1.55f * configuredScale,
            false,
            0.94f,
            IsolationColor);
        DrawOutlinedText(
            draw,
            new Vector2(textLeft, topLeft.Y + (58f * scale)),
            "NO ALLY <=20y IN SIGHT",
            0.76f * configuredScale,
            false,
            0.86f,
            new Vector4(0.9f, 0.94f, 1f, 1f));
    }

    private static (Vector2 Minimum, Vector2 Maximum) HighPressureWarningBounds()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var viewport = ImGui.GetMainViewport();
        var cardSize = new Vector2(410f, 108f) * scale;
        var topLeft = new Vector2(
            viewport.WorkPos.X + Math.Max(0f, (viewport.WorkSize.X - cardSize.X) * 0.5f),
            viewport.WorkPos.Y + (42f * scale));
        return (topLeft, topLeft + cardSize);
    }

    private static bool RectanglesOverlap(
        Vector2 firstMinimum,
        Vector2 firstMaximum,
        Vector2 secondMinimum,
        Vector2 secondMaximum) =>
        firstMinimum.X < secondMaximum.X &&
        firstMaximum.X > secondMinimum.X &&
        firstMinimum.Y < secondMaximum.Y &&
        firstMaximum.Y > secondMinimum.Y;

    private void DrawResourceAuras(long now)
    {
        var anchors = resourceAuraAnchors.Capture();
        if (anchors.Count == 0 || ResourceAuraPreviewEnabled) return;

        var draw = ImGui.GetForegroundDrawList();
        foreach (var anchor in anchors)
        {
            var strength = anchor.Surface == ResourceAuraSurface.SelfHotbar ? 1f : 0.38f;
            DrawResourceAura(draw, anchor.Minimum, anchor.Maximum, anchor.Kind, strength, now);
        }
    }

    private void DrawResourceAuraPreview()
    {
        var anchors = resourceAuraAnchors.CaptureSelfHotbarsForPreview();
        if (anchors.Count == 0) return;

        var draw = ImGui.GetForegroundDrawList();
        var now = Environment.TickCount64;
        foreach (var anchor in anchors)
            DrawResourceAura(draw, anchor.Minimum, anchor.Maximum, anchor.Kind, 1f, now);
    }

    private void DrawResourceAura(
        ImDrawListPtr draw,
        Vector2 minimum,
        Vector2 maximum,
        ResourceAuraKind kind,
        float surfaceStrength,
        long now)
    {
        if (kind == ResourceAuraKind.None ||
            maximum.X <= minimum.X || maximum.Y <= minimum.Y)
        {
            return;
        }

        var color = kind switch
        {
            ResourceAuraKind.LowHp => LowHealthAuraColor,
            ResourceAuraKind.LowMp => LowManaAuraColor,
            ResourceAuraKind.LowHpAndMp => CombinedResourceAuraColor,
            _ => default,
        };
        if (color.W <= 0f) return;

        var speed = Math.Clamp(configuration.ResourceAuraPulseSpeed, 0.2f, 2f);
        var cycle = ((now % 60_000L) / 1000d) * Math.Tau * speed;
        var pulse = 0.64f + (0.36f * (float)((Math.Sin(cycle) + 1d) * 0.5d));
        var intensity = Math.Clamp(configuration.ResourceAuraIntensity, 0.1f, 1.5f);
        var alpha = Math.Clamp(intensity * surfaceStrength * pulse, 0f, 1f);
        var scale = ImGuiHelpers.GlobalScale;
        var rounding = Math.Min(10f * scale, Math.Min(maximum.X - minimum.X, maximum.Y - minimum.Y) * 0.18f);

        // Separate overlay strokes preserve every native button animation and never mutate the HUD.
        for (var layer = 3; layer >= 1; layer--)
        {
            var expansion = layer * 4f * scale;
            var layerAlpha = alpha * (0.055f + ((4 - layer) * 0.045f));
            draw.AddRect(
                minimum - new Vector2(expansion),
                maximum + new Vector2(expansion),
                Pack(new Vector4(color.X, color.Y, color.Z, layerAlpha)),
                rounding + expansion,
                ImDrawFlags.None,
                Math.Max(2f, (5 - layer) * 1.5f * scale));
        }

        draw.AddRectFilled(
            minimum,
            maximum,
            Pack(new Vector4(color.X, color.Y, color.Z, alpha * 0.045f)),
            rounding);
        draw.AddRect(
            minimum - new Vector2(1.5f * scale),
            maximum + new Vector2(1.5f * scale),
            Pack(new Vector4(color.X, color.Y, color.Z, alpha * 0.78f)),
            rounding + (1.5f * scale),
            ImDrawFlags.None,
            Math.Max(1.5f, 2.25f * scale));
    }

    private void DrawAllyRescueConfirmationPreview(long now)
    {
        if (previewAllyRescueConfirmation is not { } preview || !preview.IsVisible(now)) return;

        var stackCenterY = ImGui.GetIO().DisplaySize.Y *
                           Math.Clamp(configuration.PersonalWarningScreenY, 0.08f, 0.9f);
        DrawAllyRescueConfirmationCard(preview, 1, stackCenterY);
    }

    private void DrawMiracleInterceptConfirmationPreview(long now)
    {
        if (previewMiracleInterceptConfirmation is not { } preview || !preview.IsVisible(now)) return;

        var stackCenterY = ImGui.GetIO().DisplaySize.Y *
                           Math.Clamp(configuration.PersonalWarningScreenY, 0.08f, 0.9f);
        DrawMiracleInterceptConfirmationCard(preview, stackCenterY, now);
    }

    private void DrawPersonalWarnings(long now)
    {
        var personal = personalStatus.Snapshot;
        var defense = personalStatus.DefensiveUtilityDiagnostics;
        var rescue = personalStatus.AllyRescueDiagnostics;
        var miracle = personalStatus.MiracleInterceptDiagnostics;
        var guardianPopup = defense.GuardianPopup is { } acceptedGuardian &&
                            acceptedGuardian.IsVisible(now)
            ? acceptedGuardian
            : (GuardianTriggerPopup?)null;
        var liveConfirmation = rescue.ConfirmationPopup is { } popup && popup.IsVisible(now)
            ? popup
            : (AllyRescueConfirmationPopup?)null;
        var confirmation = liveConfirmation ??
            (previewAllyRescueConfirmation is { } preview && preview.IsVisible(now)
                ? preview
                : (AllyRescueConfirmationPopup?)null);
        var liveMiracleConfirmation =
            miracle.ConfirmationPopup is { } miraclePopup && miraclePopup.IsVisible(now)
                ? miraclePopup
                : (MiracleInterceptConfirmationPopup?)null;
        var miracleConfirmation = liveMiracleConfirmation ??
            (previewMiracleInterceptConfirmation is { } miraclePreview && miraclePreview.IsVisible(now)
                ? miraclePreview
                : (MiracleInterceptConfirmationPopup?)null);

        var statuses = personal.Active
            ? personal.Statuses
                .Where(status => status.ExpiresAtMilliseconds > now)
                .OrderByDescending(static status =>
                    status.StatusId == EnemyCombatConstants.MarksmanSpiteActionId)
                .ThenByDescending(static status => status.AlertKind)
                .ThenBy(static status => status.ExpiresAtMilliseconds)
                .Take(4)
                .ToArray()
            : [];
        if (statuses.Length == 0 &&
            guardianPopup is null &&
            confirmation is null &&
            miracleConfirmation is null)
        {
            return;
        }

        var heights = new List<float>(
            statuses.Length +
            (guardianPopup is null ? 0 : 1) +
            (confirmation is null ? 0 : 1) +
            (miracleConfirmation is null ? 0 : 1));
        if (miracleConfirmation is not null)
            heights.Add(MiracleInterceptConfirmationCardHeight());
        if (confirmation is not null)
            heights.Add(AllyRescueConfirmationCardHeight());
        if (guardianPopup is not null)
            heights.Add(GuardianTriggerCardHeight());
        heights.AddRange(statuses.Select(status => PersonalWarningCardHeight(status, now)));
        var offsets = BuildCenteredOffsets(heights, 7f * ImGuiHelpers.GlobalScale);
        var stackCenterY = ImGui.GetIO().DisplaySize.Y *
                           Math.Clamp(configuration.PersonalWarningScreenY, 0.08f, 0.9f);
        var offsetIndex = 0;
        if (miracleConfirmation is { } visibleMiracleConfirmation)
        {
            DrawMiracleInterceptConfirmationCard(
                visibleMiracleConfirmation,
                stackCenterY + offsets[offsetIndex],
                now);
            offsetIndex++;
        }

        if (confirmation is { } visibleConfirmation)
        {
            DrawAllyRescueConfirmationCard(
                visibleConfirmation,
                liveConfirmation is not null
                    ? rescue.MatchConfirmations.TotalConfirmed
                    : 1,
                stackCenterY + offsets[offsetIndex]);
            offsetIndex++;
        }

        if (guardianPopup is { } visibleGuardianPopup)
        {
            DrawGuardianTriggerCard(
                visibleGuardianPopup,
                stackCenterY + offsets[offsetIndex],
                now);
            offsetIndex++;
        }

        for (var index = 0; index < statuses.Length; index++)
        {
            DrawPersonalWarningCard(
                statuses[index],
                personal.Purify,
                stackCenterY + offsets[offsetIndex + index],
                now);
        }
    }

    private float GuardianTriggerCardHeight() =>
        70f * Math.Clamp(configuration.PersonalWarningScale, 0.55f, 1.8f) *
        ImGuiHelpers.GlobalScale;

    private void DrawGuardianTriggerCard(
        GuardianTriggerPopup popup,
        float centerY,
        long now)
    {
        var configuredScale = Math.Clamp(configuration.PersonalWarningScale, 0.55f, 1.8f);
        var scale = configuredScale * ImGuiHelpers.GlobalScale;
        var duration = Math.Max(1L, popup.EndsAtMilliseconds - popup.StartedAtMilliseconds);
        var progress = Math.Clamp((now - popup.StartedAtMilliseconds) / (float)duration, 0f, 1f);
        var entryPulse = MathF.Exp(-progress * 10f);
        var cardSize = new Vector2(328f, 70f) * scale * (1f + (entryPulse * 0.055f));
        var screen = ImGui.GetIO().DisplaySize;
        var center = new Vector2(
            screen.X * Math.Clamp(configuration.PersonalWarningScreenX, 0.05f, 0.95f),
            centerY);
        var topLeft = center - (cardSize * 0.5f);
        var bottomRight = center + (cardSize * 0.5f);
        var draw = ImGui.GetForegroundDrawList();
        var rounding = 10f * scale;

        draw.AddRectFilled(
            topLeft,
            bottomRight,
            Pack(new Vector4(
                0.012f,
                0.035f,
                0.065f,
                Math.Clamp(configuration.PersonalWarningBackgroundOpacity, 0f, 1f))),
            rounding);
        draw.AddRectFilled(
            topLeft,
            new Vector2(topLeft.X + (7f * scale), bottomRight.Y),
            Pack(GuardColor),
            rounding);
        draw.AddRect(
            topLeft,
            bottomRight,
            Pack(GuardColor),
            rounding,
            ImDrawFlags.None,
            Math.Max(2.25f, (2.75f + (entryPulse * 1.75f)) * scale));

        var iconSize = 48f * scale;
        var iconMin = new Vector2(topLeft.X + (12f * scale), center.Y - (iconSize * 0.5f));
        var iconMax = iconMin + new Vector2(iconSize);
        if (!TryDrawGameIcon(
                draw,
                EnemyCombatConstants.GuardianIconId,
                iconMin,
                iconMax,
                1f))
        {
            draw.AddRectFilled(
                iconMin,
                iconMax,
                Pack(new Vector4(GuardColor.X * 0.22f, GuardColor.Y * 0.22f, GuardColor.Z * 0.22f, 1f)),
                6f * scale);
        }

        var textCenterX = iconMax.X + ((bottomRight.X - iconMax.X) * 0.5f) - (5f * scale);
        DrawOutlinedText(
            draw,
            new Vector2(textCenterX, center.Y - (20f * scale)),
            "GUARDIAN TRIGGERED",
            1.01f * configuredScale,
            true);
        DrawOutlinedText(
            draw,
            new Vector2(textCenterX, center.Y + (11f * scale)),
            $"P{popup.PartySlot}  •  CLIENT ACCEPTED",
            0.72f * configuredScale,
            true);
    }

    private float MiracleInterceptConfirmationCardHeight() =>
        76f * Math.Clamp(configuration.PersonalWarningScale, 0.55f, 1.8f) *
        ImGuiHelpers.GlobalScale;

    private void DrawMiracleInterceptConfirmationCard(
        MiracleInterceptConfirmationPopup popup,
        float centerY,
        long now)
    {
        var configuredScale = Math.Clamp(configuration.PersonalWarningScale, 0.55f, 1.8f);
        var scale = configuredScale * ImGuiHelpers.GlobalScale;
        var duration = Math.Max(1L, popup.EndsAtMilliseconds - popup.StartedAtMilliseconds);
        var progress = Math.Clamp((now - popup.StartedAtMilliseconds) / (float)duration, 0f, 1f);
        var pulse = MathF.Exp(-progress * 9f);
        var pulseScale = 1f + (pulse * 0.08f);
        var cardSize = new Vector2(350f, 76f) * scale * pulseScale;
        var screen = ImGui.GetIO().DisplaySize;
        var center = new Vector2(
            screen.X * Math.Clamp(configuration.PersonalWarningScreenX, 0.05f, 0.95f),
            centerY);
        var topLeft = center - (cardSize * 0.5f);
        var bottomRight = center + (cardSize * 0.5f);
        var draw = ImGui.GetForegroundDrawList();
        var rounding = 11f * scale;

        draw.AddRectFilled(
            topLeft,
            bottomRight,
            Pack(new Vector4(
                0.012f,
                0.035f,
                0.07f,
                Math.Clamp(configuration.PersonalWarningBackgroundOpacity, 0f, 1f))),
            rounding);
        draw.AddRectFilled(
            topLeft,
            new Vector2(topLeft.X + (7f * scale), bottomRight.Y),
            Pack(CleanseColor),
            rounding);
        draw.AddRect(
            topLeft,
            bottomRight,
            Pack(CleanseColor),
            rounding,
            ImDrawFlags.None,
            Math.Max(2.5f, (3f + (pulse * 2f)) * scale));

        var iconSize = 50f * scale;
        var iconMin = new Vector2(topLeft.X + (13f * scale), center.Y - (iconSize * 0.5f));
        var iconMax = iconMin + new Vector2(iconSize);
        if (!TryDrawGameIcon(
                draw,
                popup.ActionId == EnemyCombatConstants.SilentNocturneActionId
                    ? EnemyCombatConstants.SilentNocturneActionIconId
                    : EnemyCombatConstants.MiracleOfNatureActionIconId,
                iconMin,
                iconMax,
                1f))
        {
            draw.AddRectFilled(
                iconMin,
                iconMax,
                Pack(new Vector4(CleanseColor.X * 0.2f, CleanseColor.Y * 0.2f, CleanseColor.Z * 0.2f, 1f)),
                6f * scale);
        }

        var textCenterX = iconMax.X + ((bottomRight.X - iconMax.X) * 0.5f) - (5f * scale);
        DrawOutlinedText(
            draw,
            new Vector2(textCenterX, center.Y - (21f * scale)),
            "AUTO CC LANDED",
            1.08f * configuredScale,
            true);
        DrawOutlinedText(
            draw,
            new Vector2(textCenterX, center.Y + (13f * scale)),
            MiracleInterceptConfirmationSubtitle(popup),
            0.72f * configuredScale,
            true);
    }

    private static string MiracleInterceptConfirmationSubtitle(
        MiracleInterceptConfirmationPopup popup)
    {
        var action = popup.ActionId == EnemyCombatConstants.SilentNocturneActionId
            ? "SILENCE"
            : "MIRACLE";
        return popup.Threat switch
        {
            MiracleInterceptThreatKind.MarksmanSpite => $"{action}  •  MCH LB START",
            MiracleInterceptThreatKind.Zantetsuken => $"{action}  •  SAM LB START",
            MiracleInterceptThreatKind.FuriousBacklash => $"{action}  •  VPR NEST START",
            MiracleInterceptThreatKind.Contradance => $"{action}  •  DNC LB START",
            MiracleInterceptThreatKind.PostPurifyCrowdControl =>
                $"{action}  •  AFTER PURIFY ({PurifyStatusLabel(popup.RemovedStatusId)})",
            MiracleInterceptThreatKind.PostGuardCrowdControl =>
                $"{action}  •  AFTER GUARD",
            _ => action,
        };
    }

    private static string PurifyStatusLabel(uint statusId) =>
        statusId switch
        {
            MiracleCleanseFollowupRules.StunStatusId => "STUN",
            MiracleCleanseFollowupRules.HeavyStatusId => "HEAVY",
            MiracleCleanseFollowupRules.BindStatusId => "BIND",
            MiracleCleanseFollowupRules.SilenceStatusId => "SILENCE",
            MiracleCleanseFollowupRules.MiracleOfNatureStatusId => "MIRACLE",
            MiracleCleanseFollowupRules.DeepFreezeStatusId => "DEEP FREEZE",
            _ => "CC",
        };

    private float AllyRescueConfirmationCardHeight() =>
        64f * Math.Clamp(configuration.PersonalWarningScale, 0.55f, 1.8f) *
        ImGuiHelpers.GlobalScale;

    private void DrawAllyRescueConfirmationCard(
        AllyRescueConfirmationPopup popup,
        long matchConfirmationCount,
        float centerY)
    {
        var configuredScale = Math.Clamp(configuration.PersonalWarningScale, 0.55f, 1.8f);
        var scale = configuredScale * ImGuiHelpers.GlobalScale;
        var cardSize = new Vector2(286f, 64f) * scale;
        var screen = ImGui.GetIO().DisplaySize;
        var center = new Vector2(
            screen.X * Math.Clamp(configuration.PersonalWarningScreenX, 0.05f, 0.95f),
            centerY);
        var topLeft = center - (cardSize * 0.5f);
        var bottomRight = center + (cardSize * 0.5f);
        var draw = ImGui.GetForegroundDrawList();
        var rounding = 10f * scale;

        draw.AddRectFilled(
            topLeft,
            bottomRight,
            Pack(new Vector4(
                0.018f,
                0.035f,
                0.055f,
                Math.Clamp(configuration.PersonalWarningBackgroundOpacity, 0f, 1f))),
            rounding);
        draw.AddRectFilled(
            topLeft,
            new Vector2(topLeft.X + (6f * scale), bottomRight.Y),
            Pack(CleanseColor),
            rounding);
        draw.AddRect(
            topLeft,
            bottomRight,
            Pack(CleanseColor),
            rounding,
            ImDrawFlags.None,
            Math.Max(2f, 3f * scale));

        var iconId = popup.ActionId switch
        {
            AllyRescueConfirmationRules.WardensPaeanActionId => WardensPaeanIconId,
            AllyRescueConfirmationRules.AquaveilActionId => AquaveilIconId,
            _ => 0u,
        };
        var iconSize = 44f * scale;
        var iconMin = new Vector2(topLeft.X + (11f * scale), center.Y - (iconSize * 0.5f));
        var iconMax = iconMin + new Vector2(iconSize);
        if (iconId == 0 || !TryDrawGameIcon(draw, iconId, iconMin, iconMax, 1f))
        {
            draw.AddRectFilled(
                iconMin,
                iconMax,
                Pack(new Vector4(CleanseColor.X * 0.22f, CleanseColor.Y * 0.22f, CleanseColor.Z * 0.22f, 1f)),
                5f * scale);
        }

        var textCenterX = iconMax.X + ((bottomRight.X - iconMax.X) * 0.5f) - (4f * scale);
        DrawOutlinedText(
            draw,
            new Vector2(textCenterX, center.Y - (19f * scale)),
            "CLEANSED",
            0.98f * configuredScale,
            true);
        DrawOutlinedText(
            draw,
            new Vector2(textCenterX, center.Y + (10f * scale)),
            $"{AllyRescueStatusName(popup.RemovedStatusId)}  •  THIS CC {Math.Max(0, matchConfirmationCount)}",
            0.76f * configuredScale,
            true);
    }

    private static string AllyRescueStatusName(uint statusId) => statusId switch
    {
        AllyRescueConfirmationRules.StunStatusId => "STUN",
        AllyRescueConfirmationRules.HeavyStatusId => "HEAVY",
        AllyRescueConfirmationRules.BindStatusId => "BIND",
        AllyRescueConfirmationRules.SilenceStatusId => "SILENCE",
        AllyRescueConfirmationRules.MiracleOfNatureStatusId => "MIRACLE",
        AllyRescueConfirmationRules.DeepFreezeStatusId => "DEEP FREEZE",
        _ => $"STATUS {statusId}",
    };

    private float PersonalWarningCardHeight(PersonalStatusSnapshot status, long now)
    {
        var configuredScale = Math.Clamp(configuration.PersonalWarningScale, 0.55f, 1.8f);
        if (status.StatusId == EnemyCombatConstants.MarksmanSpiteActionId)
            configuredScale *= Math.Clamp(configuration.MarksmanSpiteWarningScale, 1f, 2f);
        var pulseAge = status.PulseStartedAtMilliseconds < 0
            ? long.MaxValue
            : Math.Max(0, now - status.PulseStartedAtMilliseconds);
        var pulse = status.IsEntryPulseActive(now)
            ? 1f - (pulseAge / (float)PersonalStatusSnapshot.EntryPulseDurationMilliseconds)
            : 0f;
        var baseHeight = status.StatusId == EnemyCombatConstants.MarksmanSpiteActionId ? 76f : 64f;
        return baseHeight * configuredScale * ImGuiHelpers.GlobalScale * (1f + (pulse * 0.1f));
    }

    private void DrawPersonalWarningCard(
        PersonalStatusSnapshot status,
        EmergencyPurifyProbeSnapshot purify,
        float centerY,
        long now)
    {
        var remaining = Math.Max(0, status.ExpiresAtMilliseconds - now);
        if (remaining <= 0) return;

        var uiScale = ImGuiHelpers.GlobalScale;
        var configuredScale = Math.Clamp(configuration.PersonalWarningScale, 0.55f, 1.8f);
        var isMachinistLimitBreak = status.StatusId == EnemyCombatConstants.MarksmanSpiteActionId;
        if (isMachinistLimitBreak)
            configuredScale *= Math.Clamp(configuration.MarksmanSpiteWarningScale, 1f, 2f);
        var pulseAge = status.PulseStartedAtMilliseconds < 0
            ? long.MaxValue
            : Math.Max(0, now - status.PulseStartedAtMilliseconds);
        var pulse = status.IsEntryPulseActive(now)
            ? 1f - (pulseAge / (float)PersonalStatusSnapshot.EntryPulseDurationMilliseconds)
            : 0f;
        var scale = configuredScale * uiScale;
        var pulseScale = 1f + (pulse * 0.1f);
        var cardSize = (isMachinistLimitBreak
            ? new Vector2(326f, 76f)
            : new Vector2(286f, 64f)) * scale * pulseScale;
        var screen = ImGui.GetIO().DisplaySize;
        var center = new Vector2(
            screen.X * Math.Clamp(configuration.PersonalWarningScreenX, 0.05f, 0.95f),
            centerY);

        var topLeft = center - (cardSize * 0.5f);
        var bottomRight = center + (cardSize * 0.5f);
        var accent = isMachinistLimitBreak
            ? LethalWarningColor
            : status.AlertKind == PersonalDebuffAlertKind.CleanseUrgent
                ? CleanseColor
                : WarningColor;
        var draw = ImGui.GetForegroundDrawList();
        var rounding = 10f * scale;
        draw.AddRectFilled(
            topLeft,
            bottomRight,
            Pack(new Vector4(
                0.018f,
                0.012f,
                0.026f,
                Math.Clamp(configuration.PersonalWarningBackgroundOpacity, 0f, 1f))),
            rounding);
        draw.AddRect(
            topLeft,
            bottomRight,
            Pack(new Vector4(accent.X, accent.Y, accent.Z, 1f)),
            rounding,
            ImDrawFlags.None,
            Math.Max(2f, (2.4f + (pulse * 2.5f)) * scale));
        if (isMachinistLimitBreak)
        {
            draw.AddRect(
                topLeft + new Vector2(4f * scale),
                bottomRight - new Vector2(4f * scale),
                Pack(new Vector4(1f, 0.92f, 0.96f, 0.92f)),
                Math.Max(2f, rounding - (3f * scale)),
                ImDrawFlags.None,
                Math.Max(1f, 1.2f * scale));
        }

        var iconSize = (isMachinistLimitBreak ? 52f : 44f) * scale;
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
            EnemyCombatConstants.MarksmanSpiteActionId => "MCH LIMIT BREAK ON YOU",
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
        if (status.StatusId == EnemyCombatConstants.MarksmanSpiteActionId)
            return "INCOMING  •  GUARD NOW";
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
                $"PURIFY PENDING {Math.Max(0, purify.BufferRemainingMilliseconds) / 1000f:0.0}s  •  {countdown}",
            EmergencyPurifyBufferPhase.SpentUntilStatusGone => $"PURIFY ATTEMPTED  •  {countdown}",
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

        var byIdentity = new Dictionary<(ulong GameObjectId, uint EntityId), NamePlateAnchorSnapshot>(anchors.Count);
        foreach (var anchor in anchors)
        {
            if (now - anchor.CapturedAtMilliseconds is < 0 or > MaximumAnchorAgeMilliseconds) continue;
            byIdentity[(anchor.GameObjectId, anchor.EntityId)] = anchor;
        }

        var pressureByIdentity = pressureTracker.Snapshot.Opponents
            .GroupBy(static enemy => (enemy.GameObjectId, enemy.EntityId))
            .Where(static group => group.Count() == 1)
            .ToDictionary(static group => group.Key, static group => group.Single());
        var drawn = new HashSet<(ulong GameObjectId, uint EntityId)>();
        foreach (var enemy in tracker.Enemies)
        {
            var identity = (enemy.GameObjectId, enemy.EntityId);
            if (!byIdentity.TryGetValue(identity, out var anchor)) continue;
            pressureByIdentity.TryGetValue(identity, out var pressure);
            DrawIndicatorSlots(anchor, enemy, pressure, now);
            drawn.Add(identity);
        }

        foreach (var pressure in pressureByIdentity.Values)
        {
            var identity = (pressure.GameObjectId, pressure.EntityId);
            if (drawn.Contains(identity) ||
                !byIdentity.TryGetValue(identity, out var anchor))
            {
                continue;
            }

            DrawIndicatorSlots(anchor, null, pressure, now);
        }
    }

    private void DrawIndicatorSlots(
        NamePlateAnchorSnapshot anchor,
        EnemyHudSnapshot? enemy,
        TargetPressureOpponentSnapshot? pressure,
        long now)
    {
        var nativeHeight = Math.Max(1f, anchor.Height);
        var size = Math.Clamp(nativeHeight * configuration.NameplateIconScale, 12f, 48f);
        var gap = Math.Max(1f, configuration.NameplateIconSpacing * ImGuiHelpers.GlobalScale);
        var centerY = (anchor.JobIconTopLeft.Y + anchor.JobIconBottomRight.Y) * 0.5f;

        if (configuration.ShowNameplateSeiton && enemy?.SeitonEligible == true)
        {
            var rect = IndicatorRect(anchor.JobIconTopLeft.X, centerY, size, gap, 2);
            DrawIconBadge(rect.Min, rect.Max, EnemyCombatConstants.SeitonIconId, SeitonColor, false, enemy.SlotLabel, null);
        }

        var activeProtections = pressure?.Protections
            .Where(protection => protection.ExpiresAtMilliseconds > now)
            .OrderBy(static protection => protection.Kind)
            .ThenBy(static protection => protection.ExpiresAtMilliseconds)
            .ToArray() ?? [];
        var activeGuard = activeProtections.FirstOrDefault(static protection => protection.Kind == CcProtectionKind.Guard);
        var hasActiveGuard = activeGuard.StatusId != 0;
        if (!hasActiveGuard && configuration.ShowGuardUnavailable && enemy?.GuardUnavailable == true)
        {
            var rect = IndicatorRect(anchor.JobIconTopLeft.X, centerY, size, gap, 0);
            var countdown = configuration.ShowGuardCountdown
                ? Math.Max(1, (int)Math.Ceiling(enemy.GuardCooldownRemainingSeconds)).ToString()
                : null;
            DrawIconBadge(rect.Min, rect.Max, EnemyCombatConstants.GuardIconId, GuardColor, true, null, countdown);
        }

        if (configuration.ShowLowMp && enemy?.LowMp == true)
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

        const int TeamPressureSlot = 4;
        const int IncomingPressureSlot = 5;
        if (configuration.ShowTeamPressureOnNameplates && pressure is { TeamTargetCount: > 0 })
        {
            var rect = IndicatorRect(anchor.JobIconTopLeft.X, centerY, size, gap, TeamPressureSlot);
            DrawTextBadge(rect.Min, rect.Max, $"P{pressure.TeamTargetCount}", TeamPressureColor);
        }

        if (configuration.ShowIncomingPressureOnNameplates && pressure?.IsIncoming == true)
        {
            var rect = IndicatorRect(anchor.JobIconTopLeft.X, centerY, size, gap, IncomingPressureSlot);
            var label = (pressure.IncomingEvidence & TargetPressureEvidence.MachinistLimitBreakMarker) != 0
                ? "LB"
                : pressure.HasDirectIncomingIntent
                    ? "YOU"
                    : "HIT";
            DrawTextBadge(
                rect.Min,
                rect.Max,
                label,
                pressure.HasDirectIncomingIntent ? IncomingPressureColor : RecentPressureColor);
        }

        if (configuration.ShowCcProtection && activeProtections.Length > 0)
            DrawCcProtectionEmblem(anchor, activeProtections, now);
    }

    private void DrawCcProtectionEmblem(
        NamePlateAnchorSnapshot anchor,
        IReadOnlyList<CcProtectionDisplay> activeProtections,
        long now)
    {
        // The farthest verified expiry represents the complete remaining protected
        // window when Guard and another immunity overlap. One static emblem avoids
        // duplicate visual noise and cannot swap to a shorter timer mid-window.
        var protection = activeProtections
            .OrderByDescending(static candidate => candidate.ExpiresAtMilliseconds)
            .ThenByDescending(static candidate => candidate.Kind)
            .ThenBy(static candidate => candidate.StatusId)
            .FirstOrDefault();
        if (protection.StatusId == 0) return;

        var uiScale = ImGuiHelpers.GlobalScale;
        var nativeHeight = Math.Max(1f, anchor.Height);
        var desiredEmblemSize = Math.Clamp(
            nativeHeight * 2.15f * configuration.CcProtectionEmblemScale,
            52f * uiScale,
            88f * uiScale);
        var availableHeight = anchor.JobIconTopLeft.Y - (11f * uiScale);
        if (availableHeight <= 0f) return;
        var emblemSize = desiredEmblemSize;
        var timerHeight = Math.Clamp(emblemSize * 0.36f, 21f * uiScale, 31f * uiScale);
        var timerGap = Math.Max(3f * uiScale, emblemSize * 0.05f);
        var anchorGap = Math.Max(7f * uiScale, configuration.NameplateIconSpacing * uiScale);
        var requiredHeight = emblemSize + timerGap + timerHeight + anchorGap;
        if (requiredHeight > availableHeight)
        {
            var ratio = availableHeight / requiredHeight;
            emblemSize *= ratio;
            if (emblemSize < 34f * uiScale) return;
            timerHeight = Math.Clamp(emblemSize * 0.36f, 16f * uiScale, 31f * uiScale);
            timerGap = Math.Max(2f * uiScale, emblemSize * 0.05f);
            anchorGap = Math.Max(4f * uiScale, configuration.NameplateIconSpacing * uiScale);
        }

        var finalRequiredHeight = emblemSize + timerGap + timerHeight + anchorGap;
        if (finalRequiredHeight > availableHeight) return;

        var glowMargin = Math.Max(12f * uiScale, emblemSize * 0.36f);
        var screen = ImGui.GetIO().DisplaySize;
        var centerX = Math.Clamp(
            (anchor.JobIconTopLeft.X + anchor.JobIconBottomRight.X) * 0.5f,
            (emblemSize * 0.5f) + glowMargin,
            Math.Max((emblemSize * 0.5f) + glowMargin, screen.X - (emblemSize * 0.5f) - glowMargin));
        var emblemBottom = anchor.JobIconTopLeft.Y - anchorGap;
        var emblemMin = PixelSnap(new Vector2(centerX - (emblemSize * 0.5f), emblemBottom - emblemSize));
        var emblemMax = PixelSnap(emblemMin + new Vector2(emblemSize));
        var timerMin = PixelSnap(new Vector2(emblemMin.X, emblemMin.Y - timerGap - timerHeight));
        var timerMax = PixelSnap(new Vector2(emblemMax.X, emblemMin.Y - timerGap));
        DrawCcProtectionEmblem(emblemMin, emblemMax, timerMin, timerMax, protection, now);
    }

    private void DrawCcProtectionEmblem(
        Vector2 emblemMin,
        Vector2 emblemMax,
        Vector2 timerMin,
        Vector2 timerMax,
        CcProtectionDisplay protection,
        long now)
    {
        var draw = ImGui.GetForegroundDrawList();
        var accent = protection.Kind == CcProtectionKind.Guard ? GuardColor : ImmunityColor;
        var size = emblemMax.X - emblemMin.X;
        var outer = Math.Max(3f, size * 0.07f);
        var rounding = Math.Max(7f, size * 0.18f);
        var fillAlpha = Math.Max(0.9f, configuration.NameplateBackgroundOpacity);

        // Visibility comes from static contrast. There is no pulse, fade, scale
        // animation, or world projection to produce apparent flicker.
        draw.AddRectFilled(
            emblemMin - new Vector2(outer),
            emblemMax + new Vector2(outer),
            Pack(new Vector4(0f, 0f, 0f, 0.94f)),
            rounding + outer);
        draw.AddRectFilled(
            emblemMin,
            emblemMax,
            Pack(new Vector4(0.012f, 0.015f, 0.035f, fillAlpha)),
            rounding);
        draw.AddRect(
            emblemMin,
            emblemMax,
            Pack(accent),
            rounding,
            ImDrawFlags.None,
            Math.Max(3f, size * 0.065f));

        DrawStaticCcChevrons(draw, emblemMin, emblemMax, accent);

        var center = (emblemMin + emblemMax) * 0.5f;
        var ccScale = FitTextScale("CC", Math.Clamp(size / 29f, 1.45f, 2.75f), size * 0.72f);
        var ccY = center.Y - (ImGui.GetFontSize() * ccScale * 0.62f);
        DrawOutlinedText(
            draw,
            new Vector2(center.X, ccY),
            "CC",
            ccScale,
            true,
            1f,
            new Vector4(1f, 0.18f, 0.22f, 1f));

        // Thick red strokes make the state legible even when the small source
        // icon or the Guard/immunity accent color is hard to distinguish.
        var slashInset = size * 0.17f;
        var slashStart = emblemMin + new Vector2(slashInset);
        var slashEnd = emblemMax - new Vector2(slashInset);
        draw.AddLine(slashStart, slashEnd, Pack(new Vector4(0f, 0f, 0f, 0.98f)), Math.Max(8f, size * 0.16f));
        draw.AddLine(slashStart, slashEnd, Pack(new Vector4(1f, 0.07f, 0.1f, 1f)), Math.Max(4f, size * 0.085f));

        var countdown = configuration.ShowCcProtectionCountdown
            ? CcProtectionCountdownFormatter.Format((protection.ExpiresAtMilliseconds - now) / 1000f)
            : string.Empty;
        var timerText = string.IsNullOrEmpty(countdown) ? "IMMUNE" : $"{countdown}s";
        var timerRounding = Math.Max(5f, (timerMax.Y - timerMin.Y) * 0.3f);
        draw.AddRectFilled(timerMin - new Vector2(2f), timerMax + new Vector2(2f), Pack(new Vector4(0f, 0f, 0f, 0.94f)), timerRounding + 2f);
        draw.AddRectFilled(timerMin, timerMax, Pack(new Vector4(0.36f, 0.005f, 0.018f, 0.96f)), timerRounding);
        draw.AddRect(timerMin, timerMax, Pack(new Vector4(1f, 0.1f, 0.14f, 1f)), timerRounding, ImDrawFlags.None, Math.Max(2f, size * 0.045f));
        var timerScale = FitTextScale(timerText, Math.Clamp(size / 48f, 0.9f, 1.65f), (timerMax.X - timerMin.X) - 8f);
        var timerY = ((timerMin.Y + timerMax.Y) * 0.5f) - (ImGui.GetFontSize() * timerScale * 0.5f);
        DrawOutlinedText(draw, new Vector2(center.X, timerY), timerText, timerScale, true);
    }

    private static void DrawStaticCcChevrons(
        ImDrawListPtr draw,
        Vector2 emblemMin,
        Vector2 emblemMax,
        Vector4 accent)
    {
        var size = emblemMax.X - emblemMin.X;
        var centerY = (emblemMin.Y + emblemMax.Y) * 0.5f;
        var reach = Math.Max(8f, size * 0.18f);
        var halfHeight = Math.Max(10f, size * 0.23f);
        var core = Math.Max(2.5f, size * 0.05f);
        var glow = new Vector4(accent.X, accent.Y, accent.Z, 0.24f);

        var leftTip = new Vector2(emblemMin.X - (size * 0.06f), centerY);
        var leftOuterX = leftTip.X - reach;
        var rightTip = new Vector2(emblemMax.X + (size * 0.06f), centerY);
        var rightOuterX = rightTip.X + reach;
        var segments = new (Vector2 Start, Vector2 End)[]
        {
            (new Vector2(leftOuterX, centerY - halfHeight), leftTip),
            (leftTip, new Vector2(leftOuterX, centerY + halfHeight)),
            (new Vector2(rightOuterX, centerY - halfHeight), rightTip),
            (rightTip, new Vector2(rightOuterX, centerY + halfHeight)),
        };

        foreach (var segment in segments)
        {
            draw.AddLine(segment.Start, segment.End, Pack(glow), core + Math.Max(6f, size * 0.14f));
            draw.AddLine(segment.Start, segment.End, Pack(accent), core);
        }
    }

    private static Vector2 PixelSnap(Vector2 value) => new(MathF.Round(value.X), MathF.Round(value.Y));

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
        string? countdown,
        bool emphasized = false)
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

        if (emphasized)
        {
            var inset = Math.Max(1f, border * 1.5f);
            draw.AddRect(
                topLeft + new Vector2(inset),
                bottomRight - new Vector2(inset),
                Pack(new Vector4(borderColor.X, borderColor.Y, borderColor.Z, 0.72f)),
                Math.Max(1f, rounding - inset),
                ImDrawFlags.None,
                Math.Max(1.5f, border));
        }

        if (crossed) DrawCross(draw, topLeft, bottomRight);
        if (!string.IsNullOrEmpty(cornerLabel))
        {
            var labelScale = FitTextScale(
                cornerLabel,
                Math.Clamp(size / 24f, 0.65f, 1.35f),
                size - (4f * scale));
            DrawOutlinedText(
                draw,
                new Vector2((topLeft.X + bottomRight.X) * 0.5f, topLeft.Y + (1f * scale)),
                cornerLabel,
                labelScale,
                true);
        }

        if (!string.IsNullOrEmpty(countdown))
        {
            var labelScale = FitTextScale(
                countdown,
                Math.Clamp(size / 28f, 0.58f, 1.05f),
                size - (4f * scale));
            DrawOutlinedText(
                draw,
                new Vector2((topLeft.X + bottomRight.X) * 0.5f, bottomRight.Y - (ImGui.GetFontSize() * labelScale)),
                countdown,
                labelScale,
                true);
        }
    }

    private void DrawTextBadge(Vector2 topLeft, Vector2 bottomRight, string label, Vector4 accent)
    {
        var draw = ImGui.GetForegroundDrawList();
        var size = bottomRight.X - topLeft.X;
        var rounding = Math.Max(3f, size * 0.16f);
        draw.AddRectFilled(
            topLeft,
            bottomRight,
            Pack(new Vector4(0.01f, 0.015f, 0.03f, configuration.NameplateBackgroundOpacity)),
            rounding);
        draw.AddRect(
            topLeft,
            bottomRight,
            Pack(accent),
            rounding,
            ImDrawFlags.None,
            Math.Max(2f, size * 0.075f));
        var labelScale = FitTextScale(
            label,
            Math.Clamp(size / 24f, 0.7f, 1.4f),
            size - (5f * ImGuiHelpers.GlobalScale));
        var center = (topLeft + bottomRight) * 0.5f;
        DrawOutlinedText(
            draw,
            new Vector2(center.X, center.Y - (ImGui.GetFontSize() * labelScale * 0.5f)),
            label,
            labelScale,
            true);
    }

    private static float FitTextScale(string text, float desiredScale, float maximumWidth)
    {
        var width = ImGui.CalcTextSize(text).X;
        return width <= 0f
            ? desiredScale
            : Math.Max(0.35f, Math.Min(desiredScale, maximumWidth / width));
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
        var nativeMin = new Vector2(screen.X * 0.72f, screen.Y * 0.62f);
        var nativeMax = nativeMin + new Vector2(nativeSize);
        var anchor = new NamePlateAnchorSnapshot(0, 1, nativeMin, nativeMax, Environment.TickCount64);
        var enemy = new EnemyHudSnapshot(
            3,
            0,
            0,
            30,
            SeitonCueKind.Execute,
            Environment.TickCount64,
            true,
            18.4f,
            20000,
            55500,
            true,
            1200,
            10000);

        TryDrawGameIcon(
            ImGui.GetForegroundDrawList(),
            EnemyCombatConstants.JobIconBaseId + enemy.JobId,
            nativeMin,
            nativeMax,
            1f);
        var now = Environment.TickCount64;
        var pressurePreview = new TargetPressureOpponentSnapshot(
            0,
            1,
            30,
            3,
            TargetPressureEvidence.HardTarget,
            3,
            [new CcProtectionDisplay(
                EnemyCombatConstants.ResilienceStatusId,
                "Resilience",
                214891,
                CcProtectionKind.FullImmunity,
                now + 1_900)]);
        DrawIndicatorSlots(anchor, enemy, pressurePreview, now);

        var mchWarning = new PersonalStatusSnapshot(
            EnemyCombatConstants.MarksmanSpiteActionId,
            "Marksman's Spite",
            EnemyCombatConstants.MarksmanSpiteIconId,
            PersonalDebuffAlertKind.Warning,
            2,
            2,
            1_800,
            now + 1_800,
            now,
            true);
        var purifyWarning = new PersonalStatusSnapshot(
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
        var previewStatuses = new[] { mchWarning, purifyWarning };
        var previewHeights = previewStatuses.Select(status => PersonalWarningCardHeight(status, now)).ToArray();
        var previewOffsets = BuildCenteredOffsets(previewHeights, 7f * ImGuiHelpers.GlobalScale);
        var warningCenter = ImGui.GetIO().DisplaySize.Y *
                            Math.Clamp(configuration.PersonalWarningScreenY, 0.08f, 0.9f);
        DrawPersonalWarningCard(
            mchWarning,
            EmergencyPurifyProbeSnapshot.Initial,
            warningCenter + previewOffsets[0],
            now);
        DrawPersonalWarningCard(
            purifyWarning,
            EmergencyPurifyProbeSnapshot.Initial with
            {
                Phase = EmergencyPurifyBufferPhase.WaitingForFreshKey,
                StatusInstance = new PurifyCcStatusInstance(
                    EnemyCombatConstants.MiracleOfNatureStatusId,
                    1),
            },
            warningCenter + previewOffsets[1],
            now);
    }

    private void DrawCcProtectionPreview()
    {
        var screen = ImGui.GetIO().DisplaySize;
        var nativeSize = 28f * ImGuiHelpers.GlobalScale;
        var nativeMin = new Vector2(
            (screen.X * 0.5f) - (nativeSize * 0.5f),
            screen.Y * 0.62f);
        var nativeMax = nativeMin + new Vector2(nativeSize);
        var anchor = new NamePlateAnchorSnapshot(
            0,
            1,
            nativeMin,
            nativeMax,
            Environment.TickCount64);
        TryDrawGameIcon(
            ImGui.GetForegroundDrawList(),
            EnemyCombatConstants.JobIconBaseId + 30,
            nativeMin,
            nativeMax,
            1f);

        var now = Environment.TickCount64;
        DrawCcProtectionEmblem(
            anchor,
            [new CcProtectionDisplay(
                EnemyCombatConstants.ResilienceStatusId,
                "Resilience",
                214891,
                CcProtectionKind.FullImmunity,
                now + 1_900)],
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
        float alpha = 1f,
        Vector4? foregroundColor = null)
    {
        var size = ImGui.CalcTextSize(text) * textScale;
        var origin = centered ? new Vector2(position.X - (size.X * 0.5f), position.Y) : position;
        var offset = Math.Max(1f, ImGuiHelpers.GlobalScale);
        var font = ImGui.GetFont();
        var fontSize = ImGui.GetFontSize() * textScale;
        var shadow = new Vector4(ShadowColor.X, ShadowColor.Y, ShadowColor.Z, ShadowColor.W * alpha);
        var foreground = foregroundColor ?? TextColor;
        var textColor = new Vector4(foreground.X, foreground.Y, foreground.Z, foreground.W * alpha);
        draw.AddText(font, fontSize, origin + new Vector2(-offset, 0f), Pack(shadow), text);
        draw.AddText(font, fontSize, origin + new Vector2(offset, 0f), Pack(shadow), text);
        draw.AddText(font, fontSize, origin + new Vector2(0f, -offset), Pack(shadow), text);
        draw.AddText(font, fontSize, origin + new Vector2(0f, offset), Pack(shadow), text);
        draw.AddText(font, fontSize, origin, Pack(textColor), text);
    }

    private static uint Pack(Vector4 color) => ImGui.GetColorU32(color);
}
