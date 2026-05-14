using System;
using PlaylistVoting.Management;

namespace PlaylistVoting.Networking.Chat.Commands;

public class VoteResume : BaseLocalVoteCommand
{
    public override string Prefix => "/";

    public override string Command => "vote resume";

    public override string Description => "[Playlist Voting] Resumes the latest session";

    protected override Action TriggerEvent => VotingController.Instance.OnResumeRequested;
}