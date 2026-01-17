using System;
using ZeepSDK.ChatCommands;

namespace PlaylistVoting;

public class VoteStop : ILocalChatCommand
{
    public string Prefix => "/";
    public string Command => "vote stop";

    public string Description => "Stops the Count!!";

    public void Handle(string arguments)
    {
        OnHandle?.Invoke();
    }

    // Event-Definition
    public static event Action OnHandle;
}