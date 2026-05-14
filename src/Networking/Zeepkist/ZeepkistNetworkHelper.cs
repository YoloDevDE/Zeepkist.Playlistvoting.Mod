using ZeepkistClient;
using ZeepUtils.Zeepkist;

namespace PlaylistVoting.Networking.Zeepkist;

public static class ZeepkistNetworkHelper
{
    public const string CategoryVoting = "VOTING";
    public const string CategoryConflict = "CONFLICT";
    public const string CategoryError = "ERROR";

    public static ZeepkistNetworkPlayer LocalPlayer => ZeepkistNetwork.LocalPlayer;

    public static void SendLocalPrivateMessage(string message, string category = CategoryVoting)
    {
        if (LocalPlayer != null)
        {
            MessageApi.SendPrivateCustomChatMessage(message, category, LocalPlayer.SteamID);
        }
    }
}