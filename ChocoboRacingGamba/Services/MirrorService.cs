using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using ChocoboRacing.API;
using ChocoboRacing.Config;
using ChocoboRacing.Models;
using ChocoboRacing.State;
using ChocoboRacing.Utility;
using Dalamud.Plugin.Services;

/// <summary>
/// Mirrors race state to ChocoboRacingAPI and pulls web bets into the local ledger.
/// </summary>
namespace ChocoboRacing.Services;

public sealed class MirrorService : IDisposable
{
    private const string ApiBaseUrl = "https://api.oofgames.fyi/v1/chocobo-racing/";
    private const long TickIntervalMs = 250;
    private const long HeartbeatMs = 30_000;
    private const long WebBetPollMs = 2_000;

    private readonly PluginConfig _config;
    private readonly RaceState _state;
    private readonly IFramework _framework;
    private readonly IPluginLog _log;
    private readonly ChocoboApiClient _client;

    private readonly HashSet<long> _appliedWebBets = new();
    private volatile bool _disposed;
    private volatile bool _flushing;
    private bool _busy;
    private bool _needReconcile;
    private long _lastTick;
    private long _lastHeartbeat;
    private long _lastWebBetPoll;
    private long _nextSyncAttempt;
    private int _syncFailures;
    private string _lastFullSig = string.Empty;
    private string _lastPositionsSig = string.Empty;

    public string? SessionId { get; private set; }
    public string? SpectatorUrl { get; private set; }
    public string Status { get; private set; } = "Idle";
    public bool Connected { get; private set; }
    public bool StatusIsError { get; private set; }

    public MirrorService(PluginConfig config, RaceState state, IFramework framework, IPluginLog log)
    {
        _config = config;
        _state = state;
        _framework = framework;
        _log = log;
        _client = new ChocoboApiClient(() => ApiBaseUrl, () => _config.ApiHostKey, log);

        SessionId = string.IsNullOrEmpty(config.WebSessionId) ? null : config.WebSessionId;
        SpectatorUrl = string.IsNullOrEmpty(config.WebSpectatorUrl) ? null : config.WebSpectatorUrl;
        _needReconcile = SessionId != null;
        _framework.Update += OnUpdate;
    }

    public void GoLive()
    {
        if (_disposed) return;
        if (string.IsNullOrWhiteSpace(_config.ApiHostKey)) { SetStatus("Enter your API key first.", true); return; }
        var identity = LocalIdentity();
        if (string.IsNullOrEmpty(identity.Name) || string.IsNullOrEmpty(identity.World))
        {
            SetStatus("Log in to a character before going live.", true);
            return;
        }
        SetStatus("Going live...", false);
        _ = GoLiveAsync(identity);
    }

    public void EndSession()
    {
        var id = SessionId;
        _config.WebMirrorEnabled = false;
        _config.WebSessionId = string.Empty;
        _config.WebSpectatorUrl = string.Empty;
        _config.WebPins.Clear();
        _config.Save();
        SessionId = null;
        SpectatorUrl = null;
        Connected = false;
        _appliedWebBets.Clear();
        SetStatus("Idle", false);
        if (id != null) _ = _client.EndSessionAsync(id);
    }

    public async Task<RollResponse?> RollGrandNationalAsync()
    {
        if (_disposed || SessionId == null) return null;
        var snap = CaptureSnapshot();
        await _client.SyncStateAsync(SessionId, ToSyncPayload(snap)).ConfigureAwait(false);
        _lastFullSig = string.Empty;
        return await _client.RollGrandNationalAsync(SessionId).ConfigureAwait(false);
    }

    public void FlushFinishedState()
    {
        if (_disposed) return;
        if (!_config.WebMirrorEnabled || string.IsNullOrWhiteSpace(_config.ApiHostKey) || SessionId == null) return;

        var snap = CaptureSnapshot();
        if (snap.Phase != "Finished" || snap.WinningChocobo <= 0) return;

        _flushing = true;
        _ = FlushFinishedAsync(snap);
    }

    private void SetStatus(string text, bool isError)
    {
        Status = text;
        StatusIsError = isError;
    }

    private async Task FlushFinishedAsync(Snapshot snap)
    {
        try
        {
            if (await _client.SyncStateAsync(SessionId!, ToSyncPayload(snap)).ConfigureAwait(false))
                MarkSynced(FullSig(snap, includeSettings: true), PositionsSig(snap), Environment.TickCount64);
        }
        catch (Exception ex) { _log.Warning($"Mirror flush error: {ex.Message}"); }
        finally { _flushing = false; }
    }

    private async Task GoLiveAsync((string ContentId, string Name, string World) identity)
    {
        var venueName = (_config.WebVenueName ?? string.Empty).Trim();
        var venueImage = (_config.WebVenueImageUrl ?? string.Empty).Trim();

        var nameCheck = VenueValidator.ValidateName(venueName);
        if (!nameCheck.Ok) { SetStatus(nameCheck.Error, true); Connected = false; return; }

        var imageCheck = await ValidateVenueImageAsync(venueImage).ConfigureAwait(false);
        if (!imageCheck.Ok) { SetStatus(imageCheck.Error, true); Connected = false; return; }

        var (resp, statusCode) = await _client.CreateSessionAsync(new CreateSessionRequest
        {
            HostName = identity.Name,
            VenueName = venueName,
            VenueImageUrl = venueImage,
            CharacterId = identity.ContentId,
            CharacterName = identity.Name,
            World = identity.World,
        }).ConfigureAwait(false);
        if (resp == null || _disposed)
        {
            if (!_disposed)
            {
                Connected = false;
                SetStatus(statusCode switch
                {
                    401 => "Invalid API key. Please contact Felix.",
                    403 => "This API key has been disabled for key sharing. Please contact Felix.",
                    404 => "Betting service unavailable. Please contact Felix.",
                    0 => "Could not reach the server. Check your connection and try again.",
                    _ => $"Could not create session (error {statusCode}).",
                }, true);
            }
            return;
        }

        await RunOnFramework(() =>
        {
            SessionId = resp.SessionId;
            SpectatorUrl = resp.SpectatorUrl;
            _config.WebSessionId = resp.SessionId;
            _config.WebSpectatorUrl = resp.SpectatorUrl;
            _config.WebMirrorEnabled = true;
            _config.RoundNumber = 1;
            _config.GrandNationalRaceNumber = 1;
            _config.Save();
        }).ConfigureAwait(false);

        _appliedWebBets.Clear();
        _lastFullSig = string.Empty;
        _lastPositionsSig = string.Empty;
        _syncFailures = 0;
        _nextSyncAttempt = 0;
        _needReconcile = false;
        Connected = true;
        SetStatus("Live", false);
    }

    private async Task<(bool Ok, string Error)> ValidateVenueImageAsync(string url)
    {
        var format = VenueValidator.ValidateImageUrl(url);
        if (!format.Ok) return format;
        if (string.IsNullOrWhiteSpace(url)) return (true, string.Empty);

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return (false, "Could not download the venue image.");
            if (resp.Content.Headers.ContentLength is > 12_000_000) return (false, "Venue image file is too large (max 12MB).");

            var bytes = await resp.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            if (bytes.Length > 12_000_000) return (false, "Venue image file is too large (max 12MB).");
            if (!VenueValidator.TryReadSize(bytes, out var w, out var h)) return (false, "Could not read the venue image dimensions.");
            if (w > VenueValidator.MaxImageDimension || h > VenueValidator.MaxImageDimension)
                return (false, $"Venue image must be no larger than {VenueValidator.MaxImageDimension}x{VenueValidator.MaxImageDimension} pixels.");
            return (true, string.Empty);
        }
        catch (OperationCanceledException) { return (false, "Venue image check was cancelled."); }
        catch (Exception ex) { return (false, $"Venue image check failed: {ex.Message}"); }
    }

    private void OnUpdate(IFramework framework)
    {
        if (_disposed || _busy || _flushing) return;
        if (!_config.WebMirrorEnabled || string.IsNullOrWhiteSpace(_config.ApiHostKey) || SessionId == null) return;

        var now = Environment.TickCount64;
        if (now - _lastTick < TickIntervalMs) return;
        _lastTick = now;

        var snap = CaptureSnapshot();
        _busy = true;
        _ = TickAsync(snap);
    }

    private async Task TickAsync(Snapshot snap)
    {
        try
        {
            if (_needReconcile)
            {
                await ReconcileAsync().ConfigureAwait(false);
                _needReconcile = false;
            }

            await PushStateAsync(snap).ConfigureAwait(false);

            var now = Environment.TickCount64;
            if (snap.Mode == "classic" && (snap.Phase == "Betting" || snap.Phase == "BetsClosed") && now - _lastWebBetPoll > WebBetPollMs)
            {
                _lastWebBetPoll = now;
                await PollWebBetsAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex) { _log.Warning($"Mirror tick error: {ex.Message}"); }
        finally { _busy = false; }
    }

    private async Task PushStateAsync(Snapshot snap)
    {
        var now = Environment.TickCount64;
        if (now < _nextSyncAttempt) return;

        var fullSig = FullSig(snap, includeSettings: snap.Phase != "Idle");
        var posSig = PositionsSig(snap);

        bool attempted = false;
        bool ok = true;

        if (fullSig != _lastFullSig)
        {
            attempted = true;
            if (await _client.SyncStateAsync(SessionId!, ToSyncPayload(snap)).ConfigureAwait(false))
                MarkSynced(fullSig, posSig, now);
            else ok = false;
        }
        else if (snap.Phase != "Idle" && posSig != _lastPositionsSig)
        {
            attempted = true;
            if (await _client.PushCallCountsAsync(SessionId!, ToCallCounts(snap)).ConfigureAwait(false))
            { _lastPositionsSig = posSig; _lastHeartbeat = now; SetConnected(true); }
            else ok = false;
        }
        else if (now - _lastHeartbeat > HeartbeatMs)
        {
            attempted = true;
            if (await _client.SyncStateAsync(SessionId!, ToSyncPayload(snap)).ConfigureAwait(false))
            { _lastHeartbeat = now; SetConnected(true); }
            else ok = false;
        }

        if (!attempted) return;
        if (ok)
        {
            _syncFailures = 0;
            _nextSyncAttempt = 0;
        }
        else
        {
            SetConnected(false);
            _syncFailures++;
            _nextSyncAttempt = now + SyncBackoffMs(_syncFailures);
        }
    }

    private static long SyncBackoffMs(int failures)
    {
        var step = Math.Min(Math.Max(failures, 1), 4);
        return 1000L * (1 << (step - 1));
    }

    private void MarkSynced(string fullSig, string posSig, long now)
    {
        _lastFullSig = fullSig;
        _lastPositionsSig = posSig;
        _lastHeartbeat = now;
        SetConnected(true);
    }

    private async Task PollWebBetsAsync()
    {
        var resp = await _client.GetWebBetsAsync(SessionId!).ConfigureAwait(false);
        if (resp == null || resp.WebBets.Count == 0) return;

        var toApply = resp.WebBets.Where(b => !_appliedWebBets.Contains(b.Id)).ToList();
        if (toApply.Count > 0 && !_disposed)
            await RunOnFramework(() => ApplyWebBets(toApply)).ConfigureAwait(false);

        var ids = resp.WebBets.Select(b => b.Id).ToList();
        if (await _client.AckWebBetsAsync(SessionId!, new WebBetAckRequest { Ids = ids, Status = "applied" }).ConfigureAwait(false))
            _lastFullSig = string.Empty;
    }

    private async Task ReconcileAsync()
    {
        var master = await _client.GetMasterStateAsync(SessionId!).ConfigureAwait(false);
        if (master == null) return;

        if (master.PendingWebBets.Count > 0 && !_disposed)
        {
            await RunOnFramework(() => ApplyWebBets(master.PendingWebBets)).ConfigureAwait(false);
            await _client.AckWebBetsAsync(
                SessionId!,
                new WebBetAckRequest { Ids = master.PendingWebBets.Select(b => b.Id).ToList(), Status = "applied" }
            ).ConfigureAwait(false);
        }
        _lastFullSig = string.Empty;
    }

    private void ApplyWebBets(List<WebBetDto> bets)
    {
        foreach (var b in bets)
        {
            if (_appliedWebBets.Add(b.Id))
                _state.AddBet(b.Name, b.World, b.Chocobo, b.Amount);
        }
    }

    private Snapshot CaptureSnapshot()
    {
        if (_config.RaceMode == RaceMode.GrandNational)
            return CaptureGrandNationalSnapshot();

        var snap = new Snapshot
        {
            Phase = _state.Phase.ToString(),
            Round = _state.RoundNumber,
            ChocoboCount = _state.ChocoboCount,
            FinishLine = _state.FinishLine,
            Odds = _state.PayoutOdds,
            PerfectRace = _config.PerfectRace,
            PerfectRaceOdds = _config.PerfectRaceOdds,
            MaxBet = _config.MaxBetPerChocobo,
            Positions = _state.ChocoboPositions.Take(_state.ChocoboCount).ToList(),
            Names = _state.ListChocoboNames(),
            HostName = LocalHostName(),
            VenueName = (_config.WebVenueName ?? string.Empty).Trim(),
            VenueImageUrl = (_config.WebVenueImageUrl ?? string.Empty).Trim(),
        };
        snap.WinningChocobo = WinningFrom(snap.Positions, snap.FinishLine);

        var seenBanks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var bank in _state.GetBanksSnapshot().Where(b => !b.IsArchived))
            if (seenBanks.Add($"{bank.Name}@{bank.World}"))
                snap.Banks.Add(new BankDto { Name = bank.Name, World = bank.World, Balance = bank.Balance });
        foreach (var bet in _state.Bets.Where(b => b.Status == BetStatus.Confirmed))
            snap.Bets.Add(new BetDto { Name = bet.PlayerName, World = bet.PlayerWorld, Chocobo = bet.ChocoboNumber, Amount = bet.Amount });
        foreach (var kv in _config.WebPins)
        {
            var at = kv.Key.LastIndexOf('@');
            if (at > 0 && at < kv.Key.Length - 1)
                snap.Pins.Add(new PinDto { Pin = kv.Value, Name = kv.Key[..at], World = kv.Key[(at + 1)..] });
        }
        return snap;
    }

    private Snapshot CaptureGrandNationalSnapshot()
    {
        var snap = new Snapshot
        {
            Mode = "grand_national",
            Phase = MapGrandNationalPhase(_config.GrandNationalPhase),
            Round = _config.RoundNumber,
            FinishLine = _config.GrandNationalFinishLine,
            HostName = LocalHostName(),
            VenueName = (_config.WebVenueName ?? string.Empty).Trim(),
            VenueImageUrl = (_config.WebVenueImageUrl ?? string.Empty).Trim(),
            Pot = GrandNationalMath.Pot(_config),
            EntryFee = _config.GrandNationalEntryFee,
            Boost = _config.GrandNationalBoost,
            VenueCutPercent = _config.GrandNationalVenueCutPercent,
            Winner = _config.GrandNationalWinner,
            PrizeType = GrandNationalMath.PrizeTypeWire(_config),
            PrizeLabel = GrandNationalMath.PrizeLabel(_config),
        };
        snap.Runners.AddRange(GrandNationalRunners());
        return snap;
    }

    private List<RunnerDto> GrandNationalRunners()
    {
        var runners = new List<RunnerDto>();
        if (_config.GrandNationalPhase >= GrandNationalPhase.Closed)
        {
            foreach (var r in _config.GrandNationalGrid)
                runners.Add(new RunnerDto { Number = r.Number, Name = r.Name, World = r.World });
            return runners;
        }

        var number = 1;
        foreach (var entry in GrandNationalMath.OrderedRunnerEntries(_config))
        {
            runners.Add(new RunnerDto
            {
                Number = number++,
                Name = GrandNationalState.DisplayName(entry),
                World = GrandNationalState.WorldOf(entry),
            });
        }
        return runners;
    }

    private static string MapGrandNationalPhase(GrandNationalPhase phase) => phase switch
    {
        GrandNationalPhase.Registration => "Betting",
        GrandNationalPhase.Closed => "BetsClosed",
        GrandNationalPhase.Racing => "Racing",
        GrandNationalPhase.Finished => "Finished",
        _ => "Idle",
    };

    private static int WinningFrom(List<int> positions, int finishLine)
    {
        for (var i = 0; i < positions.Count; i++)
            if (finishLine > 0 && positions[i] >= finishLine) return i + 1;
        return 0;
    }

    private static string PositionsSig(Snapshot s) =>
        $"{s.Phase}|{s.WinningChocobo}|{string.Join(",", s.Positions)}";

    private static string FullSig(Snapshot s, bool includeSettings)
    {
        var sb = new StringBuilder();
        var inv = CultureInfo.InvariantCulture;
        if (s.Mode == "grand_national")
        {
            sb.Append("gn|").Append(s.Phase).Append('|').Append(s.Round).Append('|').Append(s.Winner).Append('|')
              .Append(s.FinishLine).Append('|')
              .Append(s.Pot).Append('|').Append(s.EntryFee).Append('|').Append(s.Boost).Append('|')
              .Append(s.VenueCutPercent.ToString(inv)).Append('|')
              .Append(s.PrizeType).Append('|').Append(s.PrizeLabel).Append('|')
              .Append(string.Join(",", s.Runners.Select(r => $"{r.Number}:{r.Name}:{r.World}"))).Append('|')
              .Append(s.HostName).Append('|').Append(s.VenueName).Append('|').Append(s.VenueImageUrl);
            return sb.ToString();
        }
        sb.Append(s.Phase).Append('|').Append(s.Round).Append('|').Append(s.WinningChocobo).Append('|');
        if (includeSettings)
            sb.Append(s.ChocoboCount).Append('|').Append(s.FinishLine).Append('|').Append(s.Odds.ToString(inv)).Append('|')
              .Append(s.PerfectRace).Append('|').Append(s.PerfectRaceOdds.ToString(inv)).Append('|').Append(s.MaxBet).Append('|')
              .Append(string.Join(",", s.Names)).Append('|');
        sb.Append(string.Join(",", s.Banks.Select(b => $"{b.Name}:{b.World}:{b.Balance}"))).Append('|')
          .Append(string.Join(",", s.Bets.Select(b => $"{b.Name}:{b.World}:{b.Chocobo}:{b.Amount}"))).Append('|')
          .Append(string.Join(",", s.Pins.Select(p => $"{p.Name}:{p.Pin}"))).Append('|')
          .Append(s.VenueName).Append('|').Append(s.VenueImageUrl);
        return sb.ToString();
    }

    private static SyncStatePayload ToSyncPayload(Snapshot s) => new()
    {
        Phase = s.Phase, Mode = s.Mode, Round = s.Round, ChocoboCount = s.ChocoboCount, FinishLine = s.FinishLine,
        Odds = s.Odds, PerfectRace = s.PerfectRace, PerfectRaceOdds = s.PerfectRaceOdds, MaxBetPerChocobo = s.MaxBet,
        WinningChocobo = s.WinningChocobo, Positions = s.Positions, ChocoboNames = s.Names,
        Banks = s.Banks, Bets = s.Bets, Pins = s.Pins, HostName = s.HostName,
        VenueName = s.VenueName, VenueImageUrl = s.VenueImageUrl,
        Runners = s.Runners, Pot = s.Pot, EntryFee = s.EntryFee, Boost = s.Boost,
        VenueCutPercent = s.VenueCutPercent, Winner = s.Winner,
        PrizeType = s.PrizeType, PrizeLabel = s.PrizeLabel,
    };

    private static CallCountsPayload ToCallCounts(Snapshot s) => new()
    {
        Phase = s.Phase, Positions = s.Positions, WinningChocobo = s.WinningChocobo,
    };

    private void SetConnected(bool connected)
    {
        Connected = connected;
        SetStatus(connected ? "Live" : "Reconnecting...", false);
    }

    private Task RunOnFramework(Action action) => _framework.RunOnFrameworkThread(action);

    private static string LocalHostName() => Plugin.ObjectTable.LocalPlayer?.Name.TextValue ?? string.Empty;

    private static (string ContentId, string Name, string World) LocalIdentity()
    {
        var lp = Plugin.ObjectTable.LocalPlayer;
        var name = lp?.Name.TextValue ?? string.Empty;
        var world = lp?.HomeWorld.Value.Name.ToString() ?? string.Empty;
        var contentId = Interop.LocalCharacter.ContentId().ToString(CultureInfo.InvariantCulture);
        return (contentId, name, world);
    }

    public void Dispose()
    {
        _disposed = true;
        _framework.Update -= OnUpdate;
        _client.Dispose();
    }

    private sealed class Snapshot
    {
        public string Mode = "classic";
        public string Phase = "Idle";
        public int Round;
        public int ChocoboCount;
        public int FinishLine;
        public int WinningChocobo;
        public double Odds;
        public double PerfectRaceOdds;
        public bool PerfectRace;
        public long MaxBet;
        public List<int> Positions = new();
        public List<string> Names = new();
        public List<BankDto> Banks = new();
        public List<BetDto> Bets = new();
        public List<PinDto> Pins = new();
        public string HostName = string.Empty;
        public string VenueName = string.Empty;
        public string VenueImageUrl = string.Empty;
        public List<RunnerDto> Runners = new();
        public long Pot;
        public long EntryFee;
        public long Boost;
        public double VenueCutPercent;
        public int Winner;
        public string PrizeType = "pot";
        public string PrizeLabel = string.Empty;
    }
}
