using PlaylistVoting.Commands.Local;
using PlaylistVoting.Core.Controllers;
using PlaylistVoting.Core.State.Abstractions;
using YoloDev.Text;
using ToastNotification = YoloDev.Zeepkist.ToastNotification;

namespace PlaylistVoting.Core.State;

public class VotingDisabledState : VotingStateBase
{
    private readonly VotingController _controller;

    public VotingDisabledState(VotingController controller) : base(controller)
    {
        _controller = controller;
    }

    public override void OnVoteStartRequested()
    {
        _controller.TransitionTo(new VotingActiveState(_controller));
    }

    public override void OnVoteStopRequested()
    {
        string msg = new TMPRichTextBuilder("Already stopped or no instance running.")
                     .Break()
                     .AddLayer("Type ")
                     .AddLayer($"{new VoteStart().Prefix}{new VoteStart().Command}", b => b.Color("#f00"))
                     .AddLayer(" to start.")
                     .Build();
        ToastNotification.Warning(msg, 5f);
    }

    public override void OnVoteRestartRequested()
    {
        OnVoteStartRequested();
    }
}