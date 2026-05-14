using Newtonsoft.Json;

namespace PlaylistVoting.Data.Models;

public class PlaylistSessionInfo : ModSessionInfo
{
    [JsonProperty("playlistModeEnabled")] public bool PlaylistModeEnabled { get; set; }
    [JsonProperty("hasPlaylist")] public bool HasPlaylist { get; set; }
    [JsonProperty("currentLevelUid")] public string CurrentLevelUid { get; set; }
    [JsonProperty("remainingLevelCount")] public int RemainingLevelCount { get; set; }
}