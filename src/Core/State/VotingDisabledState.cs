using PlaylistVoting.Commands.Local;
using PlaylistVoting.Core.Controllers;
using PlaylistVoting.Core.State.Abstractions;
using YoloDev.Text;
using YoloDev.Zeepkist;
using ZeepkistClient;

namespace PlaylistVoting.Core.State;

public class VotingDisabledState : VotingStateBase
{
    public VotingDisabledState(VotingController controller) : base(controller) { }

    public override void OnVoteStartRequested()
    {
        Controller.TransitionTo(new InitState(Controller));
    }

    public override void OnResumeRequested()
    {
        Controller.TransitionTo(new InitState(Controller));
    }

    public override void OnVoteStopRequested()
    {
        string msg = new TMPRichTextBuilder("Already stopped or no instance running.")
                     .Break()
                     .AddLayer("Type ")
                     .AddLayer($"{new VoteStart().Prefix}{new VoteStart().Command}", b => b.Color("#f00"))
                     .AddLayer(" to start.")
                     .Build();

        if (ZeepkistNetwork.LocalPlayer != null)
        {
            MessageApi.SendPrivateCustomChatMessage(msg, "VOTING", ZeepkistNetwork.LocalPlayer.SteamID);
        }
    }
}