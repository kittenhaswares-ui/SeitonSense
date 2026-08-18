using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Party;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using SeitonSense.Core;
using SeitonSense.Plugin.Models;

namespace SeitonSense.Plugin.Services;

internal sealed record SmartKardiaProbeSnapshot(
    SmartKardiaDecisionKind Decision,
    SmartKardiaDecisionReason Reason,
    uint ResolvedActionId,
    int CandidateCount,
    int PartySlot,
    ulong TargetGameObjectId,
    uint TargetEntityId,
    bool TargetIsSelf,
    bool PressureKnown,
    int IncomingEnemyCount,
    bool OwnKardionStateKnown,
    bool HasOwnKardion,
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
    internal static SmartKardiaProbeSnapshot Initial { get; } = new(
        SmartKardiaDecisionKind.None,
        SmartKardiaDecisionReason.None,
        0,
        0,
        0,
        0,
        0,
        false,
        false,
        0,
        false,
        false,
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
/// one exact PvP Kardia request. Selection requires one coherent, complete CC
/// party view. After the generation is consumed, only the frozen party slot
/// and actor may be revalidated; there is no alternate, fallback, or retry.
/// </summary>
internal sealed unsafe class SmartKardiaProbe
{
    internal const uint KardiaIconId = 9_580;
    internal const uint KardiaStatusIconId = 212_951;
    internal const uint KardionStatusIconId = 212_952;
    internal const ushort ExpectedRecast100ms = 10;
    internal const int ExpectedRange = 30;

    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly IPartyList partyList;
    private readonly IDutyState dutyState;
    private readonly PluginConfiguration configuration;
    private readonly TargetPressureTracker pressureTracker;
    private readonly NearAssistRedirector nearAssist;
    private readonly IPluginLog log;
    private SmartKardiaProbeSnapshot snapshot = SmartKardiaProbeSnapshot.Initial;
    private long attemptCount;
    private long acceptedCount;
    private long nextErrorLogAt;
    private string lastEvent = "Waiting";

    internal SmartKardiaProbe(
        IClientState clientState,
        IObjectTable objectTable,
        IPartyList partyList,
        IDutyState dutyState,
        PluginConfiguration configuration,
        TargetPressureTracker pressureTracker,
        NearAssistRedirector nearAssist,
        IPluginLog log)
    {
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.partyList = partyList;
        this.dutyState = dutyState;
        this.configuration = configuration;
        this.pressureTracker = pressureTracker;
        this.nearAssist = nearAssist;
        this.log = log;
    }

    internal SmartKardiaProbeSnapshot Snapshot => Volatile.Read(ref snapshot);

    internal SmartKardiaProbeSnapshot Observe(
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
        var localIdentity = TryGetExactIdentity(localPlayer, out var exactLocalIdentity)
            ? exactLocalIdentity
            : default;
        var localJobId = localPlayer?.ClassJob.IsValid == true
            ? localPlayer.ClassJob.RowId
            : 0;
        var featureContextReady = configurationEnabled &&
                                  isCrystallineConflict &&
                                  localAlive &&
                                  localJobId == SmartKardiaRules.SageJobId &&
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
        var capture = shouldResolveCandidates
            ? CaptureExactParty(localPlayer!, resolvedActionId, out candidateResolution)
            : ExactPartyCapture.Incomplete;
        var candidates = capture.Members
            .Select(static member => member.Candidate)
            .ToArray();
        var decision = SmartKardiaRules.Observe(
            new SmartKardiaObservation(
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
                capture.Complete,
                candidates,
                hardReset));

        // This physical generation becomes terminal before any final native
        // reads. Drift, rejection, or an exception cannot select another actor
        // or allow a lower-priority helper to reuse the same generation.
        var inputClaimed = decision.ShouldConsumeInputGeneration;
        if (inputClaimed) inputFrame.Consume();

        var attempted = false;
        var accepted = false;
        if (decision.ShouldDispatch &&
            decision.Intent is { } intent &&
            decision.SelectedCandidateIndex >= 0 &&
            decision.SelectedCandidateIndex < capture.Members.Count)
        {
            var selected = capture.Members[decision.SelectedCandidateIndex];
            try
            {
                var currentLocal = ResolveExactLocalPlayer(intent.LocalPlayer);
                var finalContextReady = currentLocal is not null &&
                                        configuration.Enabled &&
                                        configuration.EnableSageKardiaOnHeldKey &&
                                        IsCurrentCrystallineConflict() &&
                                        IsLivePlayer(currentLocal) &&
                                        currentLocal.ClassJob.IsValid &&
                                        currentLocal.ClassJob.RowId == SmartKardiaRules.SageJobId &&
                                        metadataVerified &&
                                        !IsCurrentlySuppressedByGuard(
                                            currentLocal,
                                            Environment.TickCount64);
                var finalResolvedActionId = 0u;
                var finalActionReady = finalContextReady &&
                                       TryGetReadyAction(currentLocal!, out finalResolvedActionId);
                if (!finalActionReady) finalResolvedActionId = 0;
                var finalCandidate = finalContextReady
                    ? ResolveFrozenCandidate(
                        currentLocal!,
                        intent,
                        selected.Address,
                        finalResolvedActionId)
                    : null;
                var currentLocalIdentity = currentLocal is not null
                    ? new TargetPressureActorIdentity(
                        currentLocal.GameObjectId,
                        currentLocal.EntityId)
                    : default;
                if (finalCandidate is { } exactCandidate &&
                    SmartKardiaRules.CanUseFrozenIntent(
                        intent,
                        exactCandidate,
                        configuration.Enabled && configuration.EnableSageKardiaOnHeldKey,
                        IsCurrentCrystallineConflict(),
                        currentLocal!.ClassJob.RowId,
                        currentLocalIdentity,
                        IsLivePlayer(currentLocal),
                        metadataVerified,
                        IsCurrentlySuppressedByGuard(
                            currentLocal,
                            Environment.TickCount64),
                        finalResolvedActionId,
                        finalActionReady))
                {
                    accepted = TryUseKardiaOnce(
                        intent,
                        selected.Address,
                        metadataVerified,
                        out attempted);
                    if (attempted) Interlocked.Increment(ref attemptCount);
                    if (accepted) Interlocked.Increment(ref acceptedCount);
                    lastEvent = attempted
                        ? $"P{intent.PartySlot} action {intent.ActionId} attempted (accepted={accepted})"
                        : $"P{intent.PartySlot} terminal UseAction-boundary revalidation failed";
                }
                else
                {
                    lastEvent = $"P{intent.PartySlot} terminal frozen-intent revalidation failed";
                }
            }
            catch (Exception exception)
            {
                if (attempted) Interlocked.Increment(ref attemptCount);
                lastEvent = $"P{intent.PartySlot} terminal action exception";
                LogAttemptFailure(exception, nowMilliseconds);
            }
        }
        else
        {
            lastEvent = decision.Reason.ToString();
        }

        var selectedCandidate = decision.SelectedCandidateIndex >= 0 &&
                                decision.SelectedCandidateIndex < candidates.Length
            ? candidates[decision.SelectedCandidateIndex]
            : (SmartKardiaCandidate?)null;
        var result = new SmartKardiaProbeSnapshot(
            decision.Kind,
            decision.Reason,
            resolvedActionId,
            candidates.Length,
            selectedCandidate?.PartySlot ?? 0,
            selectedCandidate?.Actor.GameObjectId ?? 0,
            selectedCandidate?.Actor.EntityId ?? 0,
            selectedCandidate?.IsSelf ?? false,
            selectedCandidate?.PressureKnown ?? false,
            selectedCandidate?.UniqueIncomingEnemyCount ?? 0,
            selectedCandidate?.OwnKardionStateKnown ?? false,
            selectedCandidate?.HasOwnKardion ?? false,
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
        Volatile.Write(ref snapshot, SmartKardiaProbeSnapshot.Initial with
        {
            AttemptCount = Interlocked.Read(ref attemptCount),
            AcceptedCount = Interlocked.Read(ref acceptedCount),
            LastEvent = lastEvent,
        });
    }

    internal SmartKardiaProbeSnapshot FailClosed()
    {
        lastEvent = "Failed closed";
        var result = SmartKardiaProbeSnapshot.Initial with
        {
            Decision = SmartKardiaDecisionKind.Cancelled,
            Reason = SmartKardiaDecisionReason.HardReset,
            AttemptCount = Interlocked.Read(ref attemptCount),
            AcceptedCount = Interlocked.Read(ref acceptedCount),
            LastEvent = lastEvent,
        };
        Volatile.Write(ref snapshot, result);
        return result;
    }

    private ExactPartyCapture CaptureExactParty(
        IPlayerCharacter localPlayer,
        uint actionId,
        out string resolution)
    {
        var partyBefore = CaptureExactPartyEntityIds();
        if (partyBefore is null || !partyBefore.Contains(localPlayer.EntityId))
        {
            resolution = "Incomplete or ambiguous native party list";
            return ExactPartyCapture.Incomplete;
        }

        var pressureViewActive = pressureTracker.TryCaptureIncomingAllyPressure(
            out var pressureCounts);
        if (!pressureViewActive)
        {
            resolution = "Incoming-pressure view unavailable";
            return ExactPartyCapture.Incomplete;
        }

        var members = new List<RuntimeCandidate>(
            SmartKardiaRules.RequiredCrystallineConflictPartySize);
        for (var slot = SmartKardiaRules.FirstPartySlot;
             slot <= SmartKardiaRules.LastPartySlot;
             slot++)
        {
            var player = PartySlotResolver.Resolve(objectTable, slot);
            if (!TryGetExactIdentity(player, out _)) continue;
            members.Add(BuildCandidate(
                localPlayer,
                player!,
                slot,
                actionId,
                pressureCounts));
        }

        var pressureStillActive = pressureTracker.TryCaptureIncomingAllyPressure(
            out var pressureCountsAfter);
        var partyAfter = CaptureExactPartyEntityIds();
        var complete = partyAfter is not null &&
                       partyBefore.SetEquals(partyAfter) &&
                       pressureStillActive &&
                       ReferenceEquals(pressureCounts, pressureCountsAfter) &&
                       members.Count == SmartKardiaRules.RequiredCrystallineConflictPartySize &&
                       members.Select(static member => member.Candidate.Actor.EntityId)
                           .ToHashSet()
                           .SetEquals(partyBefore) &&
                       members.Select(static member => member.Address).Distinct().Count() ==
                       members.Count &&
                       members.All(static member =>
                           !member.Candidate.Alive ||
                           !member.Candidate.Targetable ||
                           member.Candidate.PressureKnown) &&
                       PartySlotsRemainExact(members);
        resolution = complete
            ? "Exact coherent P-party with one pressure publication"
            : "Party identity changed during capture";
        return complete
            ? new ExactPartyCapture(true, members)
            : ExactPartyCapture.Incomplete;
    }

    private RuntimeCandidate BuildCandidate(
        IPlayerCharacter localPlayer,
        IPlayerCharacter target,
        int partySlot,
        uint actionId,
        IReadOnlyDictionary<TargetPressureActorIdentity, int> pressureCounts)
    {
        var localIdentity = new TargetPressureActorIdentity(
            localPlayer.GameObjectId,
            localPlayer.EntityId);
        var targetIdentity = new TargetPressureActorIdentity(
            target.GameObjectId,
            target.EntityId);
        var isSelf = targetIdentity == localIdentity;
        var sourceObject = GetNativeObject(localPlayer);
        var targetObject = GetNativeObject(target);
        var nativeTargetValid = sourceObject != null && targetObject != null;
        var rangeResult = nativeTargetValid &&
                          !isSelf &&
                          actionId == SmartKardiaRules.ActionId
            ? ActionManager.GetActionInRangeOrLoS(actionId, sourceObject, targetObject)
            : uint.MaxValue;
        var nativeRangeAndLineOfSight = nativeTargetValid &&
                                        (isSelf ||
                                         SeitonRangeRules.HasNativeRangeAndLineOfSight(
                                             rangeResult));
        var pressureKnown = pressureCounts.TryGetValue(
            targetIdentity,
            out var incomingEnemyCount) && incomingEnemyCount >= 0;
        if (!pressureKnown) incomingEnemyCount = 0;
        ReadOwnKardionState(
            target,
            localPlayer.EntityId,
            out var ownKardionStateKnown,
            out var hasOwnKardion);

        var candidate = new SmartKardiaCandidate(
            partySlot,
            targetIdentity,
            ExactPartyIdentity: targetIdentity.IsValid,
            IsSelf: isSelf,
            Alive: IsLivePlayer(target),
            target.IsTargetable,
            target.CurrentHp,
            target.MaxHp,
            nativeTargetValid,
            nativeRangeAndLineOfSight,
            pressureKnown,
            incomingEnemyCount,
            ownKardionStateKnown,
            hasOwnKardion);
        return new RuntimeCandidate(candidate, target.Address);
    }

    private SmartKardiaCandidate? ResolveFrozenCandidate(
        IPlayerCharacter localPlayer,
        SmartKardiaIntent intent,
        nint expectedAddress,
        uint actionId)
    {
        if (!intent.IsValid ||
            actionId != intent.ActionId ||
            !ExactPartyStillContains(intent.LocalPlayer, intent.Target))
        {
            return null;
        }

        var target = PartySlotResolver.Resolve(objectTable, intent.PartySlot);
        if (!TryGetExactIdentity(target, out var targetIdentity) ||
            targetIdentity != intent.Target ||
            target!.Address != expectedAddress ||
            !pressureTracker.TryCaptureIncomingAllyPressure(out var pressureCounts))
        {
            return null;
        }

        return BuildCandidate(
            localPlayer,
            target,
            intent.PartySlot,
            actionId,
            pressureCounts).Candidate;
    }

    private bool TryUseKardiaOnce(
        SmartKardiaIntent intent,
        nint expectedTargetAddress,
        bool metadataVerified,
        out bool attempted)
    {
        attempted = false;
        var currentLocal = ResolveExactLocalPlayer(intent.LocalPlayer);
        if (currentLocal is null) return false;

        var configurationEnabled = configuration.Enabled &&
                                   configuration.EnableSageKardiaOnHeldKey;
        var isCrystallineConflict = IsCurrentCrystallineConflict();
        var localAlive = IsLivePlayer(currentLocal);
        var localJobId = currentLocal.ClassJob.IsValid
            ? currentLocal.ClassJob.RowId
            : 0;
        var guardSuppressed = IsCurrentlySuppressedByGuard(
            currentLocal,
            Environment.TickCount64);
        var actionReady = TryGetReadyAction(currentLocal, out var resolvedActionId);
        if (!configurationEnabled ||
            !isCrystallineConflict ||
            !localAlive ||
            localJobId != SmartKardiaRules.SageJobId ||
            !metadataVerified ||
            guardSuppressed ||
            !actionReady ||
            resolvedActionId != intent.ActionId)
        {
            return false;
        }

        var finalCandidate = ResolveFrozenCandidate(
            currentLocal,
            intent,
            expectedTargetAddress,
            resolvedActionId);
        var localIdentity = new TargetPressureActorIdentity(
            currentLocal.GameObjectId,
            currentLocal.EntityId);
        if (finalCandidate is not { } exactCandidate ||
            !SmartKardiaRules.CanUseFrozenIntent(
                intent,
                exactCandidate,
                configurationEnabled,
                isCrystallineConflict,
                currentLocalJobId: localJobId,
                currentLocalPlayer: localIdentity,
                isLocalPlayerAlive: localAlive,
                metadataVerified,
                actionHelpersSuppressedByGuard: guardSuppressed,
                resolvedActionId,
                actionLocallyReady: actionReady))
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

    private bool PartySlotsRemainExact(IReadOnlyList<RuntimeCandidate> members)
    {
        foreach (var member in members)
        {
            var stablePlayer = PartySlotResolver.Resolve(
                objectTable,
                member.Candidate.PartySlot);
            if (!TryGetExactIdentity(stablePlayer, out var stableIdentity) ||
                stableIdentity != member.Candidate.Actor ||
                stablePlayer!.Address != member.Address)
            {
                return false;
            }
        }

        return true;
    }

    private HashSet<uint>? CaptureExactPartyEntityIds()
    {
        var ids = partyList.Select(static member => member.EntityId).ToArray();
        if (ids.Length != SmartKardiaRules.RequiredCrystallineConflictPartySize ||
            ids.Any(static entityId => !IsNetworkEntityId(entityId)))
        {
            return null;
        }

        var unique = ids.ToHashSet();
        return unique.Count == ids.Length ? unique : null;
    }

    private bool ExactPartyStillContains(
        TargetPressureActorIdentity localPlayer,
        TargetPressureActorIdentity target)
    {
        var ids = CaptureExactPartyEntityIds();
        return ids is not null &&
               ids.Contains(localPlayer.EntityId) &&
               ids.Contains(target.EntityId);
    }

    private bool TryGetExactIdentity(
        IPlayerCharacter? player,
        out TargetPressureActorIdentity identity)
    {
        identity = default;
        if (player is null ||
            player.Address == nint.Zero ||
            !IsNetworkObjectId(player.GameObjectId) ||
            !IsNetworkEntityId(player.EntityId))
        {
            return false;
        }

        var native = (GameObject*)player.Address;
        if (native == null || native->EntityId != player.EntityId) return false;
        var tablePlayer = objectTable.SearchByEntityId(player.EntityId) as IPlayerCharacter;
        if (tablePlayer is null ||
            tablePlayer.Address != player.Address ||
            tablePlayer.GameObjectId != player.GameObjectId ||
            tablePlayer.EntityId != player.EntityId)
        {
            return false;
        }

        identity = new TargetPressureActorIdentity(player.GameObjectId, player.EntityId);
        return identity.IsValid;
    }

    private IPlayerCharacter? ResolveExactLocalPlayer(TargetPressureActorIdentity expected)
    {
        var current = objectTable.LocalPlayer;
        return TryGetExactIdentity(current, out var identity) && identity == expected
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

    private static void ReadOwnKardionState(
        IPlayerCharacter player,
        uint localEntityId,
        out bool stateKnown,
        out bool hasOwnKardion)
    {
        stateKnown = IsNetworkEntityId(localEntityId);
        hasOwnKardion = false;
        if (!stateKnown) return;

        var localSourceCount = 0;
        foreach (var status in player.StatusList)
        {
            if (!SmartKardiaRules.IsKardionStatus(status.StatusId)) continue;
            if (!IsNetworkEntityId(status.SourceId))
            {
                stateKnown = false;
                hasOwnKardion = false;
                return;
            }

            if (status.SourceId == localEntityId) localSourceCount++;
        }

        if (localSourceCount > 1)
        {
            stateKnown = false;
            return;
        }

        hasOwnKardion = localSourceCount == 1;
    }

    private static bool IsLivePlayer(IPlayerCharacter? player) =>
        player is not null &&
        !player.IsDead &&
        player.CurrentHp > 0 &&
        player.MaxHp > 0 &&
        player.CurrentHp <= player.MaxHp;

    private static GameObject* GetNativeObject(IPlayerCharacter player)
    {
        var native = (GameObject*)player.Address;
        return native != null && native->EntityId == player.EntityId
            ? native
            : null;
    }

    private static bool IsNetworkEntityId(uint entityId) =>
        entityId is not 0 and not 0xE0000000 and not uint.MaxValue;

    private static bool IsNetworkObjectId(ulong gameObjectId) =>
        gameObjectId is not 0 and not 0xE0000000 and not ulong.MaxValue;

    private static bool TryGetReadyAction(
        IPlayerCharacter localPlayer,
        out uint resolvedActionId)
    {
        resolvedActionId = 0;
        if (!localPlayer.ClassJob.IsValid ||
            localPlayer.ClassJob.RowId != SmartKardiaRules.SageJobId ||
            GetNativeObject(localPlayer) == null)
        {
            return false;
        }

        var actionManager = ActionManager.Instance();
        if (actionManager == null) return false;
        resolvedActionId = actionManager->GetAdjustedActionId(SmartKardiaRules.ActionId);
        return resolvedActionId == SmartKardiaRules.ActionId &&
               actionManager->IsActionOffCooldown(ActionType.Action, resolvedActionId);
    }

    private void LogAttemptFailure(Exception exception, long nowMilliseconds)
    {
        if (nowMilliseconds < nextErrorLogAt) return;
        nextErrorLogAt = nowMilliseconds + 10_000;
        log.Error(
            exception,
            "Seiton Sense Smart Kardia attempt failed and will not be retried.");
    }

    private readonly record struct RuntimeCandidate(
        SmartKardiaCandidate Candidate,
        nint Address);

    private sealed record ExactPartyCapture(
        bool Complete,
        IReadOnlyList<RuntimeCandidate> Members)
    {
        internal static ExactPartyCapture Incomplete { get; } = new(false, []);
    }
}
