using System;
using System.Collections.Generic;
using ChocoboRacing.Actions;
using ChocoboRacing.Automation;
using ChocoboRacing.Models;
using ChocoboRacing.Services;
using ChocoboRacing.State;
using ChocoboRacing.Utility;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;

namespace ChocoboRacing.Events.ChatHandlers;

/// <summary>
/// Detects native /dice roll results in chat and advances the race accordingly.
/// </summary>
public sealed class RollHandler
{
    private readonly RaceState _state;
    private readonly RaceService _raceService;
    private readonly PartyService _partyService;
    private readonly CustomMessageSender _messageSender;
    private readonly ActionQueue _actionQueue;
    private readonly ChatQueue _chatQueue;
    private readonly IChatGui _chatGui;
    private readonly Func<bool> _isTestingMode;
    private readonly IPluginLog _pluginLog;
    private volatile bool _expectingLocalRoll;

    public RollHandler(RaceState state, RaceService raceService, PartyService partyService, CustomMessageSender messageSender, ActionQueue actionQueue, ChatQueue chatQueue, IChatGui chatGui, Func<bool> isTestingMode, IPluginLog pluginLog)
    {
        _state = state;
        _raceService = raceService;
        _partyService = partyService;
        _messageSender = messageSender;
        _actionQueue = actionQueue;
        _chatQueue = chatQueue;
        _chatGui = chatGui;
        _isTestingMode = isTestingMode;
        _pluginLog = pluginLog;
    }

    public void ExpectLocalRoll() => _expectingLocalRoll = true;

    public bool Process(SeString message, string senderName, string senderWorld, XivChatType type, SeString rawSender)
    {
        PlayerPayload? playerPayload = null;
        TextPayload? lastTextPayload = null;

        foreach (var payload in message.Payloads)
        {
            if (payload is PlayerPayload pp) playerPayload = pp;
            if (payload is TextPayload tp) lastTextPayload = tp;
        }

        bool isNamedRoll = playerPayload != null;
        bool couldBeYouRoll = !isNamedRoll && (string.IsNullOrEmpty(senderName) || _expectingLocalRoll);
        if (!isNamedRoll && !couldBeYouRoll) return false;

        if (lastTextPayload == null) return false;
        var integers = ExtractIntegers(lastTextPayload.Text ?? string.Empty);
        if (integers.Count == 0) return false;

        int rollValue, rollMax;
        if (integers.Count >= 3)
        {
            rollMax   = integers[integers.Count - 2];
            rollValue = integers[integers.Count - 1];
        }
        else
        {
            rollValue = integers[0];
            rollMax   = integers.Count >= 2 ? integers[1] : 0;

            if (rollMax == 0)
            {
                foreach (var payload in message.Payloads)
                {
                    if (payload is TextPayload tp && tp != lastTextPayload)
                    {
                        var rangeNums = ExtractIntegers(tp.Text ?? string.Empty);
                        if (rangeNums.Count >= 2) { rollMax = rangeNums[rangeNums.Count - 1]; break; }
                    }
                }
            }
        }

        if (_state.Phase != RacePhase.Racing) return false;

        if (rollMax == 0 || rollMax != _state.ChocoboCount) return isNamedRoll;

        if (rollValue < 1 || rollValue > _state.ChocoboCount) return true;

        string displayName = isNamedRoll
            ? StripWorld(SanitiseHelper.SanitiseName(playerPayload!.PlayerName))
            : Plugin.ObjectTable.LocalPlayer?.Name.TextValue ?? string.Empty;

        if (_expectingLocalRoll)
        {
            _actionQueue.ScheduleDelayedAction(0, () => ProcessRollUpdate(rollValue));
            return true;
        }

        return true;
    }

    public void SimulateRoll(int chocoboCount)
    {
        var roll = Random.Shared.Next(1, chocoboCount + 1);
        _actionQueue.ScheduleDelayedAction(0, () => ProcessRollUpdate(roll));
    }

    private void ProcessRollUpdate(int chocoboNum)
    {
        var isWinner = _raceService.AdvanceChocobo(chocoboNum);
        _expectingLocalRoll = false;

        var visual = TrackVisualiser.GetTrackVisual(_state.Config, chocoboNum);
        var prefix = _partyService.GetChatPrefix();

        _actionQueue.ScheduleDelayedAction(500, () =>
            _chatQueue.Enqueue($"{prefix} {visual}", echoInTesting: true,
                onSent: () => { if (isWinner) HandleWinner(chocoboNum); }));
    }

    private static string StripWorld(string name) => name.Split('@')[0].Trim();

    private void HandleWinner(int winningChocobo)
    {
        var isTestingMode = _isTestingMode();
        var winners       = _raceService.CalculateWinnings(winningChocobo);
        var betRecords    = _raceService.GenerateBetRecords(winningChocobo);

        _state.FinishRace(winningChocobo, betRecords, isTestingMode);

        _actionQueue.ScheduleDelayedAction(500, () =>
        {
            if (!isTestingMode)
            {
                _chatQueue.Enqueue(winners.Count > 0
                    ? _state.Config.WinnerLineMessage
                    : _state.Config.LoseLineMessage);
            }

            _messageSender.SendMessage(
                _partyService.GetChatPrefix(),
                _state.Config.RaceWinnerMessage,
                _state,
                winningChocobo,
                winners);
        });
    }

    private static List<int> ExtractIntegers(string text)
    {
        var result = new List<int>();
        int i = 0;
        while (i < text.Length)
        {
            if (char.IsDigit(text[i]))
            {
                int start = i;
                while (i < text.Length && char.IsDigit(text[i])) i++;
                if (int.TryParse(text.AsSpan(start, i - start), out var val))
                    result.Add(val);
            }
            else
            {
                i++;
            }
        }
        return result;
    }
}
