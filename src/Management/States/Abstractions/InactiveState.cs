using ZeepUtils.Zeepkist;

namespace PlaylistVoting.Management.States.Abstractions;

public abstract class InactiveState : VotingStateBase
{
    protected InactiveState(VotingController controller) : base(controller) { }

    public override void OnVoteStartRequested(string sessionName = null)
    {
        ToastNotification.Info("Starting Playlist Voting...");
        Controller.TransitionTo(new InitState(Controller, sessionName));
    }
}