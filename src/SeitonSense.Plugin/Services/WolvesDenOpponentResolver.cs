using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using SeitonSense.Core;

namespace SeitonSense.Plugin.Services;

internal static class WolvesDenOpponentResolver
{
    internal static IPlayerCharacter? Resolve(
        IObjectTable objectTable,
        IPlayerCharacter localPlayer,
        out uint nativeEnemyEntityId,
        out bool nativePlayerResolved,
        out bool hostileFlag)
    {
        var player = EnemySlotResolver.ResolveWolvesDenDuelOpponent(
            objectTable,
            out nativeEnemyEntityId);
        nativePlayerResolved = player is not null;
        hostileFlag = player is not null && (player.StatusFlags & StatusFlags.Hostile) != 0;
        if (player is null) return null;

        var candidate = new WolvesDenOpponentCandidate(
            player.EntityId,
            player.GameObjectId,
            player.EntityId == nativeEnemyEntityId,
            player.Address != 0,
            IsPlayerCharacter: true,
            player.GameObjectId == localPlayer.GameObjectId,
            hostileFlag,
            player.IsTargetable);
        var slot = WolvesDenOpponentRules.ResolveSingleSlot([candidate]);
        return slot is { Slot: EnemySlotRules.FirstSlot } &&
               slot.Value.EntityId == player.EntityId
            ? player
            : null;
    }
}
