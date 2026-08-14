using Dalamud.Game.ClientState.Party;
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
    private readonly PressureCounterWindow pressureCounter;
    private readonly NearAssistRedirector nearAssist;
    private readonly NamePlateAnchorTracker namePlateAnchors;
    private readonly ResourceAuraAnchorTracker resourceAuraAnchors;
    private readonly TargetHighlightRenderer targetHighlights;
    private readonly OverlayRenderer overlay;
    private readonly SettingsWindow settingsWindow;
    private readonly bool nearAssistCommandRegistered;
    private readonly bool nearAssistAliasRegistered;
    private readonly bool nearHelpCommandRegistered;
    private readonly bool nearHelpAliasRegistered;
    private readonly bool farHelpCommandRegistered;
    private readonly bool farHelpAliasRegistered;
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
        var ccImmunityBrake = new CcImmunityBrakeService(
            configuration,
            clientState,
            objectTable,
            partyList,
            dutyState,
            dataManager,
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
            ccImmunityBrake,
            log);
        personalStatus = new PersonalStatusService(
            clientState,
            objectTable,
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
            namePlateAnchors,
            resourceAuraAnchors,
            gameGui,
            textureProvider);
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
            pressureCounter);
        windowSystem.AddWindow(pressureCounter);
        windowSystem.AddWindow(settingsWindow);

        const string help = "Open Seiton Sense. Subcommands: show, hide, preview, flash, debug, assist, reset, help.";
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
            "CC-only lowest-health ally helper. Macro: /mlock, /nearhelp, friendly PvP action with <2>, then the same action with <t>.";
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

        pressureCommandRegistered = commandManager.AddHandler(
            PressureCommand,
            new CommandInfo(OnPressureCommand)
            {
                HelpMessage = "Open integrated pressure settings. Subcommands: show, hide, lock, unlock, preview, debug, reset.",
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
        nearAssist.Start();
        personalStatus.Start();
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
        if (pressureCommandRegistered) commandManager.RemoveHandler(PressureCommand);
        commandManager.RemoveHandler(Command);
        commandManager.RemoveHandler(AliasCommand);
        personalStatus.Dispose();
        nearAssist.Dispose();
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
                chatGui.PrintError("[Seiton Sense] /howmany [show|hide|lock|unlock|preview|debug|reset].");
                return;
        }

        configuration.Save();
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
                var rescue = personalStatus.AllyRescueDiagnostics;
                var miracle = personalStatus.MiracleInterceptDiagnostics;
                var monk = personalStatus.MonkEarthReplyDiagnostics;
                var assist = nearAssist.Diagnostics;
                var help = nearAssist.HelpDiagnostics;
                var farHelp = nearAssist.FarHelpDiagnostics;
                var ccBrake = nearAssist.CcBrakeDiagnostics;
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
                chatGui.Print(
                    $"[Seiton Sense] rescue[phase={rescue.Phase},decision={rescue.Decision}," +
                    $"cancel={rescue.CancelReason},trigger={rescue.InputTrigger},candidates={rescue.CandidateCount}," +
                    $"action={rescue.ActionId},target={rescue.TargetGameObjectId:X},status={rescue.TargetStatusId}," +
                    $"ready={rescue.LocallyReady},fresh={rescue.FreshGameplayKey},held={rescue.HeldGameplayKey}," +
                    $"attempt={rescue.UseActionAttempted}/{rescue.UseActionAccepted}," +
                    $"count={rescue.AttemptCount}/{rescue.AcceptedCount},pending={rescue.ConfirmationPending}," +
                    $"match={rescue.MatchConfirmations.TotalConfirmed}," +
                    $"session={rescue.SessionConfirmations.TotalConfirmed}," +
                    $"capture={rescue.ConfirmationCaptureCount},drop={rescue.ConfirmationDropCount}," +
                    $"last={rescue.LastEvent}]");
                chatGui.Print(
                    $"[Seiton Sense] miracle[phase={miracle.Phase},threat={miracle.Threat}," +
                    $"target={miracle.TargetGameObjectId:X}/{miracle.TargetEntityId:X},job={miracle.TargetJobId}," +
                    $"ttl={miracle.ThreatRemainingMilliseconds},scales={miracle.HardenedScalesPresent}," +
                    $"other-blocker={miracle.OtherCcProtectionPresent},range={miracle.HasNativeRangeAndLineOfSight}," +
                    $"key={miracle.InputKey},attempt={miracle.UseActionAttempted}/{miracle.UseActionAccepted}," +
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
                    $"target={miracle.CleanseFollowupTargetGameObjectId:X}/" +
                    $"{miracle.CleanseFollowupTargetEntityId:X}," +
                    $"resilience-seen={miracle.CleanseFollowupResilienceObserved}," +
                    $"signal/promote/cancel={miracle.CleanseFollowupSignalCount}/" +
                    $"{miracle.CleanseFollowupPromotionCount}/" +
                    $"{miracle.CleanseFollowupCancellationCount}," +
                    $"last={miracle.CleanseFollowupLastEvent}]]");
                chatGui.Print(
                    $"[Seiton Sense] monk-reply[phase={monk.Phase},decision={monk.Decision}," +
                    $"reason={monk.Reason},trigger={monk.Trigger},resonance={monk.ResonancePresent}," +
                    $"ttl={monk.ResonanceRemainingMilliseconds},hp={monk.CurrentHp}/{monk.MaximumHp}," +
                    $"adjusted={monk.AdjustedActionId},priority={monk.HigherPriorityClaimed}," +
                    $"attempt={monk.UseActionAttempted}/{monk.UseActionAccepted}," +
                    $"count={monk.AttemptCount}/{monk.AcceptedCount}]");
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
        chatGui.Print($"[Seiton Sense] {arguments} applied.");
    }

    private void PrintHelp(bool error = false)
    {
        const string text =
            "Usage: /seiton [show|hide|preview|flash|debug|assist|reset|help]. " +
            "/ssense is an alias; /nearassist and /ssassist arm the one-shot CC macro assist. " +
            "/nearhelp and /sshelp arm the one-shot lowest-health ally helper. " +
            "/farhelp and /ssfar arm the one-shot farthest friendly movement helper. " +
            "Integrated pressure uses /howmany.";
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
}
