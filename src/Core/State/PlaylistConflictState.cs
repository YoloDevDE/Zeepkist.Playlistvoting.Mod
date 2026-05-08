using System.Collections.Generic;
using System.Threading.Tasks;
using PlaylistVoting.Core.Controllers;
using PlaylistVoting.Core.Models;
using PlaylistVoting.Core.State.Abstractions;
using PlaylistVoting.Infrastructure.Zeepkist;
using YoloDev.Text;
using YoloDev.Zeepkist;
using ZeepkistClient;

namespace PlaylistVoting.Core.State;

public class PlaylistConflictState : SessionState
{
    private readonly List<LevelMetadata> _localLevels;
    private readonly List<LevelMetadata> _onlineLevels;
    private readonly ZeepkistPlaylistService _playlistService;
    private readonly PlaylistSyncService _syncService;

    public PlaylistConflictState(
        VotingController controller,
        PlaylistSessionInfo session,
        List<LevelMetadata> localLevels,
        List<LevelMetadata> onlineLevels) : base(controller, session)
    {
        _localLevels = localLevels;
        _onlineLevels = onlineLevels;
        _playlistService = new ZeepkistPlaylistService(controller.Logger);
        _syncService = new PlaylistSyncService();
    }

    public override void OnEnter()
    {
        string msg = new TMPRichTextBuilder()
                     .AddLayer("Local and online playlists differ!", b => b.Bold().Color("#FF0000"))
                     .Break()
                     .AddLayer("/vote use-local", b => b.Color("#ff9900"))
                     .Break()
                     .AddLayer("/vote use-online", b => b.Color("#ff9900"))
                     .Break()
                     .AddLayer("/vote merge", b => b.Color("#ff9900"))
                     .Break()
                     .AddLayer("Existing votes will not be reset.", b => b.Size(80).Color("#aaa"))
                     .Build();

        if (ZeepkistNetwork.LocalPlayer != null)
        {
            MessageApi.SendPrivateCustomChatMessage(msg, "CONFLICT", ZeepkistNetwork.LocalPlayer.SteamID);
        }
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
        if (upload)
        {
            await Controller.BackendService.UpdatePlaylistAsync(levels);
        }

        string playlistName = $"{Session.DisplayName}-toBeVoted";
        _playlistService.SavePlaylist(playlistName, levels);

        Controller.TransitionTo(new PlaylistVotingActiveState(Controller, Session, levels));
    }
}