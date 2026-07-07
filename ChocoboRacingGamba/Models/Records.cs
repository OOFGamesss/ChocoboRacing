using System;
using System.Collections.Generic;

/// <summary>
/// History records for completed bets, races, and play sessions.
/// </summary>
namespace ChocoboRacing.Models;

[Serializable]
public class BetRecord
{
    public string PlayerName { get; set; } = string.Empty;
    public string PlayerWorld { get; set; } = string.Empty;
    public int ChocoboNumber { get; set; }
    public long Amount { get; set; }
    public bool IsWinner { get; set; }
    public long Payout { get; set; }
}

[Serializable]
public class RaceRecord
{
    public int RoundNumber { get; set; }
    public int ChocoboCount { get; set; }
    public float PayoutOdds { get; set; }
    public int WinningChocobo { get; set; }
    public RaceMode Mode { get; set; } = RaceMode.Classic;
    public List<BetRecord> Bets { get; set; } = new();
}

[Serializable]
public class SessionRecord
{
    public Guid SessionId { get; set; } = Guid.NewGuid();
    public DateTime StartTime { get; set; } = DateTime.Now;
    public DateTime LastRaceTime { get; set; } = DateTime.Now;
    public List<RaceRecord> Races { get; set; } = new();
    public long Tips { get; set; }
}
