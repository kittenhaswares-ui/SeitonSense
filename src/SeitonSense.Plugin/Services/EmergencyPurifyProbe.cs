using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI;
using SeitonSense.Core;

namespace SeitonSense.Plugin.Services;

internal sealed record EmergencyPurifyProbeSnapshot(
    EmergencyPurifyBufferPhase Phase,
    PurifyCcStatusInstance? StatusInstance,
    EmergencyPurifyBufferDecisionKind Decision,
    EmergencyPurifyBufferCancelReason CancelReason,
    EmergencyPurifyInputTrigger InputTrigger,
    long BufferRemainingMilliseconds,
    bool FreshGameplayKeyPressed,
    VirtualKey FreshGameplayKey,
    bool HeldGameplayKeyEligible,
    VirtualKey HeldGameplayKey,
    bool LocallyReady,
    bool UseActionAttempted,
    bool UseActionAccepted)
{
    internal int FrozenKeyCode { get; init; }
    internal int NativeAttemptCount { get; init; }
    internal ClientActionAttemptOutcome LastNativeOutcome { get; init; }
    internal long TotalNativeAttempts { get; init; }
    internal long TotalClientRejected { get; init; }
    internal long TotalClientAccepted { get; init; }
    internal long TotalAcceptanceUnknown { get; init; }
    internal long TotalStructuralSoftWaits { get; init; }
    internal long TotalNativeRetriesScheduled { get; init; }
    internal bool InputClaimed { get; init; }
    internal HeldCastCancellationRequest? CastCancellationRequest { get; init; }
    internal string LastEvent { get; init; } = "Waiting";

    internal static EmergencyPurifyProbeSnapshot Initial { get; } = new(
        EmergencyPurifyBufferPhase.WaitingForStatus,
        null,
        EmergencyPurifyBufferDecisionKind.None,
        EmergencyPurifyBufferCancelReason.None,
        EmergencyPurifyInputTrigger.None,
        0,
        false,
        VirtualKey.NO_KEY,
        false,
        VirtualKey.NO_KEY,
        false,
        false,
        false);
}

internal sealed class EmergencyPurifyProbe
{
    private readonly IPluginLog log;
    private readonly NearAssistRedirector nearAssist;
    private readonly Func<TargetPressureActorIdentity, bool>
        finalOwnGuardActiveOrPropagating;
    private EmergencyPurifyBufferState state = EmergencyPurifyBufferState.Initial;
    private ClientActionAttemptFingerprint lastNativeBoundary;
    private long lastNativeAttemptFrameId = -1;
    private long totalNativeAttempts;
    private long totalClientRejected;
    private long totalClientAccepted;
    private long totalAcceptanceUnknown;
    private long totalStructuralSoftWaits;
    private long totalNativeRetriesScheduled;
    private long nextErrorLogAt;
    private string lastEvent = "Waiting";

    internal EmergencyPurifyProbe(
        IPluginLog log,
        NearAssistRedirector nearAssist,
        Func<TargetPressureActorIdentity, bool>
            finalOwnGuardActiveOrPropagating)
    {
        this.log = log;
        this.nearAssist = nearAssist ??
            throw new ArgumentNullException(nameof(nearAssist));
        this.finalOwnGuardActiveOrPropagating =
            finalOwnGuardActiveOrPropagating ??
            throw new ArgumentNullException(
                nameof(finalOwnGuardActiveOrPropagating));
    }

    internal PurifyCcStatusInstance? TrackedStatusInstance => state.StatusInstance;

    internal unsafe EmergencyPurifyProbeSnapshot Observe(
        IPlayerCharacter? localPlayer,
        bool isSupportedPvPContext,
        bool configurationEnabled,
        bool automaticStatusTriggerEnabled,
        bool allowHeldKeyAtStatusEntry,
        PurifyCcStatusInstance? statusInstance,
        bool statusCurrentlyObserved,
        bool resilienceActive,
        long nowMilliseconds,
        long bufferMilliseconds,
        EmergencyActionInputFrame inputFrame,
        NinjaShukuchiHiddenStatusCatalog ninjaShukuchiHiddenStatuses,
        bool adaptiveResponseEnabled,
        bool allowOccupiedNativeQueue,
        long frameworkFrameId,
        bool hardReset = false)
    {
        if (hardReset)
        {
            lastNativeBoundary = default;
            lastNativeAttemptFrameId = -1;
        }
        var stealthSuppressed = automaticStatusTriggerEnabled
            ? NinjaShukuchiStealthGate.ShouldSuppressAutomaticRecovery(
                localPlayer,
                ninjaShukuchiHiddenStatuses)
            : NinjaShukuchiStealthGate.IsActive(
                localPlayer,
                ninjaShukuchiHiddenStatuses);
        var effectiveConfigurationEnabled = configurationEnabled && !stealthSuppressed;
        // Hidden is a temporary dispatch blocker, not a lifecycle reset. In
        // particular, it must not erase a terminal exact-status latch and let
        // the same still-present CC episode auto-Purify twice after stealth.
        var effectiveHardReset = hardReset;
        var alive = IsAlive(localPlayer);
        var localPlayerIdentityValid = alive && HasValidLocalPlayer(localPlayer!);
        var input = inputFrame.Snapshot;
        var textInputActive = input.IsTextInputActive;
        if (automaticStatusTriggerEnabled)
        {
            if (!TryGetTextInputState(out textInputActive)) textInputActive = true;
        }
        var frozenKeyStillDown = state.FrozenKeyCode > 0 &&
                                 inputFrame.IsGameplayKeyPhysicallyDown(
                                     (VirtualKey)state.FrozenKeyCode);

        var actionStructurallyReady = false;
        var globalQueueReady = false;
        var nativeBoundary = default(ClientActionAttemptFingerprint);
        var actionStateReadable = localPlayerIdentityValid &&
                                  TryGetPurifyActionState(
                                      localPlayer!,
                                      out actionStructurallyReady,
                                      out globalQueueReady,
                                      out nativeBoundary,
                                      allowOccupiedNativeQueue);
        var relevantNativeBoundaryEdge = adaptiveResponseEnabled &&
                                         IsRelevantNativeBoundaryEdge(
                                             lastNativeBoundary,
                                             nativeBoundary,
                                             EnemyCombatConstants.PurifyActionId,
                                             allowOccupiedNativeQueue);
        lastNativeBoundary = nativeBoundary;
        var locallyReady = !effectiveHardReset &&
                           effectiveConfigurationEnabled &&
                           isSupportedPvPContext &&
                           localPlayerIdentityValid &&
                           statusCurrentlyObserved &&
                           !resilienceActive &&
                           !textInputActive &&
                           actionStateReadable &&
                           actionStructurallyReady &&
                           globalQueueReady;

        var decision = EmergencyPurifyBufferRules.Observe(
            state,
            new EmergencyPurifyBufferObservation(
                effectiveConfigurationEnabled,
                isSupportedPvPContext,
                alive,
                localPlayerIdentityValid,
                resilienceActive,
                textInputActive,
                statusInstance,
                FreshKeyPressed: !automaticStatusTriggerEnabled &&
                                 statusCurrentlyObserved &&
                                 input.ProbeSucceeded &&
                                 inputFrame.FreshGameplayKeyPressed,
                HeldKeyEligible: !automaticStatusTriggerEnabled &&
                                 statusCurrentlyObserved &&
                                 input.ProbeSucceeded &&
                                 inputFrame.HeldGameplayKeyEligible,
                AllowHeldKeyAtStatusEntry:
                    !automaticStatusTriggerEnabled &&
                    allowHeldKeyAtStatusEntry,
                locallyReady,
                nowMilliseconds,
                effectiveHardReset,
                bufferMilliseconds,
                FreshKeyCode: automaticStatusTriggerEnabled
                    ? 0
                    : (int)input.FreshGameplayKey,
                HeldKeyCode: automaticStatusTriggerEnabled
                    ? 0
                    : (int)input.HeldGameplayKey,
                frozenKeyStillDown,
                AutomaticStatusTriggerEnabled: automaticStatusTriggerEnabled,
                EdgeDrivenRetriesEnabled: adaptiveResponseEnabled,
                FrameworkFrameId: frameworkFrameId,
                LastNativeAttemptFrameId: lastNativeAttemptFrameId,
                RelevantNativeBoundaryEdge: relevantNativeBoundaryEdge));

        state = decision.NextState;
        // An active, exact removable self-CC owns scheduler priority even while
        // Purify is waiting on a known local/native boundary. Lower helpers
        // cannot be useful while that CC remains the higher-priority episode.
        var exactConsentStillDown = state.FrozenKeyCode > 0 &&
                                    inputFrame.IsGameplayKeyPhysicallyDown(
                                        (VirtualKey)state.FrozenKeyCode);
        var automaticStatusIntent =
            EmergencyPurifyBufferRules.IsAutomaticStatusTrigger(
                state.FrozenInputTrigger);
        var schedulerOpportunityAvailable = !automaticStatusIntent ||
                                            (actionStateReadable &&
                                             actionStructurallyReady);
        var inputClaimed = effectiveConfigurationEnabled &&
                           isSupportedPvPContext &&
                           localPlayerIdentityValid &&
                           schedulerOpportunityAvailable &&
                           EmergencyPurifyBufferRules.ClaimsSchedulerPriority(
                               state,
                               statusInstance,
                               statusCurrentlyObserved,
                               exactConsentStillDown) &&
                           !resilienceActive &&
                           !textInputActive;
        if (decision.ShouldClaimInputFrame ||
            (automaticStatusIntent && inputClaimed))
            inputFrame.Consume();

        var castCancellationRequest = BuildCastCancellationRequest(
            localPlayer,
            isSupportedPvPContext,
            effectiveConfigurationEnabled,
            statusInstance,
            statusCurrentlyObserved,
            resilienceActive,
            textInputActive,
            inputClaimed,
            actionStateReadable && actionStructurallyReady,
            state,
            inputFrame,
            ninjaShukuchiHiddenStatuses);

        var attempted = false;
        var accepted = false;
        var nativeOutcome = ClientActionAttemptOutcome.None;
        var cancelReason = decision.CancelReason;
        if (decision.ShouldDispatch)
        {
            var frozenStatus = state.StatusInstance;
            var frozenKeyCode = state.FrozenKeyCode;
            try
            {
                nativeOutcome = TryUsePurify(
                    localPlayer!,
                    effectiveConfigurationEnabled,
                    isSupportedPvPContext,
                    frozenStatus,
                    statusInstance,
                    statusCurrentlyObserved,
                    resilienceActive,
                    textInputActive,
                    inputFrame,
                    state.FrozenInputTrigger,
                    frozenKeyCode,
                    ninjaShukuchiHiddenStatuses,
                    allowOccupiedNativeQueue,
                    out attempted);
            }
            catch (Exception exception)
            {
                nativeOutcome = ClientActionAttemptOutcome.AcceptanceUnknown;
                LogAttemptFailure(exception, nowMilliseconds);
            }

            if (attempted) Interlocked.Increment(ref totalNativeAttempts);
            if (attempted) lastNativeAttemptFrameId = frameworkFrameId;
            if (nativeOutcome == ClientActionAttemptOutcome.ClientRejected)
                Interlocked.Increment(ref totalClientRejected);
            if (nativeOutcome == ClientActionAttemptOutcome.ClientAccepted)
                Interlocked.Increment(ref totalClientAccepted);
            if (nativeOutcome == ClientActionAttemptOutcome.AcceptanceUnknown)
                Interlocked.Increment(ref totalAcceptanceUnknown);

            var completion = EmergencyPurifyBufferRules.ApplyNativeAttemptOutcome(
                state,
                nativeOutcome,
                nowMilliseconds);
            state = completion.NextState;
            if (completion.RetryScheduled)
                Interlocked.Increment(ref totalNativeRetriesScheduled);
            if (completion.SoftWait)
                Interlocked.Increment(ref totalStructuralSoftWaits);
            if (completion.CancelReason != EmergencyPurifyBufferCancelReason.None)
                cancelReason = completion.CancelReason;
            accepted = completion.ClientAccepted;
            lastEvent = DescribeNativeResult(nativeOutcome, completion);
        }
        else
        {
            if (decision.Kind == EmergencyPurifyBufferDecisionKind.Armed &&
                !locallyReady)
            {
                Interlocked.Increment(ref totalStructuralSoftWaits);
            }

            lastEvent = decision.Kind == EmergencyPurifyBufferDecisionKind.Armed
                ? locallyReady
                    ? "Waiting for native retry throttle"
                    : "Waiting for exact structural readiness"
                : decision.CancelReason != EmergencyPurifyBufferCancelReason.None
                    ? decision.CancelReason.ToString()
                    : decision.Kind.ToString();
        }

        // -1 means exact-status bounded rather than timer bounded.
        var remaining = state.Phase == EmergencyPurifyBufferPhase.Buffered
            ? -1
            : 0;
        return new EmergencyPurifyProbeSnapshot(
            state.Phase,
            state.StatusInstance,
            decision.Kind,
            cancelReason,
            state.FrozenInputTrigger != EmergencyPurifyInputTrigger.None
                ? state.FrozenInputTrigger
                : decision.InputTrigger,
            remaining,
            input.FreshGameplayKeyPressed,
            input.FreshGameplayKey,
            input.HeldGameplayKeyEligible,
            input.HeldGameplayKey,
            locallyReady,
            attempted,
            accepted)
        {
            FrozenKeyCode = state.FrozenKeyCode,
            NativeAttemptCount = state.NativeAttemptCount,
            LastNativeOutcome = nativeOutcome != ClientActionAttemptOutcome.None
                ? nativeOutcome
                : state.LastNativeOutcome,
            TotalNativeAttempts = Interlocked.Read(ref totalNativeAttempts),
            TotalClientRejected = Interlocked.Read(ref totalClientRejected),
            TotalClientAccepted = Interlocked.Read(ref totalClientAccepted),
            TotalAcceptanceUnknown = Interlocked.Read(ref totalAcceptanceUnknown),
            TotalStructuralSoftWaits = Interlocked.Read(ref totalStructuralSoftWaits),
            TotalNativeRetriesScheduled =
                Interlocked.Read(ref totalNativeRetriesScheduled),
            InputClaimed = inputClaimed,
            CastCancellationRequest = castCancellationRequest,
            LastEvent = lastEvent,
        };
    }

    internal void Reset()
    {
        state = EmergencyPurifyBufferState.Initial;
        lastNativeBoundary = default;
        lastNativeAttemptFrameId = -1;
        lastEvent = "Reset";
    }

    internal EmergencyPurifyProbeSnapshot FailClosed(long nowMilliseconds)
    {
        lastNativeBoundary = default;
        lastNativeAttemptFrameId = -1;
        var decision = EmergencyPurifyBufferRules.Observe(
            state,
            new EmergencyPurifyBufferObservation(
                ConfigurationEnabled: false,
                IsSupportedPvPContext: true,
                IsAlive: true,
                IsLocalPlayerIdentityValid: true,
                IsResilienceActive: false,
                IsTextInputActive: false,
                StatusInstance: state.StatusInstance,
                FreshKeyPressed: false,
                HeldKeyEligible: false,
                AllowHeldKeyAtStatusEntry: false,
                PurifyLocallyReady: false,
                NowMilliseconds: nowMilliseconds));
        state = decision.NextState;
        lastEvent = "Failed closed";
        return EmergencyPurifyProbeSnapshot.Initial with
        {
            Phase = state.Phase,
            StatusInstance = state.StatusInstance,
            Decision = decision.Kind,
            CancelReason = decision.CancelReason,
            FrozenKeyCode = state.FrozenKeyCode,
            NativeAttemptCount = state.NativeAttemptCount,
            LastNativeOutcome = state.LastNativeOutcome,
            TotalNativeAttempts = Interlocked.Read(ref totalNativeAttempts),
            TotalClientRejected = Interlocked.Read(ref totalClientRejected),
            TotalClientAccepted = Interlocked.Read(ref totalClientAccepted),
            TotalAcceptanceUnknown = Interlocked.Read(ref totalAcceptanceUnknown),
            TotalStructuralSoftWaits = Interlocked.Read(ref totalStructuralSoftWaits),
            TotalNativeRetriesScheduled =
                Interlocked.Read(ref totalNativeRetriesScheduled),
            InputClaimed = false,
            LastEvent = lastEvent,
        };
    }

    private static bool IsAlive(IPlayerCharacter? localPlayer) =>
        localPlayer is not null &&
        !localPlayer.IsDead &&
        localPlayer.CurrentHp > 0 &&
        localPlayer.MaxHp >= localPlayer.CurrentHp;

    private unsafe ClientActionAttemptOutcome TryUsePurify(
        IPlayerCharacter localPlayer,
        bool configurationEnabled,
        bool isSupportedPvPContext,
        PurifyCcStatusInstance? expectedStatus,
        PurifyCcStatusInstance? currentlyObservedStatus,
        bool statusCurrentlyObserved,
        bool resilienceActive,
        bool textInputActive,
        EmergencyActionInputFrame inputFrame,
        EmergencyPurifyInputTrigger expectedInputTrigger,
        int expectedKeyCode,
        NinjaShukuchiHiddenStatusCatalog ninjaShukuchiHiddenStatuses,
        bool allowOccupiedNativeQueue,
        out bool attempted)
    {
        attempted = false;
        var automaticStatusTrigger =
            EmergencyPurifyBufferRules.IsAutomaticStatusTrigger(
                expectedInputTrigger);
        if (!configurationEnabled ||
            !isSupportedPvPContext ||
            expectedStatus is not { IsValid: true } ||
            !statusCurrentlyObserved ||
            currentlyObservedStatus != expectedStatus ||
            resilienceActive ||
            textInputActive ||
            !HasValidLocalPlayer(localPlayer) ||
            (automaticStatusTrigger
                ? NinjaShukuchiStealthGate.ShouldSuppressAutomaticRecovery(
                    localPlayer,
                    ninjaShukuchiHiddenStatuses)
                : NinjaShukuchiStealthGate.IsActive(
                    localPlayer,
                    ninjaShukuchiHiddenStatuses)) ||
            (!automaticStatusTrigger &&
             (expectedKeyCode <= 0 ||
              !inputFrame.IsGameplayKeyPhysicallyDown(
                  (VirtualKey)expectedKeyCode))))
        {
            // No native UseAction boundary was crossed. Keep the exact CC
            // episode alive so the next framework frame can fully revalidate it.
            return ClientActionAttemptOutcome.SoftUnavailable;
        }

        var actionManager = ActionManager.Instance();
        if (actionManager == null) return ClientActionAttemptOutcome.SoftUnavailable;
        if (actionManager->GetAdjustedActionId(EnemyCombatConstants.PurifyActionId) !=
            EnemyCombatConstants.PurifyActionId)
        {
            return ClientActionAttemptOutcome.SoftUnavailable;
        }
        var criticalBoundary = ClientActionAttemptBoundary.Capture(
            actionManager,
            EnemyCombatConstants.PurifyActionId);
        if (!criticalBoundary.IsCriticalRecoveryActionReady(
                EnemyCombatConstants.PurifyActionId,
                allowOccupiedNativeQueue) ||
            !HeldActionRetryRules.IsNativeBoundaryNearQueueable(
                actionManager->AnimationLock,
                localPlayer.IsCasting,
                actionManager->CastActionId,
                actionManager->ActionQueued,
                allowOccupiedNativeQueue))
        {
            return ClientActionAttemptOutcome.SoftUnavailable;
        }

        var exactLocal = new TargetPressureActorIdentity(
            localPlayer.GameObjectId,
            localPlayer.EntityId);
        if (!exactLocal.IsValid ||
            finalOwnGuardActiveOrPropagating(exactLocal))
        {
            return ClientActionAttemptOutcome.SoftUnavailable;
        }

        var finalTextInputActive = automaticStatusTrigger
            ? !TryGetTextInputState(out var currentTextInputActive) ||
              currentTextInputActive
            : textInputActive;
        if (finalTextInputActive)
            return ClientActionAttemptOutcome.SoftUnavailable;

        var boundaryBefore = ClientActionAttemptBoundary.Capture(
            actionManager,
            EnemyCombatConstants.PurifyActionId);
        Exception? nativeException = null;
        var invocation = nearAssist.RunExactAutomaticActionWithoutRedirect(
            new ExactAutomaticActionBoundaryIntent(
                ActionType.Action,
                EnemyCombatConstants.PurifyActionId,
                localPlayer.GameObjectId,
                ActionManager.UseActionMode.None),
            () =>
            {
                try
                {
                    return actionManager->UseAction(
                        ActionType.Action,
                        EnemyCombatConstants.PurifyActionId,
                        localPlayer.GameObjectId,
                        0,
                        ActionManager.UseActionMode.None,
                        0);
                }
                catch (Exception exception)
                {
                    nativeException = exception;
                    return false;
                }
            });
        attempted = invocation.NativeBoundaryInvoked;
        if (nativeException is not null)
        {
            if (!attempted) return ClientActionAttemptOutcome.SoftUnavailable;
            throw nativeException;
        }
        if (!attempted) return ClientActionAttemptOutcome.SoftUnavailable;

        return ClientActionAttemptBoundaryRules.ClassifyCriticalRecovery(
            invocation.ClientReturnedAccepted,
            EnemyCombatConstants.PurifyActionId,
            boundaryBefore,
            ClientActionAttemptBoundary.Capture(
                actionManager,
                EnemyCombatConstants.PurifyActionId),
            allowOccupiedNativeQueue);
    }

    private static unsafe bool TryGetTextInputState(out bool active)
    {
        try
        {
            var atkModule = RaptureAtkModule.Instance();
            if (atkModule == null)
            {
                active = true;
                return false;
            }

            active = atkModule->IsTextInputActive() || ImGui.GetIO().WantTextInput;
            return true;
        }
        catch
        {
            active = true;
            return false;
        }
    }

    private static unsafe bool TryGetPurifyActionState(
        IPlayerCharacter localPlayer,
        out bool actionStructurallyReady,
        out bool globalQueueReady,
        out ClientActionAttemptFingerprint boundary,
        bool allowOccupiedNativeQueue)
    {
        actionStructurallyReady = false;
        globalQueueReady = false;
        boundary = default;
        if (!HasValidLocalPlayer(localPlayer)) return false;

        var actionManager = ActionManager.Instance();
        var nativePlayer = (GameObject*)localPlayer.Address;
        if (actionManager == null ||
            nativePlayer == null ||
            nativePlayer->EntityId != localPlayer.EntityId)
        {
            return false;
        }

        boundary = ClientActionAttemptBoundary.Capture(
            actionManager,
            EnemyCombatConstants.PurifyActionId);
        actionStructurallyReady = boundary.Captured &&
                                  boundary.AdjustedActionId ==
                                  EnemyCombatConstants.PurifyActionId &&
                                  boundary.IsActionOffCooldown &&
                                  boundary.ResourceStatus == 0;

        // GetActionStatus is deliberately not used here: Purify is the action
        // explicitly permitted while its removable CC is active, and a broad
        // action-status gate can incorrectly classify that exceptional state.
        globalQueueReady = actionStructurallyReady &&
                           HeldActionRetryRules.IsNativeBoundaryNearQueueable(
            boundary.AnimationLockSeconds,
            localPlayer.IsCasting,
            boundary.CastActionId,
            boundary.ActionQueued,
            allowOccupiedNativeQueue);
        return true;
    }

    private static bool IsRelevantNativeBoundaryEdge(
        ClientActionAttemptFingerprint previous,
        ClientActionAttemptFingerprint current,
        uint actionId,
        bool allowOccupiedNativeQueue) =>
        ClientActionAttemptBoundaryRules.BecameCriticalRecoveryReady(
            actionId,
            previous,
            current,
            allowOccupiedNativeQueue);

    private static unsafe HeldCastCancellationRequest? BuildCastCancellationRequest(
        IPlayerCharacter? localPlayer,
        bool isSupportedPvPContext,
        bool configurationEnabled,
        PurifyCcStatusInstance? currentStatus,
        bool statusCurrentlyObserved,
        bool resilienceActive,
        bool textInputActive,
        bool inputClaimed,
        bool actionStructurallyReady,
        EmergencyPurifyBufferState currentState,
        EmergencyActionInputFrame inputFrame,
        NinjaShukuchiHiddenStatusCatalog ninjaShukuchiHiddenStatuses)
    {
        var automaticStatusTrigger =
            EmergencyPurifyBufferRules.IsAutomaticStatusTrigger(
                currentState.FrozenInputTrigger);
        var physicalConsentValid = currentState.FrozenKeyCode > 0 &&
                                   inputFrame.IsGameplayKeyPhysicallyDown(
                                       (VirtualKey)currentState.FrozenKeyCode);
        if (!configurationEnabled ||
            !isSupportedPvPContext ||
            resilienceActive ||
            textInputActive ||
            !inputClaimed ||
            !actionStructurallyReady ||
            currentState.Phase != EmergencyPurifyBufferPhase.Buffered ||
            currentState.StatusInstance is not { IsValid: true } frozenStatus ||
            currentStatus != frozenStatus ||
            !statusCurrentlyObserved ||
            (!automaticStatusTrigger && !physicalConsentValid) ||
            localPlayer is null ||
            !HasValidLocalPlayer(localPlayer) ||
            (automaticStatusTrigger
                ? NinjaShukuchiStealthGate.ShouldSuppressAutomaticRecovery(
                    localPlayer,
                    ninjaShukuchiHiddenStatuses)
                : NinjaShukuchiStealthGate.IsActive(
                    localPlayer,
                    ninjaShukuchiHiddenStatuses)) ||
            !HasCastCancellationBoundary(localPlayer))
        {
            return null;
        }

        var localIdentity = new TargetPressureActorIdentity(
            localPlayer.GameObjectId,
            localPlayer.EntityId);
        if (!localIdentity.IsValid) return null;

        return new HeldCastCancellationRequest(
            HeldCastCancellationHelperKind.Purify,
            EnemyCombatConstants.PurifyActionId,
            localIdentity,
            localIdentity,
            currentState.FrozenKeyCode,
            frozenStatus.InstanceToken);
    }

    private static unsafe bool HasCastCancellationBoundary(
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

    private static bool HasValidLocalPlayer(IPlayerCharacter localPlayer) =>
        IsAlive(localPlayer) &&
        localPlayer.Address != 0 &&
        localPlayer.EntityId is not 0 and not 0xE0000000 &&
        localPlayer.GameObjectId is not 0 and not 0xE0000000;

    private static string DescribeNativeResult(
        ClientActionAttemptOutcome outcome,
        EmergencyPurifyNativeAttemptDecision completion) =>
        outcome switch
        {
            ClientActionAttemptOutcome.ClientAccepted => "Purify client-accepted; exact CC episode terminal",
            ClientActionAttemptOutcome.ClientRejected when completion.RetryScheduled =>
                "Purify client-rejected; exact intent retained for bounded retry",
            ClientActionAttemptOutcome.ClientRejected => "Purify retry limit reached",
            ClientActionAttemptOutcome.SoftUnavailable =>
                "Purify waiting without spending retry budget",
            ClientActionAttemptOutcome.NotInvoked =>
                "Purify exact intent drifted; episode terminal",
            ClientActionAttemptOutcome.AcceptanceUnknown =>
                "Purify acceptance ambiguous; exact CC episode terminal",
            _ => completion.CancelReason.ToString(),
        };

    private void LogAttemptFailure(Exception exception, long nowMilliseconds)
    {
        if (nowMilliseconds < nextErrorLogAt) return;
        nextErrorLogAt = nowMilliseconds + 10_000;
        log.Error(
            exception,
            "Seiton Sense emergency Purify acceptance became ambiguous and will not be retried.");
    }
}
