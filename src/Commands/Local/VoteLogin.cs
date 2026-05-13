using System.Threading.Tasks;
using PlaylistVoting.Core.Controllers;
using PlaylistVoting.Infrastructure.Zeepkist;
using ZeepSDK.ChatCommands;

namespace PlaylistVoting.Commands.Local;

public class VoteLogin : ILocalChatCommand
{
    public string Prefix => "/";
    public string Command => "vote login";
    public string Description => "[Playlist Voting] Log in using your Steam account.";

    public void Handle(string arguments)
    {
        _ = LoginInternal();
    }

    private async Task LoginInternal()
    {
        ZeepkistNetworkHelper.SendLocalPrivateMessage("Logging in to PlaylistVoting via Steam...");
        bool success = await VotingController.Instance.BackendService.ManualLoginWithSteamAsync();

        if (success)
        {
            ZeepkistNetworkHelper.SendLocalPrivateMessage("Login successful! Token saved to config.");
        }
        else
        {
            ZeepkistNetworkHelper.SendLocalPrivateMessage("Login failed! Could not authenticate with Steam ticket.");
        }
    }
}