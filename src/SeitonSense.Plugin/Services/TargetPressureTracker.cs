using System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Party;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using SeitonSense.Core;
using SeitonSense.Plugin.Models;
using CorePressureSnapshot = SeitonSense.Core.TargetPressureSnapshot;

namespace SeitonSense.Plugin.Services;

internal sealed class TargetPressureTracker : IDisposable
{
    private const long UpdateIntervalMilliseconds = 100;
    private const long ProtectionMissingGraceMilliseconds = 150;
    private const float ProtectionRefreshThresholdSeconds = 0.25f;
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly IFramework framework;
    private readonly IPartyList partyList;
    private readonly IDutyState dutyState;
    private readonly IPluginLog log;
    private readonly PluginConfiguration configuration;
    private readonly PvPMetadataValidation metadata;
    private readonly MachinistLimitBreakCapture capture;
    private readonly ExecuteTracker executeTracker;
    private readonly IReadOnlySet<uint> verifiedProtectionStatusIds;
    private readonly Dictionary<TargetPressureActorIdentity, RecentPressureState> recentPressure = [];
    private readonly Dictionary<TargetPressureActorIdentity, ProtectionActorState> protectionStates = [];
    private readonly AggressorArrowTracker aggressorArrows = new();
    private TargetPressureRuntimeSnapshot snapshot = TargetPressureRuntimeSnapshot.Inactive;
    private IncomingAllyPressureRuntimeState incomingAllyPressure = IncomingAllyPressureRuntimeState.Inactive;
    private TargetPressureDiagnostics diagnostics = TargetPressureDiagnostics.Inactive;
    private long nextUpdateAt;
    private long nextErrorLogAt;
    private long requestedIncomingAllyPressureAt = -1;
    private uint activeTerritory;
    private TargetPressureActorIdentity activeLocalIdentity;
    private bool started;
    private bool disposed;

    internal TargetPressureTracker(
        IClientState clientState,
        IObjectTable objectTable,
        IFramework framework,
        IPartyList partyList,
        IDutyState dutyState,
        IDataManager dataManager,
        IPluginLog log,
        PluginConfiguration configuration,
        PvPMetadataValidation metadata,
        MachinistLimitBreakCapture capture,
        ExecuteTracker executeTracker)
    {
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.framework = framework;
        this.partyList = partyList;
        this.dutyState = dutyState;
        this.log = log;
        this.configuration = configuration;
        this.metadata = metadata;
        this.capture = capture;
        this.executeTracker = executeTracker;
        verifiedProtectionStatusIds = CcProtectionMetadataGuard.Validate(dataManager, log);
    }

    internal TargetPressureRuntimeSnapshot Snapshot => Volatile.Read(ref snapshot);
    internal bool IsActive => Snapshot.Active;
    internal bool HasActiveIncomingAllyPressureView =>
        Volatile.Read(ref incomingAllyPressure).Active;
    internal TargetPressureDiagnostics Diagnostics => Volatile.Read(ref diagnostics);
    internal int VerifiedProtectionStatusCount => verifiedProtectionStatusIds.Count;

    /// <summary>
    /// Captures one immutable published ally-pressure view. Selection code must
    /// rank every party member from this same view instead of combining values
    /// from separate framework publications.
    /// </summary>
    internal bool TryCaptureIncomingAllyPressure(
        out IReadOnlyDictionary<TargetPressureActorIdentity, int> counts)
    {
        return TryCaptureIncomingAllyPressure(
            out counts,
            out _);
    }

    internal bool TryCaptureIncomingAllyPressure(
        out IReadOnlyDictionary<TargetPressureActorIdentity, int> counts,
        out long publishedAtMilliseconds)
    {
        var current = Volatile.Read(ref incomingAllyPressure);
        counts = current.Counts;
        publishedAtMilliseconds = current.PublishedAtMilliseconds;
        return current.Active;
    }

    /// <summary>
    /// Requests one fresh ally-pressure publication for an accepted Eukrasia
    /// opportunity. This avoids keeping the full party pressure scan alive while
    /// Smart Kardia is merely enabled but idle.
    /// </summary>
    internal void RequestIncomingAllyPressureCapture(long acceptedAtMilliseconds)
    {
        if (acceptedAtMilliseconds < 0) return;
        Interlocked.Exchange(
            ref requestedIncomingAllyPressureAt,
            acceptedAtMilliseconds);
        Volatile.Write(ref nextUpdateAt, 0);
    }

    internal void CancelIncomingAllyPressureCapture(long acceptedAtMilliseconds)
    {
        if (acceptedAtMilliseconds < 0) return;
        Interlocked.CompareExchange(
            ref requestedIncomingAllyPressureAt,
            -1,
            acceptedAtMilliseconds);
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
        capture.SetPressureLocalEntityId(0);
        capture.ClearPressureEvents();
        ResetRuntime();
    }

    internal int GetTeamTargetCount(ulong gameObjectId, uint entityId)
    {
        var opponent = Snapshot.Find(gameObjectId, entityId);
        return opponent is not null
            ? opponent.TeamTargetCount
            : 0;
    }

    /// <summary>
    /// Returns one exact enemy's total team hard-target count only from the current
    /// local player's active, bounded-age pressure publication. False is
    /// unknown and must never be treated as a synthetic zero or stale focus.
    /// </summary>
    internal bool TryGetFreshTeamTargetCount(
        TargetPressureActorIdentity expectedLocalPlayer,
        TargetPressureActorIdentity expectedEnemy,
        long nowMilliseconds,
        long maximumAgeMilliseconds,
        out int teamTargetCount)
    {
        teamTargetCount = 0;
        var current = Snapshot;
        if (!expectedLocalPlayer.IsValid ||
            !expectedEnemy.IsValid ||
            !current.Active ||
            !current.PressureActive ||
            current.LocalPlayer != expectedLocalPlayer ||
            current.PublishedAtMilliseconds < 0 ||
            nowMilliseconds < current.PublishedAtMilliseconds ||
            maximumAgeMilliseconds < 0 ||
            nowMilliseconds - current.PublishedAtMilliseconds > maximumAgeMilliseconds)
        {
            return false;
        }

        var opponent = current.Find(expectedEnemy.GameObjectId, expectedEnemy.EntityId);
        if (opponent is null || opponent.TotalTeamTargetCount < 0) return false;
        teamTargetCount = opponent.TotalTeamTargetCount;
        return true;
    }

    /// <summary>
    /// Returns the existing HOWMANY union for the exact local player. False is
    /// unknown/inactive and must never be treated as a synthetic zero.
    /// </summary>
    internal bool TryGetSelfIncomingPressure(out int uniqueEnemyCount)
    {
        var current = Snapshot;
        if (current.Active && current.PressureActive)
        {
            uniqueEnemyCount = current.IncomingOpponents.Count;
            return true;
        }

        uniqueEnemyCount = 0;
        return false;
    }

    /// <summary>
    /// Captures current direct targeting of one exact local-player identity.
    /// Recent harmful actions and early MCH markers are deliberately excluded.
    /// Unknown, future-dated, or stale publications fail closed.
    /// </summary>
    internal bool TryGetFreshSelfDirectIncomingPressure(
        TargetPressureActorIdentity expectedLocalPlayer,
        long nowMilliseconds,
        long maximumAgeMilliseconds,
        out DirectSelfPressureSnapshot directPressure)
    {
        directPressure = default;
        var current = Snapshot;
        if (!expectedLocalPlayer.IsValid ||
            !current.Active ||
            !current.PressureActive ||
            current.LocalPlayer != expectedLocalPlayer ||
            current.PublishedAtMilliseconds < 0 ||
            nowMilliseconds < current.PublishedAtMilliseconds ||
            maximumAgeMilliseconds < 0 ||
            nowMilliseconds - current.PublishedAtMilliseconds > maximumAgeMilliseconds)
        {
            return false;
        }

        var unique = 0;
        var hard = 0;
        var cast = 0;
        foreach (var opponent in current.Opponents)
        {
            var hardTarget =
                (opponent.IncomingEvidence & TargetPressureEvidence.HardTarget) != 0;
            var castTarget =
                (opponent.IncomingEvidence & TargetPressureEvidence.CastTarget) != 0;
            if (!hardTarget && !castTarget) continue;
            unique++;
            if (hardTarget) hard++;
            if (castTarget) cast++;
        }

        directPressure = new DirectSelfPressureSnapshot(
            expectedLocalPlayer,
            current.PublishedAtMilliseconds,
            unique,
            hard,
            cast);
        return true;
    }

    /// <summary>
    /// Returns current incoming intent for one exact party ally. False means
    /// pressure tracking is inactive or that exact identity is absent; it must
    /// not be treated as a synthetic zero-pressure observation.
    /// </summary>
    internal bool TryGetIncomingAllyPressure(
        ulong gameObjectId,
        uint entityId,
        out int uniqueEnemyCount)
    {
        var identity = new TargetPressureActorIdentity(gameObjectId, entityId);
        var current = Volatile.Read(ref incomingAllyPressure);
        if (current.Active &&
            identity.IsValid &&
            current.Counts.TryGetValue(identity, out uniqueEnemyCount))
        {
            return true;
        }

        uniqueEnemyCount = 0;
        return false;
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        if (disposed || Environment.TickCount64 < nextUpdateAt) return;
        nextUpdateAt = Environment.TickCount64 + UpdateIntervalMilliseconds;

        try
        {
            UpdateSnapshot();
        }
        catch (Exception exception)
        {
            capture.SetPressureLocalEntityId(0);
            capture.ClearPressureEvents();
            ResetRuntime();
            var now = Environment.TickCount64;
            if (now < nextErrorLogAt) return;
            nextErrorLogAt = now + 10_000;
            log.Error(exception, "Seiton Sense pressure scan failed closed.");
        }
    }

    private void UpdateSnapshot()
    {
        var now = Environment.TickCount64;
        var requestedAllyPressureAt = Interlocked.Read(
            ref requestedIncomingAllyPressureAt);
        var oneShotAllyPressureRequested =
            requestedAllyPressureAt >= 0 &&
            now >= requestedAllyPressureAt &&
            now - requestedAllyPressureAt <
            SmartKardiaRules.TriggerLifetimeMilliseconds;
        if (requestedAllyPressureAt >= 0 && !oneShotAllyPressureRequested)
        {
            Interlocked.CompareExchange(
                ref requestedIncomingAllyPressureAt,
                -1,
                requestedAllyPressureAt);
        }
        var local = objectTable.LocalPlayer;
        var localIdentity = CreateIdentity(local);
        var localJobId = local?.ClassJob.IsValid == true
            ? local.ClassJob.RowId
            : 0;
        var isAllyRescueJob = localJobId is EnemyCombatConstants.WhiteMageJobId or
            EnemyCombatConstants.BardJobId;
        var isReactiveCounterCcJob = isAllyRescueJob ||
                                     localJobId == EnemyCombatConstants.NinjaJobId;
        var isScholar = localJobId == EnemyCombatConstants.ScholarJobId;
        var isDarkKnight =
            localJobId == DarkKnightShadowbringerRules.DarkKnightJobId;
        var isEmergencyTeleportJob =
            EmergencyTeleportRules.TryGetActionForJob(localJobId, out _);
        var isBard = localJobId == EnemyCombatConstants.BardJobId;
        var isPaladin = localJobId == EnemyCombatConstants.PaladinJobId;
        var isAstrologian = localJobId ==
                             AstrologianHarmonicOrbisRules.AstrologianJobId;
        var isViper = localJobId == ViperSerpentTailRules.ViperJobId;
        var condition = dutyState.ContentFinderCondition;
        var supportedContext = PvPMatchRules.ResolveSupportedContext(
            clientState.IsPvP,
            clientState.IsPvPExcludingDen,
            configuration.EnableWolvesDenTesting,
            clientState.TerritoryType,
            condition.IsValid,
            condition.IsValid && condition.Value.PvP,
            condition.IsValid ? condition.Value.ContentUICategory.RowId : 0,
            condition.IsValid && condition.Value.CrystallineConflictCasualRoulette,
            condition.IsValid && condition.Value.CrystallineConflictRankedRoulette);
        var isWolvesDen = supportedContext == SupportedPvPContext.WolvesDen;
        var pressureFeaturesEnabled = configuration.ShowPressureCounter ||
                                      (supportedContext == SupportedPvPContext.CrystallineConflict &&
                                       configuration.ShowCcAggressorArrows) ||
                                      configuration.ShowIncomingPressureOnNameplates ||
                                      configuration.ShowTeamPressureOnNameplates ||
                                      configuration.EnableSmartTabTargeting ||
                                      configuration.EnableSmartActionMacro ||
                                      configuration.EnableNearAssistMacro ||
                                      configuration.NearAssistPreferTeamPressure ||
                                      configuration.ShowCurrentTargetInfoHud ||
                                      configuration.EnableDefensiveUtilities ||
                                        (isReactiveCounterCcJob &&
                                         configuration.EnableReactiveCcUtilities &&
                                         (configuration.ReactiveCcAfterEnemyPurify ||
                                          configuration.ReactiveCcAfterEnemyGuard)) ||
                                       (isScholar &&
                                        configuration.EnableScholarCriticalStrategyOnHeldKey) ||
                                       (isDarkKnight &&
                                        configuration.EnableDarkKnightShadowbringerOnHeldKey) ||
                                       (isViper &&
                                        configuration.EnableViperSerpentTailOnHeldKey) ||
                                       configuration.EnableAutoEnemyFocusMark ||
                                       configuration.ShowHighPressureWarning ||
                                       configuration.PlayHighPressureWarningSound ||
                                       configuration.EnablePressureEscapeSprintOnHeldKey ||
                                       (isEmergencyTeleportJob &&
                                        configuration.EnableEmergencyTeleportOnHeldKey);
        var pressureEnabledForContext = pressureFeaturesEnabled &&
                                        (!isWolvesDen ||
                                         configuration.PressureIncludeWolvesDen ||
                                         (isEmergencyTeleportJob &&
                                          configuration.EnableEmergencyTeleportOnHeldKey) ||
                                         (isDarkKnight &&
                                          configuration.EnableDarkKnightShadowbringerOnHeldKey));
        var incomingAllyPressureEnabledForContext =
            supportedContext == SupportedPvPContext.CrystallineConflict &&
            ((isAllyRescueJob &&
              configuration.ExperimentalAllyRescueOnNextKey &&
             metadata.AllyRescueStatusesVerified) ||
             oneShotAllyPressureRequested ||
             (isBard && configuration.EnableBardWardensPaeanPressureRedirect) ||
             (isPaladin && configuration.PaladinGuardianLowAlly) ||
             (isAstrologian &&
              configuration.EnableAstrologianHarmonicOrbisOnHeldKey &&
              configuration.NearHelpPreferIncomingPressure) ||
             (configuration.EnableNearAssistMacro &&
              configuration.NearHelpPreferIncomingPressure));
        var pressureStateTrackingEnabled =
            pressureEnabledForContext || incomingAllyPressureEnabledForContext;
        var wolvesTrackingEnabled = isWolvesDen &&
                                    (configuration.ShowCcProtection ||
                                     pressureEnabledForContext ||
                                     incomingAllyPressureEnabledForContext);
        var normalPvPTrackingEnabled = clientState.IsPvPExcludingDen &&
                                       (configuration.ShowCcProtection ||
                                        pressureEnabledForContext ||
                                        incomingAllyPressureEnabledForContext);
        var supported = configuration.Enabled &&
                        local is not null &&
                        localIdentity.IsValid &&
                        !local.IsDead &&
                        local.CurrentHp > 0 &&
                        (normalPvPTrackingEnabled || wolvesTrackingEnabled);

        if (clientState.TerritoryType != activeTerritory || localIdentity != activeLocalIdentity)
        {
            ResetRuntime();
            activeTerritory = clientState.TerritoryType;
            activeLocalIdentity = localIdentity;
        }

        if (!supported || local is null)
        {
            capture.SetPressureLocalEntityId(0);
            capture.ClearPressureEvents();
            ResetRuntime();
            return;
        }

        var localPlayer = local!;

        capture.SetPressureLocalEntityId(pressureEnabledForContext ? localPlayer.EntityId : 0);
        if (!pressureEnabledForContext)
        {
            capture.ClearPressureEvents();
            recentPressure.Clear();
        }

        var isLargeScalePvP = clientState.IsPvPExcludingDen &&
                              supportedContext != SupportedPvPContext.CrystallineConflict;

        var players = objectTable.PlayerObjects.OfType<IPlayerCharacter>().ToArray();
        Dictionary<ulong, int> exactEnemySlots;
        if (isWolvesDen)
        {
            var opponent = WolvesDenOpponentResolver.Resolve(objectTable, localPlayer, out _, out _, out _);
            var opponentIdentity = CreateIdentity(opponent);
            exactEnemySlots = opponent is not null && opponentIdentity.IsValid
                ? new Dictionary<ulong, int> { [opponent.GameObjectId] = EnemySlotRules.FirstSlot }
                : [];
        }
        else
        {
            exactEnemySlots = executeTracker.IsActive
                ? executeTracker.Enemies
                    .GroupBy(static enemy => enemy.GameObjectId)
                    .Where(static group => group.Count() == 1)
                    .ToDictionary(static group => group.Key, static group => group.Single().Slot)
                : [];
        }
        var exactTrackedEnemies = exactEnemySlots.Keys.ToHashSet();
        var partyEntityIds = partyList
            .Select(static member => member.EntityId)
            .Where(IsNetworkEntityId)
            .ToHashSet();
        var playerByEntityId = players
            .Where(player => CreateIdentity(player).IsValid)
            .GroupBy(static player => player.EntityId)
            .Where(static group => group.Count() == 1)
            .ToDictionary(static group => group.Key, static group => group.Single());
        var localHardTarget = pressureEnabledForContext
            ? ResolveNativeHardTarget(localPlayer, playerByEntityId)
            : null;

        var enemies = new List<(IPlayerCharacter Player, TargetPressureEnemyObservation Observation)>();
        var allies = new List<TargetPressureAllyObservation>();
        var partyAllies = new List<TargetPressurePartyAllyObservation>();
        if (pressureStateTrackingEnabled)
        {
            // Near Help may legitimately select the local player for actions
            // whose exact resolved metadata permits self-targeting. Publish the
            // local identity into the same atomic ally-pressure view so every
            // candidate is queried by the same GOID + EntityId contract.
            partyAllies.Add(new TargetPressurePartyAllyObservation(
                localIdentity,
                true,
                false,
                localPlayer.IsTargetable));
        }

        foreach (var player in players)
        {
            var identity = CreateIdentity(player);
            if (!identity.IsValid || identity == localIdentity) continue;

            var hostile = isWolvesDen
                ? exactTrackedEnemies.Contains(player.GameObjectId)
                : (player.StatusFlags & StatusFlags.Hostile) != 0 ||
                  exactTrackedEnemies.Contains(player.GameObjectId);
            var ally = partyEntityIds.Contains(player.EntityId) ||
                       (player.StatusFlags & (StatusFlags.PartyMember | StatusFlags.AllianceMember)) != 0;
            var hardTarget = pressureStateTrackingEnabled
                ? ResolveNativeHardTarget(player, playerByEntityId)
                : null;

            if (hostile)
            {
                var castTarget = pressureStateTrackingEnabled && player.IsCasting
                    ? ResolveIdentity(player.CastTargetObjectId, playerByEntityId)
                    : null;
                var slot = exactEnemySlots.GetValueOrDefault(player.GameObjectId);
                enemies.Add((player, new TargetPressureEnemyObservation(
                    identity,
                    hardTarget,
                    castTarget,
                    player.ClassJob.IsValid ? player.ClassJob.RowId : 0,
                    slot,
                    true,
                    player.IsDead || player.CurrentHp == 0,
                    player.IsTargetable)));
                continue;
            }

            if (pressureEnabledForContext && ally)
            {
                allies.Add(new TargetPressureAllyObservation(
                    identity,
                    hardTarget,
                    true,
                    player.IsDead || player.CurrentHp == 0,
                    player.IsTargetable));
            }

            if (pressureStateTrackingEnabled && partyEntityIds.Contains(player.EntityId))
            {
                partyAllies.Add(new TargetPressurePartyAllyObservation(
                    identity,
                    true,
                    player.IsDead || player.CurrentHp == 0,
                    player.IsTargetable));
            }
        }

        var drainNow = Environment.TickCount64;
        if (pressureEnabledForContext)
            DrainCaptureEvents(localIdentity, enemies);
        RemoveExpiredPressure(drainNow);
        var recentSignals = recentPressure
            .Select(static pair => new TargetPressureSignal(pair.Key, pair.Value.Evidence))
            .ToArray();
        var core = CorePressureSnapshot.Build(
            localIdentity,
            enemies.Select(static pair => pair.Observation),
            recentSignals,
            allies,
            partyAllies);

        var result = new List<TargetPressureOpponentSnapshot>(enemies.Count);
        var arrowsEnabledForContext = configuration.ShowCcAggressorArrows &&
            supportedContext == SupportedPvPContext.CrystallineConflict;
        var arrowObservations = arrowsEnabledForContext
            ? new List<AggressorArrowObservation>(enemies.Count)
            : null;
        var protectionCount = 0;
        var liveIdentities = new HashSet<TargetPressureActorIdentity>();
        foreach (var (player, observation) in enemies)
        {
            liveIdentities.Add(observation.Actor);
            var sources = TargetPressureSources.None;
            if (pressureEnabledForContext)
            {
                if (core.TryGetOpponent(observation.Actor, out var opponentSnapshot))
                {
                    sources = opponentSnapshot.Sources;
                }
            }
            var displays = UpdateProtections(player, observation.Actor, now, isLargeScalePvP);
            var eligibleForArrow = !observation.IsDead && observation.IsTargetable;
            arrowObservations?.Add(new AggressorArrowObservation(
                observation.Actor,
                sources,
                recentPressure.TryGetValue(observation.Actor, out var recent)
                    ? recent.LastHarmfulActionAtMilliseconds
                    : -1,
                eligibleForArrow));
            protectionCount += displays.Count;
            result.Add(new TargetPressureOpponentSnapshot(
                observation.Actor.GameObjectId,
                observation.Actor.EntityId,
                observation.JobId,
                observation.CcEnemySlot,
                ToRuntimeEvidence(sources),
                pressureEnabledForContext
                    ? core.GetAllyTargetCount(observation.Actor)
                    : 0,
                displays)
            {
                WorldPosition = player.Position,
                IsAliveAndTargetable = eligibleForArrow,
                TotalTeamTargetCount = pressureEnabledForContext
                    ? core.GetTotalTeamTargetCount(observation.Actor, localHardTarget)
                    : 0,
            });
        }

        foreach (var stale in protectionStates.Keys.Where(identity => !liveIdentities.Contains(identity)).ToArray())
            protectionStates.Remove(stale);

        result.Sort(static (left, right) =>
        {
            var leftHasSlot = left.EnemySlot is >= 1 and <= 5;
            var rightHasSlot = right.EnemySlot is >= 1 and <= 5;
            if (leftHasSlot != rightHasSlot) return leftHasSlot ? -1 : 1;
            var slot = left.EnemySlot.CompareTo(right.EnemySlot);
            if (slot != 0) return slot;
            var entity = left.EntityId.CompareTo(right.EntityId);
            return entity != 0 ? entity : left.GameObjectId.CompareTo(right.GameObjectId);
        });

        var publishedAtMilliseconds = Environment.TickCount64;
        var publishedIncomingAllyPressure = new IncomingAllyPressureRuntimeState(
            pressureStateTrackingEnabled,
            publishedAtMilliseconds,
            core.IncomingAllyPressure.ToDictionary(
                static pressure => pressure.Ally,
                static pressure => pressure.UniqueEnemyCount));
        Interlocked.Exchange(ref incomingAllyPressure, publishedIncomingAllyPressure);

        var published = new TargetPressureRuntimeSnapshot(
            true,
            pressureEnabledForContext,
            localIdentity,
            publishedAtMilliseconds,
            result.ToArray())
        {
            CcAggressorArrowsActive = arrowsEnabledForContext,
            TerritoryId = clientState.TerritoryType,
            LocalWorldPosition = localPlayer.Position,
            AggressorArrows = aggressorArrows.Observe(
                arrowsEnabledForContext,
                localIdentity,
                publishedAtMilliseconds,
                arrowObservations ?? []),
        };
        Interlocked.Exchange(ref snapshot, published);
        Volatile.Write(ref diagnostics, new TargetPressureDiagnostics(
            true,
            result.Count,
            result.Count(static enemy => enemy.IsIncoming),
            result.Sum(static enemy => enemy.TeamTargetCount),
            protectionCount,
            recentPressure.Count,
            capture.DroppedPressureEvents));
    }

    private void DrainCaptureEvents(
        TargetPressureActorIdentity localIdentity,
        IReadOnlyList<(IPlayerCharacter Player, TargetPressureEnemyObservation Observation)> enemies)
    {
        var exactEnemyByEntity = enemies
            .GroupBy(static enemy => enemy.Observation.Actor.EntityId)
            .Where(static group => group.Count() == 1)
            .ToDictionary(static group => group.Key, static group => group.Single().Observation.Actor);
        var windowMilliseconds = (long)(Math.Clamp(configuration.PressureWindowSeconds, 0.5f, 8f) * 1000f);
        while (capture.TryDequeuePressure(out var pressureEvent))
        {
            var eventNow = Environment.TickCount64;
            if (pressureEvent.TargetEntityId != localIdentity.EntityId ||
                pressureEvent.ObservedAtMilliseconds > eventNow ||
                eventNow - pressureEvent.ObservedAtMilliseconds > windowMilliseconds)
            {
                continue;
            }

            var sourcePlayer = ResolvePlayerOwner(pressureEvent.CasterEntityId);
            if (sourcePlayer is null ||
                !exactEnemyByEntity.TryGetValue(sourcePlayer.EntityId, out var source) ||
                source != CreateIdentity(sourcePlayer))
            {
                continue;
            }

            var sources = pressureEvent.Evidence switch
            {
                TargetPressureEvidence.MachinistLimitBreakMarker =>
                    TargetPressureSources.MachinistLimitBreakEarlyMarker,
                TargetPressureEvidence.RecentHarmfulAction |
                    TargetPressureEvidence.MachinistLimitBreakMarker =>
                    TargetPressureSources.RecentHarmfulAction |
                    TargetPressureSources.MachinistLimitBreakEarlyMarker,
                _ when (pressureEvent.Evidence & TargetPressureEvidence.RecentHarmfulAction) != 0 =>
                    TargetPressureSources.RecentHarmfulAction,
                _ => TargetPressureSources.None,
            };
            if (sources == TargetPressureSources.None) continue;

            var expiresAt = SaturatingAdd(pressureEvent.ObservedAtMilliseconds, windowMilliseconds);
            var harmfulAt = (sources & TargetPressureSources.RecentHarmfulAction) != 0
                ? pressureEvent.ObservedAtMilliseconds
                : -1;
            if (recentPressure.TryGetValue(source, out var previous))
            {
                recentPressure[source] = new RecentPressureState(
                    Math.Max(previous.ExpiresAtMilliseconds, expiresAt),
                    previous.Evidence | sources,
                    Math.Max(previous.LastHarmfulActionAtMilliseconds, harmfulAt));
            }
            else
            {
                recentPressure[source] = new RecentPressureState(expiresAt, sources, harmfulAt);
            }
        }
    }

    private IReadOnlyList<CcProtectionDisplay> UpdateProtections(
        IPlayerCharacter player,
        TargetPressureActorIdentity identity,
        long now,
        bool isLargeScalePvP)
    {
        var protectionsEnabled = configuration.ShowCcProtection;
        if (!protectionsEnabled || verifiedProtectionStatusIds.Count == 0)
        {
            protectionStates.Remove(identity);
            return [];
        }

        if (!protectionStates.TryGetValue(identity, out var actorState))
        {
            actorState = new ProtectionActorState();
            protectionStates[identity] = actorState;
        }

        var observations = new List<ObservedCcProtectionStatus>(4);
        foreach (var status in player.StatusList)
        {
            if (!verifiedProtectionStatusIds.Contains(status.StatusId) ||
                !float.IsFinite(status.RemainingTime) ||
                status.RemainingTime <= 0f)
            {
                continue;
            }

            observations.Add(new ObservedCcProtectionStatus(status.StatusId, status.RemainingTime));
        }

        var observed = CcProtectionStatusCatalog.BuildIndicators(observations)
            .Where(indicator => IsProtectionValidForActor(
                indicator.StatusId,
                player.ClassJob.IsValid ? player.ClassJob.RowId : 0,
                isLargeScalePvP))
            .ToArray();
        var seen = new HashSet<uint>();
        foreach (var indicator in observed)
        {
            // Both Guard variants are one presentation family. Replacing normal Guard
            // with Light Guard must update the icon without a duplicate or blank frame.
            var familyId = indicator.StatusId is 3054 or 3673 ? 3054u : indicator.StatusId;
            seen.Add(familyId);
            var duration = Math.Max(1L, (long)Math.Round(indicator.RemainingTime * 1000f));
            var observedExpiry = SaturatingAdd(now, duration);
            var expiry = observedExpiry;
            if (actorState.Statuses.TryGetValue(familyId, out var previous) &&
                indicator.RemainingTime <= previous.LastObservedRemainingSeconds + ProtectionRefreshThresholdSeconds)
            {
                // RemainingTime can repeat for adjacent samples. Retaining the first
                // absolute deadline prevents a stale value from drifting the icon and
                // countdown forward forever. A real refresh has a visible duration jump.
                expiry = Math.Min(previous.ExpiresAtMilliseconds, observedExpiry);
            }

            actorState.Statuses[familyId] = new ProtectionRuntimeState(
                indicator.Name,
                indicator.IconId,
                indicator.Kind,
                expiry,
                now,
                indicator.RemainingTime);
        }

        foreach (var statusId in actorState.Statuses.Keys.ToArray())
        {
            var state = actorState.Statuses[statusId];
            if (state.ExpiresAtMilliseconds <= now ||
                !seen.Contains(statusId) && now - state.LastSeenAtMilliseconds >= ProtectionMissingGraceMilliseconds)
            {
                actorState.Statuses.Remove(statusId);
            }
        }

        if (actorState.Statuses.Count == 0)
        {
            protectionStates.Remove(identity);
            return [];
        }

        return actorState.Statuses
            .Select(static pair => new CcProtectionDisplay(
                pair.Key,
                pair.Value.Name,
                pair.Value.IconId,
                pair.Value.Kind,
                pair.Value.ExpiresAtMilliseconds))
            .OrderBy(static display => display.Kind)
            .ThenBy(static display => display.StatusId)
            .ToArray();
    }

    private static bool IsProtectionValidForActor(uint statusId, uint jobId, bool isLargeScalePvP) => statusId switch
    {
        1303 => jobId == 21, // WAR Inner Release
        1320 => jobId == 34, // SAM Meikyo Shisui
        4096 => jobId == 41, // VPR Hardened Scales
        4477 => isLargeScalePvP, // Frontline/Rival Wings duty action only
        _ => true,
    };

    private static TargetPressureEvidence ToRuntimeEvidence(TargetPressureSources sources)
    {
        var result = TargetPressureEvidence.None;
        if ((sources & TargetPressureSources.HardTarget) != 0) result |= TargetPressureEvidence.HardTarget;
        if ((sources & TargetPressureSources.CastTarget) != 0) result |= TargetPressureEvidence.CastTarget;
        if ((sources & TargetPressureSources.RecentHarmfulAction) != 0) result |= TargetPressureEvidence.RecentHarmfulAction;
        if ((sources & TargetPressureSources.MachinistLimitBreakEarlyMarker) != 0)
            result |= TargetPressureEvidence.MachinistLimitBreakMarker;
        return result;
    }

    private static unsafe TargetPressureActorIdentity? ResolveNativeHardTarget(
        IPlayerCharacter player,
        IReadOnlyDictionary<uint, IPlayerCharacter> playerByEntityId)
    {
        if (player.Address == nint.Zero) return null;
        var native = (GameObject*)player.Address;
        if (native == null || native->EntityId != player.EntityId) return null;
        var targetId = ((Character*)player.Address)->GetTargetId().ObjectId;
        return playerByEntityId.TryGetValue(targetId, out var target)
            ? CreateIdentity(target)
            : null;
    }

    private static TargetPressureActorIdentity? ResolveIdentity(
        ulong targetId,
        IReadOnlyDictionary<uint, IPlayerCharacter> playerByEntityId)
    {
        var entityId = unchecked((uint)targetId);
        return playerByEntityId.TryGetValue(entityId, out var target)
            ? CreateIdentity(target)
            : null;
    }

    private IPlayerCharacter? ResolvePlayerOwner(uint sourceEntityId)
    {
        var source = objectTable.SearchByEntityId(sourceEntityId);
        if (source is IPlayerCharacter player) return CreateIdentity(player).IsValid ? player : null;
        if (source is null || source.OwnerId is 0 or 0xE0000000) return null;

        var owner = objectTable.SearchByEntityId(unchecked((uint)source.OwnerId)) as IPlayerCharacter;
        return CreateIdentity(owner).IsValid ? owner : null;
    }

    private static unsafe TargetPressureActorIdentity CreateIdentity(IPlayerCharacter? player)
    {
        if (player is null || player.Address == nint.Zero || !IsNetworkEntityId(player.EntityId))
            return default;

        var native = (GameObject*)player.Address;
        return native != null && native->EntityId == player.EntityId
            ? new TargetPressureActorIdentity(player.GameObjectId, player.EntityId)
            : default;
    }

    private void RemoveExpiredPressure(long now)
    {
        foreach (var identity in recentPressure
                     .Where(pair => pair.Value.ExpiresAtMilliseconds <= now)
                     .Select(static pair => pair.Key)
                     .ToArray())
        {
            recentPressure.Remove(identity);
        }
    }

    private void ResetRuntime()
    {
        aggressorArrows.Reset();
        recentPressure.Clear();
        protectionStates.Clear();
        Interlocked.Exchange(ref requestedIncomingAllyPressureAt, -1);
        Interlocked.Exchange(ref incomingAllyPressure, IncomingAllyPressureRuntimeState.Inactive);
        Interlocked.Exchange(ref snapshot, TargetPressureRuntimeSnapshot.Inactive);
        Volatile.Write(ref diagnostics, TargetPressureDiagnostics.Inactive);
    }

    private static bool IsNetworkEntityId(uint value) => value is not 0 and not 0xE0000000u;

    private static long SaturatingAdd(long value, long addition) =>
        addition > 0 && value > long.MaxValue - addition ? long.MaxValue : value + addition;

    private sealed class ProtectionActorState
    {
        internal Dictionary<uint, ProtectionRuntimeState> Statuses { get; } = [];
    }

    private readonly record struct ProtectionRuntimeState(
        string Name,
        uint IconId,
        CcProtectionKind Kind,
        long ExpiresAtMilliseconds,
        long LastSeenAtMilliseconds,
        float LastObservedRemainingSeconds);

    private readonly record struct RecentPressureState(
        long ExpiresAtMilliseconds,
        TargetPressureSources Evidence,
        long LastHarmfulActionAtMilliseconds);

    private sealed record IncomingAllyPressureRuntimeState(
        bool Active,
        long PublishedAtMilliseconds,
        IReadOnlyDictionary<TargetPressureActorIdentity, int> Counts)
    {
        internal static IncomingAllyPressureRuntimeState Inactive { get; } = new(
            false,
            -1,
            new Dictionary<TargetPressureActorIdentity, int>());
    }
}
