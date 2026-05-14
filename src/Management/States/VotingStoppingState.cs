using PlaylistVoting.Management.States.Abstractions;
using PlaylistVoting.Networking.Zeepkist;
using ZeepUtils.Zeepkist;

namespace PlaylistVoting.Management.States;

public class VotingStoppingState : VotingStateBase
{
    public VotingStoppingState(VotingController controller) : base(controller) { }

    public override void OnEnter()
    {
        // ToastNotification.Info("Stopping Playlist Voting...");
        // ZeepkistNetworkHelper.SendLocalPrivateMessage("Stopping Playlist Voting...");

        ToastNotification.Info("Playlist Voting has been stopped.");
        ZeepkistNetworkHelper.SendLocalPrivateMessage("Playlist Voting has been stopped.");

        Controller.TransitionTo(new VotingDisabledState(Controller));
    }
}