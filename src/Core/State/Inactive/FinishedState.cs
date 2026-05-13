using PlaylistVoting.Core.Controllers;
using PlaylistVoting.Core.State.Abstractions;
using PlaylistVoting.Infrastructure.Zeepkist;

namespace PlaylistVoting.Core.State.Inactive;

public class FinishedState : InactiveState
{
    public FinishedState(VotingController controller) : base(controller) { }

    public override void OnEnter()
    {
        Controller.OverlayService.Clear();
        ZeepkistNetworkHelper.SendLocalPrivateMessage("Playlist Voting session finished.");
    }

    public override void OnLevelLoaded()
    {
        Controller.TransitionTo(new VotingDisabledState(Controller));
    }
}