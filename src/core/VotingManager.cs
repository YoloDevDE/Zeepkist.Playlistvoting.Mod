using System;
using System.Threading.Tasks;
using BepInEx.Configuration;
using BepInEx.Logging;
using PlaylistVoting.api;
using PlaylistVoting.commands;
using PlaylistVoting.misc;
using PlaylistVoting.states;
using ZeepkistClient;
using ZeepkistNetworking;
using ZeepSDK.ChatCommands;

namespace PlaylistVoting.core;

/// <summary>
///     Central manager that owns configuration, state machine, and API client.
///     Acts as the application context — wired up once by <see cref="Plugin" />.
/// </summary>
public class VotingManager : IDisposable
{
    // ── Private fields ───────────────────────────────────────────────────────
    private readonly ConfigFile _config;
    private readonly ManualLogSource _logger;
    private State _currentState;
    private ConfigEntry<int> _deleteRejectedDelaySeconds;

    private ConfigEntry<bool> _deleteRejectedLevels;
    private GamePhaseListener _gamePhaseListener;
    private ConfigEntry<string> _loseEmote;

    private ConfigEntry<string> _servermessageTitle;
    private ConfigEntry<string> _tieEmote;
    private ConfigEntry<string> _webApiUrl;
    private ConfigEntry<string> _webToken;
    private ConfigEntry<string> _winEmote;

    public VotingManager(ConfigFile config, ManualLogSource logger)
    {
        _config = config;
        _logger = logger;
        Instance = this;
    }

    // ── Public state ─────────────────────────────────────────────────────────
    public string CurrentLevelName { get; set; } = string.Empty;
    public string CurrentLevelAuthor { get; set; } = string.Empty;
    public string CurrentLevelUid { get; set; } = string.Empty;

    // ── Configuration properties ─────────────────────────────────────────────
    public bool DeleteRejectedLevels => _deleteRejectedLevels.Value;
    public int DeleteRejectedDelaySeconds => Math.Max(0, _deleteRejectedDelaySeconds.Value);
    public string ServermessageTitle => _servermessageTitle.Value;
    public string WinEmote => _winEmote.Value;
    public string TieEmote => _tieEmote.Value;
    public string LoseEmote => _loseEmote.Value;

    public string WebApiUrl => _webApiUrl.Value;

    // ── Dependencies ─────────────────────────────────────────────────────────
    public VotingApiClient VotingApiClient { get; private set; }

    public static VotingManager Instance { get; private set; }

    public ulong CurrentLevelWorkshopID { get; set; }

    public GamePhase CurrentPhase => _gamePhaseListener?.CurrentPhase ?? GamePhase.Unknown;

    public void Dispose()
    {
        _currentState?.Exit();
        _gamePhaseListener?.Dispose();
    }

    public async Task InitializeAsync()
    {
        BindConfiguration();
        RegisterChatCommands();

        VotingEventBus.Hub = new VotingEventHub();
        _gamePhaseListener = new GamePhaseListener();
        VotingApiClient = new VotingApiClient(() => _webToken.Value, () => _webApiUrl.Value);

        await LoadInitialLevelDataAsync();

        SwitchState(new StateInactive(this));
    }

    // ── State machine ─────────────────────────────────────────────────────────

    public void SwitchState(State newState)
    {
        _currentState?.Exit();
        _currentState = newState;
        _currentState.Enter();
    }

    // ── Vote reset ────────────────────────────────────────────────────────────

    public async Task ResetVotesAsync(bool printResults = true)
    {
        try
        {
            string resetResult = await VotingApiClient.ResetVotesAsync();

            if (printResults)
            {
                ZeepkistNetwork.SendCustomChatMessage(
                    true, 0,
                    $"<#f0f0f0>{resetResult}<br>----------------</color>",
                    ServermessageTitle);
            }

            CurrentLevelName = PlayerManager.Instance.currentMaster.GlobalLevel.Name;
            CurrentLevelAuthor = PlayerManager.Instance.currentMaster.GlobalLevel.Author;
            CurrentLevelUid = ZeepkistNetwork.CurrentLobby.LevelUID;
            CurrentLevelWorkshopID = ZeepkistNetwork.CurrentLobby.WorkshopID;

            bool success = await VotingApiClient.SetMapAsync(CurrentLevelUid, CurrentLevelName, CurrentLevelAuthor, CurrentLevelWorkshopID.ToString());
            if (!success)
            {
                ZeepkistNetwork.SendCustomChatMessage(
                    true, 0,
                    "<#ff6b6b>Error: Failed to update map data on the server. Please check your API token and connection.</color>",
                    ServermessageTitle);
            }
        }
        catch (Exception ex)
        {
            ZeepkistNetwork.SendCustomChatMessage(
                true, 0,
                $"<#ff6b6b>Error during vote reset: {ex.Message}<br>Please try again or contact an administrator.</color>",
                ServermessageTitle);
        }
    }

    // ── Private setup ─────────────────────────────────────────────────────────

    private void BindConfiguration()
    {
        _deleteRejectedLevels = _config.Bind(
            "Settings", "Delete rejected levels?", false,
            "Should a map that lost the vote be deleted from the playlist?");

        _deleteRejectedDelaySeconds = _config.Bind(
            "Settings", "Deletion delay (seconds)", 0,
            new ConfigDescription(
                "Extra delay before deleting a rejected level. Total delay = 3s + this value.",
                new AcceptableValueRange<int>(0, 300)));

        _servermessageTitle = _config.Bind(
            "Settings", "Servermessage title", "Playlist-Voting",
            "Title shown in the server message overlay.");

        _webToken = _config.Bind(
            "Web API", "WebToken", "XXXXX-XXXXX-XXX-XXX-XXX",
            "Your PlaylistVoting API token.");
        _webApiUrl = _config.Bind(
            "Web API", "WebApiURL", "https://yololurk.herokuapp.com/zeepkist/playlistvoting",
            "URL of the PlaylistVoting API.");


        AcceptableValueList<string> emoteChoices = new AcceptableValueList<string>(ZeepkistEmojis.GetEmojis().ToArray());

        _winEmote = _config.Bind(
            "Emotes", "Win emote", ":yannicsmile:",
            new ConfigDescription("Emote shown when yes votes are leading.", emoteChoices));

        _tieEmote = _config.Bind(
            "Emotes", "Tie emote", ":yannics:",
            new ConfigDescription("Emote shown when votes are tied.", emoteChoices));

        _loseEmote = _config.Bind(
            "Emotes", "Lose emote", ":yannicmegas:",
            new ConfigDescription("Emote shown when no votes are leading.", emoteChoices));
    }

    private void RegisterChatCommands()
    {
        ChatCommandApi.RegisterMixedChatCommand<VoteYes>();
        ChatCommandApi.RegisterMixedChatCommand<VoteNo>();
        ChatCommandApi.RegisterMixedChatCommand<VoteRemove>();
        ChatCommandApi.RegisterLocalChatCommand<VoteReset>();
        ChatCommandApi.RegisterLocalChatCommand<VoteStart>();
        ChatCommandApi.RegisterLocalChatCommand<VoteStop>();

        VotingEventBus.Hub.VoteResetRequested += () => _ = ResetVotesAsync();
    }

    private async Task LoadInitialLevelDataAsync()
    {
        try
        {
            CurrentLevelName = await VotingApiClient.GetCurrentLevelNameAsync();
            CurrentLevelAuthor = await VotingApiClient.GetCurrentAuthorAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Could not load initial level data: {ex.Message}");
        }
    }
}