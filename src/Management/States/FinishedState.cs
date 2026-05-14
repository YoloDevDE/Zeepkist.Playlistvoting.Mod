using PlaylistVoting.Management.States.Abstractions;
using PlaylistVoting.Networking.Zeepkist;

namespace PlaylistVoting.Management.States;

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