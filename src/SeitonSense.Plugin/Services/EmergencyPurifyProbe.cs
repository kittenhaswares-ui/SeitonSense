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
    long BufferRemainingMilliseconds,
    bool FreshGameplayKeyPressed,
    VirtualKey FreshGameplayKey,
    bool LocallyReady,
    bool UseActionAttempted,
    bool UseActionAccepted)
{
    internal static EmergencyPurifyProbeSnapshot Initial { get; } = new(
        EmergencyPurifyBufferPhase.WaitingForStatus,
        null,
        EmergencyPurifyBufferDecisionKind.None,
        EmergencyPurifyBufferCancelReason.None,
        0,
        false,
        VirtualKey.NO_KEY,
        false,
        false,
        false);
}

internal sealed class EmergencyPurifyProbe
{
    // A tiny residual lock is within the game's normal local submission window. The
    // authoritative action-status and cooldown checks below still have to pass.
    private const float MaximumAnimationLockSeconds = 0.05f;

    private readonly GameInputContextProbe inputContext;
    private readonly IPluginLog log;
    private EmergencyPurifyBufferState state = EmergencyPurifyBufferState.Initial;
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
        PurifyCcStatusInstance? statusInstance,
        bool statusCurrentlyObserved,
        bool resilienceActive,
        long nowMilliseconds,
        long bufferMilliseconds,
        bool hardReset = false)
    {
        var alive = IsAlive(localPlayer);
        var localPlayerIdentityValid = alive && HasValidLocalPlayer(localPlayer!);
        var shouldObserveInput = !hardReset &&
                                 configurationEnabled &&
                                 isSupportedPvPContext &&
                                 localPlayerIdentityValid &&
                                 statusCurrentlyObserved &&
                                 !resilienceActive &&
                                 statusInstance is { IsValid: true };
        var input = shouldObserveInput
            ? inputContext.Observe()
            : GameInputContextSnapshot.NotObserved;
        if (!shouldObserveInput) inputContext.Reset();

        var locallyReady = !hardReset &&
                           configurationEnabled &&
                           isSupportedPvPContext &&
                           localPlayerIdentityValid &&
                           statusCurrentlyObserved &&
                           !resilienceActive &&
                           !input.IsTextInputActive &&
                           IsPurifyLocallyReady(localPlayer!);

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
                input.ProbeSucceeded && input.FreshGameplayKeyPressed,
                locallyReady,
                nowMilliseconds,
                hardReset,
                bufferMilliseconds));

        // Consume first. Any exception, false return, or server rejection after this point
        // remains a single terminal attempt for this continuous status instance.
        state = decision.NextState;

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
            remaining,
            input.FreshGameplayKeyPressed,
            input.FreshGameplayKey,
            locallyReady,
            attempted,
            accepted);
    }

    internal void Reset()
    {
        state = EmergencyPurifyBufferState.Initial;
        inputContext.Reset();
    }

    internal EmergencyPurifyProbeSnapshot FailClosed(long nowMilliseconds)
    {
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
                PurifyLocallyReady: false,
                NowMilliseconds: nowMilliseconds));
        state = decision.NextState;
        return new EmergencyPurifyProbeSnapshot(
            state.Phase,
            state.StatusInstance,
            decision.Kind,
            decision.CancelReason,
            0,
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

    private static unsafe bool IsPurifyLocallyReady(IPlayerCharacter localPlayer)
    {
        if (!HasValidLocalPlayer(localPlayer) ||
            localPlayer.MaxMp == 0 ||
            localPlayer.CurrentMp > localPlayer.MaxMp ||
            localPlayer.CurrentMp < EnemyCombatConstants.PurifyMpCost)
        {
            return false;
        }

        var actionManager = ActionManager.Instance();
        if (actionManager == null ||
            !float.IsFinite(actionManager->AnimationLock) ||
            actionManager->AnimationLock < 0f ||
            actionManager->AnimationLock > MaximumAnimationLockSeconds ||
            actionManager->GetAdjustedActionId(EnemyCombatConstants.PurifyActionId) !=
            EnemyCombatConstants.PurifyActionId ||
            !actionManager->IsActionOffCooldown(
                ActionType.Action,
                EnemyCombatConstants.PurifyActionId))
        {
            return false;
        }

        return actionManager->GetActionStatus(
                   ActionType.Action,
                   EnemyCombatConstants.PurifyActionId,
                   localPlayer.GameObjectId,
                   true,
                   true) == 0;
    }

    private static unsafe bool TryUsePurifyOnce(
        IPlayerCharacter localPlayer,
        out bool attempted)
    {
        attempted = false;
        if (!IsPurifyLocallyReady(localPlayer)) return false;

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

    private static unsafe bool HasValidLocalPlayer(IPlayerCharacter localPlayer)
    {
        if (!IsAlive(localPlayer) ||
            localPlayer.GameObjectId is 0 or 0xE0000000)
        {
            return false;
        }

        var gameObject = (GameObject*)localPlayer.Address;
        if (gameObject == null ||
            !localPlayer.IsTargetable ||
            localPlayer.CurrentMount is not null)
        {
            return false;
        }

        var nativeId = gameObject->GetGameObjectId();
        return gameObject->EntityId == localPlayer.EntityId &&
               nativeId.ObjectId == localPlayer.EntityId &&
               nativeId.Id == localPlayer.GameObjectId;
    }

    private void LogAttemptFailure(Exception exception, long nowMilliseconds)
    {
        if (nowMilliseconds < nextErrorLogAt) return;
        nextErrorLogAt = nowMilliseconds + 10_000;
        log.Error(exception, "Seiton Sense emergency Purify attempt failed and will not be retried.");
    }
}
