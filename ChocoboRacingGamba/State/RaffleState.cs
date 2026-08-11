using System;
using System.Collections.Generic;
using System.Linq;
using ChocoboRacing.Config;
using ChocoboRacing.Models;
using ChocoboRacing.Utility;

/// <summary>
/// Manages the in-memory state and lifecycle of the raffle race mode, including
/// registration, payment tracking, grid assembly, racing, and result resolution.
/// </summary>
namespace ChocoboRacing.State;

public sealed class RaffleState
{
    public const int MinRunners = 2;

    private readonly PluginConfig _config;
    private readonly Random _random = new();

    public PluginConfig Config => _config;
    public RafflePhase Phase => _config.RafflePhase;
    public bool IsFree => RaffleMath.IsFree(_config);
    public long Pot => RaffleMath.Pot(_config);
    public long NetPot => RaffleMath.NetPot(_config);
    public long VenueCut => RaffleMath.VenueCut(_config);
    public long Entries => RaffleMath.Entries(_config);
    public bool IsPotPrize => RaffleMath.IsPotPrize(_config);
    public RafflePrizeType PrizeType => _config.RafflePrizeType;
    public string PrizeLabel => RaffleMath.PrizeLabel(_config);
    public int EligibleCount => RaffleMath.EligibleCount(_config);
    public int Winner => _config.RaffleWinner;

    public IReadOnlyList<string> Registered => _config.RaffleRegistered;

    public int PaidCount => _config.RafflePaid.Count;

    public bool HasLastRoster => _config.RaffleLastRoster.Count > 0;

    public bool CanClose => Phase == RafflePhase.Registration && EligibleCount >= MinRunners;

    public RaffleState(PluginConfig config)
    {
        _config = config;
    }

    public static string DisplayName(string entry)
    {
        var at = entry.LastIndexOf('@');
        return at > 0 ? entry[..at] : entry;
    }

    public static string WorldOf(string entry)
    {
        var at = entry.LastIndexOf('@');
        return at > 0 && at < entry.Length - 1 ? entry[(at + 1)..] : string.Empty;
    }

    public bool IsPaid(string entry) =>
        IsFree || _config.RafflePaid.Any(p => p.Equals(entry, StringComparison.OrdinalIgnoreCase));

    public int RunnerNumber(string entry)
    {
        var ordered = RaffleMath.OrderedRunnerEntries(_config);
        for (var i = 0; i < ordered.Count; i++)
            if (ordered[i].Equals(entry, StringComparison.OrdinalIgnoreCase))
                return i + 1;
        return 0;
    }

    public bool IsRegistered(string entry)
    {
        var name = DisplayName(entry);
        return _config.RaffleRegistered.Any(e =>
            DisplayName(e).Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public void OpenRegistration(bool withLastRunners = false)
    {
        if (_config.RafflePhase != RafflePhase.Idle) return;
        _config.RaffleRegistered.Clear();
        _config.RafflePaid.Clear();
        _config.RaffleEntryPaid.Clear();
        _config.RaffleGrid.Clear();
        _config.RaffleWinner = 0;
        if (withLastRunners)
            foreach (var entry in _config.RaffleLastRoster)
                if (!IsRegistered(entry))
                    _config.RaffleRegistered.Add(entry);
        _config.RafflePhase = RafflePhase.Registration;
        _config.Save();
    }

    public bool AddEntry(string rawEntry)
    {
        if (_config.RafflePhase != RafflePhase.Registration) return false;
        var entry = NormalizeEntry(rawEntry);
        if (entry.Length == 0 || entry.Length > 64) return false;
        if (IsRegistered(entry)) return false;
        _config.RaffleRegistered.Add(entry);
        _config.Save();
        return true;
    }

    public static string NormalizeEntry(string? rawEntry)
    {
        var entry = (rawEntry ?? string.Empty).Trim();
        if (entry.Length == 0) return string.Empty;
        var at = entry.LastIndexOf('@');
        if (at > 0 && at < entry.Length - 1) return entry;
        var name = at > 0 ? entry[..at] : entry;
        var world = Plugin.ObjectTable.LocalPlayer?.HomeWorld.Value.Name.ToString() ?? string.Empty;
        return string.IsNullOrEmpty(world) ? name : $"{name}@{world}";
    }

    public void RemoveAt(int index)
    {
        if (index < 0 || index >= _config.RaffleRegistered.Count) return;
        var entry = _config.RaffleRegistered[index];
        _config.RaffleRegistered.RemoveAt(index);
        _config.RafflePaid.RemoveAll(p => p.Equals(entry, StringComparison.OrdinalIgnoreCase));
        _config.RaffleEntryPaid.Remove(entry);
        _config.Save();
    }

    public bool TogglePaid(string entry)
    {
        if (IsFree) return false;
        var existing = _config.RafflePaid.FirstOrDefault(p => p.Equals(entry, StringComparison.OrdinalIgnoreCase));
        bool nowPaid;
        if (existing != null)
        {
            _config.RafflePaid.Remove(existing);
            _config.RaffleEntryPaid[entry] = 0;
            nowPaid = false;
        }
        else
        {
            _config.RafflePaid.Add(entry);
            _config.RaffleEntryPaid[entry] = _config.RaffleEntryFee;
            nowPaid = true;
        }
        _config.Save();
        return nowPaid;
    }

    public long EntryPaid(string entry) => _config.RaffleEntryPaid.GetValueOrDefault(entry);

    public RaffleEntryResult HandleEntryPayment(string name, string world, long paidAmount)
    {
        if (_config.RafflePhase != RafflePhase.Registration || string.IsNullOrWhiteSpace(name))
            return RaffleEntryResult.Ignored;

        var existing = _config.RaffleRegistered
            .FirstOrDefault(e => DisplayName(e).Equals(name, StringComparison.OrdinalIgnoreCase));

        if (IsFree)
        {
            if (existing != null) return RaffleEntryResult.Ignored;
            var freeEntry = string.IsNullOrEmpty(world) ? name : $"{name}@{world}";
            return AddEntry(freeEntry) ? RaffleEntryResult.Joined : RaffleEntryResult.Ignored;
        }

        if (paidAmount <= 0 && existing == null) return RaffleEntryResult.Ignored;

        var added = false;
        if (existing == null)
        {
            var entry = string.IsNullOrEmpty(world) ? name : $"{name}@{world}";
            if (!AddEntry(entry)) return RaffleEntryResult.Ignored;
            existing = _config.RaffleRegistered
                .FirstOrDefault(e => DisplayName(e).Equals(name, StringComparison.OrdinalIgnoreCase)) ?? entry;
            added = true;
        }

        if (paidAmount <= 0 && !added) return RaffleEntryResult.Ignored;

        if (paidAmount > 0)
            _config.RaffleEntryPaid[existing] = _config.RaffleEntryPaid.GetValueOrDefault(existing) + paidAmount;

        var nowPaid = false;
        if (!IsPaid(existing) && _config.RaffleEntryPaid.GetValueOrDefault(existing) >= _config.RaffleEntryFee)
        {
            _config.RafflePaid.Add(existing);
            nowPaid = true;
        }
        _config.Save();

        if (nowPaid) return RaffleEntryResult.Paid;
        return added ? RaffleEntryResult.Joined : RaffleEntryResult.Progressed;
    }

    public void MoveUp(int index)
    {
        if (index <= 0 || index >= _config.RaffleRegistered.Count) return;
        (_config.RaffleRegistered[index - 1], _config.RaffleRegistered[index]) =
            (_config.RaffleRegistered[index], _config.RaffleRegistered[index - 1]);
        _config.Save();
    }

    public void MoveDown(int index)
    {
        if (index < 0 || index >= _config.RaffleRegistered.Count - 1) return;
        (_config.RaffleRegistered[index + 1], _config.RaffleRegistered[index]) =
            (_config.RaffleRegistered[index], _config.RaffleRegistered[index + 1]);
        _config.Save();
    }

    public void Shuffle()
    {
        var list = _config.RaffleRegistered;
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        _config.Save();
    }

    public bool CloseRegistration()
    {
        if (!CanClose) return false;
        var ordered = RaffleMath.OrderedRunnerEntries(_config);
        _config.RaffleLastRoster = new List<string>(ordered);
        var grid = new List<RaffleRunner>();
        var number = 1;
        foreach (var entry in ordered)
        {
            grid.Add(new RaffleRunner
            {
                Number = number++,
                Name = DisplayName(entry),
                World = WorldOf(entry),
            });
        }
        _config.RaffleGrid = grid;
        _config.RaffleWinner = 0;
        _config.RafflePhase = RafflePhase.Closed;
        _config.Save();
        return true;
    }

    public void ReopenRegistration()
    {
        if (_config.RafflePhase != RafflePhase.Closed) return;
        _config.RaffleGrid.Clear();
        _config.RaffleWinner = 0;
        _config.RafflePhase = RafflePhase.Registration;
        _config.Save();
    }

    public bool BeginRacing()
    {
        if (_config.RafflePhase != RafflePhase.Closed) return false;
        _config.RaffleWinner = 0;
        _config.RafflePhase = RafflePhase.Racing;
        _config.Save();
        return true;
    }

    public void SetWinner(int winner)
    {
        if (_config.RafflePhase != RafflePhase.Racing) return;
        _config.RaffleWinner = winner;
        _config.Save();
    }

    public void AbortRacing()
    {
        if (_config.RafflePhase != RafflePhase.Racing) return;
        _config.RaffleWinner = 0;
        _config.RafflePhase = RafflePhase.Closed;
        _config.Save();
    }

    public void FinishRace()
    {
        if (_config.RafflePhase != RafflePhase.Racing) return;
        _config.RafflePhase = RafflePhase.Finished;
        _config.Save();
    }

    public void Reset()
    {
        _config.RaffleRegistered.Clear();
        _config.RafflePaid.Clear();
        _config.RaffleEntryPaid.Clear();
        _config.RaffleGrid.Clear();
        _config.RaffleWinner = 0;
        _config.RafflePhase = RafflePhase.Idle;
        _config.Save();
    }

    public List<RaffleRunner> GetGridSnapshot() => new(_config.RaffleGrid);

    public RaffleRunner? WinningRunner() =>
        _config.RaffleGrid.FirstOrDefault(r => r.Number == _config.RaffleWinner);
}
