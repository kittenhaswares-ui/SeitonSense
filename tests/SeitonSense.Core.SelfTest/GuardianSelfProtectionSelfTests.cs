using SeitonSense.Core;

internal static class GuardianSelfProtectionSelfTests
{
    internal static void HighResourceFallbackUsesStrictThresholdsAndFreshSamples()
    {
        Require(!Read(80_000, 6_001), "Exactly 80% HP is insufficient without ready Guard.");
        Require(!Read(80_001, 6_000), "Exactly 60% MP is insufficient without ready Guard.");
        Require(Read(80_001, 6_001), "HP above 80% and MP above 60% allow the alternative route.");
        Require(!Read(80_001, 5_999), "MP loss after target selection closes the route immediately.");
        Require(!Read(79_999, 6_001), "HP loss after target selection closes the route immediately.");
        Require(Read(100_000, 10_000), "Full resources permit Guardian when Guard is on cooldown.");

        static bool Read(uint hp, uint mp) =>
            DefensiveUtilityRules.CanUseGuardianWithGuardOrHighResources(false, hp, 100_000, mp, 10_000);
    }

    internal static void ReadyGuardPreservesOriginalRouteAndFallbackNeedsKnownResources()
    {
        Require(DefensiveUtilityRules.CanUseGuardianWithGuardOrHighResources(true, 1, 100_000, 0, 10_000),
            "A ready Guard keeps the existing low-self-resource save route.");
        Require(!DefensiveUtilityRules.CanUseGuardianWithGuardOrHighResources(false, 1, 0, 9_000, 10_000),
            "Unknown maximum HP cannot manufacture high resources.");
        Require(!DefensiveUtilityRules.CanUseGuardianWithGuardOrHighResources(false, 90_000, 100_000, 1, 0),
            "Unknown maximum MP cannot manufacture high resources.");
        Require(!DefensiveUtilityRules.CanUseGuardianWithGuardOrHighResources(false, 100_001, 100_000, 10_000, 10_000),
            "An invalid current HP sample fails closed.");
        Require(!DefensiveUtilityRules.CanUseGuardianWithGuardOrHighResources(false, 100_000, 100_000, 10_001, 10_000),
            "An invalid current MP sample fails closed.");
        Require(DefensiveUtilityRules.CanUseGuardianWithGuardOrHighResources(false, uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue),
            "Ratio products use wide arithmetic rather than overflowing uint.");
    }

    internal static void ConfiguredThresholdsAreClampedAndRemainStrict()
    {
        Require(!Read(40, 30, 40, 30), "Custom equality remains insufficient for both resources.");
        Require(Read(41, 31, 40, 30), "Both custom thresholds are used.");
        Require(!Read(41, 30, 40, 30), "Changing the HP threshold does not bypass MP.");
        Require(Read(1, 1, -20, -30), "Negative settings clamp to zero.");
        Require(!Read(100, 100, 150, 120), "Settings above 100 clamp to 100 and cannot be exceeded.");

        static bool Read(uint hp, uint mp, int hpLimit, int mpLimit) =>
            DefensiveUtilityRules.CanUseGuardianWithGuardOrHighResources(false, hp, 100, mp, 100, hpLimit, mpLimit);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
