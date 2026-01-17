using System;
using ZeepSDK.ChatCommands;

namespace PlaylistVoting;

public class VoteStart : ILocalChatCommand
{
    public string Prefix => "/";
    public string Command => "vote start";

    public string Description => "Starts the Count!!";

    public void Handle(string arguments)
    {
        OnHandle?.Invoke();
    }

    // Event-Definition
    public static event Action OnHandle;
}