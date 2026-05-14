using PlaylistVoting.Management;
using PlaylistVoting.Networking.Zeepkist;
using ZeepSDK.ChatCommands;

namespace PlaylistVoting.Networking.Chat.Commands;

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
            string masked = token.Length > 8 ? token.Substring(0, 8) + "…" : "…";
            ZeepkistNetworkHelper.SendLocalPrivateMessage($"Session token (masked): {masked}");
        }
    }
}