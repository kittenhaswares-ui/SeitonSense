using Dalamud.Game;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Lumina.Excel.Sheets;
using SeitonSense.Core;
using DalamudBattleChara = Dalamud.Game.ClientState.Objects.Types.IBattleChara;
using DalamudBattleNpc = Dalamud.Game.ClientState.Objects.Types.IBattleNpc;
using DalamudGameObject = Dalamud.Game.ClientState.Objects.Types.IGameObject;

namespace SeitonSense.Plugin.Services;

/// <summary>
/// Shared fail-closed resolver for the one supported Wolves' Den action target:
/// the local player's exact current native hard-target striking dummy. It never
/// resolves duel opponents, synthetic enemy slots, nearest actors, or fallback
/// targets.
/// </summary>
internal static unsafe class StrictWolvesDenStrikingDummyResolver
{
    internal const uint StrikingDummyNameId =
        ViperSerpentTailRules.WolvesDenStrikingDummyNameId;

    internal static bool ValidateMetadata(IDataManager dataManager, IPluginLog log)
    {
        try
        {
            var names = dataManager.GetExcelSheet<BNpcName>(ClientLanguage.English);
            var verified = names.TryGetRow(StrikingDummyNameId, out var row) &&
                           row.Singular.ToString() == "striking dummy" &&
                           row.Plural.ToString() == "striking dummies";
            if (!verified)
            {
                log.Warning(
                    "Seiton Sense strict Wolves' Den striking-dummy metadata failed closed.");
            }

            return verified;
        }
        catch (Exception exception)
        {
            log.Warning(
                exception,
                "Seiton Sense strict Wolves' Den striking-dummy metadata lookup failed closed.");
            return false;
        }
    }

    internal static bool TryResolveExactCurrentHardTarget(
        IObjectTable objectTable,
        bool metadataVerified,
        IPlayerCharacter? localPlayer,
        out DalamudBattleChara? target,
        out TargetPressureActorIdentity identity,
        out ulong nativeHardTargetId)
    {
        target = null;
        identity = default;
        nativeHardTargetId = 0;
        if (!metadataVerified || !HasValidNativeIdentity(localPlayer)) return false;

        nativeHardTargetId = GetNativeHardTargetId(localPlayer!);
        if (!IsNetworkObjectId(nativeHardTargetId)) return false;

        var byObjectId = objectTable.SearchById(nativeHardTargetId) as DalamudBattleChara;
        var byEntityId = nativeHardTargetId <= uint.MaxValue
            ? objectTable.SearchByEntityId((uint)nativeHardTargetId) as DalamudBattleChara
            : null;
        if (byObjectId is not null &&
            byEntityId is not null &&
            !HasSameNativeIdentity(byObjectId, byEntityId))
        {
            return false;
        }

        var candidate = byObjectId ?? byEntityId;
        if (!IsExactLiveStrikingDummy(localPlayer!, candidate) ||
            !ActorIdMatches(nativeHardTargetId, candidate!))
        {
            return false;
        }

        var canonicalByObjectId =
            objectTable.SearchById(candidate!.GameObjectId) as DalamudBattleChara;
        var canonicalByEntityId =
            objectTable.SearchByEntityId(candidate.EntityId) as DalamudBattleChara;
        var objectLookupMatches =
            HasSameNativeIdentity(candidate, canonicalByObjectId);
        var entityLookupMatches =
            HasSameNativeIdentity(candidate, canonicalByEntityId);
        if (!SmartActionContextRules.HasCanonicalNativeTargetIdentity(
                canonicalByObjectId is not null,
                objectLookupMatches,
                canonicalByEntityId is not null,
                entityLookupMatches) ||
            GetNativeHardTargetId(localPlayer!) != nativeHardTargetId)
        {
            return false;
        }

        identity = new TargetPressureActorIdentity(
            candidate.GameObjectId,
            candidate.EntityId);
        if (!identity.IsValid) return false;

        target = candidate;
        return true;
    }

    internal static bool TryResolveFrozenCurrentHardTarget(
        IObjectTable objectTable,
        bool metadataVerified,
        IPlayerCharacter? localPlayer,
        TargetPressureActorIdentity expectedTarget,
        out DalamudBattleChara? target)
    {
        target = null;
        if (!expectedTarget.IsValid ||
            !TryResolveExactCurrentHardTarget(
                objectTable,
                metadataVerified,
                localPlayer,
                out var current,
                out var currentIdentity,
                out _))
        {
            return false;
        }

        if (currentIdentity != expectedTarget) return false;
        target = current;
        return true;
    }

    private static bool IsExactLiveStrikingDummy(
        IPlayerCharacter localPlayer,
        DalamudBattleChara? candidate)
    {
        if (!HasValidNativeIdentity(candidate) ||
            candidate is not DalamudBattleNpc
            {
                BattleNpcKind: BattleNpcSubKind.Combatant,
            } ||
            candidate.ObjectKind != ObjectKind.BattleNpc ||
            candidate.NameId != StrikingDummyNameId ||
            candidate.GameObjectId == localPlayer.GameObjectId ||
            candidate.EntityId == localPlayer.EntityId ||
            candidate.IsDead ||
            candidate.CurrentHp == 0 ||
            candidate.MaxHp == 0 ||
            candidate.CurrentHp > candidate.MaxHp ||
            !candidate.IsTargetable)
        {
            return false;
        }

        return true;
    }

    private static ulong GetNativeHardTargetId(IPlayerCharacter localPlayer)
    {
        if (!HasValidNativeIdentity(localPlayer)) return 0;
        var character = (Character*)localPlayer.Address;
        return character == null ? 0 : character->GetTargetId().Id;
    }

    private static bool ActorIdMatches(ulong actorId, DalamudGameObject actor) =>
        actorId == actor.GameObjectId ||
        actorId <= uint.MaxValue && (uint)actorId == actor.EntityId;

    private static bool HasSameNativeIdentity(
        DalamudGameObject? left,
        DalamudGameObject? right) =>
        left is not null &&
        right is not null &&
        left.Address != nint.Zero &&
        left.Address == right.Address &&
        left.GameObjectId == right.GameObjectId &&
        left.EntityId == right.EntityId;

    private static bool HasValidNativeIdentity(DalamudGameObject? actor) =>
        actor is not null &&
        actor.Address != nint.Zero &&
        IsNetworkObjectId(actor.GameObjectId) &&
        IsNetworkEntityId(actor.EntityId);

    private static bool IsNetworkEntityId(uint entityId) =>
        entityId is not 0 and not 0xE0000000 and not uint.MaxValue;

    private static bool IsNetworkObjectId(ulong gameObjectId) =>
        gameObjectId is not 0 and not 0xE0000000 and not ulong.MaxValue;
}
