/// <summary>
/// Tracks the current stage of the race lifecycle.
/// </summary>
namespace ChocoboRacing.Models;

public enum RacePhase
{
    Idle,
    Betting,
    BetsClosed,
    Racing,
    Finished
}
