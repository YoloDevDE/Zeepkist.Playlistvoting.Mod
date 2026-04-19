using PlaylistVoting.core;
using ZeepkistClient;
using ZeepSDK.Messaging;

namespace PlaylistVoting.states;

public class StateInactive : State
{
    private bool _voteStartRequested;

    public StateInactive(VotingManager manager) : base(manager) { }

    public override void Enter()
    {
        base.Enter();
        VotingEventBus.Hub.VoteStartRequested += OnVoteStartRequested;
        VotingEventBus.Hub.VoteStopRequested += OnVoteStopRequestedWhileInactive;
    }

    public override void Exit()
    {
        base.Exit();
        VotingEventBus.Hub.VoteStartRequested -= OnVoteStartRequested;
        VotingEventBus.Hub.VoteStopRequested -= OnVoteStopRequestedWhileInactive;
    }

    protected override void OnGamePhaseChanged(GamePhase phase)
    {
        if (phase == GamePhase.Racing && _voteStartRequested)
        {
            _voteStartRequested = false;
            StartVote();
        }
    }

    private void OnVoteStartRequested()
    {
        if (!ZeepkistNetwork.LocalPlayer.isHost)
        {
            MessengerApi.LogWarning("Only the host can start the vote!");
            return;
        }

        if (Manager.CurrentPhase != GamePhase.Racing)
        {
            _voteStartRequested = true;
            MessengerApi.Log("Vote will start once the racing phase begins.");
            return;
        }

        StartVote();
    }

    private void StartVote()
    {
        MessengerApi.LogSuccess("Vote successfully started!");
        Manager.SwitchState(new StateActive(Manager));
    }

    private void OnVoteStopRequestedWhileInactive()
    {
        if (_voteStartRequested)
        {
            _voteStartRequested = false;
            MessengerApi.LogSuccess("Pending vote start cancelled.");
        }
        else
        {
            MessengerApi.LogWarning("No vote is currently running.");
        }
    }
}