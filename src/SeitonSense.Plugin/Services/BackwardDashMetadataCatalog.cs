namespace SeitonSense.Plugin.Services;

/// <summary>
/// Immutable startup snapshot of the exact directional /seitonbw action rows
/// that matched the reviewed current PvP metadata. One drifted row disables
/// only its own job instead of weakening or disabling the other mappings.
/// </summary>
internal sealed class BackwardDashMetadataCatalog
{
    private readonly uint[] actionIds;

    private BackwardDashMetadataCatalog(uint[] actionIds)
    {
        this.actionIds = actionIds;
    }

    internal static BackwardDashMetadataCatalog Empty { get; } = new([]);

    internal int Count => actionIds.Length;

    internal static BackwardDashMetadataCatalog Create(
        IEnumerable<uint> actionIds) => new(
        actionIds
            .Where(actionId => actionId != 0)
            .Distinct()
            .OrderBy(actionId => actionId)
            .ToArray());

    internal bool Contains(uint actionId) =>
        actionId != 0 && Array.BinarySearch(actionIds, actionId) >= 0;
}
