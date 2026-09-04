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

internal enum AstrologianHarmonicOrbisProbePhase : byte
{
    Waiting = 0,
    BaseBuffered = 1,
    AwaitingDoubleCast = 2,
    FollowUpBuffered = 3,
}

internal enum AstrologianHarmonicOrbisProbeDecision : byte
{
    None = 0,
    Waiting = 1,
    Dispatch = 2,
    Cancelled = 3,
}

internal sealed record AstrologianHarmonicOrbisProbeSnapshot(
    AstrologianHarmonicOrbisProbeDecision Decision,
    AstrologianHarmonicOrbisProbePhase Phase,
    uint ResolvedActionId,
    uint AdjustedDoubleCastActionId,
    int CandidateCount,
    int PartySlot,
    ulong TargetGameObjectId,
    uint TargetEntityId,
    bool TargetIsSelf,
    uint TargetCurrentHp,
    uint TargetMaximumHp,
    bool PreferIncomingPressure,
    bool DoubleCastWasReadyBeforeBase,
    uint DoubleCastChargesBeforeBase,
    long TransitionRemainingMilliseconds,
    bool LocallyReady,
    bool NativeBoundaryReady,
    VirtualKey HeldGameplayKey,
    bool InputClaimed,
    HeldCastCancellationRequest? CastCancellationRequest,
    bool UseActionAttempted,
    bool UseActionAccepted,
    ClientActionAttemptOutcome LastNativeOutcome,
    int NativeAttemptCount,
    long BaseAttemptCount,
    long BaseAcceptedCount,
    long FollowUpAttemptCount,
    long FollowUpAcceptedCount,
    long RejectedCount,
    long UnknownCount,
    long SoftWaitCount,
    string SelectionReason,
    string LastEvent)
{
    internal static AstrologianHarmonicOrbisProbeSnapshot Initial { get; } = new(
        AstrologianHarmonicOrbisProbeDecision.None,
        AstrologianHarmonicOrbisProbePhase.Waiting,
        0,
        0,
        0,
        0,
        0,
        0,
        false,
        0,
        0,
        false,
        false,
        0,
        0,
        false,
        false,
        VirtualKey.NO_KEY,
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
        0,
        0,
        "Not evaluated",
        "Waiting for AST held healing");
}

/// <summary>
/// Uses one held gameplay-key generation to heal one exact Near Help party
/// target with Aspected Benefic (Harmonischer Orbis). If and only if an
/// Double Cast charge was already available immediately before the accepted
/// base request, the probe waits briefly for the transforming carrier to become
/// the exact same-target follow-up and requests that follow-up once. A stale
/// previously prepared form may propagate briefly but is never dispatched.
/// </summary>
internal sealed unsafe class AstrologianHarmonicOrbisProbe
{
    internal const uint AstrologianJobId =
        AstrologianHarmonicOrbisRules.AstrologianJobId;
    internal const uint BaseActionId =
        AstrologianHarmonicOrbisRules.HarmonicOrbisActionId;
    internal const uint DoubleCastCarrierActionId =
        AstrologianHarmonicOrbisRules.DoubleCastCarrierActionId;
    internal const uint DoubleCastFollowUpActionId =
        AstrologianHarmonicOrbisRules.DoubleCastHarmonicOrbisActionId;
    internal const uint ExpectedActionIconId = 9_420;
    internal const int MaximumTargetHealthPercent =
        AstrologianHarmonicOrbisRules.MaximumTargetHealthPercent;
    internal const uint MaximumObservedDoubleCastCharges =
        AstrologianHarmonicOrbisRules.MaximumHarmonicOrbisCharges;
    internal const long DoubleCastTransitionMilliseconds = 1_500;
    private const long BufferedIntentLeaseMilliseconds = 1_500;
    private const long FollowUpIntentLeaseMilliseconds = 2_000;

    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly TargetPressureTracker pressureTracker;
    private readonly NearAssistRedirector nearAssist;
    private readonly IPluginLog log;
    private FrozenIntent? frozenIntent;
    private AstrologianHarmonicOrbisProbePhase phase =
        AstrologianHarmonicOrbisProbePhase.Waiting;
    private HeldActionRetryState retry = HeldActionRetryState.Initial;
    private AstrologianHarmonicOrbisBaseChargeEpochState baseChargeEpoch =
        AstrologianHarmonicOrbisBaseChargeEpochState.Initial;
    private AstrologianHarmonicOrbisIntent sequenceIntent;
    private ClientActionAttemptOutcome acceptedBaseOutcome =
        ClientActionAttemptOutcome.None;
    private ulong frameworkFrame;
    private long phaseExpiresAtMilliseconds = -1;
    private bool doubleCastWasReadyBeforeBase;
    private uint doubleCastChargesBeforeBase;
    private VirtualKey terminalHeldKey = VirtualKey.NO_KEY;
    private ulong nextIntentEpochToken;
    private long baseAttemptCount;
    private long baseAcceptedCount;
    private long followUpAttemptCount;
    private long followUpAcceptedCount;
    private long rejectedCount;
    private long unknownCount;
    private long softWaitCount;
    private long nextErrorLogAt;
    private string lastEvent = "Waiting for AST held healing";
    private AstrologianHarmonicOrbisProbeSnapshot snapshot =
        AstrologianHarmonicOrbisProbeSnapshot.Initial;

    internal AstrologianHarmonicOrbisProbe(
        IClientState clientState,
        IObjectTable objectTable,
        TargetPressureTracker pressureTracker,
        NearAssistRedirector nearAssist,
        IPluginLog log)
    {
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.pressureTracker = pressureTracker;
        this.nearAssist = nearAssist;
        this.log = log;
    }

    internal AstrologianHarmonicOrbisProbeSnapshot Snapshot =>
        Volatile.Read(ref snapshot);

    /// <summary>
    /// Pins the numeric action family to the English game rows. Runtime still
    /// proves the adjusted carrier, charge, cooldown, target, range, and LoS at
    /// the final native boundary. A changed or missing row disables the helper.
    /// </summary>
    internal static bool ValidateMetadata(IDataManager dataManager, IPluginLog log)
    {
        try
        {
            var actions = dataManager.GetExcelSheet<GameAction>(ClientLanguage.English);
            var descriptions = dataManager.GetExcelSheet<ActionTransient>(ClientLanguage.English);
            if (!actions.TryGetRow(BaseActionId, out var baseAction) ||
                !actions.TryGetRow(DoubleCastCarrierActionId, out var carrier) ||
                !actions.TryGetRow(DoubleCastFollowUpActionId, out var followUp) ||
                !descriptions.TryGetRow(BaseActionId, out var baseDescription) ||
                !descriptions.TryGetRow(DoubleCastCarrierActionId, out var carrierDescription) ||
                !descriptions.TryGetRow(DoubleCastFollowUpActionId, out var followUpDescription) ||
                !IsExpectedFriendlyAction(baseAction) ||
                !IsExpectedFriendlyAction(followUp) ||
                !string.Equals(
                    baseAction.Name.ExtractText(),
                    "Aspected Benefic",
                    StringComparison.Ordinal) ||
                !string.Equals(
                    followUp.Name.ExtractText(),
                    baseAction.Name.ExtractText(),
                    StringComparison.Ordinal) ||
                baseAction.Icon != ExpectedActionIconId ||
                followUp.Icon != ExpectedActionIconId ||
                baseAction.Range != followUp.Range ||
                baseAction.EffectRange != followUp.EffectRange ||
                baseAction.CastType != followUp.CastType ||
                !baseDescription.Description.ExtractText().Contains(
                    "Restores target's HP",
                    StringComparison.Ordinal) ||
                !followUpDescription.Description.ExtractText().Contains(
                    "Double Cast",
                    StringComparison.Ordinal) ||
                carrier.RowId != DoubleCastCarrierActionId ||
                !string.Equals(
                    carrier.Name.ExtractText(),
                    "Double Cast",
                    StringComparison.Ordinal) ||
                !carrier.IsPvP ||
                !AstrologianHarmonicOrbisRules.HasExpectedPlayerActionFlag(
                    carrier.RowId,
                    carrier.IsPlayerAction) ||
                !carrier.ClassJob.IsValid ||
                carrier.ClassJob.RowId != AstrologianJobId ||
                carrier.Cast100ms != 0 ||
                string.IsNullOrWhiteSpace(carrierDescription.Description.ExtractText()))
            {
                log.Warning(
                    "Seiton Sense AST Harmonischer Orbis metadata failed closed.");
                return false;
            }

            return true;
        }
        catch (Exception exception)
        {
            log.Warning(
                exception,
                "Seiton Sense AST Harmonischer Orbis metadata lookup failed closed.");
            return false;
        }
    }

    internal AstrologianHarmonicOrbisProbeSnapshot Observe(
        IPlayerCharacter? localPlayer,
        SupportedPvPContext context,
        bool configurationEnabled,
        bool metadataVerified,
        bool preferIncomingPressure,
        bool actionHelpersSuppressedByGuard,
        bool higherPriorityClaimed,
        EmergencyActionInputFrame inputFrame,
        long nowMilliseconds,
        bool hardReset = false)
    {
        try
        {
            return ObserveCore(
                localPlayer,
                context,
                configurationEnabled,
                metadataVerified,
                preferIncomingPressure,
                actionHelpersSuppressedByGuard,
                higherPriorityClaimed,
                inputFrame,
                nowMilliseconds,
                hardReset);
        }
        catch (Exception exception)
        {
            LogFailure(
                exception,
                nowMilliseconds,
                "Seiton Sense AST Harmonischer Orbis probe failed closed.");
            return FailClosed();
        }
    }

    internal void Reset()
    {
        ClearEpisode();
        baseChargeEpoch = AstrologianHarmonicOrbisBaseChargeEpochState.Initial;
        terminalHeldKey = VirtualKey.NO_KEY;
        lastEvent = "Reset";
        Publish(AstrologianHarmonicOrbisProbeDecision.None, lastEvent);
    }

    internal AstrologianHarmonicOrbisProbeSnapshot FailClosed()
    {
        var failedKey = frozenIntent is { IsValid: true } intent
            ? intent.HeldKey
            : terminalHeldKey;
        ClearEpisode();
        terminalHeldKey = failedKey;
        lastEvent = "Failed closed";
        return Publish(AstrologianHarmonicOrbisProbeDecision.Cancelled, lastEvent);
    }

    private AstrologianHarmonicOrbisProbeSnapshot ObserveCore(
        IPlayerCharacter? localPlayer,
        SupportedPvPContext context,
        bool configurationEnabled,
        bool metadataVerified,
        bool preferIncomingPressure,
        bool actionHelpersSuppressedByGuard,
        bool higherPriorityClaimed,
        EmergencyActionInputFrame inputFrame,
        long nowMilliseconds,
        bool hardReset)
    {
        var effectiveHardReset = hardReset || nowMilliseconds < 0;
        frameworkFrame = NextFrameworkFrame(frameworkFrame);
        var currentFrameworkFrame = frameworkFrame;
        if (terminalHeldKey != VirtualKey.NO_KEY &&
            inputFrame.Snapshot.ProbeSucceeded &&
            !inputFrame.IsGameplayKeyPhysicallyDown(terminalHeldKey))
        {
            terminalHeldKey = VirtualKey.NO_KEY;
        }

        var exactLocal = ResolveExactLocalPlayer(localPlayer, out var localIdentity);
        var localAlive = IsLivePlayer(exactLocal);
        var localJobId = exactLocal?.ClassJob.IsValid == true
            ? exactLocal.ClassJob.RowId
            : 0;
        var supportedContext = context is SupportedPvPContext.CrystallineConflict or
            SupportedPvPContext.WolvesDen;
        var resetChargeEpoch = effectiveHardReset ||
                               !configurationEnabled ||
                               !metadataVerified ||
                               !supportedContext ||
                               localJobId != AstrologianJobId;
        var observedBaseCharges = 0u;
        var chargeCountKnown = !resetChargeEpoch &&
                               TryReadBaseChargeCount(
                                   exactLocal,
                                   out observedBaseCharges);
        baseChargeEpoch = AstrologianHarmonicOrbisRules.ObserveBaseChargeEpoch(
            baseChargeEpoch,
            chargeCountKnown,
            chargeCountKnown ? observedBaseCharges : 0,
            hardReset: resetChargeEpoch);
        var guardSuppressed = actionHelpersSuppressedByGuard ||
                              (exactLocal is not null &&
                               IsCurrentlySuppressedByGuard(exactLocal, nowMilliseconds));
        var featureGateReady = !effectiveHardReset &&
                               configurationEnabled &&
                               metadataVerified &&
                               supportedContext &&
                               localAlive &&
                               exactLocal!.IsTargetable &&
                               localJobId == AstrologianJobId &&
                               !guardSuppressed &&
                               inputFrame.Snapshot.ProbeSucceeded &&
                               !inputFrame.Snapshot.IsTextInputActive;

        if (frozenIntent is { IsValid: true } currentIntent &&
            (!featureGateReady ||
             currentIntent.Context != context ||
             currentIntent.TerritoryId != clientState.TerritoryType ||
             currentIntent.LocalPlayer != localIdentity ||
             currentIntent.LocalAddress != exactLocal?.Address ||
             !inputFrame.IsFrozenGameplayKeyConsentValid(currentIntent.HeldKey)))
        {
            ClearEpisode();
            lastEvent = "Frozen AST heal cancelled by context, identity, Guard, or held-key drift";
        }
        else if (effectiveHardReset)
        {
            ClearEpisode();
            terminalHeldKey = VirtualKey.NO_KEY;
            lastEvent = "Hard reset";
        }

        var candidateCount = 0;
        var selectionReason = "Not evaluated";
        RuntimeCandidate? observedCandidate = null;
        var resolvedActionId = 0u;
        var adjustedDoubleCastActionId = 0u;
        var actionLocallyReady = false;
        var nativeBoundaryReady = false;
        var inputClaimed = false;
        HeldCastCancellationRequest? castCancellationRequest = null;
        var attempted = false;
        var accepted = false;
        var nativeOutcome = ClientActionAttemptOutcome.None;
        var decision = AstrologianHarmonicOrbisProbeDecision.None;

        if (featureGateReady && frozenIntent is not null)
        {
            observedCandidate = phase ==
                AstrologianHarmonicOrbisProbePhase.AwaitingDoubleCast
                ? ResolveFrozenPartyIdentity(
                    exactLocal!,
                    frozenIntent.Value)
                : ResolveFrozenCandidate(
                    exactLocal!,
                    frozenIntent.Value,
                    phase == AstrologianHarmonicOrbisProbePhase.BaseBuffered
                        ? AstrologianHarmonicOrbisRules.BaseDispatchAction
                            .ExpectedAdjustedActionId
                        : AstrologianHarmonicOrbisRules.DoubleCastDispatchAction
                            .ExpectedAdjustedActionId,
                    requireHealthThreshold:
                        phase == AstrologianHarmonicOrbisProbePhase.BaseBuffered);

            if (observedCandidate is null)
            {
                ClearEpisode();
                lastEvent = phase ==
                    AstrologianHarmonicOrbisProbePhase.AwaitingDoubleCast
                    ? "Frozen AST heal target failed exact party, identity, life, or targetability revalidation"
                    : "Frozen AST heal target failed exact party, identity, health, range, or LoS revalidation";
                decision = AstrologianHarmonicOrbisProbeDecision.Cancelled;
            }
        }

        if (featureGateReady &&
            frozenIntent is { IsValid: true } transitionIntent &&
            phase == AstrologianHarmonicOrbisProbePhase.AwaitingDoubleCast)
        {
            var actionManager = ActionManager.Instance();
            adjustedDoubleCastActionId = actionManager == null
                ? 0
                : actionManager->GetAdjustedActionId(DoubleCastCarrierActionId);
            if (nowMilliseconds > phaseExpiresAtMilliseconds)
            {
                ClearEpisode();
                lastEvent = "Base accepted; Double Cast transition expired, base-only terminal";
                decision = AstrologianHarmonicOrbisProbeDecision.Waiting;
            }
            else
            {
                var followUpDecision =
                    AstrologianHarmonicOrbisRules.EvaluateFollowUp(
                        sequenceIntent,
                        acceptedBaseOutcome,
                        currentFrameworkFrame,
                        observedCandidate?.Identity ?? default,
                        targetStillEligible: observedCandidate is not null,
                        adjustedDoubleCastActionId);
                switch (followUpDecision.Kind)
                {
                    case AstrologianHarmonicOrbisFollowUpKind.Dispatch:
                        phase = AstrologianHarmonicOrbisProbePhase.FollowUpBuffered;
                        retry = HeldActionRetryState.Initial;
                        phaseExpiresAtMilliseconds = SaturatingAdd(
                            nowMilliseconds,
                            FollowUpIntentLeaseMilliseconds);
                        observedCandidate = ResolveFrozenCandidate(
                            exactLocal!,
                            transitionIntent,
                            followUpDecision.Action.ExpectedAdjustedActionId,
                            requireHealthThreshold: false);
                        lastEvent = "Exact Double Cast Orbis follow-up exposed";
                        break;
                    case AstrologianHarmonicOrbisFollowUpKind.Waiting:
                        decision = AstrologianHarmonicOrbisProbeDecision.Waiting;
                        lastEvent = $"Base accepted; waiting for exact Double Cast Orbis transition ({followUpDecision.Reason})";
                        // Keep lower-priority held helpers from replacing the
                        // spell that Double Cast is still transforming into.
                        // Purify, Guardian, Recuperate, and Auto-Guard have
                        // already had their chance before this lane.
                        if (!higherPriorityClaimed && !inputFrame.IsConsumed)
                        {
                            inputClaimed = true;
                            inputFrame.Consume();
                        }
                        break;
                    case AstrologianHarmonicOrbisFollowUpKind.Complete:
                        ClearEpisode();
                        decision = AstrologianHarmonicOrbisProbeDecision.Waiting;
                        lastEvent = "Base accepted; snapshotted Double Cast unavailable, base-only terminal";
                        break;
                    default:
                        ClearEpisode();
                        decision = AstrologianHarmonicOrbisProbeDecision.Cancelled;
                        lastEvent = $"Base accepted; Double Cast follow-up cancelled ({followUpDecision.Reason})";
                        break;
                }
            }
        }

        if (featureGateReady &&
            frozenIntent is null &&
            terminalHeldKey == VirtualKey.NO_KEY &&
            !higherPriorityClaimed &&
            !inputFrame.IsConsumed &&
            inputFrame.HeldGameplayKeyEligible &&
            baseChargeEpoch.HasAvailableEpoch &&
            TryReadActionState(
                exactLocal!,
                AstrologianHarmonicOrbisRules.BaseDispatchAction,
                out resolvedActionId,
                out actionLocallyReady,
                out nativeBoundaryReady,
                out adjustedDoubleCastActionId) &&
            actionLocallyReady)
        {
            var candidates = ResolveCandidates(
                exactLocal!,
                BaseActionId,
                preferIncomingPressure,
                out selectionReason);
            candidateCount = candidates.Count;
            var selection = AstrologianHarmonicOrbisRules.SelectBestTarget(
                candidates.Select(static candidate => candidate.Selection).ToArray(),
                preferIncomingPressure,
                preferIncomingPressure && pressureTracker.HasActiveIncomingAllyPressureView);
            if (selection.SelectedIndex >= 0 &&
                selection.SelectedIndex < candidates.Count)
            {
                observedCandidate = candidates[selection.SelectedIndex];
                var heldKey = inputFrame.Snapshot.HeldGameplayKey;
                frozenIntent = new FrozenIntent(
                    localIdentity,
                    observedCandidate.Value.Identity,
                    exactLocal!.Address,
                    observedCandidate.Value.Address,
                    clientState.TerritoryType,
                    context,
                    observedCandidate.Value.Selection.PartySlot,
                    observedCandidate.Value.Selection.IsSelf,
                    heldKey,
                    NextIntentEpochToken(),
                    nowMilliseconds);
                _ = inputFrame.IsFrozenGameplayKeyConsentValid(heldKey);
                phase = AstrologianHarmonicOrbisProbePhase.BaseBuffered;
                retry = HeldActionRetryState.Initial;
                phaseExpiresAtMilliseconds = SaturatingAdd(
                    nowMilliseconds,
                    BufferedIntentLeaseMilliseconds);
                selectionReason = selection.Reason.ToString();
                lastEvent = $"Frozen P{observedCandidate.Value.Selection.PartySlot} AST heal at {HealthPercent(observedCandidate.Value.Selection):0.0}% HP";
            }
            else
            {
                decision = AstrologianHarmonicOrbisProbeDecision.Waiting;
                lastEvent = "No exact reachable Near Help target at or below 60% HP";
            }
        }

        if (featureGateReady &&
            frozenIntent is { IsValid: true } bufferedIntent &&
            phase is AstrologianHarmonicOrbisProbePhase.BaseBuffered or
                AstrologianHarmonicOrbisProbePhase.FollowUpBuffered)
        {
            var dispatchAction = phase ==
                AstrologianHarmonicOrbisProbePhase.BaseBuffered
                ? AstrologianHarmonicOrbisRules.BaseDispatchAction
                : AstrologianHarmonicOrbisRules.DoubleCastDispatchAction;
            var actionId = dispatchAction.ExpectedAdjustedActionId;
            resolvedActionId = actionId;
            if (nowMilliseconds > phaseExpiresAtMilliseconds)
            {
                ClearEpisode();
                lastEvent = $"AST action {actionId} frozen lease expired";
                decision = AstrologianHarmonicOrbisProbeDecision.Cancelled;
            }
            else
            {
                observedCandidate = ResolveFrozenCandidate(
                    exactLocal!,
                    bufferedIntent,
                    actionId,
                    requireHealthThreshold: actionId == BaseActionId);
                var actionStateReadable = TryReadActionState(
                    exactLocal!,
                    dispatchAction,
                    out var finalResolvedActionId,
                    out actionLocallyReady,
                    out nativeBoundaryReady,
                    out adjustedDoubleCastActionId);
                var exactIntentValid = observedCandidate is not null &&
                                       finalResolvedActionId == actionId;
                var retainsSchedulerFrame = HeldActionRetryRules.RetainsSchedulerFrame(
                    retry,
                    nowMilliseconds,
                    exactIntentValid,
                    actionStateReadable && actionLocallyReady,
                    targetSpecificReady: observedCandidate is not null);
                if (retainsSchedulerFrame &&
                    !higherPriorityClaimed &&
                    !inputFrame.IsConsumed)
                {
                    inputClaimed = true;
                    inputFrame.Consume();
                    decision = AstrologianHarmonicOrbisProbeDecision.Dispatch;
                    if (!nativeBoundaryReady)
                    {
                        castCancellationRequest = BuildCastCancellationRequest(
                            exactLocal!,
                            bufferedIntent,
                            actionId,
                            actionLocallyReady,
                            observedCandidate is not null,
                            inputClaimed);
                        lastEvent = castCancellationRequest is { IsValid: true }
                            ? $"AST action {actionId} requested same-intent cast cancellation"
                            : $"AST action {actionId} waiting for native queue/animation boundary";
                    }
                    else if (!HeldActionRetryRules.CanAttemptFrozenIntent(
                                 retry,
                                 nowMilliseconds))
                    {
                        lastEvent = $"AST action {actionId} retaining bounded retry throttle";
                    }
                    else if (observedCandidate is { } exactCandidate)
                    {
                        var canDispatch = true;
                        if (actionId == BaseActionId && !sequenceIntent.IsValid)
                        {
                            var preReady = TrySnapshotDoubleCastAvailability(
                                out var chargesBeforeBase,
                                out adjustedDoubleCastActionId);
                            canDispatch =
                                baseChargeEpoch.HasAvailableEpoch &&
                                AstrologianHarmonicOrbisRules.TryCreateIntent(
                                    exactCandidate.Selection,
                                    preReady,
                                    baseChargeEpoch.CurrentEpochToken,
                                    currentFrameworkFrame,
                                    out sequenceIntent);
                            if (canDispatch)
                            {
                                doubleCastWasReadyBeforeBase = preReady;
                                doubleCastChargesBeforeBase = preReady
                                    ? chargesBeforeBase
                                    : 0;
                            }
                        }
                        else if (actionId == BaseActionId)
                        {
                            canDispatch = baseChargeEpoch.HasAvailableEpoch &&
                                          sequenceIntent.BaseChargeEpochToken ==
                                          baseChargeEpoch.CurrentEpochToken;
                        }

                        if (!canDispatch ||
                            (actionId == DoubleCastFollowUpActionId &&
                             !sequenceIntent.IsValid))
                        {
                            ClearEpisode();
                            decision = AstrologianHarmonicOrbisProbeDecision.Cancelled;
                            lastEvent = "AST exact target/charge sequence intent failed closed";
                        }
                        else
                        {
                            nativeOutcome = TryDispatchOnce(
                                exactLocal!,
                                bufferedIntent,
                                exactCandidate,
                                dispatchAction,
                                out attempted);
                            accepted = nativeOutcome ==
                                ClientActionAttemptOutcome.ClientAccepted;
                            var attemptDescription = DescribeAttempt(
                                actionId,
                                nativeOutcome);
                            CompleteAttempt(
                                bufferedIntent,
                                dispatchAction,
                                nativeOutcome,
                                nowMilliseconds);
                            lastEvent = attemptDescription;
                        }
                    }
                }
                else if (!exactIntentValid)
                {
                    ClearEpisode();
                    lastEvent = $"AST action {actionId} exact frozen intent drifted";
                    decision = AstrologianHarmonicOrbisProbeDecision.Cancelled;
                }
                else
                {
                    decision = AstrologianHarmonicOrbisProbeDecision.Waiting;
                    lastEvent = higherPriorityClaimed || inputFrame.IsConsumed
                        ? $"AST action {actionId} yielded to Purify/higher priority"
                        : $"AST action {actionId} not structurally ready";
                }
            }
        }

        if (attempted)
        {
            if (resolvedActionId == BaseActionId)
                Interlocked.Increment(ref baseAttemptCount);
            else if (resolvedActionId == DoubleCastFollowUpActionId)
                Interlocked.Increment(ref followUpAttemptCount);
        }
        if (accepted)
        {
            if (resolvedActionId == BaseActionId)
                Interlocked.Increment(ref baseAcceptedCount);
            else if (resolvedActionId == DoubleCastFollowUpActionId)
                Interlocked.Increment(ref followUpAcceptedCount);
        }
        if (nativeOutcome == ClientActionAttemptOutcome.ClientRejected)
            Interlocked.Increment(ref rejectedCount);
        if (nativeOutcome == ClientActionAttemptOutcome.AcceptanceUnknown)
            Interlocked.Increment(ref unknownCount);
        if (nativeOutcome == ClientActionAttemptOutcome.SoftUnavailable)
            Interlocked.Increment(ref softWaitCount);

        var activeIntent = frozenIntent;
        var selected = observedCandidate?.Selection;
        var result = new AstrologianHarmonicOrbisProbeSnapshot(
            decision,
            phase,
            resolvedActionId,
            adjustedDoubleCastActionId,
            candidateCount,
            activeIntent?.PartySlot ?? selected?.PartySlot ?? 0,
            activeIntent?.Target.GameObjectId ?? selected?.GameObjectId ?? 0,
            activeIntent?.Target.EntityId ?? selected?.EntityId ?? 0,
            activeIntent?.TargetIsSelf ?? selected?.IsSelf ?? false,
            selected?.CurrentHp ?? 0,
            selected?.MaximumHp ?? 0,
            preferIncomingPressure,
            doubleCastWasReadyBeforeBase,
            doubleCastChargesBeforeBase,
            phase == AstrologianHarmonicOrbisProbePhase.AwaitingDoubleCast &&
            phaseExpiresAtMilliseconds >= 0
                ? Math.Max(0, phaseExpiresAtMilliseconds - nowMilliseconds)
                : 0,
            actionLocallyReady,
            nativeBoundaryReady,
            activeIntent?.HeldKey ?? inputFrame.Snapshot.HeldGameplayKey,
            inputClaimed,
            castCancellationRequest,
            attempted,
            accepted,
            nativeOutcome,
            retry.NativeAttemptCount,
            Interlocked.Read(ref baseAttemptCount),
            Interlocked.Read(ref baseAcceptedCount),
            Interlocked.Read(ref followUpAttemptCount),
            Interlocked.Read(ref followUpAcceptedCount),
            Interlocked.Read(ref rejectedCount),
            Interlocked.Read(ref unknownCount),
            Interlocked.Read(ref softWaitCount),
            selectionReason,
            lastEvent);
        Volatile.Write(ref snapshot, result);
        return result;
    }

    private IReadOnlyList<RuntimeCandidate> ResolveCandidates(
        IPlayerCharacter localPlayer,
        uint actionId,
        bool preferIncomingPressure,
        out string resolution)
    {
        var sourceObject = GetNativeObject(localPlayer);
        if (sourceObject == null)
        {
            resolution = "Local native actor unavailable";
            return [];
        }

        var partySlots = new Dictionary<uint, int>(8);
        for (var slot = NearHelpSelectionRules.FirstPartySlot;
             slot <= NearHelpSelectionRules.LastPartySlot;
             slot++)
        {
            var partyMember = PartySlotResolver.Resolve(objectTable, slot);
            if (TryGetExactIdentity(partyMember, out var partyIdentity) &&
                IsLivePlayer(partyMember))
            {
                partySlots.TryAdd(partyIdentity.EntityId, slot);
            }
        }

        var candidates = new List<RuntimeCandidate>(9);
        var seenActors = new HashSet<TargetPressureActorIdentity>();
        AddCandidate(
            localPlayer,
            localPlayer,
            partySlots.GetValueOrDefault(localPlayer.EntityId),
            actionId,
            preferIncomingPressure,
            sourceObject,
            seenActors,
            candidates);
        foreach (var player in objectTable.PlayerObjects.OfType<IPlayerCharacter>())
        {
            if (player.EntityId == localPlayer.EntityId ||
                !partySlots.TryGetValue(player.EntityId, out var partySlot))
            {
                continue;
            }

            AddCandidate(
                localPlayer,
                player,
                partySlot,
                actionId,
                preferIncomingPressure,
                sourceObject,
                seenActors,
                candidates);
        }

        resolution = $"Exact Near Help candidates {candidates.Count}, pressure-view={pressureTracker.HasActiveIncomingAllyPressureView}";
        return candidates;
    }

    private void AddCandidate(
        IPlayerCharacter localPlayer,
        IPlayerCharacter target,
        int partySlot,
        uint actionId,
        bool preferIncomingPressure,
        GameObject* sourceObject,
        HashSet<TargetPressureActorIdentity> seenActors,
        List<RuntimeCandidate> candidates)
    {
        if (!TryGetExactIdentity(target, out var targetIdentity) ||
            !seenActors.Add(targetIdentity) ||
            !IsLivePlayer(target) ||
            !target.IsTargetable)
        {
            return;
        }

        var targetObject = GetNativeObject(target);
        var validTarget = targetObject != null;
        var rangeResult = validTarget
            ? ActionManager.GetActionInRangeOrLoS(actionId, sourceObject, targetObject)
            : uint.MaxValue;
        int? pressure = null;
        if (preferIncomingPressure &&
            pressureTracker.TryGetIncomingAllyPressure(
                target.GameObjectId,
                target.EntityId,
                out var pressureCount))
        {
            pressure = pressureCount;
        }

        var isSelf = target.EntityId == localPlayer.EntityId &&
                     target.GameObjectId == localPlayer.GameObjectId;
        var selection = new NearHelpSelectionCandidate(
            target.GameObjectId,
            target.EntityId,
            partySlot,
            target.CurrentHp,
            target.MaxHp,
            System.Numerics.Vector3.DistanceSquared(localPlayer.Position, target.Position),
            IsExactFriendly: true,
            IsSelf: isSelf,
            HasValidActionTarget: validTarget,
            HasRangeAndLineOfSight:
                validTarget && SeitonRangeRules.HasNativeRangeAndLineOfSight(rangeResult),
            UniqueIncomingEnemyPressureCount: pressure,
            IsActionSelfTargetable: isSelf);
        candidates.Add(new RuntimeCandidate(selection, targetIdentity, target.Address));
    }

    private RuntimeCandidate? ResolveFrozenCandidate(
        IPlayerCharacter localPlayer,
        FrozenIntent intent,
        uint actionId,
        bool requireHealthThreshold)
    {
        if (!intent.IsValid ||
            actionId is not (BaseActionId or DoubleCastFollowUpActionId) ||
            !TryGetExactIdentity(localPlayer, out var localIdentity) ||
            localIdentity != intent.LocalPlayer ||
            localPlayer.Address != intent.LocalAddress)
        {
            return null;
        }

        IPlayerCharacter? target;
        if (intent.TargetIsSelf)
        {
            target = localPlayer;
        }
        else
        {
            target = PartySlotResolver.Resolve(objectTable, intent.PartySlot);
        }

        if (!TryGetExactIdentity(target, out var targetIdentity) ||
            targetIdentity != intent.Target ||
            target!.Address != intent.TargetAddress ||
            !IsLivePlayer(target) ||
            !target.IsTargetable)
        {
            return null;
        }

        var sourceObject = GetNativeObject(localPlayer);
        var targetObject = GetNativeObject(target);
        if (sourceObject == null || targetObject == null)
            return null;
        var rangeResult = ActionManager.GetActionInRangeOrLoS(
            actionId,
            sourceObject,
            targetObject);
        var selection = new NearHelpSelectionCandidate(
            target.GameObjectId,
            target.EntityId,
            intent.PartySlot,
            target.CurrentHp,
            target.MaxHp,
            System.Numerics.Vector3.DistanceSquared(localPlayer.Position, target.Position),
            IsExactFriendly: true,
            IsSelf: intent.TargetIsSelf,
            HasValidActionTarget: true,
            HasRangeAndLineOfSight:
                SeitonRangeRules.HasNativeRangeAndLineOfSight(rangeResult),
            UniqueIncomingEnemyPressureCount: null,
            IsActionSelfTargetable: intent.TargetIsSelf);
        if (!NearHelpSelectionRules.IsEligible(selection) ||
            (requireHealthThreshold &&
             !NearHelpSelectionRules.IsAtOrBelowHealthPercent(
                 selection,
                 MaximumTargetHealthPercent)))
        {
            return null;
        }

        return new RuntimeCandidate(selection, targetIdentity, target.Address);
    }

    /// <summary>
    /// During carrier propagation, validates only the frozen party actor. It
    /// intentionally does not query the not-yet-exposed adjusted follow-up for
    /// action status, range, or line of sight. Those native checks begin only
    /// after the carrier resolves exactly to the authored follow-up.
    /// </summary>
    private RuntimeCandidate? ResolveFrozenPartyIdentity(
        IPlayerCharacter localPlayer,
        FrozenIntent intent)
    {
        if (!intent.IsValid ||
            !TryGetExactIdentity(localPlayer, out var localIdentity) ||
            localIdentity != intent.LocalPlayer ||
            localPlayer.Address != intent.LocalAddress)
        {
            return null;
        }

        var target = intent.TargetIsSelf
            ? localPlayer
            : PartySlotResolver.Resolve(objectTable, intent.PartySlot);
        if (!TryGetExactIdentity(target, out var targetIdentity) ||
            targetIdentity != intent.Target ||
            target!.Address != intent.TargetAddress ||
            !IsLivePlayer(target) ||
            !target.IsTargetable ||
            GetNativeObject(localPlayer) == null ||
            GetNativeObject(target) == null)
        {
            return null;
        }

        var selection = new NearHelpSelectionCandidate(
            target.GameObjectId,
            target.EntityId,
            intent.PartySlot,
            target.CurrentHp,
            target.MaxHp,
            System.Numerics.Vector3.DistanceSquared(
                localPlayer.Position,
                target.Position),
            IsExactFriendly: true,
            IsSelf: intent.TargetIsSelf,
            HasValidActionTarget: true,
            HasRangeAndLineOfSight: false,
            UniqueIncomingEnemyPressureCount: null,
            IsActionSelfTargetable: intent.TargetIsSelf);
        return new RuntimeCandidate(selection, targetIdentity, target.Address);
    }

    private ClientActionAttemptOutcome TryDispatchOnce(
        IPlayerCharacter localPlayer,
        FrozenIntent intent,
        RuntimeCandidate candidate,
        AstrologianHarmonicOrbisDispatchAction dispatchAction,
        out bool attempted)
    {
        attempted = false;
        if (!dispatchAction.IsValid)
            return ClientActionAttemptOutcome.NotInvoked;

        var expectedAdjustedActionId = dispatchAction.ExpectedAdjustedActionId;
        var currentLocal = ResolveExactLocalPlayer(localPlayer, out var localIdentity);
        if (currentLocal is null ||
            localIdentity != intent.LocalPlayer ||
            IsCurrentlySuppressedByGuard(currentLocal, Environment.TickCount64))
        {
            return ClientActionAttemptOutcome.NotInvoked;
        }

        var exactCandidate = ResolveFrozenCandidate(
            currentLocal,
            intent,
            expectedAdjustedActionId,
            requireHealthThreshold: expectedAdjustedActionId == BaseActionId);
        if (exactCandidate is null ||
            exactCandidate.Value.Identity != candidate.Identity ||
            exactCandidate.Value.Address != candidate.Address)
        {
            return ClientActionAttemptOutcome.NotInvoked;
        }

        var actionManager = ActionManager.Instance();
        if (actionManager == null ||
            !TryReadActionState(
                currentLocal,
                dispatchAction,
                out var resolvedActionId,
                out var actionLocallyReady,
                out var nativeBoundaryReady,
                out _) ||
            resolvedActionId != expectedAdjustedActionId ||
            !actionLocallyReady ||
            !nativeBoundaryReady)
        {
            return ClientActionAttemptOutcome.SoftUnavailable;
        }

        var targetStatus = actionManager->GetActionStatus(
            ActionType.Action,
            expectedAdjustedActionId,
            intent.Target.GameObjectId,
            checkRecastActive: true,
            checkCastingActive: true);
        var before = CaptureDispatchBoundary(actionManager, dispatchAction);
        if (!before.IsExactActionReady(expectedAdjustedActionId) ||
            targetStatus != 0)
        {
            return ClientActionAttemptOutcome.SoftUnavailable;
        }

        attempted = true;
        var accepted = nearAssist.RunAstrologianHarmonicOrbisWithoutRedirect(
            dispatchAction.RawActionId,
            expectedAdjustedActionId,
            intent.LocalPlayer,
            intent.Target.GameObjectId,
            () => actionManager->UseAction(
                ActionType.Action,
                dispatchAction.RawActionId,
                intent.Target.GameObjectId,
                0,
                ActionManager.UseActionMode.None,
                0));
        return ClientActionAttemptBoundaryRules.Classify(
            accepted,
            expectedAdjustedActionId,
            before,
            CaptureDispatchBoundary(actionManager, dispatchAction));
    }

    private void CompleteAttempt(
        FrozenIntent intent,
        AstrologianHarmonicOrbisDispatchAction dispatchAction,
        ClientActionAttemptOutcome outcome,
        long nowMilliseconds)
    {
        var completion = HeldActionRetryRules.Complete(retry, nowMilliseconds, outcome);
        retry = completion.NextState;
        if (completion.Disposition == HeldActionRetryDisposition.SoftWait ||
            completion.RetryScheduled)
        {
            return;
        }

        if (outcome == ClientActionAttemptOutcome.ClientAccepted &&
            dispatchAction == AstrologianHarmonicOrbisRules.BaseDispatchAction)
        {
            if (!sequenceIntent.IsValid ||
                !AstrologianHarmonicOrbisRules.TrySpendBaseChargeEpoch(
                    baseChargeEpoch,
                    sequenceIntent.BaseChargeEpochToken,
                    out baseChargeEpoch))
            {
                terminalHeldKey = intent.HeldKey;
                ClearEpisode();
                return;
            }

            acceptedBaseOutcome = outcome;
            var actionManager = ActionManager.Instance();
            var adjustedDoubleCastActionId = actionManager == null
                ? 0
                : actionManager->GetAdjustedActionId(DoubleCastCarrierActionId);
            var followUpDecision =
                AstrologianHarmonicOrbisRules.EvaluateFollowUp(
                    sequenceIntent,
                    acceptedBaseOutcome,
                    frameworkFrame,
                    sequenceIntent.Target,
                    targetStillEligible: true,
                    adjustedDoubleCastActionId);
            if (followUpDecision.Kind ==
                AstrologianHarmonicOrbisFollowUpKind.Waiting)
            {
                phase = AstrologianHarmonicOrbisProbePhase.AwaitingDoubleCast;
                retry = HeldActionRetryState.Initial;
                phaseExpiresAtMilliseconds = SaturatingAdd(
                    nowMilliseconds,
                    DoubleCastTransitionMilliseconds);
                return;
            }

            ClearEpisode();
            return;
        }

        if (outcome == ClientActionAttemptOutcome.ClientAccepted &&
            dispatchAction == AstrologianHarmonicOrbisRules.DoubleCastDispatchAction)
        {
            ClearEpisode();
            return;
        }

        if (HeldActionRetryRules.ShouldLatchHeldKeyUntilRelease(
                completion.Disposition))
        {
            terminalHeldKey = intent.HeldKey;
        }
        ClearEpisode();
    }

    private HeldCastCancellationRequest? BuildCastCancellationRequest(
        IPlayerCharacter localPlayer,
        FrozenIntent intent,
        uint actionId,
        bool actionLocallyReady,
        bool targetReady,
        bool inputClaimed)
    {
        var actionManager = ActionManager.Instance();
        if (!inputClaimed ||
            !actionLocallyReady ||
            !targetReady ||
            actionManager == null ||
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

        var request = new HeldCastCancellationRequest(
            HeldCastCancellationHelperKind.AstrologianHarmonicOrbis,
            actionId,
            intent.LocalPlayer,
            intent.Target,
            (int)intent.HeldKey,
            intent.IntentEpochToken);
        return request.IsValid ? request : null;
    }

    private static bool TryReadActionState(
        IPlayerCharacter localPlayer,
        AstrologianHarmonicOrbisDispatchAction dispatchAction,
        out uint resolvedActionId,
        out bool actionLocallyReady,
        out bool nativeBoundaryReady,
        out uint adjustedDoubleCastActionId)
    {
        resolvedActionId = 0;
        actionLocallyReady = false;
        nativeBoundaryReady = false;
        adjustedDoubleCastActionId = 0;
        if (!localPlayer.ClassJob.IsValid ||
            localPlayer.ClassJob.RowId != AstrologianJobId ||
            GetNativeObject(localPlayer) == null ||
            !dispatchAction.IsValid)
        {
            return false;
        }

        var actionManager = ActionManager.Instance();
        if (actionManager == null) return false;
        adjustedDoubleCastActionId = actionManager->GetAdjustedActionId(
            DoubleCastCarrierActionId);
        var adjustedActionId = actionManager->GetAdjustedActionId(
            dispatchAction.RawActionId);
        if (adjustedActionId != dispatchAction.ExpectedAdjustedActionId)
            return true;

        resolvedActionId = dispatchAction.ExpectedAdjustedActionId;
        var fingerprint = CaptureDispatchBoundary(
            actionManager,
            dispatchAction);
        actionLocallyReady = fingerprint.Captured &&
                             fingerprint.AdjustedActionId ==
                                 dispatchAction.ExpectedAdjustedActionId &&
                             fingerprint.IsActionOffCooldown &&
                             fingerprint.ResourceStatus == 0;
        nativeBoundaryReady = actionLocallyReady &&
                              HeldActionRetryRules.IsNativeBoundaryNearQueueable(
                                  actionManager->AnimationLock,
                                  localPlayer.IsCasting,
                                  actionManager->CastActionId,
                                  actionManager->ActionQueued);
        return true;
    }

    private static bool TrySnapshotDoubleCastAvailability(
        out uint currentCharges,
        out uint adjustedActionId)
    {
        currentCharges = 0;
        adjustedActionId = 0;
        var actionManager = ActionManager.Instance();
        if (actionManager == null) return false;
        adjustedActionId = actionManager->GetAdjustedActionId(
            DoubleCastCarrierActionId);
        currentCharges = actionManager->GetCurrentCharges(
            DoubleCastCarrierActionId);
        // Double Cast is a transforming carrier. A ready charge can still show
        // the repeat for the previously used AST spell until Orbis is accepted.
        // The follow-up state machine separately requires the exact 29245 ->
        // 29247 transition before it permits the native dispatch.
        return AstrologianHarmonicOrbisRules.IsDoubleCastAvailableBeforeBase(
            adjustedActionId,
            actionManager->IsActionOffCooldown(
                ActionType.Action,
                DoubleCastCarrierActionId),
            currentCharges);
    }

    /// <summary>
    /// Captures queue evidence from the raw authored action while proving
    /// cooldown and resources against the exact adjusted action which will
    /// reach the server. Double Cast must be invoked as raw 29245 after it
    /// resolves to the non-player adjusted row 29247.
    /// </summary>
    private static ClientActionAttemptFingerprint CaptureDispatchBoundary(
        ActionManager* actionManager,
        AstrologianHarmonicOrbisDispatchAction dispatchAction)
    {
        if (actionManager == null || !dispatchAction.IsValid)
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
            actionManager->GetAdjustedActionId(dispatchAction.RawActionId),
            actionManager->IsActionOffCooldown(
                ActionType.Action,
                dispatchAction.ExpectedAdjustedActionId),
            actionManager->CheckActionResources(
                ActionType.Action,
                dispatchAction.ExpectedAdjustedActionId));
    }

    private static bool TryReadBaseChargeCount(
        IPlayerCharacter? localPlayer,
        out uint currentCharges)
    {
        currentCharges = 0;
        if (localPlayer is null ||
            !localPlayer.ClassJob.IsValid ||
            localPlayer.ClassJob.RowId != AstrologianJobId ||
            GetNativeObject(localPlayer) == null)
        {
            return false;
        }

        var actionManager = ActionManager.Instance();
        if (actionManager == null ||
            actionManager->GetAdjustedActionId(BaseActionId) != BaseActionId)
        {
            return false;
        }

        currentCharges = actionManager->GetCurrentCharges(BaseActionId);
        return currentCharges <=
            AstrologianHarmonicOrbisRules.MaximumHarmonicOrbisCharges;
    }

    private IPlayerCharacter? ResolveExactLocalPlayer(
        IPlayerCharacter? proposed,
        out TargetPressureActorIdentity identity)
    {
        identity = default;
        var current = objectTable.LocalPlayer;
        if (!TryGetExactIdentity(current, out var currentIdentity) ||
            proposed is null ||
            proposed.Address != current!.Address ||
            proposed.GameObjectId != current.GameObjectId ||
            proposed.EntityId != current.EntityId)
        {
            return null;
        }

        identity = currentIdentity;
        return current;
    }

    private bool TryGetExactIdentity(
        IPlayerCharacter? player,
        out TargetPressureActorIdentity identity)
    {
        identity = default;
        if (player is null ||
            player.Address == nint.Zero ||
            !IsNetworkObjectId(player.GameObjectId) ||
            !IsNetworkEntityId(player.EntityId))
        {
            return false;
        }

        var native = (GameObject*)player.Address;
        if (native == null || native->EntityId != player.EntityId) return false;
        var tablePlayer = objectTable.SearchByEntityId(player.EntityId) as IPlayerCharacter;
        if (tablePlayer is null ||
            tablePlayer.Address != player.Address ||
            tablePlayer.GameObjectId != player.GameObjectId ||
            tablePlayer.EntityId != player.EntityId)
        {
            return false;
        }

        identity = new TargetPressureActorIdentity(
            player.GameObjectId,
            player.EntityId);
        return identity.IsValid;
    }

    private bool IsCurrentlySuppressedByGuard(
        IPlayerCharacter localPlayer,
        long nowMilliseconds)
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

    private static bool IsExpectedFriendlyAction(GameAction action) =>
        (action.RowId is BaseActionId or DoubleCastFollowUpActionId) &&
        action.IsPvP &&
        AstrologianHarmonicOrbisRules.HasExpectedPlayerActionFlag(
            action.RowId,
            action.IsPlayerAction) &&
        action.ClassJob.IsValid &&
        action.ClassJob.RowId == AstrologianJobId &&
        action.Cast100ms == 0 &&
        action.Range > 0 &&
        action.EffectRange == 0 &&
        !action.TargetArea &&
        action.CanTargetSelf &&
        action.CanTargetParty &&
        !action.CanTargetHostile &&
        action.RequiresLineOfSight;

    private static bool IsLivePlayer(IPlayerCharacter? player) =>
        player is not null &&
        !player.IsDead &&
        player.CurrentHp > 0 &&
        player.MaxHp >= player.CurrentHp;

    private static GameObject* GetNativeObject(IPlayerCharacter player)
    {
        var native = (GameObject*)player.Address;
        return native != null && native->EntityId == player.EntityId
            ? native
            : null;
    }

    private void ClearEpisode()
    {
        frozenIntent = null;
        phase = AstrologianHarmonicOrbisProbePhase.Waiting;
        retry = HeldActionRetryState.Initial;
        phaseExpiresAtMilliseconds = -1;
        sequenceIntent = default;
        acceptedBaseOutcome = ClientActionAttemptOutcome.None;
        doubleCastWasReadyBeforeBase = false;
        doubleCastChargesBeforeBase = 0;
    }

    private AstrologianHarmonicOrbisProbeSnapshot Publish(
        AstrologianHarmonicOrbisProbeDecision decision,
        string message)
    {
        var result = AstrologianHarmonicOrbisProbeSnapshot.Initial with
        {
            Decision = decision,
            Phase = phase,
            HeldGameplayKey = frozenIntent?.HeldKey ?? terminalHeldKey,
            BaseAttemptCount = Interlocked.Read(ref baseAttemptCount),
            BaseAcceptedCount = Interlocked.Read(ref baseAcceptedCount),
            FollowUpAttemptCount = Interlocked.Read(ref followUpAttemptCount),
            FollowUpAcceptedCount = Interlocked.Read(ref followUpAcceptedCount),
            RejectedCount = Interlocked.Read(ref rejectedCount),
            UnknownCount = Interlocked.Read(ref unknownCount),
            SoftWaitCount = Interlocked.Read(ref softWaitCount),
            LastEvent = message,
        };
        Volatile.Write(ref snapshot, result);
        return result;
    }

    private void LogFailure(
        Exception exception,
        long nowMilliseconds,
        string message)
    {
        if (nowMilliseconds >= 0 && nowMilliseconds < nextErrorLogAt) return;
        nextErrorLogAt = nowMilliseconds < 0
            ? 0
            : SaturatingAdd(nowMilliseconds, 10_000);
        log.Error(exception, message);
    }

    private ulong NextIntentEpochToken()
    {
        nextIntentEpochToken = nextIntentEpochToken == ulong.MaxValue
            ? 1
            : nextIntentEpochToken + 1;
        return nextIntentEpochToken;
    }

    private static ulong NextFrameworkFrame(ulong current) =>
        current == ulong.MaxValue ? 1UL : current + 1UL;

    private string DescribeAttempt(
        uint actionId,
        ClientActionAttemptOutcome outcome) => outcome switch
    {
        ClientActionAttemptOutcome.ClientAccepted when actionId == BaseActionId &&
            doubleCastWasReadyBeforeBase =>
            "Harmonischer Orbis accepted; waiting for snapshotted Double Cast",
        ClientActionAttemptOutcome.ClientAccepted when actionId == BaseActionId =>
            "Harmonischer Orbis accepted; Double Cast was not pre-ready, base-only terminal",
        ClientActionAttemptOutcome.ClientAccepted =>
            "Double Cast Harmonischer Orbis accepted on the frozen ally",
        ClientActionAttemptOutcome.ClientRejected when retry.IsPending =>
            $"AST action {actionId} client-rejected; exact bounded retry retained",
        ClientActionAttemptOutcome.ClientRejected =>
            $"AST action {actionId} explicit rejection budget exhausted",
        ClientActionAttemptOutcome.SoftUnavailable =>
            $"AST action {actionId} soft-unavailable without spending retry budget",
        ClientActionAttemptOutcome.AcceptanceUnknown =>
            $"AST action {actionId} acceptance ambiguous; episode terminal",
        _ => $"AST action {actionId} not invoked",
    };

    private static double HealthPercent(NearHelpSelectionCandidate candidate) =>
        candidate.MaximumHp == 0
            ? 0
            : candidate.CurrentHp * 100d / candidate.MaximumHp;

    private static long SaturatingAdd(long left, long right) =>
        left > long.MaxValue - right ? long.MaxValue : left + right;

    private static bool IsNetworkEntityId(uint entityId) =>
        entityId is not 0 and not 0xE0000000 and not uint.MaxValue;

    private static bool IsNetworkObjectId(ulong gameObjectId) =>
        gameObjectId is not 0 and not 0xE0000000 and not ulong.MaxValue;

    private readonly record struct RuntimeCandidate(
        NearHelpSelectionCandidate Selection,
        TargetPressureActorIdentity Identity,
        nint Address);

    private readonly record struct FrozenIntent(
        TargetPressureActorIdentity LocalPlayer,
        TargetPressureActorIdentity Target,
        nint LocalAddress,
        nint TargetAddress,
        uint TerritoryId,
        SupportedPvPContext Context,
        int PartySlot,
        bool TargetIsSelf,
        VirtualKey HeldKey,
        ulong IntentEpochToken,
        long CreatedAtMilliseconds)
    {
        internal bool IsValid =>
            LocalPlayer.IsValid &&
            Target.IsValid &&
            LocalAddress != nint.Zero &&
            TargetAddress != nint.Zero &&
            TerritoryId != 0 &&
            (Context is SupportedPvPContext.CrystallineConflict or
                SupportedPvPContext.WolvesDen) &&
            NearHelpSelectionRules.IsValidPartySlot(PartySlot) &&
            HeldKey != VirtualKey.NO_KEY &&
            IntentEpochToken != 0 &&
            CreatedAtMilliseconds >= 0;
    }
}
