namespace ChocoboRacing.Models;

/// <summary>
/// Lifecycle state of a bet from placement through settlement.
/// </summary>
public enum BetStatus
{
    Pending,
    Confirmed,
    Settled
}
