using Dalamud.Game.ClientState.Objects.SubKinds;

namespace SeitonSense.Plugin.Services;

internal sealed class NinjaShukuchiHiddenStatusCatalog
{
    private readonly uint[] statusIds;

    private NinjaShukuchiHiddenStatusCatalog(uint[] statusIds)
    {
        this.statusIds = statusIds;
    }

    internal static NinjaShukuchiHiddenStatusCatalog Empty { get; } = new([]);

    internal int Count => statusIds.Length;

    internal bool IsVerified => statusIds.Length > 0;

    internal static NinjaShukuchiHiddenStatusCatalog Create(
        IEnumerable<uint> statusIds) => new(
        statusIds
            .Where(statusId => statusId != 0)
            .Distinct()
            .OrderBy(statusId => statusId)
            .ToArray());

    internal bool Contains(uint statusId)
    {
        if (statusId == 0) return false;
        foreach (var verifiedStatusId in statusIds)
        {
            if (statusId == verifiedStatusId) return true;
        }

        return false;
    }
}

/// <summary>
/// Keeps automatic self-recovery from breaking Shukuchi's exact Hidden buff.
/// The status rows are resolved and validated once from current English game
/// data; the runtime gate is language-independent and compares only row IDs.
/// </summary>
internal static class NinjaShukuchiStealthGate
{
    internal static bool ShouldSuppressAutomaticRecovery(
        IPlayerCharacter? localPlayer,
        NinjaShukuchiHiddenStatusCatalog? verifiedHiddenStatuses)
    {
        if (localPlayer is null) return false;
        if (!localPlayer.ClassJob.IsValid) return true;
        if (localPlayer.ClassJob.RowId != EnemyCombatConstants.NinjaJobId)
            return false;

        // Automatic recovery may break Hidden without any physical consent.
        // If the current Hidden rows could not be verified, fail closed for NIN.
        if (verifiedHiddenStatuses is not { IsVerified: true }) return true;
        return IsActive(localPlayer, verifiedHiddenStatuses);
    }

    internal static bool IsActive(
        IPlayerCharacter? localPlayer,
        NinjaShukuchiHiddenStatusCatalog? verifiedHiddenStatuses)
    {
        if (localPlayer is null ||
            verifiedHiddenStatuses is not { IsVerified: true } ||
            !localPlayer.ClassJob.IsValid ||
            localPlayer.ClassJob.RowId != EnemyCombatConstants.NinjaJobId)
        {
            return false;
        }

        foreach (var status in localPlayer.StatusList)
        {
            if (verifiedHiddenStatuses.Contains(status.StatusId)) return true;
        }

        return false;
    }
}
