namespace SeitonSense.Core;

public readonly record struct CombatLimitBreakNameplateObservation(
    TargetPressureActorIdentity Actor,
    bool IsEnemy,
    int EnemySlot,
    uint IconId,
    CombatLimitBreakPresentationKind Presentation,
    bool DurationConfirmed,
    long ActivatedAtMilliseconds,
    long ExpiresAtMilliseconds,
    long SnapshotPublishedAtMilliseconds);

public readonly record struct CombatLimitBreakNameplateDisplayPlan(
    TargetPressureActorIdentity Actor,
    int EnemySlot,
    uint IconId,
    bool ShowCountdown,
    long RemainingMilliseconds);

public readonly record struct NameplateVerticalStackRequest(
    float DesiredHeight,
    float MinimumHeight);

public readonly record struct NameplateVerticalStackPlacement(
    float Top,
    float Bottom,
    float Scale);

/// <summary>
/// Pure fail-closed admission and layout rules for enemy LB nameplate emblems.
/// The runtime action/status observer remains the authority for activation and
/// expiry; these rules only decide whether one fresh exact actor may be drawn.
/// </summary>
public static class CombatLimitBreakNameplateRules
{
    public const long MaximumAnchorAgeMilliseconds = 250;
    public const long MaximumSnapshotAgeMilliseconds = 500;
    public const int FirstEnemySlot = 1;
    public const int LastEnemySlot = 5;
    public const int MaximumStackedBlocks = 4;

    public static bool TryBuildDisplayPlan(
        TargetPressureActorIdentity anchorActor,
        long anchorCapturedAtMilliseconds,
        in CombatLimitBreakNameplateObservation observation,
        long nowMilliseconds,
        out CombatLimitBreakNameplateDisplayPlan plan)
    {
        plan = default;
        if (!anchorActor.IsValid ||
            observation.Actor != anchorActor ||
            !observation.IsEnemy ||
            observation.EnemySlot is < FirstEnemySlot or > LastEnemySlot ||
            observation.IconId == 0 ||
            observation.Presentation is not (
                CombatLimitBreakPresentationKind.Instant or
                CombatLimitBreakPresentationKind.Duration) ||
            observation.ActivatedAtMilliseconds < 0 ||
            observation.ActivatedAtMilliseconds > nowMilliseconds ||
            observation.ExpiresAtMilliseconds <= nowMilliseconds ||
            !IsFresh(
                anchorCapturedAtMilliseconds,
                nowMilliseconds,
                MaximumAnchorAgeMilliseconds) ||
            !IsFresh(
                observation.SnapshotPublishedAtMilliseconds,
                nowMilliseconds,
                MaximumSnapshotAgeMilliseconds))
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

        plan = new CombatLimitBreakNameplateDisplayPlan(
            observation.Actor,
            observation.EnemySlot,
            observation.IconId,
            showCountdown,
            observation.ExpiresAtMilliseconds - nowMilliseconds);
        return true;
    }

    /// <summary>
    /// Places already-sized blocks from the native nameplate upward. Index zero
    /// is always closest to the nameplate; later blocks are stacked above it.
    /// All blocks share one shrink factor and the complete stack is rejected if
    /// even one block would cross its declared minimum height.
    /// </summary>
    public static bool TryBuildVerticalStack(
        float anchorTop,
        float screenTopPadding,
        float anchorGap,
        float interBlockGap,
        IReadOnlyList<NameplateVerticalStackRequest>? requests,
        out NameplateVerticalStackPlacement[] placements)
    {
        placements = [];
        if (!IsFiniteNonNegative(anchorTop) ||
            !IsFiniteNonNegative(screenTopPadding) ||
            !IsFiniteNonNegative(anchorGap) ||
            !IsFiniteNonNegative(interBlockGap) ||
            anchorTop <= screenTopPadding ||
            requests is null ||
            requests.Count is < 1 or > MaximumStackedBlocks)
        {
            return false;
        }

        double desiredTotal = 0;
        foreach (var request in requests)
        {
            if (!float.IsFinite(request.DesiredHeight) ||
                !float.IsFinite(request.MinimumHeight) ||
                request.DesiredHeight <= 0f ||
                request.MinimumHeight <= 0f ||
                request.MinimumHeight > request.DesiredHeight)
            {
                return false;
            }

            desiredTotal += request.DesiredHeight;
        }

        var fixedGaps = anchorGap + (interBlockGap * (requests.Count - 1));
        var availableHeight = anchorTop - screenTopPadding - fixedGaps;
        if (!float.IsFinite(availableHeight) || availableHeight <= 0f || desiredTotal <= 0d)
            return false;

        var scale = (float)Math.Min(1d, availableHeight / desiredTotal);
        if (!float.IsFinite(scale) || scale <= 0f) return false;
        foreach (var request in requests)
        {
            if ((request.DesiredHeight * scale) + 0.001f < request.MinimumHeight)
                return false;
        }

        var result = new NameplateVerticalStackPlacement[requests.Count];
        var bottom = anchorTop - anchorGap;
        for (var index = 0; index < requests.Count; index++)
        {
            var height = requests[index].DesiredHeight * scale;
            var top = bottom - height;
            if (!float.IsFinite(top) || top + 0.001f < screenTopPadding || bottom <= top)
                return false;
            result[index] = new NameplateVerticalStackPlacement(top, bottom, scale);
            bottom = top - interBlockGap;
        }

        placements = result;
        return true;
    }

    private static bool IsFresh(long publishedAtMilliseconds, long nowMilliseconds, long maximumAgeMilliseconds) =>
        publishedAtMilliseconds >= 0 &&
        nowMilliseconds >= publishedAtMilliseconds &&
        nowMilliseconds - publishedAtMilliseconds <= maximumAgeMilliseconds;

    private static bool IsFiniteNonNegative(float value) => float.IsFinite(value) && value >= 0f;
}
