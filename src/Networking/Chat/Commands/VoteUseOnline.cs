using System;
using PlaylistVoting.Management;

namespace PlaylistVoting.Networking.Chat.Commands;

public class VoteUseOnline : BaseLocalVoteCommand
{
    public override string Prefix => "/";

    public override string Command => "vote use-online";

    public override string Description => "[Playlist Voting] Use online playlist for sync";

    protected override Action TriggerEvent => VotingController.Instance.OnUseOnlineRequested;
}