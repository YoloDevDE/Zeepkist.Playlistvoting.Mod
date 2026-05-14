using PlaylistVoting.Data.Enums;

namespace PlaylistVoting.Networking.Chat.Commands;

public class VoteIdk : BaseVoteCommand
{
    public override string Prefix => "!";

    public override string Command => "idk";

    public override string Description => "[Playlist Voting] Ramdomizes your vote";

    protected override VotingType VotingType => VotingType.Idk;
}