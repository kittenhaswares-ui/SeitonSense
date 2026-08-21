using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using SeitonSense.Core;

namespace SeitonSense.Plugin.Services;

internal sealed record MiracleInterceptProbeSnapshot(
    string Phase,
    MiracleInterceptThreatKind Threat,
    ulong TargetGameObjectId,
    uint TargetEntityId,
    uint TargetJobId,
    long ThreatRemainingMilliseconds,
    bool HardenedScalesPresent,
    bool OtherCcProtectionPresent,
    bool HasNativeRangeAndLineOfSight,
    VirtualKey InputKey,
    bool UseActionAttempted,
    bool UseActionAccepted,
    long AttemptCount,
    long AcceptedCount,
    int CaptureQueueDepth,
    long CapturedThreatCount,
    long DroppedThreatCount,
    MiracleInterceptConfirmationPopup? ConfirmationPopup,
    long ConfirmedLandingCount,
    int ConfirmationQueueDepth,
    long CapturedConfirmationCount,
    long DroppedConfirmationCount,
    string LastEvent)
{
    internal bool InputClaimed { get; init; }
    internal HeldCastCancellationRequest? CastCancellationRequest { get; init; }
    internal long RecognizedThreatCount { get; init; }
    internal long ArmedThreatCount { get; init; }
    internal long RejectedThreatCount { get; init; }
    internal long PriorityWaitCount { get; init; }
    internal long NoInputWaitCount { get; init; }
    internal long RangeWaitCount { get; init; }
    internal long ProtectionWaitCount { get; init; }
    internal long ExpiredThreatCount { get; init; }
    internal string LastOpportunity { get; init; } = "None observed";
    internal MiracleCleanseFollowupPhase CleanseFollowupPhase { get; init; }
    internal ulong CleanseFollowupTargetGameObjectId { get; init; }
    internal uint CleanseFollowupTargetEntityId { get; init; }
    internal bool CleanseFollowupResilienceObserved { get; init; }
    internal long CleanseFollowupSignalCount { get; init; }
    internal long CleanseFollowupPromotionCount { get; init; }
    internal long CleanseFollowupCancellationCount { get; init; }
    internal string CleanseFollowupLastEvent { get; init; } = "None observed";
    internal uint CounterActionId { get; init; }
    internal uint CleanseFollowupRemovedStatusId { get; init; }
    internal int CleanseFollowupTeamPressure { get; init; }
    internal int CleanseFollowupTrackedCount { get; init; }
    internal int CleanseFollowupReleaseReadyCount { get; init; }
    internal int GuardFollowupTrackedCount { get; init; }
    internal int GuardFollowupReleaseReadyCount { get; init; }
    internal ulong GuardFollowupTargetGameObjectId { get; init; }
    internal uint GuardFollowupTargetEntityId { get; init; }
    internal int GuardFollowupTeamPressure { get; init; }
    internal long GuardFollowupEpisodeCount { get; init; }
    internal long GuardFollowupPromotionCount { get; init; }
    internal long GuardFollowupExpiredCount { get; init; }
    internal long GuardFollowupRetiredCount { get; init; }
    internal string GuardFollowupLastEvent { get; init; } = "None observed";
    internal bool ProtectionEndHeldConsentActive { get; init; }
    internal VirtualKey ProtectionEndHeldConsentKey { get; init; }
    internal bool ProtectionEndRankTeamPressureKnown { get; init; }
    internal int ProtectionEndRankTeamPressure { get; init; }
    internal uint ProtectionEndRankCurrentHp { get; init; }
    internal uint ProtectionEndRankMaximumHp { get; init; }
    internal bool ProtectionEndRankMpKnown { get; init; }
    internal uint ProtectionEndRankCurrentMp { get; init; }
    internal uint ProtectionEndRankMaximumMp { get; init; }
    internal int ConfirmationPendingCount { get; init; }

    internal static MiracleInterceptProbeSnapshot Initial { get; } = new(
        "Waiting",
        MiracleInterceptThreatKind.None,
        0,
        0,
        0,
        0,
        false,
        false,
        false,
        VirtualKey.NO_KEY,
        false,
        false,
        0,
        0,
        0,
        0,
        0,
        null,
        0,
        0,
        0,
        0,
        "Not started");
}

/// <summary>
/// Experimental CC-only WHM/BRD held helper. It freezes one exact threat,
/// target, action, and physical key. Only proven client-false calls may retry
/// inside the bounded event lease; it never changes the selected target.
/// </summary>
internal sealed class MiracleInterceptProbe
{
    private const int MaximumRememberedSignals = 128;
    private const long MaximumTeamPressureAgeMilliseconds = 250;
    private static readonly uint[] RequiredMiracleProtectionStatusIds =
        CcImmunityBrakeActionCatalog
            .GetBlockerStatusIds(CcImmunityBrakeBlockerFamily.Miracle)
            .Append(EnemyCombatConstants.HardenedScalesStatusId)
            .Distinct()
            .ToArray();
    private static readonly uint[] RequiredSilentProtectionStatusIds =
        CcImmunityBrakeActionCatalog
            .GetBlockerStatusIds(CcImmunityBrakeBlockerFamily.StandardPurifyCc)
            .Distinct()
            .ToArray();

    private readonly IObjectTable objectTable;
    private readonly ExecuteTracker executeTracker;
    private readonly TargetPressureTracker pressureTracker;
    private readonly NearAssistRedirector nearAssist;
    private readonly MachinistLimitBreakCapture capture;
    private readonly IPluginLog log;
    private readonly IReadOnlySet<uint> verifiedProtectionStatusIds;
    private readonly bool silentNocturneMetadataVerified;
    private readonly bool contradanceMetadataVerified;
    private readonly HashSet<MiracleSignalIdentity> rememberedSignals = [];
    private readonly Queue<MiracleSignalIdentity> rememberedSignalOrder = [];
    private readonly Dictionary<int, MiracleCleanseFollowupState> cleanseFollowupStates = [];
    private MiracleThreatState? activeThreat;
    private bool inputClaimedThisFrame;
    private HeldCastCancellationRequest? castCancellationRequestThisFrame;
    private MiracleInterceptConfirmationState confirmationState =
        MiracleInterceptConfirmationState.Initial;
    private MiracleGuardFollowupState guardFollowupState =
        MiracleGuardFollowupState.Initial;
    private MiracleProtectionEndHeldConsentState protectionEndHeldConsent =
        MiracleProtectionEndHeldConsentState.Initial;
    private MiracleCleanseFollowupSignalLedger cleanseFollowupSignalLedger =
        MiracleCleanseFollowupSignalLedger.Initial;
    private MiracleInterceptProbeSnapshot snapshot = MiracleInterceptProbeSnapshot.Initial;
    private long attemptCount;
    private long acceptedCount;
    private long recognizedThreatCount;
    private long armedThreatCount;
    private long rejectedThreatCount;
    private long priorityWaitCount;
    private long noInputWaitCount;
    private long rangeWaitCount;
    private long protectionWaitCount;
    private long expiredThreatCount;
    private long cleanseFollowupSignalCount;
    private long cleanseFollowupPromotionCount;
    private long cleanseFollowupCancellationCount;
    private long guardFollowupEpisodeCount;
    private long guardFollowupPromotionCount;
    private long guardFollowupExpiredCount;
    private long guardFollowupRetiredCount;
    private MiracleWaitReason activeWaitReason;
    private bool priorityWaitRecorded;
    private bool noInputWaitRecorded;
    private bool rangeWaitRecorded;
    private bool protectionWaitRecorded;
    private string lastOpportunity = "None observed";
    private string cleanseFollowupLastEvent = "None observed";
    private uint counterActionId;
    private uint cleanseFollowupRemovedStatusId;
    private int cleanseFollowupTeamPressure;
    private ulong guardFollowupTargetGameObjectId;
    private uint guardFollowupTargetEntityId;
    private int guardFollowupTeamPressure;
    private uint protectionEndLocalJobId;
    private MiracleProtectionEndRankCandidate? protectionEndLastRank;
    private string guardFollowupLastEvent = "None observed";
    private long nextErrorLogAt;

    internal MiracleInterceptProbe(
        IObjectTable objectTable,
        IReadOnlySet<uint> verifiedCcBrakeStatusIds,
        ExecuteTracker executeTracker,
        TargetPressureTracker pressureTracker,
        NearAssistRedirector nearAssist,
        MachinistLimitBreakCapture capture,
        IPluginLog log,
        PvPMetadataValidation metadata)
    {
        this.objectTable = objectTable;
        this.executeTracker = executeTracker;
        this.pressureTracker = pressureTracker;
        this.nearAssist = nearAssist;
        this.capture = capture;
        this.log = log;
        verifiedProtectionStatusIds = verifiedCcBrakeStatusIds.ToHashSet();
        silentNocturneMetadataVerified = metadata.SilentNocturneVerified;
        contradanceMetadataVerified = metadata.ContradanceVerified;
    }

    internal MiracleInterceptProbeSnapshot Snapshot => Volatile.Read(ref snapshot);

    internal unsafe MiracleInterceptProbeSnapshot Observe(
        IPlayerCharacter? localPlayer,
        bool isCrystallineConflict,
        bool configurationEnabled,
        bool allowHeldGameplayKey,
        bool dispatchAllowed,
        bool enableMarksmanSpite,
        bool enableZantetsuken,
        bool enableFuriousBacklash,
        bool enableContradance,
        bool enablePostPurifyCrowdControl,
        bool enablePostGuardCrowdControl,
        bool marksmanSpiteMetadataVerified,
        bool zantetsukenMetadataVerified,
        bool furiousBacklashMetadataVerified,
        bool miracleMetadataVerified,
        bool purifyMetadataVerified,
        EmergencyActionInputFrame inputFrame,
        long nowMilliseconds,
        bool hardReset = false)
    {
        inputClaimedThisFrame = false;
        castCancellationRequestThisFrame = null;
        nowMilliseconds = Math.Max(nowMilliseconds, Environment.TickCount64);
        if (hardReset) ResetRuntime();

        var localIdentityValid = localPlayer is not null && HasValidNativeIdentity(localPlayer);
        var localAlive = localIdentityValid && IsLivePlayer(localPlayer);
        var localJobId = localIdentityValid && localPlayer!.ClassJob.IsValid
            ? localPlayer.ClassJob.RowId
            : 0;
        counterActionId = ResolveCounterActionId(
            localJobId,
            miracleMetadataVerified,
            silentNocturneMetadataVerified);
        var protectionMetadataReady = RequiredProtectionStatusIds(counterActionId).All(
            verifiedProtectionStatusIds.Contains);
        var enabled = configurationEnabled &&
                      isCrystallineConflict &&
                      localIdentityValid &&
                      counterActionId != 0 &&
                      protectionMetadataReady;
        var cleanseFollowupEnabled = enabled &&
                                     enablePostPurifyCrowdControl &&
                                     purifyMetadataVerified;
        var guardFollowupEnabled = enabled &&
                                   enablePostGuardCrowdControl &&
                                   verifiedProtectionStatusIds.Contains(
                                       MiracleGuardFollowupRules.GuardStatusId) &&
                                   verifiedProtectionStatusIds.Contains(
                                       MiracleGuardFollowupRules.GuardStatusAlternateId);
        var protectionEndFollowupEnabled = cleanseFollowupEnabled || guardFollowupEnabled;
        var protectionEndJobChanged =
            protectionEndFollowupEnabled &&
            protectionEndLocalJobId != 0 &&
            protectionEndLocalJobId != localJobId;
        if (protectionEndJobChanged)
        {
            // Retire the pre-job physical generation in the shared coordinator;
            // a job swap can never inherit the old exact held key as new consent.
            inputFrame.Consume();
            capture.ClearMiracleInterceptThreats();
            activeThreat = null;
            ClearCleanseFollowupStates();
            guardFollowupState = MiracleGuardFollowupState.Initial;
            guardFollowupTargetGameObjectId = 0;
            guardFollowupTargetEntityId = 0;
            guardFollowupTeamPressure = 0;
            ClearProtectionEndDiagnostics();
            cleanseFollowupLastEvent =
                "PostPurifyCC: local counter job changed; episodes cleared";
            guardFollowupLastEvent =
                "GuardEndCC: local counter job changed; episodes cleared";
        }
        protectionEndLocalJobId = protectionEndFollowupEnabled ? localJobId : 0;
        ObserveProtectionEndHeldConsent(
            allowHeldGameplayKey &&
            localAlive &&
            (cleanseFollowupEnabled || guardFollowupEnabled),
            dispatchAllowed,
            inputFrame,
            hardReset || protectionEndJobChanged);
        var confirmationPendingForLocalCaster = enabled &&
            confirmationState.Pending is { } pending &&
            pending.LocalCasterEntityId == localPlayer!.EntityId;
        capture.SetMiracleInterceptLocalEntityId(
            enabled && (localAlive || confirmationPendingForLocalCaster)
                ? localPlayer!.EntityId
                : 0);
        capture.SetMiracleCleanseFollowupLocalEntityId(
            cleanseFollowupEnabled && localAlive
                ? localPlayer!.EntityId
                : 0);

        if (!enabled)
        {
            capture.ClearMiracleInterceptThreats();
            capture.ClearMiracleInterceptConfirmations();
            activeThreat = null;
            ClearCleanseFollowupStates();
            guardFollowupState = MiracleGuardFollowupState.Initial;
            guardFollowupTargetGameObjectId = 0;
            guardFollowupTargetEntityId = 0;
            guardFollowupTeamPressure = 0;
            protectionEndLocalJobId = 0;
            ClearProtectionEndDiagnostics();
            confirmationState = MiracleInterceptConfirmationRules.ObserveTime(
                confirmationState,
                nowMilliseconds,
                hardReset: true);
            return Publish(
                "Disabled",
                protectionMetadataReady
                    ? "Feature gate closed or local job has no verified counter-CC"
                    : "Required CC-protection metadata unavailable",
                nowMilliseconds);
        }

        if (!localAlive)
        {
            capture.ClearMiracleInterceptThreats();
            activeThreat = null;
            ClearCleanseFollowupStates();
            guardFollowupState = MiracleGuardFollowupState.Initial;
            guardFollowupTargetGameObjectId = 0;
            guardFollowupTargetEntityId = 0;
            guardFollowupTeamPressure = 0;
            protectionEndLocalJobId = 0;
            ClearProtectionEndDiagnostics();
            if (confirmationPendingForLocalCaster)
                DrainConfirmations(nowMilliseconds);
            else
            {
                capture.ClearMiracleInterceptConfirmations();
                confirmationState = MiracleInterceptConfirmationRules.ObserveTime(
                    confirmationState,
                    nowMilliseconds);
            }

            return Publish(
                "Confirmation",
                confirmationPendingForLocalCaster
                    ? "Waiting for exact reactive-CC landing evidence"
                    : "Local player cannot dispatch",
                nowMilliseconds);
        }

        var marksmanSpiteEnabled =
            enableMarksmanSpite &&
            marksmanSpiteMetadataVerified;
        var zantetsukenEnabled =
            enableZantetsuken &&
            zantetsukenMetadataVerified;
        var furiousBacklashEnabled =
            enableFuriousBacklash &&
            furiousBacklashMetadataVerified &&
            verifiedProtectionStatusIds.Contains(EnemyCombatConstants.HardenedScalesStatusId);
        var contradanceEnabled = enableContradance && contradanceMetadataVerified;
        var cleanseSignals = DrainThreats(
            localPlayer!,
            marksmanSpiteEnabled,
            zantetsukenEnabled,
            furiousBacklashEnabled,
            contradanceEnabled,
            cleanseFollowupEnabled,
            nowMilliseconds);
        DrainConfirmations(nowMilliseconds);
        // The native hook can enqueue after the framework-frame clock was read.
        // Refresh before comparing the newly captured event against its deadline.
        nowMilliseconds = Math.Max(nowMilliseconds, Environment.TickCount64);

        if (activeThreat is { } expiringThreat &&
            (nowMilliseconds < expiringThreat.ObservedAtMilliseconds ||
             nowMilliseconds - expiringThreat.ObservedAtMilliseconds >= ThreatLifetime(expiringThreat.Kind)))
        {
            RecordExpired(expiringThreat);
            activeThreat = null;
        }

        if (activeThreat is { } disabledThreat &&
            !IsThreatKindEnabled(
                disabledThreat.Kind,
                marksmanSpiteEnabled,
                zantetsukenEnabled,
                furiousBacklashEnabled,
                contradanceEnabled,
                cleanseFollowupEnabled,
                guardFollowupEnabled))
        {
            lastOpportunity = $"{disabledThreat.Kind}: retired because its trigger was disabled";
            activeThreat = null;
        }

        var followupPromotions = new List<MiracleFollowupPromotion>(2);
        foreach (var cleanseSignal in cleanseSignals)
        {
            var promotion = ObserveCleanseFollowup(
                localPlayer!,
                cleanseFollowupEnabled,
                activeThreat is not null,
                cleanseSignal,
                nowMilliseconds,
                trackedSlot: null);
            if (promotion is { } ready) followupPromotions.Add(ready);
        }

        foreach (var cleanseSlot in cleanseFollowupStates.Keys.Order().ToArray())
        {
            var cleansePromotion = ObserveCleanseFollowup(
                localPlayer!,
                cleanseFollowupEnabled,
                activeThreat is not null,
                null,
                nowMilliseconds,
                cleanseSlot);
            if (cleansePromotion is { } cleanseReady)
                followupPromotions.Add(cleanseReady);
        }

        var guardPromotion = ObserveGuardFollowup(
            localPlayer!,
            guardFollowupEnabled,
            activeThreat is not null,
            nowMilliseconds);
        if (guardPromotion is { } guardReady) followupPromotions.Add(guardReady);

        if (activeThreat is null && followupPromotions.Count > 0)
        {
            var selected = SelectFollowupPromotion(followupPromotions);
            activeThreat = selected.Threat;
            protectionEndLastRank = selected.Rank;
            ResetWaitDiagnostics();
            lastOpportunity =
                $"{selected.Threat.Kind}: exact threat armed from release opportunity";
            foreach (var retired in followupPromotions)
            {
                if (retired == selected) continue;
                Interlocked.Increment(ref rejectedThreatCount);
                if (retired.Threat.Kind == MiracleInterceptThreatKind.PostPurifyCrowdControl)
                {
                    cleanseFollowupLastEvent =
                        $"PostPurifyCC: simultaneous release retired behind {selected.Threat.Kind}";
                }
                else if (retired.Threat.Kind == MiracleInterceptThreatKind.PostGuardCrowdControl)
                {
                    guardFollowupLastEvent =
                        $"GuardEndCC: simultaneous release retired behind {selected.Threat.Kind}";
                    Interlocked.Increment(ref guardFollowupRetiredCount);
                }
            }
        }

        if (activeThreat is not { } threat)
            return Publish("Waiting", "No current exact threat", nowMilliseconds);

        // A transient higher-priority survival/Sprint/Rescue claim cannot dispatch a
        // second action from the same physical generation, but it also need not
        // destroy the exact threat. Retain it only inside its original deadline
        // so a genuinely fresh later generation can still act; never replay or
        // extend the opportunity.
        if (!dispatchAllowed)
        {
            RecordWait(threat, MiracleWaitReason.HigherPriorityHelper);
            return Publish("Armed", "Waiting: higher-priority helper claimed this frame", nowMilliseconds);
        }

        var currentLocalJobId = localPlayer!.ClassJob.IsValid
            ? localPlayer.ClassJob.RowId
            : 0;
        if (threat.CounterActionId != counterActionId ||
            threat.LocalJobId != currentLocalJobId)
        {
            Interlocked.Increment(ref rejectedThreatCount);
            lastOpportunity = $"{threat.Kind}: frozen local job/action changed";
            activeThreat = null;
            return Publish("Cancelled", "Frozen local job/action changed", nowMilliseconds);
        }

        var candidate = ResolveCandidate(localPlayer!, threat);
        if (candidate is null)
        {
            Interlocked.Increment(ref rejectedThreatCount);
            lastOpportunity = $"{threat.Kind}: exact enemy identity changed";
            activeThreat = null;
            return Publish("Cancelled", "Exact enemy identity changed", nowMilliseconds);
        }

        var blockerFamily = BlockerFamilyForAction(threat.CounterActionId);
        var anyProtection = HasAnyVerifiedCcProtection(candidate, blockerFamily);
        var guardReappeared =
            threat.Kind == MiracleInterceptThreatKind.PostGuardCrowdControl &&
            CountActiveGuardStatuses(candidate) != 0;
        var hardenedScales = threat.Kind == MiracleInterceptThreatKind.FuriousBacklash &&
                             HasVerifiedActiveStatus(
                                 candidate,
                                 EnemyCombatConstants.HardenedScalesStatusId);
        var otherProtection = (anyProtection && !hardenedScales) || guardReappeared;
        var rangeAndLineOfSight = HasActionRangeAndLineOfSight(
            threat.CounterActionId,
            localPlayer!,
            candidate);
        var structurallyReady =
            HasStructuralActionReadiness(threat.CounterActionId);
        var exactIntentCanProgress = !hardenedScales &&
                                     !otherProtection &&
                                     rangeAndLineOfSight &&
                                     structurallyReady;
        var globallyQueueReady = exactIntentCanProgress &&
                                 HasGlobalQueueReadiness(
                                     localPlayer!,
                                     threat.CounterActionId);
        var input = inputFrame.Snapshot;
        var isProtectionEndThreat = IsProtectionEndThreat(threat.Kind);
        var triggerKey = VirtualKey.NO_KEY;
        if (threat.GameplayKeyToken > 0)
        {
            var frozenKey = (VirtualKey)threat.GameplayKeyToken;
            if (!IsExactVirtualKey(frozenKey) ||
                !inputFrame.IsGameplayKeyPhysicallyDown(frozenKey))
            {
                Interlocked.Increment(ref rejectedThreatCount);
                lastOpportunity = $"{threat.Kind}: exact held key released";
                activeThreat = null;
                return PublishCandidate(
                    threat,
                    candidate,
                    "Cancelled",
                    "Exact held key released",
                    VirtualKey.NO_KEY,
                    false,
                    false,
                    hardenedScales,
                    otherProtection,
                    rangeAndLineOfSight,
                    nowMilliseconds);
            }

            triggerKey = frozenKey;
        }
        else
        {
            if (isProtectionEndThreat &&
                TryGetLatchedProtectionEndKey(out var latchedKey) &&
                inputFrame.IsGameplayKeyPhysicallyDown(latchedKey))
            {
                triggerKey = latchedKey;
            }
            else if (allowHeldGameplayKey && inputFrame.HeldGameplayKeyEligible)
            {
                triggerKey = input.HeldGameplayKey;
            }
            else if (inputFrame.FreshGameplayKeyPressed)
            {
                triggerKey = input.FreshGameplayKey;
            }

            if (IsExactVirtualKey(triggerKey) &&
                inputFrame.IsGameplayKeyPhysicallyDown(triggerKey))
            {
                threat = threat with { GameplayKeyToken = (int)triggerKey };
                activeThreat = threat;
            }
            else
            {
                triggerKey = VirtualKey.NO_KEY;
            }
        }
        if (input.IsTextInputActive || triggerKey == VirtualKey.NO_KEY)
        {
            RecordWait(
                threat,
                input.IsTextInputActive
                    ? MiracleWaitReason.TextInput
                    : MiracleWaitReason.NoEligibleInput);
            return PublishCandidate(
                threat,
                candidate,
                "Armed",
                input.IsTextInputActive ? "Text input active" : "Waiting for held/fresh physical key",
                triggerKey,
                false,
                false,
                hardenedScales,
                otherProtection,
                rangeAndLineOfSight,
                nowMilliseconds);
        }

        if (!exactIntentCanProgress)
        {
            var waitReason = hardenedScales
                ? MiracleWaitReason.HardenedScales
                : otherProtection
                    ? MiracleWaitReason.OtherProtection
                    : !rangeAndLineOfSight
                        ? MiracleWaitReason.RangeOrLineOfSight
                        : !structurallyReady
                            ? MiracleWaitReason.ActionCooldownOrResources
                            : MiracleWaitReason.NoEligibleInput;
            RecordWait(threat, waitReason);
            return PublishCandidate(
                threat,
                candidate,
                "Armed",
                hardenedScales
                    ? "Waiting: Hardened Scales still active"
                    : otherProtection
                        ? "Waiting: verified counter-CC blocker active"
                        : !rangeAndLineOfSight
                            ? "Waiting: outside native action range/LoS"
                            : "Background wait: action cooldown/resources unavailable",
                triggerKey,
                false,
                false,
                hardenedScales,
                otherProtection,
                rangeAndLineOfSight,
                nowMilliseconds);
        }

        if (!globallyQueueReady)
        {
            RecordWait(threat, MiracleWaitReason.GlobalQueue);
            inputClaimedThisFrame = true;
            inputFrame.Consume();
            castCancellationRequestThisFrame = BuildCastCancellationRequest(
                localPlayer!,
                candidate,
                threat,
                triggerKey,
                inputFrame);
            return PublishCandidate(
                threat,
                candidate,
                "Armed",
                "Soft wait: global animation/cast/action queue busy",
                triggerKey,
                false,
                false,
                hardenedScales,
                otherProtection,
                rangeAndLineOfSight,
                nowMilliseconds);
        }

        if (!MiracleProtectionEndRules.CanAttempt(
                threat.RetryState,
                threat.ObservedAtMilliseconds,
                nowMilliseconds,
                ThreatLifetime(threat.Kind)))
        {
            inputClaimedThisFrame = true;
            inputFrame.Consume();
            return PublishCandidate(
                threat,
                candidate,
                "Armed",
                $"Proven-false retry throttle: {threat.RetryState.NativeAttemptCount}/" +
                HeldActionRetryRules.MaximumNativeAttempts,
                triggerKey,
                false,
                false,
                hardenedScales,
                otherProtection,
                rangeAndLineOfSight,
                nowMilliseconds);
        }

        inputClaimedThisFrame = true;
        inputFrame.Consume();

        // Claim only this framework frame. The exact threat/target/action/key
        // remains frozen until true, an ambiguous exception, the shared
        // rejected-attempt budget, key/identity drift, or the bounded window.
        var attempted = false;
        var accepted = false;
        var exceptionAmbiguous = false;
        var nativeOutcome = ClientActionAttemptOutcome.NotInvoked;
        var attemptedAtMilliseconds = -1L;
        var revalidated = ResolveCandidate(localPlayer!, threat);
        var revalidatedHardened = revalidated is not null &&
                                  threat.Kind == MiracleInterceptThreatKind.FuriousBacklash &&
                                  HasVerifiedActiveStatus(
                                      revalidated,
                                      EnemyCombatConstants.HardenedScalesStatusId);
        var revalidatedProtection = revalidated is not null &&
                                    HasAnyVerifiedCcProtection(revalidated, blockerFamily);
        var revalidatedGuardAbsent = revalidated is not null &&
            (threat.Kind != MiracleInterceptThreatKind.PostGuardCrowdControl ||
             CountActiveGuardStatuses(revalidated) == 0);
        var revalidatedRange = revalidated is not null &&
                               HasActionRangeAndLineOfSight(
                                   threat.CounterActionId,
                                   localPlayer!,
                                   revalidated);
        var revalidationNow = Math.Max(nowMilliseconds, Environment.TickCount64);
        var revalidatedLocalJobId = localPlayer!.ClassJob.IsValid
            ? localPlayer.ClassJob.RowId
            : 0;
        var revalidatedActionIdentity =
            threat.CounterActionId == counterActionId &&
            threat.LocalJobId == revalidatedLocalJobId;
        var revalidatedInput = !input.IsTextInputActive &&
            IsExactVirtualKey(triggerKey) &&
            threat.GameplayKeyToken == (int)triggerKey &&
            inputFrame.IsGameplayKeyPhysicallyDown(triggerKey);
        var revalidatedInsideWindow =
            revalidationNow >= threat.ObservedAtMilliseconds &&
            revalidationNow - threat.ObservedAtMilliseconds < ThreatLifetime(threat.Kind);
        var finalValidationPassed = revalidated is not null &&
                                    !revalidatedHardened &&
                                    !revalidatedProtection &&
                                    revalidatedGuardAbsent &&
                                    revalidatedRange &&
                                    revalidatedActionIdentity &&
                                    revalidatedInput &&
                                    revalidatedInsideWindow;
        var attemptOutcome = MiracleProtectionEndAttemptOutcome.None;
        if (finalValidationPassed)
        {
            try
            {
                attemptedAtMilliseconds = Environment.TickCount64;
                nativeOutcome = TryUseCounterCcOnce(
                    localPlayer!,
                    threat.CounterActionId,
                    revalidated!.GameObjectId,
                    out attempted);
                accepted = nativeOutcome == ClientActionAttemptOutcome.ClientAccepted;
                exceptionAmbiguous =
                    nativeOutcome == ClientActionAttemptOutcome.AcceptanceUnknown;
                if (attempted) Interlocked.Increment(ref attemptCount);
                if (accepted) Interlocked.Increment(ref acceptedCount);
            }
            catch (Exception exception)
            {
                exceptionAmbiguous = attempted;
                nativeOutcome = attempted
                    ? ClientActionAttemptOutcome.AcceptanceUnknown
                    : ClientActionAttemptOutcome.NotInvoked;
                if (attempted) Interlocked.Increment(ref attemptCount);
                LogAttemptFailure(exception, nowMilliseconds);
            }

            var completion = MiracleProtectionEndRules.CompleteNativeAttempt(
                threat.RetryState,
                threat.ObservedAtMilliseconds,
                Math.Max(revalidationNow, Environment.TickCount64),
                nativeOutcome,
                ThreatLifetime(threat.Kind));
            attemptOutcome = completion.Outcome;
            activeThreat = completion.IsTerminal
                ? null
                : threat with { RetryState = completion.NextState };
        }
        else
        {
            Interlocked.Increment(ref rejectedThreatCount);
            activeThreat = null;
        }

        if (attempted && accepted && revalidated is not null && attemptedAtMilliseconds >= 0)
        {
            var registered = MiracleInterceptConfirmationRules.RegisterAttempt(
                confirmationState,
                new MiracleInterceptPendingAttempt(
                    localPlayer!.EntityId,
                    threat.CounterActionId,
                    revalidated.GameObjectId,
                    revalidated.EntityId,
                    threat.Kind,
                    accepted,
                    attemptedAtMilliseconds)
                {
                    RemovedStatusId = threat.RemovedStatusId,
                },
                attemptedAtMilliseconds);
            confirmationState = registered.NextState;
        }

        var retryScheduled =
            attemptOutcome == MiracleProtectionEndAttemptOutcome.RetryScheduled;
        var softWait = attemptOutcome == MiracleProtectionEndAttemptOutcome.SoftWait;
        lastOpportunity = finalValidationPassed
            ? retryScheduled
                ? $"{threat.Kind}: action {threat.CounterActionId} rejected; exact retry scheduled"
                : softWait
                    ? $"{threat.Kind}: native boundary became temporarily unavailable"
                : $"{threat.Kind}: action {threat.CounterActionId} terminal ({attemptOutcome}/{nativeOutcome})"
            : $"{threat.Kind}: exact identity/input/range/protection validation changed";

        return PublishCandidate(
            threat,
            candidate,
            !finalValidationPassed
                ? "Cancelled"
                : retryScheduled
                    ? "Armed"
                    : softWait
                        ? "Armed"
                    : "Spent",
            !finalValidationPassed
                ? "Cancelled without action: target/range/protection changed"
                : accepted
                    ? "Reactive CC accepted locally"
                    : retryScheduled
                        ? "Reactive CC rejected locally; exact retry retained"
                        : softWait
                            ? "Reactive CC soft wait; exact intent retained"
                        : exceptionAmbiguous
                            ? "Reactive CC exception after native boundary; terminal"
                            : "Reactive CC retry budget/window exhausted",
            triggerKey,
            attempted,
            accepted,
            revalidatedHardened,
            revalidatedProtection && !revalidatedHardened,
            revalidatedRange,
            nowMilliseconds);
    }

    internal void Reset()
    {
        ResetRuntime();
        Volatile.Write(ref snapshot, WithOpportunityDiagnostics(MiracleInterceptProbeSnapshot.Initial with
        {
            ConfirmedLandingCount = confirmationState.TotalConfirmed,
            CapturedConfirmationCount = capture.CapturedMiracleInterceptConfirmations,
            DroppedConfirmationCount = capture.DroppedMiracleInterceptConfirmations,
            LastEvent = "Reset",
        }));
    }

    internal MiracleInterceptProbeSnapshot FailClosed(long nowMilliseconds, Exception? exception = null)
    {
        ResetRuntime();
        if (exception is not null) LogAttemptFailure(exception, nowMilliseconds);
        return Publish("Failed closed", "Runtime exception", nowMilliseconds);
    }

    private List<MiracleCleanseFollowupSignal> DrainThreats(
        IPlayerCharacter localPlayer,
        bool enableMarksmanSpite,
        bool enableZantetsuken,
        bool enableFuriousBacklash,
        bool enableContradance,
        bool enablePostPurifyCrowdControl,
        long nowMilliseconds)
    {
        var cleanseSignals = new List<MiracleCleanseFollowupSignal>();
        while (capture.TryDequeueMiracleInterceptThreat(out var signal))
        {
            var eventNow = Math.Max(nowMilliseconds, Environment.TickCount64);
            if (signal.ActionId == EnemyCombatConstants.PurifyActionId)
            {
                if (!enablePostPurifyCrowdControl || signal.LocalEntityId != localPlayer.EntityId)
                    continue;

                if (signal.ObservedAtMilliseconds > eventNow ||
                    eventNow - signal.ObservedAtMilliseconds >=
                    MiracleCleanseFollowupRules.ResilienceAcquisitionMilliseconds ||
                    signal.FeatureGeneration != capture.CurrentMiracleCleanseFollowupGeneration ||
                    !MiracleCleanseFollowupRules.IsExactPurifySignal(
                        signal.CasterEntityId,
                        signal.ActionId,
                        signal.EventTargetEntityId,
                        signal.EffectType,
                        signal.EffectValue,
                        signal.GlobalSequence,
                        signal.SourceSequence))
                {
                    Interlocked.Increment(ref rejectedThreatCount);
                    cleanseFollowupLastEvent = "PostPurifyCC: invalid or stale exact Purify recovery signal";
                    continue;
                }

                var cleanseSignalKey = new MiracleCleanseFollowupSignalKey(
                    signal.CasterEntityId,
                    signal.ActionId,
                    signal.EventTargetEntityId,
                    signal.EffectType,
                    signal.EffectValue,
                    signal.GlobalSequence,
                    signal.SourceSequence);
                var retirement = MiracleCleanseFollowupRules.RetireValidatedSignal(
                    cleanseFollowupSignalLedger,
                    cleanseSignalKey);
                cleanseFollowupSignalLedger = retirement.NextState;
                if (!retirement.IsNewValidatedSignal) continue;

                var canonicalCleanseTarget = ResolveCanonicalEnemy(signal.CasterEntityId);
                if (canonicalCleanseTarget is null)
                {
                    Interlocked.Increment(ref rejectedThreatCount);
                    cleanseFollowupLastEvent =
                        "PostPurifyCC: Purify caster was not one exact canonical e1-e5 enemy";
                    continue;
                }

                cleanseSignals.Add(new MiracleCleanseFollowupSignal(
                    cleanseSignalKey,
                    new MiracleCleanseFollowupTargetIdentity(
                        canonicalCleanseTarget.GameObjectId,
                        canonicalCleanseTarget.EntityId,
                        canonicalCleanseTarget.JobId),
                    signal.ObservedAtMilliseconds));
                continue;
            }

            if (signal.FeatureGeneration != capture.CurrentMiracleInterceptGeneration)
            {
                Interlocked.Increment(ref rejectedThreatCount);
                lastOpportunity = "ReactiveCC: retired stale capture generation";
                continue;
            }

            var kind = signal.ActionId switch
            {
                EnemyCombatConstants.MarksmanSpiteActionId when enableMarksmanSpite =>
                    MiracleInterceptThreatKind.MarksmanSpite,
                EnemyCombatConstants.ZantetsukenActionId when enableZantetsuken =>
                    MiracleInterceptThreatKind.Zantetsuken,
                EnemyCombatConstants.FuriousBacklashActionId when enableFuriousBacklash =>
                    MiracleInterceptThreatKind.FuriousBacklash,
                EnemyCombatConstants.ContradanceActionId when enableContradance =>
                    MiracleInterceptThreatKind.Contradance,
                _ => MiracleInterceptThreatKind.None,
            };
            if (kind == MiracleInterceptThreatKind.None ||
                signal.LocalEntityId != localPlayer.EntityId)
            {
                continue;
            }

            if (signal.ObservedAtMilliseconds > eventNow ||
                eventNow - signal.ObservedAtMilliseconds >= ThreatLifetime(kind))
            {
                Interlocked.Increment(ref rejectedThreatCount);
                Interlocked.Increment(ref expiredThreatCount);
                lastOpportunity = $"{kind}: captured outside its {ThreatLifetime(kind)} ms window";
                continue;
            }

            var identity = new MiracleSignalIdentity(
                signal.CasterEntityId,
                signal.ActionId,
                signal.GlobalSequence,
                signal.SourceSequence);
            if (!RememberSignal(identity)) continue;
            Interlocked.Increment(ref recognizedThreatCount);

            var canonical = ResolveCanonicalEnemy(signal.CasterEntityId, kind);
            if (canonical is null)
            {
                Interlocked.Increment(ref rejectedThreatCount);
                lastOpportunity = $"{kind}: caster was not one exact canonical e1-e5 enemy";
                continue;
            }
            var expectedTarget = kind is
                MiracleInterceptThreatKind.FuriousBacklash or
                MiracleInterceptThreatKind.Contradance
                ? signal.CasterEntityId
                : signal.EventTargetEntityId;
            if ((kind is MiracleInterceptThreatKind.FuriousBacklash or
                         MiracleInterceptThreatKind.Contradance) &&
                expectedTarget != signal.CasterEntityId)
            {
                Interlocked.Increment(ref rejectedThreatCount);
                lastOpportunity = $"{kind}: self-target marker identity mismatch";
                continue;
            }

            if (activeThreat is { } previousThreat && previousThreat.Signal != identity)
            {
                Interlocked.Increment(ref rejectedThreatCount);
                lastOpportunity = $"{kind}: retired behind frozen exact {previousThreat.Kind} lease";
                continue;
            }
            activeThreat = new MiracleThreatState(
                kind,
                canonical.GameObjectId,
                canonical.EntityId,
                canonical.JobId,
                canonical.Slot,
                signal.ObservedAtMilliseconds,
                identity,
                RemovedStatusId: 0,
                counterActionId,
                localPlayer.ClassJob.RowId,
                ProtectionEndRank: null,
                HeldActionRetryState.Initial,
                GameplayKeyToken: 0);
            Interlocked.Increment(ref armedThreatCount);
            ResetWaitDiagnostics();
            lastOpportunity = $"{kind}: exact threat armed";
        }

        return cleanseSignals;
    }

    private MiracleFollowupPromotion? ObserveCleanseFollowup(
        IPlayerCharacter localPlayer,
        bool configurationEnabled,
        bool higherPriorityClaimed,
        MiracleCleanseFollowupSignal? newSignal,
        long nowMilliseconds,
        int? trackedSlot)
    {
        var enemySlot = trackedSlot ?? 0;
        EnemyHudSnapshot? canonical = null;
        var signalWasNew = false;
        if (newSignal is { } exactSignal)
        {
            canonical = ResolveCanonicalEnemy(exactSignal.Target);
            if (canonical is null ||
                !EnemySlotRules.IsValidSlot(canonical.Slot))
            {
                return null;
            }

            enemySlot = canonical.Slot;
            signalWasNew = true;
        }

        if (!EnemySlotRules.IsValidSlot(enemySlot)) return null;
        var previous = cleanseFollowupStates.TryGetValue(enemySlot, out var tracked)
            ? tracked
            : MiracleCleanseFollowupState.Initial;
        if (newSignal is null && previous.ActiveSignal is null)
        {
            cleanseFollowupStates.Remove(enemySlot);
            return null;
        }

        var target = newSignal?.Target ?? previous.ActiveSignal?.Target;
        MiracleCleanseFollowupCandidate? candidate = null;
        IPlayerCharacter? player = null;
        var teamTargetCountKnown = false;
        cleanseFollowupTeamPressure = 0;
        if (target is { } targetIdentity)
        {
            canonical = ResolveCanonicalEnemy(targetIdentity);
            player = ResolveCleanseFollowupCandidate(localPlayer, targetIdentity);
            if (canonical is not null && player is not null)
            {
                candidate = new MiracleCleanseFollowupCandidate(
                    targetIdentity,
                    IsExactCanonicalEnemy: true,
                    IsAliveAndTargetable: true,
                    ActiveResilienceStatusCount: CountActiveStatuses(
                        player,
                        EnemyCombatConstants.ResilienceStatusId));
                teamTargetCountKnown = TryGetFreshTeamTargetCount(
                    localPlayer,
                    player,
                    nowMilliseconds,
                    out cleanseFollowupTeamPressure);
            }
        }

        var signalKey = newSignal?.Key ?? default;
        var decision = MiracleCleanseFollowupRules.Observe(
            previous,
            new MiracleCleanseFollowupObservation(
                configurationEnabled,
                IsCrystallineConflict: true,
                IsLocalCounterJobValid: true,
                higherPriorityClaimed,
                newSignal,
                candidate,
                teamTargetCountKnown,
                cleanseFollowupTeamPressure,
                nowMilliseconds));

        // The exact signal is retired in Core before a promotion can reach the
        // existing single Miracle action boundary.
        if (decision.NextState.ActiveSignal is null)
            cleanseFollowupStates.Remove(enemySlot);
        else
            cleanseFollowupStates[enemySlot] = decision.NextState;

        if (signalWasNew && decision.NextState.ObservedSignals.Contains(signalKey))
        {
            Interlocked.Increment(ref cleanseFollowupSignalCount);
            Interlocked.Increment(ref recognizedThreatCount);
        }

        if (signalWasNew &&
            decision.NextState.ActiveSignal is { } activeSignal &&
            activeSignal.Key == signalKey)
        {
            Interlocked.Increment(ref armedThreatCount);
        }

        if (decision.Kind == MiracleCleanseFollowupDecisionKind.Cancelled &&
            (previous.ActiveSignal is not null || newSignal is not null))
        {
            Interlocked.Increment(ref cleanseFollowupCancellationCount);
            Interlocked.Increment(ref rejectedThreatCount);
        }

        cleanseFollowupLastEvent = decision.Kind switch
        {
            MiracleCleanseFollowupDecisionKind.SignalObserved =>
                $"PostPurifyCC: exact enemy Purify removed {newSignal?.Key.EffectValue ?? previous.ActiveSignal?.Key.EffectValue}; waiting for Resilience",
            MiracleCleanseFollowupDecisionKind.ResilienceObserved =>
                "PostPurifyCC: Resilience observed live; waiting for stable absence",
            MiracleCleanseFollowupDecisionKind.ReadyForPromotion =>
                "PostPurifyCC: Resilience absent; promoted to ranked reactive-CC dispatcher",
            MiracleCleanseFollowupDecisionKind.Cancelled when
                previous.ActiveSignal is not null || newSignal is not null =>
                $"PostPurifyCC: cancelled ({decision.CancelReason})",
            MiracleCleanseFollowupDecisionKind.Cancelled => cleanseFollowupLastEvent,
            MiracleCleanseFollowupDecisionKind.Waiting when
                decision.NextState.Phase == MiracleCleanseFollowupPhase.ReleaseOpportunity &&
                higherPriorityClaimed =>
                "PostPurifyCC: release ready; waiting behind higher-priority helper/threat",
            MiracleCleanseFollowupDecisionKind.Waiting when
                decision.NextState.Phase == MiracleCleanseFollowupPhase.ReleaseOpportunity &&
                !teamTargetCountKnown =>
                "PostPurifyCC: release ready with unknown fresh team pressure",
            MiracleCleanseFollowupDecisionKind.Waiting =>
                $"PostPurifyCC: waiting ({decision.NextState.Phase})",
            _ => cleanseFollowupLastEvent,
        };

        if (!decision.ShouldPromote || decision.PromotionIntent is not { } promotion)
            return null;

        Interlocked.Increment(ref cleanseFollowupPromotionCount);
        canonical = ResolveCanonicalEnemy(promotion.Target);
        player = ResolveCleanseFollowupCandidate(localPlayer, promotion.Target);
        if (canonical is null ||
            player is null)
        {
            Interlocked.Increment(ref cleanseFollowupCancellationCount);
            Interlocked.Increment(ref rejectedThreatCount);
            cleanseFollowupLastEvent =
                "PostPurifyCC: promotion retired because the exact actor changed";
            return null;
        }

        teamTargetCountKnown = TryGetFreshTeamTargetCount(
            localPlayer,
            player,
            nowMilliseconds,
            out cleanseFollowupTeamPressure);
        var rank = new MiracleProtectionEndRankCandidate(
            MiracleInterceptThreatKind.PostPurifyCrowdControl,
            canonical.Slot,
            promotion.Target.GameObjectId,
            promotion.Target.EntityId,
            promotion.Target.JobId,
            teamTargetCountKnown,
            cleanseFollowupTeamPressure,
            player.CurrentHp,
            player.MaxHp,
            canonical.HasTrustedMp,
            canonical.CurrentMp,
            canonical.MaxMp);
        if (!rank.IsValid)
        {
            Interlocked.Increment(ref cleanseFollowupCancellationCount);
            Interlocked.Increment(ref rejectedThreatCount);
            cleanseFollowupLastEvent =
                "PostPurifyCC: promotion retired because exact rank telemetry was invalid";
            return null;
        }

        var promotionSignal = promotion.Signal;
        var threat = new MiracleThreatState(
            MiracleInterceptThreatKind.PostPurifyCrowdControl,
            promotion.Target.GameObjectId,
            promotion.Target.EntityId,
            promotion.Target.JobId,
            canonical.Slot,
            promotion.ReleasedAtMilliseconds,
            new MiracleSignalIdentity(
                promotionSignal.Key.CasterEntityId,
                promotionSignal.Key.ActionId,
                promotionSignal.Key.GlobalSequence,
                promotionSignal.Key.SourceSequence),
            promotionSignal.Key.EffectValue,
            counterActionId,
            localPlayer.ClassJob.RowId,
            rank,
            HeldActionRetryState.Initial,
            GameplayKeyToken: 0);

        cleanseFollowupRemovedStatusId = promotionSignal.Key.EffectValue;
        return new MiracleFollowupPromotion(threat, rank);
    }

    private MiracleFollowupPromotion? ObserveGuardFollowup(
        IPlayerCharacter localPlayer,
        bool configurationEnabled,
        bool higherPriorityClaimed,
        long nowMilliseconds)
    {
        var candidates = BuildGuardFollowupCandidates(localPlayer, nowMilliseconds);
        var decision = MiracleGuardFollowupRules.Observe(
            guardFollowupState,
            new MiracleGuardFollowupObservation(
                configurationEnabled,
                IsCrystallineConflict: true,
                IsLocalCounterJobValid: true,
                higherPriorityClaimed,
                candidates,
                nowMilliseconds));
        guardFollowupState = decision.NextState;

        if (decision.NewGuardEpisodeCount > 0)
        {
            Interlocked.Add(ref guardFollowupEpisodeCount, decision.NewGuardEpisodeCount);
            Interlocked.Add(ref recognizedThreatCount, decision.NewGuardEpisodeCount);
            Interlocked.Add(ref armedThreatCount, decision.NewGuardEpisodeCount);
        }

        if (decision.ExpiredOpportunityCount > 0)
        {
            Interlocked.Add(ref guardFollowupExpiredCount, decision.ExpiredOpportunityCount);
            Interlocked.Add(ref expiredThreatCount, decision.ExpiredOpportunityCount);
        }

        if (decision.RetiredOtherOpportunityCount > 0)
        {
            Interlocked.Add(
                ref guardFollowupRetiredCount,
                decision.RetiredOtherOpportunityCount);
            Interlocked.Add(
                ref rejectedThreatCount,
                decision.RetiredOtherOpportunityCount);
        }

        UpdateGuardFollowupTargetDiagnostics(candidates, decision.PromotionIntent);
        guardFollowupLastEvent = decision.Kind switch
        {
            MiracleGuardFollowupDecisionKind.ReadyForPromotion =>
                "GuardEndCC: first verified Guard-absent frame; promoted to ranked dispatcher",
            MiracleGuardFollowupDecisionKind.Waiting when
                decision.NewReleaseOpportunityCount > 0 && higherPriorityClaimed =>
                "GuardEndCC: first verified Guard-absent frame; waiting behind higher-priority helper/threat",
            MiracleGuardFollowupDecisionKind.Waiting when
                decision.NewReleaseOpportunityCount > 0 =>
                "GuardEndCC: first verified Guard-absent frame; release ready",
            MiracleGuardFollowupDecisionKind.Waiting when
                decision.NextState.Actors.Any(static actor =>
                    actor.Phase == MiracleGuardFollowupPhase.ReleaseOpportunity) &&
                higherPriorityClaimed =>
                "GuardEndCC: release ready; waiting behind higher-priority helper/threat",
            MiracleGuardFollowupDecisionKind.Waiting when
                decision.NextState.Actors.Any(static actor =>
                    actor.Phase == MiracleGuardFollowupPhase.ReleaseOpportunity) =>
                "GuardEndCC: release ready for ranked dispatch",
            MiracleGuardFollowupDecisionKind.Waiting when
                decision.NewGuardEpisodeCount > 0 =>
                "GuardEndCC: exact Guard present; episode armed",
            MiracleGuardFollowupDecisionKind.Cancelled when
                decision.CancelReason != MiracleGuardFollowupCancelReason.None =>
                $"GuardEndCC: cleared ({decision.CancelReason})",
            _ => guardFollowupLastEvent,
        };

        if (!decision.ShouldPromote || decision.PromotionIntent is not { } promotion)
            return null;

        Interlocked.Increment(ref guardFollowupPromotionCount);
        var canonical = ResolveCanonicalEnemy(promotion.Target);
        var player = ResolveGuardFollowupCandidate(localPlayer, promotion.Target);
        if (canonical is null ||
            player is null ||
            CountActiveGuardStatuses(player) != 0)
        {
            Interlocked.Increment(ref guardFollowupRetiredCount);
            Interlocked.Increment(ref rejectedThreatCount);
            guardFollowupLastEvent =
                "GuardEndCC: promotion retired because Guard/identity changed";
            return null;
        }

        var rank = new MiracleProtectionEndRankCandidate(
            MiracleInterceptThreatKind.PostGuardCrowdControl,
            promotion.Target.EnemySlot,
            promotion.Target.GameObjectId,
            promotion.Target.EntityId,
            promotion.Target.JobId,
            promotion.TeamTargetCountKnown,
            promotion.TeamTargetCount,
            promotion.CurrentHp,
            promotion.MaximumHp,
            promotion.HasTrustedMp,
            promotion.CurrentMp,
            promotion.MaximumMp);
        if (!rank.IsValid)
        {
            Interlocked.Increment(ref guardFollowupRetiredCount);
            Interlocked.Increment(ref rejectedThreatCount);
            guardFollowupLastEvent =
                "GuardEndCC: promotion retired because exact rank telemetry was invalid";
            return null;
        }

        var threat = new MiracleThreatState(
            MiracleInterceptThreatKind.PostGuardCrowdControl,
            promotion.Target.GameObjectId,
            promotion.Target.EntityId,
            promotion.Target.JobId,
            promotion.Target.EnemySlot,
            promotion.ReleasedAtMilliseconds,
            default,
            RemovedStatusId: 0,
            counterActionId,
            localPlayer.ClassJob.RowId,
            rank,
            HeldActionRetryState.Initial,
            GameplayKeyToken: 0);
        return new MiracleFollowupPromotion(threat, rank);
    }

    private void DrainConfirmations(long nowMilliseconds)
    {
        while (capture.TryDequeueMiracleInterceptConfirmation(out var effect))
        {
            var eventNow = Math.Max(nowMilliseconds, Environment.TickCount64);
            if (effect.FeatureGeneration != capture.CurrentMiracleInterceptGeneration ||
                effect.ObservedAtMilliseconds > eventNow ||
                eventNow - effect.ObservedAtMilliseconds >
                MiracleInterceptConfirmationRules.CorrelationMilliseconds)
            {
                continue;
            }

            var decision = MiracleInterceptConfirmationRules.ObserveActionEffect(
                confirmationState,
                new MiracleInterceptLandedObservation(
                    effect.CasterEntityId,
                    effect.ActionId,
                    effect.TargetEntityId,
                    effect.EffectType,
                    effect.EffectValue,
                    effect.GlobalSequence,
                    effect.SourceSequence,
                    effect.ObservedAtMilliseconds));
            confirmationState = decision.NextState;
        }

        confirmationState = MiracleInterceptConfirmationRules.ObserveTime(
            confirmationState,
            Math.Max(nowMilliseconds, Environment.TickCount64));
    }

    private EnemyHudSnapshot? ResolveCanonicalEnemy(
        uint casterEntityId,
        MiracleInterceptThreatKind kind)
    {
        var expectedJob = kind switch
        {
            MiracleInterceptThreatKind.MarksmanSpite => EnemyCombatConstants.MachinistJobId,
            MiracleInterceptThreatKind.Zantetsuken => EnemyCombatConstants.SamuraiJobId,
            MiracleInterceptThreatKind.FuriousBacklash => EnemyCombatConstants.ViperJobId,
            MiracleInterceptThreatKind.Contradance => EnemyCombatConstants.DancerJobId,
            _ => 0u,
        };
        if (expectedJob == 0) return null;

        var match = ResolveCanonicalEnemy(casterEntityId);
        return match is { } enemy && enemy.JobId == expectedJob ? enemy : null;
    }

    private EnemyHudSnapshot? ResolveCanonicalEnemy(uint casterEntityId)
    {
        var matches = executeTracker.Enemies
            .Where(enemy =>
                enemy.EntityId == casterEntityId &&
                enemy.JobId != 0 &&
                EnemySlotRules.IsValidSlot(enemy.Slot) &&
                TargetHighlightRules.IsValidGameObjectId(enemy.GameObjectId))
            .Take(2)
            .ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    private EnemyHudSnapshot? ResolveCanonicalEnemy(
        MiracleCleanseFollowupTargetIdentity target) =>
        ResolveCanonicalEnemy(
            target.GameObjectId,
            target.EntityId,
            target.JobId,
            enemySlot: null);

    private EnemyHudSnapshot? ResolveCanonicalEnemy(
        MiracleGuardFollowupTargetIdentity target) =>
        ResolveCanonicalEnemy(
            target.GameObjectId,
            target.EntityId,
            target.JobId,
            target.EnemySlot);

    private EnemyHudSnapshot? ResolveCanonicalEnemy(
        ulong gameObjectId,
        uint entityId,
        uint jobId,
        int? enemySlot)
    {
        var enemies = executeTracker.Enemies
            .Where(static enemy => EnemySlotRules.IsValidSlot(enemy.Slot))
            .ToArray();
        var matches = enemies
            .Where(enemy =>
                (!enemySlot.HasValue || enemy.Slot == enemySlot.Value) &&
                enemy.GameObjectId == gameObjectId &&
                enemy.EntityId == entityId &&
                enemy.JobId == jobId)
            .Take(2)
            .ToArray();
        if (matches.Length != 1) return null;
        var match = matches[0];
        return enemies.Count(enemy => enemy.Slot == match.Slot) == 1 &&
               enemies.Count(enemy => enemy.GameObjectId == match.GameObjectId) == 1 &&
               enemies.Count(enemy => enemy.EntityId == match.EntityId) == 1
            ? match
            : null;
    }

    private IReadOnlyList<MiracleGuardFollowupCandidate> BuildGuardFollowupCandidates(
        IPlayerCharacter localPlayer,
        long nowMilliseconds)
    {
        if (!executeTracker.IsActive) return [];
        var enemies = executeTracker.Enemies
            .Where(static enemy => EnemySlotRules.IsValidSlot(enemy.Slot))
            .ToArray();
        if (enemies.Length == 0) return [];

        var ambiguousSlots = enemies
            .GroupBy(static enemy => enemy.Slot)
            .Where(static group => group.Count() != 1)
            .Select(static group => group.Key)
            .ToHashSet();
        var ambiguousGameObjectIds = enemies
            .GroupBy(static enemy => enemy.GameObjectId)
            .Where(static group => group.Count() != 1)
            .Select(static group => group.Key)
            .ToHashSet();
        var ambiguousEntityIds = enemies
            .GroupBy(static enemy => enemy.EntityId)
            .Where(static group => group.Count() != 1)
            .Select(static group => group.Key)
            .ToHashSet();
        var candidates = new List<MiracleGuardFollowupCandidate>(enemies.Length);

        foreach (var enemy in enemies.OrderBy(static enemy => enemy.Slot))
        {
            var target = new MiracleGuardFollowupTargetIdentity(
                enemy.Slot,
                enemy.GameObjectId,
                enemy.EntityId,
                enemy.JobId);
            var exactCanonical = target.IsValid &&
                                 !ambiguousSlots.Contains(enemy.Slot) &&
                                 !ambiguousGameObjectIds.Contains(enemy.GameObjectId) &&
                                 !ambiguousEntityIds.Contains(enemy.EntityId);
            var player = exactCanonical
                ? ResolveGuardFollowupCandidate(localPlayer, target)
                : null;
            var live = player is not null;
            var teamTargetCount = 0;
            var teamTargetCountKnown = live &&
                TryGetFreshTeamTargetCount(
                    localPlayer,
                    player!,
                    nowMilliseconds,
                    out teamTargetCount);
            candidates.Add(new MiracleGuardFollowupCandidate(
                target,
                exactCanonical,
                live,
                live ? CountActiveGuardStatuses(player!) : -1,
                live ? player!.CurrentHp : 0,
                live ? player!.MaxHp : 0,
                teamTargetCountKnown,
                teamTargetCountKnown ? teamTargetCount : 0)
            {
                HasTrustedMp = live && enemy.HasTrustedMp,
                CurrentMp = live ? enemy.CurrentMp : 0,
                MaximumMp = live ? enemy.MaxMp : 0,
            });
        }

        return candidates;
    }

    private void UpdateGuardFollowupTargetDiagnostics(
        IReadOnlyList<MiracleGuardFollowupCandidate> candidates,
        MiracleGuardFollowupIntent? promotion)
    {
        var target = promotion?.Target ?? guardFollowupState.Actors
            .Where(static actor => actor.Phase == MiracleGuardFollowupPhase.ReleaseOpportunity)
            .OrderBy(static actor => actor.Target.EnemySlot)
            .Select(static actor => (MiracleGuardFollowupTargetIdentity?)actor.Target)
            .FirstOrDefault() ?? guardFollowupState.Actors
            .Where(static actor => actor.Phase == MiracleGuardFollowupPhase.GuardPresent)
            .OrderBy(static actor => actor.Target.EnemySlot)
            .Select(static actor => (MiracleGuardFollowupTargetIdentity?)actor.Target)
            .FirstOrDefault();
        guardFollowupTargetGameObjectId = target?.GameObjectId ?? 0;
        guardFollowupTargetEntityId = target?.EntityId ?? 0;
        guardFollowupTeamPressure = 0;
        if (target is not { } exactTarget) return;

        var matches = candidates
            .Where(candidate => candidate.Target == exactTarget)
            .Take(2)
            .ToArray();
        if (matches.Length == 1 && matches[0].TeamTargetCountKnown)
            guardFollowupTeamPressure = matches[0].TeamTargetCount;
    }

    private IPlayerCharacter? ResolveCleanseFollowupCandidate(
        IPlayerCharacter localPlayer,
        MiracleCleanseFollowupTargetIdentity target)
    {
        var canonical = executeTracker.Enemies
            .Where(enemy =>
                enemy.GameObjectId == target.GameObjectId &&
                enemy.EntityId == target.EntityId &&
                enemy.JobId == target.JobId)
            .Take(2)
            .ToArray();
        if (canonical.Length != 1) return null;

        var players = objectTable.PlayerObjects
            .OfType<IPlayerCharacter>()
            .Where(player =>
                player.GameObjectId == target.GameObjectId &&
                player.EntityId == target.EntityId &&
                player.GameObjectId != localPlayer.GameObjectId &&
                player.ClassJob.IsValid &&
                player.ClassJob.RowId == target.JobId)
            .Take(2)
            .ToArray();
        return players.Length == 1 &&
               IsLivePlayer(players[0]) &&
               HasValidNativeIdentity(players[0])
            ? players[0]
            : null;
    }

    private IPlayerCharacter? ResolveGuardFollowupCandidate(
        IPlayerCharacter localPlayer,
        MiracleGuardFollowupTargetIdentity target)
    {
        if (ResolveCanonicalEnemy(target) is null) return null;
        var players = objectTable.PlayerObjects
            .OfType<IPlayerCharacter>()
            .Where(player =>
                player.GameObjectId == target.GameObjectId &&
                player.EntityId == target.EntityId &&
                player.GameObjectId != localPlayer.GameObjectId &&
                player.ClassJob.IsValid &&
                player.ClassJob.RowId == target.JobId)
            .Take(2)
            .ToArray();
        return players.Length == 1 &&
               IsLivePlayer(players[0]) &&
               HasValidNativeIdentity(players[0])
            ? players[0]
            : null;
    }

    private IPlayerCharacter? ResolveCandidate(
        IPlayerCharacter localPlayer,
        MiracleThreatState threat)
    {
        var canonical = executeTracker.Enemies
            .Where(enemy =>
                EnemySlotRules.IsValidSlot(threat.EnemySlot) &&
                enemy.Slot == threat.EnemySlot &&
                enemy.GameObjectId == threat.GameObjectId &&
                enemy.EntityId == threat.EntityId &&
                enemy.JobId == threat.JobId)
            .Take(2)
            .ToArray();
        if (canonical.Length != 1) return null;

        var players = objectTable.PlayerObjects
            .OfType<IPlayerCharacter>()
            .Where(player =>
                player.GameObjectId == threat.GameObjectId &&
                player.EntityId == threat.EntityId &&
                player.GameObjectId != localPlayer.GameObjectId &&
                player.ClassJob.IsValid &&
                player.ClassJob.RowId == threat.JobId)
            .Take(2)
            .ToArray();
        return players.Length == 1 &&
               IsLivePlayer(players[0]) &&
               HasValidNativeIdentity(players[0])
            ? players[0]
            : null;
    }

    private bool HasAnyVerifiedCcProtection(
        IPlayerCharacter player,
        CcImmunityBrakeBlockerFamily blockerFamily)
    {
        var targetJobId = player.ClassJob.IsValid ? player.ClassJob.RowId : 0;
        foreach (var status in player.StatusList)
        {
            // Actor status-list membership is the authoritative live presence
            // gate. Never predict immunity expiry from RemainingTime.
            if (verifiedProtectionStatusIds.Contains(status.StatusId) &&
                CcImmunityBrakeActionCatalog.IsBlockerStatus(
                    blockerFamily,
                    status.StatusId,
                    targetJobId))
            {
                return true;
            }
        }

        return false;
    }

    private bool HasVerifiedActiveStatus(IPlayerCharacter player, uint statusId)
    {
        if (!verifiedProtectionStatusIds.Contains(statusId)) return false;
        foreach (var status in player.StatusList)
        {
            // Membership is authoritative; never predict the release from the
            // displayed remaining time because Furious Backlash can end it early.
            if (status.StatusId == statusId) return true;
        }

        return false;
    }

    private static int CountActiveStatuses(IPlayerCharacter player, uint statusId)
    {
        var count = 0;
        foreach (var status in player.StatusList)
        {
            if (status.StatusId != statusId) continue;
            count++;
            if (count > 1) return count;
        }

        return count;
    }

    private static int CountActiveGuardStatuses(IPlayerCharacter player)
    {
        var count = 0;
        foreach (var status in player.StatusList)
        {
            if (!MiracleGuardFollowupRules.IsExactGuardStatus(status.StatusId)) continue;
            count++;
            if (count > 1) return count;
        }

        return count;
    }

    private static unsafe bool HasActionRangeAndLineOfSight(
        uint actionId,
        IPlayerCharacter localPlayer,
        IPlayerCharacter target)
    {
        if (MiracleInterceptConfirmationRules.ExpectedStatusForAction(actionId) == 0)
            return false;

        var sourceObject = GetNativeObject(localPlayer);
        var targetObject = GetNativeObject(target);
        if (sourceObject == null || targetObject == null) return false;
        var result = ActionManager.GetActionInRangeOrLoS(
            actionId,
            sourceObject,
            targetObject);
        return SeitonRangeRules.HasNativeRangeAndLineOfSight(result);
    }

    private void ObserveProtectionEndHeldConsent(
        bool enabled,
        bool dispatchAllowed,
        EmergencyActionInputFrame inputFrame,
        bool hardReset)
    {
        var input = inputFrame.Snapshot;
        var latchedKeyPhysicallyDown =
            TryGetLatchedProtectionEndKey(out var previousKey) &&
            inputFrame.IsGameplayKeyPhysicallyDown(previousKey);
        var eligibleKey = VirtualKey.NO_KEY;
        if (enabled && dispatchAllowed && !input.IsTextInputActive)
        {
            var observedKey = inputFrame.HeldGameplayKeyEligible
                ? input.HeldGameplayKey
                : inputFrame.FreshGameplayKeyPressed
                    ? input.FreshGameplayKey
                    : VirtualKey.NO_KEY;
            if (IsExactVirtualKey(observedKey) &&
                inputFrame.IsGameplayKeyPhysicallyDown(observedKey))
            {
                eligibleKey = observedKey;
            }
        }

        protectionEndHeldConsent = MiracleProtectionEndRules.ObserveHeldConsent(
            protectionEndHeldConsent,
            new MiracleProtectionEndHeldConsentObservation(
                enabled,
                input.IsTextInputActive,
                eligibleKey == VirtualKey.NO_KEY ? 0 : (int)eligibleKey,
                latchedKeyPhysicallyDown,
                hardReset));
    }

    private bool TryGetLatchedProtectionEndKey(out VirtualKey key)
    {
        key = VirtualKey.NO_KEY;
        if (!protectionEndHeldConsent.IsLatched) return false;
        var candidate = (VirtualKey)protectionEndHeldConsent.GameplayKeyToken;
        if (!IsExactVirtualKey(candidate)) return false;
        key = candidate;
        return true;
    }

    private static bool TryGetLatchedOrEligibleProtectionEndKey(
        VirtualKey candidate,
        out VirtualKey key)
    {
        key = VirtualKey.NO_KEY;
        if (!IsExactVirtualKey(candidate)) return false;
        key = candidate;
        return true;
    }

    private static bool IsExactVirtualKey(VirtualKey key) =>
        key != VirtualKey.NO_KEY && Enum.IsDefined(typeof(VirtualKey), key);

    private static bool IsProtectionEndThreat(MiracleInterceptThreatKind kind) =>
        kind is MiracleInterceptThreatKind.PostPurifyCrowdControl or
            MiracleInterceptThreatKind.PostGuardCrowdControl;

    private bool TryGetFreshTeamTargetCount(
        IPlayerCharacter localPlayer,
        IPlayerCharacter candidate,
        long nowMilliseconds,
        out int teamTargetCount)
    {
        teamTargetCount = 0;
        if (!HasValidNativeIdentity(localPlayer) ||
            !HasValidNativeIdentity(candidate))
        {
            return false;
        }

        return pressureTracker.TryGetFreshTeamTargetCount(
            new TargetPressureActorIdentity(localPlayer.GameObjectId, localPlayer.EntityId),
            new TargetPressureActorIdentity(candidate.GameObjectId, candidate.EntityId),
            nowMilliseconds,
            MaximumTeamPressureAgeMilliseconds,
            out teamTargetCount);
    }

    private static MiracleFollowupPromotion SelectFollowupPromotion(
        IReadOnlyList<MiracleFollowupPromotion> promotions)
    {
        var selected = promotions[0];
        for (var index = 1; index < promotions.Count; index++)
        {
            if (CompareFollowupPromotions(promotions[index], selected) < 0)
                selected = promotions[index];
        }

        return selected;
    }

    private static int CompareFollowupPromotions(
        MiracleFollowupPromotion left,
        MiracleFollowupPromotion right)
    {
        return MiracleProtectionEndRules.Compare(left.Rank, right.Rank);
    }

    private unsafe ClientActionAttemptOutcome TryUseCounterCcOnce(
        IPlayerCharacter localPlayer,
        uint actionId,
        ulong targetGameObjectId,
        out bool attempted)
    {
        attempted = false;
        if (MiracleInterceptConfirmationRules.ExpectedStatusForAction(actionId) == 0 ||
            !TargetHighlightRules.IsValidGameObjectId(targetGameObjectId))
        {
            return ClientActionAttemptOutcome.NotInvoked;
        }

        var actionManager = ActionManager.Instance();
        if (actionManager == null ||
            !HasStructuralActionReadiness(actionId) ||
            !HasGlobalQueueReadiness(localPlayer, actionId))
        {
            return actionManager == null
                ? ClientActionAttemptOutcome.NotInvoked
                : ClientActionAttemptOutcome.SoftUnavailable;
        }

        var boundaryBefore = ClientActionAttemptBoundary.Capture(actionManager, actionId);
        attempted = true;
        var accepted = nearAssist.RunWithoutRedirect(() =>
            actionManager->UseAction(
                ActionType.Action,
                actionId,
                targetGameObjectId,
                0,
                ActionManager.UseActionMode.None,
                0));
        return ClientActionAttemptBoundaryRules.Classify(
            accepted,
            actionId,
            boundaryBefore,
            ClientActionAttemptBoundary.Capture(actionManager, actionId));
    }

    private static unsafe bool HasStructuralActionReadiness(uint actionId)
    {
        if (MiracleInterceptConfirmationRules.ExpectedStatusForAction(actionId) == 0)
            return false;

        var actionManager = ActionManager.Instance();
        return actionManager != null &&
               actionManager->GetAdjustedActionId(actionId) == actionId &&
               actionManager->IsActionOffCooldown(ActionType.Action, actionId) &&
               actionManager->CheckActionResources(ActionType.Action, actionId) == 0;
    }

    private static unsafe bool HasGlobalQueueReadiness(
        IPlayerCharacter localPlayer,
        uint actionId)
    {
        if (!HasValidNativeIdentity(localPlayer) ||
            MiracleInterceptConfirmationRules.ExpectedStatusForAction(actionId) == 0)
        {
            return false;
        }

        var actionManager = ActionManager.Instance();
        if (actionManager == null ||
            actionManager->ActionQueued ||
            localPlayer.IsCasting ||
            actionManager->CastActionId != 0)
        {
            return false;
        }

        return HeldActionRetryRules.IsNativeBoundaryNearQueueable(
            actionManager->AnimationLock,
            localPlayer.IsCasting,
            actionManager->CastActionId,
            actionManager->ActionQueued);
    }

    private static uint ResolveCounterActionId(
        uint localJobId,
        bool miracleMetadataVerified,
        bool silentNocturneMetadataVerified) =>
        localJobId switch
        {
            EnemyCombatConstants.WhiteMageJobId when miracleMetadataVerified =>
                EnemyCombatConstants.MiracleOfNatureActionId,
            EnemyCombatConstants.BardJobId when silentNocturneMetadataVerified =>
                EnemyCombatConstants.SilentNocturneActionId,
            _ => 0,
        };

    private static CcImmunityBrakeBlockerFamily BlockerFamilyForAction(uint actionId) =>
        actionId == EnemyCombatConstants.SilentNocturneActionId
            ? CcImmunityBrakeBlockerFamily.StandardPurifyCc
            : CcImmunityBrakeBlockerFamily.Miracle;

    private static IReadOnlyList<uint> RequiredProtectionStatusIds(uint actionId) =>
        actionId switch
        {
            EnemyCombatConstants.MiracleOfNatureActionId => RequiredMiracleProtectionStatusIds,
            EnemyCombatConstants.SilentNocturneActionId => RequiredSilentProtectionStatusIds,
            _ => Array.Empty<uint>(),
        };

    private bool RememberSignal(MiracleSignalIdentity identity)
    {
        if (!rememberedSignals.Add(identity)) return false;
        rememberedSignalOrder.Enqueue(identity);
        while (rememberedSignalOrder.Count > MaximumRememberedSignals)
            rememberedSignals.Remove(rememberedSignalOrder.Dequeue());
        return true;
    }

    private void ClearCleanseFollowupStates()
    {
        cleanseFollowupStates.Clear();
        cleanseFollowupSignalLedger = MiracleCleanseFollowupSignalLedger.Initial;
    }

    private MiracleInterceptProbeSnapshot Publish(
        string phase,
        string lastEvent,
        long nowMilliseconds)
    {
        var remaining = activeThreat is { } threat
            ? Math.Max(0, ThreatLifetime(threat.Kind) -
                          Math.Max(0, nowMilliseconds - threat.ObservedAtMilliseconds))
            : 0;
        var result = WithOpportunityDiagnostics(MiracleInterceptProbeSnapshot.Initial with
        {
            Phase = phase,
            Threat = activeThreat?.Kind ?? MiracleInterceptThreatKind.None,
            TargetGameObjectId = activeThreat?.GameObjectId ?? 0,
            TargetEntityId = activeThreat?.EntityId ?? 0,
            TargetJobId = activeThreat?.JobId ?? 0,
            ThreatRemainingMilliseconds = remaining,
            AttemptCount = Interlocked.Read(ref attemptCount),
            AcceptedCount = Interlocked.Read(ref acceptedCount),
            CaptureQueueDepth = capture.MiracleInterceptQueueDepth,
            CapturedThreatCount = capture.CapturedMiracleInterceptThreats,
            DroppedThreatCount = capture.DroppedMiracleInterceptThreats,
            ConfirmationPopup = confirmationState.Popup,
            ConfirmedLandingCount = confirmationState.TotalConfirmed,
            ConfirmationQueueDepth = capture.MiracleInterceptConfirmationQueueDepth,
            CapturedConfirmationCount = capture.CapturedMiracleInterceptConfirmations,
            DroppedConfirmationCount = capture.DroppedMiracleInterceptConfirmations,
            LastEvent = lastEvent,
        });
        Volatile.Write(ref snapshot, result);
        return result;
    }

    private MiracleInterceptProbeSnapshot PublishCandidate(
        MiracleThreatState threat,
        IPlayerCharacter candidate,
        string phase,
        string lastEvent,
        VirtualKey inputKey,
        bool attempted,
        bool accepted,
        bool hardenedScales,
        bool otherProtection,
        bool rangeAndLineOfSight,
        long nowMilliseconds)
    {
        var result = WithOpportunityDiagnostics(new MiracleInterceptProbeSnapshot(
            phase,
            threat.Kind,
            candidate.GameObjectId,
            candidate.EntityId,
            threat.JobId,
            Math.Max(0, ThreatLifetime(threat.Kind) -
                        Math.Max(0, nowMilliseconds - threat.ObservedAtMilliseconds)),
            hardenedScales,
            otherProtection,
            rangeAndLineOfSight,
            inputKey,
            attempted,
            accepted,
            Interlocked.Read(ref attemptCount),
            Interlocked.Read(ref acceptedCount),
            capture.MiracleInterceptQueueDepth,
            capture.CapturedMiracleInterceptThreats,
            capture.DroppedMiracleInterceptThreats,
            confirmationState.Popup,
            confirmationState.TotalConfirmed,
            capture.MiracleInterceptConfirmationQueueDepth,
            capture.CapturedMiracleInterceptConfirmations,
            capture.DroppedMiracleInterceptConfirmations,
            lastEvent));
        Volatile.Write(ref snapshot, result);
        return result;
    }

    private void ResetRuntime()
    {
        activeThreat = null;
        ClearCleanseFollowupStates();
        guardFollowupState = MiracleGuardFollowupState.Initial;
        counterActionId = 0;
        cleanseFollowupRemovedStatusId = 0;
        cleanseFollowupTeamPressure = 0;
        guardFollowupTargetGameObjectId = 0;
        guardFollowupTargetEntityId = 0;
        guardFollowupTeamPressure = 0;
        protectionEndLocalJobId = 0;
        ClearProtectionEndDiagnostics();
        ResetWaitDiagnostics();
        rememberedSignals.Clear();
        rememberedSignalOrder.Clear();
        capture.SetMiracleInterceptLocalEntityId(0);
        capture.SetMiracleCleanseFollowupLocalEntityId(0);
        capture.ClearMiracleInterceptThreats();
        capture.ClearMiracleInterceptConfirmations();
        castCancellationRequestThisFrame = null;
        confirmationState = MiracleInterceptConfirmationRules.ObserveTime(
            confirmationState,
            Environment.TickCount64,
            hardReset: true);
    }

    private MiracleInterceptProbeSnapshot WithOpportunityDiagnostics(
        MiracleInterceptProbeSnapshot value)
    {
        var diagnosticCleanseState = cleanseFollowupStates
            .OrderByDescending(static pair => pair.Value.Phase)
            .ThenBy(static pair => pair.Key)
            .Select(static pair => (MiracleCleanseFollowupState?)pair.Value)
            .FirstOrDefault() ?? MiracleCleanseFollowupState.Initial;
        return value with
        {
            InputClaimed = inputClaimedThisFrame,
            CastCancellationRequest = castCancellationRequestThisFrame,
            RecognizedThreatCount = Interlocked.Read(ref recognizedThreatCount),
            ArmedThreatCount = Interlocked.Read(ref armedThreatCount),
            RejectedThreatCount = Interlocked.Read(ref rejectedThreatCount),
            PriorityWaitCount = Interlocked.Read(ref priorityWaitCount),
            NoInputWaitCount = Interlocked.Read(ref noInputWaitCount),
            RangeWaitCount = Interlocked.Read(ref rangeWaitCount),
            ProtectionWaitCount = Interlocked.Read(ref protectionWaitCount),
            ExpiredThreatCount = Interlocked.Read(ref expiredThreatCount),
            LastOpportunity = lastOpportunity,
            CleanseFollowupPhase = diagnosticCleanseState.Phase,
            CleanseFollowupTargetGameObjectId =
                diagnosticCleanseState.ActiveSignal?.Target.GameObjectId ?? 0,
            CleanseFollowupTargetEntityId =
                diagnosticCleanseState.ActiveSignal?.Target.EntityId ?? 0,
            CleanseFollowupResilienceObserved = diagnosticCleanseState.ResiliencePresenceObserved,
            CleanseFollowupSignalCount = Interlocked.Read(ref cleanseFollowupSignalCount),
            CleanseFollowupPromotionCount = Interlocked.Read(ref cleanseFollowupPromotionCount),
            CleanseFollowupCancellationCount = Interlocked.Read(ref cleanseFollowupCancellationCount),
            CleanseFollowupLastEvent = cleanseFollowupLastEvent,
            CounterActionId = counterActionId,
            CleanseFollowupRemovedStatusId = diagnosticCleanseState.ActiveSignal?.Key.EffectValue ??
                                             activeThreat?.RemovedStatusId ??
                                             cleanseFollowupRemovedStatusId,
            CleanseFollowupTeamPressure = cleanseFollowupTeamPressure,
            CleanseFollowupTrackedCount = cleanseFollowupStates.Count,
            CleanseFollowupReleaseReadyCount = cleanseFollowupStates.Values.Count(static state =>
                state.Phase == MiracleCleanseFollowupPhase.ReleaseOpportunity),
            GuardFollowupTrackedCount = guardFollowupState.Actors.Count(static actor =>
                actor.Phase != MiracleGuardFollowupPhase.WaitingForGuard),
            GuardFollowupReleaseReadyCount = guardFollowupState.Actors.Count(static actor =>
                actor.Phase == MiracleGuardFollowupPhase.ReleaseOpportunity),
            GuardFollowupTargetGameObjectId = guardFollowupTargetGameObjectId,
            GuardFollowupTargetEntityId = guardFollowupTargetEntityId,
            GuardFollowupTeamPressure = guardFollowupTeamPressure,
            GuardFollowupEpisodeCount = Interlocked.Read(ref guardFollowupEpisodeCount),
            GuardFollowupPromotionCount = Interlocked.Read(ref guardFollowupPromotionCount),
            GuardFollowupExpiredCount = Interlocked.Read(ref guardFollowupExpiredCount),
            GuardFollowupRetiredCount = Interlocked.Read(ref guardFollowupRetiredCount),
            GuardFollowupLastEvent = guardFollowupLastEvent,
            ProtectionEndHeldConsentActive = protectionEndHeldConsent.IsLatched,
            ProtectionEndHeldConsentKey = TryGetLatchedProtectionEndKey(out var heldConsentKey)
                ? heldConsentKey
                : VirtualKey.NO_KEY,
            ProtectionEndRankTeamPressureKnown =
                protectionEndLastRank?.TeamTargetCountKnown ?? false,
            ProtectionEndRankTeamPressure = protectionEndLastRank?.TeamTargetCount ?? 0,
            ProtectionEndRankCurrentHp = protectionEndLastRank?.CurrentHp ?? 0,
            ProtectionEndRankMaximumHp = protectionEndLastRank?.MaximumHp ?? 0,
            ProtectionEndRankMpKnown = protectionEndLastRank?.HasTrustedMp ?? false,
            ProtectionEndRankCurrentMp = protectionEndLastRank?.CurrentMp ?? 0,
            ProtectionEndRankMaximumMp = protectionEndLastRank?.MaximumMp ?? 0,
            ConfirmationPendingCount = confirmationState.Pending is null ? 0 : 1,
        };
    }

    private static unsafe HeldCastCancellationRequest? BuildCastCancellationRequest(
        IPlayerCharacter localPlayer,
        IPlayerCharacter target,
        MiracleThreatState threat,
        VirtualKey frozenKey,
        EmergencyActionInputFrame inputFrame)
    {
        if (!HasValidNativeIdentity(localPlayer) ||
            !HasValidNativeIdentity(target) ||
            !IsLivePlayer(localPlayer) ||
            !IsLivePlayer(target) ||
            !IsExactVirtualKey(frozenKey) ||
            threat.GameplayKeyToken != (int)frozenKey ||
            !inputFrame.IsGameplayKeyPhysicallyDown(frozenKey) ||
            threat.CounterActionId == 0)
        {
            return null;
        }

        var actionManager = ActionManager.Instance();
        if (actionManager == null ||
            !localPlayer.IsCasting ||
            actionManager->CastActionId == 0 ||
            actionManager->ActionQueued ||
            !float.IsFinite(actionManager->AnimationLock) ||
            actionManager->AnimationLock < 0f ||
            actionManager->AnimationLock >
            HeldCastCancellationRules.MaximumCancellationAnimationLockSeconds)
        {
            return null;
        }

        var localIdentity = new TargetPressureActorIdentity(
            localPlayer.GameObjectId,
            localPlayer.EntityId);
        var targetIdentity = new TargetPressureActorIdentity(
            target.GameObjectId,
            target.EntityId);
        if (!localIdentity.IsValid || !targetIdentity.IsValid) return null;

        return new HeldCastCancellationRequest(
            HeldCastCancellationHelperKind.ReactiveCounterCc,
            threat.CounterActionId,
            localIdentity,
            targetIdentity,
            threat.GameplayKeyToken,
            GetIntentEpochToken(threat));
    }

    private static ulong GetIntentEpochToken(MiracleThreatState threat)
    {
        var token = unchecked(
            (ulong)threat.ObservedAtMilliseconds ^
            threat.GameObjectId ^
            ((ulong)threat.EntityId << 32) ^
            ((ulong)(byte)threat.Kind << 56) ^
            threat.Signal.GlobalSequence ^
            ((ulong)threat.Signal.SourceSequence << 16));
        return token == 0 ? 1 : token;
    }

    private void ClearProtectionEndHeldConsent()
    {
        protectionEndHeldConsent = MiracleProtectionEndHeldConsentState.Initial;
    }

    private void ClearProtectionEndDiagnostics()
    {
        ClearProtectionEndHeldConsent();
        protectionEndLastRank = null;
    }

    private void RecordWait(MiracleThreatState threat, MiracleWaitReason reason)
    {
        activeWaitReason = reason;
        switch (reason)
        {
            case MiracleWaitReason.HigherPriorityHelper when !priorityWaitRecorded:
                priorityWaitRecorded = true;
                Interlocked.Increment(ref priorityWaitCount);
                break;
            case MiracleWaitReason.NoEligibleInput or MiracleWaitReason.TextInput when !noInputWaitRecorded:
                noInputWaitRecorded = true;
                Interlocked.Increment(ref noInputWaitCount);
                break;
            case MiracleWaitReason.RangeOrLineOfSight when !rangeWaitRecorded:
                rangeWaitRecorded = true;
                Interlocked.Increment(ref rangeWaitCount);
                break;
            case MiracleWaitReason.HardenedScales or MiracleWaitReason.OtherProtection
                when !protectionWaitRecorded:
                protectionWaitRecorded = true;
                Interlocked.Increment(ref protectionWaitCount);
                break;
        }

        lastOpportunity = $"{threat.Kind}: waiting for {DescribeWaitReason(reason)}";
    }

    private void RecordExpired(MiracleThreatState threat)
    {
        Interlocked.Increment(ref expiredThreatCount);
        lastOpportunity = $"{threat.Kind}: expired while waiting for {DescribeWaitReason(activeWaitReason)}";
    }

    private void ResetWaitDiagnostics()
    {
        activeWaitReason = MiracleWaitReason.None;
        priorityWaitRecorded = false;
        noInputWaitRecorded = false;
        rangeWaitRecorded = false;
        protectionWaitRecorded = false;
    }

    private static string DescribeWaitReason(MiracleWaitReason reason) => reason switch
    {
        MiracleWaitReason.HigherPriorityHelper => "Higher-priority helper",
        MiracleWaitReason.NoEligibleInput => "an eligible held/fresh physical key",
        MiracleWaitReason.TextInput => "text input to close",
        MiracleWaitReason.HardenedScales => "Hardened Scales to disappear",
        MiracleWaitReason.OtherProtection => "a verified counter-CC blocker to disappear",
        MiracleWaitReason.RangeOrLineOfSight => "native action range/line of sight",
        MiracleWaitReason.GlobalQueue => "global animation/cast/action queue",
        MiracleWaitReason.ActionCooldownOrResources => "action cooldown/resources",
        _ => "the next runtime evaluation",
    };

    private static long ThreatLifetime(MiracleInterceptThreatKind kind) => kind switch
    {
        MiracleInterceptThreatKind.PostPurifyCrowdControl =>
            MiracleProtectionEndRules.HeldLeaseMilliseconds,
        MiracleInterceptThreatKind.PostGuardCrowdControl =>
            MiracleProtectionEndRules.HeldLeaseMilliseconds,
        _ => MiracleInterceptRules.GetThreatLifetimeMilliseconds(kind),
    };

    private static bool IsThreatKindEnabled(
        MiracleInterceptThreatKind kind,
        bool marksmanSpiteEnabled,
        bool zantetsukenEnabled,
        bool furiousBacklashEnabled,
        bool contradanceEnabled,
        bool postPurifyEnabled,
        bool postGuardEnabled) =>
        kind switch
        {
            MiracleInterceptThreatKind.MarksmanSpite => marksmanSpiteEnabled,
            MiracleInterceptThreatKind.Zantetsuken => zantetsukenEnabled,
            MiracleInterceptThreatKind.FuriousBacklash => furiousBacklashEnabled,
            MiracleInterceptThreatKind.Contradance => contradanceEnabled,
            MiracleInterceptThreatKind.PostPurifyCrowdControl => postPurifyEnabled,
            MiracleInterceptThreatKind.PostGuardCrowdControl => postGuardEnabled,
            _ => false,
        };

    private static bool IsLivePlayer(IPlayerCharacter? player) =>
        player is not null &&
        player.IsTargetable &&
        !player.IsDead &&
        player.CurrentHp > 0 &&
        player.MaxHp >= player.CurrentHp;

    private static unsafe bool HasValidNativeIdentity(IPlayerCharacter player)
    {
        if (player.Address == 0 ||
            player.EntityId is 0 or 0xE0000000 ||
            !TargetHighlightRules.IsValidGameObjectId(player.GameObjectId))
        {
            return false;
        }

        var native = (GameObject*)player.Address;
        return native != null && native->EntityId == player.EntityId;
    }

    private static unsafe GameObject* GetNativeObject(IPlayerCharacter player)
    {
        if (!HasValidNativeIdentity(player)) return null;
        return (GameObject*)player.Address;
    }

    private void LogAttemptFailure(Exception exception, long nowMilliseconds)
    {
        if (nowMilliseconds < nextErrorLogAt) return;
        nextErrorLogAt = nowMilliseconds + 10_000;
        log.Error(exception, "Seiton Sense reactive CC failed closed; the action will not be retried.");
    }

    private readonly record struct MiracleSignalIdentity(
        uint CasterEntityId,
        uint ActionId,
        uint GlobalSequence,
        ushort SourceSequence);

    private readonly record struct MiracleThreatState(
        MiracleInterceptThreatKind Kind,
        ulong GameObjectId,
        uint EntityId,
        uint JobId,
        int EnemySlot,
        long ObservedAtMilliseconds,
        MiracleSignalIdentity Signal,
        uint RemovedStatusId,
        uint CounterActionId,
        uint LocalJobId,
        MiracleProtectionEndRankCandidate? ProtectionEndRank,
        HeldActionRetryState RetryState,
        int GameplayKeyToken);

    private readonly record struct MiracleFollowupPromotion(
        MiracleThreatState Threat,
        MiracleProtectionEndRankCandidate Rank);

    private enum MiracleWaitReason : byte
    {
        None = 0,
        HigherPriorityHelper = 1,
        NoEligibleInput = 2,
        TextInput = 3,
        HardenedScales = 4,
        OtherProtection = 5,
        RangeOrLineOfSight = 6,
        GlobalQueue = 7,
        ActionCooldownOrResources = 8,
    }
}
