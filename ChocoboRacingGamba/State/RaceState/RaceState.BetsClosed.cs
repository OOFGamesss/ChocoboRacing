using ChocoboRacing.Models;

namespace ChocoboRacing.State;

/// <summary>
/// Race state partial covering the transition from the betting closed phase to the racing phase.
/// </summary>
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
