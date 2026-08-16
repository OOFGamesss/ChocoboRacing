using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

/// <summary>
/// Shared UI rendering helpers used across all tab components.
/// </summary>
namespace ChocoboRacing.UI.Components;

internal static class UIHelper
{
    private static readonly uint[] LaneColours =
    {
        0xFF4A4ADD,
        0xFFE2904A,
        0xFF4AD24A,
        0xFF4AD2D2,
        0xFFC04AD2,
        0xFFD2804A,
        0xFF6666FF,
        0xFFB0B0B0,
        0xFF40C0FF,
        0xFFFFFF40,
    };

    internal static uint GetLaneColourAbgr(int chocoboNumber)
    {
        if (chocoboNumber < 1) return 0xFFFFFFFF;
        return LaneColours[(chocoboNumber - 1) % LaneColours.Length];
    }

    internal static Vector4 GetLaneColourVector4(int chocoboNumber) =>
        AbgrToVector4(GetLaneColourAbgr(chocoboNumber));

    internal static void TextColoredForChocobo(int chocoboNumber, string text) =>
        ImGui.TextColored(GetLaneColourVector4(chocoboNumber), text);

    internal static string FormatGil(long amount) => $"{amount:N0} Gil";

    internal static Vector4 SignColor(long value) => value >= 0 ? UiColors.Positive : UiColors.Negative;

    internal static void DrawSignedGil(long value, bool includeGilSuffix = true)
    {
        var suffix = includeGilSuffix ? " Gil" : string.Empty;
        ImGui.TextColored(SignColor(value), $"{(value >= 0 ? "+" : "")}{value:N0}{suffix}");
    }

    private static readonly (string Label, int Amount)[] QuickAmounts =
    {
        ("+1K",   1_000),
        ("+10K",  10_000),
        ("+100K", 100_000),
        ("+1M",   1_000_000),
    };

    internal static void QuickAmountButtons(ref int amount, string idSuffix)
    {
        for (var i = 0; i < QuickAmounts.Length; i++)
        {
            if (i > 0) ImGui.SameLine();
            if (ImGui.SmallButton($"{QuickAmounts[i].Label}##q{i}_{idSuffix}"))
                amount += QuickAmounts[i].Amount;
        }
    }

    internal static float QuickAmountButtonsWidth()
    {
        var style = ImGui.GetStyle();
        var width = style.ItemSpacing.X * (QuickAmounts.Length - 1);
        foreach (var (label, _) in QuickAmounts)
            width += ImGui.CalcTextSize(label).X + style.FramePadding.X * 2f;
        return width;
    }

    internal static void SectionHeader(string title)
    {
        ImGui.TextColored(UiColors.Gold, title);
        ImGui.Spacing();
    }

    internal static bool TryDebounce(ref long lastMs, int windowMs = 500)
    {
        if (Environment.TickCount64 - lastMs <= windowMs) return false;
        lastMs = Environment.TickCount64;
        return true;
    }

    internal static bool CtrlClickConfirmed(bool clicked, string tooltip)
    {
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(tooltip);
        return clicked && ImGui.GetIO().KeyCtrl;
    }

    internal static Vector4 AbgrToVector4(uint abgr)
    {
        var a = ((abgr >> 24) & 0xFF) / 255f;
        var b = ((abgr >> 16) & 0xFF) / 255f;
        var g = ((abgr >> 8) & 0xFF) / 255f;
        var r = (abgr & 0xFF) / 255f;
        return new Vector4(r, g, b, a);
    }

    internal static float IconTextButtonWidth(FontAwesomeIcon icon, string label)
    {
        var style = ImGui.GetStyle();
        ImGui.PushFont(UiBuilder.IconFont);
        var iconSize = ImGui.CalcTextSize(icon.ToIconString());
        ImGui.PopFont();
        var textSize = ImGui.CalcTextSize(label);
        return style.FramePadding.X * 2 + iconSize.X + style.ItemInnerSpacing.X + textSize.X;
    }

    internal static float IconButtonWidth(FontAwesomeIcon icon)
    {
        ImGui.PushFont(UiBuilder.IconFont);
        var glyphWidth = ImGui.CalcTextSize(icon.ToIconString()).X;
        ImGui.PopFont();
        return Math.Max(glyphWidth + ImGui.GetStyle().FramePadding.X * 2f, ImGui.GetFrameHeight());
    }

    internal static float IconButtonRowWidth(params FontAwesomeIcon[] icons)
    {
        if (icons.Length == 0) return 0f;

        var width = ImGui.GetStyle().ItemSpacing.X * (icons.Length - 1);
        foreach (var icon in icons)
            width += IconButtonWidth(icon);
        return width;
    }

    internal static bool IconTextButton(FontAwesomeIcon icon, string label, string id = "", Vector4? textColor = null) =>
        DrawIconTextButton(icon, label, null, id, textColor);

    internal static bool IconTextButton(FontAwesomeIcon icon, string label, Vector2 size, string id = "", Vector4? textColor = null) =>
        DrawIconTextButton(icon, label, size, id, textColor);

    private static bool DrawIconTextButton(FontAwesomeIcon icon, string label, Vector2? size, string id, Vector4? textColor)
    {
        var iconFont   = UiBuilder.IconFont;
        var defaultFont = ImGui.GetFont();
        var fontSize   = ImGui.GetFontSize();
        var iconStr    = icon.ToIconString();
        var style      = ImGui.GetStyle();

        ImGui.PushFont(iconFont);
        var iconSize = ImGui.CalcTextSize(iconStr);
        ImGui.PopFont();

        var textSize = ImGui.CalcTextSize(label);
        var height   = Math.Max(iconSize.Y, textSize.Y);

        var contentWidth = iconSize.X + style.ItemInnerSpacing.X + textSize.X;
        var btnSize = size ?? new Vector2(
            style.FramePadding.X * 2 + contentWidth,
            style.FramePadding.Y * 2 + height);

        var btnId   = string.IsNullOrEmpty(id) ? $"##{label}" : id;
        var clicked = ImGui.InvisibleButton(btnId, btnSize);

        var isHovered = ImGui.IsItemHovered();
        var isActive  = ImGui.IsItemActive();

        var bgColor = isActive  ? ImGui.GetColorU32(ImGuiCol.ButtonActive)
                    : isHovered ? ImGui.GetColorU32(ImGuiCol.ButtonHovered)
                    :             ImGui.GetColorU32(ImGuiCol.Button);

        var dl  = ImGui.GetWindowDrawList();
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();

        dl.AddRectFilled(min, max, bgColor, style.FrameRounding);

        if (style.FrameBorderSize > 0f)
            dl.AddRect(min, max, ImGui.GetColorU32(ImGuiCol.Border), style.FrameRounding,
                ImDrawFlags.None, style.FrameBorderSize);

        var textColorU32 = textColor.HasValue
            ? ImGui.ColorConvertFloat4ToU32(textColor.Value)
            : ImGui.GetColorU32(ImGuiCol.Text);

        var rectSize = max - min;
        var startX = min.X + Math.Max(style.FramePadding.X, (rectSize.X - contentWidth) * 0.5f);

        var iconPos = new Vector2(startX, min.Y + (rectSize.Y - iconSize.Y) * 0.5f);
        dl.AddText(iconFont, fontSize, iconPos, textColorU32, iconStr);

        var textPos = new Vector2(iconPos.X + iconSize.X + style.ItemInnerSpacing.X,
                                  min.Y + (rectSize.Y - textSize.Y) * 0.5f);
        dl.AddText(defaultFont, fontSize, textPos, textColorU32, label);

        return clicked;
    }

    internal static ImRaii.ColorDisposable PushGreenButtonColours() =>
        new ImRaii.ColorDisposable()
            .Push(ImGuiCol.Button,        new Vector4(0.1f,  0.6f,  0.1f,  1f))
            .Push(ImGuiCol.ButtonHovered, new Vector4(0.2f,  0.8f,  0.2f,  1f))
            .Push(ImGuiCol.ButtonActive,  new Vector4(0.05f, 0.4f,  0.05f, 1f));

    internal static ImRaii.ColorDisposable PushRedButtonColours() =>
        new ImRaii.ColorDisposable()
            .Push(ImGuiCol.Button,        new Vector4(0.6f, 0.1f,  0.1f,  1f))
            .Push(ImGuiCol.ButtonHovered, new Vector4(0.8f, 0.2f,  0.2f,  1f))
            .Push(ImGuiCol.ButtonActive,  new Vector4(0.4f, 0.05f, 0.05f, 1f));

    internal static ImRaii.ColorDisposable PushBlueButtonColours() =>
        new ImRaii.ColorDisposable()
            .Push(ImGuiCol.Button,        new Vector4(0.1f,  0.35f, 0.7f,  1f))
            .Push(ImGuiCol.ButtonHovered, new Vector4(0.2f,  0.5f,  0.9f,  1f))
            .Push(ImGuiCol.ButtonActive,  new Vector4(0.05f, 0.25f, 0.55f, 1f));

    internal static ImRaii.ColorDisposable PushYellowButtonColours() =>
        new ImRaii.ColorDisposable()
            .Push(ImGuiCol.Button,        new Vector4(0.65f, 0.50f, 0.05f, 1f))
            .Push(ImGuiCol.ButtonHovered, new Vector4(0.85f, 0.68f, 0.08f, 1f))
            .Push(ImGuiCol.ButtonActive,  new Vector4(0.50f, 0.38f, 0.03f, 1f));

    internal static ImRaii.ColorDisposable PushPinkButtonColours() =>
        new ImRaii.ColorDisposable()
            .Push(ImGuiCol.Button,        new Vector4(0.80f, 0.25f, 0.55f, 1f))
            .Push(ImGuiCol.ButtonHovered, new Vector4(0.95f, 0.40f, 0.70f, 1f))
            .Push(ImGuiCol.ButtonActive,  new Vector4(0.60f, 0.15f, 0.40f, 1f));

    internal static ImRaii.ColorDisposable PushCyanButtonColours() =>
        new ImRaii.ColorDisposable()
            .Push(ImGuiCol.Button,        new Vector4(0.05f, 0.55f, 0.65f, 1f))
            .Push(ImGuiCol.ButtonHovered, new Vector4(0.10f, 0.72f, 0.85f, 1f))
            .Push(ImGuiCol.ButtonActive,  new Vector4(0.03f, 0.40f, 0.48f, 1f));
}
