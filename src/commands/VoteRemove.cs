using PlaylistVoting.core;
using ZeepkistClient;
using ZeepSDK.Chat;
using ZeepSDK.ChatCommands;

namespace PlaylistVoting.commands;

public class VoteRemove : IMixedChatCommand
{
    public string Prefix => "!";
    public string Command => "r";

    public string Description => "Removes your vote";

    public void Handle(string arguments)
    {
        Handle(ZeepkistNetwork.LocalPlayer.SteamID, arguments);
        ChatApi.SendMessage(Prefix + Command + arguments);
    }

    public void Handle(ulong playerId, string arguments)
    {
        VotingEventBus.Hub.PublishPlayerVotedRemove(playerId);
    }
}