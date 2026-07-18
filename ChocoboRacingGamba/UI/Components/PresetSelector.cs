using System;
using System.Collections.Generic;
using System.Linq;
using ChocoboRacing.Config;
using ChocoboRacing.Models;
using ChocoboRacing.State;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

/// <summary>
/// Centred preset row: switch, add, rename and delete the named host settings presets.
/// </summary>
namespace ChocoboRacing.UI.Components;

public sealed class PresetSelector
{
    private const float ComboWidth = 220f;

    private string _renameBuffer = string.Empty;

    public int Revision { get; private set; }

    public void Draw(RaceState state, PluginConfig config)
    {
        var presets = config.SettingsPresets;
        if (presets == null || presets.Count == 0)
            return;

        var presetLocked = state.Phase != RacePhase.Idle;
        if (presetLocked)
        {
            ImGui.TextColored(UiColors.Negative, "Switch or edit presets when the race is idle.");
            ImGui.BeginDisabled();
        }

        CentreRow();
        DrawCombo(state, config, presets);
        DrawButtons(state, config, presets, presetLocked);
        DrawRenamePopup(config, presets);

        if (presetLocked)
            ImGui.EndDisabled();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
    }

    private static void CentreRow()
    {
        var style = ImGui.GetStyle();

        ImGui.PushFont(UiBuilder.IconFont);
        var plusIconW = ImGui.CalcTextSize(FontAwesomeIcon.Plus.ToIconString()).X;
        var penIconW = ImGui.CalcTextSize(FontAwesomeIcon.Pen.ToIconString()).X;
        var trashIconW = ImGui.CalcTextSize(FontAwesomeIcon.Trash.ToIconString()).X;
        ImGui.PopFont();

        var sp = style.ItemSpacing.X;
        var pad = style.FramePadding.X * 2;
        var innerSp = style.ItemInnerSpacing.X;
        var labelW = ImGui.CalcTextSize("Preset").X;
        var addBtnW = pad + plusIconW + innerSp + ImGui.CalcTextSize("Add").X;
        var renameBtnW = pad + penIconW + innerSp + ImGui.CalcTextSize("Rename").X;
        var deleteBtnW = pad + trashIconW + innerSp + ImGui.CalcTextSize("Delete").X;
        var totalW = labelW + sp + ComboWidth + sp + addBtnW + sp + renameBtnW + sp + deleteBtnW;

        ImGui.SetCursorPosX(Math.Max(style.WindowPadding.X, (ImGui.GetWindowWidth() - totalW) / 2f));
    }

    private void DrawCombo(RaceState state, PluginConfig config, List<SettingsPreset> presets)
    {
        ImGui.Text("Preset");
        ImGui.SameLine();

        var names = presets.Select(p => p.Name).ToArray();
        var comboIdx = config.ActiveSettingsPresetIndex;
        ImGui.SetNextItemWidth(ComboWidth);
        if (!ImGui.Combo("##SettingsPresetCombo", ref comboIdx, names, names.Length))
            return;

        if (config.TrySwitchActivePreset(comboIdx, state))
            Revision++;
    }

    private void DrawButtons(RaceState state, PluginConfig config, List<SettingsPreset> presets, bool presetLocked)
    {
        ImGui.SameLine();
        using (UIHelper.PushGreenButtonColours())
            if (UIHelper.IconTextButton(FontAwesomeIcon.Plus, "Add", "##preset_add"))
            {
                config.AddPresetCloneFromActive(state);
                Revision++;
            }

        ImGui.SameLine();
        if (UIHelper.IconTextButton(FontAwesomeIcon.Pen, "Rename", "##preset_rename"))
        {
            _renameBuffer = presets[config.ActiveSettingsPresetIndex].Name;
            ImGui.OpenPopup("Rename Preset##crg_rename_preset");
        }

        ImGui.SameLine();
        var canDelete = presets.Count > 1 && !presetLocked;
        using (ImRaii.Disabled(!canDelete))
        using (UIHelper.PushRedButtonColours())
        {
            if (UIHelper.IconTextButton(FontAwesomeIcon.Trash, "Delete", "##preset_del")
                && canDelete
                && config.TryDeletePreset(config.ActiveSettingsPresetIndex, state))
                Revision++;
        }
    }

    private void DrawRenamePopup(PluginConfig config, List<SettingsPreset> presets)
    {
        using var renamePopup = ImRaii.PopupModal("Rename Preset##crg_rename_preset", flags: ImGuiWindowFlags.AlwaysAutoResize);
        if (!renamePopup) return;

        ImGui.InputText("Name##rename_field", ref _renameBuffer, 64);
        if (UIHelper.IconTextButton(FontAwesomeIcon.Check, "OK", "##rename_ok") && _renameBuffer.Trim().Length > 0)
        {
            presets[config.ActiveSettingsPresetIndex].Name = _renameBuffer.Trim();
            config.Save();
            ImGui.CloseCurrentPopup();
        }
        ImGui.SameLine();
        if (UIHelper.IconTextButton(FontAwesomeIcon.Times, "Cancel", "##rename_cancel"))
            ImGui.CloseCurrentPopup();
    }
}
