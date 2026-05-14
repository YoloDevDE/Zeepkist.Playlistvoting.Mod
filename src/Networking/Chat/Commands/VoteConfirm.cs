using System;
using PlaylistVoting.Management;

namespace PlaylistVoting.Networking.Chat.Commands;

public class VoteConfirm : BaseLocalVoteCommand
{
    public override string Prefix => "/";

    public override string Command => "vote confirm";

    public override string Description => "[Playlist Voting] Confirms an action (e.g. session creation)";

    protected override Action TriggerEvent => VotingController.Instance.OnConfirmRequested;
}