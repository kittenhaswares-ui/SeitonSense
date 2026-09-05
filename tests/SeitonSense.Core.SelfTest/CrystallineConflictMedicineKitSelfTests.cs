using System.Numerics;
using SeitonSense.Core;

internal static class CrystallineConflictMedicineKitSelfTests
{
    public static void BeaconClippingPreservesVisiblePillarsAndMinimumHeight()
    {
        var viewport = new Vector2(1920, 1080);
        True(CrystallineConflictMedicineKitRules.TryGetBeaconScreenSegment(
            new(500, 700), true, new(500, 650), true, viewport, 1f, out var lower, out var upper),
            "a short projected pillar gets a visible screen-space minimum");
        True(lower == new Vector2(500, 700) && upper == new Vector2(500, 520), "minimum is 180 pixels");
        True(CrystallineConflictMedicineKitRules.TryGetBeaconScreenSegment(
            new(500, 1200), true, new(500, -500), true, viewport, 1f, out lower, out upper),
            "base below screen and top above screen still expose the intersecting pillar");
        True(Vector2.Distance(lower, new Vector2(500, 1076)) < 0.001f &&
            Vector2.Distance(upper, new Vector2(500, 4)) < 0.001f, "beam clipped to both viewport edges");
        True(CrystallineConflictMedicineKitRules.TryGetBeaconScreenSegment(
            new(500, 700), true, new(float.NaN, 0), false, viewport, 1f, out lower, out upper),
            "valid detected base gets safe upward fallback when sky projection fails");
        True(upper == new Vector2(500, 520), "fallback does not rely on non-finite sky coordinates");
        True(CrystallineConflictMedicineKitRules.TryGetBeaconScreenSegment(
            new(-100, 900), true, new(500, 100), true, viewport, 1f, out lower, out upper),
            "side-offscreen base retains the visible part of a slanted world pillar");
        True(lower.X >= 3.99f && upper.X <= viewport.X - 4f, "side intersection stays in viewport");
    }

    public static void BeaconProjectionRejectsUnknownOrNonIntersectingAnchors()
    {
        var viewport = new Vector2(1920, 1080);
        False(CrystallineConflictMedicineKitRules.TryGetBeaconScreenSegment(
            new(500, 700), false, new(500, 100), true, viewport, 1f, out _, out _),
            "failed or behind-camera base projection cannot synthesize a visible kit");
        False(CrystallineConflictMedicineKitRules.TryGetBeaconScreenSegment(
            new(-100, 700), true, new(-100, 100), true, viewport, 1f, out _, out _),
            "fully offscreen pillar has no fake screen intersection");
        False(CrystallineConflictMedicineKitRules.TryGetBeaconScreenSegment(
            new(500, -100), true, new(500, -900), true, viewport, 1f, out _, out _),
            "pillar wholly above screen is hidden");
        False(CrystallineConflictMedicineKitRules.TryGetBeaconScreenSegment(
            new(float.NaN, 700), true, new(500, 100), true, viewport, 1f, out _, out _), "NaN base fails closed");
        False(CrystallineConflictMedicineKitRules.TryGetBeaconScreenSegment(
            new(500, 700), true, new(500, 100), true, Vector2.Zero, 1f, out _, out _), "empty viewport fails closed");
        False(CrystallineConflictMedicineKitRules.TryGetBeaconScreenSegment(
            new(500, 700), true, new(500, 100), true, viewport, float.NaN, out _, out _), "NaN scale fails closed");
        False(CrystallineConflictMedicineKitRules.TryGetBeaconScreenSegment(
            new(500, 700), true, new(500, 100), true, new(float.PositiveInfinity, 1080), 1f, out _, out _),
            "infinite viewport fails closed");
    }

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
