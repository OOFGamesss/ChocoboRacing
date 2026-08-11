using System;

/// <summary>
/// A single entrant in a raffle race, identified by lane number, name, and home world.
/// </summary>
namespace ChocoboRacing.Models;

[Serializable]
public sealed class RaffleRunner
{
    public int Number { get; set; }
    public string Name { get; set; } = string.Empty;
    public string World { get; set; } = string.Empty;
}
