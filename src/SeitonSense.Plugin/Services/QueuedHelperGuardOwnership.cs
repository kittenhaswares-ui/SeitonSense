using FFXIVClientStructs.FFXIV.Client.Game;
using SeitonSense.Core;

namespace SeitonSense.Plugin.Services;

internal readonly record struct QueuedHelperGuardContext(
    uint TerritoryId, TargetPressureActorIdentity LocalPlayer)
{
    internal bool IsValid => TerritoryId != 0 && LocalPlayer.IsValid;
}

internal readonly record struct QueuedHelperGuardRequest(
    ActionType ActionType, uint RequestedActionId, uint ResolvedActionId,
    ulong TargetId, uint ExtraParam, uint ComboRouteId);

internal readonly record struct QueuedHelperQueueInvocation(
    ActionType ActionType, uint ActionId, ulong TargetId, uint ExtraParam,
    ActionManager.UseActionMode Mode, uint ComboRouteId);

/// <summary>
/// Managed attribution only. Never changes, clears, or submits a native queue.
/// A helper's synchronous scope may end before FFXIV replays its queued action;
/// this lease lets that exact continuation retain the helper Guard gate.
/// </summary>
internal sealed class QueuedHelperGuardOwnership
{
    // A cleared-queue handoff ceiling, not the game's queue duration. An exact
    // live native queue is continuing proof of ownership even after this time.
    // Once ActionQueued is cleared, only an exact replay within this original
    // deadline (or fresh Guard proof) retains attribution. Observations never
    // refresh capture time, and ownership alone never claims Guard is active.
    internal const long MaximumAttributionMilliseconds = 2_000;

    private readonly object gate = new();
    private Lease? lease;
    private long nextToken;

    internal bool HasOwnership { get { lock (gate) return lease is not null; } }

    internal bool Capture(
        bool nativeBoundaryInvoked, ClientActionAttemptFingerprint before,
        ClientActionAttemptFingerprint after, QueuedHelperGuardRequest request,
        QueuedHelperGuardContext context, long nowMilliseconds)
    {
        lock (gate)
        {
            ValidateCurrent(after, context, nowMilliseconds, preserveEmpty: false);
            // A changed exact queue proves provenance even if native returned
            // false or threw. This records no action acceptance or success.
            if (!nativeBoundaryInvoked || !context.IsValid || nowMilliseconds < 0 ||
                !before.Captured || !after.Captured || !after.ActionQueued ||
                request.ActionType is not (ActionType.Action or ActionType.PvPAction) ||
                request.RequestedActionId == 0 || request.ResolvedActionId == 0 ||
                after.AdjustedActionId != request.ResolvedActionId)
                return false;

            var queued = QueueTuple.From(after);
            if (queued == QueueTuple.From(before) ||
                queued.ActionType != (uint)request.ActionType ||
                queued.ActionId != request.RequestedActionId && queued.ActionId != request.ResolvedActionId ||
                queued.TargetId != request.TargetId || queued.ExtraParam != request.ExtraParam ||
                queued.ComboRouteId != request.ComboRouteId)
                return false;

            if (nextToken == long.MaxValue) return false;
            lease = new Lease(++nextToken, queued, request, context, nowMilliseconds);
            return true;
        }
    }

    internal bool TryMatchExactQueueReplay(
        QueuedHelperQueueInvocation invocation, ClientActionAttemptFingerprint currentQueue,
        QueuedHelperGuardContext context, long nowMilliseconds, out long ownershipToken,
        bool ownGuardActiveOrAcceptedPropagation = false)
    {
        lock (gate)
        {
            ownershipToken = 0;
            if (!ValidateCurrent(currentQueue, context, nowMilliseconds, preserveEmpty: true,
                    ownGuardActiveOrAcceptedPropagation) ||
                lease is not { } owned || invocation.Mode != ActionManager.UseActionMode.Queue)
                return false;

            if (!owned.Queue.MatchesInvocation(invocation))
            {
                // A different native replay proves this is no longer the
                // continuation we captured, even if its queue fields are stale.
                lease = null;
                return false;
            }

            ownershipToken = owned.Token;
            return true; // Matching is not consumption: Guard may veto repeatedly.
        }
    }

    internal void ObservePostCall(
        ClientActionAttemptFingerprint currentQueue, QueuedHelperGuardContext context,
        long nowMilliseconds, long matchedReplayToken = 0, bool replayAccepted = false,
        bool ownGuardActiveOrAcceptedPropagation = false)
    {
        lock (gate)
        {
            var sameReplay = matchedReplayToken != 0 && lease?.Token == matchedReplayToken;
            if (sameReplay && replayAccepted)
            {
                lease = null;
                return;
            }

            // FFXIV can clear ActionQueued immediately before Mode.Queue. A
            // vetoed exact replay retains attribution, even in that window.
            ValidateCurrent(currentQueue, context, nowMilliseconds, preserveEmpty: sameReplay,
                ownGuardActiveOrAcceptedPropagation);
        }
    }

    internal void ObserveFreshUserAction(
        QueuedHelperQueueInvocation invocation, bool isManualGuard)
    {
        lock (gate)
        {
            if (isManualGuard || invocation.Mode == ActionManager.UseActionMode.Queue || lease is not { } owned)
                return;
            // A genuinely fresh press of this ability supersedes helper intent.
            // Other presses are resolved by their post-call queue snapshot;
            // notably a manual Guard must leave a still-queued helper protected.
            if ((uint)invocation.ActionType == owned.Queue.ActionType &&
                (invocation.ActionId == owned.Request.RequestedActionId ||
                 invocation.ActionId == owned.Request.ResolvedActionId))
                lease = null;
        }
    }

    internal void Clear() { lock (gate) lease = null; }

    private bool ValidateCurrent(ClientActionAttemptFingerprint current,
        QueuedHelperGuardContext context, long now, bool preserveEmpty,
        bool ownGuardActiveOrAcceptedPropagation = false)
    {
        if (lease is not { } owned) return false;
        if (!context.IsValid || context != owned.Context || !current.Captured ||
            now < owned.CapturedAtMilliseconds ||
            (!current.ActionQueued && !ownGuardActiveOrAcceptedPropagation &&
             now - owned.CapturedAtMilliseconds >= MaximumAttributionMilliseconds) ||
            (current.ActionQueued ? QueueTuple.From(current) != owned.Queue : !preserveEmpty))
        {
            lease = null;
            return false;
        }
        return true;
    }

    private sealed record Lease(long Token, QueueTuple Queue, QueuedHelperGuardRequest Request,
        QueuedHelperGuardContext Context, long CapturedAtMilliseconds);

    private readonly record struct QueueTuple(bool ActionQueued, uint ActionType, uint ActionId,
        ulong TargetId, uint ExtraParam, uint Mode, uint ComboRouteId)
    {
        internal static QueueTuple From(ClientActionAttemptFingerprint value) => new(
            value.ActionQueued, value.QueuedActionType, value.QueuedActionId, value.QueuedTargetId,
            value.QueuedExtraParam, value.QueueMode, value.QueuedComboRouteId);

        internal bool MatchesInvocation(QueuedHelperQueueInvocation value) =>
            (uint)value.ActionType == ActionType && value.ActionId == ActionId &&
            value.TargetId == TargetId && value.ExtraParam == ExtraParam &&
            value.ComboRouteId == ComboRouteId;
    }
}
