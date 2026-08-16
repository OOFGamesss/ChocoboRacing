using System;
using ChocoboRacing.Config;
using ChocoboRacing.Models;
using ChocoboRacing.Services;
using ChocoboRacing.State;
using ChocoboRacing.Utility;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;

/// <summary>Adds an "Add to Chocobo Racing" context menu entry that opens an external bank for a targeted or chat-log player.</summary>
namespace ChocoboRacing.Events;

public sealed class BankRegistrationContextMenu : IDisposable
{
    private const string MenuLabel = "Add to Chocobo Racing";

    private readonly IContextMenu _contextMenu;
    private readonly PluginConfig _config;
    private readonly PartyService _partyService;
    private readonly RaceState _state;

    public BankRegistrationContextMenu(IContextMenu contextMenu, PluginConfig config, PartyService partyService, RaceState state)
    {
        _contextMenu = contextMenu;
        _config = config;
        _partyService = partyService;
        _state = state;
        _contextMenu.OnMenuOpened += OnMenuOpened;
    }

    private void OnMenuOpened(IMenuOpenedArgs args)
    {
        if (_config.RaceMode != RaceMode.Classic) return;
        if (!_partyService.IsInParty() && !_partyService.IsInAlliance()) return;
        if (args.MenuType != ContextMenuType.Default) return;
        if (args.Target is not MenuTargetDefault target) return;
        if (!IsPlayerTarget(args, target)) return;

        var name = SanitiseHelper.SanitiseName(target.TargetName);
        if (string.IsNullOrEmpty(name)) return;
        if (target.TargetHomeWorld.RowId == 0) return;

        var world = target.TargetHomeWorld.Value.Name.ToString();
        if (string.IsNullOrEmpty(world)) return;
        if (_state.GetBank(name, world) != null) return;

        args.AddMenuItem(new MenuItem
        {
            Name = new SeString(new TextPayload(MenuLabel)),
            PrefixChar = 'C',
            PrefixColor = 506,
            OnClicked = _ => _state.AddExternalBank(name, world),
        });
    }

    private static bool IsPlayerTarget(IMenuOpenedArgs args, MenuTargetDefault target)
    {
        if (args.AddonName == "ChatLog")
            return target.TargetContentId != 0 && !string.IsNullOrEmpty(target.TargetName);
        return target.TargetObject?.ObjectKind == ObjectKind.Pc;
    }

    public void Dispose()
    {
        _contextMenu.OnMenuOpened -= OnMenuOpened;
    }
}
