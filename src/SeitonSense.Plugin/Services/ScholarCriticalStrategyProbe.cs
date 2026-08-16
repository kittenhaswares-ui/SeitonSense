using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using SeitonSense.Core;
using SeitonSense.Plugin.Models;

namespace SeitonSense.Plugin.Services;

internal sealed record ScholarCriticalStrategyProbeSnapshot(
    ScholarCriticalStrategyDecisionKind Decision,
    ScholarCriticalStrategyDecisionReason Reason,
    uint ResolvedActionId,
    int CandidateCount,
    int EnemySlot,
    ulong TargetGameObjectId,
    uint TargetEntityId,
    bool PressureKnown,
    int TeamTargetCount,
    bool LocallyReady,
    VirtualKey HeldGameplayKey,
    bool InputClaimed,
    bool UseActionAttempted,
    bool UseActionAccepted,
    long AttemptCount,
    long AcceptedCount,
    string CandidateResolution,
    string LastEvent)
{
    internal static ScholarCriticalStrategyProbeSnapshot Initial { get; } = new(
        ScholarCriticalStrategyDecisionKind.None,
        ScholarCriticalStrategyDecisionReason.None,
        0,
        0,
        0,
        0,
        0,
        false,
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
/// Converts one unclaimed held physical gameplay-key generation into at most
/// one exact Scholar Critical Strategy request against a live Guard target.
/// Selection requires a coherent canonical S1-S5 capture. Pressure is sampled
/// only for initial ranking; after input consumption only the frozen actor and
/// action are revalidated.
/// </summary>
internal sealed class ScholarCriticalStrategyProbe
{
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly IDutyState dutyState;
    private readonly PluginConfiguration configuration;
    private readonly ExecuteTracker executeTracker;
    private readonly TargetPressureTracker pressureTracker;
    private readonly NearAssistRedirector nearAssist;
    private readonly IPluginLog log;
    private ScholarCriticalStrategyProbeSnapshot snapshot = ScholarCriticalStrategyProbeSnapshot.Initial;
    private long attemptCount;
    private long acceptedCount;
    private long nextErrorLogAt;
    private string lastEvent = "Waiting";

    internal ScholarCriticalStrategyProbe(
        IClientState clientState,
        IObjectTable objectTable,
        IDutyState dutyState,
        PluginConfiguration configuration,
        ExecuteTracker executeTracker,
        TargetPressureTracker pressureTracker,
        NearAssistRedirector nearAssist,
        IPluginLog log)
    {
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.dutyState = dutyState;
        this.configuration = configuration;
        this.executeTracker = executeTracker;
        this.pressureTracker = pressureTracker;
        this.nearAssist = nearAssist;
        this.log = log;
    }

    internal ScholarCriticalStrategyProbeSnapshot Snapshot => Volatile.Read(ref snapshot);

    internal ScholarCriticalStrategyProbeSnapshot Observe(
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
                                  localJobId == ScholarCriticalStrategyRules.ScholarJobId &&
                                  metadataVerified &&
                                  !actionHelpersSuppressedByGuard &&
                                  !hardReset;
        var resolvedActionId = 0u;
        var actionReady = featureContextReady &&
                          localIdentity.IsValid &&
                          TryGetReadyAction(localPlayer!, out resolvedActionId);
        if (!actionReady) resolvedActionId = 0;

        var input = inputFrame.Snapshot;
        var shouldResolveCandidates = actionReady &&
                                      !higherPriorityClaimed &&
                                      input.ProbeSucceeded &&
                                      !input.IsTextInputActive &&
                                      inputFrame.HeldGameplayKeyEligible;
        var candidateResolution = "Not evaluated: no eligible held input";
        var candidates = shouldResolveCandidates
            ? ResolveExactCandidates(localPlayer!, resolvedActionId, out candidateResolution)
            : [];
        var completeCanonicalSet = candidates.Count == EnemySlotRules.LastSlot;
        var decision = ScholarCriticalStrategyRules.Observe(
            new ScholarCriticalStrategyObservation(
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
                inputFrame.HeldGameplayKeyEligible,
                resolvedActionId,
                actionReady,
                completeCanonicalSet,
                candidates,
                hardReset));

        // Spend the shared physical generation before any final native reads.
        // Every later drift or client rejection is terminal for this hold.
        var inputClaimed = decision.ShouldConsumeInputGeneration;
        if (inputClaimed) inputFrame.Consume();

        var attempted = false;
        var accepted = false;
        if (decision.ShouldDispatch && decision.Intent is { } intent)
        {
            var currentLocal = ResolveExactLocalPlayer(intent.LocalPlayer);
            var finalContextReady = currentLocal is not null &&
                                    IsCurrentCrystallineConflict() &&
                                    IsLivePlayer(currentLocal) &&
                                    currentLocal.ClassJob.IsValid &&
                                    currentLocal.ClassJob.RowId == ScholarCriticalStrategyRules.ScholarJobId &&
                                    metadataVerified &&
                                    !actionHelpersSuppressedByGuard &&
                                    !IsCurrentlySuppressedByGuard(currentLocal, Environment.TickCount64);
            var finalResolvedActionId = 0u;
            var finalActionReady = finalContextReady &&
                                   TryGetReadyAction(currentLocal!, out finalResolvedActionId);
            if (!finalActionReady) finalResolvedActionId = 0;

            // Pressure is intentionally not sampled here. The frozen values in
            // the intent remain diagnostic-only after the input was consumed.
            var finalCandidate = finalContextReady
                ? ResolveFrozenIntent(currentLocal!, intent, finalResolvedActionId)
                : null;
            var currentLocalIdentity = currentLocal is not null
                ? new TargetPressureActorIdentity(currentLocal.GameObjectId, currentLocal.EntityId)
                : default;
            if (finalCandidate is { } exactCandidate &&
                ScholarCriticalStrategyRules.CanUseExactIntent(
                    intent,
                    exactCandidate,
                    currentLocalIdentity,
                    finalResolvedActionId,
                    finalActionReady))
            {
                try
                {
                    accepted = TryUseCriticalStrategyOnce(
                        currentLocal!,
                        intent,
                        metadataVerified,
                        out attempted);
                    if (attempted) Interlocked.Increment(ref attemptCount);
                    if (accepted) Interlocked.Increment(ref acceptedCount);
                    lastEvent = attempted
                        ? $"S{intent.EnemySlot} action {intent.ActionId} attempted (accepted={accepted})"
                        : $"S{intent.EnemySlot} terminal exact-intent revalidation failed";
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
            : (ScholarCriticalStrategyCandidate?)null;
        var result = new ScholarCriticalStrategyProbeSnapshot(
            decision.Kind,
            decision.Reason,
            resolvedActionId,
            candidates.Count,
            selectedCandidate?.EnemySlot ?? 0,
            selectedCandidate?.Actor.GameObjectId ?? 0,
            selectedCandidate?.Actor.EntityId ?? 0,
            selectedCandidate?.PressureKnown ?? false,
            selectedCandidate?.TeamTargetCount ?? 0,
            actionReady,
            input.HeldGameplayKey,
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
        Volatile.Write(ref snapshot, ScholarCriticalStrategyProbeSnapshot.Initial with
        {
            AttemptCount = Interlocked.Read(ref attemptCount),
            AcceptedCount = Interlocked.Read(ref acceptedCount),
            LastEvent = lastEvent,
        });
    }

    internal ScholarCriticalStrategyProbeSnapshot FailClosed()
    {
        lastEvent = "Failed closed";
        var result = ScholarCriticalStrategyProbeSnapshot.Initial with
        {
            Decision = ScholarCriticalStrategyDecisionKind.Cancelled,
            Reason = ScholarCriticalStrategyDecisionReason.HardReset,
            AttemptCount = Interlocked.Read(ref attemptCount),
            AcceptedCount = Interlocked.Read(ref acceptedCount),
            LastEvent = lastEvent,
        };
        Volatile.Write(ref snapshot, result);
        return result;
    }

    private IReadOnlyList<ScholarCriticalStrategyCandidate> ResolveExactCandidates(
        IPlayerCharacter localPlayer,
        uint actionId,
        out string resolution)
    {
        var diagnosticsBefore = executeTracker.Diagnostics;
        var trackerEnemies = executeTracker.Enemies;
        if (!diagnosticsBefore.Active ||
            !diagnosticsBefore.IsCrystallineConflict ||
            !diagnosticsBefore.GuardMetadataVerified)
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

        var snapshots = trackerEnemies.ToArray();
        if (!ReferenceEquals(diagnosticsBefore, executeTracker.Diagnostics) ||
            !ReferenceEquals(trackerEnemies, executeTracker.Enemies))
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

        var snapshotSlots = new HashSet<int>();
        var snapshotGameObjectIds = new HashSet<ulong>();
        var snapshotEntityIds = new HashSet<uint>();
        var snapshotsBySlot = new Dictionary<int, EnemyHudSnapshot>(snapshots.Length);
        foreach (var trackerEnemy in snapshots)
        {
            if (!EnemySlotRules.IsValidSlot(trackerEnemy.Slot) ||
                !IsValidGameObjectId(trackerEnemy.GameObjectId) ||
                !IsValidEntityId(trackerEnemy.EntityId) ||
                !snapshotSlots.Add(trackerEnemy.Slot) ||
                !snapshotGameObjectIds.Add(trackerEnemy.GameObjectId) ||
                !snapshotEntityIds.Add(trackerEnemy.EntityId))
            {
                resolution = "Tracker snapshot identity ambiguous";
                return [];
            }

            snapshotsBySlot.Add(trackerEnemy.Slot, trackerEnemy);
        }

        var currentSlots = new List<(int Slot, IPlayerCharacter Player)>(
            EnemySlotRules.LastSlot);
        var nativeGameObjectIds = new HashSet<ulong>();
        var nativeEntityIds = new HashSet<uint>();
        var nativeAddresses = new HashSet<nint>();
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

            if (!nativeGameObjectIds.Add(player.GameObjectId) ||
                !nativeEntityIds.Add(player.EntityId) ||
                !nativeAddresses.Add(player.Address))
            {
                resolution = "Native e1-e5 identities duplicate";
                return [];
            }

            currentSlots.Add((slot, player));
        }

        var trackerEligibleSlots = currentSlots
            .Where(static entry =>
                IsLivePlayer(entry.Player) &&
                entry.Player.IsTargetable &&
                ExecuteThreshold.HasValidHp(entry.Player.CurrentHp, entry.Player.MaxHp))
            .ToArray();
        if (trackerEligibleSlots.Length != diagnosticsBefore.ValidEnemySlots ||
            trackerEligibleSlots.Length != snapshots.Length)
        {
            resolution =
                $"Tracker/native eligible count drift: {snapshots.Length}/{trackerEligibleSlots.Length}";
            return [];
        }

        foreach (var (slot, player) in trackerEligibleSlots)
        {
            if (!snapshotsBySlot.TryGetValue(slot, out var trackerEnemy) ||
                trackerEnemy.GameObjectId != player.GameObjectId ||
                trackerEnemy.EntityId != player.EntityId)
            {
                resolution = $"Tracker/native S{slot} identity mismatch";
                return [];
            }
        }

        var pressureSnapshot = pressureTracker.Snapshot;
        var pressureSnapshotUsable = pressureSnapshot.Active && pressureSnapshot.PressureActive;
        var candidates = new List<ScholarCriticalStrategyCandidate>(EnemySlotRules.LastSlot);
        foreach (var (slot, player) in currentSlots)
        {
            var actor = new TargetPressureActorIdentity(player.GameObjectId, player.EntityId);
            var pressure = pressureSnapshotUsable
                ? pressureSnapshot.Find(actor.GameObjectId, actor.EntityId)
                : null;
            var pressureKnown = pressure is not null &&
                                pressure.EnemySlot == slot &&
                                pressure.TeamTargetCount >= 0;
            var candidate = BuildExactSlotCandidate(
                localPlayer,
                actionId,
                slot,
                actor,
                pressureKnown,
                pressureKnown ? pressure!.TeamTargetCount : 0);
            if (candidate is not { } exact)
            {
                resolution = $"Native S{slot} action validation failed";
                return [];
            }

            candidates.Add(exact);
        }

        if (!ReferenceEquals(pressureSnapshot, pressureTracker.Snapshot))
        {
            // An incoherent pressure sample must degrade the entire selection to
            // exact HP ordering; it must never cancel an otherwise safe press.
            candidates = candidates
                .Select(static candidate => candidate with
                {
                    PressureKnown = false,
                    TeamTargetCount = 0,
                })
                .ToList();
            pressureSnapshotUsable = false;
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

        if (!ReferenceEquals(diagnosticsBefore, executeTracker.Diagnostics) ||
            !ReferenceEquals(trackerEnemies, executeTracker.Enemies))
        {
            resolution = "Tracker snapshot changed after native capture";
            return [];
        }

        var eligibleCandidates = candidates
            .Where(candidate =>
                ScholarCriticalStrategyRules.IsEligibleCandidate(
                    candidate,
                    new TargetPressureActorIdentity(
                        localPlayer.GameObjectId,
                        localPlayer.EntityId)))
            .ToArray();
        var pressureRanked = pressureSnapshotUsable &&
                             eligibleCandidates.Length > 0 &&
                             eligibleCandidates.All(static candidate =>
                                 candidate.PressureKnown &&
                                 candidate.TeamTargetCount >= 0) &&
                             eligibleCandidates.Any(static candidate =>
                                 candidate.TeamTargetCount > 0);
        resolution = pressureRanked
            ? "Exact coherent S1-S5; pressure ranking"
            : "Exact coherent S1-S5; HP fallback";
        return candidates;
    }

    private ScholarCriticalStrategyCandidate? ResolveFrozenIntent(
        IPlayerCharacter localPlayer,
        ScholarCriticalStrategyIntent intent,
        uint actionId) =>
        BuildExactSlotCandidate(
            localPlayer,
            actionId,
            intent.EnemySlot,
            intent.Target,
            intent.PressureKnown,
            intent.TeamTargetCount);

    private unsafe ScholarCriticalStrategyCandidate? BuildExactSlotCandidate(
        IPlayerCharacter localPlayer,
        uint actionId,
        int enemySlot,
        TargetPressureActorIdentity expectedTarget,
        bool pressureKnown,
        int teamTargetCount)
    {
        if (actionId != ScholarCriticalStrategyRules.ActionId ||
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
                                  HasRangeAndLineOfSight(
                                      localPlayer,
                                      target,
                                      actionId,
                                      out _);
        return new ScholarCriticalStrategyCandidate(
            enemySlot,
            expectedTarget,
            exactCanonicalIdentity,
            IsLivePlayer(target),
            target.IsTargetable,
            target.CurrentHp,
            target.MaxHp,
            HasLiveGuard(target),
            validActionTarget,
            rangeAndLineOfSight,
            pressureKnown,
            teamTargetCount);
    }

    private unsafe bool TryUseCriticalStrategyOnce(
        IPlayerCharacter localPlayer,
        ScholarCriticalStrategyIntent intent,
        bool metadataVerified,
        out bool attempted)
    {
        attempted = false;
        var currentLocal = ResolveExactLocalPlayer(intent.LocalPlayer);
        if (currentLocal is null ||
            currentLocal.Address != localPlayer.Address ||
            !IsLivePlayer(currentLocal) ||
            !currentLocal.ClassJob.IsValid ||
            currentLocal.ClassJob.RowId != ScholarCriticalStrategyRules.ScholarJobId ||
            !IsCurrentCrystallineConflict() ||
            !metadataVerified ||
            IsCurrentlySuppressedByGuard(currentLocal, Environment.TickCount64) ||
            !intent.IsValid ||
            !TryGetReadyAction(currentLocal, out var resolvedActionId) ||
            resolvedActionId != intent.ActionId)
        {
            return false;
        }

        var exactCandidate = ResolveFrozenIntent(currentLocal, intent, resolvedActionId);
        var currentLocalIdentity = new TargetPressureActorIdentity(
            currentLocal.GameObjectId,
            currentLocal.EntityId);
        if (exactCandidate is not { } currentCandidate ||
            !ScholarCriticalStrategyRules.CanUseExactIntent(
                intent,
                currentCandidate,
                currentLocalIdentity,
                resolvedActionId,
                actionLocallyReady: true))
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

    private unsafe bool TryGetReadyAction(
        IPlayerCharacter localPlayer,
        out uint resolvedActionId)
    {
        resolvedActionId = 0;
        if (!HasValidNativeIdentity(localPlayer) ||
            !localPlayer.ClassJob.IsValid ||
            localPlayer.ClassJob.RowId != ScholarCriticalStrategyRules.ScholarJobId)
        {
            return false;
        }

        var sourceObject = GetNativeObject(localPlayer);
        var actionManager = ActionManager.Instance();
        if (sourceObject == null || actionManager == null) return false;

        resolvedActionId = actionManager->GetAdjustedActionId(
            EnemyCombatConstants.ScholarCriticalStrategyActionId);
        return resolvedActionId == ScholarCriticalStrategyRules.ActionId &&
               actionManager->IsActionOffCooldown(ActionType.Action, resolvedActionId);
    }

    private static unsafe bool HasRangeAndLineOfSight(
        IPlayerCharacter localPlayer,
        IPlayerCharacter target,
        uint actionId,
        out uint rangeStatus)
    {
        rangeStatus = uint.MaxValue;
        if (actionId != ScholarCriticalStrategyRules.ActionId) return false;

        var sourceObject = GetNativeObject(localPlayer);
        var targetObject = GetNativeObject(target);
        if (sourceObject == null || targetObject == null) return false;

        rangeStatus = ActionManager.GetActionInRangeOrLoS(
            actionId,
            sourceObject,
            targetObject);
        return SeitonRangeRules.HasNativeRangeAndLineOfSight(rangeStatus);
    }

    private IPlayerCharacter? ResolveExactLocalPlayer(TargetPressureActorIdentity expected)
    {
        var current = objectTable.LocalPlayer;
        if (!HasValidNativeIdentity(current) ||
            current!.GameObjectId != expected.GameObjectId ||
            current.EntityId != expected.EntityId)
        {
            return null;
        }

        var tablePlayer = objectTable.SearchByEntityId(current.EntityId) as IPlayerCharacter;
        return tablePlayer is not null &&
               tablePlayer.Address == current.Address &&
               tablePlayer.GameObjectId == current.GameObjectId &&
               tablePlayer.EntityId == current.EntityId
            ? current
            : null;
    }

    private bool IsCurrentCrystallineConflict()
    {
        var condition = dutyState.ContentFinderCondition;
        var conditionValid = condition.IsValid;
        return PvPMatchRules.ResolveSupportedContext(
                   clientState.IsPvP,
                   clientState.IsPvPExcludingDen,
                   configuration.EnableWolvesDenTesting,
                   clientState.TerritoryType,
                   conditionValid,
                   conditionValid && condition.Value.PvP,
                   conditionValid ? condition.Value.ContentUICategory.RowId : 0,
                   conditionValid && condition.Value.CrystallineConflictCasualRoulette,
                   conditionValid && condition.Value.CrystallineConflictRankedRoulette) ==
               SupportedPvPContext.CrystallineConflict;
    }

    private bool IsCurrentlySuppressedByGuard(
        IPlayerCharacter localPlayer,
        long nowMilliseconds)
    {
        if (DefensiveUtilityProbe.HasActiveGuard(localPlayer)) return true;

        return nearAssist.TryGetRecentExactLocalGuardAttempt(
            clientState.TerritoryType,
            localPlayer.GameObjectId,
            localPlayer.EntityId,
            nowMilliseconds,
            DefensiveUtilityRules.GuardPropagationLatchMilliseconds,
            out _);
    }

    private static bool HasLiveGuard(IPlayerCharacter player)
    {
        foreach (var status in player.StatusList)
        {
            if (ScholarCriticalStrategyRules.IsExactGuardStatus(status.StatusId) &&
                float.IsFinite(status.RemainingTime) &&
                status.RemainingTime > 0f)
            {
                return true;
            }
        }

        return false;
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
        IsValidEntityId(player.EntityId) &&
        IsValidGameObjectId(player.GameObjectId);

    private static bool IsValidEntityId(uint entityId) =>
        entityId is not 0 and not 0xE0000000;

    private static bool IsValidGameObjectId(ulong gameObjectId) =>
        gameObjectId is not 0 and not 0xE0000000;

    private void LogAttemptFailure(Exception exception, long nowMilliseconds)
    {
        if (nowMilliseconds < nextErrorLogAt) return;
        nextErrorLogAt = nowMilliseconds + 10_000;
        log.Error(
            exception,
            "Seiton Sense Scholar Critical Strategy attempt failed and will not be retried.");
    }
}
