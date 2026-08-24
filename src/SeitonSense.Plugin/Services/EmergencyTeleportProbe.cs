using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using SeitonSense.Core;
using SeitonSense.Plugin.Models;

namespace SeitonSense.Plugin.Services;

internal sealed record EmergencyTeleportProbeSnapshot(
    EmergencyTeleportDecisionKind Decision,
    EmergencyTeleportDecisionReason Reason,
    EmergencyTeleportDangerSignal Danger,
    uint ResolvedActionId,
    uint CurrentHp,
    uint MaximumHp,
    uint CurrentMp,
    uint MaximumMp,
    bool DirectPressureKnown,
    int DirectEnemyCount,
    bool EpisodeOpen,
    bool EpisodeSpent,
    ulong EpisodeToken,
    int CandidateCount,
    int PartySlot,
    ulong TargetGameObjectId,
    uint TargetEntityId,
    float TravelDistanceYalms,
    int NearbyEnemyCount,
    float MinimumEnemyClearanceYalms,
    VirtualKey HeldGameplayKey,
    bool InputClaimed,
    HeldCastCancellationRequest? CastCancellationRequest,
    bool UseActionAttempted,
    ClientActionAttemptOutcome NativeOutcome,
    long AttemptCount,
    long AcceptedCount,
    string LastEvent)
{
    internal static EmergencyTeleportProbeSnapshot Initial { get; } = new(
        EmergencyTeleportDecisionKind.None,
        EmergencyTeleportDecisionReason.None,
        EmergencyTeleportDangerSignal.Unknown,
        0,
        0,
        0,
        0,
        0,
        false,
        0,
        false,
        false,
        0,
        0,
        0,
        0,
        0,
        0f,
        0,
        0f,
        VirtualKey.NO_KEY,
        false,
        null,
        false,
        ClientActionAttemptOutcome.None,
        0,
        0,
        "Waiting");
}

/// <summary>
/// One strict held-key escape attempt per current low-HP/low-MP/direct-focus
/// episode. The chosen party actor is frozen and revalidated; no current-target
/// fallback, alternate destination, or retry exists.
/// </summary>
internal sealed unsafe class EmergencyTeleportProbe
{
    private const float MaximumCancellationAnimationLockSeconds =
        HeldCastCancellationRules.MaximumCancellationAnimationLockSeconds;

    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly IDutyState dutyState;
    private readonly PluginConfiguration configuration;
    private readonly TargetPressureTracker pressureTracker;
    private readonly NearAssistRedirector nearAssist;
    private readonly IPluginLog log;

    private EmergencyTeleportState state = EmergencyTeleportState.Initial;
    private EmergencyTeleportProbeSnapshot snapshot = EmergencyTeleportProbeSnapshot.Initial;
    private long attemptCount;
    private long acceptedCount;
    private long nextErrorLogAt;

    internal EmergencyTeleportProbe(
        IClientState clientState,
        IObjectTable objectTable,
        IDutyState dutyState,
        PluginConfiguration configuration,
        TargetPressureTracker pressureTracker,
        NearAssistRedirector nearAssist,
        IPluginLog log)
    {
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.dutyState = dutyState;
        this.configuration = configuration;
        this.pressureTracker = pressureTracker;
        this.nearAssist = nearAssist;
        this.log = log;
    }

    internal EmergencyTeleportProbeSnapshot Snapshot => Volatile.Read(ref snapshot);

    internal EmergencyTeleportProbeSnapshot Observe(
        IPlayerCharacter? localPlayer,
        SupportedPvPContext context,
        bool configurationEnabled,
        bool metadataVerified,
        bool actionHelpersSuppressedByGuard,
        bool higherPriorityClaimed,
        EmergencyActionInputFrame inputFrame,
        long nowMilliseconds,
        bool hardReset = false)
    {
        var observation = BuildObservation(
            localPlayer,
            context,
            configurationEnabled,
            metadataVerified,
            actionHelpersSuppressedByGuard,
            higherPriorityClaimed,
            inputFrame,
            nowMilliseconds,
            hardReset);
        var decision = EmergencyTeleportRules.Observe(state, observation);
        state = decision.NextState;

        var inputClaimed = decision.InputClaimed;

        var attempted = false;
        var outcome = ClientActionAttemptOutcome.None;
        var finalCommitReason = EmergencyTeleportDecisionReason.None;
        if (decision.ShouldDispatch)
        {
            try
            {
                outcome = TryCommitAndUseOnce(
                    configurationEnabled,
                    metadataVerified,
                    actionHelpersSuppressedByGuard,
                    higherPriorityClaimed,
                    inputFrame,
                    out attempted,
                    out finalCommitReason);
            }
            catch (Exception exception)
            {
                outcome = ClientActionAttemptOutcome.AcceptanceUnknown;
                state = EmergencyTeleportRules.RecordNativeOutcome(
                    state,
                    outcome,
                    Environment.TickCount64);
                LogFailure(exception);
            }
        }

        if (attempted) Interlocked.Increment(ref attemptCount);
        if (outcome == ClientActionAttemptOutcome.ClientAccepted)
            Interlocked.Increment(ref acceptedCount);

        var selected = ResolveSelectedCandidate(decision, observation.Candidates);
        var castCancellationRequest = BuildCastCancellationRequest(
            localPlayer,
            inputFrame,
            inputClaimed);
        // Keep the raw held-key snapshot readable through the final exact commit.
        // Consuming the shared frame earlier makes HeldGameplayKeyEligible false
        // and would make every otherwise-valid native dispatch fail closed.
        if (inputClaimed) inputFrame.Consume();
        var heldKey = state.Intent is { IsValid: true } intent
            ? (VirtualKey)intent.FrozenKeyCode
            : inputFrame.Snapshot.HeldGameplayKey;
        var result = new EmergencyTeleportProbeSnapshot(
            decision.Kind,
            decision.Reason,
            decision.DangerSignal,
            observation.ResolvedActionId,
            observation.CurrentHp,
            observation.MaximumHp,
            observation.CurrentMp,
            observation.MaximumMp,
            observation.DirectPressureKnown,
            observation.DirectEnemyCount,
            state.EpisodeOpen,
            state.EpisodeSpent,
            state.EpisodeToken,
            observation.Candidates?.Count ?? 0,
            selected?.PartySlot ?? 0,
            selected?.Actor.GameObjectId ?? 0,
            selected?.Actor.EntityId ?? 0,
            selected?.TravelDistanceYalms ?? 0f,
            selected?.NearbyEnemyCount ?? 0,
            selected?.MinimumEnemyEdgeClearanceYalms ?? 0f,
            heldKey,
            inputClaimed,
            castCancellationRequest,
            attempted,
            outcome,
            Interlocked.Read(ref attemptCount),
            Interlocked.Read(ref acceptedCount),
            Describe(decision, attempted, outcome, finalCommitReason));
        Volatile.Write(ref snapshot, result);
        return result;
    }

    internal void Reset()
    {
        state = EmergencyTeleportState.Initial;
        Volatile.Write(ref snapshot, EmergencyTeleportProbeSnapshot.Initial with
        {
            AttemptCount = Interlocked.Read(ref attemptCount),
            AcceptedCount = Interlocked.Read(ref acceptedCount),
            LastEvent = "Reset",
        });
    }

    internal EmergencyTeleportProbeSnapshot FailClosed()
    {
        state = EmergencyTeleportState.Initial;
        var result = EmergencyTeleportProbeSnapshot.Initial with
        {
            Decision = EmergencyTeleportDecisionKind.Cancelled,
            Reason = EmergencyTeleportDecisionReason.HardReset,
            AttemptCount = Interlocked.Read(ref attemptCount),
            AcceptedCount = Interlocked.Read(ref acceptedCount),
            LastEvent = "Failed closed",
        };
        Volatile.Write(ref snapshot, result);
        return result;
    }

    private EmergencyTeleportObservation BuildObservation(
        IPlayerCharacter? localPlayer,
        SupportedPvPContext context,
        bool configurationEnabled,
        bool metadataVerified,
        bool actionHelpersSuppressedByGuard,
        bool higherPriorityClaimed,
        EmergencyActionInputFrame inputFrame,
        long nowMilliseconds,
        bool hardReset)
    {
        var localIdentity = TryGetExactLocalIdentity(localPlayer, out var exactLocal)
            ? exactLocal
            : default;
        var localJobId = localPlayer?.ClassJob.IsValid == true
            ? localPlayer.ClassJob.RowId
            : 0;
        var resolvedActionId = 0u;
        var cooldownReady = false;
        var resourcesReady = false;
        var nativeBoundaryReady = false;
        var actionStateKnown = localPlayer is not null &&
                               TryReadActionState(
                                   localPlayer,
                                   localJobId,
                                   out resolvedActionId,
                                   out cooldownReady,
                                   out resourcesReady,
                                   out nativeBoundaryReady);
        var actionReady = actionStateKnown && cooldownReady && resourcesReady;

        DirectSelfPressureSnapshot directPressure = default;
        var directPressureKnown = localIdentity.IsValid &&
                                  pressureTracker.TryGetFreshSelfDirectIncomingPressure(
                                      localIdentity,
                                      nowMilliseconds,
                                      EmergencyTeleportRules.MaximumPressureAgeMilliseconds,
                                      out directPressure);
        if (!directPressureKnown) directPressure = default;

        var settings = new EmergencyTeleportSettings(
            configuration.EmergencyTeleportHpPercent,
            (uint)Math.Clamp(configuration.EmergencyTeleportMpThreshold, 0, 10_000),
            configuration.EmergencyTeleportMinimumFocusedEnemies,
            configuration.EmergencyTeleportMinimumTravelYalms,
            configuration.EmergencyTeleportEnemySafetyRadiusYalms,
            configuration.EmergencyTeleportMaximumNearbyEnemies);
        var frozenKeyCode = state.Intent?.FrozenKeyCode ?? 0;
        var frozenKeyStillDown = frozenKeyCode > 0 &&
                                 inputFrame.IsGameplayKeyPhysicallyDown(
                                     (VirtualKey)frozenKeyCode);
        var shouldResolveCandidates = localPlayer is not null &&
                                      localIdentity.IsValid &&
                                      metadataVerified &&
                                      EmergencyTeleportRules.IsExactJobAction(
                                          localJobId,
                                          resolvedActionId) &&
                                      (state.Intent is not null || actionReady);
        var candidates = shouldResolveCandidates
            ? ResolveExactCandidates(
                localPlayer!,
                context,
                resolvedActionId,
                settings,
                checkCastingActive: nativeBoundaryReady)
            : [];
        var input = inputFrame.Snapshot;
        return new EmergencyTeleportObservation(
            configurationEnabled,
            settings,
            context,
            localIdentity,
            IsAlive(localPlayer),
            localPlayer?.IsTargetable == true,
            localJobId,
            metadataVerified,
            actionHelpersSuppressedByGuard,
            higherPriorityClaimed,
            input.ProbeSucceeded,
            input.IsTextInputActive,
            inputFrame.HeldGameplayKeyEligible,
            (int)input.HeldGameplayKey,
            frozenKeyStillDown,
            resolvedActionId,
            actionReady,
            nativeBoundaryReady,
            localPlayer?.CurrentHp ?? 0,
            localPlayer?.MaxHp ?? 0,
            localPlayer?.CurrentMp ?? 0,
            localPlayer?.MaxMp ?? 0,
            directPressureKnown,
            directPressure.UniqueEnemyCount,
            directPressure.PublishedAtMilliseconds,
            candidates,
            nowMilliseconds,
            hardReset);
    }

    private ClientActionAttemptOutcome TryCommitAndUseOnce(
        bool configurationEnabled,
        bool metadataVerified,
        bool actionHelpersSuppressedByGuard,
        bool higherPriorityClaimed,
        EmergencyActionInputFrame inputFrame,
        out bool attempted,
        out EmergencyTeleportDecisionReason finalCommitReason)
    {
        attempted = false;
        finalCommitReason = EmergencyTeleportDecisionReason.None;
        var currentLocal = objectTable.LocalPlayer;
        var now = Environment.TickCount64;
        var currentContext = ResolveCurrentContext();

        var finalObservation = BuildObservation(
            currentLocal,
            currentContext,
            configurationEnabled,
            metadataVerified,
            actionHelpersSuppressedByGuard,
            higherPriorityClaimed,
            inputFrame,
            now,
            hardReset: false);
        var commit = EmergencyTeleportRules.CommitNativeAttempt(state, finalObservation);
        state = commit.NextState;
        finalCommitReason = commit.Reason;
        if (!commit.ShouldInvokeNative || commit.Intent is not { IsValid: true } intent)
            return ClientActionAttemptOutcome.NotInvoked;

        var actionManager = ActionManager.Instance();
        if (actionManager == null)
        {
            state = EmergencyTeleportRules.RecordNativeOutcome(
                state,
                ClientActionAttemptOutcome.NotInvoked,
                Environment.TickCount64);
            return ClientActionAttemptOutcome.NotInvoked;
        }

        var before = ClientActionAttemptBoundary.Capture(actionManager, intent.ActionId);
        attempted = true;
        var accepted = nearAssist.RunWithoutRedirect(() =>
            actionManager->UseAction(
                ActionType.Action,
                intent.ActionId,
                intent.Target.GameObjectId,
                0,
                ActionManager.UseActionMode.None,
                0));
        var outcome = ClientActionAttemptBoundaryRules.Classify(
            accepted,
            intent.ActionId,
            before,
            ClientActionAttemptBoundary.Capture(actionManager, intent.ActionId));
        state = EmergencyTeleportRules.RecordNativeOutcome(
            state,
            outcome,
            Environment.TickCount64);
        return outcome;
    }

    private HeldCastCancellationRequest? BuildCastCancellationRequest(
        IPlayerCharacter? localPlayer,
        EmergencyActionInputFrame inputFrame,
        bool inputClaimed)
    {
        if (!inputClaimed ||
            state.Intent is not { IsValid: true } intent ||
            localPlayer is null ||
            !TryGetExactLocalIdentity(localPlayer, out var localIdentity) ||
            localIdentity != intent.LocalPlayer ||
            !localPlayer.IsCasting)
        {
            return null;
        }

        var actionManager = ActionManager.Instance();
        if (actionManager == null ||
            actionManager->CastActionId == 0 ||
            actionManager->ActionQueued ||
            !float.IsFinite(actionManager->AnimationLock) ||
            actionManager->AnimationLock < 0f ||
            actionManager->AnimationLock > MaximumCancellationAnimationLockSeconds ||
            !inputFrame.IsGameplayKeyPhysicallyDown((VirtualKey)intent.FrozenKeyCode))
        {
            return null;
        }

        var request = new HeldCastCancellationRequest(
            HeldCastCancellationHelperKind.EmergencyTeleport,
            intent.ActionId,
            intent.LocalPlayer,
            intent.Target,
            intent.FrozenKeyCode,
            intent.EpisodeToken);
        return request.IsValid ? request : null;
    }

    private IReadOnlyList<EmergencyTeleportCandidate> ResolveExactCandidates(
        IPlayerCharacter localPlayer,
        SupportedPvPContext context,
        uint actionId,
        EmergencyTeleportSettings settings,
        bool checkCastingActive)
    {
        var enemySnapshot = ResolveEnemySnapshot(localPlayer, context);
        var actionManager = ActionManager.Instance();
        var source = GetNativeObject(localPlayer);
        if (!enemySnapshot.IsComplete || actionManager == null || source == null)
            return [];

        var candidates = new List<EmergencyTeleportCandidate>(8);
        var seenGameObjectIds = new HashSet<ulong>();
        var seenEntityIds = new HashSet<uint>();
        for (var slot = EmergencyTeleportRules.FirstPartySlot;
             slot <= EmergencyTeleportRules.LastPartySlot;
             slot++)
        {
            var ally = PartySlotResolver.Resolve(objectTable, slot);
            if (!HasValidIdentity(ally)) continue;
            if (ally!.GameObjectId == localPlayer.GameObjectId ||
                ally.EntityId == localPlayer.EntityId) continue;
            if (!seenGameObjectIds.Add(ally.GameObjectId) ||
                !seenEntityIds.Add(ally.EntityId)) return [];

            var exact = objectTable.SearchByEntityId(ally.EntityId) as IPlayerCharacter;
            var exactPartyMember = HasValidIdentity(exact) &&
                                   exact!.Address == ally.Address &&
                                   exact.GameObjectId == ally.GameObjectId &&
                                   exact.EntityId == ally.EntityId;
            var target = exactPartyMember ? GetNativeObject(exact!) : null;
            var targetActionReady = target != null &&
                                    actionManager->GetAdjustedActionId(actionId) == actionId &&
                                    actionManager->GetActionStatus(
                                        ActionType.Action,
                                        actionId,
                                        ally.GameObjectId,
                                        checkRecastActive: true,
                                        checkCastingActive: checkCastingActive) == 0;
            var rangeResult = target == null
                ? uint.MaxValue
                : ActionManager.GetActionInRangeOrLoS(actionId, source, target);
            var travelDistance = HorizontalEdgeDistance(
                localPlayer.Position.X,
                localPlayer.Position.Z,
                localPlayer.HitboxRadius,
                ally.Position.X,
                ally.Position.Z,
                ally.HitboxRadius);
            var nearbyEnemyCount = 0;
            var minimumClearance = float.MaxValue;
            var distancesKnown = float.IsFinite(travelDistance);
            foreach (var enemy in enemySnapshot.LiveEnemies)
            {
                var clearance = HorizontalEdgeDistance(
                    ally.Position.X,
                    ally.Position.Z,
                    ally.HitboxRadius,
                    enemy.X,
                    enemy.Z,
                    enemy.HitboxRadius);
                if (!float.IsFinite(clearance))
                {
                    distancesKnown = false;
                    break;
                }

                minimumClearance = MathF.Min(minimumClearance, clearance);
                if (clearance <= settings.EnemySafetyRadiusYalms)
                    nearbyEnemyCount++;
            }

            candidates.Add(new EmergencyTeleportCandidate(
                new TargetPressureActorIdentity(ally.GameObjectId, ally.EntityId),
                slot,
                ally.CurrentHp,
                ally.MaxHp,
                travelDistance,
                nearbyEnemyCount,
                minimumClearance,
                exactPartyMember,
                IsSelf: false,
                IsAlive(ally),
                ally.IsTargetable,
                HasValidNativeTarget: target != null,
                HasValidActionTarget: targetActionReady,
                HasNativeRangeAndLineOfSight:
                    target != null &&
                    SeitonRangeRules.HasNativeRangeAndLineOfSight(rangeResult),
                HasCompleteEnemySnapshot: enemySnapshot.IsComplete && distancesKnown));
        }

        return candidates;
    }

    private EmergencyEnemySnapshot ResolveEnemySnapshot(
        IPlayerCharacter localPlayer,
        SupportedPvPContext context)
    {
        var threats = new List<EmergencyEnemyThreat>(5);
        var seenGameObjectIds = new HashSet<ulong>();
        var seenEntityIds = new HashSet<uint>();
        if (context == SupportedPvPContext.WolvesDen)
        {
            var opponent = WolvesDenOpponentResolver.Resolve(
                objectTable,
                localPlayer,
                out _,
                out _,
                out _);
            return TryAppendExactEnemy(
                opponent,
                localPlayer,
                threats,
                seenGameObjectIds,
                seenEntityIds)
                ? new EmergencyEnemySnapshot(true, threats.ToArray())
                : EmergencyEnemySnapshot.Incomplete;
        }

        if (context != SupportedPvPContext.CrystallineConflict)
            return EmergencyEnemySnapshot.Incomplete;

        for (var slot = EnemySlotRules.FirstSlot; slot <= EnemySlotRules.LastSlot; slot++)
        {
            if (!TryAppendExactEnemy(
                    EnemySlotResolver.Resolve(objectTable, slot),
                    localPlayer,
                    threats,
                    seenGameObjectIds,
                    seenEntityIds))
            {
                return EmergencyEnemySnapshot.Incomplete;
            }
        }

        return seenGameObjectIds.Count == EmergencyTeleportRules.MaximumCanonicalEnemyCount &&
               seenEntityIds.Count == EmergencyTeleportRules.MaximumCanonicalEnemyCount
            ? new EmergencyEnemySnapshot(true, threats.ToArray())
            : EmergencyEnemySnapshot.Incomplete;
    }

    private static bool TryAppendExactEnemy(
        IPlayerCharacter? enemy,
        IPlayerCharacter localPlayer,
        ICollection<EmergencyEnemyThreat> threats,
        ISet<ulong> seenGameObjectIds,
        ISet<uint> seenEntityIds)
    {
        if (!HasValidIdentity(enemy) ||
            enemy!.GameObjectId == localPlayer.GameObjectId ||
            enemy.EntityId == localPlayer.EntityId ||
            !seenGameObjectIds.Add(enemy.GameObjectId) ||
            !seenEntityIds.Add(enemy.EntityId) ||
            !float.IsFinite(enemy.Position.X) ||
            !float.IsFinite(enemy.Position.Z) ||
            !float.IsFinite(enemy.HitboxRadius) ||
            enemy.HitboxRadius < 0f)
        {
            return false;
        }

        if (enemy.IsDead) return true;
        if (enemy.CurrentHp == 0 || enemy.MaxHp < enemy.CurrentHp) return false;
        threats.Add(new EmergencyEnemyThreat(
            enemy.Position.X,
            enemy.Position.Z,
            enemy.HitboxRadius));
        return true;
    }

    private static unsafe bool TryReadActionState(
        IPlayerCharacter localPlayer,
        uint localJobId,
        out uint resolvedActionId,
        out bool cooldownReady,
        out bool resourcesReady,
        out bool nativeBoundaryReady)
    {
        resolvedActionId = 0;
        cooldownReady = false;
        resourcesReady = false;
        nativeBoundaryReady = false;
        if (!HasValidIdentity(localPlayer) ||
            !EmergencyTeleportRules.TryGetActionForJob(localJobId, out var baseActionId))
        {
            return false;
        }

        var actionManager = ActionManager.Instance();
        if (actionManager == null) return false;
        resolvedActionId = actionManager->GetAdjustedActionId(baseActionId);
        if (resolvedActionId != baseActionId) return true;
        cooldownReady = actionManager->IsActionOffCooldown(ActionType.Action, resolvedActionId);
        resourcesReady = actionManager->CheckActionResources(ActionType.Action, resolvedActionId) == 0;
        nativeBoundaryReady = HeldActionRetryRules.IsNativeBoundaryNearQueueable(
            actionManager->AnimationLock,
            localPlayer.IsCasting,
            actionManager->CastActionId,
            actionManager->ActionQueued);
        return true;
    }

    private bool TryGetExactLocalIdentity(
        IPlayerCharacter? localPlayer,
        out TargetPressureActorIdentity identity)
    {
        identity = default;
        if (!HasValidIdentity(localPlayer)) return false;
        var current = objectTable.LocalPlayer;
        if (!HasValidIdentity(current) ||
            current!.Address != localPlayer!.Address ||
            current.GameObjectId != localPlayer.GameObjectId ||
            current.EntityId != localPlayer.EntityId)
        {
            return false;
        }

        identity = new TargetPressureActorIdentity(
            localPlayer.GameObjectId,
            localPlayer.EntityId);
        return identity.IsValid;
    }

    private SupportedPvPContext ResolveCurrentContext()
    {
        var condition = dutyState.ContentFinderCondition;
        return PvPMatchRules.ResolveSupportedContext(
            clientState.IsPvP,
            clientState.IsPvPExcludingDen,
            configuration.EnableWolvesDenTesting,
            clientState.TerritoryType,
            condition.IsValid,
            condition.IsValid && condition.Value.PvP,
            condition.IsValid ? condition.Value.ContentUICategory.RowId : 0,
            condition.IsValid && condition.Value.CrystallineConflictCasualRoulette,
            condition.IsValid && condition.Value.CrystallineConflictRankedRoulette);
    }

    private static unsafe GameObject* GetNativeObject(IPlayerCharacter player)
    {
        var native = (GameObject*)player.Address;
        return native != null && native->EntityId == player.EntityId ? native : null;
    }

    private static float HorizontalEdgeDistance(
        float leftX,
        float leftZ,
        float leftHitboxRadius,
        float rightX,
        float rightZ,
        float rightHitboxRadius)
    {
        if (!float.IsFinite(leftX) ||
            !float.IsFinite(leftZ) ||
            !float.IsFinite(leftHitboxRadius) ||
            leftHitboxRadius < 0f ||
            !float.IsFinite(rightX) ||
            !float.IsFinite(rightZ) ||
            !float.IsFinite(rightHitboxRadius) ||
            rightHitboxRadius < 0f)
        {
            return float.NaN;
        }

        var x = leftX - rightX;
        var z = leftZ - rightZ;
        var centerSquared = (x * x) + (z * z);
        if (!float.IsFinite(centerSquared) || centerSquared < 0f) return float.NaN;
        return MathF.Max(
            0f,
            MathF.Sqrt(centerSquared) - leftHitboxRadius - rightHitboxRadius);
    }

    private static bool IsAlive(IPlayerCharacter? player) =>
        player is not null &&
        !player.IsDead &&
        player.CurrentHp > 0 &&
        player.MaxHp >= player.CurrentHp;

    private static bool HasValidIdentity(IPlayerCharacter? player) =>
        player is not null &&
        player.Address != nint.Zero &&
        player.IsValid() &&
        player.GameObjectId is not 0 and not 0xE0000000UL and not ulong.MaxValue &&
        player.EntityId is not 0 and not 0xE0000000u and not uint.MaxValue;

    private static EmergencyTeleportCandidate? ResolveSelectedCandidate(
        EmergencyTeleportDecision decision,
        IReadOnlyList<EmergencyTeleportCandidate>? candidates) =>
        candidates is not null &&
        decision.SelectedCandidateIndex >= 0 &&
        decision.SelectedCandidateIndex < candidates.Count
            ? candidates[decision.SelectedCandidateIndex]
            : null;

    private static string Describe(
        EmergencyTeleportDecision decision,
        bool attempted,
        ClientActionAttemptOutcome outcome,
        EmergencyTeleportDecisionReason finalCommitReason) =>
        attempted
            ? $"One-shot episode committed: {outcome}"
            : decision.ShouldDispatch
                ? $"Final preflight: {outcome} ({finalCommitReason})"
                : $"{decision.Kind}: {decision.Reason}";

    private void LogFailure(Exception exception)
    {
        var now = Environment.TickCount64;
        if (now < nextErrorLogAt) return;
        nextErrorLogAt = now + 10_000;
        log.Error(
            exception,
            "Seiton Sense Emergency Teleport native boundary ended ambiguously; the danger episode remains spent.");
    }

    private readonly record struct EmergencyEnemyThreat(
        float X,
        float Z,
        float HitboxRadius);

    private readonly record struct EmergencyEnemySnapshot(
        bool IsComplete,
        EmergencyEnemyThreat[] LiveEnemies)
    {
        internal static EmergencyEnemySnapshot Incomplete { get; } = new(false, []);
    }
}
