using PlaylistVoting.Core.Models;
using YoloDev.Text;

namespace PlaylistVoting.Commands.Remote;

public class VoteNo : BaseVoteCommand
{
    public override string Prefix => "!";

    public override string Command => "n";

    public override string Description => new TMPRichTextBuilder("[Playlist Voting] Lets you vote ").AddLayer("'No'", b => b.Bold()).Build();

    protected override VotingType VotingType => VotingType.No;
}