using System;
using System.Threading;
using System.Threading.Tasks;
using BepInEx.Logging;
using PlaylistVoting.Commands.Local;
using PlaylistVoting.Commands.Remote;
using PlaylistVoting.Core.Config;
using PlaylistVoting.Core.Models;
using PlaylistVoting.Core.State;
using PlaylistVoting.Infrastructure.Api;
using PlaylistVoting.Infrastructure.UI;
using PlaylistVoting.Infrastructure.Zeepkist;
using Steamworks;
using UnityEngine;
using ZeepkistClient;
using ZeepSDK.Chat;
using ZeepSDK.ChatCommands;

namespace PlaylistVoting.Core.Controllers;

/// <summary>
///     Central controller that owns configuration, state, and API client.
/// </summary>
public class VotingController : MonoBehaviour
{
    // ── Constants ─────────────────────────────────────────────────────────────
    private const int VoteReminderThresholdSeconds = 30;

    private readonly object _stateLock = new object();

    // ── Private fields ───────────────────────────────────────────────────────
    private IVotingState _currentState;
    private bool _hasRemindedToVote;

    private bool _isConnectingWebSocket;
    private CancellationTokenSource _stateCts;
    private ZeepkistLobbyStateListener _zeepkistLobbyStateListener;

    // ── Public state ─────────────────────────────────────────────────────────
    public string CurrentLevelName { get; set; } = string.Empty;
    public string CurrentLevelAuthor { get; set; } = string.Empty;
    public string CurrentLevelUid { get; set; } = string.Empty;
    public ulong CurrentLevelWorkshopID { get; set; }

    // ── Dependencies ─────────────────────────────────────────────────────────
    public VotingApiClient ApiClient { get; private set; }
    public VotingWebSocketClient WebSocketClient { get; private set; }
    public static VotingController Instance { get; private set; }
    public ManualLogSource Logger { get; private set; }
    public ZeepkistLobbyState CurrentZeepkistLobbyState => _zeepkistLobbyStateListener?.CurrentState ?? ZeepkistLobbyState.NotInALobby;

    public static string WinEmote => VotingConfig.Instance.WinEmote;
    public static string TieEmote => VotingConfig.Instance.TieEmote;
    public static string LoseEmote => VotingConfig.Instance.LoseEmote;

    public string ServermessageTitle { get; set; } = "Playlist Voting";

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        _currentState?.OnUpdate();
    }

    private void OnDestroy()
    {
        _zeepkistLobbyStateListener?.Dispose();
        _currentState?.OnExitAsync();
        WebSocketClient?.Dispose();
    }

    public void Initialize(ManualLogSource logger)
    {
        Logger = logger;
        RegisterChatCommands();

        _zeepkistLobbyStateListener = new ZeepkistLobbyStateListener();
        _zeepkistLobbyStateListener.StateChanged += OnZeepkistLobbyStateChanged;
        ApiClient = new VotingApiClient();

        // Start in NotReady state by default
        _currentState = new NotReadyState(this);
        _ = _currentState.OnEnterAsync(CancellationToken.None);
    }

    public async Task LoginAsync(CancellationToken ct = default)
    {
        try
        {
            Logger.LogInfo("Waiting for Steamworks to initialize...");
            int retryCount = 0;
            while (!SteamClient.IsValid && retryCount < 30)
            {
                await Task.Delay(1000, ct);
                retryCount++;
            }

            if (!SteamClient.IsValid)
            {
                Logger.LogError("Steamworks failed to initialize in time. Login aborted.");
                return;
            }

            Logger.LogInfo("Steamworks initialized. Fetching auth ticket...");

            AuthTicket ticket = SteamUser.GetAuthSessionTicket(default);
            if (ticket != null && ticket.Data != null)
            {
                string ticketHex = BitConverter.ToString(ticket.Data).Replace("-", "").ToLower();
                bool success = await ApiClient.LoginWithSteamTicketAsync(ticketHex, ct);
                if (success)
                {
                    Logger.LogInfo("Successfully logged in with Steam ticket.");
                    _ = InitializeWebSocketAsync(ct);
                }
                else
                {
                    Logger.LogWarning("Failed to login with Steam ticket. API might be unreachable or token invalid.");
                }
            }
            else
            {
                Logger.LogWarning("Failed to get Steam auth ticket. Ticket or Data is null.");
            }
        }
        catch (OperationCanceledException)
        {
            Logger.LogInfo("LoginAsync cancelled.");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error during Steam login: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private async Task InitializeWebSocketAsync(CancellationToken ct)
    {
        if (_isConnectingWebSocket)
        {
            return;
        }

        if (_currentState != null && !_currentState.RequiresWebSocket)
        {
            Logger.LogDebug("Skipping WebSocket initialization because current state does not require it.");
            return;
        }

        _isConnectingWebSocket = true;

        try
        {
            Logger.LogInfo($"Initializing WebSocket for host: {ApiClient.GetUserId() ?? "unknown"}");
            if (WebSocketClient != null)
            {
                WebSocketClient.OnResultReceived -= UpdateFromVotingResult;
                WebSocketClient.OnDisconnected -= OnWebSocketDisconnected;
                WebSocketClient.Dispose();
            }

            string token = ApiClient.GetSessionToken();
            if (string.IsNullOrEmpty(token))
            {
                Logger.LogWarning("No session token available for WebSocket. Attempting login...");
                await LoginAsync(ct);
                token = ApiClient.GetSessionToken();
                if (string.IsNullOrEmpty(token))
                {
                    Logger.LogError("Failed to get token for WebSocket even after login attempt.");
                    return;
                }
            }

            string userId = ApiClient.GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                userId = SteamClient.SteamId.ToString();
                Logger.LogWarning($"User ID not found in login response. Falling back to SteamID: {userId}. Updates might not work if the server expects the internal ID.");
            }

            WebSocketClient = new VotingWebSocketClient(VotingConfig.Instance.WebApiUrl, userId, token, Logger);
            WebSocketClient.OnResultReceived += UpdateFromVotingResult;
            WebSocketClient.OnDisconnected += OnWebSocketDisconnected;
            await WebSocketClient.ConnectAsync(ct);
            Logger.LogInfo("WebSocket connected and subscribed.");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to initialize WebSocket: {ex.Message}");
            _ = ReconnectWebSocketAsync();
        }
        finally
        {
            _isConnectingWebSocket = false;
        }
    }

    private void OnWebSocketDisconnected()
    {
        Logger.LogWarning("WebSocket disconnected.");

        if (_currentState != null && _currentState.RequiresWebSocket)
        {
            _ = ReconnectWebSocketAsync();
        }
        else
        {
            Logger.LogInfo("WebSocket reconnection skipped: Current state does not require it.");
        }
    }

    public void DisconnectWebSocket()
    {
        if (WebSocketClient != null)
        {
            Logger.LogInfo("Disconnecting and disposing WebSocket client.");
            WebSocketClient.OnResultReceived -= UpdateFromVotingResult;
            WebSocketClient.OnDisconnected -= OnWebSocketDisconnected;
            WebSocketClient.Dispose();
            WebSocketClient = null;
        }
    }

    private async Task ReconnectWebSocketAsync()
    {
        if (_isConnectingWebSocket || this == null)
        {
            return;
        }

        Logger.LogInfo("Scheduling WebSocket reconnection in 5 seconds...");
        await Task.Delay(5000);

        if (this == null)
        {
            return;
        }

        await InitializeWebSocketAsync(CancellationToken.None);
    }

    // ── State Transitions ────────────────────────────────────────────────────

    public async Task TransitionToStateAsync(IVotingState newState)
    {
        CancellationToken ct;

        lock (_stateLock)
        {
            Logger.LogInfo($"Transitioning state: {_currentState?.GetType().Name} -> {newState.GetType().Name}");

            // Cancel previous state tasks
            _stateCts?.Cancel();
            _stateCts?.Dispose();
            _stateCts = new CancellationTokenSource();
            ct = _stateCts.Token;

            // Exit previous state
            _currentState?.OnExitAsync();

            _currentState = newState;
        }

        if (newState.RequiresWebSocket && WebSocketClient == null)
        {
            _ = InitializeWebSocketAsync(CancellationToken.None);
        }
        else if (!newState.RequiresWebSocket && WebSocketClient != null)
        {
            DisconnectWebSocket();
        }

        // Enter new state (Async part)
        try
        {
            await _currentState.OnEnterAsync(ct);
        }
        catch (OperationCanceledException)
        {
            Logger.LogInfo($"State {newState.GetType().Name} task was cancelled.");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error entering state {newState.GetType().Name}: {ex.Message}");
        }
    }

    public async Task EnsureLoggedInAsync(CancellationToken ct)
    {
        if (ApiClient != null && string.IsNullOrEmpty(ApiClient.GetSessionToken()))
        {
            await LoginAsync(ct);
        }
    }

    public void StartVotingCycle()
    {
        _hasRemindedToVote = false;
    }

    // ── Actions (Called by States or Commands) ───────────────────────────────

    public void OnVoteStartRequested()
    {
        if (!ZeepkistNetwork.LocalPlayer.isHost)
        {
            ToastNotification.Warning("Only the host can control the voting!");
            return;
        }

        _currentState?.OnVoteStartRequested();
    }

    public void OnVoteStopRequested()
    {
        if (!ZeepkistNetwork.LocalPlayer.isHost)
        {
            ToastNotification.Warning("Only the host can control the voting!");
            return;
        }

        _currentState?.OnVoteStopRequested();
    }

    public void OnVoteRestartRequested()
    {
        if (!ZeepkistNetwork.LocalPlayer.isHost)
        {
            ToastNotification.Warning("Only the host can control the voting!");
            return;
        }

        _currentState?.OnVoteRestartRequested();
    }

    public void OnPlayerVoted(ulong steamId, VotingType votingType) => _currentState?.OnPlayerVoted(steamId, votingType);

    private void OnZeepkistLobbyStateChanged(ZeepkistLobbyState zeepkistLobbyState)
    {
        Logger.LogInfo($"Lobby state changed to: {zeepkistLobbyState}");

        if (zeepkistLobbyState == ZeepkistLobbyState.NotInALobby && _currentState is not NotReadyState)
        {
            _ = TransitionToStateAsync(new NotReadyState(this));
        }
        else
        {
            _currentState?.OnLobbyStateChanged(zeepkistLobbyState);
        }
    }

    public async Task TransitionToStateWithResultsAsync()
    {
        try
        {
            await EndVotingAndShowResultsAsync(_stateCts?.Token ?? CancellationToken.None);
        }
        finally
        {
            await TransitionToStateAsync(new IdleState(this));
        }
    }

    public async Task HandleVoteAsync(ulong steamId, VotingType votingType, CancellationToken ct = default)
    {
        VotingResultResponse result = await ApiClient.SubmitVoteAsync(steamId, votingType, ct);

        if (result != null)
        {
            string message = votingType switch
            {
                VotingType.Yes => "You voted 'yes'.",
                VotingType.No => "You voted 'no'.",
                VotingType.Remove => "Your vote was removed.",
                _ => "Vote submitted."
            };

            ZeepkistNetwork.SendCustomChatMessage(false, steamId, message, ServermessageTitle);
            UpdateFromVotingResult(result);
        }
    }

    public async Task ResetAndStartNewVotingAsync(CancellationToken ct = default)
    {
        try
        {
            if (PlayerManager.Instance?.currentMaster?.GlobalLevel != null)
            {
                CurrentLevelName = PlayerManager.Instance.currentMaster.GlobalLevel.Name;
                CurrentLevelAuthor = PlayerManager.Instance.currentMaster.GlobalLevel.Author;
            }

            if (ZeepkistNetwork.CurrentLobby != null)
            {
                CurrentLevelUid = ZeepkistNetwork.CurrentLobby.LevelUID;
                CurrentLevelWorkshopID = ZeepkistNetwork.CurrentLobby.WorkshopID;
            }

            bool success = await ApiClient.SetCurrentLevelAsync(CurrentLevelUid, CurrentLevelName, CurrentLevelAuthor,
                CurrentLevelWorkshopID.ToString(), ct);

            if (!success)
            {
                Logger.LogWarning("Failed to set current level on server.");
            }
        }
        catch (OperationCanceledException)
        {
            Logger.LogInfo("ResetAndStartNewVotingAsync cancelled.");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error in ResetAndStartNewVotingAsync: {ex.Message}");
        }
    }

    public async Task EndVotingAndShowResultsAsync(CancellationToken ct = default)
    {
        try
        {
            string resetResult = await ApiClient.ResetVotesAsync(ct);

            ZeepkistNetwork.SendCustomChatMessage(
                true, 0,
                $"<#f0f0f0>{resetResult}<br>----------------</color>",
                ServermessageTitle);
        }
        catch (OperationCanceledException)
        {
            Logger.LogInfo("EndVotingAndShowResultsAsync cancelled.");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error in EndVotingAndShowResultsAsync: {ex.Message}");
        }
    }

    public async Task FetchAndDisplayVotesAsync(CancellationToken ct = default)
    {
        try
        {
            VotingResultResponse result = await ApiClient.FetchVoteTotalsAsync(ct);
            if (result == null)
            {
                return;
            }

            UpdateFromVotingResult(result);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Logger?.LogError($"Error fetching votes: {ex.Message}");
        }
    }

    public void UpdateFromVotingResult(VotingResultResponse result)
    {
        if (result == null)
        {
            return;
        }

        if (result.Level != null)
        {
            CurrentLevelName = result.Level.LevelName;
            CurrentLevelAuthor = result.Level.LevelAuthor;
            CurrentLevelUid = result.Level.LevelUid;
            CurrentLevelWorkshopID = result.Level.WorkshopId ?? 0;
        }

        if (result.Votes != null)
        {
            string message = BuildVoteDisplayMessage(result.Votes);
            ChatApi.SendMessage(
                $"/servermessage white 0 <align=\"left\"><size=\"30%\">{message}<br><#ffffff></size>" +
                $"<size=\"20%\"><voffset=-0.5em>Type !y if you like the current level</voffset><br>Type !n if you don't");
        }
    }

    public void SendVoteReminderIfNeeded()
    {
        if (_hasRemindedToVote || ZeepkistNetwork.CurrentLobby == null)
        {
            return;
        }

        string[] timeParts = ZeepkistNetwork.CurrentLobby.timeLeftString.Split(':');
        if (timeParts.Length < 2)
        {
            return;
        }

        bool isLastMinute = timeParts[0] == "00";
        if (int.TryParse(timeParts[1], out int seconds) && isLastMinute && seconds <= VoteReminderThresholdSeconds)
        {
            ZeepkistNetwork.SendCustomChatMessage(
                true, 0,
                "<br><#f0f0f0><u>LAST CHANCE TO <b>VOTE</b>!</u><br>" +
                "Type <#00FF00><b>!y</b></color> to <b>keep</b> this level in the playlist<br>" +
                "Type <#FF0000><b>!n</b></color> to remove it<br></color>",
                ServermessageTitle);

            _hasRemindedToVote = true;
        }
    }

    private string BuildVoteDisplayMessage(VoteResult result)
    {
        string emote = result.IsYesWinning
            ? WinEmote
            : result.IsNoWinning
                ? LoseEmote
                : TieEmote;

        return $"<b><u>{ServermessageTitle}</u></b><br>" +
               $"<#ff9900>{CurrentLevelName}</color> <#ffffff>by</color> <#ff9900>{CurrentLevelAuthor}</color><br>" +
               $"Votes: <#00aa00>{result.YesVotes}</color><#ffffff>/<#aa0000>{result.NoVotes}</color> (yes/no) (!y/!n) {emote}";
    }

    private void RegisterChatCommands()
    {
        ChatCommandApi.RegisterMixedChatCommand<VoteYes>();
        ChatCommandApi.RegisterMixedChatCommand<VoteNo>();
        ChatCommandApi.RegisterMixedChatCommand<VoteRemove>();
        ChatCommandApi.RegisterLocalChatCommand<VoteStart>();
        ChatCommandApi.RegisterLocalChatCommand<VoteStop>();
        ChatCommandApi.RegisterLocalChatCommand<VoteRestart>();
    }
}