using System.Collections.Generic;

namespace ChocoboRacing.Models;

/// <summary>
/// DTOs exchanged with ChocoboRacingAPI (serialised as camelCase JSON).
/// </summary>
public sealed class CreateSessionRequest
{
    public string? HostName { get; set; }
    public string? VenueName { get; set; }
    public string? VenueImageUrl { get; set; }
    public string? CharacterId { get; set; }
    public string? CharacterName { get; set; }
    public string? World { get; set; }
}

public sealed class CreateSessionResponse
{
    public string SessionId { get; set; } = string.Empty;
    public string SpectatorUrl { get; set; } = string.Empty;
}

public sealed class BankDto
{
    public string Name { get; set; } = string.Empty;
    public string World { get; set; } = string.Empty;
    public long Balance { get; set; }
}

public sealed class BetDto
{
    public string Name { get; set; } = string.Empty;
    public string World { get; set; } = string.Empty;
    public int Chocobo { get; set; }
    public long Amount { get; set; }
}

public sealed class PinDto
{
    public string Pin { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string World { get; set; } = string.Empty;
}

public sealed class SyncStatePayload
{
    public string Phase { get; set; } = "Idle";
    public int Round { get; set; }
    public int ChocoboCount { get; set; }
    public int FinishLine { get; set; }
    public double Odds { get; set; }
    public bool PerfectRace { get; set; }
    public double PerfectRaceOdds { get; set; }
    public long MaxBetPerChocobo { get; set; }
    public int WinningChocobo { get; set; }
    public List<int> Positions { get; set; } = new();
    public List<string> ChocoboNames { get; set; } = new();
    public List<BankDto> Banks { get; set; } = new();
    public List<BetDto> Bets { get; set; } = new();
    public List<PinDto> Pins { get; set; } = new();
    public string? HostName { get; set; }
    public string? VenueName { get; set; }
    public string? VenueImageUrl { get; set; }
}

public sealed class CallCountsPayload
{
    public string? Phase { get; set; }
    public List<int> Positions { get; set; } = new();
    public int WinningChocobo { get; set; }
}

public sealed class WebBetDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string World { get; set; } = string.Empty;
    public int Chocobo { get; set; }
    public long Amount { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
}

public sealed class WebBetsResponse
{
    public List<WebBetDto> WebBets { get; set; } = new();
}

public sealed class WebBetAckRequest
{
    public List<long> Ids { get; set; } = new();
    public string Status { get; set; } = "applied";
}

public sealed class MasterStateResponse
{
    public string SessionId { get; set; } = string.Empty;
    public string Phase { get; set; } = "Idle";
    public int Round { get; set; }
    public int FinishLine { get; set; }
    public double Odds { get; set; }
    public bool PerfectRace { get; set; }
    public double PerfectRaceOdds { get; set; }
    public long MaxBetPerChocobo { get; set; }
    public List<int> Positions { get; set; } = new();
    public List<string> ChocoboNames { get; set; } = new();
    public int WinningChocobo { get; set; }
    public List<BankDto> Banks { get; set; } = new();
    public List<WebBetDto> PendingWebBets { get; set; } = new();
}
