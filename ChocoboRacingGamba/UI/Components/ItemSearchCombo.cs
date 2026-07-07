using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Lumina.Excel.Sheets;

/// <summary>
/// Combo box for searching and selecting an in-game item.
/// </summary>
namespace ChocoboRacing.UI.Components;

public static class ItemSearchCombo
{
    private static Dictionary<uint, string>? _itemById;
    private static List<(uint Id, string Name)>? _allSorted;
    private static List<(uint Id, string Name)> _filtered = new();
    private static string _search = string.Empty;
    private static bool _wasOpen;

    public static string? GetItemName(uint itemId) =>
        itemId == 0 ? null : _itemById?.GetValueOrDefault(itemId);

    public static bool Draw(string id, ref uint selectedItemId, ref string selectedItemName)
    {
        EnsureLoaded();

        var preview = selectedItemId == 0
            ? "(None)"
            : _itemById!.GetValueOrDefault(selectedItemId, "(Unknown)");

        var changed = false;
        var isOpen = ImGui.BeginCombo(id, preview);

        if (!isOpen)
        {
            if (_wasOpen)
            {
                _search = string.Empty;
                _wasOpen = false;
            }
            return false;
        }

        if (!_wasOpen)
        {
            ImGui.SetKeyboardFocusHere();
            _wasOpen = true;
        }

        ImGui.SetNextItemWidth(-1f);
        if (ImGui.InputText("##ItemSearchInput", ref _search, 128))
            RefreshFilter();

        if (ImGui.Selectable("(None)", selectedItemId == 0))
        {
            selectedItemId = 0;
            selectedItemName = string.Empty;
            changed = true;
        }

        ImGui.Separator();

        foreach (var (itemId, name) in _filtered)
        {
            if (ImGui.Selectable($"{name}##{itemId}", selectedItemId == itemId))
            {
                selectedItemId = itemId;
                selectedItemName = name;
                changed = true;
            }
        }

        ImGui.EndCombo();
        return changed;
    }

    private static void EnsureLoaded()
    {
        if (_allSorted != null) return;

        _itemById = new Dictionary<uint, string>();
        _allSorted = new List<(uint Id, string Name)>();

        var sheet = Plugin.DataManager.GetExcelSheet<Item>();
        foreach (var row in sheet)
        {
            if (row.RowId == 0) continue;
            var name = row.Name.ToString();
            if (string.IsNullOrEmpty(name)) continue;
            _itemById[row.RowId] = name;
            _allSorted.Add((row.RowId, name));
        }

        _allSorted.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        RefreshFilter();
    }

    private static void RefreshFilter()
    {
        _filtered = string.IsNullOrWhiteSpace(_search)
            ? _allSorted!.Take(200).ToList()
            : _allSorted!.Where(x => x.Name.Contains(_search, StringComparison.OrdinalIgnoreCase)).Take(200).ToList();
    }
}
