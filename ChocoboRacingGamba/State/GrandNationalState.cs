using System;
using System.Collections.Generic;
using System.Linq;
using ChocoboRacing.Config;
using ChocoboRacing.Models;
using ChocoboRacing.Utility;

/// <summary>
/// Manages the in-memory state and lifecycle of the Grand National raffle race mode, including
/// registration, payment tracking, grid assembly, racing, and result resolution.
/// </summary>
namespace ChocoboRacing.State;

public sealed class GrandNationalState
{
    public const int MinRunners = 2;

    private readonly PluginConfig _config;
    private readonly Random _random = new();

    public PluginConfig Config => _config;
    public GrandNationalPhase Phase => _config.GrandNationalPhase;
    public bool IsFree => GrandNationalMath.IsFree(_config);
    public long Pot => GrandNationalMath.Pot(_config);
    public long NetPot => GrandNationalMath.NetPot(_config);
    public long VenueCut => GrandNationalMath.VenueCut(_config);
    public long Entries => GrandNationalMath.Entries(_config);
    public bool IsPotPrize => GrandNationalMath.IsPotPrize(_config);
    public GrandNationalPrizeType PrizeType => _config.GrandNationalPrizeType;
    public string PrizeLabel => GrandNationalMath.PrizeLabel(_config);
    public int EligibleCount => GrandNationalMath.EligibleCount(_config);
    public int Winner => _config.GrandNationalWinner;

    public IReadOnlyList<string> Registered => _config.GrandNationalRegistered;

    public int PaidCount => _config.GrandNationalPaid.Count;

    public bool HasLastRoster => _config.GrandNationalLastRoster.Count > 0;

    public bool CanClose => Phase == GrandNationalPhase.Registration && EligibleCount >= MinRunners;

    public GrandNationalState(PluginConfig config)
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
        IsFree || _config.GrandNationalPaid.Any(p => p.Equals(entry, StringComparison.OrdinalIgnoreCase));

    public int RunnerNumber(string entry)
    {
        var ordered = GrandNationalMath.OrderedRunnerEntries(_config);
        for (var i = 0; i < ordered.Count; i++)
            if (ordered[i].Equals(entry, StringComparison.OrdinalIgnoreCase))
                return i + 1;
        return 0;
    }

    public bool IsRegistered(string entry)
    {
        var name = DisplayName(entry);
        return _config.GrandNationalRegistered.Any(e =>
            DisplayName(e).Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public void OpenRegistration(bool withLastRunners = false)
    {
        if (_config.GrandNationalPhase != GrandNationalPhase.Idle) return;
        _config.GrandNationalRegistered.Clear();
        _config.GrandNationalPaid.Clear();
        _config.GrandNationalEntryPaid.Clear();
        _config.GrandNationalGrid.Clear();
        _config.GrandNationalWinner = 0;
        if (withLastRunners)
            foreach (var entry in _config.GrandNationalLastRoster)
                if (!IsRegistered(entry))
                    _config.GrandNationalRegistered.Add(entry);
        _config.GrandNationalPhase = GrandNationalPhase.Registration;
        _config.Save();
    }

    public bool AddEntry(string rawEntry)
    {
        if (_config.GrandNationalPhase != GrandNationalPhase.Registration) return false;
        var entry = NormalizeEntry(rawEntry);
        if (entry.Length == 0 || entry.Length > 64) return false;
        if (IsRegistered(entry)) return false;
        _config.GrandNationalRegistered.Add(entry);
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
        if (index < 0 || index >= _config.GrandNationalRegistered.Count) return;
        var entry = _config.GrandNationalRegistered[index];
        _config.GrandNationalRegistered.RemoveAt(index);
        _config.GrandNationalPaid.RemoveAll(p => p.Equals(entry, StringComparison.OrdinalIgnoreCase));
        _config.GrandNationalEntryPaid.Remove(entry);
        _config.Save();
    }

    public bool TogglePaid(string entry)
    {
        if (IsFree) return false;
        var existing = _config.GrandNationalPaid.FirstOrDefault(p => p.Equals(entry, StringComparison.OrdinalIgnoreCase));
        bool nowPaid;
        if (existing != null)
        {
            _config.GrandNationalPaid.Remove(existing);
            _config.GrandNationalEntryPaid[entry] = 0;
            nowPaid = false;
        }
        else
        {
            _config.GrandNationalPaid.Add(entry);
            _config.GrandNationalEntryPaid[entry] = _config.GrandNationalEntryFee;
            nowPaid = true;
        }
        _config.Save();
        return nowPaid;
    }

    public long EntryPaid(string entry) => _config.GrandNationalEntryPaid.GetValueOrDefault(entry);

    public GrandNationalEntryResult HandleEntryPayment(string name, string world, long paidAmount)
    {
        if (_config.GrandNationalPhase != GrandNationalPhase.Registration || string.IsNullOrWhiteSpace(name))
            return GrandNationalEntryResult.Ignored;

        var existing = _config.GrandNationalRegistered
            .FirstOrDefault(e => DisplayName(e).Equals(name, StringComparison.OrdinalIgnoreCase));

        if (IsFree)
        {
            if (existing != null) return GrandNationalEntryResult.Ignored;
            var freeEntry = string.IsNullOrEmpty(world) ? name : $"{name}@{world}";
            return AddEntry(freeEntry) ? GrandNationalEntryResult.Joined : GrandNationalEntryResult.Ignored;
        }

        if (paidAmount <= 0 && existing == null) return GrandNationalEntryResult.Ignored;

        var added = false;
        if (existing == null)
        {
            var entry = string.IsNullOrEmpty(world) ? name : $"{name}@{world}";
            if (!AddEntry(entry)) return GrandNationalEntryResult.Ignored;
            existing = _config.GrandNationalRegistered
                .FirstOrDefault(e => DisplayName(e).Equals(name, StringComparison.OrdinalIgnoreCase)) ?? entry;
            added = true;
        }

        if (paidAmount <= 0 && !added) return GrandNationalEntryResult.Ignored;

        if (paidAmount > 0)
            _config.GrandNationalEntryPaid[existing] = _config.GrandNationalEntryPaid.GetValueOrDefault(existing) + paidAmount;

        var nowPaid = false;
        if (!IsPaid(existing) && _config.GrandNationalEntryPaid.GetValueOrDefault(existing) >= _config.GrandNationalEntryFee)
        {
            _config.GrandNationalPaid.Add(existing);
            nowPaid = true;
        }
        _config.Save();

        if (nowPaid) return GrandNationalEntryResult.Paid;
        return added ? GrandNationalEntryResult.Joined : GrandNationalEntryResult.Progressed;
    }

    public void MoveUp(int index)
    {
        if (index <= 0 || index >= _config.GrandNationalRegistered.Count) return;
        (_config.GrandNationalRegistered[index - 1], _config.GrandNationalRegistered[index]) =
            (_config.GrandNationalRegistered[index], _config.GrandNationalRegistered[index - 1]);
        _config.Save();
    }

    public void MoveDown(int index)
    {
        if (index < 0 || index >= _config.GrandNationalRegistered.Count - 1) return;
        (_config.GrandNationalRegistered[index + 1], _config.GrandNationalRegistered[index]) =
            (_config.GrandNationalRegistered[index], _config.GrandNationalRegistered[index + 1]);
        _config.Save();
    }

    public void Shuffle()
    {
        var list = _config.GrandNationalRegistered;
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
        var ordered = GrandNationalMath.OrderedRunnerEntries(_config);
        _config.GrandNationalLastRoster = new List<string>(ordered);
        var grid = new List<GrandNationalRunner>();
        var number = 1;
        foreach (var entry in ordered)
        {
            grid.Add(new GrandNationalRunner
            {
                Number = number++,
                Name = DisplayName(entry),
                World = WorldOf(entry),
            });
        }
        _config.GrandNationalGrid = grid;
        _config.GrandNationalWinner = 0;
        _config.GrandNationalPhase = GrandNationalPhase.Closed;
        _config.Save();
        return true;
    }

    public void ReopenRegistration()
    {
        if (_config.GrandNationalPhase != GrandNationalPhase.Closed) return;
        _config.GrandNationalGrid.Clear();
        _config.GrandNationalWinner = 0;
        _config.GrandNationalPhase = GrandNationalPhase.Registration;
        _config.Save();
    }

    public bool BeginRacing()
    {
        if (_config.GrandNationalPhase != GrandNationalPhase.Closed) return false;
        _config.GrandNationalWinner = 0;
        _config.GrandNationalPhase = GrandNationalPhase.Racing;
        _config.Save();
        return true;
    }

    public void SetWinner(int winner)
    {
        if (_config.GrandNationalPhase != GrandNationalPhase.Racing) return;
        _config.GrandNationalWinner = winner;
        _config.Save();
    }

    public void AbortRacing()
    {
        if (_config.GrandNationalPhase != GrandNationalPhase.Racing) return;
        _config.GrandNationalWinner = 0;
        _config.GrandNationalPhase = GrandNationalPhase.Closed;
        _config.Save();
    }

    public void FinishRace()
    {
        if (_config.GrandNationalPhase != GrandNationalPhase.Racing) return;
        _config.GrandNationalPhase = GrandNationalPhase.Finished;
        _config.Save();
    }

    public void Reset()
    {
        _config.GrandNationalRegistered.Clear();
        _config.GrandNationalPaid.Clear();
        _config.GrandNationalEntryPaid.Clear();
        _config.GrandNationalGrid.Clear();
        _config.GrandNationalWinner = 0;
        _config.GrandNationalPhase = GrandNationalPhase.Idle;
        _config.Save();
    }

    public List<GrandNationalRunner> GetGridSnapshot() => new(_config.GrandNationalGrid);

    public GrandNationalRunner? WinningRunner() =>
        _config.GrandNationalGrid.FirstOrDefault(r => r.Number == _config.GrandNationalWinner);
}
