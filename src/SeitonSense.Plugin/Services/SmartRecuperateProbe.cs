using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using SeitonSense.Core;
using SeitonSense.Plugin.Models;

namespace SeitonSense.Plugin.Services;

internal sealed record SmartRecuperateProbeSnapshot(
    SmartRecuperateDecisionKind Decision,
    SmartRecuperateDecisionReason Reason,
    uint ResolvedActionId,
    uint CurrentHp,
    uint MaximumHp,
    uint MissingHp,
    uint CurrentMp,
    uint MaximumMp,
    bool LocallyReady,
    bool GuardSuppressed,
    VirtualKey HeldGameplayKey,
    bool InputClaimed,
    bool UseActionAttempted,
    bool UseActionAccepted,
    long AttemptCount,
    long AcceptedCount,
    string LastEvent)
{
    internal SmartRecuperatePhase Phase { get; init; }
    internal ulong HealthEventToken { get; init; }
    internal int FrozenKeyCode { get; init; }
    internal int NativeAttemptCount { get; init; }
    internal ClientActionAttemptOutcome LastNativeOutcome { get; init; }
    internal long RejectedCount { get; init; }
    internal long UnknownCount { get; init; }
    internal long SoftWaitCount { get; init; }
    internal HeldCastCancellationRequest? CastCancellationRequest { get; init; }

    internal static SmartRecuperateProbeSnapshot Initial { get; } = new(
        Decision: SmartRecuperateDecisionKind.None,
        Reason: SmartRecuperateDecisionReason.None,
        ResolvedActionId: 0,
        CurrentHp: 0,
        MaximumHp: 0,
        MissingHp: 0,
        CurrentMp: 0,
        MaximumMp: 0,
        LocallyReady: false,
        GuardSuppressed: false,
        HeldGameplayKey: VirtualKey.NO_KEY,
        InputClaimed: false,
        UseActionAttempted: false,
        UseActionAccepted: false,
        AttemptCount: 0,
        AcceptedCount: 0,
        LastEvent: "Waiting");
}

/// <summary>
/// Runs the exact self-only held Recuperate policy. The same physical hold may
/// authorize a later distinct accepted cooldown epoch, but never substitutes
/// another action, actor, key, or health event for a frozen retry.
/// </summary>
internal sealed unsafe class SmartRecuperateProbe
{
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly IDutyState dutyState;
    private readonly PluginConfiguration configuration;
    private readonly NearAssistRedirector nearAssist;
    private readonly IPluginLog log;
    private SmartRecuperateState state = SmartRecuperateState.Initial;
    private SmartRecuperateProbeSnapshot snapshot =
        SmartRecuperateProbeSnapshot.Initial;
    private long attemptCount;
    private long acceptedCount;
    private long rejectedCount;
    private long unknownCount;
    private long softWaitCount;
    private long nextErrorLogAt;
    private string lastEvent = "Waiting";

    internal SmartRecuperateProbe(
        IClientState clientState,
        IObjectTable objectTable,
        IDutyState dutyState,
        PluginConfiguration configuration,
        NearAssistRedirector nearAssist,
        IPluginLog log)
    {
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.dutyState = dutyState;
        this.configuration = configuration;
        this.nearAssist = nearAssist;
        this.log = log;
    }

    internal SmartRecuperateProbeSnapshot Snapshot => Volatile.Read(ref snapshot);

    internal SmartRecuperateProbeSnapshot Observe(
        IPlayerCharacter? localPlayer,
        SupportedPvPContext context,
        bool configurationEnabled,
        bool metadataVerified,
        bool actionHelpersSuppressedByGuard,
        bool higherPriorityClaimed,
        EmergencyActionInputFrame inputFrame,
        long nowMilliseconds,
        bool hardReset = false)
    {
        var effectiveHardReset = hardReset || nowMilliseconds < 0;
        var localIdentityValid = TryGetExactIdentity(localPlayer, out var localIdentity);
        var localAlive = localIdentityValid && IsAlive(localPlayer);
        var localTargetable = localIdentityValid && localPlayer!.IsTargetable;
        var currentHp = localIdentityValid ? localPlayer!.CurrentHp : 0;
        var maximumHp = localIdentityValid ? localPlayer!.MaxHp : 0;
        var currentMp = localIdentityValid ? localPlayer!.CurrentMp : 0;
        var maximumMp = localIdentityValid ? localPlayer!.MaxMp : 0;
        var resolvedActionId = 0u;
        var cooldownReady = false;
        var resourcesReady = false;
        var nativeBoundaryReady = false;
        var actionStateReadable = localIdentityValid && TryGetActionState(
            localPlayer!,
            out resolvedActionId,
            out cooldownReady,
            out resourcesReady,
            out nativeBoundaryReady);
        var actionReady = actionStateReadable && cooldownReady && resourcesReady;

        var input = inputFrame.Snapshot;
        var frozenKeyStillDown = state.Intent is { IsValid: true } frozen &&
                                 inputFrame.IsGameplayKeyPhysicallyDown(
                                     (VirtualKey)frozen.FrozenKeyCode);
        var decision = SmartRecuperateRules.Observe(
            state,
            new SmartRecuperateObservation(
                ConfigurationEnabled: configurationEnabled,
                Context: context,
                LocalPlayer: localIdentity,
                IsLocalPlayerAlive: localAlive,
                IsLocalPlayerTargetable: localTargetable,
                MetadataVerified: metadataVerified,
                ActionHelpersSuppressedByGuard: actionHelpersSuppressedByGuard,
                HigherPriorityClaimed: higherPriorityClaimed,
                InputProbeSucceeded: input.ProbeSucceeded,
                IsTextInputActive: input.IsTextInputActive,
                HeldGameplayKeyEligible: inputFrame.HeldGameplayKeyEligible,
                ResolvedActionId: resolvedActionId,
                ActionLocallyReady: actionReady,
                CurrentHp: currentHp,
                MaximumHp: maximumHp,
                CurrentMp: currentMp,
                MaximumMp: maximumMp,
                HardReset: effectiveHardReset,
                HeldGameplayKeyCode: (int)input.HeldGameplayKey,
                FrozenKeyStillDown: frozenKeyStillDown,
                NativeBoundaryReady: nativeBoundaryReady,
                ActionCooldownReady: actionStateReadable && cooldownReady,
                NowMilliseconds: nowMilliseconds));
        state = decision.NextState;

        var inputClaimed = decision.ShouldConsumeInputGeneration;
        if (inputClaimed) inputFrame.Consume();

        var castCancellationRequest = BuildCastCancellationRequest(
            localPlayer,
            localIdentity,
            configurationEnabled,
            metadataVerified,
            actionHelpersSuppressedByGuard,
            higherPriorityClaimed,
            resolvedActionId,
            actionStateReadable && cooldownReady && resourcesReady,
            currentHp,
            maximumHp,
            currentMp,
            maximumMp,
            inputClaimed,
            state,
            inputFrame);

        var attempted = false;
        var accepted = false;
        var nativeOutcome = ClientActionAttemptOutcome.None;
        if (decision.ShouldDispatch && decision.Intent is { } intent)
        {
            try
            {
                nativeOutcome = TryUseRecuperate(
                    intent,
                    localPlayer!.Address,
                    configurationEnabled,
                    metadataVerified,
                    higherPriorityClaimed,
                    inputFrame,
                    out attempted);
            }
            catch (Exception exception)
            {
                nativeOutcome = ClientActionAttemptOutcome.AcceptanceUnknown;
                LogAttemptFailure(exception, nowMilliseconds);
            }

            if (attempted) Interlocked.Increment(ref attemptCount);
            if (nativeOutcome == ClientActionAttemptOutcome.ClientAccepted)
                Interlocked.Increment(ref acceptedCount);
            if (nativeOutcome == ClientActionAttemptOutcome.ClientRejected)
                Interlocked.Increment(ref rejectedCount);
            if (nativeOutcome == ClientActionAttemptOutcome.AcceptanceUnknown)
                Interlocked.Increment(ref unknownCount);

            var completion = SmartRecuperateRules.ApplyNativeAttemptOutcome(
                state,
                nativeOutcome,
                nowMilliseconds);
            state = completion.NextState;
            accepted = completion.ClientAccepted;
            if (completion.SoftWait) Interlocked.Increment(ref softWaitCount);
            lastEvent = DescribeNativeResult(nativeOutcome, completion);
        }
        else
        {
            lastEvent = decision.Reason.ToString();
        }

        var result = new SmartRecuperateProbeSnapshot(
            decision.Kind,
            decision.Reason,
            resolvedActionId,
            currentHp,
            maximumHp,
            SmartRecuperateRules.GetMissingHp(currentHp, maximumHp),
            currentMp,
            maximumMp,
            actionReady && nativeBoundaryReady,
            actionHelpersSuppressedByGuard,
            input.HeldGameplayKey,
            inputClaimed,
            attempted,
            accepted,
            Interlocked.Read(ref attemptCount),
            Interlocked.Read(ref acceptedCount),
            lastEvent)
        {
            Phase = state.Phase,
            HealthEventToken = state.Intent?.HealthEventToken ?? 0,
            FrozenKeyCode = state.Intent?.FrozenKeyCode ?? 0,
            NativeAttemptCount = state.Retry.NativeAttemptCount,
            LastNativeOutcome = nativeOutcome != ClientActionAttemptOutcome.None
                ? nativeOutcome
                : state.LastNativeOutcome,
            RejectedCount = Interlocked.Read(ref rejectedCount),
            UnknownCount = Interlocked.Read(ref unknownCount),
            SoftWaitCount = Interlocked.Read(ref softWaitCount),
            CastCancellationRequest = castCancellationRequest,
        };
        Volatile.Write(ref snapshot, result);
        return result;
    }

    internal void Reset()
    {
        state = SmartRecuperateState.Initial;
        lastEvent = "Reset";
        Volatile.Write(ref snapshot, SmartRecuperateProbeSnapshot.Initial with
        {
            AttemptCount = Interlocked.Read(ref attemptCount),
            AcceptedCount = Interlocked.Read(ref acceptedCount),
            RejectedCount = Interlocked.Read(ref rejectedCount),
            UnknownCount = Interlocked.Read(ref unknownCount),
            SoftWaitCount = Interlocked.Read(ref softWaitCount),
            LastEvent = lastEvent,
        });
    }

    internal SmartRecuperateProbeSnapshot FailClosed()
    {
        state = SmartRecuperateState.Initial;
        lastEvent = "Failed closed";
        var result = SmartRecuperateProbeSnapshot.Initial with
        {
            Decision = SmartRecuperateDecisionKind.Cancelled,
            Reason = SmartRecuperateDecisionReason.HardReset,
            AttemptCount = Interlocked.Read(ref attemptCount),
            AcceptedCount = Interlocked.Read(ref acceptedCount),
            RejectedCount = Interlocked.Read(ref rejectedCount),
            UnknownCount = Interlocked.Read(ref unknownCount),
            SoftWaitCount = Interlocked.Read(ref softWaitCount),
            LastEvent = lastEvent,
        };
        Volatile.Write(ref snapshot, result);
        return result;
    }

    private ClientActionAttemptOutcome TryUseRecuperate(
        SmartRecuperateIntent intent,
        nint expectedLocalAddress,
        bool configurationEnabled,
        bool metadataVerified,
        bool higherPriorityClaimed,
        EmergencyActionInputFrame inputFrame,
        out bool attempted)
    {
        attempted = false;
        var currentLocal = ResolveExactLocalPlayer(intent.LocalPlayer, expectedLocalAddress);
        if (currentLocal is null) return ClientActionAttemptOutcome.NotInvoked;

        var guardSuppressed = IsCurrentlySuppressedByGuard(
            currentLocal,
            Environment.TickCount64);
        var actionStateReadable = TryGetActionState(
            currentLocal,
            out var resolvedActionId,
            out var cooldownReady,
            out var resourcesReady,
            out var nativeBoundaryReady);
        var currentIdentity = new TargetPressureActorIdentity(
            currentLocal.GameObjectId,
            currentLocal.EntityId);
        var frozenKeyStillDown = inputFrame.IsGameplayKeyPhysicallyDown(
            (VirtualKey)intent.FrozenKeyCode);
        if (!actionStateReadable ||
            !SmartRecuperateRules.CanUseFrozenIntent(
                intent,
                configurationEnabled,
                ResolveCurrentContext(),
                currentIdentity,
                IsAlive(currentLocal),
                currentLocal.IsTargetable,
                metadataVerified,
                guardSuppressed,
                higherPriorityClaimed,
                resolvedActionId,
                true,
                currentLocal.CurrentHp,
                currentLocal.MaxHp,
                currentLocal.CurrentMp,
                currentLocal.MaxMp,
                intent.FrozenKeyCode,
                frozenKeyStillDown))
        {
            return ClientActionAttemptOutcome.NotInvoked;
        }

        var actionManager = ActionManager.Instance();
        if (actionManager == null) return ClientActionAttemptOutcome.NotInvoked;
        if (!cooldownReady || !resourcesReady || !nativeBoundaryReady)
            return ClientActionAttemptOutcome.SoftUnavailable;

        var boundaryBefore = ClientActionAttemptBoundary.Capture(
            actionManager,
            intent.ActionId);
        attempted = true;
        var accepted = nearAssist.RunWithoutRedirect(() =>
            actionManager->UseAction(
                ActionType.Action,
                intent.ActionId,
                intent.LocalPlayer.GameObjectId,
                0,
                ActionManager.UseActionMode.None,
                0));
        return ClientActionAttemptBoundaryRules.Classify(
            accepted,
            intent.ActionId,
            boundaryBefore,
            ClientActionAttemptBoundary.Capture(actionManager, intent.ActionId));
    }

    private HeldCastCancellationRequest? BuildCastCancellationRequest(
        IPlayerCharacter? localPlayer,
        TargetPressureActorIdentity localIdentity,
        bool configurationEnabled,
        bool metadataVerified,
        bool actionHelpersSuppressedByGuard,
        bool higherPriorityClaimed,
        uint resolvedActionId,
        bool actionStructurallyReady,
        uint currentHp,
        uint maximumHp,
        uint currentMp,
        uint maximumMp,
        bool inputClaimed,
        SmartRecuperateState currentState,
        EmergencyActionInputFrame inputFrame)
    {
        if (!inputClaimed ||
            !actionStructurallyReady ||
            currentState.Phase != SmartRecuperatePhase.Buffered ||
            currentState.Intent is not { IsValid: true } intent ||
            localPlayer is null ||
            !TryGetExactIdentity(localPlayer, out var currentIdentity) ||
            currentIdentity != localIdentity ||
            !HasCastCancellationBoundary(localPlayer) ||
            !SmartRecuperateRules.CanUseFrozenIntent(
                intent,
                configurationEnabled,
                ResolveCurrentContext(),
                currentIdentity,
                IsAlive(localPlayer),
                localPlayer.IsTargetable,
                metadataVerified,
                actionHelpersSuppressedByGuard,
                higherPriorityClaimed,
                resolvedActionId,
                actionLocallyReady: true,
                currentHp,
                maximumHp,
                currentMp,
                maximumMp,
                intent.FrozenKeyCode,
                inputFrame.IsGameplayKeyPhysicallyDown(
                    (VirtualKey)intent.FrozenKeyCode)))
        {
            return null;
        }

        return new HeldCastCancellationRequest(
            HeldCastCancellationHelperKind.SmartRecuperate,
            intent.ActionId,
            intent.LocalPlayer,
            intent.LocalPlayer,
            intent.FrozenKeyCode,
            intent.HealthEventToken);
    }

    private static bool HasCastCancellationBoundary(IPlayerCharacter localPlayer)
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

        identity = new TargetPressureActorIdentity(player.GameObjectId, player.EntityId);
        return identity.IsValid;
    }

    private IPlayerCharacter? ResolveExactLocalPlayer(
        TargetPressureActorIdentity expectedIdentity,
        nint expectedAddress)
    {
        var current = objectTable.LocalPlayer;
        return TryGetExactIdentity(current, out var currentIdentity) &&
               currentIdentity == expectedIdentity &&
               current!.Address == expectedAddress
            ? current
            : null;
    }

    private SupportedPvPContext ResolveCurrentContext()
    {
        var condition = dutyState.ContentFinderCondition;
        var conditionValid = condition.IsValid;
        return PvPMatchRules.ResolveSupportedContext(
                   clientState.IsPvP,
                   clientState.IsPvPExcludingDen,
                   includeWolvesDenTesting: configuration.EnableWolvesDenTesting,
                   clientState.TerritoryType,
                   conditionValid,
                   conditionValid && condition.Value.PvP,
                   conditionValid ? condition.Value.ContentUICategory.RowId : 0,
                   conditionValid && condition.Value.CrystallineConflictCasualRoulette,
                   conditionValid && condition.Value.CrystallineConflictRankedRoulette);
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

    private static bool TryGetActionState(
        IPlayerCharacter localPlayer,
        out uint resolvedActionId,
        out bool cooldownReady,
        out bool resourcesReady,
        out bool nativeBoundaryReady)
    {
        resolvedActionId = 0;
        cooldownReady = false;
        resourcesReady = false;
        nativeBoundaryReady = false;
        if (!localPlayer.ClassJob.IsValid ||
            localPlayer.ClassJob.RowId == 0 ||
            GetNativeObject(localPlayer) == null)
        {
            return false;
        }

        var actionManager = ActionManager.Instance();
        if (actionManager == null) return false;
        resolvedActionId = actionManager->GetAdjustedActionId(
            SmartRecuperateRules.ActionId);
        if (resolvedActionId != SmartRecuperateRules.ActionId) return false;
        var fingerprint = ClientActionAttemptBoundary.Capture(
            actionManager,
            resolvedActionId);
        cooldownReady = fingerprint.Captured && fingerprint.IsActionOffCooldown;
        resourcesReady = fingerprint.Captured && fingerprint.ResourceStatus == 0;
        nativeBoundaryReady = HeldActionRetryRules.IsNativeBoundaryNearQueueable(
            actionManager->AnimationLock,
            localPlayer.IsCasting,
            actionManager->CastActionId,
            actionManager->ActionQueued);
        return true;
    }

    private static bool IsAlive(IPlayerCharacter? player) =>
        player is not null &&
        !player.IsDead &&
        SmartRecuperateRules.HasValidHealth(player.CurrentHp, player.MaxHp);

    private static GameObject* GetNativeObject(IPlayerCharacter player)
    {
        var native = (GameObject*)player.Address;
        return native != null && native->EntityId == player.EntityId
            ? native
            : null;
    }

    private static bool IsNetworkEntityId(uint entityId) =>
        entityId is not 0 and not 0xE0000000 and not uint.MaxValue;

    private static bool IsNetworkObjectId(ulong gameObjectId) =>
        gameObjectId is not 0 and not 0xE0000000 and not ulong.MaxValue;

    private static string DescribeNativeResult(
        ClientActionAttemptOutcome outcome,
        SmartRecuperateNativeAttemptDecision completion) =>
        outcome switch
        {
            ClientActionAttemptOutcome.ClientAccepted =>
                "Recuperate client-accepted; awaiting cooldown epoch",
            ClientActionAttemptOutcome.ClientRejected when completion.RetryScheduled =>
                "Recuperate client-rejected; exact intent retained for bounded retry",
            ClientActionAttemptOutcome.ClientRejected =>
                "Recuperate retry limit reached",
            ClientActionAttemptOutcome.SoftUnavailable =>
                "Recuperate waiting without spending retry budget",
            ClientActionAttemptOutcome.AcceptanceUnknown =>
                "Recuperate acceptance ambiguous; exact intent terminal",
            _ => completion.Reason.ToString(),
        };

    private void LogAttemptFailure(Exception exception, long nowMilliseconds)
    {
        if (nowMilliseconds >= 0 && nowMilliseconds < nextErrorLogAt) return;
        nextErrorLogAt = nowMilliseconds < 0
            ? 0
            : nowMilliseconds > long.MaxValue - 10_000
                ? long.MaxValue
                : nowMilliseconds + 10_000;
        log.Error(
            exception,
            "Seiton Sense Smart Recuperate acceptance became ambiguous and will not be retried.");
    }
}
