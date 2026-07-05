using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace ChocoboRacing.Interop;

public static unsafe class LocalCharacter
{
    public static ulong ContentId()
    {
        var local = Plugin.ObjectTable.LocalPlayer;
        if (local == null) return 0;
        return ((Character*)local.Address)->ContentId;
    }
}
