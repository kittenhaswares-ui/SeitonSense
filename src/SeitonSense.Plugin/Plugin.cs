using Dalamud.Game.ClientState.Party;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using SeitonSense.Plugin.Models;
using SeitonSense.Plugin.Services;
using SeitonSense.Plugin.UI;

namespace SeitonSense.Plugin;

public sealed class Plugin : IDalamudPlugin
{
    private const string Command = "/seiton";
    private const string AliasCommand = "/ssense";

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commandManager;
    private readonly IChatGui chatGui;
    private readonly IPluginLog log;
    private readonly PluginConfiguration configuration;
    private readonly WindowSystem windowSystem = new("SeitonSense");
    private readonly ExecuteTracker tracker;
    private readonly NamePlateAnchorTracker namePlateAnchors;
    private readonly OverlayRenderer overlay;
    private readonly SettingsWindow settingsWindow;

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
        IGameGui gameGui,
        INamePlateGui namePlateGui,
        ITextureProvider textureProvider,
        IPluginLog log)
    {
        this.pluginInterface = pluginInterface;
        this.commandManager = commandManager;
        this.chatGui = chatGui;
        this.log = log;

        configuration = pluginInterface.GetPluginConfig() as PluginConfiguration ?? new PluginConfiguration();
        configuration.Initialize(pluginInterface);

        var metadata = PvPMetadataGuard.Validate(dataManager, log);
        tracker = new ExecuteTracker(
            clientState,
            objectTable,
            framework,
            dutyState,
            partyList,
            log,
            configuration,
            metadata);
        namePlateAnchors = new NamePlateAnchorTracker(namePlateGui, gameGui, log);
        overlay = new OverlayRenderer(
            configuration,
            tracker,
            namePlateAnchors,
            gameGui,
            textureProvider);
        settingsWindow = new SettingsWindow(configuration, tracker, overlay);
        windowSystem.AddWindow(settingsWindow);

        const string help = "Open Seiton Sense. Subcommands: show, hide, preview, flash, debug, reset, help.";
        commandManager.AddHandler(Command, new CommandInfo(OnCommand) { HelpMessage = help });
        commandManager.AddHandler(AliasCommand, new CommandInfo(OnCommand) { HelpMessage = help });

        pluginInterface.UiBuilder.Draw += Draw;
        pluginInterface.UiBuilder.OpenMainUi += OpenSettings;
        pluginInterface.UiBuilder.OpenConfigUi += OpenSettings;
        namePlateAnchors.Start();
        tracker.Start();
    }

    public void Dispose()
    {
        pluginInterface.UiBuilder.Draw -= Draw;
        pluginInterface.UiBuilder.OpenMainUi -= OpenSettings;
        pluginInterface.UiBuilder.OpenConfigUi -= OpenSettings;
        commandManager.RemoveHandler(Command);
        commandManager.RemoveHandler(AliasCommand);
        tracker.Dispose();
        namePlateAnchors.Dispose();
        windowSystem.RemoveAllWindows();
    }

    private void Draw()
    {
        windowSystem.Draw();
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
                break;
            case "preview":
                overlay.PreviewEnabled = !overlay.PreviewEnabled;
                chatGui.Print($"[Seiton Sense] Preview {(overlay.PreviewEnabled ? "enabled" : "disabled")}.");
                return;
            case "flash":
                overlay.TriggerPreviewPopup();
                return;
            case "debug":
                chatGui.Print(
                    $"[Seiton Sense] {tracker.Diagnostics.ToChatLine()}, native-anchors={overlay.NativeAnchorCount}");
                return;
            case "reset":
                configuration.ResetToDefaults();
                overlay.PreviewEnabled = false;
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
        const string text = "Usage: /seiton [show|hide|preview|flash|debug|reset|help]. /ssense is an alias.";
        if (error) chatGui.PrintError($"[Seiton Sense] {text}");
        else chatGui.Print($"[Seiton Sense] {text}");
    }
}
