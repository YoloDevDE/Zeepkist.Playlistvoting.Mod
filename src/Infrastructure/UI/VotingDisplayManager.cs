using System;
using System.Linq;
using PlaylistVoting.Core.Models;
using YoloDev.Text;
using YoloDev.Zeepkist;

namespace PlaylistVoting.Infrastructure.UI;

public static class VotingDisplayManager
{
    private const int BarWidth = 50;
    private const string BangersFont = "Bangers SDF";
    private const string HighwayFont = "ELECTRONIC HIGHWAY SIGN SDF";

    public static string BuildVoteDisplayMessage(string title, string sessionName, LevelMetadata level,
        VoteResult result, bool isConnected) => new TMPRichTextBuilder()
                                                .AddLayer(BuildHeader(title, sessionName, isConnected))
                                                .Break()
                                                .AddLayer(BuildLevelInfo(level))
                                                .Break()
                                                .AddLayer(BuildVoteSummary(result))
                                                .Break()
                                                .AddLayer(BuildVoteBar(result))
                                                .AddLayer(BuildPlatformStats(result))
                                                .Break()
                                                .AddLayer(BuildHelpText())
                                                .Build();

    public static void SendVotingUpdate(string content)
    {
        string wrappedContent = new TMPRichTextBuilder(content)
                                .Align(TMPRichTextBuilder.AlignmentType.Left)
                                .Size(150)
                                .Build();

        MessageApi.SetServerMessage(wrappedContent);
    }

    private static string BuildHelpText()
    {
        return new TMPRichTextBuilder()
               .AddLayer("!y", b => b.Color("#777777")).AddLayer(" yes · ")
               .AddLayer("!n", b => b.Color("#777777")).AddLayer(" no · ")
               .AddLayer("!idk", b => b.Color("#777777")).AddLayer(" random · ")
               .AddLayer("!r", b => b.Color("#777777")).AddLayer(" remove")
               .Build();
    }

    private static string BuildHeader(string title, string sessionName, bool isConnected)
    {
        string displaySession = string.IsNullOrEmpty(sessionName) ? title : sessionName;

        string dotColor = DateTime.Now.Second % 2 == 0 ? "#f00" : "#00000000";
        string dot = new TMPRichTextBuilder("·").Color(dotColor).Build();

        string liveStatus;
        if (isConnected)
        {
            liveStatus = new TMPRichTextBuilder()
                         .AddLayer("  ")
                         .AddLayer(dot, b => b.Bold().Size(20, TMPRichTextBuilder.UnitType.Plus).VOffset(0))
                         .AddLayer(" LIVE", b => b.Bold().Color("#fff"))
                         .Build();
        }
        else
        {
            liveStatus = new TMPRichTextBuilder()
                         .AddLayer("  ")
                         .AddLayer(dot, b => b.Size(10, TMPRichTextBuilder.UnitType.Plus).VOffset(0))
                         .AddLayer(" OFFLINE")
                         .Color("#ff4444")
                         .Build();
        }

        return new TMPRichTextBuilder(displaySession)
               .Size(15, TMPRichTextBuilder.UnitType.Plus)
               .Bold()
               .Underline()
               .Font(BangersFont)
               .Color("#00f8ad")
               .AddLayer(liveStatus)
               .Build();
    }

    private static string BuildLevelInfo(LevelMetadata level)
    {
        string levelName = level?.Name ?? "Unknown";
        string levelAuthor = level?.Author ?? "Unknown";

        return new TMPRichTextBuilder()
               .AddLayer("Level ", b => b.Color("#aaa"))
               .AddLayer(levelName, b => b.Color("#ff9900").Bold())
               .Break()
               .AddLayer("by ", b => b.Size(85).Color("#aaa"))
               .AddLayer(levelAuthor, b => b.Size(85).Color("#99ff00"))
               .Build();
    }

    private static string BuildVoteSummary(VoteResult result)
    {
        int total = result.YesVotes + result.NoVotes;
        string emote = result.IsWin
            ? Emojis.YannicSmile
            : result.IsLose
                ? Emojis.OhNo
                : Emojis.YannicScared;

        int half = BarWidth / 2;
        int totalWidth = BarWidth + 2; // [ + dots + ]

        // emote centered over the bar's | marker: ([ + half dots + 0.5) * mspace
        float emotePos = (half + 0.5f) * 0.45f;

        // invisible Highway spacer establishes the bar width so overlays align correctly
        string spacer = new TMPRichTextBuilder(new string(' ', totalWidth))
                        .Font(HighwayFont)
                        .MSpace(0.45, TMPRichTextBuilder.UnitType.Em)
                        .Color("#00000000") // alpha=#00
                        .Build();

        if (total == 0)
        {
            return new TMPRichTextBuilder(spacer)
                   .AddLayer("–", b => b.Pos(0).Color("#555555"))
                   .AddLayer(emote, b => b.Pos(0).AddLayer($"<pos={emotePos:F3}em>"))
                   .AddLayer("–", b => b.Pos(0).AddLayer($"<pos={(totalWidth - 1) * 0.45f:F3}em>")
                                        .Color("#555555"))
                   .Build();
        }

        int yesPct = (int)Math.Round((double)result.YesVotes / total * 100);
        int noPct = 100 - yesPct;

        string yesText = new TMPRichTextBuilder($"✓{result.YesVotes} ({yesPct}%)")
                         .Color("#00cc44")
                         .Bold()
                         .Build();

        string noText = new TMPRichTextBuilder($"✗{result.NoVotes} ({noPct}%)")
                        .Color("#cc3333")
                        .Bold()
                        .Build();

        // noText right-aligned: estimate char width at ~0.45em in default font
        string noRaw = $"✗{result.NoVotes} ({noPct}%)";
        float noStartPos = (totalWidth - noRaw.Length) * 0.45f;

        return new TMPRichTextBuilder(spacer)
               .AddLayer(yesText, b => b.Pos(0))
               .AddLayer(emote, b => b.Pos(0).AddLayer($"<pos={emotePos:F3}em>"))
               .AddLayer(noText, b => b.Pos(0).AddLayer($"<pos={noStartPos:F3}em>"))
               .Build();
    }

    private static string BuildVoteBar(VoteResult result)
    {
        int total = result.YesVotes + result.NoVotes;
        int half = BarWidth / 2;

        TMPRichTextBuilder barBuilder = new TMPRichTextBuilder();

        if (total == 0)
        {
            barBuilder.AddLayer(new string('·', BarWidth), b => b.Color("#555555"));
        }
        else
        {
            int yesWidth = (int)Math.Round((double)result.YesVotes / total * BarWidth);
            int noWidth = BarWidth - yesWidth;
            barBuilder.AddLayer(new string('·', yesWidth), b => b.Color("#00aa00"))
                      .AddLayer(new string('·', noWidth), b => b.Color("#aa0000"));
        }

        string bar = barBuilder.Build();

        // [bar] with brackets
        string barLayer = new TMPRichTextBuilder()
                          .AddLayer("[", b => b.Color("#888888"))
                          .AddLayer(bar)
                          .AddLayer("]", b => b.Color("#888888"))
                          .Font(HighwayFont)
                          .MSpace(0.45, TMPRichTextBuilder.UnitType.Em)
                          .Build();

        // | centered in the gap between dot (half-1) and dot half
        // gap center = (half + 1) * 0.45em, |'s left edge = gap center - 0.225 = (half + 0.5) * 0.45em
        float midPos = (half + 1.5f) * 0.45f;
        string midLayer = new TMPRichTextBuilder()
                          .Pos(0)
                          .Font(HighwayFont)
                          .AddLayer("|", b => b.AddLayer($"<pos={midPos:F3}em>").Color("#aaaaaa"))
                          .Build();

        return barLayer + midLayer;
    }

    private static string BuildPlatformStats(VoteResult result)
    {
        if (result.Platforms == null || result.Platforms.Count == 0)
        {
            return "";
        }

        string platforms = string.Join(" | ", result.Platforms.Select(p => $"{p.Key}: {p.Value}"));
        return new TMPRichTextBuilder(platforms)
               .Break()
               .Size(80)
               .Build();
    }
}