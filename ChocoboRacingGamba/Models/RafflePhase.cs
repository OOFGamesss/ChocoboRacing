/// <summary>
/// Lifecycle phase of a raffle race: registration, closed for entries, racing, and finished.
/// </summary>
namespace ChocoboRacing.Models;

public enum RafflePhase
{
    Idle,
    Registration,
    Closed,
    Racing,
    Finished
}
