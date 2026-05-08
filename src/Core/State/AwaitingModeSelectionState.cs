using PlaylistVoting.Core.Controllers;
using PlaylistVoting.Core.Models;
using PlaylistVoting.Core.State.Abstractions;
using YoloDev.Text;
using YoloDev.Zeepkist;
using ZeepkistClient;

namespace PlaylistVoting.Core.State;

public class AwaitingModeSelectionState : SessionState
{
    public AwaitingModeSelectionState(VotingController controller, PlaylistSessionInfo session) : base(controller, session)
    {
    }

    public override void OnEnter()
    {
        string msg = new TMPRichTextBuilder()
                     .AddLayer("How do you want to start Playlist Voting?", b => b.Bold().Color("#00f8ad"))
                     .Break()
                     .AddLayer("/vote playlistmode", b => b.Color("#ff9900"))
                     .AddLayer(" - automated playlist voting")
                     .Break()
                     .AddLayer("/vote simplemode", b => b.Color("#ff9900"))
                     .AddLayer(" - result broadcast only")
                     .Build();

        if (ZeepkistNetwork.LocalPlayer != null)
        {
            MessageApi.SendPrivateCustomChatMessage(msg, "VOTING", ZeepkistNetwork.LocalPlayer.SteamID);
        }
    }

    public override void OnPlaylistModeRequested()
    {
        Controller.TransitionTo(new PlaylistStartupState(Controller, Session));
    }

    public override void OnSimpleModeRequested()
    {
        Controller.TransitionTo(new SimpleVotingActiveState(Controller, Session));
    }
}