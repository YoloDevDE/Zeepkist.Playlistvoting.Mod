using System;
using System.Threading.Tasks;
using BepInEx.Logging;
using PlaylistVoting.Core.Config;
using PlaylistVoting.Core.Models;
using Steamworks;

namespace PlaylistVoting.Infrastructure.Api;

public class VotingBackendService : IDisposable
{
    private readonly VotingApiClient _api = new VotingApiClient();
    private readonly ManualLogSource _logger;
    private string _hostId;
    private bool _isConnecting;
    private bool _isDisposed;
    private VotingWebSocketClient _wsClient;

    public VotingBackendService(ManualLogSource logger)
    {
        _logger = logger;
    }

    public bool IsConnected => _wsClient != null;

    public void Dispose()
    {
        _isDisposed = true;
        DisposeWebSocket();
    }

    public event Action<VotingResultResponse> OnResultReceived;
    public event Action OnConnected;
    public event Action OnDisconnected;

    /// <summary>Idempotent — no-op if already connected or connecting.</summary>
    public Task ConnectAsync(string hostId)
    {
        if (_isDisposed)
        {
            return Task.CompletedTask;
        }

        if (IsConnected)
        {
            return Task.CompletedTask;
        }

        _hostId = hostId;
        return ConnectInternalAsync();
    }

    /// <summary>Cancels the reconnect loop and disposes the WebSocket.</summary>
    public void Disconnect()
    {
        DisposeWebSocket();
        OnDisconnected?.Invoke();
    }

    // ── API wrappers ──────────────────────────────────────────────────────────

    public Task<VotingResultResponse> SubmitVoteAsync(ulong steamId, VotingType type, string platform = "STEAM") => _api.SubmitVoteAsync(steamId, type, platform);

    public Task SetCurrentLevelAsync(LevelMetadata level, bool includeAbstain) => _api.SetCurrentLevelAsync(level, includeAbstain);

    public Task<VotingResultResponse> FetchVotesAsync() => _api.FetchVoteTotalsAsync();

    public Task<string> ResetVotesAsync() => _api.ResetVotesAsync();

    public void SendTimer(string time)
    {
        _wsClient?.SendTimer(time);
    }

    public string GetSessionToken() => _api.GetSessionToken();

    // ── Internal connection logic ─────────────────────────────────────────────

    private async Task ConnectInternalAsync()
    {
        if (_isConnecting)
        {
            while (_isConnecting) await Task.Delay(100);
            return;
        }

        _isConnecting = true;
        try
        {
            // 1. Login if we don't have a session token yet
            if (string.IsNullOrEmpty(_api.GetSessionToken()))
            {
                await LoginAsync();
            }

            if (_isDisposed)
            {
                return;
            }

            string token = _api.GetSessionToken();
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("No session token after login — backend will be unavailable.");
                return;
            }

            string userId = _api.GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                userId = SteamClient.SteamId.ToString();
                _logger.LogWarning($"No user ID from server, falling back to SteamID: {userId}");
            }

            // 2. Connect WebSocket
            _wsClient = new VotingWebSocketClient(VotingConfig.Instance.WebApiUrl, userId, token, _logger);
            _wsClient.OnResultReceived += HandleResult;
            _wsClient.OnDisconnected += HandleDisconnected;
            await _wsClient.ConnectAsync();

            // 3. Sync current votes after reconnect
            VotingResultResponse current = await _api.FetchVoteTotalsAsync();
            if (current != null)
            {
                HandleResult(current);
            }

            OnConnected?.Invoke();
            _logger.LogInfo("Backend connected.");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Backend connection failed: {ex.Message}");
            _ = ReconnectAsync();
        }
        finally
        {
            _isConnecting = false;
        }
    }

    private async Task LoginAsync()
    {
        try
        {
            _logger.LogInfo("Waiting for Steamworks to initialize...");
            int retries = 0;
            while (!SteamClient.IsValid && retries < 30)
            {
                await Task.Delay(1000);
                retries++;
            }

            if (!SteamClient.IsValid)
            {
                _logger.LogError("Steamworks not initialized. Login aborted.");
                return;
            }

            AuthTicket ticket = SteamUser.GetAuthSessionTicket(default);
            if (ticket?.Data == null)
            {
                _logger.LogWarning("Steam auth ticket is null.");
                return;
            }

            string ticketHex = BitConverter.ToString(ticket.Data).Replace("-", "").ToLower();
            bool success = await _api.LoginWithSteamTicketAsync(ticketHex);

            if (success)
            {
                _logger.LogInfo("Steam login successful.");
            }
            else
            {
                _logger.LogWarning("Steam login failed — API may be unreachable.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Login error: {ex.Message}");
        }
    }

    private async Task ReconnectAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        _logger.LogInfo("Scheduling reconnect in 5 seconds...");
        await Task.Delay(5000);

        if (_isDisposed)
        {
            return;
        }

        DisposeWebSocket();
        await ConnectInternalAsync();
    }

    private void HandleResult(VotingResultResponse result)
    {
        OnResultReceived?.Invoke(result);
    }

    private void HandleDisconnected()
    {
        _logger.LogWarning("WebSocket disconnected.");
        DisposeWebSocket();
        OnDisconnected?.Invoke();

        if (!_isDisposed)
        {
            _ = ReconnectAsync();
        }
    }

    private void DisposeWebSocket()
    {
        if (_wsClient == null)
        {
            return;
        }

        _wsClient.OnResultReceived -= HandleResult;
        _wsClient.OnDisconnected -= HandleDisconnected;
        _wsClient.Dispose();
        _wsClient = null;
    }
}