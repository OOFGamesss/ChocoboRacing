using System.Numerics;
using Dalamud.Bindings.ImGui;

/// <summary>
/// Draws a small rounded status pill used to flag a row, such as a pending cash-out request.
/// </summary>
namespace ChocoboRacing.UI.Components;

internal static class Badge
{
    private const float PaddingX = 6f;
    private const float PaddingY = 2f;

    internal static void Draw(string text, Vector4 background, Vector4 textColour)
    {
        var style = ImGui.GetStyle();
        var textSize = ImGui.CalcTextSize(text);
        var size = new Vector2(textSize.X + PaddingX * 2f, textSize.Y + PaddingY * 2f);
        var origin = ImGui.GetCursorScreenPos();

        ImGui.Dummy(size);

        var dl = ImGui.GetWindowDrawList();
        dl.AddRectFilled(origin, origin + size, ImGui.ColorConvertFloat4ToU32(background), style.FrameRounding);
        dl.AddText(origin + new Vector2(PaddingX, PaddingY), ImGui.ColorConvertFloat4ToU32(textColour), text);
    }
}
