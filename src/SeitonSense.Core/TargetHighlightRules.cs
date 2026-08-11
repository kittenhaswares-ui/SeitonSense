using System.Globalization;

namespace SeitonSense.Core;

public enum TargetHighlightRelation
{
    Current,
    Focus,
    CurrentAndFocus,
}

public readonly record struct TargetHighlightCandidate(
    ulong GameObjectId,
    bool IsValid,
    uint JobId,
    uint CurrentHp,
    uint MaximumHp,
    float CenterDistanceYalms,
    float LocalHitboxRadius,
    float TargetHitboxRadius,
    int EnemySlot);

public readonly record struct TargetHighlightObservation(
    bool IsLoggedIn,
    bool IsPvP,
    bool IncludeCurrentTarget,
    bool CurrentTargetPvPOnly,
    TargetHighlightCandidate? CurrentTarget,
    bool IncludeFocusTarget,
    bool FocusTargetPvPOnly,
    TargetHighlightCandidate? FocusTarget);

public readonly record struct TargetHighlightPlanItem(
    ulong GameObjectId,
    TargetHighlightRelation Relation,
    uint JobId,
    int? HpPercent,
    float? DistanceYalms,
    int? EnemySlot)
{
    public string HpLabel => TargetHighlightRules.FormatHpPercent(HpPercent);
    public string DistanceLabel => TargetHighlightRules.FormatDistance(DistanceYalms);
    public string EnemySlotLabel => TargetHighlightRules.FormatEnemySlot(EnemySlot);
}

/// <summary>
/// Produces a display-only target plan. The caller owns target acquisition and screen placement;
/// these rules never select, replace, or mutate an in-game target.
/// </summary>
public static class TargetHighlightRules
{
    private const ulong InvalidEntityGameObjectId = 0xE0000000UL;

    public static TargetHighlightPlanItem[] BuildPlan(TargetHighlightObservation observation)
    {
        if (!observation.IsLoggedIn) return [];

        var current = CanInclude(
            observation.IncludeCurrentTarget,
            observation.CurrentTargetPvPOnly,
            observation.IsPvP,
            observation.CurrentTarget);
        var focus = CanInclude(
            observation.IncludeFocusTarget,
            observation.FocusTargetPvPOnly,
            observation.IsPvP,
            observation.FocusTarget);

        if (current is null && focus is null) return [];
        if (current is null) return [CreateItem(focus!.Value, TargetHighlightRelation.Focus)];
        if (focus is null) return [CreateItem(current.Value, TargetHighlightRelation.Current)];

        if (current.Value.GameObjectId == focus.Value.GameObjectId)
        {
            return
            [
                CreateCombinedItem(current.Value, focus.Value),
            ];
        }

        return
        [
            CreateItem(current.Value, TargetHighlightRelation.Current),
            CreateItem(focus.Value, TargetHighlightRelation.Focus),
        ];
    }

    public static bool IsValidGameObjectId(ulong gameObjectId) =>
        gameObjectId is not 0 and not InvalidEntityGameObjectId and not ulong.MaxValue;

    public static bool TryCalculateHpPercent(uint currentHp, uint maximumHp, out int hpPercent)
    {
        if (maximumHp == 0 || currentHp > maximumHp)
        {
            hpPercent = 0;
            return false;
        }

        hpPercent = (int)(((ulong)currentHp * 100UL + ((ulong)maximumHp / 2UL)) / maximumHp);
        return true;
    }

    public static string FormatHpPercent(uint currentHp, uint maximumHp) =>
        TryCalculateHpPercent(currentHp, maximumHp, out var hpPercent)
            ? FormatHpPercent(hpPercent)
            : string.Empty;

    public static string FormatHpPercent(int? hpPercent) =>
        hpPercent is >= 0 and <= 100
            ? $"{hpPercent.Value}%"
            : string.Empty;

    public static bool TryCalculateDistance(
        float centerDistanceYalms,
        float localHitboxRadius,
        float targetHitboxRadius,
        out float distanceYalms)
    {
        if (!float.IsFinite(centerDistanceYalms) ||
            !float.IsFinite(localHitboxRadius) ||
            !float.IsFinite(targetHitboxRadius) ||
            centerDistanceYalms < 0f ||
            localHitboxRadius < 0f ||
            targetHitboxRadius < 0f)
        {
            distanceYalms = 0f;
            return false;
        }

        distanceYalms = Math.Max(0f, centerDistanceYalms - localHitboxRadius - targetHitboxRadius);
        return float.IsFinite(distanceYalms);
    }

    public static string FormatDistance(
        float centerDistanceYalms,
        float localHitboxRadius,
        float targetHitboxRadius) =>
        TryCalculateDistance(
            centerDistanceYalms,
            localHitboxRadius,
            targetHitboxRadius,
            out var distanceYalms)
            ? FormatDistance(distanceYalms)
            : string.Empty;

    public static string FormatDistance(float? distanceYalms)
    {
        if (distanceYalms is null ||
            !float.IsFinite(distanceYalms.Value) ||
            distanceYalms.Value < 0f)
        {
            return string.Empty;
        }

        var format = distanceYalms.Value < 100f ? "0.0" : "0";
        return $"~{distanceYalms.Value.ToString(format, CultureInfo.InvariantCulture)}y";
    }

    public static string FormatEnemySlot(int slot) => EnemySlotRules.Label(slot);

    public static string FormatEnemySlot(int? slot) =>
        slot.HasValue ? FormatEnemySlot(slot.Value) : string.Empty;

    private static TargetHighlightCandidate? CanInclude(
        bool enabled,
        bool pvpOnly,
        bool isPvP,
        TargetHighlightCandidate? candidate)
    {
        if (!enabled || (pvpOnly && !isPvP) || candidate is null) return null;

        var value = candidate.Value;
        return value.IsValid && IsValidGameObjectId(value.GameObjectId)
            ? value
            : null;
    }

    private static TargetHighlightPlanItem CreateItem(
        TargetHighlightCandidate candidate,
        TargetHighlightRelation relation)
    {
        int? hpPercent = TryCalculateHpPercent(
            candidate.CurrentHp,
            candidate.MaximumHp,
            out var safeHpPercent)
            ? safeHpPercent
            : null;
        float? distanceYalms = TryCalculateDistance(
            candidate.CenterDistanceYalms,
            candidate.LocalHitboxRadius,
            candidate.TargetHitboxRadius,
            out var safeDistanceYalms)
            ? safeDistanceYalms
            : null;
        int? enemySlot = EnemySlotRules.IsValidSlot(candidate.EnemySlot)
            ? candidate.EnemySlot
            : null;

        return new TargetHighlightPlanItem(
            candidate.GameObjectId,
            relation,
            candidate.JobId,
            hpPercent,
            distanceYalms,
            enemySlot);
    }

    private static TargetHighlightPlanItem CreateCombinedItem(
        TargetHighlightCandidate current,
        TargetHighlightCandidate focus)
    {
        var primary = CreateItem(current, TargetHighlightRelation.CurrentAndFocus);
        var fallback = CreateItem(focus, TargetHighlightRelation.CurrentAndFocus);

        return primary with
        {
            JobId = primary.JobId != 0 ? primary.JobId : fallback.JobId,
            HpPercent = primary.HpPercent ?? fallback.HpPercent,
            DistanceYalms = primary.DistanceYalms ?? fallback.DistanceYalms,
            EnemySlot = primary.EnemySlot ?? fallback.EnemySlot,
        };
    }
}
