using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using PlaylistVoting.api;
using PlaylistVoting.core;
using ZeepkistClient;
using ZeepkistNetworking;
using ZeepSDK.Chat;
using ZeepSDK.Level;
using ZeepSDK.Messaging;
using ZeepSDK.Multiplayer;
using ZeepSDK.Racing;

namespace PlaylistVoting.states;

public class StateActive : State
{
    // ── Constants ─────────────────────────────────────────────────────────────
    private const int TimerIntervalMs = 1000;
    private const int LevelLoadedBaseDelaySeconds = 3;
    private const int VoteReminderThresholdSeconds = 30;
    private bool _hasRemindedToVote;
    private int _noVotes;
    private Timer _pollTimer;

    // ── Fields ────────────────────────────────────────────────────────────────
    private int _yesVotes;

    public StateActive(VotingManager manager) : base(manager) { }

    private bool IsCurrentlyRacing => Manager.CurrentPhase == GamePhase.Racing;

    // ── State lifecycle ───────────────────────────────────────────────────────

    public override void Enter()
    {
        base.Enter();
        VotingEventBus.Hub.PlayerVotedYes += OnPlayerVotedYes;
        VotingEventBus.Hub.PlayerVotedNo += OnPlayerVotedNo;
        VotingEventBus.Hub.PlayerVotedRemove += OnPlayerVotedRemove;
        VotingEventBus.Hub.VoteStartRequested += OnVoteStartRequestedWhileActive;
        VotingEventBus.Hub.VoteStopRequested += OnVoteStopRequested;
        RacingApi.LevelLoaded += OnLevelLoaded;
        RacingApi.RoundEnded += OnRoundEnded;
        MultiplayerApi.DisconnectedFromGame += OnVoteStopRequested;
        ZeepkistNetwork.MasterChanged += OnMasterChanged;

        if (Manager.CurrentPhase == GamePhase.Racing)
        {
            StartPolling();
        }

        bool isNewLevel = Manager.CurrentLevelUid != LevelApi.CurrentLevel.UID;
        if (isNewLevel)
        {
            _ = Manager.ResetVotesAsync(false);
        }
    }


    public override void Exit()
    {
        base.Exit();
        VotingEventBus.Hub.PlayerVotedYes -= OnPlayerVotedYes;
        VotingEventBus.Hub.PlayerVotedNo -= OnPlayerVotedNo;
        VotingEventBus.Hub.PlayerVotedRemove -= OnPlayerVotedRemove;
        VotingEventBus.Hub.VoteStartRequested -= OnVoteStartRequestedWhileActive;
        VotingEventBus.Hub.VoteStopRequested -= OnVoteStopRequested;
        RacingApi.LevelLoaded -= OnLevelLoaded;
        RacingApi.RoundEnded -= OnRoundEnded;
        MultiplayerApi.DisconnectedFromGame -= OnVoteStopRequested;
        ZeepkistNetwork.MasterChanged -= OnMasterChanged;

        StopPolling();
    }

    protected override void OnGamePhaseChanged(GamePhase phase)
    {
        if (phase == GamePhase.Racing)
        {
            StartPolling();
        }
        else
        {
            StopPolling();
        }
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void OnRoundEnded()
    {
        /* Reserved for future use */
    }

    private void OnMasterChanged(ZeepkistNetworkPlayer newMaster)
    {
        MessengerApi.LogWarning("Vote stopped because the host changed!");
        Manager.SwitchState(new StateInactive(Manager));
    }

    private void OnVoteStartRequestedWhileActive() => MessengerApi.LogWarning("A vote is already running!");

    private void OnVoteStopRequested()
    {
        MessengerApi.LogSuccess("Vote successfully stopped!");
        Manager.SwitchState(new StateInactive(Manager));
    }

    private void OnPlayerVotedYes(ulong playerId)
    {
        ZeepkistNetwork.SendCustomChatMessage(false, playerId, "You voted 'yes'.", Manager.ServermessageTitle);
        _ = FetchAndDisplayVotesAsync(Manager.VotingApiClient.SubmitVoteYesAsync(playerId));
    }

    private void OnPlayerVotedRemove(ulong playerId)
    {
        ZeepkistNetwork.SendCustomChatMessage(false, playerId, "Your vote was removed.", Manager.ServermessageTitle);
        _ = FetchAndDisplayVotesAsync(Manager.VotingApiClient.SubmitVoteRemoveAsync(playerId));
    }

    private void OnPlayerVotedNo(ulong playerId)
    {
        ZeepkistNetwork.SendCustomChatMessage(false, playerId, "You voted 'no'.", Manager.ServermessageTitle);
        _ = FetchAndDisplayVotesAsync(Manager.VotingApiClient.SubmitVoteNoAsync(playerId));
    }

    private void OnLevelLoaded()
    {
        StopPolling();
        // TODO Handle level loaded logic
        _ = Manager.ResetVotesAsync();
        StartPolling();
    }

    // ── Level change logic ────────────────────────────────────────────────────


    private void TryRemoveRejectedLevelFromPlaylist()
    {
        string rejectedUid = Manager.CurrentLevelUid;
        bool levelExistsInPlaylist = ZeepkistNetwork.CurrentLobby.Playlist.Any(l => l.UID.Equals(rejectedUid));

        if (!levelExistsInPlaylist)
        {
            MessengerApi.LogWarning(
                $"Could not remove '{Manager.CurrentLevelName} by {Manager.CurrentLevelAuthor}' — not found in playlist.", 5f);
            return;
        }

        RemoveLevelFromPlaylist(rejectedUid);

        bool removed = !ZeepkistNetwork.CurrentLobby.Playlist.Any(l => l.UID.Equals(rejectedUid));
        if (removed)
        {
            MessengerApi.Log($"'{Manager.CurrentLevelName} by {Manager.CurrentLevelAuthor}' was removed from the playlist.");
        }
        else
        {
            MessengerApi.LogWarning("Failed to remove the level from the playlist!", 5f);
        }
    }

    private void RemoveLevelFromPlaylist(string uid)
    {
        ZeepkistLobby lobby = ZeepkistNetwork.CurrentLobby;

        List<OnlineZeeplevel> filtered = lobby.Playlist
                                              .Where(l => !l.UID.Equals(uid))
                                              .ToList();

        lobby.Playlist.Clear();
        lobby.Playlist.AddRange(filtered);
        lobby.PlaylistRandom = false;

        lobby.CurrentPlaylistIndex = lobby.CurrentPlaylistIndex > 0 ? lobby.CurrentPlaylistIndex - 1 : 0;
        lobby.NextPlaylistIndex = lobby.NextPlaylistIndex < lobby.Playlist.Count ? lobby.NextPlaylistIndex - 1 : 0;

        ZeepkistNetwork.SendLobbyPlaylistToServer(lobby.CurrentPlaylistIndex, lobby.NextPlaylistIndex);
    }

    // ── Polling ───────────────────────────────────────────────────────────────

    private void StartPolling()
    {
        if (_pollTimer != null)
        {
            return;
        }

        _hasRemindedToVote = false;
        _pollTimer = new Timer(TimerIntervalMs) { AutoReset = true };
        _pollTimer.Elapsed += OnPollTimerElapsed;
        _pollTimer.Start();
    }

    private void StopPolling()
    {
        if (_pollTimer == null)
        {
            return;
        }

        _pollTimer.Stop();
        _pollTimer.Elapsed -= OnPollTimerElapsed;
        _pollTimer.Dispose();
        _pollTimer = null;

        if (IsCurrentlyRacing)
        {
            ChatApi.SendMessage("/servermessage remove");
        }
    }

    private void OnPollTimerElapsed(object sender, ElapsedEventArgs e) => _ = FetchAndDisplayVotesAsync(Manager.VotingApiClient.FetchVoteTotalsAsync());

    // ── Vote display ──────────────────────────────────────────────────────────

    private async Task FetchAndDisplayVotesAsync(Task<VoteResult?> apiCall)
    {
        try
        {
            SendVoteReminderIfNeeded();

            VoteResult? result = await apiCall;
            if (result == null)
            {
                MessengerApi.LogError("Vote system error: received invalid API response.");
                return;
            }

            if (!IsCurrentlyRacing)
            {
                StopPolling();
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
            ChatApi.SendMessage($"Error: {ex.Message}");
        }
    }

    private void SendVoteReminderIfNeeded()
    {
        if (_hasRemindedToVote)
        {
            return;
        }

        string[] timeParts = ZeepkistNetwork.CurrentLobby.timeLeftString.Split(":");
        bool isLastMinute = timeParts[0] == "00";
        bool underThreshold = int.Parse(timeParts[1]) <= VoteReminderThresholdSeconds;

        if (!isLastMinute || !underThreshold)
        {
            return;
        }

        ZeepkistNetwork.SendCustomChatMessage(
            true, 0,
            "<br><#f0f0f0>LAST CHANCE TO <b>VOTE</b>!<br>" +
            "Type <#00FF00><b>!y</b></color> to <b>keep</b> this level in the playlist<br>" +
            "Type <#FF0000><b>!n</b></color> to remove it<br>----------------</color>",
            Manager.ServermessageTitle);

        _hasRemindedToVote = true;
    }

    private string BuildVoteDisplayMessage(VoteResult result)
    {
        string emote = result.IsYesWinning
            ? Manager.WinEmote
            : result.IsNoWinning
                ? Manager.LoseEmote
                : Manager.TieEmote;

        return $"<b><u>{Manager.ServermessageTitle}</u></b><br>" +
               $"<#ff9900>{Manager.CurrentLevelName}</color> <#ffffff>by</color> <#ff9900>{Manager.CurrentLevelAuthor}</color><br>" +
               $"Votes: <#00aa00>{result.YesVotes}</color><#ffffff>/<#aa0000>{result.NoVotes}</color> (yes/no) (!y/!n) {emote}";
    }
}