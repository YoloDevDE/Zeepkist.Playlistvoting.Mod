using PlaylistVoting.Data.DTOs;
using PlaylistVoting.Data.Enums;
using ZeepUtils.Zeepkist;

namespace PlaylistVoting.Management.States.Abstractions;

public abstract class VotingStateBase : IVotingState
{
    protected readonly VotingController Controller;

    protected VotingStateBase(VotingController controller)
    {
        Controller = controller;
    }

    void IVotingState.OnLobbyStateChanged(ZeepkistLobbyState state)
    {
        LogCall($"{nameof(IVotingState.OnLobbyStateChanged)}({state})");
        OnLobbyStateChanged(state);
    }

    void IVotingState.OnMasterStatusChanged()
    {
        LogCall($"{nameof(IVotingState.OnMasterStatusChanged)}");
        OnMasterStatusChanged();
    }

    void IVotingState.OnPlayerVoted(ulong steamId, VotingType type)
    {
        LogCall($"{nameof(IVotingState.OnPlayerVoted)}({steamId}, {type})");
        OnPlayerVoted(steamId, type);
    }

    void IVotingState.OnLevelLoaded()
    {
        LogCall(nameof(IVotingState.OnLevelLoaded));
        OnLevelLoaded();
    }

    void IVotingState.OnLevelDataReceived(string levelName, string[] levelLines, string adventureUid)
    {
        LogCall(nameof(IVotingState.OnLevelDataReceived));
        OnLevelDataReceived(levelName, levelLines, adventureUid);
    }

    void IVotingState.OnVoteStartRequested(string sessionName)
    {
        LogCall($"{nameof(IVotingState.OnVoteStartRequested)}({sessionName})");
        OnVoteStartRequested(sessionName);
    }

    void IVotingState.OnVoteStopRequested()
    {
        LogCall(nameof(IVotingState.OnVoteStopRequested));
        OnVoteStopRequested();
    }

    void IVotingState.OnVoteRestartRequested()
    {
        LogCall(nameof(IVotingState.OnVoteRestartRequested));
        OnVoteRestartRequested();
    }

    void IVotingState.OnVotingResultReceived(VotingResultResponse result)
    {
        // Don't log full result as it might be large
        LogCall(nameof(IVotingState.OnVotingResultReceived));
        OnVotingResultReceived(result);
    }

    void IVotingState.OnPlaylistModeRequested()
    {
        LogCall(nameof(IVotingState.OnPlaylistModeRequested));
        OnPlaylistModeRequested();
    }

    void IVotingState.OnSimpleModeRequested()
    {
        LogCall(nameof(IVotingState.OnSimpleModeRequested));
        OnSimpleModeRequested();
    }

    void IVotingState.OnResumeRequested()
    {
        LogCall(nameof(IVotingState.OnResumeRequested));
        OnResumeRequested();
    }

    void IVotingState.OnUseLocalRequested()
    {
        LogCall(nameof(IVotingState.OnUseLocalRequested));
        OnUseLocalRequested();
    }

    void IVotingState.OnUseOnlineRequested()
    {
        LogCall(nameof(IVotingState.OnUseOnlineRequested));
        OnUseOnlineRequested();
    }

    void IVotingState.OnMergeRequested()
    {
        LogCall(nameof(IVotingState.OnMergeRequested));
        OnMergeRequested();
    }

    public virtual void OnConfirmRequested() { }

    void IState.OnEnter()
    {
        LogCall(nameof(IVotingState.OnEnter));
        OnEnter();
    }

    void IState.OnExit()
    {
        LogCall(nameof(IVotingState.OnExit));
        OnExit();
    }

    void IState.OnUpdate()
    {
        // We usually don't want to log OnUpdate as it fires every frame.
        OnUpdate();
    }

    public virtual void OnEnter() { }
    public virtual void OnExit() { }
    public virtual void OnUpdate() { }

    private void LogCall(string methodName)
    {
        Logger.Info($"[State] {GetType().Name}: {methodName}");
    }

    public virtual void OnLobbyStateChanged(ZeepkistLobbyState state) { }
    public virtual void OnMasterStatusChanged() { }
    public virtual void OnPlayerVoted(ulong steamId, VotingType type) { }
    public virtual void OnLevelLoaded() { }
    public virtual void OnLevelDataReceived(string levelName, string[] levelLines, string adventureUid) { }

    public virtual void OnVoteStartRequested(string sessionName = null)
    {
        ToastNotification.Warning("Playlist Voting is already running.");
    }

    public virtual void OnVoteStopRequested() { }
    public virtual void OnVoteRestartRequested() { }

    public virtual void OnVotingResultReceived(VotingResultResponse result) { }

    public virtual void OnPlaylistModeRequested() { }
    public virtual void OnSimpleModeRequested() { }
    public virtual void OnResumeRequested() { }
    public virtual void OnUseLocalRequested() { }
    public virtual void OnUseOnlineRequested() { }
    public virtual void OnMergeRequested() { }
}