using Dalamud.Game.ClientState.Objects.SubKinds;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace SeitonSense.Plugin.Services;

internal static class SeitonReadinessProbe
{
    internal const uint BaseActionId = 29515;
    internal const uint FollowUpActionId = 29516;
    internal const uint UnsealedStatusId = 3192;
    internal const float MaximumRange = 20f;

    internal static unsafe bool IsAvailableForTarget(
        IPlayerCharacter localPlayer,
        IPlayerCharacter target,
        out uint resolvedActionId,
        out uint actionStatus,
        out uint rangeStatus)
    {
        resolvedActionId = 0;
        actionStatus = uint.MaxValue;
        rangeStatus = uint.MaxValue;

        var actionManager = ActionManager.Instance();
        if (actionManager == null) return false;

        var sourceObject = (GameObject*)localPlayer.Address;
        var targetObject = (GameObject*)target.Address;
        if (sourceObject == null || targetObject == null ||
            sourceObject->EntityId != localPlayer.EntityId ||
            targetObject->EntityId != target.EntityId)
        {
            return false;
        }

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

        if (!ActionManager.CanUseActionOnTarget(resolvedActionId, targetObject)) return false;
        rangeStatus = ActionManager.GetActionInRangeOrLoS(
            resolvedActionId,
            sourceObject,
            targetObject);
        if (rangeStatus != 0) return false;

        actionStatus = actionManager->GetActionStatus(
            ActionType.Action,
            resolvedActionId,
            target.GameObjectId,
            true,
            true);
        return actionStatus == 0 &&
               actionManager->IsActionOffCooldown(ActionType.Action, resolvedActionId);
    }
}
