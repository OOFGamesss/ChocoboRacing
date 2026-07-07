using System;
using System.Numerics;
using ChocoboRacing.Config;
using ChocoboRacing.Models;
using ChocoboRacing.Services;
using ChocoboRacing.State;
using ChocoboRacing.UI.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using ECommons.ImGuiMethods;

/// <summary>
/// Draws the host Grand National tab: setup, registration, grid, live race, and winner/payout flow across each phase.
/// </summary>
namespace ChocoboRacing.UI.Tabs;

public sealed class GrandNationalTab
{
    private readonly Plugin _plugin;
    private string _nearbyFilter = string.Empty;
    private string _keywordInput;
    private string _prizeTextInput;
    private long _lastActionMs;
    private long _winnerPayoutRemaining;

    private GrandNationalService Service => _plugin.GrandNationalManager;
    private GrandNationalState State => _plugin.GrandNationalState;
    private PluginConfig Config => _plugin.Configuration;

    public GrandNationalTab(Plugin plugin)
    {
        _plugin = plugin;
        _keywordInput = _plugin.Configuration.GrandNationalJoinKeyword;
        _prizeTextInput = _plugin.Configuration.GrandNationalPrizeText;
    }

    public void Draw()
    {
        DrawLiveHint();
        switch (State.Phase)
        {
            case GrandNationalPhase.Idle: DrawSetup(); break;
            case GrandNationalPhase.Registration: DrawRegistration(); break;
            case GrandNationalPhase.Closed: DrawClosed(); break;
            case GrandNationalPhase.Racing: DrawRacing(); break;
            case GrandNationalPhase.Finished: DrawFinished(); break;
        }
    }

    private void DrawLiveHint()
    {
        if (Service.IsLive) return;
        ImGui.TextColored(UiColors.Warning, "⚠  Go Live on the Web Betting tab to run a Grand National.");
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
    }

    private void DrawSetup()
    {
        UIHelper.SectionHeader("Grand National");
        ImGui.TextWrapped("Runners register onto a list, each becomes a numbered chocobo, and the server draws the winner when you start. The winning runner takes the prize.");
        ImGui.Spacing();
        DrawSettingsEditors();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        using (UIHelper.PushGreenButtonColours())
            if (UIHelper.IconTextButton(FontAwesomeIcon.DoorOpen, "Open Registration", "##gn_open"))
            {
                State.OpenRegistration();
                Service.AnnounceRegistrationOpen();
            }
        ImGui.SameLine();
        using (ImRaii.Disabled(!Service.IsLive))
        using (UIHelper.PushRedButtonColours())
        {
            var clicked = UIHelper.IconTextButton(FontAwesomeIcon.Stop, "End Session", "##gn_endsession_setup");
            if (UIHelper.CtrlClickConfirmed(clicked, "Hold Ctrl and click to end the web session.") && UIHelper.TryDebounce(ref _lastActionMs))
                _plugin.WebMirror.EndSession();
        }

        if (State.HasLastRoster)
        {
            ImGui.Spacing();
            using (UIHelper.PushBlueButtonColours())
                if (UIHelper.IconTextButton(FontAwesomeIcon.UserFriends, "Open with Last Runners", "##gn_open_last"))
                {
                    State.OpenRegistration(true);
                    Service.AnnounceRegistrationOpen();
                }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Open registration and re-add last race's runners, all marked unpaid.");
        }
    }

    private void DrawSettingsEditors()
    {
        DrawPrizeEditors();

        var fee = (int)Math.Clamp(Config.GrandNationalEntryFee, 0, int.MaxValue);
        if (ImGuiEx.InputFancyNumeric(200f, "Entry fee (0 = free)", ref fee, 0))
        {
            Config.GrandNationalEntryFee = Math.Max(0, fee);
            Config.Save();
        }

        if (Config.GrandNationalPrizeType == GrandNationalPrizeType.Pot)
        {
            var cut = Config.GrandNationalVenueCutPercent;
            ImGui.SetNextItemWidth(200f);
            if (ImGui.SliderFloat("##gn_cut", ref cut, 0f, 100f, "%.0f%%"))
            {
                Config.GrandNationalVenueCutPercent = cut;
                Config.Save();
            }
            ImGui.SameLine();
            ImGui.Text("Venue cut %");
        }

        var finish = Config.GrandNationalFinishLine;
        if (ImGuiEx.InputFancyNumeric(200f, "Finish line (yalms)", ref finish, 0))
        {
            Config.GrandNationalFinishLine = Math.Clamp(finish, 5, 100);
            Config.Save();
        }

        var autoJoin = Config.GrandNationalAutoJoin;
        if (ImGui.Checkbox("Chat join keyword##gn_autojoin", ref autoJoin))
        {
            Config.GrandNationalAutoJoin = autoJoin;
            Config.Save();
        }
        if (autoJoin)
        {
            ImGui.Indent();
            ImGui.SetNextItemWidth(120f);
            if (ImGui.InputText("Keyword##gn_keyword", ref _keywordInput, 32))
            {
                Config.GrandNationalJoinKeyword = _keywordInput.Trim();
                Config.Save();
            }
            ImGui.Unindent();
        }
    }

    private void DrawPrizeEditors()
    {
        var prize = Config.GrandNationalPrizeType;
        ImGui.TextColored(UiColors.Muted, "Prize");
        ImGui.SameLine();
        if (ImGui.RadioButton("Pot##gn_prize_pot", prize == GrandNationalPrizeType.Pot)) SetPrizeType(GrandNationalPrizeType.Pot);
        ImGui.SameLine();
        if (ImGui.RadioButton("Item##gn_prize_item", prize == GrandNationalPrizeType.Item)) SetPrizeType(GrandNationalPrizeType.Item);
        ImGui.SameLine();
        if (ImGui.RadioButton("Custom##gn_prize_custom", prize == GrandNationalPrizeType.Custom)) SetPrizeType(GrandNationalPrizeType.Custom);

        if (prize == GrandNationalPrizeType.Item)
        {
            var id = Config.GrandNationalPrizeItemId;
            var name = Config.GrandNationalPrizeItemName;
            ImGui.SetNextItemWidth(260f);
            if (ItemSearchCombo.Draw("Prize item##gn_prize_item_combo", ref id, ref name))
            {
                Config.GrandNationalPrizeItemId = id;
                Config.GrandNationalPrizeItemName = name;
                Config.Save();
            }
        }
        else if (prize == GrandNationalPrizeType.Custom)
        {
            ImGui.SetNextItemWidth(260f);
            if (ImGui.InputText("Prize (max 50)##gn_prize_text", ref _prizeTextInput, 50))
            {
                Config.GrandNationalPrizeText = _prizeTextInput;
                Config.Save();
            }
        }
        else
        {
            var boost = (int)Math.Clamp(Config.GrandNationalBoost, 0, int.MaxValue);
            if (ImGuiEx.InputFancyNumeric(200f, "Host pot boost", ref boost, 0))
            {
                Config.GrandNationalBoost = Math.Max(0, boost);
                Config.Save();
            }
        }

        ImGui.Spacing();
    }

    private void SetPrizeType(GrandNationalPrizeType type)
    {
        Config.GrandNationalPrizeType = type;
        Config.Save();
    }

    private void DrawRegistration()
    {
        DrawStatsBlock();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        DrawAddControls();
        ImGui.Spacing();

        var footerHeight = ImGui.GetFrameHeight() + ImGui.GetStyle().ItemSpacing.Y * 4f;
        DrawRunnerList(footerHeight);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        DrawRegistrationFooter();
    }

    private void DrawAddControls()
    {
        var nearby = Service.GetNearbyPlayers();
        var preview = nearby.Count == 0 ? "No players nearby" : "Add nearby player...";
        ImGui.SetNextItemWidth(260f);
        using (var combo = ImRaii.Combo("##gn_nearby", preview, ImGuiComboFlags.HeightLarge))
        {
            if (combo)
            {
                if (ImGui.IsWindowAppearing()) ImGui.SetKeyboardFocusHere();
                ImGui.SetNextItemWidth(-1f);
                ImGui.InputText("##gn_nearby_filter", ref _nearbyFilter, 64);
                foreach (var player in nearby)
                {
                    if (!player.Contains(_nearbyFilter, StringComparison.OrdinalIgnoreCase)) continue;
                    var already = State.IsRegistered(player);
                    if (already)
                    {
                        ImGui.TextDisabled($"[Added]  {GrandNationalState.DisplayName(player)}");
                        continue;
                    }
                    if (ImGui.Selectable(GrandNationalState.DisplayName(player)))
                    {
                        State.AddEntry(player);
                        _nearbyFilter = string.Empty;
                        ImGui.CloseCurrentPopup();
                    }
                }
            }
        }

        ImGui.SameLine();
        ImGui.TextDisabled("Add runners from nearby, or via chat/player right-click menus.");
    }

    private void DrawRunnerList(float footerHeight)
    {
        var registered = State.Registered;
        var eligible = State.EligibleCount;
        ImGui.TextColored(UiColors.Accent, $"Runners ({eligible} in the race / {registered.Count} listed)");
        if (registered.Count == 0)
        {
            ImGui.TextDisabled("No runners yet.");
            ImGui.Dummy(new Vector2(0f, Math.Max(0f, ImGui.GetContentRegionAvail().Y - footerHeight)));
            return;
        }

        var tableHeight = Math.Max(120f, ImGui.GetContentRegionAvail().Y - footerHeight);
        using var table = ImRaii.Table("##gn_runners", 4,
            ImGuiTableFlags.BordersOuter | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY,
            new Vector2(-1f, tableHeight));
        if (!table) return;
        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 40f);
        ImGui.TableSetupColumn("Runner", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("World", ImGuiTableColumnFlags.WidthFixed, 120f);
        ImGui.TableSetupColumn("##gn_actions", ImGuiTableColumnFlags.WidthFixed, 360f);
        ImGui.TableHeadersRow();

        int remove = -1;
        string? togglePaid = null, requestGil = null, trade = null, invite = null;
        for (var i = 0; i < registered.Count; i++)
            DrawRunnerRow(i, registered[i], ref remove, ref togglePaid, ref requestGil, ref trade, ref invite);

        if (togglePaid != null) State.TogglePaid(togglePaid);
        if (requestGil != null) Service.RequestEntryFee(requestGil);
        if (trade != null) Service.TradeRunner(trade);
        if (invite != null) Service.SendRunnerInvite(invite);
        if (remove >= 0) State.RemoveAt(remove);
    }

    private void DrawRunnerRow(int idx, string entry, ref int remove, ref string? togglePaid, ref string? requestGil, ref string? trade, ref string? invite)
    {
        var paid = State.IsPaid(entry);
        var number = State.RunnerNumber(entry);
        ImGui.TableNextRow();
        if (paid)
            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(new Vector4(0.05f, 0.25f, 0.10f, 1f)));

        ImGui.TableSetColumnIndex(0);
        if (number > 0) UIHelper.TextColoredForChocobo(number, $"#{number}");

        ImGui.TableSetColumnIndex(1);
        var nameColour = paid ? UiColors.Positive : new Vector4(0.94f, 0.92f, 0.98f, 1f);
        ImGui.TextColored(nameColour, GrandNationalState.DisplayName(entry));
        if (!State.IsFree && !paid)
        {
            ImGui.SameLine();
            ImGui.TextColored(UiColors.Subtle, $"({UIHelper.FormatGil(State.EntryPaid(entry))} / {UIHelper.FormatGil(Config.GrandNationalEntryFee)})");
        }

        ImGui.TableSetColumnIndex(2);
        ImGui.TextDisabled(GrandNationalState.WorldOf(entry));

        ImGui.TableSetColumnIndex(3);
        RightAlignActions(number, paid);
        if (number > 0)
        {
            using (UIHelper.PushBlueButtonColours())
                if (UIHelper.IconTextButton(FontAwesomeIcon.PaperPlane, "Send URL", $"##gn_send{idx}"))
                    invite = entry;
            ImGui.SameLine();
        }
        if (!State.IsFree)
        {
            if (paid)
            {
                using (UIHelper.PushRedButtonColours())
                    if (UIHelper.IconTextButton(FontAwesomeIcon.Times, "Unpay", $"##gn_paid{idx}")) togglePaid = entry;
                ImGui.SameLine();
            }
            else
            {
                using (UIHelper.PushBlueButtonColours())
                    if (UIHelper.IconTextButton(FontAwesomeIcon.CommentDots, "Request", $"##gn_req{idx}")) requestGil = entry;
                ImGui.SameLine();
                using (UIHelper.PushYellowButtonColours())
                    if (UIHelper.IconTextButton(FontAwesomeIcon.Coins, "Trade", $"##gn_trade{idx}")) trade = entry;
                ImGui.SameLine();
                using (UIHelper.PushGreenButtonColours())
                    if (UIHelper.IconTextButton(FontAwesomeIcon.Check, "Mark Paid", $"##gn_paid{idx}")) togglePaid = entry;
                ImGui.SameLine();
            }
        }
        using (UIHelper.PushRedButtonColours())
        {
            var clicked = UIHelper.IconTextButton(FontAwesomeIcon.Trash, "", $"##gn_rem{idx}");
            if (UIHelper.CtrlClickConfirmed(clicked, "Hold Ctrl and click to remove this runner.")) remove = idx;
        }
    }

    private void RightAlignActions(int number, bool paid)
    {
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var total = UIHelper.IconTextButtonWidth(FontAwesomeIcon.Trash, "");
        if (number > 0)
            total += UIHelper.IconTextButtonWidth(FontAwesomeIcon.PaperPlane, "Send URL") + spacing;
        if (!State.IsFree)
        {
            if (paid)
                total += UIHelper.IconTextButtonWidth(FontAwesomeIcon.Times, "Unpay") + spacing;
            else
                total += UIHelper.IconTextButtonWidth(FontAwesomeIcon.CommentDots, "Request") + spacing
                       + UIHelper.IconTextButtonWidth(FontAwesomeIcon.Coins, "Trade") + spacing
                       + UIHelper.IconTextButtonWidth(FontAwesomeIcon.Check, "Mark Paid") + spacing;
        }
        var avail = ImGui.GetContentRegionAvail().X;
        if (avail > total) ImGui.SetCursorPosX(ImGui.GetCursorPosX() + avail - total);
    }

    private void DrawRegistrationFooter()
    {
        using (UIHelper.PushYellowButtonColours())
            if (UIHelper.IconTextButton(FontAwesomeIcon.Bullhorn, "Announce", "##gn_announce"))
                Service.AnnounceRegistrationOpen();
        ImGui.SameLine();
        using (ImRaii.Disabled(!State.CanClose))
        using (UIHelper.PushGreenButtonColours())
            if (UIHelper.IconTextButton(FontAwesomeIcon.Lock, "Close Registration", "##gn_close"))
                State.CloseRegistration();
        if (!State.CanClose && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip($"Need at least {GrandNationalState.MinRunners} paid runners to close registration.");
        ImGui.SameLine();
        using (UIHelper.PushRedButtonColours())
        {
            var clicked = UIHelper.IconTextButton(FontAwesomeIcon.Ban, "Cancel", "##gn_cancel");
            if (UIHelper.CtrlClickConfirmed(clicked, "Hold Ctrl and click to cancel. Refund any paid entries manually.") && UIHelper.TryDebounce(ref _lastActionMs))
                State.Reset();
        }
    }

    private void DrawClosed()
    {
        DrawStatsBlock();
        ImGui.Spacing();

        var footerHeight = ImGui.GetFrameHeight() + ImGui.GetStyle().ItemSpacing.Y * 4f;
        DrawGrid(footerHeight);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        DrawClosedFooter();
    }

    private void DrawClosedFooter()
    {
        using (ImRaii.Disabled(!Service.IsLive || Service.IsRolling))
        using (UIHelper.PushGreenButtonColours())
            if (UIHelper.IconTextButton(FontAwesomeIcon.FlagCheckered, "Start Race", "##gn_start"))
                Service.StartRace();
        ImGui.SameLine();
        using (UIHelper.PushYellowButtonColours())
            if (UIHelper.IconTextButton(FontAwesomeIcon.Unlock, "Reopen Registration", "##gn_reopen"))
                State.ReopenRegistration();
    }

    private void DrawGrid(float footerHeight)
    {
        var grid = State.GetGridSnapshot();
        ImGui.TextColored(UiColors.Accent, $"Grid ({grid.Count} runners)");
        var tableHeight = Math.Max(120f, ImGui.GetContentRegionAvail().Y - footerHeight);
        using var table = ImRaii.Table("##gn_grid", 3,
            ImGuiTableFlags.BordersOuter | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY,
            new Vector2(-1f, tableHeight));
        if (!table) return;
        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 40f);
        ImGui.TableSetupColumn("Runner", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("World", ImGuiTableColumnFlags.WidthFixed, 140f);
        ImGui.TableHeadersRow();
        foreach (var r in grid)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); UIHelper.TextColoredForChocobo(r.Number, r.Number.ToString());
            ImGui.TableNextColumn(); UIHelper.TextColoredForChocobo(r.Number, r.Name);
            ImGui.TableNextColumn(); ImGui.TextDisabled(r.World);
        }
    }

    private void DrawRacing()
    {
        DrawStatsBlock();
        ImGui.Spacing();
        ImGui.TextColored(UiColors.Info, "The race is running on the website...");
        ImGui.TextDisabled("The winner will be revealed shortly.");
    }

    private void DrawFinished()
    {
        var runner = State.WinningRunner();
        UIHelper.SectionHeader("Winner");
        if (runner == null)
        {
            ImGui.TextDisabled("No winner recorded.");
        }
        else
        {
            ImGui.TextColored(UiColors.Gold, $"#{runner.Number}  {runner.Name}");
            if (!string.IsNullOrEmpty(runner.World))
            {
                ImGui.SameLine();
                ImGui.TextDisabled($"({runner.World})");
            }
            if (State.IsPotPrize)
            {
                var total = State.NetPot;
                var traded = Service.WinnerTraded;
                var left = Math.Max(0, total - traded);

                ImGui.Text("Payout Prize:");
                ImGui.SameLine();
                ImGui.TextColored(UiColors.Positive, UIHelper.FormatGil(total));
                if (State.VenueCut > 0)
                {
                    ImGui.SameLine();
                    ImGui.TextDisabled($"(venue cut {UIHelper.FormatGil(State.VenueCut)})");
                }
                ImGui.Text("Traded so far:");
                ImGui.SameLine();
                ImGui.TextColored(UiColors.Info, UIHelper.FormatGil(traded));
                ImGui.Text("Left to trade:");
                ImGui.SameLine();
                ImGui.TextColored(left > 0 ? UiColors.Warning : UiColors.Positive, UIHelper.FormatGil(left));
            }
            else
            {
                ImGui.Text("Prize:");
                ImGui.SameLine();
                ImGui.TextColored(UiColors.Gold, string.IsNullOrWhiteSpace(State.PrizeLabel) ? "(not set)" : State.PrizeLabel);
            }
            ImGui.Spacing();
            using (UIHelper.PushYellowButtonColours())
                if (UIHelper.IconTextButton(FontAwesomeIcon.Bullhorn, "Announce Winner", "##gn_annwin"))
                    Service.AnnounceWinner();
            ImGui.SameLine();
            using (UIHelper.PushGreenButtonColours())
                if (UIHelper.IconTextButton(FontAwesomeIcon.Coins, "Trade Winner", "##gn_tradewin"))
                {
                    if (State.IsPotPrize) ImGui.SetClipboardText(State.NetPot.ToString());
                    Service.TradeWinner();
                }
            if (State.IsPotPrize)
            {
                ImGui.SameLine();
                var autoPayout = _plugin.AutoPayoutService;
                var isPayingWinner = autoPayout.IsRunning && autoPayout.TargetName == runner.Name;
                var isPayingOther = autoPayout.IsRunning && !isPayingWinner;
                if (isPayingWinner)
                {
                    using (UIHelper.PushRedButtonColours())
                        if (UIHelper.IconTextButton(FontAwesomeIcon.Stop, "Stop Payout", "##gn_stop_payout"))
                            autoPayout.Stop();
                }
                else
                {
                    using (ImRaii.Disabled(isPayingOther || State.NetPot <= 0))
                    using (UIHelper.PushBlueButtonColours())
                        if (UIHelper.IconTextButton(FontAwesomeIcon.HandHoldingUsd, "Auto Payout", "##gn_auto_payout"))
                        {
                            _winnerPayoutRemaining = State.NetPot;
                            autoPayout.Start(
                                runner.Name,
                                () => _winnerPayoutRemaining,
                                given => _winnerPayoutRemaining -= given);
                        }
                    if (ImGui.IsItemHovered() && State.NetPot <= 0)
                        ImGui.SetTooltip("No payout to send.");
                }
            }
        }
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        using (UIHelper.PushGreenButtonColours())
            if (UIHelper.IconTextButton(FontAwesomeIcon.Plus, "New Race", "##gn_new"))
                State.Reset();
        ImGui.SameLine();
        using (ImRaii.Disabled(!Service.IsLive))
        using (UIHelper.PushRedButtonColours())
        {
            var clicked = UIHelper.IconTextButton(FontAwesomeIcon.Stop, "End Session", "##gn_endsession");
            if (UIHelper.CtrlClickConfirmed(clicked, "Hold Ctrl and click to end the web session.") && UIHelper.TryDebounce(ref _lastActionMs))
                _plugin.WebMirror.EndSession();
        }
    }

    private void DrawStatsBlock()
    {
        using var table = ImRaii.Table("##gn_stats", 2,
            ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.NoHostExtendX);
        if (!table) return;
        ImGui.TableSetupColumn("##gn_stat_label", ImGuiTableColumnFlags.WidthFixed, 170f);
        ImGui.TableSetupColumn("##gn_stat_value", ImGuiTableColumnFlags.WidthFixed, 150f);

        StatRow("Entry Cost", State.IsFree ? "Free" : UIHelper.FormatGil(Config.GrandNationalEntryFee), UiColors.Subtle);
        if (State.IsPotPrize)
        {
            StatRow("Boosted Pot", UIHelper.FormatGil(Config.GrandNationalBoost), UiColors.Subtle);
            StatRow("Total Pot", UIHelper.FormatGil(State.Pot), UiColors.Gold);
            StatRow("Kept from Trades", UIHelper.FormatGil(State.VenueCut), UiColors.Subtle);
            StatRow("Winner Takes", UIHelper.FormatGil(State.NetPot), UiColors.Positive);
        }
        else
        {
            StatRow("Prize", string.IsNullOrWhiteSpace(State.PrizeLabel) ? "(not set)" : State.PrizeLabel, UiColors.Gold);
            StatRow("You Keep", UIHelper.FormatGil(State.Entries), UiColors.Positive);
        }
    }

    private static void StatRow(string label, string value, Vector4 valueColour)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TextColored(UiColors.Muted, label);
        ImGui.TableNextColumn();
        var avail = ImGui.GetContentRegionAvail().X;
        var textWidth = ImGui.CalcTextSize(value).X;
        if (avail > textWidth) ImGui.SetCursorPosX(ImGui.GetCursorPosX() + avail - textWidth);
        ImGui.TextColored(valueColour, value);
    }
}
