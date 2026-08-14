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
/// Experimental CC-only WHM helper. It consumes at most one shared physical
/// gameplay-key generation and makes one exact-target Miracle of Nature call.
/// It never changes the selected target and never retries.
/// </summary>
internal sealed class MiracleInterceptProbe
{
    private const int MaximumRememberedSignals = 128;
    private static readonly uint[] RequiredCcProtectionStatusIds =
        CcImmunityBrakeActionCatalog
            .GetBlockerStatusIds(CcImmunityBrakeBlockerFamily.Miracle)
            .Append(EnemyCombatConstants.HardenedScalesStatusId)
            .Distinct()
            .ToArray();

    private readonly IObjectTable objectTable;
    private readonly ExecuteTracker executeTracker;
    private readonly NearAssistRedirector nearAssist;
    private readonly MachinistLimitBreakCapture capture;
    private readonly IPluginLog log;
    private readonly IReadOnlySet<uint> verifiedProtectionStatusIds;
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
    private long nextErrorLogAt;

    internal MiracleInterceptProbe(
        IObjectTable objectTable,
        IReadOnlySet<uint> verifiedCcBrakeStatusIds,
        ExecuteTracker executeTracker,
        NearAssistRedirector nearAssist,
        MachinistLimitBreakCapture capture,
        IPluginLog log)
    {
        this.objectTable = objectTable;
        this.executeTracker = executeTracker;
        this.nearAssist = nearAssist;
        this.capture = capture;
        this.log = log;
        // Miracle has a narrower blocker matrix than ordinary Purify-removable
        // CC. Hardened Scales is verified through that matrix for VPR and also
        // remains the explicit Furious Backlash release-timing gate below.
        verifiedProtectionStatusIds = verifiedCcBrakeStatusIds
            .Where(RequiredCcProtectionStatusIds.Contains)
            .ToHashSet();
    }

    internal MiracleInterceptProbeSnapshot Snapshot => Volatile.Read(ref snapshot);

    internal unsafe MiracleInterceptProbeSnapshot Observe(
        IPlayerCharacter? localPlayer,
        bool isCrystallineConflict,
        bool configurationEnabled,
        bool dispatchAllowed,
        bool enableMarksmanSpite,
        bool enableZantetsuken,
        bool enableFuriousBacklash,
        bool enablePostPurifyStun,
        bool marksmanSpiteMetadataVerified,
        bool zantetsukenMetadataVerified,
        bool furiousBacklashMetadataVerified,
        bool purifyMetadataVerified,
        EmergencyActionInputFrame inputFrame,
        long nowMilliseconds,
        bool hardReset = false)
    {
        nowMilliseconds = Math.Max(nowMilliseconds, Environment.TickCount64);
        if (hardReset) ResetRuntime();

        var localIdentityValid = localPlayer is not null && HasValidNativeIdentity(localPlayer);
        var localAlive = localIdentityValid && IsLivePlayer(localPlayer);
        var isWhiteMage = localIdentityValid &&
                           localPlayer!.ClassJob.IsValid &&
                          localPlayer.ClassJob.RowId == EnemyCombatConstants.WhiteMageJobId;
        var protectionMetadataReady = RequiredCcProtectionStatusIds.All(
            verifiedProtectionStatusIds.Contains);
        var enabled = configurationEnabled &&
                      isCrystallineConflict &&
                      localIdentityValid &&
                      isWhiteMage &&
                      protectionMetadataReady;
        var cleanseFollowupEnabled = enabled &&
                                     enablePostPurifyStun &&
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
                    ? "Feature gate closed"
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
                    ? "Waiting for exact Miracle landing evidence"
                    : "Local player cannot dispatch",
                nowMilliseconds);
        }

        var cleanseSignals = DrainThreats(
            localPlayer!,
            enableMarksmanSpite && marksmanSpiteMetadataVerified,
            enableZantetsuken && zantetsukenMetadataVerified,
            enableFuriousBacklash &&
            furiousBacklashMetadataVerified &&
            verifiedProtectionStatusIds.Contains(EnemyCombatConstants.HardenedScalesStatusId),
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

        if (!cleanseFollowupEnabled &&
            activeThreat is { Kind: MiracleInterceptThreatKind.PostPurifyStun })
        {
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

        // A transient higher-priority Purify/Rescue claim cannot dispatch a
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

        var anyProtection = HasAnyVerifiedCcProtection(candidate);
        var hardenedScales = threat.Kind == MiracleInterceptThreatKind.FuriousBacklash &&
                             HasVerifiedActiveStatus(
                                 candidate,
                                 EnemyCombatConstants.HardenedScalesStatusId);
        var otherProtection = anyProtection && !hardenedScales;
        var rangeAndLineOfSight = HasMiracleRangeAndLineOfSight(localPlayer!, candidate);
        var locallyReady = !hardenedScales &&
                           !otherProtection &&
                           rangeAndLineOfSight &&
                           ActionManager.Instance() != null;

        var input = inputFrame.Snapshot;
        var triggerKey = inputFrame.FreshGameplayKeyPressed
            ? input.FreshGameplayKey
            : inputFrame.HeldGameplayKeyEligible
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
                        : MiracleWaitReason.RangeOrLineOfSight);
            // Keep the generation available while VPR protection is genuinely
            // present or the exact enemy is briefly out of native 10y/LoS.
            return PublishCandidate(
                threat,
                candidate,
                "Armed",
                hardenedScales
                    ? "Waiting: Hardened Scales still active"
                    : otherProtection
                        ? "Waiting: verified Miracle blocker active"
                    : "Waiting: outside native 10y/LoS",
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
        var revalidatedProtection = revalidated is not null && HasAnyVerifiedCcProtection(revalidated);
        var revalidatedRange = revalidated is not null &&
                               HasMiracleRangeAndLineOfSight(localPlayer!, revalidated);
        if (revalidated is not null &&
            !revalidatedHardened &&
            !revalidatedProtection &&
            revalidatedRange)
        {
            try
            {
                attemptedAtMilliseconds = Environment.TickCount64;
                accepted = TryUseMiracleOnce(revalidated.GameObjectId, out attempted);
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
                    MiracleInterceptConfirmationRules.MiracleOfNatureActionId,
                    revalidated.GameObjectId,
                    revalidated.EntityId,
                    threat.Kind,
                    accepted,
                    attemptedAtMilliseconds),
                attemptedAtMilliseconds);
            confirmationState = registered.NextState;
        }

        lastOpportunity = attempted
            ? $"{threat.Kind}: Miracle attempted (accepted={accepted})"
            : $"{threat.Kind}: consumed but final identity/range/protection validation changed";

        return PublishCandidate(
            threat,
            candidate,
            attempted ? "Spent" : "Cancelled",
            attempted
                ? accepted ? "Miracle action accepted locally" : "Miracle action rejected locally"
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
        bool enablePostPurifyStun,
        long nowMilliseconds)
    {
        var cleanseSignals = new List<MiracleCleanseFollowupSignal>();
        while (capture.TryDequeueMiracleInterceptThreat(out var signal))
        {
            var eventNow = Math.Max(nowMilliseconds, Environment.TickCount64);
            if (signal.ActionId == EnemyCombatConstants.PurifyActionId)
            {
                if (!enablePostPurifyStun || signal.LocalEntityId != localPlayer.EntityId)
                    continue;

                if (signal.ObservedAtMilliseconds > eventNow ||
                    eventNow - signal.ObservedAtMilliseconds >=
                    MiracleCleanseFollowupRules.ResilienceAcquisitionMilliseconds ||
                    signal.FeatureGeneration != capture.CurrentMiracleCleanseFollowupGeneration ||
                    !MiracleCleanseFollowupRules.IsExactStunPurifySignal(
                        signal.CasterEntityId,
                        signal.ActionId,
                        signal.EventTargetEntityId,
                        signal.EffectType,
                        signal.EffectValue,
                        signal.GlobalSequence,
                        signal.SourceSequence))
                {
                    Interlocked.Increment(ref rejectedThreatCount);
                    cleanseFollowupLastEvent = "PostPurifyStun: invalid or stale exact Purify/Stun signal";
                    continue;
                }

                var canonicalCleanseTarget = ResolveCanonicalEnemy(signal.CasterEntityId);
                if (canonicalCleanseTarget is null)
                {
                    Interlocked.Increment(ref rejectedThreatCount);
                    cleanseFollowupLastEvent =
                        "PostPurifyStun: Purify caster was not one exact canonical e1-e5 enemy";
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

            var kind = signal.ActionId switch
            {
                EnemyCombatConstants.MarksmanSpiteActionId when enableMarksmanSpite =>
                    MiracleInterceptThreatKind.MarksmanSpite,
                EnemyCombatConstants.ZantetsukenActionId when enableZantetsuken =>
                    MiracleInterceptThreatKind.Zantetsuken,
                EnemyCombatConstants.FuriousBacklashActionId when enableFuriousBacklash =>
                    MiracleInterceptThreatKind.FuriousBacklash,
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
            var expectedTarget = kind == MiracleInterceptThreatKind.FuriousBacklash
                ? signal.CasterEntityId
                : signal.EventTargetEntityId;
            if (kind == MiracleInterceptThreatKind.FuriousBacklash &&
                expectedTarget != signal.CasterEntityId)
            {
                Interlocked.Increment(ref rejectedThreatCount);
                lastOpportunity = $"{kind}: self-target marker identity mismatch";
                continue;
            }

            if (activeThreat is { } previousThreat && previousThreat.Signal != identity)
            {
                Interlocked.Increment(ref rejectedThreatCount);
                lastOpportunity = $"{previousThreat.Kind}: superseded by newer exact {kind} signal";
            }
            activeThreat = new MiracleThreatState(
                kind,
                canonical.GameObjectId,
                canonical.EntityId,
                canonical.JobId,
                signal.ObservedAtMilliseconds,
                identity);
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
                IsLocalWhiteMageValid: true,
                higherPriorityClaimed,
                newSignal,
                candidate,
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
                "PostPurifyStun: exact enemy Purify removed Stun; waiting for Resilience",
            MiracleCleanseFollowupDecisionKind.ResilienceObserved =>
                "PostPurifyStun: Resilience observed live; waiting for stable absence",
            MiracleCleanseFollowupDecisionKind.ReadyForPromotion =>
                "PostPurifyStun: Resilience absent for 150 ms; promoted to Miracle dispatcher",
            MiracleCleanseFollowupDecisionKind.Cancelled when
                previous.ActiveSignal is not null || newSignal is not null =>
                $"PostPurifyStun: cancelled ({decision.CancelReason})",
            MiracleCleanseFollowupDecisionKind.Cancelled => cleanseFollowupLastEvent,
            MiracleCleanseFollowupDecisionKind.Waiting when
                decision.NextState.Phase == MiracleCleanseFollowupPhase.ReleaseOpportunity &&
                higherPriorityClaimed =>
                "PostPurifyStun: release ready; waiting behind higher-priority helper/threat",
            MiracleCleanseFollowupDecisionKind.Waiting =>
                $"PostPurifyStun: waiting ({decision.NextState.Phase})",
            _ => cleanseFollowupLastEvent,
        };

        if (!decision.ShouldPromote || decision.PromotionIntent is not { } promotion)
            return;

        Interlocked.Increment(ref cleanseFollowupPromotionCount);
        if (activeThreat is not null)
        {
            // Defensive only: HigherPriorityClaimed prevents Core promotion
            // whenever another Miracle opportunity already owns this path.
            Interlocked.Increment(ref cleanseFollowupCancellationCount);
            cleanseFollowupLastEvent = "PostPurifyStun: promotion retired while another threat owned dispatch";
            return;
        }

        var promotionSignal = promotion.Signal;
        activeThreat = new MiracleThreatState(
            MiracleInterceptThreatKind.PostPurifyStun,
            promotion.Target.GameObjectId,
            promotion.Target.EntityId,
            promotion.Target.JobId,
            promotion.ReleasedAtMilliseconds,
            new MiracleSignalIdentity(
                promotionSignal.Key.CasterEntityId,
                promotionSignal.Key.ActionId,
                promotionSignal.Key.GlobalSequence,
                promotionSignal.Key.SourceSequence));
        ResetWaitDiagnostics();
        lastOpportunity = "PostPurifyStun: exact threat armed after verified Resilience absence";
    }

    private void DrainConfirmations(long nowMilliseconds)
    {
        while (capture.TryDequeueMiracleInterceptConfirmation(out var effect))
        {
            var eventNow = Math.Max(nowMilliseconds, Environment.TickCount64);
            if (effect.ObservedAtMilliseconds > eventNow ||
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

    private bool HasAnyVerifiedCcProtection(IPlayerCharacter player)
    {
        var targetJobId = player.ClassJob.IsValid ? player.ClassJob.RowId : 0;
        foreach (var status in player.StatusList)
        {
            // Actor status-list membership is the authoritative live presence
            // gate. Never predict immunity expiry from RemainingTime.
            if (verifiedProtectionStatusIds.Contains(status.StatusId) &&
                CcImmunityBrakeActionCatalog.IsBlockerStatus(
                    CcImmunityBrakeBlockerFamily.Miracle,
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

    private static unsafe bool HasMiracleRangeAndLineOfSight(
        IPlayerCharacter localPlayer,
        IPlayerCharacter target)
    {
        var sourceObject = GetNativeObject(localPlayer);
        var targetObject = GetNativeObject(target);
        if (sourceObject == null || targetObject == null) return false;
        var result = ActionManager.GetActionInRangeOrLoS(
            EnemyCombatConstants.MiracleOfNatureActionId,
            sourceObject,
            targetObject);
        return SeitonRangeRules.HasNativeRangeAndLineOfSight(result);
    }

    private unsafe bool TryUseMiracleOnce(ulong targetGameObjectId, out bool attempted)
    {
        attempted = false;
        if (!TargetHighlightRules.IsValidGameObjectId(targetGameObjectId)) return false;
        var actionManager = ActionManager.Instance();
        if (actionManager == null) return false;

        attempted = true;
        return nearAssist.RunWithoutRedirect(() =>
            actionManager->UseAction(
                ActionType.Action,
                EnemyCombatConstants.MiracleOfNatureActionId,
                targetGameObjectId,
                0,
                ActionManager.UseActionMode.None,
                0));
    }

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
        MiracleWaitReason.HigherPriorityHelper => "Purify/Ally Rescue priority",
        MiracleWaitReason.NoEligibleInput => "an eligible held/fresh physical key",
        MiracleWaitReason.TextInput => "text input to close",
        MiracleWaitReason.HardenedScales => "Hardened Scales to disappear",
        MiracleWaitReason.OtherProtection => "a verified Miracle blocker to disappear",
        MiracleWaitReason.RangeOrLineOfSight => "native 10y range/line of sight",
        _ => "the next runtime evaluation",
    };

    private static long ThreatLifetime(MiracleInterceptThreatKind kind) =>
        kind == MiracleInterceptThreatKind.PostPurifyStun
            ? MiracleCleanseFollowupRules.ReleaseOpportunityMilliseconds
            : MiracleInterceptRules.GetThreatLifetimeMilliseconds(kind);

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
        log.Error(exception, "Seiton Sense Miracle intercept failed closed; the action will not be retried.");
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
        MiracleSignalIdentity Signal);

    private enum MiracleWaitReason : byte
    {
        None = 0,
        HigherPriorityHelper = 1,
        NoEligibleInput = 2,
        TextInput = 3,
        HardenedScales = 4,
        OtherProtection = 5,
        RangeOrLineOfSight = 6,
    }
}
