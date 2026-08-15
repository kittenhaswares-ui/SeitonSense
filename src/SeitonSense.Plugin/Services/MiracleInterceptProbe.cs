using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
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
/// Experimental CC-only WHM/BRD helper. It consumes at most one shared physical
/// gameplay-key generation and makes one exact-target Miracle or Silent Nocturne call.
/// It never changes the selected target and never retries.
/// </summary>
internal sealed class MiracleInterceptProbe
{
    private const int MaximumRememberedSignals = 128;
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
    private MiracleThreatState? activeThreat;
    private MiracleInterceptConfirmationState confirmationState =
        MiracleInterceptConfirmationState.Initial;
    private MiracleCleanseFollowupState cleanseFollowupState =
        MiracleCleanseFollowupState.Initial;
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
        bool marksmanSpiteMetadataVerified,
        bool zantetsukenMetadataVerified,
        bool furiousBacklashMetadataVerified,
        bool miracleMetadataVerified,
        bool purifyMetadataVerified,
        EmergencyActionInputFrame inputFrame,
        long nowMilliseconds,
        bool hardReset = false)
    {
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
            cleanseFollowupState = MiracleCleanseFollowupState.Initial;
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
            cleanseFollowupState = MiracleCleanseFollowupState.Initial;
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
            counterActionId == EnemyCombatConstants.MiracleOfNatureActionId &&
            enableMarksmanSpite &&
            marksmanSpiteMetadataVerified;
        var zantetsukenEnabled =
            counterActionId == EnemyCombatConstants.MiracleOfNatureActionId &&
            enableZantetsuken &&
            zantetsukenMetadataVerified;
        var furiousBacklashEnabled =
            counterActionId == EnemyCombatConstants.MiracleOfNatureActionId &&
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
                cleanseFollowupEnabled))
        {
            lastOpportunity = $"{disabledThreat.Kind}: retired because its trigger was disabled";
            activeThreat = null;
        }

        foreach (var cleanseSignal in cleanseSignals)
        {
            ObserveCleanseFollowup(
                localPlayer!,
                cleanseFollowupEnabled,
                !dispatchAllowed || activeThreat is not null,
                cleanseSignal,
                nowMilliseconds);
        }

        ObserveCleanseFollowup(
            localPlayer!,
            cleanseFollowupEnabled,
            !dispatchAllowed || activeThreat is not null,
            null,
            nowMilliseconds);

        if (activeThreat is not { } threat)
            return Publish("Waiting", "No current exact threat", nowMilliseconds);

        // A transient higher-priority Purify/defense/Rescue claim cannot dispatch a
        // second action from the same physical generation, but it also need not
        // destroy the exact threat. Retain it only inside its original deadline
        // so a genuinely fresh later generation can still act; never replay or
        // extend the opportunity.
        if (!dispatchAllowed)
        {
            RecordWait(threat, MiracleWaitReason.HigherPriorityHelper);
            return Publish("Armed", "Waiting: higher-priority helper claimed this frame", nowMilliseconds);
        }

        var candidate = ResolveCandidate(localPlayer!, threat);
        if (candidate is null)
        {
            Interlocked.Increment(ref rejectedThreatCount);
            lastOpportunity = $"{threat.Kind}: exact enemy identity changed";
            activeThreat = null;
            return Publish("Cancelled", "Exact enemy identity changed", nowMilliseconds);
        }

        var blockerFamily = BlockerFamilyForAction(counterActionId);
        var anyProtection = HasAnyVerifiedCcProtection(candidate, blockerFamily);
        var hardenedScales = threat.Kind == MiracleInterceptThreatKind.FuriousBacklash &&
                             HasVerifiedActiveStatus(
                                 candidate,
                                 EnemyCombatConstants.HardenedScalesStatusId);
        var otherProtection = anyProtection && !hardenedScales;
        var rangeAndLineOfSight = HasActionRangeAndLineOfSight(
            counterActionId,
            localPlayer!,
            candidate);
        var teamFocus = threat.Kind != MiracleInterceptThreatKind.PostPurifyCrowdControl ||
                        HasExactTeamFocus(localPlayer!, candidate, out cleanseFollowupTeamPressure);
        var locallyReady = !hardenedScales &&
                           !otherProtection &&
                           rangeAndLineOfSight &&
                           teamFocus &&
                           ActionManager.Instance() != null;

        var input = inputFrame.Snapshot;
        var triggerKey = inputFrame.FreshGameplayKeyPressed
            ? input.FreshGameplayKey
            : allowHeldGameplayKey && inputFrame.HeldGameplayKeyEligible
                ? input.HeldGameplayKey
                : VirtualKey.NO_KEY;
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

        if (!locallyReady)
        {
            RecordWait(
                threat,
                hardenedScales
                    ? MiracleWaitReason.HardenedScales
                    : otherProtection
                        ? MiracleWaitReason.OtherProtection
                        : !rangeAndLineOfSight
                            ? MiracleWaitReason.RangeOrLineOfSight
                            : MiracleWaitReason.TeamFocus);
            // Keep the generation available while protection is genuinely
            // present or the exact enemy is briefly out of native action range/LoS.
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
                        : "Waiting: exact team focus below 2",
                triggerKey,
                false,
                false,
                hardenedScales,
                otherProtection,
                rangeAndLineOfSight,
                nowMilliseconds);
        }

        // Terminal state and shared input are committed before final validation
        // and the sole native call. Any false return or exception cannot retry.
        activeThreat = null;
        inputFrame.Consume();
        var attempted = false;
        var accepted = false;
        var attemptedAtMilliseconds = -1L;
        var revalidated = ResolveCandidate(localPlayer!, threat);
        var revalidatedHardened = revalidated is not null &&
                                  threat.Kind == MiracleInterceptThreatKind.FuriousBacklash &&
                                  HasVerifiedActiveStatus(
                                      revalidated,
                                      EnemyCombatConstants.HardenedScalesStatusId);
        var revalidatedProtection = revalidated is not null &&
                                    HasAnyVerifiedCcProtection(revalidated, blockerFamily);
        var revalidatedRange = revalidated is not null &&
                               HasActionRangeAndLineOfSight(
                                   counterActionId,
                                   localPlayer!,
                                   revalidated);
        var revalidatedTeamFocus = revalidated is not null &&
            (threat.Kind != MiracleInterceptThreatKind.PostPurifyCrowdControl ||
             HasExactTeamFocus(localPlayer!, revalidated, out cleanseFollowupTeamPressure));
        if (revalidated is not null &&
            !revalidatedHardened &&
            !revalidatedProtection &&
            revalidatedRange &&
            revalidatedTeamFocus)
        {
            try
            {
                attemptedAtMilliseconds = Environment.TickCount64;
                accepted = TryUseCounterCcOnce(
                    counterActionId,
                    revalidated.GameObjectId,
                    out attempted);
                if (attempted) Interlocked.Increment(ref attemptCount);
                if (accepted) Interlocked.Increment(ref acceptedCount);
            }
            catch (Exception exception)
            {
                if (attempted) Interlocked.Increment(ref attemptCount);
                LogAttemptFailure(exception, nowMilliseconds);
            }
        }

        if (attempted && revalidated is not null && attemptedAtMilliseconds >= 0)
        {
            var registered = MiracleInterceptConfirmationRules.RegisterAttempt(
                confirmationState,
                new MiracleInterceptPendingAttempt(
                    localPlayer!.EntityId,
                    counterActionId,
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

        lastOpportunity = attempted
            ? $"{threat.Kind}: action {counterActionId} attempted (accepted={accepted})"
            : $"{threat.Kind}: consumed but final identity/range/protection validation changed";

        return PublishCandidate(
            threat,
            candidate,
            attempted ? "Spent" : "Cancelled",
            attempted
                ? accepted ? "Reactive CC accepted locally" : "Reactive CC rejected locally"
                : "Consumed without action: target/range/protection changed",
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

                var canonicalCleanseTarget = ResolveCanonicalEnemy(signal.CasterEntityId);
                if (canonicalCleanseTarget is null)
                {
                    Interlocked.Increment(ref rejectedThreatCount);
                    cleanseFollowupLastEvent =
                        "PostPurifyCC: Purify caster was not one exact canonical e1-e5 enemy";
                    continue;
                }

                cleanseSignals.Add(new MiracleCleanseFollowupSignal(
                    new MiracleCleanseFollowupSignalKey(
                        signal.CasterEntityId,
                        signal.ActionId,
                        signal.EventTargetEntityId,
                        signal.EffectType,
                        signal.EffectValue,
                        signal.GlobalSequence,
                        signal.SourceSequence),
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
                if (MiracleInterceptRules.GetDispatchPriority(kind) <=
                    MiracleInterceptRules.GetDispatchPriority(previousThreat.Kind))
                {
                    Interlocked.Increment(ref rejectedThreatCount);
                    lastOpportunity = $"{kind}: retired behind exact {previousThreat.Kind}";
                    continue;
                }

                Interlocked.Increment(ref rejectedThreatCount);
                lastOpportunity = $"{previousThreat.Kind}: superseded by higher-priority exact {kind} signal";
            }
            activeThreat = new MiracleThreatState(
                kind,
                canonical.GameObjectId,
                canonical.EntityId,
                canonical.JobId,
                signal.ObservedAtMilliseconds,
                identity,
                RemovedStatusId: 0);
            Interlocked.Increment(ref armedThreatCount);
            ResetWaitDiagnostics();
            lastOpportunity = $"{kind}: exact threat armed";
        }

        return cleanseSignals;
    }

    private void ObserveCleanseFollowup(
        IPlayerCharacter localPlayer,
        bool configurationEnabled,
        bool higherPriorityClaimed,
        MiracleCleanseFollowupSignal? newSignal,
        long nowMilliseconds)
    {
        var previous = cleanseFollowupState;
        var target = newSignal?.Target ?? previous.ActiveSignal?.Target;
        MiracleCleanseFollowupCandidate? candidate = null;
        var hasExactTeamFocus = false;
        cleanseFollowupTeamPressure = 0;
        if (target is { } targetIdentity)
        {
            var player = ResolveCleanseFollowupCandidate(localPlayer, targetIdentity);
            if (player is not null)
            {
                candidate = new MiracleCleanseFollowupCandidate(
                    targetIdentity,
                    IsExactCanonicalEnemy: true,
                    IsAliveAndTargetable: true,
                    ActiveResilienceStatusCount: CountActiveStatuses(
                        player,
                        EnemyCombatConstants.ResilienceStatusId));
                hasExactTeamFocus = HasExactTeamFocus(
                    localPlayer,
                    player,
                    out cleanseFollowupTeamPressure);
            }
        }

        var signalKey = default(MiracleCleanseFollowupSignalKey);
        var signalWasNew = false;
        if (newSignal is { } exactSignal)
        {
            signalKey = exactSignal.Key;
            signalWasNew = !previous.ObservedSignals.Contains(signalKey);
        }
        var decision = MiracleCleanseFollowupRules.Observe(
            previous,
            new MiracleCleanseFollowupObservation(
                configurationEnabled,
                IsCrystallineConflict: true,
                IsLocalCounterJobValid: true,
                higherPriorityClaimed,
                newSignal,
                candidate,
                hasExactTeamFocus,
                nowMilliseconds));

        // The exact signal is retired in Core before a promotion can reach the
        // existing single Miracle action boundary.
        cleanseFollowupState = decision.NextState;

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
                "PostPurifyCC: Resilience absent and team focus >=2; promoted to reactive-CC dispatcher",
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
                !hasExactTeamFocus =>
                $"PostPurifyCC: release ready; waiting for exact team focus >=2 (now {cleanseFollowupTeamPressure})",
            MiracleCleanseFollowupDecisionKind.Waiting =>
                $"PostPurifyCC: waiting ({decision.NextState.Phase})",
            _ => cleanseFollowupLastEvent,
        };

        if (!decision.ShouldPromote || decision.PromotionIntent is not { } promotion)
            return;

        Interlocked.Increment(ref cleanseFollowupPromotionCount);
        if (activeThreat is not null)
        {
            // Defensive only: HigherPriorityClaimed prevents Core promotion
            // whenever another reactive-CC opportunity already owns this path.
            Interlocked.Increment(ref cleanseFollowupCancellationCount);
            cleanseFollowupLastEvent = "PostPurifyCC: promotion retired while another threat owned dispatch";
            return;
        }

        var promotionSignal = promotion.Signal;
        activeThreat = new MiracleThreatState(
            MiracleInterceptThreatKind.PostPurifyCrowdControl,
            promotion.Target.GameObjectId,
            promotion.Target.EntityId,
            promotion.Target.JobId,
            promotion.ReleasedAtMilliseconds,
            new MiracleSignalIdentity(
                promotionSignal.Key.CasterEntityId,
                promotionSignal.Key.ActionId,
                promotionSignal.Key.GlobalSequence,
                promotionSignal.Key.SourceSequence),
            promotionSignal.Key.EffectValue);
        cleanseFollowupRemovedStatusId = promotionSignal.Key.EffectValue;
        ResetWaitDiagnostics();
        lastOpportunity = "PostPurifyCC: exact threat armed after verified Resilience absence and team focus";
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
                TargetHighlightRules.IsValidGameObjectId(enemy.GameObjectId))
            .Take(2)
            .ToArray();
        return matches.Length == 1 ? matches[0] : null;
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

    private IPlayerCharacter? ResolveCandidate(
        IPlayerCharacter localPlayer,
        MiracleThreatState threat)
    {
        var canonical = executeTracker.Enemies
            .Where(enemy =>
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

    private static unsafe bool HasExactTeamFocus(
        IPlayerCharacter localPlayer,
        IPlayerCharacter candidate,
        TargetPressureTracker pressureTracker,
        out int totalTargetCount)
    {
        totalTargetCount = 0;
        if (!HasValidNativeIdentity(localPlayer) ||
            !HasValidNativeIdentity(candidate))
        {
            return false;
        }

        var targetId = ((Character*)localPlayer.Address)->GetTargetId();
        if (targetId.Id != candidate.GameObjectId ||
            targetId.ObjectId != candidate.EntityId)
        {
            return false;
        }

        var alliedTargetCount = pressureTracker.GetTeamTargetCount(
            candidate.GameObjectId,
            candidate.EntityId);
        totalTargetCount = 1 + Math.Max(0, alliedTargetCount);
        return alliedTargetCount >= 1;
    }

    private bool HasExactTeamFocus(
        IPlayerCharacter localPlayer,
        IPlayerCharacter candidate,
        out int totalTargetCount) =>
        HasExactTeamFocus(localPlayer, candidate, pressureTracker, out totalTargetCount);

    private unsafe bool TryUseCounterCcOnce(
        uint actionId,
        ulong targetGameObjectId,
        out bool attempted)
    {
        attempted = false;
        if (MiracleInterceptConfirmationRules.ExpectedStatusForAction(actionId) == 0 ||
            !TargetHighlightRules.IsValidGameObjectId(targetGameObjectId))
        {
            return false;
        }

        var actionManager = ActionManager.Instance();
        if (actionManager == null) return false;

        attempted = true;
        return nearAssist.RunWithoutRedirect(() =>
            actionManager->UseAction(
                ActionType.Action,
                actionId,
                targetGameObjectId,
                0,
                ActionManager.UseActionMode.None,
                0));
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
        cleanseFollowupState = MiracleCleanseFollowupState.Initial;
        counterActionId = 0;
        cleanseFollowupRemovedStatusId = 0;
        cleanseFollowupTeamPressure = 0;
        ResetWaitDiagnostics();
        rememberedSignals.Clear();
        rememberedSignalOrder.Clear();
        capture.SetMiracleInterceptLocalEntityId(0);
        capture.SetMiracleCleanseFollowupLocalEntityId(0);
        capture.ClearMiracleInterceptThreats();
        capture.ClearMiracleInterceptConfirmations();
        confirmationState = MiracleInterceptConfirmationRules.ObserveTime(
            confirmationState,
            Environment.TickCount64,
            hardReset: true);
    }

    private MiracleInterceptProbeSnapshot WithOpportunityDiagnostics(
        MiracleInterceptProbeSnapshot value) =>
        value with
        {
            RecognizedThreatCount = Interlocked.Read(ref recognizedThreatCount),
            ArmedThreatCount = Interlocked.Read(ref armedThreatCount),
            RejectedThreatCount = Interlocked.Read(ref rejectedThreatCount),
            PriorityWaitCount = Interlocked.Read(ref priorityWaitCount),
            NoInputWaitCount = Interlocked.Read(ref noInputWaitCount),
            RangeWaitCount = Interlocked.Read(ref rangeWaitCount),
            ProtectionWaitCount = Interlocked.Read(ref protectionWaitCount),
            ExpiredThreatCount = Interlocked.Read(ref expiredThreatCount),
            LastOpportunity = lastOpportunity,
            CleanseFollowupPhase = cleanseFollowupState.Phase,
            CleanseFollowupTargetGameObjectId =
                cleanseFollowupState.ActiveSignal?.Target.GameObjectId ?? 0,
            CleanseFollowupTargetEntityId =
                cleanseFollowupState.ActiveSignal?.Target.EntityId ?? 0,
            CleanseFollowupResilienceObserved = cleanseFollowupState.ResiliencePresenceObserved,
            CleanseFollowupSignalCount = Interlocked.Read(ref cleanseFollowupSignalCount),
            CleanseFollowupPromotionCount = Interlocked.Read(ref cleanseFollowupPromotionCount),
            CleanseFollowupCancellationCount = Interlocked.Read(ref cleanseFollowupCancellationCount),
            CleanseFollowupLastEvent = cleanseFollowupLastEvent,
            CounterActionId = counterActionId,
            CleanseFollowupRemovedStatusId = cleanseFollowupState.ActiveSignal?.Key.EffectValue ??
                                             activeThreat?.RemovedStatusId ??
                                             cleanseFollowupRemovedStatusId,
            CleanseFollowupTeamPressure = cleanseFollowupTeamPressure,
        };

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
        MiracleWaitReason.HigherPriorityHelper => "Purify/defense/Ally Rescue priority",
        MiracleWaitReason.NoEligibleInput => "an eligible held/fresh physical key",
        MiracleWaitReason.TextInput => "text input to close",
        MiracleWaitReason.HardenedScales => "Hardened Scales to disappear",
        MiracleWaitReason.OtherProtection => "a verified counter-CC blocker to disappear",
        MiracleWaitReason.RangeOrLineOfSight => "native action range/line of sight",
        MiracleWaitReason.TeamFocus => "exact local-plus-one-ally team focus",
        _ => "the next runtime evaluation",
    };

    private static long ThreatLifetime(MiracleInterceptThreatKind kind) =>
        kind == MiracleInterceptThreatKind.PostPurifyCrowdControl
            ? MiracleCleanseFollowupRules.ReleaseOpportunityMilliseconds
            : MiracleInterceptRules.GetThreatLifetimeMilliseconds(kind);

    private static bool IsThreatKindEnabled(
        MiracleInterceptThreatKind kind,
        bool marksmanSpiteEnabled,
        bool zantetsukenEnabled,
        bool furiousBacklashEnabled,
        bool contradanceEnabled,
        bool postPurifyEnabled) =>
        kind switch
        {
            MiracleInterceptThreatKind.MarksmanSpite => marksmanSpiteEnabled,
            MiracleInterceptThreatKind.Zantetsuken => zantetsukenEnabled,
            MiracleInterceptThreatKind.FuriousBacklash => furiousBacklashEnabled,
            MiracleInterceptThreatKind.Contradance => contradanceEnabled,
            MiracleInterceptThreatKind.PostPurifyCrowdControl => postPurifyEnabled,
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
        long ObservedAtMilliseconds,
        MiracleSignalIdentity Signal,
        uint RemovedStatusId);

    private enum MiracleWaitReason : byte
    {
        None = 0,
        HigherPriorityHelper = 1,
        NoEligibleInput = 2,
        TextInput = 3,
        HardenedScales = 4,
        OtherProtection = 5,
        RangeOrLineOfSight = 6,
        TeamFocus = 7,
    }
}
