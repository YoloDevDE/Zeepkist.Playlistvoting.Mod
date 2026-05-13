using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PlaylistVoting.Core.Controllers;
using PlaylistVoting.Core.Models;
using PlaylistVoting.Core.State.Abstractions;
using PlaylistVoting.Core.State.Active;
using PlaylistVoting.Infrastructure.UI;
using PlaylistVoting.Infrastructure.Zeepkist;
using ZeepUtils.Text;
using ZeepUtils.Zeepkist;

namespace PlaylistVoting.Core.State.Setup;

public class PlaylistConflictState : SessionState
{
    private readonly List<LevelMetadata> _localLevels;
    private readonly List<LevelMetadata> _onlineLevels;
    private readonly ZeepkistPlaylistService _playlistService;
    private readonly PlaylistSyncService _syncService;

    public PlaylistConflictState(VotingController controller, PlaylistSessionInfo session, List<LevelMetadata> localLevels, List<LevelMetadata> onlineLevels) : base(controller, session)
    {
        _localLevels = localLevels;
        _onlineLevels = onlineLevels;
        _playlistService = new ZeepkistPlaylistService();
        _syncService = new PlaylistSyncService();
    }

    public override void OnEnter()
    {
        string localPreview = VotingDisplayManager.BuildPlaylistPreview("Local Playlist", _localLevels, "#3498db");
        string onlinePreview = VotingDisplayManager.BuildPlaylistPreview("Online Playlist", _onlineLevels, "#2ecc71");

        string msg = new RichText().Append("Local and online playlists differ!", b => b.Bold().Color("#FF0000")).Break().Append(localPreview).Break().Append(onlinePreview).Break().Append("Choose which one to use:", b => b.Italic().Color("#aaaaaa")).Break()
                                   .Append("/vote use-local", b => b.Color("#ff9900")).Break().Append("/vote use-online", b => b.Color("#ff9900")).Break().Append("/vote merge", b => b.Color("#ff9900")).Break()
                                   .Append("Existing votes will not be reset.", b => b.Size(80).Color("#aaa")).Build();

        ZeepkistNetworkHelper.SendLocalPrivateMessage(msg, ZeepkistNetworkHelper.CategoryConflict);
    }

    public override void OnUseLocalRequested()
    {
        ToastNotification.Info("Using local playlist...");
        List<LevelMetadata> levels = _syncService.Deduplicate(_localLevels);
        _ = FinishConflictAsync(levels, true);
    }

    public override void OnUseOnlineRequested()
    {
        ToastNotification.Info("Using online playlist...");
        List<LevelMetadata> levels = _syncService.Deduplicate(_onlineLevels);
        _ = FinishConflictAsync(levels, false);
    }

    public override void OnMergeRequested()
    {
        ToastNotification.Info("Merging local and online playlists...");
        List<LevelMetadata> levels = _syncService.Merge(_localLevels, _onlineLevels);
        _ = FinishConflictAsync(levels, true);
    }

    private async Task FinishConflictAsync(List<LevelMetadata> levels, bool upload)
    {
        try
        {
            if (upload)
            {
                Logger.Info("PlaylistConflictState: Uploading chosen playlist to backend...");
                await Controller.BackendService.UpdatePlaylistAsync(levels);
            }

            string playlistName = $"{Session.DisplayName}-toBeVoted";
            Logger.Info($"PlaylistConflictState: Saving playlist locally as '{playlistName}'");
            _playlistService.SavePlaylist(playlistName, levels);

            Logger.Info("PlaylistConflictState: Syncing playlist with Zeepkist lobby...");
            _playlistService.UpdateLobbyPlaylist(levels);

            string successMsg = new RichText().Append("Successfully initialized playlist!", b => b.Bold().Color("#00f8ad")).Break().Append($"{levels.Count} maps are now ready for voting.", b => b.Color("#dddddd")).Build();

            ZeepkistNetworkHelper.SendLocalPrivateMessage(successMsg);

            Controller.TransitionTo(new PlaylistVotingActiveState(Controller, Session, levels));
        }
        catch (Exception ex)
        {
            Logger.Error($"PlaylistConflictState: Error finishing conflict: {ex}");
            ToastNotification.Error("Conflict resolution failed.");
            ZeepkistNetworkHelper.SendLocalPrivateMessage("An error occurred during conflict resolution. Please try again or check logs.", ZeepkistNetworkHelper.CategoryConflict);
        }
    }
}