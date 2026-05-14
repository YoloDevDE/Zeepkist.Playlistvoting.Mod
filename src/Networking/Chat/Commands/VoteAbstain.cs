using PlaylistVoting.Data.Enums;
using ZeepUtils.Text;

namespace PlaylistVoting.Networking.Chat.Commands;

public class VoteAbstain : BaseVoteCommand
{
    public override string Prefix => "!";

    public override string Command => "a";

    public override string Description => new RichText("[Playlist Voting] Lets you vote ").Append("'Abstain'", b => b.Bold()).Build();

    protected override VotingType VotingType => VotingType.Abstain;
}