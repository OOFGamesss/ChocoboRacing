using System;

namespace ChocoboRacing.Models;

/// <summary>
/// Tracks a player's balance.
/// </summary>
[Serializable]
public class PlayerBank
{
    public string Name { get; set; } = string.Empty;
    public string World { get; set; } = string.Empty;
    public long Balance { get; set; }
    public bool IsArchived { get; set; }
}
