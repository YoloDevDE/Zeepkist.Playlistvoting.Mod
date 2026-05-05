using PlaylistVoting.Core.Models;

namespace PlaylistVoting.Commands.Remote;

public class VoteAbstain : BaseVoteCommand
{
    public override string Prefix => "!";

    public override string Command => "a";

    public override string Description => "[Playlist Voting] Lets you vote <b>'Abstain'</b>";

    protected override VotingType VotingType => VotingType.Abstain;
}