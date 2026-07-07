using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ChocoboRacing.Models;
using ChocoboRacing.State;
using ChocoboRacing.UI.Components;
using ChocoboRacing.Utility;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using ECommons.ImGuiMethods;

/// <summary>
/// Draws the host Banks tab: active player bank management, balance adjustments, cash-out alerts, and archived banks.
/// </summary>
namespace ChocoboRacing.UI.Tabs;

public sealed class BanksTab
{
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
            {
                state.ArchiveBank(bank.Name, bank.World);
            }
        }

        DrawActivePlayers(state, localPlayer);
        ImGui.Spacing();
        DrawArchivedPlayers(state, localPlayer);
    }

    private static void EnsureBankActive(RaceState state, string name, string world)
    {
        var bank = state.GetBank(name, world);
        if (bank == null)
            state.GetOrCreateBank(name, world);
        else if (bank.IsArchived)
            state.UnarchiveBank(name, world);
    }

    private void TellBankBalance(PlayerBank bank)
    {
        var msg = _plugin.Configuration.TellBankBalanceMessage.Replace("{bankvalue}", bank.Balance.ToString("N0"));
        Actions.ChatAction.SendChatMessage($"/tell {bank.Name}@{bank.World} {msg}");
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
        var cashOutNames = state.GetActiveCashOutRequests();

        var contentStartX = ImGui.GetCursorPosX();
        var totalWidth = ImGui.GetContentRegionAvail().X;

        ImGui.TextColored(UiColors.Gold, "Active Players");

        if (activeBanks.Count == 0)
        {
            ImGui.Spacing();
            ImGui.TextColored(UiColors.Muted, "No active party members with banks.");
            return;
        }

        var otherBanks = activeBanks.Where(b => !IsLocalPlayerBank(b, localPlayer)).ToList();
        var canTellAll = !_isTellingAll && otherBanks.Count > 0;

        ImGui.SameLine();
        ImGui.SetCursorPosX(contentStartX + totalWidth * 0.75f);
        using (ImRaii.Disabled(!canTellAll))
        using (UIHelper.PushBlueButtonColours())
        {
            if (UIHelper.IconTextButton(FontAwesomeIcon.CommentDots, _isTellingAll ? "Telling All..." : "Tell Bank Balance All", "##tell_bal_all"))
            {
                _isTellingAll = true;
                for (var i = 0; i < otherBanks.Count; i++)
                {
                    var bank = otherBanks[i];
                    _plugin.ActionQueue.ScheduleDelayedAction(i * 2500L, () => TellBankBalance(bank));
                }
                _plugin.ActionQueue.ScheduleDelayedAction((otherBanks.Count - 1) * 2500L + 500, () => _isTellingAll = false);
            }
        }

        var webActive = _plugin.Configuration.WebMirrorEnabled;
        var columnCount = webActive ? 5 : 4;

        using var activeTable = ImRaii.Table("ActiveBanks", columnCount, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.Resizable);
        if (activeTable)
        {
            ImGui.TableSetupColumn("Player",  ImGuiTableColumnFlags.WidthStretch, 0.20f);
            ImGui.TableSetupColumn("Balance", ImGuiTableColumnFlags.WidthStretch, 0.15f);
            ImGui.TableSetupColumn("Adjust",  ImGuiTableColumnFlags.WidthStretch, 0.40f);
            ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthStretch, 0.25f);
            if (webActive)
                ImGui.TableSetupColumn("Web Actions", ImGuiTableColumnFlags.WidthStretch, 0.25f);
            ImGui.TableHeadersRow();
            foreach (var bank in activeBanks)
                DrawActivePlayerRow(state, bank, cashOutNames, localPlayer, webActive);
        }
    }

    private void DrawActivePlayerRow(RaceState state, PlayerBank bank, HashSet<string> cashOutNames, (string Name, string World)? localPlayer, bool webActive)
    {
        var key = $"{bank.Name}@{bank.World}";
        if (!_bankAdjustments.ContainsKey(key)) _bankAdjustments[key] = 0;

        ImGui.PushID(key);
        ImGui.TableNextRow();

        ImGui.TableNextColumn();
        ImGui.Text(bank.Name);
        ImGui.TextColored(UiColors.Muted, bank.World);

        if (cashOutNames.Contains($"{bank.Name}@{bank.World}"))
        {
            var pulse = (float)(0.5 + 0.5 * Math.Sin(ImGui.GetTime() * 5.0));
            ImGui.TextColored(new Vector4(1f, 0.7f + 0.3f * pulse, 0f, 0.6f + 0.4f * pulse), "Wants to cash out!");
        }

        ImGui.TableNextColumn();
        var balanceColor = bank.Balance >= 0 ? UiColors.Positive : UiColors.Negative;
        ImGui.TextColored(balanceColor, UIHelper.FormatGil(bank.Balance));

        ImGui.TableNextColumn();
        var adj = _bankAdjustments[key];

        UIHelper.QuickAmountButtons(ref adj, key);
        _bankAdjustments[key] = adj;

        if (ImGuiEx.InputFancyNumeric(ImGui.GetContentRegionAvail().X * 0.35f, "##adj", ref adj, 0)) _bankAdjustments[key] = adj;
        ImGui.SameLine();
        
        using (UIHelper.PushGreenButtonColours())
        {
            if (ImGuiComponents.IconButton("##add_bank", FontAwesomeIcon.Plus))
            {
                state.AdjustBalance(bank.Name, bank.World, _bankAdjustments[key]);
                _bankAdjustments[key] = 0;
            }
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Add to Bank: adds the amount to this player's bank.");
        ImGui.SameLine();
        using (UIHelper.PushRedButtonColours())
        {
            if (ImGuiComponents.IconButton("##take_off_bank", FontAwesomeIcon.Minus))
            {
                state.AdjustBalance(bank.Name, bank.World, -_bankAdjustments[key]);
                _bankAdjustments[key] = 0;
            }
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Take off Bank: removes the amount from this player's bank.");
        ImGui.SameLine();
        using (UIHelper.PushPinkButtonColours())
        {
            if (ImGuiComponents.IconButton("##keep_as_tip", FontAwesomeIcon.HandHoldingUsd))
            {
                var tipAmount = _bankAdjustments[key];
                if (tipAmount > 0)
                {
                    state.AdjustBalance(bank.Name, bank.World, -tipAmount);
                    state.AddTip(tipAmount);
                    _bankAdjustments[key] = 0;
                }
            }
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Keep as Tip: removes the amount from the player's bank and records it as a tip (not counted in profit/loss).");

        ImGui.TableNextColumn();
        var isLocal = IsLocalPlayerBank(bank, localPlayer);
        if (!isLocal)
        {
            using (ImRaii.Disabled(_isTellingAll))
            {
                using (UIHelper.PushBlueButtonColours())
                    if (UIHelper.IconTextButton(FontAwesomeIcon.CommentDots, "Tell Bank Balance", "##tell_bal"))
                        TellBankBalance(bank);
                if (UIHelper.IconTextButton(FontAwesomeIcon.Handshake, "Trade", "##trade"))
                {
                    state.DismissCashOutRequest(bank.Name, bank.World);
                    if (bank.Balance > 0) ImGui.SetClipboardText(Math.Min(bank.Balance, 1000000).ToString());
                    _plugin.TradeAction.InitiateTrade(bank.Name);
                }

                var autoPayout = _plugin.AutoPayoutService;
                var isPayingThisBank = autoPayout.IsRunning && autoPayout.TargetName == bank.Name;
                var isPayingOther = autoPayout.IsRunning && !isPayingThisBank;

                if (isPayingThisBank)
                {
                    using (UIHelper.PushRedButtonColours())
                    {
                        if (UIHelper.IconTextButton(FontAwesomeIcon.Stop, "Stop Payout", "##stop_payout"))
                            autoPayout.Stop();
                    }
                }
                else
                {
                    using (ImRaii.Disabled(isPayingOther || bank.Balance <= 0))
                    using (UIHelper.PushGreenButtonColours())
                    {
                        if (UIHelper.IconTextButton(FontAwesomeIcon.Coins, "Auto Payout", "##auto_payout"))
                        {
                            state.DismissCashOutRequest(bank.Name, bank.World);
                            autoPayout.Start(
                                bank.Name,
                                () => state.GetBank(bank.Name, bank.World)?.Balance ?? 0L,
                                amount => state.AdjustBalance(bank.Name, bank.World, -amount));
                        }
                    }
                    if (ImGui.IsItemHovered() && bank.Balance <= 0)
                        ImGui.SetTooltip("Player has no balance to pay out.");
                }
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Auto copies bank to clipboard and trades with player.");
        }

        if (webActive)
        {
            ImGui.TableNextColumn();
            DrawWebPin(bank, isLocal);
        }

        ImGui.PopID();
    }

    private void DrawWebPin(PlayerBank bank, bool isLocalPlayer)
    {
        var cfg = _plugin.Configuration;
        if (!cfg.WebMirrorEnabled) return;

        var key = $"{bank.Name}@{bank.World}";
        cfg.WebPins.TryGetValue(key, out var pin);

        if (string.IsNullOrEmpty(pin))
        {
            using (UIHelper.PushGreenButtonColours())
                if (UIHelper.IconTextButton(FontAwesomeIcon.Key, "Generate PIN Code", "##mk_pin"))
                {
                    pin = PinGenerator.Generate(cfg.WebPins.Values);
                    cfg.WebPins[key] = pin;
                    cfg.Save();
                }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Generates a 6-digit PIN this player enters on the website to bet.");
        }
        else
        {
            ImGui.TextColored(UiColors.Gold, $"Web PIN active: {pin}");
            using (UIHelper.PushGreenButtonColours())
                if (UIHelper.IconTextButton(FontAwesomeIcon.Sync, "New PIN", "##rot_pin"))
            {
                pin = PinGenerator.Generate(cfg.WebPins.Values);
                cfg.WebPins[key] = pin;
                cfg.Save();
            }
        }

        if (!isLocalPlayer)
        {
            using (UIHelper.PushBlueButtonColours())
                if (UIHelper.IconTextButton(FontAwesomeIcon.PaperPlane, "Send Code", "##send_pin"))
                {
                    if (string.IsNullOrEmpty(pin))
                    {
                        pin = PinGenerator.Generate(cfg.WebPins.Values);
                        cfg.WebPins[key] = pin;
                        cfg.Save();
                    }
                    TellWebPin(bank, pin);
                }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Sends the player a /tell with their PIN and the betting link.");
        }
    }

    private void TellWebPin(PlayerBank bank, string pin)
    {
        var cfg = _plugin.Configuration;
        var url = _plugin.WebMirror.SpectatorUrl;
        if (string.IsNullOrEmpty(url)) url = cfg.WebSpectatorUrl;
        var msg = cfg.TellWebPinMessage
            .Replace("{pin}", pin)
            .Replace("{url}", url ?? string.Empty);
        Actions.ChatAction.SendChatMessage($"/tell {bank.Name}@{bank.World} {msg}");
    }

    private void DrawArchivedPlayers(RaceState state, (string Name, string World)? localPlayer)
    {
        var archivedBanks = state.GetBanksSnapshot().Where(b => b.IsArchived).ToList();
        if (archivedBanks.Count == 0) return;

        ImGui.TextColored(UiColors.Subtle, "Archived Players (left party with balance)");
        ImGui.Spacing();

        using var archivedTable = ImRaii.Table("ArchivedBanks", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp);
        if (!archivedTable) return;

        ImGui.TableSetupColumn("Player",  ImGuiTableColumnFlags.WidthStretch, 0.3f);
        ImGui.TableSetupColumn("World",   ImGuiTableColumnFlags.WidthStretch, 0.2f);
        ImGui.TableSetupColumn("Balance", ImGuiTableColumnFlags.WidthStretch, 0.2f);
        ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthStretch, 0.3f);
        ImGui.TableHeadersRow();

        foreach (var bank in archivedBanks)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.Text(bank.Name);
            ImGui.TableNextColumn(); ImGui.Text(bank.World);
            ImGui.TableNextColumn(); ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.2f, 1f), UIHelper.FormatGil(bank.Balance));
            ImGui.TableNextColumn();
            using (UIHelper.PushRedButtonColours())
            {
                if (UIHelper.IconTextButton(FontAwesomeIcon.Trash, "Delete", $"##del_{bank.Name}@{bank.World}"))
                    state.DeleteBank(bank.Name, bank.World);
            }
            if (!IsLocalPlayerBank(bank, localPlayer))
            {
                ImGui.SameLine();
                using (UIHelper.PushBlueButtonColours())
                    if (UIHelper.IconTextButton(FontAwesomeIcon.CommentDots, "Tell Bank Balance", $"##tell_arch_{bank.Name}@{bank.World}"))
                        TellBankBalance(bank);
            }
            ImGui.SameLine();
            using (UIHelper.PushPinkButtonColours())
            {
                if (UIHelper.IconTextButton(FontAwesomeIcon.HandHoldingUsd, "Keep as Tip", $"##tip_arch_{bank.Name}@{bank.World}"))
                {
                    if (bank.Balance > 0)
                        state.AddTip(bank.Balance);
                    state.DeleteBank(bank.Name, bank.World);
                }
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Records entire balance as a tip and deletes the bank.");
        }
    }
}
