using System;
using PlaylistVoting.core;

namespace PlaylistVoting.commands.local;

public class VoteStart : BaseLocalVoteCommand
{
    public override string Prefix => "/";
    public override string Command => "vote start";
    public override string Description => "[Playlist Voting] Starts the Mod";
    protected override Action TriggerEvent => VotingEventBus.Hub.OnVotingStarted;
}