using PlaylistVoting.Core.Models;

namespace PlaylistVoting.Commands.Remote;

public class VoteRemove : BaseVoteCommand
{
    public override string Prefix => "!";

    public override string Command => "r";

    public override string Description => "[Playlist Voting] Lets you remove your vote";

    protected override VotingType VotingType => VotingType.Remove;
}