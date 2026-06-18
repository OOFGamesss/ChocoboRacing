using System;
using System.Collections.Generic;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;

namespace ChocoboRacing.IPC;

/// <summary>
/// Pushes the active race settings to GambaWhere.
/// </summary>
public sealed class GambaWhereRules : IDisposable
{
    private const string PluginName = "Chocobo Racing";
    private const string Category = "Chocobo Racing";
    private const string Gate = "GambaWhere.SubmitRules";

    private static readonly TimeSpan PushInterval = TimeSpan.FromSeconds(30);

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

    public void Dispose()
    {
        _framework.Update -= OnFrameworkUpdate;
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

        var presets = cfg.SettingsPresets;
        var preset = presets[Math.Clamp(cfg.ActiveSettingsPresetIndex, 0, presets.Count - 1)];
        var partyCount = _plugin.PartyManager.GetPartyMembers().Count;

        var payload = new GambaWhereRulesPayload();
        payload.Rules.Add(new GambaWhereRuleEntry { Label = "Chocobo Runners", Value = preset.ChocoboCount });
        payload.Rules.Add(new GambaWhereRuleEntry { Label = "Race Track Length", Value = preset.FinishLine });
        payload.Rules.Add(new GambaWhereRuleEntry { Label = "Max Bet Per Chocobo", Value = (long)preset.MaxBetPerChocobo });
        payload.Rules.Add(new GambaWhereRuleEntry { Label = "Payout Odds", Value = (double)preset.PayoutOdds });
        if (preset.PerfectRace)
            payload.Rules.Add(new GambaWhereRuleEntry { Label = "Perfect Race Odds", Value = (double)preset.PerfectRaceOdds });
        payload.Rules.Add(new GambaWhereRuleEntry { Label = "Current Players", Value = partyCount });
        return payload;
    }
}

/// <summary>
/// Mirror of GambaWhere's IPC v2 rules contract. Property names (Rules / Label / Value) must match
/// what GambaWhere reflects on receipt. Value must be a string, bool, int, long or double; a maximum
/// of ten entries is accepted.
/// </summary>
public sealed class GambaWhereRulesPayload
{
    public List<GambaWhereRuleEntry> Rules { get; set; } = new();
}

public sealed class GambaWhereRuleEntry
{
    public string Label { get; set; } = string.Empty;

    public object? Value { get; set; }
}
