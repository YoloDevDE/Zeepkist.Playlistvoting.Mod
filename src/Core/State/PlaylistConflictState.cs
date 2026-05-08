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
    private readonly PlaylistFileService _playlistFileService;
    private readonly PlaylistSyncService _syncService;

    public PlaylistConflictState(
        VotingController controller,
        PlaylistSessionInfo session,
        List<LevelMetadata> localLevels,
        List<LevelMetadata> onlineLevels) : base(controller, session)
    {
        _localLevels = localLevels;
        _onlineLevels = onlineLevels;
        _playlistFileService = new PlaylistFileService(controller.Logger);
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
        List<LevelMetadata> levels = _syncService.Deduplicate(_localLevels);
        _ = FinishConflictAsync(levels);
    }

    public override void OnUseOnlineRequested()
    {
        List<LevelMetadata> levels = _syncService.Deduplicate(_onlineLevels);
        _ = FinishConflictAsync(levels);
    }

    public override void OnMergeRequested()
    {
        List<LevelMetadata> levels = _syncService.Merge(_localLevels, _onlineLevels);
        _ = FinishConflictAsync(levels);
    }

    private async Task FinishConflictAsync(List<LevelMetadata> levels)
    {
        string playlistName = $"{Session.DisplayName}-toBeVoted";
        _playlistFileService.SavePlaylist(playlistName, levels);

        // TODO: Update online playlist if use-local or merge was chosen?
        // The issue says: "Replace online playlist with local playlist"

        Controller.TransitionTo(new PlaylistVotingActiveState(Controller, Session, levels));
    }
}