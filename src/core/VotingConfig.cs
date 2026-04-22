using BepInEx.Configuration;
using ZeepkistNetworking;

namespace PlaylistVoting.core;

public class VotingConfig
{
    private readonly ConfigEntry<string> _loseEmote;
    private readonly ConfigEntry<string> _tieEmote;
    private readonly ConfigEntry<string> _webApiUrl;
    private readonly ConfigEntry<string> _webToken;
    private readonly ConfigEntry<string> _winEmote;

    private VotingConfig(ConfigFile config)
    {
        _webToken = config.Bind(
            "Web API", "WebToken", "XXXXX-XXXXX-XXX-XXX-XXX",
            "Your PlaylistVoting API token.");

        _webApiUrl = config.Bind(
            "Playlistvoting API", "Playlist-Voting-Api-URL", "http://localhost:8080/api/playlistvoting",
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

    public static VotingConfig Instance { get; private set; }

    public string WinEmote => _winEmote.Value;
    public string TieEmote => _tieEmote.Value;
    public string LoseEmote => _loseEmote.Value;
    public string WebApiUrl => _webApiUrl.Value;
    public string WebToken => _webToken.Value;

    public static void Init(ConfigFile config)
    {
        if (Instance != null)
        {
            return;
        }

        Instance = new VotingConfig(config);
    }
}