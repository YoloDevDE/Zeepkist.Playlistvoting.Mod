using System;
using PlaylistVoting.Management;

namespace PlaylistVoting.Networking.Chat.Commands;

public class VoteMerge : BaseLocalVoteCommand
{
    public override string Prefix => "/";

    public override string Command => "vote merge";

    public override string Description => "[Playlist Voting] Merge local and online playlists";

    protected override Action TriggerEvent => VotingController.Instance.OnMergeRequested;
}