using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ChocoboRacing.Actions;
using ChocoboRacing.Automation;
using ChocoboRacing.Config;
using ChocoboRacing.Models;
using ChocoboRacing.State;
using ChocoboRacing.UI.Components;
using ChocoboRacing.Utility;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using ECommons.GameHelpers;

/// <summary>
/// Drives the raffle race mode: registration, rolling the race outcome, announcing
/// the winner, and requesting/collecting entry fees and payouts via trade.
/// </summary>
namespace ChocoboRacing.Services;

public sealed class RaffleService : IDisposable
{
    private const long CountdownMs = 3_000;
    private const long MsPerYalm = 1_000;
    private const long FinishBufferMs = 400;

    private readonly PluginConfig _config;
    private readonly RaffleState _state;
    private readonly MirrorService _mirror;
    private readonly TradeAction _tradeAction;
    private readonly ActionQueue _actionQueue;
    private readonly ChatQueue _chatQueue;
    private readonly RaceHistoryService _historyService;
    private readonly IObjectTable _objectTable;
    private readonly IFramework _framework;
    private readonly IChatGui _chatGui;
    private readonly Func<bool> _isTesting;

    private bool _rolling;
    private long _winnerTraded;

    public RaffleState State => _state;
    public bool IsLive => _mirror.SessionId != null && _config.WebMirrorEnabled;
    public bool IsRolling => _rolling;
    public long WinnerTraded => _winnerTraded;

    public RaffleService(
        PluginConfig config,
        RaffleState state,
        MirrorService mirror,
        TradeAction tradeAction,
        ActionQueue actionQueue,
        ChatQueue chatQueue,
        RaceHistoryService historyService,
        IObjectTable objectTable,
        IFramework framework,
        IChatGui chatGui,
        Func<bool> isTesting)
    {
        _config = config;
        _state = state;
        _mirror = mirror;
        _tradeAction = tradeAction;
        _actionQueue = actionQueue;
        _chatQueue = chatQueue;
        _historyService = historyService;
        _objectTable = objectTable;
        _framework = framework;
        _chatGui = chatGui;
        _isTesting = isTesting;
        TradeDetectionManager.OnTradeEnd += OnTradeEnd;
    }

    public List<string> GetNearbyPlayers()
    {
        return _objectTable
            .OfType<IPlayerCharacter>()
            .Where(p => p.ObjectKind == ObjectKind.Pc && !string.IsNullOrEmpty(p.Name.TextValue))
            .Select(p => $"{p.Name.TextValue}@{p.HomeWorld.Value.Name.ToString()}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public void TryHandleJoin(string playerName, string playerWorld, string message)
    {
        if (!_config.RaffleAutoJoin) return;
        if (_config.RaceMode != RaceMode.Raffle) return;
        if (_state.Phase != RafflePhase.Registration) return;
        if (string.IsNullOrWhiteSpace(playerName)) return;

        var keyword = (_config.RaffleJoinKeyword ?? string.Empty).Trim();
        if (keyword.Length == 0) return;

        var first = message.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (first == null || !first.Equals(keyword, StringComparison.OrdinalIgnoreCase)) return;

        var entry = string.IsNullOrWhiteSpace(playerWorld) ? playerName : $"{playerName}@{playerWorld}";
        _state.AddEntry(entry);
    }

    public void StartRace()
    {
        if (_rolling) return;
        if (_state.Phase != RafflePhase.Closed) return;
        if (!IsLive)
        {
            _chatGui.PrintError("Go Live on the Webview tab before starting a raffle.");
            return;
        }
        if (!_state.BeginRacing()) return;
        _rolling = true;
        _ = RunRaceAsync();
    }

    public void AnnounceRegistrationOpen()
    {
        var hasKeyword = _config.RaffleAutoJoin && !string.IsNullOrWhiteSpace(_config.RaffleJoinKeyword);
        var template = hasKeyword ? _config.RaffleRegistrationMessage : _config.RaffleRegistrationNoKeywordMessage;
        Send(FormatGnMessage(template));
    }

    public void AnnounceWinner()
    {
        var runner = _state.WinningRunner();
        if (runner == null) return;
        Send(FormatGnMessage(_config.RaffleWinnerMessage, runner.Name, runner.Number));
    }

    public void AnnounceClosingTime()
    {
        if (!_config.RaffleCloseTimeEnabled) return;
        Send(FormatGnMessage(_config.RaffleClosingTimeMessage));
    }

    public void RequestEntryFee(string entry)
    {
        var name = RaffleState.DisplayName(entry);
        var world = RaffleState.WorldOf(entry);
        var target = string.IsNullOrEmpty(world) ? name : $"{name}@{world}";
        _chatQueue.Enqueue($"/tell {target} {FormatGnMessage(_config.RaffleRequestFeeMessage, name)}", echoInTesting: true);
    }

    public void SendRunnerInvite(string entry)
    {
        var name = RaffleState.DisplayName(entry);
        var world = RaffleState.WorldOf(entry);
        var target = string.IsNullOrEmpty(world) ? name : $"{name}@{world}";
        _chatQueue.Enqueue($"/tell {target} {FormatGnMessage(_config.RaffleRunnerInviteMessage, name)}", echoInTesting: true);
    }

    public void TradeRunner(string entry)
    {
        var name = RaffleState.DisplayName(entry);
        _tradeAction.InitiateTrade(name);
    }

    public void TradeWinner()
    {
        var runner = _state.WinningRunner();
        if (runner == null) return;
        _tradeAction.InitiateTrade(runner.Name);
    }

    private async Task RunRaceAsync()
    {
        try
        {
            var result = await _mirror.RollRaffleAsync().ConfigureAwait(false);
            await _framework.RunOnFrameworkThread(() =>
            {
                if (result == null || result.Winner <= 0)
                {
                    _chatGui.PrintError("Could not roll the raffle. Please try again.");
                    _state.AbortRacing();
                    return;
                }
                _state.SetWinner(result.Winner);
                var finish = Math.Max(5, _config.RaffleFinishLine);
                var animationMs = CountdownMs + finish * MsPerYalm + FinishBufferMs;
                _actionQueue.ScheduleDelayedAction(animationMs, CompleteRace);
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _chatGui.PrintError($"Raffle roll failed: {ex.Message}");
            await _framework.RunOnFrameworkThread(() => _state.AbortRacing()).ConfigureAwait(false);
        }
        finally
        {
            _rolling = false;
        }
    }

    private void CompleteRace()
    {
        if (_state.Phase != RafflePhase.Racing) return;
        _winnerTraded = 0;
        RecordHistory();
        _state.FinishRace();
        if (_config.RaffleAutoAnnounceWinner) AnnounceWinner();
    }

    private void OnTradeEnd(IPlayerCharacter? counterparty, TradeDetectionManager.TradeDescriptor? result)
    {
        if (counterparty == null || result == null) return;

        if (_state.Phase == RafflePhase.Finished)
        {
            if (!_state.IsPotPrize) return;
            var winner = _state.WinningRunner();
            if (winner == null || counterparty.Name.TextValue != winner.Name) return;
            var given = result.ReceivedGil < 0 ? -(long)result.ReceivedGil : 0L;
            if (given > 0) _winnerTraded += given;
            return;
        }

        if (_state.Phase == RafflePhase.Registration)
            TryAutoRegister(counterparty, result);
    }

    private void TryAutoRegister(IPlayerCharacter counterparty, TradeDetectionManager.TradeDescriptor result)
    {
        var received = result.ReceivedGil > 0 ? (long)result.ReceivedGil : 0L;
        var name = counterparty.Name.TextValue;
        var world = counterparty.HomeWorld.Value.Name.ToString();
        switch (_state.HandleEntryPayment(name, world, received))
        {
            case RaffleEntryResult.Joined:
                _chatGui.Print(_state.IsFree
                    ? $"[Chocobo Racing] {name} joined the raffle."
                    : $"[Chocobo Racing] {name} joined and paid {UIHelper.FormatGil(received)} toward the entry fee.");
                break;
            case RaffleEntryResult.Progressed:
                _chatGui.Print($"[Chocobo Racing] {name} paid {UIHelper.FormatGil(received)} more toward the entry fee.");
                break;
            case RaffleEntryResult.Paid:
                _chatGui.Print($"[Chocobo Racing] {name} has paid the entry fee in full and is entered.");
                if (_config.RaffleAutoTellPaid)
                    SendRunnerInvite(string.IsNullOrEmpty(world) ? name : $"{name}@{world}");
                break;
        }
    }

    private void RecordHistory()
    {
        var grid = _state.GetGridSnapshot();
        if (grid.Count == 0) return;
        var fee = _state.IsFree ? 0 : _config.RaffleEntryFee;
        var net = _state.NetPot;
        var winner = _config.RaffleWinner;
        var record = new RaceRecord
        {
            RoundNumber = _config.RaffleRaceNumber,
            ChocoboCount = grid.Count,
            PayoutOdds = 0f,
            WinningChocobo = winner,
            Mode = RaceMode.Raffle,
        };
        foreach (var r in grid)
            record.Bets.Add(new BetRecord
            {
                PlayerName = r.Name,
                PlayerWorld = r.World,
                ChocoboNumber = r.Number,
                Amount = fee,
                IsWinner = r.Number == winner,
                Payout = r.Number == winner ? net : 0,
            });
        var (sessionId, _) = _historyService.EnsureActiveSession(DateTime.Now);
        _historyService.RecordRace(sessionId, record);
        _config.RaffleRaceNumber++;
        _config.Save();
    }

    private string PrizeDisplay()
    {
        if (_state.IsPotPrize) return UIHelper.FormatGil(_state.NetPot);
        var label = _state.PrizeLabel;
        return string.IsNullOrWhiteSpace(label) ? "the prize" : label;
    }

    private string CloseTimeDisplay() => _config.RaffleCloseTimeEnabled
        ? ServerTimeUtil.FormatCloseLabel(_config.RaffleCloseHour, _config.RaffleCloseMinute)
        : "TBA";

    private string TimeLeftDisplay() => _config.RaffleCloseTimeEnabled
        ? ServerTimeUtil.FormatTimeLeft(_config.RaffleCloseHour, _config.RaffleCloseMinute)
        : "TBA";

    private string FormatGnMessage(string template, string? name = null, int number = 0)
    {
        var keyword = _config.RaffleAutoJoin ? (_config.RaffleJoinKeyword ?? string.Empty).Trim() : string.Empty;
        var url = _mirror.SpectatorUrl;
        if (string.IsNullOrEmpty(url)) url = _config.WebSpectatorUrl ?? string.Empty;
        return template
            .Replace("{prize}", PrizeDisplay())
            .Replace("{entryfee}", _state.IsFree ? "Free" : UIHelper.FormatGil(_config.RaffleEntryFee))
            .Replace("{keyword}", keyword)
            .Replace("{runners}", _state.EligibleCount.ToString())
            .Replace("{boostedpot}", UIHelper.FormatGil(_config.RaffleBoost))
            .Replace("{closetime}", CloseTimeDisplay())
            .Replace("{timeleft}", TimeLeftDisplay())
            .Replace("{name}", name ?? string.Empty)
            .Replace("{number}", number > 0 ? number.ToString() : string.Empty)
            .Replace("{url}", url);
    }

    private void Send(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        _chatQueue.Enqueue(message, echoInTesting: true);
    }

    public void Dispose()
    {
        TradeDetectionManager.OnTradeEnd -= OnTradeEnd;
    }
}
