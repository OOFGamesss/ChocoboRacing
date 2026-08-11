using System;
using ChocoboRacing.Config;
using ChocoboRacing.Models;
using ChocoboRacing.State;
using ChocoboRacing.Utility;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;

/// <summary>
/// Adds an "Add to Raffle" context menu entry to player targets during the raffle registration phase.
/// </summary>
namespace ChocoboRacing.Events;

public sealed class PlayerRegistrationContextMenu : IDisposable
{
    private const string MenuLabel = "Add to Raffle";

    private readonly IContextMenu _contextMenu;
    private readonly PluginConfig _config;
    private readonly RaffleState _state;

    public PlayerRegistrationContextMenu(IContextMenu contextMenu, PluginConfig config, RaffleState state)
    {
        _contextMenu = contextMenu;
        _config = config;
        _state = state;
        _contextMenu.OnMenuOpened += OnMenuOpened;
    }

    private void OnMenuOpened(IMenuOpenedArgs args)
    {
        if (_config.RaceMode != RaceMode.Raffle) return;
        if (_state.Phase != RafflePhase.Registration) return;
        if (args.MenuType != ContextMenuType.Default) return;
        if (args.Target is not MenuTargetDefault target) return;
        if (!IsPlayerTarget(args, target)) return;

        var name = SanitiseHelper.SanitiseName(target.TargetName);
        if (string.IsNullOrEmpty(name)) return;

        var world = target.TargetHomeWorld.RowId != 0 ? target.TargetHomeWorld.Value.Name.ToString() : string.Empty;
        var entry = string.IsNullOrEmpty(world) ? name : $"{name}@{world}";
        if (_state.IsRegistered(entry)) return;

        args.AddMenuItem(new MenuItem
        {
            Name = new SeString(new TextPayload(MenuLabel)),
            PrefixChar = 'C',
            PrefixColor = 506,
            OnClicked = _ => _state.AddEntry(entry),
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
