using SeitonSense.Core;

internal static class CcProtectionRulesSelfTests
{
    public static void AllowlistMetadataIsExact()
    {
        var expected = new[]
        {
            new CcProtectionDefinition(3054, "Guard", 214890, CcProtectionKind.Guard, 4.25f, "All Stun, Heavy, Bind, Silence"),
            new CcProtectionDefinition(3673, "Guard", 214715, CcProtectionKind.Guard, 4.25f, "All Stun, Heavy, Bind, Silence"),
            new CcProtectionDefinition(3248, "Resilience", 214891, CcProtectionKind.FullImmunity, 2.25f, "Nullifying status afflictions that can be removed by Purify"),
            new CcProtectionDefinition(1303, "Inner Release", 212556, CcProtectionKind.FullImmunity, 15.25f, "All Stun, Heavy, Bind, Silence"),
            new CcProtectionDefinition(1320, "Meikyo Shisui", 214955, CcProtectionKind.FullImmunity, 3.25f, "Status afflictions that can be removed by Purify"),
            new CcProtectionDefinition(4096, "Hardened Scales", 214992, CcProtectionKind.FullImmunity, 4.25f, "All Stun, Heavy, Bind, Silence"),
            new CcProtectionDefinition(4477, "Swift", 216678, CcProtectionKind.FullImmunity, 4.25f, "All Stun, Heavy, Bind, Silence"),
        };

        Equal(expected.Length, CcProtectionStatusCatalog.Definitions.Count, "definition count");

        for (var index = 0; index < expected.Length; index++)
        {
            Equal(expected[index], CcProtectionStatusCatalog.Definitions[index], $"definition {index}");
            True(CcProtectionStatusCatalog.TryGet(expected[index].StatusId, out var actual), "known ID lookup");
            Equal(expected[index], actual, "lookup metadata");
        }
    }

    public static void UnknownStatusesAndAquaveilFailClosed()
    {
        True(
            !CcProtectionStatusCatalog.Definitions.Any(definition =>
                string.Equals(definition.Name, "Aquaveil", StringComparison.Ordinal)),
            "Aquaveil must not be presented as an intact one-hit ward");
        True(!CcProtectionStatusCatalog.TryGet(0, out _), "zero ID lookup");
        True(!CcProtectionStatusCatalog.TryGet(999_999, out _), "unknown ID lookup");

        var results = CcProtectionStatusCatalog.BuildIndicators(
        [
            new ObservedCcProtectionStatus(0, 30f),
            new ObservedCcProtectionStatus(999_999, 30f),
        ]);

        Equal(0, results.Count, "unknown status count");
    }

    public static void InvalidDurationsAreIgnored()
    {
        var results = CcProtectionStatusCatalog.BuildIndicators(
        [
            new ObservedCcProtectionStatus(3054, 0f),
            new ObservedCcProtectionStatus(3248, -0.01f),
            new ObservedCcProtectionStatus(1303, float.NaN),
            new ObservedCcProtectionStatus(1320, float.PositiveInfinity),
            new ObservedCcProtectionStatus(3054, 4.251f),
            new ObservedCcProtectionStatus(4096, float.Epsilon),
            new ObservedCcProtectionStatus(4477, 4.25f),
        ]);

        Equal(2, results.Count, "only finite positive in-range durations survive");
        Equal(4096u, results[0].StatusId, "small positive duration survives");
        Equal(4477u, results[1].StatusId, "exact maximum duration survives");
    }

    public static void DuplicateStatusesKeepLongestDuration()
    {
        var results = CcProtectionStatusCatalog.BuildIndicators(
        [
            new ObservedCcProtectionStatus(3248, 1.25f),
            new ObservedCcProtectionStatus(3248, 2.25f),
            new ObservedCcProtectionStatus(3248, 2f),
        ]);

        Equal(1, results.Count, "deduplicated count");
        Equal(2.25f, results[0].RemainingTime, "longest duration");
    }

    public static void IndicatorsUseStableProtectionPriority()
    {
        var results = CcProtectionStatusCatalog.BuildIndicators(
        [
            new ObservedCcProtectionStatus(4477, 3.5f),
            new ObservedCcProtectionStatus(4096, 4f),
            new ObservedCcProtectionStatus(1303, 6f),
            new ObservedCcProtectionStatus(3673, 4.2f),
            new ObservedCcProtectionStatus(3054, 4f),
        ]);

        var actualIds = results.Select(result => result.StatusId).ToArray();
        var expectedIds = new uint[] { 3054, 3673, 1303, 4096, 4477 };
        SequenceEqual(expectedIds, actualIds, "ordered IDs");
    }

    public static void CountdownFormattingIsConservative()
    {
        Equal(string.Empty, CcProtectionCountdownFormatter.Format(0f), "zero");
        Equal(string.Empty, CcProtectionCountdownFormatter.Format(-1f), "negative");
        Equal(string.Empty, CcProtectionCountdownFormatter.Format(float.NaN), "NaN");
        Equal(string.Empty, CcProtectionCountdownFormatter.Format(float.PositiveInfinity), "infinity");
        Equal("0.1", CcProtectionCountdownFormatter.Format(0.001f), "minimum active tenth");
        Equal("1.1", CcProtectionCountdownFormatter.Format(1.01f), "tenths round upward");
        Equal("5.0", CcProtectionCountdownFormatter.Format(4.999f), "below-five format");
        Equal("5", CcProtectionCountdownFormatter.Format(5f), "five-second threshold");
        Equal("6", CcProtectionCountdownFormatter.Format(5.001f), "whole seconds round upward");
    }

    private static void True(bool condition, string label)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Expected true: {label}");
        }
    }

    private static void Equal<T>(T expected, T actual, string label)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
        }
    }

    private static void SequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual, string label)
    {
        if (!expected.SequenceEqual(actual))
        {
            throw new InvalidOperationException(
                $"{label}: expected [{string.Join(", ", expected)}], got [{string.Join(", ", actual)}]");
        }
    }
}
