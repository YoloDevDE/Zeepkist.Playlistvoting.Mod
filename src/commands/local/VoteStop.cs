using System;
using PlaylistVoting.core;

namespace PlaylistVoting.commands.local;

public class VoteStop : BaseLocalVoteCommand
{
    public override string Prefix => "/";
    public override string Command => "vote stop";
    public override string Description => "[Playlist Voting] Stops the Mod";
    protected override Action TriggerEvent => VotingEventBus.Hub.OnVotingStopped;
}