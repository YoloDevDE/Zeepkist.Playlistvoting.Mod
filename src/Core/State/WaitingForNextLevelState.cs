using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PlaylistVoting.Core.Controllers;
using PlaylistVoting.Core.Models;
using PlaylistVoting.Core.State.Abstractions;
using PlaylistVoting.Infrastructure.Zeepkist;

namespace PlaylistVoting.Core.State;

public class WaitingForNextLevelState : RunningState
{
    private readonly ServerMessageService _serverMessageService;

    public WaitingForNextLevelState(VotingController controller, PlaylistSessionInfo session) : base(controller, session)
    {
        _serverMessageService = new ServerMessageService(controller.ServermessageTitle);
    }

    protected override void OnRunningEnter()
    {
        RefreshDisplay();
    }

    public override void OnLevelLoaded()
    {
        _ = CheckNextLevelAsync();
    }

    private async Task CheckNextLevelAsync()
    {
        try
        {
            Controller.Logger.LogInfo("WaitingForNextLevelState: Checking next level...");
            List<LevelMetadata> toBeVoted = await Controller.BackendService.GetToBeVotedPlaylistAsync();

            if (toBeVoted == null || !toBeVoted.Any())
            {
                Controller.Logger.LogInfo("WaitingForNextLevelState: No more levels in playlist. Transitioning to Active to handle finish.");
                Controller.TransitionTo(new PlaylistVotingActiveState(Controller, Session, toBeVoted ?? new List<LevelMetadata>()));
                return;
            }

            LevelMetadata currentLevel = ZeepkistMetadataProvider.GetCurrentLevelMetadata();
            if (toBeVoted.Any(l => l.Uid == currentLevel.Uid))
            {
                Controller.Logger.LogInfo($"WaitingForNextLevelState: Next level '{currentLevel.Name}' is in playlist. Transitioning back to Active.");
                Controller.TransitionTo(new PlaylistVotingActiveState(Controller, Session, toBeVoted));
            }
            else
            {
                Controller.Logger.LogInfo($"WaitingForNextLevelState: Level '{currentLevel.Name}' still not in playlist.");
                RefreshDisplay();
            }
        }
        catch (Exception ex)
        {
            Controller.Logger.LogError($"WaitingForNextLevelState: Exception in CheckNextLevelAsync: {ex}");
        }
    }

    private void RefreshDisplay()
    {
        LevelMetadata currentLevel = ZeepkistMetadataProvider.GetCurrentLevelMetadata();
        _serverMessageService.UpdateDisplay(
            Session.DisplayName,
            currentLevel,
            null,
            Controller.BackendService.IsConnected);
    }
}