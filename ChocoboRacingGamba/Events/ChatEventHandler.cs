using System;
using Dalamud.Game.Chat;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;
using ChocoboRacing.Automation;
using ChocoboRacing.State;
using ChocoboRacing.Services;
using ChocoboRacing.Actions;
using ChocoboRacing.Events.ChatHandlers;

namespace ChocoboRacing.Events;

/// <summary>
/// Subscribes to chat events and routes them to independent parsers.
/// </summary>
public sealed class ChatEventHandler
{
    private readonly IChatGui _chatGui;
    private readonly Func<bool> _isWindowOpen;

    private readonly RollHandler _rollHandler;
    private readonly BetHandler _betHandler;

    public ChatEventHandler(RaceState state, RaceService raceService, PartyService partyService, CustomMessageSender messageSender, ActionQueue actionQueue, IChatGui chatGui, IPluginLog pluginLog, Func<bool> isTestingMode, Func<bool> isWindowOpen)
    {
        _chatGui = chatGui;
        _isWindowOpen = isWindowOpen;

        _rollHandler = new RollHandler(state, raceService, partyService, messageSender, actionQueue, chatGui, isTestingMode, pluginLog);
        _betHandler = new BetHandler(state, partyService, actionQueue);
    }

    public void Subscribe() => _chatGui.ChatMessage += OnChatMessage;
    public void Unsubscribe() => _chatGui.ChatMessage -= OnChatMessage;
    public void ExpectLocalRoll() => _rollHandler.ExpectLocalRoll();
    public void SimulateRoll(int chocoboCount) => _rollHandler.SimulateRoll(chocoboCount);

    private void OnChatMessage(IHandleableChatMessage chatMessage)
    {
        if (!_isWindowOpen()) return;

        var playerName  = ChatHelper.ExtractPlayerName(chatMessage.Sender);
        var playerWorld = ChatHelper.ExtractPlayerWorld(chatMessage.Sender);
        var text        = chatMessage.Message.TextValue.Replace("\u00a0", " ");

        if (string.IsNullOrEmpty(text)) return;

        if (_rollHandler.Process(chatMessage.Message, playerName, playerWorld, chatMessage.LogKind, chatMessage.Sender)) return;

        bool isTell  = chatMessage.LogKind == XivChatType.TellIncoming;
        bool isParty = chatMessage.LogKind == XivChatType.Party
            || chatMessage.LogKind == XivChatType.Alliance
            || chatMessage.LogKind == XivChatType.CrossParty;

        if (!isTell && !isParty) return;

        _betHandler.Process(text, playerName, playerWorld);
    }
}

