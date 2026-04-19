using PlaylistVoting.core;
using ZeepkistClient;
using ZeepSDK.Chat;
using ZeepSDK.ChatCommands;

namespace PlaylistVoting.commands;

public class VoteNo : IMixedChatCommand
{
    public string Prefix => "!";
    public string Command => "n";

    public string Description => "Votes No!";

    public void Handle(string arguments)
    {
        Handle(ZeepkistNetwork.LocalPlayer.SteamID, arguments);
        ChatApi.SendMessage(Prefix + Command + arguments);
    }

    public void Handle(ulong playerId, string arguments)
    {
        VotingEventBus.Hub.PublishPlayerVotedNo(playerId);
    }
}