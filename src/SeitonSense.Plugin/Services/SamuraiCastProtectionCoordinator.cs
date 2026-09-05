using FFXIVClientStructs.FFXIV.Client.Game;
using SeitonSense.Core;

namespace SeitonSense.Plugin.Services;

internal enum SamuraiCastProtectionPhase : byte { Idle, InFlight, Queued, AwaitingCast, ExactCast }

internal readonly record struct SamuraiCastProtectionSnapshot(
    bool RuntimeEnabled,
    uint TerritoryId,
    TargetPressureActorIdentity LocalPlayer,
    bool LocalPlayerAlive,
    uint LocalJobId,
    long CurrentTapGeneration,
    bool CastBreakingCrowdControl,
    bool GuardActive,
    bool IsCasting,
    uint CastActionId,
    uint AdjustedCastActionId,
    ulong CastTargetGameObjectId)
{
    internal ClientActionAttemptFingerprint Queue { get; init; }
    internal float CurrentCastTime { get; init; }
    internal float TotalCastTime { get; init; }
}

internal readonly record struct SamuraiCastProtectionStatus(
    SamuraiCastProtectionPhase Phase,
    int InFlightCount,
    long RequestedCount,
    long AcceptedCastCount,
    long ObservedCastCount,
    string LastEvent,
    string LastReleaseReason)
{
    internal bool AcceptedCastActive => Phase is
        SamuraiCastProtectionPhase.AwaitingCast or SamuraiCastProtectionPhase.ExactCast;
    internal long LateFacingAttempts { get; init; }
}

internal sealed class SamuraiCastProtectionRequest(
    SamuraiCastProtectionSnapshot snapshot, long startedAt,
    ActionType actionType, uint rawActionId, uint resolvedActionId,
    ulong targetId, uint extraParam, uint comboRouteId, bool queueContinuation, uint targetEntityId = 0)
{
    internal SamuraiCastProtectionSnapshot Snapshot { get; } = snapshot;
    internal long StartedAt { get; } = startedAt;
    internal ActionType ActionType { get; } = actionType;
    internal uint RawActionId { get; } = rawActionId;
    internal uint ResolvedActionId { get; } = resolvedActionId;
    internal ulong TargetId { get; } = targetId;
    internal uint ExtraParam { get; } = extraParam;
    internal uint ComboRouteId { get; } = comboRouteId;
    internal bool QueueContinuation { get; } = queueContinuation;
    internal uint TargetEntityId { get; } = targetEntityId;
}

internal readonly record struct SamuraiQueuedCastPreparation(
    SamuraiCastProtectionRequest? Request, bool Blocked, Exception? Failure = null);

/// <summary>
/// Owns only one authored SAM request's managed protection. It never submits
/// or edits a native queue, changes a target, cancels a cast, or retries.
/// The injected snapshot/clock and Execute boundary are shared by production
/// and integration tests; client acceptance is distinct from observed casting.
/// </summary>
internal sealed class SamuraiCastProtectionCoordinator(
    Func<SamuraiCastProtectionSnapshot> readSnapshot, Func<long> readClock)
{
    internal const long MaximumQueueHandoffMilliseconds = 2_000;
    private readonly object gate = new();
    private readonly HashSet<SamuraiCastProtectionRequest> inFlight = [];
    private AcceptedLease? accepted;
    private QueuedLease? queued;
    private long requestedCount;
    private long acceptedCount;
    private long observedCount;
    private long lateFacingAttempts;
    private long lastObservationAt = -1;
    private string lastEvent = "Not requested";
    private string lastReleaseReason = "None";

    internal bool HasOwnership { get { lock (gate) return inFlight.Count != 0 || accepted is not null || queued is not null; } }
    internal uint OwnedCastActionId { get { lock (gate) return accepted?.Request.ResolvedActionId ?? 0; } }

    internal static bool IsPluginOwnedAction(
        bool synchronousHelperOwned, bool pluginOwnedHeldRetry, bool exactAutomaticScope) =>
        synchronousHelperOwned || pluginOwnedHeldRetry || exactAutomaticScope;

    internal void Abandon(SamuraiCastProtectionRequest request, string reason)
    {
        lock (gate) ReleaseRequestLocked(request, reason);
    }

    internal SamuraiCastProtectionRequest? Begin(
        uint rawActionId, uint resolvedActionId, ulong targetGameObjectId,
        long tapGeneration, bool metadataVerified,
        ActionType actionType = ActionType.Action, uint extraParam = 0, uint comboRouteId = 0,
        uint targetEntityId = 0)
    {
        if (!TryRead(out var snapshot, out var now)) return null;
        lock (gate)
        {
            ObserveLocked(snapshot, now);
            if (ContextFailure(snapshot) is not null ||
                actionType is not (ActionType.Action or ActionType.PvPAction) ||
                !SamuraiOgiCastProtectionRules.CanBeginExactInFlightRequest(
                    metadataVerified, true, tapGeneration, snapshot.CurrentTapGeneration,
                    rawActionId, resolvedActionId, IsNetworkTarget(targetGameObjectId)))
            {
                lastEvent = "Request not owned: metadata, context, action, target or generation";
                return null;
            }

            var request = new SamuraiCastProtectionRequest(snapshot, now, actionType,
                rawActionId, resolvedActionId, targetGameObjectId, extraParam, comboRouteId, false, targetEntityId);
            inFlight.Add(request);
            requestedCount++;
            lastEvent = "Exact request in flight";
            return request;
        }
    }

    internal SamuraiCastProtectionRequest? TryClaimQueuedContinuation(
        QueuedHelperQueueInvocation invocation, uint resolvedActionId)
    {
        if (!TryRead(out var snapshot, out var now)) return null;
        lock (gate)
        {
            ObserveLocked(snapshot, now);
            if (queued is not { } pending || invocation.Mode != ActionManager.UseActionMode.Queue ||
                !pending.Queue.Matches(invocation) || resolvedActionId != pending.Request.ResolvedActionId)
                return null;

            queued = null; // Exactly one continuation, never a same-ID spam exemption.
            var request = new SamuraiCastProtectionRequest(snapshot, now, invocation.ActionType,
                invocation.ActionId, resolvedActionId, invocation.TargetId,
                invocation.ExtraParam, invocation.ComboRouteId, true, pending.Request.TargetEntityId);
            inFlight.Add(request);
            requestedCount++;
            lastEvent = "Exact native queued continuation in flight";
            return request;
        }
    }

    internal SamuraiQueuedCastPreparation TryPrepareQueuedContinuation(
        QueuedHelperQueueInvocation invocation,
        Func<uint> resolveActionId,
        Func<SamuraiCastProtectionRequest, TargetPressureActorIdentity> resolveExactTarget,
        Func<uint, ulong, bool> isExactTargetProtectionSafe,
        bool metadataVerified)
    {
        if (invocation.Mode != ActionManager.UseActionMode.Queue) return default;
        SamuraiCastProtectionRequest? claimed = null;
        try
        {
            var resolved = resolveActionId();
            claimed = TryClaimQueuedContinuation(invocation, resolved);
            if (claimed is null) return default;
            var target = resolveExactTarget(claimed);
            var frozenTarget = new TargetPressureActorIdentity(claimed.TargetId, claimed.TargetEntityId);
            if (!frozenTarget.IsValid || target != frozenTarget)
            {
                Abandon(claimed, "Queued cast exact target identity changed");
                return new(null, true);
            }
            if (!metadataVerified || !isExactTargetProtectionSafe(resolved, claimed.TargetId))
            {
                Abandon(claimed, "Queued cast target protection changed");
                return new(null, true);
            }
            return new(claimed, false);
        }
        catch (Exception exception)
        {
            if (claimed is not null) Abandon(claimed, "Claimed queued cast inspection unavailable");
            else Reset("Unclaimed queue inspection unavailable; native action preserved");
            // Failure alone is not proof this is our action. Only a consumed
            // exact queue claim permits a fail-closed veto of native execution.
            return new(null, claimed is not null, exception);
        }
    }

    internal bool Execute(SamuraiCastProtectionRequest? request, Func<bool> invokeNative)
    {
        if (request is null) return invokeNative();
        try
        {
            var clientAccepted = invokeNative();
            Complete(request, clientAccepted);
            return clientAccepted;
        }
        catch
        {
            lock (gate) ReleaseRequestLocked(request, "Native request threw; no retry");
            throw;
        }
        finally
        {
            lock (gate) inFlight.Remove(request);
        }
    }

    private void Complete(SamuraiCastProtectionRequest request, bool clientAccepted)
    {
        if (!TryRead(out var snapshot, out var now)) return;
        lock (gate)
        {
            ObserveLocked(snapshot, now);
            // Reset, CC, Guard, owner replacement or context loss during
            // native execution must not be resurrected by its return value.
            if (!inFlight.Contains(request)) return;
            if (clientAccepted) acceptedCount++;

            var afterQueue = QueueTuple.From(snapshot.Queue);
            var changedExactQueue = !request.QueueContinuation && snapshot.Queue.Captured &&
                snapshot.Queue.ActionQueued && afterQueue != QueueTuple.From(request.Snapshot.Queue) &&
                afterQueue.Matches(request);
            if (changedExactQueue && request.Snapshot.Queue.Captured)
            {
                queued = new QueuedLease(request, afterQueue, now);
                lastEvent = "Exact native queue captured; movement remains available";
                return;
            }
            if (!clientAccepted)
            {
                ReleaseRequestLocked(request, "Native request rejected; no retry");
                return;
            }
            if (snapshot.Queue.Captured && snapshot.Queue.ActionQueued &&
                !HasExactCast(snapshot, request))
            {
                ReleaseRequestLocked(request, "Accepted request has no exact new queue or cast proof");
                return;
            }

            accepted = new AcceptedLease(request, now, false);
            lastEvent = "Client accepted; awaiting exact native cast";
            ObserveAcceptedLocked(snapshot, now);
        }
    }

    internal bool IsProtected()
    {
        lock (gate)
            if (inFlight.Count == 0 && accepted is null && queued is null) return false;
        if (!TryRead(out var snapshot, out var now)) return false;
        lock (gate)
        {
            ObserveLocked(snapshot, now);
            return inFlight.Count != 0 || accepted is not null;
        }
    }

    internal bool TryAllowCanonicalEmergencyAction(ActionType actionType, uint actionId, bool pluginOwnedAction)
    {
        if (actionType != ActionType.Action) return false;
        if (actionId == HeldCastCancellationRules.AutomaticPurifyActionId) return true;
        if (actionId == EnemyCombatConstants.GuardActionId && !pluginOwnedAction)
        {
            Reset("Manual Guard override");
            return true;
        }
        return false;
    }

    internal bool ShouldBlockAction(uint resolvedActionId, bool pluginOwnedAction)
    {
        if (TryAllowCanonicalEmergencyAction(ActionType.Action, resolvedActionId, pluginOwnedAction)) return false;
        return IsProtected();
    }

    internal TargetPressureActorIdentity? GetLateFacingTarget(float windowSeconds)
    {
        lock (gate)
            if (accepted is null || accepted.FacingClaimed) return null;
        if (!TryRead(out var snapshot, out var now)) return null;
        lock (gate)
        {
            ObserveLocked(snapshot, now);
            if (accepted is not { Observed: true, FacingClaimed: false } lease ||
                !IsInsideFacingWindow(snapshot, windowSeconds)) return null;
            var target = new TargetPressureActorIdentity(lease.Request.TargetId, lease.Request.TargetEntityId);
            return target.IsValid ? target : null;
        }
    }

    // Called only by the explicit per-frame adapter, never by an input query.
    // The claim is one-shot even if the subsequent native facing call fails.
    internal SamuraiCastProtectionRequest? TryClaimLateFacing(
        TargetPressureActorIdentity currentTarget, float windowSeconds)
    {
        if (!float.IsFinite(windowSeconds) || windowSeconds is < 0.05f or > 0.30f ||
            !currentTarget.IsValid || !TryRead(out var snapshot, out var now)) return null;
        lock (gate)
        {
            ObserveLocked(snapshot, now);
            if (accepted is not { Observed: true, FacingClaimed: false } lease ||
                currentTarget.GameObjectId != lease.Request.TargetId ||
                currentTarget.EntityId != lease.Request.TargetEntityId ||
                !HasExactCast(snapshot, lease.Request) || !IsInsideFacingWindow(snapshot, windowSeconds))
                return null;
            accepted = lease with { FacingClaimed = true };
            lateFacingAttempts++;
            lastEvent = "One late facing attempt claimed for exact cast target";
            return lease.Request;
        }
    }

    internal SamuraiCastProtectionStatus Status
    {
        get
        {
            _ = IsProtected();
            lock (gate)
            {
                var phase = accepted is { Observed: true } ? SamuraiCastProtectionPhase.ExactCast :
                    accepted is not null ? SamuraiCastProtectionPhase.AwaitingCast :
                    inFlight.Count != 0 ? SamuraiCastProtectionPhase.InFlight :
                    queued is not null ? SamuraiCastProtectionPhase.Queued : SamuraiCastProtectionPhase.Idle;
                return new(phase, inFlight.Count, requestedCount, acceptedCount,
                    observedCount, lastEvent, lastReleaseReason) { LateFacingAttempts = lateFacingAttempts };
            }
        }
    }

    internal void Reset(string reason = "Runtime reset")
    {
        lock (gate) ResetLocked(reason);
    }

    private bool TryRead(out SamuraiCastProtectionSnapshot snapshot, out long now)
    {
        try
        {
            now = readClock();
            snapshot = readSnapshot();
            if (now >= 0) return true;
        }
        catch { }
        snapshot = default;
        now = -1;
        Reset("Snapshot or clock unavailable");
        return false;
    }

    private void ObserveLocked(SamuraiCastProtectionSnapshot snapshot, long now)
    {
        var clockReversed = lastObservationAt >= 0 && now < lastObservationAt;
        lastObservationAt = now;
        if (clockReversed)
        {
            ResetLocked("Processing clock reversed");
            return;
        }
        if (ContextFailure(snapshot) is { } contextFailure)
        {
            ResetLocked(contextFailure);
            return;
        }
        foreach (var request in inFlight.ToArray())
        {
            var failure = OwnerFailure(request, snapshot, now, checkGeneration: true);
            if (failure is not null) ReleaseRequestLocked(request, failure);
        }
        if (queued is { } pending)
        {
            // A new arm does not cancel the already-proven native queue.
            // Fresh exact queue observations are continuing proof; only the
            // cleared-queue handoff has a short, non-refreshing deadline.
            var failure = OwnerFailure(pending.Request, snapshot, now,
                checkGeneration: false, boundRequestDuration: false);
            if (failure is null && snapshot.Queue.Captured && snapshot.Queue.ActionQueued &&
                QueueTuple.From(snapshot.Queue) == pending.Queue)
                queued = pending = pending with { LastObservedAt = now };
            if (failure is null && !snapshot.Queue.ActionQueued &&
                now - pending.LastObservedAt >= MaximumQueueHandoffMilliseconds)
                failure = "Exact queue handoff timed out";
            if (failure is null && (!snapshot.Queue.Captured ||
                snapshot.Queue.ActionQueued && QueueTuple.From(snapshot.Queue) != pending.Queue))
                failure = "Exact native queue replaced or unavailable";
            if (failure is not null)
            {
                queued = null;
                RecordReleaseLocked(failure);
            }
        }
        ObserveAcceptedLocked(snapshot, now);
    }

    private void ObserveAcceptedLocked(SamuraiCastProtectionSnapshot snapshot, long now)
    {
        if (accepted is not { } lease) return;
        // A fresh macro arm does not revoke an already accepted cast. Native
        // action/target identity, not the next tap, determines its lifetime.
        var failure = OwnerFailure(lease.Request, snapshot, now, checkGeneration: false);
        if (failure is null && (now < lease.AcceptedAt ||
            now - lease.AcceptedAt >= SamuraiOgiCastProtectionRules.MaximumLeaseMilliseconds))
            failure = "Accepted cast lease timed out or clock reversed";
        if (failure is null && HasExactCast(snapshot, lease.Request))
        {
            if (!lease.Observed)
            {
                accepted = lease with { Observed = true };
                observedCount++;
                lastEvent = "Exact native cast observed";
            }
            return;
        }
        if (failure is null && lease.Observed)
            failure = "Exact native cast ended or changed";
        if (failure is null && snapshot.IsCasting &&
            (snapshot.CastActionId != 0 && snapshot.CastActionId != lease.Request.ResolvedActionId &&
             snapshot.AdjustedCastActionId != lease.Request.ResolvedActionId ||
             IsNetworkTarget(snapshot.CastTargetGameObjectId) &&
             snapshot.CastTargetGameObjectId != lease.Request.TargetId))
            failure = "Different native cast action or target";
        if (failure is null && now - lease.AcceptedAt > SamuraiOgiCastProtectionRules.StartPropagationMilliseconds)
            failure = "Exact cast startup was not observed before timeout";
        if (failure is null) return; // Only the bounded accepted startup gap.
        accepted = null;
        RecordReleaseLocked(failure);
    }

    private static string? ContextFailure(SamuraiCastProtectionSnapshot snapshot) =>
        !snapshot.RuntimeEnabled ? "Runtime or PvP context disabled" :
        !snapshot.LocalPlayerAlive || !snapshot.LocalPlayer.IsValid ||
        snapshot.LocalJobId != SamuraiSmartActionCastRules.SamuraiJobId ? "Local player or SAM job unavailable" :
        snapshot.CastBreakingCrowdControl ? "Cast-breaking crowd control" :
        snapshot.GuardActive ? "Native Guard active" : null;

    private static string? OwnerFailure(SamuraiCastProtectionRequest request,
        SamuraiCastProtectionSnapshot snapshot, long now, bool checkGeneration, bool boundRequestDuration = true) =>
        now < request.StartedAt ? "Processing clock reversed" :
        boundRequestDuration && now - request.StartedAt >= SamuraiOgiCastProtectionRules.MaximumLeaseMilliseconds ? "Request ownership timed out" :
        request.Snapshot.LocalPlayer != snapshot.LocalPlayer ||
        request.Snapshot.TerritoryId != snapshot.TerritoryId ? "Exact owner or territory changed" :
        checkGeneration && request.Snapshot.CurrentTapGeneration != snapshot.CurrentTapGeneration ?
            "SAM tap generation replaced" : null;

    private static bool HasExactCast(SamuraiCastProtectionSnapshot snapshot, SamuraiCastProtectionRequest request) =>
        snapshot.IsCasting && snapshot.CastTargetGameObjectId == request.TargetId &&
        (snapshot.CastActionId == request.ResolvedActionId || snapshot.AdjustedCastActionId == request.ResolvedActionId);

    private static bool IsNetworkTarget(ulong id) => id is not 0 and not 0xE0000000 and not ulong.MaxValue;

    private static bool IsInsideFacingWindow(SamuraiCastProtectionSnapshot snapshot, float windowSeconds) =>
        float.IsFinite(windowSeconds) && windowSeconds is >= 0.05f and <= 0.30f &&
        float.IsFinite(snapshot.CurrentCastTime) && float.IsFinite(snapshot.TotalCastTime) &&
        snapshot.CurrentCastTime >= 0 && snapshot.TotalCastTime > 0 &&
        snapshot.CurrentCastTime < snapshot.TotalCastTime &&
        snapshot.TotalCastTime - snapshot.CurrentCastTime <= windowSeconds;

    private void ReleaseRequestLocked(SamuraiCastProtectionRequest request, string reason)
    {
        if (inFlight.Remove(request)) RecordReleaseLocked(reason);
    }

    private void RecordReleaseLocked(string reason)
    {
        lastReleaseReason = reason;
        lastEvent = reason;
    }

    private void ResetLocked(string reason)
    {
        var hadOwnership = inFlight.Count != 0 || accepted is not null || queued is not null;
        inFlight.Clear();
        accepted = null;
        queued = null;
        if (hadOwnership) RecordReleaseLocked(reason);
    }

    private sealed record AcceptedLease(SamuraiCastProtectionRequest Request, long AcceptedAt, bool Observed,
        bool FacingClaimed = false);
    private sealed record QueuedLease(SamuraiCastProtectionRequest Request, QueueTuple Queue, long LastObservedAt);
    private readonly record struct QueueTuple(bool Queued, uint Type, uint ActionId,
        ulong TargetId, uint ExtraParam, uint Mode, uint ComboRouteId)
    {
        internal static QueueTuple From(ClientActionAttemptFingerprint value) => new(value.ActionQueued,
            value.QueuedActionType, value.QueuedActionId, value.QueuedTargetId,
            value.QueuedExtraParam, value.QueueMode, value.QueuedComboRouteId);
        internal bool Matches(SamuraiCastProtectionRequest request) => Queued &&
            Type == (uint)request.ActionType &&
            (ActionId == request.RawActionId || ActionId == request.ResolvedActionId) &&
            TargetId == request.TargetId && ExtraParam == request.ExtraParam && ComboRouteId == request.ComboRouteId;
        internal bool Matches(QueuedHelperQueueInvocation value) =>
            Type == (uint)value.ActionType && ActionId == value.ActionId &&
            TargetId == value.TargetId && ExtraParam == value.ExtraParam && ComboRouteId == value.ComboRouteId;
    }
}
