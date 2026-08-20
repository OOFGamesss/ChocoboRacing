using System;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;

/// <summary>
/// Plays the game chat sound effects.
/// </summary>
namespace ChocoboRacing.Services;

public sealed class SoundService
{
    public const int MinSoundEffectId = 1;
    public const int MaxSoundEffectId = 16;

    private readonly IPluginLog _log;

    public SoundService(IPluginLog log)
    {
        _log = log;
    }

    public void PlayChatSoundEffect(int id)
    {
        try
        {
            UIGlobals.PlayChatSoundEffect((uint)Math.Clamp(id, MinSoundEffectId, MaxSoundEffectId));
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "PlayChatSoundEffect failed for SE {Id}", id);
        }
    }
}
