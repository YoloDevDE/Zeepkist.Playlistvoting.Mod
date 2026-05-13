using PlaylistVoting.Commands.Local;
using PlaylistVoting.Core.Controllers;
using PlaylistVoting.Core.State.Abstractions;
using PlaylistVoting.Core.State.Setup;
using PlaylistVoting.Infrastructure.Zeepkist;
using ZeepUtils.Text;

namespace PlaylistVoting.Core.State.Inactive;

public class VotingDisabledState : InactiveState
{
    public VotingDisabledState(VotingController controller) : base(controller) { }

    public override void OnEnter()
    {
        Controller.ResetSession();
        Controller.OverlayService.Clear();
    }

    public override void OnResumeRequested()
    {
        Controller.TransitionTo(new InitState(Controller));
    }

    public override void OnVoteStopRequested()
    {
        string msg = new RichText("Already stopped or no instance running.").Break().Append("Type ").Append($"{new VoteStart().Prefix}{new VoteStart().Command}", b => b.Color("#f00")).Append(" to start.").Build();

        ZeepkistNetworkHelper.SendLocalPrivateMessage(msg);
    }
}