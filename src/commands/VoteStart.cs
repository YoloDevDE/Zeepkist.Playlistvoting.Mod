using PlaylistVoting.core;
using ZeepSDK.ChatCommands;

namespace PlaylistVoting.commands;

public class VoteStart : ILocalChatCommand
{
    public string Prefix => "/";
    public string Command => "vote start";

    public string Description => "Starts the Count!!";

    public void Handle(string arguments)
    {
        VotingEventBus.Hub.PublishVoteStartRequested();
    }
}