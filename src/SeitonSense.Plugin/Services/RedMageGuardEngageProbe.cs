using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using SeitonSense.Core;

namespace SeitonSense.Plugin.Services;

internal sealed record RedMageGuardEngageProbeSnapshot(
    RedMageGuardEngageDecisionReason Reason,
    SupportedPvPContext Context,
    uint ResolvedActionId,
    uint ResolvedComboCarrierActionId,
    bool CorpsReady,
    bool MeleeStarterReady,
    int CandidateCount,
    int EnemySlot,
    ulong TargetGameObjectId,
    uint TargetEntityId,
    int GuardRemainingMilliseconds,
    ulong GuardEpisodeToken,
    long LeaseRemainingMilliseconds,
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
    internal static RedMageGuardEngageProbeSnapshot Initial { get; } = new(
        RedMageGuardEngageDecisionReason.None,
        SupportedPvPContext.None,
        0,
        0,
        false,
        false,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
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
/// Default-off RDM held helper. One exact absent-to-present enemy Guard episode
/// can freeze one Corps-a-corps request for no longer than Guard's first second.
/// The melee starter is readiness proof only; this probe never executes it.
/// </summary>
internal sealed class RedMageGuardEngageProbe
{
    private readonly IClientState clientState;
    private readonly IDutyState dutyState;
    private readonly IObjectTable objectTable;
    private readonly ITargetManager targetManager;
    private readonly NearAssistRedirector nearAssist;
    private readonly IPluginLog log;
    private readonly Dictionary<TargetPressureActorIdentity, RedMageGuardEpisodeState>
        guardEpisodes = [];

    private RedMageGuardEngageProbeSnapshot snapshot =
        RedMageGuardEngageProbeSnapshot.Initial;
    private FrozenGuardEngageRetry? frozenRetry;
    private VirtualKey ownedHeldKey = VirtualKey.NO_KEY;
    private VirtualKey terminalHeldKey = VirtualKey.NO_KEY;
    private long nextIntentEpochToken;
    private long attemptCount;
    private long acceptedCount;
    private long targetConfirmedCount;
    private long rejectedCount;
    private long unknownCount;
    private long nextErrorLogAt;
    private string lastEvent = "Waiting";

    internal RedMageGuardEngageProbe(
        IClientState clientState,
        IDutyState dutyState,
        IObjectTable objectTable,
        ITargetManager targetManager,
        NearAssistRedirector nearAssist,
        IPluginLog log)
    {
        this.clientState = clientState;
        this.dutyState = dutyState;
        this.objectTable = objectTable;
        this.targetManager = targetManager;
        this.nearAssist = nearAssist;
        this.log = log;
    }

    internal RedMageGuardEngageProbeSnapshot Snapshot => Volatile.Read(ref snapshot);

    internal RedMageGuardEngageProbeSnapshot Observe(
        IPlayerCharacter? localPlayer,
        SupportedPvPContext context,
        bool wolvesDenTestingEnabled,
        bool configurationEnabled,
        bool metadataVerified,
        bool wolvesDenStrikingDummyMetadataVerified,
        int minimumHpPercent,
        int minimumMpPercent,
        bool actionHelpersSuppressedByGuard,
        bool higherPriorityClaimed,
        EmergencyActionInputFrame inputFrame,
        long nowMilliseconds,
        bool hardReset = false)
    {
        if (hardReset)
            ResetRuntime(clearTerminalKey: true);

        if (terminalHeldKey != VirtualKey.NO_KEY &&
            !inputFrame.IsGameplayKeyPhysicallyDown(terminalHeldKey))
        {
            terminalHeldKey = VirtualKey.NO_KEY;
        }
        if (ownedHeldKey != VirtualKey.NO_KEY &&
            !inputFrame.IsGameplayKeyPhysicallyDown(ownedHeldKey))
        {
            ownedHeldKey = VirtualKey.NO_KEY;
        }

        var localIdentity = HasValidNativeIdentity(localPlayer)
            ? new TargetPressureActorIdentity(
                localPlayer!.GameObjectId,
                localPlayer.EntityId)
            : default;
        var localJobId = localPlayer?.ClassJob.IsValid == true
            ? localPlayer.ClassJob.RowId
            : 0;
        var localAliveAndTargetable = IsLivePlayer(localPlayer) &&
                                      localPlayer!.IsTargetable;
        var supportedContext = context == SupportedPvPContext.CrystallineConflict ||
                               (context == SupportedPvPContext.WolvesDen &&
                                wolvesDenTestingEnabled);
        var trackingContextValid = configurationEnabled &&
                                   supportedContext &&
                                   localIdentity.IsValid &&
                                   localAliveAndTargetable &&
                                   localJobId == RedMageGuardEngageRules.RedMageJobId &&
                                   metadataVerified &&
                                   !hardReset;

        var resolvedActionId = 0u;
        var resolvedComboCarrierActionId = 0u;
        var corpsReady = false;
        var meleeStarterReady = false;
        var nativeBoundaryReady = false;
        var actionStateKnown = trackingContextValid &&
                               TryReadActionState(
                                   localPlayer!,
                                   out resolvedActionId,
                                   out corpsReady,
                                   out resolvedComboCarrierActionId,
                                   out meleeStarterReady,
                                   out nativeBoundaryReady);

        var candidateResolution = trackingContextValid
            ? "No exact target view"
            : "Feature context inactive";
        IReadOnlyList<RedMageGuardEngageCandidate> candidates = [];
        var wolvesDenKind = DarkKnightWolvesDenTargetKind.None;
        if (trackingContextValid)
        {
            candidates = ResolveExactCandidates(
                localPlayer!,
                context,
                wolvesDenStrikingDummyMetadataVerified,
                out wolvesDenKind,
                out candidateResolution);
        }
        else
        {
            guardEpisodes.Clear();
            frozenRetry = null;
            ownedHeldKey = VirtualKey.NO_KEY;
        }

        var input = inputFrame.Snapshot;
        var ownedKeyEligible = ownedHeldKey != VirtualKey.NO_KEY &&
                               input.ProbeSucceeded &&
                               !input.IsTextInputActive &&
                               inputFrame.IsGameplayKeyPhysicallyDown(ownedHeldKey);
        var heldKeyEligible = terminalHeldKey == VirtualKey.NO_KEY &&
                              (ownedKeyEligible || input.HeldGameplayKeyEligible);
        var heldKey = ownedKeyEligible
            ? ownedHeldKey
            : input.HeldGameplayKey;

        var observation = new RedMageGuardEngageObservation(
            configurationEnabled,
            context,
            localJobId,
            localIdentity,
            localAliveAndTargetable,
            localPlayer?.CurrentHp ?? 0,
            localPlayer?.MaxHp ?? 0,
            localPlayer?.CurrentMp ?? 0,
            localPlayer?.MaxMp ?? 0,
            minimumHpPercent,
            minimumMpPercent,
            metadataVerified,
            actionHelpersSuppressedByGuard,
            higherPriorityClaimed || inputFrame.IsConsumed,
            input.ProbeSucceeded,
            input.IsTextInputActive,
            heldKeyEligible,
            (int)heldKey,
            actionStateKnown ? resolvedActionId : 0,
            actionStateKnown && corpsReady,
            actionStateKnown ? resolvedComboCarrierActionId : 0,
            actionStateKnown && meleeStarterReady,
            nowMilliseconds,
            candidates,
            hardReset);
        var decision = RedMageGuardEngageRules.Evaluate(observation);

        var inputClaimed = false;
        HeldCastCancellationRequest? castCancellationRequest = null;
        var attempted = false;
        var accepted = false;
        var targetConfirmed = false;
        RedMageGuardEngageCandidate? observedCandidate = null;

        if (frozenRetry is { } retry)
        {
            var exactCandidate = FindFrozenCandidate(candidates, retry.Intent);
            observedCandidate = exactCandidate;
            var exactContext = trackingContextValid &&
                               localIdentity == retry.LocalPlayer &&
                               context == retry.Intent.Context &&
                               input.ProbeSucceeded &&
                               !input.IsTextInputActive &&
                               inputFrame.IsGameplayKeyPhysicallyDown(retry.HeldKey) &&
                               !actionHelpersSuppressedByGuard &&
                               RedMageGuardEngageRules.MeetsInclusivePercent(
                                   localPlayer!.CurrentHp,
                                   localPlayer.MaxHp,
                                   minimumHpPercent) &&
                               localPlayer.MaxMp == RedMageGuardEngageRules.ExpectedMaximumPvpMp &&
                               RedMageGuardEngageRules.MeetsInclusivePercent(
                                   localPlayer.CurrentMp,
                                   localPlayer.MaxMp,
                                   minimumMpPercent);
            var exactIntentValid = exactContext &&
                                   actionStateKnown &&
                                   exactCandidate is { } candidate &&
                                   RedMageGuardEngageRules.CanUseFrozenIntent(
                                       retry.Intent,
                                       candidate,
                                       nowMilliseconds,
                                       exactHeldKeyStillDown: true,
                                       resolvedActionId,
                                       corpsReady,
                                       resolvedComboCarrierActionId,
                                       meleeStarterReady);
            if (!exactIntentValid)
            {
                frozenRetry = null;
                lastEvent = nowMilliseconds > retry.Intent.ExpiresAtMilliseconds
                    ? "Frozen Guard engage expired at the one-second boundary"
                    : "Frozen Guard engage cancelled by exact context, actor, status, range, resource, or threshold drift";
            }
            else if (!higherPriorityClaimed && !inputFrame.IsConsumed)
            {
                inputClaimed = true;
                inputFrame.Consume();
                if (!nativeBoundaryReady)
                {
                    castCancellationRequest = CreateCastCancellationRequest(
                        localPlayer!,
                        retry,
                        nowMilliseconds);
                    lastEvent = "Frozen Guard engage waiting inside its one-second native-boundary lease";
                }
                else if (!HeldActionRetryRules.CanAttemptFrozenIntent(
                             retry.Retry,
                             nowMilliseconds))
                {
                    lastEvent = "Frozen Guard engage retaining exact retry throttle priority";
                }
                else
                {
                    var outcome = TryUseCorpsOnce(
                        localPlayer!,
                        retry,
                        wolvesDenTestingEnabled,
                        configurationEnabled,
                        metadataVerified,
                        wolvesDenStrikingDummyMetadataVerified,
                        minimumHpPercent,
                        minimumMpPercent,
                        inputFrame,
                        out attempted,
                        out observedCandidate,
                        out targetConfirmed);
                    accepted = outcome == ClientActionAttemptOutcome.ClientAccepted;
                    CompleteAttempt(retry, outcome, nowMilliseconds);
                    lastEvent = DescribeAttempt(retry, outcome, targetConfirmed);
                }
            }
        }
        else if (decision.ShouldDispatch && decision.Intent is { } intent)
        {
            var selected = decision.SelectedCandidateIndex >= 0 &&
                           decision.SelectedCandidateIndex < candidates.Count
                ? candidates[decision.SelectedCandidateIndex]
                : (RedMageGuardEngageCandidate?)null;
            if (selected is { } exact && SpendEpisode(exact))
            {
                observedCandidate = exact;
                var newRetry = new FrozenGuardEngageRetry(
                    intent,
                    localIdentity,
                    heldKey,
                    wolvesDenKind,
                    NextIntentEpochToken(),
                    HeldActionRetryState.Initial);
                frozenRetry = newRetry;
                inputClaimed = true;
                inputFrame.Consume();
                if (!nativeBoundaryReady)
                {
                    castCancellationRequest = CreateCastCancellationRequest(
                        localPlayer!,
                        newRetry,
                        nowMilliseconds);
                    lastEvent = "Fresh Guard engage frozen before the native boundary";
                }
                else
                {
                    var outcome = TryUseCorpsOnce(
                        localPlayer!,
                        newRetry,
                        wolvesDenTestingEnabled,
                        configurationEnabled,
                        metadataVerified,
                        wolvesDenStrikingDummyMetadataVerified,
                        minimumHpPercent,
                        minimumMpPercent,
                        inputFrame,
                        out attempted,
                        out observedCandidate,
                        out targetConfirmed);
                    accepted = outcome == ClientActionAttemptOutcome.ClientAccepted;
                    CompleteAttempt(newRetry, outcome, nowMilliseconds);
                    if (outcome == ClientActionAttemptOutcome.SoftUnavailable)
                    {
                        castCancellationRequest = CreateCastCancellationRequest(
                            localPlayer!,
                            newRetry,
                            nowMilliseconds);
                    }
                    lastEvent = DescribeAttempt(newRetry, outcome, targetConfirmed);
                }
            }
            else
            {
                lastEvent = "Fresh Guard episode could not be spent exactly; no alternate target";
            }
        }

        if (attempted) Interlocked.Increment(ref attemptCount);
        if (accepted) Interlocked.Increment(ref acceptedCount);
        if (targetConfirmed) Interlocked.Increment(ref targetConfirmedCount);

        var selectedCandidate = observedCandidate ??
                                (decision.SelectedCandidateIndex >= 0 &&
                                 decision.SelectedCandidateIndex < candidates.Count
                                    ? candidates[decision.SelectedCandidateIndex]
                                    : (RedMageGuardEngageCandidate?)null);
        var leaseRemaining = frozenRetry is { } active
            ? Math.Max(0, active.Intent.ExpiresAtMilliseconds - nowMilliseconds)
            : 0;
        var result = new RedMageGuardEngageProbeSnapshot(
            decision.Reason,
            context,
            resolvedActionId,
            resolvedComboCarrierActionId,
            corpsReady,
            meleeStarterReady,
            candidates.Count(static candidate => candidate.GuardEpisodeUnspent),
            selectedCandidate?.EnemySlot ?? 0,
            selectedCandidate?.Actor.GameObjectId ?? 0,
            selectedCandidate?.Actor.EntityId ?? 0,
            selectedCandidate?.GuardRemainingMilliseconds ?? 0,
            selectedCandidate?.GuardEpisodeToken ?? 0,
            leaseRemaining,
            frozenRetry?.HeldKey ??
            (ownedHeldKey != VirtualKey.NO_KEY ? ownedHeldKey : heldKey),
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
            candidateResolution,
            lastEvent);
        Volatile.Write(ref snapshot, result);
        return result;
    }

    internal void Reset()
    {
        ResetRuntime(clearTerminalKey: true);
        lastEvent = "Reset";
        Volatile.Write(ref snapshot, RedMageGuardEngageProbeSnapshot.Initial with
        {
            AttemptCount = Interlocked.Read(ref attemptCount),
            AcceptedCount = Interlocked.Read(ref acceptedCount),
            TargetConfirmedCount = Interlocked.Read(ref targetConfirmedCount),
            RejectedCount = Interlocked.Read(ref rejectedCount),
            UnknownCount = Interlocked.Read(ref unknownCount),
            LastEvent = lastEvent,
        });
    }

    internal RedMageGuardEngageProbeSnapshot FailClosed()
    {
        var failedKey = frozenRetry?.HeldKey ?? terminalHeldKey;
        ResetRuntime(clearTerminalKey: true);
        terminalHeldKey = failedKey;
        lastEvent = "Failed closed";
        var result = RedMageGuardEngageProbeSnapshot.Initial with
        {
            Reason = RedMageGuardEngageDecisionReason.HardReset,
            HeldGameplayKey = terminalHeldKey,
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

    private IReadOnlyList<RedMageGuardEngageCandidate> ResolveExactCandidates(
        IPlayerCharacter localPlayer,
        SupportedPvPContext context,
        bool wolvesDenStrikingDummyMetadataVerified,
        out DarkKnightWolvesDenTargetKind wolvesDenKind,
        out string resolution)
    {
        wolvesDenKind = DarkKnightWolvesDenTargetKind.None;
        var resolved = new List<(int Slot, IBattleChara Target, DarkKnightWolvesDenTargetKind Kind)>();
        if (context == SupportedPvPContext.CrystallineConflict)
        {
            var seenObjects = new HashSet<ulong>();
            var seenEntities = new HashSet<uint>();
            var seenAddresses = new HashSet<nint>();
            for (var slot = EnemySlotRules.FirstSlot; slot <= EnemySlotRules.LastSlot; slot++)
            {
                var target = EnemySlotResolver.Resolve(objectTable, slot);
                if (!HasValidNativeIdentity(target))
                    continue;

                var tableTarget = HasValidNativeIdentity(target)
                    ? objectTable.SearchByEntityId(target!.EntityId) as IPlayerCharacter
                    : null;
                if (!HasSameNativeIdentity(target, tableTarget) ||
                    !seenObjects.Add(target!.GameObjectId) ||
                    !seenEntities.Add(target.EntityId) ||
                    !seenAddresses.Add(target.Address))
                {
                    resolution = $"CC resolved S{slot} identity is ambiguous or overlaps another slot";
                    guardEpisodes.Clear();
                    return [];
                }

                resolved.Add((slot, target, DarkKnightWolvesDenTargetKind.None));
            }
        }
        else if (context == SupportedPvPContext.WolvesDen &&
                 DarkKnightWolvesDenCurrentTargetResolver.TryResolveExactCurrentHardTarget(
                     objectTable,
                     wolvesDenStrikingDummyMetadataVerified,
                     localPlayer,
                     out var target,
                     out _,
                     out wolvesDenKind,
                     out _) &&
                 target is not null)
        {
            resolved.Add((0, target, wolvesDenKind));
        }
        else
        {
            resolution = "Wolves' Den requires the exact current duel target";
            guardEpisodes.Clear();
            return [];
        }

        var activeActors = new HashSet<TargetPressureActorIdentity>();
        var candidates = new List<RedMageGuardEngageCandidate>(resolved.Count);
        foreach (var (slot, target, _) in resolved)
        {
            var identity = new TargetPressureActorIdentity(
                target.GameObjectId,
                target.EntityId);
            if (!identity.IsValid || !activeActors.Add(identity))
            {
                resolution = "Resolved target identities overlap";
                guardEpisodes.Clear();
                return [];
            }

            var candidate = BuildExactCandidate(
                localPlayer,
                context,
                slot,
                identity,
                wolvesDenStrikingDummyMetadataVerified,
                observeEpisode: true);
            if (candidate is not { } exact)
            {
                resolution = "Exact target revalidation failed";
                guardEpisodes.Clear();
                return [];
            }
            candidates.Add(exact);
        }

        foreach (var stale in guardEpisodes.Keys.Where(actor => !activeActors.Contains(actor)).ToArray())
            guardEpisodes.Remove(stale);

        resolution = context == SupportedPvPContext.CrystallineConflict
            ? candidates.Count == 0
                ? "No exact canonical CC enemy slot resolved"
                : $"Exact independently resolved CC slots: {candidates.Count}"
            : "Exact current Wolves' Den duel target";
        return candidates;
    }

    private unsafe RedMageGuardEngageCandidate? BuildExactCandidate(
        IPlayerCharacter localPlayer,
        SupportedPvPContext context,
        int enemySlot,
        TargetPressureActorIdentity expectedTarget,
        bool wolvesDenStrikingDummyMetadataVerified,
        bool observeEpisode)
    {
        if (!HasValidNativeIdentity(localPlayer) || !expectedTarget.IsValid)
            return null;

        if (!TryResolveFrozenTarget(
                localPlayer,
                context,
                enemySlot,
                expectedTarget,
                wolvesDenStrikingDummyMetadataVerified,
                out var target,
                out _))
        {
            return null;
        }

        var guard = ScanGuard(target!);
        guardEpisodes.TryGetValue(expectedTarget, out var episode);
        if (observeEpisode)
        {
            episode = RedMageGuardEngageRules.ObserveGuardEpisode(
                episode,
                guard.Observation);
            guardEpisodes[expectedTarget] = episode;
        }

        var sourceObject = GetNativeObject(localPlayer);
        var targetObject = GetNativeObject(target);
        var actionManager = ActionManager.Instance();
        var nativePointersValid = sourceObject != null && targetObject != null;
        var targetActionReady = nativePointersValid &&
                                actionManager != null &&
                                actionManager->GetActionStatus(
                                    ActionType.Action,
                                    RedMageGuardEngageRules.CorpsACorpsActionId,
                                    expectedTarget.GameObjectId,
                                    checkRecastActive: true,
                                    checkCastingActive: false) == 0;
        var rangeAndLineOfSight = nativePointersValid &&
                                  SeitonRangeRules.HasNativeRangeAndLineOfSight(
                                      ActionManager.GetActionInRangeOrLoS(
                                          RedMageGuardEngageRules.CorpsACorpsActionId,
                                          sourceObject,
                                          targetObject));

        return new RedMageGuardEngageCandidate(
            context,
            enemySlot,
            expectedTarget,
            ExactCanonicalIdentity: true,
            Alive: IsLiveBattleChara(target),
            Targetable: target!.IsTargetable,
            target.CurrentHp,
            target.MaxHp,
            guard.ExactStatusCount,
            guard.RemainingMilliseconds,
            episode.CurrentEpisodeToken,
            episode.HasUnspentEpisode,
            guard.HasOtherReviewedProtection,
            targetActionReady,
            rangeAndLineOfSight);
    }

    private unsafe ClientActionAttemptOutcome TryUseCorpsOnce(
        IPlayerCharacter expectedLocalPlayer,
        FrozenGuardEngageRetry frozen,
        bool wolvesDenTestingEnabled,
        bool configurationEnabled,
        bool metadataVerified,
        bool wolvesDenStrikingDummyMetadataVerified,
        int minimumHpPercent,
        int minimumMpPercent,
        EmergencyActionInputFrame inputFrame,
        out bool attempted,
        out RedMageGuardEngageCandidate? boundaryCandidate,
        out bool hardTargetConfirmed)
    {
        attempted = false;
        boundaryCandidate = null;
        hardTargetConfirmed = false;
        var attemptedAtBoundary = false;
        var softUnavailable = false;
        RedMageGuardEngageCandidate? candidateAtBoundary = null;
        var before = default(ClientActionAttemptFingerprint);
        var after = default(ClientActionAttemptFingerprint);
        try
        {
            var clientReturnedTrue = nearAssist.RunWithoutRedirect(() =>
            {
                var now = Environment.TickCount64;
                var currentLocal = objectTable.LocalPlayer;
                if (!configurationEnabled ||
                    !metadataVerified ||
                    !HasSameNativeIdentity(expectedLocalPlayer, currentLocal) ||
                    currentLocal!.ClassJob.IsValid != true ||
                    currentLocal.ClassJob.RowId != RedMageGuardEngageRules.RedMageJobId ||
                    !IsLivePlayer(currentLocal) ||
                    !currentLocal.IsTargetable ||
                    ResolveCurrentContext(wolvesDenTestingEnabled) != frozen.Intent.Context ||
                    now > frozen.Intent.ExpiresAtMilliseconds ||
                    !inputFrame.IsGameplayKeyPhysicallyDown(frozen.HeldKey) ||
                    IsCurrentlySuppressedByGuard(currentLocal, now) ||
                    !RedMageGuardEngageRules.MeetsInclusivePercent(
                        currentLocal.CurrentHp,
                        currentLocal.MaxHp,
                        minimumHpPercent) ||
                    currentLocal.MaxMp != RedMageGuardEngageRules.ExpectedMaximumPvpMp ||
                    !RedMageGuardEngageRules.MeetsInclusivePercent(
                        currentLocal.CurrentMp,
                        currentLocal.MaxMp,
                        minimumMpPercent))
                {
                    return false;
                }

                if (!TryReadActionState(
                        currentLocal,
                        out var actionId,
                        out var corpsReady,
                        out var comboActionId,
                        out var meleeStarterReady,
                        out var nativeBoundaryReady) ||
                    actionId != frozen.Intent.ActionId ||
                    comboActionId != frozen.Intent.ComboCarrierActionId ||
                    !corpsReady ||
                    !meleeStarterReady)
                {
                    return false;
                }
                if (!nativeBoundaryReady)
                {
                    softUnavailable = true;
                    return false;
                }

                var candidate = BuildExactCandidate(
                    currentLocal,
                    frozen.Intent.Context,
                    frozen.Intent.EnemySlot,
                    frozen.Intent.Target,
                    wolvesDenStrikingDummyMetadataVerified,
                    observeEpisode: false);
                candidateAtBoundary = candidate;
                if (candidate is not { } exact ||
                    (frozen.Intent.Context == SupportedPvPContext.WolvesDen &&
                     (!TryResolveFrozenTarget(
                         currentLocal,
                         frozen.Intent.Context,
                         frozen.Intent.EnemySlot,
                         frozen.Intent.Target,
                         wolvesDenStrikingDummyMetadataVerified,
                         out _,
                         out var boundaryKind) ||
                      boundaryKind != frozen.WolvesDenKind)) ||
                    !RedMageGuardEngageRules.CanUseFrozenIntent(
                        frozen.Intent,
                        exact,
                        now,
                        exactHeldKeyStillDown: true,
                        actionId,
                        corpsReady,
                        comboActionId,
                        meleeStarterReady))
                {
                    return false;
                }

                var actionManager = ActionManager.Instance();
                if (actionManager == null ||
                    actionManager->GetActionStatus(
                        ActionType.Action,
                        frozen.Intent.ActionId,
                        frozen.Intent.Target.GameObjectId,
                        checkRecastActive: true,
                        checkCastingActive: true) != 0)
                {
                    return false;
                }

                before = ClientActionAttemptBoundary.Capture(
                    actionManager,
                    frozen.Intent.ActionId);
                attemptedAtBoundary = true;
                var accepted = actionManager->UseAction(
                    ActionType.Action,
                    frozen.Intent.ActionId,
                    frozen.Intent.Target.GameObjectId,
                    0,
                    ActionManager.UseActionMode.None,
                    0);
                after = ClientActionAttemptBoundary.Capture(
                    actionManager,
                    frozen.Intent.ActionId);
                return accepted;
            });

            var outcome = attemptedAtBoundary
                ? ClientActionAttemptBoundaryRules.Classify(
                    clientReturnedTrue,
                    frozen.Intent.ActionId,
                    before,
                    after)
                : softUnavailable
                    ? ClientActionAttemptOutcome.SoftUnavailable
                    : ClientActionAttemptOutcome.NotInvoked;
            if (outcome == ClientActionAttemptOutcome.ClientAccepted)
                hardTargetConfirmed = TrySetExactHardTargetOnce(
                    frozen,
                    wolvesDenStrikingDummyMetadataVerified);
            return outcome;
        }
        catch (Exception exception)
        {
            LogFailure(exception, "RDM Guard engage native boundary ended ambiguously");
            return attemptedAtBoundary
                ? ClientActionAttemptOutcome.AcceptanceUnknown
                : softUnavailable
                    ? ClientActionAttemptOutcome.SoftUnavailable
                    : ClientActionAttemptOutcome.NotInvoked;
        }
        finally
        {
            attempted = attemptedAtBoundary;
            boundaryCandidate = candidateAtBoundary;
        }
    }

    private bool TrySetExactHardTargetOnce(
        FrozenGuardEngageRetry frozen,
        bool wolvesDenStrikingDummyMetadataVerified)
    {
        try
        {
            var localPlayer = objectTable.LocalPlayer;
            if (!TryResolveFrozenTarget(
                    localPlayer,
                    frozen.Intent.Context,
                    frozen.Intent.EnemySlot,
                    frozen.Intent.Target,
                    wolvesDenStrikingDummyMetadataVerified,
                    out var target,
                    out var kind) ||
                (frozen.Intent.Context == SupportedPvPContext.WolvesDen &&
                 kind != frozen.WolvesDenKind) ||
                !IsLiveBattleChara(target) ||
                !target!.IsTargetable)
            {
                return false;
            }

            targetManager.Target = target;
            return MatchesExactTarget(targetManager.Target, target);
        }
        catch (Exception exception)
        {
            LogFailure(exception, "accepted RDM Guard engage hard-target setter failed terminally");
            return false;
        }
    }

    private bool TryResolveFrozenTarget(
        IPlayerCharacter? localPlayer,
        SupportedPvPContext context,
        int enemySlot,
        TargetPressureActorIdentity expectedTarget,
        bool wolvesDenStrikingDummyMetadataVerified,
        out IBattleChara? target,
        out DarkKnightWolvesDenTargetKind kind)
    {
        target = null;
        kind = DarkKnightWolvesDenTargetKind.None;
        if (!expectedTarget.IsValid) return false;

        if (context == SupportedPvPContext.CrystallineConflict)
        {
            var exact = EnemySlotResolver.Resolve(objectTable, enemySlot);
            var tableTarget = HasValidNativeIdentity(exact)
                ? objectTable.SearchByEntityId(exact!.EntityId) as IPlayerCharacter
                : null;
            if (!HasSameNativeIdentity(exact, tableTarget) ||
                exact!.GameObjectId != expectedTarget.GameObjectId ||
                exact.EntityId != expectedTarget.EntityId)
            {
                return false;
            }
            target = exact;
            return true;
        }

        if (context != SupportedPvPContext.WolvesDen ||
            !DarkKnightWolvesDenCurrentTargetResolver.TryResolveExactCurrentHardTarget(
                objectTable,
                wolvesDenStrikingDummyMetadataVerified,
                localPlayer,
                out var current,
                out var identity,
                out kind,
                out _) ||
            current is null ||
            identity != expectedTarget)
        {
            return false;
        }

        target = current;
        return true;
    }

    private static unsafe bool TryReadActionState(
        IPlayerCharacter localPlayer,
        out uint resolvedActionId,
        out bool corpsReady,
        out uint resolvedComboCarrierActionId,
        out bool meleeStarterReady,
        out bool nativeBoundaryReady)
    {
        resolvedActionId = 0;
        corpsReady = false;
        resolvedComboCarrierActionId = 0;
        meleeStarterReady = false;
        nativeBoundaryReady = false;
        if (!HasValidNativeIdentity(localPlayer)) return false;

        var actionManager = ActionManager.Instance();
        if (actionManager == null) return false;
        resolvedActionId = actionManager->GetAdjustedActionId(
            RedMageGuardEngageRules.CorpsACorpsActionId);
        resolvedComboCarrierActionId = actionManager->GetAdjustedActionId(
            RedMageGuardEngageRules.MeleeComboCarrierActionId);
        if (resolvedActionId == RedMageGuardEngageRules.CorpsACorpsActionId)
        {
            corpsReady = !HasActiveStatus(localPlayer, EnemyCombatConstants.PvPBindStatusId) &&
                          actionManager->IsActionOffCooldown(
                              ActionType.Action,
                              resolvedActionId) &&
                          actionManager->CheckActionResources(
                              ActionType.Action,
                              resolvedActionId) == 0;
        }
        if (resolvedComboCarrierActionId ==
            RedMageGuardEngageRules.MeleeComboCarrierActionId)
        {
            meleeStarterReady = actionManager->IsActionOffCooldown(
                                    ActionType.Action,
                                    resolvedComboCarrierActionId) &&
                                actionManager->CheckActionResources(
                                    ActionType.Action,
                                    resolvedComboCarrierActionId) == 0;
        }
        nativeBoundaryReady = HeldActionRetryRules.IsNativeBoundaryNearQueueable(
            actionManager->AnimationLock,
            localPlayer.IsCasting,
            actionManager->CastActionId,
            actionManager->ActionQueued);
        return true;
    }

    private static GuardScan ScanGuard(IBattleChara target)
    {
        var exactCount = 0;
        var remainingMilliseconds = 0;
        var telemetryValid = true;
        var hasOtherProtection = false;
        foreach (var status in target.StatusList)
        {
            var protection = SmartActionProtectionRules.ClassifyExactStatus(status.StatusId);
            if (protection != SmartActionProtectionKind.None &&
                protection != SmartActionProtectionKind.Guard &&
                (!float.IsFinite(status.RemainingTime) || status.RemainingTime > 0f))
            {
                hasOtherProtection = true;
            }

            if (!RedMageGuardEngageRules.IsExactGuardStatus(status.StatusId)) continue;
            exactCount++;
            if (!float.IsFinite(status.RemainingTime) ||
                status.RemainingTime <= 0f ||
                status.RemainingTime * 1_000f >
                RedMageGuardEngageRules.GuardTelemetryCeilingMilliseconds)
            {
                telemetryValid = false;
                continue;
            }
            remainingMilliseconds = (int)Math.Floor(status.RemainingTime * 1_000f);
        }

        var observation = exactCount switch
        {
            0 => RedMageGuardObservationKind.Absent,
            1 when telemetryValid => RedMageGuardObservationKind.ExactActive,
            _ => RedMageGuardObservationKind.Ambiguous,
        };
        if (observation != RedMageGuardObservationKind.ExactActive)
            remainingMilliseconds = 0;
        return new GuardScan(
            observation,
            exactCount,
            remainingMilliseconds,
            hasOtherProtection);
    }

    private bool SpendEpisode(RedMageGuardEngageCandidate candidate)
    {
        if (!guardEpisodes.TryGetValue(candidate.Actor, out var state) ||
            !RedMageGuardEngageRules.TrySpendGuardEpisode(
                state,
                candidate.GuardEpisodeToken,
                out var spent))
        {
            return false;
        }
        guardEpisodes[candidate.Actor] = spent;
        return true;
    }

    private static RedMageGuardEngageCandidate? FindFrozenCandidate(
        IReadOnlyList<RedMageGuardEngageCandidate> candidates,
        RedMageGuardEngageIntent intent)
    {
        RedMageGuardEngageCandidate? found = null;
        foreach (var candidate in candidates)
        {
            if (candidate.Context != intent.Context ||
                candidate.EnemySlot != intent.EnemySlot ||
                candidate.Actor != intent.Target)
            {
                continue;
            }
            if (found is not null) return null;
            found = candidate;
        }
        return found;
    }

    private void CompleteAttempt(
        FrozenGuardEngageRetry frozen,
        ClientActionAttemptOutcome outcome,
        long nowMilliseconds)
    {
        var completion = HeldActionRetryRules.Complete(
            frozen.Retry,
            Math.Max(0, nowMilliseconds),
            outcome);
        if (completion.RetryScheduled &&
            completion.NextState.NextNativeAttemptAtMilliseconds <=
            frozen.Intent.ExpiresAtMilliseconds)
        {
            frozenRetry = frozen with { Retry = completion.NextState };
            return;
        }
        if (completion.Disposition == HeldActionRetryDisposition.SoftWait &&
            nowMilliseconds <= frozen.Intent.ExpiresAtMilliseconds)
        {
            frozenRetry = frozen;
            return;
        }

        frozenRetry = null;
        if (outcome == ClientActionAttemptOutcome.ClientAccepted)
        {
            ownedHeldKey = frozen.HeldKey;
            return;
        }
        if (outcome == ClientActionAttemptOutcome.ClientRejected)
            Interlocked.Increment(ref rejectedCount);
        else if (outcome == ClientActionAttemptOutcome.AcceptanceUnknown)
            Interlocked.Increment(ref unknownCount);
        if (HeldActionRetryRules.ShouldLatchHeldKeyUntilRelease(completion.Disposition))
            terminalHeldKey = frozen.HeldKey;
    }

    private static HeldCastCancellationRequest? CreateCastCancellationRequest(
        IPlayerCharacter localPlayer,
        FrozenGuardEngageRetry frozen,
        long nowMilliseconds)
    {
        if (nowMilliseconds > frozen.Intent.ExpiresAtMilliseconds ||
            !IsCastCancellationBoundaryReady(localPlayer))
        {
            return null;
        }
        var request = new HeldCastCancellationRequest(
            HeldCastCancellationHelperKind.RedMageGuardEngage,
            frozen.Intent.ActionId,
            frozen.LocalPlayer,
            frozen.Intent.Target,
            (int)frozen.HeldKey,
            frozen.IntentEpochToken);
        return request.IsValid ? request : null;
    }

    private static unsafe bool IsCastCancellationBoundaryReady(
        IPlayerCharacter localPlayer)
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

    private bool IsCurrentlySuppressedByGuard(
        IPlayerCharacter localPlayer,
        long nowMilliseconds)
    {
        try
        {
            if (DefensiveUtilityProbe.HasActiveGuard(localPlayer)) return true;
            return nearAssist.TryGetRecentExactLocalGuardAttempt(
                clientState.TerritoryType,
                localPlayer.GameObjectId,
                localPlayer.EntityId,
                nowMilliseconds,
                DefensiveUtilityRules.GuardPropagationLatchMilliseconds,
                out _);
        }
        catch
        {
            return true;
        }
    }

    private SupportedPvPContext ResolveCurrentContext(bool includeWolvesDenTesting)
    {
        var condition = dutyState.ContentFinderCondition;
        return PvPMatchRules.ResolveSupportedContext(
            clientState.IsPvP,
            clientState.IsPvPExcludingDen,
            includeWolvesDenTesting,
            clientState.TerritoryType,
            condition.IsValid,
            condition.IsValid && condition.Value.PvP,
            condition.IsValid ? condition.Value.ContentUICategory.RowId : 0,
            condition.IsValid && condition.Value.CrystallineConflictCasualRoulette,
            condition.IsValid && condition.Value.CrystallineConflictRankedRoulette);
    }

    private ulong NextIntentEpochToken()
    {
        while (true)
        {
            var current = Volatile.Read(ref nextIntentEpochToken);
            var next = current >= long.MaxValue ? 1 : current + 1;
            if (Interlocked.CompareExchange(
                    ref nextIntentEpochToken,
                    next,
                    current) == current)
            {
                return (ulong)next;
            }
        }
    }

    private void ResetRuntime(bool clearTerminalKey)
    {
        guardEpisodes.Clear();
        frozenRetry = null;
        ownedHeldKey = VirtualKey.NO_KEY;
        if (clearTerminalKey) terminalHeldKey = VirtualKey.NO_KEY;
    }

    private static unsafe GameObject* GetNativeObject(IGameObject? actor)
    {
        if (!HasValidNativeIdentity(actor)) return null;
        var native = (GameObject*)actor!.Address;
        return native != null && native->EntityId == actor.EntityId ? native : null;
    }

    private static bool HasActiveStatus(IBattleChara actor, uint statusId)
    {
        foreach (var status in actor.StatusList)
        {
            if (status.StatusId == statusId &&
                float.IsFinite(status.RemainingTime) &&
                status.RemainingTime > 0f)
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsLivePlayer(IPlayerCharacter? player) =>
        player is not null &&
        !player.IsDead &&
        player.CurrentHp > 0 &&
        player.MaxHp >= player.CurrentHp;

    private static bool IsLiveBattleChara(IBattleChara? actor) =>
        actor is not null &&
        !actor.IsDead &&
        actor.CurrentHp > 0 &&
        actor.MaxHp >= actor.CurrentHp;

    private static bool HasValidNativeIdentity(IGameObject? actor) =>
        actor is not null &&
        actor.Address != nint.Zero &&
        actor.IsValid() &&
        actor.GameObjectId is not 0 and not 0xE0000000UL and not ulong.MaxValue &&
        actor.EntityId is not 0 and not 0xE0000000 and not uint.MaxValue;

    private static bool HasSameNativeIdentity(
        IGameObject? left,
        IGameObject? right) =>
        HasValidNativeIdentity(left) &&
        HasValidNativeIdentity(right) &&
        left!.Address == right!.Address &&
        left.GameObjectId == right.GameObjectId &&
        left.EntityId == right.EntityId;

    private static bool MatchesExactTarget(
        IGameObject? observed,
        IGameObject expected) =>
        HasSameNativeIdentity(observed, expected);

    private static string DescribeAttempt(
        FrozenGuardEngageRetry frozen,
        ClientActionAttemptOutcome outcome,
        bool hardTargetConfirmed) =>
        $"RDM Guard engage {frozen.Intent.Context}/S{frozen.Intent.EnemySlot} " +
        $"attempt {frozen.Retry.NativeAttemptCount + 1}/" +
        $"{HeldActionRetryRules.ResolveAttemptLimit(frozen.Retry)}: {outcome}; " +
        $"hard-target={hardTargetConfirmed}";

    private void LogFailure(Exception exception, string message)
    {
        var now = Environment.TickCount64;
        if (now < nextErrorLogAt) return;
        nextErrorLogAt = now + 10_000;
        log.Error(exception, $"Seiton Sense {message}.");
    }

    private readonly record struct GuardScan(
        RedMageGuardObservationKind Observation,
        int ExactStatusCount,
        int RemainingMilliseconds,
        bool HasOtherReviewedProtection);

    private readonly record struct FrozenGuardEngageRetry(
        RedMageGuardEngageIntent Intent,
        TargetPressureActorIdentity LocalPlayer,
        VirtualKey HeldKey,
        DarkKnightWolvesDenTargetKind WolvesDenKind,
        ulong IntentEpochToken,
        HeldActionRetryState Retry);
}
