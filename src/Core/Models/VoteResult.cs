using System.Collections.Generic;
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

    [JsonProperty("platforms")] public Dictionary<string, int> Platforms { get; set; } = new Dictionary<string, int>();

    public bool IsWin => YesVotes > NoVotes;

    public bool IsLose => NoVotes > YesVotes;
    public bool IsTie => NoVotes == YesVotes;
}