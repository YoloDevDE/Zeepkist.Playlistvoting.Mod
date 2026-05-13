using System;

namespace PlaylistVoting.Core.Models;

public enum RequestType
{
    FinalizeLevel
    , SetCurrentLevel
    , UpdateTimer
    , ResetVotesForLevel
    , SetPlaylistMode
    , UpdatePlaylist
    , SubmitVote
}

public class PendingRequest
{
    public RequestType Type { get; set; }
    public object Data { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int RetryCount { get; set; }
}

public class SubmitVoteRequest
{
    public ulong SteamId { get; set; }
    public VotingType VotingType { get; set; }
    public string Platform { get; set; }
}

public class SetCurrentLevelRequest
{
    public LevelMetadata Level { get; set; }
    public bool IncludeAbstain { get; set; }
}