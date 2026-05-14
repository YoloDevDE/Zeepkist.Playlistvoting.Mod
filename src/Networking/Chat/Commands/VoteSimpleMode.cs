using System;
using PlaylistVoting.Management;

namespace PlaylistVoting.Networking.Chat.Commands;

public class VoteSimpleMode : BaseLocalVoteCommand
{
    public override string Prefix => "/";

    public override string Command => "vote simplemode";

    public override string Description => "[Playlist Voting] Starts in Simple Mode";

    protected override Action TriggerEvent => VotingController.Instance.OnSimpleModeRequested;
}