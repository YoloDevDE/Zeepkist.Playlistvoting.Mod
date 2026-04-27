using PlaylistVoting.Core.Models;

namespace PlaylistVoting.Commands.Remote;

public class VoteYes : BaseVoteCommand
{
    public override string Prefix => "!";
    public override string Command => "y";
    public override string Description => "[Playlist Voting] Lets you vote <b>'Yes'</b>";
    protected override VotingType VotingType => VotingType.Yes;
}