namespace PlaylistVoting.commands.remote;

public class VoteNo : BaseVoteCommand
{
    public override string Prefix => "!";
    public override string Command => "n";
    public override string Description => "[Playlist Voting] Lets you vote <b>'No'</b>";
    protected override VotingType VotingType => VotingType.No;
}