using System;
using System.Numerics;
using ChocoboRacing.Config;
using ChocoboRacing.Models;
using ChocoboRacing.Services;
using ChocoboRacing.State;
using ChocoboRacing.UI.Components;
using ChocoboRacing.Utility;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using ECommons.ImGuiMethods;

/// <summary>
/// Draws the host raffle tab: setup, registration, grid, live race, and winner/payout flow across each phase.
/// </summary>
namespace ChocoboRacing.UI.Tabs;

public sealed class RaffleTab
{
    private const string PrizeTypeLabel = "Prize Type";
    private const string PrizeItemLabel = "Prize Item";
    private const string PrizeTextLabel = "Prize";
    private const string PotBoostLabel = "Host Pot Boost";
    private const string VenueCutLabel = "Venue Cut";
    private const string EntryFeeLabel = "Entry Fee";
    private const string FinishLineLabel = "Finish Line";
    private const string ChatJoinLabel = "Chat Join";
    private const string JoinKeywordLabel = "Join Keyword";
    private const string ClosingTimeLabel = "Closing Time";
    private const string ClosesAtLabel = "Closes At";

    private const float FieldWidth = 220f;
    private const float TimeFieldWidth = 96f;
    private const int MinFinishLine = 5;
    private const int MaxFinishLine = 100;

    private static readonly string[] FormLabels =
    {
        PrizeTypeLabel, PrizeItemLabel, PrizeTextLabel, PotBoostLabel, VenueCutLabel,
        EntryFeeLabel, FinishLineLabel, ChatJoinLabel, JoinKeywordLabel, ClosingTimeLabel, ClosesAtLabel,
    };

    private readonly Plugin _plugin;
    private string _nearbyFilter = string.Empty;
    private string _keywordInput;
    private string _prizeTextInput;
    private long _lastActionMs;
    private long _winnerPayoutRemaining;

    private RaffleService Service => _plugin.RaffleManager;
    private RaffleState State => _plugin.RaffleState;
    private PluginConfig Config => _plugin.Configuration;

    public RaffleTab(Plugin plugin)
    {
        _plugin = plugin;
        _keywordInput = _plugin.Configuration.RaffleJoinKeyword;
        _prizeTextInput = _plugin.Configuration.RafflePrizeText;
    }

    public void Draw()
    {
        DrawLiveHint();
        switch (State.Phase)
        {
            case RafflePhase.Idle: DrawSetup(); break;
            case RafflePhase.Registration: DrawRegistration(); break;
            case RafflePhase.Closed: DrawClosed(); break;
            case RafflePhase.Racing: DrawRacing(); break;
            case RafflePhase.Finished: DrawFinished(); break;
        }
    }

    private void DrawLiveHint()
    {
        if (Service.IsLive) return;
        ImGui.TextColored(UiColors.Warning, "⚠  Go Live on the Webview tab to run a raffle.");
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
    }

    private void DrawSetup()
    {
        DrawSetupIntro();
        DrawPrizeSection();
        DrawEntrySection();
        DrawRaceSection();
        DrawRegistrationSection();
        DrawSetupActions();
    }

    private static void DrawSetupIntro()
    {
        DrawSectionHeader("Raffle");
        ImGui.TextWrapped("Runners register onto a list, each becomes a numbered chocobo, and the server draws the winner when you start. The winning runner takes the prize.");
        ImGuiHelpers.ScaledDummy(8f);
    }

    private static void DrawSectionHeader(string label)
    {
        ImGui.TextColored(UiColors.Gold, label);
        ImGuiHelpers.ScaledDummy(2f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(6f);
    }

    private static float FormLabelWidth()
    {
        var widest = 0f;
        foreach (var label in FormLabels)
            widest = Math.Max(widest, ImGui.CalcTextSize(label).X);
        return widest + ImGui.GetStyle().CellPadding.X * 2f;
    }

    private static void FormLabel(string label)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(UiColors.Muted, label);
        ImGui.TableNextColumn();
    }

    private static void FormHint(string hint)
    {
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(UiColors.Subtle, hint);
    }

    private void DrawPrizeSection()
    {
        DrawSectionHeader("Prize");

        using (var table = ImRaii.Table("##gn_prize_form", 2, ImGuiTableFlags.SizingFixedFit))
        {
            if (!table) return;
            ImGui.TableSetupColumn("##label", ImGuiTableColumnFlags.WidthFixed, FormLabelWidth());
            ImGui.TableSetupColumn("##field", ImGuiTableColumnFlags.WidthStretch);

            FormLabel(PrizeTypeLabel);
            DrawPrizeTypeRadios();

            switch (Config.RafflePrizeType)
            {
                case RafflePrizeType.Item:   DrawPrizeItemField(); break;
                case RafflePrizeType.Custom: DrawPrizeTextField(); break;
                default:                            DrawPotFields(); break;
            }
        }

        ImGuiHelpers.ScaledDummy(8f);
    }

    private void DrawPrizeTypeRadios()
    {
        var prize = Config.RafflePrizeType;
        if (ImGui.RadioButton("Pot##gn_prize_pot", prize == RafflePrizeType.Pot)) SetPrizeType(RafflePrizeType.Pot);
        ImGui.SameLine();
        if (ImGui.RadioButton("Item##gn_prize_item", prize == RafflePrizeType.Item)) SetPrizeType(RafflePrizeType.Item);
        ImGui.SameLine();
        if (ImGui.RadioButton("Custom##gn_prize_custom", prize == RafflePrizeType.Custom)) SetPrizeType(RafflePrizeType.Custom);
    }

    private void DrawPrizeItemField()
    {
        FormLabel(PrizeItemLabel);
        var id = Config.RafflePrizeItemId;
        var name = Config.RafflePrizeItemName;
        ImGui.SetNextItemWidth(FieldWidth * ImGuiHelpers.GlobalScale);
        if (!ItemSearchCombo.Draw("##gn_prize_item_combo", ref id, ref name)) return;

        Config.RafflePrizeItemId = id;
        Config.RafflePrizeItemName = name;
        Config.Save();
    }

    private void DrawPrizeTextField()
    {
        FormLabel(PrizeTextLabel);
        ImGui.SetNextItemWidth(FieldWidth * ImGuiHelpers.GlobalScale);
        if (ImGui.InputText("##gn_prize_text", ref _prizeTextInput, 50))
        {
            Config.RafflePrizeText = _prizeTextInput;
            Config.Save();
        }
        FormHint("(max 50 characters)");
    }

    private void DrawPotFields()
    {
        FormLabel(PotBoostLabel);
        var boost = (int)Math.Clamp(Config.RaffleBoost, 0, int.MaxValue);
        if (ImGuiEx.InputFancyNumeric(FieldWidth * ImGuiHelpers.GlobalScale, "##gn_boost", ref boost, 0))
        {
            Config.RaffleBoost = Math.Max(0, boost);
            Config.Save();
        }
        FormHint("(gil you add to the pot)");

        FormLabel(VenueCutLabel);
        var cut = Config.RaffleVenueCutPercent;
        ImGui.SetNextItemWidth(FieldWidth * ImGuiHelpers.GlobalScale);
        if (ImGui.SliderFloat("##gn_cut", ref cut, 0f, 100f, "%.0f%%"))
        {
            Config.RaffleVenueCutPercent = cut;
            Config.Save();
        }
        FormHint("(kept from the pot)");
    }

    private void DrawEntrySection()
    {
        DrawSectionHeader("Entry");

        using (var table = ImRaii.Table("##gn_entry_form", 2, ImGuiTableFlags.SizingFixedFit))
        {
            if (!table) return;
            ImGui.TableSetupColumn("##label", ImGuiTableColumnFlags.WidthFixed, FormLabelWidth());
            ImGui.TableSetupColumn("##field", ImGuiTableColumnFlags.WidthStretch);

            FormLabel(EntryFeeLabel);
            var fee = (int)Math.Clamp(Config.RaffleEntryFee, 0, int.MaxValue);
            if (ImGuiEx.InputFancyNumeric(FieldWidth * ImGuiHelpers.GlobalScale, "##gn_entry_fee", ref fee, 0))
            {
                Config.RaffleEntryFee = Math.Max(0, fee);
                Config.Save();
            }
            FormHint("(0 = free to enter)");
        }

        ImGuiHelpers.ScaledDummy(8f);
    }

    private void DrawRaceSection()
    {
        DrawSectionHeader("Race");

        using (var table = ImRaii.Table("##gn_race_form", 2, ImGuiTableFlags.SizingFixedFit))
        {
            if (!table) return;
            ImGui.TableSetupColumn("##label", ImGuiTableColumnFlags.WidthFixed, FormLabelWidth());
            ImGui.TableSetupColumn("##field", ImGuiTableColumnFlags.WidthStretch);

            FormLabel(FinishLineLabel);
            var finish = Config.RaffleFinishLine;
            if (ImGuiEx.InputFancyNumeric(FieldWidth * ImGuiHelpers.GlobalScale, "##gn_finish", ref finish, 0))
            {
                Config.RaffleFinishLine = Math.Clamp(finish, MinFinishLine, MaxFinishLine);
                Config.Save();
            }
            FormHint($"(yalms, {MinFinishLine}-{MaxFinishLine})");
        }

        ImGuiHelpers.ScaledDummy(8f);
    }

    private void DrawRegistrationSection()
    {
        DrawSectionHeader("Registration");

        using (var table = ImRaii.Table("##gn_reg_form", 2, ImGuiTableFlags.SizingFixedFit))
        {
            if (!table) return;
            ImGui.TableSetupColumn("##label", ImGuiTableColumnFlags.WidthFixed, FormLabelWidth());
            ImGui.TableSetupColumn("##field", ImGuiTableColumnFlags.WidthStretch);

            DrawJoinKeywordFields();
            DrawCloseTimeFields();
        }

        ImGuiHelpers.ScaledDummy(8f);
    }

    private void DrawJoinKeywordFields()
    {
        FormLabel(ChatJoinLabel);
        var autoJoin = Config.RaffleAutoJoin;
        if (ImGui.Checkbox("##gn_autojoin", ref autoJoin))
        {
            Config.RaffleAutoJoin = autoJoin;
            Config.Save();
        }
        FormHint("(runners join by typing a keyword in chat)");

        if (!autoJoin) return;

        FormLabel(JoinKeywordLabel);
        ImGui.SetNextItemWidth(FieldWidth * ImGuiHelpers.GlobalScale);
        if (!ImGui.InputText("##gn_keyword", ref _keywordInput, 32)) return;

        Config.RaffleJoinKeyword = _keywordInput.Trim();
        Config.Save();
    }

    private void DrawCloseTimeFields()
    {
        FormLabel(ClosingTimeLabel);
        var enabled = Config.RaffleCloseTimeEnabled;
        if (ImGui.Checkbox("##gn_closetime", ref enabled))
            SetCloseTimeEnabled(enabled);
        FormHint("(announce when registration closes)");

        if (!enabled) return;

        FormLabel(ClosesAtLabel);
        DrawCloseTimeInputs();

        FormLabel(string.Empty);
        ImGui.TextColored(UiColors.Muted,
            $"{ServerTimeUtil.FormatCloseLabel(Config.RaffleCloseHour, Config.RaffleCloseMinute)} ST  ·  {ServerTimeUtil.FormatTimeLeft(Config.RaffleCloseHour, Config.RaffleCloseMinute)}");
    }

    private void SetCloseTimeEnabled(bool enabled)
    {
        Config.RaffleCloseTimeEnabled = enabled;
        if (enabled && Config.RaffleCloseHour == 0 && Config.RaffleCloseMinute == 0)
        {
            var (h, m) = ServerTimeUtil.SuggestCloseTime();
            Config.RaffleCloseHour = h;
            Config.RaffleCloseMinute = m;
        }
        Config.Save();
    }

    private void DrawCloseTimeInputs()
    {
        var hour = Config.RaffleCloseHour;
        var minute = Config.RaffleCloseMinute;
        var changed = false;

        ImGui.SetNextItemWidth(TimeFieldWidth * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt("##gn_closehour", ref hour, 1, 1)) changed = true;
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(UiColors.Muted, ":");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(TimeFieldWidth * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt("##gn_closemin", ref minute, 1, 5)) changed = true;
        FormHint("(hour 0-23, minute 0-59, Server Time)");

        if (!changed) return;

        Config.RaffleCloseHour = Math.Clamp(hour, 0, 23);
        Config.RaffleCloseMinute = Math.Clamp(minute, 0, 59);
        Config.Save();
    }

    private void DrawSetupActions()
    {
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(8f);

        using (ImRaii.Disabled(!Service.IsLive))
        using (UIHelper.PushGreenButtonColours())
            if (UIHelper.IconTextButton(FontAwesomeIcon.DoorOpen, "Open Registration", "##gn_open"))
                OpenRegistration(false);
        DrawNeedsSessionTooltip();

        if (State.HasLastRoster)
        {
            ImGui.SameLine();
            using (ImRaii.Disabled(!Service.IsLive))
            using (UIHelper.PushBlueButtonColours())
                if (UIHelper.IconTextButton(FontAwesomeIcon.UserFriends, "Open with Last Runners", "##gn_open_last"))
                    OpenRegistration(true);
            if (!DrawNeedsSessionTooltip() && ImGui.IsItemHovered())
                ImGui.SetTooltip("Open registration and re-add last race's runners, all marked unpaid.");
        }

        if (!Service.IsLive) return;

        ImGui.SameLine();
        using (UIHelper.PushRedButtonColours())
        {
            var clicked = UIHelper.IconTextButton(FontAwesomeIcon.Stop, "End Session", "##gn_endsession_setup");
            if (UIHelper.CtrlClickConfirmed(clicked, "Hold Ctrl and click to end the web session.") && UIHelper.TryDebounce(ref _lastActionMs))
                _plugin.WebMirror.EndSession();
        }
    }

    private void OpenRegistration(bool withLastRunners)
    {
        State.OpenRegistration(withLastRunners);
        if (Config.RaffleAutoAnnounceRegistration) Service.AnnounceRegistrationOpen();
    }

    private bool DrawNeedsSessionTooltip()
    {
        if (Service.IsLive) return false;
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Go Live on the Webview tab to start a raffle session.");
        return true;
    }

    private void SetPrizeType(RafflePrizeType type)
    {
        Config.RafflePrizeType = type;
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
        var nearby = _plugin.NearbyPlayers.GetNearbyPlayers();
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
                        ImGui.TextDisabled($"[Added]  {RaffleState.DisplayName(player)}");
                        continue;
                    }
                    if (ImGui.Selectable(RaffleState.DisplayName(player)))
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

        if (togglePaid != null && State.TogglePaid(togglePaid) && Config.RaffleAutoTellPaid)
            Service.SendRunnerInvite(togglePaid);
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
        ImGui.TextColored(nameColour, RaffleState.DisplayName(entry));
        if (!State.IsFree && !paid)
        {
            ImGui.SameLine();
            ImGui.TextColored(UiColors.Subtle, $"({UIHelper.FormatGil(State.EntryPaid(entry))} / {UIHelper.FormatGil(Config.RaffleEntryFee)})");
        }

        ImGui.TableSetColumnIndex(2);
        ImGui.TextDisabled(RaffleState.WorldOf(entry));

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
        using (ImRaii.Disabled(!Config.RaffleCloseTimeEnabled))
        using (UIHelper.PushCyanButtonColours())
            if (UIHelper.IconTextButton(FontAwesomeIcon.Clock, "Announce Closing", "##gn_announce_close"))
                Service.AnnounceClosingTime();
        if (!Config.RaffleCloseTimeEnabled && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Set a closing time on the setup screen to announce it.");
        ImGui.SameLine();
        using (ImRaii.Disabled(!State.CanClose))
        using (UIHelper.PushGreenButtonColours())
            if (UIHelper.IconTextButton(FontAwesomeIcon.Lock, "Close Registration", "##gn_close"))
                State.CloseRegistration();
        if (!State.CanClose && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip($"Need at least {RaffleState.MinRunners} paid runners to close registration.");
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
        if (Service.IsLive)
        {
            ImGui.SameLine();
            using (UIHelper.PushRedButtonColours())
            {
                var clicked = UIHelper.IconTextButton(FontAwesomeIcon.Stop, "End Session", "##gn_endsession");
                if (UIHelper.CtrlClickConfirmed(clicked, "Hold Ctrl and click to end the web session.") && UIHelper.TryDebounce(ref _lastActionMs))
                    _plugin.WebMirror.EndSession();
            }
        }
    }

    private void DrawStatsBlock()
    {
        using var table = ImRaii.Table("##gn_stats", 2,
            ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.NoHostExtendX);
        if (!table) return;
        ImGui.TableSetupColumn("##gn_stat_label", ImGuiTableColumnFlags.WidthFixed, 170f);
        ImGui.TableSetupColumn("##gn_stat_value", ImGuiTableColumnFlags.WidthFixed, 150f);

        StatRow("Entry Cost", State.IsFree ? "Free" : UIHelper.FormatGil(Config.RaffleEntryFee), UiColors.Subtle);
        if (State.IsPotPrize)
        {
            StatRow("Boosted Pot", UIHelper.FormatGil(Config.RaffleBoost), UiColors.Subtle);
            StatRow("Total Pot", UIHelper.FormatGil(State.Pot), UiColors.Gold);
            StatRow("Kept from Trades", UIHelper.FormatGil(State.VenueCut), UiColors.Subtle);
            StatRow("Winner Takes", UIHelper.FormatGil(State.NetPot), UiColors.Positive);
        }
        else
        {
            StatRow("Prize", string.IsNullOrWhiteSpace(State.PrizeLabel) ? "(not set)" : State.PrizeLabel, UiColors.Gold);
            StatRow("You Keep", UIHelper.FormatGil(State.Entries), UiColors.Positive);
        }
        DrawCloseTimeRows();
    }

    private void DrawCloseTimeRows()
    {
        if (!Config.RaffleCloseTimeEnabled) return;
        if (State.Phase != RafflePhase.Registration) return;

        var hour = Config.RaffleCloseHour;
        var minute = Config.RaffleCloseMinute;
        var seconds = ServerTimeUtil.SecondsUntil(hour, minute);
        var countColour = seconds <= 0 ? UiColors.Warning : seconds <= 60 ? UiColors.Negative : UiColors.Info;
        StatRow("Closes", $"{ServerTimeUtil.FormatCloseLabel(hour, minute)} ST", UiColors.Info);
        StatRow("Time Left", ServerTimeUtil.FormatTimeLeft(hour, minute), countColour);
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
