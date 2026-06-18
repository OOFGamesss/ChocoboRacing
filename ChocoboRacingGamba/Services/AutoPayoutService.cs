using System;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using ECommons.Automation;
using ECommons.Automation.NeoTaskManager;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ChocoboRacing.Actions;

namespace ChocoboRacing.Services;

/// <summary>Automates sequential winner payouts in 1,000,000 gil chunks, interacting with the Trade, InputNumeric, and SelectYesno addons via ECommons callbacks. Cancels automatically if the trade is declined.</summary>
public sealed unsafe class AutoPayoutService : IDisposable
{
    private const long MaxChunkGil = 1_000_000L;
    private const int ChunkTimeoutSeconds = 120;

    private readonly TradeAction _tradeAction;
    private readonly IPluginLog _log;
    private readonly TaskManager _taskManager = new();

    private Func<long>? _getRemainingFn;
    private Action<long>? _deductFn;
    private volatile bool _tradeCompleted;
    private volatile bool _tradeCancelled;
    private DateTime _chunkDeadline;
    private int _gilTick;

    public bool IsRunning { get; private set; }
    public string? TargetName { get; private set; }

    public AutoPayoutService(TradeAction tradeAction, IPluginLog log)
    {
        _tradeAction = tradeAction;
        _log = log;
        TradeDetectionManager.OnTradeEnd += OnTradeEnd;
    }

    public void Start(string targetName, Func<long> getRemainingFn, Action<long> deductFn)
    {
        if (IsRunning) return;
        TargetName = targetName;
        _getRemainingFn = getRemainingFn;
        _deductFn = deductFn;
        IsRunning = true;
        EnqueueChunk();
    }

    public void Stop()
    {
        if (IsRunning)
            _log.Information("[AutoPayout] Stopped.");
        IsRunning = false;
        TargetName = null;
        _tradeCompleted = false;
        _tradeCancelled = false;
        _taskManager.Abort();
    }

    private void Finish(string logMessage)
    {
        _log.Information("[AutoPayout] {Message}", logMessage);
        IsRunning = false;
        TargetName = null;
        _tradeCompleted = false;
        _tradeCancelled = false;
    }

    private void EnqueueChunk()
    {
        var remaining = _getRemainingFn?.Invoke() ?? 0L;
        if (remaining <= 0L) { Stop(); return; }

        var chunk = (int)Math.Min(remaining, MaxChunkGil);
        _tradeCompleted = false;
        _tradeCancelled = false;
        _gilTick = 0;
        _chunkDeadline = DateTime.UtcNow.AddSeconds(ChunkTimeoutSeconds);

        _log.Information("[AutoPayout] Sending trade request to '{Target}' for {Chunk} gil ({Remaining} remaining).", TargetName ?? string.Empty, chunk, remaining);
        _tradeAction.InitiateTrade(TargetName!);
        _taskManager.Enqueue(() => SetGilAmount(chunk));
        _taskManager.EnqueueDelay(300);
        _taskManager.Enqueue(ClickTradeAccept);
        _taskManager.Enqueue(AwaitAndConfirmSelectYesno);
        _taskManager.Enqueue(AwaitTradeEnd);
        _taskManager.Enqueue(() => OnChunkComplete(chunk));
    }

    private bool? SetGilAmount(int chunk)
    {
        if (_tradeCancelled || IsExpired()) return true;

        var inAddon = Svc.GameGui.GetAddonByName("InputNumeric", 1);
        if (!inAddon.IsNull && ((AtkUnitBase*)inAddon.Address)->IsVisible)
        {
            new AddonMaster.InputNumeric(inAddon.Address).Ok(chunk);
            return true;
        }

        var tradeAddon = Svc.GameGui.GetAddonByName("Trade", 1);
        if (tradeAddon.IsNull || !((AtkUnitBase*)tradeAddon.Address)->IsVisible) return false;

        if (++_gilTick % 60 == 1)
            Callback.Fire((AtkUnitBase*)tradeAddon.Address, true, 2);

        return false;
    }

    private bool? ClickTradeAccept()
    {
        if (_tradeCancelled || IsExpired()) return true;
        var tradeAddon = Svc.GameGui.GetAddonByName("Trade", 1);
        if (tradeAddon.IsNull) return false;
        var trade = (AtkUnitBase*)tradeAddon.Address;
        if (!trade->IsVisible) return false;
        Callback.Fire(trade, true, 0);
        return true;
    }

    private bool? AwaitAndConfirmSelectYesno()
    {
        if (_tradeCancelled || IsExpired()) return true;
        var yesnoAddon = Svc.GameGui.GetAddonByName("SelectYesno", 1);
        if (yesnoAddon.IsNull) return false;
        var addon = (AtkUnitBase*)yesnoAddon.Address;
        if (!addon->IsVisible) return false;
        new AddonMaster.SelectYesno(yesnoAddon.Address).Yes();
        return true;
    }

    private bool? AwaitTradeEnd()
    {
        if (_tradeCancelled || _tradeCompleted || IsExpired()) return true;
        return false;
    }

    private bool? OnChunkComplete(long chunkAmount)
    {
        if (!IsRunning) return true;

        if (_tradeCancelled)
        {
            Finish("Trade was cancelled by the other party - stopping payout.");
            return true;
        }

        if (IsExpired())
        {
            Finish($"Chunk timed out after {ChunkTimeoutSeconds}s - stopping payout.");
            return true;
        }

        _deductFn?.Invoke(chunkAmount);

        var remaining = _getRemainingFn?.Invoke() ?? 0L;
        if (remaining <= 0L)
        {
            Finish("Payout complete - all gil transferred.");
            return true;
        }

        _taskManager.EnqueueDelay(1500);
        _taskManager.Enqueue(() => { EnqueueChunk(); return true; });
        return true;
    }

    private bool IsExpired() => DateTime.UtcNow >= _chunkDeadline;

    private void OnTradeEnd(IPlayerCharacter? counterparty, TradeDetectionManager.TradeDescriptor? result)
    {
        if (!IsRunning) return;
        if (result == null)
            _tradeCancelled = true;
        else
            _tradeCompleted = true;
    }

    public void Dispose()
    {
        TradeDetectionManager.OnTradeEnd -= OnTradeEnd;
        _taskManager.Dispose();
    }
}
