using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Party;
using Dalamud.Plugin.Services;
using SeitonSense.Core;

namespace SeitonSense.Plugin.Services;

internal enum CombatLimitBreakRosterSide : byte
{
    Self = 0,
    Ally = 1,
    Enemy = 2,
}

internal readonly record struct CombatLimitBreakActorState(
    TargetPressureActorIdentity Actor,
    CombatLimitBreakRosterSide Side,
    int Slot,
    uint JobId,
    uint ActivationActionId,
    uint IconId,
    string LimitBreakName,
    CombatLimitBreakPresentationKind Presentation,
    bool DurationConfirmed,
    uint EvidenceStatusId,
    string Phase,
    long ActivatedAtMilliseconds,
    long RemainingMilliseconds,
    long ExpiresAtMilliseconds,
    ulong EpisodeToken);

internal readonly record struct CombatLimitBreakDamageEventSnapshot(
    TargetPressureActorIdentity Caster,
    int CasterPartySlot,
    TargetPressureActorIdentity Target,
    int TargetEnemySlot,
    uint CasterJobId,
    uint ActionId,
    uint IconId,
    uint Damage,
    long ObservedAtMilliseconds,
    long ExpiresAtMilliseconds,
    ulong EpisodeToken,
    ulong EventToken);

/// <summary>
/// Immutable, value-only LB presentation state. Actor names and game-object
/// wrappers are deliberately absent; consumers can pair exact identities with
/// their own current frame/roster view.
/// </summary>
internal sealed class CombatLimitBreakRuntimeSnapshot
{
    internal static CombatLimitBreakRuntimeSnapshot Inactive { get; } = new(
        false,
        -1,
        [],
        []);

    internal CombatLimitBreakRuntimeSnapshot(
        bool active,
        long publishedAtMilliseconds,
        IEnumerable<CombatLimitBreakActorState> actors,
        IEnumerable<CombatLimitBreakDamageEventSnapshot> allyDamageEvents)
    {
        ArgumentNullException.ThrowIfNull(actors);
        ArgumentNullException.ThrowIfNull(allyDamageEvents);
        Active = active;
        PublishedAtMilliseconds = publishedAtMilliseconds;
        Actors = Array.AsReadOnly(actors.ToArray());
        AllyDamageEvents = Array.AsReadOnly(allyDamageEvents.ToArray());
    }

    internal bool Active { get; }
    internal long PublishedAtMilliseconds { get; }
    internal IReadOnlyList<CombatLimitBreakActorState> Actors { get; }
    internal IReadOnlyList<CombatLimitBreakDamageEventSnapshot> AllyDamageEvents { get; }

    internal bool TryFindActor(
        TargetPressureActorIdentity actor,
        out CombatLimitBreakActorState state)
    {
        if (actor.IsValid)
        {
            foreach (var candidate in Actors)
            {
                if (candidate.Actor != actor) continue;
                state = candidate;
                return true;
            }
        }

        state = default;
        return false;
    }

    internal bool TryFindEnemySlot(int enemySlot, out CombatLimitBreakActorState state)
    {
        if (enemySlot is >= 1 and <= 5)
        {
            foreach (var candidate in Actors)
            {
                if (candidate.Side != CombatLimitBreakRosterSide.Enemy ||
                    candidate.Slot != enemySlot)
                {
                    continue;
                }

                state = candidate;
                return true;
            }
        }

        state = default;
        return false;
    }

    internal bool TryFindSelf(out CombatLimitBreakActorState state)
    {
        foreach (var candidate in Actors)
        {
            if (candidate.Side != CombatLimitBreakRosterSide.Self) continue;
            state = candidate;
            return true;
        }

        state = default;
        return false;
    }
}

internal sealed record CombatLimitBreakRuntimeDiagnostics(
    bool MetadataVerified,
    int VerifiedActivationActions,
    int ExpectedActivationActions,
    int VerifiedDamageActions,
    int ExpectedDamageActions,
    int VerifiedStatuses,
    int ExpectedStatuses,
    bool Active,
    int FeatureGeneration,
    int ExactRosterActors,
    int ActiveEpisodes,
    int VisibleAllyDamageEvents,
    int ActivationQueueDepth,
    int DamageQueueDepth,
    long CapturedActivations,
    long CapturedDamageEvents,
    long CaptureDroppedActivations,
    long CaptureDroppedDamageEvents,
    long AcceptedActivations,
    long DuplicateActivationEvents,
    long SuppressedActivationEpisodes,
    long RejectedActivations,
    long AcceptedAllyDamageEvents,
    long DuplicateDamageEvents,
    long RejectedDamageEvents)
{
    internal static CombatLimitBreakRuntimeDiagnostics Inactive(
        CombatLimitBreakMetadataValidation metadata) => new(
        metadata.Verified,
        metadata.VerifiedActivationActions,
        metadata.ExpectedActivationActions,
        metadata.VerifiedDamageActions,
        metadata.ExpectedDamageActions,
        metadata.VerifiedStatuses,
        metadata.ExpectedStatuses,
        false,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0);
}

/// <summary>
/// Converts bounded action-effect captures into exact CC-only LB episodes.
/// Activations are never inferred from gauge timing. A duration exists only
/// while a mapped live status is observed on its exact carrier; otherwise the
/// activation remains the catalog's short flash. Follow-up damage is accepted
/// only inside an already active episode and only for exact ally-to-enemy
/// direct-caster attribution.
/// </summary>
internal sealed class CombatLimitBreakRuntimeService : IDisposable
{
    private const long MaximumCaptureAgeMilliseconds = 5_000;
    private const long FutureCaptureToleranceMilliseconds = 250;
    private const long ConfirmedStatusLossGraceMilliseconds = 150;
    private const long AllyDamageEventLifetimeMilliseconds = 3_000;
    private const int MaximumDisplayNameCharacters = 40;
    private const int MaximumActivationKeys = 256;
    private const int MaximumDamageKeys = 1_024;
    private const int MaximumVisibleAllyDamageEvents = 32;

    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly IFramework framework;
    private readonly IPartyList partyList;
    private readonly IPluginLog log;
    private readonly ExecuteTracker executeTracker;
    private readonly CombatLimitBreakCaptureBuffer captureBuffer;
    private readonly CombatLimitBreakMetadataValidation metadata;
    private readonly Func<bool> enabledProvider;
    private readonly Func<bool> damageFeedEnabledProvider;
    private readonly Dictionary<TargetPressureActorIdentity, ActiveEpisode> episodes = [];
    private readonly List<CombatLimitBreakDamageEventSnapshot> allyDamageEvents = [];
    private readonly HashSet<ActivationEventKey> activationKeys = [];
    private readonly Queue<ActivationEventKey> activationKeyOrder = [];
    private readonly HashSet<CombatLimitBreakEventKey> damageKeys = [];
    private readonly Queue<CombatLimitBreakEventKey> damageKeyOrder = [];

    private CombatLimitBreakRuntimeSnapshot snapshot = CombatLimitBreakRuntimeSnapshot.Inactive;
    private CombatLimitBreakRuntimeDiagnostics diagnostics;
    private TargetPressureActorIdentity activeLocalIdentity;
    private uint activeTerritory;
    private ulong nextEpisodeToken;
    private ulong nextDamageEventToken;
    private long acceptedActivations;
    private long duplicateActivationEvents;
    private long suppressedActivationEpisodes;
    private long rejectedActivations;
    private long acceptedAllyDamageEvents;
    private long duplicateDamageEvents;
    private long rejectedDamageEvents;
    private long nextErrorLogAtMilliseconds;
    private bool started;
    private bool disposed;

    internal CombatLimitBreakRuntimeService(
        IClientState clientState,
        IObjectTable objectTable,
        IFramework framework,
        IPartyList partyList,
        IPluginLog log,
        ExecuteTracker executeTracker,
        CombatLimitBreakCaptureBuffer captureBuffer,
        CombatLimitBreakMetadataValidation metadata,
        Func<bool> enabledProvider,
        Func<bool> damageFeedEnabledProvider)
    {
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.framework = framework;
        this.partyList = partyList;
        this.log = log;
        this.executeTracker = executeTracker;
        this.captureBuffer = captureBuffer;
        this.metadata = metadata;
        this.enabledProvider = enabledProvider;
        this.damageFeedEnabledProvider = damageFeedEnabledProvider;
        diagnostics = CombatLimitBreakRuntimeDiagnostics.Inactive(metadata);
    }

    internal CombatLimitBreakRuntimeSnapshot Snapshot => Volatile.Read(ref snapshot);
    internal CombatLimitBreakRuntimeDiagnostics Diagnostics => Volatile.Read(ref diagnostics);

    /// <summary>
    /// Resolves display names only when the renderer asks for one still-live
    /// published event. Names are sanitized, bounded, never placed in capture
    /// records/snapshots/diagnostics, and are discarded when this call returns.
    /// </summary>
    internal bool TryResolveCurrentDamageDisplayNames(
        CombatLimitBreakDamageEventSnapshot damageEvent,
        out string casterName,
        out string targetName)
    {
        casterName = string.Empty;
        targetName = string.Empty;
        var current = Snapshot;
        var now = Environment.TickCount64;
        if (!current.Active ||
            damageEvent.ExpiresAtMilliseconds <= now ||
            !current.AllyDamageEvents.Any(candidate =>
                candidate.EventToken == damageEvent.EventToken && candidate == damageEvent) ||
            damageEvent.CasterPartySlot is < 1 or > 5 ||
            damageEvent.TargetEnemySlot is < 1 or > 5)
        {
            return false;
        }

        var caster = PartySlotResolver.Resolve(objectTable, damageEvent.CasterPartySlot);
        var target = EnemySlotResolver.Resolve(objectTable, damageEvent.TargetEnemySlot);
        if (caster is null ||
            target is null ||
            !caster.IsValid() ||
            !target.IsValid() ||
            CreateIdentity(caster) != damageEvent.Caster ||
            CreateIdentity(target) != damageEvent.Target ||
            !caster.ClassJob.IsValid ||
            caster.ClassJob.RowId != damageEvent.CasterJobId)
        {
            return false;
        }

        casterName = SanitizeDisplayName(caster);
        targetName = SanitizeDisplayName(target);
        return casterName.Length > 0 && targetName.Length > 0;
    }

    internal void Start()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (started) return;
        started = true;
        framework.Update += OnFrameworkUpdate;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (started) framework.Update -= OnFrameworkUpdate;
        captureBuffer.SetEnabled(false);
        ClearPresentationState();
        PublishInactiveDiagnostics();
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        if (disposed) return;
        var now = Environment.TickCount64;
        try
        {
            Update(now);
        }
        catch (Exception exception)
        {
            Deactivate();
            if (now < nextErrorLogAtMilliseconds) return;
            nextErrorLogAtMilliseconds = SaturatingAdd(now, 10_000);
            log.Error(exception, "Seiton Sense LB runtime failed closed.");
        }
    }

    private void Update(long nowMilliseconds)
    {
        var tracker = executeTracker.Diagnostics;
        if (!metadata.Verified ||
            !enabledProvider() ||
            !tracker.Active ||
            !tracker.IsCrystallineConflict ||
            !tracker.IsPvP ||
            !clientState.IsPvP ||
            !clientState.IsPvPExcludingDen ||
            tracker.TerritoryId == 0 ||
            tracker.TerritoryId != clientState.TerritoryType ||
            !TryBuildExactRoster(out var roster))
        {
            Deactivate();
            return;
        }

        var damageFeedEnabled = damageFeedEnabledProvider();
        if (tracker.TerritoryId != activeTerritory ||
            roster.Local.Identity != activeLocalIdentity ||
            !captureBuffer.Enabled)
        {
            captureBuffer.SetEnabled(false);
            ClearPresentationState();
            activeTerritory = tracker.TerritoryId;
            activeLocalIdentity = roster.Local.Identity;
            captureBuffer.SetEnabled(true, damageFeedEnabled);
        }
        else
            captureBuffer.SetEnabled(true, damageFeedEnabled);

        if (damageFeedEnabled)
            RemoveExpiredDamageEvents(nowMilliseconds);
        else
        {
            allyDamageEvents.Clear();
            damageKeys.Clear();
            damageKeyOrder.Clear();
        }
        RefreshEpisodes(roster, nowMilliseconds);
        DrainActivations(roster, nowMilliseconds);
        RefreshEpisodes(roster, nowMilliseconds);
        if (damageFeedEnabled)
            DrainDamageEvents(roster, nowMilliseconds);
        Publish(roster, nowMilliseconds);
    }

    private void DrainActivations(ExactRoster roster, long nowMilliseconds)
    {
        while (captureBuffer.TryDequeueActivation(out var activation))
        {
            if (!IsFreshCapture(activation.ObservedAtMilliseconds, nowMilliseconds) ||
                !CombatLimitBreakCatalog.TryFindByAction(
                    activation.ActionId,
                    out var definition,
                    out var action) ||
                !CombatLimitBreakCatalog.IsActivation(action) ||
                definition.JobId != activation.JobId ||
                CombatLimitBreakCatalog.ResolveIconId(definition, action) != activation.IconId ||
                !roster.ByEntityId.TryGetValue(activation.CasterEntityId, out var caster) ||
                caster.Actor.JobId != definition.JobId ||
                !IsLiveActivationCaster(caster))
            {
                rejectedActivations++;
                continue;
            }

            var key = new ActivationEventKey(
                activation.CasterEntityId,
                activation.ActionId,
                activation.GlobalSequence,
                activation.SourceSequence);
            if (!RememberActivationKey(key))
            {
                duplicateActivationEvents++;
                continue;
            }

            if (episodes.ContainsKey(caster.Actor.Identity))
            {
                suppressedActivationEpisodes++;
                continue;
            }

            var flashEndsAt = SaturatingAdd(
                activation.ObservedAtMilliseconds,
                CombatLimitBreakCatalog.InstantFlashMilliseconds);
            if (definition.Presentation == CombatLimitBreakPresentationKind.Instant &&
                flashEndsAt <= nowMilliseconds)
            {
                rejectedActivations++;
                continue;
            }

            var token = NextNonZero(ref nextEpisodeToken);
            episodes.Add(
                caster.Actor.Identity,
                new ActiveEpisode(
                    caster.Actor,
                    definition,
                    activation.ActionId,
                    activation.IconId,
                    activation.ObservedAtMilliseconds,
                    flashEndsAt,
                    token));
            acceptedActivations++;
        }
    }

    private void RefreshEpisodes(ExactRoster roster, long nowMilliseconds)
    {
        if (episodes.Count == 0) return;
        var observations = BuildStatusObservations(roster);
        foreach (var pair in episodes.ToArray())
        {
            var episode = pair.Value;
            if (!roster.ByIdentity.TryGetValue(pair.Key, out var current) ||
                current.Actor.JobId != episode.Actor.JobId ||
                current.Actor.Side != episode.Actor.Side ||
                current.Actor.Slot != episode.Actor.Slot)
            {
                episodes.Remove(pair.Key);
                continue;
            }

            if (episode.Definition.Presentation == CombatLimitBreakPresentationKind.Instant)
            {
                if (episode.FlashEndsAtMilliseconds <= nowMilliseconds)
                    episodes.Remove(pair.Key);
                continue;
            }

            if (CombatLimitBreakEventRules.TryResolveDuration(
                    episode.Definition,
                    episode.Actor.Identity.EntityId,
                    observations,
                    out var evidence))
            {
                episode.DurationConfirmed = true;
                episode.EvidenceStatusId = evidence.StatusId;
                episode.Phase = evidence.Phase;
                episode.RemainingMilliseconds = evidence.RemainingMilliseconds;
                episode.ExpiresAtMilliseconds = SaturatingAdd(
                    nowMilliseconds,
                    evidence.RemainingMilliseconds);
                episode.MissingStatusSinceMilliseconds = -1;
                continue;
            }

            if (!episode.DurationConfirmed)
            {
                if (episode.FlashEndsAtMilliseconds <= nowMilliseconds)
                    episodes.Remove(pair.Key);
                continue;
            }

            if (nowMilliseconds >= episode.ExpiresAtMilliseconds)
            {
                episodes.Remove(pair.Key);
                continue;
            }

            if (episode.MissingStatusSinceMilliseconds < 0)
                episode.MissingStatusSinceMilliseconds = nowMilliseconds;
            else if (nowMilliseconds - episode.MissingStatusSinceMilliseconds >=
                     ConfirmedStatusLossGraceMilliseconds)
                episodes.Remove(pair.Key);
        }
    }

    private void DrainDamageEvents(ExactRoster roster, long nowMilliseconds)
    {
        while (captureBuffer.TryDequeueDamage(out var damage))
        {
            if (!IsFreshCapture(damage.ObservedAtMilliseconds, nowMilliseconds) ||
                damage.Damage == 0 ||
                !CombatLimitBreakCatalog.TryFindByAction(
                    damage.ActionId,
                    out var definition,
                    out var action) ||
                !CombatLimitBreakCatalog.IsDirectlyAttributableDamage(action) ||
                definition.JobId != damage.JobId ||
                CombatLimitBreakCatalog.ResolveIconId(definition, action) != damage.IconId ||
                !roster.ByEntityId.TryGetValue(damage.CasterEntityId, out var caster) ||
                !roster.ByEntityId.TryGetValue(damage.TargetEntityId, out var target) ||
                caster.Actor.Side != CombatLimitBreakRosterSide.Ally ||
                target.Actor.Side != CombatLimitBreakRosterSide.Enemy ||
                caster.Actor.JobId != definition.JobId ||
                !IsLiveActivationCaster(caster) ||
                !episodes.TryGetValue(caster.Actor.Identity, out var episode) ||
                episode.Definition.JobId != definition.JobId ||
                damage.ObservedAtMilliseconds < episode.ActivatedAtMilliseconds ||
                !ActionBelongsToEpisode(action, damage.ActionId, episode))
            {
                rejectedDamageEvents++;
                continue;
            }

            var key = new CombatLimitBreakEventKey(
                damage.CasterEntityId,
                damage.ActionId,
                damage.TargetEntityId,
                damage.GlobalSequence,
                damage.SourceSequence);
            if (!RememberDamageKey(key))
            {
                duplicateDamageEvents++;
                continue;
            }

            var eventExpiresAt = SaturatingAdd(
                damage.ObservedAtMilliseconds,
                AllyDamageEventLifetimeMilliseconds);
            if (eventExpiresAt <= nowMilliseconds)
            {
                rejectedDamageEvents++;
                continue;
            }

            if (allyDamageEvents.Count >= MaximumVisibleAllyDamageEvents)
                allyDamageEvents.RemoveAt(0);

            allyDamageEvents.Add(new CombatLimitBreakDamageEventSnapshot(
                caster.Actor.Identity,
                caster.Actor.Slot,
                target.Actor.Identity,
                target.Actor.Slot,
                caster.Actor.JobId,
                damage.ActionId,
                damage.IconId,
                damage.Damage,
                damage.ObservedAtMilliseconds,
                eventExpiresAt,
                episode.EpisodeToken,
                NextNonZero(ref nextDamageEventToken)));
            acceptedAllyDamageEvents++;
        }
    }

    private void Publish(ExactRoster roster, long nowMilliseconds)
    {
        var actors = episodes.Values
            .Select(episode =>
            {
                var remaining = episode.DurationConfirmed
                    ? Math.Max(0, episode.ExpiresAtMilliseconds - nowMilliseconds)
                    : Math.Max(0, episode.FlashEndsAtMilliseconds - nowMilliseconds);
                var expiresAt = episode.DurationConfirmed
                    ? episode.ExpiresAtMilliseconds
                    : episode.FlashEndsAtMilliseconds;
                return new CombatLimitBreakActorState(
                    episode.Actor.Identity,
                    episode.Actor.Side,
                    episode.Actor.Slot,
                    episode.Actor.JobId,
                    episode.ActivationActionId,
                    episode.IconId,
                    episode.Definition.Name,
                    episode.Definition.Presentation,
                    episode.DurationConfirmed,
                    episode.EvidenceStatusId,
                    episode.Phase,
                    episode.ActivatedAtMilliseconds,
                    remaining,
                    expiresAt,
                    episode.EpisodeToken);
            })
            .OrderBy(static actor => actor.Side)
            .ThenBy(static actor => actor.Slot)
            .ThenBy(static actor => actor.Actor.EntityId)
            .ToArray();
        var damage = allyDamageEvents
            .OrderBy(static damageEvent => damageEvent.ObservedAtMilliseconds)
            .ThenBy(static damageEvent => damageEvent.EventToken)
            .ToArray();

        Interlocked.Exchange(
            ref snapshot,
            new CombatLimitBreakRuntimeSnapshot(true, nowMilliseconds, actors, damage));
        Interlocked.Exchange(
            ref diagnostics,
            new CombatLimitBreakRuntimeDiagnostics(
                metadata.Verified,
                metadata.VerifiedActivationActions,
                metadata.ExpectedActivationActions,
                metadata.VerifiedDamageActions,
                metadata.ExpectedDamageActions,
                metadata.VerifiedStatuses,
                metadata.ExpectedStatuses,
                true,
                captureBuffer.FeatureGeneration,
                roster.Actors.Count,
                actors.Length,
                damage.Length,
                captureBuffer.ActivationQueueDepth,
                captureBuffer.DamageQueueDepth,
                captureBuffer.CapturedActivations,
                captureBuffer.CapturedDamageEvents,
                captureBuffer.DroppedActivations,
                captureBuffer.DroppedDamageEvents,
                acceptedActivations,
                duplicateActivationEvents,
                suppressedActivationEpisodes,
                rejectedActivations,
                acceptedAllyDamageEvents,
                duplicateDamageEvents,
                rejectedDamageEvents));
    }

    private bool TryBuildExactRoster(out ExactRoster roster)
    {
        roster = default!;
        var localPlayer = objectTable.LocalPlayer;
        if (!TryCreateActor(localPlayer, CombatLimitBreakRosterSide.Self, 0, out var local))
            return false;

        var partyEntityIds = partyList
            .Select(static member => member.EntityId)
            .Where(CombatLimitBreakEventRules.IsNetworkEntityId)
            .ToHashSet();
        if (!partyEntityIds.Contains(local.Actor.Identity.EntityId)) return false;

        // Resolution may be temporarily partial, but every admitted actor is a
        // current exact native P/S slot. Dead rows remain eligible identities;
        // liveness is required separately at the activation event boundary.
        var actors = new List<ResolvedRosterActor>(11) { local };
        for (var slot = 1; slot <= 5; slot++)
        {
            var player = PartySlotResolver.Resolve(objectTable, slot);
            if (player is null) continue;
            var identity = CreateIdentity(player);
            if (identity == local.Actor.Identity) continue;
            if (!partyEntityIds.Contains(player.EntityId) ||
                !TryCreateActor(player, CombatLimitBreakRosterSide.Ally, slot, out var ally) ||
                !TryAddExactActor(actors, ally))
            {
                return false;
            }
        }

        for (var slot = 1; slot <= 5; slot++)
        {
            var player = EnemySlotResolver.Resolve(objectTable, slot);
            if (player is null) continue;
            if (!TryCreateActor(player, CombatLimitBreakRosterSide.Enemy, slot, out var enemy) ||
                !TryAddExactActor(actors, enemy))
            {
                return false;
            }
        }

        roster = new ExactRoster(local.Actor, actors);
        return true;
    }

    private static bool TryCreateActor(
        IPlayerCharacter? player,
        CombatLimitBreakRosterSide side,
        int slot,
        out ResolvedRosterActor actor)
    {
        actor = default!;
        var identity = CreateIdentity(player);
        var jobId = player?.ClassJob.IsValid == true ? player.ClassJob.RowId : 0;
        if (player is null ||
            player.Address == nint.Zero ||
            !identity.IsValid ||
            !CombatLimitBreakCatalog.TryFindByJob(jobId, out _))
        {
            return false;
        }

        actor = new ResolvedRosterActor(
            new ExactRosterActor(identity, side, slot, jobId),
            player);
        return true;
    }

    private static bool TryAddExactActor(
        List<ResolvedRosterActor> actors,
        ResolvedRosterActor candidate)
    {
        foreach (var existing in actors)
        {
            if (SharesEitherId(existing.Actor.Identity, candidate.Actor.Identity))
                return false;
        }

        if (candidate.Actor.Side == CombatLimitBreakRosterSide.Enemy &&
            actors.Any(actor => actor.Actor.Side != CombatLimitBreakRosterSide.Enemy &&
                                actor.Actor.Identity.EntityId == candidate.Actor.Identity.EntityId))
        {
            return false;
        }

        actors.Add(candidate);
        return true;
    }

    private static CombatLimitBreakStatusObservation[] BuildStatusObservations(ExactRoster roster)
    {
        var observations = new List<CombatLimitBreakStatusObservation>(64);
        foreach (var actor in roster.Actors)
        {
            foreach (var status in actor.Player.StatusList)
            {
                observations.Add(new CombatLimitBreakStatusObservation(
                    actor.Actor.Identity.EntityId,
                    status.StatusId,
                    status.SourceId,
                    status.RemainingTime));
            }
        }

        return observations.ToArray();
    }

    private static bool ActionBelongsToEpisode(
        CombatLimitBreakActionBinding action,
        uint actionId,
        ActiveEpisode episode)
    {
        if ((action.Role & CombatLimitBreakActionRole.Activation) != 0)
            return actionId == episode.ActivationActionId;
        return (action.Role & CombatLimitBreakActionRole.FollowUp) != 0;
    }

    private static bool IsLiveActivationCaster(ResolvedRosterActor caster) =>
        caster.Player.IsValid() &&
        caster.Player.Address != nint.Zero &&
        !caster.Player.IsDead &&
        caster.Player.CurrentHp > 0 &&
        caster.Player.IsTargetable &&
        CreateIdentity(caster.Player) == caster.Actor.Identity;

    private bool RememberActivationKey(ActivationEventKey key)
    {
        if (!activationKeys.Add(key)) return false;
        activationKeyOrder.Enqueue(key);
        while (activationKeyOrder.Count > MaximumActivationKeys)
            activationKeys.Remove(activationKeyOrder.Dequeue());
        return true;
    }

    private bool RememberDamageKey(CombatLimitBreakEventKey key)
    {
        if (!damageKeys.Add(key)) return false;
        damageKeyOrder.Enqueue(key);
        while (damageKeyOrder.Count > MaximumDamageKeys)
            damageKeys.Remove(damageKeyOrder.Dequeue());
        return true;
    }

    private void RemoveExpiredDamageEvents(long nowMilliseconds) =>
        allyDamageEvents.RemoveAll(damage => damage.ExpiresAtMilliseconds <= nowMilliseconds);

    private void Deactivate()
    {
        captureBuffer.SetEnabled(false);
        ClearPresentationState();
        activeTerritory = 0;
        activeLocalIdentity = default;
        PublishInactiveDiagnostics();
    }

    private void ClearPresentationState()
    {
        episodes.Clear();
        allyDamageEvents.Clear();
        activationKeys.Clear();
        activationKeyOrder.Clear();
        damageKeys.Clear();
        damageKeyOrder.Clear();
        Interlocked.Exchange(ref snapshot, CombatLimitBreakRuntimeSnapshot.Inactive);
    }

    private void PublishInactiveDiagnostics()
    {
        Interlocked.Exchange(
            ref diagnostics,
            CombatLimitBreakRuntimeDiagnostics.Inactive(metadata) with
            {
                FeatureGeneration = captureBuffer.FeatureGeneration,
                CapturedActivations = captureBuffer.CapturedActivations,
                CapturedDamageEvents = captureBuffer.CapturedDamageEvents,
                CaptureDroppedActivations = captureBuffer.DroppedActivations,
                CaptureDroppedDamageEvents = captureBuffer.DroppedDamageEvents,
                AcceptedActivations = acceptedActivations,
                DuplicateActivationEvents = duplicateActivationEvents,
                SuppressedActivationEpisodes = suppressedActivationEpisodes,
                RejectedActivations = rejectedActivations,
                AcceptedAllyDamageEvents = acceptedAllyDamageEvents,
                DuplicateDamageEvents = duplicateDamageEvents,
                RejectedDamageEvents = rejectedDamageEvents,
            });
    }

    private static bool IsFreshCapture(long observedAtMilliseconds, long nowMilliseconds) =>
        observedAtMilliseconds <= SaturatingAdd(nowMilliseconds, FutureCaptureToleranceMilliseconds) &&
        observedAtMilliseconds >= nowMilliseconds - MaximumCaptureAgeMilliseconds;

    private static TargetPressureActorIdentity CreateIdentity(IPlayerCharacter? player) =>
        player is null
            ? default
            : new TargetPressureActorIdentity(player.GameObjectId, player.EntityId);

    private static string SanitizeDisplayName(IPlayerCharacter player)
    {
        var raw = player.Name.TextValue?.Trim();
        if (string.IsNullOrEmpty(raw)) return string.Empty;
        return new string(raw
                .Where(static character => !char.IsControl(character))
                .Take(MaximumDisplayNameCharacters)
                .ToArray())
            .Trim();
    }

    private static bool SharesEitherId(
        TargetPressureActorIdentity left,
        TargetPressureActorIdentity right) =>
        left.GameObjectId == right.GameObjectId || left.EntityId == right.EntityId;

    private static ulong NextNonZero(ref ulong value)
    {
        value++;
        if (value == 0) value++;
        return value;
    }

    private static long SaturatingAdd(long left, long right) =>
        right > 0 && left > long.MaxValue - right ? long.MaxValue : left + right;

    private readonly record struct ActivationEventKey(
        uint CasterEntityId,
        uint ActionId,
        uint GlobalSequence,
        ushort SourceSequence);

    private readonly record struct ExactRosterActor(
        TargetPressureActorIdentity Identity,
        CombatLimitBreakRosterSide Side,
        int Slot,
        uint JobId);

    private sealed record ResolvedRosterActor(
        ExactRosterActor Actor,
        IPlayerCharacter Player);

    private sealed class ExactRoster
    {
        internal ExactRoster(ExactRosterActor local, IEnumerable<ResolvedRosterActor> actors)
        {
            Local = local;
            Actors = Array.AsReadOnly(actors.ToArray());
            ByIdentity = Actors.ToDictionary(static actor => actor.Actor.Identity);
            ByEntityId = Actors.ToDictionary(static actor => actor.Actor.Identity.EntityId);
        }

        internal ExactRosterActor Local { get; }
        internal IReadOnlyList<ResolvedRosterActor> Actors { get; }
        internal IReadOnlyDictionary<TargetPressureActorIdentity, ResolvedRosterActor> ByIdentity { get; }
        internal IReadOnlyDictionary<uint, ResolvedRosterActor> ByEntityId { get; }
    }

    private sealed class ActiveEpisode
    {
        internal ActiveEpisode(
            ExactRosterActor actor,
            CombatLimitBreakDefinition definition,
            uint activationActionId,
            uint iconId,
            long activatedAtMilliseconds,
            long flashEndsAtMilliseconds,
            ulong episodeToken)
        {
            Actor = actor;
            Definition = definition;
            ActivationActionId = activationActionId;
            IconId = iconId;
            ActivatedAtMilliseconds = activatedAtMilliseconds;
            FlashEndsAtMilliseconds = flashEndsAtMilliseconds;
            ExpiresAtMilliseconds = flashEndsAtMilliseconds;
            EpisodeToken = episodeToken;
        }

        internal ExactRosterActor Actor { get; }
        internal CombatLimitBreakDefinition Definition { get; }
        internal uint ActivationActionId { get; }
        internal uint IconId { get; }
        internal long ActivatedAtMilliseconds { get; }
        internal long FlashEndsAtMilliseconds { get; }
        internal ulong EpisodeToken { get; }
        internal bool DurationConfirmed { get; set; }
        internal uint EvidenceStatusId { get; set; }
        internal string Phase { get; set; } = string.Empty;
        internal long RemainingMilliseconds { get; set; }
        internal long ExpiresAtMilliseconds { get; set; }
        internal long MissingStatusSinceMilliseconds { get; set; } = -1;
    }
}
