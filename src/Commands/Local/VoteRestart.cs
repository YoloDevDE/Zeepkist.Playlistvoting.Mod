using System;
using PlaylistVoting.Core.Controllers;

namespace PlaylistVoting.Commands.Local;

public class VoteRestart : BaseLocalVoteCommand
{
    public override string Prefix => "/";

    public override string Command => "vote restart";

    public override string Description => "[Playlist Voting] Restarts the Mod/Voting";

    protected override Action TriggerEvent => VotingController.Instance.OnVoteRestartRequested;
}