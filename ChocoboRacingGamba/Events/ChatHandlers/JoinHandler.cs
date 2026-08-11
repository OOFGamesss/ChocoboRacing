using ChocoboRacing.Services;
using ChocoboRacing.Utility;

/// <summary>
/// Processes incoming chat messages to detect and apply raffle race entry join requests.
/// </summary>
namespace ChocoboRacing.Events.ChatHandlers;

public sealed class JoinHandler
{
    private readonly RaffleService _service;

    public JoinHandler(RaffleService service)
    {
        _service = service;
    }

    public void Process(string message, string playerName, string playerWorld)
    {
        var cleanName = SanitiseHelper.SanitiseName(playerName);
        _service.TryHandleJoin(cleanName, playerWorld, message);
    }
}
