using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;

/// <summary>Lists the players loaded around the host as "Name@World" entries for add-player pickers.</summary>
namespace ChocoboRacing.Services;

public sealed class NearbyPlayerService
{
    private readonly IObjectTable _objectTable;

    public NearbyPlayerService(IObjectTable objectTable)
    {
        _objectTable = objectTable;
    }

    public List<string> GetNearbyPlayers()
    {
        return _objectTable
            .OfType<IPlayerCharacter>()
            .Where(p => p.ObjectKind == ObjectKind.Pc && !string.IsNullOrEmpty(p.Name.TextValue))
            .Select(p => $"{p.Name.TextValue}@{p.HomeWorld.Value.Name.ToString()}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static (string Name, string World) SplitEntry(string entry)
    {
        var at = entry.LastIndexOf('@');
        return at <= 0 ? (entry, string.Empty) : (entry[..at], entry[(at + 1)..]);
    }
}
