using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Party;
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

internal readonly record struct SamuraiReactiveCaptureDiagnostics(
    bool CaptureRunning,
    int QueueDepth,
    long CapturedSignals,
    long DroppedSignals,
    int ActionEffectQueueDepth,
    long CapturedActionEffects,
    long DroppedActionEffects,
    int FeatureGeneration);

internal sealed class PersonalStatusService : IDisposable
{
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly ITargetManager targetManager;
    private readonly IFramework framework;
    private readonly IDutyState dutyState;
    private readonly IPluginLog log;
    private readonly PluginConfiguration configuration;
    private readonly PvPMetadataValidation metadata;
    private readonly SamuraiReactiveMetadataValidation samuraiReactiveMetadata;
    private readonly TargetPressureTracker pressureTracker;
    private readonly NearAssistRedirector nearAssist;
    private readonly EmergencyActionInputCoordinator emergencyInput;
    private readonly CriticalUtilityCoordinationService criticalUtilityCoordination;
    private readonly HeldCastCancellationService heldCastCancellation;
    private readonly EmergencyPurifyProbe emergencyPurify;
    private readonly DefensiveUtilityProbe defensiveUtility;
    private readonly SmartRecuperateProbe smartRecuperate;
    private readonly EmergencyTeleportProbe emergencyTeleport;
    private readonly PressureEscapeSprintProbe pressureEscapeSprint;
    private readonly GuardianCommunicationService guardianCommunication;
    private readonly AllyRescueProbe allyRescue;
    private readonly MiracleInterceptProbe miracleIntercept;
    private readonly SamuraiReactiveCounterCcProbe samuraiReactive;
    private readonly SmartKardiaProbe smartKardia;
    private readonly NinjaGuardShukuchiProbe ninjaGuardShukuchi;
    private readonly NinjaSeitonDispatchProbe ninjaSeiton;
    private readonly ViperSerpentTailProbe viperSerpentTail;
    private readonly GunbreakerContinuationProbe gunbreakerContinuation;
    private readonly ScholarCriticalStrategyProbe scholarCriticalStrategy;
    private readonly ScholarSpreadProbe scholarSpread;
    private readonly MonkEarthReplyProbe monkEarthReply;
    private readonly DarkKnightPlungeProbe darkKnightPlunge;
    private readonly DarkKnightShadowbringerProbe darkKnightShadowbringer;
    private readonly MonkHeldComboProbe monkHeldCombo;
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
    private LocalMpWarningState localMpWarningState = LocalMpWarningState.Initial;
    private MachinistLimitBreakThreatState? machinistLimitBreakThreat;
    private long consumedAutoGuardPopupToken;
    private long nextAutoGuardSoundPreviewAt;
    private uint activeTerritory = uint.MaxValue;
    private ulong activeLocalPlayerId;
    private SupportedPvPContext activeContext;
    private bool started;
    private bool disposed;

    internal PersonalStatusService(
        IClientState clientState,
        IObjectTable objectTable,
        ITargetManager targetManager,
        IPartyList partyList,
        IFramework framework,
        IDutyState dutyState,
        IKeyState keyState,
        IDataManager dataManager,
        ISigScanner sigScanner,
        TargetPressureTracker pressureTracker,
        ExecuteTracker executeTracker,
        NearAssistRedirector nearAssist,
        MachinistLimitBreakCapture machinistLimitBreakCapture,
        IPluginLog log,
        PluginConfiguration configuration,
        PvPMetadataValidation metadata,
        SamuraiReactiveMetadataValidation samuraiReactiveMetadata,
        ReviewedPvpCommandDispatcher commands,
        CriticalUtilityCoordinationService criticalUtilityCoordination)
    {
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.targetManager = targetManager;
        this.framework = framework;
        this.dutyState = dutyState;
        this.log = log;
        this.configuration = configuration;
        this.metadata = metadata;
        this.samuraiReactiveMetadata = samuraiReactiveMetadata;
        this.pressureTracker = pressureTracker;
        this.nearAssist = nearAssist;
        this.criticalUtilityCoordination = criticalUtilityCoordination;
        emergencyInput = new EmergencyActionInputCoordinator(
            keyState,
            criticalUtilityCoordination.ClaimCurrentFrame);
        heldCastCancellation = new HeldCastCancellationService(log);
        emergencyPurify = new EmergencyPurifyProbe(log);
        defensiveUtility = new DefensiveUtilityProbe(
            objectTable,
            dataManager,
            pressureTracker,
            nearAssist,
            log,
            metadata);
        smartRecuperate = new SmartRecuperateProbe(
            clientState,
            objectTable,
            dutyState,
            configuration,
            nearAssist,
            log);
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
            nearAssist.VerifiedCcBrakeActionIds,
            nearAssist.VerifiedCcBrakeStatusIds,
            executeTracker,
            pressureTracker,
            nearAssist,
            machinistLimitBreakCapture,
            log,
            metadata,
            configuration);
        samuraiReactive = new SamuraiReactiveCounterCcProbe(
            objectTable,
            executeTracker,
            nearAssist,
            log,
            samuraiReactiveMetadata,
            configuration);
        smartKardia = new SmartKardiaProbe(
            clientState,
            objectTable,
            partyList,
            dutyState,
            configuration,
            pressureTracker,
            nearAssist,
            log);
        ninjaGuardShukuchi = new NinjaGuardShukuchiProbe(
            clientState,
            objectTable,
            targetManager,
            pressureTracker,
            nearAssist,
            log);
        ninjaSeiton = new NinjaSeitonDispatchProbe(
            objectTable,
            executeTracker,
            nearAssist,
            log);
        emergencyTeleport = new EmergencyTeleportProbe(
            clientState,
            objectTable,
            dutyState,
            configuration,
            pressureTracker,
            nearAssist,
            log);
        viperSerpentTail = new ViperSerpentTailProbe(
            clientState,
            objectTable,
            nearAssist,
            log);
        gunbreakerContinuation = new GunbreakerContinuationProbe(
            clientState,
            objectTable,
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
        scholarSpread = new ScholarSpreadProbe(
            clientState,
            objectTable,
            dutyState,
            configuration,
            nearAssist,
            machinistLimitBreakCapture,
            log);
        monkEarthReply = new MonkEarthReplyProbe(nearAssist, log);
        darkKnightPlunge = new DarkKnightPlungeProbe(
            clientState,
            dutyState,
            objectTable,
            executeTracker,
            nearAssist,
            log);
        darkKnightShadowbringer = new DarkKnightShadowbringerProbe(
            clientState,
            objectTable,
            executeTracker,
            pressureTracker,
            nearAssist,
            log);
        monkHeldCombo = new MonkHeldComboProbe(
            clientState,
            objectTable,
            nearAssist,
            sigScanner,
            log);
        this.machinistLimitBreakCapture = machinistLimitBreakCapture;
        machinistLimitBreakWarningSound = new MachinistLimitBreakWarningSound(log);
    }

    internal PersonalAlertSnapshot Snapshot => Volatile.Read(ref snapshot);
    internal DefensiveUtilityProbeSnapshot DefensiveUtilityDiagnostics => defensiveUtility.Snapshot;
    internal AutoGuardProtectionDiagnostics AutoGuardProtectionDiagnostics =>
        nearAssist.AutoGuardProtectionDiagnostics;
    internal SmartRecuperateProbeSnapshot SmartRecuperateDiagnostics => smartRecuperate.Snapshot;
    internal EmergencyTeleportProbeSnapshot EmergencyTeleportDiagnostics =>
        emergencyTeleport.Snapshot;
    internal PressureEscapeSprintProbeSnapshot PressureEscapeDiagnostics =>
        pressureEscapeSprint.Snapshot;
    internal GuardianCommunicationDiagnostics GuardianCommunicationDiagnostics =>
        guardianCommunication.Diagnostics;
    internal AllyRescueProbeSnapshot AllyRescueDiagnostics => allyRescue.Snapshot;
    internal MiracleInterceptProbeSnapshot MiracleInterceptDiagnostics => miracleIntercept.Snapshot;
    internal SamuraiReactiveCounterCcProbeSnapshot SamuraiReactiveDiagnostics =>
        samuraiReactive.Snapshot;
    internal SamuraiReactiveMetadataValidation SamuraiReactiveMetadata =>
        samuraiReactiveMetadata;
    internal SamuraiReactiveCaptureDiagnostics SamuraiReactiveCaptureDiagnostics => new(
        machinistLimitBreakCapture.IsRunning,
        machinistLimitBreakCapture.SamuraiReactiveProtectionQueueDepth,
        machinistLimitBreakCapture.CapturedSamuraiReactiveProtectionSignals,
        machinistLimitBreakCapture.DroppedSamuraiReactiveProtectionSignals,
        machinistLimitBreakCapture.SamuraiReactiveActionEffectQueueDepth,
        machinistLimitBreakCapture.CapturedSamuraiReactiveActionEffects,
        machinistLimitBreakCapture.DroppedSamuraiReactiveActionEffects,
        machinistLimitBreakCapture.CurrentSamuraiReactiveGeneration);
    internal SmartKardiaProbeSnapshot SmartKardiaDiagnostics => smartKardia.Snapshot;
    internal NinjaGuardShukuchiProbeSnapshot NinjaGuardShukuchiDiagnostics =>
        ninjaGuardShukuchi.Snapshot;
    internal NinjaSeitonDispatchProbeSnapshot NinjaSeitonDiagnostics => ninjaSeiton.Snapshot;
    internal ViperSerpentTailProbeSnapshot ViperSerpentTailDiagnostics =>
        viperSerpentTail.Snapshot;
    internal GunbreakerContinuationProbeSnapshot GunbreakerContinuationDiagnostics =>
        gunbreakerContinuation.Snapshot;
    internal ScholarCriticalStrategyProbeSnapshot ScholarCriticalStrategyDiagnostics =>
        scholarCriticalStrategy.Snapshot;
    internal ScholarSpreadProbeSnapshot ScholarSpreadDiagnostics => scholarSpread.Snapshot;
    internal MonkEarthReplyProbeSnapshot MonkEarthReplyDiagnostics => monkEarthReply.Snapshot;
    internal DarkKnightPlungeProbeSnapshot DarkKnightPlungeDiagnostics =>
        darkKnightPlunge.Snapshot;
    internal DarkKnightShadowbringerProbeSnapshot DarkKnightShadowbringerDiagnostics =>
        darkKnightShadowbringer.Snapshot;
    internal MonkHeldComboProbeSnapshot MonkHeldComboDiagnostics => monkHeldCombo.Snapshot;
    internal HeldCastCancellationSnapshot HeldCastCancellationDiagnostics =>
        heldCastCancellation.Snapshot;
    internal CriticalUtilityCoordinationSnapshot CriticalUtilityCoordinationDiagnostics =>
        criticalUtilityCoordination.Snapshot;
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

    internal bool PlayAutoGuardActivationSoundPreview()
    {
        var now = Environment.TickCount64;
        if (now < nextAutoGuardSoundPreviewAt) return false;
        nextAutoGuardSoundPreviewAt = SaturatingAdd(now, 350);
        return MachinistLimitBreakWarningSound.TryPlayShared(
            Math.Clamp(configuration.AutoGuardActivationSoundId, 1, 16),
            log,
            "Seiton Sense Auto-Guard activation sound preview failed closed.");
    }

    internal bool PlayHighPressureWarningSoundPreview() =>
        pressureEscapeSprint.PlayWarningSoundPreview(
            Math.Clamp(configuration.HighPressureWarningSoundId, 1, 16));

    internal bool PlayLocalMpWarning4000SoundPreview() =>
        PlayLocalMpWarningSound(
            configuration.LocalMpWarning4000SoundId,
            "4,000 MP warning preview");

    internal bool PlayLocalMpWarning2000SoundPreview() =>
        PlayLocalMpWarningSound(
            configuration.LocalMpWarning2000SoundId,
            "2,000 MP warning preview");

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
                "Seiton Sense shared action-effect capture is unavailable; dependent MCH, pressure, reactive, and Scholar signals are disabled while other features remain active.");
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
        ResetRuntime();
        scholarSpread.Dispose();
        machinistLimitBreakCapture.Dispose();
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
            machinistLimitBreakCapture.SetSamuraiReactiveLocalEntityId(0);
            machinistLimitBreakCapture.ClearSamuraiReactiveProtectionSignals();
            machinistLimitBreakThreat = null;
            emergencyInput.Reset();
            criticalUtilityCoordination.Clear("Personal-status scan failed closed");
            HeldActionRetryRules.ConfigureLatencyResponsePolicy(false, 0);
            localMpWarningState = LocalMpWarningState.Initial;
            var purify = emergencyPurify.FailClosed(now);
            defensiveUtility.FailClosed(now, exception);
            smartRecuperate.FailClosed();
            emergencyTeleport.FailClosed();
            pressureEscapeSprint.FailClosed(now, exception);
            guardianCommunication.FailClosed(now, exception);
            allyRescue.FailClosed(now, exception);
            miracleIntercept.FailClosed(now, exception);
            samuraiReactive.Reset();
            smartKardia.FailClosed();
            ninjaGuardShukuchi.FailClosed();
            ninjaSeiton.FailClosed();
            viperSerpentTail.FailClosed();
            gunbreakerContinuation.FailClosed();
            scholarCriticalStrategy.FailClosed();
            scholarSpread.FailClosed();
            monkEarthReply.FailClosed(now);
            darkKnightPlunge.FailClosed();
            darkKnightShadowbringer.FailClosed();
            monkHeldCombo.FailClosed();
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
            smartRecuperate.Reset();
            emergencyTeleport.Reset();
            pressureEscapeSprint.Reset();
            guardianCommunication.Reset();
            allyRescue.Reset();
            miracleIntercept.Reset();
            samuraiReactive.Reset();
            machinistLimitBreakCapture.SetSamuraiReactiveLocalEntityId(0);
            machinistLimitBreakCapture.ClearSamuraiReactiveProtectionSignals();
            smartKardia.Reset();
            ninjaGuardShukuchi.Reset();
            ninjaSeiton.Reset();
            viperSerpentTail.Reset();
            gunbreakerContinuation.Reset();
            scholarCriticalStrategy.Reset();
            scholarSpread.Reset();
            monkEarthReply.Reset();
            darkKnightPlunge.Reset();
            darkKnightShadowbringer.Reset();
            monkHeldCombo.Reset();
        }

        var isSupportedPvPContext = context != SupportedPvPContext.None;
        var isCrystallineConflict = context == SupportedPvPContext.CrystallineConflict;
        var alive = IsAlive(localPlayer);
        HeldActionRetryRules.ConfigureLatencyResponsePolicy(
            configuration.Enabled &&
            configuration.EnablePvpLatencyResponseHelper &&
            isSupportedPvPContext &&
            alive &&
            !hardReset,
            configuration.PvpLatencyResponseWindowMilliseconds);
        criticalUtilityCoordination.BeginFrame(
            configuration.Enabled,
            configuration.EnablePvpLatencyResponseHelper,
            context,
            alive,
            hardReset);
        ObserveLocalMpWarnings(
            localPlayer,
            isSupportedPvPContext,
            alive,
            hardReset);
        var localJobId = localPlayer?.ClassJob.IsValid == true
            ? localPlayer.ClassJob.RowId
            : 0;
        var isPaladin = localJobId == EnemyCombatConstants.PaladinJobId;
        var isRedMage = localJobId == ReactiveCounterCcProfileRules.RedMageJobId;
        var isBlackMage = localJobId == ReactiveCounterCcProfileRules.BlackMageJobId;
        var isAllyRescueJob = localJobId is EnemyCombatConstants.WhiteMageJobId or
            EnemyCombatConstants.BardJobId;
        var isNinja = ExecuteThreshold.IsNinja(localJobId);
        var isReactiveCcJob =
            isAllyRescueJob || isNinja || isPaladin || isRedMage || isBlackMage;
        var isSage = localJobId == SmartKardiaRules.SageJobId;
        var isScholar = localJobId == ScholarCriticalStrategyRules.ScholarJobId;
        var isMonk = localJobId == MonkEarthReplyRules.MonkJobId;
        var isDarkKnight = localJobId == DarkKnightPlungeRules.DarkKnightJobId;
        var isViper = localJobId == ViperSerpentTailRules.ViperJobId;
        var isGunbreaker = localJobId == GunbreakerContinuationRules.GunbreakerJobId;
        var isSamurai = localJobId == SamuraiReactiveCounterCcRules.SamuraiJobId;
        var isEmergencyTeleportJob =
            EmergencyTeleportRules.TryGetActionForJob(localJobId, out _);
        var anyPurifyAutomationEnabled = AnyPurifyAutomationEnabled();
        var defensiveUtilitiesConfigurationEnabled = configuration.Enabled &&
                                                     configuration.EnableDefensiveUtilities &&
                                                     isCrystallineConflict;
        var paladinGuardianConfigurationEnabled = configuration.Enabled &&
                                                  configuration.PaladinGuardianLowAlly &&
                                                  isCrystallineConflict &&
                                                  isPaladin;
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
                                             isAllyRescueJob &&
                                             !guardActive;
        var miracleInterceptConfigurationEnabled = configuration.Enabled &&
                                                     configuration.EnableReactiveCcUtilities &&
                                                     isSupportedPvPContext &&
                                                     isReactiveCcJob;
        var samuraiCounterCcConfigurationEnabled = configuration.Enabled &&
                                                    configuration.EnableReactiveCcUtilities &&
                                                    configuration.ReactiveCcSamuraiSotenMineuchi &&
                                                    (configuration.ReactiveCcAfterEnemyPurify ||
                                                     configuration.ReactiveCcAfterEnemyGuard) &&
                                                    isSupportedPvPContext &&
                                                    isSamurai;
        var samuraiZantetsukenConfigurationEnabled = configuration.Enabled &&
                                                     configuration.EnableSamuraiZantetsukenOnHeldKey &&
                                                     isSupportedPvPContext &&
                                                     isSamurai;
        var ninjaSeitonConfigurationEnabled = configuration.Enabled &&
                                               configuration.EnableNinjaSeitonOnHeldGameplayKey &&
                                               isCrystallineConflict &&
                                               isNinja;
        var viperSerpentTailConfigurationEnabled = configuration.Enabled &&
                                                    configuration.EnableViperSerpentTailOnHeldKey &&
                                                    isSupportedPvPContext &&
                                                    isViper;
        var gunbreakerContinuationConfigurationEnabled = configuration.Enabled &&
                                                          configuration.EnableGunbreakerContinuationOnHeldKey &&
                                                          isSupportedPvPContext &&
                                                          isGunbreaker;
        var ninjaGuardShukuchiConfigurationEnabled = configuration.Enabled &&
                                                     configuration.EnableNinjaGuardShukuchiOnHeldGameplayKey &&
                                                     isCrystallineConflict &&
                                                     isNinja;
        var smartKardiaConfigurationEnabled = configuration.Enabled &&
                                               configuration.EnableSageKardiaAfterEukrasia &&
                                               isCrystallineConflict &&
                                               isSage;
        var scholarCriticalStrategyConfigurationEnabled = configuration.Enabled &&
                                                           configuration.EnableScholarCriticalStrategyOnHeldKey &&
                                                           isCrystallineConflict &&
                                                           isScholar;
        var scholarSpreadConfigurationEnabled = configuration.Enabled &&
                                                configuration.EnableScholarSpreadOnHeldKey &&
                                                isCrystallineConflict &&
                                                isScholar &&
                                                metadata.ScholarSpreadVerified;
        var darkKnightPlungeConfigurationEnabled = configuration.Enabled &&
                                                    configuration.EnableDarkKnightPlungeOnHeldKey;
        var darkKnightShadowbringerConfigurationEnabled = configuration.Enabled &&
                                                           configuration.EnableDarkKnightShadowbringerOnHeldKey &&
                                                           isSupportedPvPContext &&
                                                           isDarkKnight;
        var monkHeldComboConfigurationEnabled = configuration.Enabled &&
                                                configuration.EnableMonkHeldComboOnHeldKey &&
                                                isSupportedPvPContext &&
                                                isMonk;

        // Keep the shared physical-key observer enabled from stable opt-in gates,
        // not from the current action opportunity. Guard suppresses every direct
        // helper below, but it must not reset/prime away a hold that was already
        // valid before Guard and is still physically down when Guard ends.
        var purifyHeldInputEnabled = configuration.Enabled &&
                                     metadata.PurifyVerified &&
                                     ((configuration.ExperimentalPurifyOnNextKey &&
                                       anyPurifyAutomationEnabled &&
                                       configuration.PurifyOnHeldGameplayKey) ||
                                      (configuration.EnableDefensiveUtilities &&
                                       configuration.GuardOnStunPressure &&
                                       configuration.DefensiveUtilitiesOnHeldKey &&
                                       isCrystallineConflict));
        var defensiveUtilityHeldInputEnabled = defensiveUtilitiesConfigurationEnabled &&
                                                configuration.DefensiveUtilitiesOnHeldKey &&
                                                metadata.GuardVerified;
        var paladinGuardianHeldInputEnabled = paladinGuardianConfigurationEnabled &&
                                              configuration.PaladinGuardianOnHeldKey &&
                                              metadata.GuardVerified &&
                                              metadata.GuardianVerified;
        var smartRecuperateHeldInputEnabled = configuration.Enabled &&
                                               configuration.EnableSmartRecuperateOnHeldKey &&
                                               isSupportedPvPContext &&
                                               metadata.RecuperateVerified;
        var allyRescueHeldInputEnabled = configuration.Enabled &&
                                         configuration.ExperimentalAllyRescueOnNextKey &&
                                         configuration.AllyRescueOnHeldGameplayKey &&
                                         metadata.AllyRescueStatusesVerified &&
                                         isCrystallineConflict &&
                                         isAllyRescueJob;
        var reactiveCcActionMetadataVerified =
            (localJobId == EnemyCombatConstants.WhiteMageJobId &&
             metadata.MiracleOfNatureActionVerified) ||
            (localJobId == EnemyCombatConstants.BardJobId &&
             metadata.SilentNocturneVerified) ||
            (localJobId == EnemyCombatConstants.NinjaJobId &&
             nearAssist.VerifiedCcBrakeActionIds.Contains(
                  EnemyCombatConstants.ForkedRaijuActionId) &&
              nearAssist.VerifiedCcBrakeActionIds.Contains(
                  EnemyCombatConstants.FleetingRaijuActionId)) ||
            (isPaladin &&
             configuration.ReactiveCcPaladinIntervene &&
             nearAssist.VerifiedCcBrakeActionIds.Contains(
                 MiracleInterceptConfirmationRules.InterveneActionId)) ||
            (isRedMage &&
             ((configuration.ReactiveCcRedMageResolution &&
               metadata.RedMageResolutionVerified) ||
              (configuration.ReactiveCcRedMageViceOfThorns &&
               metadata.RedMageViceOfThornsVerified))) ||
            (isBlackMage &&
             configuration.ReactiveCcBlackMageFrostStar &&
             metadata.BlackMageFrostStarVerified);
        var miracleInterceptHeldInputEnabled = configuration.Enabled &&
                                               configuration.EnableReactiveCcUtilities &&
                                               configuration.ReactiveCcOnHeldKey &&
                                               reactiveCcActionMetadataVerified &&
                                               isSupportedPvPContext &&
                                               isReactiveCcJob;
        var samuraiCounterCcHeldInputEnabled =
            samuraiCounterCcConfigurationEnabled &&
            configuration.ReactiveCcOnHeldKey &&
            samuraiReactiveMetadata.CounterCcVerified;
        var samuraiZantetsukenHeldInputEnabled =
            samuraiZantetsukenConfigurationEnabled &&
            samuraiReactiveMetadata.ZantetsukenWorkflowVerified;
        var scholarCriticalStrategyHeldInputEnabled =
            scholarCriticalStrategyConfigurationEnabled &&
            metadata.ScholarCriticalStrategyVerified;
        var emergencyTeleportHeldInputEnabled = configuration.Enabled &&
                                                configuration.EnableEmergencyTeleportOnHeldKey &&
                                                isSupportedPvPContext &&
                                                isEmergencyTeleportJob &&
                                                metadata.IsEmergencyTeleportVerified(localJobId);
        var pressureEscapeSprintHeldInputEnabled = configuration.Enabled &&
                                                   configuration.EnablePressureEscapeSprintOnHeldKey &&
                                                   isCrystallineConflict;
        var darkKnightPlungeHeldInputEnabled = darkKnightPlungeConfigurationEnabled &&
                                               isCrystallineConflict &&
                                               metadata.DarkKnightPlungeVerified &&
                                               isDarkKnight;
        var ninjaSeitonHeldInputEnabled = ninjaSeitonConfigurationEnabled &&
                                          metadata.SeitonVerified;
        var viperSerpentTailHeldInputEnabled =
            viperSerpentTailConfigurationEnabled &&
            metadata.ViperSerpentTailVerified;
        var gunbreakerContinuationHeldInputEnabled =
            gunbreakerContinuationConfigurationEnabled &&
            metadata.GunbreakerContinuationVerified;
        var darkKnightShadowbringerHeldInputEnabled =
            darkKnightShadowbringerConfigurationEnabled &&
            metadata.DarkKnightShadowbringerVerified;
        var monkHeldComboInputEnabled = monkHeldComboConfigurationEnabled &&
                                        metadata.MonkHeldComboVerified;
        var ninjaGuardShukuchiHeldInputEnabled =
            ninjaGuardShukuchiConfigurationEnabled &&
            metadata.PanicShukuchiVerified &&
            metadata.GuardVerified;
        var shouldCaptureSamuraiProtectionSignals =
            !hardReset &&
            alive &&
            localPlayer is not null &&
            samuraiCounterCcConfigurationEnabled &&
            samuraiReactiveMetadata.CounterCcVerified;
        machinistLimitBreakCapture.SetSamuraiReactiveLocalEntityId(
            shouldCaptureSamuraiProtectionSignals ? localPlayer!.EntityId : 0);
        if (shouldCaptureSamuraiProtectionSignals)
        {
            while (machinistLimitBreakCapture
                       .TryDequeueSamuraiReactiveProtectionSignal(out var signal))
            {
                samuraiReactive.EnqueueProtectionSignal(signal);
            }
            while (machinistLimitBreakCapture
                       .TryDequeueSamuraiReactiveActionEffect(out var effect))
            {
                samuraiReactive.EnqueueActionEffectSignal(effect);
            }
        }
        var anyPersistentHeldInputEnabled = purifyHeldInputEnabled ||
                                            defensiveUtilityHeldInputEnabled ||
                                            paladinGuardianHeldInputEnabled ||
                                            smartRecuperateHeldInputEnabled ||
                                            allyRescueHeldInputEnabled ||
                                             miracleInterceptHeldInputEnabled ||
                                             samuraiCounterCcHeldInputEnabled ||
                                             samuraiZantetsukenHeldInputEnabled ||
                                             scholarCriticalStrategyHeldInputEnabled ||
                                             scholarSpreadConfigurationEnabled ||
                                             emergencyTeleportHeldInputEnabled ||
                                             pressureEscapeSprintHeldInputEnabled ||
                                            darkKnightPlungeHeldInputEnabled ||
                                            ninjaGuardShukuchiHeldInputEnabled ||
                                            ninjaSeitonHeldInputEnabled ||
                                            viperSerpentTailHeldInputEnabled ||
                                            gunbreakerContinuationHeldInputEnabled ||
                                            darkKnightShadowbringerHeldInputEnabled ||
                                            monkHeldComboInputEnabled;
        var emergencyInputFrame = emergencyInput.Observe(
            !hardReset &&
            alive &&
            isSupportedPvPContext &&
            (anyPersistentHeldInputEnabled ||
             purifyConfigurationEnabled ||
             defensiveUtilitiesConfigurationEnabled ||
             paladinGuardianConfigurationEnabled ||
             allyRescueConfigurationEnabled ||
                                             miracleInterceptConfigurationEnabled ||
             samuraiCounterCcConfigurationEnabled ||
             samuraiZantetsukenConfigurationEnabled ||
             ninjaGuardShukuchiHeldInputEnabled ||
             ninjaSeitonHeldInputEnabled ||
             viperSerpentTailHeldInputEnabled ||
             gunbreakerContinuationHeldInputEnabled ||
             darkKnightShadowbringerHeldInputEnabled ||
             monkHeldComboInputEnabled ||
             scholarCriticalStrategyHeldInputEnabled ||
             scholarSpreadConfigurationEnabled ||
             emergencyTeleportHeldInputEnabled ||
             smartRecuperateHeldInputEnabled ||
             pressureEscapeSprintHeldInputEnabled ||
             darkKnightPlungeHeldInputEnabled),
            purifyHeldInputEnabled,
            defensiveUtilityHeldInputEnabled,
            paladinGuardianHeldInputEnabled,
            smartRecuperateHeldInputEnabled,
            allyRescueHeldInputEnabled,
            miracleInterceptHeldInputEnabled,
            scholarCriticalStrategyHeldInputEnabled,
            emergencyTeleportHeldInputEnabled,
            pressureEscapeSprintHeldInputEnabled,
            darkKnightPlungeHeldInputEnabled,
            ninjaGuardShukuchiHeldEnabled: ninjaGuardShukuchiHeldInputEnabled,
            ninjaSeitonHeldEnabled: ninjaSeitonHeldInputEnabled,
            viperSerpentTailHeldEnabled: viperSerpentTailHeldInputEnabled,
            gunbreakerContinuationHeldEnabled: gunbreakerContinuationHeldInputEnabled,
            darkKnightShadowbringerHeldEnabled: darkKnightShadowbringerHeldInputEnabled,
            monkHeldComboEnabled: monkHeldComboInputEnabled,
            samuraiCounterCcHeldEnabled: samuraiCounterCcHeldInputEnabled,
            samuraiZantetsukenHeldEnabled: samuraiZantetsukenHeldInputEnabled);
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
        // Self-Purify owns the scheduler while its exact enabled CC/key lease is
        // actionable or waiting at the global native boundary. It no longer
        // consumes that physical hold through release.
        var purifyClaimedPriority = purify.InputClaimed;
        // SAM's exact post-protection sequence is a job helper directly after
        // Purify. Counter-CC runs before Zantetsuken so an accepted Soten can
        // reserve its bounded Mineuchi arrival window without another SAM
        // action inserting animation lock.
        now = Environment.TickCount64;
        var samuraiCounter = samuraiReactive.ObserveCounterCc(
            localPlayer,
            context,
            samuraiCounterCcConfigurationEnabled,
            configuration.ReactiveCcAfterEnemyPurify,
            configuration.ReactiveCcAfterEnemyGuard,
            configuration.ReactiveCcOnHeldKey,
            dispatchAllowed:
                !guardActive &&
                !purifyClaimedPriority &&
                !emergencyInputFrame.IsConsumed,
            configuration.ReactiveCcSamuraiSotenMaximumRangeYalms,
            emergencyInputFrame,
            now,
            hardReset);
        now = Environment.TickCount64;
        var samurai = samuraiZantetsukenConfigurationEnabled
            ? samuraiReactive.ObserveZantetsuken(
                localPlayer,
                context,
                enabled: true,
                allowHeldGameplayKey: true,
                dispatchAllowed:
                    !guardActive &&
                    !purifyClaimedPriority &&
                    !samuraiCounter.InputClaimed &&
                    !emergencyInputFrame.IsConsumed,
                emergencyInputFrame,
                now,
                hardReset)
            : samuraiReactive.ResetZantetsukenLane();
        var samuraiClaimedPriority = samurai.InputClaimed;
        // The scheduler is ordered by the next action which may be
        // client-accepted, not by ownership of the whole physical hold.
        // Purify is absolute. Auto-Seiton is the next action-level priority;
        // once it claims this frame, every later held helper observes that
        // claim and stays armed for a later free frame.
        now = Environment.TickCount64;
        var ninja = ninjaSeiton.Observe(
            localPlayer,
            isCrystallineConflict,
            ninjaSeitonConfigurationEnabled,
            metadata.SeitonVerified,
            guardActive,
            purifyClaimedPriority ||
            samuraiClaimedPriority ||
            emergencyInputFrame.IsConsumed,
            emergencyInputFrame,
            now,
            hardReset);
        now = Environment.TickCount64;
        var viper = viperSerpentTail.Observe(
            localPlayer,
            context,
            viperSerpentTailConfigurationEnabled,
            metadata.ViperSerpentTailVerified,
            metadata.WolvesDenStrikingDummyVerified,
            guardActive,
            purifyClaimedPriority ||
            samuraiClaimedPriority ||
            ninja.InputClaimed ||
            emergencyInputFrame.IsConsumed,
            emergencyInputFrame,
            now,
            hardReset);
        now = Environment.TickCount64;
        var gunbreaker = gunbreakerContinuation.Observe(
            localPlayer,
            context,
            gunbreakerContinuationConfigurationEnabled,
            metadata.GunbreakerContinuationVerified,
            guardActive,
            purifyClaimedPriority ||
            samuraiClaimedPriority ||
            ninja.InputClaimed ||
            viper.InputClaimed ||
            emergencyInputFrame.IsConsumed,
            emergencyInputFrame,
            now,
            hardReset);
        // Reactive counter-CC and the remaining job-specific helpers follow
        // Auto-Seiton, then generic self-healing/defense. A held key remains
        // consent for a later distinct episode after an accepted action clears.
        // The native hook may enqueue after this framework scan began. Refresh
        // the monotonic clock immediately before draining so a same-frame start
        // marker is never rejected as if it came from the future.
        now = Environment.TickCount64;
        var miracle = miracleIntercept.Observe(
            localPlayer,
            isCrystallineConflict,
            miracleInterceptConfigurationEnabled,
            configuration.ReactiveCcOnHeldKey,
            !guardActive &&
            !purifyClaimedPriority &&
            !samuraiClaimedPriority &&
            !ninja.InputClaimed &&
            !viper.InputClaimed &&
            !gunbreaker.InputClaimed &&
            !emergencyInputFrame.IsConsumed,
            configuration.MiracleInterceptMchLimitBreak,
            configuration.MiracleInterceptSamZantetsuken,
            configuration.MiracleInterceptViperNest,
            configuration.ReactiveCcDancerLimitBreak,
            configuration.ReactiveCcAfterEnemyPurify,
            configuration.ReactiveCcAfterEnemyGuard,
            metadata.MarksmanSpiteVerified,
            metadata.ZantetsukenVerified,
            metadata.FuriousBacklashVerified,
            metadata.MiracleOfNatureActionVerified,
            metadata.PurifyVerified,
            emergencyInputFrame,
            now,
            hardReset,
            enablePaladinIntervene: configuration.ReactiveCcPaladinIntervene,
            paladinInterveneMaximumRangeYalms:
                configuration.ReactiveCcPaladinInterveneMaximumRangeYalms,
            enableRedMageResolution: configuration.ReactiveCcRedMageResolution,
            redMageResolutionMetadataVerified: metadata.RedMageResolutionVerified,
            isWolvesDenTesting: context == SupportedPvPContext.WolvesDen,
            wolvesDenCurrentHardTarget: targetManager.Target as IPlayerCharacter,
            enableRedMageViceOfThorns:
                configuration.ReactiveCcRedMageViceOfThorns,
            redMageViceOfThornsMetadataVerified:
                metadata.RedMageViceOfThornsVerified,
            enableBlackMageFrostStar:
                configuration.ReactiveCcBlackMageFrostStar,
            blackMageFrostStarMetadataVerified:
                metadata.BlackMageFrostStarVerified);
        var rescue = allyRescue.Observe(
            localPlayer,
            isCrystallineConflict,
            allyRescueConfigurationEnabled,
            configuration.AllyRescueOnHeldGameplayKey,
            emergencyInputFrame,
            now,
            AllyRescueBufferRules.DefaultBufferMilliseconds,
            hardReset,
            dispatchAllowed:
                !purifyClaimedPriority &&
                !samuraiClaimedPriority &&
                !ninja.InputClaimed &&
                !viper.InputClaimed &&
                !gunbreaker.InputClaimed &&
                !miracle.InputClaimed &&
                !emergencyInputFrame.IsConsumed);
        var allyRescueClaimedPriority = rescue.InputClaimed;
        var defense = defensiveUtility.ObserveGuardian(
            localPlayer,
            isCrystallineConflict,
            paladinGuardianConfigurationEnabled,
            configuration.PaladinGuardianOnHeldKey,
            guardActive,
            purifyClaimedPriority ||
            samuraiClaimedPriority ||
            ninja.InputClaimed ||
            viper.InputClaimed ||
            gunbreaker.InputClaimed ||
            allyRescueClaimedPriority ||
            miracle.InputClaimed ||
            emergencyInputFrame.IsConsumed,
            emergencyInputFrame,
            now,
            hardReset,
            beginsFrame: true);
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
        var guardianClaimedPriority = defense.InputClaimed;
        now = Environment.TickCount64;
        var guardShukuchi = ninjaGuardShukuchi.Observe(
            localPlayer,
            isCrystallineConflict,
            ninjaGuardShukuchiConfigurationEnabled,
            metadata.PanicShukuchiVerified && metadata.GuardVerified,
            guardActive,
            purifyClaimedPriority ||
            samuraiClaimedPriority ||
            ninja.InputClaimed ||
            viper.InputClaimed ||
            gunbreaker.InputClaimed ||
            allyRescueClaimedPriority ||
            miracle.InputClaimed ||
            guardianClaimedPriority ||
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
            samuraiClaimedPriority ||
            viper.InputClaimed ||
            gunbreaker.InputClaimed ||
            allyRescueClaimedPriority ||
            miracle.InputClaimed ||
            guardianClaimedPriority ||
            guardShukuchi.InputClaimed ||
            ninja.InputClaimed ||
            emergencyInputFrame.IsConsumed,
            emergencyInputFrame,
            now,
            hardReset);
        now = Environment.TickCount64;
        var shadowbringerPre = darkKnightShadowbringer.ObservePriorityDarkArts(
            localPlayer,
            context,
            darkKnightShadowbringerConfigurationEnabled,
            metadata.DarkKnightShadowbringerVerified,
            metadata.WolvesDenStrikingDummyVerified,
            configuration.DarkKnightShadowbringerMinimumHpPercent,
            configuration.DarkKnightShadowbringerPressureLimitExclusive,
            guardActive,
            purifyClaimedPriority ||
            samuraiClaimedPriority ||
            viper.InputClaimed ||
            gunbreaker.InputClaimed ||
            allyRescueClaimedPriority ||
            rescue.UseActionAttempted ||
            miracle.InputClaimed ||
            guardianClaimedPriority ||
            guardShukuchi.InputClaimed ||
            ninja.InputClaimed ||
            scholar.InputClaimed ||
            emergencyInputFrame.IsConsumed,
            emergencyInputFrame,
            now,
            hardReset);
        var plunge = darkKnightPlunge.Observe(
            localPlayer,
            isCrystallineConflict,
            darkKnightPlungeConfigurationEnabled,
            metadata.DarkKnightPlungeVerified,
            guardActive,
            purifyClaimedPriority ||
            samuraiClaimedPriority ||
            viper.InputClaimed ||
            gunbreaker.InputClaimed ||
            allyRescueClaimedPriority ||
            rescue.UseActionAttempted ||
            miracle.InputClaimed ||
            guardianClaimedPriority ||
            guardShukuchi.InputClaimed ||
            ninja.InputClaimed ||
            scholar.InputClaimed ||
            shadowbringerPre.InputClaimed ||
            emergencyInputFrame.IsConsumed,
            emergencyInputFrame,
            now,
            hardReset);

        var shadowbringer = shadowbringerPre;
        if (shadowbringerPre.CanRunDeferredSafeFallback)
        {
            now = Environment.TickCount64;
            shadowbringer = darkKnightShadowbringer.ObserveDeferredSafeFallback(
                shadowbringerPre.DeferredFrameToken,
                localPlayer,
                context,
                darkKnightShadowbringerConfigurationEnabled,
                metadata.DarkKnightShadowbringerVerified,
                metadata.WolvesDenStrikingDummyVerified,
                configuration.DarkKnightShadowbringerMinimumHpPercent,
                configuration.DarkKnightShadowbringerPressureLimitExclusive,
                guardActive,
                purifyClaimedPriority ||
                samuraiClaimedPriority ||
                viper.InputClaimed ||
                gunbreaker.InputClaimed ||
                allyRescueClaimedPriority ||
                rescue.UseActionAttempted ||
                miracle.InputClaimed ||
                guardianClaimedPriority ||
                guardShukuchi.InputClaimed ||
                ninja.InputClaimed ||
                scholar.InputClaimed ||
                shadowbringerPre.InputClaimed ||
                plunge.InputClaimed ||
                emergencyInputFrame.IsConsumed,
                emergencyInputFrame,
                now);
        }

        now = Environment.TickCount64;
        var monkCombo = monkHeldCombo.Observe(
            localPlayer,
            context,
            monkHeldComboConfigurationEnabled,
            metadata.MonkHeldComboVerified,
            metadata.WolvesDenStrikingDummyVerified,
            guardActive,
            purifyClaimedPriority ||
            samuraiClaimedPriority ||
            viper.InputClaimed ||
            gunbreaker.InputClaimed ||
            allyRescueClaimedPriority ||
            rescue.UseActionAttempted ||
            miracle.InputClaimed ||
            guardianClaimedPriority ||
            guardShukuchi.InputClaimed ||
            ninja.InputClaimed ||
            scholar.InputClaimed ||
            shadowbringer.InputClaimed ||
            plunge.InputClaimed ||
            emergencyInputFrame.IsConsumed,
            emergencyInputFrame,
            now,
            hardReset);

        var jobSpecificHeldClaimedPriority = samuraiClaimedPriority ||
                                             ninja.InputClaimed ||
                                             viper.InputClaimed ||
                                             gunbreaker.InputClaimed ||
                                             allyRescueClaimedPriority ||
                                             miracle.InputClaimed ||
                                             guardianClaimedPriority ||
                                             guardShukuchi.InputClaimed ||
                                             scholar.InputClaimed ||
                                             plunge.InputClaimed ||
                                             shadowbringer.InputClaimed ||
                                             monkCombo.InputClaimed;
        var recuperate = smartRecuperate.Observe(
            localPlayer,
            context,
            configuration.Enabled && configuration.EnableSmartRecuperateOnHeldKey,
            metadata.RecuperateVerified,
            guardActive,
            hasPurifyRemovableCrowdControl ||
            purifyClaimedPriority ||
            jobSpecificHeldClaimedPriority ||
            emergencyInputFrame.IsConsumed,
            emergencyInputFrame,
            now,
            hardReset);
        var smartRecuperateClaimedPriority = recuperate.InputClaimed;
        now = Environment.TickCount64;
        var teleport = emergencyTeleport.Observe(
            localPlayer,
            context,
            emergencyTeleportHeldInputEnabled,
            metadata.IsEmergencyTeleportVerified(localJobId),
            guardActive,
            purifyClaimedPriority ||
            jobSpecificHeldClaimedPriority ||
            smartRecuperateClaimedPriority ||
            emergencyInputFrame.IsConsumed,
            emergencyInputFrame,
            now,
            hardReset);
        var emergencyTeleportClaimedPriority = teleport.InputClaimed;
        var guardDefense = defensiveUtility.ObserveGuard(
            localPlayer,
            isCrystallineConflict,
            defensiveUtilitiesConfigurationEnabled,
            configuration.DefensiveUtilitiesOnHeldKey,
            configuration.GuardOnStunPressure,
            pressureKnown,
            incomingEnemyCount,
            highPressureStunObserved,
            purify.UseActionAttempted,
            resilienceActive,
            hasPurifyRemovableCrowdControl,
            guardActive,
            purifyClaimedPriority ||
            jobSpecificHeldClaimedPriority ||
            smartRecuperateClaimedPriority ||
            emergencyTeleportClaimedPriority ||
            emergencyInputFrame.IsConsumed,
            emergencyInputFrame,
            now,
            hardReset: false,
            prioritizedGuardianPass: defense);
        var defensiveUtilityClaimedPriority = guardDefense.InputClaimed ||
                                               guardianClaimedPriority;
        ObserveAutoGuardFeedback(guardDefense.AutoGuardPopup);
        now = Environment.TickCount64;
        var pressureEscape = pressureEscapeSprint.Observe(
            localPlayer,
            isCrystallineConflict,
            configuration.Enabled && configuration.ShowHighPressureWarning,
            configuration.Enabled && configuration.PlayHighPressureWarningSound,
            configuration.HighPressureWarningSoundId,
            configuration.Enabled && configuration.EnablePressureEscapeSprintOnHeldKey,
            guardActive,
            purifyClaimedPriority ||
             jobSpecificHeldClaimedPriority ||
             smartRecuperateClaimedPriority ||
             emergencyTeleportClaimedPriority ||
             defensiveUtilityClaimedPriority,
            emergencyInputFrame,
            now,
            hardReset || !alive);
        var pressureEscapeClaimedPriority = pressureEscape.InputClaimed;
        var kardia = smartKardia.Observe(
            localPlayer,
            isCrystallineConflict,
            smartKardiaConfigurationEnabled,
            metadata.SmartKardiaVerified,
            guardActive,
            purifyClaimedPriority ||
             jobSpecificHeldClaimedPriority ||
             smartRecuperateClaimedPriority ||
             emergencyTeleportClaimedPriority ||
             defensiveUtilityClaimedPriority ||
            pressureEscapeClaimedPriority ||
            emergencyInputFrame.IsConsumed,
            now,
            hardReset);
        var monk = monkEarthReply.Observe(
            localPlayer,
            isSupportedPvPContext,
            configuration.Enabled &&
            configuration.EnableMonkEarthReplyHelper &&
            isMonk &&
            !guardActive,
            metadata.MonkEarthReplyVerified,
            configuration.MonkEarthReplyOnLowHp,
            configuration.MonkEarthReplyBeforeExpiry,
            configuration.MonkEarthReplyHpPercent,
            configuration.MonkEarthReplyExpirySeconds,
            purifyClaimedPriority ||
             jobSpecificHeldClaimedPriority ||
             smartRecuperateClaimedPriority ||
             emergencyTeleportClaimedPriority ||
             defensiveUtilityClaimedPriority ||
            pressureEscapeClaimedPriority ||
            kardia.UseActionAttempted ||
            emergencyInputFrame.IsConsumed,
            now,
            hardReset);

        // The producers have already frozen and revalidated one exact intent.
        // Select only the first request in the canonical held-helper order. A
        // cast-cancel request owns this frame; the normal UseAction boundary is
        // deliberately reached no earlier than a later clear-cast frame.
        var castCancellationRequest =
            ClaimedCastCancellationRequest(
                purify.InputClaimed,
                purify.CastCancellationRequest) ??
            ClaimedCastCancellationRequest(
                samurai.InputClaimed,
                samurai.CastCancellationRequest) ??
            ClaimedCastCancellationRequest(
                ninja.InputClaimed,
                ninja.CastCancellationRequest) ??
            ClaimedCastCancellationRequest(
                miracle.InputClaimed,
                miracle.CastCancellationRequest) ??
            ClaimedCastCancellationRequest(
                rescue.InputClaimed,
                rescue.CastCancellationRequest) ??
            ClaimedCastCancellationRequest(
                defense.InputClaimed,
                defense.CastCancellationRequest) ??
            ClaimedCastCancellationRequest(
                guardShukuchi.InputClaimed,
                guardShukuchi.CastCancellationRequest) ??
            ClaimedCastCancellationRequest(
                scholar.InputClaimed,
                scholar.CastCancellationRequest) ??
            ClaimedDarkKnightShadowbringerCastCancellationRequest(
                shadowbringerPre.InputClaimed &&
                shadowbringerPre.Opportunity ==
                    DarkKnightShadowbringerOpportunityKind.DarkArts,
                shadowbringerPre.CastCancellationLease) ??
            ClaimedCastCancellationRequest(
                plunge.InputClaimed,
                plunge.CastCancellationRequest) ??
            ClaimedDarkKnightShadowbringerCastCancellationRequest(
                shadowbringer.InputClaimed &&
                shadowbringer.Opportunity ==
                    DarkKnightShadowbringerOpportunityKind.SafeHpCost,
                shadowbringer.CastCancellationLease) ??
            ClaimedCastCancellationRequest(
                recuperate.InputClaimed,
                recuperate.CastCancellationRequest) ??
            ClaimedCastCancellationRequest(
                teleport.InputClaimed,
                teleport.CastCancellationRequest) ??
            ClaimedCastCancellationRequest(
                guardDefense.InputClaimed,
                guardDefense.CastCancellationRequest) ??
            ClaimedCastCancellationRequest(
                pressureEscape.InputClaimed,
                pressureEscape.CastCancellationRequest);
        heldCastCancellation.Observe(
            localPlayer,
            configuration.Enabled &&
            configuration.AllowHeldHelpersToCancelOwnCast,
            isSupportedPvPContext,
            guardActive,
            prioritizedInputClaimed: castCancellationRequest is { IsValid: true },
            intentOtherwiseReady: castCancellationRequest is { IsValid: true },
            request: castCancellationRequest,
            inputFrame: emergencyInputFrame,
            hardReset: hardReset);

        // Scholar owns a separate recast lane. It reads the immutable raw held
        // snapshot after the entire shared scheduler/cast-cancel pass, never
        // consumes that frame, and simply waits for the real native boundary.
        now = Environment.TickCount64;
        scholarSpread.Observe(
            localPlayer,
            isCrystallineConflict,
            scholarSpreadConfigurationEnabled,
            metadata.ScholarSpreadVerified,
            guardActive,
            emergencyInputFrame,
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

    private static HeldCastCancellationRequest? ClaimedCastCancellationRequest(
        bool inputClaimed,
        HeldCastCancellationRequest? request) =>
        inputClaimed && request is { IsValid: true }
            ? request
            : null;

    private static HeldCastCancellationRequest?
        ClaimedDarkKnightShadowbringerCastCancellationRequest(
            bool inputClaimed,
            DarkKnightShadowbringerCastCancellationLease? lease)
    {
        if (!inputClaimed || lease is not { IsValid: true } exact) return null;
        var request = new HeldCastCancellationRequest(
            HeldCastCancellationHelperKind.DarkKnightShadowbringer,
            exact.ExpectedAdjustedActionId,
            exact.LocalPlayer,
            exact.Target,
            exact.FrozenKeyCode,
            exact.IntentEpochToken);
        return request.IsValid ? request : null;
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
        criticalUtilityCoordination.Clear("Runtime reset");
        HeldActionRetryRules.ConfigureLatencyResponsePolicy(false, 0);
        emergencyInput.Reset();
        emergencyPurify.Reset();
        defensiveUtility.Reset();
        smartRecuperate.Reset();
        emergencyTeleport.Reset();
        pressureEscapeSprint.Reset();
        guardianCommunication.Reset();
        allyRescue.Reset();
        miracleIntercept.Reset();
        samuraiReactive.Reset();
        machinistLimitBreakCapture.SetSamuraiReactiveLocalEntityId(0);
        machinistLimitBreakCapture.ClearSamuraiReactiveProtectionSignals();
        smartKardia.Reset();
        ninjaGuardShukuchi.Reset();
        ninjaSeiton.Reset();
        viperSerpentTail.Reset();
        gunbreakerContinuation.Reset();
        scholarCriticalStrategy.Reset();
        scholarSpread.Reset();
        monkEarthReply.Reset();
        darkKnightPlunge.Reset();
        darkKnightShadowbringer.Reset();
        monkHeldCombo.Reset();
        Interlocked.Exchange(ref snapshot, PersonalAlertSnapshot.Inactive);
    }

    private void ClearStatusTracking()
    {
        instanceTokens.Clear();
        lastPresentations.Clear();
        pulseStartedAt.Clear();
        localMpWarningState = LocalMpWarningState.Initial;
        machinistLimitBreakWarningSound.Reset();
        ClearMachinistLimitBreakThreat();
        purifyMissingObservedAt = -1;
        resiliencePresence = DebouncedVisibilityState.Initial;
        alertStates = [];
    }

    private void ObserveLocalMpWarnings(
        IPlayerCharacter? localPlayer,
        bool isSupportedPvPContext,
        bool alive,
        bool hardReset)
    {
        var telemetryTrusted =
            configuration.Enabled &&
            isSupportedPvPContext &&
            HasTrustedLocalPlayerIdentity(localPlayer) &&
            localPlayer!.MaxMp == CombatFrameRules.ExpectedMaximumMp &&
            localPlayer.CurrentMp <= localPlayer.MaxMp;
        var decision = LocalMpWarningRules.Observe(
            localMpWarningState,
            localPlayer?.CurrentMp ?? 0,
            localPlayer?.MaxMp ?? 0,
            telemetryTrusted,
            alive,
            hardReset || !configuration.Enabled || !isSupportedPvPContext);
        localMpWarningState = decision.NextState;

        if (!configuration.PlayLocalMpWarningSounds ||
            !configuration.Enabled ||
            !isSupportedPvPContext ||
            !alive ||
            decision.Edges == LocalMpWarningEdge.None)
        {
            return;
        }

        // A one-frame drop through both thresholds emits only the more urgent
        // cue. The Core state still consumes both edges, so 4,000 never leaks
        // into a later frame as a stale second sound.
        if (decision.MostSevereEdge == LocalMpWarningEdge.TwoThousand)
        {
            PlayLocalMpWarningSound(
                configuration.LocalMpWarning2000SoundId,
                "2,000 MP warning");
            return;
        }

        PlayLocalMpWarningSound(
            configuration.LocalMpWarning4000SoundId,
            "4,000 MP warning");
    }

    private bool PlayLocalMpWarningSound(int configuredSoundId, string label) =>
        MachinistLimitBreakWarningSound.TryPlayShared(
            Math.Clamp(configuredSoundId, 1, 16),
            log,
            $"Seiton Sense local-player {label} failed closed.");

    private void ObserveAutoGuardFeedback(AutoGuardTriggerPopup? popup)
    {
        var now = Environment.TickCount64;
        if (popup is not { } visible ||
            !visible.IsVisible(now) ||
            visible.Token == consumedAutoGuardPopupToken)
        {
            return;
        }

        // Consume before the native call. Disabled, failed, or throwing sound
        // requests are never replayed later in the same Auto-Guard episode.
        consumedAutoGuardPopupToken = visible.Token;
        if (!configuration.Enabled || !configuration.PlayAutoGuardActivationSound) return;

        MachinistLimitBreakWarningSound.TryPlayShared(
            Math.Clamp(configuration.AutoGuardActivationSoundId, 1, 16),
            log,
            "Seiton Sense Auto-Guard activation sound failed closed.");
    }

    private static bool HasTrustedLocalPlayerIdentity(IPlayerCharacter? localPlayer) =>
        localPlayer is not null &&
        localPlayer.GameObjectId is not 0 and not 0xE0000000 &&
        localPlayer.EntityId is not 0 and not 0xE0000000;

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
