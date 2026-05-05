using PlaylistVoting.Core.Models;

namespace PlaylistVoting.Commands.Remote;

public class VoteIdk : BaseVoteCommand
{
    public override string Prefix => "!";

    public override string Command => "idk";

    public override string Description => "[Playlist Voting] Ramdomizes your vote";

    protected override VotingType VotingType => VotingType.Idk;
}