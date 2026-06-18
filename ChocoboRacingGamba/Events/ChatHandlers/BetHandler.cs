using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ChocoboRacing.Automation;
using ChocoboRacing.Models;
using ChocoboRacing.Services;
using ChocoboRacing.State;
using ChocoboRacing.Utility;

namespace ChocoboRacing.Events.ChatHandlers;

/// <summary>
/// Processes incoming party and tell messages to detect and apply chocobo race bets and cash-out requests.
/// </summary>
public sealed class BetHandler
{
    private static readonly Regex PlayerBetRegex = new(@"chocobo\s+(.+?)\s*-\s*(\d+\.\d+[kmbKMB]|[\d,]+[kmbKMB]?)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ShorthandBetRegex = new(@"^\s*(.+?)\s+([\d,]*\d{4,}|\d+\.\d+[kmbKMB]|[\d,]+[kmbKMB])\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AmountRegex = new(@"\b(\d+\.\d+[kmbKMB]|[\d,]+[kmbKMB]?)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CashOutRegex = new(@"\bcash[\s-]?out\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SpacedSuffixRegex = new(@"([\d,]+)\s+([kKmMbB])\b", RegexOptions.Compiled);
    private static readonly Regex DotThousandsRegex = new(@"\b\d{1,3}(?:\.\d{3})+\b", RegexOptions.Compiled);
    private static readonly Regex SpacedThousandsRegex = new(@"\b\d{1,3}(?: \d{3})+\b", RegexOptions.Compiled);

    private static readonly Regex AllInRegex = new(@"\ball\s?in\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex MilSuffixRegex = new(@"(\d)\s*mill?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly RaceState _state;
    private readonly PartyService _partyService;
    private readonly ActionQueue _actionQueue;

    public BetHandler(RaceState state, PartyService partyService, ActionQueue actionQueue)
    {
        _state = state;
        _partyService = partyService;
        _actionQueue = actionQueue;
    }

    public void Process(string messageText, string playerName, string playerWorld)
    {
        if (!_state.IsHosting) return;

        var localPlayer = _partyService.GetLocalPlayer();
        var cleanPlayerName = SanitiseHelper.SanitiseName(playerName);
        var cleanLocalName = SanitiseHelper.SanitiseName(localPlayer.HasValue ? localPlayer.Value.Name : string.Empty);
        var localWorld = localPlayer.HasValue ? localPlayer.Value.World : string.Empty;
        bool isSelfMessage = cleanPlayerName.Equals(cleanLocalName, StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrEmpty(playerWorld) || playerWorld.Equals(localWorld, StringComparison.OrdinalIgnoreCase));
        if (isSelfMessage) return;

        HandleCashOut(messageText, playerName, playerWorld);

        if (_state.Phase == RacePhase.Betting)
        {
            HandleBetPlacement(messageText, playerName, playerWorld);
        }
    }

    private void HandleCashOut(string text, string playerName, string playerWorld)
    {
        if (CashOutRegex.IsMatch(text))
        {
            var cleanName = SanitiseHelper.SanitiseName(playerName);
            var bank = _state.GetBank(cleanName, playerWorld);

            if (bank != null && bank.Balance > 0)
            {
                _actionQueue.ScheduleDelayedAction(0, () => _state.AddCashOutRequest(cleanName, playerWorld));
            }
        }
    }

    private void HandleBetPlacement(string text, string playerName, string playerWorld)
    {
        text = SpacedSuffixRegex.Replace(text, "$1$2");
        text = MilSuffixRegex.Replace(text, "${1}m");
        text = DotThousandsRegex.Replace(text, m => m.Value.Replace(".", ""));
        text = SpacedThousandsRegex.Replace(text, m => m.Value.Replace(" ", ""));

        if (HandleAllIn(text, playerName, playerWorld)) return;

        int chocoboNum = -1;
        long amount = 0;

        var match = PlayerBetRegex.Match(text);
        if (!match.Success) match = ShorthandBetRegex.Match(text);

        if (match.Success)
            ParseExplicitMatch(match, ref chocoboNum, ref amount);

        if (chocoboNum == -1 || amount <= 0)
        {
            chocoboNum = -1;
            amount = 0;
            ParseImplicitMatch(text, ref chocoboNum, ref amount);
        }

        if (chocoboNum != -1 && amount > 0)
            ApplyBetIfValid(playerName, playerWorld, chocoboNum, amount);
    }

    private void ParseExplicitMatch(Match match, ref int chocoboNum, ref long amount)
    {
        var idString = match.Groups[1].Value.Trim();

        if (int.TryParse(idString, out var parsedNum))
        {
            chocoboNum = parsedNum;
        }
        else
        {
            var names = _state.ListChocoboNames();
            for (int i = 0; i < names.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(names[i]) &&
                    idString.Equals(names[i], StringComparison.OrdinalIgnoreCase))
                {
                    chocoboNum = i + 1;
                    break;
                }
            }
        }
        amount = ChatHelper.ParseAmountString(match.Groups[2].Value);
    }

    private void ParseImplicitMatch(string text, ref int chocoboNum, ref long amount)
    {
        var allMatches = AmountRegex.Matches(text);
        if (allMatches.Count == 0) return;

        var names = _state.ListChocoboNames();
        var mentionedChocobos = new HashSet<int>();

        for (int i = 0; i < names.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(names[i]) &&
                Regex.IsMatch(text, $@"\b{Regex.Escape(names[i])}\b", RegexOptions.IgnoreCase))
            {
                mentionedChocobos.Add(i + 1);
            }
        }

        for (int i = 1; i <= _state.ChocoboCount; i++)
        {
            if (Regex.IsMatch(text, $@"\b{i}\b"))
            {
                mentionedChocobos.Add(i);
            }
        }

        var definiteAmounts = new List<long>();

        foreach (Match m in allMatches)
        {
            var raw = m.Groups[1].Value;
            var hasMultiplierSuffix = raw.EndsWith("k", StringComparison.OrdinalIgnoreCase)
                                   || raw.EndsWith("m", StringComparison.OrdinalIgnoreCase)
                                   || raw.EndsWith("b", StringComparison.OrdinalIgnoreCase);

            var pureDigits = raw.Replace(",", "");
            var isLargeRaw = !hasMultiplierSuffix
                             && long.TryParse(pureDigits, out var rawVal)
                             && rawVal >= 1000;

            var isChocoboIdentifier = !hasMultiplierSuffix
                                      && int.TryParse(pureDigits, out var smallVal)
                                      && mentionedChocobos.Contains(smallVal);

            if (isChocoboIdentifier) continue;

            definiteAmounts.Add(ChatHelper.ParseAmountString(raw));
        }

        if (definiteAmounts.Count == 1 && definiteAmounts[0] > 0 && mentionedChocobos.Count == 1)
        {
            chocoboNum = mentionedChocobos.First();
            amount = definiteAmounts[0];
        }
    }

    private bool HandleAllIn(string text, string playerName, string playerWorld)
    {
        if (!AllInRegex.IsMatch(text)) return false;

        var chocoboNum = ResolveChocoboFromText(text);
        if (chocoboNum == -1) return true;

        var cleanName = SanitiseHelper.SanitiseName(playerName);
        var bank = _state.GetBank(cleanName, playerWorld);
        if (bank == null || bank.Balance <= 0) return true;

        ApplyBetIfValid(playerName, playerWorld, chocoboNum, bank.Balance);
        return true;
    }

    private int ResolveChocoboFromText(string text)
    {
        var names = _state.ListChocoboNames();
        var mentioned = new HashSet<int>();

        for (int i = 0; i < names.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(names[i]) &&
                Regex.IsMatch(text, $@"\b{Regex.Escape(names[i])}\b", RegexOptions.IgnoreCase))
            {
                mentioned.Add(i + 1);
            }
        }

        for (int i = 1; i <= _state.ChocoboCount; i++)
        {
            if (Regex.IsMatch(text, $@"\b{i}\b"))
                mentioned.Add(i);
        }

        return mentioned.Count == 1 ? mentioned.First() : -1;
    }

    private void ApplyBetIfValid(string playerName, string playerWorld, int chocoboNum, long amount)
    {
        var localPlayer = _partyService.GetLocalPlayer();
        var localName = localPlayer.HasValue ? localPlayer.Value.Name : string.Empty;
        var localWorld = localPlayer.HasValue ? localPlayer.Value.World : string.Empty;

        var cleanPlayerName = SanitiseHelper.SanitiseName(playerName);
        var cleanLocalName = SanitiseHelper.SanitiseName(localName);

        bool isSelf = cleanPlayerName.Equals(cleanLocalName, StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrEmpty(playerWorld) || playerWorld.Equals(localWorld, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(cleanPlayerName) && !isSelf &&
            chocoboNum >= 1 && chocoboNum <= _state.ChocoboCount)
        {
            _actionQueue.ScheduleDelayedAction(0, () =>
                _state.AddPendingBet(playerName, playerWorld, chocoboNum, amount));
        }
    }
}
