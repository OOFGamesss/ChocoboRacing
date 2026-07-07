using System.Collections.Generic;
using System.Linq;
using ChocoboRacing.Config;
using ChocoboRacing.Models;
using ChocoboRacing.State;

/// <summary>
/// Handles race movement, logic, and evaluation decoupled from state storage.
/// </summary>
namespace ChocoboRacing.Services;

public sealed class RaceService
{
    private readonly PluginConfig _config;
    private readonly RaceState _state;

    public RaceService(PluginConfig config, RaceState state)
    {
        _config = config;
        _state = state;
    }

    public bool AdvanceChocobo(int chocoboNumber)
    {
        if (chocoboNumber < 1 || chocoboNumber > _config.ChocoboCount) return false;
        var index = chocoboNumber - 1;
        if (_config.ChocoboPositions[index] >= _config.FinishLine) return false;

        _config.ChocoboPositions[index]++;
        _config.Save();

        return _config.ChocoboPositions[index] >= _config.FinishLine;
    }

    public int GetWinningChocobo()
    {
        for (var i = 0; i < _config.ChocoboPositions.Length; i++)
        {
            if (_config.ChocoboPositions[i] >= _config.FinishLine) return i + 1;
        }
        return 0;
    }

    public bool HasWinner() => GetWinningChocobo() > 0;

    public List<(string Name, string World, long Winnings)> CalculateWinnings(int winningChocobo)
    {
        var winners = new List<(string Name, string World, long Winnings)>();
        var winningBets = _config.CurrentBets.Where(b => b.ChocoboNumber == winningChocobo && b.Status == BetStatus.Confirmed).ToList();

        var mult = _state.GetPayoutMultiplierForWinner(winningChocobo);
        foreach (var bet in winningBets)
        {
            var payout = (long)(bet.Amount * mult);
            _state.AdjustBalance(bet.PlayerName, bet.PlayerWorld, payout);
            winners.Add((bet.PlayerName, bet.PlayerWorld, payout));
        }

        _config.Save();
        return winners;
    }

    public List<BetRecord> GenerateBetRecords(int winningChocobo)
    {
        var mult = _state.GetPayoutMultiplierForWinner(winningChocobo);
        return _config.CurrentBets.Where(b => b.Status == BetStatus.Confirmed).Select(b => new BetRecord
        {
            PlayerName = b.PlayerName,
            PlayerWorld = b.PlayerWorld,
            ChocoboNumber = b.ChocoboNumber,
            Amount = b.Amount,
            IsWinner = b.ChocoboNumber == winningChocobo,
            Payout = b.ChocoboNumber == winningChocobo ? (long)(b.Amount * mult) : 0
        }).ToList();
    }
}
