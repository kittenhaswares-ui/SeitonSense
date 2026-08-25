using System.Numerics;
using Dalamud.Game;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using SeitonSense.Core;
using Lumina.Excel.Sheets;
using GameAction = Lumina.Excel.Sheets.Action;
using GameStatus = Lumina.Excel.Sheets.Status;

namespace SeitonSense.Plugin.Services;

internal sealed record GunbreakerContinuationProbeSnapshot(
    GunbreakerContinuationDecisionKind Decision,
    GunbreakerContinuationDecisionReason Reason,
    GunbreakerContinuationPhase Phase,
    uint ResolvedActionId,
    uint ResolvedProcStatusId,
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
    internal static GunbreakerContinuationProbeSnapshot Initial { get; } = new(
        GunbreakerContinuationDecisionKind.None,
        GunbreakerContinuationDecisionReason.None,
        GunbreakerContinuationPhase.Waiting,
        0,
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
        "Waiting for transformed Continuation");
}

/// <summary>
/// Observes the native Continuation carrier continuously and converts one
/// exact carrier-plus-own-proc exposure into one exact held action. The action,
/// chosen actor, context, physical key generation, and native addresses are
/// frozen before bounded retries. It never changes selected target state,
/// substitutes an action or actor, or cancels casts.
/// </summary>
internal sealed unsafe class GunbreakerContinuationProbe
{
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly NearAssistRedirector nearAssist;
    private readonly IPluginLog log;
    private GunbreakerContinuationState state = GunbreakerContinuationState.Initial;
    private GunbreakerContinuationExposureState exposure =
        GunbreakerContinuationExposureState.Initial;
    private GunbreakerContinuationProbeSnapshot snapshot = GunbreakerContinuationProbeSnapshot.Initial;
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
    private string lastEvent = "Waiting for transformed Continuation";

    internal GunbreakerContinuationProbe(
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

    internal GunbreakerContinuationProbeSnapshot Snapshot => Volatile.Read(ref snapshot);

    /// <summary>
    /// Performs the one-time, English-sheet fail-closed validation expected by
    /// <see cref="Observe"/>. Runtime proc evidence is still checked separately
    /// on every frame and again at the native dispatch boundary.
    /// </summary>
    internal static bool ValidateMetadata(
        IDataManager dataManager,
        IPluginLog log)
    {
        try
        {
            var actions = dataManager.GetExcelSheet<GameAction>(
                ClientLanguage.English);
            var procStatuses = dataManager.GetExcelSheet<ActionProcStatus>(
                ClientLanguage.English);
            var statuses = dataManager.GetExcelSheet<GameStatus>(
                ClientLanguage.English);
            if (!actions.TryGetRow(
                    GunbreakerContinuationRules.CarrierActionId,
                    out var carrier) ||
                !IsExpectedCarrier(carrier))
            {
                log.Warning(
                    "Seiton Sense GNB Continuation carrier metadata failed closed.");
                return false;
            }

            var definitions = new[]
            {
                new FollowUpMetadata(
                    GunbreakerContinuationRules.HypervelocityActionId,
                    "Hypervelocity", 9_599,
                    GunbreakerContinuationRules.HypervelocityProcRowId,
                    GunbreakerContinuationRules.ReadyToBlastStatusId,
                    "Ready to Blast", 213_618, 5, 0, 1, false),
                new FollowUpMetadata(
                    GunbreakerContinuationRules.JugularRipActionId,
                    "Jugular Rip", 9_358,
                    GunbreakerContinuationRules.JugularRipProcRowId,
                    GunbreakerContinuationRules.ReadyToRipStatusId,
                    "Ready to Rip", 213_611, 5, 0, 1, false),
                new FollowUpMetadata(
                    GunbreakerContinuationRules.AbdomenTearActionId,
                    "Abdomen Tear", 9_359,
                    GunbreakerContinuationRules.AbdomenTearProcRowId,
                    GunbreakerContinuationRules.ReadyToTearStatusId,
                    "Ready to Tear", 213_612, 5, 0, 1, false),
                new FollowUpMetadata(
                    GunbreakerContinuationRules.EyeGougeActionId,
                    "Eye Gouge", 9_360,
                    GunbreakerContinuationRules.EyeGougeProcRowId,
                    GunbreakerContinuationRules.ReadyToGougeStatusId,
                    "Ready to Gouge", 213_613, 5, 0, 1, false),
                new FollowUpMetadata(
                    GunbreakerContinuationRules.FatedBrandActionId,
                    "Fated Brand", 9_771,
                    GunbreakerContinuationRules.FatedBrandProcRowId,
                    GunbreakerContinuationRules.ReadyToRazeStatusId,
                    "Ready to Raze", 213_620, 0, 6, 2, true),
            };
            foreach (var definition in definitions)
            {
                if (!actions.TryGetRow(definition.ActionId, out var action) ||
                    !procStatuses.TryGetRow(
                        definition.ProcRowId,
                        out var procStatus) ||
                    !statuses.TryGetRow(
                        definition.StatusId,
                        out var status) ||
                    !IsExpectedFollowUp(
                        action,
                        procStatus,
                        status,
                        definition))
                {
                    log.Warning(
                        "Seiton Sense GNB Continuation metadata failed closed at action {ActionId}.",
                        definition.ActionId);
                    return false;
                }
            }

            return true;
        }
        catch (Exception exception)
        {
            log.Warning(
                exception,
                "Seiton Sense GNB Continuation metadata lookup failed closed.");
            return false;
        }
    }

    internal GunbreakerContinuationProbeSnapshot Observe(
        IPlayerCharacter? localPlayer,
        SupportedPvPContext context,
        bool configurationEnabled,
        bool actionMetadataVerified,
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
                "Seiton Sense Gunbreaker Continuation probe failed closed.");
            return FailClosed();
        }
    }

    internal void Reset()
    {
        state = GunbreakerContinuationState.Initial;
        exposure = GunbreakerContinuationExposureState.Initial;
        terminalHeldKey = VirtualKey.NO_KEY;
        ClearFrozenRuntime();
        lastEvent = "Reset";
        PublishTerminalSnapshot(
            GunbreakerContinuationDecisionKind.None,
            GunbreakerContinuationDecisionReason.HardReset,
            lastEvent);
    }

    internal GunbreakerContinuationProbeSnapshot FailClosed()
    {
        var failedKey = state.Intent is { IsValid: true } intent
            ? (VirtualKey)intent.FrozenKeyCode
            : terminalHeldKey;
        state = GunbreakerContinuationState.Initial;
        exposure = GunbreakerContinuationExposureState.Initial;
        terminalHeldKey = failedKey;
        ClearFrozenRuntime();

        lastEvent = "Failed closed";
        return PublishTerminalSnapshot(
            GunbreakerContinuationDecisionKind.Cancelled,
            GunbreakerContinuationDecisionReason.HardReset,
            lastEvent);
    }

    private GunbreakerContinuationProbeSnapshot ObserveCore(
        IPlayerCharacter? localPlayer,
        SupportedPvPContext context,
        bool configurationEnabled,
        bool actionMetadataVerified,
        bool actionHelpersSuppressedByGuard,
        bool higherPriorityClaimed,
        EmergencyActionInputFrame inputFrame,
        long nowMilliseconds,
        bool hardReset)
    {
        var effectiveHardReset = hardReset || nowMilliseconds < 0;
        if (effectiveHardReset)
        {
            state = GunbreakerContinuationState.Initial;
            exposure = GunbreakerContinuationExposureState.Initial;
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
                            localJobId != GunbreakerContinuationRules.GunbreakerJobId ||
                            !metadataVerified);
        if (runtimeDrift)
        {
            effectiveHardReset = true;
            state = GunbreakerContinuationState.Initial;
            exposure = GunbreakerContinuationExposureState.Initial;
            terminalHeldKey = VirtualKey.NO_KEY;
            ClearFrozenRuntime();
        }

        var featureGateReady = !effectiveHardReset &&
                               configurationEnabled &&
                               context is SupportedPvPContext.CrystallineConflict or
                                   SupportedPvPContext.WolvesDen &&
                               localAlive &&
                               localJobId == GunbreakerContinuationRules.GunbreakerJobId &&
                               metadataVerified;
        var guardSuppressed = featureGateReady &&
                              (actionHelpersSuppressedByGuard ||
                               IsCurrentlySuppressedByGuard(
                                   exactLocal,
                                   nowMilliseconds));
        var effectiveHigherPriorityClaimed = higherPriorityClaimed ||
                                             inputFrame.IsConsumed;
        var resolvedActionId = 0u;
        var observedProcStatusId = 0u;
        var actionLocallyReady = false;
        var nativeBoundaryReady = false;
        if (featureGateReady && exactLocal is not null)
        {
            TryObserveActionState(
                exactLocal,
                out resolvedActionId,
                out observedProcStatusId,
                out actionLocallyReady,
                out nativeBoundaryReady);
        }

        // The native carrier is the source of truth. This observation runs on
        // every active GNB frame, even without a held key or while Purify owns
        // the scheduler frame.
        exposure = GunbreakerContinuationRules.ObserveCarrierExposure(
            exposure,
            resolvedActionId,
            observedProcStatusId,
            hardReset: !featureGateReady);

        var input = inputFrame.Snapshot;
        var frozenKeyStillDown = state.Intent is { IsValid: true } heldIntent &&
                                 inputFrame.IsGameplayKeyPhysicallyDown(
                                     (VirtualKey)heldIntent.FrozenKeyCode);
        var currentIntentStillTracked = state.Intent is { IsValid: true } currentIntent &&
                                        GunbreakerContinuationRules.IsTrackedUnspentExposure(
                                            exposure,
                                            currentIntent.ExposureGeneration,
                                            currentIntent.ActionId,
                                            currentIntent.ProcStatusId);
        var expectedActionId = currentIntentStillTracked
            ? state.Intent!.Value.ActionId
            : exposure.CurrentActionId;
        RuntimeCandidate? runtimeCandidate = null;
        if (exactLocal is not null &&
            GunbreakerContinuationRules.IsExactFollowUpAction(expectedActionId))
        {
            runtimeCandidate = currentIntentStillTracked
                ? ResolveExactCandidate(
                    exactLocal,
                    context,
                    state.Intent!.Value.EnemySlot,
                    state.Intent.Value.Target,
                    expectedActionId)
                : ResolveCurrentExactCandidate(
                    exactLocal,
                    context,
                    expectedActionId);
        }

        if (currentIntentStillTracked &&
            runtimeCandidate is { } trackedCandidate &&
            trackedCandidate.Target.Address != frozenTargetAddress)
        {
            runtimeCandidate = null;
        }

        var exactActionLocallyReady = actionLocallyReady &&
                                      resolvedActionId == expectedActionId &&
                                      runtimeCandidate?.TargetActionReady == true;
        var previousIntent = state.Intent;
        var decision = GunbreakerContinuationRules.Observe(
            state,
            new GunbreakerContinuationObservation(
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

        if (decision.NextState.Intent is { IsValid: true } nextIntent &&
            (!previousIntent.HasValue || previousIntent.Value != nextIntent))
        {
            if (runtimeCandidate is not { } frozenCandidate ||
                exactLocal is null ||
                frozenCandidate.Core.Context != nextIntent.Context ||
                frozenCandidate.Core.EnemySlot != nextIntent.EnemySlot ||
                frozenCandidate.Core.Actor != nextIntent.Target)
            {
                decision = new GunbreakerContinuationDecision(
                    GunbreakerContinuationState.Initial,
                    GunbreakerContinuationDecisionKind.Cancelled,
                    GunbreakerContinuationDecisionReason.CandidateUnavailable);
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

        state = decision.NextState;
        var inputClaimed = decision.InputClaimed;
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

            var completion = GunbreakerContinuationRules.ApplyNativeAttemptOutcome(
                state,
                nativeOutcome,
                nowMilliseconds);
            if (completion.SpendExposure)
            {
                exposure = GunbreakerContinuationRules.MarkCarrierExposureSpent(
                    exposure,
                    intent.ExposureGeneration,
                    intent.ActionId,
                    intent.ProcStatusId);
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

        if (state.Phase != GunbreakerContinuationPhase.Buffered)
            ClearFrozenRuntime();

        var activeIntent = state.Intent ?? decision.Intent;
        var selectedCandidate = runtimeCandidate?.Core;
        var result = new GunbreakerContinuationProbeSnapshot(
            decision.Kind,
            decision.Reason,
            state.Phase,
            resolvedActionId,
            observedProcStatusId,
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
        GunbreakerContinuationIntent intent,
        SupportedPvPContext context,
        bool configurationEnabled,
        bool metadataVerified,
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
        var procStatusBefore = 0u;
        var procStatusAfter = 0u;
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
                        out var observedProcStatusId,
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
                    intent.ActionId);
                if (candidate is null ||
                    candidate.Value.Target.Address != frozenTargetAddress)
                {
                    return false;
                }

                var exactKey = (VirtualKey)intent.FrozenKeyCode;
                var exactGenerationEligible =
                    inputFrame.IsGameplayKeyGenerationEligible(exactKey);
                if (adjustedActionId != intent.ActionId ||
                    observedProcStatusId != intent.ProcStatusId ||
                    !GunbreakerContinuationRules.CanUseFrozenIntent(
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
                        (int)inputFrame.Snapshot.HeldGameplayKey,
                        exactGenerationEligible,
                        candidate.Value.Core))
                {
                    return false;
                }

                var actionManager = ActionManager.Instance();
                if (actionManager == null) return false;
                var useTargetId = GunbreakerContinuationRules.IsSelfCenteredAction(
                    intent.ActionId)
                    ? currentLocal.GameObjectId
                    : intent.Target.GameObjectId;
                targetStatusBefore = actionManager->GetActionStatus(
                    ActionType.Action,
                    intent.ActionId,
                    useTargetId,
                    checkRecastActive: true,
                    checkCastingActive: true);
                if (!nativeBoundaryReady)
                {
                    softUnavailableAtBoundary = true;
                    return false;
                }

                carrierBefore = actionManager->GetAdjustedActionId(
                    GunbreakerContinuationRules.CarrierActionId);
                procStatusBefore = GetExactOwnProcStatusId(
                    currentLocal,
                    intent.ActionId);
                before = ClientActionAttemptBoundary.Capture(
                    actionManager,
                    intent.ActionId);
                if (carrierBefore != intent.ActionId ||
                    procStatusBefore != intent.ProcStatusId ||
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
                    useTargetId,
                    0,
                    ActionManager.UseActionMode.None,
                    0);
                after = ClientActionAttemptBoundary.Capture(
                    actionManager,
                    intent.ActionId);
                carrierAfter = actionManager->GetAdjustedActionId(
                    GunbreakerContinuationRules.CarrierActionId);
                procStatusAfter = GetExactOwnProcStatusId(
                    currentLocal,
                    intent.ActionId);
                targetStatusAfter = actionManager->GetActionStatus(
                    ActionType.Action,
                    intent.ActionId,
                    useTargetId,
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

            return GunbreakerContinuationRules.ClassifyFollowUpBoundary(
                clientAccepted,
                intent.ActionId,
                intent.ProcStatusId,
                procStatusBefore,
                procStatusAfter,
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
                "Seiton Sense Gunbreaker Continuation native boundary failed.");
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
        uint actionId)
    {
        if (!GunbreakerContinuationRules.IsExactFollowUpAction(actionId)) return null;

        switch (context)
        {
            case SupportedPvPContext.CrystallineConflict:
            {
                var runtimeCandidates = new List<RuntimeCandidate>(
                    EnemySlotRules.LastSlot - EnemySlotRules.FirstSlot + 1);
                for (var slot = EnemySlotRules.FirstSlot;
                     slot <= EnemySlotRules.LastSlot;
                     slot++)
                {
                    var enemy = EnemySlotResolver.Resolve(objectTable, slot);
                    if (!HasValidNativeIdentity(enemy)) continue;

                    var enemyIdentity = new TargetPressureActorIdentity(
                        enemy!.GameObjectId,
                        enemy.EntityId);
                    var candidate = ResolveExactCandidate(
                        localPlayer,
                        context,
                        slot,
                        enemyIdentity,
                        actionId);
                    if (candidate.HasValue) runtimeCandidates.Add(candidate.Value);
                }

                if (runtimeCandidates.Count == 0) return null;
                var coreCandidates = new GunbreakerContinuationCandidate[
                    runtimeCandidates.Count];
                for (var index = 0; index < runtimeCandidates.Count; index++)
                    coreCandidates[index] = runtimeCandidates[index].Core;
                var selected = GunbreakerContinuationRules.SelectBestCandidate(
                    context,
                    coreCandidates);
                if (!selected.HasValue) return null;
                for (var index = 0; index < runtimeCandidates.Count; index++)
                {
                    if (runtimeCandidates[index].Core == selected.Value)
                        return runtimeCandidates[index];
                }

                return null;
            }
            case SupportedPvPContext.WolvesDen:
                return TryResolveExactCurrentHardTarget(
                           localPlayer,
                           out _,
                           out var identity)
                    ? ResolveExactCandidate(
                        localPlayer,
                        context,
                        0,
                        identity,
                        actionId)
                    : null;
            default:
                return null;
        }
    }

    private RuntimeCandidate? ResolveExactCandidate(
        IPlayerCharacter localPlayer,
        SupportedPvPContext context,
        int enemySlot,
        TargetPressureActorIdentity expectedTarget,
        uint actionId)
    {
        if (!expectedTarget.IsValid ||
            expectedTarget == new TargetPressureActorIdentity(
                localPlayer.GameObjectId,
                localPlayer.EntityId) ||
            !GunbreakerContinuationRules.IsExactFollowUpAction(actionId))
        {
            return null;
        }

        IBattleChara? target;
        var exactCanonicalIdentity = false;
        switch (context)
        {
            case SupportedPvPContext.CrystallineConflict:
            {
                if (!EnemySlotRules.IsValidSlot(enemySlot)) return null;

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
                    !TryResolveExactCurrentHardTarget(
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
        var useTargetId = GunbreakerContinuationRules.IsSelfCenteredAction(actionId)
            ? localPlayer.GameObjectId
            : expectedTarget.GameObjectId;
        var targetActionReady = actionManager != null &&
                                actionManager->GetAdjustedActionId(
                                    GunbreakerContinuationRules.CarrierActionId) == actionId &&
                                actionManager->GetActionStatus(
                                    ActionType.Action,
                                    actionId,
                                    useTargetId,
                                    checkRecastActive: true,
                                    checkCastingActive: true) == 0;
        var nativeRangeAndLineOfSight = hasValidActionTarget &&
            (GunbreakerContinuationRules.IsSelfCenteredAction(actionId)
                ? IsWithinExactFatedBrandRadius(localPlayer, target)
                : SeitonRangeRules.HasNativeRangeAndLineOfSight(
                    ActionManager.GetActionInRangeOrLoS(
                        actionId,
                        sourceObject,
                        targetObject)));
        var alive = IsLiveBattleCharacter(target);
        return new RuntimeCandidate(
            new GunbreakerContinuationCandidate(
                context,
                enemySlot,
                expectedTarget,
                exactCanonicalIdentity,
                alive,
                target.IsTargetable,
                target.CurrentHp,
                target.MaxHp,
                hasValidActionTarget,
                nativeRangeAndLineOfSight),
            target,
            targetActionReady);
    }

    private static bool TryObserveActionState(
        IPlayerCharacter localPlayer,
        out uint resolvedActionId,
        out uint exactProcStatusId,
        out bool actionLocallyReady,
        out bool nativeBoundaryReady)
    {
        resolvedActionId = 0;
        exactProcStatusId = 0;
        actionLocallyReady = false;
        nativeBoundaryReady = false;
        if (!HasValidNativeIdentity(localPlayer) ||
            localPlayer.ClassJob.IsValid != true ||
            localPlayer.ClassJob.RowId != GunbreakerContinuationRules.GunbreakerJobId)
        {
            return false;
        }

        var actionManager = ActionManager.Instance();
        if (actionManager == null) return false;
        resolvedActionId = actionManager->GetAdjustedActionId(
            GunbreakerContinuationRules.CarrierActionId);
        if (!GunbreakerContinuationRules.IsExactFollowUpAction(resolvedActionId))
            return true;

        exactProcStatusId = GetExactOwnProcStatusId(
            localPlayer,
            resolvedActionId);
        if (exactProcStatusId == 0) return true;

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

    private bool TryResolveExactCurrentHardTarget(
        IPlayerCharacter localPlayer,
        out IBattleChara? target,
        out TargetPressureActorIdentity identity)
    {
        target = null;
        identity = default;
        var nativeTargetId = GetNativeHardTargetId(localPlayer);
        if (!IsNetworkObjectId(nativeTargetId)) return false;

        var byObjectId = objectTable.SearchById(nativeTargetId) as IBattleChara;
        var byEntityId = nativeTargetId <= uint.MaxValue
            ? objectTable.SearchByEntityId((uint)nativeTargetId) as IBattleChara
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
        if (!HasValidNativeIdentity(candidate) ||
            !ActorIdMatches(nativeTargetId, candidate!) ||
            HasSameNativeIdentity(localPlayer, candidate))
        {
            return false;
        }

        var canonicalByObject = objectTable.SearchById(candidate!.GameObjectId)
            as IBattleChara;
        var canonicalByEntity = objectTable.SearchByEntityId(candidate.EntityId)
            as IBattleChara;
        if (!HasSameNativeIdentity(candidate, canonicalByObject) ||
            !HasSameNativeIdentity(candidate, canonicalByEntity))
        {
            return false;
        }

        target = candidate;
        identity = new TargetPressureActorIdentity(
            candidate.GameObjectId,
            candidate.EntityId);
        return identity.IsValid;
    }

    private static uint GetExactOwnProcStatusId(
        IPlayerCharacter localPlayer,
        uint actionId)
    {
        var expected = GunbreakerContinuationRules.GetExpectedProcStatusId(actionId);
        if (expected == 0 || !IsNetworkEntityId(localPlayer.EntityId)) return 0;

        var matches = 0;
        foreach (var status in localPlayer.StatusList)
        {
            if (status.StatusId != expected) continue;
            if (status.SourceId != localPlayer.EntityId ||
                !float.IsFinite(status.RemainingTime) ||
                status.RemainingTime <= 0f ||
                ++matches > 1)
            {
                return 0;
            }
        }

        return matches == 1 ? expected : 0;
    }

    private static bool IsWithinExactFatedBrandRadius(
        IGameObject localPlayer,
        IGameObject target)
    {
        var local = localPlayer.Position;
        var remote = target.Position;
        return float.IsFinite(local.X) && float.IsFinite(local.Y) &&
               float.IsFinite(local.Z) && float.IsFinite(remote.X) &&
               float.IsFinite(remote.Y) && float.IsFinite(remote.Z) &&
               Vector3.Distance(local, remote) <=
               GunbreakerContinuationRules.FatedBrandRadiusYalms;
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
        GunbreakerContinuationIntent intent,
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
        GunbreakerContinuationIntent intent,
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

    private GunbreakerContinuationProbeSnapshot PublishTerminalSnapshot(
        GunbreakerContinuationDecisionKind decision,
        GunbreakerContinuationDecisionReason reason,
        string message)
    {
        var result = GunbreakerContinuationProbeSnapshot.Initial with
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
        GunbreakerContinuationIntent intent,
        ClientActionAttemptOutcome outcome,
        GunbreakerContinuationNativeAttemptDecision completion) =>
        outcome switch
        {
            ClientActionAttemptOutcome.ClientAccepted =>
                $"Gunbreaker {intent.ActionId} client-accepted",
            ClientActionAttemptOutcome.ClientRejected when completion.RetryScheduled =>
                $"Gunbreaker {intent.ActionId} client-rejected; exact intent retained",
            ClientActionAttemptOutcome.ClientRejected =>
                $"Gunbreaker {intent.ActionId} retry limit reached",
            ClientActionAttemptOutcome.SoftUnavailable =>
                $"Gunbreaker {intent.ActionId} waiting for native boundary",
            ClientActionAttemptOutcome.AcceptanceUnknown =>
                $"Gunbreaker {intent.ActionId} acceptance ambiguous; intent terminal",
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

    private static bool IsExpectedCarrier(GameAction action) =>
        action.Name.ToString() == "Continuation" &&
        action.Icon == 9_361 &&
        action.IsPvP &&
        action.IsPlayerAction &&
        action.ClassJob.IsValid &&
        action.ClassJob.RowId == GunbreakerContinuationRules.GunbreakerJobId &&
        action.ActionCategory.IsValid &&
        action.ActionCategory.RowId == 4 &&
        action.Range == 0 &&
        action.EffectRange == 0 &&
        action.CastType == 0 &&
        action.Cast100ms == 0 &&
        action.Recast100ms == 10 &&
        action.CooldownGroup == 3 &&
        action.AdditionalCooldownGroup == 0 &&
        action.MaxCharges == 0 &&
        action.ActionProcStatus.RowId == 0 &&
        action.PrimaryCostType == 0 &&
        action.PrimaryCostValue == 0 &&
        !action.CanTargetSelf &&
        !action.CanTargetParty &&
        !action.CanTargetAlliance &&
        !action.CanTargetHostile &&
        !action.CanTargetAlly &&
        !action.TargetArea &&
        action.RequiresLineOfSight &&
        action.NeedToFaceTarget &&
        !action.AffectsPosition;

    private static bool IsExpectedFollowUp(
        GameAction action,
        ActionProcStatus procStatus,
        GameStatus status,
        FollowUpMetadata definition)
    {
        var hostile = !definition.SelfCentered;
        return GunbreakerContinuationRules.IsExactFollowUpAction(
                   definition.ActionId) &&
               GunbreakerContinuationRules.GetExpectedProcRowId(
                   definition.ActionId) == definition.ProcRowId &&
               GunbreakerContinuationRules.GetExpectedProcStatusId(
                   definition.ActionId) == definition.StatusId &&
               action.Name.ToString() == definition.Name &&
               action.Icon == definition.Icon &&
               action.IsPvP &&
               !action.IsPlayerAction &&
               action.ClassJob.IsValid &&
               action.ClassJob.RowId ==
                   GunbreakerContinuationRules.GunbreakerJobId &&
               action.ActionCategory.IsValid &&
               action.ActionCategory.RowId == 4 &&
               action.Range == definition.Range &&
               action.EffectRange == definition.EffectRange &&
               action.CastType == definition.CastType &&
               action.Cast100ms == 0 &&
               action.Recast100ms == 10 &&
               action.CooldownGroup == 3 &&
               action.AdditionalCooldownGroup == 0 &&
               action.MaxCharges == 0 &&
               action.ActionProcStatus.RowId == definition.ProcRowId &&
               procStatus.Status.RowId == definition.StatusId &&
               action.PrimaryCostType == 10 &&
               action.PrimaryCostValue == definition.StatusId &&
               action.CanTargetSelf == definition.SelfCentered &&
               action.CanTargetHostile == hostile &&
               !action.CanTargetParty &&
               !action.CanTargetAlliance &&
               !action.CanTargetAlly &&
               !action.TargetArea &&
               action.RequiresLineOfSight &&
               action.NeedToFaceTarget &&
               !action.AffectsPosition &&
               status.Name.ToString() == definition.StatusName &&
               status.Icon == definition.StatusIcon &&
               status.StatusCategory == 1 &&
               !status.CanDispel &&
               !status.IsPermanent;
    }

    private readonly record struct FollowUpMetadata(
        uint ActionId,
        string Name,
        uint Icon,
        uint ProcRowId,
        uint StatusId,
        string StatusName,
        uint StatusIcon,
        sbyte Range,
        byte EffectRange,
        byte CastType,
        bool SelfCentered);

    private readonly record struct RuntimeCandidate(
        GunbreakerContinuationCandidate Core,
        IBattleChara Target,
        bool TargetActionReady);
}
