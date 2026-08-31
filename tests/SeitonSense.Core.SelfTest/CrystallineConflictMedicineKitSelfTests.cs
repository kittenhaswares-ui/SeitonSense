using SeitonSense.Core;

internal static class CrystallineConflictMedicineKitSelfTests
{
    public static void FirstSpawnCountdownUsesOnlyTheOpeningThirtySeconds()
    {
        False(
            CrystallineConflictMedicineKitRules.TryGetFirstSpawnCountdown(300f, out _),
            "a frozen 5:00 timer does not claim that the match has started");
        True(
            CrystallineConflictMedicineKitRules.TryGetFirstSpawnCountdown(299.9f, out var start) &&
            Math.Abs(start - 29.9f) < 0.001f,
            "the first decreasing match-timer sample exposes the countdown");
        True(
            CrystallineConflictMedicineKitRules.TryGetFirstSpawnCountdown(285.25f, out var middle) &&
            Math.Abs(middle - 15.25f) < 0.001f,
            "the countdown follows the native content timer");
        False(
            CrystallineConflictMedicineKitRules.TryGetFirstSpawnCountdown(270f, out _),
            "the countdown disappears at first spawn");
        False(
            CrystallineConflictMedicineKitRules.TryGetFirstSpawnCountdown(30f, out _),
            "the pre-match countdown cannot masquerade as the first-kit timer");
        False(
            CrystallineConflictMedicineKitRules.TryGetFirstSpawnCountdown(float.NaN, out _),
            "non-finite native timers fail closed");
        False(
            CrystallineConflictMedicineKitRules.TryGetFirstSpawnCountdown(302f, out _),
            "out-of-range native timers fail closed");
    }

    public static void LocalizedMedicineKitNamesAreNarrow()
    {
        True(CrystallineConflictMedicineKitRules.IsMedicineKitName("Medicine Kit"), "English");
        True(CrystallineConflictMedicineKitRules.IsMedicineKitName("Medizin-Set"), "German");
        True(CrystallineConflictMedicineKitRules.IsMedicineKitName("Stimulant médical"), "French");
        True(CrystallineConflictMedicineKitRules.IsMedicineKitName("メディカルキット"), "Japanese");
        False(
            CrystallineConflictMedicineKitRules.IsMedicineKitName("Unused Medicine Kit Marker"),
            "longer names cannot teach a false BaseId");
        False(CrystallineConflictMedicineKitRules.IsMedicineKitName("Military-grade Elixir"), "other PvP pickup");
        False(CrystallineConflictMedicineKitRules.IsMedicineKitName(string.Empty), "empty runtime name");
    }

    private static void True(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static void False(bool value, string message) => True(!value, message);
}
