using System;
using System.Collections.Generic;
using System.Diagnostics;
using Dalamud.Plugin.Services;

namespace ChocoboRacing.Automation;

/// <summary>
/// Manages delayed actions on the main framework thread.
/// Ensure safe execution of actions.
/// </summary>
public sealed class ActionQueue : IDisposable
{
    private readonly IFramework _framework;
    private readonly IPluginLog _log;
    private readonly List<(Stopwatch Timer, long DelayMs, Action Action)> _pendingActions = new();
    private readonly object _pendingLock = new();

    public ActionQueue(IFramework framework, IPluginLog log)
    {
        _framework = framework;
        _log = log;
        _framework.Update += OnFrameworkUpdate;
    }

    public void Dispose()
    {
        _framework.Update -= OnFrameworkUpdate;
    }

    public void ScheduleDelayedAction(long delayMs, Action action)
    {
        var timer = Stopwatch.StartNew();
        lock (_pendingLock)
        {
            _pendingActions.Add((timer, delayMs, action));
        }
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        List<Action> due = new();
        lock (_pendingLock)
        {
            for (var i = _pendingActions.Count - 1; i >= 0; i--)
            {
                var item = _pendingActions[i];
                if (item.Timer.ElapsedMilliseconds >= item.DelayMs)
                {
                    _pendingActions.RemoveAt(i);
                    due.Add(item.Action);
                }
            }
        }

        foreach (var action in due)
        {
            try { action(); }
            catch (Exception ex) { _log.Error(ex, "Delayed action failed."); }
        }
    }
}
