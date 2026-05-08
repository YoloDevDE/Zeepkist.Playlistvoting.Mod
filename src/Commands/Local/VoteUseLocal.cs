using System;
using PlaylistVoting.Core.Controllers;

namespace PlaylistVoting.Commands.Local;

public class VoteUseLocal : BaseLocalVoteCommand
{
    public override string Prefix => "/";

    public override string Command => "vote use-local";

    public override string Description => "[Playlist Voting] Use local playlist for sync";

    protected override Action TriggerEvent => VotingController.Instance.OnUseLocalRequested;
}