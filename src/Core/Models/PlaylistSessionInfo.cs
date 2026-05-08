using Newtonsoft.Json;

namespace PlaylistVoting.Core.Models;

public class PlaylistSessionInfo
{
    [JsonProperty("id")] public string Id { get; set; }
    [JsonProperty("displayName")] public string DisplayName { get; set; }
    [JsonProperty("state")] public string State { get; set; }
    [JsonProperty("playlistModeEnabled")] public bool PlaylistModeEnabled { get; set; }
    [JsonProperty("hasPlaylist")] public bool HasPlaylist { get; set; }
    [JsonProperty("currentLevel")] public LevelMetadata CurrentLevel { get; set; }
    [JsonProperty("remainingLevels")] public int RemainingLevels { get; set; }
}