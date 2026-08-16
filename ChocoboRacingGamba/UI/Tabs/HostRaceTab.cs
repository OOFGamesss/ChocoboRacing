using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ChocoboRacing.Models;
using ChocoboRacing.State;
using ChocoboRacing.UI.Components;
using ChocoboRacing.Utility;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using ECommons.ImGuiMethods;

/// <summary>
/// Draws the host Race tab: phase controls, bet placement, pending/confirmed bet tables, and winner display.
/// </summary>
namespace ChocoboRacing.UI.Tabs;

public sealed class HostRaceTab
{
    private const string PlaceBetLabel = "Place Bet";
    private const string RemoveBetLabel = "Remove Bet";
    private const float PlayerComboWidth = 250f;
    private const float BetInputWidth = 100f;

    private readonly Plugin _plugin;
    private int _betPlayerIndex;
    private readonly Dictionary<int, int> _betAmountsPerChocobo = new();
    private long _lastActionTimeMs = 0;

    public HostRaceTab(Plugin Plugin)
    {
        _plugin = Plugin;
    }

    public void Draw()
    {
        var state      = _plugin.GameState;
        var inParty    = _plugin.PartyManager.IsInParty();
        var inAlliance = _plugin.PartyManager.IsInAlliance();
        var hasGroup   = inParty || inAlliance;

        var canInteract = hasGroup || _plugin.IsTestingMode;

        if (!canInteract)
        {
            using (new ImRaii.ColorDisposable().Push(ImGuiCol.Text, new Vector4(1f, 0.35f, 0.35f, 1f)))
                ImGui.TextWrapped("\u26A0  You must be in a Party or Alliance to use host controls.");
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
        }

        using (ImRaii.Disabled(!canInteract))
            DrawControls(state);

        if (state.Phase != RacePhase.Idle)
        {
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
        }

        if (state.Phase == RacePhase.Betting)
            DrawPlaceBetsSection(state);

        if (state.Phase == RacePhase.Racing || state.Phase == RacePhase.BetsClosed || state.Phase == RacePhase.Finished)
            RaceTrackDraw.DrawTrack(_plugin);

        if (state.Phase != RacePhase.Idle)
        {
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
        }

        DrawPendingBetsTable(state);
        DrawCurrentBetsTable(state);
        DrawWinnersTable(state);

    }

    private void DrawControls(RaceState state)
    {
        using var disabled = ImRaii.Disabled(_plugin.MessageSender.IsSendingMessages);
        using var table = ImRaii.Table("RaceControlsLayout", 2,
            ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchSame);
        if (!table) return;

        ImGui.TableNextRow();

        ImGui.TableNextColumn();
        DrawRaceInfoControls(state);

        ImGui.TableNextColumn();
        DrawRaceControlButtons(state);
    }

    private void DrawRaceInfoControls(RaceState state)
    {
        DrawControlSectionHeader("Race Info");

        var prefix = _plugin.PartyManager.GetChatPrefix();
        if (UIHelper.IconTextButton(FontAwesomeIcon.Bullhorn, "Announce Rules", "##announce_rules_btn"))
            _plugin.MessageSender.SendMessage(prefix, _plugin.Configuration.AnnounceRulesMessage, state);

        if (state.Phase != RacePhase.Betting) return;

        ImGui.SameLine();
        DrawRepostChocobosButton(state);
    }

    private void DrawRaceControlButtons(RaceState state)
    {
        DrawControlSectionHeader("Race Control");

        var prefix = _plugin.PartyManager.GetChatPrefix();

        switch (state.Phase)
        {
            case RacePhase.Idle:
                DrawOpenBettingButton(state, prefix, state.Phase);
                break;
            case RacePhase.Betting:
                DrawLastBetsButton(state);
                ImGui.SameLine();
                DrawNoMoreBetsButton(state, prefix, state.Phase);
                ImGui.SameLine();
                DrawVoidRaceButton(state);
                break;
            case RacePhase.BetsClosed:
                DrawStartRaceButton(state, prefix);
                ImGui.SameLine();
                DrawVoidRaceButton(state);
                break;
            case RacePhase.Racing:
                DrawRollButton(state);
                ImGui.SameLine();
                DrawVoidRaceButton(state);
                break;
            case RacePhase.Finished:
                DrawEndRaceButton(state);
                break;
        }
    }

    private void DrawStartRaceButton(RaceState state, string prefix)
    {
        using var colours = UIHelper.PushGreenButtonColours();
        if (!UIHelper.IconTextButton(FontAwesomeIcon.FlagCheckered, "Start Race", "##start_race_btn")) return;

        state.StartRacing();
        _plugin.MessageSender.SendMessage(prefix, _plugin.Configuration.RaceStartedMessage, state);
    }

    private static void DrawControlSectionHeader(string title) => UIHelper.SectionHeader(title);

    private void BroadcastBet(RaceState state, string tag, string playerName, int chocoboNumber, long amount)
    {
        var prefix      = _plugin.PartyManager.GetChatPrefix();
        var chocoboName = TrackVisualiser.GetChocoboName(state.Config, chocoboNumber);
        _plugin.ChatQueue.Enqueue(
            $"{prefix} [{tag}] {playerName}: {chocoboName} for {UIHelper.FormatGil(amount)}",
            echoInTesting: true);
    }

    private void DrawOpenBettingButton(RaceState state, string p, RacePhase phase)
    {
        if (phase != RacePhase.Idle) return;

        using var _ = UIHelper.PushGreenButtonColours();
        if (UIHelper.IconTextButton(FontAwesomeIcon.DoorOpen, "Open Betting", "##open_betting_btn"))
        {
            state.StartBetting();
            _plugin.MessageSender.SendMessage(p, _plugin.Configuration.OpenBettingMessage, state);
        }
    }

    private void DrawNoMoreBetsButton(RaceState state, string p, RacePhase phase)
    {
        if (phase != RacePhase.Betting) return;

        using var _ = UIHelper.PushGreenButtonColours();
        if (UIHelper.IconTextButton(FontAwesomeIcon.DoorClosed, "No More Bets", "##no_more_bets_btn"))
        {
            state.CloseBets();
            _plugin.MessageSender.SendMessage(p, _plugin.Configuration.NoMoreBetsMessage, state);
        }
    }

    private void DrawLastBetsButton(RaceState state)
    {
        if (state.Phase != RacePhase.Betting) return;

        using var _ = UIHelper.PushYellowButtonColours();
        if (UIHelper.IconTextButton(FontAwesomeIcon.History, "Last Bets", "##last_bets_btn"))
        {
            var p = _plugin.PartyManager.GetChatPrefix();
            _plugin.MessageSender.SendMessage(p, _plugin.Configuration.LastBetsMessage, state);
        }
    }

    private void DrawRollButton(RaceState state)
    {
        if (state.Phase != RacePhase.Racing) return;

        var hasWinner = _plugin.RaceManager.HasWinner();
        var rollLabel = _plugin.IsTestingMode ? "Roll (Test)" : $"Roll /dice {state.ChocoboCount}";
        using (ImRaii.Disabled(hasWinner))
        using (UIHelper.PushGreenButtonColours())
        {
            if (UIHelper.IconTextButton(FontAwesomeIcon.Dice, rollLabel, "##roll_btn"))
            {
                if (_plugin.IsTestingMode)
                {
                    _plugin.ChatHandler.SimulateRoll(state.ChocoboCount);
                }
                else
                {
                    _plugin.ChatQueue.Enqueue(_plugin.Configuration.RandomLineMessage);

                    _plugin.ChatHandler.ExpectLocalRoll();
                    _plugin.ChatQueue.Enqueue(_plugin.PartyManager.GetDiceCommand(state.ChocoboCount));
                }
            }
        }
    }

    private void DrawRepostChocobosButton(RaceState state)
    {
        if (UIHelper.IconTextButton(FontAwesomeIcon.Sync, "Repost Chocobos", "##repost_chocobos_btn"))
        {
            var p = _plugin.PartyManager.GetChatPrefix();
            _plugin.MessageSender.SendMessage(p, "Place your bets: '[Chocobo Number or Name] [Amount]'\n{chocobonames}", state);
        }
    }

    private void DrawEndRaceButton(RaceState state)
    {
        if (state.Phase != RacePhase.Finished) return;

        using var _ = UIHelper.PushRedButtonColours();
        bool endClicked = UIHelper.IconTextButton(FontAwesomeIcon.Trophy, "End Race", "##end_race_btn");
        if (UIHelper.CtrlClickConfirmed(endClicked, "Hold Ctrl and Left Click to end this race."))
        {
            _plugin.WebMirror.FlushFinishedState();
            state.ResetForNextRace();
        }
    }

    private void DrawVoidRaceButton(RaceState state)
    {
        var ph = state.Phase;
        if (ph != RacePhase.Betting && ph != RacePhase.BetsClosed && ph != RacePhase.Racing) return;

        using var _ = UIHelper.PushRedButtonColours();
        bool voidClicked = UIHelper.IconTextButton(FontAwesomeIcon.Ban, "Void Race", "##void_race_btn");
        if (UIHelper.CtrlClickConfirmed(voidClicked, "Hold Ctrl and Left Click to void this race."))
        {
            state.VoidRace();
            _plugin.ChatQueue.ClearPendingAnnouncements();
            _plugin.MessageSender.SendMessage(_plugin.PartyManager.GetChatPrefix(), _plugin.Configuration.VoidRaceMessage, state);
        }
    }

    private void DrawPlaceBetsSection(RaceState state)
    {
        ImGui.TextColored(UiColors.Accent, "Place Bets");
        ImGui.Spacing();

        var activeBanks = state.GetBanksSnapshot().ToList();

        if (activeBanks.Count == 0)
        {
            ImGui.TextColored(UiColors.Muted, "No players with banks available.");
            return;
        }

        DrawBetPlayerCombo(activeBanks);
        ImGui.Spacing();
        DrawPlaceBetsTable(state, activeBanks);
        ImGui.Spacing();
    }

    private void DrawBetPlayerCombo(List<PlayerBank> activeBanks)
    {
        var playerNames = activeBanks.Select(b => $"{b.Name} ({UIHelper.FormatGil(b.Balance)})").ToArray();

        ImGui.AlignTextToFramePadding();
        ImGui.Text("Player:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(PlayerComboWidth * ImGuiHelpers.GlobalScale);
        ImGui.Combo("##BetPlayer", ref _betPlayerIndex, playerNames, playerNames.Length);
    }

    private void DrawPlaceBetsTable(RaceState state, List<PlayerBank> activeBanks)
    {
        const ImGuiTableFlags flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg
            | ImGuiTableFlags.SizingFixedFit;

        using var table = ImRaii.Table("PlaceBetsTable", 4, flags);
        if (!table) return;

        ImGui.TableSetupColumn("Chocobo",      ImGuiTableColumnFlags.WidthFixed, ChocoboColumnWidth(state));
        ImGui.TableSetupColumn("Current Bets", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Amount",       ImGuiTableColumnFlags.WidthFixed, BetAmountColumnWidth());
        ImGui.TableSetupColumn("Actions",      ImGuiTableColumnFlags.WidthFixed, BetActionsColumnWidth());
        ImGui.TableHeadersRow();

        for (var i = 1; i <= state.ChocoboCount; i++)
            DrawChocoboBetRow(state, activeBanks, i);
    }

    private static float ChocoboColumnWidth(RaceState state)
    {
        var widest = 0f;
        for (var i = 1; i <= state.ChocoboCount; i++)
            widest = Math.Max(widest, ImGui.CalcTextSize(TrackVisualiser.GetChocoboName(state.Config, i)).X);
        return widest + ImGui.GetStyle().CellPadding.X * 2f;
    }

    private static float BetAmountColumnWidth() =>
        Math.Max(UIHelper.QuickAmountButtonsWidth(), BetInputWidth * ImGuiHelpers.GlobalScale)
            + ImGui.GetStyle().CellPadding.X * 2f;

    private static float BetActionsColumnWidth() =>
        UIHelper.IconTextButtonWidth(FontAwesomeIcon.MoneyBillWave, PlaceBetLabel)
            + ImGui.GetStyle().CellPadding.X * 2f;

    private void DrawChocoboBetRow(RaceState state, List<PlayerBank> activeBanks, int chocoboNumber)
    {
        using var id = ImRaii.PushId($"bet_row_{chocoboNumber}");
        ImGui.TableNextRow();

        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        UIHelper.TextColoredForChocobo(chocoboNumber, TrackVisualiser.GetChocoboName(state.Config, chocoboNumber));

        ImGui.TableNextColumn();
        DrawCurrentBetsCell(state, chocoboNumber);

        ImGui.TableNextColumn();
        DrawBetAmountCell(chocoboNumber);

        ImGui.TableNextColumn();
        DrawPlaceBetButton(state, activeBanks, chocoboNumber);
    }

    private static void DrawCurrentBetsCell(RaceState state, int chocoboNumber)
    {
        ImGui.AlignTextToFramePadding();

        var existingBets = state.GetBetsForChocobo(chocoboNumber);
        if (existingBets.Count == 0)
        {
            ImGui.TextColored(UiColors.Muted, "No bets");
            return;
        }

        foreach (var bet in existingBets)
            ImGui.TextColored(UiColors.Subtle, $"{bet.PlayerName}: {UIHelper.FormatGil(bet.Amount)}");
    }

    private void DrawBetAmountCell(int chocoboNumber)
    {
        if (!_betAmountsPerChocobo.ContainsKey(chocoboNumber)) _betAmountsPerChocobo[chocoboNumber] = 0;
        var amt = _betAmountsPerChocobo[chocoboNumber];

        UIHelper.QuickAmountButtons(ref amt, chocoboNumber.ToString());
        _betAmountsPerChocobo[chocoboNumber] = amt;

        if (ImGuiEx.InputFancyNumeric(BetInputWidth * ImGuiHelpers.GlobalScale, "##bet_amt", ref amt, 0))
            _betAmountsPerChocobo[chocoboNumber] = Math.Max(0, amt);
    }

    private void DrawPlaceBetButton(RaceState state, List<PlayerBank> activeBanks, int chocoboNumber)
    {
        var amount = _betAmountsPerChocobo[chocoboNumber];
        var playerValid = _betPlayerIndex >= 0 && _betPlayerIndex < activeBanks.Count;

        using (ImRaii.Disabled(amount <= 0 || !playerValid))
        using (UIHelper.PushGreenButtonColours())
            if (UIHelper.IconTextButton(FontAwesomeIcon.MoneyBillWave, PlaceBetLabel, "##place_bet"))
                PlaceBet(state, activeBanks[_betPlayerIndex], chocoboNumber, amount);

        if (!ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) return;

        ImGui.SetTooltip(!playerValid
            ? "Select a player first."
            : amount <= 0
                ? "Enter an amount to bet."
                : "Place Bet: stakes this amount on this chocobo.");
    }

    private void PlaceBet(RaceState state, PlayerBank bank, int chocoboNumber, int amount)
    {
        var toPlace = Math.Min((long)amount, bank.Balance);
        if (toPlace > 0)
        {
            state.AddBet(bank.Name, bank.World, chocoboNumber, toPlace);
            BroadcastBet(state, "BET CONFIRMED", bank.Name, chocoboNumber, toPlace);
        }

        _betAmountsPerChocobo[chocoboNumber] = 0;
    }

    private void DrawPendingBetsTable(RaceState state)
    {
        var pendingBets = state.Bets.Where(b => b.Status == BetStatus.Pending).ToList();
        if (pendingBets.Count == 0 || state.Phase != RacePhase.Betting) return;

        ImGui.TextColored(UiColors.Warning, "Pending Bets");
        ImGui.Spacing();

        using (var table = ImRaii.Table("PendingBetsTable", 5, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
        {
            if (table)
            {
                ImGui.TableSetupColumn("Player",  ImGuiTableColumnFlags.WidthStretch, 0.25f);
                ImGui.TableSetupColumn("Chocobo", ImGuiTableColumnFlags.WidthStretch, 0.15f);
                ImGui.TableSetupColumn("Amount",  ImGuiTableColumnFlags.WidthStretch, 0.20f);
                ImGui.TableSetupColumn("Bank",    ImGuiTableColumnFlags.WidthStretch, 0.20f);
                ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthStretch, 0.20f);
                ImGui.TableHeadersRow();
                foreach (var bet in pendingBets)
                    DrawPendingBetRow(state, bet);
            }
        }
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
    }

    private void DrawPendingBetRow(RaceState state, Bet bet)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn(); ImGui.Text(bet.PlayerName);
        ImGui.TableNextColumn(); UIHelper.TextColoredForChocobo(bet.ChocoboNumber, TrackVisualiser.GetChocoboName(state.Config, bet.ChocoboNumber));
        ImGui.TableNextColumn(); ImGui.Text(UIHelper.FormatGil(bet.Amount));
        ImGui.TableNextColumn();

        var bank    = state.GetBank(bet.PlayerName, bet.PlayerWorld);
        var bal     = bank?.Balance ?? 0;
        var balColor = bal >= bet.Amount ? UiColors.Positive : UiColors.Negative;
        ImGui.TextColored(balColor, UIHelper.FormatGil(bal));

        ImGui.TableNextColumn();
        var allowed    = state.ClampedBetAmount(bet.PlayerName, bet.PlayerWorld, bet.ChocoboNumber, bet.Amount);
        var overLimit  = bet.Amount > allowed;
        bool canAfford = bal >= bet.Amount;

        bool acceptClicked;
        using (ImRaii.Disabled(!canAfford))
        {
            using var overLimitColor = new ImRaii.ColorDisposable()
                .Push(ImGuiCol.Button,        new Vector4(0.55f, 0.15f, 0.15f, 1f), overLimit && canAfford)
                .Push(ImGuiCol.ButtonHovered, new Vector4(0.72f, 0.22f, 0.22f, 1f), overLimit && canAfford)
                .Push(ImGuiCol.ButtonActive,  new Vector4(0.45f, 0.10f, 0.10f, 1f), overLimit && canAfford);
            acceptClicked = ImGui.SmallButton($"Accept##acc_{bet.PlayerName}_{bet.ChocoboNumber}_{bet.Amount}");
        }

        if (ImGui.IsItemHovered())
        {
            if (!canAfford)
                ImGui.SetTooltip("Player cannot afford this bet.");
            else if (overLimit)
                ImGui.SetTooltip("This bet is over your bet limit. Use CTRL and Left click to accept.");
        }

        if (acceptClicked && canAfford && (!overLimit || ImGui.GetIO().KeyCtrl) && UIHelper.TryDebounce(ref _lastActionTimeMs))
        {
            state.AcceptPendingBet(bet);
            BroadcastBet(state, "BET CONFIRMED", bet.PlayerName, bet.ChocoboNumber, bet.Amount);
        }

        ImGui.SameLine();
        if (ImGui.SmallButton($"Deny##deny_{bet.PlayerName}") && UIHelper.TryDebounce(ref _lastActionTimeMs))
        {
            state.DenyPendingBet(bet);
        }

        var localPlayer = _plugin.PartyManager.GetLocalPlayer();
        bool isSelf = localPlayer.HasValue
            && bet.PlayerName.Equals(localPlayer.Value.Name, StringComparison.OrdinalIgnoreCase)
            && bet.PlayerWorld.Equals(localPlayer.Value.World, StringComparison.OrdinalIgnoreCase);

        if (!isSelf)
        {
            ImGui.SameLine();
            if (ImGui.SmallButton($"Trade##trade_{bet.PlayerName}"))
            {
                if (bank != null && bank.Balance > 0)
                    ImGui.SetClipboardText(Math.Min(bank.Balance, 1000000).ToString());
                _plugin.TradeAction.InitiateTrade(bet.PlayerName);
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Auto copies bank to clipboard and trades with player.");
        }
    }

    private void DrawCurrentBetsTable(RaceState state)
    {
        var confirmedBets = state.Bets.Where(b => b.Status == BetStatus.Confirmed).ToList();
        if (confirmedBets.Count == 0) return;

        ImGui.TextColored(UiColors.Positive, "Current Bets");
        ImGui.Spacing();

        using var table = ImRaii.Table("BetsTable", 5, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp);
        if (!table) return;

        ImGui.TableSetupColumn("Player",  ImGuiTableColumnFlags.WidthStretch, 0.25f);
        ImGui.TableSetupColumn("Chocobo", ImGuiTableColumnFlags.WidthStretch, 0.15f);
        ImGui.TableSetupColumn("Amount",  ImGuiTableColumnFlags.WidthStretch, 0.25f);
        ImGui.TableSetupColumn("Status",  ImGuiTableColumnFlags.WidthStretch, 0.15f);
        ImGui.TableSetupColumn("Action",  ImGuiTableColumnFlags.WidthFixed, RemoveBetColumnWidth());
        ImGui.TableHeadersRow();

        foreach (var bet in confirmedBets)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text(bet.PlayerName);
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); UIHelper.TextColoredForChocobo(bet.ChocoboNumber, TrackVisualiser.GetChocoboName(state.Config, bet.ChocoboNumber));
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text(UIHelper.FormatGil(bet.Amount));
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.TextColored(UiColors.Positive, bet.Status.ToString());
            ImGui.TableNextColumn();
            DrawRemoveBetButton(state, bet);
        }
    }

    private static float RemoveBetColumnWidth() =>
        UIHelper.IconTextButtonWidth(FontAwesomeIcon.Trash, RemoveBetLabel)
            + ImGui.GetStyle().CellPadding.X * 2f;

    private void DrawRemoveBetButton(RaceState state, Bet bet)
    {
        if (state.Phase != RacePhase.Betting) return;

        using var id = ImRaii.PushId($"rm_{bet.PlayerName}_{bet.ChocoboNumber}_{bet.Amount}");

        bool isClicked;
        using (UIHelper.PushRedButtonColours())
            isClicked = UIHelper.IconTextButton(FontAwesomeIcon.Trash, RemoveBetLabel, "##remove_bet");

        if (!UIHelper.CtrlClickConfirmed(isClicked, "Hold Ctrl and Left Click to remove this bet.")) return;
        if (!UIHelper.TryDebounce(ref _lastActionTimeMs)) return;

        state.RemoveBet(bet);
        BroadcastBet(state, "BET REMOVED", bet.PlayerName, bet.ChocoboNumber, bet.Amount);
    }

    private void DrawWinnersTable(RaceState state)
    {
        if (state.Phase != RacePhase.Finished) return;

        var winningChocobo = _plugin.RaceManager.GetWinningChocobo();
        if (winningChocobo <= 0) return;

        var winningBets = state.Bets.Where(b => b.Status == BetStatus.Confirmed && b.ChocoboNumber == winningChocobo).ToList();
        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
        UIHelper.SectionHeader("Winners");

        if (winningBets.Count == 0)
        {
            ImGui.TextColored(UiColors.Muted, "No bets on the winning chocobo.");
            return;
        }

        using var table = ImRaii.Table("WinnersTable", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp);
        if (!table) return;

        ImGui.TableSetupColumn("Player",           ImGuiTableColumnFlags.WidthStretch, 0.25f);
        ImGui.TableSetupColumn("Bet",              ImGuiTableColumnFlags.WidthStretch, 0.25f);
        ImGui.TableSetupColumn("Amount Won",       ImGuiTableColumnFlags.WidthStretch, 0.25f);
        ImGui.TableSetupColumn("New Bank Balance", ImGuiTableColumnFlags.WidthStretch, 0.25f);
        ImGui.TableHeadersRow();

        foreach (var bet in winningBets)
        {
            var payout  = (long)(bet.Amount * state.GetPayoutMultiplierForWinner(winningChocobo));
            var bank    = state.GetBank(bet.PlayerName, bet.PlayerWorld);
            var balance = bank?.Balance ?? 0;

            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.Text(bet.PlayerName);
            ImGui.TableNextColumn(); ImGui.Text(UIHelper.FormatGil(bet.Amount));
            ImGui.TableNextColumn(); ImGui.TextColored(UiColors.Positive, UIHelper.FormatGil(payout));
            ImGui.TableNextColumn(); ImGui.Text(UIHelper.FormatGil(balance));
        }
    }
}
