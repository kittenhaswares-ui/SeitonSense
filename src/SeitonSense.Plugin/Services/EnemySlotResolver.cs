using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace SeitonSense.Plugin.Services;

internal static class EnemySlotResolver
{
    internal static unsafe IPlayerCharacter? Resolve(IObjectTable objectTable, int slot)
    {
        if (slot is < 1 or > 5) return null;

        var pronouns = PronounModule.Instance();
        if (pronouns == null) return null;

        var nativeObject = pronouns->ResolvePlaceholder($"<e{slot}>", 1, 0);
        if (nativeObject == null) return null;

        var entityId = nativeObject->EntityId;
        if (entityId is 0 or 0xE0000000u) return null;

        var player = objectTable.SearchByEntityId(entityId) as IPlayerCharacter;
        return player is not null &&
               player.EntityId == entityId &&
               player.Address == (nint)nativeObject
            ? player
            : null;
    }

    internal static unsafe IPlayerCharacter? ResolveWolvesDenDuelOpponent(
        IObjectTable objectTable,
        out uint nativeEnemyEntityId)
    {
        nativeEnemyEntityId = 0;
        var gameMain = GameMain.Instance();
        if (gameMain == null) return null;

        nativeEnemyEntityId = gameMain->PvPDuelManager.EnemyEntityId;
        if (nativeEnemyEntityId is 0 or 0xE0000000u) return null;

        var player = objectTable.SearchByEntityId(nativeEnemyEntityId) as IPlayerCharacter;
        return player is not null &&
               player.EntityId == nativeEnemyEntityId &&
               player.Address != 0
            ? player
            : null;
    }
}
