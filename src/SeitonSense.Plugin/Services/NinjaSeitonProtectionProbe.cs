using Dalamud.Game.ClientState.Objects.Types;
using SeitonSense.Core;

namespace SeitonSense.Plugin.Services;

/// <summary>
/// Reads only exact live status rows from the already resolved enemy actor.
/// A matching row blocks while it remains in StatusList even if its remaining
/// time is temporarily zero or non-finite; a one-frame conservative block is
/// safer than spending Seiton into Covered or an invulnerability phase.
/// </summary>
internal static class NinjaSeitonProtectionProbe
{
    internal static bool TryFindExecuteBlockingStatus(
        IBattleChara? player,
        out uint statusId,
        out float remainingTime)
    {
        statusId = 0;
        remainingTime = 0f;
        if (player is null) return false;

        foreach (var status in player.StatusList)
        {
            if (!NinjaSeitonProtectionStatusCatalog.IsExecuteBlockingStatus(
                    status.StatusId))
            {
                continue;
            }

            statusId = status.StatusId;
            remainingTime = status.RemainingTime;
            return true;
        }

        return false;
    }
}
