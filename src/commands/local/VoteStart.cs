using System;
using PlaylistVoting.Core.Controllers;

namespace PlaylistVoting.Commands.Local;

public class VoteStart : BaseLocalVoteCommand
{
    public override string Prefix => "/";
    public override string Command => "vote start";
    public override string Description => "[Playlist Voting] Starts the Mod";
    protected override Action TriggerEvent => VotingController.Instance.OnVoteStartRequested;
}