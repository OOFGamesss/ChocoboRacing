using System;
using ChocoboRacing.Config;
using Newtonsoft.Json;

/// <summary>
/// Named host settings bundle holding every configurable host setting for both race modes,
/// excluding game keys, live session identifiers, and volatile race state.
/// </summary>
namespace ChocoboRacing.Models;

[Serializable]
public sealed class SettingsPreset
{
    public const string DefaultName = "Default";
    public const int CurrentSchemaVersion = 1;

    public string Name { get; set; } = DefaultName;
    public int SchemaVersion { get; set; }

    public int ChocoboCount { get; set; } = 5;
    public int FinishLine { get; set; } = 5;
    public float PayoutOdds { get; set; } = 4.0f;
    public int MaxBetPerChocobo { get; set; } = 1_000_000;
    public bool PerfectRace { get; set; }
    public float PerfectRaceOdds { get; set; } = 20f;
    public float VenueCutPercent { get; set; }
    public BetlistLayout BetlistLayout { get; set; } = BetlistLayout.SplitByChocobo;

    public string WebVenueName { get; set; } = string.Empty;
    public string WebVenueImageUrl { get; set; } = string.Empty;

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

    public string AnnounceRulesMessage { get; set; } =
        "=============  Rules  ============= <se.2>\n♦ You can wager between 1K - 500K per Chocobo\n♦ You can bet on as many Chocobos as you wish\n♦ Chocobos racing today: 1 - {chocobos}\n♦ Current payout is {odds}x your bet\n♦ Current distance for your Chocobo to win: {distance} Yalms\n==========  How to Play  ==========\n♦ Trade your host Gil to create a bank\n♦ Choose a Chocobo and how much you wish to bet\n♦ Example: \"1 100k\" would put 100,000 on Chocobo 1\n♦ Wait for the race to start and all winnings will pay into your bank";
    public string AdvertiseMessage { get; set; } =
        "Come on down to play Chocobo Racing! {chocobos} chocobos racing, {odds}x payout on your bet!";
    public string RaceStartedMessage { get; set; } =
        "================== <se.7>\nＲＡＣＥ   ＳＴＡＲＴＥＤ\n==================\n{racelist}\n==================";
    public string NoMoreBetsMessage { get; set; } =
        "=======  BETTING CLOSED  ======= <se.6>\n{betlist}";
    public string VoidRaceMessage { get; set; } =
        "Race has been cancelled. All bets have returned to your banks. <se.11>";
    public string OpenBettingMessage { get; set; } =
        "=========  BETTING OPEN  ========= <se.5>\nPlace your bets: '[Chocobo Number or Name] [Amount]'\n{chocobonames}";
    public string RaceWinnerMessage { get; set; } =
        "\n====================== <se.3>\nＷＩＮＮＥＲ - {winningchocobo}\n======================\n{winnerlist}\n======================";
    public string BetConfirmedMessage { get; set; } =
        "[BET CONFIRMED] {betplayer}: {betchocobo} for {betamount} Gil";
    public string BetRemovedMessage { get; set; } =
        "[BET REMOVED] {betplayer}: {betchocobo} for {betamount} Gil";
    public string LastBetsMessage { get; set; } = "Last Bets! <se.2>";
    public string TellBankBalanceMessage { get; set; } = "You have {bankvalue} Gil in the bank.";
    public string TellWebPinMessage { get; set; } = "Your Chocobo Racing PIN is {pin}. Place bets here: {url}";
    public string RandomLineMessage { get; set; } = "/cheer";
    public string WinnerLineMessage { get; set; } = "/congratulate";
    public string LoseLineMessage { get; set; } = "/sad";

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
    public bool RaffleAutoAnnounceRegistration { get; set; } = true;
    public bool RaffleAutoAnnounceWinner { get; set; } = true;
    public bool RaffleAutoTellPaid { get; set; } = true;

    public string RaffleRegistrationMessage { get; set; } =
        "/shout Raffle registration is open! Entry: {entryfee}. Type {keyword} to enter - the winner takes home {prize}!";
    public string RaffleRegistrationNoKeywordMessage { get; set; } =
        "/shout Raffle registration is open! Entry: {entryfee} - see me to enter. The winner takes home {prize}!";
    public string RaffleAdvertiseMessage { get; set; } =
        "Come on down to play Chocobo Racing! Raffle entry: {entryfee} - the winner takes home {prize}!";
    public string RaffleWinnerMessage { get; set; } =
        "/shout Raffle winner: #{number} {name} takes home {prize}!";
    public string RaffleClosingTimeMessage { get; set; } =
        "/shout The raffle closes at {closetime} ST ({timeleft} left)! Entry: {entryfee} - {runners} runners in. The winner takes home {prize}!";
    public string RaffleRequestFeeMessage { get; set; } = "Please trade {entryfee} to enter the raffle.";
    public string RaffleRunnerInviteMessage { get; set; } =
        "You're in the raffle! See your chocobo number and watch the race live here: {url}";

    public static void ResetClassicChatToDefaults(PluginConfig cfg) => new SettingsPreset().ApplyClassicChatTo(cfg);

    public static void ResetRaffleChatToDefaults(PluginConfig cfg) => new SettingsPreset().ApplyRaffleChatTo(cfg);

    public static SettingsPreset FromLiveConfig(PluginConfig cfg)
    {
        var p = new SettingsPreset { Name = DefaultName };
        p.CopyFrom(cfg);
        return p;
    }

    public void CopyFrom(PluginConfig cfg)
    {
        CopyRaceFrom(cfg);
        CopyChocoboNamesFrom(cfg);
        CopyClassicChatFrom(cfg);
        CopyRaffleFrom(cfg);
        CopyRaffleChatFrom(cfg);
        SchemaVersion = CurrentSchemaVersion;
    }

    public void ApplyTo(PluginConfig cfg)
    {
        ApplyRaceTo(cfg);
        ApplyChocoboNamesTo(cfg);
        ApplyClassicChatTo(cfg);
        ApplyRaffleTo(cfg);
        ApplyRaffleChatTo(cfg);
    }

    public void MigrateFrom(PluginConfig cfg)
    {
        if (SchemaVersion >= CurrentSchemaVersion) return;

        VenueCutPercent = cfg.VenueCutPercent;
        AdvertiseMessage = cfg.AdvertiseMessage;
        CopyRaffleFrom(cfg);
        CopyRaffleChatFrom(cfg);
        SchemaVersion = CurrentSchemaVersion;
    }

    public SettingsPreset DeepCopy() =>
        JsonConvert.DeserializeObject<SettingsPreset>(JsonConvert.SerializeObject(this))!;

    private void CopyRaceFrom(PluginConfig cfg)
    {
        ChocoboCount = cfg.ChocoboCount;
        FinishLine = cfg.FinishLine;
        PayoutOdds = cfg.PayoutOdds;
        MaxBetPerChocobo = cfg.MaxBetPerChocobo;
        PerfectRace = cfg.PerfectRace;
        PerfectRaceOdds = cfg.PerfectRaceOdds;
        VenueCutPercent = cfg.VenueCutPercent;
        BetlistLayout = cfg.BetlistLayout;
        WebVenueName = cfg.WebVenueName;
        WebVenueImageUrl = cfg.WebVenueImageUrl;
    }

    private void CopyChocoboNamesFrom(PluginConfig cfg)
    {
        Chocobo1Name = cfg.Chocobo1Name;
        Chocobo2Name = cfg.Chocobo2Name;
        Chocobo3Name = cfg.Chocobo3Name;
        Chocobo4Name = cfg.Chocobo4Name;
        Chocobo5Name = cfg.Chocobo5Name;
        Chocobo6Name = cfg.Chocobo6Name;
        Chocobo7Name = cfg.Chocobo7Name;
        Chocobo8Name = cfg.Chocobo8Name;
        Chocobo9Name = cfg.Chocobo9Name;
        Chocobo10Name = cfg.Chocobo10Name;
    }

    private void CopyClassicChatFrom(PluginConfig cfg)
    {
        AnnounceRulesMessage = cfg.AnnounceRulesMessage;
        AdvertiseMessage = cfg.AdvertiseMessage;
        RaceStartedMessage = cfg.RaceStartedMessage;
        NoMoreBetsMessage = cfg.NoMoreBetsMessage;
        VoidRaceMessage = cfg.VoidRaceMessage;
        OpenBettingMessage = cfg.OpenBettingMessage;
        BetConfirmedMessage = cfg.BetConfirmedMessage;
        BetRemovedMessage = cfg.BetRemovedMessage;
        LastBetsMessage = cfg.LastBetsMessage;
        RaceWinnerMessage = cfg.RaceWinnerMessage;
        TellBankBalanceMessage = cfg.TellBankBalanceMessage;
        TellWebPinMessage = cfg.TellWebPinMessage;
        RandomLineMessage = cfg.RandomLineMessage;
        WinnerLineMessage = cfg.WinnerLineMessage;
        LoseLineMessage = cfg.LoseLineMessage;
    }

    private void CopyRaffleFrom(PluginConfig cfg)
    {
        RafflePrizeType = cfg.RafflePrizeType;
        RafflePrizeItemId = cfg.RafflePrizeItemId;
        RafflePrizeItemName = cfg.RafflePrizeItemName;
        RafflePrizeText = cfg.RafflePrizeText;
        RaffleEntryFee = cfg.RaffleEntryFee;
        RaffleBoost = cfg.RaffleBoost;
        RaffleVenueCutPercent = cfg.RaffleVenueCutPercent;
        RaffleFinishLine = cfg.RaffleFinishLine;
        RaffleCloseTimeEnabled = cfg.RaffleCloseTimeEnabled;
        RaffleCloseHour = cfg.RaffleCloseHour;
        RaffleCloseMinute = cfg.RaffleCloseMinute;
        RaffleAutoJoin = cfg.RaffleAutoJoin;
        RaffleJoinKeyword = cfg.RaffleJoinKeyword;
        RaffleJoinSoundEnabled = cfg.RaffleJoinSoundEnabled;
        RaffleJoinSoundEffectId = cfg.RaffleJoinSoundEffectId;
        RaffleAutoAnnounceRegistration = cfg.RaffleAutoAnnounceRegistration;
        RaffleAutoAnnounceWinner = cfg.RaffleAutoAnnounceWinner;
        RaffleAutoTellPaid = cfg.RaffleAutoTellPaid;
    }

    private void CopyRaffleChatFrom(PluginConfig cfg)
    {
        RaffleRegistrationMessage = cfg.RaffleRegistrationMessage;
        RaffleRegistrationNoKeywordMessage = cfg.RaffleRegistrationNoKeywordMessage;
        RaffleAdvertiseMessage = cfg.RaffleAdvertiseMessage;
        RaffleWinnerMessage = cfg.RaffleWinnerMessage;
        RaffleClosingTimeMessage = cfg.RaffleClosingTimeMessage;
        RaffleRequestFeeMessage = cfg.RaffleRequestFeeMessage;
        RaffleRunnerInviteMessage = cfg.RaffleRunnerInviteMessage;
    }

    private void ApplyRaceTo(PluginConfig cfg)
    {
        cfg.ChocoboCount = ChocoboCount;
        cfg.FinishLine = FinishLine;
        cfg.PayoutOdds = PayoutOdds;
        cfg.MaxBetPerChocobo = MaxBetPerChocobo;
        cfg.PerfectRace = PerfectRace;
        cfg.PerfectRaceOdds = PerfectRaceOdds;
        cfg.VenueCutPercent = VenueCutPercent;
        cfg.BetlistLayout = BetlistLayout;
        cfg.WebVenueName = WebVenueName;
        cfg.WebVenueImageUrl = WebVenueImageUrl;
    }

    private void ApplyChocoboNamesTo(PluginConfig cfg)
    {
        cfg.Chocobo1Name = Chocobo1Name;
        cfg.Chocobo2Name = Chocobo2Name;
        cfg.Chocobo3Name = Chocobo3Name;
        cfg.Chocobo4Name = Chocobo4Name;
        cfg.Chocobo5Name = Chocobo5Name;
        cfg.Chocobo6Name = Chocobo6Name;
        cfg.Chocobo7Name = Chocobo7Name;
        cfg.Chocobo8Name = Chocobo8Name;
        cfg.Chocobo9Name = Chocobo9Name;
        cfg.Chocobo10Name = Chocobo10Name;
    }

    private void ApplyClassicChatTo(PluginConfig cfg)
    {
        cfg.AnnounceRulesMessage = AnnounceRulesMessage;
        cfg.AdvertiseMessage = AdvertiseMessage;
        cfg.RaceStartedMessage = RaceStartedMessage;
        cfg.NoMoreBetsMessage = NoMoreBetsMessage;
        cfg.VoidRaceMessage = VoidRaceMessage;
        cfg.OpenBettingMessage = OpenBettingMessage;
        cfg.BetConfirmedMessage = BetConfirmedMessage;
        cfg.BetRemovedMessage = BetRemovedMessage;
        cfg.LastBetsMessage = LastBetsMessage;
        cfg.RaceWinnerMessage = RaceWinnerMessage;
        cfg.TellBankBalanceMessage = TellBankBalanceMessage;
        cfg.TellWebPinMessage = TellWebPinMessage;
        cfg.RandomLineMessage = RandomLineMessage;
        cfg.WinnerLineMessage = WinnerLineMessage;
        cfg.LoseLineMessage = LoseLineMessage;
    }

    private void ApplyRaffleTo(PluginConfig cfg)
    {
        cfg.RafflePrizeType = RafflePrizeType;
        cfg.RafflePrizeItemId = RafflePrizeItemId;
        cfg.RafflePrizeItemName = RafflePrizeItemName;
        cfg.RafflePrizeText = RafflePrizeText;
        cfg.RaffleEntryFee = RaffleEntryFee;
        cfg.RaffleBoost = RaffleBoost;
        cfg.RaffleVenueCutPercent = RaffleVenueCutPercent;
        cfg.RaffleFinishLine = RaffleFinishLine;
        cfg.RaffleCloseTimeEnabled = RaffleCloseTimeEnabled;
        cfg.RaffleCloseHour = RaffleCloseHour;
        cfg.RaffleCloseMinute = RaffleCloseMinute;
        cfg.RaffleAutoJoin = RaffleAutoJoin;
        cfg.RaffleJoinKeyword = RaffleJoinKeyword;
        cfg.RaffleJoinSoundEnabled = RaffleJoinSoundEnabled;
        cfg.RaffleJoinSoundEffectId = RaffleJoinSoundEffectId;
        cfg.RaffleAutoAnnounceRegistration = RaffleAutoAnnounceRegistration;
        cfg.RaffleAutoAnnounceWinner = RaffleAutoAnnounceWinner;
        cfg.RaffleAutoTellPaid = RaffleAutoTellPaid;
    }

    private void ApplyRaffleChatTo(PluginConfig cfg)
    {
        cfg.RaffleRegistrationMessage = RaffleRegistrationMessage;
        cfg.RaffleRegistrationNoKeywordMessage = RaffleRegistrationNoKeywordMessage;
        cfg.RaffleAdvertiseMessage = RaffleAdvertiseMessage;
        cfg.RaffleWinnerMessage = RaffleWinnerMessage;
        cfg.RaffleClosingTimeMessage = RaffleClosingTimeMessage;
        cfg.RaffleRequestFeeMessage = RaffleRequestFeeMessage;
        cfg.RaffleRunnerInviteMessage = RaffleRunnerInviteMessage;
    }
}
