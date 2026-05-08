using PlaylistVoting.Core.Controllers;
using PlaylistVoting.Core.State.Abstractions;
using YoloDev.Text;
using YoloDev.Zeepkist;
using ZeepkistClient;

namespace PlaylistVoting.Core.State;

public class NoSessionState : VotingStateBase
{
    public NoSessionState(VotingController controller) : base(controller) { }

    public override void OnEnter()
    {
        string msg = new TMPRichTextBuilder()
                     .AddLayer("No active Playlist Voting session found.", b => b.Color("#FF0000"))
                     .Break()
                     .AddLayer("Use /vote start or create a session in the dashboard.", b => b.Size(80))
                     .Build();

        if (ZeepkistNetwork.LocalPlayer != null)
        {
            MessageApi.SendPrivateCustomChatMessage(msg, "VOTING", ZeepkistNetwork.LocalPlayer.SteamID);
        }
    }

    public override void OnResumeRequested()
    {
        Controller.TransitionTo(new InitState(Controller));
    }
}