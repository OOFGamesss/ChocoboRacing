using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using ECommons.GameHelpers;
using ECommons.PartyFunctions;

namespace ChocoboRacing.Services;

public sealed class PartyService
{
    private readonly IPartyList _partyList;

    public PartyService(IPartyList partyList)
    {
        _partyList = partyList;
    }

    public bool IsInAlliance() => UniversalParty.IsAlliance;

    public bool IsInParty() => _partyList.Length > 0 || UniversalParty.IsCrossWorldParty;

    public (string Name, string World)? GetLocalPlayer()
    {
        if (!Player.Available) return null;
        return (Player.Name!, Player.HomeWorld.Value.Name.ToString());
    }

    public List<(string Name, string World)> GetPartyMembers()
        => UniversalParty.Members
            .Select(m => (m.Name, m.HomeWorld.Value.Name.ToString()))
            .ToList();

    public string GetChatPrefix()
    {
        if (IsInAlliance()) return "/alliance";
        if (IsInParty()) return "/p";
        return "/echo";
    }

    public string GetDiceCommand(int max)
    {
        if (IsInAlliance()) return $"/dice alliance {max}";
        if (IsInParty()) return $"/dice party {max}";
        return $"/dice {max}";
    }
}
