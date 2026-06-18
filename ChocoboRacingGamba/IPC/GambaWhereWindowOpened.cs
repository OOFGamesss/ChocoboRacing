using System;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using ChocoboRacing.UI;

namespace ChocoboRacing.IPC;

/// <summary>
/// Notifies GambaWhere when the Chocobo Racing window opens, so it can offer to start a session.
/// </summary>
public sealed class GambaWhereWindowOpened : IDisposable
{
    private const string PluginName = "Chocobo Racing";
    private const string Category = "Chocobo Racing";
    private const string Gate = "GambaWhere.WindowOpened";

    private readonly ICallGateSubscriber<string, string, bool> _windowOpened;
    private readonly IPluginLog _log;
    private readonly MainWindow _mainWindow;

    public GambaWhereWindowOpened(
        IDalamudPluginInterface pluginInterface,
        IPluginLog log,
        MainWindow mainWindow)
    {
        _log = log;
        _mainWindow = mainWindow;
        _windowOpened = pluginInterface.GetIpcSubscriber<string, string, bool>(Gate);
        _mainWindow.WindowOpened += OnWindowOpened;
    }

    public void Dispose()
    {
        _mainWindow.WindowOpened -= OnWindowOpened;
    }

    private void OnWindowOpened()
    {
        try
        {
            _windowOpened.InvokeFunc(PluginName, Category);
        }
        catch (Exception ex)
        {
            _log.Debug($"GambaWhere WindowOpened IPC unavailable: {ex.Message}");
        }
    }
}
