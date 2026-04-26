using System;
using System.Threading;
using System.Threading.Tasks;
using BepInEx.Logging;
using PlaylistVoting.api;
using PlaylistVoting.commands.local;
using PlaylistVoting.commands.remote;
using PlaylistVoting.misc;
using Steamworks;
using UnityEngine;
using ZeepkistClient;
using ZeepSDK.Chat;
using ZeepSDK.ChatCommands;

namespace PlaylistVoting.core;

public enum VotingState
{
    Disabled,
    Idle,
    Active
}

/// <summary>
///     Central manager that owns configuration, state, and API client.
/// </summary>
public class VotingManager : MonoBehaviour
{
    // ── Constants ─────────────────────────────────────────────────────────────
    private const float TimerIntervalSeconds = 5f;
    private const int VoteReminderThresholdSeconds = 30;
    private readonly object _stateLock = new object();
    private bool _hasRemindedToVote;

    // ── Private fields ───────────────────────────────────────────────────────
    private ManualLogSource _logger;
    private int _noVotes;
    private float _pollTimer;
    private VotingState _state = VotingState.Disabled;
    private CancellationTokenSource _stateCts;


    private int _yesVotes;
    private ZeepkistLobbyStateListener _zeepkistLobbyStateListener;

    // ── Public state ─────────────────────────────────────────────────────────
    public string CurrentLevelName { get; set; } = string.Empty;
    public string CurrentLevelAuthor { get; set; } = string.Empty;
    public string CurrentLevelUid { get; set; } = string.Empty;
    public ulong CurrentLevelWorkshopID { get; set; }

    // ── Dependencies ─────────────────────────────────────────────────────────
    public RestController RestController { get; private set; }
    public static VotingManager Instance { get; private set; }
    public ZeepkistLobbyState CurrentZeepkistLobbyState => _zeepkistLobbyStateListener?.CurrentState ?? ZeepkistLobbyState.NotInALobby;

    public string WinEmote => VotingConfig.Instance.WinEmote;
    public string TieEmote => VotingConfig.Instance.TieEmote;
    public string LoseEmote => VotingConfig.Instance.LoseEmote;

    public string ServermessageTitle { get; set; } = "Playlist Voting";

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        // Periodic logic that doesn't fit well into async tasks
        if (_state == VotingState.Active)
        {
            UpdateActive();
        }
    }

    private void OnDestroy()
    {
        _zeepkistLobbyStateListener?.Dispose();

        if (VotingEventBus.Hub != null)
        {
            VotingEventBus.Hub.VotingStarted -= OnVoteStartRequested;
            VotingEventBus.Hub.VotingStopped -= OnVoteStopRequested;
            VotingEventBus.Hub.PlayerVoted -= OnPlayerVoted;
            VotingEventBus.Hub.ZeepkistLobbyStateChanged -= OnZeepkistLobbyStateChanged;
        }
    }

    public void Initialize(ManualLogSource logger)
    {
        _logger = logger;
        RegisterChatCommands();

        VotingEventBus.Hub = new VotingEventHub();
        _zeepkistLobbyStateListener = new ZeepkistLobbyStateListener();
        RestController = new RestController();

        // Subscribe to events
        VotingEventBus.Hub.VotingStarted += OnVoteStartRequested;
        VotingEventBus.Hub.VotingStopped += OnVoteStopRequested;
        VotingEventBus.Hub.PlayerVoted += OnPlayerVoted;
        VotingEventBus.Hub.ZeepkistLobbyStateChanged += OnZeepkistLobbyStateChanged;

        // Start in Disabled state by default
        _state = VotingState.Disabled;
    }

    private async Task LoginAsync(CancellationToken ct = default)
    {
        try
        {
            _logger.LogInfo("Waiting for Steamworks to initialize...");
            int retryCount = 0;
            while (!SteamClient.IsValid && retryCount < 30)
            {
                await Task.Delay(1000, ct);
                retryCount++;
            }

            if (!SteamClient.IsValid)
            {
                _logger.LogError("Steamworks failed to initialize in time. Login aborted.");
                return;
            }

            _logger.LogInfo("Steamworks initialized. Fetching auth ticket...");

            // Facepunch.Steamworks v2 verwendet NetIdentity
            AuthTicket ticket = SteamUser.GetAuthSessionTicket(default);
            if (ticket != null && ticket.Data != null)
            {
                string ticketHex = BitConverter.ToString(ticket.Data).Replace("-", "").ToLower();
                bool success = await RestController.LoginWithSteamTicketAsync(ticketHex, ct);
                if (success)
                {
                    _logger.LogInfo("Successfully logged in with Steam ticket.");
                }
                else
                {
                    _logger.LogWarning("Failed to login with Steam ticket. API might be unreachable or token invalid.");
                }
            }
            else
            {
                _logger.LogWarning("Failed to get Steam auth ticket. Ticket or Data is null.");
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInfo("LoginAsync cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error during Steam login: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private void UpdateActive()
    {
        _pollTimer -= Time.deltaTime;
        if (_pollTimer <= 0)
        {
            _pollTimer = TimerIntervalSeconds;
            _ = FetchAndDisplayVotesAsync(_stateCts?.Token ?? default);
        }
    }

    // ── State Transitions ────────────────────────────────────────────────────

    public async Task TransitionToStateAsync(VotingState newState)
    {
        CancellationToken ct;

        lock (_stateLock)
        {
            if (_state == newState && newState != VotingState.Active)
            {
                return;
            }

            _logger.LogInfo($"Transitioning state: {_state} -> {newState}");

            // Cancel previous state tasks
            _stateCts?.Cancel();
            _stateCts?.Dispose();
            _stateCts = new CancellationTokenSource();
            ct = _stateCts.Token;

            // Exit previous state
            OnExitState(_state);

            _state = newState;
        }

        // Enter new state (Async part)
        try
        {
            await OnEnterStateAsync(newState, ct);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInfo($"State {newState} task was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error entering state {newState}: {ex.Message}");
        }
    }

    private void OnExitState(VotingState state)
    {
        switch (state)
        {
            case VotingState.Disabled:
                break;
            case VotingState.Idle:
                break;
            case VotingState.Active:
                ChatApi.SendMessage("/servermessage remove");
                break;
        }
    }

    private async Task OnEnterStateAsync(VotingState state, CancellationToken ct)
    {
        switch (state)
        {
            case VotingState.Disabled:
                _logger.LogInfo("Mod is now Disabled.");
                break;

            case VotingState.Idle:
                _logger.LogInfo("Mod is now Idle. Waiting for race start...");
                // Ensure we are logged in
                await EnsureLoggedInAsync(ct);
                break;

            case VotingState.Active:
                _logger.LogInfo("Mod is now Active. Starting voting cycle...");
                await EnsureLoggedInAsync(ct);
                await ResetAndStartNewVotingAsync(ct);
                break;
        }
    }

    private async Task EnsureLoggedInAsync(CancellationToken ct)
    {
        // If we don't have a token, try to login
        if (RestController != null && string.IsNullOrEmpty(RestController.GetSessionToken()))
        {
            await LoginAsync(ct);
        }
    }

    private void StartVotingCycle()
    {
        _hasRemindedToVote = false;
        _pollTimer = 0; // Trigger immediate poll
    }

    private void StopVotingCycle()
    {
        // Handled by OnExitState and state transition
    }

    // ── Event Handlers ───────────────────────────────────────────────────────

    private void OnVoteStartRequested()
    {
        if (!ZeepkistNetwork.LocalPlayer.isHost)
        {
            ToastNotification.Warning("Only the host can start the vote!");
            return;
        }

        if (_state != VotingState.Disabled)
        {
            ToastNotification.Warning("Mod is already running.");
            return;
        }

        ToastNotification.Custom("Mod started", Color.black, Color.green);

        if (CurrentZeepkistLobbyState == ZeepkistLobbyState.Racing)
        {
            _ = TransitionToStateAsync(VotingState.Active);
        }
        else
        {
            _ = TransitionToStateAsync(VotingState.Idle);
        }
    }

    private void OnVoteStopRequested()
    {
        if (_state != VotingState.Disabled)
        {
            _ = TransitionToStateAsync(VotingState.Disabled);
            ToastNotification.Custom("stopped", Color.black, Color.red);
        }
        else
        {
            ToastNotification.Warning("is not currently running.<br>Type '<#00ff00><b>/vote start</b></color>' to start it", 5f);
        }
    }

    private void OnPlayerVoted(ulong steamId, VotingType votingType)
    {
        if (_state != VotingState.Active)
        {
            return;
        }

        _ = HandleVoteAsync(steamId, votingType, _stateCts?.Token ?? default);
    }

    private async Task HandleVoteAsync(ulong steamId, VotingType votingType, CancellationToken ct = default)
    {
        VoteResult result = await RestController.SubmitVoteAsync(steamId, votingType, ct);

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
            _ = FetchAndDisplayVotesAsync(ct);
        }
    }

    private void OnZeepkistLobbyStateChanged(ZeepkistLobbyState zeepkistLobbyState)
    {
        if (_state == VotingState.Disabled)
        {
            return;
        }

        _logger.LogInfo($"Lobby state changed to: {zeepkistLobbyState}");

        if (zeepkistLobbyState == ZeepkistLobbyState.Racing)
        {
            if (_state != VotingState.Active)
            {
                _ = TransitionToStateAsync(VotingState.Active);
            }
        }
        else if (zeepkistLobbyState == ZeepkistLobbyState.NotInALobby)
        {
            // Optional: Disable or go Idle when leaving lobby
            _ = TransitionToStateAsync(VotingState.Idle);
        }
        else
        {
            if (_state == VotingState.Active)
            {
                _ = TransitionToIdleWithResultsAsync();
            }
        }
    }

    private async Task TransitionToIdleWithResultsAsync()
    {
        try
        {
            // First end voting (using current state token)
            await EndVotingAndShowResultsAsync(_stateCts?.Token ?? default);
        }
        finally
        {
            // Then transition to Idle
            await TransitionToStateAsync(VotingState.Idle);
        }
    }

    private async Task ResetAndStartNewVotingAsync(CancellationToken ct = default)
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

            bool success = await RestController.SetCurrentLevelAsync(CurrentLevelUid, CurrentLevelName, CurrentLevelAuthor,
                CurrentLevelWorkshopID.ToString(), ct);

            if (!success)
            {
                _logger.LogWarning("Failed to set current level on server.");
            }

            StartVotingCycle();
        }
        catch (OperationCanceledException)
        {
            _logger.LogInfo("ResetAndStartNewVotingAsync cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in ResetAndStartNewVotingAsync: {ex.Message}");
        }
    }

    private async Task EndVotingAndShowResultsAsync(CancellationToken ct = default)
    {
        try
        {
            string resetResult = await RestController.ResetVotesAsync(ct);

            ZeepkistNetwork.SendCustomChatMessage(
                true, 0,
                $"<#f0f0f0>{resetResult}<br>----------------</color>",
                ServermessageTitle);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInfo("EndVotingAndShowResultsAsync cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in EndVotingAndShowResultsAsync: {ex.Message}");
        }
    }

    // ── Logic ────────────────────────────────────────────────────────────────

    private async Task FetchAndDisplayVotesAsync(CancellationToken ct = default)
    {
        try
        {
            SendVoteReminderIfNeeded();

            VoteResult result = await RestController.FetchVoteTotalsAsync(ct);
            if (result == null)
            {
                return;
            }

            if (_state != VotingState.Active)
            {
                return;
            }

            _yesVotes = result.YesVotes;
            _noVotes = result.NoVotes;

            string message = BuildVoteDisplayMessage(result);
            ChatApi.SendMessage(
                $"/servermessage white 0 <align=\"left\"><size=\"30%\">{message}<br><#ffffff></size>" +
                $"<size=\"20%\"><voffset=-0.5em>Type !y if you like the current level</voffset><br>Type !n if you don't");
        }
        catch (OperationCanceledException)
        {
            // Normal when state changes
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error fetching votes: {ex.Message}");
        }
    }

    private void SendVoteReminderIfNeeded()
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
    }
}