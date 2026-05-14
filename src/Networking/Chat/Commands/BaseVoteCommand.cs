using PlaylistVoting.Data.Enums;
using PlaylistVoting.Management;
using PlaylistVoting.Networking.Zeepkist;
using ZeepSDK.Chat;
using ZeepSDK.ChatCommands;

namespace PlaylistVoting.Networking.Chat.Commands;

public abstract class BaseVoteCommand : IMixedChatCommand
{
    protected abstract VotingType VotingType { get; }
    public abstract string Prefix { get; }
    public abstract string Command { get; }
    public abstract string Description { get; }

    public void Handle(string arguments)
    {
        ChatApi.SendMessage(Prefix + Command + arguments);

        if (ZeepkistNetworkHelper.LocalPlayer != null)
        {
            Handle(ZeepkistNetworkHelper.LocalPlayer.SteamID, arguments);
        }
    }

    public void Handle(ulong steamId, string arguments)
    {
        VotingController.Instance.OnPlayerVoted(steamId, VotingType);
    }
}