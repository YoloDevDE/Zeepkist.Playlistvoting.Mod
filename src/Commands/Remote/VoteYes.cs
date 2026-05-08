using PlaylistVoting.Core.Models;
using YoloDev.Text;

namespace PlaylistVoting.Commands.Remote;

public class VoteYes : BaseVoteCommand
{
    public override string Prefix => "!";

    public override string Command => "y";

    public override string Description => new TMPRichTextBuilder("[Playlist Voting] Lets you vote ").AddLayer("'Yes'", b => b.Bold()).Build();

    protected override VotingType VotingType => VotingType.Yes;
}