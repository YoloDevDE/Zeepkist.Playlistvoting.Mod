namespace PlaylistVoting.api;

public record VoteResult(int YesVotes, int NoVotes, int AbstainVotes)
{
    public bool IsYesWinning => YesVotes > NoVotes;
    public bool IsNoWinning => NoVotes > YesVotes;
    public bool IsTie => YesVotes == NoVotes;
    public int YesVotes { get; } = YesVotes;
    public int NoVotes { get; } = NoVotes;
    public int AbstainVotes { get; } = AbstainVotes;
}