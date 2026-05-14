using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PlaylistVoting.Data.Models;
using PlaylistVoting.Management.States.Abstractions;
using PlaylistVoting.Networking.Zeepkist;

namespace PlaylistVoting.Management.States;

public class WaitingForNextLevelState : RunningState
{
    private readonly bool _isInitial;

    public WaitingForNextLevelState(VotingController controller, PlaylistSessionInfo session, bool isInitial = false) : base(controller, session)
    {
        _isInitial = isInitial;
    }

    protected override void OnRunningEnter()
    {
        if (!_isInitial)
        {
            RefreshDisplay();
        }
    }

    public override void OnUpdate()
    {
        base.OnUpdate();
    }

    public override void OnLevelLoaded()
    {
        _ = CheckNextLevelAsync();
    }

    private async Task CheckNextLevelAsync()
    {
        try
        {
            Logger.Info("WaitingForNextLevelState: Checking next level...");
            List<LevelMetadata> toBeVoted = await Controller.BackendService.GetToBeVotedPlaylistAsync();

            if (toBeVoted == null || !toBeVoted.Any())
            {
                Logger.Info("WaitingForNextLevelState: No more levels in playlist. Transitioning to Active to handle finish.");
                Controller.TransitionTo(new PlaylistVotingActiveState(Controller, Session, toBeVoted ?? new List<LevelMetadata>()));
                return;
            }

            LevelMetadata currentLevel = ZeepkistMetadataProvider.GetCurrentLevelMetadata();

            if (toBeVoted.Any(l => l.Uid == currentLevel.Uid))
            {
                Logger.Info($"WaitingForNextLevelState: Next level '{currentLevel.Name}' is in playlist. Transitioning back to Active.");
                Controller.TransitionTo(new PlaylistVotingActiveState(Controller, Session, toBeVoted));
            }
            else
            {
                Logger.Info($"WaitingForNextLevelState: Level '{currentLevel.Name}' still not in playlist.");
                RefreshDisplay();
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"WaitingForNextLevelState: Exception in CheckNextLevelAsync: {ex}");
        }
    }

    protected override void OnRefreshDisplay()
    {
        RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        LevelMetadata currentLevel = ZeepkistMetadataProvider.GetCurrentLevelMetadata();
        Controller.OverlayService.UpdateVotingDisplay(Session.DisplayName, currentLevel, null, Controller.BackendService.IsConnected);
    }
}