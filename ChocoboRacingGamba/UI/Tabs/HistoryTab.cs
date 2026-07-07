using System;
using System.Linq;
using System.Numerics;
using ChocoboRacing.Models;
using ChocoboRacing.State;
using ChocoboRacing.UI.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

/// <summary>
/// Draws the host History tab: a per-session race log with individual round and bet records.
/// </summary>
namespace ChocoboRacing.UI.Tabs;

public sealed class HistoryTab
{
    private readonly Plugin _plugin;
    private int _historySessionIndex = 0;
    private Guid _pendingDeleteSessionId = Guid.Empty;
    private int _pendingDeleteRaceIndex = -1;
    private bool _openDeleteRacePopup = false;

    public HistoryTab(Plugin Plugin)
    {
        _plugin = Plugin;
    }

    public void Draw()
    {
        var sessions = _plugin.HistoryService.Sessions;
        if (sessions.Count == 0)
        {
            ImGui.TextColored(UiColors.Muted, "No history available.");
            return;
        }

        var sessionNames = sessions.Select(s => $"Session {s.StartTime:MMM dd HH:mm} ({s.Races.Count} Races)").ToArray();

        if (_historySessionIndex >= sessionNames.Length || _historySessionIndex < 0)
            _historySessionIndex = 0;

        ImGui.Combo("Session", ref _historySessionIndex, sessionNames, sessionNames.Length);
        ImGui.SameLine();

        using (UIHelper.PushRedButtonColours())
        {
            if (UIHelper.IconTextButton(FontAwesomeIcon.Trash, "Delete Session", "##del_session"))
            {
                _pendingDeleteSessionId = sessions[_historySessionIndex].SessionId;
                ImGui.OpenPopup("Delete Session?##crg_del_session");
            }
        }

        DrawDeleteSessionPopup();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        var selectedSession = sessions[_historySessionIndex];

        using var racesTable = ImRaii.Table("HistoryRacesTable", 7, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.PadOuterX);
        if (racesTable)
        {
            ImGui.TableSetupColumn("Mode",        ImGuiTableColumnFlags.WidthStretch, 0.16f);
            ImGui.TableSetupColumn("Round",       ImGuiTableColumnFlags.WidthStretch, 0.1f);
            ImGui.TableSetupColumn("Chocobos",    ImGuiTableColumnFlags.WidthStretch, 0.15f);
            ImGui.TableSetupColumn("Odds",        ImGuiTableColumnFlags.WidthStretch, 0.1f);
            ImGui.TableSetupColumn("Winner",      ImGuiTableColumnFlags.WidthStretch, 0.15f);
            ImGui.TableSetupColumn("Bets/Payouts",ImGuiTableColumnFlags.WidthStretch, 0.5f);
            ImGui.TableSetupColumn("Actions",     ImGuiTableColumnFlags.WidthFixed,   120f);
            ImGui.TableHeadersRow();

            for (var raceIndex = 0; raceIndex < selectedSession.Races.Count; raceIndex++)
            {
                var race = selectedSession.Races[raceIndex];
                ImGui.PushID(raceIndex);
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                if (race.Mode == RaceMode.GrandNational)
                    ImGui.TextColored(UiColors.Gold, "Grand National");
                else
                    ImGui.Text("Classic");
                ImGui.TableNextColumn(); ImGui.Text(race.RoundNumber.ToString());
                ImGui.TableNextColumn(); ImGui.Text(race.ChocoboCount.ToString());
                ImGui.TableNextColumn();
                if (race.Mode == RaceMode.GrandNational)
                    ImGui.TextDisabled("-");
                else
                    ImGui.Text($"{race.PayoutOdds:F2}x");
                ImGui.TableNextColumn();
                if (race.WinningChocobo > 0)
                    UIHelper.TextColoredForChocobo(race.WinningChocobo, $"#{race.WinningChocobo}");
                else
                    ImGui.Text("Void/None");
                ImGui.TableNextColumn();

                if (race.Bets.Count == 0)
                {
                    ImGui.TextColored(UiColors.Muted, "No bets");
                }
                else
                {
                    foreach (var bet in race.Bets)
                    {
                        if (bet.IsWinner)
                        {
                            ImGui.TextColored(UiColors.Positive, $"+ {bet.PlayerName} won {bet.Payout:N0} (on ");
                            ImGui.SameLine(0, 0);
                            UIHelper.TextColoredForChocobo(bet.ChocoboNumber, $"#{bet.ChocoboNumber})");
                        }
                        else
                        {
                            ImGui.TextColored(new Vector4(1f, 0.5f, 0.5f, 1f), $"- {bet.PlayerName} lost {bet.Amount:N0} (on ");
                            ImGui.SameLine(0, 0);
                            UIHelper.TextColoredForChocobo(bet.ChocoboNumber, $"#{bet.ChocoboNumber})");
                        }
                    }
                }

                ImGui.TableNextColumn();
                using (UIHelper.PushRedButtonColours())
                {
                    if (UIHelper.IconTextButton(FontAwesomeIcon.Trash, "Delete Round", $"##del_race_{raceIndex}"))
                    {
                        _pendingDeleteRaceIndex = raceIndex;
                        _openDeleteRacePopup = true;
                    }
                }

                ImGui.PopID();
            }
        }

        if (_openDeleteRacePopup)
        {
            ImGui.OpenPopup("Delete Round?##crg_del_race");
            _openDeleteRacePopup = false;
        }

        DrawDeleteRacePopup(selectedSession.SessionId);
    }

    private void DrawDeleteRacePopup(Guid sessionId)
    {
        using var popup = ImRaii.PopupModal("Delete Round?##crg_del_race", flags: ImGuiWindowFlags.AlwaysAutoResize);
        if (!popup) return;

        ImGui.TextColored(UiColors.Negative, "This data cannot be recovered.");
        ImGui.Text("Are you sure you want to delete this round?");
        ImGui.Spacing();

        if (UIHelper.IconTextButton(FontAwesomeIcon.Trash, "Delete", "##confirm_del_race"))
        {
            if (_pendingDeleteRaceIndex >= 0)
                _plugin.HistoryService.DeleteRaceAt(sessionId, _pendingDeleteRaceIndex);

            _pendingDeleteRaceIndex = -1;
            ImGui.CloseCurrentPopup();
        }

        ImGui.SameLine();

        if (UIHelper.IconTextButton(FontAwesomeIcon.Times, "Cancel", "##cancel_del_race"))
        {
            _pendingDeleteRaceIndex = -1;
            ImGui.CloseCurrentPopup();
        }
    }

    private void DrawDeleteSessionPopup()
    {
        using var popup = ImRaii.PopupModal("Delete Session?##crg_del_session", flags: ImGuiWindowFlags.AlwaysAutoResize);
        if (!popup) return;

        ImGui.TextColored(UiColors.Negative, "This data cannot be recovered.");
        ImGui.Text("Are you sure you want to delete this session?");
        ImGui.Spacing();

        if (UIHelper.IconTextButton(FontAwesomeIcon.Trash, "Delete", "##confirm_del_session"))
        {
            _plugin.HistoryService.DeleteSession(_pendingDeleteSessionId);
            _historySessionIndex = 0;
            _pendingDeleteSessionId = Guid.Empty;
            ImGui.CloseCurrentPopup();
        }

        ImGui.SameLine();

        if (UIHelper.IconTextButton(FontAwesomeIcon.Times, "Cancel", "##cancel_del_session"))
        {
            _pendingDeleteSessionId = Guid.Empty;
            ImGui.CloseCurrentPopup();
        }
    }
}
