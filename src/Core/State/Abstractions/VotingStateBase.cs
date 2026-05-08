using PlaylistVoting.Core.Controllers;
using PlaylistVoting.Core.Models;

namespace PlaylistVoting.Core.State.Abstractions;

public abstract class VotingStateBase : IVotingState
{
    protected readonly VotingController Controller;

    protected VotingStateBase(VotingController controller)
    {
        Controller = controller;
    }

    void IVotingState.OnEnter()
    {
        LogCall(nameof(IVotingState.OnEnter));
        OnEnter();
    }

    void IVotingState.OnExit()
    {
        LogCall(nameof(IVotingState.OnExit));
        OnExit();
    }

    void IVotingState.OnUpdate()
    {
        // We usually don't want to log OnUpdate as it fires every frame.
        OnUpdate();
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

    void IVotingState.OnVoteStartRequested()
    {
        LogCall(nameof(IVotingState.OnVoteStartRequested));
        OnVoteStartRequested();
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

    private void LogCall(string methodName)
    {
        VotingController.Instance?.Logger?.LogInfo($"[State] {GetType().Name}: {methodName}");
    }

    public virtual void OnEnter() { }
    public virtual void OnExit() { }
    public virtual void OnUpdate() { }
    public virtual void OnLobbyStateChanged(ZeepkistLobbyState state) { }
    public virtual void OnMasterStatusChanged() { }
    public virtual void OnPlayerVoted(ulong steamId, VotingType type) { }
    public virtual void OnLevelLoaded() { }
    public virtual void OnVoteStartRequested() { }
    public virtual void OnVoteStopRequested() { }
    public virtual void OnVoteRestartRequested() { }

    public virtual void OnVotingResultReceived(VotingResultResponse result) { }
}