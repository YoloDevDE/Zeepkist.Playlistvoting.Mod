using System;
using PlaylistVoting.Core.Models;
using ZeepUtils.Text;
using ZeepUtils.Zeepkist;

namespace PlaylistVoting.Infrastructure.Zeepkist;

public class VotingResultBroadcaster
{
    public void BroadcastResult(LevelMetadata level, VoteResult result)
    {
        string verdict = result.IsWin
            ? "PASSED"
            : result.IsLose
                ? "REJECTED"
                : "TIED (REJECTED)";

        string color = result.IsWin
            ? "#00FF00"
            : result.IsLose
                ? "#FF0000"
                : "#FFFF00";

        int total = result.YesVotes + result.NoVotes;
        int yesPct = total > 0 ? (int)Math.Round((double)result.YesVotes / total * 100) : 0;
        int noPct = total > 0 ? 100 - yesPct : 0;

        string message = new RichText().Append("Result for ").Append(level.Name, b => b.Bold().Color("#ff9900")).Append(": ").Append(verdict, b => b.Color(color).Bold()).Break()
                                       .Append($"  {Emojis.YannicSmile} {result.YesVotes} ({yesPct}%)", b => b.Color("#00cc44")).Append("  vs  ", b => b.Color("#888888")).Append($"{Emojis.OhNo} {result.NoVotes} ({noPct}%)", b => b.Color("#cc3333"))
                                       .Build();

        MessageApi.SendBroadcastCustomChatMessageTo(message, ZeepkistNetworkHelper.CategoryVoting);
    }
}