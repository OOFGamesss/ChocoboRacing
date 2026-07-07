/// <summary>
/// Outcome of a Grand National entry-fee payment: ignored, joined as a new runner, added partial progress, or completed the fee.
/// </summary>
namespace ChocoboRacing.Models;

public enum GrandNationalEntryResult
{
    Ignored,
    Joined,
    Progressed,
    Paid
}
