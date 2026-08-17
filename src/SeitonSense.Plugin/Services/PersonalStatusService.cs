using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using SeitonSense.Core;
using SeitonSense.Plugin.Models;

namespace SeitonSense.Plugin.Services;

internal readonly record struct MachinistLimitBreakDiagnostics(
    bool CaptureRunning,
    int QueueDepth,
    long AcceptedWarnings,
    long CaptureErrors,
    long DroppedWarnings,
    bool WarningActive);

internal sealed class PersonalStatusService : IDisposable
{
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly IFramework framework;
    private readonly IDutyState dutyState;
    private readonly IPluginLog log;
    private readonly PluginConfiguration configuration;
    private readonly PvPMetadataValidation metadata;
    private readonly TargetPressureTracker pressureTracker;
    private readonly NearAssistRedirector nearAssist;
    private readonly EmergencyActionInputCoordinator emergencyInput;
    private readonly EmergencyPurifyProbe emergencyPurify;
    private readonly DefensiveUtilityProbe defensiveUtility;
    private readonly PressureEscapeSprintProbe pressureEscapeSprint;
    private readonly GuardianCommunicationService guardianCommunication;
    private readonly AllyRescueProbe allyRescue;
    private readonly MiracleInterceptProbe miracleIntercept;
    private readonly NinjaSeitonDispatchProbe ninjaSeiton;
    private readonly ScholarCriticalStrategyProbe scholarCriticalStrategy;
    private readonly MonkEarthReplyProbe monkEarthReply;
    private readonly MachinistLimitBreakCapture machinistLimitBreakCapture;
    private readonly MachinistLimitBreakWarningSound machinistLimitBreakWarningSound;
    private readonly Dictionary<ObservedStatusKey, StatusIdentityState> instanceTokens = [];
    private readonly Dictionary<uint, ObservedPersonalStatus> lastPresentations = [];
    private readonly Dictionary<StatusPulseKey, long> pulseStartedAt = [];
    private PersonalDebuffAlertState[] alertStates = [];
    private PersonalAlertSnapshot snapshot = PersonalAlertSnapshot.Inactive;
    private ulong nextInstanceToken = 1;
    private long nextErrorLogAt;
    private long acceptedMachinistLimitBreakWarnings;
    private long purifyMissingObservedAt = -1;
    private DebouncedVisibilityState resiliencePresence = DebouncedVisibilityState.Initial;
    private MachinistLimitBreakThreatState? machinistLimitBreakThreat;
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
        IDataManager dataManager,
        TargetPressureTracker pressureTracker,
        ExecuteTracker executeTracker,
        NearAssistRedirector nearAssist,
        MachinistLimitBreakCapture machinistLimitBreakCapture,
        IPluginLog log,
        PluginConfiguration configuration,
        PvPMetadataValidation metadata,
        ReviewedPvpCommandDispatcher commands)
    {
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.framework = framework;
        this.dutyState = dutyState;
        this.log = log;
        this.configuration = configuration;
        this.metadata = metadata;
        this.pressureTracker = pressureTracker;
        this.nearAssist = nearAssist;
        emergencyInput = new EmergencyActionInputCoordinator(keyState);
        emergencyPurify = new EmergencyPurifyProbe(log);
        defensiveUtility = new DefensiveUtilityProbe(
            objectTable,
            dataManager,
            pressureTracker,
            nearAssist,
            log,
            metadata);
        pressureEscapeSprint = new PressureEscapeSprintProbe(
            clientState,
            dutyState,
            objectTable,
            dataManager,
            pressureTracker,
            nearAssist,
            defensiveUtility,
            log);
        guardianCommunication = new GuardianCommunicationService(
            configuration,
            clientState,
            objectTable,
            dutyState,
            dataManager,
            log,
            commands);
        allyRescue = new AllyRescueProbe(
            objectTable,
            dataManager,
            pressureTracker,
            nearAssist,
            machinistLimitBreakCapture,
            log);
        miracleIntercept = new MiracleInterceptProbe(
            objectTable,
            nearAssist.VerifiedCcBrakeStatusIds,
            executeTracker,
            pressureTracker,
            nearAssist,
            machinistLimitBreakCapture,
            log,
            metadata);
        ninjaSeiton = new NinjaSeitonDispatchProbe(
            objectTable,
            executeTracker,
            nearAssist,
            log);
        scholarCriticalStrategy = new ScholarCriticalStrategyProbe(
            clientState,
            objectTable,
            dutyState,
            configuration,
            executeTracker,
            pressureTracker,
            nearAssist,
            log);
        monkEarthReply = new MonkEarthReplyProbe(nearAssist, log);
        this.machinistLimitBreakCapture = machinistLimitBreakCapture;
        machinistLimitBreakWarningSound = new MachinistLimitBreakWarningSound(log);
    }

    internal PersonalAlertSnapshot Snapshot => Volatile.Read(ref snapshot);
    internal DefensiveUtilityProbeSnapshot DefensiveUtilityDiagnostics => defensiveUtility.Snapshot;
    internal PressureEscapeSprintProbeSnapshot PressureEscapeDiagnostics =>
        pressureEscapeSprint.Snapshot;
    internal GuardianCommunicationDiagnostics GuardianCommunicationDiagnostics =>
        guardianCommunication.Diagnostics;
    internal AllyRescueProbeSnapshot AllyRescueDiagnostics => allyRescue.Snapshot;
    internal MiracleInterceptProbeSnapshot MiracleInterceptDiagnostics => miracleIntercept.Snapshot;
    internal NinjaSeitonDispatchProbeSnapshot NinjaSeitonDiagnostics => ninjaSeiton.Snapshot;
    internal ScholarCriticalStrategyProbeSnapshot ScholarCriticalStrategyDiagnostics =>
        scholarCriticalStrategy.Snapshot;
    internal MonkEarthReplyProbeSnapshot MonkEarthReplyDiagnostics => monkEarthReply.Snapshot;
    internal void ResetAllyRescueStatistics() => allyRescue.RequestStatisticsReset();
    internal MachinistLimitBreakDiagnostics MachinistLimitBreakDiagnostics => new(
        machinistLimitBreakCapture.IsRunning,
        machinistLimitBreakCapture.QueueDepth,
        Interlocked.Read(ref acceptedMachinistLimitBreakWarnings),
        machinistLimitBreakCapture.CaptureErrors,
        machinistLimitBreakCapture.DroppedWarnings,
        machinistLimitBreakThreat is { ExpiresAtMilliseconds: var expiresAt } &&
        expiresAt > Environment.TickCount64);

    internal bool PlayMachinistLimitBreakSoundPreview() =>
        machinistLimitBreakWarningSound.TryPlayPreview(
            Math.Clamp(configuration.MchLimitBreakSoundId, 1, 16),
            Environment.TickCount64);

    internal bool PlayHighPressureWarningSoundPreview() =>
        pressureEscapeSprint.PlayWarningSoundPreview(
            Math.Clamp(configuration.HighPressureWarningSoundId, 1, 16));

    internal void Start()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (started) return;
        started = true;
        try
        {
            machinistLimitBreakCapture.Start();
        }
        catch (Exception exception)
        {
            log.Warning(
                exception,
                "Seiton Sense MCH limit-break capture is unavailable; other features remain active.");
        }
        framework.Update += OnFrameworkUpdate;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (started) framework.Update -= OnFrameworkUpdate;
        guardianCommunication.TryClearOneExactOwnershipOnDispose(
            objectTable.LocalPlayer,
            ResolveSupportedPvPContext(),
            Environment.TickCount64);
        machinistLimitBreakCapture.Dispose();
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
            machinistLimitBreakCapture.SetMachinistLocalEntityId(0);
            machinistLimitBreakCapture.ClearWarnings();
            machinistLimitBreakThreat = null;
            emergencyInput.Reset();
            var purify = emergencyPurify.FailClosed(now);
            defensiveUtility.FailClosed(now, exception);
            pressureEscapeSprint.FailClosed(now, exception);
            guardianCommunication.FailClosed(now, exception);
            allyRescue.FailClosed(now, exception);
            miracleIntercept.FailClosed(now, exception);
            ninjaSeiton.FailClosed();
            scholarCriticalStrategy.FailClosed();
            monkEarthReply.FailClosed(now);
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
            emergencyInput.Reset();
            emergencyPurify.Reset();
            defensiveUtility.Reset();
            pressureEscapeSprint.Reset();
            guardianCommunication.Reset();
            allyRescue.Reset();
            miracleIntercept.Reset();
            ninjaSeiton.Reset();
            scholarCriticalStrategy.Reset();
            monkEarthReply.Reset();
        }

        var isSupportedPvPContext = context != SupportedPvPContext.None;
        var isCrystallineConflict = context == SupportedPvPContext.CrystallineConflict;
        var alive = IsAlive(localPlayer);
        var anyPurifyAutomationEnabled = AnyPurifyAutomationEnabled();
        var defensiveUtilitiesConfigurationEnabled = configuration.Enabled &&
                                                    configuration.EnableDefensiveUtilities &&
                                                    isCrystallineConflict;
        var pressureKnown = pressureTracker.TryGetSelfIncomingPressure(out var incomingEnemyCount);
        var highPressure = DefensiveUtilityRules.IsHighPressure(
            pressureKnown,
            incomingEnemyCount);
        var guardActive = DefensiveUtilityProbe.HasActiveGuard(localPlayer);
        var exactGuardActive = guardActive;
        var guardObservationNow = Math.Max(now, Environment.TickCount64);
        var observedGuardAttemptAt = -1L;
        if (localPlayer is not null)
        {
            nearAssist.TryGetRecentExactLocalGuardAttempt(
                clientState.TerritoryType,
                localPlayer.GameObjectId,
                localPlayer.EntityId,
                guardObservationNow,
                DefensiveUtilityRules.GuardPropagationLatchMilliseconds,
                out observedGuardAttemptAt);
        }

        guardActive = defensiveUtility.ObserveGuardSuppression(
            exactGuardActive,
            observedGuardAttemptAt,
            guardObservationNow,
            hardReset).SuppressDirectActionHelpers;
        now = guardObservationNow;
        var highPressureStunObserved = defensiveUtilitiesConfigurationEnabled &&
                                        configuration.GuardOnStunPressure &&
                                        highPressure &&
                                        HasActiveStatus(
                                            localPlayer,
                                            EnemyCombatConstants.PvPStunStatusId);
        var hasPurifyRemovableCrowdControl = HasAnyPurifyRemovableCrowdControl(localPlayer);
        var anyWarningEnabled = configuration.ShowPersonalWarnings &&
                                (configuration.WarnWildfire ||
                                 configuration.WarnDeathWarrant ||
                                 configuration.WarnMarksmanSpite ||
                                 configuration.WarnPurifiableCrowdControl);
        var shouldScanStatuses = configuration.Enabled &&
                                 isSupportedPvPContext &&
                                 alive &&
                                  (anyWarningEnabled ||
                                   (configuration.ExperimentalPurifyOnNextKey &&
                                    anyPurifyAutomationEnabled) ||
                                   defensiveUtilitiesConfigurationEnabled);
        var observed = shouldScanStatuses
            ? ScanExactStatuses(localPlayer, now)
            : [];
        var shouldCaptureMachinistLimitBreak = configuration.Enabled &&
                                               configuration.ShowPersonalWarnings &&
                                               configuration.WarnMarksmanSpite &&
                                               metadata.MarksmanSpiteVerified &&
                                               isSupportedPvPContext &&
                                               alive &&
                                               localPlayer is not null;
        machinistLimitBreakCapture.SetMachinistLocalEntityId(
            shouldCaptureMachinistLimitBreak ? localPlayer!.EntityId : 0);
        if (shouldCaptureMachinistLimitBreak && localPlayer is not null)
        {
            // The hook can enqueue while this framework scan is in progress. Refresh
            // the clock immediately before draining so a new event is never mistaken
            // for a future timestamp and discarded.
            now = Environment.TickCount64;
            AppendMachinistLimitBreakThreat(observed, localPlayer, context, now);
        }
        else
            ClearMachinistLimitBreakThreat();
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

        var regularPurifyConfigurationEnabled = configuration.Enabled &&
                                                configuration.ExperimentalPurifyOnNextKey &&
                                                anyPurifyAutomationEnabled &&
                                                metadata.PurifyVerified &&
                                                !guardActive;
        var pressureStunPurifyConfigurationEnabled = defensiveUtilitiesConfigurationEnabled &&
                                                      configuration.GuardOnStunPressure &&
                                                      highPressureStunObserved &&
                                                      metadata.PurifyVerified &&
                                                      !guardActive;
        var purifyStatus = SelectPurifyStatus(
            observed,
            now,
            regularPurifyConfigurationEnabled,
            pressureStunPurifyConfigurationEnabled,
            out var purifyStatusCurrentlyObserved);
        var purifyConfigurationEnabled = regularPurifyConfigurationEnabled ||
                                         pressureStunPurifyConfigurationEnabled;
        var allowPurifyHeldGameplayKey =
            (regularPurifyConfigurationEnabled && configuration.PurifyOnHeldGameplayKey) ||
            (pressureStunPurifyConfigurationEnabled && configuration.DefensiveUtilitiesOnHeldKey);
        var allyRescueConfigurationEnabled = configuration.Enabled &&
                                             configuration.ExperimentalAllyRescueOnNextKey &&
                                             metadata.AllyRescueStatusesVerified &&
                                             isCrystallineConflict &&
                                             !guardActive;
        var miracleInterceptConfigurationEnabled = configuration.Enabled &&
                                                     configuration.EnableReactiveCcUtilities &&
                                                     isCrystallineConflict &&
                                                     !guardActive;
        var ninjaSeitonConfigurationEnabled = configuration.Enabled &&
                                              configuration.EnableNinjaSeitonOnFreshGameplayKey;
        var scholarCriticalStrategyConfigurationEnabled = configuration.Enabled &&
                                                           configuration.EnableScholarCriticalStrategyOnHeldKey;
        var localJobId = localPlayer?.ClassJob.IsValid == true
            ? localPlayer.ClassJob.RowId
            : 0;
        var scholarCriticalStrategyHeldEnabled =
            scholarCriticalStrategyConfigurationEnabled &&
            isCrystallineConflict &&
            metadata.ScholarCriticalStrategyVerified &&
            !guardActive &&
            localJobId == ScholarCriticalStrategyRules.ScholarJobId;
        var pressureEscapeSprintHeldEnabled = configuration.Enabled &&
                                              configuration.EnablePressureEscapeSprintOnHeldKey &&
                                              isCrystallineConflict &&
                                              !guardActive;
        var emergencyInputFrame = emergencyInput.Observe(
            !hardReset &&
            alive &&
            isSupportedPvPContext &&
            (purifyConfigurationEnabled ||
             defensiveUtilitiesConfigurationEnabled ||
             allyRescueConfigurationEnabled ||
             miracleInterceptConfigurationEnabled ||
             (ninjaSeitonConfigurationEnabled &&
              isCrystallineConflict &&
              metadata.SeitonVerified &&
              !guardActive) ||
             scholarCriticalStrategyHeldEnabled ||
             pressureEscapeSprintHeldEnabled),
            allowPurifyHeldGameplayKey,
            defensiveUtilitiesConfigurationEnabled && configuration.DefensiveUtilitiesOnHeldKey,
            allyRescueConfigurationEnabled && configuration.AllyRescueOnHeldGameplayKey,
            miracleInterceptConfigurationEnabled && configuration.ReactiveCcOnHeldKey,
            scholarCriticalStrategyHeldEnabled,
            pressureEscapeSprintHeldEnabled);
        var purify = emergencyPurify.Observe(
            localPlayer,
            isSupportedPvPContext,
            purifyConfigurationEnabled,
            allowPurifyHeldGameplayKey,
            purifyStatus,
            purifyStatusCurrentlyObserved,
            resilienceActive,
            now,
            configuration.ExperimentalPurifyBufferMilliseconds,
            emergencyInputFrame,
            hardReset);
        // Self-Purify always observes and claims the shared physical generation
        // first. If it arms or dispatches, Ally Rescue is cancelled for this frame
        // as well; this prevents an older rescue buffer and a new self-Purify from
        // ever producing two helper actions together.
        var purifyClaimedPriority =
            EmergencyActionPriorityRules.SelfPurifyClaimsPriority(
                purify.Decision,
                purify.InputTrigger);
        var defense = defensiveUtility.Observe(
            localPlayer,
            isCrystallineConflict,
            defensiveUtilitiesConfigurationEnabled,
            configuration.DefensiveUtilitiesOnHeldKey,
            configuration.GuardOnStunPressure,
            configuration.PreGuardOnLowHpPressure,
            configuration.PaladinGuardianLowAlly,
            pressureKnown,
            incomingEnemyCount,
            highPressureStunObserved,
            purify.UseActionAttempted,
            resilienceActive,
            hasPurifyRemovableCrowdControl,
            guardActive,
            purifyClaimedPriority,
            emergencyInputFrame,
            now,
            hardReset);
        // Guardian may return client-accepted a millisecond after this frame's
        // original timestamp. Refresh before handing the exact episode to the
        // same-frame communication consumer so it is never misclassified as a
        // future event.
        now = Math.Max(now, Environment.TickCount64);
        guardianCommunication.Observe(
            localPlayer,
            context,
            defense.LastAcceptedGuardianEpisode,
            now,
            hardReset);
        var defensiveUtilityClaimedPriority = defense.InputClaimed;
        now = Environment.TickCount64;
        var pressureEscape = pressureEscapeSprint.Observe(
            localPlayer,
            isCrystallineConflict,
            configuration.Enabled && configuration.ShowHighPressureWarning,
            configuration.Enabled && configuration.PlayHighPressureWarningSound,
            configuration.HighPressureWarningSoundId,
            configuration.Enabled && configuration.EnablePressureEscapeSprintOnHeldKey,
            guardActive,
            purifyClaimedPriority || defensiveUtilityClaimedPriority,
            emergencyInputFrame,
            now,
            hardReset || !alive);
        var pressureEscapeClaimedPriority = pressureEscape.InputClaimed;
        var rescue = allyRescue.Observe(
            localPlayer,
            isCrystallineConflict,
            allyRescueConfigurationEnabled &&
            !purifyClaimedPriority &&
            !defensiveUtilityClaimedPriority &&
            !pressureEscapeClaimedPriority,
            configuration.AllyRescueOnHeldGameplayKey,
            emergencyInputFrame,
            now,
            AllyRescueBufferRules.DefaultBufferMilliseconds,
            hardReset);
        var allyRescueClaimedPriority =
            EmergencyActionPriorityRules.AllyRescueClaimsPriority(
                rescue.Decision,
                rescue.InputTrigger);
        // The native hook may enqueue after this framework scan began. Refresh
        // the monotonic clock immediately before draining so a same-frame start
        // marker is never rejected as if it came from the future.
        now = Environment.TickCount64;
        var miracle = miracleIntercept.Observe(
            localPlayer,
            isCrystallineConflict,
            miracleInterceptConfigurationEnabled,
            configuration.ReactiveCcOnHeldKey,
            !purifyClaimedPriority &&
            !defensiveUtilityClaimedPriority &&
            !pressureEscapeClaimedPriority &&
            !allyRescueClaimedPriority,
            configuration.MiracleInterceptMchLimitBreak,
            configuration.MiracleInterceptSamZantetsuken,
            configuration.MiracleInterceptViperNest,
            configuration.ReactiveCcDancerLimitBreak,
            configuration.ReactiveCcAfterEnemyPurify,
            metadata.MarksmanSpiteVerified,
            metadata.ZantetsukenVerified,
            metadata.FuriousBacklashVerified,
            metadata.MiracleOfNatureActionVerified,
            metadata.PurifyVerified,
            emergencyInputFrame,
            now,
            hardReset);
        var ninja = ninjaSeiton.Observe(
            localPlayer,
            isCrystallineConflict,
            ninjaSeitonConfigurationEnabled,
            metadata.SeitonVerified,
            guardActive,
            purifyClaimedPriority ||
            defensiveUtilityClaimedPriority ||
            pressureEscapeClaimedPriority ||
            allyRescueClaimedPriority ||
            miracle.UseActionAttempted ||
            emergencyInputFrame.IsConsumed,
            emergencyInputFrame,
            now,
            hardReset);
        var scholar = scholarCriticalStrategy.Observe(
            localPlayer,
            isCrystallineConflict,
            scholarCriticalStrategyConfigurationEnabled,
            metadata.ScholarCriticalStrategyVerified,
            guardActive,
            purifyClaimedPriority ||
            defensiveUtilityClaimedPriority ||
            pressureEscapeClaimedPriority ||
            allyRescueClaimedPriority ||
            miracle.UseActionAttempted ||
            ninja.InputClaimed ||
            emergencyInputFrame.IsConsumed,
            emergencyInputFrame,
            now,
            hardReset);
        monkEarthReply.Observe(
            localPlayer,
            isSupportedPvPContext,
            configuration.Enabled &&
            configuration.EnableMonkEarthReplyHelper &&
            !guardActive,
            metadata.MonkEarthReplyVerified,
            configuration.MonkEarthReplyOnLowHp,
            configuration.MonkEarthReplyBeforeExpiry,
            configuration.MonkEarthReplyHpPercent,
            configuration.MonkEarthReplyExpirySeconds,
            purifyClaimedPriority ||
            defense.InputClaimed ||
            pressureEscapeClaimedPriority ||
            rescue.UseActionAttempted ||
            miracle.UseActionAttempted ||
            ninja.InputClaimed ||
            scholar.InputClaimed,
            now,
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

    private void AppendMachinistLimitBreakThreat(
        ICollection<ObservedPersonalStatus> observed,
        IPlayerCharacter localPlayer,
        SupportedPvPContext context,
        long nowMilliseconds)
    {
        while (machinistLimitBreakCapture.TryDequeue(out var warning))
        {
            var eventNow = Environment.TickCount64;
            if (warning.TargetEntityId != localPlayer.EntityId ||
                warning.ObservedAtMilliseconds > eventNow ||
                eventNow - warning.ObservedAtMilliseconds > 1_000 ||
                !MachinistLimitBreakThreatResolver.IsVerifiedOpponent(
                    objectTable,
                    warning.CasterEntityId,
                    localPlayer,
                    context))
            {
                continue;
            }

            var duplicate = machinistLimitBreakThreat is { } current &&
                            current.CasterEntityId == warning.CasterEntityId &&
                            current.GlobalSequence == warning.GlobalSequence &&
                            current.SourceSequence == warning.SourceSequence;
            if (duplicate) continue;

            machinistLimitBreakThreat = new MachinistLimitBreakThreatState(
                warning.CasterEntityId,
                warning.GlobalSequence,
                warning.SourceSequence,
                NextInstanceToken(),
                warning.ObservedAtMilliseconds,
                SaturatingAdd(
                    warning.ObservedAtMilliseconds,
                    EnemyCombatConstants.MarksmanSpiteWarningDurationMilliseconds));
            Interlocked.Increment(ref acceptedMachinistLimitBreakWarnings);
            if (configuration.MchLimitBreakSoundEnabled)
            {
                machinistLimitBreakWarningSound.TryPlayThreat(
                    machinistLimitBreakThreat.Value.InstanceToken,
                    Math.Clamp(configuration.MchLimitBreakSoundId, 1, 16),
                    eventNow);
            }
        }

        nowMilliseconds = Environment.TickCount64;
        if (machinistLimitBreakThreat is not { } threat ||
            threat.ExpiresAtMilliseconds <= nowMilliseconds)
        {
            machinistLimitBreakThreat = null;
            return;
        }

        observed.Add(new ObservedPersonalStatus(
            PersonalStatusDefinitions.MarksmanSpite,
            threat.CasterEntityId,
            threat.InstanceToken,
            Math.Max(1, threat.ExpiresAtMilliseconds - nowMilliseconds),
            threat.ExpiresAtMilliseconds));
    }

    private void ClearMachinistLimitBreakThreat()
    {
        machinistLimitBreakCapture.ClearWarnings();
        machinistLimitBreakThreat = null;
    }

    private PurifyCcStatusInstance? SelectPurifyStatus(
        IReadOnlyList<ObservedPersonalStatus> observed,
        long nowMilliseconds,
        bool regularPurifyEnabled,
        bool pressureStunPurifyEnabled,
        out bool currentlyObserved)
    {
        currentlyObserved = false;
        var purifiable = observed
            .Where(status =>
                status.Definition.CanTriggerPurifyBuffer &&
                IsPurifyStatusEnabled(
                    status.Definition.StatusId,
                    regularPurifyEnabled,
                    pressureStunPurifyEnabled))
            .ToArray();
        var tracked = emergencyPurify.TrackedStatusInstance;
        if (tracked is not null &&
            IsPurifyStatusEnabled(
                tracked.Value.StatusId,
                regularPurifyEnabled,
                pressureStunPurifyEnabled))
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
            PersonalStatusFeature.MarksmanSpite => configuration.WarnMarksmanSpite,
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

    private bool IsPurifyStatusEnabled(
        uint statusId,
        bool regularPurifyEnabled,
        bool pressureStunPurifyEnabled) =>
        (regularPurifyEnabled && IsPurifyAutomationEnabled(statusId)) ||
        (pressureStunPurifyEnabled && statusId == EnemyCombatConstants.PvPStunStatusId);

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
        emergencyInput.Reset();
        emergencyPurify.Reset();
        defensiveUtility.Reset();
        pressureEscapeSprint.Reset();
        guardianCommunication.Reset();
        allyRescue.Reset();
        miracleIntercept.Reset();
        ninjaSeiton.Reset();
        scholarCriticalStrategy.Reset();
        monkEarthReply.Reset();
        Interlocked.Exchange(ref snapshot, PersonalAlertSnapshot.Inactive);
    }

    private void ClearStatusTracking()
    {
        instanceTokens.Clear();
        lastPresentations.Clear();
        pulseStartedAt.Clear();
        machinistLimitBreakWarningSound.Reset();
        ClearMachinistLimitBreakThreat();
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
                float.IsFinite(status.RemainingTime) &&
                status.RemainingTime > 0f)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasAnyPurifyRemovableCrowdControl(IPlayerCharacter? player) =>
        HasActiveStatus(player, EnemyCombatConstants.PvPStunStatusId) ||
        HasActiveStatus(player, EnemyCombatConstants.PvPHeavyStatusId) ||
        HasActiveStatus(player, EnemyCombatConstants.PvPBindStatusId) ||
        HasActiveStatus(player, EnemyCombatConstants.PvPSilenceStatusId) ||
        HasActiveStatus(player, EnemyCombatConstants.DeepFreezeStatusId) ||
        HasActiveStatus(player, EnemyCombatConstants.MiracleOfNatureStatusId);

    private static long SaturatingAdd(long left, long right) =>
        left > long.MaxValue - right ? long.MaxValue : left + right;

    private readonly record struct ObservedStatusKey(uint StatusId, uint SourceId);

    private readonly record struct StatusIdentityState(ulong Token, long LastSeenAtMilliseconds);

    private readonly record struct StatusPulseKey(uint StatusId, ulong InstanceToken);

    private readonly record struct MachinistLimitBreakThreatState(
        uint CasterEntityId,
        uint GlobalSequence,
        ushort SourceSequence,
        ulong InstanceToken,
        long ObservedAtMilliseconds,
        long ExpiresAtMilliseconds);

    private sealed record ObservedPersonalStatus(
        PersonalStatusDefinition Definition,
        uint SourceId,
        ulong InstanceToken,
        long RemainingMilliseconds,
        long ExpiresAtMilliseconds);
}
