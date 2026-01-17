using System;
using ZeepSDK.ChatCommands;

namespace PlaylistVoting.commands;

public class VoteReset : ILocalChatCommand
{
    public string Prefix => "/";
    public string Command => "vote reset";

    public string Description => "Reset the votes and prints the result";

    public void Handle(string arguments)
    {
        OnHandle?.Invoke();
    }

    // Event-Definition
    public static event Action OnHandle;
}