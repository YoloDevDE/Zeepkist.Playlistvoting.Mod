using BepInEx.Configuration;
using PlaylistVoting.Core.Models;

namespace PlaylistVoting.Core.Config;

public class VotingConfig
{
    private readonly ConfigEntry<string> _authToken;
    private readonly ConfigEntry<string> _authUserId;
    private readonly ConfigEntry<bool> _includeAbstainVotes;
    private readonly ConfigEntry<PlaylistVotingStartupMode> _startupMode;
    private readonly ConfigEntry<int> _voteReminderThreshold;
    private readonly ConfigEntry<string> _webApiUrl;

    private VotingConfig(ConfigFile config)
    {
        _webApiUrl = config.Bind(
            "Playlistvoting API", "Playlist-Voting-Api-URL", "http://localhost:8080/api/playlistvoting",
            "URL of the PlaylistVoting API.");

        _authToken = config.Bind(
            "Auth", "Auth Token", string.Empty,
            "The session token for the API. Do not share this.");

        _authUserId = config.Bind(
            "Auth", "Auth User ID", string.Empty,
            "The user ID for the API.");

        _voteReminderThreshold = config.Bind(
            "Voting", "Reminder Threshold", 30,
            "The number of seconds left in the round when the voting reminder is shown.");

        _includeAbstainVotes = config.Bind(
            "Voting", "Include Abstain Votes", true,
            "Whether to include abstain votes in the calculation (will be used by the backend).");

        _startupMode = config.Bind(
            "General", "Startup Mode", PlaylistVotingStartupMode.AlwaysAsk,
            "How the mod should behave when an active session is found.");
    }

    public static VotingConfig Instance { get; private set; }

    public string WebApiUrl => _webApiUrl.Value;

    public int VoteReminderThreshold => _voteReminderThreshold.Value;

    public bool IncludeAbstainVotes => _includeAbstainVotes.Value;

    public PlaylistVotingStartupMode StartupMode => _startupMode.Value;

    public string AuthToken
    {
        get => _authToken.Value;
        set => _authToken.Value = value;
    }

    public string AuthUserId
    {
        get => _authUserId.Value;
        set => _authUserId.Value = value;
    }

    public static void Init(ConfigFile config)
    {
        if (Instance != null)
        {
            return;
        }

        Instance = new VotingConfig(config);
    }
}