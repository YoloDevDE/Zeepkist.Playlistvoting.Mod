using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PlaylistVoting.Data.DTOs;
using PlaylistVoting.Data.Enums;
using PlaylistVoting.Data.Models;
using PlaylistVoting.Data.Requests;
using PlaylistVoting.Management;
using Steamworks;

namespace PlaylistVoting.Networking.Backend;

public class VotingBackendService : IDisposable
{
    private readonly VotingApiClient _api = new VotingApiClient();
    private readonly CancellationTokenSource _queueCts = new CancellationTokenSource();
    private readonly ConcurrentQueue<PendingRequest> _requestQueue = new ConcurrentQueue<PendingRequest>();
    private bool _allowWebsocket;
    private string _hostId;
    private bool _isConnecting;
    private bool _isDisposed;
    private VotingWebSocketClient _wsClient;

    public VotingBackendService()
    {
        VotingConfig.Instance.OnConfigChanged += HandleConfigChanged;
        _ = ProcessQueueLoopAsync(_queueCts.Token);
    }

    public bool IsConnected => _wsClient != null;

    public void Dispose()
    {
        if (VotingConfig.Instance != null)
        {
            VotingConfig.Instance.OnConfigChanged -= HandleConfigChanged;
        }

        _isDisposed = true;
        _queueCts.Cancel();
        _queueCts.Dispose();
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

        _allowWebsocket = true;
        _hostId = hostId;

        return IsConnected ? Task.CompletedTask : ConnectInternalAsync();
    }

    /// <summary>Cancels the reconnect loop and disposes the WebSocket.</summary>
    public void Disconnect()
    {
        _allowWebsocket = false;
        DisposeWebSocket();
        OnDisconnected?.Invoke();
    }

    // ── API wrappers ──────────────────────────────────────────────────────────

    public async Task<VotingResultResponse> SubmitVoteAsync(ulong steamId, VotingType type, string platform = "STEAM")
    {
        try
        {
            return await _api.SubmitVoteAsync(steamId, type, platform);
        }
        catch (Exception ex)
        {
            Logger.Warn($"SubmitVote failed, enqueuing: {ex.Message}");
            _requestQueue.Enqueue(new PendingRequest
            {
                Type = RequestType.SubmitVote, Data = new SubmitVoteRequest { SteamId = steamId, VotingType = type, Platform = platform }
            });
            return null;
        }
    }

    public async Task SetCurrentLevelAsync(LevelMetadata level, bool includeAbstain)
    {
        try
        {
            await _api.SetCurrentLevelAsync(level, includeAbstain);
        }
        catch (Exception ex)
        {
            Logger.Warn($"SetCurrentLevel failed, enqueuing: {ex.Message}");
            _requestQueue.Enqueue(new PendingRequest
            {
                Type = RequestType.SetCurrentLevel, Data = new SetCurrentLevelRequest { Level = level, IncludeAbstain = includeAbstain }
            });
        }
    }

    public Task<VotingResultResponse> FetchVotesAsync() => _api.FetchVoteTotalsAsync();

    public Task<bool> CheckHealthAsync() => _api.GetHealthAsync();

    public async Task<string> ResetVotesAsync()
    {
        try
        {
            return await _api.ResetVotesAsync();
        }
        catch (Exception ex)
        {
            Logger.Warn($"ResetVotes failed, enqueuing: {ex.Message}");
            _requestQueue.Enqueue(new PendingRequest { Type = RequestType.ResetVotes });
            return null;
        }
    }

    public Task<bool> ValidateTokenAsync() => _api.ValidateTokenAsync(GetSessionToken());

    public Task<VotingResultResponse> GetLevelResultAsync(string levelUid) => _api.GetLevelResultAsync(levelUid);

    public async Task<PlaylistSessionInfo> GetActiveSessionAsync()
    {
        try
        {
            return await _api.GetActiveSessionAsync().ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.Error($"VotingBackendService: Error in GetActiveSessionAsync: {ex.Message}");
            return null;
        }
    }

    public async Task<PlaylistSessionInfo> GetLatestActiveSessionAsync()
    {
        try
        {
            return await _api.GetLatestActiveSessionAsync();
        }
        catch (UnauthorizedAccessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.Error($"VotingBackendService: Error in GetLatestActiveSessionAsync: {ex.Message}");
            return null;
        }
    }

    public async Task<PlaylistSessionInfo> CreateSessionAsync(string displayName, List<LevelMetadata> playlist)
    {
        try
        {
            return await _api.CreateSessionAsync(displayName, playlist);
        }
        catch (Exception ex)
        {
            Logger.Error($"CreateSessionAsync failed: {ex.Message}");
            // Session creation is critical, might not want to queue blindly without a result
            throw;
        }
    }

    public async Task<bool> SetPlaylistModeAsync(bool enabled)
    {
        try
        {
            return await _api.SetPlaylistModeAsync(enabled);
        }
        catch (Exception ex)
        {
            Logger.Warn($"SetPlaylistMode failed, enqueuing: {ex.Message}");
            _requestQueue.Enqueue(new PendingRequest { Type = RequestType.SetPlaylistMode, Data = enabled });
            return false;
        }
    }

    public async Task<List<LevelMetadata>> GetToBeVotedPlaylistAsync()
    {
        try
        {
            Logger.Info("VotingBackendService: Requesting to-be-voted playlist from API...");
            List<LevelMetadata> result = await _api.GetToBeVotedPlaylistAsync().ConfigureAwait(false);
            Logger.Info($"VotingBackendService: Received {result?.Count ?? -1} levels from API.");
            return result;
        }
        catch (Exception ex)
        {
            Logger.Error($"VotingBackendService: Error in GetToBeVotedPlaylistAsync: {ex.Message}");
            return null;
        }
    }

    public Task<List<LevelMetadata>> GetFinalPlaylistAsync() => _api.GetFinalPlaylistAsync();

    public async Task<bool> UpdatePlaylistAsync(List<LevelMetadata> levels)
    {
        try
        {
            return await _api.UpdatePlaylistAsync(levels);
        }
        catch (Exception ex)
        {
            Logger.Warn($"UpdatePlaylist failed, enqueuing: {ex.Message}");
            _requestQueue.Enqueue(new PendingRequest { Type = RequestType.UpdatePlaylist, Data = levels });
            return false;
        }
    }

    public async Task<bool> FinalizeLevelAsync(string levelUid)
    {
        try
        {
            return await _api.FinalizeLevelAsync(levelUid);
        }
        catch (Exception ex)
        {
            Logger.Warn($"FinalizeLevel failed, enqueuing: {ex.Message}");
            _requestQueue.Enqueue(new PendingRequest { Type = RequestType.FinalizeLevel, Data = levelUid });
            return false;
        }
    }

    public async Task<bool> ResetVotesForLevelAsync(string levelUid)
    {
        try
        {
            return await _api.ResetVotesForLevelAsync(levelUid);
        }
        catch (Exception ex)
        {
            Logger.Warn($"ResetVotesForLevel failed, enqueuing: {ex.Message}");
            _requestQueue.Enqueue(new PendingRequest { Type = RequestType.ResetVotesForLevel, Data = levelUid });
            return false;
        }
    }

    public async Task<bool> UpdateTimerAsync(TimerUpdateDto dto)
    {
        try
        {
            return await _api.UpdateTimerAsync(dto);
        }
        catch (Exception ex)
        {
            Logger.Warn($"UpdateTimer failed, enqueuing: {ex.Message}");
            _requestQueue.Enqueue(new PendingRequest { Type = RequestType.UpdateTimer, Data = dto });
            return false;
        }
    }

    public void SendTimer(string time)
    {
        _wsClient?.SendTimer(time);
    }

    private async Task ProcessQueueLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (_requestQueue.TryPeek(out PendingRequest request))
                {
                    bool success = await TryProcessRequest(request);

                    if (success)
                    {
                        _requestQueue.TryDequeue(out _);
                        Logger.Info($"Queue: Successfully processed {request.Type} request.");
                    }
                    else
                    {
                        request.RetryCount++;

                        if (request.RetryCount > 10)
                        {
                            _requestQueue.TryDequeue(out _);
                            Logger.Error($"Queue: Dropping {request.Type} request after 10 failed attempts.");
                        }
                        else
                        {
                            // Wait a bit before retrying the same request
                            await Task.Delay(5000 * request.RetryCount, ct);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Logger.Error($"Queue: Error in process loop: {ex.Message}");
            }

            await Task.Delay(2000, ct);
        }
    }

    private async Task<bool> TryProcessRequest(PendingRequest request)
    {
        try
        {
            switch (request.Type)
            {
                case RequestType.SubmitVote:
                    SubmitVoteRequest vote = (SubmitVoteRequest)request.Data;
                    await _api.SubmitVoteAsync(vote.SteamId, vote.VotingType, vote.Platform);
                    return true;
                case RequestType.SetCurrentLevel:
                    SetCurrentLevelRequest levelReq = (SetCurrentLevelRequest)request.Data;
                    return await _api.SetCurrentLevelAsync(levelReq.Level, levelReq.IncludeAbstain);
                case RequestType.UpdateTimer:
                    return await _api.UpdateTimerAsync((TimerUpdateDto)request.Data);
                case RequestType.ResetVotesForLevel:
                    return await _api.ResetVotesForLevelAsync((string)request.Data);
                case RequestType.SetPlaylistMode:
                    return await _api.SetPlaylistModeAsync((bool)request.Data);
                case RequestType.UpdatePlaylist:
                    await _api.UpdatePlaylistAsync((List<LevelMetadata>)request.Data);
                    return true;
                case RequestType.ResetVotes:
                    await _api.ResetVotesAsync();
                    return true;
                case RequestType.FinalizeLevel:
                    return await _api.FinalizeLevelAsync((string)request.Data);
                default:
                    return true;
            }
        }
        catch
        {
            return false;
        }
    }

    public string GetSessionToken() => _api.GetSessionToken();

    public async Task<bool> ManualLoginWithSteamAsync()
    {
        try
        {
            if (await LoginAsync())
            {
                Logger.Info("Manual Steam login successful.");

                // Reconnect if we should be connected
                if (_allowWebsocket)
                {
                    _ = ReconnectAsync();
                }

                return true;
            }

            Logger.Warn("Manual Steam login failed.");
            return false;
        }
        catch (Exception ex)
        {
            Logger.Error($"Manual Steam login error: {ex.Message}");
            return false;
        }
    }


    // ── Internal connection logic ─────────────────────────────────────────────

    private async Task ConnectInternalAsync()
    {
        if (!_allowWebsocket || _isDisposed)
        {
            return;
        }

        if (_isConnecting)
        {
            while (_isConnecting)
                await Task.Delay(100);

            return;
        }

        _isConnecting = true;

        try
        {
            await LoginAsync();


            if (_isDisposed)
            {
                return;
            }

            string token = _api.GetSessionToken();

            if (string.IsNullOrEmpty(token))
            {
                Logger.Warn("No session token after login — backend will be unavailable.");
                return;
            }

            string userId = _api.GetUserId();

            if (string.IsNullOrEmpty(userId))
            {
                userId = SteamClient.SteamId.ToString();
                Logger.Warn($"No user ID from server, falling back to SteamID: {userId}");
            }

            // 2. Connect WebSocket
            _wsClient = new VotingWebSocketClient(VotingConfig.Instance.BaseUrl, userId, token);
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
            Logger.Info("Backend connected.");
        }
        catch (Exception ex)
        {
            Logger.Error($"Backend connection failed: {ex.Message}");
            _ = ReconnectAsync();
        }
        finally
        {
            _isConnecting = false;
        }
    }

    private async Task<bool> LoginAsync()
    {
        try
        {
            Logger.Info("Waiting for Steamworks to initialize...");
            int retries = 0;

            while (!SteamClient.IsValid && retries < 30)
            {
                await Task.Delay(1000);
                retries++;
            }

            if (!SteamClient.IsValid)
            {
                Logger.Error("Steamworks not initialized. Login aborted.");
                return false;
            }

            AuthTicket ticket = SteamUser.GetAuthSessionTicket(default);

            if (ticket?.Data == null)
            {
                Logger.Warn("Steam auth ticket is null.");
                return false;
            }

            string ticketHex = BitConverter.ToString(ticket.Data).Replace("-", "").ToLower();
            bool success = await _api.LoginWithSteamTicketAsync(ticketHex);

            if (success)
            {
                Logger.Info("Steam login successful.");
                return true;
            }

            Logger.Warn("Steam login failed — API may be unreachable.");
            return false;
        }
        catch (Exception ex)
        {
            Logger.Error($"Login error: {ex.Message}");
            return false;
        }
    }

    private async Task ReconnectAsync()
    {
        if (_isDisposed || !_allowWebsocket)
        {
            return;
        }

        Logger.Info("Scheduling reconnect in 5 seconds...");
        await Task.Delay(5000);

        if (_isDisposed || !_allowWebsocket)
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
        Logger.Warn("WebSocket disconnected.");
        DisposeWebSocket();
        OnDisconnected?.Invoke();

        if (!_isDisposed && _allowWebsocket)
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

    private void HandleConfigChanged()
    {
        if (_isDisposed || !_allowWebsocket)
        {
            return;
        }

        Logger.Info("Config changed, reconnecting backend to apply new settings...");
        DisposeWebSocket();
        _ = ConnectInternalAsync();
    }
}