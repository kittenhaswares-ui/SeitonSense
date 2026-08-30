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

public readonly record struct DragoonLimitBreakWarningObservation(
    TargetPressureActorIdentity Actor,
    bool IsEnemy,
    int EnemySlot,
    uint JobId,
    uint ActivationActionId,
    uint IconId,
    CombatLimitBreakPresentationKind Presentation,
    bool DurationConfirmed,
    uint EvidenceStatusId,
    long ActivatedAtMilliseconds,
    long ExpiresAtMilliseconds,
    long SnapshotPublishedAtMilliseconds,
    ulong EpisodeToken);

public readonly record struct DragoonLimitBreakWarningPlan(
    uint IconId,
    bool ShowCountdown,
    long RemainingMilliseconds,
    ulong EpisodeToken);

public readonly record struct SummonerLimitBreakWarningObservation(
    TargetPressureActorIdentity Actor,
    bool IsEnemy,
    int EnemySlot,
    uint JobId,
    uint ActivationActionId,
    uint IconId,
    CombatLimitBreakPresentationKind Presentation,
    bool DurationConfirmed,
    uint EvidenceStatusId,
    long ActivatedAtMilliseconds,
    long ExpiresAtMilliseconds,
    long SnapshotPublishedAtMilliseconds,
    ulong EpisodeToken);

public readonly record struct SummonerLimitBreakWarningPlan(
    uint IconId,
    string SummonName,
    bool ShowCountdown,
    long RemainingMilliseconds,
    ulong EpisodeToken);

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
    public const uint DragoonJobId = 22;
    public const uint DragoonSkyHighActionId = 29_497;
    public const uint DragoonSkyHighIconId = 9_652;
    public const uint DragoonSkyHighStatusId = 3_180;
    public const long MaximumDragoonAirborneEpisodeMilliseconds = 5_000;
    public const uint SummonerJobId = 27;
    public const uint SummonBahamutActionId = 29_673;
    public const uint SummonPhoenixActionId = 29_678;
    public const uint SummonBahamutIconId = 9_681;
    public const uint SummonPhoenixIconId = 9_683;
    public const uint DreadwyrmTranceStatusId = 3_228;
    public const uint FirebirdTranceStatusId = 3_229;
    public const long MaximumSummonerEpisodeMilliseconds = 30_000;
    public const long MaximumSnapshotAgeMilliseconds = 500;
    public const long AllyDamageLifetimeMilliseconds = 3_000;
    public const int MaximumVisibleDamageCards = 3;
    public const int MaximumVisibleDangerBanners = 3;
    public const int SelfSlot = 0;
    public const int FirstPartySlot = 1;
    public const int LastPartySlot = 5;
    public const int FirstEnemySlot = 1;
    public const int LastEnemySlot = 5;

    private const float SelfBannerWidth = 520f;
    private const float SelfBannerHeight = 84f;
    private const float SelfBannerMinimumWidth = 300f;
    private const float SelfBannerTopOffset = 168f;
    private const float EnemyDangerBannerTopOffset = 28f;
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

    /// <summary>
    /// Admits only the exact enemy DRG Sky High activation episode. The first
    /// short flash is enough to warn immediately; a longer countdown requires
    /// the exact live Sky High caster status from that same episode. The later
    /// Sky Shatter impact phase is deliberately not an airborne warning.
    /// </summary>
    public static bool TryBuildDragoonWarningPlan(
        in DragoonLimitBreakWarningObservation observation,
        long nowMilliseconds,
        out DragoonLimitBreakWarningPlan plan)
    {
        plan = default;
        if (!observation.Actor.IsValid ||
            !observation.IsEnemy ||
            observation.EnemySlot is < FirstEnemySlot or > LastEnemySlot ||
            observation.JobId != DragoonJobId ||
            observation.ActivationActionId != DragoonSkyHighActionId ||
            observation.IconId != DragoonSkyHighIconId ||
            observation.Presentation != CombatLimitBreakPresentationKind.Duration ||
            observation.ActivatedAtMilliseconds < 0 ||
            observation.ActivatedAtMilliseconds > nowMilliseconds ||
            observation.ExpiresAtMilliseconds <= nowMilliseconds ||
            observation.EpisodeToken == 0 ||
            !IsFresh(observation.SnapshotPublishedAtMilliseconds, nowMilliseconds))
        {
            return false;
        }

        var lifetime = observation.ExpiresAtMilliseconds - observation.ActivatedAtMilliseconds;
        if (observation.DurationConfirmed)
        {
            if (observation.EvidenceStatusId != DragoonSkyHighStatusId ||
                lifetime is <= 0 or > MaximumDragoonAirborneEpisodeMilliseconds)
            {
                return false;
            }
        }
        else if (observation.EvidenceStatusId != 0 ||
                 lifetime is <= 0 or > CombatLimitBreakCatalog.InstantFlashMilliseconds)
        {
            return false;
        }

        plan = new DragoonLimitBreakWarningPlan(
            observation.IconId,
            observation.DurationConfirmed,
            observation.ExpiresAtMilliseconds - nowMilliseconds,
            observation.EpisodeToken);
        return true;
    }

    /// <summary>
    /// Admits only exact enemy Summon Bahamut/Phoenix activation pairs. The
    /// activation produces an immediate bounded flash; a timer requires the
    /// matching live trance status from the same runtime episode.
    /// </summary>
    public static bool TryBuildSummonerWarningPlan(
        in SummonerLimitBreakWarningObservation observation,
        long nowMilliseconds,
        out SummonerLimitBreakWarningPlan plan)
    {
        plan = default;
        if (!observation.Actor.IsValid ||
            !observation.IsEnemy ||
            observation.EnemySlot is < FirstEnemySlot or > LastEnemySlot ||
            observation.JobId != SummonerJobId ||
            observation.Presentation != CombatLimitBreakPresentationKind.Duration ||
            observation.ActivatedAtMilliseconds < 0 ||
            observation.ActivatedAtMilliseconds > nowMilliseconds ||
            observation.ExpiresAtMilliseconds <= nowMilliseconds ||
            observation.EpisodeToken == 0 ||
            !IsFresh(observation.SnapshotPublishedAtMilliseconds, nowMilliseconds))
        {
            return false;
        }

        var bahamut = observation.ActivationActionId == SummonBahamutActionId &&
                      observation.IconId == SummonBahamutIconId;
        var phoenix = observation.ActivationActionId == SummonPhoenixActionId &&
                      observation.IconId == SummonPhoenixIconId;
        if (!bahamut && !phoenix) return false;

        var lifetime = observation.ExpiresAtMilliseconds - observation.ActivatedAtMilliseconds;
        if (observation.DurationConfirmed)
        {
            var exactStatus = bahamut
                ? observation.EvidenceStatusId == DreadwyrmTranceStatusId
                : observation.EvidenceStatusId == FirebirdTranceStatusId;
            if (!exactStatus || lifetime is <= 0 or > MaximumSummonerEpisodeMilliseconds)
                return false;
        }
        else if (observation.EvidenceStatusId != 0 ||
                 lifetime is <= 0 or > CombatLimitBreakCatalog.InstantFlashMilliseconds)
        {
            return false;
        }

        plan = new SummonerLimitBreakWarningPlan(
            observation.IconId,
            bahamut ? "BAHAMUT SUMMONED" : "PHOENIX SUMMONED",
            observation.DurationConfirmed,
            observation.ExpiresAtMilliseconds - nowMilliseconds,
            observation.EpisodeToken);
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

    public static bool TryBuildEnemyDangerBannerRectangle(
        float workLeft,
        float workTop,
        float workWidth,
        float workHeight,
        float uiScale,
        out LimitBreakNotificationRectangle rectangle) =>
        TryBuildTopCenterBannerRectangle(
            workLeft,
            workTop,
            workWidth,
            workHeight,
            uiScale,
            EnemyDangerBannerTopOffset,
            out rectangle);

    public static bool TryBuildEnemyDangerBannerRectangles(
        float workLeft,
        float workTop,
        float workWidth,
        float workHeight,
        float uiScale,
        int bannerCount,
        out LimitBreakNotificationRectangle[] rectangles)
    {
        rectangles = [];
        if (bannerCount is < 1 or > MaximumVisibleDangerBanners ||
            !TryBuildEnemyDangerBannerRectangle(
                workLeft,
                workTop,
                workWidth,
                workHeight,
                uiScale,
                out var first))
        {
            return false;
        }

        var gap = 8f * uiScale;
        var height = first.Height;
        var safeBandBottom = workTop + (workHeight * 0.45f);
        var result = new List<LimitBreakNotificationRectangle>(bannerCount);
        for (var index = 0; index < bannerCount; index++)
        {
            var top = first.Top + (index * (height + gap));
            var rectangle = first with { Top = top, Bottom = top + height };
            if (!rectangle.IsValid || rectangle.Bottom > safeBandBottom + 0.001f)
                break;
            result.Add(rectangle);
        }

        rectangles = result.ToArray();
        return rectangles.Length > 0;
    }

    public static bool TryBuildSelfBannerRectangleBelow(
        float workLeft,
        float workTop,
        float workWidth,
        float workHeight,
        float uiScale,
        in LimitBreakNotificationRectangle upperBanner,
        out LimitBreakNotificationRectangle rectangle)
    {
        if (!TryBuildSelfBannerRectangle(
                workLeft,
                workTop,
                workWidth,
                workHeight,
                uiScale,
                out rectangle) ||
            !upperBanner.IsValid)
        {
            return false;
        }

        if (!RectanglesOverlap(rectangle, upperBanner)) return true;
        var shiftedTop = upperBanner.Bottom + (12f * uiScale);
        var height = rectangle.Bottom - rectangle.Top;
        var shifted = rectangle with
        {
            Top = shiftedTop,
            Bottom = shiftedTop + height,
        };
        var safeBandBottom = workTop + (workHeight * 0.65f);
        if (!shifted.IsValid ||
            shifted.Left < workLeft ||
            shifted.Right > workLeft + workWidth ||
            shifted.Top < workTop ||
            shifted.Bottom > safeBandBottom)
        {
            rectangle = default;
            return false;
        }

        rectangle = shifted;
        return true;
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

    private static bool RectanglesOverlap(
        in LimitBreakNotificationRectangle first,
        in LimitBreakNotificationRectangle second) =>
        first.Left < second.Right &&
        first.Right > second.Left &&
        first.Top < second.Bottom &&
        first.Bottom > second.Top;

    private static bool TryBuildTopCenterBannerRectangle(
        float workLeft,
        float workTop,
        float workWidth,
        float workHeight,
        float uiScale,
        float topOffset,
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
            workTop + (topOffset * uiScale),
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
