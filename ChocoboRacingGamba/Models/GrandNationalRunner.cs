using System;

/// <summary>
/// A single entrant in a Grand National race, identified by lane number, name, and home world.
/// </summary>
namespace ChocoboRacing.Models;

[Serializable]
public sealed class GrandNationalRunner
{
    public int Number { get; set; }
    public string Name { get; set; } = string.Empty;
    public string World { get; set; } = string.Empty;
}
