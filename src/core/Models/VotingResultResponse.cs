using Newtonsoft.Json;

namespace PlaylistVoting.Core.Models;

public class VotingResultResponse
{
    [JsonProperty("sessionName")] public string SessionName { get; set; }

    [JsonProperty("sessionState")] public string SessionState { get; set; }

    [JsonProperty("level")] public LevelResponse Level { get; set; }

    [JsonProperty("votes")] public VoteResult Votes { get; set; }
}

public class LevelResponse
{
    [JsonProperty("levelUid")] public string LevelUid { get; set; }

    [JsonProperty("levelName")] public string LevelName { get; set; }

    [JsonProperty("levelAuthor")] public string LevelAuthor { get; set; }

    [JsonProperty("workshopId")] public ulong? WorkshopId { get; set; }
}