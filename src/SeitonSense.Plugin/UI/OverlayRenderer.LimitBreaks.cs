using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using SeitonSense.Core;
using SeitonSense.Plugin.Services;

namespace SeitonSense.Plugin.UI;

internal sealed partial class OverlayRenderer
{
    private static readonly Vector4 LimitBreakNameplateColor = new(1f, 0.7f, 0.1f, 1f);
    private static readonly Vector4 LimitBreakTimerColor = new(1f, 0.93f, 0.58f, 1f);

    private CombatLimitBreakNameplateSource? combatLimitBreakNameplateSource;

    /// <summary>
    /// Attaches the already-owned runtime once during plugin composition. The
    /// overlay neither starts nor disposes it; a conflicting second attachment
    /// is rejected instead of silently switching evidence providers.
    /// </summary>
    internal void AttachCombatLimitBreakRuntime(CombatLimitBreakRuntimeService runtime) =>
        AttachCombatLimitBreakRuntime(runtime, static () => true);

    internal void AttachCombatLimitBreakRuntime(
        CombatLimitBreakRuntimeService runtime,
        Func<bool> enabledProvider)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(enabledProvider);
        var source = new CombatLimitBreakNameplateSource(runtime, enabledProvider);
        if (Interlocked.CompareExchange(ref combatLimitBreakNameplateSource, source, null) is not null)
            throw new InvalidOperationException("The LB nameplate source is already attached.");
    }

    private CombatLimitBreakRuntimeSnapshot GetCombatLimitBreakNameplateSnapshot()
    {
        var source = Volatile.Read(ref combatLimitBreakNameplateSource);
        if (source is null) return CombatLimitBreakRuntimeSnapshot.Inactive;

        try
        {
            var snapshot = source.EnabledProvider()
                ? source.Runtime.Snapshot
                : CombatLimitBreakRuntimeSnapshot.Inactive;
            return snapshot.Active ? snapshot : CombatLimitBreakRuntimeSnapshot.Inactive;
        }
        catch
        {
            // A configuration/provider fault must only hide this optional layer.
            return CombatLimitBreakRuntimeSnapshot.Inactive;
        }
    }

    private void DrawStackedNameplateEmblems(
        NamePlateAnchorSnapshot anchor,
        IReadOnlyList<CcProtectionDisplay> activeProtections,
        CombatLimitBreakActorState? limitBreak,
        long limitBreakSnapshotPublishedAtMilliseconds,
        long nowMilliseconds)
    {
        var anchorActor = new TargetPressureActorIdentity(anchor.GameObjectId, anchor.EntityId);
        var limitBreakPlan = default(CombatLimitBreakNameplateDisplayPlan);
        var hasLimitBreak = limitBreak is { } state &&
                            CombatLimitBreakNameplateRules.TryBuildDisplayPlan(
                                anchorActor,
                                anchor.CapturedAtMilliseconds,
                                new CombatLimitBreakNameplateObservation(
                                    state.Actor,
                                    state.Side == CombatLimitBreakRosterSide.Enemy,
                                    state.Slot,
                                    state.IconId,
                                    state.Presentation,
                                    state.DurationConfirmed,
                                    state.ActivatedAtMilliseconds,
                                    state.ExpiresAtMilliseconds,
                                    limitBreakSnapshotPublishedAtMilliseconds),
                                nowMilliseconds,
                                out limitBreakPlan);
        var protection = configuration.ShowCcProtection
            ? activeProtections
                .Where(candidate => candidate.ExpiresAtMilliseconds > nowMilliseconds)
                .OrderByDescending(static candidate => candidate.ExpiresAtMilliseconds)
                .ThenByDescending(static candidate => candidate.Kind)
                .ThenBy(static candidate => candidate.StatusId)
                .FirstOrDefault()
            : default;
        var hasProtection = protection.StatusId != 0;
        if (!hasLimitBreak && !hasProtection) return;

        var uiScale = Math.Max(0.5f, ImGuiHelpers.GlobalScale);
        var nativeHeight = Math.Max(1f, anchor.Height);
        var limitBreakMetrics = hasLimitBreak
            ? BuildLimitBreakMetrics(nativeHeight, uiScale, limitBreakPlan.ShowCountdown)
            : default;
        var protectionMetrics = hasProtection
            ? BuildProtectionMetrics(nativeHeight, uiScale)
            : default;

        var requests = new List<NameplateVerticalStackRequest>(2);
        var kinds = new List<NameplateEmblemKind>(2);
        if (hasLimitBreak)
        {
            requests.Add(new NameplateVerticalStackRequest(
                limitBreakMetrics.TotalHeight,
                limitBreakMetrics.MinimumTotalHeight));
            kinds.Add(NameplateEmblemKind.LimitBreak);
        }

        if (hasProtection)
        {
            requests.Add(new NameplateVerticalStackRequest(
                protectionMetrics.TotalHeight,
                protectionMetrics.MinimumTotalHeight));
            kinds.Add(NameplateEmblemKind.CcProtection);
        }

        var screen = ImGui.GetIO().DisplaySize;
        var topPadding = 11f * uiScale;
        var anchorGap = Math.Max(7f * uiScale, configuration.NameplateIconSpacing * uiScale);
        // Both existing badges paint a small outline outside their nominal
        // rectangles, so reserve enough air that those pixels cannot overlap.
        var interBlockGap = Math.Max(10f * uiScale, configuration.NameplateIconSpacing * uiScale);
        if (!CombatLimitBreakNameplateRules.TryBuildVerticalStack(
                anchor.JobIconTopLeft.Y,
                topPadding,
                anchorGap,
                interBlockGap,
                requests,
                out var placements))
        {
            // Do not regress the existing CC warning at the extreme top edge.
            // If both minimum-size blocks cannot fit, retain only CC; otherwise
            // the single LB block still gets its own exact admission check.
            if (hasProtection && hasLimitBreak)
            {
                requests =
                [
                    new NameplateVerticalStackRequest(
                        protectionMetrics.TotalHeight,
                        protectionMetrics.MinimumTotalHeight),
                ];
                kinds = [NameplateEmblemKind.CcProtection];
                if (!CombatLimitBreakNameplateRules.TryBuildVerticalStack(
                        anchor.JobIconTopLeft.Y,
                        topPadding,
                        anchorGap,
                        interBlockGap,
                        requests,
                        out placements))
                {
                    return;
                }
            }
            else
                return;
        }

        var nativeCenterX = (anchor.JobIconTopLeft.X + anchor.JobIconBottomRight.X) * 0.5f;
        for (var index = 0; index < placements.Length; index++)
        {
            var placement = placements[index];
            switch (kinds[index])
            {
                case NameplateEmblemKind.LimitBreak:
                    DrawLimitBreakPlacement(
                        nativeCenterX,
                        screen.X,
                        placement,
                        limitBreakMetrics,
                        limitBreakPlan,
                        uiScale);
                    break;
                case NameplateEmblemKind.CcProtection:
                    DrawProtectionPlacement(
                        nativeCenterX,
                        screen.X,
                        placement,
                        protectionMetrics,
                        protection,
                        nowMilliseconds,
                        uiScale);
                    break;
            }
        }
    }

    private void DrawLimitBreakPlacement(
        float nativeCenterX,
        float screenWidth,
        NameplateVerticalStackPlacement placement,
        NameplateEmblemMetrics metrics,
        CombatLimitBreakNameplateDisplayPlan plan,
        float uiScale)
    {
        var iconSize = metrics.IconSize * placement.Scale;
        var timerGap = metrics.TimerGap * placement.Scale;
        var timerHeight = metrics.TimerHeight * placement.Scale;
        var glowMargin = Math.Max(12f * uiScale, iconSize * 0.36f);
        var centerX = ClampNameplateCenter(nativeCenterX, screenWidth, iconSize, glowMargin);
        var iconMax = PixelSnap(new Vector2(centerX + (iconSize * 0.5f), placement.Bottom));
        var iconMin = PixelSnap(iconMax - new Vector2(iconSize));

        DrawIconBadge(
            iconMin,
            iconMax,
            plan.IconId,
            LimitBreakNameplateColor,
            crossed: false,
            cornerLabel: "LB",
            countdown: null,
            emphasized: true);

        if (!plan.ShowCountdown || timerHeight <= 0f) return;
        var timerMax = PixelSnap(new Vector2(iconMax.X, iconMin.Y - timerGap));
        var timerMin = PixelSnap(new Vector2(iconMin.X, timerMax.Y - timerHeight));
        DrawLimitBreakTimer(timerMin, timerMax, plan.RemainingMilliseconds, uiScale);
    }

    private void DrawProtectionPlacement(
        float nativeCenterX,
        float screenWidth,
        NameplateVerticalStackPlacement placement,
        NameplateEmblemMetrics metrics,
        CcProtectionDisplay protection,
        long nowMilliseconds,
        float uiScale)
    {
        var emblemSize = metrics.IconSize * placement.Scale;
        var timerGap = metrics.TimerGap * placement.Scale;
        var timerHeight = metrics.TimerHeight * placement.Scale;
        var glowMargin = Math.Max(12f * uiScale, emblemSize * 0.36f);
        var centerX = ClampNameplateCenter(nativeCenterX, screenWidth, emblemSize, glowMargin);
        var emblemMax = PixelSnap(new Vector2(centerX + (emblemSize * 0.5f), placement.Bottom));
        var emblemMin = PixelSnap(emblemMax - new Vector2(emblemSize));
        var timerMax = PixelSnap(new Vector2(emblemMax.X, emblemMin.Y - timerGap));
        var timerMin = PixelSnap(new Vector2(emblemMin.X, timerMax.Y - timerHeight));
        DrawCcProtectionEmblem(emblemMin, emblemMax, timerMin, timerMax, protection, nowMilliseconds);
    }

    private void DrawLimitBreakTimer(
        Vector2 timerMin,
        Vector2 timerMax,
        long remainingMilliseconds,
        float uiScale)
    {
        if (remainingMilliseconds <= 0 || timerMax.X <= timerMin.X || timerMax.Y <= timerMin.Y) return;
        var draw = ImGui.GetForegroundDrawList();
        var timerText = $"{remainingMilliseconds / 1000d:0.0}s";
        var height = timerMax.Y - timerMin.Y;
        var rounding = Math.Max(5f, height * 0.3f);
        draw.AddRectFilled(
            timerMin - new Vector2(2f * uiScale),
            timerMax + new Vector2(2f * uiScale),
            Pack(new Vector4(0f, 0f, 0f, 0.94f)),
            rounding + (2f * uiScale));
        draw.AddRectFilled(
            timerMin,
            timerMax,
            Pack(new Vector4(0.22f, 0.11f, 0.005f, 0.97f)),
            rounding);
        draw.AddRect(
            timerMin,
            timerMax,
            Pack(LimitBreakNameplateColor),
            rounding,
            ImDrawFlags.None,
            Math.Max(2f * uiScale, height * 0.08f));
        var timerScale = FitTextScale(
            timerText,
            Math.Clamp(height / (ImGui.GetFontSize() * 0.9f), 0.8f, 1.65f),
            (timerMax.X - timerMin.X) - (8f * uiScale));
        var center = (timerMin + timerMax) * 0.5f;
        DrawOutlinedText(
            draw,
            new Vector2(center.X, center.Y - (ImGui.GetFontSize() * timerScale * 0.5f)),
            timerText,
            timerScale,
            true,
            1f,
            LimitBreakTimerColor);
    }

    private NameplateEmblemMetrics BuildLimitBreakMetrics(
        float nativeHeight,
        float uiScale,
        bool showCountdown)
    {
        var iconSize = Math.Clamp(
            nativeHeight * 2.45f * configuration.NameplateIconScale *
            configuration.LimitBreakNameplateScale,
            58f * uiScale,
            96f * uiScale);
        if (!showCountdown)
            return new NameplateEmblemMetrics(iconSize, 0f, 0f, iconSize, 46f * uiScale);

        var timerHeight = Math.Clamp(iconSize * 0.34f, 20f * uiScale, 31f * uiScale);
        var timerGap = Math.Max(3f * uiScale, iconSize * 0.05f);
        var totalHeight = iconSize + timerGap + timerHeight;
        var minimumScale = Math.Min(
            1f,
            Math.Max(
                (46f * uiScale) / iconSize,
                (17f * uiScale) / timerHeight));
        return new NameplateEmblemMetrics(
            iconSize,
            timerHeight,
            timerGap,
            totalHeight,
            totalHeight * minimumScale);
    }

    private NameplateEmblemMetrics BuildProtectionMetrics(float nativeHeight, float uiScale)
    {
        var emblemSize = Math.Clamp(
            nativeHeight * 2.15f * configuration.CcProtectionEmblemScale,
            52f * uiScale,
            88f * uiScale);
        var timerHeight = Math.Clamp(emblemSize * 0.36f, 21f * uiScale, 31f * uiScale);
        var timerGap = Math.Max(3f * uiScale, emblemSize * 0.05f);
        var totalHeight = emblemSize + timerGap + timerHeight;
        var minimumScale = Math.Min(
            1f,
            Math.Max(
                (34f * uiScale) / emblemSize,
                (16f * uiScale) / timerHeight));
        return new NameplateEmblemMetrics(
            emblemSize,
            timerHeight,
            timerGap,
            totalHeight,
            totalHeight * minimumScale);
    }

    private static float ClampNameplateCenter(
        float desiredCenter,
        float screenWidth,
        float contentSize,
        float margin)
    {
        var minimum = (contentSize * 0.5f) + margin;
        var maximum = Math.Max(minimum, screenWidth - (contentSize * 0.5f) - margin);
        return Math.Clamp(desiredCenter, minimum, maximum);
    }

    private enum NameplateEmblemKind : byte
    {
        LimitBreak = 0,
        CcProtection = 1,
    }

    private readonly record struct NameplateEmblemMetrics(
        float IconSize,
        float TimerHeight,
        float TimerGap,
        float TotalHeight,
        float MinimumTotalHeight);

    private sealed record CombatLimitBreakNameplateSource(
        CombatLimitBreakRuntimeService Runtime,
        Func<bool> EnabledProvider);
}
