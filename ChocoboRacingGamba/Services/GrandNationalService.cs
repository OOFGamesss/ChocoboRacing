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
/// Drives the Grand National race mode: registration, rolling the race outcome, announcing
/// the winner, and requesting/collecting entry fees and payouts via trade.
/// </summary>
namespace ChocoboRacing.Services;

public sealed class GrandNationalService : IDisposable
{
    private const long CountdownMs = 3_000;
    private const long MsPerYalm = 1_000;
    private const long FinishBufferMs = 400;

    private readonly PluginConfig _config;
    private readonly GrandNationalState _state;
    private readonly MirrorService _mirror;
    private readonly TradeAction _tradeAction;
    private readonly ActionQueue _actionQueue;
    private readonly RaceHistoryService _historyService;
    private readonly IObjectTable _objectTable;
    private readonly IFramework _framework;
    private readonly IChatGui _chatGui;
    private readonly Func<bool> _isTesting;

    private bool _rolling;
    private long _winnerTraded;

    public GrandNationalState State => _state;
    public bool IsLive => _mirror.SessionId != null && _config.WebMirrorEnabled;
    public bool IsRolling => _rolling;
    public long WinnerTraded => _winnerTraded;

    public GrandNationalService(
        PluginConfig config,
        GrandNationalState state,
        MirrorService mirror,
        TradeAction tradeAction,
        ActionQueue actionQueue,
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
        if (!_config.GrandNationalAutoJoin) return;
        if (_config.RaceMode != RaceMode.GrandNational) return;
        if (_state.Phase != GrandNationalPhase.Registration) return;
        if (string.IsNullOrWhiteSpace(playerName)) return;

        var keyword = (_config.GrandNationalJoinKeyword ?? string.Empty).Trim();
        if (keyword.Length == 0) return;

        var first = message.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (first == null || !first.Equals(keyword, StringComparison.OrdinalIgnoreCase)) return;

        var entry = string.IsNullOrWhiteSpace(playerWorld) ? playerName : $"{playerName}@{playerWorld}";
        _state.AddEntry(entry);
    }

    public void StartRace()
    {
        if (_rolling) return;
        if (_state.Phase != GrandNationalPhase.Closed) return;
        if (!IsLive)
        {
            _chatGui.PrintError("Go Live on the Web Betting tab before starting a Grand National.");
            return;
        }
        if (!_state.BeginRacing()) return;
        _rolling = true;
        _ = RunRaceAsync();
    }

    public void AnnounceRegistrationOpen()
    {
        var hasKeyword = _config.GrandNationalAutoJoin && !string.IsNullOrWhiteSpace(_config.GrandNationalJoinKeyword);
        var template = hasKeyword ? _config.GrandNationalRegistrationMessage : _config.GrandNationalRegistrationNoKeywordMessage;
        Send(FormatGnMessage(template));
    }

    public void AnnounceWinner()
    {
        var runner = _state.WinningRunner();
        if (runner == null) return;
        Send(FormatGnMessage(_config.GrandNationalWinnerMessage, runner.Name, runner.Number));
    }

    public void AnnounceClosingTime()
    {
        if (!_config.GrandNationalCloseTimeEnabled) return;
        Send(FormatGnMessage(_config.GrandNationalClosingTimeMessage));
    }

    public void RequestEntryFee(string entry)
    {
        var name = GrandNationalState.DisplayName(entry);
        var world = GrandNationalState.WorldOf(entry);
        var target = string.IsNullOrEmpty(world) ? name : $"{name}@{world}";
        ChatAction.SendChatMessage($"/tell {target} {FormatGnMessage(_config.GrandNationalRequestFeeMessage, name)}", _chatGui, _isTesting());
    }

    public void SendRunnerInvite(string entry)
    {
        var name = GrandNationalState.DisplayName(entry);
        var world = GrandNationalState.WorldOf(entry);
        var target = string.IsNullOrEmpty(world) ? name : $"{name}@{world}";
        ChatAction.SendChatMessage($"/tell {target} {FormatGnMessage(_config.GrandNationalRunnerInviteMessage, name)}", _chatGui, _isTesting());
    }

    public void TradeRunner(string entry)
    {
        var name = GrandNationalState.DisplayName(entry);
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
            var result = await _mirror.RollGrandNationalAsync().ConfigureAwait(false);
            await _framework.RunOnFrameworkThread(() =>
            {
                if (result == null || result.Winner <= 0)
                {
                    _chatGui.PrintError("Could not roll the Grand National. Please try again.");
                    _state.AbortRacing();
                    return;
                }
                _state.SetWinner(result.Winner);
                var finish = Math.Max(5, _config.GrandNationalFinishLine);
                var animationMs = CountdownMs + finish * MsPerYalm + FinishBufferMs;
                _actionQueue.ScheduleDelayedAction(animationMs, CompleteRace);
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _chatGui.PrintError($"Grand National roll failed: {ex.Message}");
            await _framework.RunOnFrameworkThread(() => _state.AbortRacing()).ConfigureAwait(false);
        }
        finally
        {
            _rolling = false;
        }
    }

    private void CompleteRace()
    {
        if (_state.Phase != GrandNationalPhase.Racing) return;
        _winnerTraded = 0;
        RecordHistory();
        _state.FinishRace();
        if (_config.GrandNationalAutoAnnounceWinner) AnnounceWinner();
    }

    private void OnTradeEnd(IPlayerCharacter? counterparty, TradeDetectionManager.TradeDescriptor? result)
    {
        if (counterparty == null || result == null) return;

        if (_state.Phase == GrandNationalPhase.Finished)
        {
            if (!_state.IsPotPrize) return;
            var winner = _state.WinningRunner();
            if (winner == null || counterparty.Name.TextValue != winner.Name) return;
            var given = result.ReceivedGil < 0 ? -(long)result.ReceivedGil : 0L;
            if (given > 0) _winnerTraded += given;
            return;
        }

        if (_state.Phase == GrandNationalPhase.Registration)
            TryAutoRegister(counterparty, result);
    }

    private void TryAutoRegister(IPlayerCharacter counterparty, TradeDetectionManager.TradeDescriptor result)
    {
        var received = result.ReceivedGil > 0 ? (long)result.ReceivedGil : 0L;
        var name = counterparty.Name.TextValue;
        var world = counterparty.HomeWorld.Value.Name.ToString();
        switch (_state.HandleEntryPayment(name, world, received))
        {
            case GrandNationalEntryResult.Joined:
                _chatGui.Print(_state.IsFree
                    ? $"[Chocobo Racing] {name} joined the Grand National."
                    : $"[Chocobo Racing] {name} joined and paid {UIHelper.FormatGil(received)} toward the entry fee.");
                break;
            case GrandNationalEntryResult.Progressed:
                _chatGui.Print($"[Chocobo Racing] {name} paid {UIHelper.FormatGil(received)} more toward the entry fee.");
                break;
            case GrandNationalEntryResult.Paid:
                _chatGui.Print($"[Chocobo Racing] {name} has paid the entry fee in full and is entered.");
                if (_config.GrandNationalAutoTellPaid)
                    SendRunnerInvite(string.IsNullOrEmpty(world) ? name : $"{name}@{world}");
                break;
        }
    }

    private void RecordHistory()
    {
        var grid = _state.GetGridSnapshot();
        if (grid.Count == 0) return;
        var fee = _state.IsFree ? 0 : _config.GrandNationalEntryFee;
        var net = _state.NetPot;
        var winner = _config.GrandNationalWinner;
        var record = new RaceRecord
        {
            RoundNumber = _config.GrandNationalRaceNumber,
            ChocoboCount = grid.Count,
            PayoutOdds = 0f,
            WinningChocobo = winner,
            Mode = RaceMode.GrandNational,
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
        _config.GrandNationalRaceNumber++;
        _config.Save();
    }

    private string PrizeDisplay()
    {
        if (_state.IsPotPrize) return UIHelper.FormatGil(_state.NetPot);
        var label = _state.PrizeLabel;
        return string.IsNullOrWhiteSpace(label) ? "the prize" : label;
    }

    private string CloseTimeDisplay() => _config.GrandNationalCloseTimeEnabled
        ? ServerTimeUtil.FormatCloseLabel(_config.GrandNationalCloseHour, _config.GrandNationalCloseMinute)
        : "TBA";

    private string TimeLeftDisplay() => _config.GrandNationalCloseTimeEnabled
        ? ServerTimeUtil.FormatTimeLeft(_config.GrandNationalCloseHour, _config.GrandNationalCloseMinute)
        : "TBA";

    private string FormatGnMessage(string template, string? name = null, int number = 0)
    {
        var keyword = _config.GrandNationalAutoJoin ? (_config.GrandNationalJoinKeyword ?? string.Empty).Trim() : string.Empty;
        var url = _mirror.SpectatorUrl;
        if (string.IsNullOrEmpty(url)) url = _config.WebSpectatorUrl ?? string.Empty;
        return template
            .Replace("{prize}", PrizeDisplay())
            .Replace("{entryfee}", _state.IsFree ? "Free" : UIHelper.FormatGil(_config.GrandNationalEntryFee))
            .Replace("{keyword}", keyword)
            .Replace("{runners}", _state.EligibleCount.ToString())
            .Replace("{boostedpot}", UIHelper.FormatGil(_config.GrandNationalBoost))
            .Replace("{closetime}", CloseTimeDisplay())
            .Replace("{timeleft}", TimeLeftDisplay())
            .Replace("{name}", name ?? string.Empty)
            .Replace("{number}", number > 0 ? number.ToString() : string.Empty)
            .Replace("{url}", url);
    }

    private void Send(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        ChatAction.SendChatMessage(message, _chatGui, _isTesting());
    }

    public void Dispose()
    {
        TradeDetectionManager.OnTradeEnd -= OnTradeEnd;
    }
}
