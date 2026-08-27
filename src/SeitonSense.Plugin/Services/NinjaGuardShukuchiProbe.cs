using System.Numerics;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using SeitonSense.Core;

namespace SeitonSense.Plugin.Services;

internal sealed record NinjaGuardShukuchiProbeSnapshot(
    NinjaGuardShukuchiDecisionKind Decision,
    NinjaGuardShukuchiDecisionReason Reason,
    uint ResolvedActionId,
    int CandidateCount,
    int EnemySlot,
    ulong TargetGameObjectId,
    uint TargetEntityId,
    uint RevalidatedCurrentHp,
    uint RevalidatedMaximumHp,
    bool RevalidatedGuardActive,
    float RevalidatedDistanceYalms,
    NinjaGuardShukuchiPoint Destination,
    bool PressureKnown,
    int TeamTargetCount,
    bool LocallyReady,
    VirtualKey HeldGameplayKey,
    bool InputClaimed,
    HeldCastCancellationRequest? CastCancellationRequest,
    bool UseActionAttempted,
    bool UseActionAccepted,
    bool HardTargetConfirmed,
    long AttemptCount,
    long AcceptedCount,
    long TargetConfirmedCount,
    long RejectedCount,
    long UnknownCount,
    string CandidateResolution,
    string LastEvent)
{
    internal static NinjaGuardShukuchiProbeSnapshot Initial { get; } = new(
        NinjaGuardShukuchiDecisionKind.None,
        NinjaGuardShukuchiDecisionReason.None,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        false,
        0f,
        default,
        false,
        0,
        false,
        VirtualKey.NO_KEY,
        false,
        null,
        false,
        false,
        false,
        0,
        0,
        0,
        0,
        0,
        "Not evaluated",
        "Waiting");
}

/// <summary>
/// Turns one continuous physical-key generation into at most one accepted PvP
/// Shukuchi toward one exact guarded enemy below 20 percent HP. Proven client
/// false may retry only the frozen actor. Hard targeting happens once and only
/// after the location action returned client-accepted.
/// </summary>
internal sealed class NinjaGuardShukuchiProbe
{
    private const ulong DefaultTargetSentinel = 0xE0000000UL;
    private const long MaximumPressureAgeMilliseconds = 250;

    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly ITargetManager targetManager;
    private readonly TargetPressureTracker pressureTracker;
    private readonly NearAssistRedirector nearAssist;
    private readonly IPluginLog log;

    private NinjaGuardShukuchiProbeSnapshot snapshot =
        NinjaGuardShukuchiProbeSnapshot.Initial;
    private NinjaGuardShukuchiHoldState acceptedHold =
        NinjaGuardShukuchiHoldState.Initial;
    private FrozenGuardShukuchiRetry? frozenRetry;
    private VirtualKey terminalHeldKey = VirtualKey.NO_KEY;
    private long frozenIntentEpochToken;
    private long attemptCount;
    private long acceptedCount;
    private long targetConfirmedCount;
    private long rejectedCount;
    private long unknownCount;
    private long nextErrorLogAt;
    private string lastEvent = "Waiting";

    internal NinjaGuardShukuchiProbe(
        IClientState clientState,
        IObjectTable objectTable,
        ITargetManager targetManager,
        TargetPressureTracker pressureTracker,
        NearAssistRedirector nearAssist,
        IPluginLog log)
    {
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.targetManager = targetManager;
        this.pressureTracker = pressureTracker;
        this.nearAssist = nearAssist;
        this.log = log;
    }

    internal NinjaGuardShukuchiProbeSnapshot Snapshot => Volatile.Read(ref snapshot);

    internal NinjaGuardShukuchiProbeSnapshot Observe(
        IPlayerCharacter? localPlayer,
        bool isCrystallineConflict,
        bool configurationEnabled,
        bool metadataVerified,
        bool actionHelpersSuppressedByGuard,
        bool higherPriorityClaimed,
        EmergencyActionInputFrame inputFrame,
        long nowMilliseconds,
        bool hardReset = false)
    {
        if (hardReset)
        {
            acceptedHold = NinjaGuardShukuchiHoldState.Initial;
            frozenRetry = null;
            terminalHeldKey = VirtualKey.NO_KEY;
        }

        if (terminalHeldKey != VirtualKey.NO_KEY &&
            !inputFrame.IsGameplayKeyPhysicallyDown(terminalHeldKey))
        {
            terminalHeldKey = VirtualKey.NO_KEY;
        }

        var localIdentity = HasValidIdentity(localPlayer)
            ? new TargetPressureActorIdentity(localPlayer!.GameObjectId, localPlayer.EntityId)
            : default;
        var localJobId = localPlayer?.ClassJob.IsValid == true
            ? localPlayer.ClassJob.RowId
            : 0;
        var localAliveAndTargetable = IsLivePlayer(localPlayer) && localPlayer!.IsTargetable;
        var featureContextReady = configurationEnabled &&
                                  isCrystallineConflict &&
                                  localAliveAndTargetable &&
                                  localJobId == NinjaGuardShukuchiRules.NinjaJobId &&
                                  metadataVerified &&
                                  !actionHelpersSuppressedByGuard &&
                                  !hardReset;
        var resolvedActionId = 0u;
        var cooldownReady = false;
        var resourcesReady = false;
        var nativeBoundaryReady = false;
        var readinessKnown = featureContextReady &&
                             TryReadActionState(
                                 localPlayer!,
                                 out resolvedActionId,
                                 out cooldownReady,
                                 out resourcesReady,
                                 out nativeBoundaryReady);
        var actionLocallyReady = readinessKnown && cooldownReady && resourcesReady;
        var input = inputFrame.Snapshot;
        var acceptedKey = acceptedHold.OwnsHold
            ? (VirtualKey)acceptedHold.HeldKeyCode
            : VirtualKey.NO_KEY;
        acceptedHold = NinjaGuardShukuchiRules.ObserveAcceptedHold(
            acceptedHold,
            hardReset,
            featureContextReady && input.ProbeSucceeded && !input.IsTextInputActive,
            acceptedHold.OwnsHold &&
            inputFrame.IsGameplayKeyPhysicallyDown(acceptedKey),
            readinessKnown && resolvedActionId == NinjaGuardShukuchiRules.ActionId,
            cooldownReady);
        acceptedKey = acceptedHold.OwnsHold
            ? (VirtualKey)acceptedHold.HeldKeyCode
            : VirtualKey.NO_KEY;
        var hasHeldEpoch = acceptedHold.OwnsHold
            ? acceptedHold.HasAvailableReadyEpoch
            : input.HeldGameplayKeyEligible;

        var shouldResolveCandidates = frozenRetry is null &&
                                      terminalHeldKey == VirtualKey.NO_KEY &&
                                      actionLocallyReady &&
                                      !higherPriorityClaimed &&
                                      !inputFrame.IsConsumed &&
                                      hasHeldEpoch &&
                                      input.ProbeSucceeded &&
                                      !input.IsTextInputActive;
        var candidateResolution = "Not evaluated: no eligible held Shukuchi epoch";
        var candidates = shouldResolveCandidates
            ? ResolveExactCandidates(
                localPlayer!,
                localIdentity,
                nowMilliseconds,
                out candidateResolution)
            : [];
        var decision = NinjaGuardShukuchiRules.Observe(
            new NinjaGuardShukuchiObservation(
                configurationEnabled,
                isCrystallineConflict,
                localJobId,
                localIdentity,
                localAliveAndTargetable,
                metadataVerified,
                actionHelpersSuppressedByGuard,
                higherPriorityClaimed || inputFrame.IsConsumed,
                input.ProbeSucceeded,
                input.IsTextInputActive,
                hasHeldEpoch && terminalHeldKey == VirtualKey.NO_KEY,
                resolvedActionId,
                actionLocallyReady,
                candidates,
                hardReset));

        var inputClaimed = false;
        HeldCastCancellationRequest? castCancellationRequest = null;
        var attempted = false;
        var accepted = false;
        var targetConfirmed = false;
        NinjaGuardShukuchiCandidate? observedCandidate = null;

        if (frozenRetry is { } retry)
        {
            var exactBaseContext = featureContextReady &&
                                   localIdentity == retry.LocalPlayer &&
                                   input.ProbeSucceeded &&
                                   !input.IsTextInputActive &&
                                   inputFrame.IsGameplayKeyPhysicallyDown(retry.HeldKey);
            if (!exactBaseContext)
            {
                SpendFrozenEpisode(retry);
                lastEvent = $"S{retry.Intent.EnemySlot} frozen Guard-Shukuchi cancelled by exact context/key drift";
            }
            else if (!TryReadActionState(
                         localPlayer!,
                         out var finalActionId,
                         out var finalCooldownReady,
                         out var finalResourcesReady,
                         out var finalNativeBoundaryReady) ||
                     finalActionId != retry.Intent.ActionId)
            {
                SpendFrozenEpisode(retry);
                lastEvent = $"S{retry.Intent.EnemySlot} frozen Shukuchi action identity changed";
            }
            else
            {
                var exactCandidate = BuildExactCandidate(
                    localPlayer!,
                    localIdentity,
                    retry.Intent.EnemySlot,
                    retry.Intent.Target,
                    nowMilliseconds,
                    includePressure: false);
                observedCandidate = exactCandidate;
                var exactIntentValid = exactCandidate is { } candidate &&
                                       NinjaGuardShukuchiRules.CanUseExactIntent(
                                           retry.Intent,
                                           candidate,
                                           localIdentity,
                                           finalActionId,
                                           finalCooldownReady && finalResourcesReady);
                if (!exactIntentValid)
                {
                    SpendFrozenEpisode(retry);
                    lastEvent = $"S{retry.Intent.EnemySlot} frozen Guard/HP/range/identity drift; no alternate";
                }
                else if (!higherPriorityClaimed && !inputFrame.IsConsumed)
                {
                    inputClaimed = true;
                    inputFrame.Consume();
                    if (!finalNativeBoundaryReady)
                    {
                        castCancellationRequest = CreateCastCancellationRequest(
                            localPlayer!,
                            retry);
                        lastEvent = $"S{retry.Intent.EnemySlot} frozen Guard-Shukuchi waiting for global native boundary";
                    }
                    else if (!HeldActionRetryRules.CanAttemptFrozenIntent(
                                 retry.Retry,
                                 nowMilliseconds))
                    {
                        lastEvent = $"S{retry.Intent.EnemySlot} frozen Guard-Shukuchi retaining retry throttle priority";
                    }
                    else
                    {
                        var outcome = TryUseShukuchiOnce(
                            localPlayer!,
                            retry.Intent,
                            out attempted,
                            out var boundaryCandidate,
                            out targetConfirmed);
                        observedCandidate = boundaryCandidate ?? observedCandidate;
                        accepted = outcome == ClientActionAttemptOutcome.ClientAccepted;
                        CompleteAttempt(retry, outcome, nowMilliseconds);
                        lastEvent = DescribeAttempt(
                            retry.Intent,
                            retry.Retry.NativeAttemptCount + 1,
                            retry.Retry,
                            outcome,
                            targetConfirmed);
                    }
                }
            }
        }
        else if (terminalHeldKey == VirtualKey.NO_KEY &&
                 decision.ShouldDispatch &&
                 decision.Intent is { } intent)
        {
            var fromAcceptedHold = acceptedHold.OwnsHold;
            var readyEpochToken = fromAcceptedHold
                ? acceptedHold.CurrentReadyEpochToken
                : 0UL;
            var heldKey = fromAcceptedHold
                ? (VirtualKey)acceptedHold.HeldKeyCode
                : input.HeldGameplayKey;
            if (fromAcceptedHold &&
                !NinjaGuardShukuchiRules.TrySpendReadyEpoch(
                    acceptedHold,
                    readyEpochToken,
                    out acceptedHold))
            {
                lastEvent = "Accepted hold had no spendable Shukuchi cooldown epoch";
                goto Publish;
            }

            var newRetry = new FrozenGuardShukuchiRetry(
                intent,
                localIdentity,
                heldKey,
                NextIntentEpochToken(),
                fromAcceptedHold,
                readyEpochToken,
                HeldActionRetryState.Initial);
            inputClaimed = true;
            inputFrame.Consume();
            if (!nativeBoundaryReady)
            {
                frozenRetry = newRetry;
                castCancellationRequest = CreateCastCancellationRequest(localPlayer!, newRetry);
                lastEvent = $"S{intent.EnemySlot} Guard-Shukuchi frozen before cast/native boundary";
            }
            else
            {
                var outcome = TryUseShukuchiOnce(
                    localPlayer!,
                    intent,
                    out attempted,
                    out var boundaryCandidate,
                    out targetConfirmed);
                observedCandidate = boundaryCandidate;
                accepted = outcome == ClientActionAttemptOutcome.ClientAccepted;
                CompleteAttempt(newRetry, outcome, nowMilliseconds);
                if (outcome == ClientActionAttemptOutcome.SoftUnavailable)
                    castCancellationRequest = CreateCastCancellationRequest(localPlayer!, newRetry);
                lastEvent = DescribeAttempt(
                    intent,
                    1,
                    newRetry.Retry,
                    outcome,
                    targetConfirmed);
            }
        }

    Publish:
        if (attempted) Interlocked.Increment(ref attemptCount);
        if (accepted) Interlocked.Increment(ref acceptedCount);
        if (targetConfirmed) Interlocked.Increment(ref targetConfirmedCount);

        var selectedCandidate = observedCandidate ??
                                (decision.SelectedCandidateIndex >= 0 &&
                                 decision.SelectedCandidateIndex < candidates.Count
                                    ? candidates[decision.SelectedCandidateIndex]
                                    : (NinjaGuardShukuchiCandidate?)null);
        var revalidatedFrozenCandidate = candidates.Count == 0 &&
                                         observedCandidate is not null;
        var publishedDecision = revalidatedFrozenCandidate && (inputClaimed || attempted)
            ? NinjaGuardShukuchiDecisionKind.Dispatch
            : decision.Kind;
        var publishedReason = publishedDecision == NinjaGuardShukuchiDecisionKind.Dispatch
            ? NinjaGuardShukuchiDecisionReason.None
            : decision.Reason;
        var publishedCandidateCount = revalidatedFrozenCandidate ? 1 : candidates.Count;
        var publishedCandidateResolution = revalidatedFrozenCandidate
            ? "Frozen exact actor revalidated; no rerank"
            : candidateResolution;
        var result = new NinjaGuardShukuchiProbeSnapshot(
            publishedDecision,
            publishedReason,
            resolvedActionId,
            publishedCandidateCount,
            selectedCandidate?.EnemySlot ?? 0,
            selectedCandidate?.Actor.GameObjectId ?? 0,
            selectedCandidate?.Actor.EntityId ?? 0,
            selectedCandidate?.CurrentHp ?? 0,
            selectedCandidate?.MaximumHp ?? 0,
            selectedCandidate?.GuardActive ?? false,
            selectedCandidate is { } exact
                ? Distance(localPlayer, exact.Position)
                : 0f,
            selectedCandidate?.Position ?? default,
            selectedCandidate?.PressureKnown ?? false,
            selectedCandidate?.TeamTargetCount ?? 0,
            actionLocallyReady,
            frozenRetry?.HeldKey ??
            (terminalHeldKey != VirtualKey.NO_KEY ? terminalHeldKey : input.HeldGameplayKey),
            inputClaimed,
            castCancellationRequest,
            attempted,
            accepted,
            targetConfirmed,
            Interlocked.Read(ref attemptCount),
            Interlocked.Read(ref acceptedCount),
            Interlocked.Read(ref targetConfirmedCount),
            Interlocked.Read(ref rejectedCount),
            Interlocked.Read(ref unknownCount),
            publishedCandidateResolution,
            lastEvent);
        Volatile.Write(ref snapshot, result);
        return result;
    }

    internal void Reset()
    {
        acceptedHold = NinjaGuardShukuchiHoldState.Initial;
        frozenRetry = null;
        terminalHeldKey = VirtualKey.NO_KEY;
        lastEvent = "Reset";
        Volatile.Write(ref snapshot, NinjaGuardShukuchiProbeSnapshot.Initial with
        {
            AttemptCount = Interlocked.Read(ref attemptCount),
            AcceptedCount = Interlocked.Read(ref acceptedCount),
            TargetConfirmedCount = Interlocked.Read(ref targetConfirmedCount),
            RejectedCount = Interlocked.Read(ref rejectedCount),
            UnknownCount = Interlocked.Read(ref unknownCount),
            LastEvent = lastEvent,
        });
    }

    internal NinjaGuardShukuchiProbeSnapshot FailClosed()
    {
        var failedKey = frozenRetry?.HeldKey ?? terminalHeldKey;
        acceptedHold = NinjaGuardShukuchiHoldState.Initial;
        frozenRetry = null;
        terminalHeldKey = failedKey;
        lastEvent = "Failed closed";
        var result = NinjaGuardShukuchiProbeSnapshot.Initial with
        {
            Decision = NinjaGuardShukuchiDecisionKind.Cancelled,
            Reason = NinjaGuardShukuchiDecisionReason.HardReset,
            AttemptCount = Interlocked.Read(ref attemptCount),
            AcceptedCount = Interlocked.Read(ref acceptedCount),
            TargetConfirmedCount = Interlocked.Read(ref targetConfirmedCount),
            RejectedCount = Interlocked.Read(ref rejectedCount),
            UnknownCount = Interlocked.Read(ref unknownCount),
            LastEvent = lastEvent,
        };
        Volatile.Write(ref snapshot, result);
        return result;
    }

    private IReadOnlyList<NinjaGuardShukuchiCandidate> ResolveExactCandidates(
        IPlayerCharacter localPlayer,
        TargetPressureActorIdentity localIdentity,
        long nowMilliseconds,
        out string resolution)
    {
        var candidates = new List<NinjaGuardShukuchiCandidate>(EnemySlotRules.LastSlot);
        for (var slot = EnemySlotRules.FirstSlot; slot <= EnemySlotRules.LastSlot; slot++)
        {
            var target = EnemySlotResolver.Resolve(objectTable, slot);
            if (!HasValidIdentity(target)) continue;
            var identity = new TargetPressureActorIdentity(target!.GameObjectId, target.EntityId);
            var candidate = BuildExactCandidate(
                localPlayer,
                localIdentity,
                slot,
                identity,
                nowMilliseconds,
                includePressure: true);
            if (candidate is { } exact) candidates.Add(exact);
        }

        resolution = candidates.Count == 0
            ? "No exact canonical enemy slot resolved"
            : $"Exact independently resolved slots: {candidates.Count}";
        return candidates;
    }

    private NinjaGuardShukuchiCandidate? BuildExactCandidate(
        IPlayerCharacter localPlayer,
        TargetPressureActorIdentity localIdentity,
        int enemySlot,
        TargetPressureActorIdentity expectedTarget,
        long nowMilliseconds,
        bool includePressure)
    {
        if (!HasValidIdentity(localPlayer) ||
            !localIdentity.IsValid ||
            !EnemySlotRules.IsValidSlot(enemySlot) ||
            !expectedTarget.IsValid)
        {
            return null;
        }

        var target = EnemySlotResolver.Resolve(objectTable, enemySlot);
        if (!HasValidIdentity(target) ||
            target!.GameObjectId != expectedTarget.GameObjectId ||
            target.EntityId != expectedTarget.EntityId)
        {
            return null;
        }

        var tableTarget = objectTable.SearchByEntityId(target.EntityId) as IPlayerCharacter;
        var exactIdentity = HasValidIdentity(tableTarget) &&
                            tableTarget!.Address == target.Address &&
                            tableTarget.GameObjectId == target.GameObjectId &&
                            tableTarget.EntityId == target.EntityId;
        if (!exactIdentity) return null;

        var targetPosition = target.Position;
        var position = new NinjaGuardShukuchiPoint(
            targetPosition.X,
            targetPosition.Y,
            targetPosition.Z);
        var localPosition = localPlayer.Position;
        var origin = new NinjaGuardShukuchiPoint(
            localPosition.X,
            localPosition.Y,
            localPosition.Z);
        var teamTargetCount = 0;
        var pressureKnown = includePressure &&
                            pressureTracker.TryGetFreshTeamTargetCount(
                                localIdentity,
                                expectedTarget,
                                nowMilliseconds,
                                MaximumPressureAgeMilliseconds,
                                out teamTargetCount);
        if (!pressureKnown) teamTargetCount = 0;

        return new NinjaGuardShukuchiCandidate(
            enemySlot,
            expectedTarget,
            exactIdentity,
            IsLivePlayer(target),
            target.IsTargetable,
            target.CurrentHp,
            target.MaxHp,
            HasLiveGuard(target),
            position,
            NinjaGuardShukuchiRules.IsWithinNativeRange(origin, position),
            pressureKnown,
            teamTargetCount);
    }

    private unsafe ClientActionAttemptOutcome TryUseShukuchiOnce(
        IPlayerCharacter localPlayer,
        NinjaGuardShukuchiIntent intent,
        out bool attempted,
        out NinjaGuardShukuchiCandidate? boundaryCandidate,
        out bool hardTargetConfirmed)
    {
        attempted = false;
        boundaryCandidate = null;
        hardTargetConfirmed = false;
        if (!HasValidIdentity(localPlayer) || !intent.IsValid)
            return ClientActionAttemptOutcome.NotInvoked;

        var actionManager = ActionManager.Instance();
        if (actionManager == null) return ClientActionAttemptOutcome.NotInvoked;

        var attemptedAtBoundary = false;
        var softUnavailableAtBoundary = false;
        NinjaGuardShukuchiCandidate? candidateAtBoundary = null;
        var before = default(ClientActionAttemptFingerprint);
        var after = default(ClientActionAttemptFingerprint);
        try
        {
            var clientReturnedTrue = nearAssist.RunWithoutRedirect(() =>
            {
                if (!TryReadActionState(
                        localPlayer,
                        out var actionId,
                        out var cooldownReady,
                        out var resourcesReady,
                        out var nativeBoundaryReady) ||
                    actionId != intent.ActionId)
                {
                    return false;
                }

                if (!cooldownReady || !resourcesReady)
                    return false;
                if (!nativeBoundaryReady)
                {
                    softUnavailableAtBoundary = true;
                    return false;
                }

                var localIdentity = new TargetPressureActorIdentity(
                    localPlayer.GameObjectId,
                    localPlayer.EntityId);
                var candidate = BuildExactCandidate(
                    localPlayer,
                    localIdentity,
                    intent.EnemySlot,
                    intent.Target,
                    Environment.TickCount64,
                    includePressure: false);
                candidateAtBoundary = candidate;
                if (candidate is not { } exact ||
                    !NinjaGuardShukuchiRules.CanUseExactIntent(
                        intent,
                        exact,
                        localIdentity,
                        actionId,
                        actionLocallyReady: true))
                {
                    return false;
                }

                var destination = new Vector3(
                    exact.Position.X,
                    exact.Position.Y,
                    exact.Position.Z);
                if (IsOwnGuardActiveOrPropagating(localPlayer))
                    return false;
                before = ClientActionAttemptBoundary.Capture(actionManager, intent.ActionId);
                attemptedAtBoundary = true;
                var accepted = actionManager->UseActionLocation(
                    ActionType.Action,
                    intent.ActionId,
                    DefaultTargetSentinel,
                    &destination,
                    0,
                    0);
                after = ClientActionAttemptBoundary.Capture(actionManager, intent.ActionId);
                return accepted;
            });

            var outcome = attemptedAtBoundary
                ? ClientActionAttemptBoundaryRules.Classify(
                    clientReturnedTrue,
                    intent.ActionId,
                    before,
                    after)
                : softUnavailableAtBoundary
                    ? ClientActionAttemptOutcome.SoftUnavailable
                    : ClientActionAttemptOutcome.NotInvoked;
            if (outcome == ClientActionAttemptOutcome.ClientAccepted)
                hardTargetConfirmed = TrySetExactHardTargetOnce(intent);
            return outcome;
        }
        catch (Exception exception)
        {
            LogFailure(exception, "Guard-Shukuchi native boundary ended ambiguously");
            return attemptedAtBoundary
                ? ClientActionAttemptOutcome.AcceptanceUnknown
                : softUnavailableAtBoundary
                    ? ClientActionAttemptOutcome.SoftUnavailable
                    : ClientActionAttemptOutcome.NotInvoked;
        }
        finally
        {
            attempted = attemptedAtBoundary;
            boundaryCandidate = candidateAtBoundary;
        }
    }

    private bool TrySetExactHardTargetOnce(NinjaGuardShukuchiIntent intent)
    {
        try
        {
            var target = EnemySlotResolver.Resolve(objectTable, intent.EnemySlot);
            if (!HasValidIdentity(target) ||
                target!.GameObjectId != intent.Target.GameObjectId ||
                target.EntityId != intent.Target.EntityId ||
                !IsLivePlayer(target) ||
                !target.IsTargetable)
            {
                return false;
            }

            var tableTarget = objectTable.SearchByEntityId(target.EntityId) as IPlayerCharacter;
            if (!HasValidIdentity(tableTarget) ||
                tableTarget!.Address != target.Address ||
                tableTarget.GameObjectId != target.GameObjectId ||
                tableTarget.EntityId != target.EntityId)
            {
                return false;
            }

            targetManager.Target = target;
            return MatchesExactTarget(targetManager.Target, target);
        }
        catch (Exception exception)
        {
            LogFailure(exception, "accepted Guard-Shukuchi hard-target setter failed terminally");
            return false;
        }
    }

    private static unsafe bool TryReadActionState(
        IPlayerCharacter localPlayer,
        out uint resolvedActionId,
        out bool cooldownReady,
        out bool resourcesReady,
        out bool nativeBoundaryReady)
    {
        resolvedActionId = 0;
        cooldownReady = false;
        resourcesReady = false;
        nativeBoundaryReady = false;
        if (!HasValidIdentity(localPlayer)) return false;

        var actionManager = ActionManager.Instance();
        if (actionManager == null) return false;
        resolvedActionId = actionManager->GetAdjustedActionId(
            NinjaGuardShukuchiRules.ActionId);
        if (resolvedActionId != NinjaGuardShukuchiRules.ActionId) return true;

        cooldownReady = actionManager->IsActionOffCooldown(
            ActionType.Action,
            resolvedActionId);
        resourcesReady = actionManager->CheckActionResources(
            ActionType.Action,
            resolvedActionId) == 0;
        nativeBoundaryReady = HeldActionRetryRules.IsNativeBoundaryNearQueueable(
            actionManager->AnimationLock,
            localPlayer.IsCasting,
            actionManager->CastActionId,
            actionManager->ActionQueued);
        return true;
    }

    private static unsafe bool IsCastCancellationBoundaryReady(IPlayerCharacter localPlayer)
    {
        var actionManager = ActionManager.Instance();
        return actionManager != null &&
               localPlayer.IsCasting &&
               actionManager->CastActionId != 0 &&
               !actionManager->ActionQueued &&
               float.IsFinite(actionManager->AnimationLock) &&
               actionManager->AnimationLock >= 0f &&
               actionManager->AnimationLock <=
               HeldCastCancellationRules.MaximumCancellationAnimationLockSeconds;
    }

    private static HeldCastCancellationRequest? CreateCastCancellationRequest(
        IPlayerCharacter localPlayer,
        FrozenGuardShukuchiRetry frozen)
    {
        if (!IsCastCancellationBoundaryReady(localPlayer)) return null;

        var request = new HeldCastCancellationRequest(
            HeldCastCancellationHelperKind.NinjaGuardShukuchi,
            frozen.Intent.ActionId,
            frozen.LocalPlayer,
            frozen.Intent.Target,
            (int)frozen.HeldKey,
            frozen.IntentEpochToken);
        return request.IsValid ? request : null;
    }

    private void CompleteAttempt(
        FrozenGuardShukuchiRetry frozen,
        ClientActionAttemptOutcome outcome,
        long nowMilliseconds)
    {
        var completion = HeldActionRetryRules.Complete(
            frozen.Retry,
            Math.Max(0, nowMilliseconds),
            outcome);
        if (completion.RetryScheduled ||
            completion.Disposition == HeldActionRetryDisposition.SoftWait)
        {
            frozenRetry = frozen with { Retry = completion.NextState };
            return;
        }

        if (outcome == ClientActionAttemptOutcome.ClientRejected)
            Interlocked.Increment(ref rejectedCount);
        else if (outcome == ClientActionAttemptOutcome.AcceptanceUnknown)
            Interlocked.Increment(ref unknownCount);

        frozenRetry = null;
        if (outcome == ClientActionAttemptOutcome.ClientAccepted)
        {
            if (!frozen.FromAcceptedHold)
            {
                acceptedHold = NinjaGuardShukuchiRules.BeginAcceptedHold(
                    (int)frozen.HeldKey);
            }

            return;
        }

        SpendFrozenEpisode(frozen);
    }

    private void SpendFrozenEpisode(FrozenGuardShukuchiRetry frozen)
    {
        frozenRetry = null;
        if (!frozen.FromAcceptedHold)
            terminalHeldKey = frozen.HeldKey;
    }

    private ulong NextIntentEpochToken()
    {
        while (true)
        {
            var current = Volatile.Read(ref frozenIntentEpochToken);
            var next = current >= long.MaxValue ? 1 : current + 1;
            if (Interlocked.CompareExchange(
                    ref frozenIntentEpochToken,
                    next,
                    current) == current)
            {
                return (ulong)next;
            }
        }
    }

    private static string DescribeAttempt(
        NinjaGuardShukuchiIntent intent,
        int attempt,
        HeldActionRetryState retryState,
        ClientActionAttemptOutcome outcome,
        bool hardTargetConfirmed) =>
        $"S{intent.EnemySlot} Guard-Shukuchi attempt {attempt}/" +
        $"{HeldActionRetryRules.ResolveAttemptLimit(retryState)}: {outcome}; target={hardTargetConfirmed}";

    private static bool HasLiveGuard(IPlayerCharacter target)
    {
        foreach (var status in target.StatusList)
        {
            if (NinjaGuardShukuchiRules.IsExactGuardStatus(status.StatusId) &&
                float.IsFinite(status.RemainingTime) &&
                status.RemainingTime > 0f)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsOwnGuardActiveOrPropagating(IPlayerCharacter localPlayer)
    {
        try
        {
            if (!HasValidIdentity(localPlayer) ||
                DefensiveUtilityProbe.HasActiveGuard(localPlayer))
            {
                return true;
            }

            return nearAssist.TryGetRecentExactLocalGuardAttempt(
                clientState.TerritoryType,
                localPlayer.GameObjectId,
                localPlayer.EntityId,
                Environment.TickCount64,
                DefensiveUtilityRules.GuardPropagationLatchMilliseconds,
                out _);
        }
        catch
        {
            // Uncertain own-Guard evidence must never enable an automatic jump.
            return true;
        }
    }

    private static float Distance(
        IPlayerCharacter? localPlayer,
        NinjaGuardShukuchiPoint target)
    {
        if (!HasValidIdentity(localPlayer) || !target.IsFinite) return 0f;
        var local = localPlayer!.Position;
        var distance = Vector3.Distance(
            new Vector3(local.X, local.Y, local.Z),
            new Vector3(target.X, target.Y, target.Z));
        return float.IsFinite(distance) ? distance : 0f;
    }

    private static bool IsLivePlayer(IPlayerCharacter? player) =>
        player is not null &&
        !player.IsDead &&
        player.CurrentHp > 0 &&
        player.MaxHp >= player.CurrentHp;

    private static bool HasValidIdentity(IGameObject? gameObject) =>
        gameObject is not null &&
        gameObject.Address != nint.Zero &&
        gameObject.IsValid() &&
        gameObject.GameObjectId is not 0 and not DefaultTargetSentinel and not ulong.MaxValue &&
        gameObject.EntityId is not 0 and not (uint)DefaultTargetSentinel and not uint.MaxValue;

    private static bool MatchesExactTarget(IGameObject? observed, IGameObject expected) =>
        HasValidIdentity(observed) &&
        HasValidIdentity(expected) &&
        observed!.Address == expected.Address &&
        observed.GameObjectId == expected.GameObjectId &&
        observed.EntityId == expected.EntityId;

    private void LogFailure(Exception exception, string message)
    {
        var now = Environment.TickCount64;
        if (now < nextErrorLogAt) return;
        nextErrorLogAt = now + 10_000;
        log.Error(exception, $"Seiton Sense NIN {message}.");
    }

    private readonly record struct FrozenGuardShukuchiRetry(
        NinjaGuardShukuchiIntent Intent,
        TargetPressureActorIdentity LocalPlayer,
        VirtualKey HeldKey,
        ulong IntentEpochToken,
        bool FromAcceptedHold,
        ulong ReadyEpochToken,
        HeldActionRetryState Retry);
}
