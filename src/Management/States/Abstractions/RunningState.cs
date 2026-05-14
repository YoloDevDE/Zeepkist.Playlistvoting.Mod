using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using PlaylistVoting.Data.DTOs;
using PlaylistVoting.Data.Enums;
using PlaylistVoting.Data.Models;
using PlaylistVoting.Networking.Chat;
using UnityEngine;
using ZeepkistClient;

namespace PlaylistVoting.Management.States.Abstractions;

public abstract class RunningState : SessionState
{
    private readonly ConcurrentQueue<(ulong SteamId, VotingType Type)> _pendingVotes = new ConcurrentQueue<(ulong, VotingType)>();

    private int _lastGameState = -1;
    private string _lastTimeSent;
    private float _nextHeartbeat;

    protected RunningState(VotingController controller, PlaylistSessionInfo session) : base(controller, session) { }

    public sealed override void OnEnter()
    {
        Controller.BackendService.OnConnected += HandleConnected;
        Controller.BackendService.OnDisconnected += HandleDisconnected;
        _ = Controller.BackendService.ConnectAsync(ZeepkistNetwork.LocalPlayer?.SteamID.ToString() ?? "");
        VotingChatManager.RegisterRemoteCommands();
        OnRunningEnter();
    }

    public sealed override void OnExit()
    {
        OnRunningExit();
        Controller.BackendService.OnConnected -= HandleConnected;
        Controller.BackendService.OnDisconnected -= HandleDisconnected;
        Controller.BackendService.Disconnect();
        Controller.OverlayService.Clear();
        VotingChatManager.UnregisterRemoteCommands();
    }

    public override void OnUpdate()
    {
        HandleTimerHeartbeat();
        SendTimerIfChanged();
    }

    public override void OnLevelDataReceived(string levelName, string[] levelLines, string adventureUid)
    {
        _ = SendTimerUpdateToServer();
    }

    public override void OnPlayerVoted(ulong steamId, VotingType type)
    {
        if (Controller.BackendService.IsConnected)
        {
            _ = Controller.BackendService.SubmitVoteAsync(steamId, type);
        }
        else
        {
            Logger.Warn($"Offline — caching vote from {steamId} ({type})");
            _pendingVotes.Enqueue((steamId, type));
        }
    }

    protected abstract void OnRefreshDisplay();

    protected virtual void OnRunningEnter() { }
    protected virtual void OnRunningExit() { }

    private void SendTimerIfChanged()
    {
        string currentTime = ZeepkistNetwork.CurrentLobby?.timeLeftString ?? "--:--";

        if (currentTime != _lastTimeSent)
        {
            Controller.BackendService.SendTimer(currentTime);
            _lastTimeSent = currentTime;
        }
    }

    private void HandleTimerHeartbeat()
    {
        if (!ZeepkistNetwork.IsConnectedToGame || ZeepkistNetwork.CurrentLobby == null)
        {
            return;
        }

        if (!ZeepkistNetwork.IsMasterClient)
        {
            return;
        }

        bool stateChanged = ZeepkistNetwork.CurrentLobby.GameState != _lastGameState;
        bool heartbeat = Time.time >= _nextHeartbeat;

        if (stateChanged || heartbeat)
        {
            _ = SendTimerUpdateToServer();
            _lastGameState = ZeepkistNetwork.CurrentLobby.GameState;
            _nextHeartbeat = Time.time + 10f;
        }
    }

    private async Task SendTimerUpdateToServer()
    {
        try
        {
            if (Controller.BackendService == null)
            {
                return;
            }

            if (!ZeepkistNetwork.IsConnectedToGame || ZeepkistNetwork.CurrentLobby == null)
            {
                return;
            }

            if (!ZeepkistNetwork.IsMasterClient)
            {
                return;
            }

            TimerUpdateDto dto = new TimerUpdateDto
            {
                RoundTime = ZeepkistNetwork.CurrentLobby.RoundTime, LevelLoadedAtTime = ZeepkistNetwork.CurrentLobby.LevelLoadedAtTime, CurrentTime = ZeepkistNetwork.Time, GameState = ZeepkistNetwork.CurrentLobby.GameState
            };

            await Controller.BackendService.UpdateTimerAsync(dto);
        }
        catch (Exception ex)
        {
            Logger.Error($"SendTimerUpdateToServer failed: {ex.Message}");
        }
    }

    private void HandleConnected()
    {
        Logger.Info("RunningState: Backend reconnected — syncing cached votes.");
        _ = FlushPendingVotesAsync();
        OnRefreshDisplay();
    }

    private void HandleDisconnected()
    {
        Logger.Warn("RunningState: Backend disconnected.");
        OnRefreshDisplay();
    }

    private async Task FlushPendingVotesAsync()
    {
        int count = 0;

        while (_pendingVotes.TryDequeue(out (ulong SteamId, VotingType Type) vote))
            try
            {
                await Controller.BackendService.SubmitVoteAsync(vote.SteamId, vote.Type);
                count++;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to sync cached vote from {vote.SteamId}: {ex.Message}");
                _pendingVotes.Enqueue(vote);
                break;
            }

        if (count > 0)
        {
            Logger.Info($"Synced {count} cached vote(s) to backend.");
        }
    }
}