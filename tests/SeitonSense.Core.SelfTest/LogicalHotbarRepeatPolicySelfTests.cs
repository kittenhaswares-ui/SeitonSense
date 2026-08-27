using SeitonSense.Core;

internal static class LogicalHotbarRepeatPolicySelfTests
{
    public static IEnumerable<(string Name, Action Run)> All()
    {
        yield return ("logical repeat scope supports every valid combat context", CombatHasNoTerritoryGate);
        yield return ("logical repeat outside combat requires explicit opt-in", OutsideCombatRequiresOptIn);
        yield return ("logical repeat outside-combat opt-in starts a new hold lifecycle", OutsideCombatOptInChangesConfigurationFingerprint);
        yield return ("logical repeat yields to internal priority without losing generic scope", InternalPriorityPausesRepeat);
    }

    private static void CombatHasNoTerritoryGate()
    {
        True(Enabled(inCombat: true, allowOutsideCombat: false));
    }

    private static void OutsideCombatRequiresOptIn()
    {
        False(Enabled(inCombat: false, allowOutsideCombat: false));
        True(Enabled(inCombat: false, allowOutsideCombat: true));
        False(LogicalHotbarRepeatPolicy.IsRepeatEnabled(new LogicalHotbarRepeatPolicyInput(
            FeatureEnabled: true,
            ContextValid: false,
            InCombat: false,
            AllowOutsideCombat: true)));
    }

    private static void InternalPriorityPausesRepeat()
    {
        var priority = new LogicalHotbarRepeatPolicyInput(
            FeatureEnabled: true,
            ContextValid: true,
            InCombat: true,
            AllowOutsideCombat: true,
            InternalPriorityClaimed: true);
        False(LogicalHotbarRepeatPolicy.IsRepeatEnabled(priority));
        True(LogicalHotbarRepeatPolicy.ShouldSuppressAttributedExternalRepeat(priority));

        False(LogicalHotbarRepeatPolicy.ShouldSuppressAttributedExternalRepeat(priority with
        {
            FeatureEnabled = false,
        }));
        False(LogicalHotbarRepeatPolicy.ShouldSuppressAttributedExternalRepeat(priority with
        {
            ContextValid = false,
        }));
        False(LogicalHotbarRepeatPolicy.ShouldSuppressAttributedExternalRepeat(priority with
        {
            InCombat = false,
            AllowOutsideCombat = false,
        }));
        False(LogicalHotbarRepeatPolicy.IsRepeatDomainActive(priority with
        {
            FeatureEnabled = false,
        }));
        True(LogicalHotbarRepeatPolicy.IsRepeatDomainActive(priority));
        True(Enabled(inCombat: true, allowOutsideCombat: true));
    }

    private static void OutsideCombatOptInChangesConfigurationFingerprint()
    {
        var disabled = LogicalHotbarRepeatPolicy.GetConfigurationFingerprint(
            featureEnabled: false,
            allowOutsideCombat: false);
        var disabledWithDormantOptIn = LogicalHotbarRepeatPolicy.GetConfigurationFingerprint(
            featureEnabled: false,
            allowOutsideCombat: true);
        var combatOnly = LogicalHotbarRepeatPolicy.GetConfigurationFingerprint(
            featureEnabled: true,
            allowOutsideCombat: false);
        var outsideCombat = LogicalHotbarRepeatPolicy.GetConfigurationFingerprint(
            featureEnabled: true,
            allowOutsideCombat: true);

        Equal(disabled, disabledWithDormantOptIn);
        NotEqual(disabled, combatOnly);
        NotEqual(combatOnly, outsideCombat);
    }

    private static bool Enabled(bool inCombat, bool allowOutsideCombat) =>
        LogicalHotbarRepeatPolicy.IsRepeatEnabled(new LogicalHotbarRepeatPolicyInput(
            FeatureEnabled: true,
            ContextValid: true,
            InCombat: inCombat,
            AllowOutsideCombat: allowOutsideCombat));

    private static void True(bool condition)
    {
        if (!condition) throw new InvalidOperationException("Expected true, got false.");
    }

    private static void False(bool condition) => True(!condition);

    private static void Equal<T>(T expected, T actual) where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"Expected {expected}, got {actual}.");
    }

    private static void NotEqual<T>(T left, T right) where T : notnull
    {
        if (EqualityComparer<T>.Default.Equals(left, right))
            throw new InvalidOperationException($"Expected distinct values, both were {left}.");
    }
}
