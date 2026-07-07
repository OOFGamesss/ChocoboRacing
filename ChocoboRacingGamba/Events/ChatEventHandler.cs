using System;
using ChocoboRacing.Actions;
using ChocoboRacing.Automation;
using ChocoboRacing.Events.ChatHandlers;
using ChocoboRacing.Services;
using ChocoboRacing.State;
using Dalamud.Game.Chat;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;

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
    private readonly JoinHandler _joinHandler;

    public ChatEventHandler(RaceState state, RaceService raceService, PartyService partyService, CustomMessageSender messageSender, ActionQueue actionQueue, IChatGui chatGui, IPluginLog pluginLog, GrandNationalService grandNationalService, Func<bool> isTestingMode, Func<bool> isWindowOpen)
    {
        _chatGui = chatGui;
        _isWindowOpen = isWindowOpen;

        _rollHandler = new RollHandler(state, raceService, partyService, messageSender, actionQueue, chatGui, isTestingMode, pluginLog);
        _betHandler = new BetHandler(state, partyService, actionQueue);
        _joinHandler = new JoinHandler(grandNationalService);
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

        var kind = chatMessage.LogKind;
        bool isTell  = kind == XivChatType.TellIncoming;
        bool isParty = kind == XivChatType.Party
            || kind == XivChatType.Alliance
            || kind == XivChatType.CrossParty;
        bool isPublic = kind == XivChatType.Say
            || kind == XivChatType.Shout
            || kind == XivChatType.Yell;

        if (isTell || isParty || isPublic)
            _joinHandler.Process(text, playerName, playerWorld);

        if (!isTell && !isParty) return;

        _betHandler.Process(text, playerName, playerWorld);
    }
}

