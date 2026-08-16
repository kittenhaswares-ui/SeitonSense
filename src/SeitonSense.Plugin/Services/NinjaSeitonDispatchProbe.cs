using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using SeitonSense.Core;

namespace SeitonSense.Plugin.Services;

internal sealed record NinjaSeitonDispatchProbeSnapshot(
    NinjaSeitonDispatchDecisionKind Decision,
    NinjaSeitonDispatchDecisionReason Reason,
    uint ResolvedActionId,
    int CandidateCount,
    int EnemySlot,
    ulong TargetGameObjectId,
    uint TargetEntityId,
    bool LocallyReady,
    VirtualKey FreshGameplayKey,
    bool InputClaimed,
    bool UseActionAttempted,
    bool UseActionAccepted,
    long AttemptCount,
    long AcceptedCount,
    string CandidateResolution,
    string LastEvent)
{
    internal static NinjaSeitonDispatchProbeSnapshot Initial { get; } = new(
        NinjaSeitonDispatchDecisionKind.None,
        NinjaSeitonDispatchDecisionReason.None,
        0,
        0,
        0,
        0,
        0,
        false,
        VirtualKey.NO_KEY,
        false,
        false,
        false,
        0,
        0,
        "Not evaluated",
        "Waiting");
}

/// <summary>
/// Converts one unclaimed fresh physical gameplay-key generation into at most
/// one exact Seiton Tenchu request. Selection uses only current native S1-S5
/// actors whose identities match the fail-closed ExecuteTracker snapshot.
/// </summary>
internal sealed class NinjaSeitonDispatchProbe
{
    private readonly IObjectTable objectTable;
    private readonly ExecuteTracker executeTracker;
    private readonly NearAssistRedirector nearAssist;
    private readonly IPluginLog log;
    private NinjaSeitonDispatchProbeSnapshot snapshot = NinjaSeitonDispatchProbeSnapshot.Initial;
    private long attemptCount;
    private long acceptedCount;
    private long nextErrorLogAt;
    private string lastEvent = "Waiting";

    internal NinjaSeitonDispatchProbe(
        IObjectTable objectTable,
        ExecuteTracker executeTracker,
        NearAssistRedirector nearAssist,
        IPluginLog log)
    {
        this.objectTable = objectTable;
        this.executeTracker = executeTracker;
        this.nearAssist = nearAssist;
        this.log = log;
    }

    internal NinjaSeitonDispatchProbeSnapshot Snapshot => Volatile.Read(ref snapshot);

    internal NinjaSeitonDispatchProbeSnapshot Observe(
        IPlayerCharacter? localPlayer,
        bool isCrystallineConflict,
        bool configurationEnabled,
        bool metadataVerified,
        bool actionHelpersSuppressedByGuard,
        bool higherPriorityClaimed,
        EmergencyActionInputFrame inputFrame,
        long nowMilliseconds,
        bool hardReset = false)
    {
        var localAlive = IsLivePlayer(localPlayer);
        var localIdentity = HasValidNativeIdentity(localPlayer)
            ? new TargetPressureActorIdentity(localPlayer!.GameObjectId, localPlayer.EntityId)
            : default;
        var localJobId = localPlayer?.ClassJob.IsValid == true
            ? localPlayer.ClassJob.RowId
            : 0;
        var featureContextReady = configurationEnabled &&
                                  isCrystallineConflict &&
                                  localAlive &&
                                  ExecuteThreshold.IsNinja(localJobId) &&
                                  metadataVerified &&
                                  !actionHelpersSuppressedByGuard &&
                                  !hardReset;
        var resolvedActionId = 0u;
        var actionReady = featureContextReady &&
                          localIdentity.IsValid &&
                          SeitonReadinessProbe.TryGetReadyAction(localPlayer!, out resolvedActionId);
        if (!actionReady) resolvedActionId = 0;

        var input = inputFrame.Snapshot;
        var shouldResolveCandidates = actionReady &&
                                      !higherPriorityClaimed &&
                                      input.ProbeSucceeded &&
                                      !input.IsTextInputActive &&
                                      inputFrame.FreshGameplayKeyPressed;
        var candidateResolution = "Not evaluated: no eligible fresh input";
        var candidates = shouldResolveCandidates
            ? ResolveExactCandidates(localPlayer!, resolvedActionId, out candidateResolution)
            : [];
        var decision = NinjaSeitonDispatchRules.Observe(
            new NinjaSeitonDispatchObservation(
                configurationEnabled,
                isCrystallineConflict,
                localJobId,
                localIdentity,
                localAlive,
                metadataVerified,
                actionHelpersSuppressedByGuard,
                higherPriorityClaimed,
                input.ProbeSucceeded,
                input.IsTextInputActive,
                inputFrame.FreshGameplayKeyPressed,
                resolvedActionId,
                actionReady,
                candidates,
                hardReset));

        // Commit the one physical generation before any final validation or
        // native boundary. Drift after this point cancels instead of selecting
        // another candidate or retrying this generation.
        var inputClaimed = decision.ShouldConsumeInputGeneration;
        if (inputClaimed) inputFrame.Consume();

        var attempted = false;
        var accepted = false;
        if (decision.ShouldDispatch && decision.Intent is { } intent)
        {
            var finalActionReady = SeitonReadinessProbe.TryGetReadyAction(
                localPlayer!,
                out var finalResolvedActionId);
            var finalCandidate = ResolveFrozenIntent(localPlayer!, intent, finalResolvedActionId);
            if (finalCandidate is { } exactCandidate &&
                NinjaSeitonDispatchRules.CanUseExactIntent(
                    intent,
                    exactCandidate,
                    localIdentity,
                    finalResolvedActionId,
                    finalActionReady))
            {
                try
                {
                    accepted = TryUseSeitonOnce(localPlayer!, intent, out attempted);
                    if (attempted) Interlocked.Increment(ref attemptCount);
                    if (accepted) Interlocked.Increment(ref acceptedCount);
                    lastEvent =
                        $"S{intent.EnemySlot} action {intent.ActionId} attempted (accepted={accepted})";
                }
                catch (Exception exception)
                {
                    if (attempted) Interlocked.Increment(ref attemptCount);
                    lastEvent = $"S{intent.EnemySlot} terminal action exception";
                    LogAttemptFailure(exception, nowMilliseconds);
                }
            }
            else
            {
                lastEvent = $"S{intent.EnemySlot} terminal exact-intent revalidation failed";
            }
        }

        var selectedCandidate = decision.SelectedCandidateIndex >= 0 &&
                                decision.SelectedCandidateIndex < candidates.Count
            ? candidates[decision.SelectedCandidateIndex]
            : (NinjaSeitonDispatchCandidate?)null;
        var result = new NinjaSeitonDispatchProbeSnapshot(
            decision.Kind,
            decision.Reason,
            resolvedActionId,
            candidates.Count,
            selectedCandidate?.EnemySlot ?? 0,
            selectedCandidate?.Actor.GameObjectId ?? 0,
            selectedCandidate?.Actor.EntityId ?? 0,
            actionReady,
            input.FreshGameplayKey,
            inputClaimed,
            attempted,
            accepted,
            Interlocked.Read(ref attemptCount),
            Interlocked.Read(ref acceptedCount),
            candidateResolution,
            lastEvent);
        Volatile.Write(ref snapshot, result);
        return result;
    }

    internal void Reset()
    {
        lastEvent = "Reset";
        Volatile.Write(ref snapshot, NinjaSeitonDispatchProbeSnapshot.Initial with
        {
            AttemptCount = Interlocked.Read(ref attemptCount),
            AcceptedCount = Interlocked.Read(ref acceptedCount),
            LastEvent = lastEvent,
        });
    }

    internal NinjaSeitonDispatchProbeSnapshot FailClosed()
    {
        lastEvent = "Failed closed";
        var result = NinjaSeitonDispatchProbeSnapshot.Initial with
        {
            Decision = NinjaSeitonDispatchDecisionKind.Cancelled,
            Reason = NinjaSeitonDispatchDecisionReason.HardReset,
            AttemptCount = Interlocked.Read(ref attemptCount),
            AcceptedCount = Interlocked.Read(ref acceptedCount),
            LastEvent = lastEvent,
        };
        Volatile.Write(ref snapshot, result);
        return result;
    }

    private IReadOnlyList<NinjaSeitonDispatchCandidate> ResolveExactCandidates(
        IPlayerCharacter localPlayer,
        uint actionId,
        out string resolution)
    {
        var diagnosticsBefore = executeTracker.Diagnostics;
        if (!diagnosticsBefore.Active ||
            !diagnosticsBefore.IsCrystallineConflict ||
            !diagnosticsBefore.SeitonMetadataVerified)
        {
            resolution = "Tracker context unavailable";
            return [];
        }

        if (diagnosticsBefore.SlotCapacity != EnemySlotRules.LastSlot ||
            diagnosticsBefore.ResolvedSlots != EnemySlotRules.LastSlot)
        {
            resolution =
                $"Tracker slots incomplete: {diagnosticsBefore.ResolvedSlots}/{diagnosticsBefore.SlotCapacity}";
            return [];
        }

        var snapshots = executeTracker.Enemies.ToArray();
        var diagnosticsAfter = executeTracker.Diagnostics;
        if (!ReferenceEquals(diagnosticsBefore, diagnosticsAfter))
        {
            resolution = "Tracker snapshot changed during capture";
            return [];
        }

        if (snapshots.Length > EnemySlotRules.LastSlot ||
            snapshots.Length != diagnosticsBefore.ValidEnemySlots)
        {
            resolution =
                $"Tracker snapshot count drift: {snapshots.Length}/{diagnosticsBefore.ValidEnemySlots}";
            return [];
        }

        var seenSlots = new HashSet<int>();
        var seenGameObjectIds = new HashSet<ulong>();
        var seenEntityIds = new HashSet<uint>();
        var snapshotsBySlot = new Dictionary<int, EnemyHudSnapshot>(snapshots.Length);
        foreach (var snapshotEnemy in snapshots)
        {
            if (!EnemySlotRules.IsValidSlot(snapshotEnemy.Slot) ||
                snapshotEnemy.GameObjectId is 0 or 0xE0000000 ||
                snapshotEnemy.EntityId is 0 or 0xE0000000 ||
                !seenSlots.Add(snapshotEnemy.Slot) ||
                !seenGameObjectIds.Add(snapshotEnemy.GameObjectId) ||
                !seenEntityIds.Add(snapshotEnemy.EntityId))
            {
                resolution = "Tracker snapshot identity ambiguous";
                return [];
            }

            snapshotsBySlot.Add(snapshotEnemy.Slot, snapshotEnemy);
        }

        var currentSlots = new List<(int Slot, IPlayerCharacter Player)>(
            EnemySlotRules.LastSlot);
        seenGameObjectIds.Clear();
        seenEntityIds.Clear();
        var seenAddresses = new HashSet<nint>();
        for (var slot = EnemySlotRules.FirstSlot; slot <= EnemySlotRules.LastSlot; slot++)
        {
            var player = EnemySlotResolver.Resolve(objectTable, slot);
            if (!HasValidNativeIdentity(player))
            {
                resolution = $"Native S{slot} unresolved";
                return [];
            }

            var tablePlayer = objectTable.SearchByEntityId(player!.EntityId) as IPlayerCharacter;
            if (tablePlayer is null ||
                tablePlayer.Address != player.Address ||
                tablePlayer.GameObjectId != player.GameObjectId ||
                tablePlayer.EntityId != player.EntityId)
            {
                resolution = $"Native S{slot} object-table identity mismatch";
                return [];
            }

            if (!seenGameObjectIds.Add(player.GameObjectId) ||
                !seenEntityIds.Add(player.EntityId) ||
                !seenAddresses.Add(player.Address))
            {
                resolution = "Native e1-e5 identities duplicate";
                return [];
            }

            currentSlots.Add((slot, player));
        }

        var eligibleCurrentSlots = currentSlots
            .Where(static entry =>
                IsLivePlayer(entry.Player) &&
                entry.Player.IsTargetable &&
                ExecuteThreshold.HasValidHp(entry.Player.CurrentHp, entry.Player.MaxHp))
            .ToArray();
        if (eligibleCurrentSlots.Length != diagnosticsBefore.ValidEnemySlots ||
            eligibleCurrentSlots.Length != snapshots.Length)
        {
            resolution =
                $"Tracker/native eligible count drift: {snapshots.Length}/{eligibleCurrentSlots.Length}";
            return [];
        }

        var candidates = new List<NinjaSeitonDispatchCandidate>(eligibleCurrentSlots.Length);
        foreach (var (slot, player) in eligibleCurrentSlots)
        {
            if (!snapshotsBySlot.TryGetValue(slot, out var snapshotEnemy) ||
                snapshotEnemy.GameObjectId != player.GameObjectId ||
                snapshotEnemy.EntityId != player.EntityId)
            {
                resolution = $"Tracker/native S{slot} identity mismatch";
                return [];
            }

            var expectedTarget = new TargetPressureActorIdentity(
                player.GameObjectId,
                player.EntityId);
            var candidate = BuildExactSlotCandidate(
                localPlayer,
                actionId,
                slot,
                expectedTarget);
            if (candidate is not { } exact)
            {
                resolution = $"Native S{slot} action validation failed";
                return [];
            }

            candidates.Add(exact);
        }

        foreach (var (slot, player) in currentSlots)
        {
            var stablePlayer = EnemySlotResolver.Resolve(objectTable, slot);
            if (!HasValidNativeIdentity(stablePlayer) ||
                stablePlayer!.Address != player.Address ||
                stablePlayer.GameObjectId != player.GameObjectId ||
                stablePlayer.EntityId != player.EntityId)
            {
                resolution = $"Native S{slot} changed during capture";
                return [];
            }
        }

        resolution = $"Exact coherent set: {candidates.Count} candidates";
        return candidates;
    }

    private NinjaSeitonDispatchCandidate? ResolveFrozenIntent(
        IPlayerCharacter localPlayer,
        NinjaSeitonDispatchIntent intent,
        uint actionId) =>
        BuildExactSlotCandidate(
            localPlayer,
            actionId,
            intent.EnemySlot,
            intent.Target);

    private unsafe NinjaSeitonDispatchCandidate? BuildExactSlotCandidate(
        IPlayerCharacter localPlayer,
        uint actionId,
        int enemySlot,
        TargetPressureActorIdentity expectedTarget)
    {
        if (!NinjaSeitonDispatchRules.IsExactSeitonAction(actionId) ||
            !EnemySlotRules.IsValidSlot(enemySlot) ||
            !expectedTarget.IsValid)
        {
            return null;
        }

        var target = EnemySlotResolver.Resolve(objectTable, enemySlot);
        if (!HasValidNativeIdentity(target) ||
            target!.GameObjectId != expectedTarget.GameObjectId ||
            target.EntityId != expectedTarget.EntityId)
        {
            return null;
        }

        var tableTarget = objectTable.SearchByEntityId(target.EntityId) as IPlayerCharacter;
        var exactCanonicalIdentity = tableTarget is not null &&
                                     tableTarget.Address == target.Address &&
                                     tableTarget.GameObjectId == target.GameObjectId &&
                                     tableTarget.EntityId == target.EntityId;
        var sourceObject = GetNativeObject(localPlayer);
        var targetObject = GetNativeObject(target);
        var validActionTarget = sourceObject != null && targetObject != null;
        var rangeAndLineOfSight = validActionTarget &&
                                  SeitonReadinessProbe.HasRangeAndLineOfSight(
                                      localPlayer,
                                      target,
                                      actionId,
                                      out _);
        return new NinjaSeitonDispatchCandidate(
            enemySlot,
            expectedTarget,
            exactCanonicalIdentity,
            IsLivePlayer(target),
            target.IsTargetable,
            target.CurrentHp,
            target.MaxHp,
            validActionTarget,
            rangeAndLineOfSight);
    }

    private unsafe bool TryUseSeitonOnce(
        IPlayerCharacter localPlayer,
        NinjaSeitonDispatchIntent intent,
        out bool attempted)
    {
        attempted = false;
        if (!HasValidNativeIdentity(localPlayer) ||
            !intent.IsValid ||
            !SeitonReadinessProbe.TryGetReadyAction(localPlayer, out var resolvedActionId) ||
            resolvedActionId != intent.ActionId)
        {
            return false;
        }

        var actionManager = ActionManager.Instance();
        if (actionManager == null) return false;

        attempted = true;
        return nearAssist.RunWithoutRedirect(() =>
            actionManager->UseAction(
                ActionType.Action,
                intent.ActionId,
                intent.Target.GameObjectId,
                0,
                ActionManager.UseActionMode.None,
                0));
    }

    private static unsafe GameObject* GetNativeObject(IPlayerCharacter? player)
    {
        if (!HasValidNativeIdentity(player)) return null;
        var native = (GameObject*)player!.Address;
        return native != null && native->EntityId == player.EntityId
            ? native
            : null;
    }

    private static bool IsLivePlayer(IPlayerCharacter? player) =>
        player is not null &&
        !player.IsDead &&
        player.CurrentHp > 0 &&
        player.MaxHp >= player.CurrentHp;

    private static bool HasValidNativeIdentity(IPlayerCharacter? player) =>
        player is not null &&
        player.Address != 0 &&
        player.EntityId is not 0 and not 0xE0000000 &&
        player.GameObjectId is not 0 and not 0xE0000000;

    private void LogAttemptFailure(Exception exception, long nowMilliseconds)
    {
        if (nowMilliseconds < nextErrorLogAt) return;
        nextErrorLogAt = nowMilliseconds + 10_000;
        log.Error(exception, "Seiton Sense Ninja Seiton attempt failed and will not be retried.");
    }
}
