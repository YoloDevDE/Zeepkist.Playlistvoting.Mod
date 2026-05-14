using Lombok.NET;
using Newtonsoft.Json;
using PlaylistVoting.Data.Models;

namespace PlaylistVoting.Data.DTOs;

[NoArgsConstructor]
[AllArgsConstructor]
public partial class VotingResultResponse
{
    [JsonProperty("sessionName")] public string SessionName { get; set; }

    [JsonProperty("sessionState")] public string SessionState { get; set; }

    [JsonProperty("level")] public LevelMetadata Level { get; set; }

    [JsonProperty("votes")] public VoteResult Votes { get; set; }
}