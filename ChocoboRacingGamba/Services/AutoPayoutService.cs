using System;
using ChocoboRacing.Actions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using ECommons.Automation;
using ECommons.Automation.NeoTaskManager;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Component.GUI;

/// <summary>
/// Pays out winners in 1,000,000 gil chunks and deducts the gil transferred from their bank.
/// </summary>
namespace ChocoboRacing.Services;

public sealed unsafe class AutoPayoutService : IDisposable
{
    private const long MaxChunkGil = 1_000_000L;

    private readonly TradeAction _tradeAction;
    private readonly IPluginLog _log;
    private readonly TaskManager _taskManager = new();

    private Func<long>? _getRemainingFn;
    private Action<long>? _deductFn;
    private volatile bool _awaitingResult;
    private string? _awaitingTargetName;
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
            _log.Information("[AutoPayout] Stopped - no further chunks will be sent.");
        IsRunning = false;
        TargetName = null;
        _taskManager.Abort();
    }

    private void Finish(string logMessage)
    {
        _log.Information("[AutoPayout] {Message}", logMessage);
        IsRunning = false;
        TargetName = null;
        _awaitingResult = false;
        _awaitingTargetName = null;
    }

    private void EnqueueChunk()
    {
        var remaining = _getRemainingFn?.Invoke() ?? 0L;
        if (remaining <= 0L) { Finish("Payout complete - all gil transferred."); return; }

        var chunk = (int)Math.Min(remaining, MaxChunkGil);
        _gilTick = 0;
        _awaitingResult = true;
        _awaitingTargetName = TargetName;

        _log.Information("[AutoPayout] Sending trade request to '{Target}' for {Chunk} gil ({Remaining} remaining).", TargetName ?? string.Empty, chunk, remaining);
        _tradeAction.InitiateTrade(TargetName!);
        _taskManager.Enqueue(() => SetGilAmount(chunk));
        _taskManager.EnqueueDelay(300);
        _taskManager.Enqueue(ClickTradeAccept);
        _taskManager.Enqueue(AwaitAndConfirmSelectYesno);
    }

    private bool? SetGilAmount(int chunk)
    {
        if (!_awaitingResult) return true;

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
        if (!_awaitingResult) return true;
        var tradeAddon = Svc.GameGui.GetAddonByName("Trade", 1);
        if (tradeAddon.IsNull) return false;
        var trade = (AtkUnitBase*)tradeAddon.Address;
        if (!trade->IsVisible) return false;
        Callback.Fire(trade, true, 0);
        return true;
    }

    private bool? AwaitAndConfirmSelectYesno()
    {
        if (!_awaitingResult) return true;
        var yesnoAddon = Svc.GameGui.GetAddonByName("SelectYesno", 1);
        if (yesnoAddon.IsNull) return false;
        var addon = (AtkUnitBase*)yesnoAddon.Address;
        if (!addon->IsVisible) return false;
        new AddonMaster.SelectYesno(yesnoAddon.Address).Yes();
        return true;
    }

    private void OnTradeEnd(IPlayerCharacter? counterparty, TradeDetectionManager.TradeDescriptor? result)
    {
        if (!_awaitingResult) return;
        if (counterparty != null && counterparty.Name.TextValue != _awaitingTargetName) return;

        _awaitingResult = false;
        var target = _awaitingTargetName;
        _awaitingTargetName = null;

        if (result == null)
        {
            if (IsRunning) Finish("Trade was cancelled or declined - stopping payout.");
            return;
        }

        var given = result.ReceivedGil < 0 ? -(long)result.ReceivedGil : 0L;
        if (given <= 0L)
        {
            if (IsRunning) Finish("Trade completed but no gil was sent - stopping payout.");
            return;
        }

        _deductFn?.Invoke(given);
        _log.Information("[AutoPayout] Recorded {Given} gil paid to '{Target}'.", given, target ?? string.Empty);

        if (!IsRunning) return;

        var remaining = _getRemainingFn?.Invoke() ?? 0L;
        if (remaining <= 0L)
        {
            Finish("Payout complete - all gil transferred.");
            return;
        }

        _taskManager.EnqueueDelay(1500);
        _taskManager.Enqueue(() => { EnqueueChunk(); return true; });
    }

    public void Dispose()
    {
        TradeDetectionManager.OnTradeEnd -= OnTradeEnd;
        _taskManager.Dispose();
    }
}
