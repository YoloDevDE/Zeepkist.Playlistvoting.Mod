using PlaylistVoting.Core.Controllers;
using PlaylistVoting.Infrastructure.Zeepkist;
using ZeepSDK.ChatCommands;

namespace PlaylistVoting.Commands.Local;

public class VoteGetToken : ILocalChatCommand
{
    public string Prefix => "/";
    public string Command => "vote token";
    public string Description => "[Playlist Voting] Prints your current session token.";

    public void Handle(string arguments)
    {
        string token = VotingController.Instance.BackendService.GetSessionToken();

        if (string.IsNullOrEmpty(token))
        {
            ZeepkistNetworkHelper.SendLocalPrivateMessage("No session token found. Use /vote login to get one.");
        }
        else
        {
            ZeepkistNetworkHelper.SendLocalPrivateMessage($"Your session token is: {token}");
        }
    }
}