using System;
using ZeepkistClient;
using ZeepSDK.Chat;
using ZeepSDK.ChatCommands;

namespace PlaylistVoting;

public class VoteNo : IMixedChatCommand
{
    public string Prefix => "!";
    public string Command => "n";

    public string Description =>
        "Votes No!";

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