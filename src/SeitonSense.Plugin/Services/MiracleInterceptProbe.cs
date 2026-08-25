using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using SeitonSense.Core;
using System.Numerics;

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
    internal VirtualKey ProtectionEndReservedKey { get; init; }
    internal long ProtectionEndExpectedRemainingMilliseconds { get; init; }
    internal bool ProtectionEndRankTeamPressureKnown { get; init; }
    internal int ProtectionEndRankTeamPressure { get; init; }
    internal uint ProtectionEndRankCurrentHp { get; init; }
    internal uint ProtectionEndRankMaximumHp { get; init; }
    internal bool ProtectionEndRankMpKnown { get; init; }
    internal uint ProtectionEndRankCurrentMp { get; init; }
    internal uint ProtectionEndRankMaximumMp { get; init; }
    internal int ConfirmationPendingCount { get; init; }
    internal bool ConfirmationAwaitingSourceSequence { get; init; }
    internal uint ConfirmationPendingActionId { get; init; }
    internal bool WolvesDenCurrentTargetMode { get; init; }

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
/// Explicit WHM/BRD/NIN/PLD/RDM held reactive-CC helper. It freezes one exact
/// threat, target, action, range policy, and physical key. Only proven
/// client-false calls may retry inside the bounded event lease; it never
/// changes the selected target.
/// </summary>
internal sealed class MiracleInterceptProbe
{
    private const int MaximumRememberedSignals = 128;
    private const long MaximumTeamPressureAgeMilliseconds = 250;
    // Core state requires one stable positive identity key. In Wolves' Den it
    // denotes only the current hard target; it is never resolved as an e-slot.
    private const int WolvesDenCurrentTargetStateKey = 1;
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
    private readonly IReadOnlySet<uint> verifiedCounterActionIds;
    private readonly IReadOnlySet<uint> verifiedProtectionStatusIds;
    private readonly bool silentNocturneMetadataVerified;
    private readonly bool contradanceMetadataVerified;
    private readonly HashSet<MiracleSignalIdentity> rememberedSignals = [];
    private readonly Queue<MiracleSignalIdentity> rememberedSignalOrder = [];
    private readonly Dictionary<int, MiracleCleanseFollowupState> cleanseFollowupStates = [];
    private readonly List<MiracleCleanseFollowupPendingResolution>
        pendingCleanseTargetResolutions = [];
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
    private float counterActionMaximumRangeYalms = float.PositiveInfinity;
    private bool wolvesDenContextThisFrame;
    private IPlayerCharacter? wolvesDenCurrentHardTargetThisFrame;
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
        IReadOnlySet<uint> verifiedCcBrakeActionIds,
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
        verifiedCounterActionIds = verifiedCcBrakeActionIds.ToHashSet();
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
        bool hardReset = false,
        bool enablePaladinIntervene = false,
        float paladinInterveneMaximumRangeYalms =
            ReactiveCounterCcProfileRules.InterveneMaximumRangeYalms,
        bool enableRedMageResolution = false,
        bool redMageResolutionMetadataVerified = false,
        bool isWolvesDenTesting = false,
        IPlayerCharacter? wolvesDenCurrentHardTarget = null)
    {
        inputClaimedThisFrame = false;
        castCancellationRequestThisFrame = null;
        nowMilliseconds = Math.Max(nowMilliseconds, Environment.TickCount64);
        if (hardReset) ResetRuntime();

        wolvesDenContextThisFrame = !isCrystallineConflict && isWolvesDenTesting;
        wolvesDenCurrentHardTargetThisFrame = wolvesDenContextThisFrame
            ? wolvesDenCurrentHardTarget
            : null;

        var localIdentityValid = localPlayer is not null && HasValidNativeIdentity(localPlayer);
        var localAlive = localIdentityValid && IsLivePlayer(localPlayer);
        var localJobId = localIdentityValid && localPlayer!.ClassJob.IsValid
            ? localPlayer.ClassJob.RowId
            : 0;
        counterActionId = ResolveCounterActionId(
            localJobId,
            miracleMetadataVerified,
            silentNocturneMetadataVerified,
            enablePaladinIntervene,
            enableRedMageResolution,
            redMageResolutionMetadataVerified);
        counterActionMaximumRangeYalms = ResolveCounterMaximumRangeYalms(
            counterActionId,
            paladinInterveneMaximumRangeYalms);
        var protectionMetadataReady = RequiredProtectionStatusIds(counterActionId).All(
            verifiedProtectionStatusIds.Contains);
        var enabled = configurationEnabled &&
                      ReactiveCounterCcProfileRules.IsSupportedContext(
                          isCrystallineConflict,
                          isWolvesDenTesting) &&
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
            inputFrame,
            hardReset || protectionEndJobChanged);
        var episodeGameplayKeyToken = ResolveEpisodeGameplayKeyToken(
            allowHeldGameplayKey,
            inputFrame);
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
            isCrystallineConflict &&
            enableMarksmanSpite &&
            marksmanSpiteMetadataVerified;
        var zantetsukenEnabled =
            isCrystallineConflict &&
            enableZantetsuken &&
            zantetsukenMetadataVerified;
        var furiousBacklashEnabled =
            isCrystallineConflict &&
            enableFuriousBacklash &&
            furiousBacklashMetadataVerified &&
            verifiedProtectionStatusIds.Contains(EnemyCombatConstants.HardenedScalesStatusId);
        var contradanceEnabled = isCrystallineConflict &&
                                 enableContradance &&
                                 contradanceMetadataVerified;

        // Retire an old lease before draining new packets. Otherwise an already
        // expired or newly disabled threat could terminally suppress the first
        // fresh exact event in this same framework frame and only then be
        // cleared below.
        nowMilliseconds = Math.Max(nowMilliseconds, Environment.TickCount64);
        if (activeThreat is { } staleThreatBeforeDrain &&
            (nowMilliseconds < staleThreatBeforeDrain.ObservedAtMilliseconds ||
             nowMilliseconds - staleThreatBeforeDrain.ObservedAtMilliseconds >=
             ThreatLifetime(staleThreatBeforeDrain)))
        {
            RecordExpired(staleThreatBeforeDrain);
            activeThreat = null;
        }

        if (activeThreat is { } disabledThreatBeforeDrain &&
            !IsThreatKindEnabled(
                disabledThreatBeforeDrain.Kind,
                marksmanSpiteEnabled,
                zantetsukenEnabled,
                furiousBacklashEnabled,
                contradanceEnabled,
                cleanseFollowupEnabled,
                guardFollowupEnabled))
        {
            lastOpportunity =
                $"{disabledThreatBeforeDrain.Kind}: retired because its trigger was disabled";
            activeThreat = null;
        }

        if (activeThreat is { } frozenThreatBeforeDrain)
        {
            if (TryRefreshAndResolveFrozenThreat(
                    localPlayer!,
                    frozenThreatBeforeDrain,
                    out var refreshedThreatBeforeDrain,
                    out _))
            {
                activeThreat = refreshedThreatBeforeDrain;
            }
            else
            {
                Interlocked.Increment(ref rejectedThreatCount);
                lastOpportunity =
                    $"{frozenThreatBeforeDrain.Kind}: retired before drain after exact job/action/actor drift";
                activeThreat = null;
            }
        }

        var cleanseSignals = DrainThreats(
            localPlayer!,
            marksmanSpiteEnabled,
            zantetsukenEnabled,
            furiousBacklashEnabled,
            contradanceEnabled,
            cleanseFollowupEnabled,
            nowMilliseconds,
            episodeGameplayKeyToken);
        DrainConfirmations(nowMilliseconds);
        // The native hook can enqueue after the framework-frame clock was read.
        // Refresh before comparing the newly captured event against its deadline.
        nowMilliseconds = Math.Max(nowMilliseconds, Environment.TickCount64);

        if (activeThreat is { } expiringThreat &&
            (nowMilliseconds < expiringThreat.ObservedAtMilliseconds ||
             nowMilliseconds - expiringThreat.ObservedAtMilliseconds >= ThreatLifetime(expiringThreat)))
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
                trackedSlot: null,
                inputFrame,
                episodeGameplayKeyToken);
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
                cleanseSlot,
                inputFrame,
                episodeGameplayKeyToken);
            if (cleansePromotion is { } cleanseReady)
                followupPromotions.Add(cleanseReady);
        }

        var guardPromotion = ObserveGuardFollowup(
            localPlayer!,
            guardFollowupEnabled,
            activeThreat is not null,
            nowMilliseconds,
            inputFrame,
            episodeGameplayKeyToken);
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

        if (!ReactiveCounterCcProfileRules.IsThreatSupportedByAction(
                threat.CounterActionId,
                threat.Kind))
        {
            Interlocked.Increment(ref rejectedThreatCount);
            lastOpportunity =
                $"{threat.Kind}: counter action {threat.CounterActionId} is not reviewed for this trigger";
            activeThreat = null;
            return Publish(
                "Cancelled",
                "Counter action is not reviewed for this trigger family",
                nowMilliseconds);
        }

        // The exact hostile packet owns the short event lease, not whichever
        // movement key happened to win the very same framework frame. Attach
        // the first currently eligible held/fresh generation inside that
        // original lease, then freeze it for every later validation. This
        // restores the pre-v0.27 behavior without extending, retargeting, or
        // replaying the event.
        if (threat.GameplayKeyToken <= 0)
        {
            if (!TryRefreshAndResolveFrozenThreat(
                    localPlayer!,
                    threat,
                    out var refreshedKeylessThreat,
                    out _))
            {
                Interlocked.Increment(ref rejectedThreatCount);
                lastOpportunity = $"{threat.Kind}: keyless lease exact job/action/actor changed";
                activeThreat = null;
                return Publish(
                    "Cancelled",
                    "Keyless lease exact job/action/actor changed",
                    nowMilliseconds);
            }
            threat = refreshedKeylessThreat;
            activeThreat = threat;

            if (episodeGameplayKeyToken <= 0)
            {
                RecordWait(threat, MiracleWaitReason.NoEligibleInput);
                return Publish(
                    "Armed",
                    "Waiting: exact threat stored; no eligible held/fresh key yet",
                    nowMilliseconds);
            }

            threat = threat with { GameplayKeyToken = episodeGameplayKeyToken };
            activeThreat = threat;
            lastOpportunity =
                $"{threat.Kind}: exact key {episodeGameplayKeyToken} attached inside original event lease";
        }

        // Validate the frozen key before yielding to another helper. Otherwise
        // a release/text-input frame hidden behind a priority wait could let a
        // later press of the same virtual key impersonate the old reservation.
        var input = inputFrame.Snapshot;
        var triggerKey = threat.GameplayKeyToken > 0
            ? (VirtualKey)threat.GameplayKeyToken
            : VirtualKey.NO_KEY;
        if (!IsExactVirtualKey(triggerKey) ||
            !inputFrame.IsGameplayKeyGenerationEligible(triggerKey))
        {
            Interlocked.Increment(ref rejectedThreatCount);
            lastOpportunity = input.IsTextInputActive
                ? $"{threat.Kind}: reserved key generation poisoned by text input"
                : threat.GameplayKeyToken > 0
                    ? $"{threat.Kind}: exact held key released"
                    : $"{threat.Kind}: no exact key was reserved at capture";
            activeThreat = null;
            return Publish(
                "Cancelled",
                input.IsTextInputActive
                    ? "Reserved key generation invalidated by text input"
                    : threat.GameplayKeyToken > 0
                        ? "Exact held key released"
                        : "No exact held key was reserved at capture",
                nowMilliseconds);
        }

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
        if (currentLocalJobId == EnemyCombatConstants.NinjaJobId &&
            threat.CounterActionId == EnemyCombatConstants.NinjaAeolianEdgeComboCarrierActionId &&
            IsExactRaijuAction(counterActionId))
        {
            // The carrier is a family placeholder only until FFXIV exposes one
            // exact executable Raiju variant. Freeze that variant once.
            threat = threat with { CounterActionId = counterActionId };
            activeThreat = threat;
        }

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

        if (threat.CounterActionId == EnemyCombatConstants.NinjaAeolianEdgeComboCarrierActionId)
        {
            var reservedKey = (VirtualKey)threat.GameplayKeyToken;
            if (!IsExactVirtualKey(reservedKey) ||
                !inputFrame.IsGameplayKeyGenerationEligible(reservedKey))
            {
                Interlocked.Increment(ref rejectedThreatCount);
                lastOpportunity = $"{threat.Kind}: exact held key released before Raiju became executable";
                activeThreat = null;
                return Publish("Cancelled", "Exact held key released", nowMilliseconds);
            }

            RecordWait(threat, MiracleWaitReason.ActionCooldownOrResources);
            return PublishCandidate(
                threat,
                candidate,
                "Armed",
                "Background wait: no exact Raiju variant exposed by the combo carrier",
                reservedKey,
                false,
                false,
                false,
                false,
                false,
                nowMilliseconds);
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
            candidate,
            threat.MaximumRangeYalms);
        var structurallyReady =
            HasStructuralActionReadiness(localPlayer!, threat.CounterActionId);
        var exactIntentCanProgress = !hardenedScales &&
                                     !otherProtection &&
                                     rangeAndLineOfSight &&
                                     structurallyReady;
        var globallyQueueReady = exactIntentCanProgress &&
                                 HasGlobalQueueReadiness(
                                     localPlayer!,
                                     threat.CounterActionId);
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
                ThreatLifetime(threat)))
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
        ushort expectedSourceSequence = 0;
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
                                   revalidated,
                                   threat.MaximumRangeYalms);
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
            inputFrame.IsGameplayKeyGenerationEligible(triggerKey);
        var revalidatedInsideWindow =
            revalidationNow >= threat.ObservedAtMilliseconds &&
            revalidationNow - threat.ObservedAtMilliseconds < ThreatLifetime(threat);
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
                    out attempted,
                    out expectedSourceSequence);
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
                ThreatLifetime(threat));
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
                    attemptedAtMilliseconds,
                    expectedSourceSequence)
                {
                    RemovedStatusId = threat.RemovedStatusId,
                },
                attemptedAtMilliseconds);
            confirmationState = registered.NextState;
        }

        if (attempted)
        {
            log.Information(
                "Seiton Sense reactive CC attempt: kind={ThreatKind} action={ActionId} " +
                "target={TargetEntityId:X8} key={KeyToken} outcome={AttemptOutcome}/{NativeOutcome} " +
                "sourceSequence={SourceSequence}",
                threat.Kind,
                threat.CounterActionId,
                revalidated?.EntityId ?? threat.EntityId,
                threat.GameplayKeyToken,
                attemptOutcome,
                nativeOutcome,
                expectedSourceSequence);
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
        long nowMilliseconds,
        int episodeGameplayKeyToken)
    {
        var cleanseSignals = new List<MiracleCleanseFollowupSignal>();
        var immediateThreats = new List<MiracleThreatState>();
        ResolvePendingCleanseTargets(
            localPlayer,
            enablePostPurifyCrowdControl,
            nowMilliseconds,
            cleanseSignals);

        // A keyless event lease must remain exact while waiting for a later
        // held/fresh generation. Retire stale job/action/actor identity before
        // it can suppress a newly dequeued exact hostile packet.
        if (activeThreat is { GameplayKeyToken: <= 0 } unboundThreat)
        {
            if (TryRefreshAndResolveFrozenThreat(
                    localPlayer,
                    unboundThreat,
                    out var refreshedThreat,
                    out _))
            {
                activeThreat = refreshedThreat;
            }
            else
            {
                Interlocked.Increment(ref rejectedThreatCount);
                lastOpportunity = $"{unboundThreat.Kind}: keyless lease retired after exact identity drift";
                activeThreat = null;
            }
        }

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

                var pendingResolution = new MiracleCleanseFollowupPendingResolution(
                    cleanseSignalKey,
                    signal.ObservedAtMilliseconds,
                    signal.LocalEntityId,
                    localPlayer.ClassJob.RowId,
                    signal.FeatureGeneration);
                if (wolvesDenContextThisFrame &&
                    ResolveExactWolvesDenCurrentTarget(
                        localPlayer,
                        expectedActorEntityId: signal.CasterEntityId) is null)
                {
                    Interlocked.Increment(ref rejectedThreatCount);
                    cleanseFollowupLastEvent =
                        "PostPurifyCC: Wolves' Den current hard target did not exactly match the Purify actor";
                    continue;
                }

                var resolution = ResolveCleanseTarget(
                    pendingResolution,
                    localPlayer,
                    enablePostPurifyCrowdControl,
                    eventNow);
                if (resolution.DidResolve && resolution.ResolvedSignal is { } resolvedSignal)
                {
                    cleanseSignals.Add(resolvedSignal);
                    continue;
                }

                if (resolution.ShouldRetry &&
                    !wolvesDenContextThisFrame &&
                    pendingCleanseTargetResolutions.Count <
                    MiracleCleanseFollowupRules.MaximumPendingResolutions)
                {
                    pendingCleanseTargetResolutions.Add(pendingResolution);
                    cleanseFollowupLastEvent =
                        "PostPurifyCC: exact signal retained; waiting for one unique canonical e1-e5 row";
                    log.Information(
                        "Seiton Sense reactive CC pending target: source=Purify " +
                        "caster={CasterEntityId:X8} generation={FeatureGeneration} " +
                        "deadline={DeadlineMilliseconds}",
                        pendingResolution.Key.CasterEntityId,
                        pendingResolution.FeatureGeneration,
                        pendingResolution.ObservedAtMilliseconds +
                        MiracleCleanseFollowupRules.ResilienceAcquisitionMilliseconds);
                    continue;
                }

                Interlocked.Increment(ref rejectedThreatCount);
                cleanseFollowupLastEvent = resolution.ShouldRetry
                    ? "PostPurifyCC: pending canonical-resolution capacity exhausted"
                    : $"PostPurifyCC: exact signal retired ({resolution.RetireReason})";
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

            if (!ReactiveCounterCcProfileRules.IsThreatSupportedByAction(
                    counterActionId,
                    kind))
            {
                Interlocked.Increment(ref rejectedThreatCount);
                lastOpportunity =
                    $"{kind}: counter action {counterActionId} is protection-end only";
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

            var incomingThreat = new MiracleThreatState(
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
                GameplayKeyToken: episodeGameplayKeyToken,
                MaximumRangeYalms: counterActionMaximumRangeYalms);
            if (activeThreat is { } previousThreat && previousThreat.Signal != identity &&
                !MiracleProtectionEndRules.CanPreemptUnattemptedLowerPriorityThreat(
                    previousThreat.Kind,
                    previousThreat.RetryState,
                    incomingThreat.Kind))
            {
                Interlocked.Increment(ref rejectedThreatCount);
                lastOpportunity = $"{kind}: retired behind frozen exact {previousThreat.Kind} lease";
                continue;
            }

            immediateThreats.Add(incomingThreat);
        }

        if (immediateThreats.Count > 0)
        {
            var selected = immediateThreats
                .OrderByDescending(static threat =>
                    MiracleInterceptRules.GetDispatchPriority(threat.Kind))
                .ThenBy(static threat => threat.ObservedAtMilliseconds)
                .ThenBy(static threat => threat.EnemySlot)
                .ThenBy(static threat => threat.GameObjectId)
                .First();
            var preemptedThreat = activeThreat;
            activeThreat = selected;
            Interlocked.Increment(ref armedThreatCount);
            if (preemptedThreat is { } retiredActive && retiredActive.Signal != selected.Signal)
            {
                Interlocked.Increment(ref rejectedThreatCount);
                lastOpportunity =
                    $"{selected.Kind}: exact higher-priority event preempted unattempted {retiredActive.Kind}";
                log.Information(
                    "Seiton Sense reactive CC preemption: incoming={IncomingThreat} " +
                    "retired={RetiredThreat} incomingTarget={TargetEntityId:X8} " +
                    "retiredNativeAttempts={NativeAttemptCount}",
                    selected.Kind,
                    retiredActive.Kind,
                    selected.EntityId,
                    retiredActive.RetryState.NativeAttemptCount);
            }
            foreach (var retired in immediateThreats)
            {
                if (retired == selected) continue;
                Interlocked.Increment(ref rejectedThreatCount);
            }
            ResetWaitDiagnostics();
            if (preemptedThreat is null || preemptedThreat.Value.Signal == selected.Signal)
                lastOpportunity = $"{selected.Kind}: exact ranked threat armed";
        }

        return cleanseSignals;
    }

    private void ResolvePendingCleanseTargets(
        IPlayerCharacter localPlayer,
        bool configurationEnabled,
        long nowMilliseconds,
        List<MiracleCleanseFollowupSignal> resolvedSignals)
    {
        var index = 0;
        while (index < pendingCleanseTargetResolutions.Count)
        {
            var pending = pendingCleanseTargetResolutions[index];
            var decision = ResolveCleanseTarget(
                pending,
                localPlayer,
                configurationEnabled,
                Math.Max(nowMilliseconds, Environment.TickCount64));
            if (decision.ShouldRetry)
            {
                index++;
                continue;
            }

            // Remove before exposing a resolved lifecycle. The terminal signal
            // ledger already owns duplicate suppression, so this exact pending
            // entry can neither replay nor extend its deadline.
            pendingCleanseTargetResolutions.RemoveAt(index);
            if (decision.DidResolve && decision.ResolvedSignal is { } resolvedSignal)
            {
                resolvedSignals.Add(resolvedSignal);
                cleanseFollowupLastEvent =
                    "PostPurifyCC: exact pending caster resolved to one canonical e1-e5 identity";
                log.Information(
                    "Seiton Sense reactive CC pending target resolved: source=Purify " +
                    "caster={CasterEntityId:X8} gameObject={GameObjectId:X16}",
                    resolvedSignal.Key.CasterEntityId,
                    resolvedSignal.Target.GameObjectId);
                continue;
            }

            Interlocked.Increment(ref rejectedThreatCount);
            if (decision.RetireReason ==
                MiracleCleanseFollowupResolutionRetireReason.AcquisitionExpired)
            {
                Interlocked.Increment(ref expiredThreatCount);
            }
            cleanseFollowupLastEvent =
                $"PostPurifyCC: pending canonical resolution retired ({decision.RetireReason})";
            log.Information(
                "Seiton Sense reactive CC pending target retired: source=Purify " +
                "caster={CasterEntityId:X8} reason={RetireReason}",
                pending.Key.CasterEntityId,
                decision.RetireReason);
        }
    }

    private MiracleCleanseFollowupResolutionDecision ResolveCleanseTarget(
        MiracleCleanseFollowupPendingResolution pending,
        IPlayerCharacter localPlayer,
        bool configurationEnabled,
        long nowMilliseconds)
    {
        MiracleCleanseFollowupTargetIdentity? target;
        if (wolvesDenContextThisFrame)
        {
            var currentTarget = ResolveExactWolvesDenCurrentTarget(
                localPlayer,
                expectedActorEntityId: pending.Key.CasterEntityId);
            target = currentTarget is null
                ? null
                : new MiracleCleanseFollowupTargetIdentity(
                    currentTarget.GameObjectId,
                    currentTarget.EntityId,
                    currentTarget.ClassJob.RowId);
        }
        else
        {
            var canonical = ResolveUniqueCanonicalCleanseEnemy(
                pending.Key.CasterEntityId);
            target = canonical is null
                ? null
                : new MiracleCleanseFollowupTargetIdentity(
                    canonical.GameObjectId,
                    canonical.EntityId,
                    canonical.JobId);
        }
        var localJobId = localPlayer.ClassJob.IsValid
            ? localPlayer.ClassJob.RowId
            : 0;
        return MiracleCleanseFollowupRules.ResolvePendingSignal(
            pending,
            new MiracleCleanseFollowupResolutionObservation(
                configurationEnabled,
                IsCrystallineConflict: !wolvesDenContextThisFrame,
                IsLocalCounterJobValid: counterActionId != 0 && localJobId != 0,
                localPlayer.EntityId,
                localJobId,
                capture.CurrentMiracleCleanseFollowupGeneration,
                target,
                nowMilliseconds)
            {
                IsWolvesDenTesting = wolvesDenContextThisFrame,
            });
    }

    private MiracleFollowupPromotion? ObserveCleanseFollowup(
        IPlayerCharacter localPlayer,
        bool configurationEnabled,
        bool higherPriorityClaimed,
        MiracleCleanseFollowupSignal? newSignal,
        long nowMilliseconds,
        int? trackedSlot,
        EmergencyActionInputFrame inputFrame,
        int episodeGameplayKeyToken)
    {
        var enemySlot = trackedSlot ?? 0;
        EnemyHudSnapshot? canonical = null;
        var signalWasNew = false;
        if (newSignal is { } exactSignal)
        {
            if (wolvesDenContextThisFrame)
            {
                if (ResolveCleanseFollowupCandidate(localPlayer, exactSignal.Target) is null)
                    return null;
                enemySlot = WolvesDenCurrentTargetStateKey;
            }
            else
            {
                canonical = ResolveCanonicalEnemy(exactSignal.Target);
                if (canonical is null ||
                    !EnemySlotRules.IsValidSlot(canonical.Slot))
                {
                    return null;
                }

                enemySlot = canonical.Slot;
            }

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
            canonical = wolvesDenContextThisFrame
                ? null
                : ResolveCanonicalEnemy(targetIdentity);
            player = ResolveCleanseFollowupCandidate(localPlayer, targetIdentity);
            if ((wolvesDenContextThisFrame || canonical is not null) && player is not null)
            {
                candidate = new MiracleCleanseFollowupCandidate(
                    targetIdentity,
                    IsExactCanonicalEnemy: true,
                    IsAliveAndTargetable: true,
                    ActiveResilienceStatusCount: CountActiveStatuses(
                        player,
                        EnemyCombatConstants.ResilienceStatusId,
                        out var resilienceRemainingMilliseconds))
                {
                    ResilienceRemainingMilliseconds = resilienceRemainingMilliseconds,
                    ReservationGameplayKeyToken = episodeGameplayKeyToken,
                    ReservedGameplayKeyPhysicallyDown = IsReservedGameplayKeyPhysicallyDown(
                        previous.GameplayKeyToken > 0
                            ? previous.GameplayKeyToken
                            : episodeGameplayKeyToken,
                        inputFrame),
                    CounterActionReachable = IsCounterActionReachable(
                        localPlayer,
                        player),
                };
                teamTargetCountKnown = !wolvesDenContextThisFrame &&
                    TryGetFreshTeamTargetCount(
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
                IsCrystallineConflict: !wolvesDenContextThisFrame,
                IsLocalCounterJobValid: true,
                higherPriorityClaimed,
                newSignal,
                candidate,
                teamTargetCountKnown,
                cleanseFollowupTeamPressure,
                nowMilliseconds)
            {
                IsWolvesDenTesting = wolvesDenContextThisFrame,
            });

        if (signalWasNew ||
            decision.Kind is MiracleCleanseFollowupDecisionKind.ResilienceObserved or
                MiracleCleanseFollowupDecisionKind.ReadyForPromotion or
                MiracleCleanseFollowupDecisionKind.Cancelled ||
            previous.Phase != decision.NextState.Phase)
        {
            log.Information(
                "Seiton Sense reactive CC memory: source=Purify target={TargetEntityId:X8} " +
                "kind={DecisionKind} phase={PreviousPhase}->{NextPhase} key={KeyToken} " +
                "reachable={Reachable} reason={CancelReason}",
                target?.EntityId ?? 0,
                decision.Kind,
                previous.Phase,
                decision.NextState.Phase,
                decision.NextState.GameplayKeyToken,
                candidate?.CounterActionReachable ?? false,
                decision.CancelReason);
        }

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

        if (promotion.GameplayKeyToken <= 0)
        {
            Interlocked.Increment(ref cleanseFollowupCancellationCount);
            Interlocked.Increment(ref rejectedThreatCount);
            cleanseFollowupLastEvent =
                "PostPurifyCC: release retired because no exact key was bound at Resilience end";
            return null;
        }

        Interlocked.Increment(ref cleanseFollowupPromotionCount);
        canonical = wolvesDenContextThisFrame
            ? null
            : ResolveCanonicalEnemy(promotion.Target);
        player = ResolveCleanseFollowupCandidate(localPlayer, promotion.Target);
        if ((!wolvesDenContextThisFrame && canonical is null) || player is null)
        {
            Interlocked.Increment(ref cleanseFollowupCancellationCount);
            Interlocked.Increment(ref rejectedThreatCount);
            cleanseFollowupLastEvent =
                "PostPurifyCC: promotion retired because the exact actor changed";
            return null;
        }

        teamTargetCountKnown = !wolvesDenContextThisFrame &&
            TryGetFreshTeamTargetCount(
                localPlayer,
                player,
                nowMilliseconds,
                out cleanseFollowupTeamPressure);
        var hasTrustedMp = wolvesDenContextThisFrame
            ? player.MaxMp == CombatFrameRules.ExpectedMaximumMp &&
              player.CurrentMp <= player.MaxMp
            : canonical!.HasTrustedMp;
        var rank = new MiracleProtectionEndRankCandidate(
            MiracleInterceptThreatKind.PostPurifyCrowdControl,
            wolvesDenContextThisFrame
                ? WolvesDenCurrentTargetStateKey
                : canonical!.Slot,
            promotion.Target.GameObjectId,
            promotion.Target.EntityId,
            promotion.Target.JobId,
            teamTargetCountKnown,
            cleanseFollowupTeamPressure,
            player.CurrentHp,
            player.MaxHp,
            hasTrustedMp,
            hasTrustedMp
                ? wolvesDenContextThisFrame ? player.CurrentMp : canonical!.CurrentMp
                : 0,
            hasTrustedMp
                ? wolvesDenContextThisFrame ? player.MaxMp : canonical!.MaxMp
                : 0);
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
            rank.EnemySlot,
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
            promotion.GameplayKeyToken,
            counterActionMaximumRangeYalms);

        cleanseFollowupRemovedStatusId = promotionSignal.Key.EffectValue;
        return new MiracleFollowupPromotion(threat, rank);
    }

    private MiracleFollowupPromotion? ObserveGuardFollowup(
        IPlayerCharacter localPlayer,
        bool configurationEnabled,
        bool higherPriorityClaimed,
        long nowMilliseconds,
        EmergencyActionInputFrame inputFrame,
        int episodeGameplayKeyToken)
    {
        var candidates = BuildGuardFollowupCandidates(
            localPlayer,
            nowMilliseconds,
            inputFrame,
            episodeGameplayKeyToken);
        var previous = guardFollowupState;
        var decision = MiracleGuardFollowupRules.Observe(
            guardFollowupState,
            new MiracleGuardFollowupObservation(
                configurationEnabled,
                IsCrystallineConflict: !wolvesDenContextThisFrame,
                IsLocalCounterJobValid: true,
                higherPriorityClaimed,
                candidates,
                nowMilliseconds)
            {
                IsWolvesDenTesting = wolvesDenContextThisFrame,
            });
        guardFollowupState = decision.NextState;

        if (decision.NewGuardEpisodeCount > 0 ||
            decision.NewReleaseOpportunityCount > 0 ||
            decision.ExpiredOpportunityCount > 0 ||
            decision.ShouldPromote ||
            (decision.Kind == MiracleGuardFollowupDecisionKind.Cancelled &&
             previous.Actors.Length > 0))
        {
            log.Information(
                "Seiton Sense reactive CC memory: source=Guard actors={PreviousActors}->{NextActors} " +
                "episodes={Episodes} releases={Releases} expired={Expired} promoted={Promoted} " +
                "target={TargetEntityId:X8} key={KeyToken} reason={CancelReason}",
                previous.Actors.Length,
                decision.NextState.Actors.Length,
                decision.NewGuardEpisodeCount,
                decision.NewReleaseOpportunityCount,
                decision.ExpiredOpportunityCount,
                decision.ShouldPromote,
                decision.PromotionIntent?.Target.EntityId ?? 0,
                decision.PromotionIntent?.GameplayKeyToken ?? 0,
                decision.CancelReason);
        }

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

        if (promotion.GameplayKeyToken <= 0)
        {
            Interlocked.Increment(ref guardFollowupRetiredCount);
            Interlocked.Increment(ref rejectedThreatCount);
            guardFollowupLastEvent =
                "GuardEndCC: release retired because no exact key was bound at Guard end";
            return null;
        }

        Interlocked.Increment(ref guardFollowupPromotionCount);
        var canonical = wolvesDenContextThisFrame
            ? null
            : ResolveCanonicalEnemy(promotion.Target);
        var player = ResolveGuardFollowupCandidate(localPlayer, promotion.Target);
        if ((!wolvesDenContextThisFrame && canonical is null) ||
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
            promotion.GameplayKeyToken,
            counterActionMaximumRangeYalms);
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
            if (decision.Confirmed)
            {
                log.Information(
                    "Seiton Sense reactive CC confirmed: action={ActionId} target={TargetEntityId:X8} " +
                    "status={StatusId} sourceSequence={SourceSequence}",
                    effect.ActionId,
                    effect.TargetEntityId,
                    effect.EffectValue,
                    effect.SourceSequence);
            }
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

    private EnemyHudSnapshot? ResolveUniqueCanonicalCleanseEnemy(uint casterEntityId)
    {
        if (!executeTracker.IsActive) return null;
        var enemies = executeTracker.Enemies
            .Where(static enemy => EnemySlotRules.IsValidSlot(enemy.Slot))
            .ToArray();
        var matches = enemies
            .Where(enemy =>
                enemy.EntityId == casterEntityId &&
                enemy.JobId != 0 &&
                TargetHighlightRules.IsValidGameObjectId(enemy.GameObjectId))
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

    private bool TryRefreshAndResolveFrozenThreat(
        IPlayerCharacter localPlayer,
        MiracleThreatState threat,
        out MiracleThreatState refreshedThreat,
        out IPlayerCharacter? candidate)
    {
        refreshedThreat = threat;
        candidate = null;
        var currentLocalJobId = localPlayer.ClassJob.IsValid
            ? localPlayer.ClassJob.RowId
            : 0;
        if (currentLocalJobId == EnemyCombatConstants.NinjaJobId &&
            refreshedThreat.CounterActionId ==
            EnemyCombatConstants.NinjaAeolianEdgeComboCarrierActionId &&
            IsExactRaijuAction(counterActionId))
        {
            refreshedThreat = refreshedThreat with { CounterActionId = counterActionId };
        }

        if (refreshedThreat.CounterActionId != counterActionId ||
            refreshedThreat.LocalJobId != currentLocalJobId)
        {
            return false;
        }

        candidate = ResolveCandidate(localPlayer, refreshedThreat);
        return candidate is not null;
    }

    private IReadOnlyList<MiracleGuardFollowupCandidate> BuildGuardFollowupCandidates(
        IPlayerCharacter localPlayer,
        long nowMilliseconds,
        EmergencyActionInputFrame inputFrame,
        int episodeGameplayKeyToken)
    {
        if (wolvesDenContextThisFrame)
        {
            return BuildWolvesDenGuardFollowupCandidates(
                localPlayer,
                inputFrame,
                episodeGameplayKeyToken);
        }

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
            var previousActor = guardFollowupState.Actors
                .Where(actor => actor.Target == target)
                .Take(2)
                .ToArray();
            var ownedGameplayKeyToken = previousActor.Length == 1 &&
                                        previousActor[0].GameplayKeyToken > 0
                ? previousActor[0].GameplayKeyToken
                : episodeGameplayKeyToken;
            var teamTargetCount = 0;
            var teamTargetCountKnown = live &&
                TryGetFreshTeamTargetCount(
                    localPlayer,
                    player!,
                    nowMilliseconds,
                    out teamTargetCount);
            var guardRemainingMilliseconds = 0L;
            var guardCount = live
                ? CountActiveGuardStatuses(player!, out guardRemainingMilliseconds)
                : -1;
            candidates.Add(new MiracleGuardFollowupCandidate(
                target,
                exactCanonical,
                live,
                guardCount,
                live ? player!.CurrentHp : 0,
                live ? player!.MaxHp : 0,
                teamTargetCountKnown,
                teamTargetCountKnown ? teamTargetCount : 0)
            {
                HasTrustedMp = live && enemy.HasTrustedMp,
                CurrentMp = live ? enemy.CurrentMp : 0,
                MaximumMp = live ? enemy.MaxMp : 0,
                GuardRemainingMilliseconds = live ? guardRemainingMilliseconds : 0,
                ReservationGameplayKeyToken = episodeGameplayKeyToken,
                ReservedGameplayKeyPhysicallyDown = IsReservedGameplayKeyPhysicallyDown(
                    ownedGameplayKeyToken,
                    inputFrame),
                CounterActionReachable = live && IsCounterActionReachable(
                    localPlayer,
                    player!),
            });
        }

        return candidates;
    }

    private IReadOnlyList<MiracleGuardFollowupCandidate>
        BuildWolvesDenGuardFollowupCandidates(
            IPlayerCharacter localPlayer,
            EmergencyActionInputFrame inputFrame,
            int episodeGameplayKeyToken)
    {
        var player = ResolveExactWolvesDenCurrentTarget(localPlayer);
        if (player is null) return [];

        var target = new MiracleGuardFollowupTargetIdentity(
            WolvesDenCurrentTargetStateKey,
            player.GameObjectId,
            player.EntityId,
            player.ClassJob.RowId);
        var previousActor = guardFollowupState.Actors
            .Where(actor => actor.Target == target)
            .Take(2)
            .ToArray();
        var ownedGameplayKeyToken = previousActor.Length == 1 &&
                                    previousActor[0].GameplayKeyToken > 0
            ? previousActor[0].GameplayKeyToken
            : episodeGameplayKeyToken;
        var guardCount = CountActiveGuardStatuses(
            player,
            out var guardRemainingMilliseconds);
        var hasTrustedMp = player.MaxMp == CombatFrameRules.ExpectedMaximumMp &&
                           player.CurrentMp <= player.MaxMp;
        return
        [
            new MiracleGuardFollowupCandidate(
                target,
                IsExactCanonicalEnemy: true,
                IsAliveAndTargetable: true,
                guardCount,
                player.CurrentHp,
                player.MaxHp,
                TeamTargetCountKnown: false,
                TeamTargetCount: 0)
            {
                HasTrustedMp = hasTrustedMp,
                CurrentMp = hasTrustedMp ? player.CurrentMp : 0,
                MaximumMp = hasTrustedMp ? player.MaxMp : 0,
                GuardRemainingMilliseconds = guardRemainingMilliseconds,
                ReservationGameplayKeyToken = episodeGameplayKeyToken,
                ReservedGameplayKeyPhysicallyDown = IsReservedGameplayKeyPhysicallyDown(
                    ownedGameplayKeyToken,
                    inputFrame),
                CounterActionReachable = IsCounterActionReachable(
                    localPlayer,
                    player),
            },
        ];
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
        if (wolvesDenContextThisFrame)
        {
            return ResolveExactWolvesDenCurrentTarget(
                localPlayer,
                target.EntityId,
                target.GameObjectId,
                target.EntityId,
                target.JobId);
        }

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
        if (wolvesDenContextThisFrame)
        {
            return ResolveExactWolvesDenCurrentTarget(
                localPlayer,
                target.EntityId,
                target.GameObjectId,
                target.EntityId,
                target.JobId);
        }

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
        if (wolvesDenContextThisFrame)
        {
            return ResolveExactWolvesDenCurrentTarget(
                localPlayer,
                threat.EntityId,
                threat.GameObjectId,
                threat.EntityId,
                threat.JobId);
        }

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

    private IPlayerCharacter? ResolveExactWolvesDenCurrentTarget(
        IPlayerCharacter localPlayer,
        uint expectedActorEntityId = 0,
        ulong expectedGameObjectId = 0,
        uint expectedEntityId = 0,
        uint expectedJobId = 0)
    {
        if (!wolvesDenContextThisFrame ||
            wolvesDenCurrentHardTargetThisFrame is not { } current ||
            !current.ClassJob.IsValid ||
            current.GameObjectId == localPlayer.GameObjectId ||
            !HasValidNativeIdentity(current) ||
            !IsLivePlayer(current))
        {
            return null;
        }

        var currentJobId = current.ClassJob.RowId;
        if (expectedActorEntityId != 0 &&
            current.EntityId != expectedActorEntityId)
        {
            return null;
        }

        if (expectedGameObjectId != 0 ||
            expectedEntityId != 0 ||
            expectedJobId != 0)
        {
            var actor = expectedActorEntityId != 0
                ? expectedActorEntityId
                : expectedEntityId;
            if (!ReactiveCounterCcProfileRules.IsExactWolvesDenCurrentTarget(
                    actor,
                    expectedGameObjectId,
                    expectedEntityId,
                    expectedJobId,
                    current.GameObjectId,
                    current.EntityId,
                    currentJobId))
            {
                return null;
            }
        }

        var matches = objectTable.PlayerObjects
            .OfType<IPlayerCharacter>()
            .Where(player =>
                player.GameObjectId == current.GameObjectId &&
                player.EntityId == current.EntityId &&
                player.ClassJob.IsValid &&
                player.ClassJob.RowId == currentJobId)
            .Take(2)
            .ToArray();
        return matches.Length == 1 &&
               matches[0].GameObjectId != localPlayer.GameObjectId &&
               HasValidNativeIdentity(matches[0]) &&
               IsLivePlayer(matches[0])
            ? matches[0]
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

    private static int CountActiveStatuses(
        IPlayerCharacter player,
        uint statusId,
        out long longestRemainingMilliseconds)
    {
        longestRemainingMilliseconds = 0;
        var count = 0;
        foreach (var status in player.StatusList)
        {
            if (status.StatusId != statusId) continue;
            count++;
            longestRemainingMilliseconds = Math.Max(
                longestRemainingMilliseconds,
                ValidatedProtectionRemainingMilliseconds(
                    status.StatusId,
                    status.RemainingTime));
            if (count > 1) return count;
        }

        return count;
    }

    private static int CountActiveGuardStatuses(IPlayerCharacter player) =>
        CountActiveGuardStatuses(player, out _);

    private static int CountActiveGuardStatuses(
        IPlayerCharacter player,
        out long longestRemainingMilliseconds)
    {
        longestRemainingMilliseconds = 0;
        var count = 0;
        foreach (var status in player.StatusList)
        {
            if (!MiracleGuardFollowupRules.IsExactGuardStatus(status.StatusId)) continue;
            count++;
            longestRemainingMilliseconds = Math.Max(
                longestRemainingMilliseconds,
                ValidatedProtectionRemainingMilliseconds(
                    status.StatusId,
                    status.RemainingTime));
            if (count > 1) return count;
        }

        return count;
    }

    private static long ValidatedProtectionRemainingMilliseconds(
        uint statusId,
        float remainingSeconds)
    {
        if (!CcProtectionStatusCatalog.TryGet(statusId, out var definition) ||
            !float.IsFinite(remainingSeconds) ||
            remainingSeconds <= 0f ||
            remainingSeconds > definition.MaximumRemainingTime)
        {
            return 0;
        }

        return Math.Max(1L, (long)Math.Ceiling((double)remainingSeconds * 1_000d));
    }

    private unsafe bool HasActionRangeAndLineOfSight(
        uint actionId,
        IPlayerCharacter localPlayer,
        IPlayerCharacter target,
        float maximumRangeYalms = float.PositiveInfinity)
    {
        if (MiracleInterceptConfirmationRules.ExpectedStatusForAction(actionId) == 0)
            return false;

        if (!IsInsideConfiguredRange(localPlayer, target, maximumRangeYalms))
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
        EmergencyActionInputFrame inputFrame,
        bool hardReset)
    {
        var input = inputFrame.Snapshot;
        var latchedKeyPhysicallyDown =
            TryGetLatchedProtectionEndKey(out var previousKey) &&
            inputFrame.IsGameplayKeyPhysicallyDown(previousKey);
        var eligibleKey = VirtualKey.NO_KEY;
        if (enabled && !input.IsTextInputActive)
        {
            // Read the immutable raw frame snapshot so a same-frame Purify claim
            // cannot hide the held generation from a future protection episode.
            var observedKey = input.ProbeSucceeded && input.HeldGameplayKeyEligible
                ? input.HeldGameplayKey
                : input.ProbeSucceeded && input.FreshGameplayKeyPressed
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

    private int ResolveEpisodeGameplayKeyToken(
        bool allowHeldGameplayKey,
        EmergencyActionInputFrame inputFrame)
    {
        var input = inputFrame.Snapshot;
        if (!input.ProbeSucceeded || input.IsTextInputActive) return 0;
        if (allowHeldGameplayKey &&
            TryGetLatchedProtectionEndKey(out var latchedKey) &&
            inputFrame.IsGameplayKeyGenerationEligible(latchedKey))
        {
            return (int)latchedKey;
        }

        var candidate = allowHeldGameplayKey && input.HeldGameplayKeyEligible
            ? input.HeldGameplayKey
            : input.FreshGameplayKeyPressed
                ? input.FreshGameplayKey
                : VirtualKey.NO_KEY;
        return IsExactVirtualKey(candidate) &&
               inputFrame.IsGameplayKeyGenerationEligible(candidate)
            ? (int)candidate
            : 0;
    }

    private static bool IsReservedGameplayKeyPhysicallyDown(
        int gameplayKeyToken,
        EmergencyActionInputFrame inputFrame)
    {
        if (gameplayKeyToken <= 0) return false;
        var key = (VirtualKey)gameplayKeyToken;
        return IsExactVirtualKey(key) && inputFrame.IsGameplayKeyGenerationEligible(key);
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
        out bool attempted,
        out ushort expectedSourceSequence)
    {
        attempted = false;
        expectedSourceSequence = 0;
        if (MiracleInterceptConfirmationRules.ExpectedStatusForAction(actionId) == 0 ||
            !TargetHighlightRules.IsValidGameObjectId(targetGameObjectId))
        {
            return ClientActionAttemptOutcome.NotInvoked;
        }

        var actionManager = ActionManager.Instance();
        if (actionManager == null ||
            !HasStructuralActionReadiness(localPlayer, actionId) ||
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
        var boundaryAfter = ClientActionAttemptBoundary.Capture(actionManager, actionId);
        if (accepted &&
            boundaryAfter.LastUsedActionSequence != 0 &&
            boundaryAfter.LastUsedActionSequence != boundaryBefore.LastUsedActionSequence)
        {
            expectedSourceSequence = boundaryAfter.LastUsedActionSequence;
        }

        return ClientActionAttemptBoundaryRules.Classify(
            accepted,
            actionId,
            boundaryBefore,
            boundaryAfter);
    }

    private bool IsCounterActionReachable(
        IPlayerCharacter localPlayer,
        IPlayerCharacter candidate)
    {
        var rangeActionId = counterActionId == EnemyCombatConstants.NinjaAeolianEdgeComboCarrierActionId
            ? EnemyCombatConstants.ForkedRaijuActionId
            : counterActionId;
        return MiracleInterceptConfirmationRules.ExpectedStatusForAction(rangeActionId) != 0 &&
               !HasAnyVerifiedCcProtection(
                   candidate,
                   BlockerFamilyForAction(rangeActionId)) &&
               HasActionRangeAndLineOfSight(
                   rangeActionId,
                   localPlayer,
                   candidate,
                   counterActionMaximumRangeYalms);
    }

    private static unsafe bool HasStructuralActionReadiness(
        IPlayerCharacter localPlayer,
        uint actionId)
    {
        if (MiracleInterceptConfirmationRules.ExpectedStatusForAction(actionId) == 0)
            return false;

        // PvP Forked Raiju can remain exposed by the combo carrier while the
        // exact local "Sealed Forked Raiju" row forbids its execution. Live
        // StatusList membership is authoritative; duration telemetry is not.
        if (actionId == EnemyCombatConstants.ForkedRaijuActionId &&
            localPlayer.StatusList.Any(static status =>
                status.StatusId == EnemyCombatConstants.SealedForkedRaijuStatusId))
        {
            return false;
        }

        // Both PvP Raiju variants are movement attacks and explicitly cannot
        // execute while Bound. Membership of the exact live Bind row is the
        // authority; duration telemetry is deliberately ignored.
        if (CannotExecuteWhileBound(actionId) &&
            localPlayer.StatusList.Any(static status =>
                status.StatusId == EnemyCombatConstants.PvPBindStatusId))
        {
            return false;
        }

        var actionManager = ActionManager.Instance();
        var adjustedActionId = actionManager == null
            ? 0
            : IsExactRaijuAction(actionId)
                ? actionManager->GetAdjustedActionId(
                    EnemyCombatConstants.NinjaAeolianEdgeComboCarrierActionId)
                : actionManager->GetAdjustedActionId(actionId);
        return actionManager != null &&
               adjustedActionId == actionId &&
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

    private unsafe uint ResolveCounterActionId(
        uint localJobId,
        bool miracleMetadataVerified,
        bool silentNocturneMetadataVerified,
        bool enablePaladinIntervene,
        bool enableRedMageResolution,
        bool redMageResolutionMetadataVerified)
    {
        if (localJobId == EnemyCombatConstants.NinjaJobId)
        {
            if (!verifiedCounterActionIds.Contains(
                    EnemyCombatConstants.ForkedRaijuActionId) ||
                !verifiedCounterActionIds.Contains(
                    EnemyCombatConstants.FleetingRaijuActionId))
            {
                return 0;
            }

            var actionManager = ActionManager.Instance();
            if (actionManager == null)
                return EnemyCombatConstants.NinjaAeolianEdgeComboCarrierActionId;
            var adjustedActionId = actionManager->GetAdjustedActionId(
                EnemyCombatConstants.NinjaAeolianEdgeComboCarrierActionId);
            return IsExactRaijuAction(adjustedActionId) &&
                   verifiedCounterActionIds.Contains(adjustedActionId)
                ? adjustedActionId
                : EnemyCombatConstants.NinjaAeolianEdgeComboCarrierActionId;
        }

        return localJobId switch
        {
            ReactiveCounterCcProfileRules.PaladinJobId when
                enablePaladinIntervene &&
                verifiedCounterActionIds.Contains(
                    MiracleInterceptConfirmationRules.InterveneActionId) =>
                MiracleInterceptConfirmationRules.InterveneActionId,
            EnemyCombatConstants.WhiteMageJobId when miracleMetadataVerified =>
                EnemyCombatConstants.MiracleOfNatureActionId,
            EnemyCombatConstants.BardJobId when silentNocturneMetadataVerified =>
                EnemyCombatConstants.SilentNocturneActionId,
            ReactiveCounterCcProfileRules.RedMageJobId when
                enableRedMageResolution &&
                redMageResolutionMetadataVerified =>
                MiracleInterceptConfirmationRules.ResolutionActionId,
            _ => 0,
        };
    }

    private static float ResolveCounterMaximumRangeYalms(
        uint actionId,
        float paladinInterveneMaximumRangeYalms) =>
        actionId == MiracleInterceptConfirmationRules.InterveneActionId
            ? ReactiveCounterCcProfileRules.NormalizeInterveneMaximumRangeYalms(
                paladinInterveneMaximumRangeYalms)
            : ReactiveCounterCcProfileRules.Get(actionId)?.NativeMaximumRangeYalms ??
              float.PositiveInfinity;

    private static CcImmunityBrakeBlockerFamily BlockerFamilyForAction(uint actionId) =>
        actionId == EnemyCombatConstants.MiracleOfNatureActionId
            ? CcImmunityBrakeBlockerFamily.Miracle
            : CcImmunityBrakeBlockerFamily.StandardPurifyCc;

    private static IReadOnlyList<uint> RequiredProtectionStatusIds(uint actionId) =>
        actionId switch
        {
            EnemyCombatConstants.MiracleOfNatureActionId => RequiredMiracleProtectionStatusIds,
            EnemyCombatConstants.SilentNocturneActionId or
            EnemyCombatConstants.ForkedRaijuActionId or
            EnemyCombatConstants.FleetingRaijuActionId or
            EnemyCombatConstants.NinjaAeolianEdgeComboCarrierActionId or
            MiracleInterceptConfirmationRules.InterveneActionId or
            MiracleInterceptConfirmationRules.ResolutionActionId =>
                RequiredSilentProtectionStatusIds,
            _ => Array.Empty<uint>(),
        };

    private static bool IsExactRaijuAction(uint actionId) =>
        actionId is EnemyCombatConstants.ForkedRaijuActionId or
            EnemyCombatConstants.FleetingRaijuActionId;

    private static bool CannotExecuteWhileBound(uint actionId) =>
        ReactiveCounterCcProfileRules.Get(actionId)?.CannotExecuteWhileBound ??
        IsExactRaijuAction(actionId);

    private static bool IsInsideConfiguredRange(
        IPlayerCharacter localPlayer,
        IPlayerCharacter target,
        float maximumRangeYalms)
    {
        if (float.IsPositiveInfinity(maximumRangeYalms)) return true;
        if (!float.IsFinite(maximumRangeYalms) || maximumRangeYalms <= 0f ||
            !float.IsFinite(localPlayer.HitboxRadius) ||
            localPlayer.HitboxRadius < 0f ||
            !float.IsFinite(target.HitboxRadius) ||
            target.HitboxRadius < 0f)
        {
            return false;
        }

        var centerDistance = Vector3.Distance(localPlayer.Position, target.Position);
        if (!float.IsFinite(centerDistance)) return false;
        var edgeDistance = MathF.Max(
            0f,
            centerDistance - localPlayer.HitboxRadius - target.HitboxRadius);
        return edgeDistance <= maximumRangeYalms;
    }

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
        pendingCleanseTargetResolutions.Clear();
        cleanseFollowupSignalLedger = MiracleCleanseFollowupSignalLedger.Initial;
    }

    private MiracleInterceptProbeSnapshot Publish(
        string phase,
        string lastEvent,
        long nowMilliseconds)
    {
        var remaining = activeThreat is { } threat
            ? Math.Max(0, ThreatLifetime(threat) -
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
            Math.Max(0, ThreatLifetime(threat) -
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
        counterActionMaximumRangeYalms = float.PositiveInfinity;
        wolvesDenContextThisFrame = false;
        wolvesDenCurrentHardTargetThisFrame = null;
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
        var diagnosticGuardState = guardFollowupState.Actors
            .OrderByDescending(static actor => actor.Phase)
            .ThenBy(static actor => actor.Target.EnemySlot)
            .Select(static actor => (MiracleGuardFollowupActorState?)actor)
            .FirstOrDefault();
        var reservedKeyToken = activeThreat is { } reservedThreat &&
                               IsProtectionEndThreat(reservedThreat.Kind)
            ? reservedThreat.GameplayKeyToken
            : diagnosticCleanseState.GameplayKeyToken > 0
                ? diagnosticCleanseState.GameplayKeyToken
                : diagnosticGuardState?.GameplayKeyToken ?? 0;
        var expectedProtectionEnd = diagnosticCleanseState.ExpectedProtectionEndAtMilliseconds > 0
            ? diagnosticCleanseState.ExpectedProtectionEndAtMilliseconds
            : diagnosticGuardState?.ExpectedProtectionEndAtMilliseconds ?? -1;
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
            ProtectionEndReservedKey = IsExactVirtualKey((VirtualKey)reservedKeyToken)
                ? (VirtualKey)reservedKeyToken
                : VirtualKey.NO_KEY,
            ProtectionEndExpectedRemainingMilliseconds = expectedProtectionEnd > 0
                ? Math.Max(0, expectedProtectionEnd - Environment.TickCount64)
                : 0,
            ProtectionEndRankTeamPressureKnown =
                protectionEndLastRank?.TeamTargetCountKnown ?? false,
            ProtectionEndRankTeamPressure = protectionEndLastRank?.TeamTargetCount ?? 0,
            ProtectionEndRankCurrentHp = protectionEndLastRank?.CurrentHp ?? 0,
            ProtectionEndRankMaximumHp = protectionEndLastRank?.MaximumHp ?? 0,
            ProtectionEndRankMpKnown = protectionEndLastRank?.HasTrustedMp ?? false,
            ProtectionEndRankCurrentMp = protectionEndLastRank?.CurrentMp ?? 0,
            ProtectionEndRankMaximumMp = protectionEndLastRank?.MaximumMp ?? 0,
            ConfirmationPendingCount = confirmationState.Pending is null ? 0 : 1,
            ConfirmationAwaitingSourceSequence =
                confirmationState.Pending is { HasBoundSourceSequence: false },
            ConfirmationPendingActionId = confirmationState.Pending?.ActionId ?? 0,
            WolvesDenCurrentTargetMode = wolvesDenContextThisFrame,
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
            !inputFrame.IsGameplayKeyGenerationEligible(frozenKey) ||
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

    private static long ThreatLifetime(MiracleThreatState threat) =>
        IsProtectionEndThreat(threat.Kind) &&
        threat.LocalJobId == EnemyCombatConstants.NinjaJobId
            ? MiracleProtectionEndRules.NinjaWeaponskillHeldLeaseMilliseconds
            : ThreatLifetime(threat.Kind);

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
        int GameplayKeyToken,
        float MaximumRangeYalms);

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
