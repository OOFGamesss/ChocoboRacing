using System;
using System.Numerics;
using ChocoboRacing.Utility;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;

/// <summary>
/// Draws the visual racing lanes for the Chocobo Race UI.
/// </summary>
namespace ChocoboRacing.UI.Components;

public static class RaceTrackDraw
{
    private const float LaneHeight = 52f;
    private const float LanePadding = 6f;
    private const float AnimationDuration = 1.5f;
    private const float CheckerSize = 6f;
    private const int CheckerColumns = 6;

    private static readonly uint TrackBgColor = 0xFF2A2A3E;

    private static float[] _displayPositions = Array.Empty<float>();
    private static int[] _lastKnownPositions = Array.Empty<int>();
    private static float[] _animStartPositions = Array.Empty<float>();
    private static double[] _animStartTimes = Array.Empty<double>();
    private static int _lastChocoboCount;

    public static void DrawTrack(Plugin plugin)
    {
        var state = plugin.GameState;
        IDalamudTextureWrap? chocoboImage = null;

        var iconPath = System.IO.Path.Combine(
            Plugin.PluginInterface.AssemblyLocation.Directory?.FullName!, "Images", "chocobo-sprite.png");
        if (System.IO.File.Exists(iconPath))
            chocoboImage = Plugin.TextureProvider.GetFromFile(iconPath).GetWrapOrDefault();

        var scale = ImGuiHelpers.GlobalScale;
        var availWidth = ImGui.GetContentRegionAvail().X;
        var laneH = LaneHeight * scale;
        var padding = LanePadding * scale;
        var finishLine = state.FinishLine;
        var now = ImGui.GetTime();

        InitAnimationArrays(state.ChocoboCount);
        UpdateAnimations(state, now);

        UIHelper.SectionHeader("CHOCOBO RACE TRACK");

        var drawList = ImGui.GetWindowDrawList();
        var iconSize = laneH - 6 * scale;

        for (var i = 0; i < state.ChocoboCount; i++)
        {
            DrawLane(state, drawList, i, laneH, padding, iconSize, scale,
                     availWidth, finishLine, now, chocoboImage);
        }
    }

    private static float EaseOutCubic(float t) => 1f - MathF.Pow(1f - t, 3f);

    private static void InitAnimationArrays(int chocoboCount)
    {
        if (chocoboCount == _lastChocoboCount && _displayPositions.Length == chocoboCount) return;

        _lastChocoboCount = chocoboCount;
        _displayPositions = new float[chocoboCount];
        _lastKnownPositions = new int[chocoboCount];
        _animStartPositions = new float[chocoboCount];
        _animStartTimes = new double[chocoboCount];
    }

    private static void UpdateAnimations(ChocoboRacing.State.RaceState state, double now)
    {
        for (var i = 0; i < state.ChocoboCount; i++)
        {
            var targetPos = (i < state.ChocoboPositions.Length) ? state.ChocoboPositions[i] : 0;

            if (targetPos != _lastKnownPositions[i])
            {
                _animStartPositions[i] = _displayPositions[i];
                _animStartTimes[i] = now;
                _lastKnownPositions[i] = targetPos;
            }

            var elapsed = (float)(now - _animStartTimes[i]);
            var t = Math.Clamp(elapsed / AnimationDuration, 0f, 1f);
            _displayPositions[i] = _animStartPositions[i] + (targetPos - _animStartPositions[i]) * EaseOutCubic(t);
        }
    }

    private static void DrawLane(
        ChocoboRacing.State.RaceState state,
        ImDrawListPtr drawList,
        int i, float laneH, float padding, float iconSize, float scale,
        float availWidth, int finishLine, double now,
        IDalamudTextureWrap? chocoboImage)
    {
        var chocoboNum = i + 1;
        var actualPosition = (i < state.ChocoboPositions.Length) ? state.ChocoboPositions[i] : 0;
        var displayPos = _displayPositions[i];
        var laneColor = UIHelper.GetLaneColourAbgr(chocoboNum);
        var cursorPos = ImGui.GetCursorScreenPos();

        var labelColor = laneColor;

        var labelWidth = 120 * scale;
        var trackStart = new Vector2(cursorPos.X + labelWidth, cursorPos.Y);
        var trackEnd = new Vector2(cursorPos.X + availWidth - 10 * scale, cursorPos.Y + laneH);
        var checkerSz = CheckerSize * scale;
        var checkerW = CheckerColumns * checkerSz;
        var checkerStartX = trackEnd.X - checkerW + checkerSz;

        drawList.AddRectFilled(trackStart, trackEnd, TrackBgColor, 4f);
        drawList.AddRect(trackStart, trackEnd, laneColor, 4f, ImDrawFlags.None, 1.5f);

        DrawCheckerboard(drawList, trackStart, trackEnd, laneH, checkerStartX, checkerSz);
        DrawChocoboIcon(drawList, trackStart, trackEnd, cursorPos, laneH, iconSize, scale,
                        displayPos, finishLine, checkerStartX, checkerW, laneColor, actualPosition, chocoboImage);

        if (actualPosition >= finishLine)
            DrawWinnerBanner(drawList, trackStart, trackEnd, cursorPos, laneH, scale, now);

        var effectiveName = state.GetEffectiveChocoboVisualName(chocoboNum);
        var labelText = TrackVisualiser.GetChocoboName(chocoboNum, effectiveName);
        drawList.AddText(new Vector2(cursorPos.X, cursorPos.Y + (laneH - 16 * scale) * 0.3f), labelColor, labelText);

        ImGui.Dummy(new Vector2(0, laneH + padding));
    }

    private static void DrawCheckerboard(ImDrawListPtr drawList, Vector2 trackStart, Vector2 trackEnd,
                                          float laneH, float checkerStartX, float checkerSz)
    {
        var rowCount = (int)Math.Ceiling(laneH / checkerSz);
        for (var row = 0; row < rowCount; row++)
        {
            for (var col = 0; col < CheckerColumns; col++)
            {
                var color = (row + col) % 2 == 0 ? 0xFFFFFFFF : 0xFF000000;
                var sqMin = new Vector2(checkerStartX + col * checkerSz, trackStart.Y + row * checkerSz);
                var sqMax = new Vector2(sqMin.X + checkerSz, Math.Min(sqMin.Y + checkerSz, trackEnd.Y));
                drawList.AddRectFilled(sqMin, sqMax, color);
            }
        }
    }

    private static void DrawChocoboIcon(ImDrawListPtr drawList, Vector2 trackStart, Vector2 trackEnd,
                                         Vector2 cursorPos, float laneH, float iconSize, float scale,
                                         float displayPos, int finishLine, float checkerStartX, float checkerW,
                                         uint laneColor, int actualPosition, IDalamudTextureWrap? chocoboImage)
    {
        var progress = displayPos / finishLine;
        var startPad = iconSize * 0.5f + 2 * scale;
        var maxTravelX = checkerStartX + checkerW * 0.3f;
        var chocoboX = trackStart.X + startPad + ((maxTravelX - trackStart.X - startPad) * progress);
        var chocoboY = cursorPos.Y + laneH * 0.5f;

        if (chocoboImage != null)
        {
            var imgPos = new Vector2(chocoboX - iconSize * 0.5f, chocoboY - iconSize * 0.5f);
            var savedCursor = ImGui.GetCursorScreenPos();
            ImGui.SetCursorScreenPos(imgPos);
            ImGui.Image(chocoboImage.Handle, new Vector2(iconSize, iconSize));
            ImGui.SetCursorScreenPos(savedCursor);
        }
        else
        {
            var radius = (laneH - 8 * scale) * 0.4f;
            drawList.AddCircleFilled(new Vector2(chocoboX, chocoboY), radius, laneColor);
            drawList.AddCircle(new Vector2(chocoboX, chocoboY), radius, 0xFFFFFFFF, 0, 2f);
            var posText = actualPosition.ToString();
            var textSize = ImGui.CalcTextSize(posText);
            drawList.AddText(new Vector2(chocoboX - textSize.X * 0.5f, chocoboY - textSize.Y * 0.5f), 0xFFFFFFFF, posText);
        }
    }

    private static void DrawWinnerBanner(ImDrawListPtr drawList, Vector2 trackStart, Vector2 trackEnd,
                                          Vector2 cursorPos, float laneH, float scale, double now)
    {
        const string winText = "WINNER!";
        var winTextSize = ImGui.CalcTextSize(winText);
        var trackMidX = (trackStart.X + trackEnd.X) * 0.5f;
        var winPos = new Vector2(trackMidX - winTextSize.X * 0.5f, cursorPos.Y + (laneH - winTextSize.Y) * 0.5f);

        var hue = (float)((now * 2.0) % 1.0);
        var winColor = HsvToAbgr(hue, 1f, 1f);

        var pillPad = 4 * scale;
        drawList.AddRectFilled(
            new Vector2(winPos.X - pillPad, winPos.Y - pillPad * 0.5f),
            new Vector2(winPos.X + winTextSize.X + pillPad, winPos.Y + winTextSize.Y + pillPad * 0.5f),
            0xCC000000, 4f);
        drawList.AddText(winPos, winColor, winText);
    }

    private static uint HsvToAbgr(float h, float s, float v)
    {
        var hi = (int)(h * 6f) % 6;
        var f = h * 6f - (int)(h * 6f);
        var p = v * (1f - s);
        var q = v * (1f - f * s);
        var t = v * (1f - (1f - f) * s);

        float r, g, b;
        switch (hi)
        {
            case 0: r = v; g = t; b = p; break;
            case 1: r = q; g = v; b = p; break;
            case 2: r = p; g = v; b = t; break;
            case 3: r = p; g = q; b = v; break;
            case 4: r = t; g = p; b = v; break;
            default: r = v; g = p; b = q; break;
        }

        var ri = (uint)(r * 255f);
        var gi = (uint)(g * 255f);
        var bi = (uint)(b * 255f);
        return 0xFF000000 | (bi << 16) | (gi << 8) | ri;
    }
}
