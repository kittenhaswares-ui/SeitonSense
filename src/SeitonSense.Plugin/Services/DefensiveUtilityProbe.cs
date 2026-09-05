using System.Numerics;
using Dalamud.Game;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Lumina.Excel.Sheets;
using SeitonSense.Core;
using SeitonSense.Plugin.Models;
using GameAction = Lumina.Excel.Sheets.Action;
using GameStatus = Lumina.Excel.Sheets.Status;

namespace SeitonSense.Plugin.Services;

internal readonly record struct AcceptedAutoGuardianEpisode(
    long Token,
    long AcceptedAtMilliseconds,
    TargetPressureActorIdentity LocalPlayer,
    TargetPressureActorIdentity Target,
    int PartySlot)
{
    internal bool IsValid =>
        Token > 0 &&
        AcceptedAtMilliseconds >= 0 &&
        LocalPlayer.IsValid &&
        Target.IsValid &&
        LocalPlayer != Target &&
        PartySlot is >= 1 and <= 8;
}

internal readonly record struct GuardUseAttemptResult(
    ClientActionAttemptOutcome Outcome,
    long GenerationBeforeCall)
{
    internal static GuardUseAttemptResult NotInvoked =>
        new(ClientActionAttemptOutcome.NotInvoked, -1);
}

internal sealed record DefensiveUtilityProbeSnapshot(
    bool Active,
    DefensiveUtilityActionKind Action,
    DefensiveUtilityTrigger Trigger,
    bool PressureKnown,
    int IncomingEnemyCount,
    bool GuardActive,
    bool GuardPropagationLatchActive,
    long GuardPropagationLatchRemainingMilliseconds,
    bool HighPressureStunObserved,
    bool WaitingForPostPurifyGuard,
    long PostPurifyGuardRemainingMilliseconds,
    int GuardianCandidateCount,
    ulong TargetGameObjectId,
    uint TargetEntityId,
    VirtualKey FreshGameplayKey,
    VirtualKey HeldGameplayKey,
    bool InputClaimed,
    HeldCastCancellationRequest? CastCancellationRequest,
    bool UseActionAttempted,
    bool UseActionAccepted,
    long AttemptCount,
    long AcceptedCount,
    bool GuardMetadataVerified,
    bool GuardianMetadataVerified,
    AcceptedAutoGuardianEpisode? LastAcceptedGuardianEpisode,
    GuardianTriggerPopup? GuardianPopup,
    AutoGuardTriggerPopup? AutoGuardPopup,
    string LastEvent)
{
    internal int GuardianCriticalCandidateCount { get; init; }
    internal int GuardianProactiveCandidateCount { get; init; }
    internal PaladinGuardianRiskTier GuardianSelectedRiskTier { get; init; }
    internal uint GuardianSelectedCurrentHp { get; init; }
    internal uint GuardianSelectedMaximumHp { get; init; }
    internal int? GuardianSelectedIncomingEnemyCount { get; init; }
    internal long GuardianPressureAgeMilliseconds { get; init; } = -1;

    internal static DefensiveUtilityProbeSnapshot Initial { get; } = new(
        false,
        DefensiveUtilityActionKind.None,
        DefensiveUtilityTrigger.None,
        false,
        0,
        false,
        false,
        0,
        false,
        false,
        0,
        0,
        0,
        0,
        VirtualKey.NO_KEY,
        VirtualKey.NO_KEY,
        false,
        null,
        false,
        false,
        0,
        0,
        false,
        false,
        null,
        null,
        null,
        "Not started");
}

/// <summary>
/// Optional CC-only defensive action helper. It never produces more than one
/// action request for one physical gameplay-key generation. A high-pressure
/// Stun is handled by the existing Purify probe first; this probe can only use
/// Guard on a later physical generation after exact Resilience observation.
/// </summary>
internal sealed class DefensiveUtilityProbe
{
    private readonly IObjectTable objectTable;
    private readonly TargetPressureTracker pressureTracker;
    private readonly NearAssistRedirector nearAssist;
    private readonly IPluginLog log;
    private readonly PluginConfiguration configuration;
    private readonly bool guardMetadataVerified;
    private readonly bool guardianMetadataVerified;
    private readonly HashSet<TargetPressureActorIdentity> guardianSpentActors = [];
    private DefensiveUtilityProbeSnapshot snapshot = DefensiveUtilityProbeSnapshot.Initial;
    private bool awaitingPostPurifyConfirmation;
    private long postPurifyGuardExpiresAt = -1;
    private GuardPropagationState guardPropagationState = GuardPropagationState.Initial;
    private GuardianTriggerPopup? guardianPopup;
    private AutoGuardTriggerPopup? autoGuardPopup;
    private AutoGuardConfirmationState autoGuardConfirmationState =
        AutoGuardConfirmationState.Initial;
    private AcceptedAutoGuardianEpisode? lastAcceptedGuardianEpisode;
    private FrozenGuardRetry? frozenGuardRetry;
    private ClientActionAttemptFingerprint lastGuardNativeBoundary;
    private long lastGuardNativeAttemptFrameId = -1;
    private FrozenGuardianRetry? frozenGuardianRetry;
    private VirtualKey terminalGuardianKey = VirtualKey.NO_KEY;
    private long frozenIntentEpochToken;
    private long guardianEpisodeToken;
    private long autoGuardNotificationToken;
    private long attemptCount;
    private long acceptedCount;
    private long nextErrorLogAt;

    internal DefensiveUtilityProbe(
        IObjectTable objectTable,
        IDataManager dataManager,
        TargetPressureTracker pressureTracker,
        NearAssistRedirector nearAssist,
        IPluginLog log,
        PvPMetadataValidation metadata,
        PluginConfiguration configuration)
    {
        this.objectTable = objectTable;
        this.pressureTracker = pressureTracker;
        this.nearAssist = nearAssist;
        this.log = log;
        this.configuration = configuration;
        var localMetadata = ValidateMetadata(dataManager, log);
        guardMetadataVerified = metadata.GuardVerified && localMetadata.Guard;
        guardianMetadataVerified = metadata.GuardianVerified && localMetadata.Guardian;
    }

    internal DefensiveUtilityProbeSnapshot Snapshot => Volatile.Read(ref snapshot);

    internal GuardPropagationDecision ObserveGuardSuppression(
        bool exactGuardActive,
        long observedGuardAttemptAtMilliseconds,
        long nowMilliseconds,
        bool hardReset = false)
    {
        var decision = DefensiveUtilityRules.ObserveGuardPropagation(
            guardPropagationState,
            exactGuardActive,
            observedGuardAttemptAtMilliseconds,
            nowMilliseconds,
            hardReset);
        guardPropagationState = decision.NextState;
        return decision;
    }

    /// <summary>
    /// Generic defensive pass. This owns only the verified, keyless
    /// high-pressure Stun Purify -> confirmed Resilience -> Guard follow-up. A
    /// client-true Guard return remains provisional until exact live Guard is
    /// visible; only that confirmation may arm protection and presentation.
    /// </summary>
    internal unsafe DefensiveUtilityProbeSnapshot ObserveGuard(
        IPlayerCharacter? localPlayer,
        bool isCrystallineConflict,
        bool guardConfigurationEnabled,
        bool allowHeldGameplayKey,
        bool enableGuardOnStunPressure,
        bool pressureKnown,
        int incomingEnemyCount,
        bool highPressureStunObserved,
        bool purifyUseActionAccepted,
        bool resilienceActive,
        bool hasPurifyRemovableCrowdControl,
        bool guardActive,
        bool higherPriorityClaimed,
        EmergencyActionInputFrame inputFrame,
        long nowMilliseconds,
        bool adaptiveResponseEnabled,
        bool allowOccupiedNativeQueue,
        long frameworkFrameId,
        bool hardReset = false,
        DefensiveUtilityProbeSnapshot? prioritizedGuardianPass = null)
    {
        if (hardReset)
            ResetRuntime();
        else if (!guardConfigurationEnabled || !isCrystallineConflict)
            ResetGuardOpportunityRuntime();

        var localIdentityValid = HasValidLocalPlayer(localPlayer);
        var highPressure = DefensiveUtilityRules.IsHighPressure(
            pressureKnown,
            incomingEnemyCount);

        UpdatePostPurifyGuard(
            guardConfigurationEnabled &&
            isCrystallineConflict &&
            enableGuardOnStunPressure,
            highPressureStunObserved,
            purifyUseActionAccepted,
            resilienceActive,
            hasPurifyRemovableCrowdControl,
            nowMilliseconds);

        var input = inputFrame.Snapshot;
        var freshKey = inputFrame.FreshGameplayKeyPressed
            ? input.FreshGameplayKey
            : VirtualKey.NO_KEY;
        // Retain physical-key fields for shared diagnostics only. The generic
        // Auto-Guard path no longer consumes either one as consent.
        var heldKey = allowHeldGameplayKey && inputFrame.HeldGameplayKeyEligible
            ? input.HeldGameplayKey
            : VirtualKey.NO_KEY;
        var action = DefensiveUtilityActionKind.None;
        var trigger = DefensiveUtilityTrigger.None;
        var inputClaimed = false;
        HeldCastCancellationRequest? castCancellationRequest = null;
        var attempted = false;
        var accepted = false;
        var autoGuardProtectionArmed = false;
        var autoGuardConfirmed = false;
        var targetGameObjectId = 0UL;
        var targetEntityId = 0U;
        var lastEvent = DescribeWaitingState(
            guardConfigurationEnabled,
            isCrystallineConflict,
            localIdentityValid,
            guardActive,
            higherPriorityClaimed,
            pressureKnown,
            incomingEnemyCount);

        var canDispatch = guardConfigurationEnabled &&
                          isCrystallineConflict &&
                          localIdentityValid &&
                          !guardActive &&
                          !higherPriorityClaimed;
        var guardActionSpecificallyReady = guardMetadataVerified &&
                                           nearAssist.CanProtectAutomaticGuard &&
                                           IsActionSpecificallyReady(
                                               EnemyCombatConstants.GuardActionId);
        var guardNativeBoundary = CaptureGuardNativeBoundary();
        var relevantGuardNativeBoundaryEdge = adaptiveResponseEnabled &&
                                              IsRelevantGuardNativeBoundaryEdge(
                                                  lastGuardNativeBoundary,
                                                  guardNativeBoundary,
                                                  allowOccupiedNativeQueue);
        lastGuardNativeBoundary = guardNativeBoundary;
        var currentIdentity = localIdentityValid
            ? new TargetPressureActorIdentity(
                localPlayer!.GameObjectId,
                localPlayer.EntityId)
            : default;

        if (autoGuardConfirmationState.IsPending)
        {
            action = DefensiveUtilityActionKind.Guard;
            trigger = DefensiveUtilityTrigger.PostPurifyHighPressureStun;
            var exactGuardActive = HasActiveGuard(localPlayer);
            var retryReadiness = higherPriorityClaimed || inputFrame.IsConsumed
                ? AutoGuardRetryReadiness.NativeBoundaryBusy
                : ObserveGuardRetryReadiness(
                    localPlayer,
                    allowOccupiedNativeQueue);
            var confirmation = AutoGuardConfirmationRules.Observe(
                autoGuardConfirmationState,
                new AutoGuardConfirmationObservation(
                    guardConfigurationEnabled && isCrystallineConflict,
                    nearAssist.CurrentTerritoryId,
                    currentIdentity,
                    localIdentityValid,
                    exactGuardActive,
                    retryReadiness,
                    nowMilliseconds,
                    hardReset));

            if (confirmation.Confirmed)
            {
                autoGuardProtectionArmed = nearAssist.TryArmConfirmedAutoGuardProtection(
                    localPlayer!.GameObjectId,
                    localPlayer.EntityId,
                    autoGuardConfirmationState.GenerationBeforeCall);
                autoGuardConfirmed = autoGuardProtectionArmed;
                lastEvent = autoGuardProtectionArmed
                    ? "Exact automatic Guard status confirmed and protected"
                    : "Exact Guard appeared without matching automatic ownership";
                autoGuardConfirmationState = AutoGuardConfirmationState.Initial;
                frozenGuardRetry = null;
                awaitingPostPurifyConfirmation = false;
                postPurifyGuardExpiresAt = -1;
            }
            else if (confirmation.ShouldRetry &&
                     !higherPriorityClaimed &&
                     !inputFrame.IsConsumed)
            {
                inputClaimed = true;
                inputFrame.Consume();
                var retryResult = TryUseGuardOnce(
                    localPlayer!,
                    allowOccupiedNativeQueue);
                attempted = retryResult.Outcome is not
                    (ClientActionAttemptOutcome.NotInvoked or
                     ClientActionAttemptOutcome.SoftUnavailable);
                accepted = retryResult.Outcome == ClientActionAttemptOutcome.ClientAccepted;
                if (attempted) lastGuardNativeAttemptFrameId = frameworkFrameId;
                if (accepted)
                {
                    autoGuardConfirmationState = AutoGuardConfirmationRules.ArmProvisional(
                        retryResult.GenerationBeforeCall,
                        nearAssist.CurrentTerritoryId,
                        currentIdentity,
                        Math.Max(nowMilliseconds, Environment.TickCount64),
                        autoGuardConfirmationState.OpportunityExpiresAtMilliseconds,
                        confirmationRetrySpent: true);
                    lastEvent = "Guard confirmation retry accepted provisionally; waiting for exact status";
                }
                else if (AutoGuardConfirmationRules.ShouldRetainUnspentRetry(
                             retryResult.Outcome))
                {
                    // No native boundary was crossed. Keep the original
                    // confirmation lease and its unspent single retry so one
                    // queue/animation-lock race cannot lose Auto-Guard.
                    autoGuardConfirmationState = confirmation.NextState;
                    lastEvent = "Guard confirmation retry waiting for native boundary";
                }
                else
                {
                    autoGuardConfirmationState = AutoGuardConfirmationState.Initial;
                    awaitingPostPurifyConfirmation = false;
                    postPurifyGuardExpiresAt = -1;
                    lastEvent = $"Guard confirmation retry retired ({retryResult.Outcome})";
                }
            }
            else
            {
                autoGuardConfirmationState = confirmation.NextState;
                lastEvent = confirmation.NextState.IsPending
                    ? $"Provisional Guard waiting ({confirmation.Reason})"
                    : $"Provisional Guard retired ({confirmation.Reason})";
                if (!confirmation.NextState.IsPending)
                {
                    awaitingPostPurifyConfirmation = false;
                    postPurifyGuardExpiresAt = -1;
                }
            }
        }
        else if (frozenGuardRetry is { } frozenGuard)
        {
            action = DefensiveUtilityActionKind.Guard;
            trigger = DefensiveUtilityTrigger.PostPurifyHighPressureStun;
            var exactRetryContext = guardConfigurationEnabled &&
                                    isCrystallineConflict &&
                                    currentIdentity == frozenGuard.LocalPlayer &&
                                    !HasActiveGuard(localPlayer) &&
                                    frozenGuard.ExpiresAtMilliseconds == postPurifyGuardExpiresAt &&
                                    nowMilliseconds < frozenGuard.ExpiresAtMilliseconds;
            if (!exactRetryContext)
            {
                frozenGuardRetry = null;
                awaitingPostPurifyConfirmation = false;
                postPurifyGuardExpiresAt = -1;
                lastEvent = "Frozen post-Purify Guard retry cancelled by exact context drift";
            }
            else if (!higherPriorityClaimed &&
                     !inputFrame.IsConsumed &&
                     HeldActionRetryRules.RetainsSchedulerFrame(
                         frozenGuard.Retry,
                         nowMilliseconds,
                         exactRetryContext,
                         guardActionSpecificallyReady))
            {
                inputClaimed = true;
                inputFrame.Consume();
                if (!IsCriticalGuardBoundaryNearQueueable(
                        localPlayer!,
                        allowOccupiedNativeQueue))
                {
                    frozenGuardRetry = frozenGuard;
                    lastEvent = "Frozen post-Purify Guard waiting for global native boundary";
                }
                else if (!(adaptiveResponseEnabled
                    ? HeldActionRetryRules.CanAttemptFrozenIntentOnBoundaryEdgeOrThrottle(
                        frozenGuard.Retry,
                        nowMilliseconds,
                        frameworkFrameId,
                        lastGuardNativeAttemptFrameId,
                        relevantGuardNativeBoundaryEdge)
                    : HeldActionRetryRules.CanAttemptFrozenIntent(
                        frozenGuard.Retry,
                        nowMilliseconds)))
                {
                    lastEvent = "Frozen post-Purify Guard retaining retry throttle priority";
                }
                else
                {
                    var attemptResult = TryUseGuardOnce(
                        localPlayer!,
                        allowOccupiedNativeQueue);
                    attempted = attemptResult.Outcome is not
                        (ClientActionAttemptOutcome.NotInvoked or
                         ClientActionAttemptOutcome.SoftUnavailable);
                    accepted = attemptResult.Outcome == ClientActionAttemptOutcome.ClientAccepted;
                    if (attempted) lastGuardNativeAttemptFrameId = frameworkFrameId;
                    CompleteGuardAttempt(frozenGuard, attemptResult, nowMilliseconds);

                    lastEvent = DescribeAttempt(
                        "Guard retry",
                        frozenGuard.Retry.NativeAttemptCount + 1,
                        frozenGuard.Retry,
                        attemptResult.Outcome);
                }
            }
        }
        else if (canDispatch &&
            DefensiveUtilityRules.CanDispatchPostPurifyGuard(
                awaitingPostPurifyConfirmation,
                resilienceActive,
                hasPurifyRemovableCrowdControl,
                postPurifyGuardExpiresAt,
                nowMilliseconds) &&
            guardActionSpecificallyReady)
        {
            action = DefensiveUtilityActionKind.Guard;
            trigger = DefensiveUtilityTrigger.PostPurifyHighPressureStun;
            inputClaimed = true;
            inputFrame.Consume();
            var frozen = new FrozenGuardRetry(
                new TargetPressureActorIdentity(localPlayer!.GameObjectId, localPlayer.EntityId),
                postPurifyGuardExpiresAt,
                HeldActionRetryState.Initial);
            if (!IsCriticalGuardBoundaryNearQueueable(
                    localPlayer!,
                    allowOccupiedNativeQueue))
            {
                frozenGuardRetry = frozen;
                lastEvent = "Frozen post-Purify Guard waiting for global native boundary";
            }
            else
            {
                var attemptResult = TryUseGuardOnce(
                    localPlayer!,
                    allowOccupiedNativeQueue);
                attempted = attemptResult.Outcome is not
                    (ClientActionAttemptOutcome.NotInvoked or
                     ClientActionAttemptOutcome.SoftUnavailable);
                accepted = attemptResult.Outcome == ClientActionAttemptOutcome.ClientAccepted;
                if (attempted) lastGuardNativeAttemptFrameId = frameworkFrameId;
                CompleteGuardAttempt(frozen, attemptResult, nowMilliseconds);

                lastEvent = accepted
                    ? "Guard initial accepted provisionally; waiting for exact status"
                    : DescribeAttempt("Guard initial", 1, frozen.Retry, attemptResult.Outcome);
            }
        }

        if (attempted) Interlocked.Increment(ref attemptCount);
        if (accepted) Interlocked.Increment(ref acceptedCount);

        var autoGuardToken = autoGuardConfirmed &&
                             autoGuardProtectionArmed &&
                             action == DefensiveUtilityActionKind.Guard
            ? NextAutoGuardNotificationToken()
            : autoGuardPopup?.Token ?? 0;
        autoGuardPopup = DefensiveUtilityRules.ObserveAutoGuardTriggerPopup(
            autoGuardPopup,
            guardConfigurationEnabled && isCrystallineConflict && localIdentityValid,
            action,
            autoGuardConfirmed && autoGuardProtectionArmed,
            autoGuardToken,
            nowMilliseconds,
            hardReset);

        var guardSuppressionNow = Math.Max(nowMilliseconds, Environment.TickCount64);
        var guardSuppression = ObserveGuardSuppression(
            HasActiveGuard(localPlayer),
            observedGuardAttemptAtMilliseconds: -1,
            guardSuppressionNow);

        var guardResult = new DefensiveUtilityProbeSnapshot(
            guardConfigurationEnabled && isCrystallineConflict && localIdentityValid,
            action,
            trigger,
            pressureKnown,
            incomingEnemyCount,
            guardActive,
            guardSuppression.PropagationLatchActive,
            guardSuppression.RemainingMilliseconds,
            highPressureStunObserved,
            awaitingPostPurifyConfirmation || postPurifyGuardExpiresAt > nowMilliseconds,
            postPurifyGuardExpiresAt > nowMilliseconds
                ? postPurifyGuardExpiresAt - nowMilliseconds
                : 0,
            0,
            targetGameObjectId,
            targetEntityId,
            freshKey,
            heldKey,
            inputClaimed,
            castCancellationRequest,
            attempted,
            accepted,
            Interlocked.Read(ref attemptCount),
            Interlocked.Read(ref acceptedCount),
            guardMetadataVerified,
            guardianMetadataVerified,
            lastAcceptedGuardianEpisode,
            guardianPopup,
            autoGuardPopup,
            lastEvent);
        var result = prioritizedGuardianPass is { } guardianPass
            ? MergePrioritizedGuardianPass(guardResult, guardianPass)
            : guardResult;
        Volatile.Write(ref snapshot, result);
        return result;
    }

    /// <summary>
    /// Job-specific defensive pass. Paladin Guardian has its own feature and
    /// held-key gates and is scheduled directly after Purify.
    /// </summary>
    internal unsafe DefensiveUtilityProbeSnapshot ObserveGuardian(
        IPlayerCharacter? localPlayer,
        bool isCrystallineConflict,
        bool guardianConfigurationEnabled,
        bool allowHeldGameplayKey,
        bool guardActive,
        bool higherPriorityClaimed,
        EmergencyActionInputFrame inputFrame,
        long nowMilliseconds,
        bool hardReset = false,
        bool beginsFrame = false)
    {
        if (hardReset)
            ResetRuntime();
        else if (!guardianConfigurationEnabled || !isCrystallineConflict)
            ResetGuardianOpportunityRuntime();

        ReleaseTerminalKeyWhenUp(inputFrame, ref terminalGuardianKey);

        // Historically Guardian was the second pass and could merge the Guard
        // snapshot published earlier in the same frame. The priority scheduler
        // now runs Guardian first, so it must start from a clean frame-local
        // base instead of inheriting a stale claimed/action bit from the prior
        // framework frame.
        var prior = beginsFrame
            ? CreateFrameSnapshotBase(guardActive)
            : Snapshot;
        var localIdentityValid = HasValidLocalPlayer(localPlayer);
        HashSet<TargetPressureActorIdentity> guardianRiskActors = [];
        var guardianPressureAgeMilliseconds = -1L;
        var guardianCandidates = guardianConfigurationEnabled &&
                                 isCrystallineConflict &&
                                 localIdentityValid &&
                                 IsPaladin(localPlayer!)
            ? BuildGuardianCandidates(
                localPlayer!,
                nowMilliseconds,
                out guardianRiskActors,
                out guardianPressureAgeMilliseconds)
            : [];
        if (!guardianConfigurationEnabled || !isCrystallineConflict)
            guardianSpentActors.Clear();
        else
            guardianSpentActors.RemoveWhere(actor => !guardianRiskActors.Contains(actor));

        var criticalGuardianCandidateCount = guardianCandidates.Count(candidate =>
            DefensiveUtilityRules.IsGuardianCandidate(candidate) &&
            DefensiveUtilityRules.ClassifyGuardianRisk(candidate) ==
            PaladinGuardianRiskTier.Critical);
        var proactiveGuardianCandidateCount = guardianCandidates.Count(candidate =>
            DefensiveUtilityRules.IsGuardianCandidate(candidate) &&
            DefensiveUtilityRules.ClassifyGuardianRisk(candidate) ==
            PaladinGuardianRiskTier.ProactiveHighPressure);
        var eligibleGuardianCandidateCount =
            criticalGuardianCandidateCount + proactiveGuardianCandidateCount;

        var input = inputFrame.Snapshot;
        var freshKey = inputFrame.FreshGameplayKeyPressed
            ? input.FreshGameplayKey
            : VirtualKey.NO_KEY;
        var heldKey = allowHeldGameplayKey && inputFrame.HeldGameplayKeyEligible
            ? input.HeldGameplayKey
            : VirtualKey.NO_KEY;
        var inputEligible = freshKey != VirtualKey.NO_KEY || heldKey != VirtualKey.NO_KEY;
        var selectedKey = heldKey != VirtualKey.NO_KEY ? heldKey : freshKey;
        var action = DefensiveUtilityActionKind.None;
        var trigger = DefensiveUtilityTrigger.None;
        var inputClaimed = false;
        HeldCastCancellationRequest? castCancellationRequest = null;
        var attempted = false;
        var accepted = false;
        var targetGameObjectId = 0UL;
        var targetEntityId = 0U;
        var selectedGuardianPartySlot = 0;
        var selectedGuardianRiskTier = PaladinGuardianRiskTier.None;
        var selectedGuardianCurrentHp = 0U;
        var selectedGuardianMaximumHp = 0U;
        int? selectedGuardianIncomingEnemyCount = null;
        var lastEvent = DescribeGuardianWaitingState(
            guardianConfigurationEnabled,
            isCrystallineConflict,
            localIdentityValid,
            guardActive,
            higherPriorityClaimed,
            criticalGuardianCandidateCount,
            proactiveGuardianCandidateCount,
            guardianPressureAgeMilliseconds);

        var canDispatch = guardianConfigurationEnabled &&
                          isCrystallineConflict &&
                          localIdentityValid &&
                          input.ProbeSucceeded &&
                          !input.IsTextInputActive &&
                          inputEligible &&
                          !guardActive &&
                          !higherPriorityClaimed;
        var guardianActionSpecificallyReady = IsGuardianActionSpecificallyReady(localPlayer);
        if (frozenGuardianRetry is { } frozenGuardian)
        {
            action = DefensiveUtilityActionKind.Guardian;
            trigger = DefensiveUtilityTrigger.PaladinGuardianLowAlly;
            targetGameObjectId = frozenGuardian.Intent.GameObjectId;
            targetEntityId = frozenGuardian.Intent.EntityId;
            selectedGuardianPartySlot = frozenGuardian.Intent.PartySlot;
            var currentIdentity = localIdentityValid
                ? new TargetPressureActorIdentity(localPlayer!.GameObjectId, localPlayer.EntityId)
                : default;
            var exactCandidate = guardianCandidates.FirstOrDefault(candidate =>
                candidate.PartySlot == frozenGuardian.Intent.PartySlot &&
                candidate.Actor == frozenGuardian.Intent.Actor);
            selectedGuardianRiskTier = DefensiveUtilityRules.ClassifyGuardianRisk(
                exactCandidate);
            selectedGuardianCurrentHp = exactCandidate.CurrentHp;
            selectedGuardianMaximumHp = exactCandidate.MaximumHp;
            selectedGuardianIncomingEnemyCount = exactCandidate.IncomingEnemyCount;
            var exactRetryContext = guardianConfigurationEnabled &&
                                    isCrystallineConflict &&
                                    currentIdentity == frozenGuardian.LocalPlayer &&
                                    input.ProbeSucceeded &&
                                    !input.IsTextInputActive &&
                                    inputFrame.IsFrozenGameplayKeyConsentValid(frozenGuardian.HeldKey) &&
                                    !guardActive &&
                                    IsPaladin(localPlayer!) &&
                                    DefensiveUtilityRules.IsGuardianCandidate(exactCandidate);
            if (!exactRetryContext)
            {
                frozenGuardianRetry = null;
                guardianSpentActors.Add(frozenGuardian.Intent.Actor);
                lastEvent =
                    $"Frozen Guardian P{frozenGuardian.Intent.PartySlot} " +
                    $"({DescribeGuardianRisk(frozenGuardian.Intent, guardianPressureAgeMilliseconds)}) " +
                    "retry cancelled by exact target/context drift";
            }
            else if (!higherPriorityClaimed &&
                     !inputFrame.IsConsumed &&
                     HeldActionRetryRules.RetainsSchedulerFrame(
                         frozenGuardian.Retry,
                         nowMilliseconds,
                         exactRetryContext,
                         guardianActionSpecificallyReady))
            {
                inputClaimed = true;
                inputFrame.Consume();
                if (!IsNativeBoundaryNearQueueable(localPlayer!))
                {
                    frozenGuardianRetry = frozenGuardian;
                    castCancellationRequest = CreateGuardianCastCancellationRequest(
                        localPlayer!,
                        frozenGuardian,
                        guardianActionSpecificallyReady);
                    lastEvent = castCancellationRequest is not null
                        ? $"Frozen Guardian P{frozenGuardian.Intent.PartySlot} " +
                          $"({DescribeGuardianRisk(exactCandidate, guardianPressureAgeMilliseconds)}) " +
                          "waiting for active cast cancellation"
                        : $"Frozen Guardian P{frozenGuardian.Intent.PartySlot} " +
                          $"({DescribeGuardianRisk(exactCandidate, guardianPressureAgeMilliseconds)}) " +
                          "waiting for global native boundary";
                }
                else if (!HeldActionRetryRules.CanAttemptFrozenIntent(
                             frozenGuardian.Retry,
                             nowMilliseconds))
                {
                    lastEvent =
                        $"Frozen Guardian P{frozenGuardian.Intent.PartySlot} " +
                        $"({DescribeGuardianRisk(exactCandidate, guardianPressureAgeMilliseconds)}) " +
                        "retaining retry throttle priority";
                }
                else
                {
                    var outcome = TryUseGuardianOnce(
                        localPlayer!,
                        frozenGuardian.Intent,
                        exactCandidate,
                        out attempted);
                    accepted = outcome == ClientActionAttemptOutcome.ClientAccepted;
                    CompleteGuardianAttempt(frozenGuardian, outcome, nowMilliseconds);
                    if (outcome == ClientActionAttemptOutcome.SoftUnavailable)
                    {
                        castCancellationRequest = CreateGuardianCastCancellationRequest(
                            localPlayer!,
                            frozenGuardian,
                            guardianActionSpecificallyReady);
                    }

                    if (accepted)
                    {
                        lastAcceptedGuardianEpisode = new AcceptedAutoGuardianEpisode(
                            NextGuardianEpisodeToken(),
                            Math.Max(nowMilliseconds, Environment.TickCount64),
                            frozenGuardian.LocalPlayer,
                            frozenGuardian.Intent.Actor,
                            frozenGuardian.Intent.PartySlot);
                    }

                    lastEvent = DescribeAttempt(
                        $"Guardian P{frozenGuardian.Intent.PartySlot} " +
                        $"({DescribeGuardianRisk(exactCandidate, guardianPressureAgeMilliseconds)}) retry",
                        frozenGuardian.Retry.NativeAttemptCount + 1,
                        frozenGuardian.Retry,
                        outcome);
                }
            }
        }
        else if (terminalGuardianKey == VirtualKey.NO_KEY &&
            canDispatch &&
            guardianActionSpecificallyReady &&
            IsPaladin(localPlayer!) &&
            eligibleGuardianCandidateCount > 0)
        {
            var selectedIndex = DefensiveUtilityRules.SelectGuardianCandidateIndex(
                guardianCandidates,
                guardianSpentActors);
            if (selectedIndex >= 0)
            {
                var selected = guardianCandidates[selectedIndex];
                action = DefensiveUtilityActionKind.Guardian;
                trigger = DefensiveUtilityTrigger.PaladinGuardianLowAlly;
                targetGameObjectId = selected.GameObjectId;
                targetEntityId = selected.EntityId;
                selectedGuardianPartySlot = selected.PartySlot;
                selectedGuardianRiskTier = DefensiveUtilityRules.ClassifyGuardianRisk(selected);
                selectedGuardianCurrentHp = selected.CurrentHp;
                selectedGuardianMaximumHp = selected.MaximumHp;
                selectedGuardianIncomingEnemyCount = selected.IncomingEnemyCount;
                inputClaimed = true;
                inputFrame.Consume();
                var frozen = new FrozenGuardianRetry(
                    new TargetPressureActorIdentity(
                        localPlayer!.GameObjectId,
                        localPlayer.EntityId),
                    selected,
                    selectedKey,
                    NextFrozenIntentEpochToken(),
                    HeldActionRetryState.Initial);
                _ = inputFrame.IsFrozenGameplayKeyConsentValid(selectedKey);
                if (!IsNativeBoundaryNearQueueable(localPlayer!))
                {
                    frozenGuardianRetry = frozen;
                    castCancellationRequest = CreateGuardianCastCancellationRequest(
                        localPlayer!,
                        frozen,
                        guardianActionSpecificallyReady);
                    lastEvent = castCancellationRequest is not null
                        ? $"Frozen Guardian P{selected.PartySlot} " +
                          $"({DescribeGuardianRisk(selected, guardianPressureAgeMilliseconds)}) " +
                          "waiting for active cast cancellation"
                        : $"Frozen Guardian P{selected.PartySlot} " +
                          $"({DescribeGuardianRisk(selected, guardianPressureAgeMilliseconds)}) " +
                          "waiting for global native boundary";
                }
                else
                {
                    var outcome = TryUseGuardianOnce(
                        localPlayer!,
                        selected,
                        selected,
                        out attempted);
                    accepted = outcome == ClientActionAttemptOutcome.ClientAccepted;
                    CompleteGuardianAttempt(frozen, outcome, nowMilliseconds);
                    if (outcome == ClientActionAttemptOutcome.SoftUnavailable)
                    {
                        castCancellationRequest = CreateGuardianCastCancellationRequest(
                            localPlayer!,
                            frozen,
                            guardianActionSpecificallyReady);
                    }

                    if (attempted && accepted)
                    {
                        lastAcceptedGuardianEpisode = new AcceptedAutoGuardianEpisode(
                            NextGuardianEpisodeToken(),
                            Math.Max(nowMilliseconds, Environment.TickCount64),
                            new TargetPressureActorIdentity(
                                localPlayer!.GameObjectId,
                                localPlayer.EntityId),
                            selected.Actor,
                            selected.PartySlot);
                    }

                    lastEvent = DescribeAttempt(
                        $"Guardian P{selected.PartySlot} " +
                        $"({DescribeGuardianRisk(selected, guardianPressureAgeMilliseconds)}) initial",
                        1,
                        frozen.Retry,
                        outcome);
                }
            }
        }

        if (attempted) Interlocked.Increment(ref attemptCount);
        if (accepted) Interlocked.Increment(ref acceptedCount);

        guardianPopup = DefensiveUtilityRules.ObserveGuardianTriggerPopup(
            guardianPopup,
            guardianConfigurationEnabled &&
            isCrystallineConflict &&
            localIdentityValid &&
            IsPaladin(localPlayer!),
            action,
            trigger,
            attempted,
            accepted,
            selectedGuardianPartySlot,
            nowMilliseconds,
            hardReset);

        var guardianActed = action == DefensiveUtilityActionKind.Guardian;
        var result = prior with
        {
            Active = prior.Active ||
                     guardianConfigurationEnabled &&
                     isCrystallineConflict &&
                     localIdentityValid,
            Action = guardianActed ? action : prior.Action,
            Trigger = guardianActed ? trigger : prior.Trigger,
            GuardActive = guardActive,
            GuardianCandidateCount = eligibleGuardianCandidateCount,
            GuardianCriticalCandidateCount = criticalGuardianCandidateCount,
            GuardianProactiveCandidateCount = proactiveGuardianCandidateCount,
            GuardianSelectedRiskTier = selectedGuardianRiskTier,
            GuardianSelectedCurrentHp = selectedGuardianCurrentHp,
            GuardianSelectedMaximumHp = selectedGuardianMaximumHp,
            GuardianSelectedIncomingEnemyCount = selectedGuardianIncomingEnemyCount,
            GuardianPressureAgeMilliseconds = guardianPressureAgeMilliseconds,
            TargetGameObjectId = guardianActed
                ? targetGameObjectId
                : prior.TargetGameObjectId,
            TargetEntityId = guardianActed
                ? targetEntityId
                : prior.TargetEntityId,
            FreshGameplayKey = guardianActed ? freshKey : prior.FreshGameplayKey,
            HeldGameplayKey = guardianActed ? heldKey : prior.HeldGameplayKey,
            InputClaimed = prior.InputClaimed || inputClaimed,
            CastCancellationRequest = inputClaimed
                ? castCancellationRequest
                : prior.CastCancellationRequest,
            UseActionAttempted = prior.UseActionAttempted || attempted,
            UseActionAccepted = prior.UseActionAccepted || accepted,
            AttemptCount = Interlocked.Read(ref attemptCount),
            AcceptedCount = Interlocked.Read(ref acceptedCount),
            LastAcceptedGuardianEpisode = lastAcceptedGuardianEpisode,
            GuardianPopup = guardianPopup,
            LastEvent = guardianActed || !prior.UseActionAttempted
                ? lastEvent
                : prior.LastEvent,
        };
        Volatile.Write(ref snapshot, result);
        return result;
    }

    private DefensiveUtilityProbeSnapshot CreateFrameSnapshotBase(bool guardActive) =>
        DefensiveUtilityProbeSnapshot.Initial with
        {
            Active = false,
            GuardActive = guardActive,
            AttemptCount = Interlocked.Read(ref attemptCount),
            AcceptedCount = Interlocked.Read(ref acceptedCount),
            GuardMetadataVerified = guardMetadataVerified,
            GuardianMetadataVerified = guardianMetadataVerified,
            LastAcceptedGuardianEpisode = lastAcceptedGuardianEpisode,
            GuardianPopup = guardianPopup,
            AutoGuardPopup = autoGuardPopup,
            LastEvent = "Frame initialized",
        };

    private static DefensiveUtilityProbeSnapshot MergePrioritizedGuardianPass(
        DefensiveUtilityProbeSnapshot guardPass,
        DefensiveUtilityProbeSnapshot guardianPass)
    {
        var aggregate = DefensiveUtilityRules.AggregateFramePasses(
            new DefensiveUtilityFramePass(
                guardianPass.Action,
                guardianPass.InputClaimed,
                guardianPass.UseActionAttempted,
                guardianPass.UseActionAccepted),
            new DefensiveUtilityFramePass(
                guardPass.Action,
                guardPass.InputClaimed,
                guardPass.UseActionAttempted,
                guardPass.UseActionAccepted));
        return guardPass with
        {
            Active = guardPass.Active || guardianPass.Active,
            Action = aggregate.GuardianOwnsPresentation
                ? guardianPass.Action
                : guardPass.Action,
            Trigger = aggregate.GuardianOwnsPresentation
                ? guardianPass.Trigger
                : guardPass.Trigger,
            GuardianCandidateCount = guardianPass.GuardianCandidateCount,
            GuardianCriticalCandidateCount = guardianPass.GuardianCriticalCandidateCount,
            GuardianProactiveCandidateCount = guardianPass.GuardianProactiveCandidateCount,
            GuardianSelectedRiskTier = guardianPass.GuardianSelectedRiskTier,
            GuardianSelectedCurrentHp = guardianPass.GuardianSelectedCurrentHp,
            GuardianSelectedMaximumHp = guardianPass.GuardianSelectedMaximumHp,
            GuardianSelectedIncomingEnemyCount =
                guardianPass.GuardianSelectedIncomingEnemyCount,
            GuardianPressureAgeMilliseconds =
                guardianPass.GuardianPressureAgeMilliseconds,
            TargetGameObjectId = aggregate.GuardianOwnsPresentation
                ? guardianPass.TargetGameObjectId
                : guardPass.TargetGameObjectId,
            TargetEntityId = aggregate.GuardianOwnsPresentation
                ? guardianPass.TargetEntityId
                : guardPass.TargetEntityId,
            FreshGameplayKey = aggregate.GuardianOwnsPresentation
                ? guardianPass.FreshGameplayKey
                : guardPass.FreshGameplayKey,
            HeldGameplayKey = aggregate.GuardianOwnsPresentation
                ? guardianPass.HeldGameplayKey
                : guardPass.HeldGameplayKey,
            InputClaimed = aggregate.InputClaimed,
            CastCancellationRequest = guardianPass.InputClaimed
                ? guardianPass.CastCancellationRequest
                : guardPass.CastCancellationRequest,
            UseActionAttempted = aggregate.UseActionAttempted,
            UseActionAccepted = aggregate.UseActionAccepted,
            LastAcceptedGuardianEpisode = guardianPass.LastAcceptedGuardianEpisode,
            GuardianPopup = guardianPass.GuardianPopup,
            AutoGuardPopup = guardPass.AutoGuardPopup,
            LastEvent = aggregate.GuardianOwnsPresentation
                ? guardianPass.LastEvent
                : guardPass.LastEvent,
        };
    }

    internal void Reset()
    {
        ResetRuntime();
        Volatile.Write(ref snapshot, DefensiveUtilityProbeSnapshot.Initial with
        {
            AttemptCount = Interlocked.Read(ref attemptCount),
            AcceptedCount = Interlocked.Read(ref acceptedCount),
            GuardMetadataVerified = guardMetadataVerified,
            GuardianMetadataVerified = guardianMetadataVerified,
            LastEvent = "Reset",
        });
    }

    internal DefensiveUtilityProbeSnapshot FailClosed(long nowMilliseconds, Exception? exception = null)
    {
        var failedGuardianKey = frozenGuardianRetry?.HeldKey ?? terminalGuardianKey;
        ResetOpportunityRuntime();
        terminalGuardianKey = failedGuardianKey;
        if (exception is not null) LogAttemptFailure(exception, nowMilliseconds);
        var guardSuppression = ObserveGuardSuppression(
            exactGuardActive: false,
            observedGuardAttemptAtMilliseconds: -1,
            Math.Max(0, nowMilliseconds));
        var failed = DefensiveUtilityProbeSnapshot.Initial with
        {
            GuardActive = guardSuppression.SuppressDirectActionHelpers,
            GuardPropagationLatchActive = guardSuppression.PropagationLatchActive,
            GuardPropagationLatchRemainingMilliseconds = guardSuppression.RemainingMilliseconds,
            AttemptCount = Interlocked.Read(ref attemptCount),
            AcceptedCount = Interlocked.Read(ref acceptedCount),
            GuardMetadataVerified = guardMetadataVerified,
            GuardianMetadataVerified = guardianMetadataVerified,
            LastEvent = "Failed closed",
        };
        Volatile.Write(ref snapshot, failed);
        return failed;
    }

    private void UpdatePostPurifyGuard(
        bool enabled,
        bool highPressureStunObserved,
        bool purifyUseActionAccepted,
        bool resilienceActive,
        bool hasPurifyRemovableCrowdControl,
        long nowMilliseconds)
    {
        if (!enabled || HasActiveGuard(objectTable.LocalPlayer) || nowMilliseconds < 0)
        {
            awaitingPostPurifyConfirmation = false;
            postPurifyGuardExpiresAt = -1;
            return;
        }

        if (highPressureStunObserved && purifyUseActionAccepted)
        {
            awaitingPostPurifyConfirmation = true;
            postPurifyGuardExpiresAt = SaturatingAdd(
                nowMilliseconds,
                DefensiveUtilityRules.PostPurifyGuardWindowMilliseconds);
        }

        if (postPurifyGuardExpiresAt <= nowMilliseconds)
        {
            awaitingPostPurifyConfirmation = false;
            postPurifyGuardExpiresAt = -1;
            return;
        }

        if (awaitingPostPurifyConfirmation &&
            resilienceActive &&
            !hasPurifyRemovableCrowdControl)
        {
            awaitingPostPurifyConfirmation = false;
        }
    }

    private unsafe List<PaladinGuardianCandidate> BuildGuardianCandidates(
        IPlayerCharacter localPlayer,
        long nowMilliseconds,
        out HashSet<TargetPressureActorIdentity> riskActors,
        out long pressureAgeMilliseconds)
    {
        riskActors = [];
        pressureAgeMilliseconds = -1;
        var pressureViewFresh =
            pressureTracker.TryCaptureIncomingAllyPressure(
                out var incomingPressureByActor,
                out var pressurePublishedAtMilliseconds) &&
            DefensiveUtilityRules.IsFreshGuardianPressurePublication(
                nowMilliseconds,
                pressurePublishedAtMilliseconds);
        if (pressureViewFresh)
            pressureAgeMilliseconds = nowMilliseconds - pressurePublishedAtMilliseconds;

        var candidates = new List<PaladinGuardianCandidate>(7);
        var sourceObject = GetNativeObject(localPlayer);
        foreach (var (slot, ally) in ResolveExactPartyMembers())
        {
            if (ally.GameObjectId == localPlayer.GameObjectId ||
                ally.EntityId == localPlayer.EntityId)
            {
                continue;
            }

            var actor = new TargetPressureActorIdentity(ally.GameObjectId, ally.EntityId);
            var targetObject = GetNativeObject(ally);
            var nativeTargetValid = sourceObject != null && targetObject != null;
            var rangeResult = nativeTargetValid
                ? ActionManager.GetActionInRangeOrLoS(
                    EnemyCombatConstants.GuardianActionId,
                    sourceObject,
                    targetObject)
                : uint.MaxValue;
            int? incomingPressure = pressureViewFresh &&
                                    incomingPressureByActor.TryGetValue(
                                        actor,
                                        out var uniqueEnemyCount) &&
                                    uniqueEnemyCount is >= 0 and <= EnemySlotRules.LastSlot
                ? uniqueEnemyCount
                : null;
            var candidate = new PaladinGuardianCandidate(
                ally.GameObjectId,
                ally.EntityId,
                slot,
                ally.CurrentHp,
                ally.MaxHp,
                incomingPressure,
                Vector3.DistanceSquared(localPlayer.Position, ally.Position),
                IsExactPartyMember: true,
                IsSelf: false,
                IsAlive: IsLivePlayer(ally),
                ally.IsTargetable,
                nativeTargetValid,
                SeitonRangeRules.HasNativeRangeAndLineOfSight(rangeResult));
            candidates.Add(candidate);
            if (IsLivePlayer(ally) &&
                DefensiveUtilityRules.ClassifyGuardianRisk(candidate) !=
                PaladinGuardianRiskTier.None)
            {
                riskActors.Add(actor);
            }
        }

        return candidates;
    }

    private unsafe GuardUseAttemptResult TryUseGuardOnce(
        IPlayerCharacter localPlayer,
        bool allowOccupiedNativeQueue)
    {
        if (!guardMetadataVerified ||
            !nearAssist.CanProtectAutomaticGuard ||
            !HasValidLocalPlayer(localPlayer) ||
            nearAssist.IsExactLocalGuardActiveOrPropagating(
                new(localPlayer.GameObjectId, localPlayer.EntityId)))
        {
            return GuardUseAttemptResult.NotInvoked;
        }

        var actionManager = ActionManager.Instance();
        if (actionManager == null ||
            actionManager->GetAdjustedActionId(EnemyCombatConstants.GuardActionId) !=
            EnemyCombatConstants.GuardActionId)
        {
            return GuardUseAttemptResult.NotInvoked;
        }

        var criticalBoundary = ClientActionAttemptBoundary.Capture(
            actionManager,
            EnemyCombatConstants.GuardActionId);
        if (!criticalBoundary.IsCriticalRecoveryActionReady(
                EnemyCombatConstants.GuardActionId,
                allowOccupiedNativeQueue) ||
            !IsCriticalGuardBoundaryNearQueueable(
                localPlayer,
                actionManager,
                allowOccupiedNativeQueue))
        {
            return new GuardUseAttemptResult(
                ClientActionAttemptOutcome.SoftUnavailable,
                -1);
        }

        var generationBeforeCall = nearAssist.CaptureLocalGuardAttemptGeneration();
        var boundaryBefore = ClientActionAttemptBoundary.Capture(
            actionManager,
            EnemyCombatConstants.GuardActionId);
        try
        {
            var accepted = nearAssist.RunWithoutRedirect(() =>
                actionManager->UseAction(
                    ActionType.Action,
                    EnemyCombatConstants.GuardActionId,
                    localPlayer.GameObjectId,
                    0,
                    ActionManager.UseActionMode.None,
                    0));
            var outcome = ClientActionAttemptBoundaryRules.ClassifyCriticalRecovery(
                accepted,
                EnemyCombatConstants.GuardActionId,
                boundaryBefore,
                ClientActionAttemptBoundary.Capture(
                    actionManager,
                    EnemyCombatConstants.GuardActionId),
                allowOccupiedNativeQueue);
            if (outcome == ClientActionAttemptOutcome.ClientRejected)
            {
                nearAssist.TryRetractClientRejectedLocalGuardAttempt(
                    localPlayer.GameObjectId,
                    localPlayer.EntityId,
                    generationBeforeCall);
            }

            if (outcome == ClientActionAttemptOutcome.ClientAccepted)
            {
                var acceptedAt = Environment.TickCount64;
                ObserveGuardSuppression(
                    exactGuardActive: false,
                    observedGuardAttemptAtMilliseconds: acceptedAt,
                    nowMilliseconds: acceptedAt);
            }

            return new GuardUseAttemptResult(outcome, generationBeforeCall);
        }
        catch (Exception exception)
        {
            LogAttemptFailure(exception, Environment.TickCount64);
            return new GuardUseAttemptResult(
                ClientActionAttemptOutcome.AcceptanceUnknown,
                generationBeforeCall);
        }
    }

    private static unsafe AutoGuardRetryReadiness ObserveGuardRetryReadiness(
        IPlayerCharacter? localPlayer,
        bool allowOccupiedNativeQueue)
    {
        if (!HasValidLocalPlayer(localPlayer) || HasActiveGuard(localPlayer))
            return AutoGuardRetryReadiness.Unknown;

        var actionManager = ActionManager.Instance();
        if (actionManager == null) return AutoGuardRetryReadiness.Unknown;

        var readiness = ClientActionAttemptBoundary.Capture(
            actionManager,
            EnemyCombatConstants.GuardActionId);
        if (!readiness.Captured ||
            readiness.AdjustedActionId != EnemyCombatConstants.GuardActionId ||
            readiness.ResourceStatus != 0)
        {
            return AutoGuardRetryReadiness.Unknown;
        }

        if (!readiness.IsActionOffCooldown)
            return AutoGuardRetryReadiness.CooldownUnavailable;

        return readiness.IsCriticalRecoveryActionReady(
                EnemyCombatConstants.GuardActionId,
                allowOccupiedNativeQueue)
            ? AutoGuardRetryReadiness.Ready
            : AutoGuardRetryReadiness.NativeBoundaryBusy;
    }

    private unsafe ClientActionAttemptOutcome TryUseGuardianOnce(
        IPlayerCharacter localPlayer,
        PaladinGuardianCandidate intent,
        PaladinGuardianCandidate currentCandidate,
        out bool attempted)
    {
        attempted = false;
        if (!guardMetadataVerified ||
            !guardianMetadataVerified ||
            !HasValidLocalPlayer(localPlayer) ||
            !IsPaladin(localPlayer) ||
            nearAssist.IsExactLocalGuardActiveOrPropagating(
                new(localPlayer.GameObjectId, localPlayer.EntityId)) ||
            currentCandidate.Actor != intent.Actor ||
            currentCandidate.PartySlot != intent.PartySlot)
        {
            return ClientActionAttemptOutcome.NotInvoked;
        }

        var actionManager = ActionManager.Instance();
        if (actionManager == null ||
            actionManager->GetAdjustedActionId(EnemyCombatConstants.GuardActionId) !=
            EnemyCombatConstants.GuardActionId ||
            actionManager->GetAdjustedActionId(EnemyCombatConstants.GuardianActionId) !=
            EnemyCombatConstants.GuardianActionId)
        {
            return ClientActionAttemptOutcome.NotInvoked;
        }

        if (!IsGuardianActionSpecificallyReady(localPlayer) ||
            !ClientActionAttemptBoundary.IsExactActionReady(
                actionManager,
                EnemyCombatConstants.GuardianActionId) ||
            !IsNativeBoundaryNearQueueable(localPlayer, actionManager))
        {
            return ClientActionAttemptOutcome.SoftUnavailable;
        }

        foreach (var (slot, ally) in ResolveExactPartyMembers())
        {
            if (slot != intent.PartySlot ||
                ally.GameObjectId != intent.GameObjectId ||
                ally.EntityId != intent.EntityId)
            {
                continue;
            }

            var sourceObject = GetNativeObject(localPlayer);
            var targetObject = GetNativeObject(ally);
            var nativeTargetValid = sourceObject != null && targetObject != null;
            var rangeResult = nativeTargetValid
                ? ActionManager.GetActionInRangeOrLoS(
                    EnemyCombatConstants.GuardianActionId,
                    sourceObject,
                    targetObject)
                : uint.MaxValue;
            var revalidated = intent with
            {
                CurrentHp = ally.CurrentHp,
                MaximumHp = ally.MaxHp,
                IncomingEnemyCount = currentCandidate.IncomingEnemyCount,
                DistanceSquared = Vector3.DistanceSquared(localPlayer.Position, ally.Position),
                IsAlive = IsLivePlayer(ally),
                IsTargetable = ally.IsTargetable,
                HasValidNativeTarget = nativeTargetValid,
                HasNativeRangeAndLineOfSight =
                    SeitonRangeRules.HasNativeRangeAndLineOfSight(rangeResult),
            };
            if (!DefensiveUtilityRules.IsGuardianCandidate(revalidated))
                return ClientActionAttemptOutcome.NotInvoked;

            if (!IsGuardianActionSpecificallyReady(localPlayer) ||
                !ClientActionAttemptBoundary.IsExactActionReady(
                    actionManager,
                    EnemyCombatConstants.GuardianActionId))
            {
                return ClientActionAttemptOutcome.SoftUnavailable;
            }

            var boundaryBefore = ClientActionAttemptBoundary.Capture(
                actionManager,
                EnemyCombatConstants.GuardianActionId);
            try
            {
                var accepted = OwnGuardActionBoundary.Invoke(
                    () => nearAssist.IsExactLocalGuardActiveOrPropagating(
                        new(localPlayer.GameObjectId, localPlayer.EntityId)),
                    () => nearAssist.RunWithoutRedirect(() =>
                        actionManager->UseAction(
                            ActionType.Action,
                            EnemyCombatConstants.GuardianActionId,
                            ally.GameObjectId,
                            0,
                            ActionManager.UseActionMode.None,
                            0)),
                    out attempted);
                if (!attempted) return ClientActionAttemptOutcome.SoftUnavailable;
                return ClientActionAttemptBoundaryRules.Classify(
                    accepted,
                    EnemyCombatConstants.GuardianActionId,
                    boundaryBefore,
                    ClientActionAttemptBoundary.Capture(
                        actionManager,
                        EnemyCombatConstants.GuardianActionId));
            }
            catch (Exception exception)
            {
                LogAttemptFailure(exception, Environment.TickCount64);
                return ClientActionAttemptOutcome.AcceptanceUnknown;
            }
        }

        return ClientActionAttemptOutcome.NotInvoked;
    }

    private IReadOnlyList<(int Slot, IPlayerCharacter Player)> ResolveExactPartyMembers()
    {
        var resolved = new List<(int Slot, IPlayerCharacter Player)>(8);
        for (var slot = 1; slot <= 8; slot++)
        {
            var player = PartySlotResolver.Resolve(objectTable, slot);
            if (HasValidNativeIdentity(player)) resolved.Add((slot, player!));
        }

        var duplicateGameIds = resolved
            .GroupBy(static item => item.Player.GameObjectId)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToHashSet();
        var duplicateEntityIds = resolved
            .GroupBy(static item => item.Player.EntityId)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToHashSet();
        return resolved
            .Where(item =>
                !duplicateGameIds.Contains(item.Player.GameObjectId) &&
                !duplicateEntityIds.Contains(item.Player.EntityId))
            .ToArray();
    }

    internal bool CanUseGuardianNow(TargetPressureActorIdentity expectedLocalPlayer)
    {
        var localPlayer = objectTable.LocalPlayer;
        return expectedLocalPlayer.IsValid &&
               HasValidLocalPlayer(localPlayer) &&
               new TargetPressureActorIdentity(localPlayer!.GameObjectId, localPlayer.EntityId) == expectedLocalPlayer &&
               !nearAssist.IsExactLocalGuardActiveOrPropagating(expectedLocalPlayer) &&
               IsGuardianActionSpecificallyReady(localPlayer);
    }

    private bool IsGuardianActionSpecificallyReady(IPlayerCharacter? localPlayer) =>
        guardMetadataVerified && guardianMetadataVerified &&
        HasValidLocalPlayer(localPlayer) && IsPaladin(localPlayer!) &&
        IsActionSpecificallyReady(EnemyCombatConstants.GuardianActionId) &&
        DefensiveUtilityRules.CanUseGuardianWithGuardOrHighResources(
            IsActionSpecificallyReady(EnemyCombatConstants.GuardActionId),
            localPlayer!.CurrentHp, localPlayer.MaxHp,
            localPlayer.CurrentMp, localPlayer.MaxMp,
            configuration.GuardianNoGuardMinimumHpPercent,
            configuration.GuardianNoGuardMinimumMpPercent);

    private static unsafe bool IsActionSpecificallyReady(uint actionId)
    {
        var actionManager = ActionManager.Instance();
        return actionManager != null &&
               actionManager->GetAdjustedActionId(actionId) == actionId &&
               actionManager->IsActionOffCooldown(ActionType.Action, actionId) &&
               actionManager->CheckActionResources(ActionType.Action, actionId) == 0;
    }

    private static unsafe bool IsNativeBoundaryNearQueueable(IPlayerCharacter localPlayer)
    {
        var actionManager = ActionManager.Instance();
        return actionManager != null &&
               IsNativeBoundaryNearQueueable(localPlayer, actionManager);
    }

    private static unsafe bool IsNativeBoundaryNearQueueable(
        IPlayerCharacter localPlayer,
        ActionManager* actionManager) =>
        HeldActionRetryRules.IsNativeBoundaryNearQueueable(
            actionManager->AnimationLock,
            localPlayer.IsCasting,
            actionManager->CastActionId,
            actionManager->ActionQueued);

    private static unsafe bool IsCriticalGuardBoundaryNearQueueable(
        IPlayerCharacter localPlayer,
        bool allowOccupiedNativeQueue)
    {
        var actionManager = ActionManager.Instance();
        return actionManager != null &&
               IsCriticalGuardBoundaryNearQueueable(
                   localPlayer,
                   actionManager,
                   allowOccupiedNativeQueue);
    }

    private static unsafe bool IsCriticalGuardBoundaryNearQueueable(
        IPlayerCharacter localPlayer,
        ActionManager* actionManager,
        bool allowOccupiedNativeQueue) =>
        HeldActionRetryRules.IsNativeBoundaryNearQueueable(
            actionManager->AnimationLock,
            localPlayer.IsCasting,
            actionManager->CastActionId,
            actionManager->ActionQueued,
            allowOccupiedNativeQueue);

    private static unsafe ClientActionAttemptFingerprint CaptureGuardNativeBoundary()
    {
        var actionManager = ActionManager.Instance();
        return ClientActionAttemptBoundary.Capture(
            actionManager,
            EnemyCombatConstants.GuardActionId);
    }

    private static bool IsRelevantGuardNativeBoundaryEdge(
        ClientActionAttemptFingerprint previous,
        ClientActionAttemptFingerprint current,
        bool allowOccupiedNativeQueue) =>
        ClientActionAttemptBoundaryRules.BecameCriticalRecoveryReady(
            EnemyCombatConstants.GuardActionId,
            previous,
            current,
            allowOccupiedNativeQueue);

    private static unsafe bool IsCastCancellationBoundaryReady(IPlayerCharacter localPlayer)
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

    private static HeldCastCancellationRequest? CreateGuardianCastCancellationRequest(
        IPlayerCharacter localPlayer,
        FrozenGuardianRetry frozen,
        bool actionsSpecificallyReady)
    {
        if (!actionsSpecificallyReady || !IsCastCancellationBoundaryReady(localPlayer))
            return null;

        var request = new HeldCastCancellationRequest(
            HeldCastCancellationHelperKind.Guardian,
            EnemyCombatConstants.GuardianActionId,
            frozen.LocalPlayer,
            frozen.Intent.Actor,
            (int)frozen.HeldKey,
            frozen.IntentEpochToken);
        return request.IsValid ? request : null;
    }

    internal static bool HasActiveGuard(IPlayerCharacter? player)
    {
        if (player is null) return false;
        foreach (var status in player.StatusList)
        {
            // A live Guard slot is authoritative until removed. A rounded zero
            // or unreadable duration must not authorize a Guard-breaking helper.
            if (DefensiveUtilityRules.IsOwnGuardStatusPresent(status.StatusId))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPaladin(IPlayerCharacter player) =>
        player.ClassJob.IsValid &&
        player.ClassJob.RowId == EnemyCombatConstants.PaladinJobId;

    private static bool IsLivePlayer(IPlayerCharacter? player) =>
        HasValidNativeIdentity(player) &&
        player!.IsTargetable &&
        !player.IsDead &&
        player.CurrentHp > 0 &&
        player.MaxHp >= player.CurrentHp;

    private static bool HasValidLocalPlayer(IPlayerCharacter? player) =>
        IsLivePlayer(player) &&
        player!.GameObjectId is not 0 and not 0xE0000000;

    private static unsafe bool HasValidNativeIdentity(IPlayerCharacter? player)
    {
        if (player is null ||
            player.Address == 0 ||
            player.EntityId is 0 or 0xE0000000 ||
            player.GameObjectId is 0 or 0xE0000000)
        {
            return false;
        }

        var native = (GameObject*)player.Address;
        return native != null && native->EntityId == player.EntityId;
    }

    private static unsafe GameObject* GetNativeObject(IPlayerCharacter player)
    {
        var native = (GameObject*)player.Address;
        return native != null && native->EntityId == player.EntityId ? native : null;
    }

    private static (bool Guard, bool Guardian) ValidateMetadata(
        IDataManager dataManager,
        IPluginLog log)
    {
        try
        {
            var actions = dataManager.GetExcelSheet<GameAction>(ClientLanguage.English);
            var descriptions = dataManager.GetExcelSheet<ActionTransient>(ClientLanguage.English);
            var statuses = dataManager.GetExcelSheet<GameStatus>(ClientLanguage.English);
            var guard = actions.TryGetRow(EnemyCombatConstants.GuardActionId, out var guardAction) &&
                        descriptions.TryGetRow(EnemyCombatConstants.GuardActionId, out var guardTransient) &&
                        statuses.TryGetRow(EnemyCombatConstants.GuardStatusId, out var guardStatus) &&
                        statuses.TryGetRow(
                            EnemyCombatConstants.GuardStatusAlternateId,
                            out var alternateGuardStatus) &&
                        IsExpectedGuard(
                            guardAction,
                            guardTransient,
                            guardStatus,
                            alternateGuardStatus);
            var guardian = actions.TryGetRow(
                               EnemyCombatConstants.GuardianActionId,
                               out var guardianAction) &&
                           descriptions.TryGetRow(
                               EnemyCombatConstants.GuardianActionId,
                               out var guardianTransient) &&
                           IsExpectedGuardian(guardianAction, guardianTransient);
            if (!guard || !guardian)
            {
                log.Warning(
                    "Seiton Sense defensive utility metadata failed closed: Guard={Guard}, Guardian={Guardian}.",
                    guard,
                    guardian);
            }

            return (guard, guardian);
        }
        catch (Exception exception)
        {
            log.Warning(exception, "Seiton Sense defensive utility metadata lookup failed closed.");
            return (false, false);
        }
    }

    private static bool IsExpectedGuard(
        GameAction action,
        ActionTransient transient,
        GameStatus guardStatus,
        GameStatus alternateGuardStatus)
    {
        var description = transient.Description.ToString();
        return action.Name.ToString() == "Guard" &&
               action.Icon == EnemyCombatConstants.GuardIconId &&
               action.IsPvP &&
               action.IsPlayerAction &&
               action.Range == 0 &&
               action.EffectRange == 0 &&
               action.Cast100ms == 0 &&
               action.Recast100ms == 300 &&
               action.CanTargetSelf &&
               !action.CanTargetParty &&
               !action.CanTargetAlly &&
               !action.CanTargetAlliance &&
               !action.CanTargetHostile &&
               !action.TargetArea &&
               !action.AffectsPosition &&
               description.Contains("Reduces damage taken by 99%", StringComparison.Ordinal) &&
               description.Contains(
                   "Effect ends upon reuse, using another action",
                   StringComparison.Ordinal) &&
               guardStatus.Name.ToString() == "Guard" &&
               alternateGuardStatus.Name.ToString() == "Guard";
    }

    private static bool IsExpectedGuardian(GameAction action, ActionTransient transient)
    {
        var description = transient.Description.ToString();
        return action.Name.ToString() == "Guardian" &&
               action.Icon == EnemyCombatConstants.GuardianIconId &&
               action.IsPvP &&
               action.IsPlayerAction &&
               action.ClassJob.IsValid &&
               action.ClassJob.RowId == EnemyCombatConstants.PaladinJobId &&
               action.Range == EnemyCombatConstants.GuardianSheetRange &&
               action.EffectRange == 0 &&
               action.Cast100ms == 0 &&
               action.Recast100ms == EnemyCombatConstants.GuardianRecast100ms &&
               !action.CanTargetSelf &&
               action.CanTargetParty &&
               !action.CanTargetAlly &&
               !action.CanTargetAlliance &&
               !action.CanTargetHostile &&
               !action.TargetArea &&
               action.RequiresLineOfSight &&
               action.AffectsPosition &&
               description.Contains("Take all damage intended for the targeted party member", StringComparison.Ordinal) &&
               description.Contains("Duration: 8s", StringComparison.Ordinal) &&
               description.Contains("closer than 10 yalms", StringComparison.Ordinal) &&
               description.Contains("Cannot be executed while bound", StringComparison.Ordinal);
    }

    private void ResetRuntime()
    {
        ResetGuardOpportunityRuntime();
        ResetGuardianOpportunityRuntime();
        guardPropagationState = GuardPropagationState.Initial;
        lastAcceptedGuardianEpisode = null;
        autoGuardPopup = null;
    }

    private void ResetOpportunityRuntime()
    {
        ResetGuardOpportunityRuntime();
        ResetGuardianOpportunityRuntime();
    }

    private void ResetGuardOpportunityRuntime()
    {
        awaitingPostPurifyConfirmation = false;
        postPurifyGuardExpiresAt = -1;
        frozenGuardRetry = null;
        lastGuardNativeBoundary = default;
        lastGuardNativeAttemptFrameId = -1;
        autoGuardConfirmationState = AutoGuardConfirmationState.Initial;
    }

    private long NextAutoGuardNotificationToken()
    {
        if (autoGuardNotificationToken == long.MaxValue)
            autoGuardNotificationToken = 0;
        autoGuardNotificationToken++;
        return autoGuardNotificationToken;
    }

    private void ResetGuardianOpportunityRuntime()
    {
        guardianSpentActors.Clear();
        frozenGuardianRetry = null;
        terminalGuardianKey = VirtualKey.NO_KEY;
        guardianPopup = null;
    }

    private void CompleteGuardAttempt(
        FrozenGuardRetry frozen,
        GuardUseAttemptResult result,
        long nowMilliseconds)
    {
        if (result.Outcome == ClientActionAttemptOutcome.ClientAccepted)
        {
            frozenGuardRetry = null;
            autoGuardConfirmationState = AutoGuardConfirmationRules.ArmProvisional(
                result.GenerationBeforeCall,
                nearAssist.CurrentTerritoryId,
                frozen.LocalPlayer,
                Math.Max(nowMilliseconds, Environment.TickCount64),
                frozen.ExpiresAtMilliseconds,
                confirmationRetrySpent: false);
            if (autoGuardConfirmationState.IsPending) return;

            awaitingPostPurifyConfirmation = false;
            postPurifyGuardExpiresAt = -1;
            return;
        }

        var completion = HeldActionRetryRules.Complete(
            frozen.Retry,
            Math.Max(0, nowMilliseconds),
            result.Outcome);
        if (completion.RetryScheduled ||
            completion.Disposition == HeldActionRetryDisposition.SoftWait)
        {
            frozenGuardRetry = frozen with { Retry = completion.NextState };
            return;
        }

        frozenGuardRetry = null;
        awaitingPostPurifyConfirmation = false;
        postPurifyGuardExpiresAt = -1;
    }

    private void CompleteGuardianAttempt(
        FrozenGuardianRetry frozen,
        ClientActionAttemptOutcome outcome,
        long nowMilliseconds)
    {
        var completion = HeldActionRetryRules.Complete(
            frozen.Retry,
            Math.Max(0, nowMilliseconds),
            outcome);
        if (completion.RetryScheduled ||
            completion.Disposition == HeldActionRetryDisposition.SoftWait)
        {
            frozenGuardianRetry = frozen with { Retry = completion.NextState };
            return;
        }

        frozenGuardianRetry = null;
        if (HeldActionRetryRules.ShouldLatchHeldKeyUntilRelease(completion.Disposition))
            terminalGuardianKey = frozen.HeldKey;
        guardianSpentActors.Add(frozen.Intent.Actor);
    }

    private static void ReleaseTerminalKeyWhenUp(
        EmergencyActionInputFrame inputFrame,
        ref VirtualKey key)
    {
        if (key != VirtualKey.NO_KEY && !inputFrame.IsGameplayKeyPhysicallyDown(key))
            key = VirtualKey.NO_KEY;
    }

    private static string DescribeAttempt(
        string label,
        int attempt,
        HeldActionRetryState retryState,
        ClientActionAttemptOutcome outcome) =>
        $"{label} attempt {attempt}/{HeldActionRetryRules.ResolveAttemptLimit(retryState)}: {outcome}";

    private long NextGuardianEpisodeToken()
    {
        while (true)
        {
            var current = Volatile.Read(ref guardianEpisodeToken);
            if (current == long.MaxValue) return long.MaxValue;

            var next = current + 1;
            if (Interlocked.CompareExchange(ref guardianEpisodeToken, next, current) == current)
                return next;
        }
    }

    private ulong NextFrozenIntentEpochToken()
    {
        while (true)
        {
            var current = Volatile.Read(ref frozenIntentEpochToken);
            var next = current >= long.MaxValue ? 1 : current + 1;
            if (Interlocked.CompareExchange(ref frozenIntentEpochToken, next, current) == current)
                return (ulong)next;
        }
    }

    private static string DescribeWaitingState(
        bool configurationEnabled,
        bool isCrystallineConflict,
        bool localIdentityValid,
        bool guardActive,
        bool higherPriorityClaimed,
        bool pressureKnown,
        int incomingEnemyCount)
    {
        if (!configurationEnabled) return "Disabled";
        if (!isCrystallineConflict) return "Outside Crystalline Conflict";
        if (!localIdentityValid) return "Local player invalid";
        if (guardActive) return "Active or propagating Guard blocks every plugin-owned action";
        if (higherPriorityClaimed) return "Waiting behind higher-priority survival helper";
        if (!pressureKnown) return "Pressure unknown";
        return $"Waiting; self pressure={incomingEnemyCount}";
    }

    private static string DescribeGuardianWaitingState(
        bool configurationEnabled,
        bool isCrystallineConflict,
        bool localIdentityValid,
        bool guardActive,
        bool higherPriorityClaimed,
        int criticalCandidateCount,
        int proactiveCandidateCount,
        long pressureAgeMilliseconds)
    {
        if (!configurationEnabled) return "Paladin Guardian disabled";
        if (!isCrystallineConflict) return "Paladin Guardian outside Crystalline Conflict";
        if (!localIdentityValid) return "Paladin Guardian local player invalid";
        if (guardActive) return "Active or propagating Guard blocks Guardian";
        if (higherPriorityClaimed) return "Guardian waiting behind higher-priority survival helper";
        var pressureAge = pressureAgeMilliseconds >= 0
            ? $"{pressureAgeMilliseconds}ms"
            : "unknown";
        return $"Guardian waiting; exact candidates=critical:{criticalCandidateCount}/" +
               $"proactive:{proactiveCandidateCount}, pressure-age={pressureAge}, gameplay key required";
    }

    private static string DescribeGuardianRisk(
        PaladinGuardianCandidate candidate,
        long pressureAgeMilliseconds)
    {
        var tier = DefensiveUtilityRules.ClassifyGuardianRisk(candidate);
        var pressure = candidate.IncomingEnemyCount?.ToString() ?? "unknown";
        var age = candidate.IncomingEnemyCount.HasValue && pressureAgeMilliseconds >= 0
            ? $"{pressureAgeMilliseconds}ms"
            : "unknown";
        return $"tier={tier}, HP={candidate.CurrentHp}/{candidate.MaximumHp}, " +
               $"pressure={pressure}, pressure-age={age}";
    }

    private void LogAttemptFailure(Exception exception, long nowMilliseconds)
    {
        if (nowMilliseconds < nextErrorLogAt) return;
        nextErrorLogAt = nowMilliseconds + 10_000;
        log.Error(
            exception,
            "Seiton Sense defensive utility attempt ended with ambiguous acceptance.");
    }

    private static long SaturatingAdd(long left, long right) =>
        left > long.MaxValue - right ? long.MaxValue : left + right;

    private readonly record struct FrozenGuardRetry(
        TargetPressureActorIdentity LocalPlayer,
        long ExpiresAtMilliseconds,
        HeldActionRetryState Retry);

    private readonly record struct FrozenGuardianRetry(
        TargetPressureActorIdentity LocalPlayer,
        PaladinGuardianCandidate Intent,
        VirtualKey HeldKey,
        ulong IntentEpochToken,
        HeldActionRetryState Retry);
}
