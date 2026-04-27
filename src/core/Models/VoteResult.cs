using Newtonsoft.Json;

namespace PlaylistVoting.Core.Models;

public class VoteResult
{
    public VoteResult() { }

    public VoteResult(int yesVotes, int noVotes, int abstainVotes, int totalVotes = 0)
    {
        YesVotes = yesVotes;
        NoVotes = noVotes;
        AbstainVotes = abstainVotes;
        TotalVotes = totalVotes;
    }

    [JsonProperty("yes")] public int YesVotes { get; set; }

    [JsonProperty("no")] public int NoVotes { get; set; }

    [JsonProperty("abstain")] public int AbstainVotes { get; set; }

    [JsonProperty("total")] public int TotalVotes { get; set; }

    public bool IsYesWinning => YesVotes > NoVotes;
    public bool IsNoWinning => NoVotes > YesVotes;
    public bool IsTie => YesVotes == NoVotes;
}