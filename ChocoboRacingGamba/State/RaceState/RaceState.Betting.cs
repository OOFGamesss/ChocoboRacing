using System;
using System.Collections.Generic;
using System.Linq;
using ChocoboRacing.Models;
using ChocoboRacing.Utility;

namespace ChocoboRacing.State;

/// <summary>
/// Race state partial covering bet placement, acceptance, and removal during the betting phase.
/// </summary>
public sealed partial class RaceState
{
    public long ClampedBetAmount(string name, string world, int chocoboNumber, long amount)
    {
        lock (_lock) return ClampedBetAmountUnderLock(name, world, chocoboNumber, amount);
    }

    private long ClampedBetAmountUnderLock(string name, string world, int chocoboNumber, long amount)
    {
        var cap = Math.Max(1, Config.MaxBetPerChocobo);
        var cleanName = SanitiseHelper.SanitiseName(name);
        var used = Config.CurrentBets
            .Where(b => b.Status == BetStatus.Confirmed &&
                        b.PlayerName.Equals(cleanName, StringComparison.OrdinalIgnoreCase) &&
                        b.PlayerWorld.Equals(world, StringComparison.OrdinalIgnoreCase) &&
                        b.ChocoboNumber == chocoboNumber)
            .Sum(b => b.Amount);
        var room = (long)cap - used;
        if (room <= 0) return 0;
        return Math.Min(amount, room);
    }

    public void StartBetting()
    {
        lock (_lock)
        {
            if (Phase != RacePhase.Idle) return;

            var (_, isNew) = _historyService.EnsureActiveSession(DateTime.Now);
            if (isNew) Config.RoundNumber = 1;

            Config.CurrentPhase = RacePhase.Betting;
            Config.CurrentBets.Clear();
            Config.ChocoboPositions = new int[Config.ChocoboCount];
            Config.Save();
        }
    }

    public void AddBet(string name, string world, int chocoboNumber, long amount)
    {
        lock (_lock)
        {
            if (amount <= 0) return;

            var cleanName = SanitiseHelper.SanitiseName(name);

            var existing = Config.CurrentBets.FirstOrDefault(b =>
                b.Status == BetStatus.Confirmed &&
                b.PlayerName.Equals(cleanName, StringComparison.OrdinalIgnoreCase) &&
                b.PlayerWorld.Equals(world, StringComparison.OrdinalIgnoreCase) &&
                b.ChocoboNumber == chocoboNumber);

            if (existing != null) existing.Amount += amount;
            else
            {
                Config.CurrentBets.Add(new Bet { PlayerName = cleanName, PlayerWorld = world, ChocoboNumber = chocoboNumber, Amount = amount, Status = BetStatus.Confirmed });
            }

            var bank = GetOrCreateBank(cleanName, world);
            bank.Balance -= amount;
            Config.Save();
        }
    }

    public void RemoveBet(Bet bet)
    {
        lock (_lock)
        {
            var removed = Config.CurrentBets.Remove(bet);
            if (removed)
            {
                if (bet.Status == BetStatus.Confirmed)
                {
                    var bank = GetOrCreateBank(bet.PlayerName, bet.PlayerWorld);
                    bank.Balance += bet.Amount;
                }
                Config.Save();
            }
        }
    }

    public void AddPendingBet(string name, string world, int chocoboNumber, long amount)
    {
        lock (_lock)
        {
            Config.CurrentBets.Add(new Bet { PlayerName = SanitiseHelper.SanitiseName(name), PlayerWorld = world, ChocoboNumber = chocoboNumber, Amount = amount, Status = BetStatus.Pending });
            Config.Save();
        }
    }

    public void AcceptPendingBet(Bet bet)
    {
        lock (_lock)
        {
            if (bet.Status != BetStatus.Pending) return;

            var existing = Config.CurrentBets.FirstOrDefault(b =>
                b != bet && b.Status == BetStatus.Confirmed &&
                b.PlayerName.Equals(bet.PlayerName, StringComparison.OrdinalIgnoreCase) &&
                b.PlayerWorld.Equals(bet.PlayerWorld, StringComparison.OrdinalIgnoreCase) &&
                b.ChocoboNumber == bet.ChocoboNumber);

            if (existing != null)
            {
                existing.Amount += bet.Amount;
                Config.CurrentBets.Remove(bet);
            }
            else bet.Status = BetStatus.Confirmed;

            var bank = GetOrCreateBank(bet.PlayerName, bet.PlayerWorld);
            bank.Balance -= bet.Amount;
            Config.Save();
        }
    }

    public void DenyPendingBet(Bet bet)
    {
        lock (_lock)
        {
            if (bet.Status == BetStatus.Pending)
            {
                Config.CurrentBets.Remove(bet);
                Config.Save();
            }
        }
    }

    public List<Bet> GetBetsForChocobo(int chocoboNumber)
    {
        lock (_lock) return Config.CurrentBets.Where(b => b.ChocoboNumber == chocoboNumber).ToList();
    }
}
