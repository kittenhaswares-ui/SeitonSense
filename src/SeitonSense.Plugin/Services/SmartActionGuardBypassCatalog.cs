namespace SeitonSense.Plugin.Services;

/// <summary>
/// Immutable startup snapshot of exact resolved PvP action rows which may
/// intentionally select a Guarded primary target. Generic members explicitly
/// ignore Guard damage; individually verified members may instead reduce
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

    internal SmartActionGuardBypassCatalog WithVerified(
        params uint[] verifiedActionIds) => Create(
        actionIds.Concat(verifiedActionIds ?? []));

    internal bool Contains(uint resolvedActionId) =>
        resolvedActionId != 0 &&
        Array.BinarySearch(actionIds, resolvedActionId) >= 0;
}
