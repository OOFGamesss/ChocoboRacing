namespace ChocoboRacing.Models;

/// <summary>
/// Tracks the current stage of the race lifecycle.
/// </summary>
public enum RacePhase
{
    Idle,
    Betting,
    BetsClosed,
    Racing,
    Finished
}
