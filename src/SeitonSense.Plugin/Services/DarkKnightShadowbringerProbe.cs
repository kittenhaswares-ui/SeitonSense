using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using SeitonSense.Core;

namespace SeitonSense.Plugin.Services;

/// <summary>
/// Adapter payload for the central optional cast-cancellation coordinator. The
/// integration layer maps this to a distinct DarkKnightShadowbringer helper
/// kind; this file intentionally does not renumber the shared enum.
/// </summary>
internal readonly record struct DarkKnightShadowbringerCastCancellationLease(
    uint ExpectedAdjustedActionId,
    TargetPressureActorIdentity LocalPlayer,
    TargetPressureActorIdentity Target,
    int FrozenKeyCode,
    ulong IntentEpochToken)
{
    internal bool IsValid =>
        ExpectedAdjustedActionId is
            DarkKnightShadowbringerRules.ShadowbringerActionId or
            DarkKnightShadowbringerRules.DarkArtsShadowbringerActionId &&
        LocalPlayer.IsValid &&
        Target.IsValid &&
        FrozenKeyCode > 0 &&
        IntentEpochToken != 0;
}

internal sealed record DarkKnightShadowbringerProbeSnapshot(
    DarkKnightShadowbringerDecisionKind Decision,
    DarkKnightShadowbringerDecisionReason Reason,
    DarkKnightShadowbringerOpportunityKind Opportunity,
    long OpportunityGeneration,
    long DarkArtsGeneration,
    bool DarkArtsExposed,
    bool DarkArtsSpent,
    bool BlackbloodPreservationEnabled,
    bool BlackbloodMetadataVerified,
    bool BlackbloodStatusPresent,
    DarkKnightBlackbloodGatePhase BlackbloodGatePhase,
    int BlackbloodAbsentObservations,
    long BlackbloodLastObservedAtMilliseconds,
    bool BlackbloodDispatchAllowed,
    long LastAutomaticBoundaryAtMilliseconds,
    long AutomaticCadenceRemainingMilliseconds,
    bool AutomaticCadenceReady,
    long FallbackGeneration,
    bool FallbackEligible,
    bool FallbackSpent,
    uint ResolvedAdjustedActionId,
    bool CooldownStateKnown,
    bool CooldownReady,
    bool ResourcesReady,
    bool ActionLocallyReady,
    bool NativeBoundaryReady,
    bool PressureKnown,
    int IncomingPressure,
    long PressureAgeMilliseconds,
    bool WolvesDenTestPressureAssumed,
    VirtualKey HeldGameplayKey,
    ulong DeferredFrameToken,
    bool CanRunDeferredSafeFallback,
    int CandidateCount,
    int EnemySlot,
    ulong TargetGameObjectId,
    uint TargetEntityId,
    DarkKnightWolvesDenTargetKind WolvesDenTargetKind,
    bool InputClaimed,
    DarkKnightShadowbringerCastCancellationLease? CastCancellationLease,
    bool UseActionAttempted,
    bool UseActionAccepted,
    ClientActionAttemptOutcome LastNativeOutcome,
    int NativeAttemptCount,
    long AttemptCount,
    long AcceptedCount,
    long RejectedCount,
    long UnknownCount,
    long SoftWaitCount,
    string CandidateResolution,
    string LastEvent)
{
    internal static DarkKnightShadowbringerProbeSnapshot Initial { get; } = new(
        DarkKnightShadowbringerDecisionKind.None,
        DarkKnightShadowbringerDecisionReason.None,
        DarkKnightShadowbringerOpportunityKind.None,
        0,
        0,
        false,
        false,
        false,
        false,
        false,
        DarkKnightBlackbloodGatePhase.Ready,
        0,
        -1,
        true,
        -1,
        0,
        true,
        0,
        false,
        false,
        0,
        false,
        false,
        false,
        false,
        false,
        false,
        0,
        -1,
        false,
        VirtualKey.NO_KEY,
        0,
        false,
        0,
        0,
        0,
        0,
        DarkKnightWolvesDenTargetKind.None,
        false,
        null,
        false,
        false,
        ClientActionAttemptOutcome.None,
        0,
        0,
        0,
        0,
        0,
        0,
        "Not evaluated",
        "Waiting");
}

/// <summary>
/// Default-off held DRK Shadowbringer helper. It consumes one exact Dark Arts
/// exposure, or one configured high-HP/low-pressure fallback episode, on the
/// lowest-HP exact reachable enemy. CC uses canonical S1-S5 actors; Wolves'
/// Den uses only the current hard-target duel opponent or striking dummy.
/// </summary>
internal sealed unsafe class DarkKnightShadowbringerProbe
{
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly ExecuteTracker executeTracker;
    private readonly TargetPressureTracker pressureTracker;
    private readonly NearAssistRedirector nearAssist;
    private readonly IPluginLog log;
    private DarkKnightShadowbringerState state =
        DarkKnightShadowbringerState.Initial;
    private DarkKnightShadowbringerDarkArtsState darkArts =
        DarkKnightShadowbringerDarkArtsState.Initial;
    private DarkKnightBlackbloodGateState blackbloodGate =
        DarkKnightBlackbloodGateState.Initial;
    private DarkKnightShadowbringerFallbackState fallback =
        DarkKnightShadowbringerFallbackState.Initial;
    // Process-local hard cadence: reset/fail-closed/context drift may retire
    // every intent, but must never make a recent automatic boundary younger
    // than 1.8 seconds eligible again. A new probe instance starts at -1.
    private long lastAutomaticBoundaryAtMilliseconds = -1;
    private DarkKnightShadowbringerProbeSnapshot snapshot =
        DarkKnightShadowbringerProbeSnapshot.Initial;
    private VirtualKey terminalHeldKey = VirtualKey.NO_KEY;
    private uint frozenTerritoryId;
    private nint frozenLocalAddress;
    private nint frozenTargetAddress;
    private DarkKnightWolvesDenTargetKind frozenWolvesDenTargetKind;
    private ulong frozenIntentEpochToken;
    private long nextIntentEpochToken;
    private ulong preparedFrameToken;
    private long nextPreparedFrameToken;
    private long preparedFrameAtMilliseconds = -1;
    private uint preparedFrameTerritoryId;
    private SupportedPvPContext preparedFrameContext;
    private TargetPressureActorIdentity preparedFrameLocalPlayer;
    private bool preparedFrameConsumed;
    private long attemptCount;
    private long acceptedCount;
    private long rejectedCount;
    private long unknownCount;
    private long softWaitCount;
    private long nextErrorLogAt;
    private string lastEvent = "Waiting";

    internal DarkKnightShadowbringerProbe(
        IClientState clientState,
        IObjectTable objectTable,
        ExecuteTracker executeTracker,
        TargetPressureTracker pressureTracker,
        NearAssistRedirector nearAssist,
        IPluginLog log)
    {
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.executeTracker = executeTracker;
        this.pressureTracker = pressureTracker;
        this.nearAssist = nearAssist;
        this.log = log;
    }

    internal DarkKnightShadowbringerProbeSnapshot Snapshot =>
        Volatile.Read(ref snapshot);

    internal DarkKnightShadowbringerProbeSnapshot Observe(
        IPlayerCharacter? localPlayer,
        SupportedPvPContext context,
        bool configurationEnabled,
        bool actionMetadataVerified,
        bool preserveBlackblood,
        bool blackbloodMetadataVerified,
        bool wolvesDenStrikingDummyMetadataVerified,
        int minimumHpPercent,
        int pressureLimitExclusive,
        bool actionHelpersSuppressedByGuard,
        bool higherPriorityClaimed,
        EmergencyActionInputFrame inputFrame,
        long nowMilliseconds,
        bool hardReset = false)
    {
        ClearPreparedFrame();
        return ObserveWithPolicy(
            localPlayer,
            context,
            configurationEnabled,
            actionMetadataVerified,
            preserveBlackblood,
            blackbloodMetadataVerified,
            wolvesDenStrikingDummyMetadataVerified,
            minimumHpPercent,
            pressureLimitExclusive,
            actionHelpersSuppressedByGuard,
            higherPriorityClaimed,
            inputFrame,
            nowMilliseconds,
            DarkKnightShadowbringerDispatchPolicy.Any,
            observeEpisodes: true,
            deferredFrameToken: 0,
            hardReset);
    }

    /// <summary>
    /// First half of the audited DRK-local scheduler. It observes Dark Arts and
    /// fallback eligibility once, but may dispatch only the free Dark Arts
    /// opportunity. The returned frame token can authorize one later fallback
    /// pass after Hiebsprung.
    /// </summary>
    internal DarkKnightShadowbringerProbeSnapshot ObservePriorityDarkArts(
        IPlayerCharacter? localPlayer,
        SupportedPvPContext context,
        bool configurationEnabled,
        bool actionMetadataVerified,
        bool preserveBlackblood,
        bool blackbloodMetadataVerified,
        bool wolvesDenStrikingDummyMetadataVerified,
        int minimumHpPercent,
        int pressureLimitExclusive,
        bool actionHelpersSuppressedByGuard,
        bool higherPriorityClaimed,
        EmergencyActionInputFrame inputFrame,
        long nowMilliseconds,
        bool hardReset = false)
    {
        var frameToken = hardReset || nowMilliseconds < 0
            ? 0
            : PrepareFrame(localPlayer, context, nowMilliseconds);
        return ObserveWithPolicy(
            localPlayer,
            context,
            configurationEnabled,
            actionMetadataVerified,
            preserveBlackblood,
            blackbloodMetadataVerified,
            wolvesDenStrikingDummyMetadataVerified,
            minimumHpPercent,
            pressureLimitExclusive,
            actionHelpersSuppressedByGuard,
            higherPriorityClaimed,
            inputFrame,
            nowMilliseconds,
            DarkKnightShadowbringerDispatchPolicy.DarkArtsOnly,
            observeEpisodes: true,
            frameToken,
            hardReset);
    }

    /// <summary>
    /// Second half of the audited DRK-local scheduler. It consumes exactly the
    /// preparation token produced by ObservePriorityDarkArts and never advances
    /// the proc/fallback debouncers a second time.
    /// </summary>
    internal DarkKnightShadowbringerProbeSnapshot ObserveDeferredSafeFallback(
        ulong frameToken,
        IPlayerCharacter? localPlayer,
        SupportedPvPContext context,
        bool configurationEnabled,
        bool actionMetadataVerified,
        bool preserveBlackblood,
        bool blackbloodMetadataVerified,
        bool wolvesDenStrikingDummyMetadataVerified,
        int minimumHpPercent,
        int pressureLimitExclusive,
        bool actionHelpersSuppressedByGuard,
        bool higherPriorityClaimed,
        EmergencyActionInputFrame inputFrame,
        long nowMilliseconds)
    {
        if (!TryConsumePreparedFrame(
                frameToken,
                localPlayer,
                context,
                nowMilliseconds))
        {
            return PublishDeferredFrameFailure();
        }

        return ObserveWithPolicy(
            localPlayer,
            context,
            configurationEnabled,
            actionMetadataVerified,
            preserveBlackblood,
            blackbloodMetadataVerified,
            wolvesDenStrikingDummyMetadataVerified,
            minimumHpPercent,
            pressureLimitExclusive,
            actionHelpersSuppressedByGuard,
            higherPriorityClaimed,
            inputFrame,
            nowMilliseconds,
            DarkKnightShadowbringerDispatchPolicy.SafeHpCostOnly,
            observeEpisodes: false,
            deferredFrameToken: 0,
            hardReset: false);
    }

    private DarkKnightShadowbringerProbeSnapshot ObserveWithPolicy(
        IPlayerCharacter? localPlayer,
        SupportedPvPContext context,
        bool configurationEnabled,
        bool actionMetadataVerified,
        bool preserveBlackblood,
        bool blackbloodMetadataVerified,
        bool wolvesDenStrikingDummyMetadataVerified,
        int minimumHpPercent,
        int pressureLimitExclusive,
        bool actionHelpersSuppressedByGuard,
        bool higherPriorityClaimed,
        EmergencyActionInputFrame inputFrame,
        long nowMilliseconds,
        DarkKnightShadowbringerDispatchPolicy dispatchPolicy,
        bool observeEpisodes,
        ulong deferredFrameToken,
        bool hardReset)
    {
        try
        {
            return ObserveCore(
                localPlayer,
                context,
                configurationEnabled,
                actionMetadataVerified,
                preserveBlackblood,
                blackbloodMetadataVerified,
                wolvesDenStrikingDummyMetadataVerified,
                minimumHpPercent,
                pressureLimitExclusive,
                actionHelpersSuppressedByGuard,
                higherPriorityClaimed,
                inputFrame,
                nowMilliseconds,
                dispatchPolicy,
                observeEpisodes,
                deferredFrameToken,
                hardReset);
        }
        catch (Exception exception)
        {
            LogFailure(
                exception,
                nowMilliseconds,
                "Seiton Sense DRK held Shadowbringer probe failed closed.");
            return FailClosed();
        }
    }

    internal void Reset()
    {
        state = DarkKnightShadowbringerState.Initial;
        darkArts = DarkKnightShadowbringerDarkArtsState.Initial;
        blackbloodGate = DarkKnightBlackbloodGateState.Initial;
        fallback = DarkKnightShadowbringerFallbackState.Initial;
        terminalHeldKey = VirtualKey.NO_KEY;
        ClearPreparedFrame();
        ClearFrozenRuntime();
        lastEvent = "Reset";
        PublishTerminalSnapshot(
            DarkKnightShadowbringerDecisionKind.None,
            DarkKnightShadowbringerDecisionReason.HardReset,
            lastEvent);
    }

    internal DarkKnightShadowbringerProbeSnapshot FailClosed()
    {
        var failedKey = state.Intent is { IsValid: true } intent
            ? (VirtualKey)intent.FrozenKeyCode
            : terminalHeldKey;
        state = DarkKnightShadowbringerState.Initial;
        darkArts = DarkKnightShadowbringerDarkArtsState.Initial;
        blackbloodGate = DarkKnightBlackbloodGateState.Initial;
        fallback = DarkKnightShadowbringerFallbackState.Initial;
        terminalHeldKey = failedKey;
        ClearPreparedFrame();
        ClearFrozenRuntime();
        lastEvent = "Failed closed";
        return PublishTerminalSnapshot(
            DarkKnightShadowbringerDecisionKind.Cancelled,
            DarkKnightShadowbringerDecisionReason.HardReset,
            lastEvent);
    }

    private DarkKnightShadowbringerProbeSnapshot ObserveCore(
        IPlayerCharacter? localPlayer,
        SupportedPvPContext context,
        bool configurationEnabled,
        bool actionMetadataVerified,
        bool preserveBlackblood,
        bool blackbloodMetadataVerified,
        bool wolvesDenStrikingDummyMetadataVerified,
        int minimumHpPercent,
        int pressureLimitExclusive,
        bool actionHelpersSuppressedByGuard,
        bool higherPriorityClaimed,
        EmergencyActionInputFrame inputFrame,
        long nowMilliseconds,
        DarkKnightShadowbringerDispatchPolicy dispatchPolicy,
        bool observeEpisodes,
        ulong deferredFrameToken,
        bool hardReset)
    {
        var effectiveHardReset = hardReset || nowMilliseconds < 0;
        if (effectiveHardReset)
        {
            state = DarkKnightShadowbringerState.Initial;
            darkArts = DarkKnightShadowbringerDarkArtsState.Initial;
            blackbloodGate = DarkKnightBlackbloodGateState.Initial;
            fallback = DarkKnightShadowbringerFallbackState.Initial;
            terminalHeldKey = VirtualKey.NO_KEY;
            ClearFrozenRuntime();
        }

        if (terminalHeldKey != VirtualKey.NO_KEY &&
            inputFrame.Snapshot.ProbeSucceeded &&
            !inputFrame.IsGameplayKeyPhysicallyDown(terminalHeldKey))
        {
            terminalHeldKey = VirtualKey.NO_KEY;
        }

        var exactLocal = ResolveExactLocalPlayer(localPlayer);
        var localIdentity = exactLocal is null
            ? default
            : new TargetPressureActorIdentity(
                exactLocal.GameObjectId,
                exactLocal.EntityId);
        var localAlive = IsLivePlayer(exactLocal);
        var localTargetable = exactLocal?.IsTargetable == true;
        var localJobId = exactLocal?.ClassJob.IsValid == true
            ? exactLocal.ClassJob.RowId
            : 0;
        var territoryId = clientState.TerritoryType;
        var supportedContext = context is
            SupportedPvPContext.CrystallineConflict or
            SupportedPvPContext.WolvesDen;
        var blackbloodPreservationMetadataReady =
            !preserveBlackblood || blackbloodMetadataVerified;
        var featureGateReady = !effectiveHardReset &&
                               configurationEnabled &&
                               supportedContext &&
                               localAlive &&
                               localTargetable &&
                               localJobId ==
                                   DarkKnightShadowbringerRules.DarkKnightJobId &&
                               actionMetadataVerified &&
                               blackbloodPreservationMetadataReady;
        var runtimeDrift = state.Intent is { IsValid: true } frozenIntent &&
                           (!FrozenRuntimeMatches(
                                frozenIntent,
                                territoryId,
                                exactLocal?.Address ?? nint.Zero) ||
                            frozenIntent.Context != context ||
                            frozenIntent.LocalPlayer != localIdentity ||
                            !featureGateReady);
        if (runtimeDrift)
        {
            effectiveHardReset = true;
            state = DarkKnightShadowbringerState.Initial;
            darkArts = DarkKnightShadowbringerDarkArtsState.Initial;
            blackbloodGate = DarkKnightBlackbloodGateState.Initial;
            fallback = DarkKnightShadowbringerFallbackState.Initial;
            terminalHeldKey = VirtualKey.NO_KEY;
            ClearFrozenRuntime();
            featureGateReady = false;
        }

        var guardSuppressed = featureGateReady &&
                              (actionHelpersSuppressedByGuard ||
                               IsCurrentlySuppressedByGuard(
                                   exactLocal,
                                   nowMilliseconds));
        var effectiveHigherPriorityClaimed = higherPriorityClaimed ||
                                             inputFrame.IsConsumed;
        var nativeState = featureGateReady &&
                          TryObserveNativeState(
                              exactLocal!,
                              out var observedNativeState)
            ? observedNativeState
            : ShadowbringerNativeState.Unknown;
        var blackbloodStatusPresent = featureGateReady &&
                                      HasExactStatusRow(
                                          exactLocal!,
                                          DarkKnightShadowbringerRules
                                              .BlackbloodStatusId);
        if (observeEpisodes)
        {
            blackbloodGate = DarkKnightShadowbringerRules
                .ObserveBlackbloodGate(
                    blackbloodGate,
                    preserveBlackblood,
                    blackbloodStatusPresent,
                    nowMilliseconds,
                    hardReset: !featureGateReady);
        }
        else if (blackbloodStatusPresent)
        {
            // The deferred fallback pass may observe a newly propagated row,
            // but it must never count a second absent sample in one framework
            // frame. Only the first pass advances absence debounce.
            blackbloodGate = DarkKnightShadowbringerRules
                .ObserveBlackbloodGate(
                    blackbloodGate,
                    preserveBlackblood,
                    exactBlackbloodActive: true,
                    nowMilliseconds);
        }
        var automaticCadenceReady =
            DarkKnightShadowbringerRules.IsAutomaticCadenceReady(
                lastAutomaticBoundaryAtMilliseconds,
                nowMilliseconds);
        var automaticCadenceRemainingMilliseconds =
            DarkKnightShadowbringerRules
                .GetAutomaticCadenceRemainingMilliseconds(
                    lastAutomaticBoundaryAtMilliseconds,
                    nowMilliseconds);
        var dispatchConfigurationEnabled = featureGateReady &&
                                           automaticCadenceReady &&
                                           (!preserveBlackblood ||
                                            blackbloodGate.IsDispatchAllowed);
        var hasDarkArts = featureGateReady &&
                          HasActiveStatus(
                              exactLocal!,
                              DarkKnightShadowbringerRules.DarkArtsStatusId);
        var exactDarkArtsExposure = featureGateReady &&
                                    hasDarkArts &&
                                    nativeState.ResolvedAdjustedActionId ==
                                    DarkKnightShadowbringerRules
                                        .DarkArtsShadowbringerActionId;
        var incomingPressure = 0;
        var pressureAgeMilliseconds = -1L;
        var wolvesDenTestPressureAssumed = featureGateReady &&
                                           context ==
                                               SupportedPvPContext.WolvesDen;
        var pressureKnown = wolvesDenTestPressureAssumed ||
                            featureGateReady &&
                            TryGetFreshSelfIncomingPressure(
                                localIdentity,
                                nowMilliseconds,
                                out incomingPressure,
                                out pressureAgeMilliseconds);
        if (wolvesDenTestPressureAssumed)
            pressureAgeMilliseconds = 0;

        var exactFallbackEligibility = dispatchConfigurationEnabled &&
            !hasDarkArts &&
            nativeState.ResolvedAdjustedActionId ==
                DarkKnightShadowbringerRules.ShadowbringerActionId &&
            DarkKnightShadowbringerRules.IsSafeFallbackEligible(
                exactLocal!.CurrentHp,
                exactLocal.MaxHp,
                pressureKnown,
                incomingPressure,
                minimumHpPercent,
                pressureLimitExclusive);
        if (observeEpisodes)
        {
            darkArts = DarkKnightShadowbringerRules.ObserveDarkArts(
                darkArts,
                exactDarkArtsExposure,
                hardReset: !featureGateReady);
            fallback = DarkKnightShadowbringerRules.ObserveFallback(
                fallback,
                exactFallbackEligibility,
                hardReset: !featureGateReady);
        }

        var input = inputFrame.Snapshot;
        var frozenKeyStillDown = state.Intent is { IsValid: true } currentIntent &&
                                 inputFrame.IsFrozenGameplayKeyConsentValid(
                                     (VirtualKey)currentIntent.FrozenKeyCode);
        var hasOpportunity = DarkKnightShadowbringerRules.TrySelectOpportunity(
            darkArts,
            fallback,
            out var selectedOpportunity,
            out var selectedOpportunityGeneration,
            out var selectedAdjustedActionId,
            dispatchPolicy);
        var darkArtsSupersedesBufferedFallback =
            dispatchConfigurationEnabled &&
            state.Intent is
                {
                    IsValid: true,
                    Opportunity:
                        DarkKnightShadowbringerOpportunityKind.SafeHpCost,
                } &&
            dispatchPolicy is DarkKnightShadowbringerDispatchPolicy.Any or
                DarkKnightShadowbringerDispatchPolicy.DarkArtsOnly &&
            darkArts.IsValid &&
            darkArts.HasTrackedEpisode &&
            darkArts.IsCurrentlyExposed &&
            !darkArts.IsSpent;
        var expectedAdjustedActionId = darkArtsSupersedesBufferedFallback
            ? DarkKnightShadowbringerRules.DarkArtsShadowbringerActionId
            : state.Intent is { IsValid: true } intent
                ? intent.ExpectedAdjustedActionId
                : selectedAdjustedActionId;
        RuntimeCandidate? runtimeCandidate = null;
        var candidateCount = 0;
        var selectedWinnerInvalidated = false;
        var candidateResolution = "Not evaluated: no exact ready opportunity";
        if (dispatchConfigurationEnabled &&
            exactLocal is not null &&
            expectedAdjustedActionId != 0)
        {
            if (state.Intent is { IsValid: true } bufferedIntent &&
                !darkArtsSupersedesBufferedFallback)
            {
                runtimeCandidate = ResolveExactCandidate(
                    exactLocal,
                    bufferedIntent.Context,
                    bufferedIntent.EnemySlot,
                    bufferedIntent.Target,
                    bufferedIntent.ExpectedAdjustedActionId,
                    wolvesDenStrikingDummyMetadataVerified,
                    frozenWolvesDenTargetKind,
                    checkCastingActive: false);
                candidateCount = runtimeCandidate is null ? 0 : 1;
                candidateResolution = runtimeCandidate is null
                    ? "Frozen exact target unavailable"
                    : "Frozen exact target revalidated";
            }
            else if ((hasOpportunity || darkArtsSupersedesBufferedFallback) &&
                     nativeState.ActionLocallyReady &&
                     nativeState.ResolvedAdjustedActionId ==
                         expectedAdjustedActionId &&
                     terminalHeldKey == VirtualKey.NO_KEY &&
                     !guardSuppressed &&
                     !effectiveHigherPriorityClaimed &&
                     inputFrame.HeldGameplayKeyEligible)
            {
                var candidates = ResolveCurrentCandidates(
                    exactLocal,
                    context,
                    expectedAdjustedActionId,
                    wolvesDenStrikingDummyMetadataVerified,
                    checkCastingActive: false,
                    out selectedWinnerInvalidated,
                    out candidateResolution);
                candidateCount = candidates.Count;
                var coreCandidates = candidates
                    .Select(static candidate => candidate.Core)
                    .ToArray();
                var selectedIndex =
                    DarkKnightShadowbringerRules.SelectBestCandidateIndex(
                        coreCandidates,
                        context,
                        localIdentity);
                if (selectedIndex >= 0 && selectedIndex < candidates.Count)
                    runtimeCandidate = candidates[selectedIndex];
            }
        }

        if (selectedWinnerInvalidated)
        {
            // Retire the freshly selected opportunity even when a new Dark
            // Arts generation was trying to supersede an older buffered HP
            // fallback. Only that exact new generation is spent; the old
            // frozen fallback is never relabeled or reranked here.
            SpendOpportunity(
                selectedOpportunity,
                selectedOpportunityGeneration);
        }

        if (state.Intent is { IsValid: true } trackedIntent &&
            !darkArtsSupersedesBufferedFallback &&
            runtimeCandidate is { } trackedCandidate &&
            (trackedCandidate.Target.Address != frozenTargetAddress ||
             trackedCandidate.WolvesDenTargetKind !=
                 frozenWolvesDenTargetKind))
        {
            runtimeCandidate = null;
            candidateResolution = "Frozen native target identity drift";
        }

        var exactActionLocallyReady = nativeState.ActionLocallyReady &&
                                      nativeState.ResolvedAdjustedActionId ==
                                      expectedAdjustedActionId;
        var previousIntent = state.Intent;
        var decision = DarkKnightShadowbringerRules.Observe(
            state,
            new DarkKnightShadowbringerObservation(
                dispatchConfigurationEnabled,
                context,
                localIdentity,
                localAlive,
                localTargetable,
                localJobId,
                actionMetadataVerified,
                guardSuppressed,
                effectiveHigherPriorityClaimed,
                input.ProbeSucceeded,
                input.IsTextInputActive,
                terminalHeldKey == VirtualKey.NO_KEY &&
                inputFrame.HeldGameplayKeyEligible,
                (int)input.HeldGameplayKey,
                frozenKeyStillDown,
                darkArts,
                fallback,
                dispatchPolicy,
                nativeState.ResolvedAdjustedActionId,
                exactActionLocallyReady,
                nativeState.NativeBoundaryReady,
                runtimeCandidate?.Core,
                effectiveHardReset,
                nowMilliseconds));

        if (decision.SpendOpportunity && previousIntent is { IsValid: true })
            SpendOpportunity(previousIntent.Value);

        if (decision.NextState.Intent is { IsValid: true } nextIntent &&
            (!previousIntent.HasValue || previousIntent.Value != nextIntent))
        {
            if (runtimeCandidate is not { } frozenCandidate ||
                exactLocal is null ||
                frozenCandidate.Core.Context != nextIntent.Context ||
                frozenCandidate.Core.EnemySlot != nextIntent.EnemySlot ||
                frozenCandidate.Core.Actor != nextIntent.Target)
            {
                decision = new DarkKnightShadowbringerDecision(
                    DarkKnightShadowbringerState.Initial,
                    DarkKnightShadowbringerDecisionKind.Cancelled,
                    DarkKnightShadowbringerDecisionReason.CandidateUnavailable,
                    SpendOpportunity: true);
                SpendOpportunity(nextIntent);
                ClearFrozenRuntime();
            }
            else
            {
                FreezeRuntime(
                    nextIntent,
                    territoryId,
                    exactLocal.Address,
                    frozenCandidate.Target.Address,
                    frozenCandidate.WolvesDenTargetKind);
            }
        }

        state = decision.NextState;
        if (state.Intent is { IsValid: true } claimedIntent)
        {
            _ = inputFrame.IsFrozenGameplayKeyConsentValid(
                (VirtualKey)claimedIntent.FrozenKeyCode);
        }
        if (decision.InputClaimed) inputFrame.Consume();

        var attempted = false;
        var accepted = false;
        var nativeOutcome = ClientActionAttemptOutcome.None;
        if (decision.ShouldDispatch && decision.Intent is { } dispatchIntent)
        {
            nativeOutcome = TryUseOnce(
                dispatchIntent,
                context,
                configurationEnabled,
                actionMetadataVerified,
                preserveBlackblood,
                blackbloodMetadataVerified,
                wolvesDenStrikingDummyMetadataVerified,
                minimumHpPercent,
                pressureLimitExclusive,
                actionHelpersSuppressedByGuard,
                effectiveHigherPriorityClaimed,
                inputFrame,
                out attempted);
            // Start every terminal boundary clock no earlier than the sole
            // native call. The framework-frame timestamp was captured before
            // TryUseOnce and could otherwise shorten the hard 1.8-second gate.
            var boundaryCompletedAtMilliseconds = Math.Max(
                nowMilliseconds,
                Environment.TickCount64);
            if (attempted) Interlocked.Increment(ref attemptCount);
            if (nativeOutcome == ClientActionAttemptOutcome.ClientAccepted)
                Interlocked.Increment(ref acceptedCount);
            if (nativeOutcome == ClientActionAttemptOutcome.ClientRejected)
                Interlocked.Increment(ref rejectedCount);
            if (nativeOutcome == ClientActionAttemptOutcome.AcceptanceUnknown)
                Interlocked.Increment(ref unknownCount);

            var completion =
                DarkKnightShadowbringerRules.ApplyNativeAttemptOutcome(
                    state,
                    nativeOutcome,
                    boundaryCompletedAtMilliseconds);
            blackbloodGate = DarkKnightShadowbringerRules
                .MarkAutomaticShadowbringerBoundary(
                    blackbloodGate,
                    preserveBlackblood,
                    nativeOutcome,
                    boundaryCompletedAtMilliseconds);
            lastAutomaticBoundaryAtMilliseconds =
                DarkKnightShadowbringerRules.MarkAutomaticCadenceBoundary(
                    lastAutomaticBoundaryAtMilliseconds,
                    nativeOutcome,
                    boundaryCompletedAtMilliseconds);
            fallback = DarkKnightShadowbringerRules
                .RetireFallbackAfterAutomaticBoundary(
                    fallback,
                    nativeOutcome);
            automaticCadenceReady =
                DarkKnightShadowbringerRules.IsAutomaticCadenceReady(
                    lastAutomaticBoundaryAtMilliseconds,
                    nowMilliseconds);
            automaticCadenceRemainingMilliseconds =
                DarkKnightShadowbringerRules
                    .GetAutomaticCadenceRemainingMilliseconds(
                        lastAutomaticBoundaryAtMilliseconds,
                        nowMilliseconds);
            if (completion.SpendOpportunity)
                SpendOpportunity(dispatchIntent);
            state = completion.NextState;
            accepted = completion.ClientAccepted;
            if (completion.SoftWait) Interlocked.Increment(ref softWaitCount);
            if (completion.Terminal &&
                HeldActionRetryRules.ShouldLatchHeldKeyUntilRelease(
                    completion.Disposition))
            {
                terminalHeldKey = (VirtualKey)dispatchIntent.FrozenKeyCode;
            }

            lastEvent = DescribeNativeResult(
                dispatchIntent,
                nativeOutcome,
                completion);
        }
        else
        {
            lastEvent = decision.Reason.ToString();
        }

        var castCancellationLease = state.Intent is { IsValid: true } leasedIntent &&
                                    decision.InputClaimed &&
                                    !nativeState.NativeBoundaryReady &&
                                    runtimeCandidate is { } leasedCandidate
            ? CreateCastCancellationLease(
                exactLocal,
                leasedIntent,
                leasedCandidate)
            : null;

        var observedIntent = state.Intent ?? decision.Intent;
        var observedCandidate = runtimeCandidate;
        var ownsBufferedSafeFallback =
            state.Intent is
                {
                    IsValid: true,
                    Opportunity:
                        DarkKnightShadowbringerOpportunityKind.SafeHpCost,
                } bufferedSafeIntent &&
            DarkKnightShadowbringerRules.IsTrackedOpportunity(
                bufferedSafeIntent,
                darkArts,
                fallback,
                requireCurrent: true);
        var canRunDeferredSafeFallback =
            deferredFrameToken != 0 &&
            automaticCadenceReady &&
            (!preserveBlackblood ||
             blackbloodGate.IsDispatchAllowed) &&
            dispatchPolicy ==
                DarkKnightShadowbringerDispatchPolicy.DarkArtsOnly &&
            state.Intent is not
                { Opportunity: DarkKnightShadowbringerOpportunityKind.DarkArts } &&
            !(darkArts.IsValid &&
              darkArts.HasTrackedEpisode &&
              darkArts.IsCurrentlyExposed &&
              !darkArts.IsSpent) &&
            (ownsBufferedSafeFallback ||
             fallback.IsValid &&
             fallback.HasTrackedEpisode &&
             fallback.IsCurrentlyEligible &&
             !fallback.IsSpent);
        var result = new DarkKnightShadowbringerProbeSnapshot(
            decision.Kind,
            decision.Reason,
            observedIntent?.Opportunity ??
                DarkKnightShadowbringerOpportunityKind.None,
            observedIntent?.OpportunityGeneration ?? 0,
            darkArts.Generation,
            darkArts.IsCurrentlyExposed,
            darkArts.IsSpent,
            preserveBlackblood,
            blackbloodMetadataVerified,
            blackbloodStatusPresent,
            blackbloodGate.Phase,
            blackbloodGate.ConsecutiveAbsentObservations,
            blackbloodGate.LastObservedAtMilliseconds,
            blackbloodGate.IsDispatchAllowed,
            lastAutomaticBoundaryAtMilliseconds,
            automaticCadenceRemainingMilliseconds,
            automaticCadenceReady,
            fallback.Generation,
            fallback.IsCurrentlyEligible,
            fallback.IsSpent,
            nativeState.ResolvedAdjustedActionId,
            nativeState.CooldownStateKnown,
            nativeState.CooldownReady,
            nativeState.ResourcesReady,
            nativeState.ActionLocallyReady,
            nativeState.NativeBoundaryReady,
            pressureKnown,
            incomingPressure,
            pressureAgeMilliseconds,
            wolvesDenTestPressureAssumed,
            observedIntent is { IsValid: true } observed
                ? (VirtualKey)observed.FrozenKeyCode
                : input.HeldGameplayKey,
            deferredFrameToken,
            canRunDeferredSafeFallback,
            candidateCount,
            observedCandidate?.Core.EnemySlot ?? 0,
            observedCandidate?.Core.Actor.GameObjectId ?? 0,
            observedCandidate?.Core.Actor.EntityId ?? 0,
            observedCandidate?.WolvesDenTargetKind ??
                DarkKnightWolvesDenTargetKind.None,
            decision.InputClaimed,
            castCancellationLease,
            attempted,
            accepted,
            nativeOutcome,
            state.Retry.NativeAttemptCount,
            Interlocked.Read(ref attemptCount),
            Interlocked.Read(ref acceptedCount),
            Interlocked.Read(ref rejectedCount),
            Interlocked.Read(ref unknownCount),
            Interlocked.Read(ref softWaitCount),
            candidateResolution,
            lastEvent);
        Volatile.Write(ref snapshot, result);

        if (state.Phase != DarkKnightShadowbringerPhase.Buffered)
            ClearFrozenRuntime();
        return result;
    }

    private IReadOnlyList<RuntimeCandidate> ResolveCurrentCandidates(
        IPlayerCharacter localPlayer,
        SupportedPvPContext context,
        uint expectedAdjustedActionId,
        bool wolvesDenStrikingDummyMetadataVerified,
        bool checkCastingActive,
        out bool selectedWinnerInvalidated,
        out string resolution)
    {
        selectedWinnerInvalidated = false;
        switch (context)
        {
            case SupportedPvPContext.CrystallineConflict:
            {
                if (!nearAssist.TryResolveHeldSmartActionTarget(
                        expectedAdjustedActionId,
                        out var slot,
                        out var smartIdentity,
                        out selectedWinnerInvalidated,
                        out resolution))
                {
                    return [];
                }

                var smartCandidate = ResolveExactCandidate(
                    localPlayer,
                    context,
                    slot,
                    smartIdentity,
                    expectedAdjustedActionId,
                    wolvesDenStrikingDummyMetadataVerified: false,
                    DarkKnightWolvesDenTargetKind.None,
                    checkCastingActive);
                var localIdentity = new TargetPressureActorIdentity(
                    localPlayer.GameObjectId,
                    localPlayer.EntityId);
                if (smartCandidate is null ||
                    !DarkKnightShadowbringerRules.IsEligibleCandidate(
                        smartCandidate.Value.Core,
                        localIdentity))
                {
                    selectedWinnerInvalidated = true;
                    resolution =
                        "Frozen Smart Action winner failed exact DRK eligibility";
                    return [];
                }

                resolution = $"Frozen Smart Action S{slot}: {resolution}";
                return [smartCandidate.Value];
            }
            case SupportedPvPContext.WolvesDen:
                if (!DarkKnightWolvesDenCurrentTargetResolver
                        .TryResolveExactCurrentHardTarget(
                            objectTable,
                            wolvesDenStrikingDummyMetadataVerified,
                            localPlayer,
                            out _,
                            out var identity,
                            out var kind,
                            out _))
                {
                    resolution =
                        "Wolves' Den current hard target is not the exact duel enemy or dummy";
                    return [];
                }

                var candidate = ResolveExactCandidate(
                    localPlayer,
                    context,
                    0,
                    identity,
                    expectedAdjustedActionId,
                    wolvesDenStrikingDummyMetadataVerified,
                    kind,
                    checkCastingActive);
                resolution = candidate is null
                    ? "Wolves' Den current hard target action validation failed"
                    : $"Exact Wolves' Den current target: {kind}";
                return candidate is null ? [] : [candidate.Value];
            default:
                resolution = "Unsupported PvP context";
                return [];
        }
    }

    private IReadOnlyList<RuntimeCandidate> ResolveExactCcCandidates(
        IPlayerCharacter localPlayer,
        uint expectedAdjustedActionId,
        bool checkCastingActive,
        out string resolution)
    {
        var diagnosticsBefore = executeTracker.Diagnostics;
        if (!diagnosticsBefore.Active || !diagnosticsBefore.IsCrystallineConflict)
        {
            resolution = "Tracker CC context unavailable";
            return [];
        }

        if (diagnosticsBefore.SlotCapacity != EnemySlotRules.LastSlot ||
            diagnosticsBefore.ResolvedSlots != EnemySlotRules.LastSlot)
        {
            resolution =
                $"Tracker slots incomplete: {diagnosticsBefore.ResolvedSlots}/{diagnosticsBefore.SlotCapacity}";
            return [];
        }

        var snapshots = executeTracker.Enemies.ToArray();
        var diagnosticsAfter = executeTracker.Diagnostics;
        if (!ReferenceEquals(diagnosticsBefore, diagnosticsAfter))
        {
            resolution = "Tracker snapshot changed during capture";
            return [];
        }

        if (snapshots.Length > EnemySlotRules.LastSlot ||
            snapshots.Length != diagnosticsBefore.ValidEnemySlots)
        {
            resolution =
                $"Tracker snapshot count drift: {snapshots.Length}/{diagnosticsBefore.ValidEnemySlots}";
            return [];
        }

        var seenSlots = new HashSet<int>();
        var seenGameObjectIds = new HashSet<ulong>();
        var seenEntityIds = new HashSet<uint>();
        var snapshotsBySlot = new Dictionary<int, EnemyHudSnapshot>(
            snapshots.Length);
        foreach (var enemy in snapshots)
        {
            if (!EnemySlotRules.IsValidSlot(enemy.Slot) ||
                !IsNetworkObjectId(enemy.GameObjectId) ||
                !IsNetworkEntityId(enemy.EntityId) ||
                !seenSlots.Add(enemy.Slot) ||
                !seenGameObjectIds.Add(enemy.GameObjectId) ||
                !seenEntityIds.Add(enemy.EntityId))
            {
                resolution = "Tracker snapshot identity ambiguous";
                return [];
            }

            snapshotsBySlot.Add(enemy.Slot, enemy);
        }

        var currentSlots = new List<(int Slot, IPlayerCharacter Player)>(
            EnemySlotRules.LastSlot);
        seenGameObjectIds.Clear();
        seenEntityIds.Clear();
        var seenAddresses = new HashSet<nint>();
        for (var slot = EnemySlotRules.FirstSlot;
             slot <= EnemySlotRules.LastSlot;
             slot++)
        {
            var player = EnemySlotResolver.Resolve(objectTable, slot);
            if (!HasValidNativeIdentity(player))
            {
                resolution = $"Native S{slot} unresolved";
                return [];
            }

            var byObjectId = objectTable.SearchById(player!.GameObjectId)
                as IPlayerCharacter;
            var byEntityId = objectTable.SearchByEntityId(player.EntityId)
                as IPlayerCharacter;
            if (!HasSameNativeIdentity(player, byObjectId) ||
                !HasSameNativeIdentity(player, byEntityId) ||
                !seenGameObjectIds.Add(player.GameObjectId) ||
                !seenEntityIds.Add(player.EntityId) ||
                !seenAddresses.Add(player.Address))
            {
                resolution = $"Native S{slot} canonical identity mismatch";
                return [];
            }

            currentSlots.Add((slot, player));
        }

        var eligibleCurrentSlots = currentSlots
            .Where(static entry =>
                IsLivePlayer(entry.Player) &&
                entry.Player.IsTargetable)
            .ToArray();
        if (eligibleCurrentSlots.Length != diagnosticsBefore.ValidEnemySlots ||
            eligibleCurrentSlots.Length != snapshots.Length)
        {
            resolution =
                $"Tracker/native eligible count drift: {snapshots.Length}/{eligibleCurrentSlots.Length}";
            return [];
        }

        var candidates = new List<RuntimeCandidate>(eligibleCurrentSlots.Length);
        foreach (var (slot, player) in eligibleCurrentSlots)
        {
            if (!snapshotsBySlot.TryGetValue(slot, out var enemy) ||
                enemy.GameObjectId != player.GameObjectId ||
                enemy.EntityId != player.EntityId)
            {
                resolution = $"Tracker/native S{slot} identity mismatch";
                return [];
            }

            var candidate = ResolveExactCandidate(
                localPlayer,
                SupportedPvPContext.CrystallineConflict,
                slot,
                new TargetPressureActorIdentity(
                    player.GameObjectId,
                    player.EntityId),
                expectedAdjustedActionId,
                wolvesDenStrikingDummyMetadataVerified: false,
                DarkKnightWolvesDenTargetKind.None,
                checkCastingActive);
            if (candidate is null)
            {
                resolution = $"Native S{slot} action validation failed";
                return [];
            }

            candidates.Add(candidate.Value);
        }

        foreach (var (slot, player) in currentSlots)
        {
            if (!HasSameNativeIdentity(
                    player,
                    EnemySlotResolver.Resolve(objectTable, slot)))
            {
                resolution = $"Native S{slot} changed during capture";
                return [];
            }
        }

        resolution = $"Exact coherent CC set: {candidates.Count} candidates";
        return candidates;
    }

    private RuntimeCandidate? ResolveExactCandidate(
        IPlayerCharacter localPlayer,
        SupportedPvPContext context,
        int enemySlot,
        TargetPressureActorIdentity expectedTarget,
        uint expectedAdjustedActionId,
        bool wolvesDenStrikingDummyMetadataVerified,
        DarkKnightWolvesDenTargetKind expectedWolvesDenKind,
        bool checkCastingActive)
    {
        if (!expectedTarget.IsValid ||
            expectedTarget == new TargetPressureActorIdentity(
                localPlayer.GameObjectId,
                localPlayer.EntityId) ||
            expectedAdjustedActionId is not
                (DarkKnightShadowbringerRules.ShadowbringerActionId or
                 DarkKnightShadowbringerRules.DarkArtsShadowbringerActionId))
        {
            return null;
        }

        IBattleChara? target;
        var exactCanonicalIdentity = false;
        var wolvesDenKind = DarkKnightWolvesDenTargetKind.None;
        switch (context)
        {
            case SupportedPvPContext.CrystallineConflict:
            {
                if (!EnemySlotRules.IsValidSlot(enemySlot) ||
                    !nearAssist.CanUseExactHeldSmartActionTarget(
                        expectedAdjustedActionId,
                        enemySlot,
                        expectedTarget))
                {
                    return null;
                }
                var player = EnemySlotResolver.Resolve(objectTable, enemySlot);
                if (!HasValidNativeIdentity(player) ||
                    player!.GameObjectId != expectedTarget.GameObjectId ||
                    player.EntityId != expectedTarget.EntityId)
                {
                    return null;
                }

                var byObjectId = objectTable.SearchById(player.GameObjectId)
                    as IPlayerCharacter;
                var byEntityId = objectTable.SearchByEntityId(player.EntityId)
                    as IPlayerCharacter;
                exactCanonicalIdentity =
                    HasSameNativeIdentity(player, byObjectId) &&
                    HasSameNativeIdentity(player, byEntityId) &&
                    HasSameNativeIdentity(
                        player,
                        EnemySlotResolver.Resolve(objectTable, enemySlot));
                target = player;
                break;
            }
            case SupportedPvPContext.WolvesDen:
                if (enemySlot != 0 ||
                    !DarkKnightWolvesDenCurrentTargetResolver
                        .TryResolveFrozenCurrentHardTarget(
                            objectTable,
                            wolvesDenStrikingDummyMetadataVerified,
                            localPlayer,
                            expectedTarget,
                            expectedWolvesDenKind,
                            out target))
                {
                    return null;
                }

                exactCanonicalIdentity = true;
                wolvesDenKind = expectedWolvesDenKind;
                break;
            default:
                return null;
        }

        if (!exactCanonicalIdentity ||
            !HasValidNativeIdentity(target) ||
            target!.GameObjectId != expectedTarget.GameObjectId ||
            target.EntityId != expectedTarget.EntityId)
        {
            return null;
        }

        var sourceObject = GetNativeObject(localPlayer);
        var targetObject = GetNativeObject(target);
        var nativePointersValid = sourceObject != null && targetObject != null;
        var actionManager = ActionManager.Instance();
        var targetActionReady = nativePointersValid &&
                                actionManager != null &&
                                actionManager->GetAdjustedActionId(
                                    DarkKnightShadowbringerRules
                                        .ShadowbringerActionId) ==
                                    expectedAdjustedActionId &&
                                actionManager->GetActionStatus(
                                    ActionType.Action,
                                    expectedAdjustedActionId,
                                    expectedTarget.GameObjectId,
                                    checkRecastActive: true,
                                    checkCastingActive) == 0;
        var nativeRangeAndLineOfSight = nativePointersValid &&
            SeitonRangeRules.HasNativeRangeAndLineOfSight(
                ActionManager.GetActionInRangeOrLoS(
                    expectedAdjustedActionId,
                    sourceObject,
                    targetObject));
        var core = new DarkKnightShadowbringerCandidate(
            context,
            enemySlot,
            expectedTarget,
            exactCanonicalIdentity,
            IsLiveBattleCharacter(target),
            target.IsTargetable,
            target.CurrentHp,
            target.MaxHp,
            HasActiveGuard(target),
            targetActionReady,
            nativeRangeAndLineOfSight);
        return new RuntimeCandidate(core, target, wolvesDenKind);
    }

    private ClientActionAttemptOutcome TryUseOnce(
        DarkKnightShadowbringerIntent intent,
        SupportedPvPContext context,
        bool configurationEnabled,
        bool metadataVerified,
        bool preserveBlackblood,
        bool blackbloodMetadataVerified,
        bool wolvesDenStrikingDummyMetadataVerified,
        int minimumHpPercent,
        int pressureLimitExclusive,
        bool actionHelpersSuppressedByGuard,
        bool higherPriorityClaimed,
        EmergencyActionInputFrame inputFrame,
        out bool attempted)
    {
        attempted = false;
        if (!intent.IsValid ||
            frozenTerritoryId == 0 ||
            frozenLocalAddress == nint.Zero ||
            frozenTargetAddress == nint.Zero)
        {
            return ClientActionAttemptOutcome.NotInvoked;
        }

        var attemptedAtBoundary = false;
        var softUnavailableAtBoundary = false;
        var before = default(ClientActionAttemptFingerprint);
        var after = default(ClientActionAttemptFingerprint);
        var adjustedBefore = 0u;
        var adjustedAfter = 0u;
        var targetStatusBefore = uint.MaxValue;
        var targetStatusAfter = uint.MaxValue;
        try
        {
            var clientAccepted = nearAssist.RunWithoutRedirect(() =>
            {
                var currentLocal = ResolveExactLocalPlayer(objectTable.LocalPlayer);
                if (currentLocal is null ||
                    currentLocal.Address != frozenLocalAddress ||
                    clientState.TerritoryType != frozenTerritoryId ||
                    context != intent.Context)
                {
                    return false;
                }

                var currentIdentity = new TargetPressureActorIdentity(
                    currentLocal.GameObjectId,
                    currentLocal.EntityId);
                var boundaryNow = Environment.TickCount64;
                var guardSuppressed = actionHelpersSuppressedByGuard ||
                                      IsCurrentlySuppressedByGuard(
                                          currentLocal,
                                          boundaryNow);
                if (!TryObserveNativeState(currentLocal, out var nativeState))
                    return false;

                var boundaryBlackbloodStatusPresent = HasExactStatusRow(
                    currentLocal,
                    DarkKnightShadowbringerRules.BlackbloodStatusId);
                if (boundaryBlackbloodStatusPresent)
                {
                    // Boundary presence is an immediate veto. Absence is not
                    // sampled here because the once-per-frame observer owns
                    // the two-sample consumption debounce.
                    blackbloodGate =
                        DarkKnightShadowbringerRules.ObserveBlackbloodGate(
                            blackbloodGate,
                            preserveBlackblood,
                            exactBlackbloodActive: true,
                            boundaryNow);
                }
                var boundaryConfigurationEnabled = configurationEnabled &&
                    DarkKnightShadowbringerRules.IsAutomaticCadenceReady(
                        lastAutomaticBoundaryAtMilliseconds,
                        boundaryNow) &&
                    (!preserveBlackblood ||
                     blackbloodMetadataVerified &&
                     blackbloodGate.IsDispatchAllowed);
                if (!boundaryConfigurationEnabled) return false;

                var hasDarkArts = HasActiveStatus(
                    currentLocal,
                    DarkKnightShadowbringerRules.DarkArtsStatusId);
                var incomingPressure = 0;
                var pressureKnown = context == SupportedPvPContext.WolvesDen ||
                    TryGetFreshSelfIncomingPressure(
                        currentIdentity,
                        boundaryNow,
                        out incomingPressure,
                        out _);
                var exactOpportunity = intent.Opportunity switch
                {
                    DarkKnightShadowbringerOpportunityKind.DarkArts =>
                        hasDarkArts &&
                        nativeState.ResolvedAdjustedActionId ==
                            DarkKnightShadowbringerRules
                                .DarkArtsShadowbringerActionId,
                    DarkKnightShadowbringerOpportunityKind.SafeHpCost =>
                        !hasDarkArts &&
                        nativeState.ResolvedAdjustedActionId ==
                            DarkKnightShadowbringerRules.ShadowbringerActionId &&
                        DarkKnightShadowbringerRules.IsSafeFallbackEligible(
                            currentLocal.CurrentHp,
                            currentLocal.MaxHp,
                            pressureKnown,
                            incomingPressure,
                            minimumHpPercent,
                            pressureLimitExclusive),
                    _ => false,
                };
                if (!exactOpportunity) return false;

                var candidate = ResolveExactCandidate(
                    currentLocal,
                    intent.Context,
                    intent.EnemySlot,
                    intent.Target,
                    intent.ExpectedAdjustedActionId,
                    wolvesDenStrikingDummyMetadataVerified,
                    frozenWolvesDenTargetKind,
                    checkCastingActive: true);
                if (candidate is null ||
                    candidate.Value.Target.Address != frozenTargetAddress)
                {
                    return false;
                }

                var exactKey = (VirtualKey)intent.FrozenKeyCode;
                var exactGenerationEligible =
                    inputFrame.IsFrozenGameplayKeyConsentValid(exactKey);
                if (!DarkKnightShadowbringerRules.CanUseFrozenIntent(
                        intent,
                        boundaryConfigurationEnabled,
                        context,
                        currentIdentity,
                        IsLivePlayer(currentLocal) && currentLocal.IsTargetable,
                        currentLocal.ClassJob.IsValid
                            ? currentLocal.ClassJob.RowId
                            : 0,
                        metadataVerified,
                        guardSuppressed,
                        higherPriorityClaimed,
                        darkArts,
                        fallback,
                        nativeState.ResolvedAdjustedActionId,
                        nativeState.ActionLocallyReady,
                        intent.FrozenKeyCode,
                        exactGenerationEligible,
                        candidate.Value.Core))
                {
                    return false;
                }

                var actionManager = ActionManager.Instance();
                if (actionManager == null) return false;
                targetStatusBefore = actionManager->GetActionStatus(
                    ActionType.Action,
                    intent.ExpectedAdjustedActionId,
                    intent.Target.GameObjectId,
                    checkRecastActive: true,
                    checkCastingActive: true);
                if (!nativeState.NativeBoundaryReady)
                {
                    softUnavailableAtBoundary = true;
                    return false;
                }

                adjustedBefore = actionManager->GetAdjustedActionId(
                    DarkKnightShadowbringerRules.ShadowbringerActionId);
                before = CaptureShadowbringerBoundary(
                    actionManager,
                    intent.ExpectedAdjustedActionId);
                if (adjustedBefore != intent.ExpectedAdjustedActionId ||
                    !before.IsExactActionReady(intent.ExpectedAdjustedActionId) ||
                    targetStatusBefore != 0)
                {
                    softUnavailableAtBoundary =
                        adjustedBefore == intent.ExpectedAdjustedActionId;
                    return false;
                }

                // Claim the exact opportunity before crossing the sole native
                // boundary. Explicit-false retries retain this same frozen
                // lease; no second selector can observe it as unspent.
                SpendOpportunity(intent);
                attemptedAtBoundary = true;
                var accepted = actionManager->UseAction(
                    ActionType.Action,
                    DarkKnightShadowbringerRules.ShadowbringerActionId,
                    intent.Target.GameObjectId,
                    0,
                    ActionManager.UseActionMode.None,
                    0);
                after = CaptureShadowbringerBoundary(
                    actionManager,
                    intent.ExpectedAdjustedActionId);
                adjustedAfter = actionManager->GetAdjustedActionId(
                    DarkKnightShadowbringerRules.ShadowbringerActionId);
                targetStatusAfter = actionManager->GetActionStatus(
                    ActionType.Action,
                    intent.ExpectedAdjustedActionId,
                    intent.Target.GameObjectId,
                    checkRecastActive: true,
                    checkCastingActive: true);
                return accepted;
            });

            if (!attemptedAtBoundary)
            {
                return softUnavailableAtBoundary
                    ? ClientActionAttemptOutcome.SoftUnavailable
                    : ClientActionAttemptOutcome.NotInvoked;
            }

            return DarkKnightShadowbringerRules.ClassifyBoundary(
                clientAccepted,
                intent.ExpectedAdjustedActionId,
                targetStatusBefore,
                targetStatusAfter,
                adjustedBefore,
                adjustedAfter,
                before,
                after);
        }
        catch (Exception exception)
        {
            LogFailure(
                exception,
                Environment.TickCount64,
                "Seiton Sense DRK held Shadowbringer native boundary failed.");
            return attemptedAtBoundary
                ? ClientActionAttemptOutcome.AcceptanceUnknown
                : softUnavailableAtBoundary
                    ? ClientActionAttemptOutcome.SoftUnavailable
                    : ClientActionAttemptOutcome.NotInvoked;
        }
        finally
        {
            attempted = attemptedAtBoundary;
        }
    }

    private static bool TryObserveNativeState(
        IPlayerCharacter localPlayer,
        out ShadowbringerNativeState state)
    {
        state = ShadowbringerNativeState.Unknown;
        if (!HasValidNativeIdentity(localPlayer) ||
            localPlayer.ClassJob.IsValid != true ||
            localPlayer.ClassJob.RowId !=
                DarkKnightShadowbringerRules.DarkKnightJobId)
        {
            return false;
        }

        var actionManager = ActionManager.Instance();
        if (actionManager == null) return false;
        var adjustedActionId = actionManager->GetAdjustedActionId(
            DarkKnightShadowbringerRules.ShadowbringerActionId);
        if (adjustedActionId is not
            (DarkKnightShadowbringerRules.ShadowbringerActionId or
             DarkKnightShadowbringerRules.DarkArtsShadowbringerActionId))
        {
            return false;
        }

        var recastGroup = actionManager->GetRecastGroup(
            (int)ActionType.Action,
            adjustedActionId);
        var recast = recastGroup < 0
            ? null
            : actionManager->GetRecastGroupDetail(recastGroup);
        var cooldownKnown =
            recastGroup ==
                DarkKnightShadowbringerRules.ExpectedRuntimeRecastGroupIndex &&
            recast != null &&
            actionManager->GetAdditionalRecastGroup(
                ActionType.Action,
                adjustedActionId) < 0 &&
            ActionManager.GetAdjustedRecastTime(
                ActionType.Action,
                adjustedActionId,
                true) ==
                DarkKnightShadowbringerRules
                    .ExpectedAdjustedRecastMilliseconds;
        var cooldownReady = cooldownKnown &&
                            !recast->IsActive &&
                            actionManager->IsActionOffCooldown(
                                ActionType.Action,
                                adjustedActionId);
        var resourcesReady = actionManager->CheckActionResources(
            ActionType.Action,
            adjustedActionId) == 0;
        var actionLocallyReady = cooldownReady && resourcesReady;
        var nativeBoundaryReady =
            HeldActionRetryRules.IsNativeBoundaryNearQueueable(
                actionManager->AnimationLock,
                localPlayer.IsCasting,
                actionManager->CastActionId,
                actionManager->ActionQueued);
        state = new ShadowbringerNativeState(
            adjustedActionId,
            cooldownKnown,
            cooldownReady,
            resourcesReady,
            actionLocallyReady,
            nativeBoundaryReady);
        return true;
    }

    /// <summary>
    /// Captures queue/acceptance evidence from the raw Shadowbringer carrier,
    /// while checking cooldown and resources against the exact adjusted action.
    /// This is required for low-HP Dark Arts, where raw 29091 still advertises
    /// the 12,000-HP cost but adjusted 29738 consumes status 3034 instead.
    /// </summary>
    private static ClientActionAttemptFingerprint CaptureShadowbringerBoundary(
        ActionManager* actionManager,
        uint expectedAdjustedActionId)
    {
        if (actionManager == null ||
            expectedAdjustedActionId is not
                (DarkKnightShadowbringerRules.ShadowbringerActionId or
                 DarkKnightShadowbringerRules.DarkArtsShadowbringerActionId))
        {
            return default;
        }

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
            actionManager->GetAdjustedActionId(
                DarkKnightShadowbringerRules.ShadowbringerActionId),
            actionManager->IsActionOffCooldown(
                ActionType.Action,
                expectedAdjustedActionId),
            actionManager->CheckActionResources(
                ActionType.Action,
                expectedAdjustedActionId));
    }

    private bool TryGetFreshSelfIncomingPressure(
        TargetPressureActorIdentity expectedLocalPlayer,
        long nowMilliseconds,
        out int incomingPressure,
        out long ageMilliseconds)
    {
        incomingPressure = 0;
        ageMilliseconds = -1;
        var current = pressureTracker.Snapshot;
        if (!expectedLocalPlayer.IsValid ||
            !current.Active ||
            !current.PressureActive ||
            current.LocalPlayer != expectedLocalPlayer ||
            current.PublishedAtMilliseconds < 0 ||
            nowMilliseconds < current.PublishedAtMilliseconds)
        {
            return false;
        }

        ageMilliseconds = nowMilliseconds - current.PublishedAtMilliseconds;
        if (ageMilliseconds >
            DarkKnightShadowbringerRules.MaximumPressureAgeMilliseconds)
        {
            ageMilliseconds = -1;
            return false;
        }

        incomingPressure = current.IncomingOpponents.Count;
        return incomingPressure is >= 0 and <= EnemySlotRules.LastSlot;
    }

    private DarkKnightShadowbringerCastCancellationLease?
        CreateCastCancellationLease(
            IPlayerCharacter? localPlayer,
            DarkKnightShadowbringerIntent intent,
            RuntimeCandidate candidate)
    {
        var actionManager = ActionManager.Instance();
        if (localPlayer is null ||
            actionManager == null ||
            !localPlayer.IsCasting ||
            actionManager->CastActionId == 0 ||
            actionManager->ActionQueued ||
            !float.IsFinite(actionManager->AnimationLock) ||
            actionManager->AnimationLock < 0f ||
            actionManager->AnimationLock >
                HeldCastCancellationRules
                    .MaximumCancellationAnimationLockSeconds ||
            candidate.Core.Actor != intent.Target)
        {
            return null;
        }

        var lease = new DarkKnightShadowbringerCastCancellationLease(
            intent.ExpectedAdjustedActionId,
            intent.LocalPlayer,
            intent.Target,
            intent.FrozenKeyCode,
            frozenIntentEpochToken);
        return lease.IsValid ? lease : null;
    }

    private bool IsCurrentlySuppressedByGuard(
        IPlayerCharacter? localPlayer,
        long nowMilliseconds)
    {
        if (localPlayer is null) return true;
        if (DefensiveUtilityProbe.HasActiveGuard(localPlayer)) return true;
        return nearAssist.TryGetRecentExactLocalGuardAttempt(
            clientState.TerritoryType,
            localPlayer.GameObjectId,
            localPlayer.EntityId,
            nowMilliseconds,
            DefensiveUtilityRules.GuardPropagationLatchMilliseconds,
            out _);
    }

    private IPlayerCharacter? ResolveExactLocalPlayer(IPlayerCharacter? expected)
    {
        if (!HasValidNativeIdentity(expected)) return null;
        var current = objectTable.LocalPlayer;
        if (!HasSameNativeIdentity(expected, current)) return null;
        var byObjectId = objectTable.SearchById(expected!.GameObjectId)
            as IPlayerCharacter;
        var byEntityId = objectTable.SearchByEntityId(expected.EntityId)
            as IPlayerCharacter;
        return HasSameNativeIdentity(expected, byObjectId) &&
               HasSameNativeIdentity(expected, byEntityId)
            ? expected
            : null;
    }

    private void SpendOpportunity(DarkKnightShadowbringerIntent intent)
    {
        SpendOpportunity(intent.Opportunity, intent.OpportunityGeneration);
    }

    private void SpendOpportunity(
        DarkKnightShadowbringerOpportunityKind opportunity,
        long generation)
    {
        switch (opportunity)
        {
            case DarkKnightShadowbringerOpportunityKind.DarkArts:
                darkArts = DarkKnightShadowbringerRules.MarkDarkArtsSpent(
                    darkArts,
                    generation);
                break;
            case DarkKnightShadowbringerOpportunityKind.SafeHpCost:
                fallback = DarkKnightShadowbringerRules.MarkFallbackSpent(
                    fallback,
                    generation);
                break;
        }
    }

    private void FreezeRuntime(
        DarkKnightShadowbringerIntent intent,
        uint territoryId,
        nint localAddress,
        nint targetAddress,
        DarkKnightWolvesDenTargetKind wolvesDenTargetKind)
    {
        frozenTerritoryId = territoryId;
        frozenLocalAddress = localAddress;
        frozenTargetAddress = targetAddress;
        frozenWolvesDenTargetKind = wolvesDenTargetKind;
        frozenIntentEpochToken = NextIntentEpochToken();
    }

    private ulong PrepareFrame(
        IPlayerCharacter? localPlayer,
        SupportedPvPContext context,
        long nowMilliseconds)
    {
        ClearPreparedFrame();
        if (nowMilliseconds < 0 ||
            context is not
                (SupportedPvPContext.CrystallineConflict or
                 SupportedPvPContext.WolvesDen) ||
            !HasValidNativeIdentity(localPlayer))
        {
            return 0;
        }

        var token = NextPreparedFrameToken();
        if (token == 0) return 0;
        preparedFrameToken = token;
        preparedFrameAtMilliseconds = nowMilliseconds;
        preparedFrameTerritoryId = clientState.TerritoryType;
        preparedFrameContext = context;
        preparedFrameLocalPlayer = new TargetPressureActorIdentity(
            localPlayer!.GameObjectId,
            localPlayer.EntityId);
        preparedFrameConsumed = false;
        return token;
    }

    private bool TryConsumePreparedFrame(
        ulong frameToken,
        IPlayerCharacter? localPlayer,
        SupportedPvPContext context,
        long nowMilliseconds)
    {
        if (frameToken == 0 ||
            preparedFrameToken != frameToken ||
            preparedFrameConsumed ||
            preparedFrameAtMilliseconds < 0 ||
            nowMilliseconds < preparedFrameAtMilliseconds ||
            nowMilliseconds - preparedFrameAtMilliseconds >
                DarkKnightShadowbringerRules.MaximumPressureAgeMilliseconds ||
            clientState.TerritoryType != preparedFrameTerritoryId ||
            context != preparedFrameContext ||
            !HasValidNativeIdentity(localPlayer))
        {
            return false;
        }

        var identity = new TargetPressureActorIdentity(
            localPlayer!.GameObjectId,
            localPlayer.EntityId);
        if (identity != preparedFrameLocalPlayer) return false;
        preparedFrameConsumed = true;
        return true;
    }

    private void ClearPreparedFrame()
    {
        preparedFrameToken = 0;
        preparedFrameAtMilliseconds = -1;
        preparedFrameTerritoryId = 0;
        preparedFrameContext = SupportedPvPContext.None;
        preparedFrameLocalPlayer = default;
        preparedFrameConsumed = false;
    }

    private bool FrozenRuntimeMatches(
        DarkKnightShadowbringerIntent intent,
        uint territoryId,
        nint localAddress) =>
        intent.IsValid &&
        frozenTerritoryId == territoryId &&
        frozenLocalAddress != nint.Zero &&
        frozenLocalAddress == localAddress &&
        frozenTargetAddress != nint.Zero &&
        frozenIntentEpochToken != 0;

    private void ClearFrozenRuntime()
    {
        frozenTerritoryId = 0;
        frozenLocalAddress = nint.Zero;
        frozenTargetAddress = nint.Zero;
        frozenWolvesDenTargetKind = DarkKnightWolvesDenTargetKind.None;
        frozenIntentEpochToken = 0;
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

    private ulong NextPreparedFrameToken()
    {
        while (true)
        {
            var current = Volatile.Read(ref nextPreparedFrameToken);
            var next = current >= long.MaxValue ? 1 : current + 1;
            if (Interlocked.CompareExchange(
                    ref nextPreparedFrameToken,
                    next,
                    current) == current)
            {
                return (ulong)next;
            }
        }
    }

    private DarkKnightShadowbringerProbeSnapshot PublishTerminalSnapshot(
        DarkKnightShadowbringerDecisionKind decision,
        DarkKnightShadowbringerDecisionReason reason,
        string message)
    {
        var result = DarkKnightShadowbringerProbeSnapshot.Initial with
        {
            Decision = decision,
            Reason = reason,
            AttemptCount = Interlocked.Read(ref attemptCount),
            AcceptedCount = Interlocked.Read(ref acceptedCount),
            RejectedCount = Interlocked.Read(ref rejectedCount),
            UnknownCount = Interlocked.Read(ref unknownCount),
            SoftWaitCount = Interlocked.Read(ref softWaitCount),
            LastEvent = message,
        };
        Volatile.Write(ref snapshot, result);
        return result;
    }

    private DarkKnightShadowbringerProbeSnapshot PublishDeferredFrameFailure()
    {
        var current = Snapshot;
        var result = current with
        {
            Decision = DarkKnightShadowbringerDecisionKind.None,
            Reason = DarkKnightShadowbringerDecisionReason.DeferredFrameInvalid,
            DeferredFrameToken = 0,
            CanRunDeferredSafeFallback = false,
            InputClaimed = false,
            CastCancellationLease = null,
            UseActionAttempted = false,
            UseActionAccepted = false,
            LastNativeOutcome = ClientActionAttemptOutcome.None,
            LastEvent = "Deferred HP-cost Shadowbringer frame token invalid",
        };
        Volatile.Write(ref snapshot, result);
        return result;
    }

    private static string DescribeNativeResult(
        DarkKnightShadowbringerIntent intent,
        ClientActionAttemptOutcome outcome,
        DarkKnightShadowbringerNativeAttemptDecision completion) =>
        outcome switch
        {
            ClientActionAttemptOutcome.ClientAccepted =>
                $"{intent.Opportunity} Shadowbringer client-accepted on " +
                FormatTarget(intent),
            ClientActionAttemptOutcome.ClientRejected when
                completion.RetryScheduled =>
                $"{intent.Opportunity} Shadowbringer client-rejected; exact intent retained",
            ClientActionAttemptOutcome.ClientRejected =>
                $"{intent.Opportunity} Shadowbringer retry limit reached",
            ClientActionAttemptOutcome.SoftUnavailable =>
                $"{intent.Opportunity} Shadowbringer waiting for native boundary",
            ClientActionAttemptOutcome.AcceptanceUnknown =>
                $"{intent.Opportunity} Shadowbringer acceptance ambiguous; intent terminal",
            _ => completion.Reason.ToString(),
        };

    private static string FormatTarget(DarkKnightShadowbringerIntent intent) =>
        intent.Context == SupportedPvPContext.CrystallineConflict
            ? $"S{intent.EnemySlot}"
            : "current <t>";

    private void LogFailure(
        Exception exception,
        long nowMilliseconds,
        string message)
    {
        if (nowMilliseconds >= 0 && nowMilliseconds < nextErrorLogAt) return;
        nextErrorLogAt = nowMilliseconds < 0
            ? 0
            : nowMilliseconds > long.MaxValue - 10_000
                ? long.MaxValue
                : nowMilliseconds + 10_000;
        log.Error(exception, message);
    }

    private static bool IsLivePlayer(IPlayerCharacter? player) =>
        HasValidNativeIdentity(player) &&
        !player!.IsDead &&
        player.CurrentHp > 0 &&
        player.MaxHp >= player.CurrentHp;

    private static bool IsLiveBattleCharacter(IBattleChara? actor) =>
        HasValidNativeIdentity(actor) &&
        !actor!.IsDead &&
        actor.CurrentHp > 0 &&
        actor.MaxHp >= actor.CurrentHp;

    private static bool HasActiveGuard(IBattleChara actor) =>
        HasActiveStatus(actor, EnemyCombatConstants.GuardStatusId) ||
        HasActiveStatus(actor, EnemyCombatConstants.GuardStatusAlternateId);

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

    private static bool HasExactStatusRow(IBattleChara actor, uint statusId)
    {
        foreach (var status in actor.StatusList)
        {
            // Presence is authoritative for the preservation gate. In
            // particular, an expiry-edge row with zero or non-finite remaining
            // time must stay blocked until the row itself disappears stably.
            if (status.StatusId == statusId) return true;
        }

        return false;
    }

    private static bool HasValidNativeIdentity(IGameObject? actor)
    {
        if (actor is null ||
            actor.Address == nint.Zero ||
            !IsNetworkObjectId(actor.GameObjectId) ||
            !IsNetworkEntityId(actor.EntityId))
        {
            return false;
        }

        var native = (GameObject*)actor.Address;
        return native != null && native->EntityId == actor.EntityId;
    }

    private static bool HasSameNativeIdentity(
        IGameObject? left,
        IGameObject? right) =>
        HasValidNativeIdentity(left) &&
        HasValidNativeIdentity(right) &&
        left!.Address == right!.Address &&
        left.GameObjectId == right.GameObjectId &&
        left.EntityId == right.EntityId;

    private static GameObject* GetNativeObject(IGameObject actor)
    {
        var native = (GameObject*)actor.Address;
        return native != null && native->EntityId == actor.EntityId
            ? native
            : null;
    }

    private static bool IsNetworkEntityId(uint entityId) =>
        entityId is not 0 and not 0xE0000000 and not uint.MaxValue;

    private static bool IsNetworkObjectId(ulong gameObjectId) =>
        gameObjectId is not 0 and not 0xE0000000UL and not ulong.MaxValue;

    private readonly record struct RuntimeCandidate(
        DarkKnightShadowbringerCandidate Core,
        IBattleChara Target,
        DarkKnightWolvesDenTargetKind WolvesDenTargetKind);

    private readonly record struct ShadowbringerNativeState(
        uint ResolvedAdjustedActionId,
        bool CooldownStateKnown,
        bool CooldownReady,
        bool ResourcesReady,
        bool ActionLocallyReady,
        bool NativeBoundaryReady)
    {
        internal static ShadowbringerNativeState Unknown => default;
    }
}
