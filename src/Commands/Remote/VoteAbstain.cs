using PlaylistVoting.Core.Models;
using ZeepUtils.Text;

namespace PlaylistVoting.Commands.Remote;

public class VoteAbstain : BaseVoteCommand
{
    public override string Prefix => "!";

    public override string Command => "a";

    public override string Description => new RichText("[Playlist Voting] Lets you vote ").Append("'Abstain'", b => b.Bold()).Build();

    protected override VotingType VotingType => VotingType.Abstain;
}