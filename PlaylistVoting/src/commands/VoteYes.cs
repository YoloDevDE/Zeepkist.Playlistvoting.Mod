using System;
using ZeepkistClient;
using ZeepSDK.Chat;
using ZeepSDK.ChatCommands;

namespace PlaylistVoting;

public class VoteYes : IMixedChatCommand
{
    public string Prefix => "!";
    public string Command => "y";

    public string Description =>
        "Votes Yes!";

    public void Handle(string arguments)
    {
        Handle(ZeepkistNetwork.LocalPlayer.SteamID, arguments);
        ChatApi.SendMessage(Prefix + Command + arguments);
    }

    public void Handle(ulong playerId, string arguments)
    {
        OnHandle?.Invoke(playerId);
    }


    // Event-Definition
    public static event Action<ulong> OnHandle;
}