using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PlaylistVoting.Core.Controllers;
using PlaylistVoting.Core.Models;
using PlaylistVoting.Core.State.Abstractions;
using PlaylistVoting.Infrastructure.Zeepkist;
using YoloDev.Zeepkist;
using ZeepkistClient;

namespace PlaylistVoting.Core.State;

public class PlaylistStartupState : SessionState
{
    private readonly PlaylistFileService _playlistFileService;
    private readonly PlaylistSyncService _syncService;

    public PlaylistStartupState(VotingController controller, PlaylistSessionInfo session) : base(controller, session)
    {
        _playlistFileService = new PlaylistFileService(controller.Logger);
        _syncService = new PlaylistSyncService();
    }

    public override void OnEnter()
    {
        _ = StartupAsync();
    }

    private async Task StartupAsync()
    {
        List<LevelMetadata> onlineLevels = await Controller.BackendService.GetToBeVotedPlaylistAsync();
        if (onlineLevels == null)
        {
            if (ZeepkistNetwork.LocalPlayer != null)
            {
                MessageApi.SendPrivateCustomChatMessage("Failed to download online playlist.", "VOTING", ZeepkistNetwork.LocalPlayer.SteamID);
            }

            Controller.TransitionTo(new NoSessionState(Controller));
            return;
        }

        // TODO: Read current local Zeepkist playlist properly
        List<LevelMetadata> localLevels = _playlistFileService.GetCurrentZeepkistPlaylist();

        PlaylistComparisonResult comparison = _syncService.Compare(localLevels, onlineLevels);

        if (!comparison.AreEqual && localLevels.Any())
        {
            Controller.TransitionTo(new PlaylistConflictState(Controller, Session, localLevels, onlineLevels));
        }
        else
        {
            // Sync is perfect or local is empty, just use online
            await StartWithPlaylistAsync(onlineLevels);
        }
    }

    private async Task StartWithPlaylistAsync(List<LevelMetadata> levels)
    {
        // Save toBeVoted locally
        string playlistName = $"{Session.DisplayName}-toBeVoted";
        _playlistFileService.SavePlaylist(playlistName, levels);

        // Transition to active
        Controller.TransitionTo(new PlaylistVotingActiveState(Controller, Session, levels));
    }
}