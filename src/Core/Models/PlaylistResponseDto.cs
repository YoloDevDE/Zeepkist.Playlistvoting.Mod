using System.Collections.Generic;
using Newtonsoft.Json;

namespace PlaylistVoting.Core.Models;

public class PlaylistResponseDto
{
    [JsonProperty("name")] public string Name { get; set; }
    [JsonProperty("amountOfLevels")] public int AmountOfLevels { get; set; }
    [JsonProperty("roundLength")] public float RoundLength { get; set; }
    [JsonProperty("shufflePlaylist")] public bool ShufflePlaylist { get; set; }
    [JsonProperty("levels")] public List<LevelMetadata> Levels { get; set; }
}