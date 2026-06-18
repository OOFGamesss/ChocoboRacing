using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Dalamud.Plugin.Services;
using ChocoboRacing.Models;
using ChocoboRacing.Automation;
using ChocoboRacing.Utility;
using ChocoboRacing.State;

namespace ChocoboRacing.Actions;

/// <summary>
/// Handles formatting and dispatching multi-line chat templates.
/// </summary>
public sealed class CustomMessageSender
{
    private static readonly Regex WaitTagRegex =
        new(@"\s*<wait\.(\d+)>\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly ActionQueue _actionQueue;
    private readonly IChatGui _chatGui;
    private int _sendBatchId;
    public bool IsSendingMessages { get; private set; }

    public CustomMessageSender(ActionQueue actionQueue, IChatGui chatGui)
    {
        _actionQueue = actionQueue;
        _chatGui = chatGui;
    }

    public int SendMessage(string prefix, string template, RaceState state, bool isTestingMode, int winner = 0, List<(string name, string, long winnings)>? winners = null)
    {
        if (string.IsNullOrWhiteSpace(template)) return 0;

        var msg = ReplaceBasicTags(template, prefix, state);
        if (winner > 0) msg = ReplaceWinnerTags(msg, state, winner, winners);

        msg = ReplaceBetListTag(msg, state, state.Config.BetlistLayout);
        msg = ReplaceRaceListTag(msg, state);
        msg = ReplaceChocoboNamesTag(msg, state);

        return DispatchLines(prefix, msg, isTestingMode);
    }

    private string ReplaceBasicTags(string text, string prefix, RaceState state)
    {
        var output = text.Replace("{prefix}", prefix);
        output = output.Replace("{chocobos}", state.ChocoboCount.ToString());
        output = output.Replace("{distance}", state.FinishLine.ToString());
        output = output.Replace("{perfectodds}", state.Config.PerfectRaceOdds.ToString("F2"));
        return output.Replace("{odds}", state.PayoutOdds.ToString("F2"));
    }

    private string ReplaceWinnerTags(string text, RaceState state, int winner, List<(string name, string, long winnings)>? winners)
    {
        var outText = text.Replace("{winningchocobo}", TrackVisualiser.GetChocoboName(state.Config, winner));
        if (!outText.Contains("{winnerlist}")) return outText;

        if (winners != null && winners.Count > 0)
        {
            var sb = new StringBuilder();
            foreach (var (name, _, winnings) in winners)
            {
                sb.AppendLine($"{name}: {winnings:N0} Gil");
            }
            return outText.Replace("{winnerlist}", sb.ToString().TrimEnd());
        }
        return outText.Replace("{winnerlist}", "No bets on the winner.");
    }

    private string ReplaceBetListTag(string text, RaceState state, BetlistLayout layout)
    {
        if (!text.Contains("{betlist}")) return text;
        return layout == BetlistLayout.SplitByPlayer
            ? text.Replace("{betlist}", BuildBetListByPlayer(state))
            : text.Replace("{betlist}", BuildBetListByChocobo(state));
    }

    private string BuildBetListByChocobo(RaceState state)
    {
        var sb = new StringBuilder();
        bool hasBets = false;
        sb.AppendLine("The following bets have been placed:");

        for (int i = 1; i <= state.ChocoboCount; i++)
        {
            var bets = state.GetBetsForChocobo(i).Where(b => b.Status == BetStatus.Confirmed).ToList();
            if (bets.Count == 0) continue;
            hasBets = true;
            var betStrings = bets.Select(b => $"{b.PlayerName} ({b.Amount:N0} Gil)");
            sb.AppendLine($" {TrackVisualiser.GetChocoboName(state.Config, i)}: {string.Join(", ", betStrings)}");
        }

        if (!hasBets) sb.AppendLine("No bets placed.");
        return sb.ToString().TrimEnd();
    }

    private string BuildBetListByPlayer(RaceState state)
    {
        var sb = new StringBuilder();
        sb.AppendLine("The following bets have been placed:");

        var allBets = Enumerable.Range(1, state.ChocoboCount)
            .SelectMany(i => state.GetBetsForChocobo(i)
                .Where(b => b.Status == BetStatus.Confirmed)
                .Select(b => (Chocobo: i, Bet: b)))
            .GroupBy(x => x.Bet.PlayerName)
            .OrderBy(g => g.Key)
            .ToList();

        if (allBets.Count == 0)
        {
            sb.AppendLine("No bets placed.");
            return sb.ToString().TrimEnd();
        }

        foreach (var group in allBets)
        {
            var byAmount = group
                .GroupBy(x => x.Bet.Amount)
                .OrderBy(g => g.Min(x => x.Chocobo));

            var parts = byAmount.Select(g =>
            {
                var names = string.Join(", ", g.OrderBy(x => x.Chocobo).Select(x => TrackVisualiser.GetChocoboName(state.Config, x.Chocobo)));
                return $"{names} ({g.Key:N0} Gil)";
            });

            sb.AppendLine($" {group.Key}: {string.Join(", ", parts)}");
        }

        return sb.ToString().TrimEnd();
    }

    private string ReplaceRaceListTag(string text, RaceState state)
    {
        if (!text.Contains("{racelist}")) return text;

        var sb = new StringBuilder();
        for (int i = 1; i <= state.ChocoboCount; i++)
        {
            sb.AppendLine(TrackVisualiser.GetTrackVisual(state.Config, i));
        }
        return text.Replace("{racelist}", sb.ToString().TrimEnd());
    }

    private string ReplaceChocoboNamesTag(string text, RaceState state)
    {
        if (!text.Contains("{chocobonames}")) return text;

        var sb = new StringBuilder();
        for (int i = 1; i <= state.ChocoboCount; i++)
        {
            sb.AppendLine($" {TrackVisualiser.GetChocoboName(state.Config, i)}");
        }
        return text.Replace("{chocobonames}", sb.ToString().TrimEnd());
    }

    private int DispatchLines(string prefix, string fullMessage, bool isTestingMode)
    {
        var lines = fullMessage.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0) return 0;

        int batchId = ++_sendBatchId;
        IsSendingMessages = true;
        int delayMs = 0;
        int lastSendTime = 0;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            var lineDelay = 500;

            var match = WaitTagRegex.Match(trimmed);
            if (match.Success)
            {
                trimmed = trimmed[..match.Index].TrimEnd();
                if (int.TryParse(match.Groups[1].Value, out var seconds))
                    lineDelay = Math.Clamp(seconds, 1, 60) * 1000;
            }

            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                var formatted = trimmed;
                if (!formatted.StartsWith(prefix) && !formatted.StartsWith("/"))
                    formatted = $"{prefix} {formatted}";

                lastSendTime = delayMs;
                if (delayMs == 0)
                    ChatAction.SendChatMessage(formatted, _chatGui, isTestingMode);
                else
                {
                    var captured = formatted;
                    _actionQueue.ScheduleDelayedAction(delayMs, () => ChatAction.SendChatMessage(captured, _chatGui, isTestingMode));
                }
            }

            delayMs += lineDelay;
        }

        _actionQueue.ScheduleDelayedAction(delayMs, () =>
        {
            if (_sendBatchId == batchId)
                IsSendingMessages = false;
        });

        return lastSendTime;
    }
}
