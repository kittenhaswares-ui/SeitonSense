using Dalamud.Game.ClientState.Party;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using SeitonSense.Core;
using SeitonSense.Plugin.Models;
using SeitonSense.Plugin.Services;
using SeitonSense.Plugin.UI;

namespace SeitonSense.Plugin;

public sealed class Plugin : IDalamudPlugin
{
    private const string Command = "/seiton";
    private const string AliasCommand = "/ssense";
    private const string NearAssistCommand = "/nearassist";
    private const string NearAssistAliasCommand = "/ssassist";
    private const string NearHelpCommand = "/nearhelp";
    private const string NearHelpAliasCommand = "/sshelp";
    private const string FarHelpCommand = "/farhelp";
    private const string FarHelpAliasCommand = "/ssfar";
    private const string PressureCommand = "/howmany";

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commandManager;
    private readonly IChatGui chatGui;
    private readonly IPluginLog log;
    private readonly PluginConfiguration configuration;
    private readonly WindowSystem windowSystem = new("SeitonSense");
    private readonly ExecuteTracker tracker;
    private readonly PersonalStatusService personalStatus;
    private readonly TargetPressureTracker pressureTracker;
    private readonly AutoEnemyFocusMarkService autoEnemyFocusMark;
    private readonly AutoLowMpFocusTargetService autoLowMpFocusTarget;
    private readonly IsolationAwarenessService isolationAwareness;
    private readonly PressureCounterWindow pressureCounter;
    private readonly NearAssistRedirector nearAssist;
    private readonly DarkKnightShadowbringerMacroService darkKnightShadowbringer;
    private readonly NamePlateAnchorTracker namePlateAnchors;
    private readonly ResourceAuraAnchorTracker resourceAuraAnchors;
    private readonly TargetHighlightRenderer targetHighlights;
    private readonly OverlayRenderer overlay;
    private readonly CombatFramesSnapshotService combatFramesSnapshots;
    private readonly CombatLimitBreakRuntimeService combatLimitBreakRuntime;
    private readonly CombatFrameLimitGaugeService combatFrameLimitGauge;
    private readonly CombatFramesTargetingService combatFramesTargeting;
    private readonly CombatFramesRenderer combatFrames;
    private readonly SettingsWindow settingsWindow;
    private readonly bool nearAssistCommandRegistered;
    private readonly bool nearAssistAliasRegistered;
    private readonly bool nearHelpCommandRegistered;
    private readonly bool nearHelpAliasRegistered;
    private readonly bool farHelpCommandRegistered;
    private readonly bool farHelpAliasRegistered;
    private readonly bool darkKnightShadowbringerCommandRegistered;
    private readonly bool pressureCommandRegistered;

    public Plugin(
        IDalamudPluginInterface pluginInterface,
        ICommandManager commandManager,
        IChatGui chatGui,
        IClientState clientState,
        IObjectTable objectTable,
        IFramework framework,
        IDutyState dutyState,
        IPartyList partyList,
        IDataManager dataManager,
        ITargetManager targetManager,
        IGameGui gameGui,
        INamePlateGui namePlateGui,
        IKeyState keyState,
        ITextureProvider textureProvider,
        IGameInteropProvider interop,
        IPluginLog log)
    {
        this.pluginInterface = pluginInterface;
        this.commandManager = commandManager;
        this.chatGui = chatGui;
        this.log = log;

        configuration = pluginInterface.GetPluginConfig() as PluginConfiguration ?? new PluginConfiguration();
        configuration.Initialize(pluginInterface);

        var metadata = PvPMetadataGuard.Validate(dataManager, log);
        var combatLimitBreakMetadata = CombatLimitBreakMetadataGuard.Validate(dataManager, log);
        var machinistLimitBreakCapture = new MachinistLimitBreakCapture(interop, log);
        tracker = new ExecuteTracker(
            clientState,
            objectTable,
            framework,
            dutyState,
            partyList,
            log,
            configuration,
            metadata);
        combatLimitBreakRuntime = new CombatLimitBreakRuntimeService(
            clientState,
            objectTable,
            framework,
            partyList,
            log,
            tracker,
            machinistLimitBreakCapture.CombatLimitBreakCaptureBuffer,
            combatLimitBreakMetadata,
            () => configuration.Enabled &&
                  configuration.ShowCombatFrames &&
                  (configuration.CombatFramesShowLimitBreaks ||
                   configuration.ShowAllyLimitBreakDamageEvents),
            () => configuration.ShowAllyLimitBreakDamageEvents);
        pressureTracker = new TargetPressureTracker(
            clientState,
            objectTable,
            framework,
            partyList,
            dutyState,
            dataManager,
            log,
            configuration,
            metadata,
            machinistLimitBreakCapture,
            tracker);
        var reviewedPvpCommands = new ReviewedPvpCommandDispatcher();
        autoEnemyFocusMark = new AutoEnemyFocusMarkService(
            configuration,
            clientState,
            objectTable,
            framework,
            dutyState,
            log,
            metadata,
            tracker,
            pressureTracker,
            reviewedPvpCommands);
        autoLowMpFocusTarget = new AutoLowMpFocusTargetService(
            configuration,
            clientState,
            objectTable,
            framework,
            dutyState,
            targetManager,
            log,
            metadata,
            tracker);
        isolationAwareness = new IsolationAwarenessService(
            configuration,
            clientState,
            objectTable,
            partyList,
            dutyState,
            framework,
            dataManager,
            log);
        var ccImmunityBrake = new CcImmunityBrakeService(
            configuration,
            clientState,
            objectTable,
            partyList,
            dutyState,
            dataManager,
            log);
        var smartWardensPaean = new SmartWardensPaeanService(
            configuration,
            clientState,
            objectTable,
            partyList,
            dutyState,
            dataManager,
            pressureTracker,
            log);
        darkKnightShadowbringer = new DarkKnightShadowbringerMacroService(
            configuration,
            clientState,
            objectTable,
            partyList,
            dutyState,
            dataManager,
            framework,
            log);
        nearAssist = new NearAssistRedirector(
            configuration,
            clientState,
            objectTable,
            partyList,
            dutyState,
            dataManager,
            interop,
            framework,
            pressureTracker,
            smartWardensPaean,
            ccImmunityBrake,
            darkKnightShadowbringer,
            log);
        personalStatus = new PersonalStatusService(
            clientState,
            objectTable,
            partyList,
            framework,
            dutyState,
            keyState,
            dataManager,
            pressureTracker,
            tracker,
            nearAssist,
            machinistLimitBreakCapture,
            log,
            configuration,
            metadata,
            reviewedPvpCommands);
        namePlateAnchors = new NamePlateAnchorTracker(namePlateGui, gameGui, log);
        resourceAuraAnchors = new ResourceAuraAnchorTracker(
            configuration,
            clientState,
            objectTable,
            gameGui,
            log);
        targetHighlights = new TargetHighlightRenderer(
            configuration,
            pluginInterface,
            clientState,
            objectTable,
            targetManager,
            gameGui,
            textureProvider,
            tracker,
            pressureTracker);
        overlay = new OverlayRenderer(
            configuration,
            tracker,
            personalStatus,
            pressureTracker,
            isolationAwareness,
            namePlateAnchors,
            resourceAuraAnchors,
            gameGui,
            textureProvider);
        combatFramesSnapshots = new CombatFramesSnapshotService(
            objectTable,
            framework,
            log,
            tracker,
            pressureTracker,
            metadata,
            () => configuration.Enabled && configuration.ShowCombatFrames,
            () =>
            {
                var current = targetManager.Target;
                var focus = targetManager.FocusTarget;
                return new CombatFrameTargetSelection(
                    current is not null && current.Address != nint.Zero && current.IsValid()
                        ? new TargetPressureActorIdentity(current.GameObjectId, current.EntityId)
                        : default,
                    focus is not null && focus.Address != nint.Zero && focus.IsValid()
                        ? new TargetPressureActorIdentity(focus.GameObjectId, focus.EntityId)
                     : default);
            });
        combatFrameLimitGauge = new CombatFrameLimitGaugeService(
            clientState,
            objectTable,
            framework,
            gameGui,
            tracker,
            log,
            () => configuration.Enabled &&
                  configuration.ShowCombatFrames &&
                  configuration.CombatFramesShowLimitBreaks);
        combatFramesTargeting = new CombatFramesTargetingService(
            clientState,
            objectTable,
            targetManager,
            framework,
            tracker,
            log);
        combatFrames = new CombatFramesRenderer(
            combatFramesSnapshots,
            combatFramesTargeting,
            combatLimitBreakRuntime,
            combatFrameLimitGauge,
            gameGui,
            textureProvider,
            log,
            () => new CombatFramesOptions(
                configuration.Enabled && configuration.ShowCombatFrames,
                false,
                configuration.CombatFramesEnableInteraction,
                configuration.CombatFramesEnemyScreenX,
                configuration.CombatFramesEnemyScreenY,
                configuration.CombatFramesSelfScreenX,
                configuration.CombatFramesSelfScreenY,
                configuration.CombatFramesScale,
                configuration.CombatFramesBackgroundOpacity,
                configuration.CombatFramesShowNames,
                configuration.CombatFramesShowExactValues,
                configuration.CombatFramesShowStatuses,
                configuration.CombatFramesShowPressure,
                configuration.CombatFramesShowLimitBreaks,
                configuration.ShowAllyLimitBreakDamageEvents));
        pressureCounter = new PressureCounterWindow(
            configuration,
            pressureTracker,
            textureProvider,
            gameGui,
            pluginInterface);
        settingsWindow = new SettingsWindow(
            configuration,
            tracker,
            personalStatus,
            overlay,
            pressureTracker,
            isolationAwareness,
            pressureCounter,
            combatFrames);
        windowSystem.AddWindow(pressureCounter);
        windowSystem.AddWindow(settingsWindow);

        const string help =
            "Open Seiton Sense settings. show/hide enable or disable the entire plugin; " +
            "other subcommands: preview, flash, debug, assist, reset, help.";
        commandManager.AddHandler(
            Command,
            new CommandInfo(OnCommand) { AllowedInMacros = true, HelpMessage = help });
        commandManager.AddHandler(
            AliasCommand,
            new CommandInfo(OnCommand) { AllowedInMacros = true, HelpMessage = help });
        const string nearAssistHelp =
            "CC-only one-shot assist. Macro: /nearassist, then /pvpac with <e1>, then the same action with <t>. Turbo is supported.";
        nearAssistCommandRegistered = commandManager.AddHandler(
            NearAssistCommand,
            new CommandInfo(OnNearAssistCommand)
            {
                AllowedInMacros = true,
                HelpMessage = nearAssistHelp,
            });
        nearAssistAliasRegistered = commandManager.AddHandler(
            NearAssistAliasCommand,
            new CommandInfo(OnNearAssistCommand)
            {
                AllowedInMacros = true,
                HelpMessage = nearAssistHelp,
            });
        if (!nearAssistCommandRegistered)
        {
            log.Warning(
                "/nearassist is already owned by another plugin; /ssassist registered={Registered}.",
                nearAssistAliasRegistered);
            chatGui.PrintError(
                "[Seiton Sense] /nearassist is still owned by the old NearAssist plugin. " +
                (nearAssistAliasRegistered
                    ? "Disable it and reload, or use /ssassist meanwhile."
                    : "Disable it and reload before using the integrated helper."));
        }

        const string nearHelpHelp =
            "CC-only survival-target helper: bounded pressure, plus exact self when the action allows it. Macro: /mlock, /nearhelp, friendly PvP action with <2>, then the same action with <t>.";
        nearHelpCommandRegistered = commandManager.AddHandler(
            NearHelpCommand,
            new CommandInfo(OnNearHelpCommand)
            {
                AllowedInMacros = true,
                HelpMessage = nearHelpHelp,
            });
        nearHelpAliasRegistered = commandManager.AddHandler(
            NearHelpAliasCommand,
            new CommandInfo(OnNearHelpCommand)
            {
                AllowedInMacros = true,
                HelpMessage = nearHelpHelp,
            });
        if (!nearHelpCommandRegistered)
        {
            log.Warning(
                "/nearhelp is already owned by another plugin; /sshelp registered={Registered}.",
                nearHelpAliasRegistered);
            chatGui.PrintError(
                "[Seiton Sense] /nearhelp is owned by another plugin. " +
                (nearHelpAliasRegistered ? "Use /sshelp meanwhile." : "Disable the conflicting plugin and reload."));
        }

        const string farHelpHelp =
            "CC-only farthest ally movement helper. Safe macro: /mlock, /farhelp, then one supported friendly movement action with <me>. No <t> fallback.";
        farHelpCommandRegistered = commandManager.AddHandler(
            FarHelpCommand,
            new CommandInfo(OnFarHelpCommand)
            {
                AllowedInMacros = true,
                HelpMessage = farHelpHelp,
            });
        farHelpAliasRegistered = commandManager.AddHandler(
            FarHelpAliasCommand,
            new CommandInfo(OnFarHelpCommand)
            {
                AllowedInMacros = true,
                HelpMessage = farHelpHelp,
            });
        if (!farHelpCommandRegistered)
        {
            log.Warning(
                "/farhelp is already owned by another plugin; /ssfar registered={Registered}.",
                farHelpAliasRegistered);
            chatGui.PrintError(
                "[Seiton Sense] /farhelp is owned by another plugin. " +
                (farHelpAliasRegistered ? "Use /ssfar meanwhile." : "Disable the conflicting plugin and reload."));
        }

        darkKnightShadowbringerCommandRegistered = commandManager.AddHandler(
            DarkKnightShadowbringerMacroService.Command,
            new CommandInfo(OnDarkKnightShadowbringerCommand)
            {
                AllowedInMacros = true,
                HelpMessage =
                    "Exact CC or enabled Wolves' Den striking-dummy DRK helper: /seitonbringer, then " +
                    "/pvpac \"Souleater Combo\" <t>; " +
                    "use the localized action name and ReAction Macro Queue + Turbo.",
            });
        if (!darkKnightShadowbringerCommandRegistered)
        {
            log.Warning("/seitonbringer is already owned by another plugin; the DRK macro helper remains unavailable.");
            chatGui.PrintError(
                "[Seiton Sense] /seitonbringer is owned by another plugin. Disable the conflict and reload before using the DRK macro helper.");
        }

        pressureCommandRegistered = commandManager.AddHandler(
            PressureCommand,
            new CommandInfo(OnPressureCommand)
            {
                HelpMessage =
                    "Open integrated pressure settings. show/hide affect only the counter; " +
                    "reset restores only its position. Other subcommands: lock, unlock, preview, debug.",
            });
        if (!pressureCommandRegistered)
        {
            log.Warning("/howmany is still owned by the standalone HOWMANY plugin; integrated pressure remains available through /seiton.");
            chatGui.PrintError(
                "[Seiton Sense] The standalone HOWMANY plugin is still loaded. " +
                "Disable it and reload Seiton Sense to avoid duplicate pressure overlays; integrated settings remain under /seiton.");
        }

        pluginInterface.UiBuilder.Draw += Draw;
        pluginInterface.UiBuilder.OpenMainUi += OpenSettings;
        pluginInterface.UiBuilder.OpenConfigUi += OpenSettings;
        namePlateAnchors.Start();
        tracker.Start();
        pressureTracker.Start();
        autoEnemyFocusMark.Start();
        autoLowMpFocusTarget.Start();
        isolationAwareness.Start();
        darkKnightShadowbringer.Start();
        nearAssist.Start();
        personalStatus.Start();
        combatLimitBreakRuntime.Start();
        combatFrameLimitGauge.Start();
        combatFramesSnapshots.Start();
        combatFramesTargeting.Start();
    }

    public void Dispose()
    {
        pluginInterface.UiBuilder.Draw -= Draw;
        pluginInterface.UiBuilder.OpenMainUi -= OpenSettings;
        pluginInterface.UiBuilder.OpenConfigUi -= OpenSettings;
        if (nearAssistCommandRegistered) commandManager.RemoveHandler(NearAssistCommand);
        if (nearAssistAliasRegistered) commandManager.RemoveHandler(NearAssistAliasCommand);
        if (nearHelpCommandRegistered) commandManager.RemoveHandler(NearHelpCommand);
        if (nearHelpAliasRegistered) commandManager.RemoveHandler(NearHelpAliasCommand);
        if (farHelpCommandRegistered) commandManager.RemoveHandler(FarHelpCommand);
        if (farHelpAliasRegistered) commandManager.RemoveHandler(FarHelpAliasCommand);
        if (darkKnightShadowbringerCommandRegistered)
            commandManager.RemoveHandler(DarkKnightShadowbringerMacroService.Command);
        if (pressureCommandRegistered) commandManager.RemoveHandler(PressureCommand);
        commandManager.RemoveHandler(Command);
        commandManager.RemoveHandler(AliasCommand);
        combatFramesTargeting.Dispose();
        combatFramesSnapshots.Dispose();
        combatFrameLimitGauge.Dispose();
        combatLimitBreakRuntime.Dispose();
        personalStatus.Dispose();
        nearAssist.Dispose();
        darkKnightShadowbringer.Dispose();
        isolationAwareness.Dispose();
        autoLowMpFocusTarget.Dispose();
        autoEnemyFocusMark.Dispose();
        pressureTracker.Dispose();
        tracker.Dispose();
        namePlateAnchors.Dispose();
        pressureCounter.Dispose();
        windowSystem.RemoveAllWindows();
    }

    private void Draw()
    {
        // Submit transparent combat-frame hit regions before ordinary windows so
        // settings and other plugin windows retain normal input priority.
        combatFrames.Draw();
        windowSystem.Draw();
        targetHighlights.Draw();
        overlay.Draw();
    }

    private void OpenSettings() => settingsWindow.IsOpen = true;

    private void OnCommand(string _, string arguments)
    {
        try
        {
            HandleCommand(arguments.Trim().ToLowerInvariant());
        }
        catch (Exception exception)
        {
            log.Error(exception, "Seiton Sense command failed.");
            chatGui.PrintError("[Seiton Sense] Command failed. See the Dalamud log for details.");
        }
    }

    private void OnPressureCommand(string _, string arguments)
    {
        try
        {
            HandlePressureCommand(arguments.Trim().ToLowerInvariant());
        }
        catch (Exception exception)
        {
            log.Error(exception, "Seiton Sense pressure command failed.");
            chatGui.PrintError("[Seiton Sense] Pressure command failed. See the Dalamud log.");
        }
    }

    private void HandlePressureCommand(string arguments)
    {
        switch (arguments)
        {
            case "":
            case "open":
            case "config":
                settingsWindow.IsOpen = true;
                return;
            case "show":
                configuration.ShowPressureCounter = true;
                break;
            case "hide":
                configuration.ShowPressureCounter = false;
                break;
            case "lock":
                configuration.PressureLocked = true;
                break;
            case "unlock":
                configuration.PressureLocked = false;
                break;
            case "preview":
                pressureCounter.PreviewEnabled = !pressureCounter.PreviewEnabled;
                chatGui.Print($"[Seiton Sense] Pressure preview {(pressureCounter.PreviewEnabled ? "enabled" : "disabled")}.");
                return;
            case "debug":
                chatGui.Print($"[Seiton Sense] {pressureTracker.Diagnostics.ToChatLine()}");
                return;
            case "reset":
                pressureCounter.PreviewEnabled = false;
                pressureCounter.ResetWindowPosition();
                break;
            default:
                chatGui.PrintError(
                    "[Seiton Sense] /howmany [show|hide|lock|unlock|preview|debug|reset]. " +
                    "show/hide affect only the counter; reset restores only its position.");
                return;
        }

        configuration.Save();
        chatGui.Print(
            arguments switch
            {
                "show" => "[Seiton Sense] Pressure counter shown; the rest of the plugin is unchanged.",
                "hide" => "[Seiton Sense] Pressure counter hidden; pressure-dependent helpers remain enabled.",
                "lock" => "[Seiton Sense] Pressure counter locked.",
                "unlock" => "[Seiton Sense] Pressure counter unlocked.",
                "reset" => "[Seiton Sense] Pressure counter position restored; no other setting was reset.",
                _ => $"[Seiton Sense] Pressure counter {arguments} applied.",
            });
    }

    private void HandleCommand(string arguments)
    {
        switch (arguments)
        {
            case "":
            case "open":
            case "config":
                settingsWindow.Toggle();
                return;
            case "show":
                configuration.Enabled = true;
                break;
            case "hide":
                configuration.Enabled = false;
                overlay.PreviewEnabled = false;
                overlay.CcProtectionPreviewEnabled = false;
                overlay.ResourceAuraPreviewEnabled = false;
                overlay.IsolationWarningPreviewEnabled = false;
                overlay.HighPressureWarningPreviewEnabled = false;
                pressureCounter.PreviewEnabled = false;
                combatFrames.PreviewEnabled = false;
                break;
            case "preview":
                overlay.PreviewEnabled = !overlay.PreviewEnabled;
                chatGui.Print($"[Seiton Sense] Preview {(overlay.PreviewEnabled ? "enabled" : "disabled")}.");
                return;
            case "flash":
                overlay.TriggerPreviewPopup();
                return;
            case "debug":
                var personal = personalStatus.Snapshot;
                var mchLimitBreak = personalStatus.MachinistLimitBreakDiagnostics;
                var defense = personalStatus.DefensiveUtilityDiagnostics;
                var recuperate = personalStatus.SmartRecuperateDiagnostics;
                var pressureEscape = personalStatus.PressureEscapeDiagnostics;
                var guardianCommunication = personalStatus.GuardianCommunicationDiagnostics;
                var rescue = personalStatus.AllyRescueDiagnostics;
                var miracle = personalStatus.MiracleInterceptDiagnostics;
                var kardia = personalStatus.SmartKardiaDiagnostics;
                var ninja = personalStatus.NinjaSeitonDiagnostics;
                var scholar = personalStatus.ScholarCriticalStrategyDiagnostics;
                var monk = personalStatus.MonkEarthReplyDiagnostics;
                var plunge = personalStatus.DarkKnightPlungeDiagnostics;
                var castCancellation = personalStatus.HeldCastCancellationDiagnostics;
                var limitBreakRuntime = combatLimitBreakRuntime.Diagnostics;
                var limitBreakGauge = combatFrameLimitGauge.Diagnostics;
                var assist = nearAssist.Diagnostics;
                var help = nearAssist.HelpDiagnostics;
                var farHelp = nearAssist.FarHelpDiagnostics;
                var ccBrake = nearAssist.CcBrakeDiagnostics;
                var smartPaean = nearAssist.SmartWardensPaeanDiagnostics;
                chatGui.Print(
                    $"[Seiton Sense] {tracker.Diagnostics.ToChatLine()}, native-anchors={overlay.NativeAnchorCount}, " +
                    $"resource-anchors={overlay.ResourceAuraAnchorCount}" +
                    $"({overlay.ResourceAuraSelfHotbarCount}/{overlay.ResourceAuraPartyRowCount}/{overlay.ResourceAuraCcRowCount}), " +
                    $"personal={personal.Statuses.Length}, purify={personal.Purify.Phase}/" +
                    $"{personal.Purify.Decision}, cancel={personal.Purify.CancelReason}, " +
                    $"trigger={personal.Purify.InputTrigger}, ready={personal.Purify.LocallyReady}, " +
                    $"fresh={personal.Purify.FreshGameplayKey}, held={personal.Purify.HeldGameplayKey}, " +
                    $"attempt={personal.Purify.UseActionAttempted}/{personal.Purify.UseActionAccepted}, " +
                    $"mchlb[hook={mchLimitBreak.CaptureRunning},q={mchLimitBreak.QueueDepth}," +
                    $"accepted={mchLimitBreak.AcceptedWarnings},active={mchLimitBreak.WarningActive}," +
                    $"errors={mchLimitBreak.CaptureErrors},drops={mchLimitBreak.DroppedWarnings}], " +
                    $"assist[hook={assist.HookAvailable},cmd={nearAssistCommandRegistered},armed={assist.Armed}," +
                    $"S={assist.EnemySlot},ttl={assist.RemainingMilliseconds},arm={assist.ArmedCount}," +
                    $"redirect={assist.RedirectedCount},fallback={assist.FallbackCount},last={assist.LastEvent}], " +
                    $"help[cmd={nearHelpCommandRegistered},armed={help.Armed},ttl={help.RemainingMilliseconds}," +
                    $"arm={help.ArmedCount},redirect={help.RedirectedCount},fallback={help.FallbackCount}," +
                    $"last={help.LastEvent}], " +
                    $"far[cmd={farHelpCommandRegistered},alias={farHelpAliasRegistered},armed={farHelp.Armed}," +
                    $"ttl={farHelp.RemainingMilliseconds},arm={farHelp.ArmedCount}," +
                    $"redirect={farHelp.RedirectedCount},fallback={farHelp.FallbackCount}," +
                    $"party={farHelp.LastPartySlot},distance={farHelp.LastDistance:0.0},last={farHelp.LastEvent}], " +
                    $"cc-brake[configured={ccBrake.Configured},active={ccBrake.ActiveInCurrentContext}," +
                    $"meta={ccBrake.VerifiedActions}/" +
                    $"{CcImmunityBrakeActionCatalog.Definitions.Count},statuses={ccBrake.VerifiedStatuses}," +
                    $"eval={ccBrake.EvaluatedAttempts},blocked={ccBrake.BlockedAttempts}," +
                    $"fail-open={ccBrake.FailedOpenAttempts},default={ccBrake.DefaultTargetResolutions}," +
                    $"exact={ccBrake.ExactTargetResolutions},resolve-fail={ccBrake.TargetResolutionFailures}," +
                    $"action={ccBrake.LastActionId},status={ccBrake.LastBlockerStatusId}," +
                    $"e={ccBrake.LastEnemySlot},mode={ccBrake.LastMode}," +
                    $"target={ccBrake.LastOriginalTargetId:X}/{ccBrake.LastForwardedTargetId:X}/" +
                    $"{ccBrake.LastEffectiveTargetId:X},suppressed={ccBrake.LastTargetSuppressedByRedirect}," +
                    $"resolve={ccBrake.LastTargetResolution}," +
                    $"sample={ccBrake.LastSampledStatuses},last={ccBrake.LastEvent}], " +
                    $"pressure[{pressureTracker.Diagnostics.ToChatLine()}," +
                    $"ccmeta={pressureTracker.VerifiedProtectionStatusCount}/" +
                    $"{CcProtectionStatusCatalog.Definitions.Count}]");
                chatGui.Print($"[Seiton Sense] smart-paean[{smartPaean.ToChatLine()}]");
                chatGui.Print(
                    $"[Seiton Sense] rescue[phase={rescue.Phase},decision={rescue.Decision}," +
                    $"cancel={rescue.CancelReason},trigger={rescue.InputTrigger},candidates={rescue.CandidateCount}," +
                    $"action={rescue.ActionId},target={rescue.TargetGameObjectId:X},status={rescue.TargetStatusId}," +
                    $"ready={rescue.LocallyReady},fresh={rescue.FreshGameplayKey},held={rescue.HeldGameplayKey}," +
                    $"claimed={rescue.InputClaimed}," +
                    $"attempt={rescue.UseActionAttempted}/{rescue.UseActionAccepted}," +
                    $"count={rescue.AttemptCount}/{rescue.AcceptedCount},pending={rescue.ConfirmationPending}," +
                    $"match={rescue.MatchConfirmations.TotalConfirmed}," +
                    $"session={rescue.SessionConfirmations.TotalConfirmed}," +
                    $"capture={rescue.ConfirmationCaptureCount},drop={rescue.ConfirmationDropCount}," +
                    $"last={rescue.LastEvent}]");
                chatGui.Print(
                    $"[Seiton Sense] defense[active={defense.Active},action={defense.Action}," +
                    $"trigger={defense.Trigger},pressure={defense.PressureKnown}/{defense.IncomingEnemyCount}," +
                    $"guard={defense.GuardActive},stun3={defense.HighPressureStunObserved}," +
                    $"post-purify={defense.WaitingForPostPurifyGuard}/" +
                    $"{defense.PostPurifyGuardRemainingMilliseconds},candidates={defense.GuardianCandidateCount}," +
                    $"target={defense.TargetGameObjectId:X}/{defense.TargetEntityId:X}," +
                    $"fresh={defense.FreshGameplayKey},held={defense.HeldGameplayKey}," +
                    $"claimed={defense.InputClaimed},attempt={defense.UseActionAttempted}/" +
                    $"{defense.UseActionAccepted},count={defense.AttemptCount}/{defense.AcceptedCount}," +
                    $"popup={defense.GuardianPopup?.PartySlot ?? 0}/" +
                    $"{Math.Max(0, (defense.GuardianPopup?.EndsAtMilliseconds ?? 0) - Environment.TickCount64)}," +
                    $"meta={defense.GuardMetadataVerified}/{defense.GuardianMetadataVerified}," +
                    $"last={defense.LastEvent}]");
                chatGui.Print(
                    $"[Seiton Sense] smart-recuperate[decision={recuperate.Decision}," +
                    $"reason={recuperate.Reason},action={recuperate.ResolvedActionId}," +
                    $"hp={recuperate.CurrentHp}/{recuperate.MaximumHp},missing={recuperate.MissingHp}," +
                    $"mp={recuperate.CurrentMp}/{recuperate.MaximumMp},ready={recuperate.LocallyReady}," +
                    $"guard={recuperate.GuardSuppressed},held={recuperate.HeldGameplayKey}," +
                    $"claimed={recuperate.InputClaimed},attempt={recuperate.UseActionAttempted}/" +
                    $"{recuperate.UseActionAccepted},count={recuperate.AttemptCount}/" +
                    $"{recuperate.AcceptedCount},last={recuperate.LastEvent}]");
                chatGui.Print(
                    $"[Seiton Sense] cast-cancel[enabled=" +
                    $"{configuration.AllowHeldHelpersToCancelOwnCast}," +
                    $"state={castCancellation.Decision}/{castCancellation.Reason}," +
                    $"cast={castCancellation.CastActionId},epoch={castCancellation.CastEpochToken}," +
                    $"current-helper={castCancellation.Request?.HelperKind ?? HeldCastCancellationHelperKind.None}," +
                    $"last-helper={castCancellation.LastRequestedIntent?.HelperKind ?? HeldCastCancellationHelperKind.None}," +
                    $"last-action={castCancellation.LastRequestedIntent?.HelperActionId ?? 0}," +
                    $"last-target={castCancellation.LastRequestedIntent?.Target.GameObjectId ?? 0:X}," +
                    $"last-key={castCancellation.LastRequestedIntent?.FrozenKeyCode ?? 0}," +
                    $"last-intent={castCancellation.LastRequestedIntent?.IntentEpochToken ?? 0}," +
                    $"native/last-native={castCancellation.NativeStatus}/{castCancellation.LastNativeStatus}," +
                    $"requested/faulted={castCancellation.NativeRequestCount}/" +
                    $"{castCancellation.NativeFaultCount},last={castCancellation.LastEvent}]");
                chatGui.Print(
                    $"[Seiton Sense] pressure-escape[active={pressureEscape.Active}," +
                    $"warning/sprint={pressureEscape.WarningEnabled}/{pressureEscape.SprintEnabled}," +
                    $"direct={pressureEscape.PressureKnown}/{pressureEscape.DirectEnemyCount}" +
                    $"(hard={pressureEscape.DirectHardTargetCount},cast={pressureEscape.DirectCastTargetCount}," +
                    $"age={pressureEscape.PressureAgeMilliseconds}),high={pressureEscape.HighPressure}," +
                    $"visible={pressureEscape.WarningActive},episode={pressureEscape.WarningEpisodeToken}/" +
                    $"{pressureEscape.SprintEpisodeSpent},guard={pressureEscape.GuardSuppressed}," +
                    $"sprint={pressureEscape.SprintActive},cc={pressureEscape.Incapacitated}," +
                    $"meta={pressureEscape.SprintMetadataVerified},key={pressureEscape.HeldGameplayKey}," +
                    $"claimed={pressureEscape.InputClaimed},attempt={pressureEscape.UseActionAttempted}/" +
                    $"{pressureEscape.UseActionAccepted},count={pressureEscape.AttemptCount}/" +
                    $"{pressureEscape.AcceptedCount},last={pressureEscape.LastEvent}]");
                chatGui.Print(
                    $"[Seiton Sense] guardian-comm[{guardianCommunication.ToChatLine()}]");
                chatGui.Print(
                    $"[Seiton Sense] miracle[phase={miracle.Phase},threat={miracle.Threat}," +
                    $"action={miracle.CounterActionId},target={miracle.TargetGameObjectId:X}/" +
                    $"{miracle.TargetEntityId:X},job={miracle.TargetJobId}," +
                    $"ttl={miracle.ThreatRemainingMilliseconds},scales={miracle.HardenedScalesPresent}," +
                    $"other-blocker={miracle.OtherCcProtectionPresent},range={miracle.HasNativeRangeAndLineOfSight}," +
                    $"key={miracle.InputKey},claimed={miracle.InputClaimed}," +
                    $"attempt={miracle.UseActionAttempted}/{miracle.UseActionAccepted}," +
                    $"count={miracle.AttemptCount}/{miracle.AcceptedCount},q={miracle.CaptureQueueDepth}," +
                    $"capture={miracle.CapturedThreatCount},drop={miracle.DroppedThreatCount}," +
                    $"seen/armed/reject={miracle.RecognizedThreatCount}/{miracle.ArmedThreatCount}/" +
                    $"{miracle.RejectedThreatCount},wait[p/r/k]={miracle.ProtectionWaitCount}/" +
                    $"{miracle.RangeWaitCount}/{miracle.NoInputWaitCount}," +
                    $"priority={miracle.PriorityWaitCount},expired={miracle.ExpiredThreatCount}," +
                    $"landed={miracle.ConfirmedLandingCount},confirm-q={miracle.ConfirmationQueueDepth}," +
                    $"confirm-capture={miracle.CapturedConfirmationCount}," +
                    $"confirm-drop={miracle.DroppedConfirmationCount}," +
                    $"last={miracle.LastEvent},last-op={miracle.LastOpportunity}," +
                    $"cleanse[phase={miracle.CleanseFollowupPhase}," +
                    $"removed={miracle.CleanseFollowupRemovedStatusId}," +
                    $"focus={miracle.CleanseFollowupTeamPressure}," +
                    $"target={miracle.CleanseFollowupTargetGameObjectId:X}/" +
                    $"{miracle.CleanseFollowupTargetEntityId:X}," +
                    $"resilience-seen={miracle.CleanseFollowupResilienceObserved}," +
                    $"signal/promote/cancel={miracle.CleanseFollowupSignalCount}/" +
                    $"{miracle.CleanseFollowupPromotionCount}/" +
                    $"{miracle.CleanseFollowupCancellationCount}," +
                    $"last={miracle.CleanseFollowupLastEvent}]]");
                chatGui.Print(
                    $"[Seiton Sense] smart-kardia[decision={kardia.Decision},reason={kardia.Reason}," +
                    $"ready={kardia.LocallyReady},action={kardia.ResolvedActionId}," +
                    $"candidates={kardia.CandidateCount},P={kardia.PartySlot},self={kardia.TargetIsSelf}," +
                    $"target={kardia.TargetGameObjectId:X}/{kardia.TargetEntityId:X}," +
                    $"pressure={kardia.PressureKnown}/{kardia.IncomingEnemyCount}," +
                    $"kardion={kardia.OwnKardionStateKnown}/{kardia.HasOwnKardion}," +
                    $"trigger-consumed={kardia.TriggerConsumed}," +
                    $"attempt={kardia.UseActionAttempted}/{kardia.UseActionAccepted}," +
                    $"count={kardia.AttemptCount}/{kardia.AcceptedCount}," +
                    $"resolve={kardia.CandidateResolution},last={kardia.LastEvent}]");
                var combatFrameSnapshot = combatFramesSnapshots.Snapshot;
                chatGui.Print(
                    $"[Seiton Sense] combat-frames[enabled={configuration.ShowCombatFrames}," +
                    $"active={combatFrameSnapshot.Active},published={combatFrameSnapshot.PublishedAtMilliseconds}," +
                    $"enemies={combatFrameSnapshot.Enemies.Count},preview={combatFrames.PreviewEnabled}]");
                chatGui.Print(
                    $"[Seiton Sense] combat-lb[meta={limitBreakRuntime.MetadataVerified}," +
                    $"activations={limitBreakRuntime.VerifiedActivationActions}/" +
                    $"{limitBreakRuntime.ExpectedActivationActions},damage-actions=" +
                    $"{limitBreakRuntime.VerifiedDamageActions}/" +
                    $"{limitBreakRuntime.ExpectedDamageActions},statuses={limitBreakRuntime.VerifiedStatuses}/" +
                    $"{limitBreakRuntime.ExpectedStatuses},active={limitBreakRuntime.Active}," +
                    $"roster={limitBreakRuntime.ExactRosterActors},episodes={limitBreakRuntime.ActiveEpisodes}," +
                    $"damage={limitBreakRuntime.VisibleAllyDamageEvents}," +
                    $"q={limitBreakRuntime.ActivationQueueDepth}/{limitBreakRuntime.DamageQueueDepth}," +
                    $"capture={limitBreakRuntime.CapturedActivations}/{limitBreakRuntime.CapturedDamageEvents}," +
                    $"drop={limitBreakRuntime.CaptureDroppedActivations}/" +
                    $"{limitBreakRuntime.CaptureDroppedDamageEvents}," +
                    $"accepted={limitBreakRuntime.AcceptedActivations}/" +
                    $"{limitBreakRuntime.AcceptedAllyDamageEvents}," +
                    $"rejected={limitBreakRuntime.RejectedActivations}/" +
                    $"{limitBreakRuntime.RejectedDamageEvents}]");
                chatGui.Print($"[Seiton Sense] combat-lb-gauge[{limitBreakGauge.ToTraceLine()}]");
                chatGui.Print(
                    $"[Seiton Sense] ninja-seiton[decision={ninja.Decision},reason={ninja.Reason}," +
                    $"ready={ninja.LocallyReady},action={ninja.ResolvedActionId}," +
                    $"candidates={ninja.CandidateCount},S={ninja.EnemySlot}," +
                    $"target={ninja.TargetGameObjectId:X}/{ninja.TargetEntityId:X}," +
                    $"hp={ninja.RevalidatedCurrentHp}/{ninja.RevalidatedMaximumHp}," +
                    $"boundary<50={ninja.BoundaryThresholdRevalidated}," +
                    $"threshold-cancel={ninja.ThresholdDriftCancelled}/" +
                    $"{ninja.ThresholdDriftCancellationCount}," +
                    $"fresh={ninja.FreshGameplayKey},claimed={ninja.InputClaimed}," +
                    $"attempt={ninja.UseActionAttempted}/{ninja.UseActionAccepted}," +
                    $"count={ninja.AttemptCount}/{ninja.AcceptedCount}," +
                    $"resolve={ninja.CandidateResolution},last={ninja.LastEvent}]");
                chatGui.Print(
                    $"[Seiton Sense] scholar-strategy[decision={scholar.Decision},reason={scholar.Reason}," +
                    $"ready={scholar.LocallyReady},action={scholar.ResolvedActionId}," +
                    $"candidates={scholar.CandidateCount},S={scholar.EnemySlot}," +
                    $"target={scholar.TargetGameObjectId:X}/{scholar.TargetEntityId:X}," +
                    $"pressure={scholar.PressureKnown}/{scholar.TeamTargetCount}," +
                    $"held={scholar.HeldGameplayKey},claimed={scholar.InputClaimed}," +
                    $"attempt={scholar.UseActionAttempted}/{scholar.UseActionAccepted}," +
                    $"count={scholar.AttemptCount}/{scholar.AcceptedCount}," +
                    $"resolve={scholar.CandidateResolution},last={scholar.LastEvent}]");
                chatGui.Print(
                    $"[Seiton Sense] monk-reply[phase={monk.Phase},decision={monk.Decision}," +
                    $"reason={monk.Reason},trigger={monk.Trigger},resonance={monk.ResonancePresent}," +
                    $"ttl={monk.ResonanceRemainingMilliseconds},hp={monk.CurrentHp}/{monk.MaximumHp}," +
                    $"adjusted={monk.AdjustedActionId},priority={monk.HigherPriorityClaimed}," +
                    $"attempt={monk.UseActionAttempted}/{monk.UseActionAccepted}," +
                    $"count={monk.AttemptCount}/{monk.AcceptedCount}]");
                chatGui.Print(
                    $"[Seiton Sense] dark-knight-plunge[decision={plunge.Decision},reason={plunge.Reason}," +
                    $"hold={plunge.HoldOutcome}/{plunge.OwnsContinuousHold}," +
                    $"saw-not-ready={plunge.CooldownUnavailableObserved},key={plunge.HeldGameplayKey}," +
                    $"epoch={plunge.CurrentReadyEpochToken}/{plunge.SpentReadyEpochToken}," +
                    $"ready={plunge.CooldownStateKnown}/{plunge.CooldownReady}/{plunge.StructurallyReady}," +
                    $"action={plunge.ResolvedActionId},candidates={plunge.CandidateCount},S={plunge.EnemySlot}," +
                    $"target={plunge.TargetGameObjectId:X}/{plunge.TargetEntityId:X}," +
                    $"hp={plunge.RevalidatedCurrentHp}/{plunge.RevalidatedMaximumHp}," +
                    $"distance={plunge.RevalidatedCenterDistanceYalms:0.00}," +
                    $"target-guard={plunge.RevalidatedTargetGuardActive},claimed={plunge.InputClaimed}," +
                    $"attempt={plunge.UseActionAttempted}/{plunge.UseActionAccepted}," +
                    $"count={plunge.AttemptCount}/{plunge.AcceptedCount}," +
                    $"resolve={plunge.CandidateResolution},last={plunge.LastEvent}]");
                chatGui.Print($"[Seiton Sense] isolation[{isolationAwareness.Diagnostics.ToChatLine()}]");
                chatGui.Print($"[Seiton Sense] auto-mark[{autoEnemyFocusMark.Diagnostics.ToChatLine()}]");
                chatGui.Print($"[Seiton Sense] auto-low-mp-focus[{autoLowMpFocusTarget.Diagnostics.ToChatLine()}]");
                chatGui.Print(
                    $"[Seiton Sense] shadowbringer[cmd={darkKnightShadowbringerCommandRegistered}," +
                    $"{darkKnightShadowbringer.Diagnostics.ToChatLine()}]");
                if (!string.IsNullOrEmpty(assist.RecentTrace))
                    chatGui.Print($"[Seiton Sense] assist trace: {assist.RecentTrace}");
                return;
            case "assist":
                nearAssist.Arm();
                return;
            case "reset":
                configuration.ResetToDefaults();
                overlay.PreviewEnabled = false;
                overlay.CcProtectionPreviewEnabled = false;
                overlay.ResourceAuraPreviewEnabled = false;
                overlay.IsolationWarningPreviewEnabled = false;
                overlay.HighPressureWarningPreviewEnabled = false;
                pressureCounter.PreviewEnabled = false;
                combatFrames.PreviewEnabled = false;
                pressureCounter.ResetWindowPosition();
                break;
            case "help":
                PrintHelp();
                return;
            default:
                PrintHelp(true);
                return;
        }

        configuration.Save();
        chatGui.Print(
            arguments switch
            {
                "show" => "[Seiton Sense] Entire plugin enabled.",
                "hide" => "[Seiton Sense] Entire plugin disabled.",
                "reset" => "[Seiton Sense] All plugin settings restored to defaults.",
                _ => $"[Seiton Sense] {arguments} applied.",
            });
    }

    private void PrintHelp(bool error = false)
    {
        const string text =
            "Usage: /seiton [show|hide|preview|flash|debug|assist|reset|help]. " +
            "show/hide enable or disable the entire plugin; reset restores all plugin settings. " +
            "/ssense is an alias; /nearassist and /ssassist arm the one-shot CC macro assist. " +
            "/nearhelp and /sshelp arm the one-shot survival-target helper (pressure/self when the action allows). " +
            "/farhelp and /ssfar arm the one-shot farthest friendly movement helper. " +
            "/seitonbringer arms only the immediately following authored DRK Souleater Combo <t> macro line in " +
            "CC or enabled Wolves' Den striking-dummy testing. " +
            "Integrated pressure uses /howmany; its reset subcommand restores only the counter position.";
        if (error) chatGui.PrintError($"[Seiton Sense] {text}");
        else chatGui.Print($"[Seiton Sense] {text}");
    }

    private void OnNearAssistCommand(string _, string arguments)
    {
        if (!string.IsNullOrWhiteSpace(arguments)) return;

        try
        {
            nearAssist.Arm();
        }
        catch (Exception exception)
        {
            log.Error(exception, "Seiton Sense Near Assist command failed closed.");
        }
    }

    private void OnNearHelpCommand(string _, string arguments)
    {
        if (!string.IsNullOrWhiteSpace(arguments)) return;

        try
        {
            nearAssist.ArmHelp();
        }
        catch (Exception exception)
        {
            log.Error(exception, "Seiton Sense Near Help command failed closed.");
        }
    }

    private void OnFarHelpCommand(string _, string arguments)
    {
        if (!string.IsNullOrWhiteSpace(arguments)) return;

        try
        {
            nearAssist.ArmFarHelp();
        }
        catch (Exception exception)
        {
            log.Error(exception, "Seiton Sense Far Help command failed closed.");
        }
    }

    private void OnDarkKnightShadowbringerCommand(string _, string arguments)
    {
        try
        {
            darkKnightShadowbringer.Arm(arguments, nearAssist.Diagnostics.HookAvailable);
        }
        catch (Exception exception)
        {
            log.Error(exception, "Seiton Sense Shadowbringer macro command failed closed.");
        }
    }
}
