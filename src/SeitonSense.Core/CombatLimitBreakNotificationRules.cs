namespace SeitonSense.Core;

public readonly record struct CombatLimitBreakSelfNotificationObservation(
    bool IsSelf,
    int Slot,
    uint IconId,
    CombatLimitBreakPresentationKind Presentation,
    bool DurationConfirmed,
    long ActivatedAtMilliseconds,
    long ExpiresAtMilliseconds,
    long SnapshotPublishedAtMilliseconds);

public readonly record struct CombatLimitBreakSelfNotificationPlan(
    uint IconId,
    bool ShowCountdown,
    long RemainingMilliseconds);

public readonly record struct CombatLimitBreakDamageNotificationObservation(
    TargetPressureActorIdentity Caster,
    int CasterPartySlot,
    TargetPressureActorIdentity Target,
    int TargetEnemySlot,
    uint IconId,
    uint Damage,
    long ObservedAtMilliseconds,
    long ExpiresAtMilliseconds,
    long SnapshotPublishedAtMilliseconds,
    ulong EpisodeToken,
    ulong EventToken);

public readonly record struct CombatLimitBreakDamageNotificationPlan(
    int CasterPartySlot,
    int TargetEnemySlot,
    uint IconId,
    uint Damage,
    long RemainingMilliseconds,
    ulong EventToken);

public readonly record struct LimitBreakNotificationRectangle(
    float Left,
    float Top,
    float Right,
    float Bottom)
{
    public float Width => Right - Left;
    public float Height => Bottom - Top;
    public bool IsValid =>
        float.IsFinite(Left) &&
        float.IsFinite(Top) &&
        float.IsFinite(Right) &&
        float.IsFinite(Bottom) &&
        Right > Left &&
        Bottom > Top;
}

/// <summary>
/// Pure admission and screen-space layout rules for LB notifications that do
/// not depend on the retired combat-frame surface.
/// </summary>
public static class CombatLimitBreakNotificationRules
{
    public const long MaximumSnapshotAgeMilliseconds = 500;
    public const long AllyDamageLifetimeMilliseconds = 3_000;
    public const int MaximumVisibleDamageCards = 3;
    public const int SelfSlot = 0;
    public const int FirstPartySlot = 1;
    public const int LastPartySlot = 5;
    public const int FirstEnemySlot = 1;
    public const int LastEnemySlot = 5;

    private const float SelfBannerWidth = 520f;
    private const float SelfBannerHeight = 84f;
    private const float SelfBannerMinimumWidth = 300f;
    private const float SelfBannerTopOffset = 168f;
    private const float DamageCardWidth = 330f;
    private const float DamageCardHeight = 66f;
    private const float DamageCardGap = 7f;
    private const float DamageCardTopOffset = 190f;
    private const float ViewportPadding = 12f;

    public static bool TryBuildSelfPlan(
        in CombatLimitBreakSelfNotificationObservation observation,
        long nowMilliseconds,
        out CombatLimitBreakSelfNotificationPlan plan)
    {
        plan = default;
        if (!observation.IsSelf ||
            observation.Slot != SelfSlot ||
            observation.IconId == 0 ||
            observation.Presentation is not (
                CombatLimitBreakPresentationKind.Instant or
                CombatLimitBreakPresentationKind.Duration) ||
            observation.ActivatedAtMilliseconds < 0 ||
            observation.ActivatedAtMilliseconds > nowMilliseconds ||
            observation.ExpiresAtMilliseconds <= nowMilliseconds ||
            !IsFresh(observation.SnapshotPublishedAtMilliseconds, nowMilliseconds))
        {
            return false;
        }

        var showCountdown = observation.Presentation == CombatLimitBreakPresentationKind.Duration &&
                            observation.DurationConfirmed;
        if (!showCountdown)
        {
            var flashDuration = observation.ExpiresAtMilliseconds - observation.ActivatedAtMilliseconds;
            if (flashDuration is <= 0 or > CombatLimitBreakCatalog.InstantFlashMilliseconds)
                return false;
        }

        plan = new CombatLimitBreakSelfNotificationPlan(
            observation.IconId,
            showCountdown,
            observation.ExpiresAtMilliseconds - nowMilliseconds);
        return true;
    }

    public static bool TryBuildDamagePlan(
        in CombatLimitBreakDamageNotificationObservation observation,
        long nowMilliseconds,
        out CombatLimitBreakDamageNotificationPlan plan)
    {
        plan = default;
        if (!observation.Caster.IsValid ||
            !observation.Target.IsValid ||
            observation.Caster == observation.Target ||
            observation.CasterPartySlot is < FirstPartySlot or > LastPartySlot ||
            observation.TargetEnemySlot is < FirstEnemySlot or > LastEnemySlot ||
            observation.IconId == 0 ||
            observation.Damage == 0 ||
            observation.ObservedAtMilliseconds < 0 ||
            observation.ObservedAtMilliseconds > nowMilliseconds ||
            observation.ExpiresAtMilliseconds <= nowMilliseconds ||
            observation.EpisodeToken == 0 ||
            observation.EventToken == 0 ||
            !IsFresh(observation.SnapshotPublishedAtMilliseconds, nowMilliseconds))
        {
            return false;
        }

        var lifetime = observation.ExpiresAtMilliseconds - observation.ObservedAtMilliseconds;
        if (lifetime is <= 0 or > AllyDamageLifetimeMilliseconds) return false;

        plan = new CombatLimitBreakDamageNotificationPlan(
            observation.CasterPartySlot,
            observation.TargetEnemySlot,
            observation.IconId,
            observation.Damage,
            observation.ExpiresAtMilliseconds - nowMilliseconds,
            observation.EventToken);
        return true;
    }

    /// <summary>
    /// Places the self notification in a fixed top-center lane, detached from
    /// the self combat frame and the usual lower-screen HP/MP HUD region.
    /// </summary>
    public static bool TryBuildSelfBannerRectangle(
        float workLeft,
        float workTop,
        float workWidth,
        float workHeight,
        float uiScale,
        out LimitBreakNotificationRectangle rectangle)
    {
        rectangle = default;
        if (!IsValidViewport(workLeft, workTop, workWidth, workHeight, uiScale)) return false;

        var padding = ViewportPadding * uiScale;
        var availableWidth = workWidth - (padding * 2f);
        var width = Math.Min(SelfBannerWidth * uiScale, availableWidth);
        var height = SelfBannerHeight * uiScale;
        if (width < SelfBannerMinimumWidth * uiScale || height + (padding * 2f) > workHeight)
            return false;

        var safeBandBottom = workTop + (workHeight * 0.45f);
        var latestSafeTop = safeBandBottom - height;
        var top = Math.Clamp(
            workTop + (SelfBannerTopOffset * uiScale),
            workTop + padding,
            Math.Max(workTop + padding, latestSafeTop));
        var left = workLeft + ((workWidth - width) * 0.5f);
        rectangle = new LimitBreakNotificationRectangle(left, top, left + width, top + height);
        return rectangle.IsValid &&
               rectangle.Left >= workLeft &&
               rectangle.Right <= workLeft + workWidth &&
               rectangle.Top >= workTop &&
               rectangle.Bottom <= workTop + workHeight &&
               rectangle.Bottom <= safeBandBottom + 0.001f;
    }

    public static bool TryBuildDamageCardRectangles(
        float workLeft,
        float workTop,
        float workWidth,
        float workHeight,
        float uiScale,
        int cardCount,
        out LimitBreakNotificationRectangle[] rectangles)
    {
        rectangles = [];
        if (!IsValidViewport(workLeft, workTop, workWidth, workHeight, uiScale) ||
            cardCount is < 1 or > MaximumVisibleDamageCards)
        {
            return false;
        }

        var padding = ViewportPadding * uiScale;
        var width = Math.Min(DamageCardWidth * uiScale, workWidth - (padding * 2f));
        var height = DamageCardHeight * uiScale;
        var gap = DamageCardGap * uiScale;
        var totalHeight = (height * cardCount) + (gap * (cardCount - 1));
        if (width < 250f * uiScale || totalHeight + (padding * 2f) > workHeight)
            return false;

        var left = workLeft + padding;
        var desiredTop = workTop + Math.Max(DamageCardTopOffset * uiScale, workHeight * 0.3f);
        var top = Math.Clamp(
            desiredTop,
            workTop + padding,
            workTop + workHeight - padding - totalHeight);
        var result = new LimitBreakNotificationRectangle[cardCount];
        for (var index = 0; index < cardCount; index++)
        {
            var cardTop = top + (index * (height + gap));
            var card = new LimitBreakNotificationRectangle(
                left,
                cardTop,
                left + width,
                cardTop + height);
            if (!card.IsValid ||
                card.Right > workLeft + workWidth ||
                card.Bottom > workTop + workHeight)
            {
                return false;
            }

            result[index] = card;
        }

        rectangles = result;
        return true;
    }

    private static bool IsFresh(long publishedAtMilliseconds, long nowMilliseconds) =>
        publishedAtMilliseconds >= 0 &&
        nowMilliseconds >= publishedAtMilliseconds &&
        nowMilliseconds - publishedAtMilliseconds <= MaximumSnapshotAgeMilliseconds;

    private static bool IsValidViewport(
        float workLeft,
        float workTop,
        float workWidth,
        float workHeight,
        float uiScale) =>
        float.IsFinite(workLeft) &&
        float.IsFinite(workTop) &&
        float.IsFinite(workWidth) &&
        float.IsFinite(workHeight) &&
        float.IsFinite(uiScale) &&
        workWidth > 0f &&
        workHeight > 0f &&
        uiScale is >= 0.5f and <= 4f;
}
