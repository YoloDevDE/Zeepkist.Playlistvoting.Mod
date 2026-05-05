using PlaylistVoting.Core.Controllers;
using PlaylistVoting.Core.Models;
using ZeepkistClient;
using ZeepSDK.Chat;
using ZeepSDK.ChatCommands;

namespace PlaylistVoting.Commands;

public abstract class BaseVoteCommand : IMixedChatCommand
{
    protected abstract VotingType VotingType { get; }
    public abstract string Prefix { get; }
    public abstract string Command { get; }
    public abstract string Description { get; }

    public void Handle(string arguments)
    {
        ChatApi.SendMessage(Prefix + Command + arguments);
        Handle(ZeepkistNetwork.LocalPlayer.SteamID, arguments);
    }

    public void Handle(ulong steamId, string arguments)
    {
        VotingController.Instance.OnPlayerVoted(steamId, VotingType);
    }
}