using System;
using System.Collections.Generic;
using System.Linq;
using ChocoboRacing.Models;

/// <summary>
/// Race state partial covering shared state management, synchronisation, balance operations, and bank resolution.
/// </summary>
namespace ChocoboRacing.State;

public sealed partial class RaceState
{
    public PlayerBank? GetBank(string name, string world)
    {
        lock (_lock)
        {
            return Config.Banks.FirstOrDefault(b =>
                b.Name.Equals(name, StringComparison.OrdinalIgnoreCase) &&
                b.World.Equals(world, StringComparison.OrdinalIgnoreCase));
        }
    }

    public PlayerBank GetOrCreateBank(string name, string world)
    {
        lock (_lock)
        {
            var bank = GetBank(name, world);
            if (bank == null)
            {
                bank = new PlayerBank { Name = name, World = world, Balance = 0 };
                Config.Banks.Add(bank);
                Config.Save();
            }
            return bank;
        }
    }

    public PlayerBank AddExternalBank(string name, string world)
    {
        lock (_lock)
        {
            var bank = GetBank(name, world);
            if (bank != null) return bank;

            bank = new PlayerBank { Name = name, World = world, Balance = 0, IsExternal = true };
            Config.Banks.Add(bank);
            Config.Save();
            return bank;
        }
    }

    public void AdjustBalance(string name, string world, long amount)
    {
        lock (_lock)
        {
            var bank = GetOrCreateBank(name, world);
            bank.Balance += amount;
            Config.Save();
        }
    }

    public void MarkBankExternal(string name, string world)
    {
        lock (_lock)
        {
            var bank = GetBank(name, world);
            if (bank == null || bank.IsExternal) return;

            bank.IsExternal = true;
            Config.Save();
        }
    }

    public void MarkBankInParty(string name, string world)
    {
        lock (_lock)
        {
            var bank = GetBank(name, world);
            if (bank == null || !bank.IsExternal) return;

            bank.IsExternal = false;
            Config.Save();
        }
    }

    public void DeleteBank(string name, string world)
    {
        lock (_lock)
        {
            var bank = GetBank(name, world);
            if (bank != null)
            {
                Config.Banks.Remove(bank);
                Config.Save();
            }
        }
    }

    public List<PlayerBank> GetBanksSnapshot()
    {
        lock (_lock) return new List<PlayerBank>(Config.Banks);
    }

    public PlayerBank? GetBankByName(string name)
    {
        lock (_lock)
            return Config.Banks.FirstOrDefault(b =>
                b.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public PlayerBank? GetBankByCrossWorldName(string crossWorldName)
    {
        lock (_lock)
            return Config.Banks.FirstOrDefault(b =>
                crossWorldName.Length == b.Name.Length + b.World.Length &&
                crossWorldName.StartsWith(b.Name, StringComparison.OrdinalIgnoreCase) &&
                crossWorldName.EndsWith(b.World, StringComparison.OrdinalIgnoreCase));
    }

    public void AddTip(long amount)
    {
        if (amount <= 0) return;
        _historyService.AddTip(amount);
    }

    public void AddCashOutRequest(string playerName, string playerWorld)
    {
        lock (_lock) _cashOutRequests[$"{playerName}@{playerWorld}".ToLowerInvariant()] = DateTime.Now;
    }

    public void DismissCashOutRequest(string playerName, string playerWorld)
    {
        lock (_lock) _cashOutRequests.Remove($"{playerName}@{playerWorld}".ToLowerInvariant());
    }

    public HashSet<string> GetActiveCashOutRequests()
    {
        lock (_lock)
        {
            var now = DateTime.Now;
            var expired = _cashOutRequests.Where(kv => (now - kv.Value).TotalMinutes >= 5).Select(kv => kv.Key).ToList();
            foreach (var key in expired) _cashOutRequests.Remove(key);
            return new HashSet<string>(_cashOutRequests.Keys, StringComparer.OrdinalIgnoreCase);
        }
    }
}
