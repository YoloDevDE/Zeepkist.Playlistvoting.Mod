using Newtonsoft.Json;

namespace PlaylistVoting.Core.Models;

public class TimerUpdateDto
{
    [JsonProperty("roundTime")] public double RoundTime { get; set; }

    [JsonProperty("levelLoadedAtTime")] public double LevelLoadedAtTime { get; set; }

    [JsonProperty("currentTime")] public double CurrentTime { get; set; }

    [JsonProperty("gameState")] public int GameState { get; set; }
}