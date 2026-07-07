using ChocoboRacing.Services;
using ChocoboRacing.Utility;

/// <summary>
/// Processes incoming chat messages to detect and apply Grand National race entry join requests.
/// </summary>
namespace ChocoboRacing.Events.ChatHandlers;

public sealed class JoinHandler
{
    private readonly GrandNationalService _service;

    public JoinHandler(GrandNationalService service)
    {
        _service = service;
    }

    public void Process(string message, string playerName, string playerWorld)
    {
        var cleanName = SanitiseHelper.SanitiseName(playerName);
        _service.TryHandleJoin(cleanName, playerWorld, message);
    }
}
