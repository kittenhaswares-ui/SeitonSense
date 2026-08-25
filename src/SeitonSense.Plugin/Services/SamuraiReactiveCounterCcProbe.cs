using System.Numerics;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using SeitonSense.Core;

namespace SeitonSense.Plugin.Services;

internal sealed record SamuraiReactiveCounterCcProbeSnapshot(
    string CounterPhase,
    SamuraiReactiveProtectionKind ProtectionKind,
    int EnemySlot,
    ulong TargetGameObjectId,
    uint TargetEntityId,
    uint TargetJobId,
    VirtualKey ReservedKey,
    bool ProtectionObserved,
    bool InputClaimed,
    uint LastAttemptedActionId,
    ClientActionAttemptOutcome LastAttemptOutcome,
    long SotenAttemptCount,
    long MineuchiAttemptCount,
    long ZantetsukenAttemptCount,
    long AcceptedCount,
    int ProtectionSignalQueueDepth,
    long CapturedProtectionSignalCount,
    long DroppedProtectionSignalCount,
    string ZantetsukenPhase,
    string LastEvent)
{
    internal HeldCastCancellationRequest? CastCancellationRequest { get; init; }

    internal static SamuraiReactiveCounterCcProbeSnapshot Initial { get; } = new(
        "Waiting",
        SamuraiReactiveProtectionKind.None,
        0,
        0,
        0,
        0,
        VirtualKey.NO_KEY,
        false,
        false,
        0,
        ClientActionAttemptOutcome.NotInvoked,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        "Waiting",
        "Not started");
}

/// <summary>
/// Isolated SAM action runtime. An existing shared ActionEffect capture feeds
/// exact self-target Purify/Guard records through EnqueueProtectionSignal. The
/// probe then requires live Resilience/Guard membership before it can remember
/// the episode, and authoritative status absence before Soten or Mineuchi.
/// Zantetsuken is a separate held lane with an exact own-source Kuzushi and
/// zero ShieldPercentage requirement at the final native boundary.
/// </summary>
internal sealed class SamuraiReactiveCounterCcProbe
{
    private const int MaximumQueuedSignals = 64;
    private const int MaximumRememberedSignals = 128;

    private readonly object signalGate = new();
    private readonly IObjectTable objectTable;
    private readonly ExecuteTracker executeTracker;
    private readonly NearAssistRedirector nearAssist;
    private readonly IPluginLog log;
    private readonly SamuraiReactiveMetadataValidation metadata;
    private readonly Queue<SamuraiReactiveProtectionSignal> pendingSignals = [];
    private readonly HashSet<ProtectionSignalIdentity> rememberedSignals = [];
    private readonly Queue<ProtectionSignalIdentity> rememberedSignalOrder = [];

    private FrozenProtectionEpisode? protectionEpisode;
    private SamuraiReactiveCounterCcState counterState =
        SamuraiReactiveCounterCcState.Initial;
    private FrozenActionTarget? zantetsukenTarget;
    private SamuraiZantetsukenState zantetsukenState =
        SamuraiZantetsukenState.Initial;
    private int zantetsukenSpentKeyToken;
    private long sotenArrivalDeadlineMilliseconds = -1;
    private long capturedSignalCount;
    private long droppedSignalCount;
    private long sotenAttemptCount;
    private long mineuchiAttemptCount;
    private long zantetsukenAttemptCount;
    private long acceptedCount;
    private bool inputClaimedThisFrame;
    private HeldCastCancellationRequest? castCancellationRequestThisFrame;
    private uint lastAttemptedActionId;
    private ClientActionAttemptOutcome lastAttemptOutcome =
        ClientActionAttemptOutcome.NotInvoked;
    private string lastEvent = "Not started";
    private long nextErrorLogAt;
    private SamuraiReactiveCounterCcProbeSnapshot snapshot =
        SamuraiReactiveCounterCcProbeSnapshot.Initial;

    internal SamuraiReactiveCounterCcProbe(
        IObjectTable objectTable,
        ExecuteTracker executeTracker,
        NearAssistRedirector nearAssist,
        IPluginLog log,
        SamuraiReactiveMetadataValidation metadata)
    {
        this.objectTable = objectTable;
        this.executeTracker = executeTracker;
        this.nearAssist = nearAssist;
        this.log = log;
        this.metadata = metadata;
    }

    internal SamuraiReactiveCounterCcProbeSnapshot Snapshot =>
        Volatile.Read(ref snapshot);

    /// <summary>
    /// Thread-safe mirror feed for the existing shared ActionEffect hook. Only
    /// exact enemy self-target Purify/Guard action records with a network
    /// sequence are accepted; target resolution remains a framework-thread job.
    /// </summary>
    internal bool EnqueueProtectionSignal(SamuraiReactiveProtectionSignal signal)
    {
        if (!signal.IsValid) return false;
        var identity = new ProtectionSignalIdentity(
            signal.CasterEntityId,
            signal.ActionId,
            signal.GlobalSequence,
            signal.SourceSequence);
        lock (signalGate)
        {
            if (!rememberedSignals.Add(identity)) return false;
            rememberedSignalOrder.Enqueue(identity);
            while (rememberedSignalOrder.Count > MaximumRememberedSignals)
                rememberedSignals.Remove(rememberedSignalOrder.Dequeue());

            while (pendingSignals.Count >= MaximumQueuedSignals)
            {
                pendingSignals.Dequeue();
                droppedSignalCount++;
            }

            pendingSignals.Enqueue(signal);
            capturedSignalCount++;
            return true;
        }
    }

    internal unsafe SamuraiReactiveCounterCcProbeSnapshot ObserveCounterCc(
        IPlayerCharacter? localPlayer,
        SupportedPvPContext context,
        bool enabled,
        bool enablePostPurify,
        bool enablePostGuard,
        bool allowHeldGameplayKey,
        bool dispatchAllowed,
        float configuredSotenMaximumRangeYalms,
        EmergencyActionInputFrame inputFrame,
        long nowMilliseconds,
        bool hardReset = false)
    {
        inputClaimedThisFrame = false;
        castCancellationRequestThisFrame = null;
        try
        {
            var localValid = IsValidLocalSamurai(localPlayer);
            var contextValid = context is SupportedPvPContext.CrystallineConflict or
                SupportedPvPContext.WolvesDen;
            if (hardReset || !enabled ||
                (!enablePostPurify && !enablePostGuard) ||
                !metadata.CounterCcVerified ||
                !contextValid || !localValid)
            {
                ResetCounter(
                    clearSignals: hardReset ||
                        !enabled ||
                        !metadata.CounterCcVerified ||
                        !contextValid ||
                        !localValid);
                lastEvent = !metadata.CounterCcVerified
                    ? "SAM counter-CC metadata is not verified"
                    : "SAM counter-CC inactive";
                return PublishSnapshot();
            }

            if (protectionEpisode is { } configuredEpisode &&
                !IsProtectionKindEnabled(
                    configuredEpisode.Signal.Kind,
                    enablePostPurify,
                    enablePostGuard))
            {
                lastEvent = $"{configuredEpisode.Signal.Kind} trigger was disabled";
                ResetCounter(clearSignals: false, preserveLastEvent: true);
            }

            if (protectionEpisode is null)
            {
                TryFreezeNextProtectionEpisode(
                    localPlayer!,
                    context,
                    enablePostPurify,
                    enablePostGuard,
                    allowHeldGameplayKey,
                    inputFrame,
                    nowMilliseconds);
            }

            if (protectionEpisode is not { } episode)
                return PublishSnapshot();

            var target = ResolveFrozenTarget(localPlayer!, episode.Target);
            if (target is null ||
                !SamuraiReactiveRuntimeRules.IsInsideLease(
                    episode.Signal.ObservedAtMilliseconds,
                    nowMilliseconds,
                    SamuraiReactiveRuntimeRules.EpisodeLeaseMilliseconds(
                        episode.Signal.Kind)))
            {
                lastEvent = target is null
                    ? "Frozen protection actor drifted; episode cancelled"
                    : "Frozen protection episode expired";
                ResetCounter(clearSignals: false);
                return PublishSnapshot();
            }

            var protectionCount = CountExpectedProtectionStatuses(
                target,
                episode.Signal.Kind);
            if (protectionCount > 1)
            {
                lastEvent = "Ambiguous duplicate protection statuses; episode cancelled";
                ResetCounter(clearSignals: false);
                return PublishSnapshot();
            }

            if (!episode.ProtectionObserved)
            {
                if (protectionCount == 0)
                {
                    if (!SamuraiReactiveRuntimeRules.IsInsideLease(
                            episode.Signal.ObservedAtMilliseconds,
                            nowMilliseconds,
                            SamuraiReactiveRuntimeRules
                                .SignalStatusObservationLeaseMilliseconds))
                    {
                        lastEvent = "Protection signal never gained its exact status";
                        ResetCounter(clearSignals: false);
                    }

                    return PublishSnapshot();
                }

                var keyToken = episode.GameplayKeyToken;
                if (keyToken <= 0 && TryResolveEligibleGameplayKey(
                        inputFrame,
                        allowHeldGameplayKey,
                        out var observedKey))
                {
                    keyToken = (int)observedKey;
                }

                episode = episode with
                {
                    ProtectionObserved = true,
                    GameplayKeyToken = keyToken,
                };
                protectionEpisode = episode;
                if (keyToken > 0)
                {
                    counterState = SamuraiReactiveCounterCcRules.Arm(
                        episode.Target.Identity,
                        keyToken,
                        nowMilliseconds,
                        episode.Target.AllowJoblessWolvesDenTarget);
                }

                lastEvent = $"Exact {episode.Signal.Kind} status observed";
            }

            if (!counterState.IsActive)
            {
                if (protectionCount == 0)
                {
                    lastEvent = "Protection ended without held-key consent";
                    ResetCounter(clearSignals: false);
                    return PublishSnapshot();
                }

                if (TryResolveEligibleGameplayKey(
                        inputFrame,
                        allowHeldGameplayKey,
                        out var lateKey))
                {
                    episode = episode with { GameplayKeyToken = (int)lateKey };
                    protectionEpisode = episode;
                    counterState = SamuraiReactiveCounterCcRules.Arm(
                        episode.Target.Identity,
                        (int)lateKey,
                        nowMilliseconds,
                        episode.Target.AllowJoblessWolvesDenTarget);
                }

                if (!counterState.IsActive) return PublishSnapshot();
            }

            var exactKeyStillDown = IsExactGameplayKeyStillDown(
                inputFrame,
                counterState.GameplayKeyToken);
            if (counterState.Phase == SamuraiReactiveCounterCcPhase.ApproachAccepted &&
                sotenArrivalDeadlineMilliseconds >= 0 &&
                nowMilliseconds > sotenArrivalDeadlineMilliseconds)
            {
                lastEvent = "Accepted Soten did not reach the exact actor in time";
                ResetCounter(clearSignals: false);
                return PublishSnapshot();
            }

            var distanceKnown = TryGetEdgeDistance(localPlayer!, target, out var distance);
            var sotenReady = IsActionSpecificReady(
                SamuraiReactiveCounterCcRules.SotenActionId);
            var mineuchiReady = IsActionSpecificReady(
                SamuraiReactiveCounterCcRules.MineuchiActionId);
            var decision = SamuraiReactiveCounterCcRules.Observe(
                counterState,
                new SamuraiReactiveCounterCcObservation(
                    enabled,
                    HardReset: false,
                    ExactTargetStillCurrent: true,
                    TargetAliveAndTargetable: IsLiveTarget(target),
                    exactKeyStillDown,
                    ProtectionPresent: protectionCount == 1,
                    distanceKnown,
                    distance,
                    sotenReady,
                    mineuchiReady,
                    BoundPresent: HasStatus(
                        localPlayer!,
                        EnemyCombatConstants.PvPBindStatusId),
                    // There is no guessed travel-time lead. The dash becomes
                    // eligible only on authoritative protection absence.
                    SotenApproachWindowOpen: episode.ProtectionObserved &&
                        protectionCount == 0,
                    configuredSotenMaximumRangeYalms));
            counterState = decision.NextState;
            if (decision.Kind == SamuraiReactiveCounterCcDecisionKind.Cancelled)
            {
                lastEvent = "Frozen SAM counter-CC intent cancelled without fallback";
                ResetCounter(clearSignals: false);
                return PublishSnapshot();
            }

            // Once Soten was client-accepted, reserve the bounded arrival lease
            // for its exact Mineuchi follow-up. Purify still ran first, but a
            // lower helper cannot create animation lock between the dash and stun.
            if (counterState.Phase == SamuraiReactiveCounterCcPhase.ApproachAccepted &&
                dispatchAllowed)
            {
                inputClaimedThisFrame = true;
                inputFrame.Consume();
            }

            if (decision.Kind is not (
                    SamuraiReactiveCounterCcDecisionKind.AttemptSoten or
                    SamuraiReactiveCounterCcDecisionKind.AttemptMineuchi) ||
                !dispatchAllowed)
            {
                return PublishSnapshot();
            }

            inputClaimedThisFrame = true;
            inputFrame.Consume();
            if (!HasGlobalNativeBoundaryReadiness(localPlayer!))
            {
                castCancellationRequestThisFrame = BuildCastCancellationRequest(
                    localPlayer!,
                    target,
                    decision.ActionId,
                    counterState.GameplayKeyToken,
                    CounterIntentEpochToken(episode, decision.ActionId),
                    inputFrame);
                return PublishSnapshot();
            }

            var result = TryUseCounterActionOnce(
                localPlayer!,
                episode,
                decision.ActionId,
                configuredSotenMaximumRangeYalms);
            if (!result.Attempted)
                return PublishSnapshot();

            lastAttemptedActionId = decision.ActionId;
            lastAttemptOutcome = result.Outcome;
            if (decision.ActionId == SamuraiReactiveCounterCcRules.SotenActionId)
                sotenAttemptCount++;
            else
                mineuchiAttemptCount++;
            if (result.Outcome == ClientActionAttemptOutcome.ClientAccepted)
            {
                acceptedCount++;
                inputFrame.Consume();
            }

            counterState = SamuraiReactiveCounterCcRules.CompleteAttempt(
                counterState,
                decision.ActionId,
                result.Outcome);
            if (counterState.Phase == SamuraiReactiveCounterCcPhase.ApproachAccepted)
            {
                sotenArrivalDeadlineMilliseconds = nowMilliseconds +
                    SamuraiReactiveRuntimeRules.SotenArrivalLeaseMilliseconds;
                lastEvent = $"Soten boundary completed: {result.Outcome}; waiting for Mineuchi";
            }
            else
            {
                lastEvent = $"{decision.ActionId} boundary completed: {result.Outcome}";
                ResetCounter(clearSignals: false, preserveLastEvent: true);
            }

            return PublishSnapshot();
        }
        catch (Exception exception)
        {
            LogFailure(exception, nowMilliseconds, "SAM counter-CC failed closed");
            ResetCounter(clearSignals: false);
            lastEvent = "SAM counter-CC exception; episode cancelled";
            return PublishSnapshot();
        }
    }

    internal unsafe SamuraiReactiveCounterCcProbeSnapshot ObserveZantetsuken(
        IPlayerCharacter? localPlayer,
        SupportedPvPContext context,
        bool enabled,
        bool allowHeldGameplayKey,
        bool dispatchAllowed,
        EmergencyActionInputFrame inputFrame,
        long nowMilliseconds,
        bool hardReset = false)
    {
        try
        {
            var localValid = IsValidLocalSamurai(localPlayer);
            var contextValid = context is SupportedPvPContext.CrystallineConflict or
                SupportedPvPContext.WolvesDen;
            if (hardReset || !enabled || !metadata.ZantetsukenWorkflowVerified ||
                !contextValid || !localValid)
            {
                ResetZantetsuken();
                lastEvent = !metadata.ZantetsukenWorkflowVerified
                    ? "SAM Zantetsuken metadata is not verified"
                    : "SAM Zantetsuken inactive";
                return PublishSnapshot();
            }

            if (zantetsukenSpentKeyToken > 0 &&
                !IsExactGameplayKeyStillDown(inputFrame, zantetsukenSpentKeyToken))
            {
                zantetsukenSpentKeyToken = 0;
            }

            if (!zantetsukenState.IsActive && zantetsukenSpentKeyToken == 0 &&
                TryResolveEligibleGameplayKey(
                    inputFrame,
                    allowHeldGameplayKey,
                    out var key) &&
                TrySelectExactZantetsukenTarget(
                    localPlayer!,
                    context,
                    out var selected))
            {
                zantetsukenTarget = selected;
                zantetsukenState = SamuraiZantetsukenRules.Arm(
                    selected.Identity,
                    (int)key,
                    nowMilliseconds,
                    selected.AllowJoblessWolvesDenTarget);
                lastEvent = $"Zantetsuken froze exact target {selected.Identity.EntityId:X8}";
            }

            if (!zantetsukenState.IsActive || zantetsukenTarget is not { } frozen)
                return PublishSnapshot();

            var target = ResolveFrozenTarget(localPlayer!, frozen);
            var exactTargetCurrent = target is not null;
            var ownKuzushiCount = exactTargetCurrent
                ? CountOwnSourceKuzushi(target!, localPlayer!.EntityId)
                : 0;
            var hasRange = exactTargetCurrent && HasNativeRangeAndLineOfSight(
                localPlayer!,
                target!,
                SamuraiZantetsukenRules.ActionId);
            var frozenKeyToken = zantetsukenState.GameplayKeyToken;
            var decision = SamuraiZantetsukenRules.Observe(
                zantetsukenState,
                new SamuraiZantetsukenObservation(
                    enabled,
                    HardReset: false,
                    exactTargetCurrent,
                    exactTargetCurrent && IsLiveTarget(target!),
                    IsExactGameplayKeyStillDown(
                        inputFrame,
                        zantetsukenState.GameplayKeyToken),
                    ownKuzushiCount,
                    exactTargetCurrent ? target!.ShieldPercentage : byte.MaxValue,
                    HasStatus(localPlayer!, EnemyCombatConstants.PvPBindStatusId),
                    IsActionSpecificReady(SamuraiZantetsukenRules.ActionId),
                    hasRange));
            zantetsukenState = decision.NextState;
            if (decision.Kind == SamuraiZantetsukenDecisionKind.Cancelled)
            {
                zantetsukenSpentKeyToken = frozenKeyToken;
                zantetsukenTarget = null;
                lastEvent = "Frozen Zantetsuken intent cancelled without fallback";
                return PublishSnapshot();
            }

            if (decision.Kind != SamuraiZantetsukenDecisionKind.Attempt ||
                !dispatchAllowed)
            {
                return PublishSnapshot();
            }

            inputClaimedThisFrame = true;
            inputFrame.Consume();
            if (!HasGlobalNativeBoundaryReadiness(localPlayer!))
            {
                castCancellationRequestThisFrame = BuildCastCancellationRequest(
                    localPlayer!,
                    target!,
                    SamuraiZantetsukenRules.ActionId,
                    zantetsukenState.GameplayKeyToken,
                    ZantetsukenIntentEpochToken(zantetsukenState),
                    inputFrame);
                return PublishSnapshot();
            }

            var result = TryUseZantetsukenOnce(localPlayer!, frozen);
            if (!result.Attempted) return PublishSnapshot();

            lastAttemptedActionId = SamuraiZantetsukenRules.ActionId;
            lastAttemptOutcome = result.Outcome;
            zantetsukenAttemptCount++;
            if (result.Outcome == ClientActionAttemptOutcome.ClientAccepted)
            {
                acceptedCount++;
                inputFrame.Consume();
            }

            zantetsukenSpentKeyToken = zantetsukenState.GameplayKeyToken;
            zantetsukenState = SamuraiZantetsukenRules.CompleteAttempt(
                zantetsukenState);
            zantetsukenTarget = null;
            lastEvent = $"Zantetsuken boundary completed: {result.Outcome}";
            return PublishSnapshot();
        }
        catch (Exception exception)
        {
            LogFailure(exception, nowMilliseconds, "SAM Zantetsuken failed closed");
            ResetZantetsuken();
            lastEvent = "SAM Zantetsuken exception; intent cancelled";
            return PublishSnapshot();
        }
    }

    internal void Reset()
    {
        ResetCounter(clearSignals: true);
        ResetZantetsuken();
        lastEvent = "Reset";
        PublishSnapshot();
    }

    internal SamuraiReactiveCounterCcProbeSnapshot ResetZantetsukenLane()
    {
        ResetZantetsuken();
        return PublishSnapshot();
    }

    private void TryFreezeNextProtectionEpisode(
        IPlayerCharacter localPlayer,
        SupportedPvPContext context,
        bool enablePostPurify,
        bool enablePostGuard,
        bool allowHeldGameplayKey,
        EmergencyActionInputFrame inputFrame,
        long nowMilliseconds)
    {
        while (TryDequeueProtectionSignal(out var signal))
        {
            if (!IsProtectionKindEnabled(
                    signal.Kind,
                    enablePostPurify,
                    enablePostGuard))
            {
                continue;
            }

            if (!SamuraiReactiveRuntimeRules.IsInsideLease(
                    signal.ObservedAtMilliseconds,
                    nowMilliseconds,
                    SamuraiReactiveRuntimeRules.SignalStatusObservationLeaseMilliseconds))
            {
                continue;
            }

            var target = ResolveSignalTarget(localPlayer, context, signal);
            if (target is null) continue;
            var keyToken = TryResolveEligibleGameplayKey(
                inputFrame,
                allowHeldGameplayKey,
                out var key)
                ? (int)key
                : 0;
            protectionEpisode = new FrozenProtectionEpisode(
                signal,
                target.Value,
                keyToken,
                ProtectionObserved: false);
            lastEvent = $"Queued exact {signal.Kind} actor {signal.CasterEntityId:X8}";
            return;
        }
    }

    private bool TryDequeueProtectionSignal(
        out SamuraiReactiveProtectionSignal signal)
    {
        lock (signalGate)
        {
            if (pendingSignals.Count == 0)
            {
                signal = default;
                return false;
            }

            signal = pendingSignals.Dequeue();
            return true;
        }
    }

    private FrozenActionTarget? ResolveSignalTarget(
        IPlayerCharacter localPlayer,
        SupportedPvPContext context,
        SamuraiReactiveProtectionSignal signal)
    {
        if (!signal.IsValid || signal.CasterEntityId == localPlayer.EntityId)
            return null;

        if (context == SupportedPvPContext.WolvesDen)
        {
            return TryResolveWolvesDenCurrentTarget(localPlayer, out var current) &&
                   current.Identity.EntityId == signal.CasterEntityId
                ? current
                : null;
        }

        var snapshots = executeTracker.Enemies
            .Where(enemy =>
                enemy.EntityId == signal.CasterEntityId &&
                EnemySlotRules.IsValidSlot(enemy.Slot) &&
                enemy.JobId != 0)
            .Take(2)
            .ToArray();
        if (snapshots.Length != 1) return null;
        var enemy = snapshots[0];
        var player = EnemySlotResolver.Resolve(objectTable, enemy.Slot);
        if (!HasValidNativeIdentity(player) ||
            player!.GameObjectId != enemy.GameObjectId ||
            player.EntityId != enemy.EntityId ||
            !player.ClassJob.IsValid ||
            player.ClassJob.RowId != enemy.JobId ||
            !HasCoherentObjectTableIdentity(player))
        {
            return null;
        }

        return CreateFrozenTarget(
            context,
            enemy.Slot,
            player,
            DarkKnightWolvesDenTargetKind.None);
    }

    private bool TrySelectExactZantetsukenTarget(
        IPlayerCharacter localPlayer,
        SupportedPvPContext context,
        out FrozenActionTarget target)
    {
        target = default;
        if (context == SupportedPvPContext.WolvesDen)
        {
            if (!TryResolveWolvesDenCurrentTarget(localPlayer, out var wolvesTarget))
                return false;
            var current = ResolveFrozenTarget(localPlayer, wolvesTarget);
            if (current is null || !IsZantetsukenCandidate(localPlayer, current))
                return false;
            target = wolvesTarget;
            return true;
        }

        // Selection is deterministic and ends before the intent is frozen.
        // After this point actor drift never falls through to another S-slot.
        foreach (var enemy in executeTracker.Enemies
                     .Where(static enemy => EnemySlotRules.IsValidSlot(enemy.Slot))
                     .OrderBy(static enemy => enemy.Slot))
        {
            var player = EnemySlotResolver.Resolve(objectTable, enemy.Slot);
            if (!HasValidNativeIdentity(player) ||
                player!.GameObjectId != enemy.GameObjectId ||
                player.EntityId != enemy.EntityId ||
                !player.ClassJob.IsValid ||
                player.ClassJob.RowId != enemy.JobId ||
                !HasCoherentObjectTableIdentity(player) ||
                !IsZantetsukenCandidate(localPlayer, player))
            {
                continue;
            }

            target = CreateFrozenTarget(
                context,
                enemy.Slot,
                player,
                DarkKnightWolvesDenTargetKind.None);
            return true;
        }

        return false;
    }

    private bool IsZantetsukenCandidate(
        IPlayerCharacter localPlayer,
        IBattleChara target) =>
        IsLiveTarget(target) &&
        target.ShieldPercentage == 0 &&
        CountOwnSourceKuzushi(target, localPlayer.EntityId) == 1 &&
        !HasStatus(localPlayer, EnemyCombatConstants.PvPBindStatusId) &&
        IsActionSpecificReady(SamuraiZantetsukenRules.ActionId) &&
        HasNativeRangeAndLineOfSight(
            localPlayer,
            target,
            SamuraiZantetsukenRules.ActionId);

    private IBattleChara? ResolveFrozenTarget(
        IPlayerCharacter localPlayer,
        FrozenActionTarget frozen)
    {
        IBattleChara? target;
        if (frozen.Context == SupportedPvPContext.CrystallineConflict)
        {
            if (!EnemySlotRules.IsValidSlot(frozen.EnemySlot)) return null;
            var snapshotMatches = executeTracker.Enemies.Count(enemy =>
                enemy.Slot == frozen.EnemySlot &&
                enemy.GameObjectId == frozen.Identity.GameObjectId &&
                enemy.EntityId == frozen.Identity.EntityId &&
                enemy.JobId == frozen.Identity.JobId) == 1;
            var player = EnemySlotResolver.Resolve(objectTable, frozen.EnemySlot);
            if (!snapshotMatches ||
                !HasValidNativeIdentity(player) ||
                player!.GameObjectId != frozen.Identity.GameObjectId ||
                player.EntityId != frozen.Identity.EntityId ||
                !player.ClassJob.IsValid ||
                player.ClassJob.RowId != frozen.Identity.JobId)
            {
                return null;
            }

            target = player;
        }
        else if (frozen.Context == SupportedPvPContext.WolvesDen)
        {
            var identity = new TargetPressureActorIdentity(
                frozen.Identity.GameObjectId,
                frozen.Identity.EntityId);
            if (!DarkKnightWolvesDenCurrentTargetResolver
                    .TryResolveFrozenCurrentHardTarget(
                        objectTable,
                        metadata.WolvesDenStrikingDummyVerified,
                        localPlayer,
                        identity,
                        frozen.WolvesDenTargetKind,
                        out target))
            {
                return null;
            }
        }
        else
        {
            return null;
        }

        return HasValidNativeIdentity(target) &&
               target!.Address == frozen.Address &&
               target.GameObjectId == frozen.Identity.GameObjectId &&
               target.EntityId == frozen.Identity.EntityId &&
               HasCoherentObjectTableIdentity(target) &&
               target.GameObjectId != localPlayer.GameObjectId &&
               target.EntityId != localPlayer.EntityId
            ? target
            : null;
    }

    private bool TryResolveWolvesDenCurrentTarget(
        IPlayerCharacter localPlayer,
        out FrozenActionTarget target)
    {
        target = default;
        if (!DarkKnightWolvesDenCurrentTargetResolver.TryResolveExactCurrentHardTarget(
                objectTable,
                metadata.WolvesDenStrikingDummyVerified,
                localPlayer,
                out var current,
                out _,
                out var targetKind,
                out _) ||
            !HasValidNativeIdentity(current) ||
            !HasCoherentObjectTableIdentity(current!))
        {
            return false;
        }

        target = CreateFrozenTarget(
            SupportedPvPContext.WolvesDen,
            enemySlot: 0,
            current!,
            targetKind);
        return true;
    }

    private static FrozenActionTarget CreateFrozenTarget(
        SupportedPvPContext context,
        int enemySlot,
        IBattleChara target,
        DarkKnightWolvesDenTargetKind wolvesDenTargetKind)
    {
        var jobId = target.ClassJob.IsValid ? target.ClassJob.RowId : 0;
        return new FrozenActionTarget(
            context,
            enemySlot,
            new SamuraiReactiveCounterCcTarget(
                target.GameObjectId,
                target.EntityId,
                jobId),
            target.Address,
            wolvesDenTargetKind,
            context == SupportedPvPContext.WolvesDen && jobId == 0);
    }

    private unsafe AttemptResult TryUseCounterActionOnce(
        IPlayerCharacter localPlayer,
        FrozenProtectionEpisode episode,
        uint actionId,
        float configuredSotenMaximumRangeYalms)
    {
        var attempted = false;
        var before = default(ClientActionAttemptFingerprint);
        var after = default(ClientActionAttemptFingerprint);
        try
        {
            var returned = nearAssist.RunWithoutRedirect(() =>
            {
                var target = ResolveFrozenTarget(localPlayer, episode.Target);
                if (target is null ||
                    CountExpectedProtectionStatuses(target, episode.Signal.Kind) != 0 ||
                    !IsActionSpecificReady(actionId) ||
                    !HasGlobalNativeBoundaryReadiness(localPlayer) ||
                    !TryGetEdgeDistance(localPlayer, target, out var edgeDistance) ||
                    !HasNativeRangeAndLineOfSight(localPlayer, target, actionId))
                {
                    return false;
                }

                if (actionId == SamuraiReactiveCounterCcRules.SotenActionId)
                {
                    var maximum = SamuraiReactiveCounterCcRules
                        .NormalizeSotenMaximumRangeYalms(
                            configuredSotenMaximumRangeYalms);
                    if (edgeDistance <= SamuraiReactiveCounterCcRules
                            .MineuchiMaximumRangeYalms ||
                        edgeDistance > maximum ||
                        HasStatus(localPlayer, EnemyCombatConstants.PvPBindStatusId))
                    {
                        return false;
                    }
                }
                else if (actionId == SamuraiReactiveCounterCcRules.MineuchiActionId)
                {
                    if (edgeDistance > SamuraiReactiveCounterCcRules
                            .MineuchiMaximumRangeYalms)
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }

                var actionManager = ActionManager.Instance();
                if (actionManager == null) return false;
                before = ClientActionAttemptBoundary.Capture(actionManager, actionId);
                attempted = true;
                bool accepted;
                if (actionId == SamuraiReactiveCounterCcRules.SotenActionId)
                {
                    var destination = target.Position;
                    if (!IsFinite(destination)) return false;
                    accepted = actionManager->UseActionLocation(
                        ActionType.Action,
                        actionId,
                        target.GameObjectId,
                        &destination,
                        0,
                        0);
                }
                else
                {
                    accepted = actionManager->UseAction(
                        ActionType.Action,
                        actionId,
                        target.GameObjectId,
                        0,
                        ActionManager.UseActionMode.None,
                        0);
                }

                after = ClientActionAttemptBoundary.Capture(actionManager, actionId);
                return accepted;
            });
            return attempted
                ? new AttemptResult(
                    ClientActionAttemptBoundaryRules.Classify(
                        returned,
                        actionId,
                        before,
                        after),
                    true)
                : new AttemptResult(ClientActionAttemptOutcome.NotInvoked, false);
        }
        catch (Exception exception)
        {
            LogFailure(exception, Environment.TickCount64, "SAM counter action boundary failed");
            return new AttemptResult(
                attempted
                    ? ClientActionAttemptOutcome.AcceptanceUnknown
                    : ClientActionAttemptOutcome.NotInvoked,
                attempted);
        }
    }

    private unsafe AttemptResult TryUseZantetsukenOnce(
        IPlayerCharacter localPlayer,
        FrozenActionTarget frozen)
    {
        var attempted = false;
        var before = default(ClientActionAttemptFingerprint);
        var after = default(ClientActionAttemptFingerprint);
        try
        {
            var returned = nearAssist.RunWithoutRedirect(() =>
            {
                var target = ResolveFrozenTarget(localPlayer, frozen);
                if (target is null ||
                    !IsLiveTarget(target) ||
                    target.ShieldPercentage != 0 ||
                    CountOwnSourceKuzushi(target, localPlayer.EntityId) != 1 ||
                    HasStatus(localPlayer, EnemyCombatConstants.PvPBindStatusId) ||
                    !IsActionSpecificReady(SamuraiZantetsukenRules.ActionId) ||
                    !HasGlobalNativeBoundaryReadiness(localPlayer) ||
                    !HasNativeRangeAndLineOfSight(
                        localPlayer,
                        target,
                        SamuraiZantetsukenRules.ActionId))
                {
                    return false;
                }

                var actionManager = ActionManager.Instance();
                if (actionManager == null) return false;
                before = ClientActionAttemptBoundary.Capture(
                    actionManager,
                    SamuraiZantetsukenRules.ActionId);
                attempted = true;
                var accepted = actionManager->UseAction(
                    ActionType.Action,
                    SamuraiZantetsukenRules.ActionId,
                    target.GameObjectId,
                    0,
                    ActionManager.UseActionMode.None,
                    0);
                after = ClientActionAttemptBoundary.Capture(
                    actionManager,
                    SamuraiZantetsukenRules.ActionId);
                return accepted;
            });
            return attempted
                ? new AttemptResult(
                    ClientActionAttemptBoundaryRules.Classify(
                        returned,
                        SamuraiZantetsukenRules.ActionId,
                        before,
                        after),
                    true)
                : new AttemptResult(ClientActionAttemptOutcome.NotInvoked, false);
        }
        catch (Exception exception)
        {
            LogFailure(exception, Environment.TickCount64, "SAM Zantetsuken boundary failed");
            return new AttemptResult(
                attempted
                    ? ClientActionAttemptOutcome.AcceptanceUnknown
                    : ClientActionAttemptOutcome.NotInvoked,
                attempted);
        }
    }

    private static unsafe bool IsActionSpecificReady(uint actionId)
    {
        var actionManager = ActionManager.Instance();
        if (actionManager == null) return false;
        var fingerprint = ClientActionAttemptBoundary.Capture(actionManager, actionId);
        return fingerprint.Captured &&
               fingerprint.AdjustedActionId == actionId &&
               fingerprint.IsActionOffCooldown &&
               fingerprint.ResourceStatus == 0;
    }

    private static unsafe bool HasGlobalNativeBoundaryReadiness(
        IPlayerCharacter localPlayer)
    {
        var actionManager = ActionManager.Instance();
        return actionManager != null &&
               HeldActionRetryRules.IsNativeBoundaryNearQueueable(
                   actionManager->AnimationLock,
                   localPlayer.IsCasting,
                   actionManager->CastActionId,
                   actionManager->ActionQueued);
    }

    private static unsafe bool HasNativeRangeAndLineOfSight(
        IPlayerCharacter localPlayer,
        IBattleChara target,
        uint actionId)
    {
        var sourceObject = GetNativeObject(localPlayer);
        var targetObject = GetNativeObject(target);
        if (sourceObject == null || targetObject == null) return false;
        return SeitonRangeRules.HasNativeRangeAndLineOfSight(
            ActionManager.GetActionInRangeOrLoS(
                actionId,
                sourceObject,
                targetObject));
    }

    private static int CountExpectedProtectionStatuses(
        IBattleChara target,
        SamuraiReactiveProtectionKind kind)
    {
        var count = 0;
        foreach (var status in target.StatusList)
        {
            if (!SamuraiReactiveRuntimeRules.IsExpectedProtectionStatus(
                    kind,
                    status.StatusId))
            {
                continue;
            }

            count++;
            if (count > 1) return count;
        }

        return count;
    }

    private static int CountOwnSourceKuzushi(
        IBattleChara target,
        uint localEntityId)
    {
        var count = 0;
        foreach (var status in target.StatusList)
        {
            if (status.StatusId != SamuraiZantetsukenRules.KuzushiStatusId ||
                status.SourceId != localEntityId)
            {
                continue;
            }

            count++;
            if (count > 1) return count;
        }

        return count;
    }

    private static bool HasStatus(IBattleChara actor, uint statusId)
    {
        foreach (var status in actor.StatusList)
        {
            if (status.StatusId == statusId) return true;
        }

        return false;
    }

    private static bool TryResolveEligibleGameplayKey(
        EmergencyActionInputFrame inputFrame,
        bool allowHeldGameplayKey,
        out VirtualKey key)
    {
        key = VirtualKey.NO_KEY;
        var input = inputFrame.Snapshot;
        if (!input.ProbeSucceeded || input.IsTextInputActive) return false;
        var candidate = allowHeldGameplayKey && input.HeldGameplayKeyEligible
            ? input.HeldGameplayKey
            : input.FreshGameplayKeyPressed
                ? input.FreshGameplayKey
                : VirtualKey.NO_KEY;
        if (!IsExactVirtualKey(candidate) ||
            !inputFrame.IsGameplayKeyGenerationEligible(candidate))
        {
            return false;
        }

        key = candidate;
        return true;
    }

    private static bool IsExactGameplayKeyStillDown(
        EmergencyActionInputFrame inputFrame,
        int gameplayKeyToken)
    {
        if (gameplayKeyToken <= 0) return false;
        var key = (VirtualKey)gameplayKeyToken;
        return IsExactVirtualKey(key) &&
               inputFrame.IsGameplayKeyGenerationEligible(key);
    }

    private static bool IsExactVirtualKey(VirtualKey key) =>
        key != VirtualKey.NO_KEY && Enum.IsDefined(typeof(VirtualKey), key);

    private static bool TryGetEdgeDistance(
        IBattleChara source,
        IBattleChara target,
        out float edgeDistance)
    {
        edgeDistance = float.PositiveInfinity;
        if (!IsFinite(source.Position) ||
            !IsFinite(target.Position) ||
            !float.IsFinite(source.HitboxRadius) ||
            source.HitboxRadius < 0f ||
            !float.IsFinite(target.HitboxRadius) ||
            target.HitboxRadius < 0f)
        {
            return false;
        }

        var centerDistance = Vector3.Distance(source.Position, target.Position);
        if (!float.IsFinite(centerDistance)) return false;
        edgeDistance = MathF.Max(
            0f,
            centerDistance - source.HitboxRadius - target.HitboxRadius);
        return true;
    }

    private static bool IsFinite(Vector3 point) =>
        float.IsFinite(point.X) &&
        float.IsFinite(point.Y) &&
        float.IsFinite(point.Z);

    private static bool IsValidLocalSamurai(IPlayerCharacter? localPlayer) =>
        HasValidNativeIdentity(localPlayer) &&
        localPlayer!.ClassJob.IsValid &&
        localPlayer.ClassJob.RowId == SamuraiReactiveCounterCcRules.SamuraiJobId &&
        IsLiveTarget(localPlayer);

    private static bool IsLiveTarget(IBattleChara target) =>
        HasValidNativeIdentity(target) &&
        target.IsTargetable &&
        !target.IsDead &&
        target.CurrentHp > 0 &&
        target.MaxHp >= target.CurrentHp;

    private bool HasCoherentObjectTableIdentity(IBattleChara target)
    {
        var byObjectId = objectTable.SearchById(target.GameObjectId) as IBattleChara;
        var byEntityId = objectTable.SearchByEntityId(target.EntityId) as IBattleChara;
        return HasSameNativeIdentity(target, byObjectId) &&
               HasSameNativeIdentity(target, byEntityId);
    }

    private static bool HasSameNativeIdentity(
        IGameObject? left,
        IGameObject? right) =>
        HasValidNativeIdentity(left) &&
        HasValidNativeIdentity(right) &&
        left!.Address == right!.Address &&
        left.GameObjectId == right.GameObjectId &&
        left.EntityId == right.EntityId;

    private static unsafe bool HasValidNativeIdentity(IGameObject? actor)
    {
        if (actor is null ||
            actor.Address == nint.Zero ||
            !TargetHighlightRules.IsValidGameObjectId(actor.GameObjectId) ||
            !MiracleInterceptConfirmationRules.IsValidEntityId(actor.EntityId))
        {
            return false;
        }

        var native = (GameObject*)actor.Address;
        return native != null && native->EntityId == actor.EntityId;
    }

    private static unsafe GameObject* GetNativeObject(IGameObject actor) =>
        HasValidNativeIdentity(actor) ? (GameObject*)actor.Address : null;

    private void ResetCounter(
        bool clearSignals,
        bool preserveLastEvent = false)
    {
        protectionEpisode = null;
        counterState = SamuraiReactiveCounterCcState.Initial;
        sotenArrivalDeadlineMilliseconds = -1;
        if (clearSignals)
        {
            lock (signalGate)
            {
                pendingSignals.Clear();
                rememberedSignals.Clear();
                rememberedSignalOrder.Clear();
            }
        }

        if (!preserveLastEvent) lastEvent = "SAM counter-CC reset";
    }

    private void ResetZantetsuken()
    {
        zantetsukenTarget = null;
        zantetsukenState = SamuraiZantetsukenState.Initial;
        zantetsukenSpentKeyToken = 0;
    }

    private SamuraiReactiveCounterCcProbeSnapshot PublishSnapshot()
    {
        var episode = protectionEpisode;
        var target = episode?.Target.Identity ?? zantetsukenTarget?.Identity ?? default;
        int queueDepth;
        long captured;
        long dropped;
        lock (signalGate)
        {
            queueDepth = pendingSignals.Count;
            captured = capturedSignalCount;
            dropped = droppedSignalCount;
        }

        var next = new SamuraiReactiveCounterCcProbeSnapshot(
            counterState.Phase.ToString(),
            episode?.Signal.Kind ?? SamuraiReactiveProtectionKind.None,
            episode?.Target.EnemySlot ?? zantetsukenTarget?.EnemySlot ?? 0,
            target.GameObjectId,
            target.EntityId,
            target.JobId,
            counterState.GameplayKeyToken > 0
                ? (VirtualKey)counterState.GameplayKeyToken
                : VirtualKey.NO_KEY,
            episode?.ProtectionObserved ?? false,
            inputClaimedThisFrame,
            lastAttemptedActionId,
            lastAttemptOutcome,
            sotenAttemptCount,
            mineuchiAttemptCount,
            zantetsukenAttemptCount,
            acceptedCount,
            queueDepth,
            captured,
            dropped,
            zantetsukenState.Phase.ToString(),
            lastEvent)
        {
            CastCancellationRequest = castCancellationRequestThisFrame,
        };
        Volatile.Write(ref snapshot, next);
        return next;
    }

    private static bool IsProtectionKindEnabled(
        SamuraiReactiveProtectionKind kind,
        bool enablePostPurify,
        bool enablePostGuard) => kind switch
        {
            SamuraiReactiveProtectionKind.PurifyResilience => enablePostPurify,
            SamuraiReactiveProtectionKind.Guard => enablePostGuard,
            _ => false,
        };

    private static unsafe HeldCastCancellationRequest? BuildCastCancellationRequest(
        IPlayerCharacter localPlayer,
        IBattleChara target,
        uint actionId,
        int frozenKeyCode,
        ulong intentEpochToken,
        EmergencyActionInputFrame inputFrame)
    {
        if (!HasValidNativeIdentity(localPlayer) ||
            !HasValidNativeIdentity(target) ||
            !IsLiveTarget(localPlayer) ||
            !IsLiveTarget(target) ||
            actionId == 0 ||
            frozenKeyCode <= 0 ||
            intentEpochToken == 0)
        {
            return null;
        }

        var frozenKey = (VirtualKey)frozenKeyCode;
        if (!IsExactVirtualKey(frozenKey) ||
            !inputFrame.IsGameplayKeyGenerationEligible(frozenKey))
        {
            return null;
        }

        var actionManager = ActionManager.Instance();
        if (actionManager == null ||
            !localPlayer.IsCasting ||
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
            target.GameObjectId,
            target.EntityId);
        var request = new HeldCastCancellationRequest(
            HeldCastCancellationHelperKind.ReactiveCounterCc,
            actionId,
            localIdentity,
            targetIdentity,
            frozenKeyCode,
            intentEpochToken);
        return request.IsValid ? request : null;
    }

    private static ulong CounterIntentEpochToken(
        FrozenProtectionEpisode episode,
        uint actionId)
    {
        var signal = episode.Signal;
        var token = unchecked(
            (ulong)signal.ObservedAtMilliseconds ^
            episode.Target.Identity.GameObjectId ^
            ((ulong)episode.Target.Identity.EntityId << 32) ^
            ((ulong)actionId << 8) ^
            signal.GlobalSequence ^
            ((ulong)signal.SourceSequence << 16));
        return token == 0 ? 1 : token;
    }

    private static ulong ZantetsukenIntentEpochToken(
        SamuraiZantetsukenState state)
    {
        var token = unchecked(
            (ulong)state.ArmedAtMilliseconds ^
            state.Target.GameObjectId ^
            ((ulong)state.Target.EntityId << 32) ^
            ((ulong)SamuraiZantetsukenRules.ActionId << 8));
        return token == 0 ? 1 : token;
    }

    private void LogFailure(
        Exception exception,
        long nowMilliseconds,
        string message)
    {
        if (nowMilliseconds < nextErrorLogAt) return;
        nextErrorLogAt = nowMilliseconds + 10_000;
        log.Error(exception, "Seiton Sense {Message}.", message);
    }

    private readonly record struct ProtectionSignalIdentity(
        uint CasterEntityId,
        uint ActionId,
        uint GlobalSequence,
        ushort SourceSequence);

    private readonly record struct FrozenActionTarget(
        SupportedPvPContext Context,
        int EnemySlot,
        SamuraiReactiveCounterCcTarget Identity,
        nint Address,
        DarkKnightWolvesDenTargetKind WolvesDenTargetKind,
        bool AllowJoblessWolvesDenTarget);

    private readonly record struct FrozenProtectionEpisode(
        SamuraiReactiveProtectionSignal Signal,
        FrozenActionTarget Target,
        int GameplayKeyToken,
        bool ProtectionObserved);

    private readonly record struct AttemptResult(
        ClientActionAttemptOutcome Outcome,
        bool Attempted);
}
