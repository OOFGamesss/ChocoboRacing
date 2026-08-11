/// <summary>
/// Outcome of a raffle entry-fee payment: ignored, joined as a new runner, added partial progress, or completed the fee.
/// </summary>
namespace ChocoboRacing.Models;

public enum RaffleEntryResult
{
    Ignored,
    Joined,
    Progressed,
    Paid
}
