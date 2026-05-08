using PlaylistVoting.Core.Models;
using PlaylistVoting.Infrastructure.UI;

namespace PlaylistVoting.Infrastructure.Zeepkist;

public class ServerMessageService
{
    private readonly string _title;

    public ServerMessageService(string title)
    {
        _title = title;
    }

    public void UpdateDisplay(string sessionName, LevelMetadata level, VoteResult result, bool isConnected, int? remainingLevels = null)
    {
        string message = VotingDisplayManager.BuildVoteDisplayMessage(
            _title,
            sessionName,
            level,
            result,
            isConnected);

        if (remainingLevels.HasValue)
        {
            message += $"\n{remainingLevels.Value} levels remaining";
        }

        VotingDisplayManager.SendVotingUpdate(message);
    }
}