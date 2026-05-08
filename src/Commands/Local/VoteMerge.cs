using System;
using PlaylistVoting.Core.Controllers;

namespace PlaylistVoting.Commands.Local;

public class VoteMerge : BaseLocalVoteCommand
{
    public override string Prefix => "/";

    public override string Command => "vote merge";

    public override string Description => "[Playlist Voting] Merge local and online playlists";

    protected override Action TriggerEvent => VotingController.Instance.OnMergeRequested;
}