using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI;
using SeitonSense.Core;

namespace SeitonSense.Plugin.Services;

internal sealed record BardRepellingShotProbeSnapshot(
    bool Configured,
    SupportedPvPContext Context,
    bool MetadataVerified,
    BardRepellingShotDecisionKind Decision,
    BardRepellingShotDecisionReason Reason,
    int EnemySlot,
    ulong TargetGameObjectId,
    uint TargetEntityId,
    bool TargetInRangeAndLineOfSight,
    bool PowerfulShotCastObserved,
    bool InputClaimed,
    HeldCastCancellationRequest? CastCancellationRequest,
    bool UseActionAttempted,
    bool UseActionAccepted,
    ClientActionAttemptOutcome LastNativeOutcome,
    long AttemptCount,
    long AcceptedCount,
    string LastEvent)
{
    internal static BardRepellingShotProbeSnapshot Initial { get; } = new(
        false,
        SupportedPvPContext.None,
        false,
        BardRepellingShotDecisionKind.Inactive,
        BardRepellingShotDecisionReason.Disabled,
        0,
        0,
        0,
        false,
        false,
        false,
        null,
        false,
        false,
        ClientActionAttemptOutcome.None,
        0,
        0,
        "Disabled");
}

/// <summary>
/// Optional automatic BRD proximity escape. It freezes one exact Smart Action
/// target already inside Mannstopper range. If Powerful Shot is the only thing
/// preventing the action, it asks the central cast coordinator to cancel that
/// exact reviewed cast, then uses Mannstopper after a clear-cast frame.
/// </summary>
internal sealed unsafe class BardRepellingShotProbe
{
    private const long IntentLeaseMilliseconds = 2_500;

    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly NearAssistRedirector nearAssist;
    private readonly AutomaticRecoveryShotCastMetadataValidation basicShotMetadata;
    private readonly IPluginLog log;
    private FrozenIntent? frozenIntent;
    private bool readyEpochSpent;
    private ulong nextIntentEpoch;
    private long attemptCount;
    private long acceptedCount;
    private long nextErrorLogAt;
    private BardRepellingShotProbeSnapshot snapshot =
        BardRepellingShotProbeSnapshot.Initial;

    internal BardRepellingShotProbe(
        IClientState clientState,
        IObjectTable objectTable,
        NearAssistRedirector nearAssist,
        AutomaticRecoveryShotCastMetadataValidation basicShotMetadata,
        IPluginLog log)
    {
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.nearAssist = nearAssist;
        this.basicShotMetadata = basicShotMetadata;
        this.log = log;
    }

    internal BardRepellingShotProbeSnapshot Snapshot => Volatile.Read(ref snapshot);

    internal BardRepellingShotProbeSnapshot Observe(
        IPlayerCharacter? localPlayer,
        SupportedPvPContext context,
        bool enabled,
        bool actionMetadataVerified,
        bool wolvesDenStrikingDummyMetadataVerified,
        bool ownGuardActiveOrPropagating,
        bool higherPriorityClaimed,
        long nowMilliseconds,
        bool hardReset)
    {
        try
        {
            return ObserveCore(
                localPlayer,
                context,
                enabled,
                actionMetadataVerified,
                wolvesDenStrikingDummyMetadataVerified,
                ownGuardActiveOrPropagating,
                higherPriorityClaimed,
                nowMilliseconds,
                hardReset);
        }
        catch (Exception exception)
        {
            ResetRuntime();
            LogFailure(exception, nowMilliseconds);
            var failed = BardRepellingShotProbeSnapshot.Initial with
            {
                Configured = enabled,
                Context = context,
                MetadataVerified = actionMetadataVerified,
                LastEvent = "Failed closed",
            };
            Volatile.Write(ref snapshot, failed);
            return failed;
        }
    }

    internal void Reset()
    {
        ResetRuntime();
        Volatile.Write(ref snapshot, BardRepellingShotProbeSnapshot.Initial);
    }

    private BardRepellingShotProbeSnapshot ObserveCore(
        IPlayerCharacter? localPlayer,
        SupportedPvPContext context,
        bool enabled,
        bool actionMetadataVerified,
        bool wolvesDenStrikingDummyMetadataVerified,
        bool ownGuardActiveOrPropagating,
        bool higherPriorityClaimed,
        long nowMilliseconds,
        bool hardReset)
    {
        var supportedContext = context is
            SupportedPvPContext.CrystallineConflict or
            SupportedPvPContext.WolvesDen;
        var localValid = TryGetLiveIdentity(localPlayer, out var localIdentity);
        var localJobId = localPlayer?.ClassJob.IsValid == true
            ? localPlayer.ClassJob.RowId
            : 0;
        var actionManager = ActionManager.Instance();
        var resolvedActionId = actionManager == null
            ? 0
            : actionManager->GetAdjustedActionId(
                BardRepellingShotRules.RepellingShotActionId);
        var actionOffCooldown = actionManager != null &&
                                resolvedActionId ==
                                BardRepellingShotRules.RepellingShotActionId &&
                                actionManager->IsActionOffCooldown(
                                    ActionType.Action,
                                    resolvedActionId);
        var actionResourcesReady = actionManager != null &&
                                   resolvedActionId ==
                                   BardRepellingShotRules.RepellingShotActionId &&
                                   actionManager->CheckActionResources(
                                       ActionType.Action,
                                       resolvedActionId) == 0;
        var actionLocallyAvailable = actionOffCooldown && actionResourcesReady;
        var textInputKnown = TryGetTextInputState(out var textInputActive);

        if (!actionOffCooldown)
            readyEpochSpent = false;

        if (hardReset ||
            !enabled ||
            !supportedContext ||
            !localValid ||
            localJobId != BardRepellingShotRules.BardJobId ||
            !actionMetadataVerified ||
            ownGuardActiveOrPropagating ||
            !textInputKnown ||
            textInputActive)
        {
            ResetRuntime();
        }

        if (frozenIntent is { } existing &&
            (!IsFrozenContextValid(
                 existing,
                 localIdentity,
                 context,
                 nowMilliseconds) ||
             !actionLocallyAvailable))
        {
            frozenIntent = null;
        }

        if (frozenIntent is null &&
            !hardReset &&
            !readyEpochSpent &&
            enabled &&
            supportedContext &&
            localValid &&
            localJobId == BardRepellingShotRules.BardJobId &&
            actionMetadataVerified &&
            !ownGuardActiveOrPropagating &&
            textInputKnown &&
            !textInputActive &&
            actionLocallyAvailable &&
            TryResolveFreshTarget(
                localPlayer!,
                context,
                wolvesDenStrikingDummyMetadataVerified,
                out var freshTarget))
        {
            frozenIntent = new FrozenIntent(
                clientState.TerritoryType,
                context,
                localIdentity,
                freshTarget.Identity,
                freshTarget.EnemySlot,
                freshTarget.WolvesDenKind,
                NextToken(),
                SaturatingAdd(nowMilliseconds, IntentLeaseMilliseconds),
                HeldActionRetryState.Initial);
        }

        var currentIntent = frozenIntent;
        var runtimeTarget = currentIntent is { } intent &&
                            TryResolveFrozenTarget(
                                localPlayer!,
                                intent,
                                wolvesDenStrikingDummyMetadataVerified,
                                out var exactTarget)
            ? exactTarget
            : default;
        if (currentIntent is not null && !runtimeTarget.IsValid)
        {
            frozenIntent = null;
            currentIntent = null;
        }

        var castActionId = actionManager == null ? 0 : actionManager->CastActionId;
        var adjustedCastActionId = castActionId == 0 || actionManager == null
            ? 0
            : actionManager->GetAdjustedActionId(castActionId);
        var basicShotVerified = basicShotMetadata.IsVerified(
            localJobId,
            castActionId);
        var nativeBoundaryReady = actionManager != null &&
                                  localPlayer is not null &&
                                  HeldActionRetryRules.IsNativeBoundaryNearQueueable(
                                      actionManager->AnimationLock,
                                      localPlayer.IsCasting,
                                      castActionId,
                                      actionManager->ActionQueued);
        var decision = BardRepellingShotRules.Evaluate(
            new BardRepellingShotObservation(
                enabled,
                supportedContext,
                localIdentity,
                localJobId,
                localValid && localPlayer!.IsTargetable,
                actionMetadataVerified,
                GuardStateKnown: localValid,
                GuardActive: ownGuardActiveOrPropagating,
                TextInputStateKnown: textInputKnown,
                TextInputActive: textInputActive,
                higherPriorityClaimed,
                currentIntent?.Target ?? default,
                TargetResolvedExactly: runtimeTarget.IsValid,
                TargetAliveAndTargetable: runtimeTarget.IsValid,
                TargetInNativeRangeAndLineOfSight:
                    runtimeTarget.InNativeRangeAndLineOfSight,
                resolvedActionId,
                actionOffCooldown,
                actionResourcesReady,
                LocalPlayerIsCasting: localPlayer?.IsCasting == true,
                castActionId,
                adjustedCastActionId,
                basicShotVerified,
                nativeBoundaryReady));

        var inputClaimed = false;
        HeldCastCancellationRequest? castCancellationRequest = null;
        var attempted = false;
        var accepted = false;
        var nativeOutcome = ClientActionAttemptOutcome.None;
        if (decision.ShouldCancelCast && currentIntent is { } cancelIntent)
        {
            inputClaimed = true;
            castCancellationRequest = new HeldCastCancellationRequest(
                HeldCastCancellationHelperKind.BardRepellingShot,
                BardRepellingShotRules.RepellingShotActionId,
                cancelIntent.LocalPlayer,
                cancelIntent.Target,
                FrozenKeyCode: 0,
                cancelIntent.IntentEpochToken);
        }
        else if (decision.ShouldDispatch &&
                 currentIntent is { } dispatchIntent &&
                 runtimeTarget.IsValid &&
                 HeldActionRetryRules.CanAttemptFrozenIntent(
                     dispatchIntent.Retry,
                     nowMilliseconds))
        {
            inputClaimed = true;
            nativeOutcome = TryUseOnce(
                localPlayer!,
                dispatchIntent,
                runtimeTarget,
                actionMetadataVerified,
                wolvesDenStrikingDummyMetadataVerified,
                nowMilliseconds,
                out attempted);
            if (attempted) attemptCount++;
            accepted = nativeOutcome == ClientActionAttemptOutcome.ClientAccepted;
            if (accepted) acceptedCount++;

            var completion = HeldActionRetryRules.Complete(
                dispatchIntent.Retry,
                nowMilliseconds,
                nativeOutcome);
            if (completion.RetryScheduled ||
                completion.Disposition == HeldActionRetryDisposition.SoftWait)
            {
                frozenIntent = dispatchIntent with { Retry = completion.NextState };
            }
            else
            {
                frozenIntent = null;
                readyEpochSpent = true;
            }
        }

        var result = new BardRepellingShotProbeSnapshot(
            enabled,
            context,
            actionMetadataVerified,
            decision.Kind,
            decision.Reason,
            currentIntent?.EnemySlot ?? 0,
            currentIntent?.Target.GameObjectId ?? 0,
            currentIntent?.Target.EntityId ?? 0,
            runtimeTarget.InNativeRangeAndLineOfSight,
            decision.ShouldCancelCast,
            inputClaimed,
            castCancellationRequest,
            attempted,
            accepted,
            nativeOutcome,
            attemptCount,
            acceptedCount,
            Describe(decision, nativeOutcome));
        Volatile.Write(ref snapshot, result);
        return result;
    }

    private ClientActionAttemptOutcome TryUseOnce(
        IPlayerCharacter expectedLocalPlayer,
        FrozenIntent intent,
        RuntimeTarget expectedTarget,
        bool actionMetadataVerified,
        bool wolvesDenStrikingDummyMetadataVerified,
        long nowMilliseconds,
        out bool attempted)
    {
        attempted = false;
        if (!actionMetadataVerified ||
            clientState.TerritoryType != intent.TerritoryId ||
            !TryGetLiveIdentity(objectTable.LocalPlayer, out var localIdentity) ||
            localIdentity != intent.LocalPlayer ||
            expectedLocalPlayer.GameObjectId != localIdentity.GameObjectId ||
            nearAssist.IsExactLocalGuardActiveOrPropagating(localIdentity) ||
            !TryResolveFrozenTarget(
                objectTable.LocalPlayer!,
                intent,
                wolvesDenStrikingDummyMetadataVerified,
                out var finalTarget) ||
            finalTarget.Identity != expectedTarget.Identity ||
            !finalTarget.InNativeRangeAndLineOfSight ||
            !TryGetTextInputState(out var textInputActive) ||
            textInputActive)
        {
            return ClientActionAttemptOutcome.SoftUnavailable;
        }

        var actionManager = ActionManager.Instance();
        if (actionManager == null ||
            actionManager->GetAdjustedActionId(
                BardRepellingShotRules.RepellingShotActionId) !=
            BardRepellingShotRules.RepellingShotActionId ||
            !ClientActionAttemptBoundary.IsExactActionReady(
                actionManager,
                BardRepellingShotRules.RepellingShotActionId) ||
            actionManager->GetActionStatus(
                ActionType.Action,
                BardRepellingShotRules.RepellingShotActionId,
                intent.Target.GameObjectId,
                checkRecastActive: true,
                checkCastingActive: true) != 0 ||
            !HeldActionRetryRules.IsNativeBoundaryNearQueueable(
                actionManager->AnimationLock,
                objectTable.LocalPlayer!.IsCasting,
                actionManager->CastActionId,
                actionManager->ActionQueued))
        {
            return ClientActionAttemptOutcome.SoftUnavailable;
        }

        var before = ClientActionAttemptBoundary.Capture(
            actionManager,
            BardRepellingShotRules.RepellingShotActionId);
        Exception? nativeException = null;
        var invocation = nearAssist.RunExactAutomaticActionWithoutRedirect(
            new ExactAutomaticActionBoundaryIntent(
                ActionType.Action,
                BardRepellingShotRules.RepellingShotActionId,
                intent.Target.GameObjectId,
                ActionManager.UseActionMode.None),
            () =>
            {
                try
                {
                    if (nearAssist.IsExactLocalGuardActiveOrPropagating(
                            intent.LocalPlayer) ||
                        !TryGetTextInputState(out var finalTextInputActive) ||
                        finalTextInputActive)
                    {
                        return false;
                    }

                    return actionManager->UseAction(
                        ActionType.Action,
                        BardRepellingShotRules.RepellingShotActionId,
                        intent.Target.GameObjectId,
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
            LogFailure(nativeException, nowMilliseconds);
            return attempted
                ? ClientActionAttemptOutcome.AcceptanceUnknown
                : ClientActionAttemptOutcome.SoftUnavailable;
        }
        if (!attempted) return ClientActionAttemptOutcome.SoftUnavailable;

        return ClientActionAttemptBoundaryRules.Classify(
            invocation.ClientReturnedAccepted,
            BardRepellingShotRules.RepellingShotActionId,
            before,
            ClientActionAttemptBoundary.Capture(
                actionManager,
                BardRepellingShotRules.RepellingShotActionId));
    }

    private bool TryResolveFreshTarget(
        IPlayerCharacter localPlayer,
        SupportedPvPContext context,
        bool wolvesDenStrikingDummyMetadataVerified,
        out RuntimeTarget target)
    {
        target = default;
        if (context == SupportedPvPContext.CrystallineConflict)
        {
            if (!nearAssist.TryResolveHeldSmartActionTarget(
                    BardRepellingShotRules.RepellingShotActionId,
                    out var enemySlot,
                    out var identity,
                    out _,
                    out _) ||
                !TryResolveExactCcTarget(enemySlot, identity, out var player))
            {
                return false;
            }

            target = BuildRuntimeTarget(
                localPlayer,
                player,
                identity,
                enemySlot,
                DarkKnightWolvesDenTargetKind.None);
            return target.IsValid && target.InNativeRangeAndLineOfSight;
        }

        if (context != SupportedPvPContext.WolvesDen ||
            !DarkKnightWolvesDenCurrentTargetResolver.TryResolveExactCurrentHardTarget(
                objectTable,
                wolvesDenStrikingDummyMetadataVerified,
                localPlayer,
                out var denTarget,
                out var denIdentity,
                out var denKind,
                out _))
        {
            return false;
        }

        target = BuildRuntimeTarget(localPlayer, denTarget!, denIdentity, 0, denKind);
        return target.IsValid && target.InNativeRangeAndLineOfSight;
    }

    private bool TryResolveFrozenTarget(
        IPlayerCharacter localPlayer,
        FrozenIntent intent,
        bool wolvesDenStrikingDummyMetadataVerified,
        out RuntimeTarget target)
    {
        target = default;
        if (intent.Context == SupportedPvPContext.CrystallineConflict)
        {
            if (!nearAssist.CanUseExactHeldSmartActionTarget(
                    BardRepellingShotRules.RepellingShotActionId,
                    intent.EnemySlot,
                    intent.Target) ||
                !TryResolveExactCcTarget(
                    intent.EnemySlot,
                    intent.Target,
                    out var player))
            {
                return false;
            }

            target = BuildRuntimeTarget(
                localPlayer,
                player,
                intent.Target,
                intent.EnemySlot,
                DarkKnightWolvesDenTargetKind.None);
            return target.IsValid;
        }

        if (intent.Context != SupportedPvPContext.WolvesDen ||
            !DarkKnightWolvesDenCurrentTargetResolver.TryResolveFrozenCurrentHardTarget(
                objectTable,
                wolvesDenStrikingDummyMetadataVerified,
                localPlayer,
                intent.Target,
                intent.WolvesDenKind,
                out var denTarget))
        {
            return false;
        }

        target = BuildRuntimeTarget(
            localPlayer,
            denTarget!,
            intent.Target,
            0,
            intent.WolvesDenKind);
        return target.IsValid;
    }

    private bool TryResolveExactCcTarget(
        int enemySlot,
        TargetPressureActorIdentity expected,
        out IPlayerCharacter player)
    {
        player = null!;
        if (!EnemySlotRules.IsValidSlot(enemySlot) || !expected.IsValid)
            return false;

        var candidate = EnemySlotResolver.Resolve(objectTable, enemySlot);
        var byEntity = objectTable.SearchByEntityId(expected.EntityId)
            as IPlayerCharacter;
        if (!TryGetLiveIdentity(candidate, out var candidateIdentity) ||
            candidateIdentity != expected ||
            !TryGetLiveIdentity(byEntity, out var tableIdentity) ||
            tableIdentity != expected ||
            candidate!.Address != byEntity!.Address)
        {
            return false;
        }

        player = candidate;
        return true;
    }

    private static RuntimeTarget BuildRuntimeTarget(
        IPlayerCharacter localPlayer,
        IBattleChara target,
        TargetPressureActorIdentity identity,
        int enemySlot,
        DarkKnightWolvesDenTargetKind wolvesDenKind)
    {
        if (!identity.IsValid ||
            target.GameObjectId != identity.GameObjectId ||
            target.EntityId != identity.EntityId ||
            target.Address == nint.Zero ||
            target.IsDead ||
            target.CurrentHp == 0 ||
            target.MaxHp < target.CurrentHp ||
            !target.IsTargetable)
        {
            return default;
        }

        var sourceObject = GetNativeObject(localPlayer);
        var targetObject = GetNativeObject(target);
        var inRangeAndLos = sourceObject != null &&
                            targetObject != null &&
                            SeitonRangeRules.HasNativeRangeAndLineOfSight(
                                ActionManager.GetActionInRangeOrLoS(
                                    BardRepellingShotRules.RepellingShotActionId,
                                    sourceObject,
                                    targetObject));
        return new RuntimeTarget(
            identity,
            enemySlot,
            wolvesDenKind,
            inRangeAndLos);
    }

    private bool IsFrozenContextValid(
        FrozenIntent intent,
        TargetPressureActorIdentity localIdentity,
        SupportedPvPContext context,
        long nowMilliseconds) =>
        intent.TerritoryId == clientState.TerritoryType &&
        intent.Context == context &&
        intent.LocalPlayer == localIdentity &&
        nowMilliseconds >= 0 &&
        nowMilliseconds <= intent.ExpiresAtMilliseconds;

    private static bool TryGetLiveIdentity(
        IPlayerCharacter? player,
        out TargetPressureActorIdentity identity)
    {
        identity = default;
        if (player is null ||
            player.Address == nint.Zero ||
            player.IsDead ||
            player.CurrentHp == 0 ||
            player.MaxHp < player.CurrentHp ||
            player.GameObjectId is 0 or 0xE0000000 or ulong.MaxValue ||
            player.EntityId is 0 or 0xE0000000 or uint.MaxValue)
        {
            return false;
        }

        identity = new TargetPressureActorIdentity(
            player.GameObjectId,
            player.EntityId);
        return identity.IsValid;
    }

    private static GameObject* GetNativeObject(IGameObject? gameObject) =>
        gameObject?.Address is { } address && address != nint.Zero
            ? (GameObject*)address
            : null;

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

    private void ResetRuntime()
    {
        frozenIntent = null;
        readyEpochSpent = false;
    }

    private ulong NextToken()
    {
        nextIntentEpoch = nextIntentEpoch == ulong.MaxValue
            ? 1
            : nextIntentEpoch + 1;
        return nextIntentEpoch;
    }

    private static long SaturatingAdd(long left, long right) =>
        left > long.MaxValue - right ? long.MaxValue : left + right;

    private static string Describe(
        BardRepellingShotDecision decision,
        ClientActionAttemptOutcome outcome) =>
        outcome != ClientActionAttemptOutcome.None
            ? $"Mannstopper: {outcome}"
            : decision.Kind switch
            {
                BardRepellingShotDecisionKind.CancelPowerfulShot =>
                    "Mannstopper reserved; cancelling Powerful Shot",
                BardRepellingShotDecisionKind.Dispatch =>
                    "Mannstopper ready",
                _ => $"Mannstopper waiting: {decision.Reason}",
            };

    private void LogFailure(Exception exception, long nowMilliseconds)
    {
        if (nowMilliseconds < nextErrorLogAt) return;
        nextErrorLogAt = SaturatingAdd(nowMilliseconds, 10_000);
        log.Warning(exception, "Seiton Sense BRD Mannstopper helper failed closed.");
    }

    private readonly record struct FrozenIntent(
        uint TerritoryId,
        SupportedPvPContext Context,
        TargetPressureActorIdentity LocalPlayer,
        TargetPressureActorIdentity Target,
        int EnemySlot,
        DarkKnightWolvesDenTargetKind WolvesDenKind,
        ulong IntentEpochToken,
        long ExpiresAtMilliseconds,
        HeldActionRetryState Retry);

    private readonly record struct RuntimeTarget(
        TargetPressureActorIdentity Identity,
        int EnemySlot,
        DarkKnightWolvesDenTargetKind WolvesDenKind,
        bool InNativeRangeAndLineOfSight)
    {
        internal bool IsValid => Identity.IsValid;
    }
}
