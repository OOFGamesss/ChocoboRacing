using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ChocoboRacing.Config;
using ChocoboRacing.Models;
using ChocoboRacing.Services;
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
/// Draws the host Banks tab: active player bank management, balance adjustments, cash-out alerts, web PINs, and archived banks.
/// </summary>
namespace ChocoboRacing.UI.Tabs;

public sealed class BanksTab
{
    private const string CashOutBadge = "CASH OUT";
    private const string WidestBalance = "-999,999,999 Gil";
    private const float AdjustInputWidth = 92f;

    private static readonly Vector4 CashOutRowTint = new(0.55f, 0.30f, 0.05f, 0.35f);
    private static readonly Vector4 CashOutBadgeBg = new(0.85f, 0.45f, 0.05f, 1f);

    private readonly Plugin _plugin;
    private readonly Dictionary<string, int> _bankAdjustments = new();
    private bool _isTellingAll;

    public BanksTab(Plugin Plugin)
    {
        _plugin = Plugin;
    }

    public void Draw()
    {
        var state = _plugin.GameState;
        var localPlayer = _plugin.PartyManager.GetLocalPlayer();

        SyncBanksWithParty(state, localPlayer);

        DrawActivePlayers(state, localPlayer);
        ImGuiHelpers.ScaledDummy(8f);
        DrawArchivedPlayers(state, localPlayer);
    }

    private void SyncBanksWithParty(RaceState state, (string Name, string World)? localPlayer)
    {
        var partyMembers = _plugin.PartyManager.GetPartyMembers();

        foreach (var member in partyMembers)
            EnsureBankActive(state, member.Name, member.World);

        if (localPlayer.HasValue)
            EnsureBankActive(state, localPlayer.Value.Name, localPlayer.Value.World);

        foreach (var bank in state.GetBanksSnapshot())
        {
            if (bank.IsArchived) continue;
            if (IsLocalPlayerBank(bank, localPlayer)) continue;

            var stillInParty = partyMembers.Any(m =>
                m.Name.Equals(bank.Name, StringComparison.OrdinalIgnoreCase) &&
                m.World.Equals(bank.World, StringComparison.OrdinalIgnoreCase));

            if (!stillInParty)
                state.ArchiveBank(bank.Name, bank.World);
        }
    }

    private static void EnsureBankActive(RaceState state, string name, string world)
    {
        var bank = state.GetBank(name, world);
        if (bank == null)
            state.GetOrCreateBank(name, world);
        else if (bank.IsArchived)
            state.UnarchiveBank(name, world);
    }

    private static bool IsLocalPlayerBank(PlayerBank bank, (string Name, string World)? localPlayer)
    {
        return localPlayer.HasValue
            && bank.Name.Equals(localPlayer.Value.Name, StringComparison.OrdinalIgnoreCase)
            && bank.World.Equals(localPlayer.Value.World, StringComparison.OrdinalIgnoreCase);
    }

    private void DrawActivePlayers(RaceState state, (string Name, string World)? localPlayer)
    {
        var activeBanks = state.GetBanksSnapshot().Where(b => !b.IsArchived).ToList();
        var otherBanks = activeBanks.Where(b => !IsLocalPlayerBank(b, localPlayer)).ToList();

        DrawActiveHeader(activeBanks.Count, otherBanks);

        if (activeBanks.Count == 0)
        {
            ImGui.Spacing();
            ImGui.TextColored(UiColors.Muted, "No active party members with banks.");
            return;
        }

        DrawActiveTable(state, activeBanks, localPlayer);
    }

    private void DrawActiveHeader(int activeCount, List<PlayerBank> otherBanks)
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(UiColors.Gold, $"Active Players ({activeCount})");

        var label = _isTellingAll ? "Telling All..." : "Tell All Balances";
        var buttonWidth = UIHelper.IconTextButtonWidth(FontAwesomeIcon.CommentDots, label);
        var rightX = ImGui.GetContentRegionMax().X - buttonWidth;

        ImGui.SameLine();
        if (rightX > ImGui.GetCursorPosX())
            ImGui.SetCursorPosX(rightX);

        using (ImRaii.Disabled(_isTellingAll || otherBanks.Count == 0))
        using (UIHelper.PushBlueButtonColours())
            if (UIHelper.IconTextButton(FontAwesomeIcon.CommentDots, label, "##tell_bal_all"))
                StartTellAll(otherBanks);

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Sends every other player a /tell with their bank balance, one every 2.5 seconds.");

        ImGuiHelpers.ScaledDummy(4f);
    }

    private void StartTellAll(List<PlayerBank> otherBanks)
    {
        if (otherBanks.Count == 0) return;

        _isTellingAll = true;
        for (var i = 0; i < otherBanks.Count; i++)
        {
            var isLast = i == otherBanks.Count - 1;
            TellBankBalance(otherBanks[i], isLast ? () => _isTellingAll = false : null);
        }
    }

    private void DrawActiveTable(RaceState state, List<PlayerBank> activeBanks, (string Name, string World)? localPlayer)
    {
        var webActive = _plugin.Configuration.WebMirrorEnabled;
        var columnCount = webActive ? 5 : 4;
        const ImGuiTableFlags flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg
            | ImGuiTableFlags.SizingFixedFit;

        using var table = ImRaii.Table("ActiveBanks", columnCount, flags);
        if (!table) return;

        ImGui.TableSetupColumn("Player",  ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Balance", ImGuiTableColumnFlags.WidthFixed, BalanceColumnWidth());
        ImGui.TableSetupColumn("Adjust",  ImGuiTableColumnFlags.WidthFixed, AdjustColumnWidth());
        ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, ActionsColumnWidth());
        if (webActive)
            ImGui.TableSetupColumn("Web", ImGuiTableColumnFlags.WidthFixed, WebColumnWidth());
        ImGui.TableHeadersRow();

        var cashOutNames = state.GetActiveCashOutRequests();
        foreach (var bank in activeBanks)
            DrawActivePlayerRow(state, bank, cashOutNames, localPlayer, webActive);
    }

    private static float BalanceColumnWidth() =>
        ImGui.CalcTextSize(WidestBalance).X + ImGui.GetStyle().CellPadding.X * 2f;

    private static float AdjustColumnWidth()
    {
        var style = ImGui.GetStyle();
        var inputRow = AdjustInputWidth * ImGuiHelpers.GlobalScale
            + (ImGui.GetFrameHeight() + style.ItemSpacing.X) * 3f;
        return Math.Max(UIHelper.QuickAmountButtonsWidth(), inputRow) + style.CellPadding.X * 2f;
    }

    private static float ActionsColumnWidth()
    {
        var style = ImGui.GetStyle();
        return ImGui.GetFrameHeight() * 3f + style.ItemSpacing.X * 2f + style.CellPadding.X * 2f;
    }

    private static float WebColumnWidth()
    {
        var style = ImGui.GetStyle();
        var pinWidth = ImGui.CalcTextSize("0000").X + style.ItemSpacing.X;
        return pinWidth + ImGui.GetFrameHeight() * 2f + style.ItemSpacing.X + style.CellPadding.X * 2f;
    }

    private void DrawActivePlayerRow(RaceState state, PlayerBank bank, HashSet<string> cashOutNames, (string Name, string World)? localPlayer, bool webActive)
    {
        var key = $"{bank.Name}@{bank.World}";
        var isCashOut = cashOutNames.Contains(key);
        var isLocal = IsLocalPlayerBank(bank, localPlayer);

        using var id = ImRaii.PushId(key);
        ImGui.TableNextRow();

        if (isCashOut)
            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.ColorConvertFloat4ToU32(CashOutRowTint));

        ImGui.TableNextColumn();
        DrawPlayerCell(bank, isCashOut);

        ImGui.TableNextColumn();
        DrawBalanceCell(bank);

        ImGui.TableNextColumn();
        DrawAdjustCell(state, bank, key);

        ImGui.TableNextColumn();
        if (!isLocal)
            DrawActionsCell(state, bank);

        if (!webActive) return;

        ImGui.TableNextColumn();
        DrawWebCell(bank, isLocal);
    }

    private static void DrawPlayerCell(PlayerBank bank, bool isCashOut)
    {
        ImGui.AlignTextToFramePadding();
        ImGui.Text(bank.Name);

        if (isCashOut)
        {
            ImGui.SameLine();
            Badge.Draw(CashOutBadge, CashOutBadgeBg, new Vector4(1f, 1f, 1f, 1f));
        }

        ImGui.TextColored(UiColors.Muted, bank.World);
    }

    private static void DrawBalanceCell(PlayerBank bank)
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(UIHelper.SignColor(bank.Balance), UIHelper.FormatGil(bank.Balance));
    }

    private void DrawAdjustCell(RaceState state, PlayerBank bank, string key)
    {
        if (!_bankAdjustments.ContainsKey(key)) _bankAdjustments[key] = 0;
        var adj = _bankAdjustments[key];

        UIHelper.QuickAmountButtons(ref adj, key);
        _bankAdjustments[key] = adj;

        if (ImGuiEx.InputFancyNumeric(AdjustInputWidth * ImGuiHelpers.GlobalScale, "##adj", ref adj, 0))
            _bankAdjustments[key] = adj;

        ImGui.SameLine();
        using (UIHelper.PushGreenButtonColours())
            if (ImGuiComponents.IconButton("##add_bank", FontAwesomeIcon.Plus))
            {
                state.AdjustBalance(bank.Name, bank.World, _bankAdjustments[key]);
                _bankAdjustments[key] = 0;
            }
        SetTooltip("Add to Bank: adds the amount to this player's bank.");

        ImGui.SameLine();
        using (UIHelper.PushRedButtonColours())
            if (ImGuiComponents.IconButton("##take_off_bank", FontAwesomeIcon.Minus))
            {
                state.AdjustBalance(bank.Name, bank.World, -_bankAdjustments[key]);
                _bankAdjustments[key] = 0;
            }
        SetTooltip("Take off Bank: removes the amount from this player's bank.");

        ImGui.SameLine();
        using (UIHelper.PushPinkButtonColours())
            if (ImGuiComponents.IconButton("##keep_as_tip", FontAwesomeIcon.HandHoldingUsd))
                KeepAsTip(state, bank, key);
        SetTooltip("Keep as Tip: removes the amount from the player's bank and records it as a tip (not counted in profit/loss).");
    }

    private void KeepAsTip(RaceState state, PlayerBank bank, string key)
    {
        var tipAmount = _bankAdjustments[key];
        if (tipAmount <= 0) return;

        state.AdjustBalance(bank.Name, bank.World, -tipAmount);
        state.AddTip(tipAmount);
        _bankAdjustments[key] = 0;
    }

    private void DrawActionsCell(RaceState state, PlayerBank bank)
    {
        using (ImRaii.Disabled(_isTellingAll))
        {
            using (UIHelper.PushBlueButtonColours())
                if (ImGuiComponents.IconButton("##tell_bal", FontAwesomeIcon.CommentDots))
                    TellBankBalance(bank);
            SetTooltip("Tell Bank Balance: sends this player a /tell with their balance.");

            ImGui.SameLine();
            using (UIHelper.PushYellowButtonColours())
                if (ImGuiComponents.IconButton("##trade", FontAwesomeIcon.Handshake))
                    StartTrade(state, bank);
            SetTooltip("Trade: copies the bank value to the clipboard and opens a trade with this player.");

            ImGui.SameLine();
            DrawPayoutButton(state, bank);
        }
    }

    private void StartTrade(RaceState state, PlayerBank bank)
    {
        state.DismissCashOutRequest(bank.Name, bank.World);
        if (bank.Balance > 0) ImGui.SetClipboardText(Math.Min(bank.Balance, 1_000_000).ToString());
        _plugin.TradeAction.InitiateTrade(bank.Name);
    }

    private void DrawPayoutButton(RaceState state, PlayerBank bank)
    {
        var autoPayout = _plugin.AutoPayoutService;
        var isPayingThisBank = autoPayout.IsRunning && autoPayout.TargetName == bank.Name;

        if (isPayingThisBank)
        {
            using (UIHelper.PushRedButtonColours())
                if (ImGuiComponents.IconButton("##stop_payout", FontAwesomeIcon.Stop))
                    autoPayout.Stop();
            SetTooltip("Stop Payout: cancels the running auto payout.");
            return;
        }

        var isPayingOther = autoPayout.IsRunning;
        var noBalance = bank.Balance <= 0;

        using (ImRaii.Disabled(isPayingOther || noBalance))
        using (UIHelper.PushGreenButtonColours())
            if (ImGuiComponents.IconButton("##auto_payout", FontAwesomeIcon.Coins))
                StartPayout(state, bank, autoPayout);

        if (!ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) return;

        ImGui.SetTooltip(noBalance
            ? "Player has no balance to pay out."
            : isPayingOther
                ? "Another payout is already running."
                : "Auto Payout: repeatedly trades this player until their bank is cleared.");
    }

    private void StartPayout(RaceState state, PlayerBank bank, AutoPayoutService autoPayout)
    {
        state.DismissCashOutRequest(bank.Name, bank.World);
        autoPayout.Start(
            bank.Name,
            () => state.GetBank(bank.Name, bank.World)?.Balance ?? 0L,
            amount => state.AdjustBalance(bank.Name, bank.World, -amount));
    }

    private void DrawWebCell(PlayerBank bank, bool isLocalPlayer)
    {
        var cfg = _plugin.Configuration;
        var key = $"{bank.Name}@{bank.World}";
        cfg.WebPins.TryGetValue(key, out var pin);

        if (string.IsNullOrEmpty(pin))
        {
            using (UIHelper.PushGreenButtonColours())
                if (ImGuiComponents.IconButton("##mk_pin", FontAwesomeIcon.Key))
                    pin = AssignPin(cfg, key);
            SetTooltip("Generate PIN: creates a 6-digit PIN this player enters on the website to bet.");
        }
        else
        {
            ImGui.AlignTextToFramePadding();
            ImGui.TextColored(UiColors.Gold, pin);
            ImGui.SameLine();
            using (UIHelper.PushGreenButtonColours())
                if (ImGuiComponents.IconButton("##rot_pin", FontAwesomeIcon.Sync))
                    AssignPin(cfg, key);
            SetTooltip("New PIN: replaces this player's PIN with a fresh one.");
        }

        if (isLocalPlayer) return;

        ImGui.SameLine();
        using (UIHelper.PushBlueButtonColours())
            if (ImGuiComponents.IconButton("##send_pin", FontAwesomeIcon.PaperPlane))
            {
                if (string.IsNullOrEmpty(pin)) pin = AssignPin(cfg, key);
                TellWebPin(bank, pin);
            }
        SetTooltip("Send Code: sends the player a /tell with their PIN and the betting link.");
    }

    private static string AssignPin(PluginConfig cfg, string key)
    {
        var pin = PinGenerator.Generate(cfg.WebPins.Values);
        cfg.WebPins[key] = pin;
        cfg.Save();
        return pin;
    }

    private void TellBankBalance(PlayerBank bank, Action? onSent = null)
    {
        var msg = _plugin.Configuration.TellBankBalanceMessage.Replace("{bankvalue}", bank.Balance.ToString("N0"));
        _plugin.ChatQueue.Enqueue($"/tell {bank.Name}@{bank.World} {msg}", onSent: onSent);
    }

    private void TellWebPin(PlayerBank bank, string pin)
    {
        var cfg = _plugin.Configuration;
        var url = _plugin.WebMirror.SpectatorUrl;
        if (string.IsNullOrEmpty(url)) url = cfg.WebSpectatorUrl;
        var msg = cfg.TellWebPinMessage
            .Replace("{pin}", pin)
            .Replace("{url}", url ?? string.Empty);
        _plugin.ChatQueue.Enqueue($"/tell {bank.Name}@{bank.World} {msg}");
    }

    private void DrawArchivedPlayers(RaceState state, (string Name, string World)? localPlayer)
    {
        var archivedBanks = state.GetBanksSnapshot().Where(b => b.IsArchived).ToList();
        if (archivedBanks.Count == 0) return;

        if (!ImGui.CollapsingHeader($"Archived Players ({archivedBanks.Count})###ArchivedBanksHeader", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        ImGui.TextColored(UiColors.Subtle, "Players who left the party with a balance.");
        ImGui.Spacing();

        const ImGuiTableFlags flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg
            | ImGuiTableFlags.SizingFixedFit;

        using var table = ImRaii.Table("ArchivedBanks", 4, flags);
        if (!table) return;

        ImGui.TableSetupColumn("Player",  ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("World",   ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Balance", ImGuiTableColumnFlags.WidthFixed, BalanceColumnWidth());
        ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, ActionsColumnWidth());
        ImGui.TableHeadersRow();

        foreach (var bank in archivedBanks)
            DrawArchivedRow(state, bank, localPlayer);
    }

    private void DrawArchivedRow(RaceState state, PlayerBank bank, (string Name, string World)? localPlayer)
    {
        using var id = ImRaii.PushId($"arch_{bank.Name}@{bank.World}");
        ImGui.TableNextRow();

        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.Text(bank.Name);

        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(UiColors.Muted, bank.World);

        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(UiColors.Warning, UIHelper.FormatGil(bank.Balance));

        ImGui.TableNextColumn();
        if (!IsLocalPlayerBank(bank, localPlayer))
        {
            using (UIHelper.PushBlueButtonColours())
                if (ImGuiComponents.IconButton("##tell_arch", FontAwesomeIcon.CommentDots))
                    TellBankBalance(bank);
            SetTooltip("Tell Bank Balance: sends this player a /tell with their balance.");
            ImGui.SameLine();
        }

        using (UIHelper.PushPinkButtonColours())
            if (ImGuiComponents.IconButton("##tip_arch", FontAwesomeIcon.HandHoldingUsd))
            {
                if (bank.Balance > 0) state.AddTip(bank.Balance);
                state.DeleteBank(bank.Name, bank.World);
            }
        SetTooltip("Keep as Tip: records the entire balance as a tip and deletes the bank.");

        ImGui.SameLine();
        using (UIHelper.PushRedButtonColours())
            if (ImGuiComponents.IconButton("##del_arch", FontAwesomeIcon.Trash))
                state.DeleteBank(bank.Name, bank.World);
        SetTooltip("Delete: removes this bank and its balance.");
    }

    private static void SetTooltip(string text)
    {
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(text);
    }
}
