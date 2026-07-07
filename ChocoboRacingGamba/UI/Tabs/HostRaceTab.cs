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
using Dalamud.Interface.Utility.Raii;
using ECommons.ImGuiMethods;

/// <summary>
/// Draws the host Race tab: phase controls, bet placement, pending/confirmed bet tables, and winner display.
/// </summary>
namespace ChocoboRacing.UI.Tabs;

public sealed class HostRaceTab
{
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
        var ms            = _plugin.MessageSender;
        var p             = _plugin.PartyManager.GetChatPrefix();
        var isTestingMode = _plugin.IsTestingMode;
        var phase         = state.Phase;
        var isSending     = ms.IsSendingMessages;

        using (ImRaii.Disabled(isSending))
        {
            DrawControlSectionHeader("Race Info");
            if (UIHelper.IconTextButton(FontAwesomeIcon.Bullhorn, "Announce Rules", "##announce_rules_btn"))
                ms.SendMessage(p, _plugin.Configuration.AnnounceRulesMessage, state, isTestingMode);
            ImGui.SameLine();
            DrawRepostChocobosButton(state);

            ImGui.Spacing();
            DrawControlSectionHeader("Race Control");

            switch (phase)
            {
                case RacePhase.Idle:
                    DrawOpenBettingButton(state, p, phase, isTestingMode);
                    break;
                case RacePhase.Betting:
                    DrawLastBetsButton(state);
                    ImGui.SameLine();
                    DrawNoMoreBetsButton(state, p, phase, isTestingMode);
                    ImGui.SameLine();
                    DrawVoidRaceButton(state);
                    break;
                case RacePhase.BetsClosed:
                {
                    using var _ = UIHelper.PushGreenButtonColours();
                    if (UIHelper.IconTextButton(FontAwesomeIcon.FlagCheckered, "Start Race", "##start_race_btn"))
                    {
                        state.StartRacing();
                        ms.SendMessage(p, _plugin.Configuration.RaceStartedMessage, state, isTestingMode);
                    }
                    ImGui.SameLine();
                    DrawVoidRaceButton(state);
                    break;
                }
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
    }

    private static void DrawControlSectionHeader(string title) => UIHelper.SectionHeader(title);

    private void BroadcastBet(RaceState state, string tag, string playerName, int chocoboNumber, long amount)
    {
        var prefix      = _plugin.PartyManager.GetChatPrefix();
        var chocoboName = TrackVisualiser.GetChocoboName(state.Config, chocoboNumber);
        Actions.ChatAction.SendChatMessage(
            $"{prefix} [{tag}] {playerName}: {chocoboName} for {UIHelper.FormatGil(amount)}",
            Plugin.ChatGui, _plugin.IsTestingMode);
    }

    private void DrawOpenBettingButton(RaceState state, string p, RacePhase phase, bool isTestingMode)
    {
        if (phase != RacePhase.Idle) return;

        using var _ = UIHelper.PushGreenButtonColours();
        if (UIHelper.IconTextButton(FontAwesomeIcon.DoorOpen, "Open Betting", "##open_betting_btn"))
        {
            state.StartBetting();
            _plugin.MessageSender.SendMessage(p, _plugin.Configuration.OpenBettingMessage, state, isTestingMode);
        }
    }

    private void DrawNoMoreBetsButton(RaceState state, string p, RacePhase phase, bool isTestingMode)
    {
        if (phase != RacePhase.Betting) return;

        using var _ = UIHelper.PushGreenButtonColours();
        if (UIHelper.IconTextButton(FontAwesomeIcon.DoorClosed, "No More Bets", "##no_more_bets_btn"))
        {
            state.CloseBets();
            _plugin.MessageSender.SendMessage(p, _plugin.Configuration.NoMoreBetsMessage, state, isTestingMode);
        }
    }

    private void DrawLastBetsButton(RaceState state)
    {
        if (state.Phase != RacePhase.Betting) return;

        using var _ = UIHelper.PushYellowButtonColours();
        if (UIHelper.IconTextButton(FontAwesomeIcon.History, "Last Bets", "##last_bets_btn"))
        {
            var p = _plugin.PartyManager.GetChatPrefix();
            _plugin.MessageSender.SendMessage(p, _plugin.Configuration.LastBetsMessage, state, _plugin.IsTestingMode);
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
                    var customEmote = _plugin.Configuration.RandomLineMessage;
                    if (!string.IsNullOrWhiteSpace(customEmote))
                        Actions.ChatAction.SendChatMessage(customEmote);

                    _plugin.ChatHandler.ExpectLocalRoll();
                    Actions.ChatAction.SendChatMessage(_plugin.PartyManager.GetDiceCommand(state.ChocoboCount));
                }
            }
        }
    }

    private void DrawRepostChocobosButton(RaceState state)
    {
        if (state.Phase != RacePhase.Betting) return;

        if (UIHelper.IconTextButton(FontAwesomeIcon.Sync, "Repost Chocobos", "##repost_chocobos_btn"))
        {
            var p = _plugin.PartyManager.GetChatPrefix();
            _plugin.MessageSender.SendMessage(p, "Place your bets: '[Chocobo Number or Name] [Amount]'\n{chocobonames}", state, _plugin.IsTestingMode);
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
            _plugin.MessageSender.SendMessage(_plugin.PartyManager.GetChatPrefix(), _plugin.Configuration.VoidRaceMessage, state, _plugin.IsTestingMode);
        }
    }

    private void DrawPlaceBetsSection(RaceState state)
    {
        ImGui.TextColored(UiColors.Accent, "Place Bets");
        ImGui.Spacing();

        var activeBanks  = state.GetBanksSnapshot().Where(b => !b.IsArchived).ToList();
        var playerNames  = activeBanks.Select(b => $"{b.Name} ({UIHelper.FormatGil(b.Balance)})").ToArray();

        if (playerNames.Length == 0)
        {
            ImGui.TextColored(UiColors.Muted, "No players with banks available.");
            return;
        }

        ImGui.Text("Player:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(250);
        ImGui.Combo("##BetPlayer", ref _betPlayerIndex, playerNames, playerNames.Length);
        ImGui.Spacing();

        const float MinColWidth = 220f;
        var colCount = Math.Clamp((int)(ImGui.GetContentRegionAvail().X / MinColWidth), 2, state.ChocoboCount);

        using (var grid = ImRaii.Table("PlaceBetsGrid", colCount, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.PadOuterX | ImGuiTableFlags.SizingStretchSame))
        {
            if (grid)
            {
                for (var c = 0; c < colCount; c++)
                    ImGui.TableSetupColumn($"##col{c}", ImGuiTableColumnFlags.WidthStretch);
                for (var i = 1; i <= state.ChocoboCount; i++)
                {
                    ImGui.TableNextColumn();
                    DrawChocoboBetInput(state, activeBanks, i);
                }
            }
        }
        ImGui.Spacing();
    }

    private void DrawChocoboBetInput(RaceState state, List<PlayerBank> activeBanks, int i)
    {
        if (!_betAmountsPerChocobo.ContainsKey(i)) _betAmountsPerChocobo[i] = 0;
        var amt = _betAmountsPerChocobo[i];

        var existingBets = state.GetBetsForChocobo(i);
        var betInfo      = existingBets.Count > 0
            ? string.Join("\n", existingBets.Select(b => $"{b.PlayerName}: {UIHelper.FormatGil(b.Amount)}"))
            : "No bets";

        UIHelper.TextColoredForChocobo(i, TrackVisualiser.GetChocoboName(state.Config, i));
        using (new ImRaii.ColorDisposable().Push(ImGuiCol.Text, UiColors.Muted))
            ImGui.TextWrapped(betInfo);

        UIHelper.QuickAmountButtons(ref amt, i.ToString());
        _betAmountsPerChocobo[i] = amt;

        var betButtonWidth = ImGui.CalcTextSize("Place Bet").X + ImGui.GetStyle().FramePadding.X * 4;
        var inputWidth     = ImGui.GetContentRegionAvail().X - betButtonWidth - ImGui.GetStyle().ItemSpacing.X;
        if (ImGuiEx.InputFancyNumeric(Math.Max(inputWidth, 50), $"##bet_amt_{i}", ref amt, 0))
        {
            if (amt < 0) amt = 0;
            _betAmountsPerChocobo[i] = amt;
        }
        ImGui.SameLine();

        using (ImRaii.Disabled(amt <= 0 || _betPlayerIndex < 0 || _betPlayerIndex >= activeBanks.Count))
        {
            if (ImGui.SmallButton($"Place Bet##place_{i}"))
            {
                var bank    = activeBanks[_betPlayerIndex];
                var toPlace = Math.Min((long)amt, bank.Balance);
                if (toPlace > 0)
                {
                    state.AddBet(bank.Name, bank.World, i, toPlace);
                    BroadcastBet(state, "BET CONFIRMED", bank.Name, i, toPlace);
                }
                _betAmountsPerChocobo[i] = 0;
            }
        }
        ImGui.Spacing();
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
        ImGui.TableSetupColumn("Action",  ImGuiTableColumnFlags.WidthStretch, 0.20f);
        ImGui.TableHeadersRow();

        foreach (var bet in confirmedBets)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.Text(bet.PlayerName);
            ImGui.TableNextColumn(); UIHelper.TextColoredForChocobo(bet.ChocoboNumber, TrackVisualiser.GetChocoboName(state.Config, bet.ChocoboNumber));
            ImGui.TableNextColumn(); ImGui.Text(UIHelper.FormatGil(bet.Amount));
            ImGui.TableNextColumn(); ImGui.TextColored(UiColors.Positive, bet.Status.ToString());
            ImGui.TableNextColumn();

            if (state.Phase == RacePhase.Betting)
            {
                bool isClicked = ImGui.SmallButton($"Remove##rm_{bet.PlayerName}_{bet.Amount}");

                if (UIHelper.CtrlClickConfirmed(isClicked, "Hold Ctrl and Left Click to remove this bet.") && UIHelper.TryDebounce(ref _lastActionTimeMs))
                {
                    state.RemoveBet(bet);
                    BroadcastBet(state, "BET REMOVED", bet.PlayerName, bet.ChocoboNumber, bet.Amount);
                }
            }
        }
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
