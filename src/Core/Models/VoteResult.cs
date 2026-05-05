using Lombok.NET;
using Newtonsoft.Json;

namespace PlaylistVoting.Core.Models;

[NoArgsConstructor]
[AllArgsConstructor]
public partial class VoteResult
{
    [JsonProperty("yes")] public int YesVotes { get; set; }

    [JsonProperty("no")] public int NoVotes { get; set; }

    [JsonProperty("abstain")] public int AbstainVotes { get; set; }

    [JsonProperty("total")] public int TotalVotes { get; set; }

    public bool IsYesWinning => YesVotes > NoVotes;

    public bool IsNoWinning => NoVotes > YesVotes;
}