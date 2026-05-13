using PlaylistVoting.Core.Models;
using ZeepUtils.Text;

namespace PlaylistVoting.Commands.Remote;

public class VoteYes : BaseVoteCommand
{
    public override string Prefix => "!";

    public override string Command => "y";

    public override string Description => new RichText("[Playlist Voting] Lets you vote ").Append("'Yes'", b => b.Bold()).Build();

    protected override VotingType VotingType => VotingType.Yes;
}