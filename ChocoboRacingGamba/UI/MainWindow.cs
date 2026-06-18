using System;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility;
using Dalamud.Bindings.ImGui;
using ChocoboRacing.UI.Components;
using ChocoboRacing.UI.Tabs;
using ChocoboRacing.Models;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility.Raii;

namespace ChocoboRacing.UI;

/// <summary>
/// Primary UI window. Hosts the tabbed host-mode interface (race control, banks, settings, history, profit/loss, support).
/// </summary>
public sealed class MainWindow : Window, IDisposable
{
    private readonly Plugin _plugin;
    private readonly HostRaceTab _raceTab;
    private readonly BanksTab _banksTab;
    private readonly SettingsTab _settingsTab;
    private readonly HistoryTab _historyTab;
    private readonly ProfitLossTab _profitLossTab;
    private readonly SupportTab _supportTab;
    private readonly ISharedImmediateTexture? _icon;

    private const int ThemeColourCount = 19;

    private bool _selectSettingsTab;
    private bool _raceOrBanksTabActive;

    public MainWindow(Plugin Plugin, string iconPath) : base("Chocobo Racing###ChocoboRacingGamba")
    {
        _plugin = Plugin;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(600, 450),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        if (System.IO.File.Exists(iconPath))
            _icon = Plugin.TextureProvider.GetFromFile(iconPath);

        _raceTab       = new HostRaceTab(Plugin);
        _banksTab      = new BanksTab(Plugin);
        _settingsTab   = new SettingsTab(Plugin);
        _historyTab    = new HistoryTab(Plugin);
        _profitLossTab = new ProfitLossTab(Plugin);
        _supportTab    = new SupportTab();
    }

    public event Action? WindowOpened;
    public override void OnOpen() => WindowOpened?.Invoke();

    /// <summary>Opens the window (if closed) and switches to the Settings tab.</summary>
    public void OpenToSettings()
    {
        _selectSettingsTab = true;
        IsOpen = true;
    }
    public void Dispose() => (_icon as IDisposable)?.Dispose();

    public override void PreDraw()
    {
        ImGui.PushStyleColor(ImGuiCol.WindowBg,          new Vector4(0.08f, 0.08f, 0.07f, 0.98f));
        ImGui.PushStyleColor(ImGuiCol.TitleBgActive,     new Vector4(0.50f, 0.38f, 0.05f, 1.00f));
        ImGui.PushStyleColor(ImGuiCol.Border,            new Vector4(0.70f, 0.54f, 0.08f, 0.50f));
        ImGui.PushStyleColor(ImGuiCol.Separator,         new Vector4(0.60f, 0.46f, 0.06f, 0.60f));
        ImGui.PushStyleColor(ImGuiCol.Button,            new Vector4(0.55f, 0.40f, 0.05f, 1.00f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered,     new Vector4(0.72f, 0.55f, 0.08f, 1.00f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive,      new Vector4(0.44f, 0.32f, 0.02f, 1.00f));
        ImGui.PushStyleColor(ImGuiCol.Tab,               new Vector4(0.18f, 0.14f, 0.02f, 0.90f));
        ImGui.PushStyleColor(ImGuiCol.TabHovered,        new Vector4(0.60f, 0.46f, 0.06f, 1.00f));
        ImGui.PushStyleColor(ImGuiCol.TabActive,         new Vector4(0.78f, 0.60f, 0.08f, 1.00f));
        ImGui.PushStyleColor(ImGuiCol.Header,            new Vector4(0.38f, 0.28f, 0.03f, 0.80f));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered,     new Vector4(0.55f, 0.42f, 0.06f, 1.00f));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive,      new Vector4(0.44f, 0.34f, 0.04f, 1.00f));
        ImGui.PushStyleColor(ImGuiCol.FrameBg,           new Vector4(0.15f, 0.12f, 0.02f, 1.00f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered,    new Vector4(0.22f, 0.17f, 0.03f, 1.00f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive,     new Vector4(0.30f, 0.23f, 0.04f, 1.00f));
        ImGui.PushStyleColor(ImGuiCol.CheckMark,         new Vector4(1.00f, 0.85f, 0.20f, 1.00f));
        ImGui.PushStyleColor(ImGuiCol.SliderGrab,        new Vector4(0.80f, 0.62f, 0.10f, 1.00f));
        ImGui.PushStyleColor(ImGuiCol.SliderGrabActive,  new Vector4(1.00f, 0.80f, 0.15f, 1.00f));
    }

    public override void PostDraw()
    {
        ImGui.PopStyleColor(ThemeColourCount);
    }

    public override void Draw()
    {
        if (_plugin.GameState.IsHosting)
        {
            var s   = _plugin.GameState;
            var cfg = _plugin.Configuration;

            ImGui.AlignTextToFramePadding();
            ImGui.SetWindowFontScale(1.05f);

            var gold  = UiColors.Gold;
            var white = new Vector4(0.95f, 0.95f, 0.95f, 1.0f);
            var dim   = new Vector4(0.5f, 0.5f, 0.4f, 1.0f);

            void DrawPart(string label, string val, Vector4 valColor)
            {
                ImGui.TextColored(dim, label);
                ImGui.SameLine(0, 4f);
                ImGui.TextColored(valColor, val);
                ImGui.SameLine(0, 16f);
            }

            DrawPart("ROUND",    $"{s.RoundNumber}",      white);
            DrawPart("CHOCOBOS", $"{s.ChocoboCount}",     white);
            DrawPart("DISTANCE", $"{s.FinishLine}y",      white);
            DrawPart("PAYOUT",   $"{s.PayoutOdds:F2}x",  gold);

            if (cfg.PerfectRace)
                DrawPart("PERFECT RACE", $"{cfg.PerfectRaceOdds:F2}x", UiColors.Accent);

            ImGui.SetWindowFontScale(1.0f);
        }

        const string testLabel = "Testing Mode";
        var testBtnWidth = ImGui.CalcTextSize(testLabel).X + ImGui.GetStyle().FramePadding.X * 2 + ImGui.GetStyle().ItemSpacing.X + 8f;
        ImGui.SetCursorPosX(ImGui.GetWindowWidth() - testBtnWidth - ImGui.GetStyle().WindowPadding.X - 20f);

        using (new ImRaii.ColorDisposable()
            .Push(ImGuiCol.Button,        new Vector4(0.10f, 0.55f, 0.10f, 1f), _plugin.IsTestingMode)
            .Push(ImGuiCol.ButtonHovered, new Vector4(0.15f, 0.70f, 0.15f, 1f), _plugin.IsTestingMode)
            .Push(ImGuiCol.ButtonActive,  new Vector4(0.08f, 0.40f, 0.08f, 1f), _plugin.IsTestingMode))
        {
            if (UIHelper.IconTextButton(FontAwesomeIcon.Flask, testLabel, "##test_mode_btn"))
            {
                if (_plugin.IsTestingMode) _plugin.DisableTestingMode();
                else _plugin.EnableTestingMode();
            }
        }

        ImGui.Separator();

        if (_raceOrBanksTabActive) DrawIconWatermark();
        DrawTabs();
    }

    private void DrawTabs()
    {
        _raceOrBanksTabActive = false;

        using var tabBar = ImRaii.TabBar("HostTabBar");
        if (!tabBar) return;

        using (var tab = ImRaii.TabItem("Race"))
            if (tab) { _raceOrBanksTabActive = true; _raceTab.Draw(); }

        var cashOutRequests = _plugin.GameState.GetActiveCashOutRequests();
        var activeBanks     = _plugin.GameState.GetBanksSnapshot().Where(b => !b.IsArchived).ToList();
        var cashOutCount    = activeBanks.Count(b => cashOutRequests.Contains($"{b.Name}@{b.World}"));
        var banksTabLabel   = cashOutCount > 0 ? $"Banks ({cashOutCount})###BanksTab" : "Banks###BanksTab";

        using (var banksColour = new ImRaii.ColorDisposable())
        {
            if (cashOutCount > 0)
            {
                var pulse = (float)(0.5 + 0.5 * System.Math.Sin(ImGui.GetTime() * 4.0));
                banksColour
                    .Push(ImGuiCol.Tab,       new Vector4(0.55f, 0.25f + 0.25f * pulse, 0f, 1f))
                    .Push(ImGuiCol.TabHovered, new Vector4(0.75f, 0.40f + 0.30f * pulse, 0f, 1f))
                    .Push(ImGuiCol.TabActive,  new Vector4(0.90f, 0.55f + 0.30f * pulse, 0f, 1f));
            }
            using var banksTab = ImRaii.TabItem(banksTabLabel);
            if (banksTab) { _raceOrBanksTabActive = true; _banksTab.Draw(); }
        }

        var settingsFlags = ImGuiTabItemFlags.None;
        if (_selectSettingsTab)
        {
            settingsFlags = ImGuiTabItemFlags.SetSelected;
            _selectSettingsTab = false;
        }
        using (var tab = ImRaii.TabItem("Settings", settingsFlags))
            if (tab) _settingsTab.Draw();

        using (var tab = ImRaii.TabItem("History"))
            if (tab) _historyTab.Draw();

        using (var tab = ImRaii.TabItem("Profit/Loss"))
            if (tab) _profitLossTab.Draw();

        using (var tab = ImRaii.TabItem("Support"))
            if (tab) _supportTab.Draw();
    }

    private void DrawIconWatermark()
    {
        if (_plugin.GameState.Phase != RacePhase.Idle) return;

        var wrap = _icon?.GetWrapOrDefault();
        if (wrap == null) return;

        const float iconSize = 400f;
        var scale = ImGuiHelpers.GlobalScale;
        var scaledSize = iconSize * scale;
        var origin = ImGui.GetCursorScreenPos();
        var region = ImGui.GetContentRegionAvail();
        var centreX = origin.X + (region.X - scaledSize) * 0.5f;
        var centreY = origin.Y + (region.Y - scaledSize) * 0.5f;

        ImGui.GetWindowDrawList().AddImage(
            wrap.Handle,
            new Vector2(centreX, centreY),
            new Vector2(centreX + scaledSize, centreY + scaledSize),
            Vector2.Zero, Vector2.One,
            0x40FFFFFF);
    }
}
