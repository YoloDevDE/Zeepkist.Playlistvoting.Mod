using Newtonsoft.Json;

namespace PlaylistVoting.Core.Models;

public class ModSessionInfo
{
    [JsonProperty("id")] public string Id { get; set; }
    [JsonProperty("displayName")] public string DisplayName { get; set; }
    [JsonProperty("state")] public string State { get; set; }
}

public class PlaylistSessionInfo : ModSessionInfo
{
    [JsonProperty("playlistModeEnabled")] public bool PlaylistModeEnabled { get; set; }
    [JsonProperty("hasPlaylist")] public bool HasPlaylist { get; set; }
    [JsonProperty("currentLevelUid")] public string CurrentLevelUid { get; set; }
    [JsonProperty("remainingLevelCount")] public int RemainingLevelCount { get; set; }
}