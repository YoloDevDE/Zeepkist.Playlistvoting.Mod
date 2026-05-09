using System;
using PlaylistVoting.Core.Models;
using YoloDev.Text;
using YoloDev.Zeepkist;

namespace PlaylistVoting.Infrastructure.Zeepkist;

public class VotingResultBroadcaster
{
    public void BroadcastResult(LevelMetadata level, VoteResult result)
    {
        string verdict = result.IsWin
            ? "PASSED"
            : result.IsLose
                ? "REJECTED"
                : "TIED / ABSTAINED";

        string color = result.IsWin
            ? "#00FF00"
            : result.IsLose
                ? "#FF0000"
                : "#FFFF00";

        int total = result.YesVotes + result.NoVotes;
        int yesPct = total > 0 ? (int)Math.Round((double)result.YesVotes / total * 100) : 0;
        int noPct = total > 0 ? 100 - yesPct : 0;

        string message = new TMPRichTextBuilder()
                         .AddLayer("Result for ")
                         .AddLayer(level.Name, b => b.Bold().Color("#ff9900"))
                         .AddLayer(": ")
                         .AddLayer(verdict, b => b.Color(color).Bold())
                         .Break()
                         .AddLayer($"  {Emojis.YannicSmile} {result.YesVotes} ({yesPct}%)", b => b.Color("#00cc44"))
                         .AddLayer("  vs  ", b => b.Color("#888888"))
                         .AddLayer($"{Emojis.OhNo} {result.NoVotes} ({noPct}%)", b => b.Color("#cc3333"))
                         .Build();

        MessageApi.SendBroadcastCustomChatMessageTo(message, "VOTING");
    }
}