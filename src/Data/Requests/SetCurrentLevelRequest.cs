using PlaylistVoting.Data.Models;

namespace PlaylistVoting.Data.Requests;

public class SetCurrentLevelRequest
{
    public LevelMetadata Level { get; set; }
    public bool IncludeAbstain { get; set; }
}