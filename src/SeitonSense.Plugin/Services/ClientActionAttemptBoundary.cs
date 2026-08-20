using FFXIVClientStructs.FFXIV.Client.Game;
using SeitonSense.Core;

namespace SeitonSense.Plugin.Services;

internal static class ClientActionAttemptBoundary
{
    internal static unsafe ClientActionAttemptFingerprint Capture(
        ActionManager* actionManager,
        uint actionId)
    {
        if (actionManager == null || actionId == 0)
            return default;

        return new ClientActionAttemptFingerprint(
            Captured: true,
            actionManager->ActionQueued,
            (uint)actionManager->QueuedActionType,
            actionManager->QueuedActionId,
            (ulong)actionManager->QueuedTargetId,
            actionManager->QueuedExtraParam,
            (uint)actionManager->QueueType,
            actionManager->QueuedComboRouteId,
            actionManager->LastUsedActionSequence,
            actionManager->AnimationLock,
            actionManager->CastActionId,
            actionManager->GetAdjustedActionId(actionId),
            actionManager->IsActionOffCooldown(ActionType.Action, actionId),
            actionManager->CheckActionResources(ActionType.Action, actionId));
    }

    internal static unsafe bool IsExactActionReady(
        ActionManager* actionManager,
        uint actionId) =>
        Capture(actionManager, actionId).IsExactActionReady(actionId);
}
