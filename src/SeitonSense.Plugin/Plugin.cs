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
    private const string CurrentReleaseVersion = "0.32.0.0";
    private const string Command = "/seiton";
    private const string AliasCommand = "/ssense";
    private const string NearAssistCommand = "/nearassist";
    private const string NearAssistAliasCommand = "/ssassist";
    private const string SmartTabCommand = "/smarttab";
    private const string SmartTabAliasCommand = "/sstarget";
    private const string SmartActionCommand = "/smartaction";
    private const string SmartActionAliasCommand = "/ssaction";
    private const string AutoSeitonCommand = "/autoseiton";
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
    private readonly AutoSeitonToggleWindow autoSeitonToggle;
    private readonly WhatsNewWindow whatsNew;
    private readonly NearAssistRedirector nearAssist;
    private readonly SmartTabTargetingService smartTabTargeting;
    private readonly DarkKnightShadowbringerMacroService darkKnightShadowbringer;
    private readonly PanicShukuchiService panicShukuchi;
    private readonly NamePlateAnchorTracker namePlateAnchors;
    private readonly ResourceAuraAnchorTracker resourceAuraAnchors;
    private readonly TargetHighlightRenderer targetHighlights;
    private readonly OverlayRenderer overlay;
    private readonly LimitBreakNotificationRenderer limitBreakNotifications;
    private readonly CombatLimitBreakRuntimeService combatLimitBreakRuntime;
    private readonly SettingsWindow settingsWindow;
    private readonly bool nearAssistCommandRegistered;
    private readonly bool nearAssistAliasRegistered;
    private readonly bool smartTabCommandRegistered;
    private readonly bool smartTabAliasRegistered;
    private readonly bool smartActionCommandRegistered;
    private readonly bool smartActionAliasRegistered;
    private readonly bool autoSeitonCommandRegistered;
    private readonly bool nearHelpCommandRegistered;
    private readonly bool nearHelpAliasRegistered;
    private readonly bool farHelpCommandRegistered;
    private readonly bool farHelpAliasRegistered;
    private readonly bool darkKnightShadowbringerCommandRegistered;
    private readonly bool panicShukuchiCommandRegistered;
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
        ISigScanner sigScanner,
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
                  (configuration.ShowEnemyLimitBreaksOnNameplates ||
                   configuration.ShowLimitBreakActivationMessages ||
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
            tracker,
            smartWardensPaean,
            ccImmunityBrake,
            darkKnightShadowbringer,
            log);
        smartTabTargeting = new SmartTabTargetingService(
            configuration,
            clientState,
            objectTable,
            partyList,
            dutyState,
            interop,
            sigScanner,
            pressureTracker,
            tracker,
            log);
        personalStatus = new PersonalStatusService(
            clientState,
            objectTable,
            targetManager,
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
        panicShukuchi = new PanicShukuchiService(
            configuration,
            clientState,
            objectTable,
            dutyState,
            nearAssist,
            log,
            metadata);
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
        overlay.AttachCombatLimitBreakRuntime(
            combatLimitBreakRuntime,
            () => configuration.Enabled && configuration.ShowEnemyLimitBreaksOnNameplates);
        limitBreakNotifications = new LimitBreakNotificationRenderer(
            combatLimitBreakRuntime,
            gameGui,
            textureProvider,
            log,
            () => new LimitBreakNotificationOptions(
                configuration.Enabled,
                configuration.ShowLimitBreakActivationMessages,
                configuration.ShowAllyLimitBreakDamageEvents,
                configuration.LimitBreakFeedShowNames,
                configuration.PersonalWarningScale,
                configuration.PersonalWarningBackgroundOpacity));
        pressureCounter = new PressureCounterWindow(
            configuration,
            pressureTracker,
            textureProvider,
            gameGui,
            pluginInterface);
        autoSeitonToggle = new AutoSeitonToggleWindow(
            objectTable,
            textureProvider,
            gameGui,
            log,
            () =>
            {
                var diagnostics = tracker.Diagnostics;
                var context = diagnostics.IsCrystallineConflict
                    ? SupportedPvPContext.CrystallineConflict
                    : diagnostics.IsWolvesDen
                        ? SupportedPvPContext.WolvesDen
                        : SupportedPvPContext.None;
                return new AutoSeitonToggleWidgetOptions(
                    configuration.Enabled,
                    context,
                    diagnostics.SeitonMetadataVerified,
                    configuration.EnableNinjaSeitonOnHeldGameplayKey);
            },
            enabled =>
            {
                configuration.EnableNinjaSeitonOnHeldGameplayKey = enabled;
                configuration.Save();
            });
        whatsNew = new WhatsNewWindow(
            CurrentReleaseVersion,
            [
                "Emergency Teleport is a new default-off held helper for MNK, BLM, SGE, and VPR: below your HP/MP limits and at or above the direct-focus minimum it makes one exact jump to the safest distant party member.",
                "Scholar Smart Spread is a separate default-off held lane: it prioritizes Biolysis coverage, reserves Deployment Tactics for the next DoT opportunity, and never adopts a manually started chain.",
                "Purify remains first; Emergency Teleport follows Smart Recuperate, while Scholar waits independently for the real cast, queue, and animation boundary. Schema 35; all 423 Core tests pass.",
            ],
            () => !string.Equals(
                configuration.LastSeenReleaseNotesVersion,
                CurrentReleaseVersion,
                StringComparison.Ordinal),
            () =>
            {
                configuration.LastSeenReleaseNotesVersion = CurrentReleaseVersion;
                configuration.Save();
            });
        settingsWindow = new SettingsWindow(
            configuration,
            tracker,
            personalStatus,
            overlay,
            pressureTracker,
            isolationAwareness,
            pressureCounter);
        windowSystem.AddWindow(pressureCounter);
        windowSystem.AddWindow(autoSeitonToggle);
        windowSystem.AddWindow(whatsNew);
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

        const string smartTabHelp =
            "Toggle the CC-only melee override for FFXIV's native forward-target command. Optional argument: on, off, or toggle.";
        smartTabCommandRegistered = commandManager.AddHandler(
            SmartTabCommand,
            new CommandInfo(OnSmartTabCommand)
            {
                AllowedInMacros = true,
                HelpMessage = smartTabHelp,
            });
        smartTabAliasRegistered = commandManager.AddHandler(
            SmartTabAliasCommand,
            new CommandInfo(OnSmartTabCommand)
            {
                AllowedInMacros = true,
                HelpMessage = smartTabHelp,
            });
        if (!smartTabCommandRegistered)
        {
            log.Warning(
                "/smarttab is already owned by another plugin; /sstarget registered={Registered}.",
                smartTabAliasRegistered);
            chatGui.PrintError(
                "[Seiton Sense] /smarttab is owned by another plugin. " +
                (smartTabAliasRegistered
                    ? "Use /sstarget meanwhile."
                    : "Disable the conflicting plugin and reload."));
        }

        const string smartActionHelp =
            "Optional CC-only harmful-action redirect. Macro: /smartaction, then /pvpac with <e1>, then the same action with <t>.";
        smartActionCommandRegistered = commandManager.AddHandler(
            SmartActionCommand,
            new CommandInfo(OnSmartActionCommand)
            {
                AllowedInMacros = true,
                HelpMessage = smartActionHelp,
            });
        smartActionAliasRegistered = commandManager.AddHandler(
            SmartActionAliasCommand,
            new CommandInfo(OnSmartActionCommand)
            {
                AllowedInMacros = true,
                HelpMessage = smartActionHelp,
            });
        if (!smartActionCommandRegistered)
        {
            log.Warning(
                "/smartaction is already owned by another plugin; /ssaction registered={Registered}.",
                smartActionAliasRegistered);
            chatGui.PrintError(
                "[Seiton Sense] /smartaction is owned by another plugin. " +
                (smartActionAliasRegistered
                    ? "Use /ssaction meanwhile."
                    : "Disable the conflicting plugin and reload."));
        }

        autoSeitonCommandRegistered = commandManager.AddHandler(
            AutoSeitonCommand,
            new CommandInfo(OnAutoSeitonCommand)
            {
                AllowedInMacros = true,
                HelpMessage = "Toggle NIN Auto-Seiton. Optional argument: on, off, or toggle.",
            });
        if (!autoSeitonCommandRegistered)
        {
            log.Warning("/autoseiton is already owned by another plugin; the Auto-Seiton toggle command is unavailable.");
            chatGui.PrintError(
                "[Seiton Sense] /autoseiton is owned by another plugin. Disable the conflict and reload before using the toggle macro.");
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

        panicShukuchiCommandRegistered = commandManager.AddHandler(
            PanicShukuchiService.Command,
            new CommandInfo(OnPanicShukuchiCommand)
            {
                AllowedInMacros = true,
                HelpMessage =
                    "NIN-only: immediately try one PvP Shukuchi 19.5 yalms straight ahead, including from own Guard. " +
                    "Works in Crystalline Conflict and enabled Wolves' Den testing without cursor or target changes.",
            });
        if (!panicShukuchiCommandRegistered)
        {
            log.Warning("/panicshu is already owned by another plugin; Panic Shukuchi remains unavailable.");
            chatGui.PrintError(
                "[Seiton Sense] /panicshu is owned by another plugin. Disable the conflict and reload before using Panic Shukuchi.");
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
        smartTabTargeting.Start();
        autoEnemyFocusMark.Start();
        autoLowMpFocusTarget.Start();
        isolationAwareness.Start();
        darkKnightShadowbringer.Start();
        nearAssist.Start();
        personalStatus.Start();
        combatLimitBreakRuntime.Start();
    }

    public void Dispose()
    {
        pluginInterface.UiBuilder.Draw -= Draw;
        pluginInterface.UiBuilder.OpenMainUi -= OpenSettings;
        pluginInterface.UiBuilder.OpenConfigUi -= OpenSettings;
        if (nearAssistCommandRegistered) commandManager.RemoveHandler(NearAssistCommand);
        if (nearAssistAliasRegistered) commandManager.RemoveHandler(NearAssistAliasCommand);
        if (smartTabCommandRegistered) commandManager.RemoveHandler(SmartTabCommand);
        if (smartTabAliasRegistered) commandManager.RemoveHandler(SmartTabAliasCommand);
        if (smartActionCommandRegistered) commandManager.RemoveHandler(SmartActionCommand);
        if (smartActionAliasRegistered) commandManager.RemoveHandler(SmartActionAliasCommand);
        if (autoSeitonCommandRegistered) commandManager.RemoveHandler(AutoSeitonCommand);
        if (nearHelpCommandRegistered) commandManager.RemoveHandler(NearHelpCommand);
        if (nearHelpAliasRegistered) commandManager.RemoveHandler(NearHelpAliasCommand);
        if (farHelpCommandRegistered) commandManager.RemoveHandler(FarHelpCommand);
        if (farHelpAliasRegistered) commandManager.RemoveHandler(FarHelpAliasCommand);
        if (darkKnightShadowbringerCommandRegistered)
            commandManager.RemoveHandler(DarkKnightShadowbringerMacroService.Command);
        if (panicShukuchiCommandRegistered)
            commandManager.RemoveHandler(PanicShukuchiService.Command);
        if (pressureCommandRegistered) commandManager.RemoveHandler(PressureCommand);
        commandManager.RemoveHandler(Command);
        commandManager.RemoveHandler(AliasCommand);
        combatLimitBreakRuntime.Dispose();
        personalStatus.Dispose();
        smartTabTargeting.Dispose();
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
        windowSystem.Draw();
        targetHighlights.Draw();
        overlay.Draw();
        limitBreakNotifications.Draw();
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
                var autoGuardProtection = personalStatus.AutoGuardProtectionDiagnostics;
                var recuperate = personalStatus.SmartRecuperateDiagnostics;
                var emergencyTeleport = personalStatus.EmergencyTeleportDiagnostics;
                var pressureEscape = personalStatus.PressureEscapeDiagnostics;
                var guardianCommunication = personalStatus.GuardianCommunicationDiagnostics;
                var rescue = personalStatus.AllyRescueDiagnostics;
                var miracle = personalStatus.MiracleInterceptDiagnostics;
                var kardia = personalStatus.SmartKardiaDiagnostics;
                var guardShukuchi = personalStatus.NinjaGuardShukuchiDiagnostics;
                var ninja = personalStatus.NinjaSeitonDiagnostics;
                var viper = personalStatus.ViperSerpentTailDiagnostics;
                var scholar = personalStatus.ScholarCriticalStrategyDiagnostics;
                var scholarSpread = personalStatus.ScholarSpreadDiagnostics;
                var monk = personalStatus.MonkEarthReplyDiagnostics;
                var plunge = personalStatus.DarkKnightPlungeDiagnostics;
                var castCancellation = personalStatus.HeldCastCancellationDiagnostics;
                var limitBreakRuntime = combatLimitBreakRuntime.Diagnostics;
                var assist = nearAssist.Diagnostics;
                var smartTab = smartTabTargeting.Diagnostics;
                var smartAction = nearAssist.SmartTargetDiagnostics;
                var help = nearAssist.HelpDiagnostics;
                var farHelp = nearAssist.FarHelpDiagnostics;
                var ccBrake = nearAssist.CcBrakeDiagnostics;
                var smartPaean = nearAssist.SmartWardensPaeanDiagnostics;
                var panic = panicShukuchi.Diagnostics;
                chatGui.Print(
                    $"[Seiton Sense] {tracker.Diagnostics.ToChatLine()}, native-anchors={overlay.NativeAnchorCount}, " +
                    $"resource-anchors={overlay.ResourceAuraAnchorCount}" +
                    $"({overlay.ResourceAuraSelfHotbarCount}/{overlay.ResourceAuraPartyRowCount}/{overlay.ResourceAuraCcRowCount}), " +
                    $"personal={personal.Statuses.Length}, purify={personal.Purify.Phase}/" +
                    $"{personal.Purify.Decision}, cancel={personal.Purify.CancelReason}, " +
                    $"trigger={personal.Purify.InputTrigger}, ready={personal.Purify.LocallyReady}, " +
                    $"fresh={personal.Purify.FreshGameplayKey}, held={personal.Purify.HeldGameplayKey}, " +
                    $"attempt={personal.Purify.UseActionAttempted}/{personal.Purify.UseActionAccepted}, " +
                    $"actionfx[hook={mchLimitBreak.CaptureRunning},mch-q={mchLimitBreak.QueueDepth}," +
                    $"accepted={mchLimitBreak.AcceptedWarnings},active={mchLimitBreak.WarningActive}," +
                    $"shared-errors={mchLimitBreak.CaptureErrors},mch-drops={mchLimitBreak.DroppedWarnings}], " +
                    $"assist[hook={assist.HookAvailable},cmd={nearAssistCommandRegistered},armed={assist.Armed}," +
                    $"S={assist.EnemySlot},ttl={assist.RemainingMilliseconds},arm={assist.ArmedCount}," +
                    $"redirect={assist.RedirectedCount},fallback={assist.FallbackCount},last={assist.LastEvent}], " +
                    $"smart-tab[cmd={smartTabCommandRegistered},alias={smartTabAliasRegistered}," +
                    $"{smartTab.ToChatLine()}], " +
                    $"smart-action[cmd={smartActionCommandRegistered},alias={smartActionAliasRegistered}," +
                    $"armed={smartAction.Armed},ttl={smartAction.RemainingMilliseconds}," +
                    $"arm={smartAction.ArmedCount},redirect={smartAction.RedirectedCount}," +
                    $"fallback={smartAction.FallbackCount},S={smartAction.LastEnemySlot}," +
                    $"last={smartAction.LastEvent}], " +
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
                    $"[Seiton Sense] auto-guard-protection[hook={autoGuardProtection.HookAvailable}," +
                    $"armed={autoGuardProtection.Armed},status={autoGuardProtection.ExactGuardObserved}," +
                    $"remaining={autoGuardProtection.RemainingMilliseconds}," +
                    $"count={autoGuardProtection.ArmedCount}/{autoGuardProtection.BlockedActionCount}/" +
                    $"{autoGuardProtection.ReleasedCount},last={autoGuardProtection.LastEvent}]");
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
                    $"[Seiton Sense] emergency-teleport[decision={emergencyTeleport.Decision}," +
                    $"reason={emergencyTeleport.Reason},danger={emergencyTeleport.Danger}," +
                    $"action={emergencyTeleport.ResolvedActionId},hp={emergencyTeleport.CurrentHp}/" +
                    $"{emergencyTeleport.MaximumHp},mp={emergencyTeleport.CurrentMp}/" +
                    $"{emergencyTeleport.MaximumMp},pressure={emergencyTeleport.DirectPressureKnown}/" +
                    $"{emergencyTeleport.DirectEnemyCount},episode={emergencyTeleport.EpisodeToken}/" +
                    $"{emergencyTeleport.EpisodeOpen}/{emergencyTeleport.EpisodeSpent}," +
                    $"candidates={emergencyTeleport.CandidateCount},P={emergencyTeleport.PartySlot}," +
                    $"target={emergencyTeleport.TargetGameObjectId:X}/{emergencyTeleport.TargetEntityId:X}," +
                    $"distance={emergencyTeleport.TravelDistanceYalms:0.0},nearby=" +
                    $"{emergencyTeleport.NearbyEnemyCount},clearance=" +
                    $"{emergencyTeleport.MinimumEnemyClearanceYalms:0.0},key=" +
                    $"{emergencyTeleport.HeldGameplayKey},claimed={emergencyTeleport.InputClaimed}," +
                    $"attempt={emergencyTeleport.UseActionAttempted}/{emergencyTeleport.NativeOutcome}," +
                    $"count={emergencyTeleport.AttemptCount}/{emergencyTeleport.AcceptedCount}," +
                    $"last={emergencyTeleport.LastEvent}]");
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
                     $"reservation={miracle.ProtectionEndReservedKey}/" +
                     $"{miracle.ProtectionEndExpectedRemainingMilliseconds}ms," +
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
                chatGui.Print(
                    $"[Seiton Sense] ninja-guard-shukuchi[decision={guardShukuchi.Decision}," +
                    $"reason={guardShukuchi.Reason},ready={guardShukuchi.LocallyReady}," +
                    $"action={guardShukuchi.ResolvedActionId},candidates={guardShukuchi.CandidateCount}," +
                    $"S={guardShukuchi.EnemySlot},target={guardShukuchi.TargetGameObjectId:X}/" +
                    $"{guardShukuchi.TargetEntityId:X},hp={guardShukuchi.RevalidatedCurrentHp}/" +
                    $"{guardShukuchi.RevalidatedMaximumHp},guard={guardShukuchi.RevalidatedGuardActive}," +
                    $"distance={guardShukuchi.RevalidatedDistanceYalms:0.00}," +
                    $"destination={guardShukuchi.Destination.X:0.00}/" +
                    $"{guardShukuchi.Destination.Y:0.00}/{guardShukuchi.Destination.Z:0.00}," +
                    $"pressure={guardShukuchi.PressureKnown}/{guardShukuchi.TeamTargetCount}," +
                    $"key={guardShukuchi.HeldGameplayKey},claimed={guardShukuchi.InputClaimed}," +
                    $"attempt/accepted/target={guardShukuchi.UseActionAttempted}/" +
                    $"{guardShukuchi.UseActionAccepted}/{guardShukuchi.HardTargetConfirmed}," +
                    $"count={guardShukuchi.AttemptCount}/{guardShukuchi.AcceptedCount}/" +
                    $"{guardShukuchi.TargetConfirmedCount},rejected/unknown=" +
                    $"{guardShukuchi.RejectedCount}/{guardShukuchi.UnknownCount}," +
                    $"resolve={guardShukuchi.CandidateResolution},last={guardShukuchi.LastEvent}]");
                chatGui.Print(
                    $"[Seiton Sense] ninja-seiton[decision={ninja.Decision},reason={ninja.Reason}," +
                    $"ready={ninja.LocallyReady},action={ninja.ResolvedActionId}," +
                    $"candidates={ninja.CandidateCount},S={ninja.EnemySlot}," +
                    $"target={ninja.TargetGameObjectId:X}/{ninja.TargetEntityId:X}," +
                    $"hp={ninja.RevalidatedCurrentHp}/{ninja.RevalidatedMaximumHp}," +
                    $"protection={ninja.ExecuteBlockingStatusId}," +
                    $"boundary<50={ninja.BoundaryThresholdRevalidated}," +
                    $"threshold-cancel={ninja.ThresholdDriftCancelled}/" +
                    $"{ninja.ThresholdDriftCancellationCount}," +
                    $"protection-cancel={ninja.ProtectionDriftCancelled}/" +
                    $"{ninja.ProtectionDriftCancellationCount}," +
                    $"fresh={ninja.FreshGameplayKey},claimed={ninja.InputClaimed}," +
                    $"attempt={ninja.UseActionAttempted}/{ninja.UseActionAccepted}," +
                    $"count={ninja.AttemptCount}/{ninja.AcceptedCount}," +
                    $"resolve={ninja.CandidateResolution},last={ninja.LastEvent}]");
                chatGui.Print(
                    $"[Seiton Sense] viper-serpent-tail[phase={viper.Phase}," +
                    $"decision={viper.Decision},reason={viper.Reason},action={viper.ResolvedActionId}," +
                    $"exposure={viper.ExposureGeneration}/{viper.ExposureSpent}/" +
                    $"{viper.NonFollowUpObservations},S={viper.EnemySlot}," +
                    $"target={viper.TargetGameObjectId:X}/{viper.TargetEntityId:X}," +
                    $"ready/boundary={viper.LocallyReady}/{viper.NativeBoundaryReady}," +
                    $"key={viper.HeldGameplayKey},claimed={viper.InputClaimed}," +
                    $"attempt={viper.UseActionAttempted}/{viper.UseActionAccepted}," +
                    $"native={viper.NativeAttemptCount}/{viper.LastNativeOutcome}," +
                    $"count={viper.AttemptCount}/{viper.AcceptedCount}/{viper.RejectedCount}/" +
                    $"{viper.UnknownCount}/{viper.SoftWaitCount},last={viper.LastEvent}]");
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
                    $"[Seiton Sense] scholar-spread[phase={scholarSpread.Phase},kind={scholarSpread.Kind}," +
                    $"plan/intent/effect={scholarSpread.PlanReason}/{scholarSpread.IntentReason}/" +
                    $"{scholarSpread.EffectReason},capture={scholarSpread.CaptureRunning}/" +
                    $"{scholarSpread.CaptureQueueDepth}/{scholarSpread.CaptureCount}/" +
                    $"{scholarSpread.CaptureDropCount},raw-held/consumed=" +
                    $"{scholarSpread.RawHeldGameplayKeyEligible}/{scholarSpread.SharedInputFrameWasConsumed}," +
                    $"key={scholarSpread.HeldGameplayKey},next={scholarSpread.NextActionId},charges=" +
                    $"{scholarSpread.DeploymentCharges},deploy/bio=" +
                    $"{scholarSpread.DeploymentNextChargeRemainingMilliseconds}/" +
                    $"{scholarSpread.BiolysisRemainingMilliseconds},boundary=" +
                    $"{scholarSpread.NativeBoundaryClear},dot/shield=" +
                    $"{scholarSpread.DotCandidateCount}/{scholarSpread.ShieldCandidateCount},slot=" +
                    $"{scholarSpread.TargetSlot},target={scholarSpread.TargetGameObjectId:X}/" +
                    $"{scholarSpread.TargetEntityId:X},coverage=" +
                    $"{scholarSpread.PredictedAffectedCount}/{scholarSpread.CurrentAffectedCount}," +
                    $"crystal={scholarSpread.TacticalCrystalResolved}/" +
                    $"{scholarSpread.TacticalCrystalPriorityRadiusYalms:0.0},attempt=" +
                    $"{scholarSpread.UseActionAttempted}/{scholarSpread.NativeOutcome},confirm=" +
                    $"{scholarSpread.SetupConfirmationCount}/{scholarSpread.DeploymentConfirmationCount}," +
                    $"manual={scholarSpread.ManualConflictCount},last={scholarSpread.LastEvent}]");
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
                chatGui.Print(
                    $"[Seiton Sense] panic-shukuchi[cmd={panicShukuchiCommandRegistered}," +
                    $"{panic.ToChatLine()}]");
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
            "/smarttab and /sstarget [on|off|toggle] control the melee override for FFXIV's normal forward targeting. " +
            "/smartaction and /ssaction optionally arm one harmful-action target redirect. " +
            "/nearhelp and /sshelp arm the one-shot survival-target helper (pressure/self when the action allows). " +
            "/farhelp and /ssfar arm the one-shot farthest friendly movement helper. " +
            "/seitonbringer arms only the immediately following authored DRK Souleater Combo <t> macro line in " +
            "CC or enabled Wolves' Den striking-dummy testing. " +
            "/panicshu immediately makes one NIN-only Shukuchi attempt 19.5 yalms straight ahead in CC or enabled " +
            "Wolves' Den testing, including from own Guard and without cursor or target changes. " +
            "/autoseiton [on|off|toggle] controls whether held-key NIN Auto-Seiton is available. " +
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

    private void OnSmartTabCommand(string _, string arguments)
    {
        try
        {
            var normalized = arguments.Trim().ToLowerInvariant();
            switch (normalized)
            {
                case "":
                case "toggle":
                    configuration.EnableSmartTabTargeting =
                        !configuration.EnableSmartTabTargeting;
                    break;
                case "on":
                    configuration.EnableSmartTabTargeting = true;
                    break;
                case "off":
                    configuration.EnableSmartTabTargeting = false;
                    break;
                default:
                    chatGui.PrintError("[Seiton Sense] Usage: /smarttab [on|off|toggle].");
                    return;
            }

            configuration.Save();
            var enabled = configuration.EnableSmartTabTargeting;
            chatGui.Print(
                enabled
                    ? "[Seiton Sense] Smart Tab ON: FFXIV's forward target command uses reviewed DPS Smart Targeting in exact CC."
                    : "[Seiton Sense] Smart Tab OFF: FFXIV targeting is fully vanilla.");
            if (enabled && !smartTabTargeting.Diagnostics.HookAvailable)
            {
                chatGui.PrintError(
                    "[Seiton Sense] Smart Tab hooks are unavailable in this load; targeting remains vanilla.");
            }
        }
        catch (Exception exception)
        {
            log.Error(exception, "Seiton Sense Smart Tab toggle command failed.");
        }
    }

    private void OnSmartActionCommand(string _, string arguments)
    {
        if (!string.IsNullOrWhiteSpace(arguments)) return;

        try
        {
            nearAssist.ArmSmartActionTarget();
        }
        catch (Exception exception)
        {
            log.Error(exception, "Seiton Sense Smart Action command failed closed.");
        }
    }

    private void OnAutoSeitonCommand(string _, string arguments)
    {
        try
        {
            var normalized = arguments.Trim().ToLowerInvariant();
            switch (normalized)
            {
                case "":
                case "toggle":
                    configuration.EnableNinjaSeitonOnHeldGameplayKey =
                        !configuration.EnableNinjaSeitonOnHeldGameplayKey;
                    break;
                case "on":
                    configuration.EnableNinjaSeitonOnHeldGameplayKey = true;
                    break;
                case "off":
                    configuration.EnableNinjaSeitonOnHeldGameplayKey = false;
                    break;
                default:
                    chatGui.PrintError("[Seiton Sense] Usage: /autoseiton [on|off|toggle].");
                    return;
            }

            configuration.Save();
        }
        catch (Exception exception)
        {
            log.Error(exception, "Seiton Sense Auto-Seiton toggle command failed.");
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

    private void OnPanicShukuchiCommand(string _, string arguments)
    {
        try
        {
            panicShukuchi.Execute(arguments);
        }
        catch (Exception exception)
        {
            log.Error(exception, "Seiton Sense Panic Shukuchi command failed closed.");
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
