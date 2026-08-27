namespace SeitonSense.Plugin.Services;

/// <summary>
/// Immutable startup snapshot of exact resolved PvP action rows whose current
/// English ActionTransient description explicitly says their damage ignores
/// Guard. Unknown or drifted rows are deliberately absent.
/// </summary>
internal sealed class SmartActionGuardBypassCatalog
{
    private readonly uint[] actionIds;

    private SmartActionGuardBypassCatalog(uint[] actionIds)
    {
        this.actionIds = actionIds;
    }

    internal static SmartActionGuardBypassCatalog Empty { get; } = new([]);

    internal int Count => actionIds.Length;

    internal bool IsVerified => actionIds.Length > 0;

    internal static SmartActionGuardBypassCatalog Create(
        IEnumerable<uint> actionIds) => new(
        actionIds
            .Where(actionId => actionId != 0)
            .Distinct()
            .OrderBy(actionId => actionId)
            .ToArray());

    internal bool Contains(uint resolvedActionId) =>
        resolvedActionId != 0 &&
        Array.BinarySearch(actionIds, resolvedActionId) >= 0;
}
