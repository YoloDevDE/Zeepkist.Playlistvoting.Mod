using PlaylistVoting.Commands.Local;
using PlaylistVoting.Commands.Remote;
using PlaylistVoting.Core.Config;
using ZeepSDK.ChatCommands;

namespace PlaylistVoting.Infrastructure.Zeepkist;

public static class VotingChatManager
{
    public static void RegisterCommands()
    {
        ChatCommandApi.RegisterLocalChatCommand<VoteStart>();
        ChatCommandApi.RegisterLocalChatCommand<VoteStop>();
        ChatCommandApi.RegisterLocalChatCommand<VoteRestart>();
        ChatCommandApi.RegisterLocalChatCommand<VoteTest>();
    }

    public static void RegisterRemoteCommands()
    {
        ChatCommandApi.RegisterMixedChatCommand<VoteYes>();
        ChatCommandApi.RegisterMixedChatCommand<VoteNo>();
        ChatCommandApi.RegisterMixedChatCommand<VoteIdk>();
        ChatCommandApi.RegisterMixedChatCommand<VoteRemove>();
        if (VotingConfig.Instance.IncludeAbstainVotes)
        {
            ChatCommandApi.RegisterMixedChatCommand<VoteAbstain>();
        }
    }

    public static void UnregisterRemoteCommands()
    {
        ChatCommandApi.UnregisterMixedChatCommand(new VoteYes());
        ChatCommandApi.UnregisterMixedChatCommand(new VoteNo());
        ChatCommandApi.UnregisterMixedChatCommand(new VoteIdk());
        ChatCommandApi.UnregisterMixedChatCommand(new VoteRemove());
        ChatCommandApi.UnregisterMixedChatCommand(new VoteAbstain());
    }
}