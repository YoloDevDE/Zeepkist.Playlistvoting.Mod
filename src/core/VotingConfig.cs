using BepInEx.Configuration;
using ZeepkistNetworking;

namespace PlaylistVoting.core;

public interface IVotingConfig
{
    bool DeleteRejectedLevels { get; }
    int DeleteRejectedDelaySeconds { get; }
    string ServermessageTitle { get; }
    string WinEmote { get; }
    string TieEmote { get; }
    string LoseEmote { get; }
    string WebApiUrl { get; }
    string WebToken { get; }
}

public class VotingConfig : IVotingConfig
{
    private readonly ConfigEntry<int> _deleteRejectedDelaySeconds;
    private readonly ConfigEntry<bool> _deleteRejectedLevels;
    private readonly ConfigEntry<string> _loseEmote;
    private readonly ConfigEntry<string> _servermessageTitle;
    private readonly ConfigEntry<string> _tieEmote;
    private readonly ConfigEntry<string> _webApiUrl;
    private readonly ConfigEntry<string> _webToken;
    private readonly ConfigEntry<string> _winEmote;

    public VotingConfig(ConfigFile config)
    {
        _deleteRejectedLevels = config.Bind(
            "Settings", "Delete rejected levels?", false,
            "Should a map that lost the vote be deleted from the playlist?");

        _deleteRejectedDelaySeconds = config.Bind(
            "Settings", "Deletion delay (seconds)", 0,
            new ConfigDescription(
                "Extra delay before deleting a rejected level. Total delay = 3s + this value.",
                new AcceptableValueRange<int>(0, 300)));

        _servermessageTitle = config.Bind(
            "Settings", "Servermessage title", "Playlist-Voting",
            "Title shown in the server message overlay.");

        _webToken = config.Bind(
            "Web API", "WebToken", "XXXXX-XXXXX-XXX-XXX-XXX",
            "Your PlaylistVoting API token.");

        _webApiUrl = config.Bind(
            "Web API", "WebApiURL", "https://yololurk.herokuapp.com/api/ronan",
            "URL of the PlaylistVoting API.");

        AcceptableValueList<string> emoteChoices = new AcceptableValueList<string>(ZeepkistEmojis.GetEmojis().ToArray());

        _winEmote = config.Bind(
            "Emotes", "Win emote", ":yannicsmile:",
            new ConfigDescription("Emote shown when yes votes are leading.", emoteChoices));

        _tieEmote = config.Bind(
            "Emotes", "Tie emote", ":yannics:",
            new ConfigDescription("Emote shown when votes are tied.", emoteChoices));

        _loseEmote = config.Bind(
            "Emotes", "Lose emote", ":yannicmegas:",
            new ConfigDescription("Emote shown when no votes are leading.", emoteChoices));
    }

    public bool DeleteRejectedLevels => _deleteRejectedLevels.Value;
    public int DeleteRejectedDelaySeconds => _deleteRejectedDelaySeconds.Value;
    public string ServermessageTitle => _servermessageTitle.Value;
    public string WinEmote => _winEmote.Value;
    public string TieEmote => _tieEmote.Value;
    public string LoseEmote => _loseEmote.Value;
    public string WebApiUrl => _webApiUrl.Value;
    public string WebToken => _webToken.Value;
}