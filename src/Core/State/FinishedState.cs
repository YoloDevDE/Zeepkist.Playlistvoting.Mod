using PlaylistVoting.Core.Controllers;
using PlaylistVoting.Core.State.Abstractions;
using YoloDev.Zeepkist;
using ZeepkistClient;

namespace PlaylistVoting.Core.State;

public class FinishedState : VotingStateBase
{
    public FinishedState(VotingController controller) : base(controller) { }

    public override void OnEnter()
    {
        if (ZeepkistNetwork.LocalPlayer != null)
        {
            MessageApi.SendPrivateCustomChatMessage("Playlist Voting session finished.", "VOTING", ZeepkistNetwork.LocalPlayer.SteamID);
        }
    }
}