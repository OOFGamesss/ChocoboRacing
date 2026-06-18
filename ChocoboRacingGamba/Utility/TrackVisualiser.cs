using System.Linq;
using ChocoboRacing.Config;

namespace ChocoboRacing.Utility;

/// <summary>
/// Generates race-track chat visuals and chocobo display names with circled-number markers.
/// </summary>
public static class TrackVisualiser
{
    private static readonly string[] CircledNumbers = { "", "", "", "", "", "", "", "", "", "" };

    public static string GetTrackVisual(PluginConfig config, int chocoboNumber)
    {
        if (chocoboNumber < 1 || chocoboNumber > config.ChocoboCount)
            return string.Empty;

        var index = chocoboNumber - 1;
        var pos = config.ChocoboPositions[index];
        var fl = config.FinishLine;
        if (fl <= 1)
            return string.Empty;

        var marker = index < CircledNumbers.Length ? CircledNumbers[index] : $"{chocoboNumber}";
        var size = fl - 1;
        var name = GetChocoboVisualName(config, chocoboNumber);

        if (pos <= 0)
        {
            var dots = string.Join(" ", Enumerable.Repeat("■", size));
            return $"{marker} | {dots} |   {name}";
        }
        
        if (pos >= fl)
        {
            var dots = string.Join(" ", Enumerable.Repeat("■", size));
            return $" | {dots} | {marker}  {name}";
        }
        
        var track = new string[size];
        for (int i = 0; i < size; i++)
        {
            track[i] = (i + 1 == pos) ? marker : "■";
        }
        var midDots = string.Join(" ", track);
        return $" | {midDots} |   {name}";
    }

    public static string GetChocoboName(PluginConfig config, int chocoboNumber)
    {
        if (chocoboNumber < 1 || chocoboNumber > config.ChocoboCount)
            return string.Empty;

        var index = chocoboNumber - 1;
        var marker = index < CircledNumbers.Length ? CircledNumbers[index] : $"{chocoboNumber}";
        string currentName = GetChocoboVisualName(config, chocoboNumber);

        return $"{marker} {currentName}";
    }

    public static string GetChocoboName(int chocoboNumber, string resolvedName)
    {
        if (chocoboNumber < 1) return string.Empty;
        var index = chocoboNumber - 1;
        var marker = index < CircledNumbers.Length ? CircledNumbers[index] : $"{chocoboNumber}";
        return $"{marker} {resolvedName}";
    }

    public static string GetChocoboVisualName(PluginConfig config, int chocoboNumber)
    {
        if (chocoboNumber < 1 || chocoboNumber > config.ChocoboCount)
            return string.Empty;

        string currentName = chocoboNumber switch
        {
            1 => config.Chocobo1Name,
            2 => config.Chocobo2Name,
            3 => config.Chocobo3Name,
            4 => config.Chocobo4Name,
            5 => config.Chocobo5Name,
            6 => config.Chocobo6Name,
            7 => config.Chocobo7Name,
            8 => config.Chocobo8Name,
            9 => config.Chocobo9Name,
            10 => config.Chocobo10Name,
            _ => "Unknown Chocobo"
        };

        if (string.IsNullOrEmpty(currentName))
        {
            currentName = "Unknown Chocobo";
        }

        return currentName;
    }
}
