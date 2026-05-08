using System;
using PlaylistVoting.Core.Controllers;

namespace PlaylistVoting.Commands.Local;

public class VotePlaylistMode : BaseLocalVoteCommand
{
    public override string Prefix => "/";

    public override string Command => "vote playlistmode";

    public override string Description => "[Playlist Voting] Starts in Playlist Mode";

    protected override Action TriggerEvent => VotingController.Instance.OnPlaylistModeRequested;
}