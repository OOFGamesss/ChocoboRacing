using System.Text.RegularExpressions;

/// <summary>
/// Provides a helper for sanitising untrusted display names (e.g. player or chocobo names) by stripping disallowed characters.
/// </summary>
namespace ChocoboRacing.Utility;

public static class SanitiseHelper
{
    public static string SanitiseName(string? name)
    {
        if (string.IsNullOrEmpty(name)) return "Unknown";
        return Regex.Replace(name, @"[^\w\s'@-]", "").Trim();
    }
}
