using System;
using System.Collections.Generic;
using ChocoboRacing.Config;
using ChocoboRacing.Models;
using ChocoboRacing.Utility;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;

/// <summary>
/// Pushes the active race settings to GambaWhere.
/// </summary>
namespace ChocoboRacing.IPC;

public sealed class GambaWhereRules : IDisposable
{
    private const string PluginName = "Chocobo Racing";
    private const string Category = "Chocobo Racing";
    private const string Gate = "GambaWhere.SubmitRules";

    private static readonly TimeSpan PushInterval = TimeSpan.FromSeconds(1);

    private readonly ICallGateSubscriber<string, string, object, bool> _submitRules;
    private readonly IFramework _framework;
    private readonly IPluginLog _log;
    private readonly Plugin _plugin;

    private DateTime _nextPushUtc;

    public GambaWhereRules(
        IDalamudPluginInterface pluginInterface,
        IFramework framework,
        IPluginLog log,
        Plugin plugin)
    {
        _framework = framework;
        _log = log;
        _plugin = plugin;
        _submitRules = pluginInterface.GetIpcSubscriber<string, string, object, bool>(Gate);

        _framework.Update += OnFrameworkUpdate;
        _nextPushUtc = DateTime.UtcNow;
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (DateTime.UtcNow < _nextPushUtc) return;

        _nextPushUtc = DateTime.UtcNow + PushInterval;
        PushRules();
    }

    private void PushRules()
    {
        try
        {
            _submitRules.InvokeFunc(PluginName, Category, BuildPayload());
        }
        catch (Exception ex)
        {
            _log.Debug($"GambaWhere SubmitRules IPC unavailable: {ex.Message}");
        }
    }

    private GambaWhereRulesPayload BuildPayload()
    {
        var cfg = _plugin.Configuration;
        cfg.EnsurePresetsMigrated();

        var payload = new GambaWhereRulesPayload();
        if (cfg.RaceMode == RaceMode.GrandNational)
            AddGrandNationalRules(payload, cfg);
        else
            AddClassicRules(payload, cfg);

        var spectatorUrl = _plugin.WebMirror.SpectatorUrl;
        if (!string.IsNullOrEmpty(spectatorUrl))
            payload.Rules.Add(new GambaWhereRuleEntry { Label = "Spectator URL", Value = spectatorUrl });

        return payload;
    }

    private void AddClassicRules(GambaWhereRulesPayload payload, PluginConfig cfg)
    {
        var presets = cfg.SettingsPresets;
        var preset = presets[Math.Clamp(cfg.ActiveSettingsPresetIndex, 0, presets.Count - 1)];
        var partyCount = _plugin.PartyManager.GetPartyMembers().Count;

        payload.Rules.Add(new GambaWhereRuleEntry { Label = "Race Type", Value = "Classic" });
        payload.Rules.Add(new GambaWhereRuleEntry { Label = "Chocobo Runners", Value = preset.ChocoboCount });
        payload.Rules.Add(new GambaWhereRuleEntry { Label = "Race Track Length", Value = preset.FinishLine });
        payload.Rules.Add(new GambaWhereRuleEntry { Label = "Max Bet Per Chocobo", Value = (long)preset.MaxBetPerChocobo });
        payload.Rules.Add(new GambaWhereRuleEntry { Label = "Payout Odds", Value = (double)preset.PayoutOdds });
        if (preset.PerfectRace)
            payload.Rules.Add(new GambaWhereRuleEntry { Label = "Perfect Race Odds", Value = (double)preset.PerfectRaceOdds });
        payload.Rules.Add(new GambaWhereRuleEntry { Label = "Current Players", Value = partyCount });
    }

    private void AddGrandNationalRules(GambaWhereRulesPayload payload, PluginConfig cfg)
    {
        payload.Rules.Add(new GambaWhereRuleEntry { Label = "Race Type", Value = "Grand National" });
        if (cfg.GrandNationalPhase == GrandNationalPhase.Idle)
            return;

        if (GrandNationalMath.IsPotPrize(cfg))
        {
            payload.Rules.Add(new GambaWhereRuleEntry { Label = "Total Pot", Value = GrandNationalMath.NetPot(cfg) });
            payload.Rules.Add(new GambaWhereRuleEntry { Label = "Boosted Pot", Value = cfg.GrandNationalBoost });
        }
        else
        {
            var label = GrandNationalMath.PrizeLabel(cfg);
            payload.Rules.Add(new GambaWhereRuleEntry { Label = "Prize", Value = string.IsNullOrWhiteSpace(label) ? "(not set)" : label });
        }

        payload.Rules.Add(new GambaWhereRuleEntry
        {
            Label = "Entry Cost",
            Value = GrandNationalMath.IsFree(cfg) ? (object)"Free" : cfg.GrandNationalEntryFee,
        });
        payload.Rules.Add(new GambaWhereRuleEntry { Label = "Current Runners", Value = GrandNationalMath.EligibleCount(cfg) });
        if (cfg.GrandNationalCloseTimeEnabled)
            payload.Rules.Add(new GambaWhereRuleEntry
            {
                Label = "Registration Closes",
                Value = $"{ServerTimeUtil.FormatCloseLabel(cfg.GrandNationalCloseHour, cfg.GrandNationalCloseMinute)} ST",
            });
        if (cfg.GrandNationalAutoJoin && !string.IsNullOrWhiteSpace(cfg.GrandNationalJoinKeyword))
            payload.Rules.Add(new GambaWhereRuleEntry { Label = "Join Keyword", Value = cfg.GrandNationalJoinKeyword });
    }

    public void Dispose()
    {
        _framework.Update -= OnFrameworkUpdate;
    }
}

public sealed class GambaWhereRulesPayload
{
    public List<GambaWhereRuleEntry> Rules { get; set; } = new();
}

public sealed class GambaWhereRuleEntry
{
    public string Label { get; set; } = string.Empty;

    public object? Value { get; set; }
}
