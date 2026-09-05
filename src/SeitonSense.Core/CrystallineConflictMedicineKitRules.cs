using System.Numerics;

namespace SeitonSense.Core;

/// <summary>
/// Pure timing and localized-name policy for the read-only Crystalline
/// Conflict medicine-kit overlay. The current match timer runs down from five
/// minutes; the first kits appear roughly thirty seconds after commencement.
/// Values outside that narrow first-spawn interval expose no countdown.
/// </summary>
public static class CrystallineConflictMedicineKitRules
{
    public const float MatchDurationSeconds = 300f;
    public const float FirstSpawnElapsedSeconds = 30f;
    public const float FirstSpawnAtContentTimeLeftSeconds =
        MatchDurationSeconds - FirstSpawnElapsedSeconds;
    public const float BeaconMinimumScreenHeight = 180f;

    /// <summary>
    /// Clips an already detected kit's projected beam, not just its ground
    /// anchor. A ground point below the screen may still have a visible pillar.
    /// This never supplies an object position or claims that a kit is available.
    /// </summary>
    public static bool TryGetBeaconScreenSegment(
        Vector2 basePoint,
        bool baseProjected,
        Vector2 skyPoint,
        bool skyProjected,
        Vector2 viewport,
        float scale,
        out Vector2 visibleBase,
        out Vector2 visibleTop)
    {
        visibleBase = default;
        visibleTop = default;
        if (!baseProjected || !IsFinite(basePoint) || !IsFinite(viewport) ||
            viewport.X <= 8f || viewport.Y <= 8f || !float.IsFinite(scale)) return false;

        var height = BeaconMinimumScreenHeight * Math.Clamp(scale, 0.6f, 2f);
        if (!skyProjected || !IsFinite(skyPoint) || skyPoint.Y > basePoint.Y - height)
            skyPoint = basePoint - new Vector2(0f, height);
        if (!IsFinite(skyPoint)) return false;

        var delta = skyPoint - basePoint;
        if (!IsFinite(delta)) return false;
        var from = 0f;
        var to = 1f;
        const float inset = 4f;
        if (!Clip(-delta.X, basePoint.X - inset, ref from, ref to) ||
            !Clip(delta.X, viewport.X - inset - basePoint.X, ref from, ref to) ||
            !Clip(-delta.Y, basePoint.Y - inset, ref from, ref to) ||
            !Clip(delta.Y, viewport.Y - inset - basePoint.Y, ref from, ref to)) return false;

        var min = new Vector2(inset);
        var max = viewport - min;
        visibleBase = Vector2.Clamp(basePoint + delta * from, min, max);
        visibleTop = Vector2.Clamp(basePoint + delta * to, min, max);
        return IsFinite(visibleBase) && IsFinite(visibleTop) &&
            Vector2.DistanceSquared(visibleBase, visibleTop) >= 1f;
    }

    private static bool Clip(float direction, float distance, ref float from, ref float to)
    {
        if (direction == 0f) return distance >= 0f;
        var ratio = distance / direction;
        if (!float.IsFinite(ratio)) return false;
        if (direction < 0f)
        {
            if (ratio > to) return false;
            from = Math.Max(from, ratio);
        }
        else
        {
            if (ratio < from) return false;
            to = Math.Min(to, ratio);
        }
        return true;
    }

    private static bool IsFinite(Vector2 point) => float.IsFinite(point.X) && float.IsFinite(point.Y);

    public static bool TryGetFirstSpawnCountdown(
        float contentTimeLeftSeconds,
        out float remainingSeconds)
    {
        remainingSeconds = 0f;
        if (!float.IsFinite(contentTimeLeftSeconds) ||
            contentTimeLeftSeconds <= FirstSpawnAtContentTimeLeftSeconds ||
            contentTimeLeftSeconds >= MatchDurationSeconds)
        {
            return false;
        }

        remainingSeconds = Math.Clamp(
            contentTimeLeftSeconds - FirstSpawnAtContentTimeLeftSeconds,
            0f,
            FirstSpawnElapsedSeconds);
        return remainingSeconds > 0f;
    }

    public static bool IsMedicineKitName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        var value = name.Trim();
        return value.Equals("Medicine Kit", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("Medizin-Set", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("Medizinset", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("stimulant médical", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("stimulants médicaux", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("メディカルキット", StringComparison.Ordinal);
    }
}
