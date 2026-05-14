using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PlaylistVoting.Data.DTOs;
using PlaylistVoting.Data.Models;
using PlaylistVoting.Management.States.Abstractions;
using PlaylistVoting.Networking.Zeepkist;
using ZeepkistClient;
using ZeepUtils.Text;
using ZeepUtils.Zeepkist;

namespace PlaylistVoting.Management.States;

public class PlaylistVotingActiveState : RunningState
{
    private readonly VotingResultBroadcaster _broadcaster;
    private readonly ZeepkistPlaylistService _playlistService;
    private bool _hasRemindedToVote;
    private bool _isFirstCheck = true;
    private VotingResultResponse _lastResult;
    private LevelMetadata _previousLevel;
    private List<LevelMetadata> _toBeVoted;

    public PlaylistVotingActiveState(VotingController controller, PlaylistSessionInfo session, List<LevelMetadata> toBeVoted) : base(controller, session)
    {
        _toBeVoted = toBeVoted;
        _broadcaster = new VotingResultBroadcaster();
        _playlistService = new ZeepkistPlaylistService();
    }

    protected override void OnRunningEnter()
    {
        _hasRemindedToVote = false;
        _ = HandleLevelLoadedAsync(); // Initial check if we are already on a level
    }

    protected override void OnRunningExit() { }

    public override void OnUpdate()
    {
        base.OnUpdate();
        SendVoteReminderIfNeeded();
    }

    private void SendVoteReminderIfNeeded()
    {
        if (_hasRemindedToVote || ZeepkistNetwork.CurrentLobby == null)
        {
            return;
        }

        double timeLeft = (ZeepkistNetwork.CurrentLobby.LevelLoadedAtTime + ZeepkistNetwork.CurrentLobby.RoundTime) - ZeepkistNetwork.Time;

        if (timeLeft <= VotingConfig.Instance.VoteReminderThreshold)
        {
            string reminderMsg = new RichText().Break().Append("LAST CHANCE TO ", b => b.Underline()).Append("VOTE", b => b.Underline().Bold()).Append("!", b => b.Underline()).Break().Append("Type ").Append("!y", b => b.Color("#00FF00").Bold())
                                               .Append(" to ").Append("keep", b => b.Bold()).Append(" this level in the playlist").Break().Append("Type ").Append("!n", b => b.Color("#FF0000").Bold()).Append(" to remove it").Color("#f0f0f0").Build();

            MessageApi.SendBroadcastCustomChatMessageTo(reminderMsg, Controller.ServermessageTitle);
            _hasRemindedToVote = true;
        }
    }

    public override void OnLevelLoaded()
    {
        _ = HandleLevelLoadedAsync();
    }

    public override void OnVotingResultReceived(VotingResultResponse result)
    {
        if (Controller.CurrentLevel != null && result.Level != null && result.Level.Uid != Controller.CurrentLevel.Uid)
        {
            Logger.Debug($"Ignoring result for level {result.Level.Uid} (current is {Controller.CurrentLevel.Uid})");
            return;
        }

        _lastResult = result;
        RefreshDisplay();
    }

    private async Task HandleLevelLoadedAsync()
    {
        try
        {
            Logger.Info("PlaylistVotingActiveState: Handling level loaded...");
            _lastResult = null; // Reset results for the new level
            RefreshDisplay(); // Show initial state immediately

            // 1. Broadcast result for previous level and finalize
            if (_previousLevel != null)
            {
                Logger.Info($"PlaylistVotingActiveState: Finalizing previous level: {_previousLevel.Name} ({_previousLevel.Uid})");
                VotingResultResponse result = await Controller.BackendService.GetLevelResultAsync(_previousLevel.Uid);

                if (result != null)
                {
                    _broadcaster.BroadcastResult(_previousLevel, result.Votes);
                }

                await Controller.BackendService.FinalizeLevelAsync(_previousLevel.Uid);
            }

            // 2. Fetch remaining levels
            Logger.Info("PlaylistVotingActiveState: Fetching to-be-voted playlist...");
            _toBeVoted = await Controller.BackendService.GetToBeVotedPlaylistAsync();

            if (_toBeVoted == null || !_toBeVoted.Any())
            {
                Logger.Info("PlaylistVotingActiveState: No more levels in playlist.");
                await HandleFinishedAsync();
                return;
            }

            Logger.Info($"PlaylistVotingActiveState: {_toBeVoted.Count} levels remaining.");

            // 3. Check if current level is in toBeVoted
            LevelMetadata currentLevel = ZeepkistMetadataProvider.GetCurrentLevelMetadata();
            LevelMetadata matchingLevel = _toBeVoted.FirstOrDefault(l => l.Uid == currentLevel.Uid);

            if (matchingLevel != null)
            {
                Logger.Info($"PlaylistVotingActiveState: Current level '{matchingLevel.Name}' matches playlist. Setting in backend.");
                await Controller.BackendService.SetCurrentLevelAsync(matchingLevel, VotingConfig.Instance.IncludeAbstainVotes);
                _previousLevel = matchingLevel;
                Controller.CurrentLevel = matchingLevel;
                _isFirstCheck = false;
            }
            else
            {
                Logger.Warn($"PlaylistVotingActiveState: Current level '{currentLevel.Name}' is NOT in playlist! Transitioning to WaitingForNextLevelState.");
                ToastNotification.Warning("Level not in voting playlist!");

                Controller.TransitionTo(new WaitingForNextLevelState(Controller, Session, _isFirstCheck));
                _isFirstCheck = false;
            }

            RefreshDisplay();
        }
        catch (Exception ex)
        {
            Logger.Error($"PlaylistVotingActiveState: Exception in HandleLevelLoadedAsync: {ex}");
        }
    }

    protected override void OnRefreshDisplay()
    {
        RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        Controller.OverlayService.UpdateVotingDisplay(Session.DisplayName, Controller.CurrentLevel, _lastResult?.Votes, Controller.BackendService.IsConnected);
    }

    private async Task HandleFinishedAsync()
    {
        ToastNotification.Info("Playlist Voting finished!");
        ZeepkistNetworkHelper.SendLocalPrivateMessage("Playlist Voting finished!");

        List<LevelMetadata> finalLevels = await Controller.BackendService.GetFinalPlaylistAsync();

        if (finalLevels != null)
        {
            _playlistService.SavePlaylist($"{Session.DisplayName}-Final", finalLevels);
            ZeepkistNetworkHelper.SendLocalPrivateMessage($"Saved final playlist: {Session.DisplayName}-Final");
        }

        Controller.TransitionTo(new FinishedState(Controller));
    }
}