/// <summary>
/// Lifecycle phase of a Grand National race: registration, closed for entries, racing, and finished.
/// </summary>
namespace ChocoboRacing.Models;

public enum GrandNationalPhase
{
    Idle,
    Registration,
    Closed,
    Racing,
    Finished
}
