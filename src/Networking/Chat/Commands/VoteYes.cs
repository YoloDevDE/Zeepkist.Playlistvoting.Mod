using PlaylistVoting.Data.Enums;
using ZeepUtils.Text;

namespace PlaylistVoting.Networking.Chat.Commands;

public class VoteYes : BaseVoteCommand
{
    public override string Prefix => "!";

    public override string Command => "y";

    public override string Description => new RichText("[Playlist Voting] Lets you vote ").Append("'Yes'", b => b.Bold()).Build();

    protected override VotingType VotingType => VotingType.Yes;
}