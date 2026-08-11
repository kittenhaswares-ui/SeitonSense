using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using SeitonSense.Core;
using SeitonSense.Plugin.Models;

namespace SeitonSense.Plugin.Services;

internal sealed class PersonalStatusService : IDisposable
{
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly IFramework framework;
    private readonly IDutyState dutyState;
    private readonly IPluginLog log;
    private readonly PluginConfiguration configuration;
    private readonly PvPMetadataValidation metadata;
    private readonly EmergencyPurifyProbe emergencyPurify;
    private readonly Dictionary<ObservedStatusKey, StatusIdentityState> instanceTokens = [];
    private readonly Dictionary<uint, ObservedPersonalStatus> lastPresentations = [];
    private readonly Dictionary<StatusPulseKey, long> pulseStartedAt = [];
    private PersonalDebuffAlertState[] alertStates = [];
    private PersonalAlertSnapshot snapshot = PersonalAlertSnapshot.Inactive;
    private ulong nextInstanceToken = 1;
    private long nextErrorLogAt;
    private long purifyMissingObservedAt = -1;
    private DebouncedVisibilityState resiliencePresence = DebouncedVisibilityState.Initial;
    private uint activeTerritory = uint.MaxValue;
    private ulong activeLocalPlayerId;
    private SupportedPvPContext activeContext;
    private bool started;
    private bool disposed;

    internal PersonalStatusService(
        IClientState clientState,
        IObjectTable objectTable,
        IFramework framework,
        IDutyState dutyState,
        IKeyState keyState,
        IPluginLog log,
        PluginConfiguration configuration,
        PvPMetadataValidation metadata)
    {
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.framework = framework;
        this.dutyState = dutyState;
        this.log = log;
        this.configuration = configuration;
        this.metadata = metadata;
        emergencyPurify = new EmergencyPurifyProbe(new GameInputContextProbe(keyState), log);
    }

    internal PersonalAlertSnapshot Snapshot => Volatile.Read(ref snapshot);

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
        ResetRuntime();
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        if (disposed) return;

        try
        {
            UpdateSnapshot();
        }
        catch (Exception exception)
        {
            var now = Environment.TickCount64;
            alertStates = [];
            lastPresentations.Clear();
            pulseStartedAt.Clear();
            var purify = emergencyPurify.FailClosed(now);
            Interlocked.Exchange(ref snapshot, new PersonalAlertSnapshot(
                false,
                SupportedPvPContext.None,
                false,
                [],
                purify));
            if (now < nextErrorLogAt) return;
            nextErrorLogAt = now + 10_000;
            log.Error(exception, "Seiton Sense personal-status scan failed closed.");
        }
    }

    private void UpdateSnapshot()
    {
        var now = Environment.TickCount64;
        var localPlayer = objectTable.LocalPlayer;
        var localPlayerId = localPlayer?.GameObjectId ?? 0;
        var context = ResolveSupportedPvPContext();
        var hardReset = activeTerritory != clientState.TerritoryType ||
                        activeLocalPlayerId != localPlayerId ||
                        activeContext != context;
        if (hardReset)
        {
            activeTerritory = clientState.TerritoryType;
            activeLocalPlayerId = localPlayerId;
            activeContext = context;
            ClearStatusTracking();
            emergencyPurify.Reset();
        }

        var isSupportedPvPContext = context != SupportedPvPContext.None;
        var alive = IsAlive(localPlayer);
        var anyPurifyAutomationEnabled = AnyPurifyAutomationEnabled();
        var anyWarningEnabled = configuration.ShowPersonalWarnings &&
                                (configuration.WarnWildfire ||
                                 configuration.WarnDeathWarrant ||
                                 configuration.WarnPurifiableCrowdControl);
        var shouldScanStatuses = configuration.Enabled &&
                                 isSupportedPvPContext &&
                                 alive &&
                                 (anyWarningEnabled ||
                                  (configuration.ExperimentalPurifyOnNextKey &&
                                   anyPurifyAutomationEnabled));
        var observed = shouldScanStatuses
            ? ScanExactStatuses(localPlayer, now)
            : [];
        if (!shouldScanStatuses) instanceTokens.Clear();
        var resilienceObserved = shouldScanStatuses &&
                                 metadata.PurifyVerified &&
                                 HasActiveStatus(localPlayer, EnemyCombatConstants.ResilienceStatusId);
        resiliencePresence = DebouncedVisibilityRules.Observe(
            resiliencePresence,
            resilienceObserved,
            now,
            hardReset || !shouldScanStatuses || !metadata.PurifyVerified,
            PersonalDebuffAlertRules.MissingGraceMilliseconds);
        var resilienceActive = resiliencePresence.IsVisible;
        var warningsActive = configuration.Enabled &&
                             configuration.ShowPersonalWarnings &&
                             isSupportedPvPContext &&
                             alive &&
                             !hardReset;
        var statuses = BuildAlertSnapshots(observed, now, warningsActive, hardReset);

        var purifyStatus = SelectPurifyStatus(
            observed,
            now,
            out var purifyStatusCurrentlyObserved);
        var purifyConfigurationEnabled = configuration.Enabled &&
                                         configuration.ExperimentalPurifyOnNextKey &&
                                         anyPurifyAutomationEnabled &&
                                         metadata.PurifyVerified;
        var purify = emergencyPurify.Observe(
            localPlayer,
            isSupportedPvPContext,
            purifyConfigurationEnabled,
            configuration.PurifyOnHeldGameplayKey,
            purifyStatus,
            purifyStatusCurrentlyObserved,
            resilienceActive,
            now,
            configuration.ExperimentalPurifyBufferMilliseconds,
            hardReset);

        Interlocked.Exchange(ref snapshot, new PersonalAlertSnapshot(
            configuration.Enabled && isSupportedPvPContext && alive && !hardReset,
            context,
            alive,
            statuses,
            purify));
    }

    private SupportedPvPContext ResolveSupportedPvPContext()
    {
        var condition = dutyState.ContentFinderCondition;
        var conditionValid = condition.IsValid;
        var context = PvPMatchRules.ResolveSupportedContext(
            clientState.IsPvP,
            clientState.IsPvPExcludingDen,
            configuration.EnableWolvesDenTesting,
            clientState.TerritoryType,
            conditionValid,
            conditionValid && condition.Value.PvP,
            conditionValid ? condition.Value.ContentUICategory.RowId : 0,
            conditionValid && condition.Value.CrystallineConflictCasualRoulette,
            conditionValid && condition.Value.CrystallineConflictRankedRoulette);
        // Personal warnings and self-Purify do not need an enemy actor. The exact
        // duel-opponent resolver remains mandatory for the enemy HUD, while this
        // self-only path needs only the supported Wolves' Den PvP context plus an
        // exact locally observed status.
        return context;
    }

    private List<ObservedPersonalStatus> ScanExactStatuses(
        IPlayerCharacter? localPlayer,
        long nowMilliseconds)
    {
        var observed = new List<ObservedPersonalStatus>(4);
        var seenKeys = new HashSet<ObservedStatusKey>();
        if (localPlayer is not null)
        {
            foreach (var status in localPlayer.StatusList)
            {
                var definition = PersonalStatusDefinitions.Find(status.StatusId);
                if (definition is null ||
                    !PersonalStatusDefinitions.IsMetadataVerified(definition, metadata) ||
                    status.Address == 0 ||
                    !float.IsFinite(status.RemainingTime) ||
                    status.RemainingTime <= 0f)
                {
                    continue;
                }

                // Status-array slots can move as unrelated statuses disappear. Identity is
                // the exact continuous (status, source) observation; absence ends it.
                var key = new ObservedStatusKey(status.StatusId, status.SourceId);
                seenKeys.Add(key);
                if (!instanceTokens.TryGetValue(key, out var identity))
                {
                    identity = new StatusIdentityState(NextInstanceToken(), nowMilliseconds);
                }
                else
                {
                    identity = identity with { LastSeenAtMilliseconds = nowMilliseconds };
                }
                instanceTokens[key] = identity;

                var remainingMilliseconds = Math.Max(
                    1,
                    (long)Math.Round(Math.Min(status.RemainingTime, 3_600f) * 1000f));
                observed.Add(new ObservedPersonalStatus(
                    definition,
                    status.SourceId,
                    identity.Token,
                    remainingMilliseconds,
                    SaturatingAdd(nowMilliseconds, remainingMilliseconds)));
            }
        }

        foreach (var stale in instanceTokens
                     .Where(pair =>
                         !seenKeys.Contains(pair.Key) &&
                         (nowMilliseconds < pair.Value.LastSeenAtMilliseconds ||
                          nowMilliseconds - pair.Value.LastSeenAtMilliseconds >=
                          PersonalDebuffAlertRules.MissingGraceMilliseconds))
                     .Select(static pair => pair.Key)
                     .ToArray())
        {
            instanceTokens.Remove(stale);
        }

        return observed;
    }

    private PersonalStatusSnapshot[] BuildAlertSnapshots(
        IReadOnlyList<ObservedPersonalStatus> observed,
        long nowMilliseconds,
        bool warningsActive,
        bool hardReset)
    {
        if (!warningsActive || hardReset)
        {
            alertStates = [];
            lastPresentations.Clear();
            return [];
        }

        var allowedStatusIds = PersonalStatusDefinitions.All
            .Where(IsWarningEnabled)
            .Select(static definition => definition.StatusId)
            .ToHashSet();
        var previous = alertStates
            .Where(state => allowedStatusIds.Contains(state.StatusId))
            .ToArray();
        var visibleObservations = observed
            .Where(status => allowedStatusIds.Contains(status.Definition.StatusId))
            .Select(status => new PersonalDebuffObservation(
                status.Definition.StatusId,
                status.Definition.AlertKind,
                status.ExpiresAtMilliseconds))
            .ToArray();
        var decision = PersonalDebuffAlertRules.Observe(
            previous,
            visibleObservations,
            nowMilliseconds);
        alertStates = decision.NextStates;

        var results = new List<PersonalStatusSnapshot>(decision.Alerts.Length);
        var retainedPresentations = new Dictionary<uint, ObservedPersonalStatus>();
        var retainedPulseKeys = new HashSet<StatusPulseKey>();
        foreach (var alert in decision.Alerts)
        {
            var presentation = observed
                .Where(status => status.Definition.StatusId == alert.StatusId)
                .OrderByDescending(static status => status.ExpiresAtMilliseconds)
                .FirstOrDefault();
            if (presentation is null && !lastPresentations.TryGetValue(alert.StatusId, out presentation))
                continue;

            retainedPresentations[alert.StatusId] = presentation;
            var pulseKey = new StatusPulseKey(alert.StatusId, presentation.InstanceToken);
            if (alert.TriggerEntryPulse) pulseStartedAt[pulseKey] = nowMilliseconds;
            var pulseStart = pulseStartedAt.GetValueOrDefault(pulseKey, -1);
            var pulseActive = pulseStart >= 0 &&
                              nowMilliseconds >= pulseStart &&
                              nowMilliseconds - pulseStart < PersonalStatusSnapshot.EntryPulseDurationMilliseconds;
            if (pulseActive) retainedPulseKeys.Add(pulseKey);
            else pulseStart = -1;

            results.Add(new PersonalStatusSnapshot(
                presentation.Definition.StatusId,
                presentation.Definition.Name,
                presentation.Definition.IconId,
                presentation.Definition.AlertKind,
                presentation.SourceId,
                presentation.InstanceToken,
                alert.RemainingMilliseconds,
                SaturatingAdd(nowMilliseconds, alert.RemainingMilliseconds),
                pulseStart,
                pulseActive));
        }

        lastPresentations.Clear();
        foreach (var (statusId, presentation) in retainedPresentations)
            lastPresentations[statusId] = presentation;
        foreach (var stalePulse in pulseStartedAt.Keys.Where(key => !retainedPulseKeys.Contains(key)).ToArray())
            pulseStartedAt.Remove(stalePulse);
        return results.ToArray();
    }

    private PurifyCcStatusInstance? SelectPurifyStatus(
        IReadOnlyList<ObservedPersonalStatus> observed,
        long nowMilliseconds,
        out bool currentlyObserved)
    {
        currentlyObserved = false;
        var purifiable = observed
            .Where(status =>
                status.Definition.CanTriggerPurifyBuffer &&
                IsPurifyAutomationEnabled(status.Definition.StatusId))
            .ToArray();
        var tracked = emergencyPurify.TrackedStatusInstance;
        if (tracked is not null && IsPurifyAutomationEnabled(tracked.Value.StatusId))
        {
            var same = purifiable.FirstOrDefault(status =>
                status.Definition.StatusId == tracked.Value.StatusId &&
                status.InstanceToken == tracked.Value.InstanceToken);
            if (same is not null)
            {
                purifyMissingObservedAt = -1;
                currentlyObserved = true;
                return tracked;
            }

            if (purifiable.Length == 0)
            {
                if (purifyMissingObservedAt < 0 || nowMilliseconds < purifyMissingObservedAt)
                    purifyMissingObservedAt = nowMilliseconds;
                if (nowMilliseconds - purifyMissingObservedAt <
                    PersonalDebuffAlertRules.MissingGraceMilliseconds)
                {
                    return tracked;
                }
            }
        }

        purifyMissingObservedAt = -1;
        var selected = purifiable
            .OrderBy(static status => status.Definition.StatusId)
            .ThenBy(static status => status.InstanceToken)
            .FirstOrDefault();
        if (selected is null) return null;

        currentlyObserved = true;
        return new PurifyCcStatusInstance(selected.Definition.StatusId, selected.InstanceToken);
    }

    private bool IsWarningEnabled(PersonalStatusDefinition definition) =>
        PersonalStatusDefinitions.IsMetadataVerified(definition, metadata) &&
        definition.RequiredFeature switch
        {
            PersonalStatusFeature.Wildfire => configuration.WarnWildfire,
            PersonalStatusFeature.DeathWarrant => configuration.WarnDeathWarrant,
            PersonalStatusFeature.Purify => configuration.WarnPurifiableCrowdControl,
            _ => false,
        };

    private bool AnyPurifyAutomationEnabled() =>
        configuration.PurifyOnStun ||
        configuration.PurifyOnHeavy ||
        configuration.PurifyOnBind ||
        configuration.PurifyOnSilence ||
        configuration.PurifyOnDeepFreeze ||
        configuration.PurifyOnMiracleOfNature;

    private bool IsPurifyAutomationEnabled(uint statusId) =>
        statusId switch
        {
            EnemyCombatConstants.PvPStunStatusId => configuration.PurifyOnStun,
            EnemyCombatConstants.PvPHeavyStatusId => configuration.PurifyOnHeavy,
            EnemyCombatConstants.PvPBindStatusId => configuration.PurifyOnBind,
            EnemyCombatConstants.PvPSilenceStatusId => configuration.PurifyOnSilence,
            EnemyCombatConstants.DeepFreezeStatusId => configuration.PurifyOnDeepFreeze,
            EnemyCombatConstants.MiracleOfNatureStatusId => configuration.PurifyOnMiracleOfNature,
            _ => false,
        };

    private ulong NextInstanceToken()
    {
        var token = nextInstanceToken++;
        if (token != 0) return token;

        token = nextInstanceToken++;
        return token == 0 ? 1 : token;
    }

    private void ResetRuntime()
    {
        ClearStatusTracking();
        emergencyPurify.Reset();
        Interlocked.Exchange(ref snapshot, PersonalAlertSnapshot.Inactive);
    }

    private void ClearStatusTracking()
    {
        instanceTokens.Clear();
        lastPresentations.Clear();
        pulseStartedAt.Clear();
        purifyMissingObservedAt = -1;
        resiliencePresence = DebouncedVisibilityState.Initial;
        alertStates = [];
    }

    private static bool IsAlive(IPlayerCharacter? localPlayer) =>
        localPlayer is not null &&
        !localPlayer.IsDead &&
        localPlayer.CurrentHp > 0 &&
        localPlayer.MaxHp >= localPlayer.CurrentHp;

    private static bool HasActiveStatus(IPlayerCharacter? player, uint statusId)
    {
        if (player is null || statusId == 0) return false;

        foreach (var status in player.StatusList)
        {
            if (status.StatusId == statusId &&
                status.Address != 0 &&
                float.IsFinite(status.RemainingTime) &&
                status.RemainingTime > 0f)
            {
                return true;
            }
        }

        return false;
    }

    private static long SaturatingAdd(long left, long right) =>
        left > long.MaxValue - right ? long.MaxValue : left + right;

    private readonly record struct ObservedStatusKey(uint StatusId, uint SourceId);

    private readonly record struct StatusIdentityState(ulong Token, long LastSeenAtMilliseconds);

    private readonly record struct StatusPulseKey(uint StatusId, ulong InstanceToken);

    private sealed record ObservedPersonalStatus(
        PersonalStatusDefinition Definition,
        uint SourceId,
        ulong InstanceToken,
        long RemainingMilliseconds,
        long ExpiresAtMilliseconds);
}
