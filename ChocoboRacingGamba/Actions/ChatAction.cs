using System;
using System.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using ECommons.Automation;

namespace ChocoboRacing.Actions;

public static class ChatAction
{
    private static readonly string[] ChatCommandPrefixes =
        ["/echo ", "/alliance ", "/p ", "/a ", "/fc "];

    public static void SendChatMessage(string command)
    {
        var byteCount = Encoding.UTF8.GetByteCount(command);
        if (byteCount == 0 || byteCount > 500) return;
        Chat.SendMessage(command);
    }

    public static void SendChatMessage(string command, IChatGui chatGui, bool isTestingMode)
    {
        if (isTestingMode)
        {
            var text = StripChatPrefix(command);
            var seStr = new SeStringBuilder().AddText(text).Build();
            chatGui.Print(seStr, "Chocobo Racing");
            return;
        }
        SendChatMessage(command);
    }

    private static string StripChatPrefix(string command)
    {
        foreach (var prefix in ChatCommandPrefixes)
            if (command.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return command[prefix.Length..];
        return command;
    }
}
