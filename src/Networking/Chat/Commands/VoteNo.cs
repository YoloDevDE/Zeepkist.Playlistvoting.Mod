using PlaylistVoting.Data.Enums;
using ZeepUtils.Text;

namespace PlaylistVoting.Networking.Chat.Commands;

public class VoteNo : BaseVoteCommand
{
    public override string Prefix => "!";

    public override string Command => "n";

    public override string Description => new RichText("[Playlist Voting] Lets you vote ").Append("'No'", b => b.Bold()).Build();

    protected override VotingType VotingType => VotingType.No;
}