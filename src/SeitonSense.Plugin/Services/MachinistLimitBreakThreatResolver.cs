using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using SeitonSense.Core;

namespace SeitonSense.Plugin.Services;

internal static class MachinistLimitBreakThreatResolver
{
    internal static bool IsVerifiedOpponent(
        IObjectTable objectTable,
        uint casterEntityId,
        IPlayerCharacter localPlayer,
        SupportedPvPContext context)
    {
        if (casterEntityId is 0 or 0xE0000000u ||
            localPlayer.EntityId is 0 or 0xE0000000u ||
            casterEntityId == localPlayer.EntityId)
        {
            return false;
        }

        IPlayerCharacter? source = null;
        if (context == SupportedPvPContext.CrystallineConflict)
        {
            for (var slot = EnemySlotRules.FirstSlot; slot <= EnemySlotRules.LastSlot; slot++)
            {
                var candidate = EnemySlotResolver.Resolve(objectTable, slot);
                if (candidate?.EntityId != casterEntityId) continue;
                source = candidate;
                break;
            }
        }
        else if (context == SupportedPvPContext.WolvesDen)
        {
            var candidate = WolvesDenOpponentResolver.Resolve(
                objectTable,
                localPlayer,
                out _,
                out _,
                out _);
            if (candidate?.EntityId == casterEntityId) source = candidate;
        }

        return source is not null &&
               source.Address != 0 &&
               source.GameObjectId != 0 &&
               source.EntityId == casterEntityId &&
               source.GameObjectId != localPlayer.GameObjectId &&
               source.ClassJob.IsValid &&
               source.ClassJob.RowId == EnemyCombatConstants.MachinistJobId &&
               !source.IsDead &&
               source.CurrentHp > 0 &&
               source.IsTargetable;
    }
}
