using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ChocoboRacing.Config;
using ChocoboRacing.Models;
using ChocoboRacing.State;
using ChocoboRacing.UI.Components;
using Dalamud.Bindings.ImGui;
using ECommons.ImGuiMethods;

/// <summary>
/// Draws the host Settings sections: race parameters, odds, chocobo renaming, and the classic and Grand National chat templates.
/// </summary>
namespace ChocoboRacing.UI.Tabs;

public sealed class SettingsTab
{
    private readonly Plugin _plugin;
    private readonly PresetSelector _presetSelector;
    private int _settingsChocoboCount;
    private int _settingsFinishLine;
    private float _settingsPayoutOdds;
    private int _settingsMaxBetPerChocobo;
    private bool _settingsPerfectRace;
    private float _settingsPerfectRaceOdds;
    private int _lastPresetRevisionForSliders = -1;

    public SettingsTab(Plugin Plugin, PresetSelector presetSelector)
    {
        _plugin = Plugin;
        _presetSelector = presetSelector;
    }

    public void DrawClassicRaceSection()
    {
        _presetSelector.Draw(_plugin.GameState, _plugin.Configuration);
        SyncSlidersFromActivePreset();
        DrawRaceSettings(_plugin.GameState);
    }

    public void DrawClassicChatSection()
    {
        _presetSelector.Draw(_plugin.GameState, _plugin.Configuration);
        DrawChatSettings();
    }

    public void DrawGrandNationalChatSection()
    {
        _presetSelector.Draw(_plugin.GameState, _plugin.Configuration);
        DrawGrandNationalChatSettings();
    }

    private void SyncSlidersFromActivePreset()
    {
        var state = _plugin.GameState;
        var config = _plugin.Configuration;

        if (_lastPresetRevisionForSliders == _presetSelector.Revision)
            return;

        _settingsChocoboCount = state.ChocoboCount;
        _settingsFinishLine = state.FinishLine;
        _settingsPayoutOdds = state.PayoutOdds;
        _settingsMaxBetPerChocobo = config.MaxBetPerChocobo;
        _settingsPerfectRace = config.PerfectRace;
        _settingsPerfectRaceOdds = config.PerfectRaceOdds;
        _lastPresetRevisionForSliders = _presetSelector.Revision;
    }

    private void DrawGrandNationalChatSettings()
    {
        ImGui.Spacing();
        ImGui.TextColored(UiColors.Gold, "Grand National Messages");
        ImGui.TextColored(UiColors.Subtle, "Placeholders (any message): {prize}, {entryfee}, {keyword}, {runners}, {boostedpot}, {closetime}, {timeleft}, {name}, {number}, {url}");
        ImGui.TextColored(UiColors.Subtle, "{name}/{number} are the winner in the winner message and the runner in /tells; blank in broadcasts.");
        ImGui.TextColored(UiColors.Subtle, "{closetime} is HH:MM Server Time and {timeleft} the time remaining; both show TBA when no closing time is set.");
        ImGui.Spacing();

        var config = _plugin.Configuration;
        var changed = false;

        ImGui.Text("Registration Open (with keyword):");
        var reg = config.GrandNationalRegistrationMessage;
        ImGui.SetNextItemWidth(-1f);
        if (ImGui.InputText("##gnmsg_reg", ref reg, 512)) { config.GrandNationalRegistrationMessage = reg; changed = true; }
        ImGui.Spacing();

        ImGui.Text("Registration Open (no keyword):");
        ImGui.TextColored(UiColors.Subtle, "Used when the chat join keyword is off.");
        var regNoKw = config.GrandNationalRegistrationNoKeywordMessage;
        ImGui.SetNextItemWidth(-1f);
        if (ImGui.InputText("##gnmsg_reg_nokw", ref regNoKw, 512)) { config.GrandNationalRegistrationNoKeywordMessage = regNoKw; changed = true; }
        ImGui.Spacing();

        ImGui.Text("Closing Time (announce):");
        ImGui.TextColored(UiColors.Subtle, "Sent by the Announce Closing button while registration is open.");
        var closing = config.GrandNationalClosingTimeMessage;
        ImGui.SetNextItemWidth(-1f);
        if (ImGui.InputText("##gnmsg_closing", ref closing, 512)) { config.GrandNationalClosingTimeMessage = closing; changed = true; }
        ImGui.Spacing();

        ImGui.Text("Winner (announce):");
        var win = config.GrandNationalWinnerMessage;
        ImGui.SetNextItemWidth(-1f);
        if (ImGui.InputText("##gnmsg_win", ref win, 512)) { config.GrandNationalWinnerMessage = win; changed = true; }
        ImGui.Spacing();

        ImGui.Text("Request Entry Fee:");
        ImGui.TextColored(UiColors.Subtle, "Sent as a /tell to the runner.");
        var fee = config.GrandNationalRequestFeeMessage;
        ImGui.SetNextItemWidth(-1f);
        if (ImGui.InputText("##gnmsg_fee", ref fee, 256)) { config.GrandNationalRequestFeeMessage = fee; changed = true; }
        ImGui.Spacing();

        ImGui.Text("Runner Invite (race link):");
        ImGui.TextColored(UiColors.Subtle, "Sent as a /tell to the runner.");
        var invite = config.GrandNationalRunnerInviteMessage;
        ImGui.SetNextItemWidth(-1f);
        if (ImGui.InputText("##gnmsg_invite", ref invite, 512)) { config.GrandNationalRunnerInviteMessage = invite; changed = true; }

        if (changed) config.Save();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        DrawGrandNationalAutoMessages(config);
    }

    private void DrawGrandNationalAutoMessages(PluginConfig config)
    {
        ImGui.TextColored(UiColors.Gold, "Auto Messages");
        ImGui.TextColored(UiColors.Subtle, "Send these automatically when the action happens, instead of announcing manually.");
        ImGui.Spacing();

        var autoChanged = false;

        var autoReg = config.GrandNationalAutoAnnounceRegistration;
        if (ImGui.Checkbox("Auto-announce registration open##gn_auto_reg", ref autoReg)) { config.GrandNationalAutoAnnounceRegistration = autoReg; autoChanged = true; }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("When off, use the Announce button in the Grand National tab to send it manually.");

        var autoWin = config.GrandNationalAutoAnnounceWinner;
        if (ImGui.Checkbox("Auto-announce winner##gn_auto_win", ref autoWin)) { config.GrandNationalAutoAnnounceWinner = autoWin; autoChanged = true; }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("When off, use the Announce Winner button in the Grand National tab to send it manually.");

        var autoTellPaid = config.GrandNationalAutoTellPaid;
        if (ImGui.Checkbox("Auto-tell payment confirmation##gn_auto_tell_paid", ref autoTellPaid)) { config.GrandNationalAutoTellPaid = autoTellPaid; autoChanged = true; }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Sends the Runner Invite message as a /tell when a runner is marked paid (manually via Mark Paid, or automatically when a trade covers the full entry fee). When off, use the Send URL button to tell them manually.");

        if (autoChanged) config.Save();
    }

    private void DrawRaceSettings(RaceState state)
    {
        var isLocked = state.Phase != RacePhase.Idle;

        if (isLocked)
        {
            ImGui.TextColored(UiColors.Negative, "Settings are locked while a race is active.");
            ImGui.Spacing();
            ImGui.BeginDisabled();
        }

        ImGui.Text("Number of Chocobos:");
        ImGui.SetNextItemWidth(200);
        if (ImGui.SliderInt("##ChocoboCount", ref _settingsChocoboCount, RaceState.MinChocobos, RaceState.MaxChocobos))
            state.UpdateSettings(_settingsChocoboCount, _settingsFinishLine, _settingsPayoutOdds);

        ImGui.Spacing();
        ImGui.Text("Finish Line Distance:");
        ImGui.SetNextItemWidth(200);
        if (ImGui.SliderInt("##FinishLine", ref _settingsFinishLine, RaceState.MinFinishLine, RaceState.MaxFinishLine))
            state.UpdateSettings(_settingsChocoboCount, _settingsFinishLine, _settingsPayoutOdds);

        ImGui.SameLine();
        ImGui.TextColored(UiColors.Subtle, _settingsFinishLine <= 7 ? "(Fast)" : _settingsFinishLine <= 11 ? "(Normal)" : "(Long)");

        DrawMaxBetSettings();
        DrawOddsControls(state);
        DrawPerfectRaceSettings(state);
        DrawChocoboRenaming();

        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();

        var profitMargin = ((double)_settingsChocoboCount - _settingsPayoutOdds) / _settingsChocoboCount * 100.0;
        ImGui.TextColored(UiColors.Positive, $"Profit Margin: {profitMargin:F1}%");
        ImGui.TextColored(UiColors.Info, $"Expected Value: A 1,000 bet pays {1000 * _settingsPayoutOdds:N0} on a win.");

        if (isLocked) ImGui.EndDisabled();
    }

    private void DrawOddsControls(RaceState state)
    {
        ImGui.Spacing();
        ImGui.Text("Payout Odds Multiplier:");
        if (ImGui.Button("-##odds_minus"))
        {
            _settingsPayoutOdds -= 0.25f;
            if (_settingsPayoutOdds < RaceState.MinPayoutOdds) _settingsPayoutOdds = RaceState.MinPayoutOdds;
            state.UpdateSettings(_settingsChocoboCount, _settingsFinishLine, _settingsPayoutOdds);
        }
        ImGui.SameLine();
        ImGui.SetNextItemWidth(150);
        if (ImGui.InputFloat("##PayoutOdds", ref _settingsPayoutOdds, 0.0f, 0.0f, "%.2f"))
        {
            _settingsPayoutOdds = Math.Clamp(_settingsPayoutOdds, RaceState.MinPayoutOdds, RaceState.MaxPayoutOdds);
            state.UpdateSettings(_settingsChocoboCount, _settingsFinishLine, _settingsPayoutOdds);
        }
        ImGui.SameLine();
        if (ImGui.Button("+##odds_plus"))
        {
            _settingsPayoutOdds += 0.25f;
            if (_settingsPayoutOdds > RaceState.MaxPayoutOdds) _settingsPayoutOdds = RaceState.MaxPayoutOdds;
            state.UpdateSettings(_settingsChocoboCount, _settingsFinishLine, _settingsPayoutOdds);
        }
        ImGui.SameLine();
        ImGui.TextColored(UiColors.Subtle, $"({_settingsPayoutOdds:F2}x payout)");
        ImGui.Spacing();
    }

    private void DrawMaxBetSettings()
    {
        var config = _plugin.Configuration;

        ImGui.Spacing();
        ImGui.Text("Max Bet per Chocobo:");
        if (ImGuiEx.InputFancyNumeric(200, "##MaxBetPerChocobo", ref _settingsMaxBetPerChocobo, 10_000))
        {
            if (_settingsMaxBetPerChocobo < 1) _settingsMaxBetPerChocobo = 1;
            config.MaxBetPerChocobo = _settingsMaxBetPerChocobo;
            config.Save();
        }
    }

    private void DrawPerfectRaceSettings(RaceState state)
    {
        var config = _plugin.Configuration;
        ImGui.Spacing();
        if (ImGui.Checkbox("Perfect Race##perfect_race", ref _settingsPerfectRace))
        {
            config.PerfectRace = _settingsPerfectRace;
            config.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(
                "Only the winning chocobo may advance: every roll was their number and no other chocobo moved. " +
                "When Perfect Race is enabled in settings, that clean win pays perfect race odds instead of standard odds.");

        if (_settingsPerfectRace)
        {
            ImGui.Indent(28f);
            ImGui.Text("Perfect Race Odds:");
            if (ImGui.Button("-##pr_odds_minus"))
            {
                _settingsPerfectRaceOdds -= 0.25f;
                if (_settingsPerfectRaceOdds < RaceState.MinPayoutOdds) _settingsPerfectRaceOdds = RaceState.MinPayoutOdds;
                config.PerfectRaceOdds = _settingsPerfectRaceOdds;
                config.Save();
            }
            ImGui.SameLine();
            ImGui.SetNextItemWidth(150);
            if (ImGui.InputFloat("##PerfectRaceOdds", ref _settingsPerfectRaceOdds, 0.0f, 0.0f, "%.2f"))
            {
                _settingsPerfectRaceOdds = Math.Clamp(_settingsPerfectRaceOdds, RaceState.MinPayoutOdds, RaceState.MaxPerfectRaceOdds);
                config.PerfectRaceOdds = _settingsPerfectRaceOdds;
                config.Save();
            }
            ImGui.SameLine();
            if (ImGui.Button("+##pr_odds_plus"))
            {
                _settingsPerfectRaceOdds += 0.25f;
                if (_settingsPerfectRaceOdds > RaceState.MaxPerfectRaceOdds) _settingsPerfectRaceOdds = RaceState.MaxPerfectRaceOdds;
                config.PerfectRaceOdds = _settingsPerfectRaceOdds;
                config.Save();
            }
            ImGui.SameLine();
            ImGui.TextColored(UiColors.Subtle, $"({_settingsPerfectRaceOdds:F2}x payout)");
            ImGui.Unindent(28f);
        }

        ImGui.Spacing();
    }

    private static string GetConfigChocoboName(PluginConfig config, int number) => number switch
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
        _ => string.Empty
    };

    private Dictionary<int, string> BuildNameSuggestions(PluginConfig config)
    {
        var result = new Dictionary<int, string>();

        var candidates = new List<string>();
        void AddCandidate(string? fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return;
            var firstName = fullName.Split(' ')[0];
            if (string.IsNullOrWhiteSpace(firstName)) return;
            if (!candidates.Any(c => c.Equals(firstName, StringComparison.OrdinalIgnoreCase)))
                candidates.Add(firstName);
        }

        var localPlayer = _plugin.PartyManager.GetLocalPlayer();
        if (localPlayer.HasValue) AddCandidate(localPlayer.Value.Name);
        foreach (var member in _plugin.PartyManager.GetPartyMembers()) AddCandidate(member.Name);

        if (candidates.Count == 0) return result;

        var currentNames = new string[_settingsChocoboCount];
        for (var i = 1; i <= _settingsChocoboCount; i++)
            currentNames[i - 1] = GetConfigChocoboName(config, i);

        var unassigned = new Queue<string>(candidates.Where(c =>
            !currentNames.Any(n => n.Equals(c, StringComparison.OrdinalIgnoreCase))));

        for (var i = 1; i <= _settingsChocoboCount && unassigned.Count > 0; i++)
        {
            var alreadyMatched = candidates.Any(c => c.Equals(currentNames[i - 1], StringComparison.OrdinalIgnoreCase));
            if (alreadyMatched) continue;

            result[i] = unassigned.Dequeue();
        }

        return result;
    }

    private void DrawChocoboRenaming()
    {
        var config = _plugin.Configuration;
        var changedName = false;

        ImGui.Text("Rename Chocobos");

        var suggestions = BuildNameSuggestions(config);

        bool DrawNameInput(int chocoboNumber, string label, string id, ref string name)
        {
            UIHelper.TextColoredForChocobo(chocoboNumber, label);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(200f);
            var changed = ImGui.InputText(id, ref name, 20);

            if (suggestions.TryGetValue(chocoboNumber, out var suggestion))
            {
                ImGui.SameLine();
                if (ImGui.SmallButton($"Use: {suggestion}##sugg_c{chocoboNumber}"))
                {
                    name = suggestion;
                    changed = true;
                }
            }

            return changed;
        }

        if (_settingsChocoboCount >= 1) { var c1 = config.Chocobo1Name; if (DrawNameInput(1, "Chocobo 1:   ", "##msg_c1", ref c1)) { config.Chocobo1Name = c1; changedName = true; } }
        if (_settingsChocoboCount >= 2) { var c2 = config.Chocobo2Name; if (DrawNameInput(2, "Chocobo 2:   ", "##msg_c2", ref c2)) { config.Chocobo2Name = c2; changedName = true; } }
        if (_settingsChocoboCount >= 3) { var c3 = config.Chocobo3Name; if (DrawNameInput(3, "Chocobo 3:   ", "##msg_c3", ref c3)) { config.Chocobo3Name = c3; changedName = true; } }
        if (_settingsChocoboCount >= 4) { var c4 = config.Chocobo4Name; if (DrawNameInput(4, "Chocobo 4:   ", "##msg_c4", ref c4)) { config.Chocobo4Name = c4; changedName = true; } }
        if (_settingsChocoboCount >= 5) { var c5 = config.Chocobo5Name; if (DrawNameInput(5, "Chocobo 5:   ", "##msg_c5", ref c5)) { config.Chocobo5Name = c5; changedName = true; } }
        if (_settingsChocoboCount >= 6) { var c6 = config.Chocobo6Name; if (DrawNameInput(6, "Chocobo 6:   ", "##msg_c6", ref c6)) { config.Chocobo6Name = c6; changedName = true; } }
        if (_settingsChocoboCount >= 7) { var c7 = config.Chocobo7Name; if (DrawNameInput(7, "Chocobo 7:   ", "##msg_c7", ref c7)) { config.Chocobo7Name = c7; changedName = true; } }
        if (_settingsChocoboCount >= 8) { var c8 = config.Chocobo8Name; if (DrawNameInput(8, "Chocobo 8:   ", "##msg_c8", ref c8)) { config.Chocobo8Name = c8; changedName = true; } }
        if (_settingsChocoboCount >= 9) { var c9 = config.Chocobo9Name; if (DrawNameInput(9, "Chocobo 9:   ", "##msg_c9", ref c9)) { config.Chocobo9Name = c9; changedName = true; } }
        if (_settingsChocoboCount >= 10) { var c10 = config.Chocobo10Name; if (DrawNameInput(10, "Chocobo 10:", "##msg_c10", ref c10)) { config.Chocobo10Name = c10; changedName = true; } }

        if (changedName) config.Save();
    }

    private void DrawChatSettings()
    {
        ImGui.Spacing();
        ImGui.TextColored(UiColors.Gold, "Custom Messages (Limit 15 lines)");
        ImGui.TextColored(UiColors.Subtle, "Placeholders: {chocobos}, {chocobonames}, {distance}, {odds}, {perfectodds}, {betlist}, {racelist}, {winningchocobo}, {winnerlist}, {bankvalue}, {pin}, {url}");
        ImGui.Spacing();

        DrawBetlistLayoutSetting();
        ImGui.Spacing();

        var config = _plugin.Configuration;
        var changed = false;

        ImGui.Text("Announce Rules:");
        var announce = config.AnnounceRulesMessage;
        if (DrawExpandingInput("##msg_rules", ref announce)) { config.AnnounceRulesMessage = announce; changed = true; }

        ImGui.Text("Open Betting:");
        var open = config.OpenBettingMessage;
        if (DrawExpandingInput("##msg_open", ref open)) { config.OpenBettingMessage = open; changed = true; }

        ImGui.Text("Last Bets:");
        var lastBets = config.LastBetsMessage;
        if (DrawExpandingInput("##msg_lastbets", ref lastBets)) { config.LastBetsMessage = lastBets; changed = true; }

        ImGui.Text("No More Bets:");
        var closed = config.NoMoreBetsMessage;
        if (DrawExpandingInput("##msg_closed", ref closed)) { config.NoMoreBetsMessage = closed; changed = true; }

        ImGui.Text("Race Started:");
        var started = config.RaceStartedMessage;
        if (DrawExpandingInput("##msg_started", ref started)) { config.RaceStartedMessage = started; changed = true; }

        ImGui.Text("Void Race:");
        var voided = config.VoidRaceMessage;
        if (DrawExpandingInput("##msg_void", ref voided)) { config.VoidRaceMessage = voided; changed = true; }

        ImGui.Text("Race Winner:");
        var winner = config.RaceWinnerMessage;
        if (DrawExpandingInput("##msg_winner", ref winner)) { config.RaceWinnerMessage = winner; changed = true; }

        ImGui.Text("Tell Bank Balance:");
        var tellBal = config.TellBankBalanceMessage;
        ImGui.SetNextItemWidth(-1f);
        if (ImGui.InputText("##msg_tell_bal", ref tellBal, 256)) { config.TellBankBalanceMessage = tellBal; changed = true; }

        ImGui.Text("Tell Web PIN:");
        var tellPin = config.TellWebPinMessage;
        ImGui.SetNextItemWidth(-1f);
        if (ImGui.InputText("##msg_tell_pin", ref tellPin, 256)) { config.TellWebPinMessage = tellPin; changed = true; }

        ImGui.Text("Roll Emote:");
        var rnd = config.RandomLineMessage;
        ImGui.SetNextItemWidth(200f);
        if (ImGui.InputText("##msg_rnd", ref rnd, 32)) { config.RandomLineMessage = rnd; changed = true; }

        ImGui.Text("Winner Emote:");
        var winnerEmote = config.WinnerLineMessage;
        ImGui.SetNextItemWidth(200f);
        if (ImGui.InputText("##msg_winner_emote", ref winnerEmote, 32)) { config.WinnerLineMessage = winnerEmote; changed = true; }

        ImGui.Text("Loser Emote:");
        var loseEmote = config.LoseLineMessage;
        ImGui.SetNextItemWidth(200f);
        if (ImGui.InputText("##msg_lose_emote", ref loseEmote, 32)) { config.LoseLineMessage = loseEmote; changed = true; }

        if (changed) config.Save();
    }

    private void DrawBetlistLayoutSetting()
    {
        var config = _plugin.Configuration;
        ImGui.Text("Betlist Layout:");
        ImGui.SameLine();

        var isByChocobo = config.BetlistLayout == BetlistLayout.SplitByChocobo;
        if (ImGui.RadioButton("Split by Chocobo##betlist_chocobo", isByChocobo))
        {
            config.BetlistLayout = BetlistLayout.SplitByChocobo;
            config.Save();
        }
        ImGui.SameLine();
        if (ImGui.RadioButton("Split by Player##betlist_player", !isByChocobo))
        {
            config.BetlistLayout = BetlistLayout.SplitByPlayer;
            config.Save();
        }
    }

    private bool DrawExpandingInput(string id, ref string text)
    {
        var lineCount = Math.Max(3, text.Split('\n').Length + 1);
        var height = ImGui.GetTextLineHeight() * lineCount + ImGui.GetStyle().FramePadding.Y * 2;
        height = Math.Min(height, 250f);
        return ImGui.InputTextMultiline(id, ref text, 2048, new Vector2(-1, height));
    }
}
