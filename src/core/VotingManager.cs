using System;
using System.Threading.Tasks;
using BepInEx.Logging;
using PlaylistVoting.api;
using PlaylistVoting.commands.local;
using PlaylistVoting.commands.remote;
using PlaylistVoting.misc;
using UnityEngine;
using ZeepkistClient;
using ZeepSDK.Chat;
using ZeepSDK.ChatCommands;

namespace PlaylistVoting.core;

public enum VotingState
{
    Inactive,
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
    private bool _hasRemindedToVote;

    // ── Private fields ───────────────────────────────────────────────────────
    private ManualLogSource _logger;
    private int _noVotes;
    private float _pollTimer;
    private VotingState _state = VotingState.Inactive;

    private bool _voteStartRequested;

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
        switch (_state)
        {
            case VotingState.Inactive:
                UpdateInactive();
                break;
            case VotingState.Active:
                UpdateActive();
                break;
            default:
                throw new ArgumentOutOfRangeException();
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
        VotingEventBus.Hub.VotingStarted+= OnVoteStartRequested;
        VotingEventBus.Hub.VotingStopped += OnVoteStopRequested;
        VotingEventBus.Hub.PlayerVoted += OnPlayerVoted;
        VotingEventBus.Hub.ZeepkistLobbyStateChanged += OnZeepkistLobbyStateChanged;
    }

    private void UpdateInactive()
    {
        if (_voteStartRequested && CurrentZeepkistLobbyState == ZeepkistLobbyState.Racing)
        {
            _voteStartRequested = false;
            StartVote();
        }
    }

    private void UpdateActive()
    {
        if (CurrentZeepkistLobbyState != ZeepkistLobbyState.Racing)
        {
            StopVote();
            return;
        }

        _pollTimer -= Time.deltaTime;
        if (_pollTimer <= 0)
        {
            _pollTimer = TimerIntervalSeconds;
            _ = FetchAndDisplayVotesAsync();
        }
    }

    // ── State Transitions ────────────────────────────────────────────────────

    private void StartVote()
    {
        _state = VotingState.Active;
        _hasRemindedToVote = false;
        _pollTimer = 0; // Trigger immediate poll
        ToastNotification.Custom("Mod started", Color.black, Color.green);
    }

    private void StopVote()
    {
        _state = VotingState.Inactive;
        ChatApi.SendMessage("/servermessage remove");
    }

    // ── Event Handlers ───────────────────────────────────────────────────────

    private void OnVoteStartRequested()
    {
        if (!ZeepkistNetwork.LocalPlayer.isHost)
        {
            ToastNotification.Warning("Only the host can start the vote!");
            return;
        }

        if (_state == VotingState.Active)
        {
            ToastNotification.Warning("Mod is already running.");
            return;
        }

        if (CurrentZeepkistLobbyState != ZeepkistLobbyState.Racing)
        {
            _voteStartRequested = true;
            ToastNotification.Custom("start pending.<br>Playlist Voting will immediately start next round!", Color.black, Color.yellow);
            return;
        }

        ToastNotification.Custom("Mod started", Color.black, Color.green);
        StartVote();
    }

    private void OnVoteStopRequested()
    {
        if (_state == VotingState.Active)
        {
            StopVote();
            ToastNotification.Custom("stopped", Color.black, Color.red);
        }
        else
        {
            ToastNotification.Warning("is not currently running.<br>Type '<#00ff00><b>/vote start<b/></color>' to start it");
        }
    }

    private void OnPlayerVoted(ulong steamId, VotingType votingType)
    {
        if (_state != VotingState.Active)
        {
            return;
        }

        _ = HandleVoteAsync(steamId, votingType);
    }

    private async Task HandleVoteAsync(ulong steamId, VotingType votingType)
    {
        VoteResult result = await RestController.SubmitVoteAsync(steamId, votingType);

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
            _ = FetchAndDisplayVotesAsync();
        }
    }

    private void OnZeepkistLobbyStateChanged(ZeepkistLobbyState phase)
    {
        if (phase != ZeepkistLobbyState.Racing && _state == VotingState.Active)
        {
            // End of round logic
            _ = ResetVotesAsync(false);
            StopVote();
        }
    }

    // ── Logic ────────────────────────────────────────────────────────────────

    private async Task FetchAndDisplayVotesAsync()
    {
        try
        {
            SendVoteReminderIfNeeded();

            VoteResult result = await RestController.FetchVoteTotalsAsync();
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
        catch (Exception ex)
        {
            if (_logger != null)
            {
                _logger.LogError($"Error fetching votes: {ex.Message}");
            }
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
                "<br><#f0f0f0>LAST CHANCE TO <b>VOTE</b>!<br>" +
                "Type <#00FF00><b>!y</b></color> to <b>keep</b> this level in the playlist<br>" +
                "Type <#FF0000><b>!n</b></color> to remove it<br>----------------</color>",
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


    public async Task ResetVotesAsync(bool printResults = true)
    {
        try
        {
            string resetResult = await RestController.ResetVotesAsync();

            if (printResults)
            {
                ZeepkistNetwork.SendCustomChatMessage(
                    true, 0,
                    $"<#f0f0f0>{resetResult}<br>----------------</color>",
                    ServermessageTitle);
            }

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

            bool success = await RestController.SetCurrentLevelAsync(CurrentLevelUid, CurrentLevelName, CurrentLevelAuthor, CurrentLevelWorkshopID.ToString());
            if (!success)
            {
                ZeepkistNetwork.SendCustomChatMessage(
                    true, 0,
                    "<#ff6b6b>Error: Failed to update map data on the server. Please check your API token and connection.</color>",
                    ServermessageTitle);
            }
        }
        catch (Exception ex)
        {
            ZeepkistNetwork.SendCustomChatMessage(
                true, 0,
                $"<#ff6b6b>Error during vote reset: {ex.Message}<br>Please try again or contact an administrator.</color>",
                ServermessageTitle);
        }
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