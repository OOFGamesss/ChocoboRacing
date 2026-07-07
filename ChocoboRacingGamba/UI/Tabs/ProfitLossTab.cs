using System.Collections.Generic;
using System.Linq;
using ChocoboRacing.Config;
using ChocoboRacing.Models;
using ChocoboRacing.UI.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

/// <summary>
/// Draws the host Profit/Loss tab: session and all-time summaries with per-player breakdowns.
/// </summary>
namespace ChocoboRacing.UI.Tabs;

public sealed class ProfitLossTab
{
    private readonly Plugin _plugin;

    public ProfitLossTab(Plugin Plugin)
    {
        _plugin = Plugin;
    }

    public void Draw()
    {
        var sessions = _plugin.HistoryService.Sessions;

        long totalBetsAllTime = 0;
        long totalPayoutsAllTime = 0;
        long totalBetsCurrent = 0;
        long totalPayoutsCurrent = 0;
        long tipsAllTime = 0;
        long tipsCurrent = 0;
        var allTimePlayers = new Dictionary<string, (string World, long Bets, long Payouts)>();

        SessionRecord? currentSession = sessions.Count > 0 ? sessions[0] : null;

        foreach (var s in sessions)
        {
            tipsAllTime += s.Tips;
            if (s == currentSession) tipsCurrent = s.Tips;

            foreach (var r in s.Races)
                foreach (var b in r.Bets)
                {
                    totalBetsAllTime += b.Amount;
                    totalPayoutsAllTime += b.Payout;
                    AccumulatePlayer(allTimePlayers, b);

                    if (s == currentSession)
                    {
                        totalBetsCurrent += b.Amount;
                        totalPayoutsCurrent += b.Payout;
                    }
                }
        }

        long profitAllTime = totalBetsAllTime - totalPayoutsAllTime;
        long profitCurrent = totalBetsCurrent - totalPayoutsCurrent;

        var localPlayer = _plugin.PartyManager.GetLocalPlayer();
        var localKey = localPlayer.HasValue ? $"{localPlayer.Value.Name}@{localPlayer.Value.World}" : null;

        UIHelper.SectionHeader("Profit / Loss (Host Perspective)");

        DrawSummaryTable(_plugin.Configuration, totalBetsCurrent, totalPayoutsCurrent, profitCurrent, tipsCurrent, totalBetsAllTime, totalPayoutsAllTime, profitAllTime, tipsAllTime);

        ImGui.Spacing();
        DrawPlayerBreakdown("All Time - Player Breakdown", allTimePlayers, "PLAllTimePlayers", localKey);

        foreach (var (session, index) in sessions.Select((s, i) => (s, i)))
        {
            var sessionPlayers = new Dictionary<string, (string World, long Bets, long Payouts)>();
            foreach (var r in session.Races)
                foreach (var b in r.Bets)
                    AccumulatePlayer(sessionPlayers, b);

            int sessionNumber = sessions.Count - index;
            var dateLabel = session.StartTime.ToString("dd MMM yyyy HH:mm");
            var suffix = session == currentSession ? " (Current)" : string.Empty;
            var label = $"Session {sessionNumber} - {dateLabel}{suffix}##PLSession{index}";

            ImGui.Spacing();
            DrawPlayerBreakdown(label, sessionPlayers, $"PLSession{index}Players", localKey);
        }
    }

    private static void AccumulatePlayer(Dictionary<string, (string World, long Bets, long Payouts)> map, BetRecord b)
    {
        var key = $"{b.PlayerName}@{b.PlayerWorld}";
        map.TryGetValue(key, out var existing);
        map[key] = (b.PlayerWorld, existing.Bets + b.Amount, existing.Payouts + b.Payout);
    }

    private static void DrawSummaryTable(PluginConfig config, long betsCurrent, long payoutsCurrent, long profitCurrent, long tipsCurrent, long betsAllTime, long payoutsAllTime, long profitAllTime, long tipsAllTime)
    {
        using var table = ImRaii.Table("PLTable", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg);
        if (!table) return;

        ImGui.TableSetupColumn("Metric", ImGuiTableColumnFlags.WidthStretch, 0.4f);
        ImGui.TableSetupColumn("Value",  ImGuiTableColumnFlags.WidthStretch, 0.6f);
        ImGui.TableHeadersRow();

        ImGui.TableNextRow();
        ImGui.TableNextColumn(); ImGui.Text("Current Session Total Bets Accepted");
        ImGui.TableNextColumn(); ImGui.Text(UIHelper.FormatGil(betsCurrent));

        ImGui.TableNextRow();
        ImGui.TableNextColumn(); ImGui.Text("Current Session Total Payouts");
        ImGui.TableNextColumn(); ImGui.Text(UIHelper.FormatGil(payoutsCurrent));

        ImGui.TableNextRow();
        ImGui.TableNextColumn(); ImGui.Text("Current Session Profit / Loss");
        ImGui.TableNextColumn();
        UIHelper.DrawSignedGil(profitCurrent);

        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.Text("Venue Cut");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(70f);
        var venuePercent = config.VenueCutPercent;
        if (ImGui.InputFloat("%##VenueCut", ref venuePercent, 0.0f, 0.0f, "%.1f"))
        {
            config.VenueCutPercent = System.Math.Clamp(venuePercent, 0.0f, 100.0f);
            config.Save();
        }
        ImGui.TableNextColumn();
        UIHelper.DrawSignedGil((long)(profitCurrent * config.VenueCutPercent / 100.0f));

        ImGui.TableNextRow();
        ImGui.TableNextColumn(); ImGui.TextColored(UiColors.Info, "Current Session Tips");
        ImGui.TableNextColumn(); ImGui.TextColored(UiColors.Info, UIHelper.FormatGil(tipsCurrent));

        ImGui.TableNextRow();
        ImGui.TableNextColumn(); ImGui.Text("---");
        ImGui.TableNextColumn(); ImGui.Text("---");

        ImGui.TableNextRow();
        ImGui.TableNextColumn(); ImGui.Text("All Time Total Bets Accepted");
        ImGui.TableNextColumn(); ImGui.Text(UIHelper.FormatGil(betsAllTime));

        ImGui.TableNextRow();
        ImGui.TableNextColumn(); ImGui.Text("All Time Total Payouts");
        ImGui.TableNextColumn(); ImGui.Text(UIHelper.FormatGil(payoutsAllTime));

        ImGui.TableNextRow();
        ImGui.TableNextColumn(); ImGui.Text("All Time Profit / Loss");
        ImGui.TableNextColumn();
        UIHelper.DrawSignedGil(profitAllTime);

        ImGui.TableNextRow();
        ImGui.TableNextColumn(); ImGui.TextColored(UiColors.Info, "All Time Tips");
        ImGui.TableNextColumn(); ImGui.TextColored(UiColors.Info, UIHelper.FormatGil(tipsAllTime));
    }

    private static void DrawPlayerBreakdown(string label, Dictionary<string, (string World, long Bets, long Payouts)> players, string tableId, string? excludeKey)
    {
        if (!ImGui.CollapsingHeader(label))
            return;

        var filtered = players.Where(p => !string.Equals(p.Key, excludeKey, System.StringComparison.OrdinalIgnoreCase)).ToList();

        if (filtered.Count == 0)
        {
            ImGui.TextDisabled("No player data available.");
            return;
        }

        using var breakdownTable = ImRaii.Table(tableId, 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp);
        if (!breakdownTable) return;

        ImGui.TableSetupColumn("Player",  ImGuiTableColumnFlags.WidthStretch, 0.35f);
        ImGui.TableSetupColumn("Bets",    ImGuiTableColumnFlags.WidthStretch, 0.20f);
        ImGui.TableSetupColumn("Payouts", ImGuiTableColumnFlags.WidthStretch, 0.20f);
        ImGui.TableSetupColumn("P / L",   ImGuiTableColumnFlags.WidthStretch, 0.25f);
        ImGui.TableHeadersRow();

        foreach (var (key, (world, bets, payouts)) in filtered.OrderBy(p => p.Key.Split('@')[0], System.StringComparer.OrdinalIgnoreCase))
        {
            var playerName = key.Split('@')[0];
            long pl = bets - payouts;

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.Text(playerName);
            ImGui.TextColored(UiColors.Muted, world);
            ImGui.TableNextColumn(); ImGui.Text($"{bets:N0}");
            ImGui.TableNextColumn(); ImGui.Text($"{payouts:N0}");
            ImGui.TableNextColumn();
            UIHelper.DrawSignedGil(pl, includeGilSuffix: false);
        }
    }
}
