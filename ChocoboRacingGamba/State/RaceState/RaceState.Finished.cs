using System;
using System.Collections.Generic;
using ChocoboRacing.Models;

namespace ChocoboRacing.State;

/// <summary>
/// Race state partial covering race completion, result recording, and history tracking.
/// </summary>
public sealed partial class RaceState
{
    public void FinishRace(int winningChocobo, List<BetRecord> betRecords, bool isTestingMode = false)
    {
        lock (_lock)
        {
            if (!isTestingMode)
            {
                var sessionId = _historyService.ActiveSessionId;
                if (sessionId != Guid.Empty)
                {
                    var raceRecord = new RaceRecord
                    {
                        RoundNumber    = RoundNumber,
                        ChocoboCount   = ChocoboCount,
                        PayoutOdds     = GetPayoutMultiplierForWinnerUnderLock(winningChocobo),
                        WinningChocobo = winningChocobo,
                        Bets           = betRecords != null ? new List<BetRecord>(betRecords) : new List<BetRecord>()
                    };
                    _historyService.RecordRace(sessionId, raceRecord);
                }
            }

            Config.CurrentPhase = RacePhase.Finished;
            Config.Save();
        }
    }
}
