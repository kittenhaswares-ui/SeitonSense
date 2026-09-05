using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game;
using SeitonSense.Core;

namespace SeitonSense.Plugin.Services;

internal sealed unsafe partial class NearAssistRedirector
{
    private long samuraiSmartActionTapGeneration;
    private SamuraiCastProtectionCoordinator? samuraiCastProtection;

    private SamuraiCastProtectionCoordinator SamuraiCastProtection
    {
        get
        {
            lock (tokenGate)
                return samuraiCastProtection ??= new SamuraiCastProtectionCoordinator(
                    ReadSamuraiCastProtectionSnapshot, static () => Environment.TickCount64);
        }
    }

    private SamuraiCastProtectionRequest? TryBeginExactInFlightSamuraiCast(
        uint rawActionId, uint resolvedActionId, ulong targetGameObjectId,
        long tapGeneration, ActionType actionType, uint extraParam, uint comboRouteId)
    {
        try
        {
            return SamuraiCastProtection.Begin(rawActionId, resolvedActionId, targetGameObjectId,
                tapGeneration, samuraiSmartActionCastsMetadataVerified, actionType, extraParam, comboRouteId,
                ResolveExactSamuraiTarget(targetGameObjectId)?.EntityId ?? 0);
        }
        catch (Exception exception)
        {
            LogFailure(exception, "Seiton Sense could not begin exact SAM request protection.");
            return null; // Bookkeeping cannot suppress the user's authored action.
        }
    }

    private SamuraiCastProtectionRequest? TryBeginQueuedSamuraiCast(
        ActionManager* actionManager, ActionType actionType, uint actionId, ulong targetId,
        uint extraParam, ActionManager.UseActionMode mode, uint comboRouteId, out bool blocked)
    {
        blocked = false;
        if (mode != ActionManager.UseActionMode.Queue || samuraiCastProtection?.HasOwnership != true)
            return null;
        var prepared = SamuraiCastProtection.TryPrepareQueuedContinuation(
            new QueuedHelperQueueInvocation(actionType, actionId, targetId, extraParam, mode, comboRouteId),
            () => ResolveActionId(actionManager, actionType, actionId),
            request =>
            {
                var target = ResolveExactSamuraiTarget(request.TargetId, request.TargetEntityId);
                return target is null ? default : new TargetPressureActorIdentity(target.GameObjectId, target.EntityId);
            },
            IsExactBufferedSmartActionProtectionSafe,
            samuraiSmartActionCastsMetadataVerified);
        blocked = prepared.Blocked;
        if (prepared.Failure is { } exception)
            LogFailure(exception, blocked
                ? "Seiton Sense claimed SAM queue continuation failed closed."
                : "Seiton Sense unclaimed queue inspection failed open for the native action.");
        return prepared.Request;
    }

    internal bool IsSamuraiCastMovementSuppressed() => IsOwnedSamuraiCastProtected();

    // The helper CancelCast boundary uses this same managed phase check:
    // an in-flight request is protected too, but an idle native queue is not.
    internal bool IsOwnedSamuraiCastProtected() => SamuraiCastProtection.IsProtected();

    internal SamuraiCastProtectionStatus SamuraiCastProtectionStatus => SamuraiCastProtection.Status;

    internal SamuraiCastInputStatus SamuraiCastInputStatus
    {
        get
        {
            var status = SamuraiCastProtection.Status;
            return new SamuraiCastInputStatus(
                samuraiSmartActionCastsMetadataVerified,
                integratedInputRuntime?.GameplayMovementHooksOperational == true,
                status.AcceptedCastActive,
                status.InFlightCount,
                status.AcceptedCastCount,
                integratedInputRuntime?.SamuraiMovementDiagnostics ?? default);
        }
    }

    private bool ShouldBlockActionDuringOwnedSamuraiCast(
        ActionManager* actionManager, ActionType actionType, uint actionId, bool pluginOwnedAction)
    {
        if (!IsSupportedActionType(actionType) || samuraiCastProtection?.HasOwnership != true)
            return false;
        // Preserve exact canonical emergencies even if adjusted-action inspection
        // is unavailable. PvP sheet-index calls still resolve below; plugin-owned
        // Guard is never mistaken for the user's manual override.
        if (samuraiCastProtection.TryAllowCanonicalEmergencyAction(
                actionType, actionId, pluginOwnedAction))
            return false;
        try
        {
            return SamuraiCastProtection.ShouldBlockAction(
                ResolveActionId(actionManager, actionType, actionId), pluginOwnedAction);
        }
        catch (Exception exception)
        {
            LogFailure(exception, "Seiton Sense could not classify an action during owned SAM casting.");
            return SamuraiCastProtection.IsProtected();
        }
    }

    private SamuraiCastProtectionSnapshot ReadSamuraiCastProtectionSnapshot()
    {
        var local = objectTable.LocalPlayer;
        var actionManager = ActionManager.Instance();
        long tapGeneration;
        lock (tokenGate) tapGeneration = samuraiSmartActionTapGeneration;
        var validLocal = IsLivePlayer(local);
        var isCasting = validLocal && local!.IsCasting;
        // All cast identity/timing fields come from the same native CastInfo
        // rather than mixing the character flag with ActionManager state.
        var castActionId = isCasting ? local!.CastActionId : 0;
        var adjustedCastActionId = castActionId != 0 && actionManager != null
            ? actionManager->GetAdjustedActionId(castActionId) : 0;
        return new SamuraiCastProtectionSnapshot(
            IsSamuraiCastProtectionRuntimeEnabled() && actionManager != null,
            clientState.TerritoryType,
            validLocal ? new(local!.GameObjectId, local.EntityId) : default,
            validLocal,
            local?.ClassJob.IsValid == true ? local.ClassJob.RowId : 0,
            tapGeneration,
            validLocal && HasCastBreakingCrowdControl(local!),
            validLocal && DefensiveUtilityProbe.HasActiveGuard(local!),
            isCasting,
            castActionId,
            adjustedCastActionId,
            isCasting ? local!.CastTargetObjectId : 0)
        {
            Queue = CaptureQueuedHelperBoundary(actionManager, 0),
            CurrentCastTime = isCasting ? local!.CurrentCastTime : 0,
            TotalCastTime = isCasting ? local!.TotalCastTime : 0,
        };
    }

    internal void UpdateSamuraiLateCastFacing()
    {
        if (!configuration.EnableSamuraiLateCastFacing || samuraiCastProtection?.HasOwnership != true) return;
        try
        {
            // Explicit per-frame work only. Camera, target selection and input
            // predicates are untouched; only the already frozen cast target is used.
            // Check exact cast timing before resolving the target: no target
            // lookup occurs before the small window or after its one claim.
            if (samuraiCastProtection.GetLateFacingTarget(
                    configuration.SamuraiLateCastFacingWindowSeconds) is not { } identity) return;
            var target = ResolveExactSamuraiTarget(identity.GameObjectId, identity.EntityId);
            var actionManager = ActionManager.Instance();
            if (target is null || actionManager == null) return;
            var position = target.Position;
            if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z)) return;
            if (!IsExactBufferedSmartActionProtectionSafe(
                    samuraiCastProtection.OwnedCastActionId, identity.GameObjectId)) return;
            var request = samuraiCastProtection.TryClaimLateFacing(identity,
                configuration.SamuraiLateCastFacingWindowSeconds);
            if (request is null) return;
            // Uses the game's ordinary auto-face policy, not direct rotation.
            // A void return proves invocation only, never the server's hit result.
            actionManager->AutoFaceTargetPosition(&position);
        }
        catch (Exception exception)
        {
            LogFailure(exception, "Seiton Sense optional SAM late facing failed; no retry.");
        }
    }

    private IBattleChara? ResolveExactSamuraiTarget(ulong gameObjectId, uint entityId = 0)
    {
        if (ResolveContext() == SupportedPvPContext.WolvesDen)
        {
            var local = objectTable.LocalPlayer;
            // Keep the already-supported Den resolver's exact visible duel/
            // dummy proof, including one absent index but never conflicting IDs.
            if (!configuration.EnableWolvesDenTesting || !IsLivePlayer(local) ||
                !DarkKnightWolvesDenCurrentTargetResolver.TryResolveExactCurrentHardTargetDirect(
                    objectTable, wolvesDenStrikingDummyMetadataVerified, local!,
                    out var denTarget, out var denIdentity, out _, out _) || denTarget is null ||
                denIdentity.GameObjectId != gameObjectId || entityId != 0 && denIdentity.EntityId != entityId)
                return null;
            return denTarget;
        }
        // Identity-only lookup of the already-admitted target, not candidate
        // discovery. IBattleChara also admits the existing Den dummy route.
        if (objectTable.SearchById(gameObjectId) is not IBattleChara target ||
            target.GameObjectId != gameObjectId || target.Address == 0 ||
            !IsNetworkEntityId(target.EntityId) || entityId != 0 && target.EntityId != entityId ||
            !target.IsTargetable || target.IsDead || target.CurrentHp == 0 ||
            target.CurrentHp > target.MaxHp ||
            !HasSameNativeIdentity(target, objectTable.SearchByEntityId(target.EntityId))) return null;
        return target;
    }

    private bool IsSamuraiCastProtectionRuntimeEnabled()
    {
        try
        {
            return SamuraiOgiCastProtectionRules.CanMaintainCastProtection(
                started, disposed, configuration.Enabled, configuration.EnableSmartActionMacro,
                clientState.IsLoggedIn, ResolveContext(), configuration.EnableWolvesDenTesting);
        }
        catch { return false; }
    }

    private void RevokeOwnedSamuraiCastProtection()
    {
        SamuraiCastProtection.Reset();
        lock (tokenGate) samuraiSmartActionTapGeneration = 0;
    }

    private static bool HasCastBreakingCrowdControl(IPlayerCharacter player) =>
        player.StatusList.Any(static status =>
            (status.StatusId is EnemyCombatConstants.PvPStunStatusId or
                EnemyCombatConstants.PvPSilenceStatusId or
                EnemyCombatConstants.DeepFreezeStatusId or
                EnemyCombatConstants.MiracleOfNatureStatusId) &&
            float.IsFinite(status.RemainingTime) &&
            status.RemainingTime > 0f);
}
