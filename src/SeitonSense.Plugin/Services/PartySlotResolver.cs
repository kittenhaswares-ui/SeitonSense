using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace SeitonSense.Plugin.Services;

internal static class PartySlotResolver
{
    internal static unsafe IPlayerCharacter? Resolve(IObjectTable objectTable, int slot)
    {
        if (slot is < 1 or > 8) return null;

        var pronouns = PronounModule.Instance();
        if (pronouns == null) return null;

        var nativeObject = pronouns->ResolvePlaceholder($"<{slot}>", 1, 0);
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
}
