using System;
using System.Collections.Generic;

namespace ChocoboRacing.Utility;

/// <summary>
/// Generates 6-digit web betting PINs that are unique within a session.
/// </summary>
internal static class PinGenerator
{
    internal static string Generate(IEnumerable<string> existing)
    {
        var taken = new HashSet<string>(existing);
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var pin = Random.Shared.Next(0, 1_000_000).ToString("D6");
            if (!taken.Contains(pin)) return pin;
        }
        return Random.Shared.Next(0, 1_000_000).ToString("D6");
    }
}
