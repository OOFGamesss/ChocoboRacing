using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ChocoboRacing.Actions;
using Dalamud.Plugin.Services;

/// <summary>
/// Serialises every outgoing chat command, leaving at least 500ms between sends and 2.5s after a /tell.
/// </summary>
namespace ChocoboRacing.Automation;

public sealed class ChatQueue : IDisposable
{
    private const int DefaultGapMs = 500;
    private const int TellGapMs = 2500;
    private const int Unbatched = 0;

    private sealed record Entry(string Command, bool EchoInTesting, int TrailingGapMs, int BatchId, Action? OnSent);

    private readonly IFramework _framework;
    private readonly IChatGui _chatGui;
    private readonly IPluginLog _log;
    private readonly Func<bool> _isTestingMode;
    private readonly LinkedList<Entry> _queue = new();
    private readonly object _lock = new();
    private readonly Stopwatch _sinceLastSend = new();

    private int _gapBeforeNextMs;
    private int _batchCounter;
    private int _currentBatchId;

    public ChatQueue(IFramework framework, IChatGui chatGui, IPluginLog log, Func<bool> isTestingMode)
    {
        _framework = framework;
        _chatGui = chatGui;
        _log = log;
        _isTestingMode = isTestingMode;
        _framework.Update += OnFrameworkUpdate;
    }

    public int StartBatch()
    {
        lock (_lock)
            return _currentBatchId = ++_batchCounter;
    }

    public bool IsBatchPending(int batchId)
    {
        if (batchId == Unbatched) return false;
        lock (_lock)
            return _queue.Any(e => e.BatchId == batchId);
    }

    public void Enqueue(string command, bool echoInTesting = false, int trailingGapMs = 0, int batchId = Unbatched, Action? onSent = null)
    {
        if (string.IsNullOrWhiteSpace(command)) return;
        lock (_lock)
            _queue.AddLast(new Entry(command, echoInTesting, trailingGapMs, batchId, onSent));
    }

    public void ClearPendingAnnouncements()
    {
        lock (_lock)
        {
            var node = _queue.First;
            while (node != null)
            {
                var next = node.Next;
                if (node.Value.BatchId != Unbatched)
                    _queue.Remove(node);
                node = next;
            }

            _currentBatchId = ++_batchCounter;
        }
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        Entry? entry;
        lock (_lock)
            entry = TakeNextDue();

        if (entry == null) return;
        Send(entry);
    }

    private Entry? TakeNextDue()
    {
        while (_queue.First != null)
        {
            var next = _queue.First.Value;

            if (next.BatchId != Unbatched && next.BatchId != _currentBatchId)
            {
                _queue.RemoveFirst();
                continue;
            }

            if (_sinceLastSend.IsRunning && _sinceLastSend.ElapsedMilliseconds < _gapBeforeNextMs)
                return null;

            _queue.RemoveFirst();
            _gapBeforeNextMs = Math.Max(next.TrailingGapMs, IsTell(next.Command) ? TellGapMs : DefaultGapMs);
            _sinceLastSend.Restart();
            return next;
        }

        return null;
    }

    private void Send(Entry entry)
    {
        try
        {
            if (entry.EchoInTesting)
                ChatAction.SendChatMessage(entry.Command, _chatGui, _isTestingMode());
            else
                ChatAction.SendChatMessage(entry.Command);

            entry.OnSent?.Invoke();
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Queued chat command failed.");
        }
    }

    private static bool IsTell(string command) =>
        command.StartsWith("/tell ", StringComparison.OrdinalIgnoreCase)
        || command.StartsWith("/t ", StringComparison.OrdinalIgnoreCase);

    public void Dispose() => _framework.Update -= OnFrameworkUpdate;
}
