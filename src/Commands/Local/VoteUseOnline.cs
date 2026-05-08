using System;
using PlaylistVoting.Core.Controllers;

namespace PlaylistVoting.Commands.Local;

public class VoteUseOnline : BaseLocalVoteCommand
{
    public override string Prefix => "/";

    public override string Command => "vote use-online";

    public override string Description => "[Playlist Voting] Use online playlist for sync";

    protected override Action TriggerEvent => VotingController.Instance.OnUseOnlineRequested;
}