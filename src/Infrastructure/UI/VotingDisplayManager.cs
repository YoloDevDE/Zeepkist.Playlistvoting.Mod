using System;
using System.Collections.Generic;
using PlaylistVoting.Core.Config;
using PlaylistVoting.Core.Models;
using ZeepUtils.Text;
using ZeepUtils.Zeepkist;

namespace PlaylistVoting.Infrastructure.UI;

public static class VotingDisplayManager
{
    private const int BarWidth = 50;
    private const RichText.FontType BangersFont = RichText.FontType.Anton;
    private const RichText.FontType HighwayFont = RichText.FontType.ElectronicHighwaySign;

    private static readonly Dictionary<string, string> PlatformColors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "twitch", "#9146FF" }, { "youtube", "#FF0000" }, { "yt", "#FF0000" }, { "kick", "#53FC18" }, { "discord", "#5865F2" }, { "zeepkist", "#00f8ad" }
    };

    public static string BuildSettingsSummary(VotingConfig config)
    {
        return new RichText().Append("CURRENT SETTINGS", b => b.Bold().Font(BangersFont).Color("#00f8ad")).Break().Append("Startup Mode: ", b => b.Color("#aaaaaa")).Append(config.StartupMode.ToString(), b => b.Color("#ffffff")).Break()
                             .Append("Reminder Threshold: ", b => b.Color("#aaaaaa")).Append($"{config.VoteReminderThreshold}s", b => b.Color("#ffffff")).Break().Append("Include Abstain: ", b => b.Color("#aaaaaa"))
                             .Append(config.IncludeAbstainVotes ? "Yes" : "No", b => b.Color("#ffffff")).Build();
    }

    public static string BuildVoteDisplayMessage(string title, string sessionName, LevelMetadata level, VoteResult result, bool isConnected)
    {
        RichText builder = new RichText().Append(BuildHeader(title, sessionName, isConnected));

        builder.Break().Append(BuildLevelInfo(level)).Break();

        if (result != null)
        {
            builder.Append(BuildVoteCountsRow(result)).Break().Append(BuildVoteBar(result)).Break().Append(BuildPercentageRow(result)).Break().Append(BuildPlatformStats(result));
        }

        builder.Append(BuildHelpText());


        return builder.Build();
    }

    public static void SendVotingUpdate(string content)
    {
        MessageApi.SetServerMessage($"<align=left><size=150%>{content}</size></align>");
    }

    public static void ClearDisplay()
    {
        MessageApi.SetServerMessage(string.Empty);
    }

    public static string BuildPlaylistPreview(string title, List<LevelMetadata> levels, string color = "#00f8ad")
    {
        RichText builder = new RichText().Append(title, b => b.Bold().Color(color)).Append($" ({levels.Count} maps)", b => b.Size(80).Italic().Color("#aaaaaa")).Break();

        int previewCount = Math.Min(levels.Count, 3);

        for (int i = 0; i < previewCount; i++)
        {
            LevelMetadata level = levels[i];
            builder.Append($"{i + 1}. ", b => b.Size(70).Color("#888888")).Append(level.Name, b => b.Size(80).Color("#dddddd"));

            if (!string.IsNullOrEmpty(level.Author))
            {
                builder.Append($" ({level.Author})", b => b.Size(70).Color("#999999"));
            }

            builder.Break();
        }

        if (levels.Count > previewCount)
        {
            builder.Append($"... and {levels.Count - previewCount} more", b => b.Size(70).Italic().Color("#888888")).Break();
        }

        return builder.Build();
    }

    private static string BuildHelpText()
    {
        return new RichText().Break().Append("COMMANDS", b => b.Bold().Font(BangersFont).Color("#00f8ad")).Break().Append("type those commands in chat", b => b.Size(80).Color("#aaaaaa")).Break().Append("!y", b => b.Color("#777777")).Append(" yes · ")
                             .Append("!n", b => b.Color("#777777")).Append(" no · ").Append("!idk", b => b.Color("#777777")).Append(" random · ").Append("!r", b => b.Color("#777777")).Append(" remove").Build();
    }

    private static string BuildHeader(string title, string sessionName, bool isConnected)
    {
        string displaySession = string.IsNullOrEmpty(sessionName) ? title : sessionName;

        string dotColor = DateTime.Now.Second % 2 == 0 ? "#f00" : "#00000000";
        string dot = new RichText("·").Color(dotColor).Build();

        string liveStatus;

        if (isConnected)
        {
            liveStatus = new RichText().Append("  ").Append(dot, b => b.Bold().Size(20, RichText.UnitType.Plus)).Append(" LIVE", b => b.Bold().Color("#fff")).Build();
        }
        else
        {
            liveStatus = new RichText().Append("  ").Append(dot, b => b.Size(10, RichText.UnitType.Plus)).Append(" OFFLINE").Color("#ff4444").Build();
        }

        return new RichText(displaySession).Size(15, RichText.UnitType.Plus).Bold().Underline().Font(BangersFont).Color("#00f8ad").Append(liveStatus).Build();
    }

    private static string BuildLevelInfo(LevelMetadata level)
    {
        string levelName = level?.Name ?? "Unknown";
        string levelAuthor = level?.Author ?? "Unknown";

        return new RichText().Append("Level ", b => b.Color("#aaa")).Append(levelName, b => b.Color("#ff9900").Bold()).Break().Append("by ", b => b.Size(85).Color("#aaa")).Append(levelAuthor, b => b.Size(85).Color("#99ff00")).Build();
    }

    private static string BuildVoteCountsRow(VoteResult result)
    {
        int total = result.YesVotes + result.NoVotes;
        string emote = result.IsWin
            ? Emojis.YannicSmile
            : result.IsLose
                ? Emojis.OhNo
                : Emojis.YannicScared;

        int totalWidth = BarWidth + 2; // [ + dots + ]
        float barWidthEm = totalWidth * 0.45f;
        float centerPos = barWidthEm / 2.0f;
        float emotePos = centerPos - 0.225f;

        // invisible Highway spacer establishes the bar width so overlays align correctly
        string spacer = new RichText(new string(' ', totalWidth)).Font(HighwayFont).MSpace(0.45, RichText.UnitType.Em).Color("#00000000").Build();

        string yesCount = total == 0 ? new RichText("–").Color("#555555").Bold().Build() : new RichText($"{result.YesVotes:D2}").Color("#00cc44").Bold().Build();

        string noCount = total == 0 ? new RichText("–").Color("#555555").Bold().Build() : new RichText($"{result.NoVotes:D2}").Color("#cc3333").Bold().Build();

        // Right-align YES at the right end of the bar
        string yesRaw = total == 0 ? "–" : $"{result.YesVotes:D2}";
        float yesStartPos = barWidthEm - yesRaw.Length * 0.45f;

        // Right-align NO at the left side: NO text ends before the bar starts
        string noRaw = total == 0 ? "–" : $"{result.NoVotes:D2}";
        float noEndPos = noRaw.Length * 0.45f;

        return new RichText(spacer).Append(noCount, b => b.Pos(0)).Append(emote, b => b.Pos(0).Append($"<pos={emotePos:F3}em>")).Append(yesCount, b => b.Pos(0).Append($"<pos={yesStartPos:F3}em>")).Build();
    }

    private static string BuildPercentageRow(VoteResult result)
    {
        int total = result.YesVotes + result.NoVotes;
        int totalWidth = BarWidth + 2;
        float barWidthEm = totalWidth * 0.45f;

        string spacer = new RichText(new string(' ', totalWidth)).Font(HighwayFont).MSpace(0.45, RichText.UnitType.Em).Color("#00000000").Build();

        if (total == 0)
        {
            return new RichText(spacer).Append("0%", b => b.Pos(0).Size(80).Color("#555555")).Append("0%", b => b.Pos(0).Append($"<pos={barWidthEm - 0.9f:F3}em>").Size(80).Color("#555555")).Build();
        }

        int yesPct = (int)Math.Round((double)result.YesVotes / total * 100);
        int noPct = 100 - yesPct;

        string noPctText = new RichText($"{noPct}%").Size(80).Color("#cc3333").Build();
        string yesPctText = new RichText($"{yesPct}%").Size(80).Color("#00cc44").Build();

        // Right-align YES% at the right end of the bar
        string yesPctRaw = $"{yesPct}%";
        float yesStartPos = barWidthEm - yesPctRaw.Length * 0.45f;

        return new RichText(spacer).Append(noPctText, b => b.Pos(0)).Append(yesPctText, b => b.Pos(0).Append($"<pos={yesStartPos:F3}em>")).Build();
    }

    private static string BuildVoteBar(VoteResult result)
    {
        int total = result.YesVotes + result.NoVotes;
        int half = BarWidth / 2;
        int totalWidth = BarWidth + 2;
        float barWidthEm = totalWidth * 0.45f;

        RichText barBuilder = new RichText();

        if (total == 0)
        {
            barBuilder.Append(new string('·', BarWidth), b => b.Color("#555555"));
        }
        else
        {
            int yesWidth = (int)Math.Round((double)result.YesVotes / total * BarWidth);
            int noWidth = BarWidth - yesWidth;
            barBuilder.Append(new string('·', yesWidth), b => b.Color("#00aa00")).Append(new string('·', noWidth), b => b.Color("#aa0000"));
        }

        string bar = barBuilder.Build();

        // [bar] with brackets
        string barLayer = new RichText().Append("[", b => b.Color("#888888")).Append(bar).Append("]", b => b.Color("#888888")).Font(HighwayFont).MSpace(0.45, RichText.UnitType.Em).Build();

        // | centered: middle of the bar
        float midPos = barWidthEm / 2.0f;
        string midLayer = new RichText().Font(HighwayFont).Append("|", b => b.Append($"<pos={midPos:F3}em>").Color("#aaaaaa")).Build();

        return barLayer + midLayer;
    }

    private static string GetPlatformColor(string platform) => PlatformColors.TryGetValue(platform, out string color) ? color : "#aaaaaa";

    private static string BuildPlatformStats(VoteResult result)
    {
        if (result == null || result.Platforms == null || result.Platforms.Count == 0)
        {
            return "";
        }

        RichText builder = new RichText();
        bool first = true;

        foreach (KeyValuePair<string, int> p in result.Platforms)
        {
            if (!first)
            {
                builder.Append("  ·  ", b => b.Size(80).Color("#666666"));
            }

            first = false;

            string platformColor = GetPlatformColor(p.Key);
            builder.Append(p.Key.ToUpper(), b => b.Size(80).Bold().Color(platformColor)).Append($": {p.Value}", b => b.Size(80).Color("#aaaaaa"));
        }

        return builder.Build();
    }
}