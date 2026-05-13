using PlaylistVoting.Core.Models;

namespace PlaylistVoting.Core.State.Abstractions;

public interface IVotingState : IState
{
    void OnLobbyStateChanged(ZeepkistLobbyState state);
    void OnMasterStatusChanged();
    void OnPlayerVoted(ulong steamId, VotingType type);
    void OnLevelLoaded();
    void OnLevelDataReceived(string levelName, string[] levelLines, string adventureUid);
    void OnVoteStartRequested(string sessionName = null);
    void OnVoteStopRequested();
    void OnVoteRestartRequested();
    void OnVotingResultReceived(VotingResultResponse result);

    void OnPlaylistModeRequested();
    void OnSimpleModeRequested();
    void OnResumeRequested();
    void OnConfirmRequested();
    void OnUseLocalRequested();
    void OnUseOnlineRequested();
    void OnMergeRequested();
}