using System;
using FFXIVClientStructs.FFXIV.Client.System.Framework;

/// <summary>
/// Server time helpers for the Grand National closing time, read from the game clock.
/// </summary>
namespace ChocoboRacing.Utility;

public static class ServerTimeUtil
{
    public static DateTime NowServer()
    {
        var epoch = Framework.GetServerTime();
        if (epoch > 0) return DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime;
        return DateTime.UtcNow;
    }

    public static (int Hour, int Minute) SuggestCloseTime()
    {
        var t = NowServer().AddHours(1);
        if (t.Minute > 0) t = t.AddHours(1);
        return (t.Hour, 0);
    }

    public static int SecondsUntil(int hour, int minute)
    {
        var h = Math.Clamp(hour, 0, 23);
        var m = Math.Clamp(minute, 0, 59);
        var now = NowServer();
        var target = new DateTime(now.Year, now.Month, now.Day, h, m, 0, DateTimeKind.Utc);
        var seconds = (int)Math.Round((target - now).TotalSeconds);
        if (seconds < -3600) seconds += 86400;
        return seconds;
    }

    public static string FormatTimeLeft(int hour, int minute)
    {
        var seconds = SecondsUntil(hour, minute);
        if (seconds <= 0) return "Closing...";
        var remaining = TimeSpan.FromSeconds(seconds);
        if (remaining.TotalHours >= 1)
            return $"{(int)remaining.TotalHours}h {remaining.Minutes}m {remaining.Seconds}s";
        return $"{remaining.Minutes}m {remaining.Seconds}s";
    }

    public static string FormatCloseLabel(int hour, int minute) =>
        $"{Math.Clamp(hour, 0, 23):00}:{Math.Clamp(minute, 0, 59):00}";
}
