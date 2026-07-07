using ChocoboRacing.Models;

/// <summary>
/// Race state partial covering the transition from the betting phase to the bets-closed phase.
/// </summary>
namespace ChocoboRacing.State;

public sealed partial class RaceState
{
    public void CloseBets()
    {
        lock (_lock)
        {
            if (Phase != RacePhase.Betting) return;
            Config.CurrentPhase = RacePhase.BetsClosed;
            Config.Save();
        }
    }
}
