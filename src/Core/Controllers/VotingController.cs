using BepInEx.Logging;
using PlaylistVoting.Core.Models;
using PlaylistVoting.Core.State;
using PlaylistVoting.Core.State.Abstractions;
using PlaylistVoting.Infrastructure.Api;
using PlaylistVoting.Infrastructure.Zeepkist;
using UnityEngine;
using ZeepkistClient;
using ZeepSDK.Racing;

namespace PlaylistVoting.Core.Controllers;

public class VotingController : MonoBehaviour
{
    private IVotingState _currentState;
    public ZeepkistLobbyState CurrentLobbyState { get; private set; } = ZeepkistLobbyState.NotInALobby;


    // ── Public API ────────────────────────────────────────────────────────────

    public static VotingController Instance { get; private set; }
    public ManualLogSource Logger { get; private set; }
    public VotingBackendService BackendService { get; private set; }

    public string ServermessageTitle { get; } = "Playlist Voting";

    // Level metadata — updated by states and BackendService events
    public LevelMetadata CurrentLevel { get; set; } = new LevelMetadata();
    public PlaylistSessionInfo CurrentSession { get; set; }

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        _currentState?.OnUpdate();
    }

    private void OnDestroy()
    {
        RacingApi.LevelLoaded -= OnLevelLoaded;
        ZeepkistNetwork.MasterChanged -= OnMasterClientChanged;
        _currentState?.OnExit();
        BackendService?.Dispose();
    }

    // ── Initialization ────────────────────────────────────────────────────────

    public void Initialize(ManualLogSource logger)
    {
        Logger = logger;

        BackendService = new VotingBackendService(logger);
        BackendService.OnResultReceived += UpdateFromVotingResult;
        ZeepkistNetwork.LobbyGameStateChanged += () => OnLobbyStateChanged((ZeepkistLobbyState)ZeepkistNetwork.CurrentLobby.GameState);
        RacingApi.LevelLoaded += OnLevelLoaded;
        ZeepkistNetwork.MasterChanged += OnMasterClientChanged;

        VotingChatManager.RegisterCommands();
        TransitionTo(new VotingDisabledState(this));
    }

    // ── State transitions ─────────────────────────────────────────────────────

    public void TransitionTo(IVotingState next)
    {
        Logger.LogInfo($"State: {_currentState?.GetType().Name} → {next.GetType().Name}");
        _currentState?.OnExit();
        _currentState = next;
        _currentState.OnEnter();
    }

    // ── Event routing ─────────────────────────────────────────────────────────

    public void OnPlayerVoted(ulong steamId, VotingType type)
    {
        _currentState?.OnPlayerVoted(steamId, type);
    }

    private void OnLevelLoaded()
    {
        _currentState?.OnLevelLoaded();
    }

    public void OnVoteStartRequested()
    {
        _currentState?.OnVoteStartRequested();
    }

    public void OnVoteStopRequested()
    {
        Logger.LogInfo("Stop requested, forcing transition to VotingDisabledState.");
        TransitionTo(new VotingDisabledState(this));
    }

    public void OnVoteRestartRequested()
    {
        Logger.LogInfo("Restart requested, forcing transition to InitState.");
        TransitionTo(new InitState(this));
    }

    public void OnPlaylistModeRequested() => _currentState?.OnPlaylistModeRequested();
    public void OnSimpleModeRequested() => _currentState?.OnSimpleModeRequested();
    public void OnResumeRequested() => _currentState?.OnResumeRequested();
    public void OnUseLocalRequested() => _currentState?.OnUseLocalRequested();
    public void OnUseOnlineRequested() => _currentState?.OnUseOnlineRequested();
    public void OnMergeRequested() => _currentState?.OnMergeRequested();

    public void UpdateFromVotingResult(VotingResultResponse result)
    {
        _currentState?.OnVotingResultReceived(result);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void OnMasterClientChanged(ZeepkistNetworkPlayer zeepkistNetworkPlayer)
    {
        Logger.LogInfo($"Master changed: {zeepkistNetworkPlayer.Username}");
        _currentState?.OnMasterStatusChanged();
    }

    private void OnLobbyStateChanged(ZeepkistLobbyState state)
    {
        CurrentLobbyState = state;
        Logger.LogInfo($"Lobby state: {state}");
        _currentState?.OnLobbyStateChanged(state);
    }
}