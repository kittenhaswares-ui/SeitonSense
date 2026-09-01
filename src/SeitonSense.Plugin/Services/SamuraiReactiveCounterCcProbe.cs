using System.Numerics;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using SeitonSense.Core;
using SeitonSense.Plugin.Models;

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
    bool ZantetsukenAutomaticHelperEnabled,
    bool ZantetsukenMetadataVerified,
    int SotenTimingSampleCount,
    int MineuchiTimingSampleCount,
    int PredictiveSotenLeadMilliseconds,
    int PredictiveMineuchiLeadMilliseconds,
    bool SotenArrivalConfirmed,
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
        false,
        false,
        0,
        0,
        0,
        0,
        false,
        "Not started");
}

/// <summary>
/// Isolated SAM action runtime. An existing shared ActionEffect capture feeds
/// exact self-target Purify/Guard records through EnqueueProtectionSignal. The
/// probe then requires live Resilience/Guard membership before it can remember
/// the episode. Exact sequence-bound ActionEffects warm the optional two-stage
/// Soten/Mineuchi timing; until then it waits for authoritative status absence.
/// Zantetsuken is a separate automatic lane which collects exact own-source
/// Kuzushi for 1.5 seconds before ranking and freezing a primary target,
/// retains reviewed hard-protection exclusions, and uses bounded native retries.
/// </summary>
internal sealed class SamuraiReactiveCounterCcProbe
{
    private const int MaximumQueuedSignals = 64;
    private const int MaximumRememberedSignals = 128;
    private const int MaximumQueuedActionEffects = 64;
    private const int MaximumPendingTimingAttempts = 8;
    private const int MaximumRememberedTimingEffects = 128;

    private readonly object signalGate = new();
    private readonly IObjectTable objectTable;
    private readonly ExecuteTracker executeTracker;
    private readonly NearAssistRedirector nearAssist;
    private readonly IPluginLog log;
    private readonly SamuraiReactiveMetadataValidation metadata;
    private readonly PluginConfiguration configuration;
    private readonly Queue<SamuraiReactiveProtectionSignal> pendingSignals = [];
    private readonly Queue<SamuraiReactiveActionEffectSignal> pendingActionEffects = [];
    private readonly HashSet<ProtectionSignalIdentity> rememberedSignals = [];
    private readonly Queue<ProtectionSignalIdentity> rememberedSignalOrder = [];
    private readonly List<PendingTimingAttempt> pendingTimingAttempts = [];
    private readonly Dictionary<uint, List<ReactiveCounterCcImpactSample>>
        currentSessionTimingSamples = [];
    private readonly HashSet<TimingEffectIdentity> learnedTimingEffects = [];
    private readonly Queue<TimingEffectIdentity> learnedTimingEffectOrder = [];

    private FrozenProtectionEpisode? protectionEpisode;
    private SamuraiReactiveCounterCcState counterState =
        SamuraiReactiveCounterCcState.Initial;
    private FrozenActionTarget? zantetsukenTarget;
    private SamuraiZantetsukenState zantetsukenState =
        SamuraiZantetsukenState.Initial;
    private SamuraiZantetsukenCollectionState zantetsukenCollectionState =
        SamuraiZantetsukenCollectionState.Initial;
    private HeldActionRetryState zantetsukenRetryState =
        HeldActionRetryState.Initial;
    private bool zantetsukenReadyEpochTerminal;
    private long sotenArrivalDeadlineMilliseconds = -1;
    private long capturedSignalCount;
    private long droppedSignalCount;
    private long capturedActionEffectCount;
    private long droppedActionEffectCount;
    private long learnedTimingSampleCount;
    private long sotenAttemptCount;
    private long mineuchiAttemptCount;
    private long zantetsukenAttemptCount;
    private long acceptedCount;
    private bool inputClaimedThisFrame;
    private bool zantetsukenAutomaticHelperEnabled;
    private uint timingLocalEntityId;
    private SupportedPvPContext timingContext;
    private int predictiveSotenLeadMilliseconds;
    private int predictiveMineuchiLeadMilliseconds;
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
        SamuraiReactiveMetadataValidation metadata,
        PluginConfiguration configuration)
    {
        this.objectTable = objectTable;
        this.executeTracker = executeTracker;
        this.nearAssist = nearAssist;
        this.log = log;
        this.metadata = metadata;
        this.configuration = configuration;
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

    internal bool EnqueueActionEffectSignal(
        SamuraiReactiveActionEffectSignal signal)
    {
        if (!signal.IsValid) return false;
        lock (signalGate)
        {
            while (pendingActionEffects.Count >= MaximumQueuedActionEffects)
            {
                pendingActionEffects.Dequeue();
                droppedActionEffectCount++;
            }

            pendingActionEffects.Enqueue(signal);
            capturedActionEffectCount++;
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
        predictiveSotenLeadMilliseconds = 0;
        predictiveMineuchiLeadMilliseconds = 0;
        try
        {
            var localValid = IsValidLocalSamurai(localPlayer);
            var contextValid = context is SupportedPvPContext.CrystallineConflict or
                SupportedPvPContext.WolvesDen;
            var blockerMetadataVerified = HasCompleteMineuchiBlockerMetadata();
            if (hardReset || !localValid || !contextValid)
            {
                ResetTimingSession();
            }
            else
            {
                EnsureTimingSession(localPlayer!.EntityId, context);
                DrainActionEffectSignals(localPlayer.EntityId, nowMilliseconds);
            }

            if (hardReset || !enabled ||
                (!enablePostPurify && !enablePostGuard) ||
                !metadata.CounterCcVerified ||
                !blockerMetadataVerified ||
                !contextValid || !localValid)
            {
                ResetCounter(
                    clearSignals: hardReset ||
                        !enabled ||
                        !metadata.CounterCcVerified ||
                        !blockerMetadataVerified ||
                        !contextValid ||
                        !localValid);
                lastEvent = !metadata.CounterCcVerified
                    ? "SAM counter-CC action/protection metadata is not verified"
                    : !blockerMetadataVerified
                        ? "SAM Mineuchi blocker-family metadata is incomplete"
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

            var protection = ObserveExpectedProtection(
                target,
                episode.Signal.Kind);
            if (protection.Count > 1)
            {
                lastEvent = "Ambiguous duplicate protection statuses; episode cancelled";
                ResetCounter(clearSignals: false);
                return PublishSnapshot();
            }

            if (!episode.ProtectionObserved)
            {
                if (protection.Count == 0)
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

                var gameplayKeyToken = TryResolveEligibleGameplayKey(
                    inputFrame,
                    allowHeldGameplayKey,
                    out var observedKey)
                    ? (int)observedKey
                    : 0;
                episode = episode with
                {
                    ProtectionObserved = true,
                    ProtectionStatusId = protection.StatusId,
                    ScheduledProtectionEndAtMilliseconds =
                        protection.RemainingMilliseconds > 0
                            ? SaturatingAdd(
                                nowMilliseconds,
                                protection.RemainingMilliseconds)
                            : -1,
                    GameplayKeyToken = gameplayKeyToken,
                };
                protectionEpisode = episode;
                lastEvent = gameplayKeyToken > 0
                    ? $"Exact {episode.Signal.Kind} status/key frozen"
                    : $"Exact {episode.Signal.Kind} status observed; waiting for held key";
            }

            if (protection.Count == 1 &&
                episode.ProtectionStatusId != protection.StatusId)
            {
                lastEvent = "Frozen protection status row drifted; episode cancelled";
                ResetCounter(clearSignals: false, preserveLastEvent: true);
                return PublishSnapshot();
            }

            if (episode.GameplayKeyToken == 0)
            {
                if (TryResolveEligibleGameplayKey(
                        inputFrame,
                        allowHeldGameplayKey,
                        out var currentKey))
                {
                    episode = episode with { GameplayKeyToken = (int)currentKey };
                    protectionEpisode = episode;
                    lastEvent = protection.Count == 1
                        ? $"Exact protection/key frozen: {currentKey}"
                        : $"Protection release-edge key frozen: {currentKey}";
                }
                else if (protection.Count == 0)
                {
                    // Absence is a single release edge, not a multi-second
                    // opportunity for an unrelated later key press.
                    lastEvent = "Protection ended without exact held-key consent";
                    ResetCounter(clearSignals: false, preserveLastEvent: true);
                    return PublishSnapshot();
                }
                else
                {
                    return PublishSnapshot();
                }
            }

            if (!counterState.IsActive)
            {
                counterState = SamuraiReactiveCounterCcRules.Arm(
                    episode.Target.Identity,
                    episode.GameplayKeyToken,
                    nowMilliseconds,
                    episode.Target.AllowJoblessWolvesDenTarget);
                if (!counterState.IsActive)
                {
                    lastEvent = "Frozen status/key could not arm the exact actor";
                    ResetCounter(clearSignals: false, preserveLastEvent: true);
                    return PublishSnapshot();
                }
            }

            if (counterState.Phase == SamuraiReactiveCounterCcPhase.Armed &&
                !IsExactGameplayKeyStillDown(
                    inputFrame,
                    counterState.GameplayKeyToken))
            {
                lastEvent = "Frozen pre-Soten held key was released or changed";
                ResetCounter(clearSignals: false, preserveLastEvent: true);
                return PublishSnapshot();
            }

            if (counterState.Phase == SamuraiReactiveCounterCcPhase.ApproachAccepted &&
                inputFrame.Snapshot.IsTextInputActive)
            {
                lastEvent = "Accepted Soten follow-up cancelled by text input";
                ResetCounter(clearSignals: false, preserveLastEvent: true);
                return PublishSnapshot();
            }

            // Once Soten is accepted, the one frozen staged intent owns its
            // exact Mineuchi completion; releasing or changing the initiating
            // movement key must not strand the SAM beside the target.
            var exactKeyStillDown =
                counterState.Phase == SamuraiReactiveCounterCcPhase.ApproachAccepted ||
                IsExactGameplayKeyStillDown(
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
            var mineuchiBlockerCount = CountMineuchiBlockingProtections(target);
            PredictiveAttemptAuthorization? predictiveAuthorization = null;
            var sotenApproachWindowOpen = protection.Count == 0;
            var mineuchiImpactWindowOpen = mineuchiBlockerCount == 0;
            if (protection.Count == 1 &&
                protection.RemainingMilliseconds > 0 &&
                episode.ScheduledProtectionEndAtMilliseconds > 0 &&
                distanceKnown)
            {
                var scheduledStatusId = protection.StatusId;
                var scheduledRemainingMilliseconds =
                    protection.RemainingMilliseconds;
                if (counterState.Phase == SamuraiReactiveCounterCcPhase.Armed &&
                    distance > SamuraiReactiveCounterCcRules.MineuchiMaximumRangeYalms &&
                    TryGetCombinedPredictiveTiming(distance, out var combinedTiming))
                {
                    predictiveSotenLeadMilliseconds =
                        combinedTiming.CombinedSotenLeadMilliseconds;
                    predictiveMineuchiLeadMilliseconds =
                        combinedTiming.MineuchiSafeLeadMilliseconds;
                    if (SamuraiReactivePredictiveTimingRules
                            .ShouldStartPredictiveSoten(
                                1,
                                scheduledRemainingMilliseconds,
                                combinedTiming) &&
                        ReactiveCounterCcImpactTimingRules
                            .IsScheduledProtectionStillValid(
                                episode.ScheduledProtectionEndAtMilliseconds,
                                nowMilliseconds,
                                scheduledRemainingMilliseconds,
                                combinedTiming.CombinedSotenLeadMilliseconds))
                    {
                        sotenApproachWindowOpen = true;
                        predictiveAuthorization = new PredictiveAttemptAuthorization(
                            SamuraiReactiveCounterCcRules.SotenActionId,
                            scheduledStatusId,
                            episode.ScheduledProtectionEndAtMilliseconds,
                            combinedTiming.CombinedSotenLeadMilliseconds);
                    }
                }

                var predictiveMineuchiMayStart =
                    counterState.Phase != SamuraiReactiveCounterCcPhase.ApproachAccepted ||
                    episode.SotenActionEffectConfirmed;
                if (predictiveMineuchiMayStart &&
                    distance <= SamuraiReactiveCounterCcRules.MineuchiMaximumRangeYalms &&
                    TryGetOnlyScheduledProtection(
                        target,
                        episode,
                        out scheduledStatusId,
                        out scheduledRemainingMilliseconds) &&
                    TryGetMineuchiPredictiveLead(
                        distance,
                        out var mineuchiSafeLeadMilliseconds))
                {
                    predictiveMineuchiLeadMilliseconds =
                        mineuchiSafeLeadMilliseconds;
                    if (SamuraiReactivePredictiveTimingRules
                            .ShouldStartPredictiveMineuchi(
                                1,
                                scheduledRemainingMilliseconds,
                                mineuchiSafeLeadMilliseconds) &&
                        ReactiveCounterCcImpactTimingRules
                            .IsScheduledProtectionStillValid(
                                episode.ScheduledProtectionEndAtMilliseconds,
                                nowMilliseconds,
                                scheduledRemainingMilliseconds,
                                mineuchiSafeLeadMilliseconds))
                    {
                        mineuchiImpactWindowOpen = true;
                        predictiveAuthorization = new PredictiveAttemptAuthorization(
                            SamuraiReactiveCounterCcRules.MineuchiActionId,
                            scheduledStatusId,
                            episode.ScheduledProtectionEndAtMilliseconds,
                            mineuchiSafeLeadMilliseconds);
                    }
                }
            }

            var decision = SamuraiReactiveCounterCcRules.Observe(
                counterState,
                new SamuraiReactiveCounterCcObservation(
                    enabled,
                    HardReset: false,
                    ExactTargetStillCurrent: true,
                    TargetAliveAndTargetable: IsLiveTarget(target),
                    exactKeyStillDown,
                    ProtectionPresent: mineuchiBlockerCount > 0,
                    distanceKnown,
                    distance,
                    sotenReady,
                    mineuchiReady,
                    BoundPresent: HasStatus(
                        localPlayer!,
                        EnemyCombatConstants.PvPBindStatusId),
                    SotenApproachWindowOpen: sotenApproachWindowOpen,
                    configuredSotenMaximumRangeYalms,
                    MineuchiImpactWindowOpen: mineuchiImpactWindowOpen));
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
                configuredSotenMaximumRangeYalms,
                predictiveAuthorization is { } authorization &&
                authorization.ActionId == decision.ActionId
                    ? authorization
                    : null);
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
                RegisterPendingTimingAttempt(
                    episode,
                    decision.ActionId,
                    result,
                    Math.Max(nowMilliseconds, Environment.TickCount64));
            }

            if (decision.ActionId == SamuraiReactiveCounterCcRules.SotenActionId &&
                result.Outcome == ClientActionAttemptOutcome.ClientAccepted)
            {
                episode = episode with
                {
                    SotenActionEffectConfirmed = false,
                    SotenSourceSequence = result.SourceSequence,
                };
                protectionEpisode = episode;
            }
            counterState = SamuraiReactiveCounterCcRules.CompleteAttempt(
                counterState,
                decision.ActionId,
                result.Outcome);
            if (counterState.Phase == SamuraiReactiveCounterCcPhase.ApproachAccepted)
            {
                sotenArrivalDeadlineMilliseconds = nowMilliseconds +
                    SamuraiReactiveRuntimeRules.SotenArrivalLeaseMilliseconds;
                lastEvent = result.SourceSequence != 0
                    ? $"Soten accepted with exact sequence {result.SourceSequence}; waiting for arrival/Mineuchi"
                    : "Soten accepted without an exact timing sequence; Mineuchi stays conservative";
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
        bool dispatchAllowed,
        EmergencyActionInputFrame inputFrame,
        long nowMilliseconds,
        bool hardReset = false)
    {
        zantetsukenAutomaticHelperEnabled = enabled;
        try
        {
            var localValid = IsValidLocalSamurai(localPlayer);
            var contextValid = context is SupportedPvPContext.CrystallineConflict or
                SupportedPvPContext.WolvesDen;
            var metadataVerified = metadata.ZantetsukenWorkflowVerified;
            if (hardReset || !enabled || !metadataVerified ||
                !contextValid || !localValid)
            {
                ResetZantetsuken();
                lastEvent = !metadataVerified
                    ? "SAM Zantetsuken metadata is not verified"
                    : "SAM Zantetsuken inactive";
                return PublishSnapshot();
            }

            var actionReady = IsActionSpecificReady(
                SamuraiZantetsukenRules.ActionId);
            if (!actionReady)
            {
                ResetZantetsuken();
                lastEvent = "Automatic Zantetsuken waiting for LB readiness";
                return PublishSnapshot();
            }

            if (zantetsukenReadyEpochTerminal)
                return PublishSnapshot();

            var exactOwnKuzushiObserved =
                HasAnyExactOwnSourceKuzushiForCollection(
                    localPlayer!,
                    context);
            var localIdentity = new TargetPressureActorIdentity(
                localPlayer!.GameObjectId,
                localPlayer.EntityId);
            var collectionDecision = SamuraiZantetsukenRules.ObserveCollection(
                zantetsukenCollectionState,
                new SamuraiZantetsukenCollectionObservation(
                    Enabled: true,
                    HardReset: false,
                    context,
                    localIdentity,
                    exactOwnKuzushiObserved,
                    nowMilliseconds));
            zantetsukenCollectionState = collectionDecision.NextState;
            if (!collectionDecision.CanSelectAndFreezeTarget)
            {
                if (zantetsukenState.IsActive || zantetsukenTarget is not null)
                    ResetZantetsukenIntent();

                if (zantetsukenCollectionState.IsCollecting)
                {
                    var elapsed = Math.Max(
                        0,
                        nowMilliseconds - zantetsukenCollectionState
                            .FirstExactOwnSourceKuzushiAtMilliseconds);
                    var remaining = Math.Max(
                        0,
                        SamuraiZantetsukenRules.CollectionDelayMilliseconds -
                        elapsed);
                    lastEvent =
                        $"Automatic Zantetsuken collecting Kuzushi: {remaining} ms";
                }
                else
                {
                    lastEvent =
                        "Automatic Zantetsuken waiting for a current own-Kuzushi hit";
                }

                return PublishSnapshot();
            }

            if (!zantetsukenState.IsActive)
            {
                if (!TrySelectExactZantetsukenTarget(
                        localPlayer,
                        context,
                        out var selected))
                {
                    lastEvent =
                        "Automatic Zantetsuken collection ready; waiting for a reachable safe cluster";
                    return PublishSnapshot();
                }

                zantetsukenTarget = selected;
                zantetsukenState = SamuraiZantetsukenRules.Arm(
                    selected.Identity,
                    nowMilliseconds,
                    selected.AllowJoblessWolvesDenTarget);
                zantetsukenRetryState = HeldActionRetryState.Initial;
                lastEvent =
                    $"Automatic Zantetsuken froze exact target {selected.Identity.EntityId:X8}";
            }

            if (!zantetsukenState.IsActive || zantetsukenTarget is not { } frozen)
            {
                lastEvent =
                    "Automatic Zantetsuken waiting for a reachable own-Kuzushi hit";
                return PublishSnapshot();
            }

            var target = ResolveFrozenTarget(localPlayer!, frozen);
            var exactTargetCurrent = target is not null;
            var exactOwnKuzushiOnTarget = exactTargetCurrent &&
                HasExactOwnSourceKuzushiOnFrozenTarget(localPlayer!, frozen);
            var executeBlockingProtectionCount = exactTargetCurrent
                ? CountExecuteBlockingProtections(target!)
                : -1;
            var hasRange = exactTargetCurrent && HasNativeRangeAndLineOfSight(
                localPlayer!,
                target!,
                SamuraiZantetsukenRules.ActionId);
            var decision = SamuraiZantetsukenRules.Observe(
                zantetsukenState,
                new SamuraiZantetsukenObservation(
                    enabled,
                    HardReset: false,
                    exactTargetCurrent,
                    exactTargetCurrent && IsLiveTarget(target!),
                    exactOwnKuzushiOnTarget,
                    executeBlockingProtectionCount,
                    HasStatus(localPlayer!, EnemyCombatConstants.PvPBindStatusId),
                    actionReady,
                    hasRange));
            zantetsukenState = decision.NextState;
            if (decision.Kind == SamuraiZantetsukenDecisionKind.Cancelled)
            {
                var nativeAttemptAlreadyMade =
                    zantetsukenRetryState.NativeAttemptCount > 0;
                ResetZantetsukenIntent();
                zantetsukenReadyEpochTerminal = nativeAttemptAlreadyMade;
                if (zantetsukenReadyEpochTerminal)
                {
                    zantetsukenCollectionState =
                        SamuraiZantetsukenCollectionState.Initial;
                }
                lastEvent = executeBlockingProtectionCount > 0
                    ? "Frozen Zantetsuken intent cancelled by exact invulnerability/Cover"
                    : !exactOwnKuzushiOnTarget
                        ? "Frozen Zantetsuken intent released because own Kuzushi left its primary target"
                        : "Frozen Zantetsuken intent released before native attempt";
                return PublishSnapshot();
            }

            if (decision.Kind != SamuraiZantetsukenDecisionKind.Attempt ||
                !dispatchAllowed)
            {
                return PublishSnapshot();
            }

            var retainsSchedulerFrame =
                HeldActionRetryRules.RetainsSchedulerFrame(
                    zantetsukenRetryState,
                    nowMilliseconds,
                    exactIntentValid: true,
                    actionSpecificReady: actionReady,
                    targetSpecificReady: hasRange);
            if (!retainsSchedulerFrame)
            {
                ResetZantetsukenIntent();
                lastEvent = "Frozen Zantetsuken retry became invalid";
                return PublishSnapshot();
            }

            inputClaimedThisFrame = true;
            inputFrame.Consume();
            if (!HasGlobalNativeBoundaryReadiness(localPlayer!))
            {
                lastEvent = "Frozen automatic Zantetsuken waiting for global native boundary";
                return PublishSnapshot();
            }

            if (!HeldActionRetryRules.CanAttemptFrozenIntent(
                    zantetsukenRetryState,
                    nowMilliseconds))
            {
                lastEvent = "Frozen automatic Zantetsuken retaining retry throttle priority";
                return PublishSnapshot();
            }

            var result = TryUseZantetsukenOnce(
                localPlayer!,
                frozen,
                zantetsukenCollectionState);
            if (!result.Attempted)
            {
                var nativeAttemptAlreadyMade =
                    zantetsukenRetryState.NativeAttemptCount > 0;
                ResetZantetsukenIntent();
                zantetsukenReadyEpochTerminal =
                    result.TerminalFailure || nativeAttemptAlreadyMade;
                if (zantetsukenReadyEpochTerminal)
                {
                    zantetsukenCollectionState =
                        SamuraiZantetsukenCollectionState.Initial;
                }
                lastEvent = result.TerminalFailure
                    ? "Frozen Zantetsuken hit an unknown boundary failure; LB-ready epoch retired"
                    : "Frozen Zantetsuken failed final exact revalidation";
                return PublishSnapshot();
            }

            lastAttemptedActionId = SamuraiZantetsukenRules.ActionId;
            lastAttemptOutcome = result.Outcome;
            zantetsukenAttemptCount++;
            if (result.Outcome == ClientActionAttemptOutcome.ClientAccepted)
            {
                acceptedCount++;
                inputFrame.Consume();
            }

            var completion = HeldActionRetryRules.Complete(
                zantetsukenRetryState,
                Math.Max(0, nowMilliseconds),
                result.Outcome);
            if (completion.RetryScheduled ||
                completion.Disposition == HeldActionRetryDisposition.SoftWait)
            {
                zantetsukenRetryState = completion.NextState;
                lastEvent =
                    $"Zantetsuken attempt {zantetsukenRetryState.NativeAttemptCount}/" +
                    $"{HeldActionRetryRules.ResolveAttemptLimit(zantetsukenRetryState)}: " +
                    $"{result.Outcome}";
                return PublishSnapshot();
            }

            zantetsukenReadyEpochTerminal = true;
            zantetsukenCollectionState =
                SamuraiZantetsukenCollectionState.Initial;
            zantetsukenRetryState = HeldActionRetryState.Initial;
            zantetsukenState = SamuraiZantetsukenRules.CompleteAttempt(zantetsukenState);
            zantetsukenTarget = null;
            lastEvent = $"Automatic Zantetsuken epoch completed: {result.Outcome}";
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
        ResetTimingSession();
        ResetZantetsuken();
        zantetsukenAutomaticHelperEnabled = false;
        lastEvent = "Reset";
        PublishSnapshot();
    }

    internal SamuraiReactiveCounterCcProbeSnapshot ResetZantetsukenLane()
    {
        ResetZantetsuken();
        zantetsukenAutomaticHelperEnabled = false;
        return PublishSnapshot();
    }

    private void TryFreezeNextProtectionEpisode(
        IPlayerCharacter localPlayer,
        SupportedPvPContext context,
        bool enablePostPurify,
        bool enablePostGuard,
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
            protectionEpisode = new FrozenProtectionEpisode(
                signal,
                target.Value,
                GameplayKeyToken: 0,
                ProtectionObserved: false,
                ProtectionStatusId: 0,
                ScheduledProtectionEndAtMilliseconds: -1,
                SotenSourceSequence: 0,
                SotenActionEffectConfirmed: false);
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

    private void EnsureTimingSession(
        uint localEntityId,
        SupportedPvPContext context)
    {
        if (timingLocalEntityId == localEntityId && timingContext == context)
            return;
        ResetTimingSession();
        timingLocalEntityId = localEntityId;
        timingContext = context;
    }

    private void ResetTimingSession()
    {
        lock (signalGate) pendingActionEffects.Clear();
        pendingTimingAttempts.Clear();
        learnedTimingEffects.Clear();
        learnedTimingEffectOrder.Clear();
        currentSessionTimingSamples.Clear();
        timingLocalEntityId = 0;
        timingContext = SupportedPvPContext.None;
        predictiveSotenLeadMilliseconds = 0;
        predictiveMineuchiLeadMilliseconds = 0;
    }

    private void DrainActionEffectSignals(
        uint localEntityId,
        long nowMilliseconds)
    {
        pendingTimingAttempts.RemoveAll(pending =>
            nowMilliseconds < pending.AttemptedAtMilliseconds ||
            nowMilliseconds - pending.AttemptedAtMilliseconds >
                ReactiveCounterCcImpactTimingRules.MaximumSampleMilliseconds);

        while (TryDequeueActionEffectSignal(out var signal))
        {
            if (!signal.IsValid || signal.CasterEntityId != localEntityId)
                continue;
            var matches = pendingTimingAttempts
                .Select((pending, index) => (Pending: pending, Index: index))
                .Where(entry =>
                    entry.Pending.ActionId == signal.ActionId &&
                    entry.Pending.TargetEntityId == signal.TargetEntityId &&
                    entry.Pending.SourceSequence == signal.SourceSequence)
                .Take(2)
                .ToArray();
            if (matches.Length != 1) continue;

            var pending = matches[0].Pending;
            pendingTimingAttempts.RemoveAt(matches[0].Index);
            var effectIdentity = new TimingEffectIdentity(
                signal.ActionId,
                signal.TargetEntityId,
                signal.SourceSequence);
            if (!learnedTimingEffects.Add(effectIdentity)) continue;
            learnedTimingEffectOrder.Enqueue(effectIdentity);
            while (learnedTimingEffectOrder.Count > MaximumRememberedTimingEffects)
            {
                learnedTimingEffects.Remove(learnedTimingEffectOrder.Dequeue());
            }

            if (signal.ActionId == SamuraiReactiveCounterCcRules.SotenActionId &&
                protectionEpisode is { } episode &&
                counterState.Phase == SamuraiReactiveCounterCcPhase.ApproachAccepted &&
                episode.Target.Identity.EntityId == signal.TargetEntityId &&
                episode.SotenSourceSequence == signal.SourceSequence &&
                pending.EpisodeToken == CounterIntentEpochToken(
                    episode,
                    SamuraiReactiveCounterCcRules.SotenActionId))
            {
                protectionEpisode = episode with
                {
                    SotenActionEffectConfirmed = true,
                };
            }

            if (pending.SourceSequence == 0 ||
                !ReactiveCounterCcImpactTimingRules.TryMeasureSample(
                    pending.ActionId,
                    pending.TargetEntityId,
                    pending.SourceSequence,
                    pending.AttemptedAtMilliseconds,
                    signal.ActionId,
                    signal.TargetEntityId,
                    signal.SourceSequence,
                    signal.ObservedAtMilliseconds,
                    out var sampleMilliseconds) ||
                !ReactiveCounterCcImpactTimingRules.TryCreateCalibrationSample(
                    sampleMilliseconds,
                    pending.EdgeDistanceYalms,
                    out var sample))
            {
                continue;
            }

            configuration.ReactiveCcImpactCalibrationSamples.TryGetValue(
                pending.ActionId,
                out var previousSamples);
            var samples = ReactiveCounterCcImpactTimingRules.AppendBoundedSample(
                previousSamples,
                sample);
            configuration.ReactiveCcImpactCalibrationSamples[pending.ActionId] =
                samples.ToList();
            currentSessionTimingSamples.TryGetValue(
                pending.ActionId,
                out var previousSessionSamples);
            currentSessionTimingSamples[pending.ActionId] =
                ReactiveCounterCcImpactTimingRules.AppendBoundedSample(
                    previousSessionSamples,
                    sample).ToList();
            learnedTimingSampleCount++;
            configuration.Save();
            lastEvent =
                $"Learned exact {pending.ActionId} effect: {sampleMilliseconds} ms at " +
                $"{pending.EdgeDistanceYalms:0.00}y ({samples.Length} stored)";
        }
    }

    private bool TryDequeueActionEffectSignal(
        out SamuraiReactiveActionEffectSignal signal)
    {
        lock (signalGate)
        {
            if (pendingActionEffects.Count == 0)
            {
                signal = default;
                return false;
            }

            signal = pendingActionEffects.Dequeue();
            return true;
        }
    }

    private void RegisterPendingTimingAttempt(
        FrozenProtectionEpisode episode,
        uint actionId,
        AttemptResult result,
        long registrationNowMilliseconds)
    {
        if (result.Outcome != ClientActionAttemptOutcome.ClientAccepted ||
            result.SourceSequence == 0 ||
            !SamuraiReactiveRuntimeRules.CanRegisterExactTimingAttempt(
                result.AttemptedAtMilliseconds,
                registrationNowMilliseconds) ||
            !float.IsFinite(result.EdgeDistanceYalms) ||
            result.EdgeDistanceYalms < 0f)
        {
            return;
        }

        pendingTimingAttempts.RemoveAll(pending =>
            pending.SourceSequence == result.SourceSequence &&
            pending.ActionId == actionId);
        if (pendingTimingAttempts.Count >= MaximumPendingTimingAttempts)
            pendingTimingAttempts.RemoveAt(0);
        pendingTimingAttempts.Add(new PendingTimingAttempt(
            actionId,
            episode.Target.Identity.EntityId,
            result.SourceSequence,
            result.AttemptedAtMilliseconds,
            result.EdgeDistanceYalms,
            CounterIntentEpochToken(episode, actionId)));
    }

    private bool TryGetCombinedPredictiveTiming(
        float sotenEdgeDistanceYalms,
        out SamuraiReactivePredictiveTiming timing)
    {
        timing = default;
        if (!currentSessionTimingSamples.TryGetValue(
                SamuraiReactiveCounterCcRules.SotenActionId,
                out var currentSotenSamples) ||
            SamuraiReactivePredictiveTimingRules.CountEligibleSotenTransitSamples(
                currentSotenSamples,
                sotenEdgeDistanceYalms) <
                SamuraiReactivePredictiveTimingRules
                    .MinimumSotenTransitSamplesForPrediction ||
            !currentSessionTimingSamples.TryGetValue(
                SamuraiReactiveCounterCcRules.MineuchiActionId,
                out var currentMineuchiSamples) ||
            SamuraiReactivePredictiveTimingRules.CountEligibleMineuchiSamples(
                currentMineuchiSamples,
                SamuraiReactiveCounterCcRules.MineuchiMaximumRangeYalms) < 1 ||
            !configuration.ReactiveCcImpactCalibrationSamples.TryGetValue(
                SamuraiReactiveCounterCcRules.MineuchiActionId,
                out var mineuchiSamples))
        {
            return false;
        }

        return SamuraiReactivePredictiveTimingRules.TryGetCombinedTiming(
            currentSotenSamples,
            sotenEdgeDistanceYalms,
            mineuchiSamples,
            out timing);
    }

    private bool TryGetMineuchiPredictiveLead(
        float edgeDistanceYalms,
        out int safeLeadMilliseconds)
    {
        safeLeadMilliseconds = 0;
        return currentSessionTimingSamples.TryGetValue(
                   SamuraiReactiveCounterCcRules.MineuchiActionId,
                   out var currentSamples) &&
               SamuraiReactivePredictiveTimingRules.CountEligibleMineuchiSamples(
                   currentSamples,
                   edgeDistanceYalms) > 0 &&
               configuration.ReactiveCcImpactCalibrationSamples.TryGetValue(
                   SamuraiReactiveCounterCcRules.MineuchiActionId,
                   out var samples) &&
               SamuraiReactivePredictiveTimingRules
                   .TryGetMineuchiSafeLeadMilliseconds(
                       samples,
                       edgeDistanceYalms,
                       out safeLeadMilliseconds);
    }

    private int StoredTimingSampleCount(uint actionId) =>
        configuration.ReactiveCcImpactCalibrationSamples.TryGetValue(
            actionId,
            out var samples)
            ? ReactiveCounterCcImpactTimingRules.NormalizeSamples(samples).Length
            : 0;

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
                   SamuraiReactiveRuntimeRules.IsExactWolvesDenCurrentTarget(
                       localPlayer.EntityId,
                       signal.CasterEntityId,
                       current.Identity.EntityId)
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

        if (HasStatus(localPlayer, EnemyCombatConstants.PvPBindStatusId) ||
            !IsActionSpecificReady(SamuraiZantetsukenRules.ActionId))
        {
            return false;
        }

        // Selection is deterministic and ends before the intent is frozen.
        // After this point actor drift never falls through to another S-slot.
        if (!TryBuildExactCcZantetsukenCandidates(localPlayer, out var candidates))
            return false;

        var selectedIndex = SamuraiZantetsukenTargetSelectionRules
            .SelectBestEligibleTargetIndex(
                candidates.Select(static candidate => candidate.Candidate).ToArray());
        if (selectedIndex < 0) return false;

        var selected = candidates[selectedIndex];
        target = CreateFrozenTarget(
            context,
            selected.Candidate.EnemySlot,
            selected.Player,
            DarkKnightWolvesDenTargetKind.None);
        return true;
    }

    private bool HasAnyExactOwnSourceKuzushiForCollection(
        IPlayerCharacter localPlayer,
        SupportedPvPContext context)
    {
        if (!metadata.KuzushiVerified) return false;
        if (context == SupportedPvPContext.WolvesDen)
        {
            if (!TryResolveWolvesDenCurrentTarget(localPlayer, out var frozen))
                return false;
            var current = ResolveFrozenTarget(localPlayer, frozen);
            return current is not null &&
                   IsLiveTarget(current) &&
                   CountOwnSourceKuzushi(current, localPlayer.EntityId) == 1;
        }

        if (context != SupportedPvPContext.CrystallineConflict)
            return false;

        var exactOwnKuzushiObserved = false;
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
                !HasCoherentObjectTableIdentity(player))
            {
                return false;
            }

            if (IsLiveTarget(player) &&
                CountOwnSourceKuzushi(player, localPlayer.EntityId) == 1)
            {
                exactOwnKuzushiObserved = true;
            }
        }

        return exactOwnKuzushiObserved;
    }

    private bool TryBuildExactCcZantetsukenCandidates(
        IPlayerCharacter localPlayer,
        out List<ResolvedZantetsukenTargetCandidate> candidates)
    {
        candidates = [];
        if (!metadata.KuzushiVerified) return false;

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
                !HasCoherentObjectTableIdentity(player))
            {
                return false;
            }

            candidates.Add(new ResolvedZantetsukenTargetCandidate(
                new SamuraiZantetsukenTargetCandidate(
                    enemy.Slot,
                    new SamuraiReactiveCounterCcTarget(
                        player.GameObjectId,
                        player.EntityId,
                        player.ClassJob.RowId),
                    ExactCanonicalIdentity: true,
                    AliveAndTargetable: IsLiveTarget(player),
                    player.CurrentHp,
                    player.MaxHp,
                    OwnSourceKuzushiCount:
                        CountOwnSourceKuzushi(player, localPlayer.EntityId),
                    player.ShieldPercentage,
                    ExecuteBlockingProtectionCount:
                        CountExecuteBlockingProtections(player),
                    HasNativeRangeAndLineOfSight(
                        localPlayer,
                        player,
                        SamuraiZantetsukenRules.ActionId),
                    player.Position,
                    player.HitboxRadius),
                player));
        }

        return candidates.Count > 0;
    }

    private bool HasExactOwnSourceKuzushiOnFrozenTarget(
        IPlayerCharacter localPlayer,
        FrozenActionTarget frozen)
    {
        if (!metadata.KuzushiVerified) return false;
        var current = ResolveFrozenTarget(localPlayer, frozen);
        return current is not null &&
               IsLiveTarget(current) &&
               CountExecuteBlockingProtections(current) == 0 &&
               CountOwnSourceKuzushi(current, localPlayer.EntityId) == 1;
    }

    private bool IsZantetsukenCandidate(
        IPlayerCharacter localPlayer,
        IBattleChara target) =>
        IsLiveTarget(target) &&
        metadata.KuzushiVerified &&
        CountOwnSourceKuzushi(target, localPlayer.EntityId) == 1 &&
        CountExecuteBlockingProtections(target) == 0 &&
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
        float configuredSotenMaximumRangeYalms,
        PredictiveAttemptAuthorization? predictiveAuthorization)
    {
        var attempted = false;
        var attemptedAtMilliseconds = -1L;
        var attemptedEdgeDistanceYalms = float.NaN;
        var before = default(ClientActionAttemptFingerprint);
        var after = default(ClientActionAttemptFingerprint);
        try
        {
            var brakeBypass = actionId == SamuraiReactiveCounterCcRules.MineuchiActionId &&
                              predictiveAuthorization is { } mineuchiAuthorization
                ? new PredictiveCcBrakeBypassIntent(
                    actionId,
                    episode.Target.Identity.GameObjectId,
                    episode.Target.Identity.EntityId,
                    episode.Target.Identity.JobId,
                    mineuchiAuthorization.ProtectionStatusId,
                    mineuchiAuthorization.ScheduledProtectionEndAtMilliseconds,
                    mineuchiAuthorization.SafeLeadMilliseconds)
                : (PredictiveCcBrakeBypassIntent?)null;
            var returned = nearAssist.RunWithoutRedirect(
                () =>
                {
                    var target = ResolveFrozenTarget(localPlayer, episode.Target);
                    if (target is null ||
                        !HasCompleteMineuchiBlockerMetadata() ||
                        !IsActionSpecificReady(actionId) ||
                        !HasGlobalNativeBoundaryReadiness(localPlayer) ||
                        !TryGetEdgeDistance(localPlayer, target, out var edgeDistance) ||
                        !HasNativeRangeAndLineOfSight(localPlayer, target, actionId))
                    {
                        return false;
                    }

                    var currentProtection = ObserveExpectedProtection(
                        target,
                        episode.Signal.Kind);
                    if (currentProtection.Count > 1) return false;
                    if (currentProtection.Count == 1)
                    {
                        if (predictiveAuthorization is not { } authorization ||
                            !authorization.IsValidFor(actionId))
                        {
                            return false;
                        }

                        var statusId = currentProtection.StatusId;
                        var remainingMilliseconds =
                            currentProtection.RemainingMilliseconds;
                        if (actionId == SamuraiReactiveCounterCcRules.MineuchiActionId &&
                            !TryGetOnlyScheduledProtection(
                                target,
                                episode,
                                out statusId,
                                out remainingMilliseconds))
                        {
                            return false;
                        }

                        if (statusId != authorization.ProtectionStatusId ||
                            remainingMilliseconds <= 0 ||
                            !ReactiveCounterCcImpactTimingRules
                                .IsScheduledProtectionStillValid(
                                    authorization.ScheduledProtectionEndAtMilliseconds,
                                    Environment.TickCount64,
                                    remainingMilliseconds,
                                    authorization.SafeLeadMilliseconds))
                            return false;
                    }
                    else if (actionId ==
                                 SamuraiReactiveCounterCcRules.MineuchiActionId &&
                             CountMineuchiBlockingProtections(target) != 0)
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
                    attemptedAtMilliseconds = Environment.TickCount64;
                    attemptedEdgeDistanceYalms = edgeDistance;
                    if (SamuraiReactiveCounterCcRules.GetNativeInvocationKind(actionId) !=
                        SamuraiReactiveCounterCcNativeInvocationKind.TargetedUseAction)
                    {
                        return false;
                    }

                    // Both SAM stages are metadata-pinned hostile target
                    // abilities. The request never changes the visible target.
                    var accepted = actionManager->UseAction(
                        ActionType.Action,
                        actionId,
                        target.GameObjectId,
                        0,
                        ActionManager.UseActionMode.None,
                        0);

                    after = ClientActionAttemptBoundary.Capture(actionManager, actionId);
                    return accepted;
                },
                brakeBypass);
            var outcome = attempted
                ? ClientActionAttemptBoundaryRules.Classify(
                    returned,
                    actionId,
                    before,
                    after)
                : ClientActionAttemptOutcome.NotInvoked;
            var sourceSequence = outcome == ClientActionAttemptOutcome.ClientAccepted &&
                                 after.LastUsedActionSequence != 0 &&
                                 after.LastUsedActionSequence != before.LastUsedActionSequence
                ? after.LastUsedActionSequence
                : (ushort)0;
            return attempted
                ? new AttemptResult(
                    outcome,
                    true,
                    sourceSequence,
                    attemptedAtMilliseconds,
                    attemptedEdgeDistanceYalms)
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
        FrozenActionTarget frozen,
        SamuraiZantetsukenCollectionState collectionState)
    {
        var attempted = false;
        var before = default(ClientActionAttemptFingerprint);
        var after = default(ClientActionAttemptFingerprint);
        try
        {
            var returned = nearAssist.RunWithoutRedirect(() =>
            {
                var target = ResolveFrozenTarget(localPlayer, frozen);
                var exactOwnKuzushiPresent = target is not null &&
                    HasExactOwnSourceKuzushiOnFrozenTarget(localPlayer, frozen);
                var collectionReady =
                    SamuraiZantetsukenRules.CanSelectAndFreezeTarget(
                        collectionState,
                        new SamuraiZantetsukenCollectionObservation(
                            Enabled: true,
                            HardReset: false,
                            frozen.Context,
                            new TargetPressureActorIdentity(
                                localPlayer.GameObjectId,
                                localPlayer.EntityId),
                            exactOwnKuzushiPresent,
                            Environment.TickCount64));
                if (target is null ||
                    !IsLiveTarget(target) ||
                    !collectionReady ||
                    CountExecuteBlockingProtections(target) != 0 ||
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
                attempted,
                TerminalFailure: true);
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

    private static ProtectionObservation ObserveExpectedProtection(
        IBattleChara target,
        SamuraiReactiveProtectionKind kind)
    {
        var count = 0;
        var statusId = 0u;
        var remainingMilliseconds = 0L;
        foreach (var status in target.StatusList)
        {
            if (!SamuraiReactiveRuntimeRules.IsExpectedProtectionStatus(
                    kind,
                    status.StatusId))
            {
                continue;
            }

            count++;
            statusId = status.StatusId;
            remainingMilliseconds = ValidatedProtectionRemainingMilliseconds(
                status.StatusId,
                status.RemainingTime);
            if (count > 1)
                return new ProtectionObservation(count, 0, 0);
        }

        return new ProtectionObservation(
            count,
            count == 1 ? statusId : 0,
            count == 1 ? remainingMilliseconds : 0);
    }

    private static bool TryGetOnlyScheduledProtection(
        IBattleChara target,
        FrozenProtectionEpisode episode,
        out uint statusId,
        out long remainingMilliseconds)
    {
        statusId = 0;
        remainingMilliseconds = 0;
        var blockingProtectionCount = 0;
        var targetJobId = target.ClassJob.IsValid ? target.ClassJob.RowId : 0;
        foreach (var status in target.StatusList)
        {
            if (!CcImmunityBrakeActionCatalog.IsBlockerStatus(
                    CcImmunityBrakeBlockerFamily.StandardPurifyCc,
                    status.StatusId,
                    targetJobId))
            {
                continue;
            }

            blockingProtectionCount++;
            if (blockingProtectionCount > 1) return false;
            statusId = status.StatusId;
            remainingMilliseconds = ValidatedProtectionRemainingMilliseconds(
                status.StatusId,
                status.RemainingTime);
        }

        return blockingProtectionCount == 1 &&
               statusId == episode.ProtectionStatusId &&
               SamuraiReactiveRuntimeRules.IsExpectedProtectionStatus(
                   episode.Signal.Kind,
                   statusId) &&
               remainingMilliseconds > 0;
    }

    private static int CountMineuchiBlockingProtections(IBattleChara target)
    {
        var count = 0;
        var targetJobId = target.ClassJob.IsValid ? target.ClassJob.RowId : 0;
        foreach (var status in target.StatusList)
        {
            if (!CcImmunityBrakeActionCatalog.IsBlockerStatus(
                    CcImmunityBrakeBlockerFamily.StandardPurifyCc,
                    status.StatusId,
                    targetJobId))
            {
                continue;
            }

            count++;
        }

        return count;
    }

    private bool HasCompleteMineuchiBlockerMetadata()
    {
        var verifiedStatusIds = nearAssist.VerifiedCcBrakeStatusIds;
        return CcImmunityBrakeActionCatalog
            .GetBlockerStatusIds(CcImmunityBrakeBlockerFamily.StandardPurifyCc)
            .All(verifiedStatusIds.Contains);
    }

    private static long ValidatedProtectionRemainingMilliseconds(
        uint statusId,
        float remainingSeconds)
    {
        if (!CcProtectionStatusCatalog.TryGet(statusId, out var definition) ||
            !float.IsFinite(remainingSeconds) ||
            remainingSeconds <= 0f ||
            remainingSeconds > definition.MaximumRemainingTime)
        {
            return 0;
        }

        return Math.Max(
            1L,
            (long)Math.Ceiling((double)remainingSeconds * 1_000d));
    }

    private static int CountOwnSourceKuzushi(
        IBattleChara target,
        uint localEntityId)
    {
        if (localEntityId is 0 or 0xE0000000 or uint.MaxValue) return -1;

        var count = 0;
        foreach (var status in target.StatusList)
        {
            if (status.StatusId != SamuraiZantetsukenRules.KuzushiStatusId ||
                status.SourceId != localEntityId)
            {
                continue;
            }

            if (!SamuraiZantetsukenRules.IsExactCurrentOwnSourceKuzushi(
                    status.StatusId,
                    status.SourceId,
                    status.RemainingTime,
                    localEntityId))
            {
                // A stale, expired, or malformed own-source row invalidates
                // this snapshot instead of masquerading as a fresh proc.
                return -1;
            }

            count++;
            if (count > 1) return -1;
        }

        return count;
    }

    private static int CountExecuteBlockingProtections(IBattleChara target)
    {
        var count = 0;
        foreach (var status in target.StatusList)
        {
            if (!NinjaSeitonProtectionStatusCatalog.IsExecuteBlockingStatus(
                    status.StatusId))
            {
                continue;
            }

            count++;
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
        ResetZantetsukenIntent();
        zantetsukenCollectionState =
            SamuraiZantetsukenCollectionState.Initial;
        zantetsukenReadyEpochTerminal = false;
    }

    private void ResetZantetsukenIntent()
    {
        zantetsukenTarget = null;
        zantetsukenState = SamuraiZantetsukenState.Initial;
        zantetsukenRetryState = HeldActionRetryState.Initial;
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
            zantetsukenAutomaticHelperEnabled,
            metadata.ZantetsukenWorkflowVerified,
            StoredTimingSampleCount(
                SamuraiReactiveCounterCcRules.SotenActionId),
            StoredTimingSampleCount(
                SamuraiReactiveCounterCcRules.MineuchiActionId),
            predictiveSotenLeadMilliseconds,
            predictiveMineuchiLeadMilliseconds,
            episode?.SotenActionEffectConfirmed ?? false,
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

    private static long SaturatingAdd(long left, long right) =>
        right > 0 && left > long.MaxValue - right
            ? long.MaxValue
            : left + right;

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

    private readonly record struct ResolvedZantetsukenTargetCandidate(
        SamuraiZantetsukenTargetCandidate Candidate,
        IPlayerCharacter Player);

    private readonly record struct FrozenProtectionEpisode(
        SamuraiReactiveProtectionSignal Signal,
        FrozenActionTarget Target,
        int GameplayKeyToken,
        bool ProtectionObserved,
        uint ProtectionStatusId,
        long ScheduledProtectionEndAtMilliseconds,
        ushort SotenSourceSequence,
        bool SotenActionEffectConfirmed);

    private readonly record struct ProtectionObservation(
        int Count,
        uint StatusId,
        long RemainingMilliseconds);

    private readonly record struct PredictiveAttemptAuthorization(
        uint ActionId,
        uint ProtectionStatusId,
        long ScheduledProtectionEndAtMilliseconds,
        int SafeLeadMilliseconds)
    {
        internal bool IsValidFor(uint actionId) =>
            ActionId == actionId &&
            actionId is SamuraiReactiveCounterCcRules.SotenActionId or
                SamuraiReactiveCounterCcRules.MineuchiActionId &&
            PredictiveCcBrakeBypassRules.IsSupportedProtectionStatus(
                ProtectionStatusId) &&
            ScheduledProtectionEndAtMilliseconds > 0 &&
            SafeLeadMilliseconds >=
                ReactiveCounterCcImpactTimingRules.MinimumUsefulLeadMilliseconds;
    }

    private readonly record struct PendingTimingAttempt(
        uint ActionId,
        uint TargetEntityId,
        ushort SourceSequence,
        long AttemptedAtMilliseconds,
        float EdgeDistanceYalms,
        ulong EpisodeToken);

    private readonly record struct TimingEffectIdentity(
        uint ActionId,
        uint TargetEntityId,
        ushort SourceSequence);

    private readonly record struct AttemptResult(
        ClientActionAttemptOutcome Outcome,
        bool Attempted,
        ushort SourceSequence = 0,
        long AttemptedAtMilliseconds = -1,
        float EdgeDistanceYalms = -1f,
        bool TerminalFailure = false);
}
