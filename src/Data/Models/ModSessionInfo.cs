using Newtonsoft.Json;

namespace PlaylistVoting.Data.Models;

public class ModSessionInfo
{
    [JsonProperty("id")] public string Id { get; set; }
    [JsonProperty("displayName")] public string DisplayName { get; set; }
    [JsonProperty("state")] public string State { get; set; }
}