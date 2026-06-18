using System;
using System.Linq;
using ChocoboRacing.Models;

namespace ChocoboRacing.State;

/// <summary>
/// Race state partial covering settings updates, resets, and phase transitions from idle.
/// </summary>
public sealed partial class RaceState
{
    public void UpdateSettings(int chocoboCount, int finishLine, float payoutOdds)
    {
        lock (_lock)
        {
            if (Phase != RacePhase.Idle) return;

            Config.ChocoboCount = Math.Clamp(chocoboCount, MinChocobos, MaxChocobos);
            Config.FinishLine = Math.Clamp(finishLine, MinFinishLine, MaxFinishLine);
            Config.PayoutOdds = Math.Clamp(payoutOdds, MinPayoutOdds, MaxPayoutOdds);
            Config.ChocoboPositions = new int[Config.ChocoboCount];
            Config.Save();
        }
    }

    public void ResetToIdle()
    {
        lock (_lock)
        {
            Config.CurrentPhase = RacePhase.Idle;
            Config.CurrentBets.Clear();
            Config.ChocoboPositions = new int[Config.ChocoboCount];
            Config.Save();
        }
    }

    public void ResetForNextRace()
    {
        lock (_lock)
        {
            Config.CurrentPhase = RacePhase.Idle;
            Config.RoundNumber++;
            Config.CurrentBets.Clear();
            Config.ChocoboPositions = new int[Config.ChocoboCount];
            Config.Save();
        }
    }

    public void VoidRace()
    {
        lock (_lock)
        {
            foreach (var bet in Config.CurrentBets.Where(b => b.Status == BetStatus.Confirmed))
            {
                var bank = GetOrCreateBank(bet.PlayerName, bet.PlayerWorld);
                bank.Balance += bet.Amount;
            }

            Config.CurrentPhase = RacePhase.Idle;
            Config.CurrentBets.Clear();
            Config.ChocoboPositions = new int[Config.ChocoboCount];
            Config.Save();
        }
    }
}
