/// <summary>
/// Lifecycle state of a bet from placement through settlement.
/// </summary>
namespace ChocoboRacing.Models;

public enum BetStatus
{
    Pending,
    Confirmed,
    Settled
}
