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
    [
        EnemyCombatConstants.GuardStatusId,
        EnemyCombatConstants.GuardStatusAlternateId,
        EnemyCombatConstants.ResilienceStatusId,
        EnemyCombatConstants.InnerReleaseStatusId,
        EnemyCombatConstants.MeikyoShisuiStatusId,
        EnemyCombatConstants.HardenedScalesStatusId,
    ];

    private readonly IObjectTable objectTable;
    private readonly ExecuteTracker executeTracker;
    private readonly NearAssistRedirector nearAssist;
    private readonly MachinistLimitBreakCapture capture;
    private readonly IPluginLog log;
    private readonly IReadOnlySet<uint> verifiedProtectionStatusIds;
    private readonly HashSet<MiracleSignalIdentity> rememberedSignals = [];
    private readonly Queue<MiracleSignalIdentity> rememberedSignalOrder = [];
    private MiracleThreatState? activeThreat;
    private MiracleInterceptProbeSnapshot snapshot = MiracleInterceptProbeSnapshot.Initial;
    private long attemptCount;
    private long acceptedCount;
    private long nextErrorLogAt;

    internal MiracleInterceptProbe(
        IObjectTable objectTable,
        IDataManager dataManager,
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
        // This allowlist is metadata-verified independently of the nameplate
        // visibility option. A raw catalog ID alone is never trusted here.
        verifiedProtectionStatusIds = CcProtectionMetadataGuard.Validate(dataManager, log);
    }

    internal MiracleInterceptProbeSnapshot Snapshot => Volatile.Read(ref snapshot);

    internal unsafe MiracleInterceptProbeSnapshot Observe(
        IPlayerCharacter? localPlayer,
        bool isCrystallineConflict,
        bool configurationEnabled,
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

        var localAlive = IsLivePlayer(localPlayer);
        var localIdentityValid = localAlive && HasValidNativeIdentity(localPlayer!);
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
        capture.SetMiracleInterceptLocalEntityId(enabled ? localPlayer!.EntityId : 0);

        if (!enabled)
        {
            capture.ClearMiracleInterceptThreats();
            activeThreat = null;
            return Publish(
                "Disabled",
                protectionMetadataReady
                    ? "Feature gate closed"
                    : "Required CC-protection metadata unavailable",
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
        // The native hook can enqueue after the framework-frame clock was read.
        // Refresh before comparing the newly captured event against its deadline.
        nowMilliseconds = Math.Max(nowMilliseconds, Environment.TickCount64);

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

        var hardenedScales = HasVerifiedActiveStatus(
            candidate,
            EnemyCombatConstants.HardenedScalesStatusId);
        var anyProtection = HasAnyVerifiedCcProtection(candidate);
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
                    ? "Waiting for Hardened Scales to be absent"
                    : otherProtection
                        ? "Waiting: verified CC protection active"
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
        var revalidated = ResolveCandidate(localPlayer!, threat);
        var revalidatedHardened = revalidated is not null && HasVerifiedActiveStatus(
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
        Volatile.Write(ref snapshot, MiracleInterceptProbeSnapshot.Initial with { LastEvent = "Reset" });
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
        foreach (var status in player.StatusList)
        {
            // Actor status-list membership is the authoritative live presence
            // gate. Never predict immunity expiry from RemainingTime.
            if (verifiedProtectionStatusIds.Contains(status.StatusId))
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
            if (status.StatusId == statusId)
            {
                return true;
            }
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
