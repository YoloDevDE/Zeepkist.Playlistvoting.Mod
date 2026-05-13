using PlaylistVoting.Core.Controllers;
using PlaylistVoting.Core.State.Abstractions;
using PlaylistVoting.Infrastructure.Zeepkist;
using ZeepUtils.Zeepkist;

namespace PlaylistVoting.Core.State.Inactive;

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