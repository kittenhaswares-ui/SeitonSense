using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using SeitonSense.Core;
using SeitonSense.Plugin.Models;
using GameAction = Lumina.Excel.Sheets.Action;

namespace SeitonSense.Plugin.Services;

internal readonly record struct SmartActionCanonicalEnemy(int Slot, IPlayerCharacter Player);

/// <summary>
/// Read-only Smart Action target/protection boundary. CC canonical slots and
/// the opted-in Den visible target have separate observation paths. Both feed
/// the same frozen-value protection evaluator. This component owns no action
/// manager, hook, macro token, timer, queue, or dispatch capability.
/// </summary>
internal sealed class SmartActionTargetProtectionService
{
    private const ulong InvalidObjectId = 0xE0000000;
    private readonly IObjectTable objectTable;
    private readonly SmartActionProtectionStatusCatalog smartActionProtectionStatuses;
    private readonly SmartActionGuardBypassCatalog smartActionGuardBypassActions;
    private readonly bool samuraiSmartActionCastsMetadataVerified;
    private readonly bool chitenMetadataVerified;
    private readonly bool wolvesDenStrikingDummyMetadataVerified;

    internal SmartActionTargetProtectionService(
        IObjectTable objectTable,
        SmartActionProtectionStatusCatalog smartActionProtectionStatuses,
        SmartActionGuardBypassCatalog smartActionGuardBypassActions,
        bool samuraiSmartActionCastsMetadataVerified,
        bool chitenMetadataVerified,
        bool wolvesDenStrikingDummyMetadataVerified)
    {
        this.objectTable = objectTable;
        this.smartActionProtectionStatuses = smartActionProtectionStatuses;
        this.smartActionGuardBypassActions = smartActionGuardBypassActions;
        this.samuraiSmartActionCastsMetadataVerified = samuraiSmartActionCastsMetadataVerified;
        this.chitenMetadataVerified = chitenMetadataVerified;
        this.wolvesDenStrikingDummyMetadataVerified = wolvesDenStrikingDummyMetadataVerified;
    }

    internal bool TryEvaluateExactSmartActionTargetProtection(
        SupportedPvPContext context,
        bool wolvesDenTestingEnabled,
        HashSet<uint>? partyEntityIds,
        uint resolvedActionId,
        IPlayerCharacter localPlayer,
        GameAction action,
        ulong requestedTargetId,
        out ulong canonicalTargetId,
        out string targetLabel)
    {
        canonicalTargetId = requestedTargetId;
        targetLabel = "unknown target";
        var attackShape = ClassifySmartActionAttackShape(action);

        if (context == SupportedPvPContext.CrystallineConflict)
        {
            if (partyEntityIds is null) return false;
            if (!TryBuildSmartActionProtectionSnapshot(
                    localPlayer,
                    partyEntityIds!,
                    attackShape,
                    out var canonicalEnemies,
                    out var ccProtectedActors))
            {
                return false;
            }

            var exactMatches = canonicalEnemies
                .Where(enemy =>
                    enemy.Player.GameObjectId == requestedTargetId ||
                    enemy.Player.EntityId == requestedTargetId)
                .Take(2)
                .ToArray();
            if (exactMatches.Length != 1) return false;

            var target = exactMatches[0];
            var safe = IsSmartActionProtectionSafe(
                resolvedActionId,
                localPlayer,
                attackShape,
                target,
                action.EffectRange,
                ccProtectedActors,
                actionIgnoresGuard:
                    CanSmartActionTargetGuard(resolvedActionId, action));
            if (!safe) return false;

            canonicalTargetId = target.Player.GameObjectId;
            targetLabel = $"S{target.Slot}";
            return true;
        }

        if (!SmartActionContextRules.CanInspectExactVisibleTargetTestFallback(
                context,
                wolvesDenTestingEnabled,
                combatPriorityMode: true,
                attackShape))
        {
            targetLabel = $"Den context/shape not admitted: context={context},shape={attackShape}";
            return false;
        }
        if (!DarkKnightWolvesDenCurrentTargetResolver.TryResolveExactCurrentHardTargetDirect(
                objectTable,
                wolvesDenStrikingDummyMetadataVerified,
                localPlayer,
                out var wolvesTarget,
                out var wolvesIdentity,
                out var wolvesKind,
                out var nativeHardTargetId) ||
            wolvesTarget is null)
        {
            targetLabel = "Den exact visible duel/dummy target proof unavailable";
            return false;
        }

        var effectiveTargetId = SmartActionContextRules
            .IsNativeSelectedTargetCarrier(requestedTargetId)
            ? nativeHardTargetId
            : requestedTargetId;
        if (!ActorIdMatches(effectiveTargetId, wolvesTarget))
        {
            targetLabel = $"Den authored/current target mismatch: authored={effectiveTargetId:X}," +
                          $"current={wolvesIdentity.GameObjectId:X}/{wolvesIdentity.EntityId:X}";
            return false;
        }
        if (!TryClassifyExactWolvesDenTargetProtection(
                wolvesTarget,
                out var protectionKind))
        {
            targetLabel = "Den exact target protection-status proof ambiguous";
            return false;
        }

        var targetGeometry = new SmartActionActorGeometry(
            EnemySlotRules.FirstSlot,
            wolvesIdentity,
            ExactCanonicalIdentity: true,
            wolvesTarget.Position,
            wolvesTarget.HitboxRadius);
        if (!TryBuildWolvesDenSmartActionProtectionSnapshot(
                localPlayer,
                wolvesTarget,
                targetGeometry,
                attackShape,
                protectionKind,
                out var wolvesProtectedActors))
        {
            targetLabel = $"Den area protection snapshot ambiguous: shape={attackShape}";
            return false;
        }
        var wolvesSafe = IsSmartActionProtectionSafe(
            resolvedActionId,
            localPlayer,
            attackShape,
            targetGeometry,
            action.EffectRange,
            wolvesProtectedActors,
            actionIgnoresGuard:
                CanSmartActionTargetGuard(resolvedActionId, action));
        if (!wolvesSafe)
        {
            var incidentalChiten = wolvesProtectedActors.Any(actor =>
                actor.Geometry.Actor != wolvesIdentity &&
                (actor.Kind & SmartActionProtectionKind.Chiten) != 0);
            targetLabel = $"Den protection blocked: target={protectionKind},shape={attackShape}," +
                          $"incidentalChiten={incidentalChiten},chitenMeta={chitenMetadataVerified}";
            return false;
        }

        canonicalTargetId = wolvesTarget.GameObjectId;
        targetLabel = wolvesKind == DarkKnightWolvesDenTargetKind.StrikingDummy
            ? "Wolves' Den dummy <t>"
            : "Wolves' Den duel <t>";
        return true;
    }

    private bool TryClassifyExactWolvesDenTargetProtection(
        IBattleChara target,
        out SmartActionProtectionKind protectionKind)
    {
        protectionKind = SmartActionProtectionKind.None;
        var player = target as IPlayerCharacter;
        var jobId = player?.ClassJob.IsValid == true
            ? player.ClassJob.RowId
            : 0u;
        if (player is not null &&
            !chitenMetadataVerified &&
            jobId is 0 or EnemyCombatConstants.SamuraiJobId)
        {
            protectionKind |= SmartActionProtectionKind.Chiten;
        }

        foreach (var status in target.StatusList)
        {
            var exactKind = ClassifySmartActionProtectionStatus(status.StatusId);
            if (exactKind == SmartActionProtectionKind.None) continue;
            if (exactKind == SmartActionProtectionKind.Chiten &&
                (player is null ||
                 jobId != EnemyCombatConstants.SamuraiJobId &&
                 !(!chitenMetadataVerified && jobId == 0)))
            {
                return false;
            }

            protectionKind |= exactKind;
        }

        return protectionKind == SmartActionProtectionKind.None ||
               SmartActionProtectionRules.IsExactProtectionKind(protectionKind);
    }

    private bool TryBuildWolvesDenSmartActionProtectionSnapshot(
        IPlayerCharacter localPlayer,
        IBattleChara exactTarget,
        SmartActionActorGeometry targetGeometry,
        SmartActionAttackShape attackShape,
        SmartActionProtectionKind targetProtectionKind,
        out SmartActionProtectedActor[] protectedActors)
    {
        var protections = new List<SmartActionProtectedActor>(5);
        if (targetProtectionKind != SmartActionProtectionKind.None)
        {
            protections.Add(new SmartActionProtectedActor(
                targetGeometry,
                targetProtectionKind));
        }

        if (!SmartActionProtectionRules.RequiresCompleteHostileSnapshot(attackShape))
        {
            protectedActors = protections.ToArray();
            return true;
        }

        var observedGameObjectIds = new HashSet<ulong>
        {
            exactTarget.GameObjectId,
        };
        var observedEntityIds = new HashSet<uint>
        {
            exactTarget.EntityId,
        };
        var nextSyntheticSlot = EnemySlotRules.FirstSlot + 1;
        foreach (var player in objectTable.PlayerObjects.OfType<IPlayerCharacter>())
        {
            if (!IsLivePlayer(player) ||
                player.GameObjectId == localPlayer.GameObjectId ||
                HasSameNativeIdentity(player, exactTarget) ||
                (player.StatusFlags & StatusFlags.Hostile) == 0)
            {
                continue;
            }

            if (!observedGameObjectIds.Add(player.GameObjectId) ||
                !observedEntityIds.Add(player.EntityId))
            {
                protectedActors = [];
                return false;
            }

            var jobId = player.ClassJob.IsValid ? player.ClassJob.RowId : 0;
            var incidentalKind = !chitenMetadataVerified &&
                                 jobId is 0 or EnemyCombatConstants.SamuraiJobId
                ? SmartActionProtectionKind.Chiten
                : SmartActionProtectionKind.None;
            foreach (var status in player.StatusList)
            {
                if (status.StatusId != SmartActionProtectionRules.ChitenStatusId)
                    continue;
                if (jobId != EnemyCombatConstants.SamuraiJobId &&
                    !(!chitenMetadataVerified && jobId == 0))
                {
                    protectedActors = [];
                    return false;
                }

                incidentalKind |= SmartActionProtectionKind.Chiten;
            }

            if (incidentalKind == SmartActionProtectionKind.None)
                continue;
            if (nextSyntheticSlot > EnemySlotRules.LastSlot)
            {
                protectedActors = [];
                return false;
            }

            protections.Add(new SmartActionProtectedActor(
                new SmartActionActorGeometry(
                    nextSyntheticSlot++,
                    new TargetPressureActorIdentity(
                        player.GameObjectId,
                        player.EntityId),
                    ExactCanonicalIdentity: true,
                    player.Position,
                    player.HitboxRadius),
                incidentalKind));
        }

        protectedActors = protections.ToArray();
        return true;
    }

    internal bool TryBuildSmartActionProtectionSnapshot(
        IPlayerCharacter localPlayer,
        HashSet<uint> partyEntityIds,
        SmartActionAttackShape attackShape,
        out SmartActionCanonicalEnemy[] canonicalEnemies,
        out SmartActionProtectedActor[] protectedActors)
    {
        if (!smartActionProtectionStatuses.IsVerified)
        {
            canonicalEnemies = [];
            protectedActors = [];
            return false;
        }

        var enemies = new List<SmartActionCanonicalEnemy>(5);
        var protections = new List<SmartActionProtectedActor>(5);
        var occupiedGameObjectIds = new HashSet<ulong>();
        var occupiedEntityIds = new HashSet<uint>();
        var occupiedActorIdentities = new HashSet<(ulong GameObjectId, uint EntityId)>();

        for (var slot = EnemySlotRules.FirstSlot; slot <= EnemySlotRules.LastSlot; slot++)
        {
            var enemy = EnemySlotResolver.Resolve(objectTable, slot);
            if (!IsLivePlayer(enemy) ||
                enemy!.GameObjectId == localPlayer.GameObjectId ||
                IsAlly(enemy, partyEntityIds))
            {
                continue;
            }

            if (!occupiedGameObjectIds.Add(enemy.GameObjectId) ||
                !occupiedEntityIds.Add(enemy.EntityId) ||
                !occupiedActorIdentities.Add((enemy.GameObjectId, enemy.EntityId)))
            {
                canonicalEnemies = [];
                protectedActors = [];
                return false;
            }

            var canonical = new SmartActionCanonicalEnemy(slot, enemy);
            enemies.Add(canonical);

            var jobId = enemy.ClassJob.IsValid ? enemy.ClassJob.RowId : 0;
            var protectionKind = !chitenMetadataVerified &&
                                 (jobId == EnemyCombatConstants.SamuraiJobId || jobId == 0)
                ? SmartActionProtectionKind.Chiten
                : SmartActionProtectionKind.None;
            foreach (var status in enemy.StatusList)
            {
                var exactKind = ClassifySmartActionProtectionStatus(status.StatusId);
                if (exactKind == SmartActionProtectionKind.None) continue;
                if (exactKind == SmartActionProtectionKind.Chiten)
                {
                    if (jobId != EnemyCombatConstants.SamuraiJobId &&
                        !(!chitenMetadataVerified && jobId == 0))
                    {
                        canonicalEnemies = [];
                        protectedActors = [];
                        return false;
                    }

                    protectionKind |= exactKind;
                    continue;
                }

                protectionKind |= exactKind;
            }

            if (protectionKind != SmartActionProtectionKind.None)
            {
                protections.Add(new SmartActionProtectedActor(
                    CreateSmartActionActorGeometry(canonical),
                    protectionKind));
            }
        }

        // A direct action can hit only its selected actor. Its own exact S-slot
        // status proof above is therefore sufficient even if an unrelated
        // hostile briefly appears in only one of Dalamud's two actor views.
        // Area and unknown shapes retain the strict complete-world comparison
        // below because an omitted protected peer could still be hit.
        if (!SmartActionProtectionRules.RequiresCompleteHostileSnapshot(attackShape))
        {
            canonicalEnemies = enemies.ToArray();
            protectedActors = protections.ToArray();
            return true;
        }

        // Area safety now needs complete incidental geometry only for Chiten,
        // the one reviewed protection which can retaliate against the caller.
        // A transient object-table actor absent from S1-S5 therefore blocks
        // only when it is a SAM, has unknown job metadata, or visibly carries
        // Chiten. Unrelated non-SAM Guard/Cover/LB actors cannot be selected
        // without a canonical slot and must not globally stall every AoE.
        var observedHostileGameObjectIds = new HashSet<ulong>();
        var observedHostileEntityIds = new HashSet<uint>();
        foreach (var player in objectTable.PlayerObjects.OfType<IPlayerCharacter>())
        {
            if (!IsLivePlayer(player) ||
                player.GameObjectId == localPlayer.GameObjectId ||
                IsAlly(player, partyEntityIds))
            {
                continue;
            }

            if (!observedHostileGameObjectIds.Add(player.GameObjectId) ||
                !observedHostileEntityIds.Add(player.EntityId))
            {
                canonicalEnemies = [];
                protectedActors = [];
                return false;
            }

            var accountedFor = occupiedActorIdentities.Contains(
                (player.GameObjectId, player.EntityId));
            if (accountedFor) continue;

            var jobId = player.ClassJob.IsValid ? player.ClassJob.RowId : 0;
            var couldCarryChiten =
                jobId is 0 or EnemyCombatConstants.SamuraiJobId ||
                player.StatusList.Any(status =>
                    status.StatusId == SmartActionProtectionRules.ChitenStatusId);
            if (!couldCarryChiten) continue;

            canonicalEnemies = [];
            protectedActors = [];
            return false;
        }

        canonicalEnemies = enemies.ToArray();
        protectedActors = protections.ToArray();
        return true;
    }

    internal static SmartActionAttackShape ClassifySmartActionAttackShape(GameAction action) =>
        SmartActionProtectionRules.ClassifyAttackShape(
            action.EffectRange,
            action.CastType);

    internal SmartActionProtectionKind ClassifySmartActionProtectionStatus(
        uint statusId) =>
        statusId == SmartActionProtectionRules.ChitenStatusId
            ? SmartActionProtectionKind.Chiten
            : smartActionProtectionStatuses.Classify(statusId);

    /// <summary>
    /// Guard may be selected when strict startup metadata proves that the
    /// action ignores/reduces Guard, or when the current exact PvP row is one
    /// of the closed ordinary hostile-target movement actions. This permission
    /// applies only to the selected actor's Guard; Chiten, Cover, and
    /// invulnerability remain blocking protections on that candidate.
    /// </summary>
    internal bool CanSmartActionTargetGuard(
        uint resolvedActionId,
        GameAction action) =>
        !SmartActionMovementGuardBypassRules.IsGuardBlockedCcMovement(resolvedActionId) &&
        (smartActionGuardBypassActions.Contains(resolvedActionId) ||
         (action.RowId == resolvedActionId &&
          action.ClassJob.IsValid &&
          SmartActionMovementGuardBypassRules.AllowsGuardTarget(
              action.ClassJob.RowId,
              resolvedActionId) &&
          action.ActionCategory.IsValid &&
          action.ActionCategory.RowId is 3 or 4 &&
          action.IsPvP &&
          action.CanTargetHostile &&
          !action.TargetArea &&
          action.Range > 0 &&
          action.AffectsPosition));

    internal bool IsSmartActionProtectionSafe(
        uint resolvedActionId,
        IPlayerCharacter localPlayer,
        SmartActionAttackShape attackShape,
        SmartActionCanonicalEnemy target,
        float effectRange,
        IReadOnlyList<SmartActionProtectedActor> protectedActors,
        bool actionIgnoresGuard,
        bool allowDamageOnlyInvulnerabilityForCcUtility = false)
        => IsSmartActionProtectionSafe(
            resolvedActionId,
            localPlayer,
            attackShape,
            CreateSmartActionActorGeometry(target),
            effectRange,
            protectedActors,
            actionIgnoresGuard,
            allowDamageOnlyInvulnerabilityForCcUtility);

    internal bool IsSmartActionProtectionSafe(
        uint resolvedActionId,
        IPlayerCharacter localPlayer,
        SmartActionAttackShape attackShape,
        SmartActionActorGeometry targetGeometry,
        float effectRange,
        IReadOnlyList<SmartActionProtectedActor> protectedActors,
        bool actionIgnoresGuard,
        bool allowDamageOnlyInvulnerabilityForCcUtility = false) =>
        SmartActionProtectionEvaluator.Evaluate(
            new SmartActionProtectionQuery(
                resolvedActionId,
                !allowDamageOnlyInvulnerabilityForCcUtility &&
                samuraiSmartActionCastsMetadataVerified &&
                SamuraiSmartActionCastRules.IsOgiNamikiriConeAction(resolvedActionId)
                    ? localPlayer.Position : default,
                targetGeometry,
                attackShape, effectRange, actionIgnoresGuard,
                allowDamageOnlyInvulnerabilityForCcUtility,
                samuraiSmartActionCastsMetadataVerified),
            protectedActors).Allowed;


    internal static SmartActionActorGeometry CreateSmartActionActorGeometry(
        SmartActionCanonicalEnemy enemy) =>
        new(
            enemy.Slot,
            new TargetPressureActorIdentity(
                enemy.Player.GameObjectId,
                enemy.Player.EntityId),
            ExactCanonicalIdentity: true,
            enemy.Player.Position,
            enemy.Player.HitboxRadius);

    private static bool IsAlly(IPlayerCharacter player, HashSet<uint> partyEntityIds) =>
        partyEntityIds.Contains(player.EntityId) ||
        (player.StatusFlags & (StatusFlags.PartyMember | StatusFlags.AllianceMember)) != 0;

    private static bool IsLivePlayer(IPlayerCharacter? player) =>
        player is not null &&
        player.Address != 0 &&
        IsNetworkEntityId(player.EntityId) &&
        IsNetworkObjectId(player.GameObjectId) &&
        player.IsTargetable &&
        !player.IsDead &&
        player.CurrentHp > 0 &&
        player.MaxHp >= player.CurrentHp;

    private static bool HasSameNativeIdentity(
        IGameObject? left,
        IGameObject? right) =>
        left is not null &&
        right is not null &&
        left.Address != 0 &&
        right.Address != 0 &&
        left.Address == right.Address &&
        left.GameObjectId == right.GameObjectId &&
        left.EntityId == right.EntityId;

    private static bool ActorIdMatches(ulong actorId, IGameObject actor) =>
        actorId == actor.GameObjectId ||
        actorId <= uint.MaxValue && (uint)actorId == actor.EntityId;

    private static bool IsNetworkEntityId(uint entityId) =>
        entityId is not 0 and not 0xE0000000;

    private static bool IsNetworkObjectId(ulong objectId) =>
        objectId is not 0 and not InvalidObjectId and not ulong.MaxValue;
}
