using System;
using PlaylistVoting.Core.Controllers;

namespace PlaylistVoting.Commands.Local;

public class VoteStop : BaseLocalVoteCommand
{
    public override string Prefix => "/";

    public override string Command => "vote stop";

    public override string Description => "[Playlist Voting] Stops the Mod";

    protected override Action TriggerEvent => VotingController.Instance.OnVoteStopRequested;
}