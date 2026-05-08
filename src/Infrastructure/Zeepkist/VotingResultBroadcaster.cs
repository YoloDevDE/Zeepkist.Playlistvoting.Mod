using PlaylistVoting.Core.Models;
using YoloDev.Text;
using YoloDev.Zeepkist;

namespace PlaylistVoting.Infrastructure.Zeepkist;

public class VotingResultBroadcaster
{
    public void BroadcastResult(LevelMetadata level, VoteResult result)
    {
        string verdict = result.IsWin
            ? "YES"
            : result.IsLose
                ? "NO"
                : "ABSTAIN";
        string color = result.IsWin
            ? "#00FF00"
            : result.IsLose
                ? "#FF0000"
                : "#FFFF00";

        string message = new TMPRichTextBuilder()
                         .AddLayer("Voting result for ")
                         .AddLayer(level.Name, b => b.Bold())
                         .AddLayer(": ")
                         .AddLayer(verdict, b => b.Color(color).Bold())
                         .Build();

        MessageApi.SendBroadcastCustomChatMessageTo(message, "VOTING");
    }
}