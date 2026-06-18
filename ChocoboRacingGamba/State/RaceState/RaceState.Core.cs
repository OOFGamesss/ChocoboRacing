using System;
using System.Collections.Generic;
using System.Linq;
using ChocoboRacing.Config;
using ChocoboRacing.Models;
using ChocoboRacing.Services;

namespace ChocoboRacing.State;

/// <summary>
/// Maintains the in-memory state of the active Chocobo Race.
/// </summary>
public sealed partial class RaceState
{
    public const int MinFinishLine = 5;
    public const int MaxFinishLine = 15;
    public const int MinChocobos = 2;
    public const int MaxChocobos = 10;
    public const float MinPayoutOdds = 1.0f;
    public const float MaxPayoutOdds = 20.0f;
    public const float MaxPerfectRaceOdds = 100.0f;

    public PluginConfig Config { get; }
    private readonly RaceHistoryService _historyService;
    private readonly object _lock = new();
    private readonly Dictionary<string, DateTime> _cashOutRequests = new();

    public RacePhase Phase => Config.CurrentPhase;
    public int ChocoboCount => Config.ChocoboCount;
    public int FinishLine => Config.FinishLine;
    public float PayoutOdds => Config.PayoutOdds;
    public bool PerfectRace => Config.PerfectRace;
    public int RoundNumber => Config.RoundNumber;

    internal float GetPayoutMultiplierForWinnerUnderLock(int winningChocobo)
    {
        if (winningChocobo < 1 || winningChocobo > Config.ChocoboCount) return Config.PayoutOdds;
        if (!Config.PerfectRace) return Config.PayoutOdds;

        for (var i = 0; i < Config.ChocoboCount; i++)
        {
            if (i == winningChocobo - 1) continue;
            if (Config.ChocoboPositions[i] != 0) return Config.PayoutOdds;
        }

        return Config.PerfectRaceOdds;
    }

    public float GetPayoutMultiplierForWinner(int winningChocobo)
    {
        lock (_lock) return GetPayoutMultiplierForWinnerUnderLock(winningChocobo);
    }

    public bool IsHosting => Config.IsHosting;
    public int[] ChocoboPositions => Config.ChocoboPositions;
    public IReadOnlyList<Bet> Bets => Config.CurrentBets;

    public RaceState(PluginConfig config, RaceHistoryService historyService)
    {
        Config = config;
        _historyService = historyService;
        if (Config.ChocoboPositions.Length != Config.ChocoboCount)
            Config.ChocoboPositions = new int[Config.ChocoboCount];
    }

    public string GetEffectiveChocoboVisualName(int chocoboNumber)
    {
        if (chocoboNumber < 1 || chocoboNumber > ChocoboCount) return string.Empty;

        return chocoboNumber switch
        {
            1  => Config.Chocobo1Name,
            2  => Config.Chocobo2Name,
            3  => Config.Chocobo3Name,
            4  => Config.Chocobo4Name,
            5  => Config.Chocobo5Name,
            6  => Config.Chocobo6Name,
            7  => Config.Chocobo7Name,
            8  => Config.Chocobo8Name,
            9  => Config.Chocobo9Name,
            10 => Config.Chocobo10Name,
            _  => string.Empty
        };
    }

    public List<string> ListChocoboNames()
    {
        lock (_lock)
        {
            return new List<string>
            {
                Config.Chocobo1Name, Config.Chocobo2Name, Config.Chocobo3Name,
                Config.Chocobo4Name, Config.Chocobo5Name, Config.Chocobo6Name,
                Config.Chocobo7Name, Config.Chocobo8Name, Config.Chocobo9Name,
                Config.Chocobo10Name
            }.Take(Config.ChocoboCount).ToList();
        }
    }
}
