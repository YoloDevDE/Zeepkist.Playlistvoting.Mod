using System.Threading.Tasks;
using PlaylistVoting.Core.Controllers;
using PlaylistVoting.Core.Models;
using PlaylistVoting.Core.State.Abstractions;
using PlaylistVoting.Infrastructure.Zeepkist;
using ZeepkistClient;

namespace PlaylistVoting.Core.State;

public class SimpleVotingActiveState : RunningState
{
    private readonly VotingResultBroadcaster _broadcaster;
    private readonly ServerMessageService _serverMessageService;
    private LevelMetadata _currentLevel;
    private VotingResultResponse _lastResult;
    private string _lastTimeSent;

    public SimpleVotingActiveState(VotingController controller, PlaylistSessionInfo session) : base(controller, session)
    {
        _broadcaster = new VotingResultBroadcaster();
        _serverMessageService = new ServerMessageService(controller.ServermessageTitle);
    }

    protected override void OnRunningEnter()
    {
        _currentLevel = ZeepkistMetadataProvider.GetCurrentLevelMetadata();
        Controller.CurrentLevel = _currentLevel;
        RefreshDisplay();
    }

    protected override void OnRunningExit() { }

    public override void OnUpdate()
    {
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
        // 1. Broadcast result for the level that just finished
        if (_currentLevel != null)
        {
            VotingResultResponse result = await Controller.BackendService.GetLevelResultAsync(_currentLevel.Uid);
            if (result != null)
            {
                _broadcaster.BroadcastResult(_currentLevel, result.Votes);
            }

            // 2. Reset votes for that level
            await Controller.BackendService.ResetVotesForLevelAsync(_currentLevel.Uid);
        }

        // 3. Update to new level
        _currentLevel = ZeepkistMetadataProvider.GetCurrentLevelMetadata();
        Controller.CurrentLevel = _currentLevel;

        RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        _serverMessageService.UpdateDisplay(
            Session.DisplayName,
            _currentLevel,
            _lastResult?.Votes,
            Controller.BackendService.IsConnected);
    }
}