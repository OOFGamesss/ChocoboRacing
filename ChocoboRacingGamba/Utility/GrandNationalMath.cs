using System;
using System.Collections.Generic;
using System.Linq;
using ChocoboRacing.Config;
using ChocoboRacing.Models;

/// <summary>
/// Pure math helpers for Grand National race economics: entry eligibility, pot size, venue cut, and net payout pot derived from plugin configuration.
/// </summary>
namespace ChocoboRacing.Utility;

public static class GrandNationalMath
{
    public static bool IsFree(PluginConfig config) => config.GrandNationalEntryFee <= 0;

    public static List<string> OrderedRunnerEntries(PluginConfig config)
    {
        if (IsFree(config))
            return new List<string>(config.GrandNationalRegistered);
        return config.GrandNationalPaid
            .Where(p => config.GrandNationalRegistered.Any(r => r.Equals(p, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    public static int EligibleCount(PluginConfig config)
    {
        if (config.GrandNationalPhase >= GrandNationalPhase.Closed)
            return config.GrandNationalGrid.Count;
        return IsFree(config) ? config.GrandNationalRegistered.Count : config.GrandNationalPaid.Count;
    }

    public static long Pot(PluginConfig config)
    {
        var entries = IsFree(config) ? 0L : (long)EligibleCount(config) * config.GrandNationalEntryFee;
        return entries + config.GrandNationalBoost;
    }

    public static long Entries(PluginConfig config) => Math.Max(0, Pot(config) - config.GrandNationalBoost);

    public static long VenueCut(PluginConfig config) =>
        (long)(Entries(config) * config.GrandNationalVenueCutPercent / 100.0);

    public static long NetPot(PluginConfig config) =>
        IsPotPrize(config) ? Math.Max(0, Pot(config) - VenueCut(config)) : 0;

    public static bool IsPotPrize(PluginConfig config) =>
        config.GrandNationalPrizeType == GrandNationalPrizeType.Pot;

    public static string PrizeTypeWire(PluginConfig config) => config.GrandNationalPrizeType switch
    {
        GrandNationalPrizeType.Item => "item",
        GrandNationalPrizeType.Custom => "custom",
        _ => "pot",
    };

    public static string PrizeLabel(PluginConfig config) => config.GrandNationalPrizeType switch
    {
        GrandNationalPrizeType.Item => config.GrandNationalPrizeItemName.Trim(),
        GrandNationalPrizeType.Custom => config.GrandNationalPrizeText.Trim(),
        _ => string.Empty,
    };
}
