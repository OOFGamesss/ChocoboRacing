using System;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using ECommons.GameHelpers;
using ChocoboRacing.State;

namespace ChocoboRacing.Events.ChatHandlers;

/// <summary>
/// Listens for completed trade events and adjusts player bank balances accordingly.
/// </summary>
public sealed class TradeDetectionService : IDisposable
{
    private readonly RaceState _state;
    private readonly IChatGui _chatGui;
    private readonly Func<bool> _isWindowOpen;

    public TradeDetectionService(RaceState state, IChatGui chatGui, Func<bool> isWindowOpen)
    {
        _state = state;
        _chatGui = chatGui;
        _isWindowOpen = isWindowOpen;
        TradeDetectionManager.OnTradeEnd += OnTradeEnd;
    }

    private void OnTradeEnd(IPlayerCharacter? counterparty, TradeDetectionManager.TradeDescriptor? result)
    {
        if (!_isWindowOpen()) return;
        if (counterparty == null || result == null) return;

        var name = counterparty.Name.TextValue;
        var world = counterparty.HomeWorld.Value.Name.ToString();

        var bank = _state.GetBank(name, world)
                   ?? _state.GetActiveBankByName(name)
                   ?? _state.GetActiveBankByCrossWorldName(name);

        if (bank == null) return;

        var net = (long)result.ReceivedGil;
        if (net <= 0) return;

        _state.AdjustBalance(bank.Name, bank.World, net);
        var act = net > 0 ? "deposited" : "withdrew";
        _chatGui.Print($"[Chocobo Racing] {name} {act} {Math.Abs(net):N0} Gil. New balance: {bank.Balance:N0} Gil.");
    }

    public void Dispose()
    {
        TradeDetectionManager.OnTradeEnd -= OnTradeEnd;
    }
}
