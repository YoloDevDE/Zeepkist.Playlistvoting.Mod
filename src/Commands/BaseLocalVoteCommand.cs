using System;
using ZeepSDK.ChatCommands;

namespace PlaylistVoting.Commands;

public abstract class BaseLocalVoteCommand : ILocalChatCommand
{
    protected abstract Action TriggerEvent { get; }
    public abstract string Prefix { get; }
    public abstract string Command { get; }
    public abstract string Description { get; }

    public virtual void Handle(string arguments)
    {
        TriggerEvent?.Invoke();
    }
}