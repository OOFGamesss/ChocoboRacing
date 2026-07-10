using System;
using System.Collections.Generic;
using System.Linq;
using ChocoboRacing.Models;
using ChocoboRacing.State;
using Dalamud.Configuration;

/// <summary>
/// Persistent configuration that survives game crashes.
/// Stores all state: banks, bets, chocobo positions, and race phase.
/// </summary>
namespace ChocoboRacing.Config;

[Serializable]
public class PluginConfig : IPluginConfiguration
{
    public int Version { get; set; } = 2;

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
    public GrandNationalPhase GrandNationalPhase { get; set; } = GrandNationalPhase.Idle;
    public GrandNationalPrizeType GrandNationalPrizeType { get; set; } = GrandNationalPrizeType.Pot;
    public uint GrandNationalPrizeItemId { get; set; }
    public string GrandNationalPrizeItemName { get; set; } = string.Empty;
    public string GrandNationalPrizeText { get; set; } = string.Empty;
    public long GrandNationalEntryFee { get; set; } = 100_000;
    public long GrandNationalBoost { get; set; }
    public float GrandNationalVenueCutPercent { get; set; }
    public int GrandNationalFinishLine { get; set; } = 20;
    public bool GrandNationalCloseTimeEnabled { get; set; }
    public int GrandNationalCloseHour { get; set; }
    public int GrandNationalCloseMinute { get; set; }
    public bool GrandNationalAutoJoin { get; set; }
    public string GrandNationalJoinKeyword { get; set; } = "!join";
    public List<string> GrandNationalRegistered { get; set; } = new();
    public List<string> GrandNationalPaid { get; set; } = new();
    public Dictionary<string, long> GrandNationalEntryPaid { get; set; } = new();
    public List<string> GrandNationalLastRoster { get; set; } = new();
    public List<GrandNationalRunner> GrandNationalGrid { get; set; } = new();
    public int GrandNationalWinner { get; set; }
    public int GrandNationalRaceNumber { get; set; } = 1;
    public bool GrandNationalAutoAnnounceRegistration { get; set; } = true;
    public bool GrandNationalAutoAnnounceWinner { get; set; } = true;
    public bool GrandNationalAutoTellPaid { get; set; } = true;

    public bool WebMirrorEnabled { get; set; }
    public string ApiHostKey { get; set; } = string.Empty;
    public string WebSessionId { get; set; } = string.Empty;
    public string WebSpectatorUrl { get; set; } = string.Empty;
    public string WebVenueName { get; set; } = string.Empty;
    public string WebVenueImageUrl { get; set; } = string.Empty;
    public Dictionary<string, string> WebPins { get; set; } = new();

    public string AnnounceRulesMessage { get; set; } = "=============  Rules  ============= <se.2>\n♦ You can wager between 1K - 500K per Chocobo\n♦ You can bet on as many Chocobos as you wish\n♦ Chocobos racing today: 1 - {chocobos}\n♦ Current payout is {odds}x your bet\n♦ Current distance for your Chocobo to win: {distance} Yalms\n==========  How to Play  ==========\n♦ Trade your host Gil to create a bank\n♦ Choose a Chocobo and how much you wish to bet\n♦ Example: \"1 100k\" would put 100,000 on Chocobo 1\n♦ Wait for the race to start and all winnings will pay into your bank";
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
    public string GrandNationalRegistrationMessage { get; set; } = "/shout Grand National registration is open! Entry: {entryfee}. Type {keyword} to enter - the winner takes home {prize}!";
    public string GrandNationalRegistrationNoKeywordMessage { get; set; } = "/shout Grand National registration is open! Entry: {entryfee} - see me to enter. The winner takes home {prize}!";
    public string GrandNationalWinnerMessage { get; set; } = "/shout Grand National winner: #{number} {name} takes home {prize}!";
    public string GrandNationalClosingTimeMessage { get; set; } = "/shout The Grand National closes at {closetime} ST ({timeleft} left)! Entry: {entryfee} - {runners} runners in. The winner takes home {prize}!";
    public string GrandNationalRequestFeeMessage { get; set; } = "Please trade {entryfee} to enter the Grand National.";
    public string GrandNationalRunnerInviteMessage { get; set; } = "You're in the Grand National! See your chocobo number and watch the race live here: {url}";
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
        if (SettingsPresets.Count != 0)
        {
            ActiveSettingsPresetIndex = Math.Clamp(ActiveSettingsPresetIndex, 0, SettingsPresets.Count - 1);

            var active = SettingsPresets[ActiveSettingsPresetIndex];
            if (string.IsNullOrEmpty(active.WebVenueName) && !string.IsNullOrEmpty(WebVenueName))
                active.WebVenueName = WebVenueName;
            if (string.IsNullOrEmpty(active.WebVenueImageUrl) && !string.IsNullOrEmpty(WebVenueImageUrl))
                active.WebVenueImageUrl = WebVenueImageUrl;
            return;
        }

        SettingsPresets.Add(SettingsPreset.FromLiveConfig(this));
        ActiveSettingsPresetIndex = 0;
        Version = Math.Max(Version, 2);
        Plugin.PluginInterface.SavePluginConfig(this);
    }

    public void SyncActivePresetFromRoot()
    {
        if (SettingsPresets == null || SettingsPresets.Count == 0) return;
        ActiveSettingsPresetIndex = Math.Clamp(ActiveSettingsPresetIndex, 0, SettingsPresets.Count - 1);
        SettingsPresets[ActiveSettingsPresetIndex].CopyRaceAndChatFrom(this);
    }

    public bool TrySwitchActivePreset(int newIndex, RaceState state)
    {
        if (SettingsPresets == null || SettingsPresets.Count == 0) return false;
        if (newIndex < 0 || newIndex >= SettingsPresets.Count) return false;
        if (newIndex == ActiveSettingsPresetIndex) return true;
        if (state.Phase != RacePhase.Idle) return false;

        SyncActivePresetFromRoot();
        ActiveSettingsPresetIndex = newIndex;
        SettingsPresets[ActiveSettingsPresetIndex].ApplyRaceAndChatTo(this);
        state.UpdateSettings(ChocoboCount, FinishLine, PayoutOdds);
        return true;
    }

    public void AddPresetCloneFromActive(RaceState state)
    {
        if (SettingsPresets == null || SettingsPresets.Count == 0) return;
        if (state.Phase != RacePhase.Idle) return;

        SyncActivePresetFromRoot();
        var clone = SettingsPresets[ActiveSettingsPresetIndex].DeepCopy();
        clone.Name = SuggestNewPresetName();
        SettingsPresets.Add(clone);
        ActiveSettingsPresetIndex = SettingsPresets.Count - 1;
        Save();
    }

    public bool TryDeletePreset(int indexToRemove, RaceState state)
    {
        if (SettingsPresets == null || SettingsPresets.Count <= 1) return false;
        if (indexToRemove < 0 || indexToRemove >= SettingsPresets.Count) return false;
        if (state.Phase != RacePhase.Idle) return false;

        var oldActive = ActiveSettingsPresetIndex;
        SyncActivePresetFromRoot();
        SettingsPresets.RemoveAt(indexToRemove);

        if (oldActive == indexToRemove)
            ActiveSettingsPresetIndex = Math.Min(indexToRemove, SettingsPresets.Count - 1);
        else if (oldActive > indexToRemove)
            ActiveSettingsPresetIndex = oldActive - 1;

        ActiveSettingsPresetIndex = Math.Clamp(ActiveSettingsPresetIndex, 0, SettingsPresets.Count - 1);
        SettingsPresets[ActiveSettingsPresetIndex].ApplyRaceAndChatTo(this);
        state.UpdateSettings(ChocoboCount, FinishLine, PayoutOdds);
        return true;
    }

    public string SuggestNewPresetName()
    {
        const string baseName = "New preset";
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
}
