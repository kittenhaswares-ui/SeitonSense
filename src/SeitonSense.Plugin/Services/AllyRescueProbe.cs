using System.Numerics;
using Dalamud.Game;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Lumina.Excel.Sheets;
using SeitonSense.Core;
using GameAction = Lumina.Excel.Sheets.Action;

namespace SeitonSense.Plugin.Services;

internal sealed record AllyRescueProbeSnapshot(
    AllyRescueBufferPhase Phase,
    AllyRescueIntent? Intent,
    AllyRescueBufferDecisionKind Decision,
    AllyRescueBufferCancelReason CancelReason,
    AllyRescueInputTrigger InputTrigger,
    long BufferRemainingMilliseconds,
    int CandidateCount,
    uint ActionId,
    ulong TargetGameObjectId,
    uint TargetStatusId,
    VirtualKey FreshGameplayKey,
    VirtualKey HeldGameplayKey,
    bool LocallyReady,
    bool UseActionAttempted,
    bool UseActionAccepted,
    long AttemptCount,
    long AcceptedCount,
    AllyRescueConfirmationPopup? ConfirmationPopup,
    AllyRescueConfirmationStatistics MatchConfirmations,
    AllyRescueConfirmationStatistics SessionConfirmations,
    bool ConfirmationPending,
    long ConfirmationCaptureCount,
    long ConfirmationDropCount,
    string LastEvent)
{
    internal bool InputClaimed { get; init; }
    internal HeldCastCancellationRequest? CastCancellationRequest { get; init; }

    internal static AllyRescueProbeSnapshot Initial { get; } = new(
        AllyRescueBufferPhase.WaitingForCandidate,
        null,
        AllyRescueBufferDecisionKind.None,
        AllyRescueBufferCancelReason.None,
        AllyRescueInputTrigger.None,
        0,
        0,
        0,
        0,
        0,
        VirtualKey.NO_KEY,
        VirtualKey.NO_KEY,
        false,
        false,
        false,
        0,
        0,
        null,
        AllyRescueConfirmationStatistics.Empty,
        AllyRescueConfirmationStatistics.Empty,
        false,
        0,
        0,
        "Not started");
}

/// <summary>
/// Optional CC-only next-key rescue for an exact non-self party member. It uses
/// Warden's Paean on BRD and Aquaveil on WHM, and recognizes only PvP Stun,
/// Silence, Miracle of Nature, and Deep Freeze.
/// </summary>
internal sealed class AllyRescueProbe
{
    internal const uint WardensPaeanActionId = 29400;
    internal const uint AquaveilActionId = 29227;
    internal const uint WardensPaeanIconId = 9628;
    internal const uint AquaveilIconId = 9607;

    private const uint BardJobId = 23;
    private const uint WhiteMageJobId = 24;
    private const int ExpectedRange = 30;
    private const ushort WardensPaeanRecast100ms = 240;
    private const ushort AquaveilRecast100ms = 180;
    private const long StatusRefreshToleranceMilliseconds = 250;

    private readonly IObjectTable objectTable;
    private readonly TargetPressureTracker pressureTracker;
    private readonly NearAssistRedirector nearAssist;
    private readonly MachinistLimitBreakCapture actionEffectCapture;
    private readonly IPluginLog log;
    private readonly bool wardensPaeanMetadataVerified;
    private readonly bool aquaveilMetadataVerified;
    private readonly Dictionary<ObservedAllyStatusKey, AllyStatusIdentityState> statusInstances = [];
    private readonly HashSet<AllyActorIdentity> trustedMpActors = [];
    private AllyRescueBufferState state = AllyRescueBufferState.Initial;
    private AllyRescueConfirmationState confirmationState = AllyRescueConfirmationState.Initial;
    private AllyRescueProbeSnapshot snapshot = AllyRescueProbeSnapshot.Initial;
    private ulong nextInstanceToken = 1;
    private long attemptCount;
    private long acceptedCount;
    private long nextErrorLogAt;
    private int resetConfirmationStatisticsRequested;

    internal AllyRescueProbe(
        IObjectTable objectTable,
        IDataManager dataManager,
        TargetPressureTracker pressureTracker,
        NearAssistRedirector nearAssist,
        MachinistLimitBreakCapture actionEffectCapture,
        IPluginLog log)
    {
        this.objectTable = objectTable;
        this.pressureTracker = pressureTracker;
        this.nearAssist = nearAssist;
        this.actionEffectCapture = actionEffectCapture;
        this.log = log;

        wardensPaeanMetadataVerified = ValidateRescueActionMetadata(
            dataManager,
            WardensPaeanActionId,
            "The Warden's Paean",
            WardensPaeanIconId,
            BardJobId,
            WardensPaeanRecast100ms,
            "Removes");
        aquaveilMetadataVerified = ValidateRescueActionMetadata(
            dataManager,
            AquaveilActionId,
            "Aquaveil",
            AquaveilIconId,
            WhiteMageJobId,
            AquaveilRecast100ms,
            "Nullifies");

        if (!wardensPaeanMetadataVerified || !aquaveilMetadataVerified)
        {
            log.Warning(
                "Seiton Sense Ally Rescue metadata validation failed: Paean={Paean}, Aquaveil={Aquaveil}. " +
                "The mismatched job action will remain unavailable.",
                wardensPaeanMetadataVerified,
                aquaveilMetadataVerified);
        }
    }

    internal AllyRescueProbeSnapshot Snapshot => Volatile.Read(ref snapshot);

    internal unsafe AllyRescueProbeSnapshot Observe(
        IPlayerCharacter? localPlayer,
        bool isCrystallineConflict,
        bool configurationEnabled,
        bool allowHeldKeyAtCandidateEntry,
        EmergencyActionInputFrame inputFrame,
        long nowMilliseconds,
        long bufferMilliseconds,
        bool hardReset = false,
        bool dispatchAllowed = true)
    {
        if (hardReset) ResetRuntime(nowMilliseconds);

        if (Interlocked.Exchange(ref resetConfirmationStatisticsRequested, 0) != 0)
        {
            confirmationState = AllyRescueConfirmationRules.ResetStatistics(confirmationState);
            Interlocked.Exchange(ref attemptCount, 0);
            Interlocked.Exchange(ref acceptedCount, 0);
        }

        var localAlive = IsLivePlayer(localPlayer);
        var localIdentityValid = localAlive && HasValidNativeIdentity(localPlayer!);
        var actionId = localIdentityValid ? ResolveActionId(localPlayer!) : 0;
        var structurallyReady = actionId != 0 &&
                                HasStructuralActionReadiness(actionId);
        var globallyQueueReady = structurallyReady &&
                                 HasGlobalQueueReadiness(localPlayer, actionId);
        var locallyReady = configurationEnabled &&
                           dispatchAllowed &&
                           isCrystallineConflict &&
                           localIdentityValid &&
                           globallyQueueReady;

        actionEffectCapture.SetAllyRescueLocalEntityId(
            configurationEnabled &&
            isCrystallineConflict &&
            localIdentityValid &&
            actionId != 0
                ? localPlayer!.EntityId
                : 0);
        DrainConfirmedCleanses();
        var confirmationNow = Math.Max(nowMilliseconds, Environment.TickCount64);
        confirmationState = AllyRescueConfirmationRules.ObserveTime(
            confirmationState,
            confirmationNow);
        var candidates = configurationEnabled &&
                         isCrystallineConflict &&
                         localIdentityValid &&
                         actionId != 0
            ? BuildCandidates(localPlayer!, actionId, nowMilliseconds)
            : [];

        var input = inputFrame.Snapshot;
        var rescueFreshKey = input.ProbeSucceeded && input.FreshGameplayKeyPressed
            ? input.FreshGameplayKey
            : VirtualKey.NO_KEY;
        var rescueHeldKey = input.ProbeSucceeded && input.HeldGameplayKeyEligible
            ? input.HeldGameplayKey
            : VirtualKey.NO_KEY;
        var trackedKeyPhysicallyDown =
            IsExactVirtualKeyToken(state.GameplayKeyToken) &&
            inputFrame.IsGameplayKeyPhysicallyDown((VirtualKey)state.GameplayKeyToken);
        var decision = AllyRescueBufferRules.Observe(
            state,
            new AllyRescueBufferObservation(
                configurationEnabled,
                isCrystallineConflict,
                localAlive,
                localIdentityValid,
                input.IsTextInputActive,
                candidates,
                rescueFreshKey != VirtualKey.NO_KEY,
                rescueHeldKey != VirtualKey.NO_KEY,
                allowHeldKeyAtCandidateEntry,
                locallyReady,
                nowMilliseconds,
                hardReset,
                bufferMilliseconds,
                FreshGameplayKeyToken: rescueFreshKey == VirtualKey.NO_KEY
                    ? 0
                    : (int)rescueFreshKey,
                HeldGameplayKeyToken: rescueHeldKey == VirtualKey.NO_KEY
                    ? 0
                    : (int)rescueHeldKey,
                TrackedGameplayKeyPhysicallyDown: trackedKeyPhysicallyDown,
                DispatchAllowed: dispatchAllowed));

        // Commit and claim this framework frame before validation/native dispatch.
        // A rejected native request retains only this exact actor/status/key lease.
        state = decision.NextState;
        var exactLeaseCanProgress = state.TrackedIntent is { } claimedIntent &&
                                    candidates.Any(candidate =>
                                        candidate.Intent == claimedIntent &&
                                        AllyRescueSelectionRules.IsEligible(candidate));
        var inputClaimed = dispatchAllowed &&
                           !input.IsTextInputActive &&
                           structurallyReady &&
                           exactLeaseCanProgress &&
                           state.Phase == AllyRescueBufferPhase.Buffered &&
                           IsExactVirtualKeyToken(state.GameplayKeyToken) &&
                           inputFrame.IsGameplayKeyPhysicallyDown(
                               (VirtualKey)state.GameplayKeyToken);
        if (inputClaimed) inputFrame.Consume();

        var castCancellationRequest = BuildCastCancellationRequest(
            localPlayer,
            actionId,
            configurationEnabled,
            isCrystallineConflict,
            input.IsTextInputActive,
            inputClaimed,
            structurallyReady,
            nowMilliseconds,
            inputFrame);

        var attempted = false;
        var accepted = false;
        var targetGameObjectId = 0UL;
        var targetStatusId = 0U;
        var lastEvent = DescribeDecision(decision, actionId, candidates.Count);
        if (!decision.ShouldDispatch &&
            state.Phase == AllyRescueBufferPhase.Buffered &&
            exactLeaseCanProgress &&
            globallyQueueReady &&
            state.NextNativeAttemptAtMilliseconds > nowMilliseconds)
        {
            lastEvent =
                $"Proven-false retry throttle: {state.NativeAttemptCount}/{AllyRescueBufferRules.MaximumNativeAttempts}";
        }
        else if (!decision.ShouldDispatch &&
                 state.Phase == AllyRescueBufferPhase.Buffered &&
                 exactLeaseCanProgress &&
                 structurallyReady &&
                 !globallyQueueReady)
        {
            lastEvent = "Soft wait: global animation/cast/action queue busy";
        }
        else if (!decision.ShouldDispatch &&
                 state.Phase == AllyRescueBufferPhase.Buffered &&
                 exactLeaseCanProgress &&
                 !structurallyReady)
        {
            lastEvent = "Background wait: action cooldown/resources unavailable";
        }
        else if (!decision.ShouldDispatch &&
                 state.Phase == AllyRescueBufferPhase.Buffered &&
                 !exactLeaseCanProgress)
        {
            lastEvent = "Background wait: exact target/status/range unavailable";
        }
        if (decision.ShouldDispatch &&
            decision.DispatchIntent is { } dispatchIntent)
        {
            targetGameObjectId = dispatchIntent.GameObjectId;
            targetStatusId = dispatchIntent.Status.StatusId;
            if (TryRevalidateCandidate(
                    localPlayer!,
                    actionId,
                    dispatchIntent,
                    nowMilliseconds,
                    out var revalidated))
            {
                var outcome = ClientActionAttemptOutcome.NotInvoked;
                ushort expectedSourceSequence = 0;
                try
                {
                    outcome = TryUseRescueOnce(
                        localPlayer!,
                        actionId,
                        revalidated.GameObjectId,
                        out attempted,
                        out expectedSourceSequence);
                    accepted = outcome == ClientActionAttemptOutcome.ClientAccepted;
                    if (attempted) Interlocked.Increment(ref attemptCount);
                    if (accepted) Interlocked.Increment(ref acceptedCount);
                    lastEvent = outcome switch
                    {
                        ClientActionAttemptOutcome.ClientAccepted when expectedSourceSequence == 0 =>
                            $"Accepted action={actionId} target={revalidated.GameObjectId:X} status={targetStatusId}; confirmation unavailable: source sequence did not advance",
                        ClientActionAttemptOutcome.ClientAccepted =>
                            $"Accepted action={actionId} target={revalidated.GameObjectId:X} status={targetStatusId}",
                        ClientActionAttemptOutcome.ClientRejected =>
                            $"Attempt rejected action={actionId} target={revalidated.GameObjectId:X} status={targetStatusId}",
                        ClientActionAttemptOutcome.AcceptanceUnknown =>
                            $"Attempt ambiguous action={actionId} target={revalidated.GameObjectId:X} status={targetStatusId}",
                        ClientActionAttemptOutcome.SoftUnavailable =>
                            $"Soft wait action={actionId} target={revalidated.GameObjectId:X} status={targetStatusId}",
                        _ =>
                            $"Attempt cancelled before native boundary action={actionId} target={revalidated.GameObjectId:X} status={targetStatusId}",
                    };
                }
                catch (Exception exception)
                {
                    outcome = attempted
                        ? ClientActionAttemptOutcome.AcceptanceUnknown
                        : ClientActionAttemptOutcome.NotInvoked;
                    if (attempted) Interlocked.Increment(ref attemptCount);
                    lastEvent = $"Attempt threw action={actionId} target={revalidated.GameObjectId:X} status={targetStatusId}";
                    LogAttemptFailure(exception, nowMilliseconds);
                }

                var completedAt = Math.Max(nowMilliseconds, Environment.TickCount64);
                var completion = AllyRescueBufferRules.CompleteNativeAttempt(
                    state,
                    dispatchIntent,
                    completedAt,
                    outcome);
                state = completion.NextState;
                if (completion.Outcome == AllyRescueNativeAttemptOutcome.RetryScheduled)
                {
                    lastEvent +=
                        $"; retry {state.NativeAttemptCount + 1}/{AllyRescueBufferRules.MaximumNativeAttempts} " +
                        $"after {AllyRescueBufferRules.NativeRetryThrottleMilliseconds} ms";
                }

                if (attempted && accepted && expectedSourceSequence != 0)
                {
                    var attemptedAt = Math.Max(confirmationNow, Environment.TickCount64);
                    confirmationState = AllyRescueConfirmationRules.RegisterAttempt(
                        confirmationState,
                        new AllyRescuePendingAttempt(
                            localPlayer!.EntityId,
                            actionId,
                            revalidated.GameObjectId,
                            revalidated.EntityId,
                            dispatchIntent,
                            accepted,
                            attemptedAt,
                            expectedSourceSequence),
                        attemptedAt).NextState;
                }

                if (attempted)
                {
                    log.Information(
                        "Seiton Sense Ally Rescue attempt: action={ActionId} target={TargetEntityId:X8} " +
                        "status={StatusId} outcome={Outcome} sourceSequence={SourceSequence}",
                        actionId,
                        revalidated.EntityId,
                        targetStatusId,
                        outcome,
                        expectedSourceSequence);
                }
            }
            else
            {
                state = AllyRescueBufferRules.CancelNativeAttempt(
                    state,
                    dispatchIntent,
                    Math.Max(nowMilliseconds, Environment.TickCount64));
                lastEvent =
                    $"Cancelled without action: target/status/range changed for {dispatchIntent.GameObjectId:X}/{targetStatusId}";
            }
        }

        const long remaining = AllyRescueBufferRules.StatusBoundBufferMilliseconds;
        var result = new AllyRescueProbeSnapshot(
            state.Phase,
            state.TrackedIntent,
            decision.Kind,
            decision.CancelReason,
            decision.InputTrigger,
            remaining,
            candidates.Count,
            actionId,
            targetGameObjectId,
            targetStatusId,
            rescueFreshKey,
            rescueHeldKey,
            locallyReady,
            attempted,
            accepted,
            Interlocked.Read(ref attemptCount),
            Interlocked.Read(ref acceptedCount),
            confirmationState.Popup,
            confirmationState.MatchStatistics,
            confirmationState.SessionStatistics,
            confirmationState.Pending is not null,
            actionEffectCapture.CapturedAllyRescueCleanses,
            actionEffectCapture.DroppedAllyRescueCleanses,
            lastEvent)
        {
            InputClaimed = inputClaimed,
            CastCancellationRequest = castCancellationRequest,
        };
        Volatile.Write(ref snapshot, result);
        return result;
    }

    internal void Reset()
    {
        var now = Environment.TickCount64;
        ResetRuntime(now);
        Volatile.Write(ref snapshot, SnapshotAfterReset("Reset"));
    }

    internal void RequestStatisticsReset() =>
        Interlocked.Exchange(ref resetConfirmationStatisticsRequested, 1);

    internal AllyRescueProbeSnapshot FailClosed(long nowMilliseconds, Exception? exception = null)
    {
        ResetRuntime(nowMilliseconds);
        if (exception is not null) LogAttemptFailure(exception, nowMilliseconds);
        var failed = AllyRescueProbeSnapshot.Initial with
        {
            Decision = AllyRescueBufferDecisionKind.Cancelled,
            CancelReason = AllyRescueBufferCancelReason.HardReset,
            AttemptCount = Interlocked.Read(ref attemptCount),
            AcceptedCount = Interlocked.Read(ref acceptedCount),
            MatchConfirmations = confirmationState.MatchStatistics,
            SessionConfirmations = confirmationState.SessionStatistics,
            ConfirmationCaptureCount = actionEffectCapture.CapturedAllyRescueCleanses,
            ConfirmationDropCount = actionEffectCapture.DroppedAllyRescueCleanses,
            LastEvent = "Failed closed",
        };
        Volatile.Write(ref snapshot, failed);
        return failed;
    }

    private void DrainConfirmedCleanses()
    {
        while (actionEffectCapture.TryDequeueAllyRescueCleanse(out var cleanse))
        {
            // The native hook may enqueue between two framework samples. Use a
            // monotonic presentation/correlation timestamp so a packet captured
            // just before the previous snapshot was published cannot look like
            // a backwards clock on the next drain.
            var observedAt = Math.Max(
                cleanse.ObservedAtMilliseconds,
                confirmationState.LastObservedAtMilliseconds);
            var decision = AllyRescueConfirmationRules.ObserveActionEffect(
                confirmationState,
                new AllyRescueActionEffectObservation(
                    cleanse.CasterEntityId,
                    cleanse.ActionId,
                    cleanse.TargetEntityId,
                    AllyRescueConfirmationRules.RecoveredFromStatusEffectType,
                    checked((ushort)cleanse.RemovedStatusId),
                    cleanse.GlobalSequence,
                    cleanse.SourceSequence,
                    observedAt));
            confirmationState = decision.NextState;
            if (decision.Confirmed)
            {
                log.Information(
                    "Seiton Sense Ally Rescue confirmed: action={ActionId} target={TargetEntityId:X8} " +
                    "status={StatusId} sourceSequence={SourceSequence}",
                    cleanse.ActionId,
                    cleanse.TargetEntityId,
                    cleanse.RemovedStatusId,
                    cleanse.SourceSequence);
            }
        }
    }

    private unsafe List<AllyRescueSelectionCandidate> BuildCandidates(
        IPlayerCharacter localPlayer,
        uint actionId,
        long nowMilliseconds)
    {
        var exactParty = ResolveExactPartyMembers();
        var observedKeys = new HashSet<ObservedAllyStatusKey>();
        var candidates = new List<AllyRescueSelectionCandidate>(8);
        foreach (var (slot, ally) in exactParty)
        {
            if (ally.GameObjectId == localPlayer.GameObjectId || ally.EntityId == localPlayer.EntityId)
                continue;

            foreach (var status in ally.StatusList)
            {
                if (!AllyRescueStatusRules.IsTriggerStatus(status.StatusId) ||
                    !float.IsFinite(status.RemainingTime) ||
                    status.RemainingTime <= 0f)
                {
                    continue;
                }

                var key = new ObservedAllyStatusKey(
                    ally.GameObjectId,
                    ally.EntityId,
                    status.StatusId,
                    status.SourceId);
                observedKeys.Add(key);
                var remainingMilliseconds = Math.Max(
                    1,
                    (long)Math.Round(Math.Min(status.RemainingTime, 3_600f) * 1000f));
                var token = ObserveStatusInstance(key, remainingMilliseconds, nowMilliseconds);
                candidates.Add(BuildCandidate(
                    localPlayer,
                    ally,
                    slot,
                    new AllyRescueStatusInstance(status.StatusId, token),
                    actionId));
            }
        }

        PruneStatusInstances(observedKeys, nowMilliseconds);
        PruneTrustedMpActors(exactParty.Select(static item => item.Player));
        return candidates;
    }

    private unsafe AllyRescueSelectionCandidate BuildCandidate(
        IPlayerCharacter localPlayer,
        IPlayerCharacter ally,
        int partySlot,
        AllyRescueStatusInstance status,
        uint actionId)
    {
        var sourceObject = GetNativeObject(localPlayer);
        var targetObject = GetNativeObject(ally);
        var validActionTarget = sourceObject != null && targetObject != null;
        var rangeResult = validActionTarget
            ? ActionManager.GetActionInRangeOrLoS(actionId, sourceObject, targetObject)
            : uint.MaxValue;
        var plausibleMp = ally.MaxMp > 0 && ally.CurrentMp <= ally.MaxMp;
        var actorIdentity = new AllyActorIdentity(ally.GameObjectId, ally.EntityId);
        if (plausibleMp && ally.CurrentMp > 0) trustedMpActors.Add(actorIdentity);
        var hasTrustedMp = plausibleMp &&
                           (ally.CurrentMp > 0 || trustedMpActors.Contains(actorIdentity));

        return new AllyRescueSelectionCandidate(
            ally.GameObjectId,
            ally.EntityId,
            partySlot,
            status,
            ally.CurrentHp,
            ally.MaxHp,
            CountDirectIncomingPressure(ally),
            ally.CurrentMp,
            ally.MaxMp,
            hasTrustedMp,
            Vector3.DistanceSquared(localPlayer.Position, ally.Position),
            IsExactPartyMember: true,
            IsSelf: false,
            IsAlive: !ally.IsDead && ally.CurrentHp > 0,
            ally.IsTargetable,
            validActionTarget,
            SeitonRangeRules.HasNativeRangeAndLineOfSight(rangeResult));
    }

    private unsafe bool TryRevalidateCandidate(
        IPlayerCharacter localPlayer,
        uint actionId,
        AllyRescueIntent intent,
        long nowMilliseconds,
        out AllyRescueSelectionCandidate candidate)
    {
        candidate = default;
        if (!IsLivePlayer(localPlayer) ||
            ResolveActionId(localPlayer) != actionId)
        {
            return false;
        }

        foreach (var (slot, ally) in ResolveExactPartyMembers())
        {
            if (ally.GameObjectId != intent.GameObjectId ||
                ally.EntityId != intent.EntityId ||
                ally.GameObjectId == localPlayer.GameObjectId ||
                ally.EntityId == localPlayer.EntityId)
            {
                continue;
            }

            foreach (var status in ally.StatusList)
            {
                if (status.StatusId != intent.Status.StatusId ||
                    !float.IsFinite(status.RemainingTime) ||
                    status.RemainingTime <= 0f)
                {
                    continue;
                }

                var key = new ObservedAllyStatusKey(
                    ally.GameObjectId,
                    ally.EntityId,
                    status.StatusId,
                    status.SourceId);
                if (!statusInstances.TryGetValue(key, out var identity) ||
                    identity.Token != intent.Status.InstanceToken ||
                    nowMilliseconds < identity.LastSeenAtMilliseconds)
                {
                    continue;
                }

                candidate = BuildCandidate(localPlayer, ally, slot, intent.Status, actionId);
                return AllyRescueSelectionRules.IsEligible(candidate) &&
                       candidate.Intent == intent;
            }
        }

        return false;
    }

    private IReadOnlyList<(int Slot, IPlayerCharacter Player)> ResolveExactPartyMembers()
    {
        var resolved = new List<(int Slot, IPlayerCharacter Player)>(8);
        for (var slot = AllyRescueSelectionRules.FirstPartySlot;
             slot <= AllyRescueSelectionRules.LastPartySlot;
             slot++)
        {
            var player = PartySlotResolver.Resolve(objectTable, slot);
            if (HasValidNativeIdentity(player)) resolved.Add((slot, player!));
        }

        // An actor exposed through more than one native slot is ambiguous. Exclude
        // every duplicate rather than choosing whichever slot happened to scan first.
        var duplicateGameIds = resolved
            .GroupBy(static item => item.Player.GameObjectId)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToHashSet();
        var duplicateEntityIds = resolved
            .GroupBy(static item => item.Player.EntityId)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToHashSet();
        return resolved
            .Where(item =>
                !duplicateGameIds.Contains(item.Player.GameObjectId) &&
                !duplicateEntityIds.Contains(item.Player.EntityId))
            .ToArray();
    }

    private int? CountDirectIncomingPressure(IPlayerCharacter ally) =>
        pressureTracker.TryGetIncomingAllyPressure(
            ally.GameObjectId,
            ally.EntityId,
            out var uniqueEnemyCount)
            ? uniqueEnemyCount
            : null;

    private unsafe ClientActionAttemptOutcome TryUseRescueOnce(
        IPlayerCharacter localPlayer,
        uint actionId,
        ulong targetGameObjectId,
        out bool attempted,
        out ushort expectedSourceSequence)
    {
        attempted = false;
        expectedSourceSequence = 0;
        if (actionId is not (WardensPaeanActionId or AquaveilActionId) ||
            targetGameObjectId is 0 or 0xE0000000)
        {
            return ClientActionAttemptOutcome.NotInvoked;
        }

        var actionManager = ActionManager.Instance();
        if (actionManager == null ||
            !HasStructuralActionReadiness(actionId) ||
            !HasGlobalQueueReadiness(localPlayer, actionId))
        {
            return actionManager == null
                ? ClientActionAttemptOutcome.NotInvoked
                : ClientActionAttemptOutcome.SoftUnavailable;
        }

        var boundaryBefore = ClientActionAttemptBoundary.Capture(actionManager, actionId);
        attempted = true;
        var accepted = nearAssist.RunWithoutRedirect(() =>
            actionManager->UseAction(
                ActionType.Action,
                actionId,
                targetGameObjectId,
                0,
                ActionManager.UseActionMode.None,
                0));
        var boundaryAfter = ClientActionAttemptBoundary.Capture(actionManager, actionId);
        if (accepted &&
            boundaryAfter.LastUsedActionSequence != 0 &&
            boundaryAfter.LastUsedActionSequence != boundaryBefore.LastUsedActionSequence)
        {
            expectedSourceSequence = boundaryAfter.LastUsedActionSequence;
        }

        return ClientActionAttemptBoundaryRules.Classify(
            accepted,
            actionId,
            boundaryBefore,
            boundaryAfter);
    }

    private unsafe HeldCastCancellationRequest? BuildCastCancellationRequest(
        IPlayerCharacter? localPlayer,
        uint actionId,
        bool configurationEnabled,
        bool isCrystallineConflict,
        bool textInputActive,
        bool inputClaimed,
        bool actionStructurallyReady,
        long nowMilliseconds,
        EmergencyActionInputFrame inputFrame)
    {
        if (!configurationEnabled ||
            !isCrystallineConflict ||
            textInputActive ||
            !inputClaimed ||
            !actionStructurallyReady ||
            state.Phase != AllyRescueBufferPhase.Buffered ||
            state.TrackedIntent is not { IsValid: true } intent ||
            !IsExactVirtualKeyToken(state.GameplayKeyToken) ||
            !inputFrame.IsGameplayKeyPhysicallyDown(
                (VirtualKey)state.GameplayKeyToken) ||
            !HasValidNativeIdentity(localPlayer) ||
            !TryRevalidateCandidate(
                localPlayer!,
                actionId,
                intent,
                nowMilliseconds,
                out var exactTarget))
        {
            return null;
        }

        var actionManager = ActionManager.Instance();
        if (actionManager == null ||
            !localPlayer!.IsCasting ||
            actionManager->CastActionId == 0 ||
            actionManager->ActionQueued ||
            !float.IsFinite(actionManager->AnimationLock) ||
            actionManager->AnimationLock < 0f ||
            actionManager->AnimationLock >
            HeldCastCancellationRules.MaximumCancellationAnimationLockSeconds)
        {
            return null;
        }

        var localIdentity = new TargetPressureActorIdentity(
            localPlayer.GameObjectId,
            localPlayer.EntityId);
        var targetIdentity = new TargetPressureActorIdentity(
            exactTarget.GameObjectId,
            exactTarget.EntityId);
        if (!localIdentity.IsValid || !targetIdentity.IsValid) return null;

        return new HeldCastCancellationRequest(
            HeldCastCancellationHelperKind.AllyRescue,
            actionId,
            localIdentity,
            targetIdentity,
            state.GameplayKeyToken,
            intent.Status.InstanceToken);
    }

    private static unsafe bool HasStructuralActionReadiness(uint actionId)
    {
        if (actionId is not (WardensPaeanActionId or AquaveilActionId))
            return false;

        var actionManager = ActionManager.Instance();
        return actionManager != null &&
               actionManager->GetAdjustedActionId(actionId) == actionId &&
               actionManager->IsActionOffCooldown(ActionType.Action, actionId) &&
               actionManager->CheckActionResources(ActionType.Action, actionId) == 0;
    }

    private static unsafe bool HasGlobalQueueReadiness(
        IPlayerCharacter? localPlayer,
        uint actionId)
    {
        if (!HasValidNativeIdentity(localPlayer) ||
            actionId is not (WardensPaeanActionId or AquaveilActionId))
        {
            return false;
        }

        var actionManager = ActionManager.Instance();
        if (actionManager == null ||
            actionManager->ActionQueued ||
            localPlayer!.IsCasting ||
            actionManager->CastActionId != 0)
        {
            return false;
        }

        return HeldActionRetryRules.IsNativeBoundaryNearQueueable(
            actionManager->AnimationLock,
            localPlayer!.IsCasting,
            actionManager->CastActionId,
            actionManager->ActionQueued);
    }

    private static bool IsExactVirtualKeyToken(int token)
    {
        if (token <= 0) return false;
        var key = (VirtualKey)token;
        return key != VirtualKey.NO_KEY && Enum.IsDefined(typeof(VirtualKey), key);
    }

    private ulong ObserveStatusInstance(
        ObservedAllyStatusKey key,
        long remainingMilliseconds,
        long nowMilliseconds)
    {
        if (!statusInstances.TryGetValue(key, out var current) ||
            remainingMilliseconds > current.RemainingMilliseconds +
            StatusRefreshToleranceMilliseconds)
        {
            current = new AllyStatusIdentityState(
                NextInstanceToken(),
                remainingMilliseconds,
                nowMilliseconds);
        }
        else
        {
            current = current with
            {
                RemainingMilliseconds = remainingMilliseconds,
                LastSeenAtMilliseconds = nowMilliseconds,
            };
        }

        statusInstances[key] = current;
        return current.Token;
    }

    private void PruneStatusInstances(
        IReadOnlySet<ObservedAllyStatusKey> observed,
        long nowMilliseconds)
    {
        foreach (var stale in statusInstances
                     .Where(pair =>
                         !observed.Contains(pair.Key) &&
                         (nowMilliseconds < pair.Value.LastSeenAtMilliseconds ||
                          nowMilliseconds - pair.Value.LastSeenAtMilliseconds >=
                          PersonalDebuffAlertRules.MissingGraceMilliseconds))
                     .Select(static pair => pair.Key)
                     .ToArray())
        {
            statusInstances.Remove(stale);
        }
    }

    private void PruneTrustedMpActors(IEnumerable<IPlayerCharacter> exactParty)
    {
        var live = exactParty
            .Select(static player => new AllyActorIdentity(player.GameObjectId, player.EntityId))
            .ToHashSet();
        trustedMpActors.RemoveWhere(identity => !live.Contains(identity));
    }

    private uint ResolveActionId(IPlayerCharacter localPlayer)
    {
        if (!localPlayer.ClassJob.IsValid) return 0;
        return localPlayer.ClassJob.RowId switch
        {
            BardJobId when wardensPaeanMetadataVerified => WardensPaeanActionId,
            WhiteMageJobId when aquaveilMetadataVerified => AquaveilActionId,
            _ => 0,
        };
    }

    private bool ValidateRescueActionMetadata(
        IDataManager dataManager,
        uint actionId,
        string expectedName,
        uint expectedIconId,
        uint expectedJobId,
        ushort expectedRecast100ms,
        string expectedCleanseVerb)
    {
        try
        {
            var actions = dataManager.GetExcelSheet<GameAction>(ClientLanguage.English);
            var descriptions = dataManager.GetExcelSheet<ActionTransient>(ClientLanguage.English);
            return actions.TryGetRow(actionId, out var action) &&
                   descriptions.TryGetRow(actionId, out var transient) &&
                   IsExpectedFriendlyRescueAction(
                       action,
                       transient,
                       expectedName,
                       expectedIconId,
                       expectedJobId,
                       expectedRecast100ms,
                       expectedCleanseVerb);
        }
        catch (Exception exception)
        {
            log.Warning(
                exception,
                "Seiton Sense Ally Rescue metadata lookup failed closed for action {ActionId} ({ActionName}).",
                actionId,
                expectedName);
            return false;
        }
    }

    private static bool IsExpectedFriendlyRescueAction(
        GameAction action,
        ActionTransient transient,
        string expectedName,
        uint expectedIconId,
        uint expectedJobId,
        ushort expectedRecast100ms,
        string expectedCleanseVerb)
    {
        var description = transient.Description.ToString();
        return string.Equals(
                   action.Name.ToString(),
                   expectedName,
                   StringComparison.OrdinalIgnoreCase) &&
        action.Icon == expectedIconId &&
        action.IsPvP &&
        action.IsPlayerAction &&
        action.ClassJob.IsValid &&
        action.ClassJob.RowId == expectedJobId &&
        action.Range == ExpectedRange &&
        action.EffectRange == 0 &&
        action.Cast100ms == 0 &&
        action.Recast100ms == expectedRecast100ms &&
        action.CanTargetSelf &&
        action.CanTargetParty &&
        !action.CanTargetAlly &&
        !action.CanTargetAlliance &&
        !action.CanTargetHostile &&
        !action.TargetArea &&
        action.RequiresLineOfSight &&
        description.Contains(expectedCleanseVerb, StringComparison.OrdinalIgnoreCase) &&
        description.Contains("status affliction", StringComparison.OrdinalIgnoreCase) &&
        description.Contains("Purify", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLivePlayer(IPlayerCharacter? player) =>
        HasValidNativeIdentity(player) &&
        player!.IsTargetable &&
        !player.IsDead &&
        player.CurrentHp > 0 &&
        player.MaxHp >= player.CurrentHp;

    private static unsafe bool HasValidNativeIdentity(IPlayerCharacter? player)
    {
        if (player is null ||
            player.Address == 0 ||
            !AllyRescueSelectionRules.IsValidEntityId(player.EntityId) ||
            !TargetHighlightRules.IsValidGameObjectId(player.GameObjectId))
        {
            return false;
        }

        var native = (GameObject*)player.Address;
        return native != null && native->EntityId == player.EntityId;
    }

    private static unsafe GameObject* GetNativeObject(IPlayerCharacter player)
    {
        var native = (GameObject*)player.Address;
        return native != null && native->EntityId == player.EntityId ? native : null;
    }

    private static string DescribeDecision(
        AllyRescueBufferDecision decision,
        uint actionId,
        int candidateCount) =>
        decision.Kind == AllyRescueBufferDecisionKind.Cancelled
            ? $"{decision.Kind}/{decision.CancelReason}, action={actionId}, candidates={candidateCount}"
            : $"{decision.Kind}, action={actionId}, candidates={candidateCount}";

    private ulong NextInstanceToken()
    {
        var token = nextInstanceToken++;
        if (token != 0) return token;
        token = nextInstanceToken++;
        return token == 0 ? 1 : token;
    }

    private void ResetRuntime(long nowMilliseconds)
    {
        state = AllyRescueBufferState.Initial;
        statusInstances.Clear();
        trustedMpActors.Clear();
        actionEffectCapture.SetAllyRescueLocalEntityId(0);
        confirmationState = AllyRescueConfirmationRules.ObserveTime(
            confirmationState,
            Math.Max(0, nowMilliseconds),
            hardReset: true);
    }

    private AllyRescueProbeSnapshot SnapshotAfterReset(string lastEvent) =>
        AllyRescueProbeSnapshot.Initial with
        {
            AttemptCount = Interlocked.Read(ref attemptCount),
            AcceptedCount = Interlocked.Read(ref acceptedCount),
            MatchConfirmations = confirmationState.MatchStatistics,
            SessionConfirmations = confirmationState.SessionStatistics,
            ConfirmationCaptureCount = actionEffectCapture.CapturedAllyRescueCleanses,
            ConfirmationDropCount = actionEffectCapture.DroppedAllyRescueCleanses,
            LastEvent = lastEvent,
        };

    private void LogAttemptFailure(Exception exception, long nowMilliseconds)
    {
        if (nowMilliseconds < nextErrorLogAt) return;
        nextErrorLogAt = nowMilliseconds + 10_000;
        log.Error(exception, "Seiton Sense Ally Rescue attempt failed and will not be retried.");
    }

    private readonly record struct ObservedAllyStatusKey(
        ulong GameObjectId,
        uint EntityId,
        uint StatusId,
        uint SourceId);

    private readonly record struct AllyStatusIdentityState(
        ulong Token,
        long RemainingMilliseconds,
        long LastSeenAtMilliseconds);

    private readonly record struct AllyActorIdentity(ulong GameObjectId, uint EntityId);
}
