using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using SeitonSense.Core;

namespace SeitonSense.Plugin.Services;

internal sealed record ViperSerpentTailProbeSnapshot(
    ViperSerpentTailDecisionKind Decision,
    ViperSerpentTailDecisionReason Reason,
    ViperSerpentTailPhase Phase,
    uint ResolvedActionId,
    long ExposureGeneration,
    bool ExposureSpent,
    int NonFollowUpObservations,
    int EnemySlot,
    ulong TargetGameObjectId,
    uint TargetEntityId,
    bool LocallyReady,
    bool NativeBoundaryReady,
    VirtualKey HeldGameplayKey,
    bool InputClaimed,
    bool UseActionAttempted,
    bool UseActionAccepted,
    ClientActionAttemptOutcome LastNativeOutcome,
    int NativeAttemptCount,
    long AttemptCount,
    long AcceptedCount,
    long RejectedCount,
    long UnknownCount,
    long SoftWaitCount,
    string LastEvent)
{
    internal static ViperSerpentTailProbeSnapshot Initial { get; } = new(
        ViperSerpentTailDecisionKind.None,
        ViperSerpentTailDecisionReason.None,
        ViperSerpentTailPhase.Waiting,
        0,
        0,
        false,
        0,
        0,
        0,
        0,
        false,
        false,
        VirtualKey.NO_KEY,
        false,
        false,
        false,
        ClientActionAttemptOutcome.None,
        0,
        0,
        0,
        0,
        0,
        0,
        "Waiting for transformed Serpent's Tail");
}

/// <summary>
/// Observes the native Serpent's Tail carrier continuously and converts one
/// currently exposed follow-up into one exact held action. In CC the concrete
/// follow-up uses the shared Smart Action target policy; Wolves' Den retains
/// its exact current-target duel/dummy path. The action, chosen target, context,
/// physical key generation, and native addresses are frozen before bounded
/// retries. It never changes selected target state, substitutes an action after
/// freezing, or cancels casts.
/// </summary>
internal sealed unsafe class ViperSerpentTailProbe
{
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly NearAssistRedirector nearAssist;
    private readonly IPluginLog log;
    private ViperSerpentTailState state = ViperSerpentTailState.Initial;
    private ViperSerpentTailExposureState exposure =
        ViperSerpentTailExposureState.Initial;
    private ViperSerpentTailProbeSnapshot snapshot = ViperSerpentTailProbeSnapshot.Initial;
    private VirtualKey terminalHeldKey = VirtualKey.NO_KEY;
    private long frozenExposureGeneration;
    private uint frozenTerritoryId;
    private nint frozenLocalAddress;
    private nint frozenTargetAddress;
    private long attemptCount;
    private long acceptedCount;
    private long rejectedCount;
    private long unknownCount;
    private long softWaitCount;
    private long nextErrorLogAt;
    private string lastEvent = "Waiting for transformed Serpent's Tail";

    internal ViperSerpentTailProbe(
        IClientState clientState,
        IObjectTable objectTable,
        NearAssistRedirector nearAssist,
        IPluginLog log)
    {
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.nearAssist = nearAssist;
        this.log = log;
    }

    internal ViperSerpentTailProbeSnapshot Snapshot => Volatile.Read(ref snapshot);

    internal ViperSerpentTailProbeSnapshot Observe(
        IPlayerCharacter? localPlayer,
        SupportedPvPContext context,
        bool configurationEnabled,
        bool actionMetadataVerified,
        bool wolvesDenDummyMetadataVerified,
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
                actionMetadataVerified,
                wolvesDenDummyMetadataVerified,
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
                "Seiton Sense Viper Serpent's Tail probe failed closed.");
            return FailClosed();
        }
    }

    internal void Reset()
    {
        state = ViperSerpentTailState.Initial;
        exposure = ViperSerpentTailExposureState.Initial;
        terminalHeldKey = VirtualKey.NO_KEY;
        ClearFrozenRuntime();
        lastEvent = "Reset";
        PublishTerminalSnapshot(
            ViperSerpentTailDecisionKind.None,
            ViperSerpentTailDecisionReason.HardReset,
            lastEvent);
    }

    internal ViperSerpentTailProbeSnapshot FailClosed()
    {
        var failedKey = state.Intent is { IsValid: true } intent
            ? (VirtualKey)intent.FrozenKeyCode
            : terminalHeldKey;
        state = ViperSerpentTailState.Initial;
        exposure = ViperSerpentTailExposureState.Initial;
        terminalHeldKey = failedKey;
        ClearFrozenRuntime();

        lastEvent = "Failed closed";
        return PublishTerminalSnapshot(
            ViperSerpentTailDecisionKind.Cancelled,
            ViperSerpentTailDecisionReason.HardReset,
            lastEvent);
    }

    private ViperSerpentTailProbeSnapshot ObserveCore(
        IPlayerCharacter? localPlayer,
        SupportedPvPContext context,
        bool configurationEnabled,
        bool actionMetadataVerified,
        bool wolvesDenDummyMetadataVerified,
        bool actionHelpersSuppressedByGuard,
        bool higherPriorityClaimed,
        EmergencyActionInputFrame inputFrame,
        long nowMilliseconds,
        bool hardReset)
    {
        var effectiveHardReset = hardReset || nowMilliseconds < 0;
        if (effectiveHardReset)
        {
            state = ViperSerpentTailState.Initial;
            exposure = ViperSerpentTailExposureState.Initial;
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
        var localIdentity = exactLocal is not null
            ? new TargetPressureActorIdentity(
                exactLocal.GameObjectId,
                exactLocal.EntityId)
            : default;
        var localAlive = IsLivePlayer(exactLocal);
        var localJobId = exactLocal?.ClassJob.IsValid == true
            ? exactLocal.ClassJob.RowId
            : 0;
        var territoryId = clientState.TerritoryType;
        // The action sheet is the feature-wide metadata gate. Wolves' Den duel
        // opponents are exact live players and therefore must not depend on the
        // optional striking-dummy name-row verification.
        var metadataVerified = actionMetadataVerified;
        var runtimeDrift = state.Intent is { IsValid: true } frozenIntent &&
                           (!FrozenRuntimeMatches(
                                frozenIntent,
                                territoryId,
                                exactLocal?.Address ?? nint.Zero) ||
                            frozenIntent.Context != context ||
                            frozenIntent.LocalPlayer != localIdentity ||
                            !configurationEnabled ||
                            !localAlive ||
                            localJobId != ViperSerpentTailRules.ViperJobId ||
                            !metadataVerified);
        if (runtimeDrift)
        {
            effectiveHardReset = true;
            state = ViperSerpentTailState.Initial;
            exposure = ViperSerpentTailExposureState.Initial;
            terminalHeldKey = VirtualKey.NO_KEY;
            ClearFrozenRuntime();
        }

        var featureGateReady = !effectiveHardReset &&
                               configurationEnabled &&
                               context is SupportedPvPContext.CrystallineConflict or
                                   SupportedPvPContext.WolvesDen &&
                               localAlive &&
                               localJobId == ViperSerpentTailRules.ViperJobId &&
                               metadataVerified;
        var guardSuppressed = featureGateReady &&
                              (actionHelpersSuppressedByGuard ||
                               IsCurrentlySuppressedByGuard(
                                   exactLocal,
                                   nowMilliseconds));
        var effectiveHigherPriorityClaimed = higherPriorityClaimed ||
                                             inputFrame.IsConsumed;
        var resolvedActionId = 0u;
        var actionLocallyReady = false;
        var nativeBoundaryReady = false;
        if (featureGateReady && exactLocal is not null)
        {
            TryObserveActionState(
                exactLocal,
                out resolvedActionId,
                out actionLocallyReady,
                out nativeBoundaryReady);
        }

        // The native carrier is the source of truth. This observation runs on
        // every active VPR frame, even without a held key or while Purify owns
        // the scheduler frame.
        exposure = ViperSerpentTailRules.ObserveCarrierExposure(
            exposure,
            resolvedActionId,
            hardReset: !featureGateReady);

        var input = inputFrame.Snapshot;
        var previousIntent = state.Intent;
        var frozenKeyStillDown = state.Intent is { IsValid: true } heldIntent &&
                                 inputFrame.IsFrozenGameplayKeyConsentValid(
                                     (VirtualKey)heldIntent.FrozenKeyCode);
        var currentIntentStillTracked = state.Intent is { IsValid: true } currentIntent &&
                                        ViperSerpentTailRules.IsTrackedUnspentExposure(
                                            exposure,
                                            currentIntent.ExposureGeneration,
                                            currentIntent.ActionId);
        var expectedActionId = currentIntentStillTracked
            ? state.Intent!.Value.ActionId
            : exposure.CurrentActionId;
        var selectedEnemySlot = 0;
        var selectedTarget = default(TargetPressureActorIdentity);
        var selectedWinnerInvalidated = false;
        RuntimeCandidate? runtimeCandidate = null;
        if (exactLocal is not null &&
            ViperSerpentTailRules.IsExactFollowUpAction(expectedActionId) &&
            (currentIntentStillTracked ||
             terminalHeldKey == VirtualKey.NO_KEY &&
             inputFrame.HeldGameplayKeyEligible &&
             !effectiveHigherPriorityClaimed &&
             !guardSuppressed))
        {
            runtimeCandidate = currentIntentStillTracked
                ? ResolveExactCandidate(
                    exactLocal,
                    context,
                    state.Intent!.Value.EnemySlot,
                    state.Intent.Value.Target,
                    expectedActionId,
                    wolvesDenDummyMetadataVerified)
                : ResolveCurrentExactCandidate(
                    exactLocal,
                    context,
                    expectedActionId,
                    wolvesDenDummyMetadataVerified,
                    out selectedEnemySlot,
                    out selectedTarget,
                    out selectedWinnerInvalidated);
        }

        if (currentIntentStillTracked &&
            runtimeCandidate is { } trackedCandidate &&
            trackedCandidate.Target.Address != frozenTargetAddress)
        {
            runtimeCandidate = null;
        }

        ViperSerpentTailIntent? freshSelectedIntent = null;
        if (!currentIntentStillTracked && selectedTarget.IsValid)
        {
            var selected = new ViperSerpentTailIntent(
                exposure.Generation,
                context,
                selectedEnemySlot,
                localIdentity,
                selectedTarget,
                expectedActionId,
                (int)input.HeldGameplayKey);
            if (selected.IsValid) freshSelectedIntent = selected;
        }

        var exactFrozenTargetInvalid = selectedWinnerInvalidated ||
                                       runtimeCandidate is null &&
                                       (currentIntentStillTracked &&
                                        previousIntent is { IsValid: true } ||
                                        freshSelectedIntent is { IsValid: true });

        var exactActionLocallyReady = actionLocallyReady &&
                                      resolvedActionId == expectedActionId &&
                                      runtimeCandidate?.TargetActionReady == true;
        // A buffered intent may belong to a carrier action that was superseded
        // in this same frame. Only retain it when it still owns the current
        // exposure; otherwise the freshly selected current-action intent wins.
        var selectedOrFrozenIntent = currentIntentStillTracked
            ? previousIntent
            : freshSelectedIntent;
        var decision = ViperSerpentTailRules.Observe(
            state,
            new ViperSerpentTailObservation(
                ConfigurationEnabled: configurationEnabled,
                Context: context,
                LocalPlayer: localIdentity,
                IsLocalPlayerAlive: localAlive,
                LocalJobId: localJobId,
                MetadataVerified: metadataVerified,
                ActionHelpersSuppressedByGuard: guardSuppressed,
                HigherPriorityClaimed: effectiveHigherPriorityClaimed,
                InputProbeSucceeded: input.ProbeSucceeded,
                IsTextInputActive: input.IsTextInputActive,
                HeldGameplayKeyEligible: terminalHeldKey == VirtualKey.NO_KEY &&
                                         inputFrame.HeldGameplayKeyEligible,
                HeldGameplayKeyCode: (int)input.HeldGameplayKey,
                FrozenKeyStillDown: frozenKeyStillDown,
                Exposure: exposure,
                ActionLocallyReady: exactActionLocallyReady,
                NativeBoundaryReady: nativeBoundaryReady,
                Candidate: runtimeCandidate?.Core,
                HardReset: effectiveHardReset,
                NowMilliseconds: nowMilliseconds));

        // Target drift outranks incidental same-frame scheduler/key state. The
        // exact frozen actor is gone or unsafe, so this carrier exposure must
        // be retired even if Purify, Guard, or key release would otherwise
        // explain the frame first.
        if (exactFrozenTargetInvalid)
        {
            decision = new ViperSerpentTailDecision(
                ViperSerpentTailState.Initial,
                ViperSerpentTailDecisionKind.Cancelled,
                ViperSerpentTailDecisionReason.CandidateUnavailable);
            ClearFrozenRuntime();
        }

        if (decision.NextState.Intent is { IsValid: true } nextIntent &&
            (!previousIntent.HasValue || previousIntent.Value != nextIntent))
        {
            selectedOrFrozenIntent = nextIntent;
            if (runtimeCandidate is not { } frozenCandidate ||
                exactLocal is null ||
                frozenCandidate.Core.Context != nextIntent.Context ||
                frozenCandidate.Core.EnemySlot != nextIntent.EnemySlot ||
                frozenCandidate.Core.Actor != nextIntent.Target)
            {
                decision = new ViperSerpentTailDecision(
                    ViperSerpentTailState.Initial,
                    ViperSerpentTailDecisionKind.Cancelled,
                    ViperSerpentTailDecisionReason.CandidateUnavailable);
                ClearFrozenRuntime();
            }
            else
            {
                FreezeRuntime(
                    nextIntent,
                    territoryId,
                    exactLocal.Address,
                    frozenCandidate.Target.Address);
            }
        }

        // A frozen episode owns exactly one actor. If that actor later becomes
        // ambiguous, protected, dead, untargetable, out of native range/LoS, or
        // otherwise fails exact revalidation, retire this carrier exposure.
        // Leaving it unspent would allow the same held key to rerank to a
        // different enemy on the next framework frame.
        exposure = ViperSerpentTailRules
            .RetireCurrentCarrierExposureAfterSelectedWinnerInvalidation(
                exposure,
                selectedWinnerInvalidated);

        exposure = ViperSerpentTailRules
            .RetireCarrierExposureAfterExactTargetDrift(
                exposure,
                selectedOrFrozenIntent,
                decision);

        state = decision.NextState;
        var inputClaimed = decision.InputClaimed;
        if (state.Intent is { IsValid: true } claimedIntent)
        {
            _ = inputFrame.IsFrozenGameplayKeyConsentValid(
                (VirtualKey)claimedIntent.FrozenKeyCode);
        }
        if (inputClaimed) inputFrame.Consume();

        var attempted = false;
        var accepted = false;
        var nativeOutcome = ClientActionAttemptOutcome.None;
        if (decision.ShouldDispatch && decision.Intent is { } intent)
        {
            nativeOutcome = TryUseOnce(
                intent,
                context,
                configurationEnabled,
                metadataVerified,
                wolvesDenDummyMetadataVerified,
                actionHelpersSuppressedByGuard,
                effectiveHigherPriorityClaimed,
                inputFrame,
                out attempted);
            if (attempted) Interlocked.Increment(ref attemptCount);
            if (nativeOutcome == ClientActionAttemptOutcome.ClientAccepted)
                Interlocked.Increment(ref acceptedCount);
            if (nativeOutcome == ClientActionAttemptOutcome.ClientRejected)
                Interlocked.Increment(ref rejectedCount);
            if (nativeOutcome == ClientActionAttemptOutcome.AcceptanceUnknown)
                Interlocked.Increment(ref unknownCount);

            var completion = ViperSerpentTailRules.ApplyNativeAttemptOutcome(
                state,
                nativeOutcome,
                nowMilliseconds);
            if (completion.SpendExposure)
            {
                exposure = ViperSerpentTailRules.MarkCarrierExposureSpent(
                    exposure,
                    intent.ExposureGeneration,
                    intent.ActionId);
            }

            state = completion.NextState;
            accepted = completion.ClientAccepted;
            if (completion.SoftWait) Interlocked.Increment(ref softWaitCount);
            if (completion.Terminal &&
                HeldActionRetryRules.ShouldLatchHeldKeyUntilRelease(
                    completion.Disposition))
            {
                terminalHeldKey = (VirtualKey)intent.FrozenKeyCode;
            }

            lastEvent = DescribeNativeResult(intent, nativeOutcome, completion);
        }
        else
        {
            lastEvent = decision.Reason.ToString();
        }

        if (state.Phase != ViperSerpentTailPhase.Buffered)
            ClearFrozenRuntime();

        var activeIntent = state.Intent ?? decision.Intent;
        var selectedCandidate = runtimeCandidate?.Core;
        var result = new ViperSerpentTailProbeSnapshot(
            decision.Kind,
            decision.Reason,
            state.Phase,
            resolvedActionId,
            activeIntent?.ExposureGeneration ?? exposure.Generation,
            exposure.IsSpent,
            exposure.ConsecutiveNonFollowUpObservations,
            activeIntent?.EnemySlot ?? selectedCandidate?.EnemySlot ?? 0,
            activeIntent?.Target.GameObjectId ?? selectedCandidate?.Actor.GameObjectId ?? 0,
            activeIntent?.Target.EntityId ?? selectedCandidate?.Actor.EntityId ?? 0,
            exactActionLocallyReady,
            nativeBoundaryReady,
            activeIntent is { IsValid: true }
                ? (VirtualKey)activeIntent.Value.FrozenKeyCode
                : input.HeldGameplayKey,
            inputClaimed,
            attempted,
            accepted,
            nativeOutcome != ClientActionAttemptOutcome.None
                ? nativeOutcome
                : state.LastNativeOutcome,
            state.Retry.NativeAttemptCount,
            Interlocked.Read(ref attemptCount),
            Interlocked.Read(ref acceptedCount),
            Interlocked.Read(ref rejectedCount),
            Interlocked.Read(ref unknownCount),
            Interlocked.Read(ref softWaitCount),
            lastEvent);
        Volatile.Write(ref snapshot, result);
        return result;
    }

    private ClientActionAttemptOutcome TryUseOnce(
        ViperSerpentTailIntent intent,
        SupportedPvPContext context,
        bool configurationEnabled,
        bool metadataVerified,
        bool wolvesDenDummyMetadataVerified,
        bool actionHelpersSuppressedByGuard,
        bool higherPriorityClaimed,
        EmergencyActionInputFrame inputFrame,
        out bool attempted)
    {
        attempted = false;
        if (!intent.IsValid ||
            frozenExposureGeneration != intent.ExposureGeneration ||
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
        var carrierBefore = 0u;
        var carrierAfter = 0u;
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
                if (!TryObserveActionState(
                        currentLocal,
                        out var adjustedActionId,
                        out var actionLocallyReady,
                        out var nativeBoundaryReady))
                {
                    return false;
                }

                var candidate = ResolveExactCandidate(
                    currentLocal,
                    context,
                    intent.EnemySlot,
                    intent.Target,
                    intent.ActionId,
                    wolvesDenDummyMetadataVerified);
                if (candidate is null ||
                    candidate.Value.Target.Address != frozenTargetAddress)
                {
                    return false;
                }

                var exactKey = (VirtualKey)intent.FrozenKeyCode;
                var exactGenerationEligible =
                    inputFrame.IsFrozenGameplayKeyConsentValid(exactKey);
                if (adjustedActionId != intent.ActionId ||
                    !ViperSerpentTailRules.CanUseFrozenIntent(
                        intent,
                        configurationEnabled,
                        context,
                        currentIdentity,
                        IsLivePlayer(currentLocal),
                        currentLocal.ClassJob.IsValid
                            ? currentLocal.ClassJob.RowId
                            : 0,
                        metadataVerified,
                        guardSuppressed,
                        higherPriorityClaimed,
                        exposure,
                        actionLocallyReady,
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
                    intent.ActionId,
                    intent.Target.GameObjectId,
                    checkRecastActive: true,
                    checkCastingActive: true);
                if (!nativeBoundaryReady)
                {
                    softUnavailableAtBoundary = true;
                    return false;
                }

                carrierBefore = actionManager->GetAdjustedActionId(
                    ViperSerpentTailRules.CarrierActionId);
                before = ClientActionAttemptBoundary.Capture(
                    actionManager,
                    intent.ActionId);
                if (carrierBefore != intent.ActionId ||
                    !before.IsExactActionReady(intent.ActionId) ||
                    targetStatusBefore != 0)
                {
                    softUnavailableAtBoundary =
                        carrierBefore == intent.ActionId;
                    return false;
                }

                attemptedAtBoundary = true;
                var accepted = actionManager->UseAction(
                    ActionType.Action,
                    intent.ActionId,
                    intent.Target.GameObjectId,
                    0,
                    ActionManager.UseActionMode.None,
                    0);
                after = ClientActionAttemptBoundary.Capture(
                    actionManager,
                    intent.ActionId);
                carrierAfter = actionManager->GetAdjustedActionId(
                    ViperSerpentTailRules.CarrierActionId);
                targetStatusAfter = actionManager->GetActionStatus(
                    ActionType.Action,
                    intent.ActionId,
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

            return ViperSerpentTailRules.ClassifyFollowUpBoundary(
                clientAccepted,
                intent.ActionId,
                targetStatusBefore,
                targetStatusAfter,
                carrierBefore,
                carrierAfter,
                before,
                after);
        }
        catch (Exception exception)
        {
            LogFailure(
                exception,
                Environment.TickCount64,
                "Seiton Sense Viper Serpent's Tail native boundary failed.");
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

    private RuntimeCandidate? ResolveCurrentExactCandidate(
        IPlayerCharacter localPlayer,
        SupportedPvPContext context,
        uint actionId,
        bool wolvesDenDummyMetadataVerified,
        out int selectedEnemySlot,
        out TargetPressureActorIdentity selectedTarget,
        out bool selectedWinnerInvalidated)
    {
        selectedEnemySlot = 0;
        selectedTarget = default;
        selectedWinnerInvalidated = false;
        if (!ViperSerpentTailRules.IsExactFollowUpAction(actionId)) return null;

        switch (context)
        {
            case SupportedPvPContext.CrystallineConflict:
            {
                if (!nearAssist.TryResolveHeldSmartActionTarget(
                        actionId,
                        out var slot,
                        out var smartIdentity,
                        out selectedWinnerInvalidated,
                        out _))
                {
                    return null;
                }

                selectedEnemySlot = slot;
                selectedTarget = smartIdentity;
                return ResolveExactCandidate(
                    localPlayer,
                    context,
                    slot,
                    smartIdentity,
                    actionId,
                    wolvesDenDummyMetadataVerified);
            }
            case SupportedPvPContext.WolvesDen:
                if (!TryResolveExactWolvesDenCurrentHardTarget(
                        objectTable,
                        wolvesDenDummyMetadataVerified,
                        localPlayer,
                        out _,
                        out var identity))
                {
                    return null;
                }

                selectedTarget = identity;
                return ResolveExactCandidate(
                    localPlayer,
                    context,
                    0,
                    identity,
                    actionId,
                    wolvesDenDummyMetadataVerified);
            default:
                return null;
        }
    }

    private RuntimeCandidate? ResolveExactCandidate(
        IPlayerCharacter localPlayer,
        SupportedPvPContext context,
        int enemySlot,
        TargetPressureActorIdentity expectedTarget,
        uint actionId,
        bool wolvesDenDummyMetadataVerified)
    {
        if (!expectedTarget.IsValid ||
            expectedTarget == new TargetPressureActorIdentity(
                localPlayer.GameObjectId,
                localPlayer.EntityId) ||
            !ViperSerpentTailRules.IsExactFollowUpAction(actionId))
        {
            return null;
        }

        IBattleChara? target;
        var exactCanonicalIdentity = false;
        switch (context)
        {
            case SupportedPvPContext.CrystallineConflict:
            {
                if (!EnemySlotRules.IsValidSlot(enemySlot) ||
                    !nearAssist.CanUseExactHeldSmartActionTarget(
                        actionId,
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
                exactCanonicalIdentity = HasSameNativeIdentity(player, byObjectId) &&
                                         HasSameNativeIdentity(player, byEntityId) &&
                                         HasSameNativeIdentity(
                                             player,
                                             EnemySlotResolver.Resolve(
                                                 objectTable,
                                                 enemySlot));
                target = player;
                break;
            }
            case SupportedPvPContext.WolvesDen:
                if (enemySlot != 0 ||
                    !TryResolveExactWolvesDenCurrentHardTarget(
                            objectTable,
                            wolvesDenDummyMetadataVerified,
                            localPlayer,
                            out target,
                            out var currentIdentity) ||
                    currentIdentity != expectedTarget)
                {
                    return null;
                }

                exactCanonicalIdentity = true;
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
        var hasValidActionTarget = sourceObject != null && targetObject != null;
        var actionManager = ActionManager.Instance();
        var targetActionReady = actionManager != null &&
                                actionManager->GetAdjustedActionId(
                                    ViperSerpentTailRules.CarrierActionId) == actionId &&
                                actionManager->GetActionStatus(
                                    ActionType.Action,
                                    actionId,
                                    expectedTarget.GameObjectId,
                                    checkRecastActive: true,
                                    checkCastingActive: true) == 0;
        var nativeRangeAndLineOfSight = hasValidActionTarget &&
                                        SeitonRangeRules
                                            .HasNativeRangeAndLineOfSight(
                                                ActionManager
                                                    .GetActionInRangeOrLoS(
                                                        actionId,
                                                        sourceObject,
                                                        targetObject));
        var alive = IsLiveBattleCharacter(target);
        return new RuntimeCandidate(
            new ViperSerpentTailCandidate(
                context,
                enemySlot,
                expectedTarget,
                exactCanonicalIdentity,
                alive,
                target.IsTargetable,
                hasValidActionTarget,
                nativeRangeAndLineOfSight),
            target,
            targetActionReady);
    }

    /// <summary>
    /// Resolves the exact current native Wolves' Den hard target. Duel targets
    /// follow the same direct object-table path as GNB Continuation and require
    /// a live hostile player; they do not depend on a duel-manager enemy slot.
    /// The previously reviewed exact current-target dummy remains a separate,
    /// metadata-gated test target. No synthetic slot or alternate is resolved.
    /// </summary>
    private static bool TryResolveExactWolvesDenCurrentHardTarget(
        IObjectTable objectTable,
        bool strikingDummyMetadataVerified,
        IPlayerCharacter localPlayer,
        out IBattleChara? target,
        out TargetPressureActorIdentity identity)
    {
        target = null;
        identity = default;
        if (!HasValidNativeIdentity(localPlayer)) return false;

        if (StrictWolvesDenStrikingDummyResolver.TryResolveExactCurrentHardTarget(
                objectTable,
                strikingDummyMetadataVerified,
                localPlayer,
                out var dummy,
                out var dummyIdentity,
                out _) &&
            ViperSerpentTailRules.IsEligibleWolvesDenCurrentTarget(
                isPlayerCharacter: false,
                hostileFlag: false,
                exactVerifiedStrikingDummy: true))
        {
            target = dummy;
            identity = dummyIdentity;
            return true;
        }

        var nativeTargetId = GetNativeHardTargetId(localPlayer);
        if (!IsNetworkObjectId(nativeTargetId)) return false;

        var byObjectId = objectTable.SearchById(nativeTargetId)
            as IPlayerCharacter;
        var byEntityId = nativeTargetId <= uint.MaxValue
            ? objectTable.SearchByEntityId((uint)nativeTargetId)
                as IPlayerCharacter
            : null;
        if (HasValidNativeIdentity(byObjectId) &&
            HasValidNativeIdentity(byEntityId) &&
            !HasSameNativeIdentity(byObjectId, byEntityId))
        {
            return false;
        }

        var candidate = HasValidNativeIdentity(byObjectId)
            ? byObjectId
            : byEntityId;
        var hostileFlag = candidate is not null &&
                          (candidate.StatusFlags & StatusFlags.Hostile) != 0;
        if (!HasValidNativeIdentity(candidate) ||
            !ViperSerpentTailRules.IsEligibleWolvesDenCurrentTarget(
                isPlayerCharacter: true,
                hostileFlag,
                exactVerifiedStrikingDummy: false) ||
            !ActorIdMatches(nativeTargetId, candidate!) ||
            HasSameNativeIdentity(localPlayer, candidate) ||
            !IsLivePlayer(candidate) ||
            !candidate!.IsTargetable)
        {
            return false;
        }

        var canonicalByObject = objectTable.SearchById(candidate.GameObjectId)
            as IPlayerCharacter;
        var canonicalByEntity = objectTable.SearchByEntityId(candidate.EntityId)
            as IPlayerCharacter;
        if (!HasSameNativeIdentity(candidate, canonicalByObject) ||
            !HasSameNativeIdentity(candidate, canonicalByEntity) ||
            GetNativeHardTargetId(localPlayer) != nativeTargetId ||
            (candidate.StatusFlags & StatusFlags.Hostile) == 0)
        {
            return false;
        }

        identity = new TargetPressureActorIdentity(
            candidate.GameObjectId,
            candidate.EntityId);
        if (!identity.IsValid) return false;

        target = candidate;
        return true;
    }

    private static bool TryObserveActionState(
        IPlayerCharacter localPlayer,
        out uint resolvedActionId,
        out bool actionLocallyReady,
        out bool nativeBoundaryReady)
    {
        resolvedActionId = 0;
        actionLocallyReady = false;
        nativeBoundaryReady = false;
        if (!HasValidNativeIdentity(localPlayer) ||
            localPlayer.ClassJob.IsValid != true ||
            localPlayer.ClassJob.RowId != ViperSerpentTailRules.ViperJobId)
        {
            return false;
        }

        var actionManager = ActionManager.Instance();
        if (actionManager == null) return false;
        resolvedActionId = actionManager->GetAdjustedActionId(
            ViperSerpentTailRules.CarrierActionId);
        if (!ViperSerpentTailRules.IsExactFollowUpAction(resolvedActionId))
            return true;

        var fingerprint = ClientActionAttemptBoundary.Capture(
            actionManager,
            resolvedActionId);
        actionLocallyReady = fingerprint.Captured &&
                             fingerprint.AdjustedActionId == resolvedActionId &&
                             fingerprint.IsActionOffCooldown &&
                             fingerprint.ResourceStatus == 0;
        nativeBoundaryReady = HeldActionRetryRules.IsNativeBoundaryNearQueueable(
            actionManager->AnimationLock,
            localPlayer.IsCasting,
            actionManager->CastActionId,
            actionManager->ActionQueued);
        return true;
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

    private bool IsCurrentlySuppressedByGuard(
        IPlayerCharacter? localPlayer,
        long nowMilliseconds)
    {
        if (localPlayer is null) return false;
        if (DefensiveUtilityProbe.HasActiveGuard(localPlayer)) return true;
        return nearAssist.TryGetRecentExactLocalGuardAttempt(
            clientState.TerritoryType,
            localPlayer.GameObjectId,
            localPlayer.EntityId,
            nowMilliseconds,
            DefensiveUtilityRules.GuardPropagationLatchMilliseconds,
            out _);
    }

    private void FreezeRuntime(
        ViperSerpentTailIntent intent,
        uint territoryId,
        nint localAddress,
        nint targetAddress)
    {
        frozenExposureGeneration = intent.ExposureGeneration;
        frozenTerritoryId = territoryId;
        frozenLocalAddress = localAddress;
        frozenTargetAddress = targetAddress;
    }

    private bool FrozenRuntimeMatches(
        ViperSerpentTailIntent intent,
        uint territoryId,
        nint localAddress) =>
        intent.IsValid &&
        frozenExposureGeneration == intent.ExposureGeneration &&
        frozenTerritoryId == territoryId &&
        frozenLocalAddress != nint.Zero &&
        frozenLocalAddress == localAddress &&
        frozenTargetAddress != nint.Zero;

    private void ClearFrozenRuntime()
    {
        frozenExposureGeneration = 0;
        frozenTerritoryId = 0;
        frozenLocalAddress = nint.Zero;
        frozenTargetAddress = nint.Zero;
    }

    private ViperSerpentTailProbeSnapshot PublishTerminalSnapshot(
        ViperSerpentTailDecisionKind decision,
        ViperSerpentTailDecisionReason reason,
        string message)
    {
        var result = ViperSerpentTailProbeSnapshot.Initial with
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

    private static string DescribeNativeResult(
        ViperSerpentTailIntent intent,
        ClientActionAttemptOutcome outcome,
        ViperSerpentTailNativeAttemptDecision completion) =>
        outcome switch
        {
            ClientActionAttemptOutcome.ClientAccepted =>
                $"Viper {intent.ActionId} client-accepted",
            ClientActionAttemptOutcome.ClientRejected when completion.RetryScheduled =>
                $"Viper {intent.ActionId} client-rejected; exact intent retained",
            ClientActionAttemptOutcome.ClientRejected =>
                $"Viper {intent.ActionId} retry limit reached",
            ClientActionAttemptOutcome.SoftUnavailable =>
                $"Viper {intent.ActionId} waiting for native boundary",
            ClientActionAttemptOutcome.AcceptanceUnknown =>
                $"Viper {intent.ActionId} acceptance ambiguous; intent terminal",
            _ => completion.Reason.ToString(),
        };

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
        player.MaxHp > 0 &&
        player.CurrentHp <= player.MaxHp;

    private static bool IsLiveBattleCharacter(IBattleChara? actor) =>
        HasValidNativeIdentity(actor) &&
        !actor!.IsDead &&
        actor.CurrentHp > 0 &&
        actor.MaxHp > 0 &&
        actor.CurrentHp <= actor.MaxHp;

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

    private static ulong GetNativeHardTargetId(IPlayerCharacter localPlayer)
    {
        if (!HasValidNativeIdentity(localPlayer)) return 0;
        var character = (Character*)localPlayer.Address;
        return character == null ? 0 : character->GetTargetId().Id;
    }

    private static bool ActorIdMatches(
        ulong actorId,
        TargetPressureActorIdentity actor) =>
        actor.IsValid &&
        (actorId == actor.GameObjectId ||
         actorId <= uint.MaxValue && (uint)actorId == actor.EntityId);

    private static bool ActorIdMatches(ulong actorId, IGameObject actor) =>
        ActorIdMatches(
            actorId,
            new TargetPressureActorIdentity(
                actor.GameObjectId,
                actor.EntityId));

    private static bool IsNetworkEntityId(uint entityId) =>
        entityId is not 0 and not 0xE0000000 and not uint.MaxValue;

    private static bool IsNetworkObjectId(ulong gameObjectId) =>
        gameObjectId is not 0 and not 0xE0000000 and not ulong.MaxValue;

    private readonly record struct RuntimeCandidate(
        ViperSerpentTailCandidate Core,
        IBattleChara Target,
        bool TargetActionReady);
}
