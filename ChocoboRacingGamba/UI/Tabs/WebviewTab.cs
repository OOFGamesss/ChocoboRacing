using System;
using System.IO;
using System.Numerics;
using ChocoboRacing.Config;
using ChocoboRacing.Services;
using ChocoboRacing.UI.Components;
using ChocoboRacing.Utility;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

/// <summary>
/// Draws the Webview tab: game key entry, venue details, and starting or ending the live web spectator and betting session.
/// </summary>
namespace ChocoboRacing.UI.Tabs;

public sealed class WebviewTab
{
    private const float FieldWidth = 320f;
    private const int GameKeyBufferLength = 128;

    private readonly Plugin _plugin;
    private readonly PresetSelector _presetSelector;
    private readonly ISharedImmediateTexture? _previewTexture;

    public WebviewTab(Plugin plugin, PresetSelector presetSelector)
    {
        _plugin = plugin;
        _presetSelector = presetSelector;

        var previewPath = Path.Combine(
            Plugin.PluginInterface.AssemblyLocation.Directory?.FullName ?? string.Empty,
            "Images", "Screenshots", "webview.png");
        if (File.Exists(previewPath))
            _previewTexture = Plugin.TextureProvider.GetFromFile(previewPath);
    }

    public void Draw()
    {
        _presetSelector.Draw(_plugin.GameState, _plugin.Configuration);

        DrawIntroSection();
        DrawGameKeySection();
        DrawVenueSection();
        DrawSessionSection();
    }

    private static void DrawSectionHeader(string label)
    {
        ImGui.TextColored(UiColors.Gold, label);
        ImGuiHelpers.ScaledDummy(2f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(6f);
    }

    private static void DrawIntroSection()
    {
        DrawSectionHeader("Web Spectator & Betting");
        ImGui.TextWrapped(
            "Mirror this race to the website so anyone can watch live, and let players bet online with a PIN.");
        ImGuiHelpers.ScaledDummy(8f);
    }

    private void DrawGameKeySection()
    {
        DrawSectionHeader("Game Key");

        var cfg = _plugin.Configuration;
        var key = cfg.ApiHostKey;

        ImGui.SetNextItemWidth(FieldWidth * ImGuiHelpers.GlobalScale);
        if (ImGui.InputText("##game_key", ref key, GameKeyBufferLength, ImGuiInputTextFlags.Password))
        {
            cfg.ApiHostKey = key.Trim();
            cfg.Save();
        }

        ImGui.SameLine();
        if (UIHelper.IconTextButton(FontAwesomeIcon.Trash, "Clear", "##clear_game_key"))
        {
            cfg.ApiHostKey = string.Empty;
            cfg.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Clear the saved game key from this plugin.");

        ImGui.SameLine();
        ImGui.TextColored(UiColors.Subtle, "No key yet? See Support > Game Key.");

        ImGuiHelpers.ScaledDummy(8f);
    }

    private void DrawVenueSection()
    {
        if (_plugin.WebMirror.SessionId != null) return;

        DrawSectionHeader("Venue");

        var cfg = _plugin.Configuration;
        var scale = ImGuiHelpers.GlobalScale;

        var venueName = cfg.WebVenueName;
        ImGui.Text("Venue Name:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(FieldWidth * scale);
        if (ImGui.InputText("##venue_name", ref venueName, 64))
        {
            cfg.WebVenueName = venueName;
            cfg.Save();
        }
        ImGui.SameLine();
        ImGui.TextColored(UiColors.Subtle, "(max 40 characters, shown on the website)");

        var nameCheck = VenueValidator.ValidateName(cfg.WebVenueName);
        if (!nameCheck.Ok) ImGui.TextColored(UiColors.Warning, nameCheck.Error);

        var venueImage = cfg.WebVenueImageUrl;
        ImGui.Text("Venue Image URL:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(FieldWidth * scale);
        if (ImGui.InputText("##venue_image", ref venueImage, 512))
        {
            cfg.WebVenueImageUrl = venueImage.Trim();
            cfg.Save();
        }
        ImGui.SameLine();
        ImGui.TextColored(UiColors.Subtle, "(.jpg/.png/.webp, max 2048x2048, no Imgur)");

        var imageCheck = VenueValidator.ValidateImageUrl(cfg.WebVenueImageUrl);
        if (!imageCheck.Ok) ImGui.TextColored(UiColors.Warning, imageCheck.Error);

        ImGuiHelpers.ScaledDummy(8f);
    }

    private void DrawSessionSection()
    {
        DrawSectionHeader("Session");

        var cfg = _plugin.Configuration;
        var mirror = _plugin.WebMirror;
        var live = mirror.SessionId != null;

        if (!live)
        {
            DrawGoLiveButton(cfg, mirror);
        }
        else
        {
            using (UIHelper.PushRedButtonColours())
                if (UIHelper.IconTextButton(FontAwesomeIcon.Stop, "End Web Session", "##end_web"))
                    mirror.EndSession();
        }

        ImGui.SameLine();
        var statusColour = mirror.StatusIsError ? UiColors.Negative
            : mirror.Connected ? UiColors.Positive
            : live ? UiColors.Warning : UiColors.Muted;
        ImGui.TextColored(statusColour, $"Status: {mirror.Status}");

        if (live)
            DrawLiveSessionDetails(mirror);

        DrawPreview();
    }

    private static void DrawGoLiveButton(PluginConfig cfg, MirrorService mirror)
    {
        var blocked = string.IsNullOrWhiteSpace(cfg.ApiHostKey)
            || !VenueValidator.ValidateName(cfg.WebVenueName).Ok
            || !VenueValidator.ValidateImageUrl(cfg.WebVenueImageUrl).Ok;

        using (ImRaii.Disabled(blocked))
        using (UIHelper.PushGreenButtonColours())
            if (UIHelper.IconTextButton(FontAwesomeIcon.Globe, "Go Live", "##go_live"))
                mirror.GoLive();
    }

    private static void DrawLiveSessionDetails(MirrorService mirror)
    {
        ImGui.Spacing();
        ImGui.Text("Session Code:");
        ImGui.SameLine();
        ImGui.TextColored(UiColors.Gold, mirror.SessionId);
        ImGui.SameLine();
        if (UIHelper.IconTextButton(FontAwesomeIcon.Copy, "Copy Link", "##copy_link"))
            ImGui.SetClipboardText(mirror.SpectatorUrl ?? string.Empty);

        ImGui.TextColored(UiColors.Subtle, mirror.SpectatorUrl ?? string.Empty);
        ImGui.TextWrapped("Share the link for spectators. Give each player their PIN from the Banks tab so they can bet online.");
    }

    private void DrawPreview()
    {
        if (_previewTexture == null) return;

        ImGuiHelpers.ScaledDummy(10f);
        DrawSectionHeader("Web Preview");

        var imgW = ImGui.GetContentRegionAvail().X;
        var startX = ImGui.GetCursorPosX();

        if (_previewTexture.TryGetWrap(out var wrap, out _))
        {
            var imgH = imgW * (wrap.Height / (float)wrap.Width);
            ImGui.Image(wrap.Handle, new Vector2(imgW, imgH));
        }
        else
        {
            ImGui.Dummy(new Vector2(imgW, imgW * 0.66f));
        }

        const string caption = "Example spectator view";
        var textW = ImGui.CalcTextSize(caption).X;
        ImGui.SetCursorPosX(startX + MathF.Max(0f, (imgW - textW) * 0.5f));
        ImGui.TextColored(UiColors.Subtle, caption);
        ImGuiHelpers.ScaledDummy(8f);
    }
}
