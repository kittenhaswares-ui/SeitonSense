using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using SeitonSense.Core;

namespace SeitonSense.Plugin.Services;

internal enum HeldCastCancellationNativeStatus : byte
{
    None = 0,
    Requested = 1,
    NativeBoundaryUnavailable = 2,
    RequestFaulted = 3,
    BlockedByOwnGuard = 4,
}

internal sealed record HeldCastCancellationSnapshot(
    HeldCastCancellationDecisionKind Decision,
    HeldCastCancellationDecisionReason Reason,
    ulong CastEpochToken,
    HeldCastCancellationRequest? Request,
    HeldCastCancellationRequest? LastRequestedIntent,
    uint CastActionId,
    HeldCastCancellationNativeStatus NativeStatus,
    HeldCastCancellationNativeStatus LastNativeStatus,
    long NativeRequestCount,
    long NativeFaultCount,
    string LastEvent)
{
    internal static HeldCastCancellationSnapshot Initial { get; } = new(
        HeldCastCancellationDecisionKind.Inactive,
        HeldCastCancellationDecisionReason.None,
        0,
        null,
        null,
        0,
        HeldCastCancellationNativeStatus.None,
        HeldCastCancellationNativeStatus.None,
        0,
        0,
        "Waiting");
}

/// <summary>
/// Requests the game's own cast-cancel path for one exact prioritized held
/// intent. The void native call is terminal for the observed cast epoch and is
/// never treated as confirmation that the cast ended. Callers must wait for a
/// later framework frame and run their normal full action preflight again.
/// </summary>
internal sealed unsafe class HeldCastCancellationService
{
    private readonly IPluginLog log;
    private readonly Func<TargetPressureActorIdentity, bool>
        finalOwnGuardActiveOrPropagating;
    private HeldCastCancellationState state = HeldCastCancellationState.Initial;
    private HeldCastCancellationSnapshot snapshot = HeldCastCancellationSnapshot.Initial;
    private HeldCastCancellationRequest? lastRequestedIntent;
    private HeldCastCancellationNativeStatus lastNativeStatus;
    private long nativeRequestCount;
    private long nativeFaultCount;
    private long nextErrorLogAt;

    internal HeldCastCancellationService(
        IPluginLog log,
        Func<TargetPressureActorIdentity, bool>
            finalOwnGuardActiveOrPropagating)
    {
        this.log = log;
        this.finalOwnGuardActiveOrPropagating =
            finalOwnGuardActiveOrPropagating ??
            throw new ArgumentNullException(
                nameof(finalOwnGuardActiveOrPropagating));
    }

    internal HeldCastCancellationSnapshot Snapshot => Volatile.Read(ref snapshot);

    internal HeldCastCancellationSnapshot Observe(
        IPlayerCharacter? localPlayer,
        bool featureEnabled,
        bool supportedContext,
        bool guardActive,
        bool prioritizedInputClaimed,
        bool intentOtherwiseReady,
        HeldCastCancellationRequest? request,
        EmergencyActionInputFrame inputFrame,
        bool hardReset = false)
    {
        var actionManager = ActionManager.Instance();
        var localIdentityValid = TryGetCurrentLocalIdentity(
            localPlayer,
            out var currentLocalIdentity);
        var requestValid = request is { IsValid: true };
        var automaticPurifyRequest =
            request is { IsValid: true, IsAutomaticPurify: true };
        var textInputActive = automaticPurifyRequest
            ? !TryGetTextInputState(out var currentTextInputActive) ||
              currentTextInputActive
            : inputFrame.Snapshot.IsTextInputActive;
        var frozenKeyStillDown = requestValid &&
                                 (automaticPurifyRequest ||
                                  (IsExactVirtualKey(request!.Value.FrozenKeyCode) &&
                                   inputFrame.IsGameplayKeyPhysicallyDown(
                                       (VirtualKey)request.Value.FrozenKeyCode)));

        var boundary = requestValid && actionManager != null
            ? ClientActionAttemptBoundary.Capture(
                actionManager,
                request!.Value.HelperActionId)
            : default;
        var observation = new HeldCastCancellationObservation(
            HardReset: hardReset,
            FeatureEnabled: featureEnabled,
            SupportedContext: supportedContext,
            TextInputActive: textInputActive,
            GuardActive: guardActive,
            PrioritizedInputClaimed:
                prioritizedInputClaimed &&
                (inputFrame.IsConsumed || request?.IsAutomaticPurify == true),
            IntentOtherwiseReady: intentOtherwiseReady,
            Request: request,
            FrozenKeyStillDown: frozenKeyStillDown,
            LocalPlayerIdentityValid: localIdentityValid,
            CurrentLocalPlayer: currentLocalIdentity,
            LocalPlayerAlive: IsAlive(localPlayer),
            LocalPlayerTargetable: localPlayer?.IsTargetable == true,
            ResolvedHelperActionId: boundary.AdjustedActionId,
            HelperActionOffCooldown: boundary.Captured && boundary.IsActionOffCooldown,
            HelperActionResourcesReady: boundary.Captured && boundary.ResourceStatus == 0,
            LocalPlayerIsCasting: localPlayer?.IsCasting == true,
            CastActionId: actionManager == null ? 0 : actionManager->CastActionId,
            ActionQueued: actionManager == null || actionManager->ActionQueued,
            AnimationLockSeconds: actionManager == null
                ? float.NaN
                : actionManager->AnimationLock);

        var decision = HeldCastCancellationRules.Observe(state, observation);
        // Latch before entering the void native boundary. An unavailable pointer
        // or exception is terminal for this cast epoch and must never be retried.
        state = decision.NextState;

        var nativeStatus = HeldCastCancellationNativeStatus.None;
        if (decision.ShouldInvokeNative)
        {
            lastRequestedIntent = request;
            try
            {
                var uiState = UIState.Instance();
                if (uiState == null)
                {
                    nativeStatus = HeldCastCancellationNativeStatus.NativeBoundaryUnavailable;
                    nativeFaultCount++;
                }
                else if (finalOwnGuardActiveOrPropagating(
                             request!.Value.LocalPlayer))
                {
                    nativeStatus =
                        HeldCastCancellationNativeStatus.BlockedByOwnGuard;
                }
                else
                {
                    uiState->Hotbar.CancelCast();
                    nativeStatus = HeldCastCancellationNativeStatus.Requested;
                    nativeRequestCount++;
                }
            }
            catch (Exception exception)
            {
                nativeStatus = HeldCastCancellationNativeStatus.RequestFaulted;
                nativeFaultCount++;
                LogNativeFault(exception);
            }

            lastNativeStatus = nativeStatus;
        }

        var result = new HeldCastCancellationSnapshot(
            decision.Kind,
            decision.Reason,
            decision.NextState.LastCastEpochToken,
            request,
            lastRequestedIntent,
            observation.CastActionId,
            nativeStatus,
            lastNativeStatus,
            nativeRequestCount,
            nativeFaultCount,
            Describe(decision, nativeStatus));
        Volatile.Write(ref snapshot, result);
        return result;
    }

    private static bool TryGetTextInputState(out bool active)
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

    private static bool TryGetCurrentLocalIdentity(
        IPlayerCharacter? localPlayer,
        out TargetPressureActorIdentity identity)
    {
        identity = default;
        if (localPlayer is null ||
            localPlayer.Address == nint.Zero ||
            !IsNetworkGameObjectId(localPlayer.GameObjectId) ||
            !IsNetworkEntityId(localPlayer.EntityId))
        {
            return false;
        }

        identity = new TargetPressureActorIdentity(
            localPlayer.GameObjectId,
            localPlayer.EntityId);
        return identity.IsValid;
    }

    private static bool IsAlive(IPlayerCharacter? localPlayer) =>
        localPlayer is not null &&
        !localPlayer.IsDead &&
        localPlayer.CurrentHp > 0 &&
        localPlayer.MaxHp >= localPlayer.CurrentHp;

    private static bool IsExactVirtualKey(int keyCode)
    {
        if (keyCode <= 0) return false;
        var key = (VirtualKey)keyCode;
        return key != VirtualKey.NO_KEY && Enum.IsDefined(typeof(VirtualKey), key);
    }

    private static bool IsNetworkGameObjectId(ulong gameObjectId) =>
        gameObjectId is not 0 and not 0xE0000000 and not ulong.MaxValue;

    private static bool IsNetworkEntityId(uint entityId) =>
        entityId is not 0 and not 0xE0000000 and not uint.MaxValue;

    private static string Describe(
        HeldCastCancellationDecision decision,
        HeldCastCancellationNativeStatus nativeStatus) =>
        nativeStatus switch
        {
            HeldCastCancellationNativeStatus.Requested =>
                "Native cast cancellation requested; awaiting a later clear-cast frame",
            HeldCastCancellationNativeStatus.BlockedByOwnGuard =>
                "Native cast cancellation vetoed by a fresh exact own-Guard check",
            HeldCastCancellationNativeStatus.NativeBoundaryUnavailable =>
                "Native cast-cancel boundary unavailable; no retry in this cast epoch",
            HeldCastCancellationNativeStatus.RequestFaulted =>
                "Native cast-cancel request faulted; no retry in this cast epoch",
            _ when decision.Kind == HeldCastCancellationDecisionKind.CastEnded =>
                "Consistent clear-cast frame observed; next cast epoch may rearm",
            _ when decision.Reason == HeldCastCancellationDecisionReason.AlreadyRequested =>
                "Cast-cancel request already terminal for this cast epoch",
            _ when decision.Kind == HeldCastCancellationDecisionKind.WaitingForCastEnd =>
                $"Cast epoch quarantined by {decision.Reason}; awaiting consistent clear signals",
            _ when decision.Kind == HeldCastCancellationDecisionKind.Inactive =>
                "No active cast observed",
            _ => $"Cast observed; cancellation blocked by {decision.Reason}",
        };

    private void LogNativeFault(Exception exception)
    {
        var now = Environment.TickCount64;
        if (now < nextErrorLogAt) return;
        nextErrorLogAt = now > long.MaxValue - 10_000
            ? long.MaxValue
            : now + 10_000;
        log.Error(
            exception,
            "Seiton Sense native cast-cancel request failed closed and will not be retried for this cast epoch.");
    }
}
