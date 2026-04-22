using System;
using PlaylistVoting.misc;

namespace PlaylistVoting.core;

public interface IVotingEventHub
{
    public event Action<ulong, VotingType> PlayerVoted;
    public event Action VotingStarted;
    public event Action VotingStopped;
    public event Action VotingReset;
    public event Action<ZeepkistLobbyState> ZeepkistLobbyStateChanged;


    public void OnPlayerVoted(ulong steamId, VotingType votingType);
    public void OnVotingStarted();
    public void OnVotingStopped();
    public void OnZeepkistLobbyStateChanged(ZeepkistLobbyState zeepkistLobbyState);
    public void OnVotingReset();
}

public sealed class VotingEventHub : IVotingEventHub
{

    public event Action<ulong, VotingType> PlayerVoted;

    public event Action VotingStarted;
    public event Action VotingStopped;
    public event Action VotingReset;
    public event Action<ZeepkistLobbyState> ZeepkistLobbyStateChanged;

    public void OnPlayerVoted(ulong steamId, VotingType votingType) => PlayerVoted?.Invoke(steamId, votingType);

    public void OnVotingStarted() => VotingStarted?.Invoke();

    public void OnVotingStopped() => VotingStopped?.Invoke();

    public void OnZeepkistLobbyStateChanged(ZeepkistLobbyState obj) => ZeepkistLobbyStateChanged?.Invoke(obj);
    public void OnVotingReset() => VotingReset?.Invoke();
    
}