using System;
using System.Threading.Tasks;
using PlaylistVoting.Data.DTOs;
using PlaylistVoting.Data.Models;
using PlaylistVoting.Management.States.Abstractions;
using PlaylistVoting.Networking.Zeepkist;

namespace PlaylistVoting.Management.States;

public class SimpleVotingActiveState : RunningState
{
    private readonly VotingResultBroadcaster _broadcaster;
    private LevelMetadata _currentLevel;
    private VotingResultResponse _lastResult;

    public SimpleVotingActiveState(VotingController controller, PlaylistSessionInfo session) : base(controller, session)
    {
        _broadcaster = new VotingResultBroadcaster();
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
        base.OnUpdate();
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
            _lastResult = null; // Reset results for the new level
            RefreshDisplay();

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
        catch (Exception ex)
        {
            Logger.Error($"SimpleVotingActiveState: Exception in HandleLevelLoadedAsync: {ex}");
        }
    }

    protected override void OnRefreshDisplay()
    {
        RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        Controller.OverlayService.UpdateVotingDisplay(Session.DisplayName, _currentLevel, _lastResult?.Votes, Controller.BackendService.IsConnected);
    }
}