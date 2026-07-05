using System;
using ChocoboRacing.Config;

namespace ChocoboRacing.Models;

/// <summary>
/// Named host settings bundle: race parameters, chocobo display names, and chat templates.
/// </summary>
[Serializable]
public sealed class SettingsPreset
{
    public const string DefaultName = "Default";

    public string Name { get; set; } = DefaultName;

    public int ChocoboCount { get; set; } = 5;
    public int FinishLine { get; set; } = 5;
    public float PayoutOdds { get; set; } = 4.0f;

    public int MaxBetPerChocobo { get; set; } = 1_000_000;
    public bool PerfectRace { get; set; }
    public float PerfectRaceOdds { get; set; } = 20f;

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
    public string LastBetsMessage { get; set; } = "Last Bets! <se.2>";
    public string TellBankBalanceMessage { get; set; } = "You have {bankvalue} Gil in the bank.";
    public string TellWebPinMessage { get; set; } = "Your Chocobo Racing PIN is {pin}. Place bets here: {url}";
    public string RandomLineMessage { get; set; } = "/cheer";
    public string WinnerLineMessage { get; set; } = "/congratulate";
    public string LoseLineMessage { get; set; } = "/sad";
    public BetlistLayout BetlistLayout { get; set; } = BetlistLayout.SplitByChocobo;

    public static SettingsPreset FromLiveConfig(PluginConfig cfg)
    {
        var p = new SettingsPreset { Name = DefaultName };
        p.CopyRaceAndChatFrom(cfg);
        return p;
    }

    public void CopyRaceAndChatFrom(PluginConfig cfg)
    {
        ChocoboCount = cfg.ChocoboCount;
        FinishLine = cfg.FinishLine;
        PayoutOdds = cfg.PayoutOdds;
        MaxBetPerChocobo = cfg.MaxBetPerChocobo;
        PerfectRace = cfg.PerfectRace;
        PerfectRaceOdds = cfg.PerfectRaceOdds;
        WebVenueName = cfg.WebVenueName;
        WebVenueImageUrl = cfg.WebVenueImageUrl;
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
        AnnounceRulesMessage = cfg.AnnounceRulesMessage;
        RaceStartedMessage = cfg.RaceStartedMessage;
        NoMoreBetsMessage = cfg.NoMoreBetsMessage;
        VoidRaceMessage = cfg.VoidRaceMessage;
        OpenBettingMessage = cfg.OpenBettingMessage;
        LastBetsMessage = cfg.LastBetsMessage;
        TellBankBalanceMessage = cfg.TellBankBalanceMessage;
        TellWebPinMessage = cfg.TellWebPinMessage;
        RaceWinnerMessage = cfg.RaceWinnerMessage;
        RandomLineMessage = cfg.RandomLineMessage;
        WinnerLineMessage = cfg.WinnerLineMessage;
        LoseLineMessage = cfg.LoseLineMessage;
        BetlistLayout = cfg.BetlistLayout;
    }

    public void ApplyRaceAndChatTo(PluginConfig cfg)
    {
        cfg.ChocoboCount = ChocoboCount;
        cfg.FinishLine = FinishLine;
        cfg.PayoutOdds = PayoutOdds;
        cfg.MaxBetPerChocobo = MaxBetPerChocobo;
        cfg.PerfectRace = PerfectRace;
        cfg.PerfectRaceOdds = PerfectRaceOdds;
        cfg.WebVenueName = WebVenueName;
        cfg.WebVenueImageUrl = WebVenueImageUrl;
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
        cfg.AnnounceRulesMessage = AnnounceRulesMessage;
        cfg.RaceStartedMessage = RaceStartedMessage;
        cfg.NoMoreBetsMessage = NoMoreBetsMessage;
        cfg.VoidRaceMessage = VoidRaceMessage;
        cfg.OpenBettingMessage = OpenBettingMessage;
        cfg.LastBetsMessage = LastBetsMessage;
        cfg.TellBankBalanceMessage = TellBankBalanceMessage;
        cfg.TellWebPinMessage = TellWebPinMessage;
        cfg.RaceWinnerMessage = RaceWinnerMessage;
        cfg.RandomLineMessage = RandomLineMessage;
        cfg.WinnerLineMessage = WinnerLineMessage;
        cfg.LoseLineMessage = LoseLineMessage;
        cfg.BetlistLayout = BetlistLayout;
    }

    public SettingsPreset DeepCopy()
    {
        var c = new SettingsPreset
        {
            Name = Name,
            ChocoboCount = ChocoboCount,
            FinishLine = FinishLine,
            PayoutOdds = PayoutOdds,
            MaxBetPerChocobo = MaxBetPerChocobo,
            PerfectRace = PerfectRace,
            PerfectRaceOdds = PerfectRaceOdds,
            WebVenueName = WebVenueName,
            WebVenueImageUrl = WebVenueImageUrl,
            Chocobo1Name = Chocobo1Name,
            Chocobo2Name = Chocobo2Name,
            Chocobo3Name = Chocobo3Name,
            Chocobo4Name = Chocobo4Name,
            Chocobo5Name = Chocobo5Name,
            Chocobo6Name = Chocobo6Name,
            Chocobo7Name = Chocobo7Name,
            Chocobo8Name = Chocobo8Name,
            Chocobo9Name = Chocobo9Name,
            Chocobo10Name = Chocobo10Name,
            AnnounceRulesMessage = AnnounceRulesMessage,
            RaceStartedMessage = RaceStartedMessage,
            NoMoreBetsMessage = NoMoreBetsMessage,
            VoidRaceMessage = VoidRaceMessage,
            OpenBettingMessage = OpenBettingMessage,
            LastBetsMessage = LastBetsMessage,
            TellBankBalanceMessage = TellBankBalanceMessage,
            TellWebPinMessage = TellWebPinMessage,
            RaceWinnerMessage = RaceWinnerMessage,
            RandomLineMessage = RandomLineMessage,
            WinnerLineMessage = WinnerLineMessage,
            LoseLineMessage = LoseLineMessage,
            BetlistLayout = BetlistLayout
        };
        return c;
    }
}
