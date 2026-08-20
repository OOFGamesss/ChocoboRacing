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
/// Centred preset row: switch, add, rename, delete, and share the named host settings presets.
/// </summary>
namespace ChocoboRacing.UI.Components;

public sealed class PresetSelector
{
    private const float ComboWidth = 220f;

    private string _renameBuffer = string.Empty;
    private string _shareStatus = string.Empty;
    private bool _shareStatusIsError;

    public int Revision { get; private set; }

    public void Draw(RaceState state, PluginConfig config)
    {
        var presets = config.SettingsPresets;
        if (presets == null || presets.Count == 0)
            return;

        var presetLocked = !config.CanEditPresets(state);
        if (presetLocked)
        {
            ImGui.TextColored(UiColors.Negative, "Switch or edit presets when no race or raffle is running.");
            ImGui.BeginDisabled();
        }

        CentreRow();
        DrawCombo(state, config, presets);
        DrawButtons(state, config, presets, presetLocked);
        DrawRenamePopup(config, presets);

        if (presetLocked)
            ImGui.EndDisabled();

        DrawShareStatus();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
    }

    private static void CentreRow()
    {
        var style = ImGui.GetStyle();
        var sp = style.ItemSpacing.X;
        var labelW = ImGui.CalcTextSize("Preset").X;

        var totalW = labelW + sp + ComboWidth + sp
                   + UIHelper.IconTextButtonWidth(FontAwesomeIcon.Plus, "Add") + sp
                   + UIHelper.IconTextButtonWidth(FontAwesomeIcon.Pen, "Rename") + sp
                   + UIHelper.IconTextButtonWidth(FontAwesomeIcon.Trash, "Delete") + sp
                   + UIHelper.IconTextButtonWidth(FontAwesomeIcon.Upload, "Export") + sp
                   + UIHelper.IconTextButtonWidth(FontAwesomeIcon.Download, "Import");

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
        {
            _shareStatus = string.Empty;
            Revision++;
        }
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
        DrawDeleteButton(state, config, presets, presetLocked);

        ImGui.SameLine();
        DrawExportButton(config);

        ImGui.SameLine();
        DrawImportButton(state, config);
    }

    private void DrawDeleteButton(RaceState state, PluginConfig config, List<SettingsPreset> presets, bool presetLocked)
    {
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

    private void DrawExportButton(PluginConfig config)
    {
        using (UIHelper.PushBlueButtonColours())
            if (UIHelper.IconTextButton(FontAwesomeIcon.Upload, "Export", "##preset_export"))
            {
                var code = config.ExportActivePreset();
                ImGui.SetClipboardText(code);
                SetShareStatus(code.Length > 0
                    ? "Preset code copied to the clipboard."
                    : "Nothing to export.", code.Length == 0);
            }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Copy this preset to the clipboard as a share code.\nSettings only: no game key, session, banks or player data.");
    }

    private void DrawImportButton(RaceState state, PluginConfig config)
    {
        using (UIHelper.PushCyanButtonColours())
            if (UIHelper.IconTextButton(FontAwesomeIcon.Download, "Import", "##preset_import"))
                ImportFromClipboard(state, config);

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Paste a preset code from the clipboard and add it as a new preset.");
    }

    private void ImportFromClipboard(RaceState state, PluginConfig config)
    {
        string clipboard;
        try
        {
            clipboard = ImGui.GetClipboardText();
        }
        catch (Exception)
        {
            SetShareStatus("Could not read the clipboard.", true);
            return;
        }

        if (config.TryImportPreset(clipboard, state))
        {
            SetShareStatus($"Imported \"{config.SettingsPresets[config.ActiveSettingsPresetIndex].Name}\".", false);
            Revision++;
            return;
        }

        SetShareStatus("Clipboard does not contain a valid preset code.", true);
    }

    private void SetShareStatus(string message, bool isError)
    {
        _shareStatus = message;
        _shareStatusIsError = isError;
    }

    private void DrawShareStatus()
    {
        if (_shareStatus.Length == 0) return;

        var width = ImGui.CalcTextSize(_shareStatus).X;
        ImGui.SetCursorPosX(Math.Max(ImGui.GetStyle().WindowPadding.X, (ImGui.GetWindowWidth() - width) / 2f));
        ImGui.TextColored(_shareStatusIsError ? UiColors.Negative : UiColors.Positive, _shareStatus);
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
