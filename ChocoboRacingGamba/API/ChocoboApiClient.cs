using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ChocoboRacing.Models;
using Dalamud.Plugin.Services;

/// <summary>
/// HTTP client for ChocoboRacingAPI.
/// </summary>
namespace ChocoboRacing.API;

public sealed class ChocoboApiClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly HttpClient _http;
    private readonly Func<string> _baseUrl;
    private readonly Func<string> _hostKey;
    private readonly IPluginLog _log;
    private readonly CancellationTokenSource _cts = new();
    private long _lastRequestTick;

    public ChocoboApiClient(Func<string> baseUrlProvider, Func<string> hostKeyProvider, IPluginLog log)
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        _baseUrl = baseUrlProvider;
        _hostKey = hostKeyProvider;
        _log = log;
    }

    public Task<(CreateSessionResponse? Data, int Status)> CreateSessionAsync(CreateSessionRequest req) =>
        SendForResultStatusAsync<CreateSessionResponse>(HttpMethod.Post, "session", req);

    public Task<(bool Ok, int Status)> SyncStateAsync(string sessionId, SyncStatePayload payload) =>
        SendAsync(HttpMethod.Post, $"session/{sessionId}/sync_state", payload);

    public Task<(bool Ok, int Status)> PushCallCountsAsync(string sessionId, CallCountsPayload payload) =>
        SendAsync(HttpMethod.Post, $"session/{sessionId}/call_counts", payload);

    public Task<(WebBetsResponse? Data, int Status)> GetWebBetsAsync(string sessionId) =>
        SendForResultStatusAsync<WebBetsResponse>(HttpMethod.Get, $"session/{sessionId}/web_bets", null);

    public Task<(bool Ok, int Status)> AckWebBetsAsync(string sessionId, WebBetAckRequest req) =>
        SendAsync(HttpMethod.Post, $"session/{sessionId}/web_bets/ack", req);

    public Task<(MasterStateResponse? Data, int Status)> GetMasterStateAsync(string sessionId) =>
        SendForResultStatusAsync<MasterStateResponse>(HttpMethod.Get, $"session/{sessionId}/master_state", null);

    public Task<(bool Ok, int Status)> EndSessionAsync(string sessionId) =>
        SendAsync(HttpMethod.Post, $"session/{sessionId}/end", null);

    public async Task<RollResponse?> RollRaffleAsync(string sessionId)
    {
        var (data, _) = await SendForResultStatusAsync<RollResponse>(HttpMethod.Post, $"session/{sessionId}/roll", null).ConfigureAwait(false);
        return data;
    }

    private void LogRequest(HttpMethod method, string path, string outcome, long startTick)
    {
        var now = Environment.TickCount64;
        var sinceLast = _lastRequestTick == 0 ? 0 : now - _lastRequestTick;
        _lastRequestTick = now;
    }

    private static async Task<string> ReadBodySnippetAsync(HttpResponseMessage resp)
    {
        try
        {
            var text = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            return text.Length > 300 ? text[..300] + "..." : text;
        }
        catch { return string.Empty; }
    }

    private static string EnsureTrailingSlash(string url) => url.EndsWith('/') ? url : url + "/";

    private HttpRequestMessage BuildRequest(HttpMethod method, string path, object? body)
    {
        var uri = new Uri(EnsureTrailingSlash(_baseUrl()) + path);
        var req = new HttpRequestMessage(method, uri);
        req.Headers.Add("X-Host-Key", _hostKey() ?? string.Empty);
        if (body != null)
            req.Content = JsonContent.Create(body, body.GetType(), options: JsonOptions);
        return req;
    }

    private async Task<(bool Ok, int Status)> SendAsync(HttpMethod method, string path, object? body)
    {
        var start = Environment.TickCount64;
        try
        {
            using var req = BuildRequest(method, path, body);
            using var resp = await _http.SendAsync(req, _cts.Token).ConfigureAwait(false);
            LogRequest(method, path, ((int)resp.StatusCode).ToString(), start);
            if (!resp.IsSuccessStatusCode)
            {
                var detail = await ReadBodySnippetAsync(resp).ConfigureAwait(false);
                _log.Warning($"Chocobo API {method} {path} -> {(int)resp.StatusCode}{(string.IsNullOrEmpty(detail) ? string.Empty : $": {detail}")}");
            }
            return (resp.IsSuccessStatusCode, (int)resp.StatusCode);
        }
        catch (OperationCanceledException) { LogRequest(method, path, "cancelled", start); _log.Warning($"Chocobo API {method} {path} timed out or was cancelled."); return (false, 0); }
        catch (Exception ex) { LogRequest(method, path, "failed", start); _log.Warning($"Chocobo API {method} {path} failed: {ex.Message}"); return (false, 0); }
    }

    private async Task<(T? Data, int Status)> SendForResultStatusAsync<T>(HttpMethod method, string path, object? body) where T : class
    {
        var start = Environment.TickCount64;
        try
        {
            using var req = BuildRequest(method, path, body);
            using var resp = await _http.SendAsync(req, _cts.Token).ConfigureAwait(false);
            LogRequest(method, path, ((int)resp.StatusCode).ToString(), start);
            if (!resp.IsSuccessStatusCode)
            {
                var detail = await ReadBodySnippetAsync(resp).ConfigureAwait(false);
                _log.Warning($"Chocobo API {method} {path} -> {(int)resp.StatusCode}{(string.IsNullOrEmpty(detail) ? string.Empty : $": {detail}")}");
                return (null, (int)resp.StatusCode);
            }
            var data = await resp.Content.ReadFromJsonAsync<T>(JsonOptions, _cts.Token).ConfigureAwait(false);
            return (data, (int)resp.StatusCode);
        }
        catch (OperationCanceledException) { LogRequest(method, path, "cancelled", start); _log.Warning($"Chocobo API {method} {path} timed out or was cancelled."); return (null, 0); }
        catch (Exception ex) { LogRequest(method, path, "failed", start); _log.Warning($"Chocobo API {method} {path} failed: {ex.Message}"); return (null, 0); }
    }

    public void Dispose()
    {
        try { _cts.Cancel(); } catch { }
        _cts.Dispose();
        _http.Dispose();
    }
}
