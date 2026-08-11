using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
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
    private readonly GameInputContextProbe inputContext;
    private readonly IPluginLog log;
    private EmergencyPurifyBufferState state = EmergencyPurifyBufferState.Initial;
    private bool heldKeyOptionWasEnabled;
    private long nextErrorLogAt;

    internal EmergencyPurifyProbe(GameInputContextProbe inputContext, IPluginLog log)
    {
        this.inputContext = inputContext;
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
        bool hardReset = false)
    {
        var alive = IsAlive(localPlayer);
        var localPlayerIdentityValid = alive && HasValidLocalPlayer(localPlayer!);
        var heldKeyOptionJustEnabled = allowHeldKeyAtStatusEntry && !heldKeyOptionWasEnabled;
        heldKeyOptionWasEnabled = allowHeldKeyAtStatusEntry;
        // Keep a baseline throughout the opted-in PvP context. Starting the key
        // probe only after CC appeared discarded the most important first press.
        var shouldObserveInput = !hardReset &&
                                 configurationEnabled &&
                                 isSupportedPvPContext &&
                                 localPlayerIdentityValid;
        var input = shouldObserveInput
            ? inputContext.Observe()
            : GameInputContextSnapshot.NotObserved;
        if (!shouldObserveInput) inputContext.Reset();
        if (shouldObserveInput && (!allowHeldKeyAtStatusEntry || heldKeyOptionJustEnabled))
        {
            // A key pressed while held-mode is disabled may still be a valid fresh
            // edge, but it must not become stale held intent if the option is
            // enabled later without a release.
            inputContext.ConsumeHeldGameplayKeys();
            input = input with
            {
                HeldGameplayKeyEligible = false,
                HeldGameplayKey = VirtualKey.NO_KEY,
            };
        }

        var locallyReady = !hardReset &&
                           configurationEnabled &&
                           isSupportedPvPContext &&
                           localPlayerIdentityValid &&
                           statusCurrentlyObserved &&
                           !resilienceActive &&
                           !input.IsTextInputActive &&
                           ActionManager.Instance() != null;

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
                input.FreshGameplayKeyPressed,
                statusCurrentlyObserved &&
                input.ProbeSucceeded &&
                input.HeldGameplayKeyEligible,
                allowHeldKeyAtStatusEntry,
                locallyReady,
                nowMilliseconds,
                hardReset,
                bufferMilliseconds));

        // Consume first. Any exception, false return, or server rejection after this point
        // remains a single terminal attempt for this continuous status instance.
        state = decision.NextState;
        if (decision.ShouldConsumeInputGeneration)
            inputContext.ConsumeHeldGameplayKeys();

        var attempted = false;
        var accepted = false;
        if (decision.ShouldDispatch)
        {
            try
            {
                accepted = TryUsePurifyOnce(localPlayer!, out attempted);
            }
            catch (Exception exception)
            {
                LogAttemptFailure(exception, nowMilliseconds);
            }
        }

        var remaining = state.Phase == EmergencyPurifyBufferPhase.Buffered
            ? Math.Max(0, state.ExpiresAtMilliseconds - nowMilliseconds)
            : 0;
        return new EmergencyPurifyProbeSnapshot(
            state.Phase,
            state.StatusInstance,
            decision.Kind,
            decision.CancelReason,
            decision.InputTrigger,
            remaining,
            input.FreshGameplayKeyPressed,
            input.FreshGameplayKey,
            input.HeldGameplayKeyEligible,
            input.HeldGameplayKey,
            locallyReady,
            attempted,
            accepted);
    }

    internal void Reset()
    {
        state = EmergencyPurifyBufferState.Initial;
        heldKeyOptionWasEnabled = false;
        inputContext.Reset();
    }

    internal EmergencyPurifyProbeSnapshot FailClosed(long nowMilliseconds)
    {
        heldKeyOptionWasEnabled = false;
        inputContext.Reset();
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
        return new EmergencyPurifyProbeSnapshot(
            state.Phase,
            state.StatusInstance,
            decision.Kind,
            decision.CancelReason,
            decision.InputTrigger,
            0,
            false,
            VirtualKey.NO_KEY,
            false,
            VirtualKey.NO_KEY,
            false,
            false,
            false);
    }

    private static bool IsAlive(IPlayerCharacter? localPlayer) =>
        localPlayer is not null &&
        !localPlayer.IsDead &&
        localPlayer.CurrentHp > 0 &&
        localPlayer.MaxHp >= localPlayer.CurrentHp;

    private static unsafe bool TryUsePurifyOnce(
        IPlayerCharacter localPlayer,
        out bool attempted)
    {
        attempted = false;
        if (!HasValidLocalPlayer(localPlayer)) return false;

        var actionManager = ActionManager.Instance();
        if (actionManager == null) return false;

        attempted = true;
        return actionManager->UseAction(
            ActionType.Action,
            EnemyCombatConstants.PurifyActionId,
            localPlayer.GameObjectId,
            0,
            ActionManager.UseActionMode.None,
            0);
    }

    private static bool HasValidLocalPlayer(IPlayerCharacter localPlayer) =>
        IsAlive(localPlayer) &&
        localPlayer.Address != 0 &&
        localPlayer.EntityId is not 0 and not 0xE0000000 &&
        localPlayer.GameObjectId is not 0 and not 0xE0000000;

    private void LogAttemptFailure(Exception exception, long nowMilliseconds)
    {
        if (nowMilliseconds < nextErrorLogAt) return;
        nextErrorLogAt = nowMilliseconds + 10_000;
        log.Error(exception, "Seiton Sense emergency Purify attempt failed and will not be retried.");
    }
}
