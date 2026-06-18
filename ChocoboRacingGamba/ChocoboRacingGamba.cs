using System.Linq;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Windowing;
using ECommons;
using ChocoboRacing.Config;
using ChocoboRacing.Models;
using ChocoboRacing.State;
using ChocoboRacing.Services;
using ChocoboRacing.Automation;
using ChocoboRacing.Actions;
using ChocoboRacing.Events;
using ChocoboRacing.Events.ChatHandlers;
using ChocoboRacing.UI;
using ChocoboRacing.IPC;
using System.IO;

namespace ChocoboRacing;

/// <summary>
/// The primary entry point for the Plugin, handling DI injection and disposal.
/// </summary>
public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IPartyList PartyList { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static ITargetManager TargetManager { get; private set; } = null!;

    private const string CommandName1 = "/cr";
    private const string CommandName2 = "/chocoboracing";
    private const string CommandName3 = "/chocoboracingconfig";

    public PluginConfig Configuration { get; init; }
    public RaceHistoryService HistoryService { get; init; }
    public RaceState GameState { get; init; }
    public PartyService PartyManager { get; init; }
    public RaceService RaceManager { get; init; }
    public ActionQueue ActionQueue { get; init; }
    public TradeAction TradeAction { get; init; }
    public CustomMessageSender MessageSender { get; init; }
    public ChatEventHandler ChatHandler { get; init; }
    public bool IsTestingMode { get; private set; }
    private bool _addedTestBank;
    
    public readonly WindowSystem WindowSystem = new("ChocoboRacing");
    public MainWindow            MainWindow           { get; }
    public GambaWhereWindowOpened GambaWhereWindowIpc { get; }
    public GambaWhereRules        GambaWhereRulesIpc  { get; }

    public TradeDetectionService TradeService { get; init; }
    public AutoPayoutService AutoPayoutService { get; init; }

    public Plugin()
    {
        ECommonsMain.Init(PluginInterface, this);

        Configuration = PluginInterface.GetPluginConfig() as PluginConfig ?? new PluginConfig();
        Configuration.EnsurePresetsMigrated();
        HistoryService = new RaceHistoryService(PluginInterface, Log);
        HistoryService.MigrateFromConfig(Configuration);
        GameState = new RaceState(Configuration, HistoryService);
        RaceManager = new RaceService(Configuration, GameState);
        PartyManager = new PartyService(PartyList);
        ActionQueue = new ActionQueue(Framework, Log);
        TradeAction = new TradeAction(ObjectTable, TargetManager, ChatGui, ActionQueue);
        MessageSender = new CustomMessageSender(ActionQueue, ChatGui);
        TradeService = new TradeDetectionService(GameState, ChatGui, () => MainWindow?.IsOpen ?? false);
        AutoPayoutService = new AutoPayoutService(TradeAction, Log);

        ChatHandler = new ChatEventHandler(GameState, RaceManager, PartyManager, MessageSender, ActionQueue, ChatGui, Log, () => IsTestingMode, () => MainWindow?.IsOpen ?? false);
        
        var iconPath = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "Images", "chocobo-racing-icon.png");
        MainWindow = new MainWindow(this, iconPath);
        WindowSystem.AddWindow(MainWindow);
        GambaWhereWindowIpc = new GambaWhereWindowOpened(PluginInterface, Log, MainWindow);
        GambaWhereRulesIpc = new GambaWhereRules(PluginInterface, Framework, Log, this);

        CommandManager.AddHandler(CommandName1, new CommandInfo(OnCommand) { HelpMessage = "Open Chocobo Racing UI." });
        CommandManager.AddHandler(CommandName2, new CommandInfo(OnCommand) { HelpMessage = "Open Chocobo Racing UI." });
        CommandManager.AddHandler(CommandName3, new CommandInfo(OnConfigCommand) { HelpMessage = "Open Chocobo Racing UI to the Settings tab." });

        ChatHandler.Subscribe();
        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;

        GambaWhereWindowIpc.Dispose();
        GambaWhereRulesIpc.Dispose();
        ChatHandler.Unsubscribe();
        ActionQueue.Dispose();

        WindowSystem.RemoveAllWindows();
        MainWindow.Dispose();

        CommandManager.RemoveHandler(CommandName1);
        CommandManager.RemoveHandler(CommandName2);
        CommandManager.RemoveHandler(CommandName3);

        TradeService.Dispose();
        AutoPayoutService.Dispose();
        HistoryService.Dispose();
        ECommonsMain.Dispose();
    }

    public void EnableTestingMode()
    {
        var local = PartyManager.GetLocalPlayer();
        if (local == null) return;

        var existing = Configuration.Banks.FirstOrDefault(
            b => b.Name == local.Value.Name && b.World == local.Value.World && !b.IsArchived);

        if (existing == null)
        {
            Configuration.Banks.Add(new PlayerBank
            {
                Name    = local.Value.Name,
                World   = local.Value.World,
                Balance = 1_000_000
            });
            Configuration.Save();
            _addedTestBank = true;
        }
        else
        {
            _addedTestBank = false;
        }

        IsTestingMode = true;
        Configuration.IsHosting = true;
        Configuration.Save();
        GameState.ResetToIdle();
    }

    public void DisableTestingMode()
    {
        IsTestingMode = false;
        GameState.ResetToIdle();

        if (!_addedTestBank) return;

        var local = PartyManager.GetLocalPlayer();
        if (local == null) return;

        var testBank = Configuration.Banks.FirstOrDefault(
            b => b.Name == local.Value.Name && b.World == local.Value.World);

        if (testBank != null)
            Configuration.Banks.Remove(testBank);

        _addedTestBank = false;
        Configuration.Save();
    }

    private void OnCommand(string command, string args) => MainWindow.Toggle();
    private void OnConfigCommand(string command, string args) => MainWindow.OpenToSettings();
    public void ToggleMainUi() => MainWindow.Toggle();
}
