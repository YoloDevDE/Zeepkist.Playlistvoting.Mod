using System;
using PlaylistVoting.Management;

namespace PlaylistVoting.Networking.Chat.Commands;

public class VoteStart : BaseLocalVoteCommand
{
    public override string Prefix => "/";

    public override string Command => "vote start";

    public override string Description => "[Playlist Voting] Starts the Mod with an optional session name (e.g. /vote start MySession)";

    protected override Action TriggerEvent => null;

    public override void Handle(string arguments)
    {
        VotingController.Instance.OnVoteStartRequested(arguments?.Trim());
    }
}