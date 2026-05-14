using System;
using PlaylistVoting.Data.Enums;

namespace PlaylistVoting.Data.Requests;

public class PendingRequest
{
    public RequestType Type { get; set; }
    public object Data { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int RetryCount { get; set; }
}