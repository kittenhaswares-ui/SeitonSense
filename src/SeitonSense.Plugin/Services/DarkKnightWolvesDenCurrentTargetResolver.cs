using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using SeitonSense.Core;

namespace SeitonSense.Plugin.Services;

internal enum DarkKnightWolvesDenTargetKind : byte
{
    None = 0,
    DuelOpponent = 1,
    StrikingDummy = 2,
}

/// <summary>
/// Resolves only the local player's exact native hard target in Wolves' Den.
/// The actor must independently be either the active native PvP duel enemy or
/// the verified NameId-541 striking dummy. No synthetic slot or target write is
/// ever used.
/// </summary>
internal static unsafe class DarkKnightWolvesDenCurrentTargetResolver
{
    /// <summary>
    /// Resolves Smart Action's exact visible Wolves' Den target without relying
    /// on PvPDuelManager.EnemyEntityId. Some valid duel frames do not expose
    /// that slot, while the native hard target, dual object-table identity and
    /// hostile flag still provide one stable exact actor. This path never
    /// selects an alternate target.
    /// </summary>
    internal static bool TryResolveExactCurrentHardTargetDirect(
        IObjectTable objectTable,
        bool strikingDummyMetadataVerified,
        IPlayerCharacter? localPlayer,
        out IBattleChara? target,
        out TargetPressureActorIdentity identity,
        out DarkKnightWolvesDenTargetKind kind,
        out ulong nativeHardTargetId)
    {
        target = null;
        identity = default;
        kind = DarkKnightWolvesDenTargetKind.None;
        nativeHardTargetId = 0;
        if (!HasValidNativeIdentity(localPlayer)) return false;

        if (StrictWolvesDenStrikingDummyResolver.TryResolveExactCurrentHardTarget(
                objectTable,
                strikingDummyMetadataVerified,
                localPlayer,
                out var dummy,
                out var dummyIdentity,
                out nativeHardTargetId) &&
            SmartActionContextRules.IsEligibleExactVisibleWolvesDenTarget(
                isPlayerCharacter: false,
                hostileFlag: false,
                exactVerifiedStrikingDummy: true))
        {
            target = dummy;
            identity = dummyIdentity;
            kind = DarkKnightWolvesDenTargetKind.StrikingDummy;
            return true;
        }

        nativeHardTargetId = GetNativeHardTargetId(localPlayer!);
        if (!IsNetworkObjectId(nativeHardTargetId)) return false;

        var byObjectId = objectTable.SearchById(nativeHardTargetId)
            as IPlayerCharacter;
        var byEntityId = nativeHardTargetId <= uint.MaxValue
            ? objectTable.SearchByEntityId((uint)nativeHardTargetId)
                as IPlayerCharacter
            : null;
        if (HasValidNativeIdentity(byObjectId) &&
            HasValidNativeIdentity(byEntityId) &&
            !HasSameNativeIdentity(byObjectId, byEntityId))
        {
            return false;
        }

        var candidate = HasValidNativeIdentity(byObjectId)
            ? byObjectId
            : byEntityId;
        var hostileFlag = candidate is not null &&
                          (candidate.StatusFlags & StatusFlags.Hostile) != 0;
        if (!HasValidNativeIdentity(candidate) ||
            !SmartActionContextRules.IsEligibleExactVisibleWolvesDenTarget(
                isPlayerCharacter: true,
                hostileFlag,
                exactVerifiedStrikingDummy: false) ||
            !ActorIdMatches(nativeHardTargetId, candidate!) ||
            HasSameNativeIdentity(localPlayer, candidate) ||
            !IsLiveTargetablePlayer(localPlayer!, candidate!))
        {
            return false;
        }

        var canonicalByObjectId = objectTable.SearchById(candidate!.GameObjectId)
            as IPlayerCharacter;
        var canonicalByEntityId = objectTable.SearchByEntityId(candidate.EntityId)
            as IPlayerCharacter;
        if (!HasSameNativeIdentity(candidate, canonicalByObjectId) ||
            !HasSameNativeIdentity(candidate, canonicalByEntityId) ||
            GetNativeHardTargetId(localPlayer!) != nativeHardTargetId ||
            !IsLiveTargetablePlayer(localPlayer!, candidate))
        {
            return false;
        }

        identity = new TargetPressureActorIdentity(
            candidate.GameObjectId,
            candidate.EntityId);
        if (!identity.IsValid) return false;

        target = candidate;
        kind = DarkKnightWolvesDenTargetKind.DuelOpponent;
        return true;
    }

    internal static bool TryResolveExactCurrentHardTarget(
        IObjectTable objectTable,
        bool strikingDummyMetadataVerified,
        IPlayerCharacter? localPlayer,
        out IBattleChara? target,
        out TargetPressureActorIdentity identity,
        out DarkKnightWolvesDenTargetKind kind,
        out ulong nativeHardTargetId)
    {
        target = null;
        identity = default;
        kind = DarkKnightWolvesDenTargetKind.None;
        nativeHardTargetId = 0;
        if (!HasValidNativeIdentity(localPlayer)) return false;

        if (StrictWolvesDenStrikingDummyResolver.TryResolveExactCurrentHardTarget(
                objectTable,
                strikingDummyMetadataVerified,
                localPlayer,
                out var dummy,
                out var dummyIdentity,
                out nativeHardTargetId))
        {
            target = dummy;
            identity = dummyIdentity;
            kind = DarkKnightWolvesDenTargetKind.StrikingDummy;
            return true;
        }

        nativeHardTargetId = GetNativeHardTargetId(localPlayer!);
        if (!IsNetworkObjectId(nativeHardTargetId)) return false;

        var duelOpponent = WolvesDenOpponentResolver.Resolve(
            objectTable,
            localPlayer!,
            out var nativeDuelEnemyEntityId,
            out var nativePlayerResolved,
            out var hostileFlag);
        if (!nativePlayerResolved ||
            !hostileFlag ||
            !HasValidNativeIdentity(duelOpponent) ||
            nativeDuelEnemyEntityId != duelOpponent!.EntityId ||
            !ActorIdMatches(nativeHardTargetId, duelOpponent) ||
            !IsLiveTargetablePlayer(localPlayer!, duelOpponent))
        {
            return false;
        }

        var byObjectId = objectTable.SearchById(duelOpponent.GameObjectId)
            as IPlayerCharacter;
        var byEntityId = objectTable.SearchByEntityId(duelOpponent.EntityId)
            as IPlayerCharacter;
        if (!HasSameNativeIdentity(duelOpponent, byObjectId) ||
            !HasSameNativeIdentity(duelOpponent, byEntityId))
        {
            return false;
        }

        // Re-read both independent native ownership facts after the object-table
        // identity proof so a duel ending or hard-target change fails closed.
        var stableOpponent = WolvesDenOpponentResolver.Resolve(
            objectTable,
            localPlayer!,
            out var stableNativeDuelEnemyEntityId,
            out var stableNativePlayerResolved,
            out var stableHostileFlag);
        if (!stableNativePlayerResolved ||
            !stableHostileFlag ||
            stableNativeDuelEnemyEntityId != nativeDuelEnemyEntityId ||
            !HasSameNativeIdentity(duelOpponent, stableOpponent) ||
            GetNativeHardTargetId(localPlayer!) != nativeHardTargetId)
        {
            return false;
        }

        identity = new TargetPressureActorIdentity(
            duelOpponent.GameObjectId,
            duelOpponent.EntityId);
        if (!identity.IsValid) return false;

        target = duelOpponent;
        kind = DarkKnightWolvesDenTargetKind.DuelOpponent;
        return true;
    }

    internal static bool TryResolveFrozenCurrentHardTarget(
        IObjectTable objectTable,
        bool strikingDummyMetadataVerified,
        IPlayerCharacter? localPlayer,
        TargetPressureActorIdentity expectedTarget,
        DarkKnightWolvesDenTargetKind expectedKind,
        out IBattleChara? target)
    {
        target = null;
        if (!expectedTarget.IsValid ||
            expectedKind is not
                (DarkKnightWolvesDenTargetKind.DuelOpponent or
                 DarkKnightWolvesDenTargetKind.StrikingDummy) ||
            !TryResolveExactCurrentHardTarget(
                objectTable,
                strikingDummyMetadataVerified,
                localPlayer,
                out var current,
                out var currentIdentity,
                out var currentKind,
                out _))
        {
            return false;
        }

        if (currentIdentity != expectedTarget || currentKind != expectedKind)
            return false;

        target = current;
        return true;
    }

    private static bool IsLiveTargetablePlayer(
        IPlayerCharacter localPlayer,
        IPlayerCharacter candidate) =>
        candidate.GameObjectId != localPlayer.GameObjectId &&
        candidate.EntityId != localPlayer.EntityId &&
        (candidate.StatusFlags & StatusFlags.Hostile) != 0 &&
        !candidate.IsDead &&
        candidate.CurrentHp > 0 &&
        candidate.MaxHp >= candidate.CurrentHp &&
        candidate.IsTargetable;

    private static ulong GetNativeHardTargetId(IPlayerCharacter localPlayer)
    {
        if (!HasValidNativeIdentity(localPlayer)) return 0;
        var character = (Character*)localPlayer.Address;
        return character == null ? 0 : character->GetTargetId().Id;
    }

    private static bool ActorIdMatches(ulong actorId, IGameObject actor) =>
        actorId == actor.GameObjectId ||
        actorId <= uint.MaxValue && (uint)actorId == actor.EntityId;

    private static bool HasSameNativeIdentity(
        IGameObject? left,
        IGameObject? right) =>
        HasValidNativeIdentity(left) &&
        HasValidNativeIdentity(right) &&
        left!.Address == right!.Address &&
        left.GameObjectId == right.GameObjectId &&
        left.EntityId == right.EntityId;

    private static bool HasValidNativeIdentity(IGameObject? actor) =>
        actor is not null &&
        actor.Address != nint.Zero &&
        IsNetworkObjectId(actor.GameObjectId) &&
        IsNetworkEntityId(actor.EntityId);

    private static bool IsNetworkEntityId(uint entityId) =>
        entityId is not 0 and not 0xE0000000 and not uint.MaxValue;

    private static bool IsNetworkObjectId(ulong gameObjectId) =>
        gameObjectId is not 0 and not 0xE0000000 and not ulong.MaxValue;
}
