using ChocoboRacing.Models;

/// <summary>
/// Race state partial covering the start of the racing phase.
/// </summary>
namespace ChocoboRacing.State;

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
