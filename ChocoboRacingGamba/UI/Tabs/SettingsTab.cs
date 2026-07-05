using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using ECommons.ImGuiMethods;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using ChocoboRacing.Config;
using ChocoboRacing.Models;
using ChocoboRacing.State;
using ChocoboRacing.UI.Components;
using ChocoboRacing.Utility;

namespace ChocoboRacing.UI.Tabs;

/// <summary>
/// Draws the host Settings tab: preset management, race parameters, odds, chocobo renaming, and chat templates.
/// </summary>
public sealed class SettingsTab
{
    private readonly Plugin _plugin;
    private int _settingsChocoboCount;
    private int _settingsFinishLine;
    private float _settingsPayoutOdds;
    private int _settingsMaxBetPerChocobo;
    private bool _settingsPerfectRace;
    private float _settingsPerfectRaceOdds;
    private int _lastPresetIndexForSliders = -1;
    private string _renameBuffer = string.Empty;

    public SettingsTab(Plugin Plugin)
    {
        _plugin = Plugin;
    }

    public void Draw()
    {
        var state = _plugin.GameState;
        var config = _plugin.Configuration;

        if (_lastPresetIndexForSliders != config.ActiveSettingsPresetIndex)
        {
            _settingsChocoboCount = state.ChocoboCount;
            _settingsFinishLine = state.FinishLine;
            _settingsPayoutOdds = state.PayoutOdds;
            _settingsMaxBetPerChocobo = config.MaxBetPerChocobo;
            _settingsPerfectRace = config.PerfectRace;
            _settingsPerfectRaceOdds = config.PerfectRaceOdds;
            _lastPresetIndexForSliders = config.ActiveSettingsPresetIndex;
        }

        DrawPresetRow(state, config);

        using var tabBar = ImRaii.TabBar("SettingsTabs");
        if (!tabBar) return;

        using (var tab = ImRaii.TabItem("Race Settings"))
            if (tab) DrawRaceSettings(state);

        using (var tab = ImRaii.TabItem("Chat Settings"))
            if (tab) DrawChatSettings();

        using (var tab = ImRaii.TabItem("Web Betting"))
            if (tab) DrawWebBetting();
    }

    private void DrawWebBetting()
    {
        var cfg = _plugin.Configuration;
        var mirror = _plugin.WebMirror;

        ImGui.Spacing();
        UIHelper.SectionHeader("Web Spectator & Betting");
        ImGui.TextColored(UiColors.Subtle, "Mirror this race to the website so anyone can watch live, and let players bet online with a PIN.");
        ImGui.Spacing();

        var key = cfg.ApiHostKey;
        ImGui.Text("Host API Key:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(320);
        if (ImGui.InputText("##api_host_key", ref key, 128, ImGuiInputTextFlags.Password))
        {
            cfg.ApiHostKey = key.Trim();
            cfg.Save();
        }
        ImGui.SameLine();
        ImGui.TextColored(UiColors.Subtle, "(from your OOF Games host registration)");

        var live = mirror.SessionId != null;
        var nameCheck = VenueValidator.ValidateName(cfg.WebVenueName);
        var imageCheck = VenueValidator.ValidateImageUrl(cfg.WebVenueImageUrl);

        if (!live)
        {
            ImGui.Spacing();
            var venueName = cfg.WebVenueName;
            ImGui.Text("Venue Name:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(320);
            if (ImGui.InputText("##venue_name", ref venueName, 64))
            {
                cfg.WebVenueName = venueName;
                cfg.Save();
            }
            ImGui.SameLine();
            ImGui.TextColored(UiColors.Subtle, "(max 40 characters, shown on the website)");
            if (!nameCheck.Ok) ImGui.TextColored(UiColors.Warning, nameCheck.Error);

            var venueImage = cfg.WebVenueImageUrl;
            ImGui.Text("Venue Image URL:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(320);
            if (ImGui.InputText("##venue_image", ref venueImage, 512))
            {
                cfg.WebVenueImageUrl = venueImage.Trim();
                cfg.Save();
            }
            ImGui.SameLine();
            ImGui.TextColored(UiColors.Subtle, "(.jpg/.png/.webp, max 2048x2048, no Imgur)");
            if (!imageCheck.Ok) ImGui.TextColored(UiColors.Warning, imageCheck.Error);
        }

        ImGui.Spacing();
        if (!live)
        {
            var blocked = string.IsNullOrWhiteSpace(cfg.ApiHostKey) || !nameCheck.Ok || !imageCheck.Ok;
            using (ImRaii.Disabled(blocked))
            using (UIHelper.PushGreenButtonColours())
                if (UIHelper.IconTextButton(FontAwesomeIcon.Globe, "Go Live", "##go_live"))
                    mirror.GoLive();
        }
        else
        {
            using (UIHelper.PushRedButtonColours())
                if (UIHelper.IconTextButton(FontAwesomeIcon.Stop, "End Web Session", "##end_web"))
                    mirror.EndSession();
        }

        ImGui.SameLine();
        var statusColour = mirror.StatusIsError ? UiColors.Negative
            : mirror.Connected ? UiColors.Positive
            : live ? UiColors.Warning : UiColors.Muted;
        ImGui.TextColored(statusColour, $"Status: {mirror.Status}");

        if (live)
        {
            ImGui.Spacing();
            ImGui.Text("Session Code:");
            ImGui.SameLine();
            ImGui.TextColored(UiColors.Gold, mirror.SessionId);
            ImGui.SameLine();
            if (UIHelper.IconTextButton(FontAwesomeIcon.Copy, "Copy Link", "##copy_link"))
                ImGui.SetClipboardText(mirror.SpectatorUrl ?? string.Empty);
            ImGui.TextColored(UiColors.Subtle, mirror.SpectatorUrl ?? string.Empty);
            ImGui.TextWrapped("Share the link for spectators. Give each player their PIN from the Banks tab so they can bet online.");
        }
    }

    private void DrawPresetRow(RaceState state, PluginConfig config)
    {
        var presets = config.SettingsPresets;
        if (presets == null || presets.Count == 0)
            return;

        var presetLocked = state.Phase != RacePhase.Idle;
        if (presetLocked)
        {
            ImGui.TextColored(UiColors.Negative, "Switch or edit presets when the race is idle.");
            ImGui.BeginDisabled();
        }

        // Centre the row in the window
        var style = ImGui.GetStyle();
        ImGui.PushFont(UiBuilder.IconFont);
        var plusIconW  = ImGui.CalcTextSize(FontAwesomeIcon.Plus.ToIconString()).X;
        var penIconW   = ImGui.CalcTextSize(FontAwesomeIcon.Pen.ToIconString()).X;
        var trashIconW = ImGui.CalcTextSize(FontAwesomeIcon.Trash.ToIconString()).X;
        ImGui.PopFont();

        var sp         = style.ItemSpacing.X;
        var pad        = style.FramePadding.X * 2;
        var innerSp    = style.ItemInnerSpacing.X;
        var labelW     = ImGui.CalcTextSize("Preset").X;
        var addBtnW    = pad + plusIconW  + innerSp + ImGui.CalcTextSize("Add").X;
        var renameBtnW = pad + penIconW   + innerSp + ImGui.CalcTextSize("Rename").X;
        var deleteBtnW = pad + trashIconW + innerSp + ImGui.CalcTextSize("Delete").X;
        var totalW     = labelW + sp + 220f + sp + addBtnW + sp + renameBtnW + sp + deleteBtnW;
        ImGui.SetCursorPosX(Math.Max(style.WindowPadding.X, (ImGui.GetWindowWidth() - totalW) / 2f));

        ImGui.Text("Preset");
        ImGui.SameLine();

        var names = presets.Select(p => p.Name).ToArray();
        var comboIdx = config.ActiveSettingsPresetIndex;
        ImGui.SetNextItemWidth(220);
        if (ImGui.Combo("##SettingsPresetCombo", ref comboIdx, names, names.Length))
        {
            if (!config.TrySwitchActivePreset(comboIdx, state))
                comboIdx = config.ActiveSettingsPresetIndex;
        }

        ImGui.SameLine();
        using (UIHelper.PushGreenButtonColours())
            if (UIHelper.IconTextButton(FontAwesomeIcon.Plus, "Add", "##preset_add"))
                config.AddPresetCloneFromActive(state);

        ImGui.SameLine();
        if (UIHelper.IconTextButton(FontAwesomeIcon.Pen, "Rename", "##preset_rename"))
        {
            _renameBuffer = presets[config.ActiveSettingsPresetIndex].Name;
            ImGui.OpenPopup("Rename Preset##crg_rename_preset");
        }

        ImGui.SameLine();
        var canDelete = presets.Count > 1 && !presetLocked;
        using (ImRaii.Disabled(!canDelete))
        using (UIHelper.PushRedButtonColours())
        {
            if (UIHelper.IconTextButton(FontAwesomeIcon.Trash, "Delete", "##preset_del") && canDelete)
            {
                if (config.TryDeletePreset(config.ActiveSettingsPresetIndex, state))
                    _lastPresetIndexForSliders = -1;
            }
        }

        using var renamePopup = ImRaii.PopupModal("Rename Preset##crg_rename_preset", flags: ImGuiWindowFlags.AlwaysAutoResize);
        if (renamePopup)
        {
            ImGui.InputText("Name##rename_field", ref _renameBuffer, 64);
            if (UIHelper.IconTextButton(FontAwesomeIcon.Check, "OK", "##rename_ok") && _renameBuffer.Trim().Length > 0)
            {
                presets[config.ActiveSettingsPresetIndex].Name = _renameBuffer.Trim();
                config.Save();
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (UIHelper.IconTextButton(FontAwesomeIcon.Times, "Cancel", "##rename_cancel"))
                ImGui.CloseCurrentPopup();
        }

        if (presetLocked)
            ImGui.EndDisabled();

        ImGui.Spacing();
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

    private void DrawChocoboRenaming()
    {
        var config = _plugin.Configuration;
        var changedName = false;

        ImGui.Text("Rename Chocobos");

        bool DrawNameInput(int chocoboNumber, string label, string id, ref string name)
        {
            UIHelper.TextColoredForChocobo(chocoboNumber, label);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(200f);
            return ImGui.InputText(id, ref name, 20);
        }

        var c1 = config.Chocobo1Name; if (DrawNameInput(1, "Chocobo 1:   ", "##msg_c1", ref c1)) { config.Chocobo1Name = c1; changedName = true; }
        var c2 = config.Chocobo2Name; if (DrawNameInput(2, "Chocobo 2:   ", "##msg_c2", ref c2)) { config.Chocobo2Name = c2; changedName = true; }
        var c3 = config.Chocobo3Name; if (DrawNameInput(3, "Chocobo 3:   ", "##msg_c3", ref c3)) { config.Chocobo3Name = c3; changedName = true; }
        var c4 = config.Chocobo4Name; if (DrawNameInput(4, "Chocobo 4:   ", "##msg_c4", ref c4)) { config.Chocobo4Name = c4; changedName = true; }
        var c5 = config.Chocobo5Name; if (DrawNameInput(5, "Chocobo 5:   ", "##msg_c5", ref c5)) { config.Chocobo5Name = c5; changedName = true; }
        var c6 = config.Chocobo6Name; if (DrawNameInput(6, "Chocobo 6:   ", "##msg_c6", ref c6)) { config.Chocobo6Name = c6; changedName = true; }
        var c7 = config.Chocobo7Name; if (DrawNameInput(7, "Chocobo 7:   ", "##msg_c7", ref c7)) { config.Chocobo7Name = c7; changedName = true; }
        var c8 = config.Chocobo8Name; if (DrawNameInput(8, "Chocobo 8:   ", "##msg_c8", ref c8)) { config.Chocobo8Name = c8; changedName = true; }
        var c9 = config.Chocobo9Name; if (DrawNameInput(9, "Chocobo 9:   ", "##msg_c9", ref c9)) { config.Chocobo9Name = c9; changedName = true; }
        var c10 = config.Chocobo10Name; if (DrawNameInput(10, "Chocobo 10:", "##msg_c10", ref c10)) { config.Chocobo10Name = c10; changedName = true; }

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
