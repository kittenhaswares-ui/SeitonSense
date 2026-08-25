using System.Numerics;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.DutyState;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using SeitonSense.Core;
using SeitonSense.Plugin.Models;
using DalamudBattleChara = Dalamud.Game.ClientState.Objects.Types.IBattleChara;
using DalamudObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;

namespace SeitonSense.Plugin.Services;

/// <summary>
/// One bounded local-Scholar action packet captured by the plugin's existing
/// shared ActionEffect hook. Single-target setup actions use their exact effect
/// recipient; Deployment uses the header animation target because area-action
/// effect recipients may be ordered independently.
/// </summary>
internal readonly record struct ScholarSpreadCapturedActionEffect(
    long ObservedAtMilliseconds,
    uint CasterEntityId,
    uint PrimaryTargetEntityId,
    uint ActionId,
    int FeatureGeneration,
    uint GlobalSequence,
    ushort SourceSequence);

/// <summary>
/// Compile-time boundary implemented by MachinistLimitBreakCapture so Scholar
/// can reuse the one existing native ActionEffect hook instead of installing a
/// second detour.
/// </summary>
internal interface IScholarSpreadActionEffectCapture
{
    bool IsRunning { get; }
    uint CurrentScholarSpreadLocalEntityId { get; }
    int CurrentScholarSpreadGeneration { get; }
    int ScholarSpreadQueueDepth { get; }
    long CapturedScholarSpreadEffects { get; }
    long DroppedScholarSpreadEffects { get; }
    long ScholarSpreadCaptureErrors { get; }

    void SetScholarSpreadLocalEntityId(uint entityId);
    bool TryDequeueScholarSpreadEffect(out ScholarSpreadCapturedActionEffect effect);
    void ClearScholarSpreadEffects();
}

internal sealed record ScholarSpreadProbeSnapshot(
    ScholarSpreadPhase Phase,
    ScholarSpreadKind Kind,
    ScholarSpreadPlanDecisionReason PlanReason,
    ScholarSpreadIntentDecisionReason IntentReason,
    ScholarSpreadEffectDecisionReason EffectReason,
    bool CaptureRunning,
    bool DutyStartedRaw,
    bool MatchStartedLatched,
    bool MatchCompletedLatched,
    bool InputProbeSucceeded,
    bool RawHeldGameplayKeyEligible,
    bool SharedInputFrameWasConsumed,
    VirtualKey HeldGameplayKey,
    bool TerminalUntilRelease,
    uint NextActionId,
    int DeploymentCharges,
    long DeploymentNextChargeRemainingMilliseconds,
    long BiolysisRemainingMilliseconds,
    bool NativeStateKnown,
    bool NativeBoundaryClear,
    int DotCandidateCount,
    int ShieldCandidateCount,
    int TargetSlot,
    ulong TargetGameObjectId,
    uint TargetEntityId,
    int PredictedAffectedCount,
    int CurrentAffectedCount,
    bool TacticalCrystalResolved,
    Vector3 TacticalCrystalPosition,
    float TacticalCrystalPriorityRadiusYalms,
    bool UseActionAttempted,
    ClientActionAttemptOutcome NativeOutcome,
    ushort PendingSourceSequence,
    long AttemptCount,
    long AcceptedCount,
    long SetupConfirmationCount,
    long DeploymentConfirmationCount,
    long ManualConflictCount,
    long CaptureCount,
    long CaptureDropCount,
    int CaptureQueueDepth,
    string LastEvent)
{
    internal bool InputClaimed => false;
    internal HeldCastCancellationRequest? CastCancellationRequest => null;

    internal static ScholarSpreadProbeSnapshot Initial { get; } = new(
        ScholarSpreadPhase.Idle,
        ScholarSpreadKind.None,
        ScholarSpreadPlanDecisionReason.None,
        ScholarSpreadIntentDecisionReason.None,
        ScholarSpreadEffectDecisionReason.None,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        VirtualKey.NO_KEY,
        false,
        0,
        0,
        -1,
        -1,
        false,
        false,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        false,
        default,
        ScholarSpreadProbe.TacticalCrystalPriorityRadiusYalms,
        false,
        ClientActionAttemptOutcome.None,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        "Waiting");
}

/// <summary>
/// Independent held-input Scholar lane. It reads only the immutable raw input
/// snapshot, never consumes/claims the shared emergency frame, never requests
/// cast cancellation, and crosses at most one native action boundary per call.
/// Biolysis spread is always planned before a safely-reserved Adloquium spread.
/// </summary>
internal sealed class ScholarSpreadProbe : IDisposable
{
    internal const float TacticalCrystalPriorityRadiusYalms = 5f;

    private const float DeploymentRadiusYalms = 15f;
    private const long OwnedEffectTimeoutMilliseconds = 2_500;
    private const long StatusPropagationTimeoutMilliseconds = 2_500;

    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly IDutyState dutyState;
    private readonly PluginConfiguration configuration;
    private readonly NearAssistRedirector nearAssist;
    private readonly IScholarSpreadActionEffectCapture actionEffectCapture;
    private readonly IPluginLog log;

    private ScholarSpreadHeldConsentState consentState = ScholarSpreadHeldConsentState.Initial;
    private ScholarSpreadWorkflowState workflowState = ScholarSpreadWorkflowState.Initial;
    private HeldActionRetryState retryState = HeldActionRetryState.Initial;
    private ScholarSpreadProbeSnapshot snapshot = ScholarSpreadProbeSnapshot.Initial;
    private long pendingAcceptedAtMilliseconds = -1;
    private long setupConfirmedAtMilliseconds = -1;
    private ulong nextEpisodeToken = 1;
    private bool terminalUntilRelease;
    private long observedCaptureDropCount;
    private long observedCaptureErrorCount;
    private long attemptCount;
    private long acceptedCount;
    private long setupConfirmationCount;
    private long deploymentConfirmationCount;
    private long manualConflictCount;
    private long nextErrorLogAt;
    private ScholarSpreadMatchGateState matchGateState = ScholarSpreadMatchGateState.Initial;
    private int signaledMatchStartTerritory;
    private int signaledMatchCompletionTerritory;
    private bool disposed;

    internal ScholarSpreadProbe(
        IClientState clientState,
        IObjectTable objectTable,
        IDutyState dutyState,
        PluginConfiguration configuration,
        NearAssistRedirector nearAssist,
        IScholarSpreadActionEffectCapture actionEffectCapture,
        IPluginLog log)
    {
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.dutyState = dutyState;
        this.configuration = configuration;
        this.nearAssist = nearAssist;
        this.actionEffectCapture = actionEffectCapture;
        this.log = log;
        observedCaptureDropCount = actionEffectCapture.DroppedScholarSpreadEffects;
        observedCaptureErrorCount = actionEffectCapture.ScholarSpreadCaptureErrors;
        dutyState.DutyStarted += OnDutyStarted;
        dutyState.DutyRecommenced += OnDutyRecommenced;
        dutyState.DutyCompleted += OnDutyCompleted;
    }

    internal ScholarSpreadProbeSnapshot Snapshot => Volatile.Read(ref snapshot);

    internal unsafe ScholarSpreadProbeSnapshot Observe(
        IPlayerCharacter? localPlayer,
        bool isCrystallineConflict,
        bool configurationEnabled,
        bool metadataVerified,
        bool actionHelpersSuppressedByGuard,
        EmergencyActionInputFrame inputFrame,
        long nowMilliseconds,
        bool hardReset = false)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var now = NormalizeNow(nowMilliseconds);
        var input = inputFrame.Snapshot;
        if (hardReset) ResetRuntime(resetConsent: true, clearCapture: true);

        var consent = ScholarSpreadRules.ObserveIndependentHeldConsent(
            consentState,
            configurationEnabled,
            input.ProbeSucceeded &&
            !input.IsTextInputActive &&
            input.HeldGameplayKeyEligible);
        consentState = consent.NextState;
        if (!input.HeldGameplayKeyEligible) terminalUntilRelease = false;

        var localAlive = IsLivePlayer(localPlayer);
        var localIdentity = HasValidNativeIdentity(localPlayer)
            ? new TargetPressureActorIdentity(localPlayer!.GameObjectId, localPlayer.EntityId)
            : default;
        var localJobId = localPlayer?.ClassJob.IsValid == true
            ? localPlayer.ClassJob.RowId
            : 0;
        var liveContextValid = isCrystallineConflict && IsCurrentCrystallineConflict();
        var dutyStartedRaw = liveContextValid && dutyState.IsDutyStarted;
        var matchStarted = ObserveMatchStartGate(
            liveContextValid,
            dutyStartedRaw,
            hardReset);
        var featureContextReady = configurationEnabled &&
                                  metadataVerified &&
                                  liveContextValid &&
                                  matchStarted &&
                                  localAlive &&
                                  localJobId == ScholarSpreadRules.ScholarJobId &&
                                  localIdentity.IsValid &&
                                  !actionHelpersSuppressedByGuard &&
                                  actionEffectCapture.IsRunning &&
                                  !hardReset;

        actionEffectCapture.SetScholarSpreadLocalEntityId(
            featureContextReady ? localPlayer!.EntityId : 0);

        if (!featureContextReady)
        {
            if (workflowState.IsActive)
            {
                workflowState = ScholarSpreadRules.Cancel(workflowState);
                terminalUntilRelease = input.HeldGameplayKeyEligible;
            }

            ClearActionEpisode();
            actionEffectCapture.ClearScholarSpreadEffects();
        }

        var nativeState = featureContextReady
            ? ObserveNativeState(localPlayer!)
            : ScholarSpreadNativeState.Unknown;
        var shieldReservationSafe = nativeState.Known &&
                                    ScholarSpreadRules.CanSpendDeploymentOnShield(
                                        nativeState.DeploymentCharges,
                                        nativeState.DeploymentTimingKnown,
                                        nativeState.DeploymentRemainingMilliseconds,
                                        nativeState.BiolysisTimingKnown,
                                        nativeState.BiolysisRemainingMilliseconds);

        var effectReason = ScholarSpreadEffectDecisionReason.None;
        if (IsOwnedEffectTimedOut(now))
        {
            workflowState = ScholarSpreadRules.Cancel(workflowState);
            ClearActionEpisode();
            terminalUntilRelease = input.HeldGameplayKeyEligible;
            effectReason = ScholarSpreadEffectDecisionReason.OwnedEffectMalformed;
        }
        else
        {
            effectReason = DrainActionEffects(
                now,
                featureContextReady,
                shieldReservationSafe,
                input.HeldGameplayKeyEligible);

            // ActionEffect is useful attribution evidence, but it is not the only
            // exact proof available. Some clients expose the locally sourced status
            // pair before (or without) a usable target/source sequence in the packet.
            // Only a setup already accepted for this frozen target may use this path.
            if (featureContextReady &&
                workflowState.Phase == ScholarSpreadPhase.AwaitingSetupEffect &&
                localPlayer is not null &&
                HasExactOwnPairOnFrozenTarget(workflowState.Plan, localPlayer.EntityId))
            {
                var statusDecision = ScholarSpreadRules.ConfirmPendingSetupFromExactStatusPair(
                    workflowState,
                    workflowState.Plan.Target,
                    expectedOwnStatusPairActive: true,
                    shieldReservationSafe);
                workflowState = statusDecision.NextState;
                if (statusDecision.Kind == ScholarSpreadEffectDecisionKind.OwnedSetupConfirmed)
                {
                    Interlocked.Increment(ref setupConfirmationCount);
                    pendingAcceptedAtMilliseconds = -1;
                    setupConfirmedAtMilliseconds = now;
                    retryState = HeldActionRetryState.Initial;
                    effectReason = statusDecision.Reason;
                }
            }
        }

        var captureBecameUnreliable = ObserveCaptureReliability();
        if (captureBecameUnreliable && workflowState.IsActive)
        {
            workflowState = ScholarSpreadRules.Cancel(workflowState);
            ClearActionEpisode();
            terminalUntilRelease = input.HeldGameplayKeyEligible;
            effectReason = ScholarSpreadEffectDecisionReason.OwnedEffectMalformed;
        }

        var terminalThisFrame = workflowState.Phase is
            ScholarSpreadPhase.Completed or ScholarSpreadPhase.Cancelled;
        if (workflowState.Phase is ScholarSpreadPhase.Completed or ScholarSpreadPhase.Cancelled)
        {
            terminalUntilRelease = input.HeldGameplayKeyEligible;
            workflowState = ScholarSpreadWorkflowState.Initial;
            ClearActionEpisode();
        }

        var dotRuntime = Array.Empty<ScholarDotRuntimeCandidate>();
        var shieldRuntime = Array.Empty<ScholarShieldRuntimeCandidate>();
        var tacticalCrystalResolved = false;
        var tacticalCrystalPosition = Vector3.Zero;
        var planReason = ScholarSpreadPlanDecisionReason.None;
        var intentReason = ScholarSpreadIntentDecisionReason.None;
        var attempted = false;
        var nativeOutcome = ClientActionAttemptOutcome.None;
        var currentAffectedCount = 0;

        if (featureContextReady &&
            consent.AllowsWorkflow &&
            !terminalUntilRelease &&
            !terminalThisFrame &&
            nativeState.Known)
        {
            ResolveRuntimeCandidates(
                localPlayer!,
                metadataVerified,
                out dotRuntime,
                out shieldRuntime,
                out tacticalCrystalResolved,
                out tacticalCrystalPosition);

            if (!workflowState.IsActive)
            {
                var planning = new ScholarSpreadPlanningObservation(
                    configurationEnabled,
                    liveContextValid,
                    matchStarted,
                    localJobId,
                    localIdentity,
                    localAlive,
                    metadataVerified,
                    actionHelpersSuppressedByGuard,
                    input.ProbeSucceeded,
                    input.IsTextInputActive,
                    consent.AllowsWorkflow,
                    input.HeldGameplayKey == VirtualKey.NO_KEY
                        ? 0
                        : (int)input.HeldGameplayKey,
                    nativeState.BiolysisReady,
                    nativeState.AdloquiumReady,
                    nativeState.DeploymentCharges,
                    nativeState.DeploymentTimingKnown,
                    nativeState.DeploymentRemainingMilliseconds,
                    nativeState.BiolysisTimingKnown,
                    nativeState.BiolysisRemainingMilliseconds,
                    dotRuntime.Select(static item => item.Candidate).ToArray(),
                    shieldRuntime.Select(static item => item.Candidate).ToArray(),
                    hardReset);
                var plan = ScholarSpreadRules.PlanNextSequence(
                    planning,
                    NextEpisodeToken());
                planReason = plan.Reason;
                if (plan.HasPlan)
                    workflowState = ScholarSpreadRules.BeginWorkflow(plan.Plan!.Value);
            }

            if (workflowState.IsActive &&
                ScholarSpreadRules.TryGetNextIntent(workflowState, out var intent))
            {
                var statusPropagationPending =
                    workflowState.Phase == ScholarSpreadPhase.DeploymentReady &&
                    setupConfirmedAtMilliseconds >= 0 &&
                    now >= setupConfirmedAtMilliseconds &&
                    now - setupConfirmedAtMilliseconds <= StatusPropagationTimeoutMilliseconds &&
                    !HasExactOwnPairOnFrozenTarget(workflowState.Plan, localPlayer!.EntityId);

                if (!statusPropagationPending &&
                    TryBuildIntentObservation(
                        localPlayer!,
                        intent,
                        nativeState,
                        shieldReservationSafe,
                        dotRuntime,
                        shieldRuntime,
                        tacticalCrystalResolved,
                        tacticalCrystalPosition,
                        out var intentObservation))
                {
                    currentAffectedCount =
                        intentObservation.ExactTarget.CurrentAffectedCount;
                    var intentDecision = ScholarSpreadRules.EvaluateExactIntent(
                        workflowState,
                        intent,
                        intentObservation);
                    intentReason = intentDecision.Reason;
                    if (intentDecision.CanDispatch &&
                        HeldActionRetryRules.CanAttemptFrozenIntent(retryState, now))
                    {
                        nativeOutcome = TryUseExactIntentOnce(
                            localPlayer!,
                            intent,
                            out attempted,
                            out var sourceSequence);
                        HandleNativeOutcome(
                            intent,
                            nativeOutcome,
                            sourceSequence,
                            now,
                            input.HeldGameplayKeyEligible);
                    }
                    else if (intentDecision.Kind == ScholarSpreadIntentDecisionKind.Cancelled)
                    {
                        workflowState = ScholarSpreadRules.Cancel(workflowState);
                        ClearActionEpisode();
                        terminalUntilRelease = input.HeldGameplayKeyEligible;
                    }
                }
                else if (!statusPropagationPending)
                {
                    workflowState = ScholarSpreadRules.Cancel(workflowState);
                    ClearActionEpisode();
                    terminalUntilRelease = input.HeldGameplayKeyEligible;
                    intentReason = ScholarSpreadIntentDecisionReason.TargetIdentityDrift;
                }
            }
        }
        else if (workflowState.IsActive &&
                 (!consent.AllowsWorkflow || !featureContextReady))
        {
            workflowState = ScholarSpreadRules.Cancel(workflowState);
            ClearActionEpisode();
            terminalUntilRelease = input.HeldGameplayKeyEligible;
        }

        if (attempted) Interlocked.Increment(ref attemptCount);
        var nextActionId = ScholarSpreadRules.TryGetNextIntent(
            workflowState,
            out var nextIntent)
            ? nextIntent.ActionId
            : 0;
        var result = new ScholarSpreadProbeSnapshot(
            workflowState.Phase,
            workflowState.Plan.Kind,
            planReason,
            intentReason,
            effectReason,
            actionEffectCapture.IsRunning,
            dutyStartedRaw,
            matchGateState.MatchStarted,
            matchGateState.MatchCompleted,
            input.ProbeSucceeded,
            input.HeldGameplayKeyEligible,
            inputFrame.IsConsumed,
            input.HeldGameplayKey,
            terminalUntilRelease,
            nextActionId,
            nativeState.DeploymentCharges,
            nativeState.DeploymentRemainingMilliseconds,
            nativeState.BiolysisRemainingMilliseconds,
            nativeState.Known,
            nativeState.NativeBoundaryClear,
            dotRuntime.Length,
            shieldRuntime.Length,
            workflowState.Plan.TargetSlot,
            workflowState.Plan.Target.GameObjectId,
            workflowState.Plan.Target.EntityId,
            workflowState.Plan.PredictedAffectedCount,
            currentAffectedCount,
            tacticalCrystalResolved,
            tacticalCrystalPosition,
            TacticalCrystalPriorityRadiusYalms,
            attempted,
            nativeOutcome,
            workflowState.PendingOwnedAction.SourceSequence,
            Interlocked.Read(ref attemptCount),
            Interlocked.Read(ref acceptedCount),
            Interlocked.Read(ref setupConfirmationCount),
            Interlocked.Read(ref deploymentConfirmationCount),
            Interlocked.Read(ref manualConflictCount),
            actionEffectCapture.CapturedScholarSpreadEffects,
            actionEffectCapture.DroppedScholarSpreadEffects,
            actionEffectCapture.ScholarSpreadQueueDepth,
            Describe(
                liveContextValid,
                matchStarted,
                featureContextReady,
                statusPropagationPending: setupConfirmedAtMilliseconds >= 0 &&
                                          workflowState.Phase == ScholarSpreadPhase.DeploymentReady &&
                                          !HasExactOwnPairOnFrozenTarget(
                                              workflowState.Plan,
                                              localPlayer?.EntityId ?? 0),
                attempted,
                nativeOutcome,
                planReason,
                intentReason,
                effectReason));
        Volatile.Write(ref snapshot, result);
        return result;
    }

    internal void Reset()
    {
        ResetRuntime(resetConsent: true, clearCapture: true);
        Volatile.Write(ref snapshot, ScholarSpreadProbeSnapshot.Initial with
        {
            CaptureRunning = actionEffectCapture.IsRunning,
            AttemptCount = Interlocked.Read(ref attemptCount),
            AcceptedCount = Interlocked.Read(ref acceptedCount),
            SetupConfirmationCount = Interlocked.Read(ref setupConfirmationCount),
            DeploymentConfirmationCount = Interlocked.Read(ref deploymentConfirmationCount),
            ManualConflictCount = Interlocked.Read(ref manualConflictCount),
            CaptureCount = actionEffectCapture.CapturedScholarSpreadEffects,
            CaptureDropCount = actionEffectCapture.DroppedScholarSpreadEffects,
            CaptureQueueDepth = actionEffectCapture.ScholarSpreadQueueDepth,
            LastEvent = "Reset",
        });
    }

    internal ScholarSpreadProbeSnapshot FailClosed()
    {
        ResetRuntime(resetConsent: true, clearCapture: true);
        var result = ScholarSpreadProbeSnapshot.Initial with
        {
            Phase = ScholarSpreadPhase.Cancelled,
            CaptureRunning = actionEffectCapture.IsRunning,
            AttemptCount = Interlocked.Read(ref attemptCount),
            AcceptedCount = Interlocked.Read(ref acceptedCount),
            SetupConfirmationCount = Interlocked.Read(ref setupConfirmationCount),
            DeploymentConfirmationCount = Interlocked.Read(ref deploymentConfirmationCount),
            ManualConflictCount = Interlocked.Read(ref manualConflictCount),
            CaptureCount = actionEffectCapture.CapturedScholarSpreadEffects,
            CaptureDropCount = actionEffectCapture.DroppedScholarSpreadEffects,
            CaptureQueueDepth = actionEffectCapture.ScholarSpreadQueueDepth,
            LastEvent = "Failed closed",
        };
        Volatile.Write(ref snapshot, result);
        return result;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        dutyState.DutyStarted -= OnDutyStarted;
        dutyState.DutyRecommenced -= OnDutyRecommenced;
        dutyState.DutyCompleted -= OnDutyCompleted;
        actionEffectCapture.SetScholarSpreadLocalEntityId(0);
        actionEffectCapture.ClearScholarSpreadEffects();
        ResetRuntime(resetConsent: true, clearCapture: false);
    }

    private ScholarSpreadEffectDecisionReason DrainActionEffects(
        long nowMilliseconds,
        bool featureContextReady,
        bool shieldReservationStillSafe,
        bool heldGameplayKeyEligible)
    {
        var lastReason = ScholarSpreadEffectDecisionReason.None;
        while (actionEffectCapture.TryDequeueScholarSpreadEffect(out var captured))
        {
            if (!featureContextReady ||
                captured.FeatureGeneration != actionEffectCapture.CurrentScholarSpreadGeneration ||
                captured.CasterEntityId != actionEffectCapture.CurrentScholarSpreadLocalEntityId ||
                captured.ObservedAtMilliseconds < 0 ||
                captured.ObservedAtMilliseconds > nowMilliseconds + 100)
            {
                continue;
            }

            if (workflowState.PendingOwnedAction.IsValid &&
                pendingAcceptedAtMilliseconds >= 0 &&
                captured.ObservedAtMilliseconds < pendingAcceptedAtMilliseconds)
            {
                continue;
            }

            var caster = workflowState.Plan.LocalPlayer;
            var primaryTarget = ResolveCapturedTargetIdentity(
                captured.PrimaryTargetEntityId,
                workflowState.Plan);
            var decision = ScholarSpreadRules.ObserveActionEffect(
                workflowState,
                new ScholarSpreadActionEffectObservation(
                    caster,
                    primaryTarget,
                    captured.ActionId,
                    captured.GlobalSequence,
                    captured.SourceSequence),
                shieldReservationStillSafe);
            workflowState = decision.NextState;
            lastReason = decision.Reason;
            switch (decision.Kind)
            {
                case ScholarSpreadEffectDecisionKind.OwnedSetupConfirmed:
                    Interlocked.Increment(ref setupConfirmationCount);
                    pendingAcceptedAtMilliseconds = -1;
                    setupConfirmedAtMilliseconds = nowMilliseconds;
                    retryState = HeldActionRetryState.Initial;
                    break;
                case ScholarSpreadEffectDecisionKind.OwnedDeploymentConfirmed:
                    Interlocked.Increment(ref deploymentConfirmationCount);
                    pendingAcceptedAtMilliseconds = -1;
                    setupConfirmedAtMilliseconds = -1;
                    retryState = HeldActionRetryState.Initial;
                    break;
                case ScholarSpreadEffectDecisionKind.Cancelled:
                    if (decision.Reason is
                        ScholarSpreadEffectDecisionReason.ManualDeploymentConflict or
                        ScholarSpreadEffectDecisionReason.ManualSetupTargetConflict)
                    {
                        Interlocked.Increment(ref manualConflictCount);
                    }

                    terminalUntilRelease = heldGameplayKeyEligible;
                    ClearActionEpisode();
                    break;
            }
        }

        return lastReason;
    }

    private bool ObserveCaptureReliability()
    {
        var drops = actionEffectCapture.DroppedScholarSpreadEffects;
        var errors = actionEffectCapture.ScholarSpreadCaptureErrors;
        var changed = drops != observedCaptureDropCount ||
                      errors != observedCaptureErrorCount;
        observedCaptureDropCount = drops;
        observedCaptureErrorCount = errors;
        return changed;
    }

    private bool IsOwnedEffectTimedOut(long nowMilliseconds) =>
        workflowState.PendingOwnedAction.IsValid &&
        pendingAcceptedAtMilliseconds >= 0 &&
        !ScholarSpreadRules.IsWithinOwnedConfirmationWindow(
            pendingAcceptedAtMilliseconds,
            nowMilliseconds,
            OwnedEffectTimeoutMilliseconds);

    private void HandleNativeOutcome(
        ScholarSpreadIntent intent,
        ClientActionAttemptOutcome outcome,
        ushort sourceSequence,
        long nowMilliseconds,
        bool heldGameplayKeyEligible)
    {
        var retry = HeldActionRetryRules.Complete(retryState, nowMilliseconds, outcome);
        retryState = retry.NextState;
        switch (outcome)
        {
            case ClientActionAttemptOutcome.NotInvoked:
                // The frozen exact intent failed its final live revalidation before
                // UseAction. Retire this episode instead of repeatedly trying an
                // actor/context that is no longer the exact one we planned for.
                workflowState = ScholarSpreadRules.Cancel(workflowState);
                terminalUntilRelease = heldGameplayKeyEligible;
                ClearActionEpisode();
                break;
            case ClientActionAttemptOutcome.ClientAccepted:
                workflowState = ScholarSpreadRules.RecordClientAcceptedAction(
                    workflowState,
                    intent,
                    sourceSequence);
                if (workflowState.Phase == ScholarSpreadPhase.Cancelled)
                {
                    terminalUntilRelease = heldGameplayKeyEligible;
                    ClearActionEpisode();
                    break;
                }

                pendingAcceptedAtMilliseconds = nowMilliseconds;
                setupConfirmedAtMilliseconds = -1;
                Interlocked.Increment(ref acceptedCount);
                retryState = HeldActionRetryState.Initial;
                break;
            case ClientActionAttemptOutcome.ClientRejected:
                if (retry.IsTerminal)
                {
                    workflowState = ScholarSpreadRules.Cancel(workflowState);
                    terminalUntilRelease = heldGameplayKeyEligible;
                    ClearActionEpisode();
                }
                break;
            case ClientActionAttemptOutcome.AcceptanceUnknown:
                workflowState = ScholarSpreadRules.Cancel(workflowState);
                terminalUntilRelease = heldGameplayKeyEligible;
                ClearActionEpisode();
                break;
        }
    }

    private unsafe ClientActionAttemptOutcome TryUseExactIntentOnce(
        IPlayerCharacter localPlayer,
        ScholarSpreadIntent intent,
        out bool attempted,
        out ushort sourceSequence)
    {
        attempted = false;
        sourceSequence = 0;
        var exactLocal = ResolveExactLocalPlayer(intent.LocalPlayer);
        if (!intent.IsValid ||
            !IsCurrentCrystallineConflict() ||
            !IsCurrentScholarMatchStarted() ||
            exactLocal is null ||
            exactLocal.Address != localPlayer.Address ||
            !IsExactLocalScholar(exactLocal, intent.LocalPlayer) ||
            IsCurrentlySuppressedByGuard(exactLocal, Environment.TickCount64) ||
            ResolveFrozenTarget(intent) is not { } exactTarget)
        {
            return ClientActionAttemptOutcome.NotInvoked;
        }

        var actionManager = ActionManager.Instance();
        if (actionManager == null ||
            actionManager->GetAdjustedActionId(intent.ActionId) != intent.ActionId ||
            !actionManager->IsActionOffCooldown(ActionType.Action, intent.ActionId) ||
            actionManager->CheckActionResources(ActionType.Action, intent.ActionId) != 0 ||
            actionManager->GetActionStatus(
                ActionType.Action,
                intent.ActionId,
                intent.Target.GameObjectId,
                checkRecastActive: true,
                checkCastingActive: true) != 0 ||
            !HeldActionRetryRules.IsNativeBoundaryNearQueueable(
                actionManager->AnimationLock,
                exactLocal.IsCasting,
                actionManager->CastActionId,
                actionManager->ActionQueued) ||
            !HasRangeAndLineOfSight(exactLocal, exactTarget, intent.ActionId))
        {
            return ClientActionAttemptOutcome.SoftUnavailable;
        }

        var before = ClientActionAttemptBoundary.Capture(actionManager, intent.ActionId);
        attempted = true;
        try
        {
            var accepted = nearAssist.RunWithoutRedirect(() =>
                actionManager->UseAction(
                    ActionType.Action,
                    intent.ActionId,
                    intent.Target.GameObjectId,
                    0,
                    ActionManager.UseActionMode.None,
                    0));
            var after = ClientActionAttemptBoundary.Capture(actionManager, intent.ActionId);
            var outcome = ClientActionAttemptBoundaryRules.Classify(
                accepted,
                intent.ActionId,
                before,
                after);
            if (accepted &&
                after.LastUsedActionSequence != 0 &&
                after.LastUsedActionSequence != before.LastUsedActionSequence)
            {
                sourceSequence = after.LastUsedActionSequence;
            }

            return outcome;
        }
        catch (Exception exception)
        {
            LogAttemptFailure(exception, Environment.TickCount64);
            return ClientActionAttemptOutcome.AcceptanceUnknown;
        }
    }

    private unsafe ScholarSpreadNativeState ObserveNativeState(IPlayerCharacter localPlayer)
    {
        if (!IsExactLocalScholar(
                localPlayer,
                new TargetPressureActorIdentity(localPlayer.GameObjectId, localPlayer.EntityId)))
        {
            return ScholarSpreadNativeState.Unknown;
        }

        var actionManager = ActionManager.Instance();
        if (actionManager == null) return ScholarSpreadNativeState.Unknown;

        var adlo = actionManager->GetAdjustedActionId(ScholarSpreadRules.AdloquiumActionId);
        var bio = actionManager->GetAdjustedActionId(ScholarSpreadRules.BiolysisActionId);
        var deploy = actionManager->GetAdjustedActionId(ScholarSpreadRules.DeploymentTacticsActionId);
        if (adlo != ScholarSpreadRules.AdloquiumActionId ||
            bio != ScholarSpreadRules.BiolysisActionId ||
            deploy != ScholarSpreadRules.DeploymentTacticsActionId)
        {
            return ScholarSpreadNativeState.Unknown;
        }

        // Runtime recast groups and adjusted totals are observations, not stable
        // identity. A transient or patch-dependent value must not disable every
        // Scholar action. Unknown timing only blocks the one-charge shield
        // reservation path; DoT spread and two-charge shield spread remain valid.
        var bioTimingKnown = TryObserveRecast(
            actionManager,
            bio,
            out var bioRemaining);
        var deployTimingKnown = TryObserveRecast(
            actionManager,
            deploy,
            out var deployRemaining);

        var charges = actionManager->GetCurrentCharges(deploy);
        if (charges > 2) return ScholarSpreadNativeState.Unknown;
        var boundaryClear = HeldActionRetryRules.IsNativeBoundaryNearQueueable(
            actionManager->AnimationLock,
            localPlayer.IsCasting,
            actionManager->CastActionId,
            actionManager->ActionQueued);
        return new ScholarSpreadNativeState(
            Known: true,
            AdloquiumReady:
                actionManager->IsActionOffCooldown(ActionType.Action, adlo) &&
                actionManager->CheckActionResources(ActionType.Action, adlo) == 0,
            BiolysisReady:
                actionManager->IsActionOffCooldown(ActionType.Action, bio) &&
                actionManager->CheckActionResources(ActionType.Action, bio) == 0,
            DeploymentReady:
                charges > 0 &&
                actionManager->IsActionOffCooldown(ActionType.Action, deploy) &&
                actionManager->CheckActionResources(ActionType.Action, deploy) == 0,
            DeploymentCharges: (int)charges,
            DeploymentTimingKnown: deployTimingKnown,
            DeploymentRemainingMilliseconds: deployRemaining,
            BiolysisTimingKnown: bioTimingKnown,
            BiolysisRemainingMilliseconds: bioRemaining,
            NativeBoundaryClear: boundaryClear);
    }

    private static unsafe bool TryObserveRecast(
        ActionManager* actionManager,
        uint actionId,
        out long remainingMilliseconds)
    {
        remainingMilliseconds = -1;
        var group = actionManager->GetRecastGroup((int)ActionType.Action, actionId);
        var detail = group < 0 ? null : actionManager->GetRecastGroupDetail(group);
        if (detail == null ||
            !float.IsFinite(detail->Elapsed) ||
            !float.IsFinite(detail->Total) ||
            detail->Elapsed < 0f ||
            detail->Total < 0f)
        {
            return false;
        }

        if (!detail->IsActive)
        {
            remainingMilliseconds = 0;
            return true;
        }

        var remainingSeconds = Math.Max(0d, detail->Total - detail->Elapsed);
        if (!double.IsFinite(remainingSeconds) ||
            remainingSeconds > (double)long.MaxValue / 1000d)
        {
            return false;
        }

        remainingMilliseconds = (long)Math.Ceiling(remainingSeconds * 1000d);
        return true;
    }

    private void ResolveRuntimeCandidates(
        IPlayerCharacter localPlayer,
        bool metadataVerified,
        out ScholarDotRuntimeCandidate[] dots,
        out ScholarShieldRuntimeCandidate[] shields,
        out bool tacticalCrystalResolved,
        out Vector3 tacticalCrystalPosition)
    {
        var enemies = ResolveExactEnemies();
        var party = ResolveExactParty(new TargetPressureActorIdentity(
            localPlayer.GameObjectId,
            localPlayer.EntityId));
        tacticalCrystalResolved = TryResolveTacticalCrystal(
            metadataVerified,
            out tacticalCrystalPosition);

        var dotList = new List<ScholarDotRuntimeCandidate>(enemies.Length);
        foreach (var member in enemies)
        {
            var ownPairKnown = TryObserveOwnPair(
                member.Player,
                localPlayer.EntityId,
                ScholarSpreadRules.BiolysisStatusId,
                ScholarSpreadRules.BiolyticStatusId,
                out var hasFirst,
                out var hasSecond);
            var coverageKnown = TryCountDotCoverage(
                member,
                enemies,
                localPlayer.EntityId,
                seedPairMustBeActive: false,
                out var affected);
            dotList.Add(new ScholarDotRuntimeCandidate(
                new ScholarSpreadDotCandidate(
                    member.Slot,
                    member.Identity,
                    member.Exact,
                    IsLivePlayer(member.Player),
                    member.Player.IsTargetable,
                    member.Player.CurrentHp,
                    member.Player.MaxHp,
                    NativeTargetValid:
                        member.Exact &&
                        HasValidActionTarget(
                            ScholarSpreadRules.BiolysisActionId,
                            member.Identity.GameObjectId),
                    NativeRangeAndLineOfSight:
                        member.Exact &&
                        HasRangeAndLineOfSight(
                            localPlayer,
                            member.Player,
                            ScholarSpreadRules.BiolysisActionId),
                    HasOwnBiolysis: ownPairKnown && hasFirst,
                    HasOwnBiolytic: ownPairKnown && hasSecond,
                    ExactCoverageKnown: ownPairKnown && coverageKnown,
                    NewlyCoveredEnemyCount: affected),
                member.Player.Address));
        }

        var shieldList = new List<ScholarShieldRuntimeCandidate>(party.Length);
        foreach (var member in party)
        {
            var ownPairKnown = TryObserveOwnPair(
                member.Player,
                localPlayer.EntityId,
                ScholarSpreadRules.GalvanizeStatusId,
                ScholarSpreadRules.CatalyzeStatusId,
                out var hasFirst,
                out var hasSecond);
            var coverageKnown = TryCountShieldCoverage(
                member,
                party,
                localPlayer.EntityId,
                seedPairMustBeActive: false,
                out var affected);
            shieldList.Add(new ScholarShieldRuntimeCandidate(
                new ScholarSpreadShieldCandidate(
                    member.Slot,
                    member.Identity,
                    member.Exact,
                    IsLivePlayer(member.Player),
                    member.Player.IsTargetable,
                    member.Player.CurrentHp,
                    member.Player.MaxHp,
                    NativeTargetValid:
                        member.Exact &&
                        HasValidActionTarget(
                            ScholarSpreadRules.AdloquiumActionId,
                            member.Identity.GameObjectId),
                    NativeRangeAndLineOfSight:
                        member.Exact &&
                        HasRangeAndLineOfSight(
                            localPlayer,
                            member.Player,
                            ScholarSpreadRules.AdloquiumActionId),
                    HasOwnGalvanize: ownPairKnown && hasFirst,
                    HasOwnCatalyze: ownPairKnown && hasSecond,
                    TacticalCrystalPresenceKnown: tacticalCrystalResolved,
                    OnTacticalCrystal:
                        tacticalCrystalResolved &&
                        IsInsideTacticalCrystal(member.Player, tacticalCrystalPosition),
                    ExactCoverageKnown: ownPairKnown && coverageKnown,
                    NewlyCoveredPartyCount: affected),
                member.Player.Address));
        }

        dots = dotList.ToArray();
        shields = shieldList.ToArray();
    }

    private bool TryBuildIntentObservation(
        IPlayerCharacter localPlayer,
        ScholarSpreadIntent intent,
        ScholarSpreadNativeState nativeState,
        bool shieldReservationSafe,
        IReadOnlyList<ScholarDotRuntimeCandidate> observedDots,
        IReadOnlyList<ScholarShieldRuntimeCandidate> observedShields,
        bool tacticalCrystalResolved,
        Vector3 tacticalCrystalPosition,
        out ScholarSpreadIntentObservation observation)
    {
        observation = default;
        var currentLocalIdentity = new TargetPressureActorIdentity(
            localPlayer.GameObjectId,
            localPlayer.EntityId);
        if (currentLocalIdentity != intent.LocalPlayer) return false;

        var currentTarget = ResolveFrozenTarget(intent);
        if (currentTarget is null) return false;
        var currentIdentity = new TargetPressureActorIdentity(
            currentTarget.GameObjectId,
            currentTarget.EntityId);
        if (currentIdentity != intent.Target) return false;

        var expectedFirst = intent.Kind == ScholarSpreadKind.Dot
            ? ScholarSpreadRules.BiolysisStatusId
            : ScholarSpreadRules.GalvanizeStatusId;
        var expectedSecond = intent.Kind == ScholarSpreadKind.Dot
            ? ScholarSpreadRules.BiolyticStatusId
            : ScholarSpreadRules.CatalyzeStatusId;
        var pairKnown = TryObserveOwnPair(
            currentTarget,
            localPlayer.EntityId,
            expectedFirst,
            expectedSecond,
            out var hasFirst,
            out var hasSecond);
        var pairActive = pairKnown && hasFirst && hasSecond;

        var exactCoverageKnown = intent.Kind == ScholarSpreadKind.Dot
            ? TryCountDotCoverage(
                new ExactRosterMember(intent.TargetSlot, currentTarget, currentIdentity, true),
                ResolveExactEnemies(),
                localPlayer.EntityId,
                seedPairMustBeActive: intent.IsDeployment,
                out var affected)
            : TryCountShieldCoverage(
                new ExactRosterMember(intent.TargetSlot, currentTarget, currentIdentity, true),
                ResolveExactParty(currentLocalIdentity),
                localPlayer.EntityId,
                seedPairMustBeActive: intent.IsDeployment,
                out affected);
        var actionReady = intent.ActionId switch
        {
            ScholarSpreadRules.BiolysisActionId => nativeState.BiolysisReady,
            ScholarSpreadRules.AdloquiumActionId => nativeState.AdloquiumReady,
            ScholarSpreadRules.DeploymentTacticsActionId => nativeState.DeploymentReady,
            _ => false,
        };
        var resolved = ResolveAdjustedActionId(intent.ActionId);
        observation = new ScholarSpreadIntentObservation(
            new ScholarSpreadExactTargetSnapshot(
                intent.Kind,
                intent.TargetSlot,
                currentLocalIdentity,
                currentIdentity,
                ExactCanonicalIdentity:
                    pairKnown &&
                    IsFrozenRuntimeTargetExact(
                        intent,
                        currentTarget,
                        observedDots,
                        observedShields),
                Alive: IsLivePlayer(currentTarget),
                Targetable: currentTarget.IsTargetable,
                CurrentHp: currentTarget.CurrentHp,
                MaximumHp: currentTarget.MaxHp,
                NativeTargetValid: HasValidActionTarget(
                    intent.ActionId,
                    intent.Target.GameObjectId),
                NativeRangeAndLineOfSight:
                    HasRangeAndLineOfSight(localPlayer, currentTarget, intent.ActionId),
                TacticalCrystalPresenceKnown:
                    intent.Kind == ScholarSpreadKind.Shield && tacticalCrystalResolved,
                OnTacticalCrystal:
                    intent.Kind == ScholarSpreadKind.Shield &&
                    tacticalCrystalResolved &&
                    IsInsideTacticalCrystal(currentTarget, tacticalCrystalPosition),
                ExactCoverageKnown: exactCoverageKnown,
                CurrentAffectedCount: affected,
                ExpectedOwnStatusPairActive: pairActive),
            HeldGameplayKeyEligible: true,
            NativeActionBoundaryClear: nativeState.NativeBoundaryClear,
            ResolvedActionId: resolved,
            ActionLocallyReady: actionReady,
            DeploymentCharges: nativeState.DeploymentCharges,
            ShieldReservationStillSafe: shieldReservationSafe);
        return true;
    }

    private static bool IsFrozenRuntimeTargetExact(
        ScholarSpreadIntent intent,
        IPlayerCharacter target,
        IReadOnlyList<ScholarDotRuntimeCandidate> dots,
        IReadOnlyList<ScholarShieldRuntimeCandidate> shields)
    {
        if (intent.Kind == ScholarSpreadKind.Dot)
        {
            return dots.Any(item =>
                item.Candidate.EnemySlot == intent.TargetSlot &&
                item.Candidate.Actor == intent.Target &&
                item.Address == target.Address);
        }

        return shields.Any(item =>
            item.Candidate.PartySlot == intent.TargetSlot &&
            item.Candidate.Actor == intent.Target &&
            item.Address == target.Address);
    }

    private ExactRosterMember[] ResolveExactEnemies()
    {
        var result = new List<ExactRosterMember>(ScholarSpreadRules.MaximumEnemyTargets);
        for (var slot = EnemySlotRules.FirstSlot; slot <= EnemySlotRules.LastSlot; slot++)
        {
            var first = EnemySlotResolver.Resolve(objectTable, slot);
            if (!HasValidNativeIdentity(first)) return [];
            var identity = new TargetPressureActorIdentity(first!.GameObjectId, first.EntityId);
            var second = EnemySlotResolver.Resolve(objectTable, slot);
            if (!HasValidNativeIdentity(second) ||
                second!.Address != first.Address ||
                second.GameObjectId != first.GameObjectId ||
                second.EntityId != first.EntityId)
            {
                return [];
            }

            result.Add(new ExactRosterMember(slot, first, identity, true));
        }

        return HasCompleteUniqueRoster(
            result,
            ScholarSpreadRules.CrystallineConflictRosterSize)
            ? result.ToArray()
            : [];
    }

    private ExactRosterMember[] ResolveExactParty(
        TargetPressureActorIdentity localPlayer)
    {
        var result = new List<ExactRosterMember>(ScholarSpreadRules.MaximumPartyTargets);
        for (var slot = ScholarSpreadRules.FirstPartySlot;
             slot <= ScholarSpreadRules.LastPartySlot;
             slot++)
        {
            var first = PartySlotResolver.Resolve(objectTable, slot);
            if (first is null) continue;
            if (!HasValidNativeIdentity(first)) return [];
            var identity = new TargetPressureActorIdentity(first!.GameObjectId, first.EntityId);
            var second = PartySlotResolver.Resolve(objectTable, slot);
            if (!HasValidNativeIdentity(second) ||
                second!.Address != first.Address ||
                second.GameObjectId != first.GameObjectId ||
                second.EntityId != first.EntityId)
            {
                return [];
            }

            result.Add(new ExactRosterMember(slot, first, identity, true));
        }

        return localPlayer.IsValid &&
               result.Count(static item => item.Identity.IsValid) ==
               ScholarSpreadRules.CrystallineConflictRosterSize &&
               result.Count(item => item.Identity == localPlayer) == 1 &&
               HasCompleteUniqueRoster(
                   result,
                   ScholarSpreadRules.CrystallineConflictRosterSize)
            ? result.ToArray()
            : [];
    }

    private static bool HasCompleteUniqueRoster(
        IReadOnlyList<ExactRosterMember> roster,
        int expectedCount) =>
        roster.Count == expectedCount &&
        roster.All(static item => item.Exact && item.Identity.IsValid) &&
        roster.Select(static item => item.Slot).Distinct().Count() == expectedCount &&
        roster.Select(static item => item.Identity).Distinct().Count() == expectedCount;

    private static bool TryCountDotCoverage(
        ExactRosterMember seed,
        IReadOnlyList<ExactRosterMember> roster,
        uint localEntityId,
        bool seedPairMustBeActive,
        out int affected)
    {
        affected = 0;
        if (!TryValidateSeedInRoster(seed, roster, out var exactSeed)) return false;
        foreach (var member in roster)
        {
            if (!member.Exact) return false;
            if (!IsLivePlayer(member.Player) || !member.Player.IsTargetable) continue;
            if (!IsWithinDeploymentRadius(exactSeed.Player, member.Player))
            {
                if (!IsFinitePosition(exactSeed.Player.Position) ||
                    !IsFinitePosition(member.Player.Position) ||
                    !float.IsFinite(member.Player.HitboxRadius) ||
                    member.Player.HitboxRadius < 0f)
                {
                    return false;
                }

                continue;
            }

            if (!TryObserveOwnPair(
                    member.Player,
                    localEntityId,
                    ScholarSpreadRules.BiolysisStatusId,
                    ScholarSpreadRules.BiolyticStatusId,
                    out var first,
                    out var second))
            {
                return false;
            }

            var isSeed = member.Identity == exactSeed.Identity;
            if (isSeed ? first == seedPairMustBeActive && second == seedPairMustBeActive
                       : !first && !second)
            {
                affected++;
            }
        }

        return true;
    }

    private static bool TryCountShieldCoverage(
        ExactRosterMember seed,
        IReadOnlyList<ExactRosterMember> roster,
        uint localEntityId,
        bool seedPairMustBeActive,
        out int affected)
    {
        affected = 0;
        if (!TryValidateSeedInRoster(seed, roster, out var exactSeed)) return false;
        foreach (var member in roster)
        {
            if (!member.Exact) return false;
            if (!IsLivePlayer(member.Player) || !member.Player.IsTargetable) continue;
            if (!IsWithinDeploymentRadius(exactSeed.Player, member.Player))
            {
                if (!IsFinitePosition(exactSeed.Player.Position) ||
                    !IsFinitePosition(member.Player.Position) ||
                    !float.IsFinite(member.Player.HitboxRadius) ||
                    member.Player.HitboxRadius < 0f)
                {
                    return false;
                }

                continue;
            }

            if (!TryObserveOwnPair(
                    member.Player,
                    localEntityId,
                    ScholarSpreadRules.GalvanizeStatusId,
                    ScholarSpreadRules.CatalyzeStatusId,
                    out var first,
                    out var second))
            {
                return false;
            }

            var isSeed = member.Identity == exactSeed.Identity;
            if (isSeed ? first == seedPairMustBeActive && second == seedPairMustBeActive
                       : !first && !second)
            {
                affected++;
            }
        }

        return true;
    }

    private static bool TryValidateSeedInRoster(
        ExactRosterMember seed,
        IReadOnlyList<ExactRosterMember> roster,
        out ExactRosterMember exactSeed)
    {
        exactSeed = default;
        var matches = roster.Where(item =>
            item.Slot == seed.Slot &&
            item.Identity == seed.Identity &&
            item.Player.Address == seed.Player.Address).ToArray();
        if (matches.Length != 1) return false;
        exactSeed = matches[0];
        return exactSeed.Exact;
    }

    private static bool TryObserveOwnPair(
        IPlayerCharacter player,
        uint localEntityId,
        uint firstStatusId,
        uint secondStatusId,
        out bool hasFirst,
        out bool hasSecond)
    {
        hasFirst = false;
        hasSecond = false;
        if (!IsNetworkEntityId(localEntityId)) return false;
        var firstCount = 0;
        var secondCount = 0;
        foreach (var status in player.StatusList)
        {
            var id = status.StatusId;
            if (status.SourceId != localEntityId ||
                (id != firstStatusId && id != secondStatusId))
            {
                continue;
            }

            if (!float.IsFinite(status.RemainingTime)) return false;
            if (status.RemainingTime <= 0f) continue;
            if (id == firstStatusId)
            {
                if (++firstCount > 1) return false;
                hasFirst = true;
            }
            else
            {
                if (++secondCount > 1) return false;
                hasSecond = true;
            }
        }

        // A locally-owned half-pair is an observation race or metadata drift;
        // never use it as either a clean setup seed or an owned spread seed.
        return hasFirst == hasSecond;
    }

    private bool TryResolveTacticalCrystal(
        bool metadataVerified,
        out Vector3 position)
    {
        position = default;
        if (!metadataVerified) return false;
        var matches = objectTable
            .Where(item =>
                item.Address != 0 &&
                item.ObjectKind == DalamudObjectKind.BattleNpc &&
                item is DalamudBattleChara character &&
                character.NameId == EnemyCombatConstants.TacticalCrystalBattleNpcNameId &&
                IsFinitePosition(item.Position))
            .ToArray();
        if (matches.Length != 1) return false;
        position = matches[0].Position;
        return true;
    }

    private static bool IsInsideTacticalCrystal(
        IPlayerCharacter player,
        Vector3 crystalPosition)
    {
        if (!IsFinitePosition(player.Position) ||
            !float.IsFinite(player.HitboxRadius) ||
            player.HitboxRadius < 0f)
        {
            return false;
        }

        var centerDistance = Vector3.Distance(player.Position, crystalPosition);
        return float.IsFinite(centerDistance) &&
               Math.Max(0f, centerDistance - player.HitboxRadius) <=
               TacticalCrystalPriorityRadiusYalms;
    }

    private static bool IsWithinDeploymentRadius(
        IPlayerCharacter seed,
        IPlayerCharacter recipient)
    {
        if (!IsFinitePosition(seed.Position) ||
            !IsFinitePosition(recipient.Position) ||
            !float.IsFinite(recipient.HitboxRadius) ||
            recipient.HitboxRadius < 0f)
        {
            return false;
        }

        var centerDistance = Vector3.Distance(seed.Position, recipient.Position);
        return float.IsFinite(centerDistance) &&
               Math.Max(0f, centerDistance - recipient.HitboxRadius) <=
               DeploymentRadiusYalms;
    }

    private IPlayerCharacter? ResolveFrozenTarget(ScholarSpreadIntent intent)
    {
        var target = intent.Kind == ScholarSpreadKind.Dot
            ? EnemySlotResolver.Resolve(objectTable, intent.TargetSlot)
            : PartySlotResolver.Resolve(objectTable, intent.TargetSlot);
        if (!HasValidNativeIdentity(target) ||
            target!.GameObjectId != intent.Target.GameObjectId ||
            target.EntityId != intent.Target.EntityId)
        {
            return null;
        }

        var stable = intent.Kind == ScholarSpreadKind.Dot
            ? EnemySlotResolver.Resolve(objectTable, intent.TargetSlot)
            : PartySlotResolver.Resolve(objectTable, intent.TargetSlot);
        return HasValidNativeIdentity(stable) &&
               stable!.Address == target.Address &&
               stable.GameObjectId == target.GameObjectId &&
               stable.EntityId == target.EntityId
            ? target
            : null;
    }

    private IPlayerCharacter? ResolveExactLocalPlayer(
        TargetPressureActorIdentity expected)
    {
        var local = objectTable.LocalPlayer;
        if (!HasValidNativeIdentity(local) ||
            local!.GameObjectId != expected.GameObjectId ||
            local.EntityId != expected.EntityId)
        {
            return null;
        }

        var table = objectTable.SearchByEntityId(local.EntityId) as IPlayerCharacter;
        return HasValidNativeIdentity(table) &&
               table!.Address == local.Address &&
               table.GameObjectId == local.GameObjectId &&
               table.EntityId == local.EntityId
            ? local
            : null;
    }

    private TargetPressureActorIdentity ResolveCapturedTargetIdentity(
        uint targetEntityId,
        ScholarSpreadPlan plan)
    {
        if (targetEntityId == plan.Target.EntityId) return plan.Target;
        if (!IsNetworkEntityId(targetEntityId)) return default;
        var target = objectTable.SearchByEntityId(targetEntityId) as IPlayerCharacter;
        return HasValidNativeIdentity(target)
            ? new TargetPressureActorIdentity(target!.GameObjectId, target.EntityId)
            : default;
    }

    private bool HasExactOwnPairOnFrozenTarget(
        ScholarSpreadPlan plan,
        uint localEntityId)
    {
        if (!plan.IsValid || !IsNetworkEntityId(localEntityId)) return false;
        var intent = new ScholarSpreadIntent(
            plan.EpisodeToken,
            plan.Kind,
            ScholarSpreadPhase.DeploymentReady,
            ScholarSpreadRules.DeploymentTacticsActionId,
            plan.LocalPlayer,
            plan.TargetSlot,
            plan.Target);
        var target = ResolveFrozenTarget(intent);
        if (target is null) return false;
        return TryObserveOwnPair(
                   target,
                   localEntityId,
                   plan.Kind == ScholarSpreadKind.Dot
                       ? ScholarSpreadRules.BiolysisStatusId
                       : ScholarSpreadRules.GalvanizeStatusId,
                   plan.Kind == ScholarSpreadKind.Dot
                       ? ScholarSpreadRules.BiolyticStatusId
                       : ScholarSpreadRules.CatalyzeStatusId,
                   out var first,
                   out var second) &&
               first && second;
    }

    private bool ObserveMatchStartGate(
        bool liveContextValid,
        bool dutyStartedRaw,
        bool hardReset)
    {
        var territory = clientState.TerritoryType;
        if (!liveContextValid)
        {
            Interlocked.Exchange(ref signaledMatchStartTerritory, 0);
            Interlocked.Exchange(ref signaledMatchCompletionTerritory, 0);
            matchGateState = ScholarSpreadRules.ObserveMatchGate(
                matchGateState,
                new ScholarSpreadMatchGateObservation(
                    territory,
                    LiveContextValid: false,
                    HardReset: hardReset,
                    DutyStartedRaw: false,
                    DutyStartSignaled: false,
                    DutyCompletionSignaled: false));
            return matchGateState.AllowsActions;
        }

        var startedTerritory = unchecked((uint)Interlocked.Exchange(
            ref signaledMatchStartTerritory,
            0));
        var completedTerritory = unchecked((uint)Interlocked.Exchange(
            ref signaledMatchCompletionTerritory,
            0));

        matchGateState = ScholarSpreadRules.ObserveMatchGate(
            matchGateState,
            new ScholarSpreadMatchGateObservation(
                territory,
                LiveContextValid: true,
                HardReset: hardReset,
                DutyStartedRaw: dutyStartedRaw,
                DutyStartSignaled: startedTerritory == territory,
                DutyCompletionSignaled: completedTerritory == territory));
        return matchGateState.AllowsActions;
    }

    private bool IsCurrentScholarMatchStarted() =>
        matchGateState.TerritoryId == clientState.TerritoryType &&
        matchGateState.AllowsActions;

    private void OnDutyStarted(IDutyStateEventArgs _) =>
        Interlocked.Exchange(
            ref signaledMatchStartTerritory,
            unchecked((int)clientState.TerritoryType));

    private void OnDutyRecommenced(IDutyStateEventArgs _) =>
        Interlocked.Exchange(
            ref signaledMatchStartTerritory,
            unchecked((int)clientState.TerritoryType));

    private void OnDutyCompleted(IDutyStateEventArgs _) =>
        Interlocked.Exchange(
            ref signaledMatchCompletionTerritory,
            unchecked((int)clientState.TerritoryType));

    private bool IsCurrentCrystallineConflict()
    {
        var condition = dutyState.ContentFinderCondition;
        var valid = condition.IsValid;
        return PvPMatchRules.ResolveSupportedContext(
                   clientState.IsPvP,
                   clientState.IsPvPExcludingDen,
                   configuration.EnableWolvesDenTesting,
                   clientState.TerritoryType,
                   valid,
                   valid && condition.Value.PvP,
                   valid ? condition.Value.ContentUICategory.RowId : 0,
                   valid && condition.Value.CrystallineConflictCasualRoulette,
                   valid && condition.Value.CrystallineConflictRankedRoulette) ==
               SupportedPvPContext.CrystallineConflict;
    }

    private bool IsCurrentlySuppressedByGuard(
        IPlayerCharacter localPlayer,
        long nowMilliseconds) =>
        DefensiveUtilityProbe.HasActiveGuard(localPlayer) ||
        nearAssist.TryGetRecentExactLocalGuardAttempt(
            clientState.TerritoryType,
            localPlayer.GameObjectId,
            localPlayer.EntityId,
            nowMilliseconds,
            DefensiveUtilityRules.GuardPropagationLatchMilliseconds,
            out _);

    private static unsafe bool HasRangeAndLineOfSight(
        IPlayerCharacter localPlayer,
        IPlayerCharacter target,
        uint actionId)
    {
        if (!ScholarSpreadRules.IsRelevantAction(actionId)) return false;
        var source = GetNativeObject(localPlayer);
        var destination = GetNativeObject(target);
        if (source == null || destination == null) return false;
        return SeitonRangeRules.HasNativeRangeAndLineOfSight(
            ActionManager.GetActionInRangeOrLoS(actionId, source, destination));
    }

    private static unsafe bool HasValidActionTarget(
        uint actionId,
        ulong targetGameObjectId)
    {
        if (!ScholarSpreadRules.IsRelevantAction(actionId) ||
            !IsNetworkObjectId(targetGameObjectId))
        {
            return false;
        }

        var manager = ActionManager.Instance();
        return manager != null &&
               manager->GetAdjustedActionId(actionId) == actionId &&
               manager->GetActionStatus(
                   ActionType.Action,
                   actionId,
                   targetGameObjectId,
                   checkRecastActive: false,
                   checkCastingActive: false) == 0;
    }

    private static unsafe uint ResolveAdjustedActionId(uint actionId)
    {
        var manager = ActionManager.Instance();
        return manager == null || !ScholarSpreadRules.IsRelevantAction(actionId)
            ? 0
            : manager->GetAdjustedActionId(actionId);
    }

    private static bool IsExactLocalScholar(
        IPlayerCharacter player,
        TargetPressureActorIdentity expected) =>
        HasValidNativeIdentity(player) &&
        expected.IsValid &&
        player.GameObjectId == expected.GameObjectId &&
        player.EntityId == expected.EntityId &&
        player.ClassJob.IsValid &&
        player.ClassJob.RowId == ScholarSpreadRules.ScholarJobId;

    private static bool IsLivePlayer(IPlayerCharacter? player) =>
        player is not null &&
        !player.IsDead &&
        player.CurrentHp > 0 &&
        player.MaxHp > 0 &&
        player.CurrentHp <= player.MaxHp;

    private static unsafe bool HasValidNativeIdentity(IPlayerCharacter? player) =>
        player is not null &&
        player.Address != 0 &&
        IsNetworkObjectId(player.GameObjectId) &&
        IsNetworkEntityId(player.EntityId) &&
        GetNativeObject(player) != null;

    private static unsafe GameObject* GetNativeObject(IPlayerCharacter player)
    {
        var native = (GameObject*)player.Address;
        return native != null && native->EntityId == player.EntityId
            ? native
            : null;
    }

    private static bool IsFinitePosition(Vector3 position) =>
        float.IsFinite(position.X) &&
        float.IsFinite(position.Y) &&
        float.IsFinite(position.Z);

    private static bool IsNetworkEntityId(uint entityId) =>
        entityId is not 0 and not 0xE0000000 and not uint.MaxValue;

    private static bool IsNetworkObjectId(ulong gameObjectId) =>
        gameObjectId is not 0 and not 0xE0000000 and not ulong.MaxValue;

    private ulong NextEpisodeToken()
    {
        var token = nextEpisodeToken;
        nextEpisodeToken = token == ulong.MaxValue ? 1 : token + 1;
        return token == 0 ? NextEpisodeToken() : token;
    }

    private static long NormalizeNow(long nowMilliseconds) =>
        nowMilliseconds >= 0 ? nowMilliseconds : Environment.TickCount64;

    private void ClearActionEpisode()
    {
        retryState = HeldActionRetryState.Initial;
        pendingAcceptedAtMilliseconds = -1;
        setupConfirmedAtMilliseconds = -1;
    }

    private void ResetRuntime(bool resetConsent, bool clearCapture)
    {
        workflowState = ScholarSpreadWorkflowState.Initial;
        if (resetConsent) consentState = ScholarSpreadHeldConsentState.Initial;
        terminalUntilRelease = false;
        ClearActionEpisode();
        if (clearCapture) actionEffectCapture.ClearScholarSpreadEffects();
        observedCaptureDropCount = actionEffectCapture.DroppedScholarSpreadEffects;
        observedCaptureErrorCount = actionEffectCapture.ScholarSpreadCaptureErrors;
    }

    private void LogAttemptFailure(Exception exception, long nowMilliseconds)
    {
        if (nowMilliseconds < nextErrorLogAt) return;
        nextErrorLogAt = nowMilliseconds + 10_000;
        log.Error(
            exception,
            "Seiton Sense Scholar spread exact action attempt failed closed.");
    }

    private static string Describe(
        bool liveContextValid,
        bool matchStarted,
        bool featureContextReady,
        bool statusPropagationPending,
        bool attempted,
        ClientActionAttemptOutcome outcome,
        ScholarSpreadPlanDecisionReason planReason,
        ScholarSpreadIntentDecisionReason intentReason,
        ScholarSpreadEffectDecisionReason effectReason)
    {
        if (liveContextValid && !matchStarted) return "Waiting for CC Duty Start";
        if (!featureContextReady) return "Waiting for exact Scholar CC context";
        if (statusPropagationPending) return "Waiting for owned setup status propagation";
        if (attempted) return $"Native action boundary: {outcome}";
        if (effectReason != ScholarSpreadEffectDecisionReason.None)
            return $"ActionEffect: {effectReason}";
        if (intentReason != ScholarSpreadIntentDecisionReason.None)
            return $"Exact intent: {intentReason}";
        if (planReason != ScholarSpreadPlanDecisionReason.None)
            return $"Planner: {planReason}";
        return "Independent lane ready";
    }

    private readonly record struct ScholarSpreadNativeState(
        bool Known,
        bool AdloquiumReady,
        bool BiolysisReady,
        bool DeploymentReady,
        int DeploymentCharges,
        bool DeploymentTimingKnown,
        long DeploymentRemainingMilliseconds,
        bool BiolysisTimingKnown,
        long BiolysisRemainingMilliseconds,
        bool NativeBoundaryClear)
    {
        internal static ScholarSpreadNativeState Unknown => new(
            false,
            false,
            false,
            false,
            0,
            false,
            -1,
            false,
            -1,
            false);
    }

    private readonly record struct ExactRosterMember(
        int Slot,
        IPlayerCharacter Player,
        TargetPressureActorIdentity Identity,
        bool Exact);

    private readonly record struct ScholarDotRuntimeCandidate(
        ScholarSpreadDotCandidate Candidate,
        nint Address);

    private readonly record struct ScholarShieldRuntimeCandidate(
        ScholarSpreadShieldCandidate Candidate,
        nint Address);
}
