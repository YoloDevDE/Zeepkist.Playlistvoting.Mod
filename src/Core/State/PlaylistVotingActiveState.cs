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
    private readonly PlaylistFileService _playlistFileService;
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
        _playlistFileService = new PlaylistFileService(controller.Logger);
    }

    protected override void OnRunningEnter()
    {
        _hasRemindedToVote = false;
        _ = HandleLevelLoadedAsync(); // Initial check if we are already on a level
    }

    protected override void OnRunningExit()
    {
    }

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
                                 .Break()
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
        _lastResult = result;
        RefreshDisplay();
    }

    private async Task HandleLevelLoadedAsync()
    {
        // 1. Broadcast result for previous level and finalize
        if (_previousLevel != null)
        {
            VotingResultResponse result = await Controller.BackendService.FetchVotesAsync();
            if (result != null)
            {
                _broadcaster.BroadcastResult(_previousLevel, result.Votes);
            }

            await Controller.BackendService.FinalizeCurrentLevelAsync();
        }

        // 2. Fetch remaining levels
        _toBeVoted = await Controller.BackendService.GetToBeVotedPlaylistAsync();
        if (_toBeVoted == null || !_toBeVoted.Any())
        {
            await HandleFinishedAsync();
            return;
        }

        // 3. Check if current level is in toBeVoted
        // TODO: Implement broken-level handling (e.g., skip button or automatic detection)
        LevelMetadata currentLevel = ZeepkistMetadataProvider.GetCurrentLevelMetadata();
        LevelMetadata matchingLevel = _toBeVoted.FirstOrDefault(l => l.Uid == currentLevel.Uid);

        if (matchingLevel != null)
        {
            await Controller.BackendService.SetCurrentLevelAsync(matchingLevel, VotingConfig.Instance.IncludeAbstainVotes);
            _previousLevel = matchingLevel;
            Controller.CurrentLevel = matchingLevel;
        }
        else
        {
            if (ZeepkistNetwork.LocalPlayer != null)
            {
                string msg = new TMPRichTextBuilder()
                             .AddLayer("This level is not part of the active voting playlist.", b => b.Color("#FF0000"))
                             .Break()
                             .AddLayer("Voting will continue on the next playlist level.", b => b.Size(80))
                             .Build();
                MessageApi.SendPrivateCustomChatMessage(msg, "VOTING", ZeepkistNetwork.LocalPlayer.SteamID);
            }

            _previousLevel = null;
            Controller.CurrentLevel = currentLevel;
        }

        RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        _serverMessageService.UpdateDisplay(
            Session.DisplayName,
            Controller.CurrentLevel,
            _lastResult?.Votes,
            Controller.BackendService.IsConnected,
            _toBeVoted?.Count);
    }

    private async Task HandleFinishedAsync()
    {
        if (ZeepkistNetwork.LocalPlayer != null)
        {
            MessageApi.SendPrivateCustomChatMessage("Playlist Voting finished!", "VOTING", ZeepkistNetwork.LocalPlayer.SteamID);
        }

        List<LevelMetadata> finalLevels = await Controller.BackendService.GetFinalPlaylistAsync();
        if (finalLevels != null)
        {
            _playlistFileService.SavePlaylist($"{Session.DisplayName}-Final", finalLevels);
            if (ZeepkistNetwork.LocalPlayer != null)
            {
                MessageApi.SendPrivateCustomChatMessage($"Saved final playlist: {Session.DisplayName}-Final", "VOTING", ZeepkistNetwork.LocalPlayer.SteamID);
            }
        }

        Controller.TransitionTo(new FinishedState(Controller));
    }
}