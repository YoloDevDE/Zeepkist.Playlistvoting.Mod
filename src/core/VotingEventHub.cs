using System;

namespace PlaylistVoting.core;

public class VotingEventHub : IVotingEventHub
{
    public event Action<ulong> PlayerVotedYes;
    public event Action<ulong> PlayerVotedNo;
    public event Action<ulong> PlayerVotedRemove;
    public event Action VoteStartRequested;
    public event Action VoteStopRequested;
    public event Action VoteResetRequested;
    public event Action<GamePhase> GamePhaseChanged;

    public void PublishPlayerVotedYes(ulong playerId) => PlayerVotedYes?.Invoke(playerId);
    public void PublishPlayerVotedNo(ulong playerId) => PlayerVotedNo?.Invoke(playerId);
    public void PublishPlayerVotedRemove(ulong playerId) => PlayerVotedRemove?.Invoke(playerId);
    public void PublishVoteStartRequested() => VoteStartRequested?.Invoke();
    public void PublishVoteStopRequested() => VoteStopRequested?.Invoke();
    public void PublishVoteResetRequested() => VoteResetRequested?.Invoke();
    public void PublishGamePhaseChanged(GamePhase phase) => GamePhaseChanged?.Invoke(phase);
}