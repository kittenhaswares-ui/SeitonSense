using Dalamud.Game.ClientState.Objects.SubKinds;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using SeitonSense.Core;

namespace SeitonSense.Plugin.Services;

internal static class SeitonReadinessProbe
{
    internal const uint BaseActionId = 29515;
    internal const uint FollowUpActionId = 29516;
    internal const uint UnsealedStatusId = 3192;
    internal const float MaximumRange = 20f;

    internal static unsafe bool TryGetReadyAction(
        IPlayerCharacter localPlayer,
        out uint resolvedActionId)
    {
        resolvedActionId = 0;

        var actionManager = ActionManager.Instance();
        if (actionManager == null) return false;

        var sourceObject = (GameObject*)localPlayer.Address;
        if (sourceObject == null || sourceObject->EntityId != localPlayer.EntityId) return false;

        resolvedActionId = actionManager->GetAdjustedActionId(BaseActionId);
        if (resolvedActionId is not (BaseActionId or FollowUpActionId)) return false;

        var hasUnsealed = false;
        foreach (var status in localPlayer.StatusList)
        {
            if (status.StatusId != UnsealedStatusId ||
                !float.IsFinite(status.RemainingTime) ||
                status.RemainingTime <= 0f)
            {
                continue;
            }

            hasUnsealed = true;
            break;
        }

        if ((resolvedActionId == FollowUpActionId) != hasUnsealed) return false;
        if (resolvedActionId == BaseActionId)
        {
            var limitBreak = LimitBreakController.Instance();
            if (limitBreak == null ||
                !limitBreak->IsPvP ||
                limitBreak->BarCount == 0 ||
                limitBreak->BarUnits == 0 ||
                limitBreak->CurrentUnits < limitBreak->BarUnits)
            {
                return false;
            }
        }

        // Cooldown is stable resource state (not facing, animation lock, casting, or current target).
        // The old per-target action-status gate flickered during movement and prevented alerts.
        return actionManager->IsActionOffCooldown(ActionType.Action, resolvedActionId);
    }

    internal static unsafe bool HasRangeAndLineOfSight(
        IPlayerCharacter localPlayer,
        IPlayerCharacter target,
        uint resolvedActionId,
        out uint rangeStatus)
    {
        rangeStatus = uint.MaxValue;
        if (resolvedActionId is not (BaseActionId or FollowUpActionId)) return false;

        var sourceObject = (GameObject*)localPlayer.Address;
        var targetObject = (GameObject*)target.Address;
        if (sourceObject == null || targetObject == null ||
            sourceObject->EntityId != localPlayer.EntityId ||
            targetObject->EntityId != target.EntityId)
        {
            return false;
        }

        rangeStatus = ActionManager.GetActionInRangeOrLoS(
            resolvedActionId,
            sourceObject,
            targetObject);
        return SeitonRangeRules.HasNativeRangeAndLineOfSight(rangeStatus);
    }
}
