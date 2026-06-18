using System;
using System.Globalization;
using System.Linq;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;

namespace ChocoboRacing.Events;

/// <summary>
/// Extracts player identity and parses amount strings from incoming chat messages.
/// </summary>
public static class ChatHelper
{
    public static string ExtractPlayerName(SeString sender)
    {
        foreach (var payload in sender.Payloads)
            if (payload is PlayerPayload pp) return pp.PlayerName;
        return sender.TextValue.Trim();
    }

    public static string ExtractPlayerWorld(SeString sender)
    {
        foreach (var payload in sender.Payloads)
            if (payload is PlayerPayload pp) return pp.World.Value.Name.ToString();
        return string.Empty;
    }

    public static long ParseAmountString(string amountStr)
    {
        amountStr = amountStr.Replace(" ", " ").Replace(",", "").Replace(" ", "").ToLowerInvariant().Trim();
        long multiplier = 1;

        if (amountStr.EndsWith("k")) { multiplier = 1000; amountStr = amountStr[..^1]; }
        else if (amountStr.EndsWith("m")) { multiplier = 1000000; amountStr = amountStr[..^1]; }
        else if (amountStr.EndsWith("b")) { multiplier = 1000000000; amountStr = amountStr[..^1]; }

        // Multiple dots = European thousand separators (e.g. 3.500.000); strip all dots
        if (amountStr.Count(c => c == '.') > 1)
            amountStr = amountStr.Replace(".", "");

        if (double.TryParse(amountStr, NumberStyles.Number, CultureInfo.InvariantCulture, out var val))
        {
            var result = val * multiplier;
            if (result > long.MaxValue || result < 0)
                return 0;
            return (long)result;
        }
        return 0;
    }
}

