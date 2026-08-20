using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using ChocoboRacing.Models;
using ChocoboRacing.State;
using ChocoboRacing.Utility;
using Dalamud.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>
/// Persistent configuration that survives game crashes.
/// Stores all state: banks, bets, chocobo positions, and race phase.
/// </summary>
namespace ChocoboRacing.Config;

[Serializable]
public class PluginConfig : IPluginConfiguration
{
    private const string LegacyRafflePrefix = "GrandNational";

    [JsonExtensionData]
    private IDictionary<string, JToken>? legacyFields;

    public int Version { get; set; } = 2;

    [JsonIgnore]
    public int SettingsRevision { get; private set; }

    public List<SettingsPreset> SettingsPresets { get; set; } = new();
    public int ActiveSettingsPresetIndex { get; set; }
    public int ChocoboCount { get; set; } = 5;
    public int FinishLine { get; set; } = 5;
    public float PayoutOdds { get; set; } = 4.0f;
    public int MaxBetPerChocobo { get; set; } = 1_000_000;
    public bool PerfectRace { get; set; }
    public float PerfectRaceOdds { get; set; } = 20f;
    public bool IsHosting { get; set; }
    public RacePhase CurrentPhase { get; set; } = RacePhase.Idle;
    public int RoundNumber { get; set; } = 1;
    public int[] ChocoboPositions { get; set; } = Array.Empty<int>();
    public List<PlayerBank> Banks { get; set; } = new();
    public List<Bet> CurrentBets { get; set; } = new();
    public Dictionary<string, float[]> WindowPositions { get; set; } = new();
    public Dictionary<string, float[]> WindowSizes { get; set; } = new();
    public List<SessionRecord> Sessions { get; set; } = new();
    public bool HasMigratedHistoryToSqlite { get; set; } = false;
    public BetlistLayout BetlistLayout { get; set; } = BetlistLayout.SplitByChocobo;
    public float VenueCutPercent { get; set; }

    public RaceMode RaceMode { get; set; } = RaceMode.Classic;
    public RafflePhase RafflePhase { get; set; } = RafflePhase.Idle;
    public RafflePrizeType RafflePrizeType { get; set; } = RafflePrizeType.Pot;
    public uint RafflePrizeItemId { get; set; }
    public string RafflePrizeItemName { get; set; } = string.Empty;
    public string RafflePrizeText { get; set; } = string.Empty;
    public long RaffleEntryFee { get; set; } = 100_000;
    public long RaffleBoost { get; set; }
    public float RaffleVenueCutPercent { get; set; }
    public int RaffleFinishLine { get; set; } = 20;
    public bool RaffleCloseTimeEnabled { get; set; }
    public int RaffleCloseHour { get; set; }
    public int RaffleCloseMinute { get; set; }
    public bool RaffleAutoJoin { get; set; }
    public string RaffleJoinKeyword { get; set; } = "!join";
    public bool RaffleJoinSoundEnabled { get; set; }
    public int RaffleJoinSoundEffectId { get; set; } = 1;
    public List<string> RaffleRegistered { get; set; } = new();
    public List<string> RafflePaid { get; set; } = new();
    public Dictionary<string, long> RaffleEntryPaid { get; set; } = new();
    public List<string> RaffleLastRoster { get; set; } = new();
    public List<RaffleRunner> RaffleGrid { get; set; } = new();
    public int RaffleWinner { get; set; }
    public int RaffleRaceNumber { get; set; } = 1;
    public bool RaffleAutoAnnounceRegistration { get; set; } = true;
    public bool RaffleAutoAnnounceWinner { get; set; } = true;
    public bool RaffleAutoTellPaid { get; set; } = true;

    public bool WebMirrorEnabled { get; set; }
    public string ApiHostKey { get; set; } = string.Empty;
    public string WebSessionId { get; set; } = string.Empty;
    public string WebSpectatorUrl { get; set; } = string.Empty;
    public string WebVenueName { get; set; } = string.Empty;
    public string WebVenueImageUrl { get; set; } = string.Empty;
    public Dictionary<string, string> WebPins { get; set; } = new();

    public string AnnounceRulesMessage { get; set; } = "=============  Rules  ============= <se.2>\n♦ You can wager between 1K - 500K per Chocobo\n♦ You can bet on as many Chocobos as you wish\n♦ Chocobos racing today: 1 - {chocobos}\n♦ Current payout is {odds}x your bet\n♦ Current distance for your Chocobo to win: {distance} Yalms\n==========  How to Play  ==========\n♦ Trade your host Gil to create a bank\n♦ Choose a Chocobo and how much you wish to bet\n♦ Example: \"1 100k\" would put 100,000 on Chocobo 1\n♦ Wait for the race to start and all winnings will pay into your bank";
    public string AdvertiseMessage { get; set; } = "Come on down to play Chocobo Racing! {chocobos} chocobos racing, {odds}x payout on your bet!";
    public string RaceStartedMessage { get; set; } = "================== <se.7>\nＲＡＣＥ   ＳＴＡＲＴＥＤ\n==================\n{racelist}\n==================";
    public string NoMoreBetsMessage { get; set; } = "=======  BETTING CLOSED  ======= <se.6>\n{betlist}";
    public string VoidRaceMessage { get; set; } = "Race has been cancelled. All bets have returned to your banks. <se.11>";
    public string OpenBettingMessage { get; set; } = "=========  BETTING OPEN  ========= <se.5>\nPlace your bets: '[Chocobo Number or Name] [Amount]'\n{chocobonames}";
    public string LastBetsMessage { get; set; } = "Last Bets! <se.2>";
    public string RaceWinnerMessage { get; set; } = "/congratulate\n====================== <se.3>\nＷＩＮＮＥＲ - {winningchocobo}\n======================\n{winnerlist}\n======================";
    public string TellBankBalanceMessage { get; set; } = "You have {bankvalue} Gil in the bank.";
    public string TellWebPinMessage { get; set; } = "Your Chocobo Racing PIN is {pin}. Place bets here: {url}";
    public string RandomLineMessage { get; set; } = "/cheer";
    public string WinnerLineMessage { get; set; } = "/congratulate";
    public string LoseLineMessage { get; set; } = "/sad";
    public string RaffleRegistrationMessage { get; set; } = "/shout Raffle registration is open! Entry: {entryfee}. Type {keyword} to enter - the winner takes home {prize}!";
    public string RaffleRegistrationNoKeywordMessage { get; set; } = "/shout Raffle registration is open! Entry: {entryfee} - see me to enter. The winner takes home {prize}!";
    public string RaffleAdvertiseMessage { get; set; } = "Come on down to play Chocobo Racing! Raffle entry: {entryfee} - the winner takes home {prize}!";
    public string RaffleWinnerMessage { get; set; } = "/shout Raffle winner: #{number} {name} takes home {prize}!";
    public string RaffleClosingTimeMessage { get; set; } = "/shout The raffle closes at {closetime} ST ({timeleft} left)! Entry: {entryfee} - {runners} runners in. The winner takes home {prize}!";
    public string RaffleRequestFeeMessage { get; set; } = "Please trade {entryfee} to enter the raffle.";
    public string RaffleRunnerInviteMessage { get; set; } = "You're in the raffle! See your chocobo number and watch the race live here: {url}";
    public string Chocobo1Name { get; set; } = "Comet";
    public string Chocobo2Name { get; set; } = "Nugget";
    public string Chocobo3Name { get; set; } = "Bolt";
    public string Chocobo4Name { get; set; } = "Flash";
    public string Chocobo5Name { get; set; } = "Blaze";
    public string Chocobo6Name { get; set; } = "Shadow";
    public string Chocobo7Name { get; set; } = "Alpha";
    public string Chocobo8Name { get; set; } = "Zenos";
    public string Chocobo9Name { get; set; } = "Echo";
    public string Chocobo10Name { get; set; } = "Titan";

    public void EnsurePresetsMigrated()
    {
        SettingsPresets ??= new List<SettingsPreset>();
        if (SettingsPresets.Count == 0)
        {
            SettingsPresets.Add(SettingsPreset.FromLiveConfig(this));
            ActiveSettingsPresetIndex = 0;
            Version = Math.Max(Version, 3);
            Plugin.PluginInterface.SavePluginConfig(this);
            return;
        }

        ActiveSettingsPresetIndex = Math.Clamp(ActiveSettingsPresetIndex, 0, SettingsPresets.Count - 1);
        MigrateVenueFieldsIntoActivePreset();

        if (!MigratePresetSchemas()) return;

        Version = Math.Max(Version, 3);
        Plugin.PluginInterface.SavePluginConfig(this);
    }

    public void SyncActivePresetFromRoot()
    {
        if (SettingsPresets == null || SettingsPresets.Count == 0) return;
        ActiveSettingsPresetIndex = Math.Clamp(ActiveSettingsPresetIndex, 0, SettingsPresets.Count - 1);
        SettingsPresets[ActiveSettingsPresetIndex].CopyFrom(this);
    }

    public bool CanEditPresets(RaceState state) =>
        state.Phase == RacePhase.Idle && RafflePhase == RafflePhase.Idle;

    public bool TrySwitchActivePreset(int newIndex, RaceState state)
    {
        if (SettingsPresets == null || SettingsPresets.Count == 0) return false;
        if (newIndex < 0 || newIndex >= SettingsPresets.Count) return false;
        if (newIndex == ActiveSettingsPresetIndex) return true;
        if (!CanEditPresets(state)) return false;

        SyncActivePresetFromRoot();
        ActiveSettingsPresetIndex = newIndex;
        ApplyActivePreset(state);
        return true;
    }

    public void AddPresetCloneFromActive(RaceState state)
    {
        if (SettingsPresets == null || SettingsPresets.Count == 0) return;
        if (!CanEditPresets(state)) return;

        SyncActivePresetFromRoot();
        var clone = SettingsPresets[ActiveSettingsPresetIndex].DeepCopy();
        clone.Name = UniquePresetName(clone.Name);
        SettingsPresets.Add(clone);
        ActiveSettingsPresetIndex = SettingsPresets.Count - 1;
        Save();
    }

    public bool TryDeletePreset(int indexToRemove, RaceState state)
    {
        if (SettingsPresets == null || SettingsPresets.Count <= 1) return false;
        if (indexToRemove < 0 || indexToRemove >= SettingsPresets.Count) return false;
        if (!CanEditPresets(state)) return false;

        var oldActive = ActiveSettingsPresetIndex;
        SyncActivePresetFromRoot();
        SettingsPresets.RemoveAt(indexToRemove);

        if (oldActive == indexToRemove)
            ActiveSettingsPresetIndex = Math.Min(indexToRemove, SettingsPresets.Count - 1);
        else if (oldActive > indexToRemove)
            ActiveSettingsPresetIndex = oldActive - 1;

        ActiveSettingsPresetIndex = Math.Clamp(ActiveSettingsPresetIndex, 0, SettingsPresets.Count - 1);
        ApplyActivePreset(state);
        return true;
    }

    public string ExportActivePreset()
    {
        if (SettingsPresets == null || SettingsPresets.Count == 0) return string.Empty;
        SyncActivePresetFromRoot();
        return PresetShare.Export(SettingsPresets[ActiveSettingsPresetIndex]);
    }

    public bool TryImportPreset(string code, RaceState state)
    {
        if (SettingsPresets == null) return false;
        if (!CanEditPresets(state)) return false;
        if (!PresetShare.TryImport(code, out var imported)) return false;

        imported.MigrateFrom(this);
        imported.Name = UniquePresetName(imported.Name);

        SyncActivePresetFromRoot();
        SettingsPresets.Add(imported);
        ActiveSettingsPresetIndex = SettingsPresets.Count - 1;
        ApplyActivePreset(state);
        return true;
    }

    public string UniquePresetName(string desired)
    {
        var baseName = string.IsNullOrWhiteSpace(desired) ? "New preset" : desired.Trim();
        if (SettingsPresets!.All(p => p.Name != baseName)) return baseName;
        for (var i = 2; ; i++)
        {
            var candidate = $"{baseName} ({i})";
            if (SettingsPresets.All(p => p.Name != candidate)) return candidate;
        }
    }

    public void Save()
    {
        if (IsHosting)
            SyncActivePresetFromRoot();
        Plugin.PluginInterface.SavePluginConfig(this);
    }

    private void ApplyActivePreset(RaceState state)
    {
        SettingsPresets![ActiveSettingsPresetIndex].ApplyTo(this);
        state.UpdateSettings(ChocoboCount, FinishLine, PayoutOdds);
        SettingsRevision++;
        Save();
    }

    private void MigrateVenueFieldsIntoActivePreset()
    {
        var active = SettingsPresets![ActiveSettingsPresetIndex];
        if (string.IsNullOrEmpty(active.WebVenueName) && !string.IsNullOrEmpty(WebVenueName))
            active.WebVenueName = WebVenueName;
        if (string.IsNullOrEmpty(active.WebVenueImageUrl) && !string.IsNullOrEmpty(WebVenueImageUrl))
            active.WebVenueImageUrl = WebVenueImageUrl;
    }

    private bool MigratePresetSchemas()
    {
        var migrated = false;
        foreach (var preset in SettingsPresets!)
        {
            if (preset.SchemaVersion >= SettingsPreset.CurrentSchemaVersion) continue;
            preset.MigrateFrom(this);
            migrated = true;
        }
        return migrated;
    }

    [OnDeserialized]
    private void MigrateLegacyRaffleFields(StreamingContext context)
    {
        if (legacyFields == null || legacyFields.Count == 0) return;

        foreach (var pair in legacyFields)
        {
            if (!pair.Key.StartsWith(LegacyRafflePrefix, StringComparison.Ordinal)) continue;

            var property = GetType().GetProperty("Raffle" + pair.Key[LegacyRafflePrefix.Length..]);
            if (property == null || !property.CanWrite || pair.Value == null) continue;

            try
            {
                property.SetValue(this, pair.Value.ToObject(property.PropertyType));
            }
            catch (Exception)
            {
            }
        }

        legacyFields = null;
    }
}
