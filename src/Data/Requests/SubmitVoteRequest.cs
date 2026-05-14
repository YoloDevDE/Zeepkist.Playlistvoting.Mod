using PlaylistVoting.Data.Enums;

namespace PlaylistVoting.Data.Requests;

public class SubmitVoteRequest
{
    public ulong SteamId { get; set; }
    public VotingType VotingType { get; set; }
    public string Platform { get; set; }
}