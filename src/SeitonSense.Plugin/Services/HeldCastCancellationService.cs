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
    BlockedByAutomaticRecoveryCastBoundary = 5,
}

internal sealed record HeldCastCancellationSnapshot(
    HeldCastCancellationDecisionKind Decision,
    HeldCastCancellationDecisionReason Reason,
    ulong CastEpochToken,
    HeldCastCancellationRequest? Request,
    HeldCastCancellationRequest? LastRequestedIntent,
    uint CastActionId,
    uint AdjustedCastActionId,
    uint LocalJobId,
    bool AutomaticRecoveryBasicShotCancellationEnabled,
    bool AutomaticRecoveryBasicShotMetadataVerified,
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
        0,
        0,
        false,
        false,
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
    private readonly AutomaticRecoveryShotCastMetadataValidation
        automaticRecoveryShotCastMetadata;
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
            finalOwnGuardActiveOrPropagating,
        AutomaticRecoveryShotCastMetadataValidation
            automaticRecoveryShotCastMetadata)
    {
        this.log = log;
        this.finalOwnGuardActiveOrPropagating =
            finalOwnGuardActiveOrPropagating ??
            throw new ArgumentNullException(
                nameof(finalOwnGuardActiveOrPropagating));
        this.automaticRecoveryShotCastMetadata =
            automaticRecoveryShotCastMetadata ??
            throw new ArgumentNullException(
                nameof(automaticRecoveryShotCastMetadata));
    }

    internal HeldCastCancellationSnapshot Snapshot => Volatile.Read(ref snapshot);

    internal HeldCastCancellationSnapshot Observe(
        IPlayerCharacter? localPlayer,
        bool featureEnabled,
        bool automaticRecoveryBasicShotCancellationEnabled,
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
        var automaticKeylessRequest =
            request is { IsValid: true, IsAutomaticKeyless: true };
        var textInputActive = automaticKeylessRequest
            ? !TryGetTextInputState(out var currentTextInputActive) ||
              currentTextInputActive
            : inputFrame.Snapshot.IsTextInputActive;
        var frozenKeyStillDown = requestValid &&
                                 (automaticKeylessRequest ||
                                  (IsExactVirtualKey(request!.Value.FrozenKeyCode) &&
                                   inputFrame.IsFrozenGameplayKeyConsentValid(
                                       (VirtualKey)request.Value.FrozenKeyCode)));

        var localJobId = localPlayer?.ClassJob.IsValid == true
            ? localPlayer.ClassJob.RowId
            : 0;
        var castActionId = actionManager == null ? 0 : actionManager->CastActionId;
        var adjustedCastActionId = TryGetAdjustedCastActionId(
            actionManager,
            castActionId);
        var automaticRecoveryBasicShotMetadataVerified =
            automaticRecoveryShotCastMetadata.IsVerified(
                localJobId,
                castActionId);

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
                (inputFrame.IsConsumed || request?.IsAutomaticKeyless == true),
            IntentOtherwiseReady: intentOtherwiseReady,
            Request: request,
            FrozenKeyStillDown: frozenKeyStillDown,
            LocalPlayerIdentityValid: localIdentityValid,
            CurrentLocalPlayer: currentLocalIdentity,
            LocalPlayerAlive: IsAlive(localPlayer),
            LocalPlayerTargetable: localPlayer?.IsTargetable == true,
            CurrentLocalJobId: localJobId,
            ResolvedHelperActionId: boundary.AdjustedActionId,
            HelperActionOffCooldown: boundary.Captured && boundary.IsActionOffCooldown,
            HelperActionResourcesReady: boundary.Captured && boundary.ResourceStatus == 0,
            LocalPlayerIsCasting: localPlayer?.IsCasting == true,
            CastActionId: castActionId,
            AdjustedCastActionId: adjustedCastActionId,
            AutomaticRecoveryBasicShotCancellationEnabled:
                automaticRecoveryBasicShotCancellationEnabled,
            AutomaticRecoveryBasicShotMetadataVerified:
                automaticRecoveryBasicShotMetadataVerified,
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
                else if (request!.Value.HelperKind is
                             HeldCastCancellationHelperKind.Purify or
                             HeldCastCancellationHelperKind.SmartRecuperate
                         ? DefensiveUtilityProbe.HasActiveGuard(localPlayer)
                         : finalOwnGuardActiveOrPropagating(
                             request.Value.LocalPlayer))
                {
                    nativeStatus =
                        HeldCastCancellationNativeStatus.BlockedByOwnGuard;
                }
                else if (!AutomaticKeylessCastBoundaryStillValid(
                             request!.Value,
                             localPlayer,
                             actionManager,
                             automaticRecoveryBasicShotCancellationEnabled))
                {
                    nativeStatus = HeldCastCancellationNativeStatus
                        .BlockedByAutomaticRecoveryCastBoundary;
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
            observation.AdjustedCastActionId,
            observation.CurrentLocalJobId,
            automaticRecoveryBasicShotCancellationEnabled,
            automaticRecoveryBasicShotMetadataVerified,
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

    private bool AutomaticKeylessCastBoundaryStillValid(
        HeldCastCancellationRequest request,
        IPlayerCharacter? localPlayer,
        ActionManager* actionManager,
        bool automaticRecoveryBasicShotCancellationEnabled)
    {
        if (!request.IsAutomaticKeyless) return true;
        if (localPlayer?.ClassJob.IsValid != true || actionManager == null)
            return false;

        var localJobId = localPlayer.ClassJob.RowId;
        var castActionId = actionManager->CastActionId;
        var adjustedCastActionId = TryGetAdjustedCastActionId(
            actionManager,
            castActionId);
        var exactReviewedBasicShot =
            localPlayer.IsCasting &&
            automaticRecoveryShotCastMetadata.IsVerified(
                localJobId,
                castActionId) &&
            AutomaticRecoveryShotCastRules
                .IsExactAllowedPairWithAdjustedIdentity(
                    localJobId,
                    castActionId,
                    adjustedCastActionId);
        if (!exactReviewedBasicShot) return false;

        if (request.IsAutomaticBardRepellingShot)
        {
            return localJobId == BardRepellingShotRules.BardJobId &&
                   castActionId == BardRepellingShotRules.PowerfulShotActionId;
        }

        return request.IsAutomaticRecovery &&
               automaticRecoveryBasicShotCancellationEnabled;
    }

    private static uint TryGetAdjustedCastActionId(
        ActionManager* actionManager,
        uint castActionId)
    {
        if (actionManager == null || castActionId == 0) return 0;
        try
        {
            return actionManager->GetAdjustedActionId(castActionId);
        }
        catch
        {
            return 0;
        }
    }

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
            HeldCastCancellationNativeStatus.BlockedByAutomaticRecoveryCastBoundary =>
                "Keyless helper cast cancellation vetoed by the final exact reviewed basic-shot boundary",
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
