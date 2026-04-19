using System;

namespace PlaylistVoting.core;

public interface IVotingEventHub
{
    event Action<ulong> PlayerVotedYes;
    event Action<ulong> PlayerVotedNo;
    event Action<ulong> PlayerVotedRemove;
    event Action VoteStartRequested;
    event Action VoteStopRequested;
    event Action VoteResetRequested;
    event Action<GamePhase> GamePhaseChanged;

    void PublishPlayerVotedYes(ulong playerId);
    void PublishPlayerVotedNo(ulong playerId);
    void PublishPlayerVotedRemove(ulong playerId);
    void PublishVoteStartRequested();
    void PublishVoteStopRequested();
    void PublishVoteResetRequested();
    void PublishGamePhaseChanged(GamePhase phase);
}