using System.Numerics;
using Dalamud.Game;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Lumina.Excel.Sheets;
using SeitonSense.Core;
using GameAction = Lumina.Excel.Sheets.Action;

namespace SeitonSense.Plugin.Services;

internal sealed record AllyRescueProbeSnapshot(
    AllyRescueBufferPhase Phase,
    AllyRescueIntent? Intent,
    AllyRescueBufferDecisionKind Decision,
    AllyRescueBufferCancelReason CancelReason,
    AllyRescueInputTrigger InputTrigger,
    long BufferRemainingMilliseconds,
    int CandidateCount,
    uint ActionId,
    ulong TargetGameObjectId,
    uint TargetStatusId,
    VirtualKey FreshGameplayKey,
    VirtualKey HeldGameplayKey,
    bool LocallyReady,
    bool UseActionAttempted,
    bool UseActionAccepted,
    long AttemptCount,
    long AcceptedCount,
    string LastEvent)
{
    internal static AllyRescueProbeSnapshot Initial { get; } = new(
        AllyRescueBufferPhase.WaitingForCandidate,
        null,
        AllyRescueBufferDecisionKind.None,
        AllyRescueBufferCancelReason.None,
        AllyRescueInputTrigger.None,
        0,
        0,
        0,
        0,
        0,
        VirtualKey.NO_KEY,
        VirtualKey.NO_KEY,
        false,
        false,
        false,
        0,
        0,
        "Not started");
}

/// <summary>
/// Optional CC-only next-key rescue for an exact non-self party member. It uses
/// Warden's Paean on BRD and Aquaveil on WHM, and recognizes only PvP Stun,
/// Silence, Miracle of Nature, and Deep Freeze.
/// </summary>
internal sealed class AllyRescueProbe
{
    internal const uint WardensPaeanActionId = 29400;
    internal const uint AquaveilActionId = 29227;
    internal const uint WardensPaeanIconId = 9628;
    internal const uint AquaveilIconId = 9607;

    private const uint BardJobId = 23;
    private const uint WhiteMageJobId = 24;
    private const int ExpectedRange = 30;
    private const ushort WardensPaeanRecast100ms = 240;
    private const ushort AquaveilRecast100ms = 180;
    private const long StatusRefreshToleranceMilliseconds = 250;

    private readonly IObjectTable objectTable;
    private readonly TargetPressureTracker pressureTracker;
    private readonly NearAssistRedirector nearAssist;
    private readonly IPluginLog log;
    private readonly bool wardensPaeanMetadataVerified;
    private readonly bool aquaveilMetadataVerified;
    private readonly Dictionary<ObservedAllyStatusKey, AllyStatusIdentityState> statusInstances = [];
    private readonly HashSet<AllyActorIdentity> trustedMpActors = [];
    private AllyRescueBufferState state = AllyRescueBufferState.Initial;
    private AllyRescueProbeSnapshot snapshot = AllyRescueProbeSnapshot.Initial;
    private ulong nextInstanceToken = 1;
    private long attemptCount;
    private long acceptedCount;
    private long nextErrorLogAt;

    internal AllyRescueProbe(
        IObjectTable objectTable,
        IDataManager dataManager,
        TargetPressureTracker pressureTracker,
        NearAssistRedirector nearAssist,
        IPluginLog log)
    {
        this.objectTable = objectTable;
        this.pressureTracker = pressureTracker;
        this.nearAssist = nearAssist;
        this.log = log;

        wardensPaeanMetadataVerified = ValidateRescueActionMetadata(
            dataManager,
            WardensPaeanActionId,
            "The Warden's Paean",
            WardensPaeanIconId,
            BardJobId,
            WardensPaeanRecast100ms,
            "Removes one status affliction");
        aquaveilMetadataVerified = ValidateRescueActionMetadata(
            dataManager,
            AquaveilActionId,
            "Aquaveil",
            AquaveilIconId,
            WhiteMageJobId,
            AquaveilRecast100ms,
            "Nullifies one status affliction");

        if (!wardensPaeanMetadataVerified || !aquaveilMetadataVerified)
        {
            log.Warning(
                "Seiton Sense Ally Rescue metadata validation failed: Paean={Paean}, Aquaveil={Aquaveil}. " +
                "The mismatched job action will remain unavailable.",
                wardensPaeanMetadataVerified,
                aquaveilMetadataVerified);
        }
    }

    internal AllyRescueProbeSnapshot Snapshot => Volatile.Read(ref snapshot);

    internal unsafe AllyRescueProbeSnapshot Observe(
        IPlayerCharacter? localPlayer,
        bool isCrystallineConflict,
        bool configurationEnabled,
        bool allowHeldKeyAtCandidateEntry,
        EmergencyActionInputFrame inputFrame,
        long nowMilliseconds,
        long bufferMilliseconds,
        bool hardReset = false)
    {
        if (hardReset) ResetRuntime();

        var localAlive = IsLivePlayer(localPlayer);
        var localIdentityValid = localAlive && HasValidNativeIdentity(localPlayer!);
        var actionId = localIdentityValid ? ResolveActionId(localPlayer!) : 0;
        var actionManager = ActionManager.Instance();
        var locallyReady = configurationEnabled &&
                           isCrystallineConflict &&
                           localIdentityValid &&
                           actionId != 0 &&
                           actionManager != null &&
                           actionManager->IsActionOffCooldown(ActionType.Action, actionId);
        var candidates = configurationEnabled &&
                         isCrystallineConflict &&
                         localIdentityValid &&
                         actionId != 0
            ? BuildCandidates(localPlayer!, actionId, nowMilliseconds)
            : [];

        var input = inputFrame.Snapshot;
        var rescueFreshKey = inputFrame.FreshGameplayKeyPressed
            ? input.FreshGameplayKey
            : VirtualKey.NO_KEY;
        var rescueHeldKey = inputFrame.HeldGameplayKeyEligible
            ? input.HeldGameplayKey
            : VirtualKey.NO_KEY;
        var decision = AllyRescueBufferRules.Observe(
            state,
            new AllyRescueBufferObservation(
                configurationEnabled,
                isCrystallineConflict,
                localAlive,
                localIdentityValid,
                input.IsTextInputActive,
                candidates,
                inputFrame.FreshGameplayKeyPressed,
                inputFrame.HeldGameplayKeyEligible,
                allowHeldKeyAtCandidateEntry,
                locallyReady,
                nowMilliseconds,
                hardReset,
                bufferMilliseconds));

        // Commit and consume before validation or native dispatch. A false return,
        // exception, or last-moment actor/status loss can therefore never retry.
        state = decision.NextState;
        if (decision.ShouldConsumeInputGeneration) inputFrame.Consume();

        var attempted = false;
        var accepted = false;
        var targetGameObjectId = 0UL;
        var targetStatusId = 0U;
        var lastEvent = DescribeDecision(decision, actionId, candidates.Count);
        if (decision.ShouldDispatch &&
            decision.DispatchIntent is { } dispatchIntent)
        {
            targetGameObjectId = dispatchIntent.GameObjectId;
            targetStatusId = dispatchIntent.Status.StatusId;
            if (TryRevalidateCandidate(
                    localPlayer!,
                    actionId,
                    dispatchIntent,
                    nowMilliseconds,
                    out var revalidated))
            {
                try
                {
                    accepted = TryUseRescueOnce(
                        actionId,
                        revalidated.GameObjectId,
                        out attempted);
                    if (attempted) Interlocked.Increment(ref attemptCount);
                    if (accepted) Interlocked.Increment(ref acceptedCount);
                    lastEvent = accepted
                        ? $"Accepted action={actionId} target={revalidated.GameObjectId:X} status={targetStatusId}"
                        : $"Attempt rejected action={actionId} target={revalidated.GameObjectId:X} status={targetStatusId}";
                }
                catch (Exception exception)
                {
                    if (attempted) Interlocked.Increment(ref attemptCount);
                    lastEvent = $"Attempt threw action={actionId} target={revalidated.GameObjectId:X} status={targetStatusId}";
                    LogAttemptFailure(exception, nowMilliseconds);
                }
            }
            else
            {
                lastEvent =
                    $"Consumed without action: target/status/range changed for {dispatchIntent.GameObjectId:X}/{targetStatusId}";
            }
        }

        var remaining = state.Phase == AllyRescueBufferPhase.Buffered
            ? Math.Max(0, state.ExpiresAtMilliseconds - nowMilliseconds)
            : 0;
        var result = new AllyRescueProbeSnapshot(
            state.Phase,
            state.TrackedIntent,
            decision.Kind,
            decision.CancelReason,
            decision.InputTrigger,
            remaining,
            candidates.Count,
            actionId,
            targetGameObjectId,
            targetStatusId,
            rescueFreshKey,
            rescueHeldKey,
            locallyReady,
            attempted,
            accepted,
            Interlocked.Read(ref attemptCount),
            Interlocked.Read(ref acceptedCount),
            lastEvent);
        Volatile.Write(ref snapshot, result);
        return result;
    }

    internal void Reset()
    {
        ResetRuntime();
        Volatile.Write(ref snapshot, AllyRescueProbeSnapshot.Initial);
    }

    internal AllyRescueProbeSnapshot FailClosed(long nowMilliseconds, Exception? exception = null)
    {
        ResetRuntime();
        if (exception is not null) LogAttemptFailure(exception, nowMilliseconds);
        var failed = AllyRescueProbeSnapshot.Initial with
        {
            Decision = AllyRescueBufferDecisionKind.Cancelled,
            CancelReason = AllyRescueBufferCancelReason.HardReset,
            AttemptCount = Interlocked.Read(ref attemptCount),
            AcceptedCount = Interlocked.Read(ref acceptedCount),
            LastEvent = "Failed closed",
        };
        Volatile.Write(ref snapshot, failed);
        return failed;
    }

    private unsafe List<AllyRescueSelectionCandidate> BuildCandidates(
        IPlayerCharacter localPlayer,
        uint actionId,
        long nowMilliseconds)
    {
        var exactParty = ResolveExactPartyMembers();
        var observedKeys = new HashSet<ObservedAllyStatusKey>();
        var candidates = new List<AllyRescueSelectionCandidate>(8);
        foreach (var (slot, ally) in exactParty)
        {
            if (ally.GameObjectId == localPlayer.GameObjectId || ally.EntityId == localPlayer.EntityId)
                continue;

            foreach (var status in ally.StatusList)
            {
                if (!AllyRescueStatusRules.IsTriggerStatus(status.StatusId) ||
                    status.Address == 0 ||
                    !float.IsFinite(status.RemainingTime) ||
                    status.RemainingTime <= 0f)
                {
                    continue;
                }

                var key = new ObservedAllyStatusKey(
                    ally.GameObjectId,
                    ally.EntityId,
                    status.StatusId,
                    status.SourceId);
                observedKeys.Add(key);
                var remainingMilliseconds = Math.Max(
                    1,
                    (long)Math.Round(Math.Min(status.RemainingTime, 3_600f) * 1000f));
                var token = ObserveStatusInstance(key, remainingMilliseconds, nowMilliseconds);
                candidates.Add(BuildCandidate(
                    localPlayer,
                    ally,
                    slot,
                    new AllyRescueStatusInstance(status.StatusId, token),
                    actionId));
            }
        }

        PruneStatusInstances(observedKeys, nowMilliseconds);
        PruneTrustedMpActors(exactParty.Select(static item => item.Player));
        return candidates;
    }

    private unsafe AllyRescueSelectionCandidate BuildCandidate(
        IPlayerCharacter localPlayer,
        IPlayerCharacter ally,
        int partySlot,
        AllyRescueStatusInstance status,
        uint actionId)
    {
        var sourceObject = GetNativeObject(localPlayer);
        var targetObject = GetNativeObject(ally);
        var validActionTarget = sourceObject != null && targetObject != null;
        var rangeResult = validActionTarget
            ? ActionManager.GetActionInRangeOrLoS(actionId, sourceObject, targetObject)
            : uint.MaxValue;
        var plausibleMp = ally.MaxMp > 0 && ally.CurrentMp <= ally.MaxMp;
        var actorIdentity = new AllyActorIdentity(ally.GameObjectId, ally.EntityId);
        if (plausibleMp && ally.CurrentMp > 0) trustedMpActors.Add(actorIdentity);
        var hasTrustedMp = plausibleMp &&
                           (ally.CurrentMp > 0 || trustedMpActors.Contains(actorIdentity));

        return new AllyRescueSelectionCandidate(
            ally.GameObjectId,
            ally.EntityId,
            partySlot,
            status,
            ally.CurrentHp,
            ally.MaxHp,
            CountDirectIncomingPressure(ally),
            ally.CurrentMp,
            ally.MaxMp,
            hasTrustedMp,
            Vector3.DistanceSquared(localPlayer.Position, ally.Position),
            IsExactPartyMember: true,
            IsSelf: false,
            IsAlive: !ally.IsDead && ally.CurrentHp > 0,
            ally.IsTargetable,
            validActionTarget,
            SeitonRangeRules.HasNativeRangeAndLineOfSight(rangeResult));
    }

    private unsafe bool TryRevalidateCandidate(
        IPlayerCharacter localPlayer,
        uint actionId,
        AllyRescueIntent intent,
        long nowMilliseconds,
        out AllyRescueSelectionCandidate candidate)
    {
        candidate = default;
        var actionManager = ActionManager.Instance();
        if (!IsLivePlayer(localPlayer) ||
            ResolveActionId(localPlayer) != actionId ||
            actionManager == null ||
            !actionManager->IsActionOffCooldown(ActionType.Action, actionId))
        {
            return false;
        }

        foreach (var (slot, ally) in ResolveExactPartyMembers())
        {
            if (ally.GameObjectId != intent.GameObjectId ||
                ally.EntityId != intent.EntityId ||
                ally.GameObjectId == localPlayer.GameObjectId ||
                ally.EntityId == localPlayer.EntityId)
            {
                continue;
            }

            foreach (var status in ally.StatusList)
            {
                if (status.StatusId != intent.Status.StatusId ||
                    status.Address == 0 ||
                    !float.IsFinite(status.RemainingTime) ||
                    status.RemainingTime <= 0f)
                {
                    continue;
                }

                var key = new ObservedAllyStatusKey(
                    ally.GameObjectId,
                    ally.EntityId,
                    status.StatusId,
                    status.SourceId);
                if (!statusInstances.TryGetValue(key, out var identity) ||
                    identity.Token != intent.Status.InstanceToken ||
                    nowMilliseconds < identity.LastSeenAtMilliseconds)
                {
                    continue;
                }

                candidate = BuildCandidate(localPlayer, ally, slot, intent.Status, actionId);
                return AllyRescueSelectionRules.IsEligible(candidate) &&
                       candidate.Intent == intent;
            }
        }

        return false;
    }

    private IReadOnlyList<(int Slot, IPlayerCharacter Player)> ResolveExactPartyMembers()
    {
        var resolved = new List<(int Slot, IPlayerCharacter Player)>(8);
        for (var slot = AllyRescueSelectionRules.FirstPartySlot;
             slot <= AllyRescueSelectionRules.LastPartySlot;
             slot++)
        {
            var player = PartySlotResolver.Resolve(objectTable, slot);
            if (HasValidNativeIdentity(player)) resolved.Add((slot, player!));
        }

        // An actor exposed through more than one native slot is ambiguous. Exclude
        // every duplicate rather than choosing whichever slot happened to scan first.
        var duplicateGameIds = resolved
            .GroupBy(static item => item.Player.GameObjectId)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToHashSet();
        var duplicateEntityIds = resolved
            .GroupBy(static item => item.Player.EntityId)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToHashSet();
        return resolved
            .Where(item =>
                !duplicateGameIds.Contains(item.Player.GameObjectId) &&
                !duplicateEntityIds.Contains(item.Player.EntityId))
            .ToArray();
    }

    private int? CountDirectIncomingPressure(IPlayerCharacter ally) =>
        pressureTracker.TryGetIncomingAllyPressure(
            ally.GameObjectId,
            ally.EntityId,
            out var uniqueEnemyCount)
            ? uniqueEnemyCount
            : null;

    private unsafe bool TryUseRescueOnce(
        uint actionId,
        ulong targetGameObjectId,
        out bool attempted)
    {
        attempted = false;
        if (actionId is not (WardensPaeanActionId or AquaveilActionId) ||
            targetGameObjectId is 0 or 0xE0000000)
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

    private ulong ObserveStatusInstance(
        ObservedAllyStatusKey key,
        long remainingMilliseconds,
        long nowMilliseconds)
    {
        if (!statusInstances.TryGetValue(key, out var current) ||
            remainingMilliseconds > current.RemainingMilliseconds +
            StatusRefreshToleranceMilliseconds)
        {
            current = new AllyStatusIdentityState(
                NextInstanceToken(),
                remainingMilliseconds,
                nowMilliseconds);
        }
        else
        {
            current = current with
            {
                RemainingMilliseconds = remainingMilliseconds,
                LastSeenAtMilliseconds = nowMilliseconds,
            };
        }

        statusInstances[key] = current;
        return current.Token;
    }

    private void PruneStatusInstances(
        IReadOnlySet<ObservedAllyStatusKey> observed,
        long nowMilliseconds)
    {
        foreach (var stale in statusInstances
                     .Where(pair =>
                         !observed.Contains(pair.Key) &&
                         (nowMilliseconds < pair.Value.LastSeenAtMilliseconds ||
                          nowMilliseconds - pair.Value.LastSeenAtMilliseconds >=
                          PersonalDebuffAlertRules.MissingGraceMilliseconds))
                     .Select(static pair => pair.Key)
                     .ToArray())
        {
            statusInstances.Remove(stale);
        }
    }

    private void PruneTrustedMpActors(IEnumerable<IPlayerCharacter> exactParty)
    {
        var live = exactParty
            .Select(static player => new AllyActorIdentity(player.GameObjectId, player.EntityId))
            .ToHashSet();
        trustedMpActors.RemoveWhere(identity => !live.Contains(identity));
    }

    private uint ResolveActionId(IPlayerCharacter localPlayer)
    {
        if (!localPlayer.ClassJob.IsValid) return 0;
        return localPlayer.ClassJob.RowId switch
        {
            BardJobId when wardensPaeanMetadataVerified => WardensPaeanActionId,
            WhiteMageJobId when aquaveilMetadataVerified => AquaveilActionId,
            _ => 0,
        };
    }

    private bool ValidateRescueActionMetadata(
        IDataManager dataManager,
        uint actionId,
        string expectedName,
        uint expectedIconId,
        uint expectedJobId,
        ushort expectedRecast100ms,
        string expectedCleanseVerb)
    {
        try
        {
            var actions = dataManager.GetExcelSheet<GameAction>(ClientLanguage.English);
            var descriptions = dataManager.GetExcelSheet<ActionTransient>(ClientLanguage.English);
            return actions.TryGetRow(actionId, out var action) &&
                   descriptions.TryGetRow(actionId, out var transient) &&
                   IsExpectedFriendlyRescueAction(
                       action,
                       transient,
                       expectedName,
                       expectedIconId,
                       expectedJobId,
                       expectedRecast100ms,
                       expectedCleanseVerb);
        }
        catch (Exception exception)
        {
            log.Warning(
                exception,
                "Seiton Sense Ally Rescue metadata lookup failed closed for action {ActionId} ({ActionName}).",
                actionId,
                expectedName);
            return false;
        }
    }

    private static bool IsExpectedFriendlyRescueAction(
        GameAction action,
        ActionTransient transient,
        string expectedName,
        uint expectedIconId,
        uint expectedJobId,
        ushort expectedRecast100ms,
        string expectedCleanseVerb) =>
        action.Name.ToString() == expectedName &&
        action.Icon == expectedIconId &&
        action.IsPvP &&
        action.IsPlayerAction &&
        action.ClassJob.IsValid &&
        action.ClassJob.RowId == expectedJobId &&
        action.Range == ExpectedRange &&
        action.EffectRange == 0 &&
        action.Cast100ms == 0 &&
        action.Recast100ms == expectedRecast100ms &&
        action.CanTargetSelf &&
        action.CanTargetParty &&
        !action.CanTargetAlly &&
        !action.CanTargetAlliance &&
        !action.CanTargetHostile &&
        !action.TargetArea &&
        action.RequiresLineOfSight &&
        transient.Description.ToString().Contains(expectedCleanseVerb, StringComparison.Ordinal) &&
        transient.Description.ToString().Contains(
            "status affliction that can be removed by Purify",
            StringComparison.Ordinal);

    private static bool IsLivePlayer(IPlayerCharacter? player) =>
        HasValidNativeIdentity(player) &&
        player!.IsTargetable &&
        !player.IsDead &&
        player.CurrentHp > 0 &&
        player.MaxHp >= player.CurrentHp;

    private static unsafe bool HasValidNativeIdentity(IPlayerCharacter? player)
    {
        if (player is null ||
            player.Address == 0 ||
            !AllyRescueSelectionRules.IsValidEntityId(player.EntityId) ||
            !TargetHighlightRules.IsValidGameObjectId(player.GameObjectId))
        {
            return false;
        }

        var native = (GameObject*)player.Address;
        return native != null && native->EntityId == player.EntityId;
    }

    private static unsafe GameObject* GetNativeObject(IPlayerCharacter player)
    {
        var native = (GameObject*)player.Address;
        return native != null && native->EntityId == player.EntityId ? native : null;
    }

    private static string DescribeDecision(
        AllyRescueBufferDecision decision,
        uint actionId,
        int candidateCount) =>
        decision.Kind == AllyRescueBufferDecisionKind.Cancelled
            ? $"{decision.Kind}/{decision.CancelReason}, action={actionId}, candidates={candidateCount}"
            : $"{decision.Kind}, action={actionId}, candidates={candidateCount}";

    private ulong NextInstanceToken()
    {
        var token = nextInstanceToken++;
        if (token != 0) return token;
        token = nextInstanceToken++;
        return token == 0 ? 1 : token;
    }

    private void ResetRuntime()
    {
        state = AllyRescueBufferState.Initial;
        statusInstances.Clear();
        trustedMpActors.Clear();
    }

    private void LogAttemptFailure(Exception exception, long nowMilliseconds)
    {
        if (nowMilliseconds < nextErrorLogAt) return;
        nextErrorLogAt = nowMilliseconds + 10_000;
        log.Error(exception, "Seiton Sense Ally Rescue attempt failed and will not be retried.");
    }

    private readonly record struct ObservedAllyStatusKey(
        ulong GameObjectId,
        uint EntityId,
        uint StatusId,
        uint SourceId);

    private readonly record struct AllyStatusIdentityState(
        ulong Token,
        long RemainingMilliseconds,
        long LastSeenAtMilliseconds);

    private readonly record struct AllyActorIdentity(ulong GameObjectId, uint EntityId);
}
