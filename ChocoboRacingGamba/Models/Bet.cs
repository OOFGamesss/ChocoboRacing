using System;

namespace ChocoboRacing.Models;

/// <summary>
/// A single wager placed by a player on a chocobo.
/// </summary>
[Serializable]
public class Bet
{
    public string PlayerName { get; set; } = string.Empty;
    public string PlayerWorld { get; set; } = string.Empty;
    public int ChocoboNumber { get; set; }
    public long Amount { get; set; }
    public BetStatus Status { get; set; } = BetStatus.Pending;
}
