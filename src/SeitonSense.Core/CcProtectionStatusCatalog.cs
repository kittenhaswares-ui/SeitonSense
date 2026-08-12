using System.Collections.ObjectModel;

namespace SeitonSense.Core;

/// <summary>
/// Fail-closed allowlist for currently verified PvP crowd-control protection statuses.
/// </summary>
public static class CcProtectionStatusCatalog
{
    private static readonly CcProtectionDefinition[] DefinitionArray =
    [
        new(3054, "Guard", 214890, CcProtectionKind.Guard, 4.25f, "All Stun, Heavy, Bind, Silence"),
        new(3673, "Guard", 214715, CcProtectionKind.Guard, 4.25f, "All Stun, Heavy, Bind, Silence"),
        new(3248, "Resilience", 214891, CcProtectionKind.FullImmunity, 2.25f, "Nullifying status afflictions that can be removed by Purify"),
        new(1303, "Inner Release", 212556, CcProtectionKind.FullImmunity, 15.25f, "All Stun, Heavy, Bind, Silence"),
        new(1320, "Meikyo Shisui", 214955, CcProtectionKind.FullImmunity, 3.25f, "Status afflictions that can be removed by Purify"),
        new(4096, "Hardened Scales", 214992, CcProtectionKind.FullImmunity, 4.25f, "All Stun, Heavy, Bind, Silence"),
        new(4477, "Swift", 216678, CcProtectionKind.FullImmunity, 4.25f, "All Stun, Heavy, Bind, Silence"),
    ];

    private static readonly ReadOnlyCollection<CcProtectionDefinition> ReadOnlyDefinitions =
        Array.AsReadOnly(DefinitionArray);

    private static readonly Dictionary<uint, CatalogEntry> Entries = DefinitionArray
        .Select((definition, order) => new CatalogEntry(definition, order))
        .ToDictionary(entry => entry.Definition.StatusId);

    /// <summary>
    /// Gets the complete immutable allowlist. A status not present here must
    /// never be shown as protection. Aquaveil is intentionally absent: its
    /// visible barrier can remain after its reactive control removal is spent.
    /// </summary>
    public static IReadOnlyList<CcProtectionDefinition> Definitions => ReadOnlyDefinitions;

    public static bool TryGet(uint statusId, out CcProtectionDefinition definition)
    {
        if (Entries.TryGetValue(statusId, out var entry))
        {
            definition = entry.Definition;
            return true;
        }

        definition = null!;
        return false;
    }

    /// <summary>
    /// Filters observations through the allowlist, removes expired, non-finite,
    /// or implausibly long durations, and folds duplicate status IDs to the
    /// longest active duration. Results are ordered Guard, full immunity, then
    /// immunity, with catalog order as the stable tie-breaker.
    /// </summary>
    public static IReadOnlyList<CcProtectionIndicator> BuildIndicators(
        IEnumerable<ObservedCcProtectionStatus> observations)
    {
        ArgumentNullException.ThrowIfNull(observations);

        var longestRemainingById = new Dictionary<uint, float>();

        foreach (var observation in observations)
        {
            if (!Entries.TryGetValue(observation.StatusId, out var entry) ||
                !float.IsFinite(observation.RemainingTime) ||
                observation.RemainingTime <= 0f ||
                observation.RemainingTime > entry.Definition.MaximumRemainingTime)
            {
                continue;
            }

            if (!longestRemainingById.TryGetValue(observation.StatusId, out var current) ||
                observation.RemainingTime > current)
            {
                longestRemainingById[observation.StatusId] = observation.RemainingTime;
            }
        }

        if (longestRemainingById.Count == 0)
        {
            return Array.Empty<CcProtectionIndicator>();
        }

        return longestRemainingById
            .Select(pair => (Entry: Entries[pair.Key], Remaining: pair.Value))
            .OrderBy(item => item.Entry.Definition.Kind)
            .ThenBy(item => item.Entry.Order)
            .Select(item => new CcProtectionIndicator(
                item.Entry.Definition.StatusId,
                item.Entry.Definition.Name,
                item.Entry.Definition.IconId,
                item.Entry.Definition.Kind,
                item.Remaining))
            .ToArray();
    }

    private sealed record CatalogEntry(CcProtectionDefinition Definition, int Order);
}
