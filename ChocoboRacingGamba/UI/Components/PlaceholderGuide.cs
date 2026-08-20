using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

/// <summary>
/// Reference tables explaining every chat message placeholder.
/// </summary>
namespace ChocoboRacing.UI.Components;

internal static class PlaceholderGuide
{
    private static readonly (string Token, string Inserts)[] ClassicTokens =
    {
        ("{chocobos}",       "How many chocobos are racing."),
        ("{chocobonames}",   "Every racing chocobo's name, one per line."),
        ("{distance}",       "The finish line distance for the race."),
        ("{odds}",           "The payout odds on a winning bet, such as 4.00."),
        ("{perfectodds}",    "The payout odds on a perfect race bet."),
        ("{betlist}",        "Every confirmed bet, grouped the way your Betlist Layout is set."),
        ("{racelist}",       "The track with each chocobo at its current position, one lane per line."),
        ("{winningchocobo}", "The name of the chocobo that won. Race Winner message only."),
        ("{winnerlist}",     "Each player who backed the winner and their winnings, one per line. Race Winner message only."),
        ("{bankvalue}",      "The player's bank balance. Tell Bank Balance message only."),
        ("{pin}",            "The player's web betting PIN. Tell Web PIN message only."),
        ("{url}",            "The link to your live race page."),
        ("{venue}",          "Your venue name from the Webview tab."),
    };

    private static readonly (string Token, string Inserts)[] RaffleTokens =
    {
        ("{prize}",      "The prize on offer: the current net pot, or your prize text when the prize is an item."),
        ("{entryfee}",   "The entry fee, or Free when the raffle is free to enter."),
        ("{keyword}",    "The chat join keyword. Blank while the keyword is switched off."),
        ("{runners}",    "How many runners are in, counting paid entries only unless the raffle is free."),
        ("{boostedpot}", "The gil you have added to the pot yourself as a host boost."),
        ("{closetime}",  "The closing time as HH:MM Server Time, or TBA when no closing time is set."),
        ("{timeleft}",   "How long is left until the raffle closes, or TBA when no closing time is set."),
        ("{name}",       "The winner in the Winner message, or the runner in a /tell. Blank in broadcasts."),
        ("{number}",     "The grid number of that same winner or runner. Blank in broadcasts."),
        ("{url}",        "The link to your live race page."),
        ("{venue}",      "Your venue name from the Webview tab."),
    };

    internal static void DrawClassic() => Draw("classic", ClassicTokens);

    internal static void DrawRaffle() => Draw("raffle", RaffleTokens);

    private static void Draw(string id, (string Token, string Inserts)[] tokens)
    {
        using var table = ImRaii.Table($"##placeholder_table_{id}", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp);
        if (!table) return;

        ImGui.TableSetupColumn("Placeholder", ImGuiTableColumnFlags.WidthStretch, 0.25f);
        ImGui.TableSetupColumn("Inserts",     ImGuiTableColumnFlags.WidthStretch, 0.75f);
        ImGui.TableHeadersRow();

        foreach (var (token, inserts) in tokens)
            DrawTokenRow(id, token, inserts);
    }

    private static void DrawTokenRow(string id, string token, string inserts)
    {
        ImGui.TableNextRow();

        ImGui.TableNextColumn();
        using (ImRaii.PushColor(ImGuiCol.Text, UiColors.Gold))
        {
            if (ImGui.Selectable($"{token}##copy_{id}_{token}"))
                ImGui.SetClipboardText(token);
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip($"Click to copy {token} to the clipboard.");

        ImGui.TableNextColumn();
        using (ImRaii.PushColor(ImGuiCol.Text, UiColors.Subtle))
            ImGui.TextWrapped(inserts);
    }
}
