using System;
using ChocoboRacing.Config;
using ChocoboRacing.Services;
using ChocoboRacing.UI.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

/// <summary>
/// Sounds tab: toggle, pick and preview the chat sound effect played when a runner joins the raffle.
/// </summary>
namespace ChocoboRacing.UI.Tabs;

public sealed class SoundsTab
{
    private static readonly string[] SoundEffectOptions = BuildSoundEffectOptions();

    private readonly Plugin _plugin;

    public SoundsTab(Plugin plugin)
    {
        _plugin = plugin;
    }

    public void Draw()
    {
        ImGui.Spacing();
        DrawJoinSoundSettings();
    }

    private void DrawJoinSoundSettings()
    {
        var config = _plugin.Configuration;

        ImGui.TextColored(UiColors.Gold, "Raffle Join Sound");
        ImGui.TextColored(UiColors.Subtle, "Plays when a runner types the join keyword in chat and is added to the raffle.");
        ImGui.Spacing();

        var enabled = config.RaffleJoinSoundEnabled;
        if (ImGui.Checkbox("Play sound when a runner joins via keyword", ref enabled))
        {
            config.RaffleJoinSoundEnabled = enabled;
            config.Save();
        }

        using (ImRaii.Disabled(!config.RaffleJoinSoundEnabled))
        {
            ImGui.Indent();
            DrawSoundPicker(config);
            ImGui.Unindent();
        }
    }

    private void DrawSoundPicker(PluginConfig config)
    {
        ImGui.Text("Sound:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(120f);

        var idx = Math.Clamp(config.RaffleJoinSoundEffectId, SoundService.MinSoundEffectId, SoundService.MaxSoundEffectId) - 1;
        using (var combo = ImRaii.Combo("##JoinSe", SoundEffectOptions[idx]))
            if (combo)
                DrawSoundOptions(config, idx);

        ImGui.SameLine();
        if (UIHelper.IconTextButton(FontAwesomeIcon.VolumeUp, "Test", "##TestJoinSound"))
            _plugin.SoundService.PlayChatSoundEffect(config.RaffleJoinSoundEffectId);
    }

    private static void DrawSoundOptions(PluginConfig config, int selectedIndex)
    {
        for (var i = 0; i < SoundEffectOptions.Length; i++)
        {
            if (!ImGui.Selectable(SoundEffectOptions[i], i == selectedIndex)) continue;
            config.RaffleJoinSoundEffectId = i + 1;
            config.Save();
        }
    }

    private static string[] BuildSoundEffectOptions()
    {
        var options = new string[SoundService.MaxSoundEffectId];
        for (var i = 0; i < options.Length; i++)
            options[i] = $"SE {i + 1}";
        return options;
    }
}
