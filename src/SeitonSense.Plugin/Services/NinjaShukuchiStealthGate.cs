using Dalamud.Game.ClientState.Objects.SubKinds;

namespace SeitonSense.Plugin.Services;

/// <summary>
/// Keeps automatic self-recovery from breaking Shukuchi's exact Hidden buff.
/// The status row is resolved and validated from the current English game data
/// at startup; this gate never relies on the client language or a status name.
/// </summary>
internal static class NinjaShukuchiStealthGate
{
    internal static bool IsActive(
        IPlayerCharacter? localPlayer,
        uint verifiedHiddenStatusId)
    {
        if (localPlayer is null ||
            verifiedHiddenStatusId == 0 ||
            !localPlayer.ClassJob.IsValid ||
            localPlayer.ClassJob.RowId != EnemyCombatConstants.NinjaJobId)
        {
            return false;
        }

        foreach (var status in localPlayer.StatusList)
        {
            if (status.StatusId == verifiedHiddenStatusId) return true;
        }

        return false;
    }
}
