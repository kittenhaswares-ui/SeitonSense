using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
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
    private EmergencyPurifyBufferState state = EmergencyPurifyBufferState.Initial;
    private long totalNativeAttempts;
    private long totalClientRejected;
    private long totalClientAccepted;
    private long totalAcceptanceUnknown;
    private long totalStructuralSoftWaits;
    private long totalNativeRetriesScheduled;
    private long nextErrorLogAt;
    private string lastEvent = "Waiting";

    internal EmergencyPurifyProbe(IPluginLog log)
    {
        this.log = log;
    }

    internal PurifyCcStatusInstance? TrackedStatusInstance => state.StatusInstance;

    internal unsafe EmergencyPurifyProbeSnapshot Observe(
        IPlayerCharacter? localPlayer,
        bool isSupportedPvPContext,
        bool configurationEnabled,
        bool allowHeldKeyAtStatusEntry,
        PurifyCcStatusInstance? statusInstance,
        bool statusCurrentlyObserved,
        bool resilienceActive,
        long nowMilliseconds,
        long bufferMilliseconds,
        EmergencyActionInputFrame inputFrame,
        bool hardReset = false)
    {
        var alive = IsAlive(localPlayer);
        var localPlayerIdentityValid = alive && HasValidLocalPlayer(localPlayer!);
        var input = inputFrame.Snapshot;
        var frozenKeyStillDown = state.FrozenKeyCode > 0 &&
                                 inputFrame.IsGameplayKeyPhysicallyDown(
                                     (VirtualKey)state.FrozenKeyCode);

        var locallyReady = !hardReset &&
                           configurationEnabled &&
                           isSupportedPvPContext &&
                           localPlayerIdentityValid &&
                           statusCurrentlyObserved &&
                           !resilienceActive &&
                           !input.IsTextInputActive &&
                           IsPurifyStructurallyReady(localPlayer!);

        var decision = EmergencyPurifyBufferRules.Observe(
            state,
            new EmergencyPurifyBufferObservation(
                configurationEnabled,
                isSupportedPvPContext,
                alive,
                localPlayerIdentityValid,
                resilienceActive,
                input.IsTextInputActive,
                statusInstance,
                statusCurrentlyObserved &&
                input.ProbeSucceeded &&
                inputFrame.FreshGameplayKeyPressed,
                statusCurrentlyObserved &&
                input.ProbeSucceeded &&
                inputFrame.HeldGameplayKeyEligible,
                allowHeldKeyAtStatusEntry,
                locallyReady,
                nowMilliseconds,
                hardReset,
                bufferMilliseconds,
                (int)input.FreshGameplayKey,
                (int)input.HeldGameplayKey,
                frozenKeyStillDown));

        state = decision.NextState;
        // An active, exact removable self-CC owns scheduler priority even while
        // Purify is waiting on a known local/native boundary. Lower helpers
        // cannot be useful while that CC remains the higher-priority episode.
        var exactConsentStillDown = state.FrozenKeyCode > 0 &&
                                    inputFrame.IsGameplayKeyPhysicallyDown(
                                        (VirtualKey)state.FrozenKeyCode);
        var inputClaimed = configurationEnabled &&
                           isSupportedPvPContext &&
                           localPlayerIdentityValid &&
                           EmergencyPurifyBufferRules.ClaimsSchedulerPriority(
                               state,
                               statusInstance,
                               statusCurrentlyObserved,
                               exactConsentStillDown) &&
                           !resilienceActive &&
                           !input.IsTextInputActive;
        if (decision.ShouldClaimInputFrame)
            inputFrame.Consume();

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
                    configurationEnabled,
                    isSupportedPvPContext,
                    frozenStatus,
                    statusInstance,
                    statusCurrentlyObserved,
                    resilienceActive,
                    input.IsTextInputActive,
                    inputFrame,
                    frozenKeyCode,
                    out attempted);
            }
            catch (Exception exception)
            {
                nativeOutcome = ClientActionAttemptOutcome.AcceptanceUnknown;
                LogAttemptFailure(exception, nowMilliseconds);
            }

            if (attempted) Interlocked.Increment(ref totalNativeAttempts);
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

        // -1 means status/key bounded rather than timer bounded.
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
            LastEvent = lastEvent,
        };
    }

    internal void Reset()
    {
        state = EmergencyPurifyBufferState.Initial;
        lastEvent = "Reset";
    }

    internal EmergencyPurifyProbeSnapshot FailClosed(long nowMilliseconds)
    {
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

    private static unsafe ClientActionAttemptOutcome TryUsePurify(
        IPlayerCharacter localPlayer,
        bool configurationEnabled,
        bool isSupportedPvPContext,
        PurifyCcStatusInstance? expectedStatus,
        PurifyCcStatusInstance? currentlyObservedStatus,
        bool statusCurrentlyObserved,
        bool resilienceActive,
        bool textInputActive,
        EmergencyActionInputFrame inputFrame,
        int expectedKeyCode,
        out bool attempted)
    {
        attempted = false;
        if (!configurationEnabled ||
            !isSupportedPvPContext ||
            expectedStatus is not { IsValid: true } ||
            !statusCurrentlyObserved ||
            currentlyObservedStatus != expectedStatus ||
            resilienceActive ||
            textInputActive ||
            !HasValidLocalPlayer(localPlayer) ||
            expectedKeyCode <= 0 ||
            !inputFrame.IsGameplayKeyPhysicallyDown((VirtualKey)expectedKeyCode))
        {
            return ClientActionAttemptOutcome.NotInvoked;
        }

        var actionManager = ActionManager.Instance();
        if (actionManager == null) return ClientActionAttemptOutcome.NotInvoked;
        if (actionManager->GetAdjustedActionId(EnemyCombatConstants.PurifyActionId) !=
            EnemyCombatConstants.PurifyActionId)
        {
            return ClientActionAttemptOutcome.NotInvoked;
        }
        if (!ClientActionAttemptBoundary.IsExactActionReady(
                actionManager,
                EnemyCombatConstants.PurifyActionId) ||
            !HeldActionRetryRules.IsNativeBoundaryNearQueueable(
                actionManager->AnimationLock,
                localPlayer.IsCasting,
                actionManager->CastActionId,
                actionManager->ActionQueued))
        {
            return ClientActionAttemptOutcome.SoftUnavailable;
        }

        var boundaryBefore = ClientActionAttemptBoundary.Capture(
            actionManager,
            EnemyCombatConstants.PurifyActionId);
        attempted = true;
        var accepted = actionManager->UseAction(
            ActionType.Action,
            EnemyCombatConstants.PurifyActionId,
            localPlayer.GameObjectId,
            0,
            ActionManager.UseActionMode.None,
            0);
        return ClientActionAttemptBoundaryRules.Classify(
            accepted,
            EnemyCombatConstants.PurifyActionId,
            boundaryBefore,
            ClientActionAttemptBoundary.Capture(
                actionManager,
                EnemyCombatConstants.PurifyActionId));
    }

    private static unsafe bool IsPurifyStructurallyReady(IPlayerCharacter localPlayer)
    {
        if (!HasValidLocalPlayer(localPlayer)) return false;

        var actionManager = ActionManager.Instance();
        var nativePlayer = (GameObject*)localPlayer.Address;
        if (actionManager == null ||
            nativePlayer == null ||
            nativePlayer->EntityId != localPlayer.EntityId ||
            !ClientActionAttemptBoundary.IsExactActionReady(
                actionManager,
                EnemyCombatConstants.PurifyActionId))
        {
            return false;
        }

        // GetActionStatus is deliberately not used here: Purify is the action
        // explicitly permitted while its removable CC is active, and a broad
        // action-status gate can incorrectly classify that exceptional state.
        return HeldActionRetryRules.IsNativeBoundaryNearQueueable(
            actionManager->AnimationLock,
            localPlayer.IsCasting,
            actionManager->CastActionId,
            actionManager->ActionQueued);
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
