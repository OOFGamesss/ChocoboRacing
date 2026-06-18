using System.Linq;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using ChocoboRacing.Automation;

namespace ChocoboRacing.Actions;

/// <summary>
/// Handles targeting and initiating trade with players.
/// </summary>
public sealed class TradeAction
{
    private readonly IObjectTable _objectTable;
    private readonly ITargetManager _targetManager;
    private readonly IChatGui _chatGui;
    private readonly ActionQueue _actionQueue;

    public TradeAction(IObjectTable objectTable, ITargetManager targetManager, IChatGui chatGui, ActionQueue actionQueue)
    {
        _objectTable = objectTable;
        _targetManager = targetManager;
        _chatGui = chatGui;
        _actionQueue = actionQueue;
    }

    public void InitiateTrade(string playerName)
    {
        var targetPlayer = _objectTable
            .OfType<IPlayerCharacter>()
            .FirstOrDefault(p => p.ObjectKind == ObjectKind.Pc && p.Name.TextValue == playerName);

        if (targetPlayer != null)
        {
            _targetManager.Target = targetPlayer;
            _actionQueue.ScheduleDelayedAction(300, () =>
            {
                ChatAction.SendChatMessage("/trade");
            });
        }
        else
        {
            _chatGui.PrintError($"Cannot find {playerName} nearby to trade.");
        }
    }
}
