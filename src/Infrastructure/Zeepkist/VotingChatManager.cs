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
        ChatCommandApi.RegisterLocalChatCommand<VoteResume>();
        ChatCommandApi.RegisterLocalChatCommand<VoteConfirm>();
        ChatCommandApi.RegisterLocalChatCommand<VotePlaylistMode>();
        ChatCommandApi.RegisterLocalChatCommand<VoteSimpleMode>();
        ChatCommandApi.RegisterLocalChatCommand<VoteUseLocal>();
        ChatCommandApi.RegisterLocalChatCommand<VoteUseOnline>();
        ChatCommandApi.RegisterLocalChatCommand<VoteMerge>();
        ChatCommandApi.RegisterLocalChatCommand<VoteLogin>();
        ChatCommandApi.RegisterLocalChatCommand<VoteGetToken>();
    }

    public static void RegisterRemoteCommands()
    {
        Logger.Info("VotingChatManager: Registering remote commands...");
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
        Logger.Info("VotingChatManager: Unregistering remote commands...");
        ChatCommandApi.UnregisterMixedChatCommand(new VoteYes());
        ChatCommandApi.UnregisterMixedChatCommand(new VoteNo());
        ChatCommandApi.UnregisterMixedChatCommand(new VoteIdk());
        ChatCommandApi.UnregisterMixedChatCommand(new VoteRemove());
        ChatCommandApi.UnregisterMixedChatCommand(new VoteAbstain());
    }
}