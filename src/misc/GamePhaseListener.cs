using System;
using PlaylistVoting.core;
using ZeepkistClient;
using ZeepSDK.Racing;

namespace PlaylistVoting.misc;

public class GamePhaseListener : IDisposable
{
    private GamePhase _currentPhase = GamePhase.Unknown;

    public GamePhaseListener()
    {
        RacingApi.LevelLoaded += UpdatePhase;
        RacingApi.RoundEnded += UpdatePhase;
    }

    public GamePhase CurrentPhase
    {
        get
        {
            UpdatePhase();
            return _currentPhase;
        }
        private set
        {
            if (_currentPhase == value)
            {
                return;
            }

            _currentPhase = value;
            VotingEventBus.Hub.PublishGamePhaseChanged(_currentPhase);
        }
    }

    public void Dispose()
    {
        RacingApi.LevelLoaded -= UpdatePhase;
        RacingApi.RoundEnded -= UpdatePhase;
    }

    private void UpdatePhase()
    {
        if (ZeepkistNetwork.CurrentLobby == null)
        {
            CurrentPhase = GamePhase.Unknown;
            return;
        }

        int gameState = ZeepkistNetwork.CurrentLobby.GameState;
        GamePhase detectedPhase = Enum.IsDefined(typeof(GamePhase), gameState)
            ? (GamePhase)gameState
            : GamePhase.Unknown;

        CurrentPhase = detectedPhase;
    }
}