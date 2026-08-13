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
    private MiracleInterceptProbeSnapshot snapshot = MiracleInterceptProbeSnapshot.Initial;
    private long attemptCount;
    private long acceptedCount;
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
        // CC. Hardened Scales remains a separate VPR-release timing gate: it is
        // not treated as a general Miracle blocker for unrelated threats.
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
        bool marksmanSpiteMetadataVerified,
        bool zantetsukenMetadataVerified,
        bool furiousBacklashMetadataVerified,
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
        var confirmationPendingForLocalCaster = enabled &&
            confirmationState.Pending is { } pending &&
            pending.LocalCasterEntityId == localPlayer!.EntityId;
        capture.SetMiracleInterceptLocalEntityId(
            enabled && (localAlive || confirmationPendingForLocalCaster)
                ? localPlayer!.EntityId
                : 0);

        if (!enabled)
        {
            capture.ClearMiracleInterceptThreats();
            capture.ClearMiracleInterceptConfirmations();
            activeThreat = null;
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

        DrainThreats(
            localPlayer!,
            enableMarksmanSpite && marksmanSpiteMetadataVerified,
            enableZantetsuken && zantetsukenMetadataVerified,
            enableFuriousBacklash &&
            furiousBacklashMetadataVerified &&
            verifiedProtectionStatusIds.Contains(EnemyCombatConstants.HardenedScalesStatusId),
            nowMilliseconds);
        DrainConfirmations(nowMilliseconds);
        // The native hook can enqueue after the framework-frame clock was read.
        // Refresh before comparing the newly captured event against its deadline.
        nowMilliseconds = Math.Max(nowMilliseconds, Environment.TickCount64);

        // A transient higher-priority Purify/Rescue claim cancels only the new
        // threat opportunity. Capture stays enabled so a server status-add for
        // an earlier Miracle attempt can still confirm and finish its popup.
        if (!dispatchAllowed)
        {
            activeThreat = null;
            return Publish("Cancelled", "Higher-priority helper claimed input", nowMilliseconds);
        }

        if (activeThreat is not { } threat ||
            nowMilliseconds < threat.ObservedAtMilliseconds ||
            nowMilliseconds - threat.ObservedAtMilliseconds >= ThreatLifetime(threat.Kind))
        {
            activeThreat = null;
            return Publish("Waiting", "No current exact threat", nowMilliseconds);
        }

        var candidate = ResolveCandidate(localPlayer!, threat);
        if (candidate is null)
        {
            activeThreat = null;
            return Publish("Cancelled", "Exact enemy identity changed", nowMilliseconds);
        }

        var anyProtection = HasAnyVerifiedCcProtection(candidate);
        var hardenedScales = threat.Kind == MiracleInterceptThreatKind.FuriousBacklash &&
                             HasVerifiedActiveStatus(
                                 candidate,
                                 EnemyCombatConstants.HardenedScalesStatusId);
        var otherProtection = anyProtection;
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
        Volatile.Write(ref snapshot, MiracleInterceptProbeSnapshot.Initial with
        {
            ConfirmedLandingCount = confirmationState.TotalConfirmed,
            CapturedConfirmationCount = capture.CapturedMiracleInterceptConfirmations,
            DroppedConfirmationCount = capture.DroppedMiracleInterceptConfirmations,
            LastEvent = "Reset",
        });
    }

    internal MiracleInterceptProbeSnapshot FailClosed(long nowMilliseconds, Exception? exception = null)
    {
        ResetRuntime();
        if (exception is not null) LogAttemptFailure(exception, nowMilliseconds);
        return Publish("Failed closed", "Runtime exception", nowMilliseconds);
    }

    private void DrainThreats(
        IPlayerCharacter localPlayer,
        bool enableMarksmanSpite,
        bool enableZantetsuken,
        bool enableFuriousBacklash,
        long nowMilliseconds)
    {
        while (capture.TryDequeueMiracleInterceptThreat(out var signal))
        {
            var eventNow = Math.Max(nowMilliseconds, Environment.TickCount64);
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
                signal.LocalEntityId != localPlayer.EntityId ||
                signal.ObservedAtMilliseconds > eventNow ||
                eventNow - signal.ObservedAtMilliseconds >= ThreatLifetime(kind))
            {
                continue;
            }

            var identity = new MiracleSignalIdentity(
                signal.CasterEntityId,
                signal.ActionId,
                signal.GlobalSequence,
                signal.SourceSequence);
            if (!RememberSignal(identity)) continue;

            var canonical = ResolveCanonicalEnemy(signal.CasterEntityId, kind);
            if (canonical is null) continue;
            var expectedTarget = kind == MiracleInterceptThreatKind.FuriousBacklash
                ? signal.CasterEntityId
                : signal.EventTargetEntityId;
            if (kind == MiracleInterceptThreatKind.FuriousBacklash &&
                expectedTarget != signal.CasterEntityId)
            {
                continue;
            }

            activeThreat = new MiracleThreatState(
                kind,
                canonical.GameObjectId,
                canonical.EntityId,
                canonical.JobId,
                signal.ObservedAtMilliseconds,
                identity);
        }
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

        var matches = executeTracker.Enemies
            .Where(enemy => enemy.EntityId == casterEntityId && enemy.JobId == expectedJob)
            .Take(2)
            .ToArray();
        return matches.Length == 1 ? matches[0] : null;
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
        var result = MiracleInterceptProbeSnapshot.Initial with
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
        };
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
        var result = new MiracleInterceptProbeSnapshot(
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
            lastEvent);
        Volatile.Write(ref snapshot, result);
        return result;
    }

    private void ResetRuntime()
    {
        activeThreat = null;
        rememberedSignals.Clear();
        rememberedSignalOrder.Clear();
        capture.SetMiracleInterceptLocalEntityId(0);
        capture.ClearMiracleInterceptThreats();
        capture.ClearMiracleInterceptConfirmations();
        confirmationState = MiracleInterceptConfirmationRules.ObserveTime(
            confirmationState,
            Environment.TickCount64,
            hardReset: true);
    }

    private static long ThreatLifetime(MiracleInterceptThreatKind kind) =>
        MiracleInterceptRules.GetThreatLifetimeMilliseconds(kind);

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
}
