using System;
using System.Linq;
using System.Numerics;
using ChocoboRacing.Models;
using ChocoboRacing.UI.Components;
using ChocoboRacing.UI.Tabs;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

/// <summary>
/// Primary UI window. Hosts a sidebar that switches between the race, banks, webview, history, profit/loss, settings and support sections.
/// </summary>
namespace ChocoboRacing.UI;

public sealed class MainWindow : Window, IDisposable
{
    private enum Tab
    {
        Race,
        Banks,
        Webview,
        History,
        ProfitLoss,
        Settings,
        Support,
    }

    private enum SettingsSection
    {
        ClassicRace,
        ClassicChat,
        GrandNationalChat,
    }

    private enum SupportSection
    {
        Help,
        GameKey,
    }

    private const int ThemeColourCount = 19;
    private const float MinSidebarWidth = 190f;
    private const float MinContentWidth = 680f;
    private const float SubItemIndent = 18f;

    private const string LivePillLabel = "LIVE";

    private static readonly Vector4 LivePillColour = new(0.15f, 0.65f, 0.25f, 1f);

    private readonly Plugin _plugin;
    private readonly HostRaceTab _raceTab;
    private readonly GrandNationalTab _grandNationalTab;
    private readonly BanksTab _banksTab;
    private readonly WebviewTab _webviewTab;
    private readonly SettingsTab _settingsTab;
    private readonly HistoryTab _historyTab;
    private readonly ProfitLossTab _profitLossTab;
    private readonly SupportTab _supportTab;
    private readonly GameKeyTab _gameKeyTab;
    private readonly ISharedImmediateTexture? _icon;

    private Tab _selected = Tab.Race;
    private SettingsSection _settingsSection = SettingsSection.ClassicRace;
    private SupportSection _supportSection = SupportSection.Help;
    private bool _raceExpanded = true;
    private bool _settingsExpanded;
    private bool _supportExpanded;

    public event Action? WindowOpened;

    public MainWindow(Plugin Plugin, string iconPath) : base("Chocobo Racing###ChocoboRacingGamba")
    {
        _plugin = Plugin;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(MinContentWidth + MinSidebarWidth, 450),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        if (System.IO.File.Exists(iconPath))
            _icon = Plugin.TextureProvider.GetFromFile(iconPath);

        var presetSelector = new PresetSelector();

        _raceTab       = new HostRaceTab(Plugin);
        _grandNationalTab = new GrandNationalTab(Plugin);
        _banksTab      = new BanksTab(Plugin);
        _webviewTab    = new WebviewTab(Plugin, presetSelector);
        _settingsTab   = new SettingsTab(Plugin, presetSelector);
        _historyTab    = new HistoryTab(Plugin);
        _profitLossTab = new ProfitLossTab(Plugin);
        _supportTab    = new SupportTab();
        _gameKeyTab    = new GameKeyTab();
    }

    public override void OnOpen() => WindowOpened?.Invoke();

    public void OpenToSettings()
    {
        IsOpen = true;
        _selected = Tab.Settings;
        _settingsSection = SettingsSection.ClassicRace;
        _settingsExpanded = true;
    }

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
        if (_selected == Tab.Banks && _plugin.Configuration.RaceMode != RaceMode.Classic)
            _selected = Tab.Race;

        using (var sidebar = ImRaii.Child("##ChocoboRacingSidebar", new Vector2(CalcSidebarWidth(), 0f), true))
        {
            if (sidebar.Success)
                DrawSidebar();
        }

        ImGui.SameLine();

        using var content = ImRaii.Child("##ChocoboRacingContent", Vector2.Zero, false);
        if (!content.Success)
            return;

        if (_selected is Tab.Race or Tab.Banks)
            DrawIconWatermark();

        DrawSelectedTab();
    }

    private void DrawHostingStatus()
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

    private void DrawTestingModeButton()
    {
        var testLabel = _plugin.IsTestingMode ? "Testing Mode: Enabled" : "Testing Mode: Disabled";
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
    }

    private static float CalcSidebarWidth()
    {
        var style = ImGui.GetStyle();
        var indent = SubItemIndent * ImGuiHelpers.GlobalScale;

        var widest = Math.Max(
            SidebarRow.CalcRowWidth("Grand National Chat", indent, false),
            SidebarRow.CalcRowWidth("Settings", 0f, true));

        var width = widest + style.WindowPadding.X * 2f + style.ScrollbarSize;
        return Math.Max(width, MinSidebarWidth * ImGuiHelpers.GlobalScale);
    }

    private void DrawSidebar()
    {
        ImGuiHelpers.ScaledDummy(4f);
        DrawRaceNav();
        DrawBanksNav();
        DrawWebviewNav();

        ImGuiHelpers.ScaledDummy(4f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(4f);

        DrawNavItem(Tab.History,    FontAwesomeIcon.History,   "History");
        DrawNavItem(Tab.ProfitLoss, FontAwesomeIcon.ChartLine, "Profit/Loss");

        ImGuiHelpers.ScaledDummy(4f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(4f);

        DrawSettingsNav();
        DrawSupportNav();
    }

    private void DrawNavItem(Tab tab, FontAwesomeIcon icon, string label)
    {
        if (SidebarRow.Draw($"tab_{tab}", icon, label, _selected == tab))
            _selected = tab;
    }

    private void DrawRaceNav()
    {
        var chevron = _raceExpanded ? FontAwesomeIcon.ChevronDown : FontAwesomeIcon.ChevronRight;

        if (SidebarRow.Draw("tab_Race", FontAwesomeIcon.Flag, "Race", _selected == Tab.Race, 0f, chevron))
        {
            _raceExpanded = !_raceExpanded;
            _selected = Tab.Race;
        }

        if (!_raceExpanded)
            return;

        DrawModeSubItem(RaceMode.Classic,       FontAwesomeIcon.Dice,   "Classic");
        DrawModeSubItem(RaceMode.GrandNational, FontAwesomeIcon.Trophy, "Grand National");
    }

    private void DrawModeSubItem(RaceMode mode, FontAwesomeIcon icon, string label)
    {
        var cfg = _plugin.Configuration;
        var isActiveMode = cfg.RaceMode == mode;
        var canSwitch = _plugin.GameState.Phase == RacePhase.Idle
            && _plugin.GrandNationalState.Phase == GrandNationalPhase.Idle;
        var locked = !canSwitch && !isActiveMode;
        var indent = SubItemIndent * ImGuiHelpers.GlobalScale;

        using (ImRaii.Disabled(locked))
        {
            if (SidebarRow.Draw($"mode_{mode}", icon, label, _selected == Tab.Race && isActiveMode, indent,
                    contentColour: locked ? UiColors.Muted : null))
            {
                _selected = Tab.Race;
                if (!isActiveMode)
                {
                    cfg.RaceMode = mode;
                    cfg.Save();
                }
            }
        }

        if (locked && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Finish the current race to switch mode.");
    }

    private void DrawWebviewNav()
    {
        if (_plugin.WebMirror.SessionId == null)
        {
            DrawNavItem(Tab.Webview, FontAwesomeIcon.Globe, "Webview");
            return;
        }

        var pulse = (float)(0.5 + 0.5 * Math.Sin(ImGui.GetTime() * 3.0));
        var pill = LivePillColour with { W = 0.45f + 0.55f * pulse };

        if (SidebarRow.Draw("tab_Webview", FontAwesomeIcon.Globe, "Webview", _selected == Tab.Webview, 0f,
                contentColour: UiColors.Positive, trailingPill: LivePillLabel, trailingPillColour: pill))
            _selected = Tab.Webview;

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Web session is live.");
    }

    private void DrawBanksNav()
    {
        if (_plugin.Configuration.RaceMode != RaceMode.Classic)
        {
            DrawDisabledBanksNav();
            return;
        }

        var cashOutRequests = _plugin.GameState.GetActiveCashOutRequests();
        var cashOutCount = _plugin.GameState.GetBanksSnapshot()
            .Count(b => !b.IsArchived && cashOutRequests.Contains($"{b.Name}@{b.World}"));

        var label = cashOutCount > 0 ? $"Banks ({cashOutCount})" : "Banks";

        Vector4? highlight = null;
        if (cashOutCount > 0)
        {
            var pulse = (float)(0.5 + 0.5 * Math.Sin(ImGui.GetTime() * 4.0));
            highlight = new Vector4(0.55f, 0.25f + 0.25f * pulse, 0f, 1f);
        }

        if (SidebarRow.Draw("tab_Banks", FontAwesomeIcon.PiggyBank, label, _selected == Tab.Banks, 0f, null, null, highlight))
            _selected = Tab.Banks;
    }

    private static void DrawDisabledBanksNav()
    {
        using (ImRaii.Disabled(true))
            SidebarRow.Draw("tab_Banks", FontAwesomeIcon.PiggyBank, "Banks", false, 0f, contentColour: UiColors.Muted);

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Banks are only available during Classic races.");
    }

    private void DrawSettingsNav()
    {
        var chevron = _settingsExpanded ? FontAwesomeIcon.ChevronDown : FontAwesomeIcon.ChevronRight;

        if (SidebarRow.Draw("tab_Settings", FontAwesomeIcon.Cog, "Settings", _selected == Tab.Settings, 0f, chevron))
        {
            _settingsExpanded = !_settingsExpanded;
            if (_settingsExpanded)
            {
                _selected = Tab.Settings;
                _settingsSection = SettingsSection.ClassicRace;
            }
        }

        if (!_settingsExpanded)
            return;

        DrawSettingsSubItem(SettingsSection.ClassicRace,      FontAwesomeIcon.SlidersH,    "Classic Race");
        DrawSettingsSubItem(SettingsSection.ClassicChat,       FontAwesomeIcon.CommentDots, "Classic Chat");
        DrawSettingsSubItem(SettingsSection.GrandNationalChat, FontAwesomeIcon.Comments,    "Grand National Chat");
    }

    private void DrawSettingsSubItem(SettingsSection section, FontAwesomeIcon icon, string label)
    {
        var selected = _selected == Tab.Settings && _settingsSection == section;
        var indent = SubItemIndent * ImGuiHelpers.GlobalScale;

        if (!SidebarRow.Draw($"settings_{section}", icon, label, selected, indent))
            return;

        _selected = Tab.Settings;
        _settingsSection = section;
    }

    private void DrawSupportNav()
    {
        var chevron = _supportExpanded ? FontAwesomeIcon.ChevronDown : FontAwesomeIcon.ChevronRight;

        if (SidebarRow.Draw("tab_Support", FontAwesomeIcon.QuestionCircle, "Support", _selected == Tab.Support, 0f, chevron))
        {
            _supportExpanded = !_supportExpanded;
            if (_supportExpanded)
            {
                _selected = Tab.Support;
                _supportSection = SupportSection.Help;
            }
        }

        if (!_supportExpanded)
            return;

        DrawSupportSubItem(SupportSection.Help,   FontAwesomeIcon.InfoCircle, "Help");
        DrawSupportSubItem(SupportSection.GameKey, FontAwesomeIcon.Key,        "Game Key");
    }

    private void DrawSupportSubItem(SupportSection section, FontAwesomeIcon icon, string label)
    {
        var selected = _selected == Tab.Support && _supportSection == section;
        var indent = SubItemIndent * ImGuiHelpers.GlobalScale;

        if (!SidebarRow.Draw($"support_{section}", icon, label, selected, indent))
            return;

        _selected = Tab.Support;
        _supportSection = section;
    }

    private void DrawSelectedTab()
    {
        switch (_selected)
        {
            case Tab.Race:       DrawRaceContent(); break;
            case Tab.Banks:      _banksTab.Draw(); break;
            case Tab.Webview:    _webviewTab.Draw(); break;
            case Tab.History:    _historyTab.Draw(); break;
            case Tab.ProfitLoss: _profitLossTab.Draw(); break;
            case Tab.Settings:   DrawSettingsContent(); break;
            case Tab.Support:    DrawSupportContent(); break;
        }
    }

    private void DrawSupportContent()
    {
        switch (_supportSection)
        {
            case SupportSection.Help:   _supportTab.Draw(); break;
            case SupportSection.GameKey: _gameKeyTab.Draw(); break;
        }
    }

    private void DrawRaceContent()
    {
        if (_plugin.Configuration.RaceMode == RaceMode.GrandNational)
        {
            DrawTestingModeButton();
            ImGui.Separator();
            _grandNationalTab.Draw();
            return;
        }

        if (_plugin.GameState.IsHosting)
            DrawHostingStatus();

        DrawTestingModeButton();
        ImGui.Separator();
        _raceTab.Draw();
    }

    private void DrawSettingsContent()
    {
        switch (_settingsSection)
        {
            case SettingsSection.ClassicRace:      _settingsTab.DrawClassicRaceSection(); break;
            case SettingsSection.ClassicChat:       _settingsTab.DrawClassicChatSection(); break;
            case SettingsSection.GrandNationalChat: _settingsTab.DrawGrandNationalChatSection(); break;
        }
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

    public void Dispose() => (_icon as IDisposable)?.Dispose();
}
