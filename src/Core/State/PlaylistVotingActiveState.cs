using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PlaylistVoting.Core.Config;
using PlaylistVoting.Core.Controllers;
using PlaylistVoting.Core.Models;
using PlaylistVoting.Core.State.Abstractions;
using PlaylistVoting.Infrastructure.Zeepkist;
using YoloDev.Text;
using YoloDev.Zeepkist;
using ZeepkistClient;

namespace PlaylistVoting.Core.State;

public class PlaylistVotingActiveState : RunningState
{
    private readonly VotingResultBroadcaster _broadcaster;
    private readonly ZeepkistPlaylistService _playlistService;
    private readonly ServerMessageService _serverMessageService;
    private bool _hasRemindedToVote;
    private VotingResultResponse _lastResult;
    private string _lastTimeSent;
    private LevelMetadata _previousLevel;
    private List<LevelMetadata> _toBeVoted;

    public PlaylistVotingActiveState(VotingController controller, PlaylistSessionInfo session, List<LevelMetadata> toBeVoted) : base(controller, session)
    {
        _toBeVoted = toBeVoted;
        _broadcaster = new VotingResultBroadcaster();
        _serverMessageService = new ServerMessageService(controller.ServermessageTitle);
        _playlistService = new ZeepkistPlaylistService(controller.Logger);
    }

    protected override void OnRunningEnter()
    {
        _hasRemindedToVote = false;
        _ = HandleLevelLoadedAsync(); // Initial check if we are already on a level
    }

    protected override void OnRunningExit() { }

    public override void OnUpdate()
    {
        SendVoteReminderIfNeeded();
        SendTimerIfChanged();
    }

    private void SendTimerIfChanged()
    {
        string currentTime = ZeepkistNetwork.CurrentLobby?.timeLeftString ?? "--:--";
        if (currentTime != _lastTimeSent)
        {
            Controller.BackendService.SendTimer(currentTime);
            _lastTimeSent = currentTime;
        }
    }

    private void SendVoteReminderIfNeeded()
    {
        if (_hasRemindedToVote || ZeepkistNetwork.CurrentLobby == null)
        {
            return;
        }

        string[] parts = ZeepkistNetwork.CurrentLobby.timeLeftString.Split(':');
        if (parts.Length < 2)
        {
            return;
        }

        if (parts[0] == "00" && int.TryParse(parts[1], out int secs) && secs <= VotingConfig.Instance.VoteReminderThreshold)
        {
            string reminderMsg = new TMPRichTextBuilder()
                                 .Break()
                                 .AddLayer("LAST CHANCE TO ", b => b.Underline())
                                 .AddLayer("VOTE", b => b.Underline().Bold())
                                 .AddLayer("!", b => b.Underline())
                                 .Break()
                                 .AddLayer("Type ")
                                 .AddLayer("!y", b => b.Color("#00FF00").Bold())
                                 .AddLayer(" to ")
                                 .AddLayer("keep", b => b.Bold())
                                 .AddLayer(" this level in the playlist")
                                 .Break()
                                 .AddLayer("Type ")
                                 .AddLayer("!n", b => b.Color("#FF0000").Bold())
                                 .AddLayer(" to remove it")
                                 .Color("#f0f0f0")
                                 .Build();

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
            Controller.Logger.LogDebug($"Ignoring result for level {result.Level.Uid} (current is {Controller.CurrentLevel.Uid})");
            return;
        }

        _lastResult = result;
        RefreshDisplay();
    }

    private async Task HandleLevelLoadedAsync()
    {
        try
        {
            Controller.Logger.LogInfo("PlaylistVotingActiveState: Handling level loaded...");
            _lastResult = null; // Reset results for the new level
            RefreshDisplay(); // Show initial state immediately

            // 1. Broadcast result for previous level and finalize
            if (_previousLevel != null)
            {
                Controller.Logger.LogInfo($"PlaylistVotingActiveState: Finalizing previous level: {_previousLevel.Name} ({_previousLevel.Uid})");
                VotingResultResponse result = await Controller.BackendService.GetLevelResultAsync(_previousLevel.Uid);
                if (result != null)
                {
                    _broadcaster.BroadcastResult(_previousLevel, result.Votes);
                }

                await Controller.BackendService.FinalizeLevelAsync(_previousLevel.Uid);
            }

            // 2. Fetch remaining levels
            Controller.Logger.LogInfo("PlaylistVotingActiveState: Fetching to-be-voted playlist...");
            _toBeVoted = await Controller.BackendService.GetToBeVotedPlaylistAsync();
            if (_toBeVoted == null || !_toBeVoted.Any())
            {
                Controller.Logger.LogInfo("PlaylistVotingActiveState: No more levels in playlist.");
                await HandleFinishedAsync();
                return;
            }

            Controller.Logger.LogInfo($"PlaylistVotingActiveState: {_toBeVoted.Count} levels remaining.");

            // 3. Check if current level is in toBeVoted
            LevelMetadata currentLevel = ZeepkistMetadataProvider.GetCurrentLevelMetadata();
            LevelMetadata matchingLevel = _toBeVoted.FirstOrDefault(l => l.Uid == currentLevel.Uid);

            if (matchingLevel != null)
            {
                Controller.Logger.LogInfo($"PlaylistVotingActiveState: Current level '{matchingLevel.Name}' matches playlist. Setting in backend.");
                await Controller.BackendService.SetCurrentLevelAsync(matchingLevel, VotingConfig.Instance.IncludeAbstainVotes);
                _previousLevel = matchingLevel;
                Controller.CurrentLevel = matchingLevel;
            }
            else
            {
                Controller.Logger.LogWarning($"PlaylistVotingActiveState: Current level '{currentLevel.Name}' is NOT in playlist! Transitioning to WaitingForNextLevelState.");
                ToastNotification.Warning("Level not in voting playlist!");

                Controller.TransitionTo(new WaitingForNextLevelState(Controller, Session));
            }

            RefreshDisplay();
        }
        catch (Exception ex)
        {
            Controller.Logger.LogError($"PlaylistVotingActiveState: Exception in HandleLevelLoadedAsync: {ex}");
        }
    }

    private void RefreshDisplay()
    {
        _serverMessageService.UpdateDisplay(
            Session.DisplayName,
            Controller.CurrentLevel,
            _lastResult?.Votes,
            Controller.BackendService.IsConnected);
    }

    private async Task HandleFinishedAsync()
    {
        ToastNotification.Info("Playlist Voting finished!");
        if (ZeepkistNetwork.LocalPlayer != null)
        {
            MessageApi.SendPrivateCustomChatMessage("Playlist Voting finished!", "VOTING", ZeepkistNetwork.LocalPlayer.SteamID);
        }

        List<LevelMetadata> finalLevels = await Controller.BackendService.GetFinalPlaylistAsync();
        if (finalLevels != null)
        {
            _playlistService.SavePlaylist($"{Session.DisplayName}-Final", finalLevels);
            if (ZeepkistNetwork.LocalPlayer != null)
            {
                MessageApi.SendPrivateCustomChatMessage($"Saved final playlist: {Session.DisplayName}-Final", "VOTING", ZeepkistNetwork.LocalPlayer.SteamID);
            }
        }

        Controller.TransitionTo(new FinishedState(Controller));
    }
}