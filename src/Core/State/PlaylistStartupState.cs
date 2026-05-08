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
    private readonly ZeepkistPlaylistService _playlistService;
    private readonly PlaylistSyncService _syncService;

    public PlaylistStartupState(VotingController controller, PlaylistSessionInfo session) : base(controller, session)
    {
        _playlistService = new ZeepkistPlaylistService(controller.Logger);
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
            ToastNotification.Error("Failed to download online playlist.");
            if (ZeepkistNetwork.LocalPlayer != null)
            {
                MessageApi.SendPrivateCustomChatMessage("Failed to download online playlist.", "VOTING", ZeepkistNetwork.LocalPlayer.SteamID);
            }

            Controller.TransitionTo(new NoSessionState(Controller));
            return;
        }

        List<LevelMetadata> localLevels = _playlistService.GetCurrentZeepkistPlaylist();

        if (localLevels.Any())
        {
            PlaylistComparisonResult comparison = _syncService.Compare(localLevels, onlineLevels);

            if (!comparison.AreEqual)
            {
                Controller.TransitionTo(new PlaylistConflictState(Controller, Session, localLevels, onlineLevels));
            }
            else
            {
                // Sync is perfect
                await StartWithPlaylistAsync(onlineLevels);
            }
        }
        else
        {
            // localLevels is empty.
            ToastNotification.Warning("Local playlist not detected. Using online.");
            if (ZeepkistNetwork.LocalPlayer != null)
            {
                MessageApi.SendPrivateCustomChatMessage("Could not detect local playlist. Using online playlist.", "VOTING", ZeepkistNetwork.LocalPlayer.SteamID);
            }

            await StartWithPlaylistAsync(onlineLevels);
        }
    }

    private async Task StartWithPlaylistAsync(List<LevelMetadata> levels)
    {
        ToastNotification.Info($"Starting with {levels.Count} levels");
        // Save toBeVoted locally
        string playlistName = $"{Session.DisplayName}-toBeVoted";
        _playlistService.SavePlaylist(playlistName, levels);

        // Transition to active
        Controller.TransitionTo(new PlaylistVotingActiveState(Controller, Session, levels));
    }
}