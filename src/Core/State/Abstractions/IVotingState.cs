using PlaylistVoting.Core.Models;

namespace PlaylistVoting.Core.State.Abstractions;

public interface IVotingState
{
    void OnEnter();
    void OnExit();
    void OnUpdate();

    void OnLobbyStateChanged(ZeepkistLobbyState state);
    void OnMasterStatusChanged();
    void OnPlayerVoted(ulong steamId, VotingType type);
    void OnLevelLoaded();
    void OnVoteStartRequested();
    void OnVoteStopRequested();
    void OnVoteRestartRequested();
    void OnVotingResultReceived(VotingResultResponse result);

    void OnPlaylistModeRequested();
    void OnSimpleModeRequested();
    void OnResumeRequested();
    void OnUseLocalRequested();
    void OnUseOnlineRequested();
    void OnMergeRequested();
}