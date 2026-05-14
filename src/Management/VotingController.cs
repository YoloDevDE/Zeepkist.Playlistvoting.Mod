using PlaylistVoting.Data.DTOs;
using PlaylistVoting.Data.Enums;
using PlaylistVoting.Data.Models;
using PlaylistVoting.Display;
using PlaylistVoting.Management.States;
using PlaylistVoting.Management.States.Abstractions;
using PlaylistVoting.Networking.Backend;
using PlaylistVoting.Networking.Chat;
using PlaylistVoting.Networking.Zeepkist;
using UnityEngine;
using ZeepkistClient;
using ZeepSDK.Racing;
using ZeepUtils.Zeepkist;

namespace PlaylistVoting.Management;

public class VotingController : MonoBehaviour
{
    private IVotingState _currentState;

    public ZeepkistLobbyState CurrentLobbyState { get; private set; } = ZeepkistLobbyState.NotInALobby;


    // ── Public API ────────────────────────────────────────────────────────────

    public static VotingController Instance { get; private set; }
    public VotingBackendService BackendService { get; private set; }
    public OverlayService OverlayService => OverlayService.Instance;

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
        OverlayService?.Refresh();
    }

    private void OnDestroy()
    {
        RacingApi.LevelLoaded -= OnLevelLoaded;
        ZeepkistNetwork.MasterChanged -= OnMasterClientChanged;
        ZeepkistNetwork.LevelDataReceived -= OnLevelDataReceived;
        _currentState?.OnExit();
        BackendService?.Dispose();
    }

    // ── Initialization ────────────────────────────────────────────────────────

    public void Initialize()
    {
        BackendService = new VotingBackendService();
        OverlayService.Init(ServermessageTitle);
        ZeepkistPlaylistService.Init();
        PlaylistSyncService.Init();

        if (ZeepkistNetwork.CurrentLobby != null && ZeepkistNetwork.CurrentLobby.GameState != 0)
        {
            ToastNotification.Info("Mod will start after this level.");
            ZeepkistNetworkHelper.SendLocalPrivateMessage("Playlist Voting Mod will start as soon as the next level is loaded.");
        }

        BackendService.OnResultReceived += UpdateFromVotingResult;
        ZeepkistNetwork.LobbyGameStateChanged += () => OnLobbyStateChanged((ZeepkistLobbyState)ZeepkistNetwork.CurrentLobby.GameState);
        RacingApi.LevelLoaded += OnLevelLoaded;
        ZeepkistNetwork.MasterChanged += OnMasterClientChanged;
        ZeepkistNetwork.LevelDataFailed += OnLevelDataFailed;
        ZeepkistNetwork.LevelDataReceived += OnLevelDataReceived;

        VotingChatManager.RegisterCommands();
        TransitionTo(new VotingDisabledState(this));
    }

    // ── State transitions ─────────────────────────────────────────────────────

    public void ResetSession()
    {
        CurrentSession = null;
        CurrentLevel = new LevelMetadata();
        BackendService?.Disconnect();
        Logger.Info("Session and Level metadata reset and backend disconnected.");
    }

    public void TransitionTo(IVotingState next)
    {
        Logger.Info($"State: {_currentState?.GetType().Name} → {next.GetType().Name}");
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

    public void OnVoteStartRequested(string sessionName = null)
    {
        _currentState?.OnVoteStartRequested(sessionName);
    }

    public void OnVoteStopRequested()
    {
        Logger.Info("Stop requested, transitioning to VotingStoppingState.");
        TransitionTo(new VotingStoppingState(this));
    }

    public void OnVoteRestartRequested()
    {
        Logger.Info("Restart requested, forcing transition to InitState.");
        TransitionTo(new InitState(this));
    }

    public void OnPlaylistModeRequested() => _currentState?.OnPlaylistModeRequested();
    public void OnSimpleModeRequested() => _currentState?.OnSimpleModeRequested();
    public void OnResumeRequested() => _currentState?.OnResumeRequested();
    public void OnConfirmRequested() => _currentState?.OnConfirmRequested();
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
        Logger.Info($"Master changed: {zeepkistNetworkPlayer.Username}");
        _currentState?.OnMasterStatusChanged();
    }

    private void OnLobbyStateChanged(ZeepkistLobbyState state)
    {
        CurrentLobbyState = state;
        Logger.Info($"Lobby state: {state}");
        _currentState?.OnLobbyStateChanged(state);
    }

    private void OnLevelDataFailed()
    {
        Logger.Warn("Level data failed to load.");
        TransitionTo(new LevelLoadErrorState(this));
    }

    private void OnLevelDataReceived(string levelName, string[] levelLines, string adventureUid)
    {
        _currentState?.OnLevelDataReceived(levelName, levelLines, adventureUid);
    }
}