using System.Linq;
using ChocoboRacing.Automation;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;

/// <summary>
/// Handles targeting and initiating trade with players.
/// </summary>
namespace ChocoboRacing.Actions;

public sealed class TradeAction
{
    private const int TargetSettleMs = 300;

    private readonly IObjectTable _objectTable;
    private readonly ITargetManager _targetManager;
    private readonly IChatGui _chatGui;
    private readonly ActionQueue _actionQueue;
    private readonly ChatQueue _chatQueue;

    public TradeAction(IObjectTable objectTable, ITargetManager targetManager, IChatGui chatGui, ActionQueue actionQueue, ChatQueue chatQueue)
    {
        _objectTable = objectTable;
        _targetManager = targetManager;
        _chatGui = chatGui;
        _actionQueue = actionQueue;
        _chatQueue = chatQueue;
    }

    public void InitiateTrade(string playerName)
    {
        var targetPlayer = _objectTable
            .OfType<IPlayerCharacter>()
            .FirstOrDefault(p => p.ObjectKind == ObjectKind.Pc && p.Name.TextValue == playerName);

        if (targetPlayer == null)
        {
            _chatGui.PrintError($"Cannot find {playerName} nearby to trade.");
            return;
        }

        _targetManager.Target = targetPlayer;
        _actionQueue.ScheduleDelayedAction(TargetSettleMs, () => _chatQueue.Enqueue("/trade"));
    }
}
