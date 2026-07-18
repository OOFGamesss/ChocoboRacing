using System;
using System.IO;
using System.Numerics;
using ChocoboRacing.UI.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;

/// <summary>
/// Support sub-tab explaining how to claim a free game key from the OOF Games Discord, what it unlocks, and the rules for keeping it.
/// </summary>
namespace ChocoboRacing.UI.Tabs;

public sealed class GameKeyTab
{
    private const string OofGamesDiscordUrl = "https://discord.gg/vM6ff4h5Ym";
    private const float DiscordLogoMaxWidth = 98f;

    private readonly ISharedImmediateTexture? _discordLogo;

    public GameKeyTab()
    {
        var logoPath = Path.Combine(
            Plugin.PluginInterface.AssemblyLocation.Directory?.FullName ?? string.Empty,
            "Images", "discordlogo.png");
        if (File.Exists(logoPath))
            _discordLogo = Plugin.TextureProvider.GetFromFile(logoPath);
    }

    public void Draw()
    {
        DrawDiscordHeader();
        DrawIntroSection();
        DrawGuideSection();
        DrawRulesSection();
    }

    private void DrawDiscordHeader()
    {
        ImGuiHelpers.ScaledDummy(10f);

        var scale = ImGuiHelpers.GlobalScale;
        var maxW = DiscordLogoMaxWidth * scale;

        var wrap = _discordLogo?.GetWrapOrDefault();
        var drawSize = wrap != null && wrap.Width > 0 && wrap.Height > 0
            ? new Vector2(maxW, maxW * (wrap.Height / (float)wrap.Width))
            : new Vector2(maxW, 48f * scale);

        CentreForWidth(drawSize.X);
        if (wrap != null)
            ImGui.Image(wrap.Handle, drawSize);
        else
            ImGui.Dummy(drawSize);

        ImGuiHelpers.ScaledDummy(10f);
        DrawJoinButtons();
        ImGuiHelpers.ScaledDummy(10f);
    }

    private static void DrawJoinButtons()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var btnH = ImGui.GetFrameHeight() + 4f * scale;
        var joinW = 168f * scale;
        var copyW = ImGui.GetFrameHeight();
        var spacing = ImGui.GetStyle().ItemInnerSpacing.X;

        CentreForWidth(joinW + spacing + copyW);

        using (UIHelper.PushGreenButtonColours())
            if (UIHelper.IconTextButton(FontAwesomeIcon.Comments, "Join Discord", new Vector2(joinW, btnH), "##GameKeyJoinDiscord"))
                Util.OpenLink(OofGamesDiscordUrl);

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip($"Open the OOF Games Discord in your browser:\n{OofGamesDiscordUrl}");

        ImGui.SameLine(0f, spacing);

        if (ImGuiComponents.IconButton("##GameKeyCopyDiscordInvite", FontAwesomeIcon.Copy))
            ImGui.SetClipboardText(OofGamesDiscordUrl);

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Copy the Discord invite link to the clipboard.");
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
        DrawSectionHeader("What is the game key for?");
        ImGui.TextWrapped(
            "The game key lets you mirror your race to the website. Spectators can watch live and your players "
                + "can place bets online with a PIN. The key is free and identifies you as the host.");
        ImGuiHelpers.ScaledDummy(8f);
    }

    private static void DrawGuideSection()
    {
        DrawSectionHeader("How to get your game key");

        GuideBullet("Join the OOF Games Discord using the button above.");
        GuideBullet("Go to the #game_keys channel under the Support category.");
        GuideBullet("Use /keys in that channel and click the button for your game.");
        GuideBullet("If you do not have a key yet, one is created for you. If you already have one, it is shown to you instead.");
        GuideBullet("Open the Webview tab and paste the key into the Game Key box.");
        GuideBullet("Set your venue name and image, press Go Live, then share the spectator link with your guests.");

        ImGuiHelpers.ScaledDummy(8f);
    }

    private static void DrawRulesSection()
    {
        DrawSectionHeader("Rules");

        GuideBullet("Keys are free. You will never need to pay, trade, or trade favours for one.");
        GuideBullet("You may only have one key per game per Discord account.");
        GuideBullet("Some games have a limited number of keys for server stability, so grab one while you can. Boosters are not affected by the key limit.");
        GuideBullet("Want a key for an alt character? Send a message in #contact_felix.");

        ImGuiHelpers.ScaledDummy(4f);
        using (ImRaii.PushColor(ImGuiCol.Text, UiColors.Warning))
        {
            ImGui.TextWrapped(
                "Zero tolerance on sharing. Give your key to anyone else, for any reason, and it is instantly "
                    + "disabled with no warning.");
            ImGuiHelpers.ScaledDummy(4f);
            ImGui.TextWrapped(
                "Disabled keys stay disabled. It is entirely up to a moderator's discretion whether to re-enable one.");
        }
    }

    private static void GuideBullet(string text)
    {
        ImGui.Bullet();
        ImGui.SameLine();
        ImGui.TextWrapped(text);
        ImGuiHelpers.ScaledDummy(2f);
    }

    private static void CentreForWidth(float width)
    {
        var avail = ImGui.GetContentRegionAvail().X;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0f, (avail - width) * 0.5f));
    }
}
