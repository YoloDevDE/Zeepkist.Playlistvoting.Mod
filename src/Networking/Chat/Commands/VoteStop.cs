using System;
using PlaylistVoting.Management;

namespace PlaylistVoting.Networking.Chat.Commands;

public class VoteStop : BaseLocalVoteCommand
{
    public override string Prefix => "/";

    public override string Command => "vote stop";

    public override string Description => "[Playlist Voting] Stops the Mod";

    protected override Action TriggerEvent => VotingController.Instance.OnVoteStopRequested;
}