using System;
using PlaylistVoting.core;
using ZeepkistClient;
using ZeepSDK.Racing;

namespace PlaylistVoting.misc;

public class ZeepkistLobbyStateListener : IDisposable
{
    public ZeepkistLobbyStateListener()
    {
        RacingApi.LevelLoaded += UpdateState;
        RacingApi.RoundEnded += UpdateState;
    }

    public ZeepkistLobbyState CurrentState
    {
        get
        {
            UpdateState();
            return field;
        }
        private set
        {
            if (field == value)
            {
                return;
            }

            VotingEventBus.Hub.OnZeepkistLobbyStateChanged(field = value);
        }
    }


    public void Dispose()
    {
        RacingApi.LevelLoaded -= UpdateState;
        RacingApi.RoundEnded -= UpdateState;
    }

    private void UpdateState()
    {
        if (ZeepkistNetwork.CurrentLobby == null)
        {
            CurrentState = ZeepkistLobbyState.NotInALobby;
            return;
        }

        int gameState = ZeepkistNetwork.CurrentLobby.GameState;
        ZeepkistLobbyState detectedPhase = Enum.IsDefined(typeof(ZeepkistLobbyState), gameState)
            ? (ZeepkistLobbyState)gameState
            : ZeepkistLobbyState.NotInALobby;

        CurrentState = detectedPhase;
    }
}