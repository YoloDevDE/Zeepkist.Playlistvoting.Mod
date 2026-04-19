using PlaylistVoting.core;
using ZeepSDK.ChatCommands;

namespace PlaylistVoting.commands;

public class VoteStop : ILocalChatCommand
{
    public string Prefix => "/";
    public string Command => "vote stop";

    public string Description => "Stops the Count!!";

    public void Handle(string arguments)
    {
        VotingEventBus.Hub.PublishVoteStopRequested();
    }
}