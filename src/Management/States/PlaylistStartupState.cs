using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PlaylistVoting.Data.Models;
using PlaylistVoting.Management.States.Abstractions;
using PlaylistVoting.Networking.Zeepkist;
using ZeepUtils.Text;
using ZeepUtils.Zeepkist;

namespace PlaylistVoting.Management.States;

public class PlaylistStartupState : SessionState
{
    private readonly ZeepkistPlaylistService _playlistService;
    private readonly PlaylistSyncService _syncService;

    public PlaylistStartupState(VotingController controller, PlaylistSessionInfo session) : base(controller, session)
    {
        _playlistService = ZeepkistPlaylistService.Instance;
        _syncService = PlaylistSyncService.Instance;
    }

    public override void OnEnter()
    {
        _ = StartupAsync();
    }

    private async Task StartupAsync()
    {
        try
        {
            Logger.Info("PlaylistStartupState: Fetching online to-be-voted playlist...");
            List<LevelMetadata> onlineLevels = await Controller.BackendService.GetToBeVotedPlaylistAsync();

            if (onlineLevels == null)
            {
                Logger.Warn("PlaylistStartupState: onlineLevels is null.");
                ToastNotification.Error("Failed to download online playlist.");
                ZeepkistNetworkHelper.SendLocalPrivateMessage("Failed to download online playlist.");

                Controller.TransitionTo(new VotingDisabledState(Controller));
                return;
            }

            Logger.Info($"PlaylistStartupState: Fetched {onlineLevels.Count} online levels.");

            List<LevelMetadata> localLevels = _playlistService.GetCurrentZeepkistPlaylist();
            Logger.Info($"PlaylistStartupState: Local playlist has {localLevels.Count} levels.");

            if (localLevels.Any())
            {
                PlaylistComparisonResult comparison = _syncService.Compare(localLevels, onlineLevels);
                Logger.Info($"PlaylistStartupState: Comparison result - AreEqual: {comparison.AreEqual}");

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
                Logger.Info("PlaylistStartupState: Local levels empty, using online.");
                ToastNotification.Warning("Local playlist not detected. Using online.");
                ZeepkistNetworkHelper.SendLocalPrivateMessage("Could not detect local playlist. Using online playlist.");

                await StartWithPlaylistAsync(onlineLevels);
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"PlaylistStartupState: Exception in StartupAsync: {ex}");
            ToastNotification.Error("Error starting playlist mode.");
            Controller.TransitionTo(new VotingDisabledState(Controller));
        }
    }

    private async Task StartWithPlaylistAsync(List<LevelMetadata> levels)
    {
        ToastNotification.Info($"Starting with {levels.Count} levels");
        // Save toBeVoted locally
        string playlistName = $"{Session.DisplayName}-toBeVoted";
        _playlistService.SavePlaylist(playlistName, levels);

        // Sync with lobby
        Logger.Info("PlaylistStartupState: Syncing online playlist with Zeepkist lobby...");
        _playlistService.UpdateLobbyPlaylist(levels);

        string successMsg = new RichText().Append("Successfully initialized playlist!", b => b.Bold().Color("#00f8ad")).Break().Append($"{levels.Count} maps are now ready for voting.", b => b.Color("#dddddd")).Build();

        ZeepkistNetworkHelper.SendLocalPrivateMessage(successMsg);

        // Transition to active
        Controller.TransitionTo(new PlaylistVotingActiveState(Controller, Session, levels));
    }
}