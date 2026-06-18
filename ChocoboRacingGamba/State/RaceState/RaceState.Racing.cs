using ChocoboRacing.Models;

namespace ChocoboRacing.State;

/// <summary>
/// Race state partial covering the start of the racing phase.
/// </summary>
public sealed partial class RaceState
{
    public void StartRacing()
    {
        lock (_lock)
        {
            if (Phase != RacePhase.BetsClosed) return;
            Config.CurrentPhase = RacePhase.Racing;
            Config.Save();
        }
    }
}
