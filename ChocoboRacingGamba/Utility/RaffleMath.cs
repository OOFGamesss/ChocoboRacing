using System;
using System.Collections.Generic;
using System.Linq;
using ChocoboRacing.Config;
using ChocoboRacing.Models;

/// <summary>
/// Pure math helpers for raffle race economics: entry eligibility, pot size, venue cut, and net payout pot derived from plugin configuration.
/// </summary>
namespace ChocoboRacing.Utility;

public static class RaffleMath
{
    public static bool IsFree(PluginConfig config) => config.RaffleEntryFee <= 0;

    public static List<string> OrderedRunnerEntries(PluginConfig config)
    {
        if (IsFree(config))
            return new List<string>(config.RaffleRegistered);
        return config.RafflePaid
            .Where(p => config.RaffleRegistered.Any(r => r.Equals(p, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    public static int EligibleCount(PluginConfig config)
    {
        if (config.RafflePhase >= RafflePhase.Closed)
            return config.RaffleGrid.Count;
        return IsFree(config) ? config.RaffleRegistered.Count : config.RafflePaid.Count;
    }

    public static long Pot(PluginConfig config)
    {
        var entries = IsFree(config) ? 0L : (long)EligibleCount(config) * config.RaffleEntryFee;
        return entries + config.RaffleBoost;
    }

    public static long Entries(PluginConfig config) => Math.Max(0, Pot(config) - config.RaffleBoost);

    public static long VenueCut(PluginConfig config) =>
        (long)(Entries(config) * config.RaffleVenueCutPercent / 100.0);

    public static long NetPot(PluginConfig config) =>
        IsPotPrize(config) ? Math.Max(0, Pot(config) - VenueCut(config)) : 0;

    public static bool IsPotPrize(PluginConfig config) =>
        config.RafflePrizeType == RafflePrizeType.Pot;

    public static string PrizeTypeWire(PluginConfig config) => config.RafflePrizeType switch
    {
        RafflePrizeType.Item => "item",
        RafflePrizeType.Custom => "custom",
        _ => "pot",
    };

    public static string PrizeLabel(PluginConfig config) => config.RafflePrizeType switch
    {
        RafflePrizeType.Item => config.RafflePrizeItemName.Trim(),
        RafflePrizeType.Custom => config.RafflePrizeText.Trim(),
        _ => string.Empty,
    };
}
