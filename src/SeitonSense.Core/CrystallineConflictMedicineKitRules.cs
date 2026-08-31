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
