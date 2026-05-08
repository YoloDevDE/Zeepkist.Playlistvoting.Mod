using PlaylistVoting.Core.Models;
using YoloDev.Text;

namespace PlaylistVoting.Commands.Remote;

public class VoteAbstain : BaseVoteCommand
{
    public override string Prefix => "!";

    public override string Command => "a";

    public override string Description => new TMPRichTextBuilder("[Playlist Voting] Lets you vote ").AddLayer("'Abstain'", b => b.Bold()).Build();

    protected override VotingType VotingType => VotingType.Abstain;
}